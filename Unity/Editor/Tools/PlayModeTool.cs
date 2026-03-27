using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Windsurf.Unity.MCP
{
    /// <summary>
    /// Tool for controlling Unity Play Mode and inspecting runtime game state.
    /// Enables a complete AI development loop: code → enter play → capture viewport → read logs → fix → repeat.
    /// </summary>
    public class PlayModeTool : McpToolBase
    {
        public override string ToolName => "playmode";
        public override string Description => "Control Play Mode and inspect runtime game state";
        public override string Category => "playmode";

        private static readonly List<LogEntry> capturedLogs = new List<LogEntry>();
        private static int lastLogReadIndex = 0;
        private static bool isLogHandlerRegistered = false;

        [Serializable]
        private class LogEntry
        {
            public string message;
            public string stackTrace;
            public string logType;
            public string timestamp;
        }

        [Serializable]
        private class PlayModeParams
        {
            // Common
            public string action;

            // Enter
            public bool maximizeGameView = false;

            // Step
            public int frames = 1;

            // Inspect
            public string gameObjectName;
            public int instanceId = 0;
            public string[] componentFilter;
            public bool includeChildren = false;

            // SetProperty
            public string componentType;
            public string propertyName;
            public object value;

            // InvokeMethod
            public string methodName;
            public object[] arguments;

            // Console logs
            public string[] logTypes;
            public int limit = 50;
            public bool sinceLastCall = false;
            public string search;

            // Runtime info
            public bool includePerformance = true;
            public bool includePhysics = true;
            public bool includeTime = true;

            // TimeScale
            public float timeScale = 1f;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<PlayModeParams>(parameters);

                EnsureLogHandlerRegistered();

                return args.action switch
                {
                    "getState" => GetPlayModeState(),
                    "enter" => EnterPlayMode(args),
                    "exit" => ExitPlayMode(),
                    "pause" => PausePlayMode(),
                    "resume" => ResumePlayMode(),
                    "step" => StepFrame(args),
                    "inspectGameObject" => InspectGameObject(args),
                    "setProperty" => SetProperty(args),
                    "invokeMethod" => InvokeMethod(args),
                    "getConsoleLogs" => GetConsoleLogs(args),
                    "getRuntimeInfo" => GetRuntimeInfo(args),
                    "setTimeScale" => SetTimeScale(args),
                    _ => CreateErrorResponse($"Unknown playmode action: {args.action}")
                };
            }
            catch (Exception e)
            {
                LogError($"PlayMode tool error: {e.Message}");
                return CreateErrorResponse(e.Message, e.StackTrace);
            }
        }

        private void EnsureLogHandlerRegistered()
        {
            if (!isLogHandlerRegistered)
            {
                Application.logMessageReceived += OnLogMessageReceived;
                isLogHandlerRegistered = true;
            }
        }

        private static void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            capturedLogs.Add(new LogEntry
            {
                message = message,
                stackTrace = stackTrace,
                logType = type.ToString(),
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
            });

            // Cap at 1000 entries to prevent memory issues
            if (capturedLogs.Count > 1000)
            {
                capturedLogs.RemoveAt(0);
                if (lastLogReadIndex > 0) lastLogReadIndex--;
            }
        }

        // ─── Play Mode Control ───────────────────────────────────────────

        private object GetPlayModeState()
        {
            string state;
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                state = "stopped";
            else if (EditorApplication.isPaused)
                state = "paused";
            else
                state = "playing";

            return CreateSuccessResponse(new
            {
                state,
                isPlaying = EditorApplication.isPlaying,
                isPaused = EditorApplication.isPaused,
                isCompiling = EditorApplication.isCompiling,
                timeSinceStartup = EditorApplication.isPlaying ? Time.realtimeSinceStartup : 0f,
                frameCount = EditorApplication.isPlaying ? Time.frameCount : 0
            }, $"Play Mode state: {state}");
        }

        private object EnterPlayMode(PlayModeParams args)
        {
            if (EditorApplication.isPlaying)
            {
                return CreateSuccessResponse(new { state = "already_playing" },
                    "Already in Play Mode");
            }

            if (EditorApplication.isCompiling)
            {
                return CreateErrorResponse("Cannot enter Play Mode while scripts are compiling. Wait for compilation to finish.");
            }

            // Clear logs on fresh play
            capturedLogs.Clear();
            lastLogReadIndex = 0;

            EditorApplication.isPlaying = true;

            LogMessage("Entering Play Mode");

            return CreateSuccessResponse(new
            {
                state = "entering_playmode",
                hint = "Play Mode is starting. Wait a moment, then use scene.capture to see the running game and playmode.getConsoleLogs to check for errors."
            }, "Entering Play Mode — game is starting");
        }

        private object ExitPlayMode()
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return CreateSuccessResponse(new { state = "already_stopped" },
                    "Already in Edit Mode");
            }

            EditorApplication.isPlaying = false;

            LogMessage("Exiting Play Mode");

            return CreateSuccessResponse(new
            {
                state = "exiting_playmode",
                logsCollected = capturedLogs.Count
            }, "Exiting Play Mode — returning to Edit Mode");
        }

        private object PausePlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Use playmode.enter first.");
            }

            if (EditorApplication.isPaused)
            {
                return CreateSuccessResponse(new { state = "already_paused" },
                    "Game is already paused");
            }

            EditorApplication.isPaused = true;
            LogMessage("Game paused");

            return CreateSuccessResponse(new
            {
                state = "paused",
                frameCount = Time.frameCount,
                gameTime = Time.time
            }, "Game paused");
        }

        private object ResumePlayMode()
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Use playmode.enter first.");
            }

            if (!EditorApplication.isPaused)
            {
                return CreateSuccessResponse(new { state = "already_running" },
                    "Game is already running");
            }

            EditorApplication.isPaused = false;
            LogMessage("Game resumed");

            return CreateSuccessResponse(new { state = "playing" }, "Game resumed");
        }

        private object StepFrame(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Use playmode.enter first.");
            }

            // Ensure paused before stepping
            if (!EditorApplication.isPaused)
            {
                EditorApplication.isPaused = true;
            }

            int steps = Mathf.Clamp(args.frames, 1, 100);
            for (int i = 0; i < steps; i++)
            {
                EditorApplication.Step();
            }

            LogMessage($"Stepped {steps} frame(s)");

            return CreateSuccessResponse(new
            {
                state = "paused",
                steppedFrames = steps,
                frameCount = Time.frameCount,
                gameTime = Time.time
            }, $"Advanced {steps} frame(s). Game is paused at frame {Time.frameCount}.");
        }

        // ─── Runtime Inspection ──────────────────────────────────────────

        private object InspectGameObject(PlayModeParams args)
        {
            GameObject go = FindGameObject(args.gameObjectName, args.instanceId);
            if (go == null)
            {
                return CreateErrorResponse(
                    $"GameObject not found: {(args.gameObjectName ?? $"ID:{args.instanceId}")}");
            }

            var result = BuildGameObjectInspection(go, args.componentFilter);

            if (args.includeChildren)
            {
                var children = new List<object>();
                foreach (Transform child in go.transform)
                {
                    children.Add(BuildGameObjectInspection(child.gameObject, args.componentFilter));
                }
                result["children"] = children;
            }

            return CreateSuccessResponse(result,
                $"Inspected '{go.name}' — {go.GetComponents<Component>().Length} components");
        }

        private Dictionary<string, object> BuildGameObjectInspection(GameObject go, string[] componentFilter)
        {
            var info = new Dictionary<string, object>
            {
                ["name"] = go.name,
                ["instanceId"] = go.GetInstanceID(),
                ["active"] = go.activeSelf,
                ["activeInHierarchy"] = go.activeInHierarchy,
                ["tag"] = go.tag,
                ["layer"] = LayerMask.LayerToName(go.layer),
                ["isStatic"] = go.isStatic
            };

            var components = new List<Dictionary<string, object>>();
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue;

                string typeName = component.GetType().Name;

                // Apply filter if specified
                if (componentFilter != null && componentFilter.Length > 0)
                {
                    if (!componentFilter.Any(f =>
                        typeName.Equals(f, StringComparison.OrdinalIgnoreCase)))
                        continue;
                }

                var compData = new Dictionary<string, object>
                {
                    ["type"] = typeName,
                    ["fullType"] = component.GetType().FullName,
                    ["enabled"] = IsComponentEnabled(component)
                };

                // Extract serialized and public fields
                var properties = ExtractComponentProperties(component);
                if (properties.Count > 0)
                {
                    compData["properties"] = properties;
                }

                components.Add(compData);
            }

            info["components"] = components;
            return info;
        }

        private Dictionary<string, object> ExtractComponentProperties(Component component)
        {
            var props = new Dictionary<string, object>();
            var type = component.GetType();

            // Handle well-known Unity types with clean output
            if (component is Transform t)
            {
                props["position"] = Vec3(t.position);
                props["localPosition"] = Vec3(t.localPosition);
                props["rotation"] = Vec3(t.eulerAngles);
                props["localRotation"] = Vec3(t.localEulerAngles);
                props["localScale"] = Vec3(t.localScale);
                props["childCount"] = t.childCount;
                return props;
            }

            if (component is Rigidbody rb)
            {
                props["velocity"] = Vec3(rb.linearVelocity);
                props["angularVelocity"] = Vec3(rb.angularVelocity);
                props["mass"] = rb.mass;
                props["drag"] = rb.linearDamping;
                props["angularDrag"] = rb.angularDamping;
                props["useGravity"] = rb.useGravity;
                props["isKinematic"] = rb.isKinematic;
                props["isSleeping"] = rb.IsSleeping();
                return props;
            }

            if (component is Rigidbody2D rb2d)
            {
                props["velocity"] = Vec2(rb2d.linearVelocity);
                props["angularVelocity"] = rb2d.angularVelocity;
                props["mass"] = rb2d.mass;
                props["gravityScale"] = rb2d.gravityScale;
                props["bodyType"] = rb2d.bodyType.ToString();
                props["isSleeping"] = rb2d.IsSleeping();
                return props;
            }

            if (component is Camera cam)
            {
                props["fieldOfView"] = cam.fieldOfView;
                props["orthographic"] = cam.orthographic;
                props["orthographicSize"] = cam.orthographicSize;
                props["nearClipPlane"] = cam.nearClipPlane;
                props["farClipPlane"] = cam.farClipPlane;
                props["depth"] = cam.depth;
                return props;
            }

            if (component is Animator anim)
            {
                props["isPlaying"] = anim.isActiveAndEnabled;
                props["speed"] = anim.speed;
                if (anim.runtimeAnimatorController != null)
                {
                    props["controller"] = anim.runtimeAnimatorController.name;
                    var currentState = anim.GetCurrentAnimatorStateInfo(0);
                    props["currentStateLength"] = currentState.length;
                    props["currentStateNormalizedTime"] = currentState.normalizedTime;
                    props["isInTransition"] = anim.IsInTransition(0);
                }
                return props;
            }

            // For custom MonoBehaviour scripts: use reflection to read public fields and serialized fields
            if (component is MonoBehaviour)
            {
                // Public fields
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    try
                    {
                        props[field.Name] = SerializeValue(field.GetValue(component));
                    }
                    catch { /* skip unreadable fields */ }
                }

                // Serialized private fields
                foreach (var field in type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (field.GetCustomAttribute<SerializeField>() != null)
                    {
                        try
                        {
                            props[field.Name] = SerializeValue(field.GetValue(component));
                        }
                        catch { /* skip */ }
                    }
                }

                // Public properties with getters (non-inherited to avoid noise)
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (prop.CanRead && prop.GetIndexParameters().Length == 0)
                    {
                        try
                        {
                            props[prop.Name] = SerializeValue(prop.GetValue(component));
                        }
                        catch { /* skip */ }
                    }
                }

                return props;
            }

            // Generic fallback: get public fields only
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                try
                {
                    props[field.Name] = SerializeValue(field.GetValue(component));
                }
                catch { /* skip */ }
            }

            return props;
        }

        private object SerializeValue(object value)
        {
            if (value == null) return null;

            var t = value.GetType();

            if (t.IsPrimitive || t == typeof(string) || t == typeof(decimal))
                return value;

            if (t.IsEnum)
                return value.ToString();

            if (value is Vector2 v2) return Vec2(v2);
            if (value is Vector3 v3) return Vec3(v3);
            if (value is Vector4 v4) return new { x = v4.x, y = v4.y, z = v4.z, w = v4.w };
            if (value is Quaternion q) return new { x = q.x, y = q.y, z = q.z, w = q.w };
            if (value is Color c) return new { r = c.r, g = c.g, b = c.b, a = c.a };
            if (value is Bounds b) return new { center = Vec3(b.center), size = Vec3(b.size) };
            if (value is Rect rc) return new { x = rc.x, y = rc.y, width = rc.width, height = rc.height };

            if (value is UnityEngine.Object uo)
                return uo != null ? $"{uo.GetType().Name}:{uo.name}" : null;

            if (value is System.Collections.IList list)
            {
                var items = new List<object>();
                int max = Math.Min(list.Count, 20); // cap array output
                for (int i = 0; i < max; i++)
                    items.Add(SerializeValue(list[i]));
                if (list.Count > 20)
                    items.Add($"... and {list.Count - 20} more");
                return items;
            }

            return value.ToString();
        }

        // ─── Runtime Mutation ────────────────────────────────────────────

        private object SetProperty(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Runtime property changes only work during Play Mode.");
            }

            GameObject go = FindGameObject(args.gameObjectName, args.instanceId);
            if (go == null)
            {
                return CreateErrorResponse($"GameObject not found: {args.gameObjectName ?? $"ID:{args.instanceId}"}");
            }

            Component component = FindComponent(go, args.componentType);
            if (component == null)
            {
                return CreateErrorResponse($"Component '{args.componentType}' not found on '{go.name}'");
            }

            var type = component.GetType();

            // Try field first, then property
            var field = type.GetField(args.propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (field != null)
            {
                try
                {
                    object converted = ConvertValue(args.value, field.FieldType);
                    field.SetValue(component, converted);
                    LogMessage($"Set {go.name}.{args.componentType}.{args.propertyName} = {converted}");
                    return CreateSuccessResponse(new
                    {
                        gameObject = go.name,
                        component = args.componentType,
                        property = args.propertyName,
                        newValue = SerializeValue(field.GetValue(component))
                    }, $"Set {args.propertyName} successfully");
                }
                catch (Exception e)
                {
                    return CreateErrorResponse($"Failed to set field '{args.propertyName}': {e.Message}");
                }
            }

            var prop = type.GetProperty(args.propertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (prop != null && prop.CanWrite)
            {
                try
                {
                    object converted = ConvertValue(args.value, prop.PropertyType);
                    prop.SetValue(component, converted);
                    LogMessage($"Set {go.name}.{args.componentType}.{args.propertyName} = {converted}");
                    return CreateSuccessResponse(new
                    {
                        gameObject = go.name,
                        component = args.componentType,
                        property = args.propertyName,
                        newValue = SerializeValue(prop.GetValue(component))
                    }, $"Set {args.propertyName} successfully");
                }
                catch (Exception e)
                {
                    return CreateErrorResponse($"Failed to set property '{args.propertyName}': {e.Message}");
                }
            }

            return CreateErrorResponse(
                $"Property/field '{args.propertyName}' not found or not writable on {args.componentType}");
        }

        private object InvokeMethod(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Method invocation only works during Play Mode.");
            }

            GameObject go = FindGameObject(args.gameObjectName, args.instanceId);
            if (go == null)
            {
                return CreateErrorResponse($"GameObject not found: {args.gameObjectName ?? $"ID:{args.instanceId}"}");
            }

            Component component = FindComponent(go, args.componentType);
            if (component == null)
            {
                return CreateErrorResponse($"Component '{args.componentType}' not found on '{go.name}'");
            }

            var type = component.GetType();
            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name == args.methodName && !m.IsSpecialName)
                .ToArray();

            if (methods.Length == 0)
            {
                return CreateErrorResponse($"Public method '{args.methodName}' not found on {args.componentType}");
            }

            // Find best matching overload by parameter count
            var methodArgs = args.arguments ?? Array.Empty<object>();
            var method = methods.FirstOrDefault(m => m.GetParameters().Length == methodArgs.Length)
                         ?? methods[0];

            try
            {
                var parameters = method.GetParameters();
                var convertedArgs = new object[parameters.Length];
                for (int i = 0; i < parameters.Length && i < methodArgs.Length; i++)
                {
                    convertedArgs[i] = ConvertValue(methodArgs[i], parameters[i].ParameterType);
                }

                object result = method.Invoke(component, convertedArgs);

                LogMessage($"Invoked {go.name}.{args.componentType}.{args.methodName}()");

                return CreateSuccessResponse(new
                {
                    gameObject = go.name,
                    component = args.componentType,
                    method = args.methodName,
                    returnValue = result != null ? SerializeValue(result) : null,
                    returnType = method.ReturnType.Name
                }, $"Invoked {args.methodName}() successfully");
            }
            catch (TargetInvocationException tie)
            {
                return CreateErrorResponse(
                    $"Method '{args.methodName}' threw an exception: {tie.InnerException?.Message ?? tie.Message}",
                    tie.InnerException?.StackTrace);
            }
            catch (Exception e)
            {
                return CreateErrorResponse($"Failed to invoke '{args.methodName}': {e.Message}");
            }
        }

        // ─── Console Logs ────────────────────────────────────────────────

        private object GetConsoleLogs(PlayModeParams args)
        {
            var types = args.logTypes ?? new[] { "Log", "Warning", "Error", "Exception" };
            var typeSet = new HashSet<string>(types, StringComparer.OrdinalIgnoreCase);

            int startIndex = args.sinceLastCall ? lastLogReadIndex : 0;
            var filtered = capturedLogs
                .Skip(startIndex)
                .Where(l => typeSet.Contains(l.logType))
                .Where(l => string.IsNullOrEmpty(args.search) ||
                            l.message.IndexOf(args.search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

            int total = filtered.Count;
            var limited = filtered.TakeLast(args.limit).ToList();

            lastLogReadIndex = capturedLogs.Count;

            // Build summary counts
            var counts = limited.GroupBy(l => l.logType)
                .ToDictionary(g => g.Key, g => g.Count());

            return CreateSuccessResponse(new
            {
                logs = limited,
                totalMatching = total,
                returned = limited.Count,
                counts,
                totalCaptured = capturedLogs.Count,
                sinceLastCall = args.sinceLastCall
            }, $"Retrieved {limited.Count} log entries ({counts.GetValueOrDefault("Error", 0)} errors, {counts.GetValueOrDefault("Warning", 0)} warnings)");
        }

        // ─── Runtime Info ────────────────────────────────────────────────

        private object GetRuntimeInfo(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Runtime info is only available during Play Mode.");
            }

            var info = new Dictionary<string, object>
            {
                ["state"] = EditorApplication.isPaused ? "paused" : "playing"
            };

            if (args.includePerformance)
            {
                info["performance"] = new
                {
                    fps = 1f / Time.unscaledDeltaTime,
                    frameTime = Time.unscaledDeltaTime * 1000f, // ms
                    smoothDeltaTime = Time.smoothDeltaTime * 1000f,
                    totalMemoryMB = (float)System.GC.GetTotalMemory(false) / (1024 * 1024),
                    activeGameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Length
                };
            }

            if (args.includePhysics)
            {
                info["physics"] = new
                {
                    gravity = Vec3(Physics.gravity),
                    simulationMode = Physics.simulationMode.ToString(),
                    rigidbodyCount = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Length,
                    rigidbody2DCount = UnityEngine.Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None).Length
                };
            }

            if (args.includeTime)
            {
                info["time"] = new
                {
                    time = Time.time,
                    unscaledTime = Time.unscaledTime,
                    deltaTime = Time.deltaTime,
                    timeScale = Time.timeScale,
                    frameCount = Time.frameCount,
                    realtimeSinceStartup = Time.realtimeSinceStartup,
                    fixedDeltaTime = Time.fixedDeltaTime
                };
            }

            return CreateSuccessResponse(info, "Runtime info retrieved");
        }

        // ─── Time Scale ──────────────────────────────────────────────────

        private object SetTimeScale(PlayModeParams args)
        {
            if (!EditorApplication.isPlaying)
            {
                return CreateErrorResponse("Not in Play Mode. Time scale can only be changed during Play Mode.");
            }

            float scale = Mathf.Clamp(args.timeScale, 0f, 100f);
            float previous = Time.timeScale;
            Time.timeScale = scale;

            LogMessage($"Time scale changed: {previous} → {scale}");

            return CreateSuccessResponse(new
            {
                previousTimeScale = previous,
                newTimeScale = scale
            }, $"Time scale set to {scale}x");
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        private GameObject FindGameObject(string name, int instanceId)
        {
            if (instanceId != 0)
            {
                return EditorUtility.InstanceIDToObject(instanceId) as GameObject;
            }

            if (!string.IsNullOrEmpty(name))
            {
                // First try exact path
                var go = GameObject.Find(name);
                if (go != null) return go;

                // Then search all objects including inactive
                foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (obj.name == name && obj.scene.isLoaded)
                        return obj;
                }
            }

            return null;
        }

        private Component FindComponent(GameObject go, string componentType)
        {
            if (string.IsNullOrEmpty(componentType)) return null;

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                if (comp.GetType().Name.Equals(componentType, StringComparison.OrdinalIgnoreCase) ||
                    comp.GetType().FullName.Equals(componentType, StringComparison.OrdinalIgnoreCase))
                    return comp;
            }

            return null;
        }

        private bool IsComponentEnabled(Component component)
        {
            if (component is Behaviour b) return b.enabled;
            if (component is Renderer r) return r.enabled;
            if (component is Collider c) return c.enabled;
            return true;
        }

        private object ConvertValue(object value, Type targetType)
        {
            if (value == null) return null;

            // Handle JObject/JToken from JSON deserialization
            if (value is JObject jObj)
            {
                if (targetType == typeof(Vector2))
                    return new Vector2(jObj.Value<float>("x"), jObj.Value<float>("y"));
                if (targetType == typeof(Vector3))
                    return new Vector3(jObj.Value<float>("x"), jObj.Value<float>("y"), jObj.Value<float>("z"));
                if (targetType == typeof(Vector4))
                    return new Vector4(jObj.Value<float>("x"), jObj.Value<float>("y"), jObj.Value<float>("z"), jObj.Value<float>("w"));
                if (targetType == typeof(Color))
                    return new Color(jObj.Value<float>("r"), jObj.Value<float>("g"), jObj.Value<float>("b"), jObj.Value<float>("a"));
                if (targetType == typeof(Quaternion))
                    return new Quaternion(jObj.Value<float>("x"), jObj.Value<float>("y"), jObj.Value<float>("z"), jObj.Value<float>("w"));

                return JsonConvert.DeserializeObject(jObj.ToString(), targetType);
            }

            if (value is JToken jToken)
            {
                return jToken.ToObject(targetType);
            }

            if (targetType.IsEnum && value is string s)
                return Enum.Parse(targetType, s, true);

            return Convert.ChangeType(value, targetType);
        }

        private static object Vec2(Vector2 v) => new { x = v.x, y = v.y };
        private static object Vec3(Vector3 v) => new { x = v.x, y = v.y, z = v.z };
    }
}
