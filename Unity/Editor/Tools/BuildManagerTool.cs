using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for build management operations
    /// </summary>
    public class BuildManagerTool : McpToolBase
    {
        public override string ToolName => "build_manage";
        public override string Description => "Manage Unity builds and build settings";
        public override string Category => "build";

        private static BuildReport lastBuildReport;
        private static readonly List<ConsoleLogEntry> capturedLogs = new List<ConsoleLogEntry>();

        [Serializable]
        private class ConsoleLogEntry
        {
            public string type;
            public string message;
            public string timestamp;
        }

        [Serializable]
        public class BuildParameters
        {
            public string action = "execute";
            public string target = "StandaloneWindows64";
            public string buildPath = "";
            public string outputPath = "";
            public bool developmentBuild = false;
            public bool development = false;
            public bool scriptDebugging = false;
            public bool connectProfiler = false;
            public bool allowDebugging = false;
            public bool clean = false;
            public bool confirmClean = false;
            public string[] scenes = null;
            public string cleanType = "all";
            public string[] logTypes;
            public int limit = 100;
            public bool clearAfterRetrieve = false;
        }

        static BuildManagerTool()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            capturedLogs.Add(new ConsoleLogEntry
            {
                type = type.ToString(),
                message = message,
                timestamp = DateTime.UtcNow.ToString("o")
            });
            if (capturedLogs.Count > 2000) capturedLogs.RemoveAt(0);
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<BuildParameters>(parameters);
                var action = McpEditorHelpers.GetAction(parameters, args.action ?? "execute");

                return action switch
                {
                    "configure" => ConfigureBuild(args),
                    "execute" => ExecuteBuild(args),
                    "run_tests" => RunTests(args),
                    "get_report" => GetBuildReport(args),
                    "clean" => CleanBuild(args),
                    "addressables" => AddressablesBuild(args),
                    "optimize" => OptimizeBuild(args),
                    "get_console_logs" => GetConsoleLogs(args),
                    "profile" => ProfileBuild(args),
                    _ => CreateErrorResponse(
                        $"Unknown build action '{action}'. Supported: configure, execute, run_tests, get_report, clean, addressables, optimize, get_console_logs, profile")
                };
            }
            catch (Exception e)
            {
                LogError($"Build operation failed: {e.Message}");
                return CreateErrorResponse($"Build operation failed: {e.Message}", e.StackTrace);
            }
        }

        private object ConfigureBuild(BuildParameters args)
        {
            if (!string.IsNullOrWhiteSpace(args.target))
            {
                if (!Enum.TryParse<BuildTarget>(args.target, out var buildTarget))
                    return CreateErrorResponse($"Invalid build target: {args.target}");

                var group = BuildPipeline.GetBuildTargetGroup(buildTarget);
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, buildTarget);
            }

            if (args.scenes != null && args.scenes.Length > 0)
            {
                var sceneList = new List<EditorBuildSettingsScene>();
                for (int i = 0; i < args.scenes.Length; i++)
                {
                    var path = McpEditorHelpers.NormalizeAssetPath(args.scenes[i]);
                    if (path != null)
                        sceneList.Add(new EditorBuildSettingsScene(path, true));
                }
                EditorBuildSettings.scenes = sceneList.ToArray();
            }

            EditorUserBuildSettings.development = args.development || args.developmentBuild;
            EditorUserBuildSettings.allowDebugging = args.scriptDebugging || args.allowDebugging;
            EditorUserBuildSettings.connectProfiler = args.connectProfiler;

            return CreateSuccessResponse(new
            {
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                sceneCount = EditorBuildSettings.scenes.Length,
                development = EditorUserBuildSettings.development
            }, "Build configured");
        }

        private object ExecuteBuild(BuildParameters args)
        {
            if (!CanExecute())
                return CreateErrorResponse("Cannot build while in Play Mode or during compilation.");

            var targetStr = args.target ?? EditorUserBuildSettings.activeBuildTarget.ToString();
            if (!Enum.TryParse<BuildTarget>(targetStr, out var buildTarget))
                return CreateErrorResponse($"Invalid build target: {targetStr}");

            var buildPath = !string.IsNullOrWhiteSpace(args.outputPath)
                ? args.outputPath
                : (!string.IsNullOrWhiteSpace(args.buildPath) ? args.buildPath : GetDefaultBuildPath(buildTarget));

            var buildDir = Path.GetDirectoryName(buildPath);
            if (!string.IsNullOrEmpty(buildDir) && !Directory.Exists(buildDir))
                Directory.CreateDirectory(buildDir);

            var buildOptions = BuildOptions.None;
            if (args.developmentBuild || args.development) buildOptions |= BuildOptions.Development;
            if (args.scriptDebugging) buildOptions |= BuildOptions.AllowDebugging;
            if (args.connectProfiler) buildOptions |= BuildOptions.ConnectWithProfiler;

            var scenesToBuild = args.scenes ?? GetScenesInBuildSettings();

            LogMessage($"Starting build for {buildTarget} at {buildPath}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenesToBuild,
                locationPathName = buildPath,
                target = buildTarget,
                options = buildOptions
            });

            lastBuildReport = report;

            var buildResult = new
            {
                success = report.summary.result == BuildResult.Succeeded,
                result = report.summary.result.ToString(),
                totalTime = report.summary.totalTime.TotalSeconds,
                totalSize = report.summary.totalSize,
                buildPath,
                platform = buildTarget.ToString(),
                warnings = report.summary.totalWarnings,
                errors = report.summary.totalErrors,
                steps = GetBuildSteps(report)
            };

            return report.summary.result == BuildResult.Succeeded
                ? CreateSuccessResponse(buildResult, "Build completed successfully")
                : CreateErrorResponse($"Build failed: {report.summary.result}", buildResult);
        }

        private object RunTests(BuildParameters args)
        {
            var testRunnerType = Type.GetType("UnityEditor.TestTools.TestRunner.Api.TestRunnerApi, UnityEditor.TestRunner");
            if (testRunnerType == null)
            {
                return CreateErrorResponse(
                    "Unity Test Framework is not installed. Add com.unity.test-framework to Packages/manifest.json.");
            }

            return CreateSuccessResponse(new
            {
                status = "queued",
                note = "Test execution requires the Unity Test Framework package. " +
                       "Install com.unity.test-framework and run tests via Window > General > Test Runner, " +
                       "or use build.runTests after installing the package."
            }, "Test runner API available — run tests via Test Runner window");
        }

        private object GetBuildReport(BuildParameters args)
        {
            if (lastBuildReport == null)
                return CreateErrorResponse("No build report available. Run build.execute first.");

            var summary = lastBuildReport.summary;
            var data = new Dictionary<string, object>
            {
                ["result"] = summary.result.ToString(),
                ["totalTime"] = summary.totalTime.TotalSeconds,
                ["totalSize"] = summary.totalSize,
                ["totalWarnings"] = summary.totalWarnings,
                ["totalErrors"] = summary.totalErrors,
                ["platform"] = summary.platform.ToString(),
                ["outputPath"] = summary.outputPath
            };

            return CreateSuccessResponse(data, "Build report retrieved");
        }

        private object CleanBuild(BuildParameters args)
        {
            if (!args.confirmClean)
            {
                return CreateErrorResponse(
                    "Set confirmClean=true to confirm cleanup. This can delete Library, Temp, or build artifacts.");
            }

            var projectRoot = Application.dataPath.Replace("/Assets", "").Replace("\\Assets", "");
            var cleaned = new List<string>();
            var cleanType = (args.cleanType ?? "all").Trim().ToLowerInvariant();

            void TryDeleteDir(string relativePath)
            {
                var full = Path.Combine(projectRoot, relativePath);
                if (Directory.Exists(full))
                {
                    try
                    {
                        Directory.Delete(full, true);
                        cleaned.Add(relativePath);
                    }
                    catch (Exception e)
                    {
                        LogError($"Could not delete {relativePath}: {e.Message}");
                    }
                }
            }

            switch (cleanType)
            {
                case "temp":
                    TryDeleteDir("Temp");
                    break;
                case "builds":
                    TryDeleteDir("Builds");
                    break;
                case "logs":
                    TryDeleteDir("Logs");
                    break;
                case "library":
                    return CreateErrorResponse(
                        "Cleaning Library requires closing Unity. Delete the Library folder manually while Unity is closed.");
                case "all":
                    TryDeleteDir("Temp");
                    TryDeleteDir("Builds");
                    TryDeleteDir("Logs");
                    break;
            }

            return CreateSuccessResponse(new { cleanType, cleaned }, $"Cleaned {cleaned.Count} location(s)");
        }

        private object AddressablesBuild(BuildParameters args)
        {
            var addrType = Type.GetType("UnityEditor.AddressableAssets.Settings.AddressableAssetSettings, Unity.Addressables.Editor");
            if (addrType == null)
            {
                return CreateErrorResponse(
                    "Addressables package is not installed. Add com.unity.addressables to Packages/manifest.json.");
            }

            return CreateSuccessResponse(new
            {
                note = "Addressables package is installed. Use Window > Asset Management > Addressables to build content."
            }, "Addressables package detected");
        }

        private object OptimizeBuild(BuildParameters args)
        {
            var suggestions = new List<string>
            {
                "Enable IL2CPP for release mobile builds to improve performance.",
                "Use asset bundles or Addressables to reduce initial build size.",
                "Review texture import settings — set max size appropriately per platform.",
                "Disable Development Build for release builds.",
                "Strip unused mesh components and disable Read/Write on textures not used at runtime."
            };

            return CreateSuccessResponse(new
            {
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                suggestions,
                textureCount = AssetDatabase.FindAssets("t:Texture").Length,
                meshCount = AssetDatabase.FindAssets("t:Mesh").Length
            }, "Build optimization suggestions generated");
        }

        private object GetConsoleLogs(BuildParameters args)
        {
            var types = args.logTypes ?? new[] { "Log", "Warning", "Error" };
            var typeSet = new HashSet<string>(types, StringComparer.OrdinalIgnoreCase);

            var logs = capturedLogs
                .Where(l => typeSet.Contains(l.type))
                .TakeLast(Mathf.Clamp(args.limit, 1, 500))
                .ToList();

            if (args.clearAfterRetrieve)
                capturedLogs.Clear();

            return CreateSuccessResponse(new
            {
                logs,
                totalCaptured = capturedLogs.Count,
                returned = logs.Count
            }, $"Retrieved {logs.Count} log entries");
        }

        private object ProfileBuild(BuildParameters args)
        {
            return CreateSuccessResponse(new
            {
                editorMetrics = new
                {
                    isCompiling = EditorApplication.isCompiling,
                    isPlaying = EditorApplication.isPlaying,
                    timeSinceStartup = EditorApplication.timeSinceStartup
                },
                note = "For detailed build profiling, use the Build Report package (com.unity.build-report-inspector) " +
                       "or inspect the last build report via build.getReport after build.execute."
            }, "Build profile info retrieved");
        }

        private string GetDefaultBuildPath(BuildTarget target)
        {
            var projectName = Application.productName;
            var extension = GetBuildExtension(target);
            return Path.Combine("Builds", target.ToString(), $"{projectName}{extension}");
        }

        private string GetBuildExtension(BuildTarget target)
        {
            return target switch
            {
                BuildTarget.StandaloneWindows or BuildTarget.StandaloneWindows64 => ".exe",
                BuildTarget.StandaloneOSX => ".app",
                BuildTarget.Android => ".apk",
                _ => ""
            };
        }

        private string[] GetScenesInBuildSettings()
        {
            return EditorBuildSettings.scenes.Select(s => s.path).ToArray();
        }

        private object[] GetBuildSteps(BuildReport report)
        {
            return report.steps.Select(step => (object)new
            {
                name = step.name,
                duration = step.duration.TotalSeconds,
                messages = step.messages?.Length ?? 0
            }).ToArray();
        }

        public override bool CanExecute()
        {
            return !EditorApplication.isPlaying && !EditorApplication.isCompiling;
        }
    }
}