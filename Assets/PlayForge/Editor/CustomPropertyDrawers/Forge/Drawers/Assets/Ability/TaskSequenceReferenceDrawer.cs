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
    /// A sequence reference (ED-57, FN-24): a field as a kind field is, the sequence's name in its box, which opens a
    /// searchable dropdown of the project's sequences, the static methods marked [TaskSequenceMethod] that take nothing and
    /// return a sequence, by their names, None first, each class's under its name where more than one class holds them. A
    /// pick is one Undo step. A method the reference names that the project no longer has shows as missing until another
    /// is picked.
    /// <para>
    /// It was Unity's popup, one long list, and before that an IMGUI popup, which wrote "(None)", and which searched every
    /// type of every assembly for the methods.
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
            var root = new VisualElement { name = "SequenceReference" };
            // The style sheet lays out the field, wherever it's drawn, as a kind field's (ED-20)
            ForgeTheme.Apply(root);
            var field = new ForgeTypeField(preferredLabel ?? property.displayName)
            {
                name = "Sequence",
                tooltip = "The sequence it runs: a static method marked [TaskSequenceMethod] that takes nothing and returns one",
            };
            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            root.Add(field);

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
                field.showMixedValue = reference.hasMultipleDifferentValues;
                // A missing method shows as missing until another is picked
                if (!field.showMixedValue) field.Show(shown == None ? null : shown, shown == None, shown == None ? "No sequence: pick one" : shown);
            }

            field.Opened += opened =>
            {
                new ForgeSequencePicker(entries, picked =>
                {
                    var reference = Fresh();
                    if (reference == null) return;
                    Pick(reference, picked?.Method);
                    Show();
                }).Show(opened.Box.worldBound);
            };

            root.TrackSerializedObjectValue(serialized, _ => Show());
            Show();
            return root;
        }

        /// <summary>
        /// Points a sequence reference at a sequence method, or at none for null, as one Undo step: what a pick in its
        /// picker does (FN-24).
        /// </summary>
        public static void Pick(SerializedProperty reference, MethodInfo method)
        {
            if (reference == null) return;
            var typeProp = reference.FindPropertyRelative(TypeField);
            var methodProp = reference.FindPropertyRelative(MethodField);
            if (method == null)
            {
                typeProp.stringValue = "";
                methodProp.stringValue = "";
            }
            else
            {
                typeProp.stringValue = method.DeclaringType?.AssemblyQualifiedName;
                methodProp.stringValue = method.Name;
            }
            reference.serializedObject.ApplyModifiedProperties();
        }
    }
}
