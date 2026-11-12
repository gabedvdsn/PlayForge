using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An ability's attribute cost (TW-29): the attribute it spends, or, where it names none, the role whose attribute it
    /// spends, and how much.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeCost))]
    public class AttributeCostDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var cost = new VisualElement { name = "AttributeCost" };
            var attribute = property.FindPropertyRelative(nameof(AttributeCost.Attribute));
            cost.Add(new PropertyField(attribute, "Attribute") { name = nameof(AttributeCost.Attribute) });
            var role = new PropertyField(property.FindPropertyRelative(nameof(AttributeCost.Role)), "Role") { name = nameof(AttributeCost.Role) };
            cost.Add(role);
            cost.Add(new PropertyField(property.FindPropertyRelative(nameof(AttributeCost.Amount)), "Amount") { name = nameof(AttributeCost.Amount) });

            // The role only while it names no attribute; the property the tracker hands back, which a reorder can't leave stale
            void Show(SerializedProperty current) => role.style.display = current.objectReferenceValue == null ? DisplayStyle.Flex : DisplayStyle.None;
            Show(attribute);
            cost.TrackPropertyValue(attribute, Show);
            return cost;
        }
    }
}
