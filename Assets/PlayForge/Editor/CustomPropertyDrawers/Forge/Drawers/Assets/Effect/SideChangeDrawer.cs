using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An effect's side change (TW-18): a switch, then, while it's on, whose sides it gives, the sides it names where it
    /// names them, its priority among others on the one entity, and whether it interrupts what that entity runs.
    /// </summary>
    [CustomPropertyDrawer(typeof(SideChange))]
    public class SideChangeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "SideChange" };
            var changes = property.FindPropertyRelative(nameof(SideChange.Changes));
            var toggle = new Toggle(preferredLabel ?? "Side Change")
            {
                name = nameof(SideChange.Changes),
                tooltip = "Change the sides of the one it's on while it lasts, to its caster's or to those it names",
            };
            toggle.AddToClassList(Toggle.alignedFieldUssClassName);
            toggle.BindProperty(changes);
            root.Add(toggle);

            var fields = new VisualElement { name = "SideChangeFields" };
            fields.AddToClassList("pf-dependent");
            var source = property.FindPropertyRelative(nameof(SideChange.Source));
            fields.Add(new PropertyField(source, "Sides From") { name = nameof(SideChange.Source) });
            var sides = new PropertyField(property.FindPropertyRelative(nameof(SideChange.Sides)), "Sides") { name = nameof(SideChange.Sides) };
            fields.Add(sides);
            fields.Add(new PropertyField(property.FindPropertyRelative(nameof(SideChange.Priority)), "Priority") { name = nameof(SideChange.Priority) });
            fields.Add(new PropertyField(property.FindPropertyRelative(nameof(SideChange.Interrupts)), "Interrupts") { name = nameof(SideChange.Interrupts) });
            root.Add(fields);

            // The fields while it changes sides, and the sides while it names them; the properties the trackers hand back,
            // which a reorder can't leave stale
            void ShowFields(SerializedProperty current) => fields.style.display = current.boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            void ShowSides(SerializedProperty current) =>
                sides.style.display = current.enumValueIndex == (int)ESideSource.Named ? DisplayStyle.Flex : DisplayStyle.None;
            ShowFields(changes);
            ShowSides(source);
            root.TrackPropertyValue(changes, ShowFields);
            root.TrackPropertyValue(source, ShowSides);
            return root;
        }
    }
}
