using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An effect's transfer (TW-20): when it moves, who gives and of what, who gets and in what, which of the values, and how
    /// much, each a field of its own as an attribute cost's are.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeTransfer))]
    public class AttributeTransferDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var transfer = new VisualElement { name = "AttributeTransfer" };
            Add(transfer, property, nameof(AttributeTransfer.Delivery), "Delivery");
            Add(transfer, property, nameof(AttributeTransfer.From), "From");
            Add(transfer, property, nameof(AttributeTransfer.FromAttribute), "From Attribute");
            Add(transfer, property, nameof(AttributeTransfer.To), "To");
            Add(transfer, property, nameof(AttributeTransfer.ToAttribute), "To Attribute");
            Add(transfer, property, nameof(AttributeTransfer.Slot), "Slot");
            Add(transfer, property, nameof(AttributeTransfer.Amount), "Amount");
            return transfer;
        }

        private static void Add(VisualElement parent, SerializedProperty property, string path, string label) =>
            parent.Add(new PropertyField(property.FindPropertyRelative(path), label) { name = path });
    }
}
