using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A starting item on one row, as the Entity mockup draws it: the item, then an Equip checkbox, which equips it as
    /// it's given. It drew as a foldout holding Item and Equip On Init (ED-37).
    /// </summary>
    [CustomPropertyDrawer(typeof(StartingItemContainer))]
    public class StartingItemDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var row = new VisualElement();
            // The style sheet lays out the row, wherever the drawer is drawn: in Unity's own inspector too (ED-20)
            ForgeTheme.Apply(row);
            row.AddToClassList("pf-starting-item");

            var item = new PropertyField(property.FindPropertyRelative(nameof(StartingItemContainer.Item)), "");
            item.AddToClassList("pf-starting-item__item");
            row.Add(item);

            var equip = new Toggle
            {
                name = nameof(StartingItemContainer.EquipOnInit),
                text = "Equip",
                tooltip = "Equip the item as it's given",
            };
            equip.AddToClassList("pf-starting-item__equip");
            equip.BindProperty(property.FindPropertyRelative(nameof(StartingItemContainer.EquipOnInit)));
            row.Add(equip);
            return row;
        }
    }
}
