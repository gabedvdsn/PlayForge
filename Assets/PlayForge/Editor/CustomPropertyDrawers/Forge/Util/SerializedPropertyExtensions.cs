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
        /// <summary>
        /// The field a property is, found along its path. A managed reference on the way holds an object of its own type,
        /// whose fields the path goes on into, as a Group's members under a number's Scaler: the walk took the type the
        /// reference is declared as, which hasn't those fields, so a field inside one was never found, nor its attributes,
        /// and a Group member's kind menu offered the kinds its field rules out (ED-81).
        /// </summary>
        public static FieldInfo GetFieldInfo(this SerializedProperty prop)
        {
            Type parentType = prop.serializedObject.targetObject.GetType();
            string[] pathParts = prop.propertyPath.Split('.');
            string walked = "";

            FieldInfo field = null;
            foreach (string part in pathParts)
            {
                walked = walked.Length == 0 ? part : $"{walked}.{part}";
                if (part != "Array" && !part.StartsWith("data["))
                {
                    field = FieldOf(parentType, part);
                    if (field is null) return null;
                    parentType = field.FieldType;

                    if (parentType.IsArray) parentType = parentType.GetElementType();
                    else if (parentType.IsGenericType && parentType.GetGenericTypeDefinition() == typeof(List<>)) parentType = parentType.GetGenericArguments()[0];
                }

                // A managed reference, the field itself or an element of its list, goes on as the object it holds
                if (field != null && field.GetCustomAttribute<SerializeReference>() != null && walked != prop.propertyPath)
                {
                    var held = prop.serializedObject.FindProperty(walked);
                    if (held != null && held.propertyType == SerializedPropertyType.ManagedReference && held.managedReferenceValue != null)
                        parentType = held.managedReferenceValue.GetType();
                }
            }

            return field;
        }

        // A field of a type or of any class it's built on, a private one of a base among them
        private static FieldInfo FieldOf(Type type, string name)
        {
            for (var each = type; each != null && each != typeof(object); each = each.BaseType)
            {
                var found = each.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (found != null) return found;
            }
            return null;
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