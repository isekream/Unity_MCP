using System;
using System.Collections.Generic;
using System.Linq;
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

        public sealed class ApplyPropertiesResult
        {
            public bool Success;
            public List<string> Applied = new List<string>();
            public List<string> Errors = new List<string>();
        }

        public static ApplyPropertiesResult ApplySerializedProperties(UnityEngine.Object target, JObject properties)
        {
            var result = new ApplyPropertiesResult { Success = true };
            if (target == null || properties == null || !properties.HasValues)
                return result;

            var serializedObject = new SerializedObject(target);
            Undo.RecordObject(target, "Modify Component Properties");

            foreach (var prop in properties.Properties())
            {
                var property = FindSerializedProperty(serializedObject, prop.Name);
                if (property == null)
                {
                    result.Success = false;
                    result.Errors.Add($"Property '{prop.Name}' not found on {target.GetType().Name}");
                    continue;
                }

                try
                {
                    if (!TrySetSerializedProperty(property, prop.Value))
                    {
                        result.Success = false;
                        result.Errors.Add($"Failed to set '{prop.Name}' (unsupported type {property.propertyType})");
                        continue;
                    }

                    result.Applied.Add(prop.Name);
                }
                catch (Exception e)
                {
                    result.Success = false;
                    result.Errors.Add($"Failed to set '{prop.Name}': {e.Message}");
                }
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            return result;
        }

        public static PrimitiveType? ParsePrimitiveType(string primitiveName)
        {
            if (string.IsNullOrWhiteSpace(primitiveName)) return null;

            switch (primitiveName.Trim().ToLowerInvariant())
            {
                case "cube": return PrimitiveType.Cube;
                case "sphere": return PrimitiveType.Sphere;
                case "capsule": return PrimitiveType.Capsule;
                case "cylinder": return PrimitiveType.Cylinder;
                case "plane": return PrimitiveType.Plane;
                case "quad": return PrimitiveType.Quad;
                default: return null;
            }
        }

        private static SerializedProperty FindSerializedProperty(SerializedObject serializedObject, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var direct = serializedObject.FindProperty(name);
            if (direct != null) return direct;

            var mName = name.StartsWith("m_", StringComparison.Ordinal)
                ? name
                : "m_" + char.ToUpperInvariant(name[0]) + name.Substring(1);

            direct = serializedObject.FindProperty(mName);
            if (direct != null) return direct;

            var iterator = serializedObject.GetIterator();
            while (iterator.NextVisible(true))
            {
                if (string.Equals(iterator.name, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(iterator.displayName, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(iterator.name, mName, StringComparison.OrdinalIgnoreCase))
                {
                    return serializedObject.FindProperty(iterator.propertyPath);
                }
            }

            return null;
        }

        private static bool TrySetSerializedProperty(SerializedProperty property, JToken value)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    property.intValue = value.Type == JTokenType.Boolean
                        ? (value.Value<bool>() ? 1 : 0)
                        : value.Value<int>();
                    return true;

                case SerializedPropertyType.Float:
                    property.floatValue = value.Value<float>();
                    return true;

                case SerializedPropertyType.Boolean:
                    property.boolValue = value.Value<bool>();
                    return true;

                case SerializedPropertyType.String:
                    property.stringValue = value.Type == JTokenType.Null ? "" : value.ToString();
                    return true;

                case SerializedPropertyType.Enum:
                {
                    if (value.Type == JTokenType.Integer)
                    {
                        property.enumValueIndex = value.Value<int>();
                        return true;
                    }

                    var enumName = value.ToString();
                    var names = property.enumDisplayNames;
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (string.Equals(names[i], enumName, StringComparison.OrdinalIgnoreCase))
                        {
                            property.enumValueIndex = i;
                            return true;
                        }
                    }

                    return false;
                }

                case SerializedPropertyType.Vector2:
                {
                    var v = ParseVector2(value);
                    if (!v.HasValue) return false;
                    property.vector2Value = v.Value;
                    return true;
                }

                case SerializedPropertyType.Vector3:
                {
                    var v = ExtractVector3(value);
                    if (!v.HasValue) return false;
                    property.vector3Value = v.Value;
                    return true;
                }

                case SerializedPropertyType.Vector4:
                {
                    var v = ParseVector4(value);
                    if (!v.HasValue) return false;
                    property.vector4Value = v.Value;
                    return true;
                }

                case SerializedPropertyType.Color:
                    property.colorValue = ExtractColor(value, property.colorValue);
                    return true;

                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = ResolveObjectReference(value, property);
                    return property.objectReferenceValue != null || value.Type == JTokenType.Null;

                case SerializedPropertyType.ArraySize:
                    return false;

                default:
                    if (property.isArray && value is JArray array)
                    {
                        property.arraySize = array.Count;
                        for (int i = 0; i < array.Count; i++)
                        {
                            var element = property.GetArrayElementAtIndex(i);
                            if (!TrySetSerializedProperty(element, array[i]))
                                return false;
                        }
                        return true;
                    }

                    return false;
            }
        }

        private static Vector2? ParseVector2(JToken token)
        {
            if (token is JArray arr && arr.Count >= 2)
                return new Vector2(arr[0].Value<float>(), arr[1].Value<float>());
            if (token is JObject obj)
                return new Vector2(
                    obj.Value<float?>("x") ?? 0f,
                    obj.Value<float?>("y") ?? 0f);
            return null;
        }

        private static Vector4? ParseVector4(JToken token)
        {
            if (token is JArray arr && arr.Count >= 4)
                return new Vector4(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>(), arr[3].Value<float>());
            if (token is JObject obj)
                return new Vector4(
                    obj.Value<float?>("x") ?? 0f,
                    obj.Value<float?>("y") ?? 0f,
                    obj.Value<float?>("z") ?? 0f,
                    obj.Value<float?>("w") ?? 0f);
            return null;
        }

        private static UnityEngine.Object ResolveObjectReference(JToken value, SerializedProperty property)
        {
            if (value == null || value.Type == JTokenType.Null)
                return null;

            if (value.Type == JTokenType.Integer)
            {
#pragma warning disable CS0618
                return EditorUtility.InstanceIDToObject(value.Value<int>());
#pragma warning restore CS0618
            }

            if (value.Type == JTokenType.String)
            {
                var path = value.ToString();
                if (path.StartsWith("Assets", StringComparison.OrdinalIgnoreCase))
                    return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(NormalizeAssetPath(path));

                var go = FindGameObject(path);
                if (go != null)
                {
                    if (property != null && typeof(Component).IsAssignableFrom(property.type))
                    {
                        var compType = property.type;
                        return go.GetComponent(compType) ?? go.GetComponents(compType).FirstOrDefault();
                    }
                    return go;
                }
            }

            if (value is JObject obj)
            {
                var instanceId = obj.Value<int?>("instanceId") ?? obj.Value<int?>("gameObjectId") ?? 0;
                var name = obj.Value<string>("gameObjectName") ?? obj.Value<string>("name");
                var assetPath = obj.Value<string>("path");

                if (instanceId != 0)
                {
#pragma warning disable CS0618
                    var byId = EditorUtility.InstanceIDToObject(instanceId);
#pragma warning restore CS0618
                    if (byId != null) return byId;
                }

                if (!string.IsNullOrWhiteSpace(assetPath))
                {
                    var normalized = NormalizeAssetPath(assetPath);
                    if (normalized != null)
                        return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(normalized);
                }

                if (!string.IsNullOrWhiteSpace(name))
                {
                    var gameObject = FindGameObject(name);
                    if (gameObject == null) return null;

                    var componentTypeName = obj.Value<string>("componentType");
                    if (!string.IsNullOrWhiteSpace(componentTypeName))
                    {
                        var type = FindComponentType(componentTypeName);
                        return type != null ? gameObject.GetComponent(type) : null;
                    }

                    if (property != null && typeof(Component).IsAssignableFrom(property.type))
                    {
                        var compType = property.type;
                        return gameObject.GetComponent(compType) ?? gameObject.GetComponents(compType).FirstOrDefault();
                    }

                    return gameObject;
                }
            }

            return null;
        }
    }
}