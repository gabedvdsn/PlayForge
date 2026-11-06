using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An attribute value: its label in the label column, then its Current and Base side by side (ED-49). It drew "Cur" and
    /// "Base" in 9 px text with a slash between them, from styles set in C#.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeValue))]
    public class AttributeValueDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) =>
            Build(property, preferredLabel ?? property.displayName, property.name);

        /// <summary>A value's field, bound to its Current and Base.</summary>
        internal static ForgeValuePairField Build(SerializedProperty value, string label, string name)
        {
            var field = new ForgeValuePairField(label) { name = name };
            ForgeTheme.Apply(field);
            field.Current.BindProperty(value.FindPropertyRelative(nameof(AttributeValue.CurrentValue)));
            field.Base.BindProperty(value.FindPropertyRelative(nameof(AttributeValue.BaseValue)));
            return field;
        }
    }
}
