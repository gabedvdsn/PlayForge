#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(TaskSequenceReference))]
    public class TaskSequenceReferenceDrawer : PropertyDrawer
    {
        private static List<(string key, string display, MethodInfo method)> _cache;
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            BuildCache();
            
            var typeProp = property.FindPropertyRelative("_typeName");
            var methodProp = property.FindPropertyRelative("_methodName");
            
            // Find current index
            int current = 0;
            if (!string.IsNullOrEmpty(typeProp.stringValue))
            {
                var type = System.Type.GetType(typeProp.stringValue);
                if (type != null)
                {
                    var key = $"{type.FullName}.{methodProp.stringValue}";
                    current = _cache.FindIndex(c => c.key == key) + 1;
                }
            }
            
            var options = new[] { "(None)" }.Concat(_cache.Select(c => c.display)).ToArray();
            
            EditorGUI.BeginProperty(position, label, property);
            int selected = EditorGUI.Popup(position, label.text, current, options);
            
            if (selected != current)
            {
                if (selected == 0)
                {
                    typeProp.stringValue = null;
                    methodProp.stringValue = null;
                }
                else
                {
                    var entry = _cache[selected - 1];
                    typeProp.stringValue = entry.method.DeclaringType.AssemblyQualifiedName;
                    methodProp.stringValue = entry.method.Name;
                }
            }
            EditorGUI.EndProperty();
        }
        
        private static void BuildCache()
        {
            if (_cache != null) return;
            _cache = new List<(string, string, MethodInfo)>();
            
            var validTypes = new[] { typeof(TaskSequence), typeof(TaskSequenceChain), typeof(TaskSequenceDefinition) };
            
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in asm.GetTypes())
                    {
                        foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            var attr = method.GetCustomAttribute<TaskSequenceMethodAttribute>();
                            if (attr == null || !validTypes.Contains(method.ReturnType) || method.GetParameters().Length > 0) continue;
                            
                            var key = $"{type.FullName}.{method.Name}";
                            var display = attr.DisplayName ?? method.Name;
                            if (method.ReturnType == typeof(TaskSequenceChain)) display += " [Chain]";
                            _cache.Add((key, display, method));
                        }
                    }
                }
                catch { }
            }
            
            _cache = _cache.OrderBy(c => c.display).ToList();
        }
        
        [InitializeOnLoadMethod]
        private static void Reset() => _cache = null;
    }
}
#endif