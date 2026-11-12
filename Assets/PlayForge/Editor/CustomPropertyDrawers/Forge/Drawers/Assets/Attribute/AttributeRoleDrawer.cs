using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>A project's role on one row (TW-29): its tag, then the attribute that plays it.</summary>
    [CustomPropertyDrawer(typeof(AttributeRole))]
    public class AttributeRoleDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = new VisualElement { name = "AttributeRole" };
            row.style.flexDirection = FlexDirection.Row;
            var role = new PropertyField(property.FindPropertyRelative(nameof(AttributeRole.Role)), "") { name = nameof(AttributeRole.Role) };
            role.style.flexGrow = 1f;
            role.style.flexBasis = 0f;
            var attribute = new PropertyField(property.FindPropertyRelative(nameof(AttributeRole.Attribute)), "") { name = nameof(AttributeRole.Attribute) };
            attribute.style.flexGrow = 1f;
            attribute.style.flexBasis = 0f;
            row.Add(role);
            row.Add(attribute);
            return row;
        }
    }
}
