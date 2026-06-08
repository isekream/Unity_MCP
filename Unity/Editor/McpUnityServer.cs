using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using System.IO;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Main MCP server for Unity Editor that handles HTTP communication
    /// and coordinates tool execution between AI IDEs and Unity.
    /// </summary>
    [InitializeOnLoad]
    public class McpUnityServer : EditorWindow
    {
        private const string MENU_PATH = "Tools/Unity MCP/Server Window";
        private const string PREF_SERVER_PORT = "UnityMCP_ServerPort";
        private const string PREF_AUTO_START = "UnityMCP_AutoStart";
        private const string PREF_REQUEST_TIMEOUT = "UnityMCP_RequestTimeout";
        // SessionState survives domain reloads (script recompile / Play Mode with Reload
        // Domain enabled) but resets when Unity restarts. We use it to restart the listener
        // after a reload without cold-starting on every fresh Unity launch.
        private const string SESSION_WAS_RUNNING = "UnityMCP_WasRunning";

        private static McpUnityServer instance;
        private static HttpListener httpListener;
        private static bool isServerRunning = false;
        private static int serverPort = 8090;
        private static int requestTimeout = 10;
        private static bool autoStart = false;
        private static Thread listenerThread;
        private static readonly ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
        private static int mainThreadId;
        private static bool mainThreadPumpScheduled;
        
        private Vector2 scrollPosition;
        private string logText = "";
        private readonly List<string> logs = new List<string>();
        private static readonly Dictionary<string, McpToolBase> tools = new Dictionary<string, McpToolBase>();
        private static bool toolsInitialized = false;

        static McpUnityServer()
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Single persistent main-thread pump. Tool execution is dispatched here
            // (via RunOnMainThread) instead of EditorApplication.delayCall, which is a
            // non-thread-safe static delegate and drops callbacks when subscribed from
            // the background listener thread under concurrency.
            EditorApplication.update += PumpMainThreadActions;

            // A domain reload wipes these statics and kills the listener thread. Persist
            // the running state just before the reload so we can restart afterwards.
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;

            // IMPORTANT: Never call LoadPreferences() (or any EditorPrefs.*) directly
            // from the static constructor of an EditorWindow/ScriptableObject-derived type.
            // Unity forbids it and throws "GetInt is not allowed to be called from a
            // ScriptableObject constructor". We defer it.
            EditorApplication.delayCall += () =>
            {
                LoadPreferences();

                // Restart if the server was running before a domain reload, or if the
                // user opted into auto-start for fresh Unity sessions.
                if (SessionState.GetBool(SESSION_WAS_RUNNING, false) || autoStart)
                {
                    StartServer();
                }
            };
        }

        private static void OnBeforeAssemblyReload()
        {
            // Capture running state for the post-reload restart, then release the HTTP
            // port directly so the new domain can re-bind it. We bypass StopServer() here
            // because that clears the persisted "was running" flag (user-stop semantics).
            SessionState.SetBool(SESSION_WAS_RUNNING, isServerRunning);
            ShutdownListener();
        }

        [MenuItem(MENU_PATH, false, 1)]
        public static void OpenWindow()
        {
            instance = GetWindow<McpUnityServer>("Unity MCP Server");
            instance.minSize = new Vector2(400, 300);
            instance.Show();
        }

        private void OnEnable()
        {
            instance = this;
            LoadPreferences();
            EnsureToolsInitialized();
            RefreshUI();
        }

        private void OnDisable()
        {
            SavePreferences();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            
            // Header
            EditorGUILayout.LabelField("Unity MCP Server", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Server Configuration
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Server Configuration", EditorStyles.boldLabel);
            
            int newPort = EditorGUILayout.IntField("HTTP Port", serverPort);
            if (newPort != serverPort)
            {
                serverPort = newPort;
                SavePreferences();
            }
            
            int newTimeout = EditorGUILayout.IntField("Request Timeout (seconds)", requestTimeout);
            if (newTimeout != requestTimeout)
            {
                requestTimeout = newTimeout;
                SavePreferences();
            }
            
            bool newAutoStart = EditorGUILayout.Toggle("Auto Start Server", autoStart);
            if (newAutoStart != autoStart)
            {
                autoStart = newAutoStart;
                SavePreferences();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Server Status
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Server Status", EditorStyles.boldLabel);
            
            string status = isServerRunning ? "Running" : "Stopped";
            Color statusColor = isServerRunning ? Color.green : Color.red;
            
            GUI.color = statusColor;
            EditorGUILayout.LabelField($"Status: {status}");
            GUI.color = Color.white;
            
            if (isServerRunning)
            {
                EditorGUILayout.LabelField($"HTTP URL: http://localhost:{serverPort}");
                EditorGUILayout.LabelField($"Ready for MCP connections");
            }
            
            EditorGUILayout.Space();
            
            // Server Controls
            EditorGUILayout.BeginHorizontal();
            
            GUI.enabled = !isServerRunning;
            if (GUILayout.Button("Start Server"))
            {
                StartServer();
            }
            
            GUI.enabled = isServerRunning;
            if (GUILayout.Button("Stop Server"))
            {
                StopServer();
            }
            
            GUI.enabled = true;
            if (GUILayout.Button("Restart Server"))
            {
                StopServer();
                EditorApplication.delayCall += () => StartServer();
            }
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Available Tools
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Available Tools", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Registered Tools: {tools.Count}");
            
            if (tools.Count > 0)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(100));
                foreach (var tool in tools.Values)
                {
                    EditorGUILayout.LabelField($"• {tool.ToolName} - {tool.Description}");
                }
                EditorGUILayout.EndScrollView();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Configuration Export
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("MCP Configuration", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Copy MCP Server Configuration to Clipboard"))
            {
                CopyMcpConfigToClipboard();
            }
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space();
            
            // Logs
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Server Logs", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(150));
            EditorGUILayout.TextArea(logText, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Logs"))
            {
                ClearLogs();
            }
            if (GUILayout.Button("Export Logs"))
            {
                ExportLogs();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        public static void StartServer()
        {
            if (isServerRunning)
            {
                LogMessage("Server is already running.");
                return;
            }

            try
            {
                // Ensure the tool registry is populated before any request can arrive.
                // The registry is static and independent of the EditorWindow, so the
                // server functions even when the window is closed.
                EnsureToolsInitialized();

                httpListener = new HttpListener();
                httpListener.Prefixes.Add($"http://localhost:{serverPort}/");
                httpListener.Start();

                isServerRunning = true;
                SessionState.SetBool(SESSION_WAS_RUNNING, true);
                LogMessage($"MCP Server started on port {serverPort}");

                EnsureMainThreadPump();

                // Start listening thread
                listenerThread = new Thread(HandleRequests);
                listenerThread.Start();
                
                // Set environment variable for Node.js server
                Environment.SetEnvironmentVariable("UNITY_PORT", serverPort.ToString());
                Environment.SetEnvironmentVariable("UNITY_REQUEST_TIMEOUT", requestTimeout.ToString());
                
                RefreshUI();
            }
            catch (Exception e)
            {
                LogError($"Failed to start server: {e.Message}");
            }
        }

        public static void StopServer()
        {
            if (!isServerRunning)
            {
                LogMessage("Server is not running.");
                return;
            }

            try
            {
                ShutdownListener();
                // User-initiated stop: do not auto-restart after a later domain reload.
                SessionState.SetBool(SESSION_WAS_RUNNING, false);

                LogMessage("MCP Server stopped");
                RefreshUI();
            }
            catch (Exception e)
            {
                LogError($"Error stopping server: {e.Message}");
            }
        }

        private static void ShutdownListener()
        {
            isServerRunning = false;
            mainThreadPumpScheduled = false;
            try { httpListener?.Stop(); }
            catch (Exception e) { LogError($"Error stopping HTTP listener: {e.Message}"); }
            listenerThread?.Join(1000);
            httpListener = null;
        }

        private static void HandleRequests()
        {
            while (isServerRunning && httpListener != null)
            {
                try
                {
                    var context = httpListener.GetContext();
                    // Service each request on a worker thread so the listener can immediately
                    // accept the next connection. ExecuteTool blocks its worker while waiting
                    // for the main-thread pump; that must not stall the accept loop.
                    ThreadPool.QueueUserWorkItem(_ => ProcessRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Expected when stopping the server
                    break;
                }
                catch (Exception e)
                {
                    LogError($"Error handling request: {e.Message}");
                }
            }
        }

        private static void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                string requestBody;
                using (var reader = new StreamReader(context.Request.InputStream))
                {
                    requestBody = reader.ReadToEnd();
                }

                var request = JsonConvert.DeserializeObject<McpRequest>(requestBody);
                var response = ProcessMcpRequest(request);
                var responseJson = JsonConvert.SerializeObject(response);

                byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
                context.Response.ContentLength64 = responseBytes.Length;
                context.Response.ContentType = "application/json";
                context.Response.OutputStream.Write(responseBytes, 0, responseBytes.Length);
                context.Response.Close();
            }
            catch (Exception e)
            {
                LogError($"Error processing request: {e.Message}");
                
                var errorResponse = McpResponse.CreateError(e.Message);
                var errorJson = JsonConvert.SerializeObject(errorResponse);
                byte[] errorBytes = Encoding.UTF8.GetBytes(errorJson);
                
                context.Response.StatusCode = 500;
                context.Response.ContentLength64 = errorBytes.Length;
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                context.Response.Close();
            }
        }

        private static void EnsureToolsInitialized()
        {
            if (toolsInitialized)
            {
                return;
            }

            tools.Clear();

            // Register all available tools. Static registry is independent of the
            // EditorWindow so requests succeed even when the window is closed.
            RegisterTool(new ProjectAnalyzerTool());
            RegisterTool(new SceneManipulationTool());
            RegisterTool(new AssetManagerTool());
            RegisterTool(new CodeGenerationTool());
            RegisterTool(new BuildManagerTool());
            RegisterTool(new ViewportCaptureTool());
            RegisterTool(new PlayModeTool());
            RegisterTool(new ProjectMemoryTool());
            RegisterTool(new LabTool());

            toolsInitialized = true;
            LogMessage($"Initialized {tools.Count} MCP tools");
        }

        private static void RegisterTool(McpToolBase tool)
        {
            if (tool != null && !string.IsNullOrEmpty(tool.ToolName))
            {
                tools[tool.ToolName] = tool;
            }
        }

        /// <summary>
        /// Enqueues an action to run on the Unity main thread. Thread-safe; drained by
        /// PumpMainThreadActions on EditorApplication.update.
        /// </summary>
        public static void RunOnMainThread(Action action)
        {
            if (action == null) return;

            if (Thread.CurrentThread.ManagedThreadId == mainThreadId)
            {
                try { action(); }
                catch (Exception e) { LogError($"Main-thread action failed: {e.Message}"); }
                return;
            }

            mainThreadActions.Enqueue(action);
            EnsureMainThreadPump();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void EnsureMainThreadPump()
        {
            if (mainThreadPumpScheduled) return;
            mainThreadPumpScheduled = true;
            EditorApplication.delayCall += MainThreadPumpTick;
        }

        private static void MainThreadPumpTick()
        {
            PumpMainThreadActions();
            if (isServerRunning)
            {
                EditorApplication.delayCall += MainThreadPumpTick;
            }
            else
            {
                mainThreadPumpScheduled = false;
            }
        }

        private static void PumpMainThreadActions()
        {
            while (mainThreadActions.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    LogError($"Main-thread action failed: {e.Message}");
                }
            }
        }

        private class ToolExecutionState
        {
            public volatile bool IsComplete;
            public McpResponse Result;
            public Exception Error;
            public volatile AsyncToolResult Async;
        }

        public static McpResponse ExecuteTool(string toolName, object parameters)
        {
            EnsureToolsInitialized();

            if (!tools.ContainsKey(toolName))
            {
                return McpResponse.CreateError($"Tool '{toolName}' not found");
            }

            try
            {
                var tool = tools[toolName];
                var state = new ToolExecutionState();

                // Dispatch tool execution to the main thread via the persistent pump.
                RunOnMainThread(() =>
                {
                    try
                    {
                        var execResult = tool.Execute(parameters);

                        if (execResult is AsyncToolResult async)
                        {
                            state.Async = async;
                        }
                        else
                        {
                            state.Result = McpResponse.CreateSuccess(execResult);
                            LogMessage($"Executed tool: {toolName}");
                            state.IsComplete = true;
                        }
                    }
                    catch (Exception e)
                    {
                        LogError($"Error executing tool '{toolName}': {e.Message}");
                        state.Error = e;
                        state.IsComplete = true;
                    }
                });

                // Wait for completion (with timeout) on this worker thread.
                var startTime = DateTime.Now;
                var defaultTimeout = TimeSpan.FromSeconds(requestTimeout);

                while (!state.IsComplete && DateTime.Now - startTime < defaultTimeout)
                {
                    var asyncOp = state.Async;
                    if (asyncOp != null)
                    {
                        var asyncTimeout = TimeSpan.FromSeconds(asyncOp.TimeoutSeconds > 0
                            ? asyncOp.TimeoutSeconds
                            : requestTimeout);
                        var asyncStart = DateTime.Now;

                        while (!asyncOp.IsComplete && DateTime.Now - asyncStart < asyncTimeout)
                        {
                            Thread.Sleep(10);
                        }

                        if (asyncOp.IsComplete)
                        {
                            state.Result = asyncOp.Error != null
                                ? McpResponse.CreateError(asyncOp.Error)
                                : McpResponse.CreateSuccess(asyncOp.Result);
                            LogMessage($"Executed async tool: {toolName}");
                        }
                        else
                        {
                            state.Result = McpResponse.CreateError(
                                $"Async tool '{toolName}' timed out after {asyncTimeout.TotalSeconds}s");
                        }

                        state.IsComplete = true;
                        break;
                    }

                    EditorApplication.QueuePlayerLoopUpdate();
                    Thread.Sleep(10);
                }

                if (!state.IsComplete)
                {
                    return McpResponse.CreateError($"Tool '{toolName}' execution timed out after {requestTimeout} seconds");
                }

                if (state.Error != null)
                {
                    return McpResponse.CreateError(state.Error.Message);
                }

                return state.Result;
            }
            catch (Exception e)
            {
                LogError($"Error executing tool '{toolName}': {e.Message}");
                return McpResponse.CreateError(e.Message);
            }
        }

        private static McpResponse ProcessMcpRequest(McpRequest request)
        {
            if (request?.Method == null)
            {
                return McpResponse.CreateError("Invalid request format");
            }

            EnsureToolsInitialized();

            var method = request.Method.Trim();

            // Connection health check used by the Node MCP client on startup.
            if (method.Equals("test", StringComparison.OrdinalIgnoreCase) ||
                method.Equals("test.ping", StringComparison.OrdinalIgnoreCase))
            {
                return McpResponse.CreateSuccess(new
                {
                    status = "ok",
                    tools = tools.Count,
                    port = serverPort
                });
            }

            // Direct tool invocation without a dot (e.g. "lab", "project_memory").
            if (!method.Contains("."))
            {
                if (tools.ContainsKey(method))
                {
                    return ExecuteTool(method, request.Params);
                }

                return McpResponse.CreateError($"No tool found for method: {method}");
            }

            var methodParts = method.Split('.');
            if (methodParts.Length < 2)
            {
                return McpResponse.CreateError($"Invalid method format: {method}");
            }

            string rawCategory = methodParts[0];
            string toolCategory = NormalizeCategory(rawCategory);
            string toolAction = methodParts[1];
            string exactToolName = $"{toolCategory}_{toolAction}";
            string exactToolNameAlt = $"{rawCategory}_{toolAction}";

            // Try the un-normalized category match first (e.g., "assets_create").
            if (tools.ContainsKey(exactToolNameAlt))
            {
                return ExecuteTool(exactToolNameAlt, request.Params);
            }

            // Then the normalized exact match (e.g., "scene_capture").
            if (tools.ContainsKey(exactToolName))
            {
                return ExecuteTool(exactToolName, request.Params);
            }

            // Fall back to category-level tool and inject action into params.
            // This routes "playmode.enter" → tool "playmode" with action="enter",
            // and "scene.createGameObject" → tool "scene_manipulate" with action injected.
            foreach (var kvp in tools)
            {
                if (kvp.Value.Category == toolCategory)
                {
                    var paramsWithAction = InjectAction(request.Params, NormalizeAction(toolAction));
                    return ExecuteTool(kvp.Key, paramsWithAction);
                }
            }

            return McpResponse.CreateError($"No tool found for method: {request.Method}");
        }

        private static string NormalizeCategory(string category)
        {
            switch (category)
            {
                case "assets":
                case "packages":
                    return "asset";
                default:
                    return category;
            }
        }

        private static string NormalizeAction(string action)
        {
            if (string.IsNullOrEmpty(action))
            {
                return action;
            }

            switch (action)
            {
                case "createGameObject": return "create_object";
                case "createPrimitive": return "create_primitive";
                case "deleteGameObject": return "delete_object";
                case "moveGameObject": return "set_transform";
                case "createScript": return "create_script";
                case "analyze": return "analyze";
                case "attachScript": return "attach_script";
                case "managePrefabs": return "manage_prefabs";
                case "createMaterial": return "create_material";
                case "createTexture": return "create_texture";
                case "getInfo": return "get_info";
                case "runTests": return "run_tests";
                case "getReport": return "get_report";
                case "getConsoleLogs": return "get_console_logs";
                default:
                    return CamelCaseToSnakeCase(action);
            }
        }

        private static string CamelCaseToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            var builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsUpper(c) && i > 0)
                {
                    builder.Append('_');
                }
                builder.Append(char.ToLowerInvariant(c));
            }
            return builder.ToString();
        }

        private static object InjectAction(object parameters, string action)
        {
            try
            {
                // Convert params to a mutable dictionary and add the action
                string json = parameters != null
                    ? JsonConvert.SerializeObject(parameters)
                    : "{}";
                var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(json)
                           ?? new Dictionary<string, object>();
                if (!dict.ContainsKey("action"))
                {
                    dict["action"] = action;
                }
                return dict;
            }
            catch
            {
                return parameters;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Handle Unity play mode changes
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                LogMessage("Unity entering Play Mode");
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                LogMessage("Unity returned to Edit Mode");
            }
        }

        private static void LoadPreferences()
        {
            serverPort = EditorPrefs.GetInt(PREF_SERVER_PORT, 8090);
            autoStart = EditorPrefs.GetBool(PREF_AUTO_START, false);
            requestTimeout = EditorPrefs.GetInt(PREF_REQUEST_TIMEOUT, 10);
        }

        private static void SavePreferences()
        {
            EditorPrefs.SetInt(PREF_SERVER_PORT, serverPort);
            EditorPrefs.SetBool(PREF_AUTO_START, autoStart);
            EditorPrefs.SetInt(PREF_REQUEST_TIMEOUT, requestTimeout);
        }

        private void CopyMcpConfigToClipboard()
        {
            var config = new
            {
                mcpServers = new
                {
                    unityMcp = new
                    {
                        command = "node",
                        args = new[] { "/absolute/path/to/UnityMCP/Server/build/index.js" },
                        env = new
                        {
                            UNITY_PORT = serverPort.ToString(),
                            REQUEST_TIMEOUT = requestTimeout.ToString()
                        }
                    }
                }
            };

            string jsonConfig = JsonConvert.SerializeObject(config, Formatting.Indented);
            EditorGUIUtility.systemCopyBuffer = jsonConfig;
            
            LogMessage("MCP server configuration copied to clipboard");
            ShowNotification(new GUIContent("Configuration copied to clipboard!"));
        }

        public static void LogMessage(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            
            if (instance != null)
            {
                instance.logs.Add(logEntry);
                instance.logText = string.Join("\n", instance.logs);
                
                if (instance.logs.Count > 100) // Keep last 100 logs
                {
                    instance.logs.RemoveAt(0);
                }
            }
            
            Debug.Log($"[Unity MCP] {message}");
        }

        public static void LogError(string message)
        {
            string logEntry = $"[{DateTime.Now:HH:mm:ss}] ERROR: {message}";
            
            if (instance != null)
            {
                instance.logs.Add(logEntry);
                instance.logText = string.Join("\n", instance.logs);
            }
            
            Debug.LogError($"[Unity MCP] {message}");
        }

        private void ClearLogs()
        {
            logs.Clear();
            logText = "";
        }

        private void ExportLogs()
        {
            string path = EditorUtility.SaveFilePanel("Export Logs", "", "unity_mcp_logs.txt", "txt");
            if (!string.IsNullOrEmpty(path))
            {
                System.IO.File.WriteAllText(path, logText);
                LogMessage($"Logs exported to: {path}");
            }
        }

        private static void RefreshUI()
        {
            if (instance != null)
            {
                instance.Repaint();
            }
        }
    }

    /// <summary>
    /// Returned by tools that need multiple frames to complete (e.g., runtime observation).
    /// ExecuteTool polls IsComplete on the background thread while EditorApplication.update
    /// drives progress on the main thread.
    /// </summary>
    public class AsyncToolResult
    {
        public volatile bool IsComplete;
        public object Result;
        public string Error;
        public float TimeoutSeconds;
    }

    /// <summary>
    /// MCP request structure
    /// </summary>
    [Serializable]
    public class McpRequest
    {
        [Newtonsoft.Json.JsonProperty("id")]
        public string Id { get; set; }
        [Newtonsoft.Json.JsonProperty("type")]
        public string Type { get; set; }
        [Newtonsoft.Json.JsonProperty("method")]
        public string Method { get; set; }
        [Newtonsoft.Json.JsonProperty("params")]
        public object Params { get; set; }
    }

    /// <summary>
    /// MCP response structure
    /// </summary>
    [Serializable]
    public class McpResponse
    {
        [Newtonsoft.Json.JsonProperty("id")]
        public string Id { get; set; }
        [Newtonsoft.Json.JsonProperty("type")]
        public string Type { get; set; } = "response";
        [Newtonsoft.Json.JsonProperty("result")]
        public object Result { get; set; }
        [Newtonsoft.Json.JsonProperty("error")]
        public McpErrorInfo Error { get; set; }

        public static McpResponse CreateSuccess(object result)
        {
            return new McpResponse { Result = result };
        }

        public static McpResponse CreateError(string message)
        {
            return new McpResponse 
            { 
                Error = new McpErrorInfo { Message = message } 
            };
        }
    }

    /// <summary>
    /// MCP error structure
    /// </summary>
    [Serializable]
    public class McpErrorInfo
    {
        [Newtonsoft.Json.JsonProperty("code")]
        public int Code { get; set; }
        [Newtonsoft.Json.JsonProperty("message")]
        public string Message { get; set; }
        [Newtonsoft.Json.JsonProperty("data")]
        public object Data { get; set; }
    }
} 