using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for asset management operations
    /// </summary>
    public class AssetManagerTool : McpToolBase
    {
        public override string ToolName => "asset_manage";
        public override string Description => "Manage project assets and resources";
        public override string Category => "asset";

        [Serializable]
        public class AssetParameters
        {
            public string action = "list_assets";
            public string folder = "Assets";
            public string filter = "";
            public string type = "";
            public string assetPath = "";
            public string destinationPath = "";
            public string targetPath = "";
            public string sourcePath = "";
            public string newName = "";
            public string name = "";
            public string path = "";
            public string savePath = "";
            public string guid = "";
            public string query = "";
            public string shader = "Standard";
            public string packageName = "";
            public string version = "";
            public string prefabPath = "";
            public string gameObjectName = "";
            public bool includeSubfolders = true;
            public bool includeMetadata = true;
            public bool includeDependencies = false;
            public bool recursive = false;
            public int maxResults = 100;
            public int width = 256;
            public int height = 256;
            public string format = "RGBA32";
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = GetParameters<AssetParameters>(parameters);
                var action = McpEditorHelpers.GetAction(parameters, args.action ?? "list_assets");

                if (action is "create" or "instantiate" or "update" &&
                    (!string.IsNullOrWhiteSpace(args.prefabPath) || !string.IsNullOrWhiteSpace(args.gameObjectName)))
                    return ManagePrefabs(args, parameters);

                if (action is "move" or "copy" or "rename" or "delete" or "createfolder" or "create_folder")
                    return OrganizeAsset(args, parameters);

                return action switch
                {
                    "list_assets" => ListAssets(args),
                    "find_assets" or "search" => SearchAssets(args),
                    "create_folder" or "createfolder" => CreateFolder(args),
                    "move_asset" or "move" => MoveAsset(args),
                    "delete_asset" or "delete" => DeleteAsset(args),
                    "import" => ImportAsset(args),
                    "create_material" => CreateMaterial(args, parameters),
                    "manage_prefabs" => ManagePrefabs(args, parameters),
                    "organize" => OrganizeAsset(args, parameters),
                    "create_texture" => CreateTexture(args, parameters),
                    "get_info" => GetAssetInfo(args),
                    "manage_packages" or "manage" or "list" => ManagePackages(args, parameters),
                    "install" or "update" or "remove" => ManagePackages(args, parameters),
                    "organize" => OrganizeAsset(args, parameters),
                    _ => CreateErrorResponse(
                        $"Unknown asset action '{action}'. Supported: import, create_material, manage_prefabs, organize, search, create_texture, get_info, manage_packages, list_assets, move_asset, delete_asset")
                };
            }
            catch (Exception e)
            {
                LogError($"Asset management failed: {e.Message}");
                return CreateErrorResponse($"Asset management failed: {e.Message}", e.StackTrace);
            }
        }

        private object ImportAsset(AssetParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.sourcePath))
                return CreateErrorResponse("sourcePath is required.");

            if (!File.Exists(args.sourcePath))
                return CreateErrorResponse($"Source file not found: {args.sourcePath}");

            var targetFolder = McpEditorHelpers.EnsureAssetFolder(
                string.IsNullOrWhiteSpace(args.targetPath) ? "Assets/Imported" : args.targetPath);

            var fileName = Path.GetFileName(args.sourcePath);
            var destPath = $"{targetFolder}/{fileName}";

            if (File.Exists(destPath))
                destPath = AssetDatabase.GenerateUniqueAssetPath(destPath);

            File.Copy(args.sourcePath, destPath, true);
            AssetDatabase.ImportAsset(destPath);

            return CreateSuccessResponse(new
            {
                source = args.sourcePath,
                importedPath = destPath,
                type = AssetDatabase.GetMainAssetTypeAtPath(destPath)?.Name
            }, "Asset imported");
        }

        private object CreateMaterial(AssetParameters args, object rawParams)
        {
            if (string.IsNullOrWhiteSpace(args.name))
                return CreateErrorResponse("name is required.");

            var folder = McpEditorHelpers.EnsureAssetFolder(
                string.IsNullOrWhiteSpace(args.path) ? "Assets/Materials" : args.path);

            var shader = Shader.Find(args.shader ?? "Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                return CreateErrorResponse($"Shader '{args.shader}' not found.");

            var material = new Material(shader) { name = args.name };

            if (rawParams is JObject jObj && jObj["properties"] is JObject props)
            {
                if (props["color"] != null)
                    material.color = McpEditorHelpers.ExtractColor(props["color"], Color.white);
                if (props["metallic"] != null && material.HasProperty("_Metallic"))
                    material.SetFloat("_Metallic", props["metallic"].Value<float>());
                if (props["smoothness"] != null && material.HasProperty("_Glossiness"))
                    material.SetFloat("_Glossiness", props["smoothness"].Value<float>());
            }

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{args.name}.mat");
            AssetDatabase.CreateAsset(material, assetPath);

            return CreateSuccessResponse(new { path = assetPath, name = args.name, shader = shader.name }, "Material created");
        }

        private object ManagePrefabs(AssetParameters args, object rawParams)
        {
            var subAction = "create";
            if (rawParams is JObject jObj)
                subAction = (jObj.Value<string>("action") ?? "create").Trim().ToLowerInvariant();

            switch (subAction)
            {
                case "create":
                {
                    if (string.IsNullOrWhiteSpace(args.gameObjectName))
                        return CreateErrorResponse("gameObjectName is required to create a prefab.");

                    var go = McpEditorHelpers.FindGameObject(args.gameObjectName);
                    if (go == null)
                        return CreateErrorResponse($"GameObject '{args.gameObjectName}' not found.");

                    var prefabPath = McpEditorHelpers.NormalizeAssetPath(args.prefabPath ?? $"Assets/Prefabs/{go.name}.prefab");
                    if (prefabPath == null)
                        return CreateErrorResponse("prefabPath must be inside Assets.");

                    McpEditorHelpers.EnsureAssetFolder(Path.GetDirectoryName(prefabPath)?.Replace("\\", "/"));
                    prefabPath = AssetDatabase.GenerateUniqueAssetPath(prefabPath);

                    var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    return CreateSuccessResponse(new { path = prefabPath, name = prefab.name }, "Prefab created");
                }
                case "instantiate":
                {
                    var prefabPath = McpEditorHelpers.NormalizeAssetPath(args.prefabPath);
                    if (prefabPath == null)
                        return CreateErrorResponse("prefabPath is required.");

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null)
                        return CreateErrorResponse($"Prefab not found at {prefabPath}");

                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    if (rawParams is JObject j && j["instantiatePosition"] is JObject pos)
                    {
                        var v = McpEditorHelpers.ExtractVector3(pos);
                        if (v.HasValue) instance.transform.position = v.Value;
                    }

                    return CreateSuccessResponse(new
                    {
                        name = instance.name,
                        instanceId = UnityCompat.GetObjectId(instance)
                    }, "Prefab instantiated");
                }
                default:
                    return CreateErrorResponse($"Prefab action '{subAction}' is not yet implemented. Supported: create, instantiate.");
            }
        }

        private object OrganizeAsset(AssetParameters args, object rawParams)
        {
            var subAction = args.action;
            if (rawParams is JObject jObj && jObj["action"] != null)
            {
                var nodeAction = jObj["action"].ToString().Trim().ToLowerInvariant();
                if (nodeAction is "move" or "copy" or "rename" or "delete" or "createfolder" or "create_folder")
                    subAction = nodeAction;
            }

            subAction = (subAction ?? "move").Trim().ToLowerInvariant();

            switch (subAction)
            {
                case "createfolder":
                case "create_folder":
                    args.folder = args.targetPath ?? args.sourcePath ?? args.folder;
                    return CreateFolder(args);

                case "move":
                case "move_asset":
                    args.assetPath = args.sourcePath ?? args.assetPath;
                    args.destinationPath = args.targetPath ?? args.destinationPath;
                    return MoveAsset(args);

                case "copy":
                {
                    var src = McpEditorHelpers.NormalizeAssetPath(args.sourcePath ?? args.assetPath);
                    var dst = McpEditorHelpers.NormalizeAssetPath(args.targetPath ?? args.destinationPath);
                    if (src == null || dst == null)
                        return CreateErrorResponse("sourcePath and targetPath are required.");

                    if (AssetDatabase.IsValidFolder(src))
                        return CreateErrorResponse("Copying folders is not supported. Copy individual assets.");

                    var error = AssetDatabase.CopyAsset(src, dst);
                    if (!string.IsNullOrEmpty(error))
                        return CreateErrorResponse(error);
                    return CreateSuccessResponse(new { from = src, to = dst }, "Asset copied");
                }

                case "rename":
                {
                    var src = McpEditorHelpers.NormalizeAssetPath(args.sourcePath ?? args.assetPath);
                    if (src == null)
                        return CreateErrorResponse("sourcePath is required.");
                    var newName = args.newName ?? args.name;
                    if (string.IsNullOrWhiteSpace(newName))
                        return CreateErrorResponse("newName is required.");

                    var dir = Path.GetDirectoryName(src)?.Replace("\\", "/");
                    var dst = $"{dir}/{newName}{Path.GetExtension(src)}";
                    var error = AssetDatabase.MoveAsset(src, dst);
                    if (!string.IsNullOrEmpty(error))
                        return CreateErrorResponse(error);
                    return CreateSuccessResponse(new { from = src, to = dst }, "Asset renamed");
                }

                case "delete":
                case "delete_asset":
                    args.assetPath = args.sourcePath ?? args.assetPath;
                    return DeleteAsset(args);

                default:
                    return CreateErrorResponse($"Unknown organize action '{subAction}'.");
            }
        }

        private object SearchAssets(AssetParameters args)
        {
            var searchFilter = "t:Object";
            if (!string.IsNullOrWhiteSpace(args.type))
                searchFilter = $"t:{args.type}";
            else if (!string.IsNullOrWhiteSpace(args.filter))
                searchFilter = args.filter;

            if (!string.IsNullOrWhiteSpace(args.query))
                searchFilter = string.IsNullOrWhiteSpace(args.type) ? args.query : $"{args.query} t:{args.type}";

            var folders = McpEditorHelpers.NormalizeAssetPath(args.path ?? args.folder) is string f
                ? new[] { f }
                : new[] { "Assets" };

            var guids = AssetDatabase.FindAssets(searchFilter, folders);
            var assets = new List<object>();
            var limit = Mathf.Clamp(args.maxResults, 1, 500);

            foreach (var guid in guids.Take(limit))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                assets.Add(new
                {
                    path = assetPath,
                    guid,
                    type = AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.Name ?? "Unknown",
                    name = Path.GetFileNameWithoutExtension(assetPath)
                });
            }

            return CreateSuccessResponse(new
            {
                query = args.query ?? searchFilter,
                count = assets.Count,
                totalMatches = guids.Length,
                assets
            }, $"Found {assets.Count} asset(s)");
        }

        private object CreateTexture(AssetParameters args, object rawParams)
        {
            if (string.IsNullOrWhiteSpace(args.name))
                return CreateErrorResponse("name is required.");

            args.width = Mathf.Clamp(args.width, 1, 4096);
            args.height = Mathf.Clamp(args.height, 1, 4096);

            var color = Color.white;
            if (rawParams is JObject jObj && jObj["color"] != null)
                color = McpEditorHelpers.ExtractColor(jObj["color"], Color.white);

            var tex = new Texture2D(args.width, args.height, TextureFormat.RGBA32, false);
            var pixels = new Color[args.width * args.height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();

            var folder = McpEditorHelpers.EnsureAssetFolder(
                string.IsNullOrWhiteSpace(args.savePath) ? "Assets/Textures" : args.savePath);
            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{args.name}.png");
            File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath);
            UnityEngine.Object.DestroyImmediate(tex);

            return CreateSuccessResponse(new
            {
                path = assetPath,
                width = args.width,
                height = args.height
            }, "Texture created");
        }

        private object GetAssetInfo(AssetParameters args)
        {
            string assetPath = null;
            if (!string.IsNullOrWhiteSpace(args.guid))
                assetPath = AssetDatabase.GUIDToAssetPath(args.guid);
            else
                assetPath = McpEditorHelpers.NormalizeAssetPath(args.assetPath ?? args.path);

            if (string.IsNullOrEmpty(assetPath))
                return CreateErrorResponse("assetPath or guid is required.");

            var mainType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath);
            long fileSize = 0;
            if (File.Exists(assetPath))
                fileSize = new FileInfo(assetPath).Length;

            var info = new Dictionary<string, object>
            {
                ["path"] = assetPath,
                ["guid"] = AssetDatabase.AssetPathToGUID(assetPath),
                ["name"] = Path.GetFileNameWithoutExtension(assetPath),
                ["type"] = mainType?.Name ?? "Unknown",
                ["sizeBytes"] = fileSize
            };

            if (args.includeMetadata && importer != null)
                info["importerType"] = importer.GetType().Name;

            if (args.includeDependencies)
            {
                var deps = AssetDatabase.GetDependencies(assetPath, args.recursive);
                info["dependencies"] = deps;
                info["dependencyCount"] = deps.Length;
            }

            return CreateSuccessResponse(info, "Asset info retrieved");
        }

        private object ManagePackages(AssetParameters args, object rawParams)
        {
            var subAction = McpEditorHelpers.GetAction(rawParams, "list");

            if (subAction is "install" or "update" or "remove")
            {
                return CreateErrorResponse(
                    $"Package {subAction} requires the Package Manager UI or manifest.json editing. " +
                    $"Use action=list to see installed packages, then edit Packages/manifest.json manually.");
            }

            var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            if (!File.Exists(manifestPath))
                return CreateErrorResponse("Packages/manifest.json not found.");

            var content = File.ReadAllText(manifestPath);
            var packages = new List<object>();

            try
            {
                var manifest = JObject.Parse(content);
                if (manifest["dependencies"] is JObject deps)
                {
                    foreach (var prop in deps.Properties())
                    {
                        packages.Add(new
                        {
                            name = prop.Name,
                            version = prop.Value.ToString(),
                            isBuiltIn = prop.Name.StartsWith("com.unity.")
                        });
                    }
                }
            }
            catch (Exception e)
            {
                return CreateErrorResponse($"Failed to parse manifest: {e.Message}");
            }

            return CreateSuccessResponse(new
            {
                totalPackages = packages.Count,
                packages = packages.OrderBy(p => ((dynamic)p).name).ToList()
            }, "Installed packages listed");
        }

        private object ListAssets(AssetParameters args)
        {
            var folder = NormalizeFolder(args.folder);
            if (folder == null)
                return CreateErrorResponse("Folder must be inside the Assets folder.");

            if (!AssetDatabase.IsValidFolder(folder))
                return CreateErrorResponse($"Folder does not exist: {folder}");

            var assetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                .Where(p => args.includeSubfolders || Path.GetDirectoryName(p) == folder)
                .ToArray();

            var assets = assetPaths.Select(path => new
            {
                path,
                type = AssetDatabase.GetMainAssetTypeAtPath(path)?.Name ?? "Unknown"
            }).ToList();

            return CreateSuccessResponse(new { folder, count = assets.Count, assets }, "Assets listed");
        }

        private object CreateFolder(AssetParameters args)
        {
            var folder = NormalizeFolder(args.folder ?? args.targetPath ?? args.path);
            if (folder == null || string.Equals(folder, "Assets", StringComparison.OrdinalIgnoreCase))
                return CreateErrorResponse("Provide a folder path inside Assets to create.");

            if (AssetDatabase.IsValidFolder(folder))
                return CreateErrorResponse($"Folder already exists: {folder}");

            var created = McpEditorHelpers.EnsureAssetFolder(folder);
            return CreateSuccessResponse(new { path = created }, "Folder created");
        }

        private object MoveAsset(AssetParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.assetPath) || string.IsNullOrWhiteSpace(args.destinationPath))
                return CreateErrorResponse("assetPath and destinationPath are required.");

            var source = McpEditorHelpers.NormalizeAssetPath(args.assetPath);
            var destination = McpEditorHelpers.NormalizeAssetPath(args.destinationPath);
            if (source == null || destination == null)
                return CreateErrorResponse("Paths must be inside the Assets folder.");

            var error = AssetDatabase.MoveAsset(source, destination);
            if (!string.IsNullOrEmpty(error))
                return CreateErrorResponse($"Failed to move asset: {error}");

            return CreateSuccessResponse(new { from = source, to = destination }, "Asset moved");
        }

        private object DeleteAsset(AssetParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.assetPath))
                return CreateErrorResponse("assetPath is required.");

            var assetPath = McpEditorHelpers.NormalizeAssetPath(args.assetPath);
            if (assetPath == null)
                return CreateErrorResponse("assetPath must be inside the Assets folder.");

            if (!AssetDatabase.DeleteAsset(assetPath))
                return CreateErrorResponse($"Failed to delete asset at {assetPath}");

            return CreateSuccessResponse(new { path = assetPath }, "Asset deleted");
        }

        private string NormalizeFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return "Assets";
            var normalized = folder.Replace("\\", "/").TrimEnd('/');
            return normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) ? normalized : null;
        }
    }
}