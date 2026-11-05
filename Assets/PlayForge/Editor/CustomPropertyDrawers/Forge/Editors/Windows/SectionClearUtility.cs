using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Clears a section's properties to a new asset's values, and empties its lists. Each field took its type's default,
    /// 0 or the first choice, so a cleared Impact allowed no target and had no chance to apply (ED-42).
    /// </summary>
    public static class SectionClearUtility
    {
        /// <summary>
        /// Clears the section's properties to their defaults at once, as one Undo step. It asked first, in a modal dialog;
        /// Undo takes a Clear back instead (Decision 28).
        /// </summary>
        public static void Clear(
            SerializedObject targetObject, 
            string sectionName, 
            string[] propertyPaths,
            Action onClearComplete = null,
            Func<string, object> getDefaultValue = null)
        {
            if (targetObject?.targetObject == null || propertyPaths == null || propertyPaths.Length == 0)
                return;
            
            // Record undo
            Undo.RecordObject(targetObject.targetObject, $"Clear {sectionName}");

            // A new asset of the same kind, whose fields hold their own defaults
            var fresh = FreshOf(targetObject.targetObject);
            try
            {
                foreach (var path in propertyPaths)
                {
                    var prop = targetObject.FindProperty(path);
                    if (prop == null) continue;

                    // Check if custom default provided
                    if (getDefaultValue != null)
                    {
                        var defaultValue = getDefaultValue(path);
                        if (defaultValue != null)
                        {
                            SetPropertyValue(prop, defaultValue);
                            continue;
                        }
                    }

                    // Otherwise a new asset's value, or the type's default where a new asset has none
                    ResetPropertyToDefault(prop, fresh);
                }
            }
            finally
            {
                if (fresh != null)
                {
                    var made = fresh.targetObject;
                    fresh.Dispose();
                    if (made != null) Object.DestroyImmediate(made);
                }
            }

            targetObject.ApplyModifiedProperties();
            onClearComplete?.Invoke();
            
            Debug.Log($"[PlayForge] Cleared {sectionName} properties");
        }
        
        private static void SetPropertyValue(SerializedProperty prop, object value)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = Convert.ToInt32(value);
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = Convert.ToSingle(value);
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = Convert.ToBoolean(value);
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value?.ToString() ?? "";
                    break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = Convert.ToInt32(value);
                    break;
                // Add more as needed
            }
        }
        
        // A new asset of the target's kind, which no one sees, or null for a target that isn't an asset
        private static SerializedObject FreshOf(Object target)
        {
            if (target is not ScriptableObject) return null;
            var made = ScriptableObject.CreateInstance(target.GetType());
            if (made == null) return null;
            made.hideFlags = HideFlags.HideAndDontSave;
            return new SerializedObject(made);
        }

        // Takes a new asset's value for a field it has, of the same type, a list aside: Clear empties a list
        private static bool TakeFresh(SerializedProperty prop, SerializedObject fresh)
        {
            var model = fresh?.FindProperty(prop.propertyPath);
            if (model == null || model.propertyType != prop.propertyType) return false;
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.longValue = model.longValue;
                    return true;
                case SerializedPropertyType.Enum:
                case SerializedPropertyType.LayerMask:
                    prop.intValue = model.intValue;
                    return true;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = model.boolValue;
                    return true;
                case SerializedPropertyType.Float:
                    prop.doubleValue = model.doubleValue;
                    return true;
                case SerializedPropertyType.String:
                    prop.stringValue = model.stringValue;
                    return true;
                case SerializedPropertyType.Color:
                    prop.colorValue = model.colorValue;
                    return true;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = model.objectReferenceValue;
                    return true;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = model.vector2Value;
                    return true;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = model.vector3Value;
                    return true;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = model.vector4Value;
                    return true;
                case SerializedPropertyType.AnimationCurve:
                    prop.animationCurveValue = model.animationCurveValue;
                    return true;
                default:
                    return false;
            }
        }

        private static void ResetPropertyToDefault(SerializedProperty prop, SerializedObject fresh)
        {
            if (TakeFresh(prop, fresh)) return;
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.intValue = 0;
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = false;
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = 0f;
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = "";
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = Color.white;
                    break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = null;
                    break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = 0;
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = Vector2.zero;
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = Vector3.zero;
                    break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = Vector4.zero;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    prop.animationCurveValue = new AnimationCurve();
                    break;
                case SerializedPropertyType.ManagedReference:
                    prop.managedReferenceValue = null;
                    break;
                case SerializedPropertyType.Generic:
                    if (prop.isArray)
                    {
                        prop.arraySize = 0;
                    }
                    else
                    {
                        // Reset children
                        var child = prop.Copy();
                        var end = prop.GetEndProperty();
                        if (child.NextVisible(true))
                        {
                            do
                            {
                                if (SerializedProperty.EqualContents(child, end)) break;
                                ResetPropertyToDefault(child, fresh);
                            } while (child.NextVisible(false));
                        }
                    }
                    break;
            }
        }
    }
}
