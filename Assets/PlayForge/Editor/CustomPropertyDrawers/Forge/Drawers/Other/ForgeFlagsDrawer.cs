using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A flags field of PlayForge's, which writes only what's picked (ED-85). Unity's own flags field stores a value that
    /// holds every flag as Everything, -1, as it binds: an asset only looked at changed, as every effect's Purges' Applied
    /// By, Any by default; a template's child took the change as an override; and the inspector's SerializedObject moved
    /// while its bindings were still being made, which Unity asserted on (IsInSyncWithParentSerializedObject). This field
    /// shows the value without writing it, stores the flags picked of those its type has, so Everything is every flag it
    /// has now and not one it gains later, and follows an Undo.
    /// </summary>
    [CustomPropertyDrawer(typeof(EPurgeRelation))]
    [CustomPropertyDrawer(typeof(EEffectCategory))]
    [CustomPropertyDrawer(typeof(ESpreadMoment))]
    [CustomPropertyDrawer(typeof(ECombatLogRecords))]
    public class ForgeFlagsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var type = EnumOf(fieldInfo?.FieldType);
            // A drawer made outside a field knows no type: the number, which a user's edit alone writes
            if (type == null) return new IntegerField(preferredLabel ?? property.displayName) { bindingPath = property.propertyPath };
            var serialized = property.serializedObject;
            var path = property.propertyPath;
            // Not bound, so nothing is written as it's shown
            var field = new EnumFlagsField(preferredLabel ?? property.displayName, ValueOf(type, property.intValue))
            {
                tooltip = property.tooltip,
                showMixedValue = property.hasMultipleDifferentValues,
            };
            field.AddToClassList(BaseField<Enum>.alignedFieldUssClassName);
            field.RegisterValueChangedCallback(change =>
            {
                if (change.newValue == null) return;
                var own = serialized.FindProperty(path);
                if (own == null) return;
                int picked = Picked(type, change.newValue);
                if (!own.hasMultipleDifferentValues && own.intValue == picked) return;
                own.intValue = picked;
                serialized.ApplyModifiedProperties();
            });
            // An Undo, or an edit made elsewhere, shows here
            field.TrackPropertyValue(property, changed =>
            {
                field.showMixedValue = changed.hasMultipleDifferentValues;
                field.SetValueWithoutNotify(ValueOf(type, changed.intValue));
            });
            return field;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var type = EnumOf(fieldInfo?.FieldType);
            using var scope = new EditorGUI.PropertyScope(position, label, property);
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();
            if (type == null)
            {
                int typed = EditorGUI.IntField(position, scope.content, property.intValue);
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck()) property.intValue = typed;
                return;
            }
            var chosen = EditorGUI.EnumFlagsField(position, scope.content, ValueOf(type, property.intValue));
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck()) property.intValue = Picked(type, chosen);
        }

        // The flags of a value, of those its type has: Everything, -1, is every flag it has
        private static int Picked(Type type, Enum value) => Convert.ToInt32(value) & FlagsOf(type);

        private static Enum ValueOf(Type type, int value) => (Enum)Enum.ToObject(type, value);

        // Every flag a type has
        private static readonly Dictionary<Type, int> flagsOf = new();

        private static int FlagsOf(Type type)
        {
            if (flagsOf.TryGetValue(type, out int flags)) return flags;
            foreach (var value in Enum.GetValues(type)) flags |= Convert.ToInt32(value);
            flagsOf[type] = flags;
            return flags;
        }

        // The enum a field holds, or its list's or array's elements hold; none for a drawer made outside a field
        private static Type EnumOf(Type type)
        {
            if (type == null) return null;
            if (type.IsArray) type = type.GetElementType();
            else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) type = type.GetGenericArguments()[0];
            return type is { IsEnum: true } ? type : null;
        }
    }
}
