using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A class with no drawer of its own drawn as a foldout of its fields, its arrow in line with the rows around it, as
    /// a list's is: an attribute read's retention groups, as an × Attribute scaler's Groups, and a turn to face's
    /// settings. Unity hangs an inspector foldout's arrow 12 px into the margin, to the left of a list's arrow and, in a
    /// scaler's block, out to the block's edge; this one keeps the lists' column, as <c>pf-foldout--aligned</c> sets in
    /// PlayForge.uss. A field marked <see cref="ForgeShowWhenAttribute"/> shows only while the field it names says so, as
    /// a read's Groups shows only for the reads that name groups.
    /// </summary>
    [CustomPropertyDrawer(typeof(RetentionGroups))]
    [CustomPropertyDrawer(typeof(TurnToFaceSettings))]
    public class AlignedFoldoutDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var foldout = new Foldout { text = preferredLabel ?? property.displayName, value = property.isExpanded, name = "AlignedFoldout" };
            ForgeTheme.Apply(foldout);
            foldout.AddToClassList("pf-foldout--aligned");
            var path = property.propertyPath;
            var serialized = property.serializedObject;
            foldout.RegisterValueChangedCallback(change =>
            {
                if (change.target != foldout) return;
                var now = serialized.targetObject != null ? serialized.FindProperty(path) : null;
                if (now != null) now.isExpanded = change.newValue;
            });

            // Each of its fields, Unity's, as a class's foldout holds them
            var type = DrawnType(fieldInfo);
            var child = property.Copy();
            var end = property.GetEndProperty();
            if (child.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    var field = new PropertyField(child.Copy()) { name = child.name };
                    foldout.Add(field);
                    var when = type?.GetField(child.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        ?.GetCustomAttributes<ForgeShowWhenAttribute>().ToArray();
                    if (when is { Length: > 0 }) ForgeShowWhen.Track(field, serialized, now => Shows(now, path, when));
                } while (child.NextVisible(false));
            }
            return foldout;
        }

        // The class the drawn field holds, or its elements' where it's a list or an array
        private static Type DrawnType(FieldInfo info)
        {
            var type = info?.FieldType;
            if (type == null) return null;
            if (type.IsArray) return type.GetElementType();
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>) ? type.GetGenericArguments()[0] : type;
        }

        // Whether each field a condition names, beside this one, holds one of its values: false where one doesn't, and
        // null, which shows the field, where one can't be told, as a selection's values differ
        private static bool? Shows(SerializedObject serialized, string path, ForgeShowWhenAttribute[] when)
        {
            bool told = true;
            foreach (var each in when)
            {
                var says = Says(serialized.FindProperty($"{path}.{each.Field}"), each.Values);
                if (says == false) return false;
                told &= says.HasValue;
            }
            return told ? true : null;
        }

        // Whether a field holds one of the values given: a bool's, or an enum's or a whole number's
        private static bool? Says(SerializedProperty field, object[] values)
        {
            if (field == null || field.hasMultipleDifferentValues) return null;
            if (field.propertyType == SerializedPropertyType.Boolean)
                return values.Length == 0 ? field.boolValue : values.Any(value => value is bool on && on == field.boolValue);
            return ForgeShowWhen.Says(field, values.OfType<IConvertible>().Select(value => Convert.ToInt32(value, CultureInfo.InvariantCulture)).ToArray());
        }
    }
}
