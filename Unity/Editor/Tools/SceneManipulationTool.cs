using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tool for scene manipulation operations
    /// </summary>
    public class SceneManipulationTool : McpToolBase
    {
        public override string ToolName => "scene_manipulate";
        public override string Description => "Manipulate scene objects and hierarchy";
        public override string Category => "scene";

        [Serializable]
        public class SceneParameters
        {
            public string action = "list_root_objects";
            public string name = "New Game Object";
            public string parentName = "";
            public string targetName = "";
            public string gameObjectName = "";
            public int gameObjectId = 0;
            public int instanceId = 0;
            public float[] position = null;
            public float[] rotation = null;
            public float[] scale = null;
            public bool relative = false;
            public string[] components;
            public string tag;
            public int layer = -1;
            public string filter = "";
            public bool includeInactive = false;
            public int maxDepth = -1;
            public string[] objectNames;
            public int[] instanceIds;
            public string path = "";
            public string scenePath = "";
            public bool additive = false;
            public string componentType = "";
            public string componentAction = "";
            public string primitiveType = "";
            public JObject properties;
        }

        public override object Execute(object parameters)
        {
            try
            {
                var args = NormalizeParameters(parameters);
                var action = (args.action ?? "list_root_objects").Trim().ToLowerInvariant();

                // Node sends action=add/remove/modify for modifyComponent; routing may not override it.
                if (action is "add" or "remove" or "modify" && !string.IsNullOrWhiteSpace(args.componentType))
                {
                    args.componentAction = action;
                    action = "modify_component";
                }

                return action switch
                {
                    "create_object" => CreateObject(args),
                    "create_primitive" => CreatePrimitive(args),
                    "delete_object" => DeleteObject(args),
                    "set_transform" => SetTransform(args),
                    "modify_component" => ModifyComponent(args, parameters),
                    "list_root_objects" or "query" => QueryScene(args),
                    "select_objects" => SelectObjects(args),
                    "save" => SaveScene(args),
                    "load" => LoadScene(args),
                    _ => CreateErrorResponse(
                        $"Unknown action '{args.action}'. Supported: create_object, create_primitive, delete_object, set_transform, modify_component, query, select_objects, save, load")
                };
            }
            catch (Exception e)
            {
                LogError($"Scene manipulation failed: {e.Message}");
                return CreateErrorResponse($"Scene manipulation failed: {e.Message}", e.StackTrace);
            }
        }

        private object CreateObject(SceneParameters args)
        {
            if (!string.IsNullOrWhiteSpace(args.primitiveType))
                return CreatePrimitive(args);

            var name = string.IsNullOrWhiteSpace(args.name) ? "New Game Object" : args.name;
            var gameObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create GameObject");
            FinalizeCreatedObject(gameObject, args);

            return CreateSuccessResponse(new
            {
                name = gameObject.name,
                instanceId = UnityCompat.GetObjectId(gameObject),
                scene = gameObject.scene.name,
                components = gameObject.GetComponents<Component>().Select(c => c.GetType().Name).ToArray()
            }, "GameObject created");
        }

        private object CreatePrimitive(SceneParameters args)
        {
            var primitiveName = !string.IsNullOrWhiteSpace(args.primitiveType)
                ? args.primitiveType
                : args.name;

            var primitive = McpEditorHelpers.ParsePrimitiveType(primitiveName);
            if (primitive == null)
            {
                return CreateErrorResponse(
                    $"Invalid primitive type '{primitiveName}'. Supported: Cube, Sphere, Capsule, Cylinder, Plane, Quad.");
            }

            var gameObject = GameObject.CreatePrimitive(primitive.Value);
            gameObject.name = string.IsNullOrWhiteSpace(args.name) || args.name == primitiveName
                ? primitiveName
                : args.name;

            Undo.RegisterCreatedObjectUndo(gameObject, "Create Primitive");
            FinalizeCreatedObject(gameObject, args);

            return CreateSuccessResponse(new
            {
                name = gameObject.name,
                primitive = primitive.Value.ToString(),
                instanceId = UnityCompat.GetObjectId(gameObject),
                scene = gameObject.scene.name,
                components = gameObject.GetComponents<Component>().Select(c => c.GetType().Name).ToArray()
            }, $"Primitive '{gameObject.name}' created");
        }

        private void FinalizeCreatedObject(GameObject gameObject, SceneParameters args)
        {
            if (!string.IsNullOrWhiteSpace(args.parentName))
            {
                var parent = McpEditorHelpers.FindGameObject(args.parentName);
                if (parent != null)
                    gameObject.transform.SetParent(parent.transform);
            }

            ApplyTransform(gameObject.transform, args);

            if (!string.IsNullOrWhiteSpace(args.tag))
            {
                try { gameObject.tag = args.tag; }
                catch { LogError($"Tag '{args.tag}' is not defined."); }
            }

            if (args.layer >= 0 && args.layer < 32)
                gameObject.layer = args.layer;

            if (args.components != null)
            {
                foreach (var compType in args.components)
                {
                    var type = McpEditorHelpers.FindComponentType(compType);
                    if (type != null)
                        Undo.AddComponent(gameObject, type);
                }
            }

            MarkSceneDirty(gameObject.scene);
        }

        private object DeleteObject(SceneParameters args)
        {
            var target = ResolveTarget(args);
            if (target == null)
                return CreateErrorResponse("GameObject not found. Provide gameObjectName or gameObjectId.");

            var targetName = target.name;
            var scene = target.scene;
            Undo.DestroyObjectImmediate(target);
            MarkSceneDirty(scene);

            return CreateSuccessResponse(new { name = targetName }, "GameObject deleted");
        }

        private object SetTransform(SceneParameters args)
        {
            var target = ResolveTarget(args);
            if (target == null)
                return CreateErrorResponse("GameObject not found. Provide gameObjectName or gameObjectId.");

            Undo.RecordObject(target.transform, "Set Transform");
            ApplyTransform(target.transform, args);
            MarkSceneDirty(target.scene);

            return CreateSuccessResponse(new
            {
                name = target.name,
                position = Vec(target.transform.position),
                rotation = Vec(target.transform.eulerAngles),
                scale = Vec(target.transform.localScale)
            }, "Transform updated");
        }

        private object ModifyComponent(SceneParameters args, object rawParams)
        {
            var target = ResolveTarget(args);
            if (target == null)
                return CreateErrorResponse("GameObject not found.");

            if (string.IsNullOrWhiteSpace(args.componentType))
                return CreateErrorResponse("componentType is required.");

            var compAction = args.componentAction;
            if (string.IsNullOrWhiteSpace(compAction) && rawParams is JObject jObj)
            {
                var nodeAction = jObj.Value<string>("action");
                if (nodeAction is "add" or "remove" or "modify")
                    compAction = nodeAction;
            }
            compAction = (compAction ?? "add").Trim().ToLowerInvariant();

            var type = McpEditorHelpers.FindComponentType(args.componentType);
            if (type == null)
                return CreateErrorResponse($"Component type '{args.componentType}' not found.");

            switch (compAction)
            {
                case "add":
                {
                    if (target.GetComponent(type) != null)
                        return CreateErrorResponse($"Component '{args.componentType}' already exists on '{target.name}'.");
                    var added = Undo.AddComponent(target, type);
                    var addResult = ApplyComponentProperties(added, args.properties);
                    if (!addResult.Success)
                        return CreateErrorResponse("Component added but property assignment failed.", string.Join("; ", addResult.Errors));

                    MarkSceneDirty(target.scene);
                    return CreateSuccessResponse(new
                    {
                        gameObject = target.name,
                        component = added.GetType().Name,
                        propertiesApplied = addResult.Applied.ToArray()
                    }, "Component added");
                }
                case "remove":
                {
                    var existing = target.GetComponent(type);
                    if (existing == null)
                        return CreateErrorResponse($"Component '{args.componentType}' not found on '{target.name}'.");
                    Undo.DestroyObjectImmediate(existing);
                    MarkSceneDirty(target.scene);
                    return CreateSuccessResponse(new { gameObject = target.name, component = args.componentType }, "Component removed");
                }
                case "modify":
                {
                    var existing = target.GetComponent(type);
                    if (existing == null)
                        return CreateErrorResponse($"Component '{args.componentType}' not found on '{target.name}'.");
                    if (args.properties == null || !args.properties.HasValues)
                        return CreateErrorResponse("properties object is required for modify action.");

                    var modifyResult = ApplyComponentProperties(existing, args.properties);
                    if (!modifyResult.Success)
                        return CreateErrorResponse("Component property modification failed.", string.Join("; ", modifyResult.Errors));

                    MarkSceneDirty(target.scene);
                    return CreateSuccessResponse(new
                    {
                        gameObject = target.name,
                        component = existing.GetType().Name,
                        propertiesApplied = modifyResult.Applied.ToArray()
                    }, "Component properties updated");
                }
                default:
                    return CreateErrorResponse($"Unknown component action '{compAction}'. Use add, remove, or modify.");
            }
        }

        private object QueryScene(SceneParameters args)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var results = new List<object>();

            foreach (var root in roots)
            {
                if (!args.includeInactive && !root.activeInHierarchy) continue;
                var node = BuildHierarchyNode(root.transform, 0, args);
                if (node != null) results.Add(node);
            }

            return CreateSuccessResponse(new
            {
                scene = scene.name,
                scenePath = scene.path,
                objectCount = results.Count,
                objects = results
            }, "Scene query completed");
        }

        private object BuildHierarchyNode(Transform transform, int depth, SceneParameters args)
        {
            if (args.maxDepth >= 0 && depth > args.maxDepth) return null;

            if (!string.IsNullOrWhiteSpace(args.filter) &&
                transform.name.IndexOf(args.filter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Still search children even if parent doesn't match
                var childMatches = new List<object>();
                foreach (Transform child in transform)
                {
                    var childNode = BuildHierarchyNode(child, depth + 1, args);
                    if (childNode != null) childMatches.Add(childNode);
                }
                if (childMatches.Count == 0) return null;
                return new { name = transform.name, children = childMatches };
            }

            if (!args.includeInactive && !transform.gameObject.activeInHierarchy)
                return null;

            var children = new List<object>();
            foreach (Transform child in transform)
            {
                var childNode = BuildHierarchyNode(child, depth + 1, args);
                if (childNode != null) children.Add(childNode);
            }

            return new
            {
                name = transform.name,
                instanceId = UnityCompat.GetObjectId(transform.gameObject),
                active = transform.gameObject.activeSelf,
                tag = transform.gameObject.tag,
                layer = LayerMask.LayerToName(transform.gameObject.layer),
                position = Vec(transform.position),
                components = transform.GetComponents<Component>()
                    .Where(c => c != null)
                    .Select(c => c.GetType().Name)
                    .ToArray(),
                children
            };
        }

        private object SelectObjects(SceneParameters args)
        {
            var selected = new List<GameObject>();

            if (args.objectNames != null)
            {
                foreach (var n in args.objectNames)
                {
                    var go = McpEditorHelpers.FindGameObject(n);
                    if (go != null) selected.Add(go);
                }
            }

            if (args.instanceIds != null)
            {
                foreach (var id in args.instanceIds)
                {
                    var go = McpEditorHelpers.FindGameObject(null, id);
                    if (go != null && !selected.Contains(go)) selected.Add(go);
                }
            }

            if (selected.Count == 0)
                return CreateErrorResponse("No matching GameObjects found to select.");

            Selection.objects = selected.ToArray();
            if (selected.Count == 1)
                Selection.activeGameObject = selected[0];

            return CreateSuccessResponse(new
            {
                selectedCount = selected.Count,
                names = selected.Select(g => g.name).ToArray()
            }, $"Selected {selected.Count} object(s)");
        }

        private object SaveScene(SceneParameters args)
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
                return CreateErrorResponse("No active scene to save.");

            string savePath = scene.path;
            if (!string.IsNullOrWhiteSpace(args.path))
            {
                savePath = McpEditorHelpers.NormalizeAssetPath(args.path);
                if (savePath == null)
                    return CreateErrorResponse("path must be inside the Assets folder.");
                if (!savePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                    savePath += ".unity";
                McpEditorHelpers.EnsureAssetFolder(System.IO.Path.GetDirectoryName(savePath)?.Replace("\\", "/"));
            }

            if (string.IsNullOrEmpty(savePath))
                return CreateErrorResponse("Scene has no path. Provide a path to save a new scene.");

            var saved = EditorSceneManager.SaveScene(scene, savePath);
            if (!saved)
                return CreateErrorResponse($"Failed to save scene to {savePath}");

            return CreateSuccessResponse(new { path = savePath, scene = scene.name }, "Scene saved");
        }

        private object LoadScene(SceneParameters args)
        {
            if (string.IsNullOrWhiteSpace(args.scenePath))
                return CreateErrorResponse("scenePath is required.");

            var path = McpEditorHelpers.NormalizeAssetPath(args.scenePath);
            if (path == null)
                return CreateErrorResponse("scenePath must be inside the Assets folder.");
            if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                path += ".unity";

            if (!System.IO.File.Exists(path))
                return CreateErrorResponse($"Scene file not found: {path}");

            var mode = args.additive ? OpenSceneMode.Additive : OpenSceneMode.Single;
            var scene = EditorSceneManager.OpenScene(path, mode);

            return CreateSuccessResponse(new
            {
                name = scene.name,
                path = scene.path,
                additive = args.additive,
                loadedScenes = UnityEngine.SceneManagement.SceneManager.loadedSceneCount
            }, $"Scene '{scene.name}' loaded");
        }

        private GameObject ResolveTarget(SceneParameters args)
        {
            var id = args.gameObjectId != 0 ? args.gameObjectId : args.instanceId;
            var name = !string.IsNullOrWhiteSpace(args.gameObjectName)
                ? args.gameObjectName
                : args.targetName;
            return McpEditorHelpers.FindGameObject(name, id);
        }

        private void ApplyTransform(Transform target, SceneParameters args)
        {
            if (args.position?.Length == 3)
            {
                var pos = new Vector3(args.position[0], args.position[1], args.position[2]);
                target.position = args.relative ? target.position + pos : pos;
            }

            if (args.rotation?.Length == 3)
            {
                var rot = new Vector3(args.rotation[0], args.rotation[1], args.rotation[2]);
                target.eulerAngles = args.relative ? target.eulerAngles + rot : rot;
            }

            if (args.scale?.Length == 3)
            {
                var scl = new Vector3(args.scale[0], args.scale[1], args.scale[2]);
                target.localScale = args.relative
                    ? Vector3.Scale(target.localScale, scl)
                    : scl;
            }
        }

        private void MarkSceneDirty(UnityEngine.SceneManagement.Scene scene)
        {
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }

        private static McpEditorHelpers.ApplyPropertiesResult ApplyComponentProperties(Component component, JObject properties)
        {
            return McpEditorHelpers.ApplySerializedProperties(component, properties);
        }

        private static object Vec(Vector3 v) => new { x = v.x, y = v.y, z = v.z };

        private SceneParameters NormalizeParameters(object parameters)
        {
            var args = GetParameters<SceneParameters>(parameters);

            JObject source = null;
            if (parameters is JObject jObject) source = jObject;
            else if (parameters is Dictionary<string, object> dict) source = JObject.FromObject(dict);

            if (source != null)
            {
                if (string.IsNullOrWhiteSpace(args.parentName))
                    args.parentName = source.Value<string>("parent") ?? args.parentName;

                if (string.IsNullOrWhiteSpace(args.targetName))
                    args.targetName = source.Value<string>("gameObjectName")
                        ?? source.Value<string>("targetName")
                        ?? "";

                if (string.IsNullOrWhiteSpace(args.name) || args.name == "New Game Object")
                {
                    var explicitName = source.Value<string>("name");
                    if (!string.IsNullOrWhiteSpace(explicitName) && args.action == "create_object")
                        args.name = explicitName;
                }

                if (args.gameObjectId == 0)
                    args.gameObjectId = source.Value<int?>("gameObjectId") ?? 0;

                if (args.instanceId == 0)
                    args.instanceId = source.Value<int?>("instanceId") ?? 0;

                if (args.position == null || args.position.Length != 3)
                    args.position = VectorToArray(McpEditorHelpers.ExtractVector3(source["position"]));

                if (args.rotation == null || args.rotation.Length != 3)
                    args.rotation = VectorToArray(McpEditorHelpers.ExtractVector3(source["rotation"]));

                if (args.scale == null || args.scale.Length != 3)
                    args.scale = VectorToArray(McpEditorHelpers.ExtractVector3(source["scale"]));

                if (source["components"] is JArray compArr)
                    args.components = compArr.Select(t => t.ToString()).ToArray();

                if (source["objectNames"] is JArray namesArr)
                    args.objectNames = namesArr.Select(t => t.ToString()).ToArray();

                if (source["instanceIds"] is JArray idsArr)
                    args.instanceIds = idsArr.Select(t => t.Value<int>()).ToArray();

                args.componentAction = source.Value<string>("action");
                if (args.componentAction is "add" or "remove" or "modify")
                {
                    // This is the component sub-action, not the scene action — already set via InjectAction on outer action
                }

                args.scenePath = source.Value<string>("scenePath") ?? args.scenePath;
                args.path = source.Value<string>("path") ?? args.path;
                args.filter = source.Value<string>("filter") ?? args.filter;
                args.includeInactive = source.Value<bool?>("includeInactive") ?? args.includeInactive;
                args.maxDepth = source.Value<int?>("maxDepth") ?? args.maxDepth;
                args.relative = source.Value<bool?>("relative") ?? args.relative;
                args.additive = source.Value<bool?>("additive") ?? args.additive;
                args.componentType = source.Value<string>("componentType") ?? args.componentType;
                args.primitiveType = source.Value<string>("primitive")
                    ?? source.Value<string>("primitiveType")
                    ?? args.primitiveType;

                if (source["properties"] is JObject propsObj)
                    args.properties = propsObj;
            }

            return args;
        }

        private static float[] VectorToArray(Vector3? v)
        {
            if (!v.HasValue) return null;
            return new[] { v.Value.x, v.Value.y, v.Value.z };
        }
    }
}