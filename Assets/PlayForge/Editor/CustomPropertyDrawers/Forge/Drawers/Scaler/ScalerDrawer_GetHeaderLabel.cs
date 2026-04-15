using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class ScalerDrawer
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Replace the GetHeaderLabel method in ScalerDrawer.cs with this implementation
        // ═══════════════════════════════════════════════════════════════════════════

        private string GetHeaderLabel(SerializedProperty prop)
        {
            var fi = prop.GetFieldInfo();
            if (fi == null) return prop.displayName;

            // 1. Check for explicit ScalerDisplayName attribute
            var displayName = fi.GetCustomAttribute<ScalerDisplayName>();
            if (displayName != null) 
                return displayName.Name;
            
            // 2. Check for DeriveScalerName - look at parent for ScalerOperationKeyword
            if (fi.GetCustomAttribute<DeriveScalerName>() != null)
            {
                var keyword = GetParentScalerOperationKeyword(prop);
                if (!string.IsNullOrEmpty(keyword))
                    return $"{keyword}";
            }

            // 3. Fallback to property display name
            return prop.displayName;
        }

        /// <summary>
        /// Traverses up the property path to find the parent field's ScalerOperationKeyword attribute.
        /// For a path like "DurationScaling.Scaler", this finds the ScalerOperationKeyword on "DurationScaling".
        /// </summary>
        private string GetParentScalerOperationKeyword(SerializedProperty prop)
        {
            // Get the parent property path by removing the last segment
            string path = prop.propertyPath;
            int lastDot = path.LastIndexOf('.');
            if (lastDot < 0) return null; // No parent
            
            string parentPath = path.Substring(0, lastDot);
            
            // Handle array elements: "SomeArray.Array.data[0].Scaler" -> parent is "SomeArray.Array.data[0]"
            // We need to get the field info of the array element type, not the array itself
            
            // Get the parent property
            var parentProp = prop.serializedObject.FindProperty(parentPath);
            if (parentProp == null) return null;
            
            // Try to get the FieldInfo for the parent
            var parentFieldInfo = GetFieldInfoForProperty(prop.serializedObject.targetObject.GetType(), parentPath);
            if (parentFieldInfo == null) return null;
            
            // Check for ScalerOperationKeyword on the parent field
            var keywordAttr = parentFieldInfo.GetCustomAttribute<ScalerOperationKeyword>();
            return keywordAttr?.Keyword;
        }

        /// <summary>
        /// Resolves FieldInfo from a property path, handling arrays and nested types.
        /// </summary>
        private static FieldInfo GetFieldInfoForProperty(Type hostType, string propertyPath)
        {
            if (hostType == null || string.IsNullOrEmpty(propertyPath)) 
                return null;
            
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type currentType = hostType;
            FieldInfo lastField = null;
            
            var parts = propertyPath.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                
                // Skip array indexing: "Array" and "data[N]"
                if (part == "Array")
                {
                    i++; // Skip the next "data[N]" part too
                    
                    // Get element type for array/list
                    if (currentType.IsArray)
                        currentType = currentType.GetElementType();
                    else if (currentType.IsGenericType && typeof(IList<>).IsAssignableFrom(currentType.GetGenericTypeDefinition()))
                        currentType = currentType.GetGenericArguments()[0];
                    
                    continue;
                }
                
                if (part.StartsWith("data["))
                    continue;
                
                // Find the field
                var field = currentType.GetField(part, flags);
                if (field == null)
                {
                    // Try base types
                    var baseType = currentType.BaseType;
                    while (baseType != null && field == null)
                    {
                        field = baseType.GetField(part, flags);
                        baseType = baseType.BaseType;
                    }
                }
                
                if (field == null) 
                    return lastField;
                
                lastField = field;
                currentType = field.FieldType;
                
                // Handle arrays/lists for next iteration
                if (currentType.IsArray)
                    currentType = currentType.GetElementType();
                else if (currentType.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(currentType))
                    currentType = currentType.GetGenericArguments()[0];
            }
            
            return lastField;
        }

    }
}