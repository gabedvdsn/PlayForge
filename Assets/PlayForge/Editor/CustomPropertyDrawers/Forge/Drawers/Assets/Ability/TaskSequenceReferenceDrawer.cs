using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A sequence reference (ED-57): Unity's popup of the project's sequences, the static methods marked
    /// [TaskSequenceMethod] that take nothing and return a sequence, by their names, None first. A pick is one Undo step.
    /// A method the reference names that the project no longer has shows as missing until another is picked.
    /// <para>
    /// It was an IMGUI popup, which wrote "(None)", and which searched every type of every assembly for the methods.
    /// </para>
    /// </summary>
    [CustomPropertyDrawer(typeof(TaskSequenceReference))]
    public class TaskSequenceReferenceDrawer : PropertyDrawer
    {
        public const string None = "None";
        private const string TypeField = "_typeName";
        private const string MethodField = "_methodName";

        // A sequence the popup offers: the method, and the name it shows
        internal readonly struct Entry
        {
            public readonly MethodInfo Method;
            public readonly string Name;

            public Entry(MethodInfo method, string name)
            {
                Method = method;
                Name = name;
            }
        }

        private static readonly Type[] Returns = { typeof(TaskSequence), typeof(TaskSequenceChain), typeof(TaskSequenceDefinition) };

        /// <summary>
        /// The project's sequences, by name, a chain's marked as one. Two of one name are told apart by the class that
        /// holds each.
        /// </summary>
        internal static List<Entry> Entries()
        {
            var methods = TypeCache.GetMethodsWithAttribute<TaskSequenceMethodAttribute>()
                .Where(method => method.IsStatic && method.GetParameters().Length == 0 && Returns.Contains(method.ReturnType))
                .ToList();
            string NameOf(MethodInfo method)
            {
                var name = method.GetCustomAttribute<TaskSequenceMethodAttribute>()?.DisplayName;
                if (string.IsNullOrEmpty(name)) name = method.Name;
                return method.ReturnType == typeof(TaskSequenceChain) ? $"{name} (Chain)" : name;
            }
            var named = methods.Select(method => (Method: method, Name: NameOf(method))).ToList();
            var repeated = new HashSet<string>(named.GroupBy(each => each.Name).Where(group => group.Count() > 1).Select(group => group.Key));
            return named
                .Select(each => new Entry(each.Method, repeated.Contains(each.Name) ? $"{each.Name} ({each.Method.DeclaringType?.Name})" : each.Name))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var serialized = property.serializedObject;
            var path = property.propertyPath;
            SerializedProperty Fresh()
            {
                try
                {
                    return serialized.targetObject != null ? serialized.FindProperty(path) : null;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            var entries = Entries();
            var popup = new PopupField<string>(preferredLabel ?? property.displayName)
            {
                name = "Sequence",
                tooltip = "The sequence it runs: a static method marked [TaskSequenceMethod] that takes nothing and returns one",
            };
            popup.AddToClassList(BaseField<string>.alignedFieldUssClassName);

            // The name of the sequence a reference names, None for none, or the method marked missing
            string NameOf(SerializedProperty reference)
            {
                var typeName = reference.FindPropertyRelative(TypeField)?.stringValue;
                var methodName = reference.FindPropertyRelative(MethodField)?.stringValue;
                if (string.IsNullOrEmpty(typeName) || string.IsNullOrEmpty(methodName)) return None;
                var type = Type.GetType(typeName);
                foreach (var entry in entries)
                    if (entry.Method.DeclaringType == type && entry.Method.Name == methodName) return entry.Name;
                return $"{methodName} (missing)";
            }

            void Show()
            {
                var reference = Fresh();
                if (reference == null) return;
                var shown = NameOf(reference);
                var choices = new List<string> { None };
                // A missing method keeps its place until another is picked
                if (shown != None && entries.All(entry => entry.Name != shown)) choices.Add(shown);
                choices.AddRange(entries.Select(entry => entry.Name));
                popup.choices = choices;
                popup.showMixedValue = reference.hasMultipleDifferentValues;
                popup.SetValueWithoutNotify(shown);
            }

            popup.RegisterValueChangedCallback(change =>
            {
                var reference = Fresh();
                if (reference == null) return;
                var typeProp = reference.FindPropertyRelative(TypeField);
                var methodProp = reference.FindPropertyRelative(MethodField);
                if (change.newValue == None)
                {
                    typeProp.stringValue = "";
                    methodProp.stringValue = "";
                }
                else
                {
                    var picked = entries.FirstOrDefault(entry => entry.Name == change.newValue);
                    if (picked.Method == null) return;
                    typeProp.stringValue = picked.Method.DeclaringType?.AssemblyQualifiedName;
                    methodProp.stringValue = picked.Method.Name;
                }
                serialized.ApplyModifiedProperties();
                Show();
            });

            popup.TrackSerializedObjectValue(serialized, _ => Show());
            Show();
            return popup;
        }
    }
}
