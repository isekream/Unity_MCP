using System;
using System.Reflection;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Cross-version helpers for Unity API differences (e.g. Unity 6 entity IDs and physics naming).
    /// </summary>
    internal static class UnityCompat
    {
        private static readonly MethodInfo GetEntityIdMethod =
            typeof(UnityEngine.Object).GetMethod("GetEntityId", BindingFlags.Public | BindingFlags.Instance);

        public static int GetObjectId(UnityEngine.Object obj)
        {
            if (obj == null) return 0;

            if (GetEntityIdMethod != null)
            {
                return (int)GetEntityIdMethod.Invoke(obj, null);
            }

#pragma warning disable CS0618
            return obj.GetInstanceID();
#pragma warning restore CS0618
        }

        public static Vector3 GetRigidbodyVelocity(Rigidbody rb)
        {
            if (rb == null) return Vector3.zero;

            var prop = rb.GetType().GetProperty("linearVelocity", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(Vector3))
            {
                return (Vector3)prop.GetValue(rb);
            }

            return rb.velocity;
        }

        public static Vector3 GetRigidbodyAngularVelocity(Rigidbody rb)
        {
            return rb != null ? rb.angularVelocity : Vector3.zero;
        }

        public static float GetRigidbodyDrag(Rigidbody rb)
        {
            if (rb == null) return 0f;

            var prop = rb.GetType().GetProperty("linearDamping", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(float))
            {
                return (float)prop.GetValue(rb);
            }

            return rb.drag;
        }

        public static float GetRigidbodyAngularDrag(Rigidbody rb)
        {
            return rb != null ? rb.angularDrag : 0f;
        }

        public static Vector2 GetRigidbody2DVelocity(Rigidbody2D rb2d)
        {
            if (rb2d == null) return Vector2.zero;

            var prop = rb2d.GetType().GetProperty("linearVelocity", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(Vector2))
            {
                return (Vector2)prop.GetValue(rb2d);
            }

            return rb2d.velocity;
        }

        public static bool TryCopyAsset(string sourcePath, string destinationPath, out string error)
        {
            error = null;
#if UNITY_6000_0_OR_NEWER
            if (!UnityEditor.AssetDatabase.CopyAsset(sourcePath, destinationPath))
            {
                error = $"Failed to copy asset from {sourcePath} to {destinationPath}";
                return false;
            }
            return true;
#else
            error = UnityEditor.AssetDatabase.CopyAsset(sourcePath, destinationPath);
            return string.IsNullOrEmpty(error);
#endif
        }
    }
}