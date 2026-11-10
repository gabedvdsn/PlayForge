using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The field a relative number reads (FEAT-29, NUM-9), picked from a menu of the other number fields of the object
    /// that holds the number, each by its name as the inspector writes it. The number's own field and any field that reads
    /// it back stay out, so a loop can't be picked. A name the object no longer has, as after a rename, shows as missing.
    /// </summary>
    [CustomPropertyDrawer(typeof(NumberSiblingFieldAttribute))]
    public class NumberSiblingFieldDrawer : PropertyDrawer
    {
        private const string NoneText = "Choose a field";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var path = property.propertyPath;
            var serialized = property.serializedObject;
            var choices = NumberFields.SiblingChoices(property);

            var popup = new PopupField<string>(preferredLabel ?? property.displayName)
            {
                name = "Field",
                tooltip = "The field it reads: another number field of the object that holds this one",
                choices = choices,
            };
            popup.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            popup.formatListItemCallback = Nice;
            popup.formatSelectedValueCallback = name =>
                string.IsNullOrEmpty(name) ? NoneText : popup.choices.Contains(name) ? Nice(name) : $"{Nice(name)} (missing)";
            popup.SetValueWithoutNotify(property.stringValue);
            popup.showMixedValue = property.hasMultipleDifferentValues;

            popup.RegisterValueChangedCallback(change =>
            {
                var fresh = serialized.FindProperty(path);
                if (fresh == null || fresh.propertyType != SerializedPropertyType.String) return;
                if (!fresh.hasMultipleDifferentValues && fresh.stringValue == change.newValue) return;
                fresh.stringValue = change.newValue;
                serialized.ApplyModifiedProperties();
            });

            // The fields on offer follow the object: another field that starts to read this one leaves the menu
            popup.TrackSerializedObjectValue(serialized, _ =>
            {
                var fresh = serialized.FindProperty(path);
                if (fresh == null || fresh.propertyType != SerializedPropertyType.String) return;
                popup.choices = NumberFields.SiblingChoices(fresh);
                popup.SetValueWithoutNotify(fresh.stringValue);
                popup.showMixedValue = fresh.hasMultipleDifferentValues;
            });
            return popup;
        }

        private static string Nice(string name) => string.IsNullOrEmpty(name) ? NoneText : ObjectNames.NicifyVariableName(name);
    }
}
