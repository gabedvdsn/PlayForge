using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public static class SerializedPropertyExtensions
    {
        public static FieldInfo GetFieldInfo(this SerializedProperty prop)
        {
            Type parentType = prop.serializedObject.targetObject.GetType();
            string[] pathParts = prop.propertyPath.Split('.');

            FieldInfo field = null;
            foreach (string part in pathParts)
            {
                if (part == "Array" || part.StartsWith("data[")) continue;

                field = parentType.GetField(part,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field is null) return null;
                parentType = field.FieldType;

                if (parentType.IsArray) parentType = parentType.GetElementType();
                else if (parentType.IsGenericType && parentType.GetGenericTypeDefinition() == typeof(List<>)) parentType = parentType.GetGenericArguments()[0];
            }

            return field;
        }
        
        public static T GetAttribute<T>(this SerializedProperty property) where T : System.Attribute
        {
            var field = property.GetFieldInfo();
            return field?.GetCustomAttribute<T>();
        }
    
        public static bool HasAttribute<T>(this SerializedProperty property) where T : System.Attribute
        {
            return property.GetAttribute<T>() != null;
        }
    }
}