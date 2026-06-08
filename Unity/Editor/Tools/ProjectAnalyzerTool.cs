using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for analyzing Unity project structure, settings, and configuration
    /// </summary>
    public class ProjectAnalyzerTool : McpToolBase
    {
        public override string ToolName => "project_analyze";
        public override string Description => "Analyze Unity project structure and return comprehensive information";
        public override string Category => "project";

        [Serializable]
        public class ProjectParameters
        {
            public string action = "analyze";
            public bool includeAssets = false;
            public bool includePackages = true;
            public bool includeScenes = true;
            public bool includeSettings = true;
            public bool includeDisabled = false;
            public bool forceReimport = false;
            public string companyName;
            public string productName;
            public string version;
            public string bundleVersion;
            public string target;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<ProjectParameters>(parameters);
                var action = McpEditorHelpers.GetAction(parameters, args.action ?? "analyze");

                return action switch
                {
                    "analyze" => RunAnalyze(args),
                    "get_info" => CreateSuccessResponse(GetProjectInfo(), "Project info retrieved"),
                    "set_settings" => SetSettings(args),
                    "list_scenes" => ListScenes(args),
                    "get_build_settings" => GetBuildSettingsResponse(),
                    "set_build_target" => SetBuildTarget(args),
                    "refresh_assets" => RefreshAssets(args),
                    _ => CreateErrorResponse(
                        $"Unknown project action '{action}'. Supported: analyze, get_info, set_settings, list_scenes, get_build_settings, set_build_target, refresh_assets")
                };
            }
            catch (Exception e)
            {
                LogError($"Project operation failed: {e.Message}");
                return CreateErrorResponse($"Project operation failed: {e.Message}", e.StackTrace);
            }
        }

        private object RunAnalyze(ProjectParameters args)
        {
            LogMessage("Starting project analysis...");

            var projectInfo = new
            {
                project = GetProjectInfo(),
                settings = args.includeSettings ? GetProjectSettings() : null,
                scenes = args.includeScenes ? GetSceneInfo() : null,
                packages = args.includePackages ? GetPackageInfo() : null,
                assets = args.includeAssets ? GetAssetInfo() : null,
                buildSettings = GetBuildSettings(),
                performance = GetPerformanceMetrics()
            };

            LogMessage("Project analysis completed successfully");
            return CreateSuccessResponse(projectInfo, "Project analysis completed");
        }

        private object SetSettings(ProjectParameters args)
        {
            var changes = new List<string>();

            if (!string.IsNullOrWhiteSpace(args.companyName))
            {
                PlayerSettings.companyName = args.companyName;
                changes.Add($"companyName={args.companyName}");
            }

            if (!string.IsNullOrWhiteSpace(args.productName))
            {
                PlayerSettings.productName = args.productName;
                changes.Add($"productName={args.productName}");
            }

            if (!string.IsNullOrWhiteSpace(args.bundleVersion))
            {
                PlayerSettings.bundleVersion = args.bundleVersion;
                changes.Add($"bundleVersion={args.bundleVersion}");
            }

            if (!string.IsNullOrWhiteSpace(args.version))
            {
                PlayerSettings.bundleVersion = args.version;
                changes.Add($"version={args.version}");
            }

            if (changes.Count == 0)
                return CreateErrorResponse("No settings provided to update.");

            AssetDatabase.SaveAssets();
            return CreateSuccessResponse(new { updated = changes }, "Project settings updated");
        }

        private object ListScenes(ProjectParameters args)
        {
            var scenes = new List<object>();

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var inBuild = false;
                var buildIndex = -1;
                var enabled = false;

                for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
                {
                    if (EditorBuildSettings.scenes[i].path == path)
                    {
                        inBuild = true;
                        buildIndex = i;
                        enabled = EditorBuildSettings.scenes[i].enabled;
                        break;
                    }
                }

                if (!inBuild && !args.includeDisabled) continue;

                scenes.Add(new
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(path),
                    path,
                    inBuildSettings = inBuild,
                    buildIndex,
                    enabled,
                    isLoaded = IsSceneLoaded(path),
                    isDirty = IsSceneDirty(path)
                });
            }

            return CreateSuccessResponse(new
            {
                totalScenes = scenes.Count,
                activeScene = EditorSceneManager.GetActiveScene().name,
                scenes
            }, "Scenes listed");
        }

        private object GetBuildSettingsResponse()
        {
            var sceneList = new List<object>();
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                var s = EditorBuildSettings.scenes[i];
                sceneList.Add(new { path = s.path, enabled = s.enabled, buildIndex = i });
            }

            return CreateSuccessResponse(new
            {
                buildSettings = GetBuildSettings(),
                playerSettings = new
                {
                    PlayerSettings.companyName,
                    PlayerSettings.productName,
                    PlayerSettings.bundleVersion,
                    applicationIdentifier = PlayerSettings.applicationIdentifier
                },
                scenesInBuild = sceneList
            }, "Build settings retrieved");
        }

        private object SetBuildTarget(ProjectParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.target))
                return CreateErrorResponse("target is required (e.g. StandaloneWindows64, Android, iOS).");

            if (!Enum.TryParse<BuildTarget>(args.target, out var buildTarget))
                return CreateErrorResponse($"Invalid build target: {args.target}");

            var group = BuildPipeline.GetBuildTargetGroup(buildTarget);
            if (!BuildPipeline.IsBuildTargetSupported(group, buildTarget))
                return CreateErrorResponse($"Build target {buildTarget} is not supported on this machine.");

            EditorUserBuildSettings.SwitchActiveBuildTarget(group, buildTarget);

            return CreateSuccessResponse(new
            {
                activeBuildTarget = buildTarget.ToString(),
                buildTargetGroup = group.ToString()
            }, $"Build target set to {buildTarget}");
        }

        private object RefreshAssets(ProjectParameters args)
        {
            if (args.forceReimport)
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            else
                AssetDatabase.Refresh();

            return CreateSuccessResponse(new
            {
                refreshed = true,
                forceReimport = args.forceReimport
            }, "Asset database refreshed");
        }

        private object GetProjectInfo()
        {
            return new
            {
                projectName = Application.productName,
                projectPath = Application.dataPath.Replace("/Assets", ""),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                companyName = Application.companyName,
                version = Application.version,
                identifier = Application.identifier,
                buildGUID = Application.buildGUID,
                cloudProjectId = Application.cloudProjectId,
                isEditor = Application.isEditor,
                systemLanguage = Application.systemLanguage.ToString()
            };
        }

        private object GetProjectSettings()
        {
            var selectedBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(selectedBuildTarget));

            return new
            {
                playerSettings = new
                {
                    companyName = PlayerSettings.companyName,
                    productName = PlayerSettings.productName,
                    applicationIdentifier = PlayerSettings.applicationIdentifier,
                    bundleVersion = PlayerSettings.bundleVersion,
                    defaultScreenWidth = PlayerSettings.defaultScreenWidth,
                    defaultScreenHeight = PlayerSettings.defaultScreenHeight,
                    colorSpace = PlayerSettings.colorSpace.ToString(),
                    apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(namedBuildTarget).ToString(),
                    scriptingBackend = PlayerSettings.GetScriptingBackend(namedBuildTarget).ToString()
                },
                qualitySettings = new
                {
                    activeColorSpace = QualitySettings.activeColorSpace.ToString(),
                    desiredColorSpace = QualitySettings.desiredColorSpace.ToString(),
                    maxQueuedFrames = QualitySettings.maxQueuedFrames,
                    pixelLightCount = QualitySettings.pixelLightCount,
                    shadowCascades = QualitySettings.shadowCascades,
                    shadowDistance = QualitySettings.shadowDistance,
                    vSyncCount = QualitySettings.vSyncCount
                },
                physicsSettings = new
                {
                    gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z },
                    defaultMaterial = GetDefaultPhysicsMaterial(),
                    bounceThreshold = Physics.bounceThreshold,
                    sleepThreshold = Physics.sleepThreshold,
                    defaultContactOffset = Physics.defaultContactOffset,
                    defaultSolverIterations = Physics.defaultSolverIterations,
                    defaultSolverVelocityIterations = Physics.defaultSolverVelocityIterations
                }
            };
        }

        private string GetDefaultPhysicsMaterial()
        {
            try
            {
                // Physics.defaultMaterial was removed in newer Unity versions
                // Try to get it via reflection for backwards compatibility
                var physicsType = typeof(Physics);
                var defaultMaterialProperty = physicsType.GetProperty("defaultMaterial");
                if (defaultMaterialProperty != null)
                {
                    // Cast to the base UnityEngine.Object: the concrete type was renamed
                    // PhysicMaterial -> PhysicsMaterial in Unity 6, and we only read .name.
                    var material = defaultMaterialProperty.GetValue(null) as UnityEngine.Object;
                    return material != null ? material.name : "None";
                }
                return "Not Available";
            }
            catch
            {
                return "Not Available";
            }
        }

        private object GetSceneInfo()
        {
            var scenes = new List<object>();
            
            // Get scenes in build settings
            for (int i = 0; i < EditorBuildSettings.scenes.Length; i++)
            {
                var scenePath = EditorBuildSettings.scenes[i];
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath.path);
                
                scenes.Add(new
                {
                    name = sceneAsset?.name ?? "Unknown",
                    path = scenePath.path,
                    enabled = scenePath.enabled,
                    buildIndex = i,
                    isLoaded = IsSceneLoaded(scenePath.path),
                    isDirty = IsSceneDirty(scenePath.path)
                });
            }

            return new
            {
                totalScenes = scenes.Count,
                activeScene = EditorSceneManager.GetActiveScene().name,
                loadedScenes = UnityEngine.SceneManagement.SceneManager.loadedSceneCount,
                scenes = scenes
            };
        }

        private object GetPackageInfo()
        {
            var packages = new List<object>();
            
            try
            {
                var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
                if (File.Exists(manifestPath))
                {
                    var manifestContent = File.ReadAllText(manifestPath);
                    var manifest = JsonUtility.FromJson<PackageManifest>(manifestContent);
                    
                    if (manifest?.dependencies != null)
                    {
                        foreach (var dependency in manifest.dependencies)
                        {
                            packages.Add(new
                            {
                                name = dependency.Key,
                                version = dependency.Value,
                                isBuiltIn = dependency.Value.StartsWith("file:") || dependency.Key.StartsWith("com.unity.")
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogError($"Failed to read package manifest: {e.Message}");
            }

            return new
            {
                totalPackages = packages.Count,
                packages = packages.OrderBy(p => ((dynamic)p).name).ToList()
            };
        }

        private object GetAssetInfo()
        {
            var assetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/"))
                .ToArray();

            var assetsByType = new Dictionary<string, int>();
            var totalSize = 0L;

            foreach (var assetPath in assetPaths)
            {
                try
                {
                    var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                    if (assetType != null)
                    {
                        var typeName = assetType.Name;
                        if (!assetsByType.ContainsKey(typeName))
                            assetsByType[typeName] = 0;
                        assetsByType[typeName]++;
                    }

                    var fileInfo = new FileInfo(assetPath);
                    if (fileInfo.Exists)
                        totalSize += fileInfo.Length;
                }
                catch
                {
                    // Skip problematic assets
                }
            }

            return new
            {
                totalAssets = assetPaths.Length,
                totalSizeBytes = totalSize,
                totalSizeMB = Math.Round(totalSize / (1024.0 * 1024.0), 2),
                assetsByType = assetsByType.OrderByDescending(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
            };
        }

        private object GetBuildSettings()
        {
            return new
            {
                activeBuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(),
                selectedBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup.ToString(),
                development = EditorUserBuildSettings.development,
                connectProfiler = EditorUserBuildSettings.connectProfiler,
                buildScriptsOnly = EditorUserBuildSettings.buildScriptsOnly,
                allowDebugging = EditorUserBuildSettings.allowDebugging,
#if UNITY_6000_0_OR_NEWER
                symlinkLibraries = EditorUserBuildSettings.symlinkSources,
#else
                symlinkLibraries = EditorUserBuildSettings.symlinkLibraries,
#endif
                exportAsGoogleAndroidProject = EditorUserBuildSettings.exportAsGoogleAndroidProject
            };
        }

        private object GetPerformanceMetrics()
        {
            return new
            {
                systemInfo = new
                {
                    operatingSystem = SystemInfo.operatingSystem,
                    processorType = SystemInfo.processorType,
                    processorCount = SystemInfo.processorCount,
                    systemMemorySize = SystemInfo.systemMemorySize,
                    graphicsDeviceName = SystemInfo.graphicsDeviceName,
                    graphicsMemorySize = SystemInfo.graphicsMemorySize,
                    maxTextureSize = SystemInfo.maxTextureSize,
                    supportsComputeShaders = SystemInfo.supportsComputeShaders,
                    deviceModel = SystemInfo.deviceModel,
                    deviceName = SystemInfo.deviceName
                },
                editorMetrics = new
                {
                    isPlaying = EditorApplication.isPlaying,
                    isPaused = EditorApplication.isPaused,
                    isCompiling = EditorApplication.isCompiling,
                    isUpdating = EditorApplication.isUpdating,
                    timeSinceStartup = EditorApplication.timeSinceStartup
                }
            };
        }

        private bool IsSceneLoaded(string scenePath)
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.loadedSceneCount; i++)
            {
                var loadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (loadedScene.path == scenePath)
                    return true;
            }
            return false;
        }

        private bool IsSceneDirty(string scenePath)
        {
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.loadedSceneCount; i++)
            {
                var loadedScene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (loadedScene.path == scenePath)
                    return loadedScene.isDirty;
            }
            return false;
        }

        [Serializable]
        private class PackageManifest
        {
            public Dictionary<string, string> dependencies;
        }
    }
} 