using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Utility for clearing section properties to default values.
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
                
                // Otherwise reset to type default
                ResetPropertyToDefault(prop);
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
        
        private static void ResetPropertyToDefault(SerializedProperty prop)
        {
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
                                ResetPropertyToDefault(child);
                            } while (child.NextVisible(false));
                        }
                    }
                    break;
            }
        }
    }
}
