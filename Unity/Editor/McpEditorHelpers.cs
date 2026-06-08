using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace UnityMCP.Editor
{
    internal static class McpEditorHelpers
    {
        public static string GetAction(object parameters, string defaultAction)
        {
            if (parameters == null) return defaultAction;

            try
            {
                if (parameters is JObject jObj && jObj["action"] != null)
                    return jObj["action"].ToString().Trim().ToLowerInvariant();

                if (parameters is Dictionary<string, object> dict && dict.TryGetValue("action", out var act))
                    return act?.ToString().Trim().ToLowerInvariant() ?? defaultAction;

                var json = JsonConvert.SerializeObject(parameters);
                var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (parsed != null && parsed.TryGetValue("action", out var actionVal) && actionVal != null)
                    return actionVal.ToString().Trim().ToLowerInvariant();
            }
            catch { /* fall through */ }

            return defaultAction;
        }

        public static GameObject FindGameObject(string name, int instanceId = 0)
        {
            if (instanceId != 0)
            {
#pragma warning disable CS0618
                var byId = EditorUtility.InstanceIDToObject(instanceId) as GameObject;
#pragma warning restore CS0618
                if (byId != null) return byId;
            }

            if (!string.IsNullOrEmpty(name))
            {
                var go = GameObject.Find(name);
                if (go != null) return go;

                foreach (var obj in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (obj.name == name && obj.scene.isLoaded)
                        return obj;
                }
            }

            return null;
        }

        public static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var normalized = path.Replace("\\", "/").Trim();
            if (!normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
            {
                if (!normalized.StartsWith("/"))
                    normalized = "Assets/" + normalized.TrimStart('/');
            }
            return normalized.StartsWith("Assets", StringComparison.OrdinalIgnoreCase) ? normalized : null;
        }

        public static string EnsureAssetFolder(string folderPath)
        {
            var safeFolder = NormalizeAssetPath(folderPath) ?? "Assets";
            if (AssetDatabase.IsValidFolder(safeFolder)) return safeFolder;

            var parent = "Assets";
            var relative = safeFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                ? safeFolder.Substring("Assets/".Length)
                : safeFolder;

            foreach (var part in relative.Split('/'))
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                var next = $"{parent}/{part}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, part);
                parent = next;
            }

            return safeFolder;
        }

        public static Vector3? ExtractVector3(JToken token)
        {
            if (token == null) return null;
            if (token is JArray arr && arr.Count >= 3)
                return new Vector3(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>());
            if (token is JObject obj)
                return new Vector3(
                    obj.Value<float?>("x") ?? 0f,
                    obj.Value<float?>("y") ?? 0f,
                    obj.Value<float?>("z") ?? 0f);
            return null;
        }

        public static Color ExtractColor(JToken token, Color defaultColor)
        {
            if (token is JObject obj)
            {
                return new Color(
                    obj.Value<float?>("r") ?? defaultColor.r,
                    obj.Value<float?>("g") ?? defaultColor.g,
                    obj.Value<float?>("b") ?? defaultColor.b,
                    obj.Value<float?>("a") ?? defaultColor.a);
            }
            return defaultColor;
        }

        public static Type FindComponentType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = assembly.GetType(typeName, false, true);
                    if (type != null && typeof(Component).IsAssignableFrom(type))
                        return type;

                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) &&
                            typeof(Component).IsAssignableFrom(t))
                            return t;
                    }
                }
                catch { /* skip dynamic assemblies */ }
            }

            return null;
        }
    }
}