using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A tag query, in Unity's aligned fields (ED-66): its tag, which tags match it, then how many it counts, as its
    /// comparison and its number on one row, "Count  Greater Than  0" for has the tag. It drew a tinted box with labels
    /// of its own size and color.
    /// </summary>
    [CustomPropertyDrawer(typeof(TagQuery))]
    public class TagQueryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "TagQuery" };
            // The style sheet lays out the count's row, wherever the query is drawn (ED-20)
            ForgeTheme.Apply(root);

            root.Add(new PropertyField(property.FindPropertyRelative(nameof(TagQuery.Tag)), "Tag") { name = "Tag" });

            var match = new EnumField("Match")
            {
                name = "MatchMode",
                tooltip = "Which tags count: this one, it and its children, it and its parents, or any under its root",
            };
            match.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
            match.BindProperty(property.FindPropertyRelative(nameof(TagQuery.MatchMode)));
            root.Add(match);

            var count = new ForgePartsField("Count")
            {
                name = "Count",
                tooltip = "How many matching tags pass: Greater Than 0 is has the tag, Equal 0 is doesn't",
            };
            var comparison = count.Part(new EnumField { name = "Operator", tooltip = "How the count of matching tags compares with the number" });
            comparison.BindProperty(property.FindPropertyRelative(nameof(TagQuery.Operator)));
            var number = count.Part(new IntegerField { name = "Magnitude", tooltip = "The number the count compares with" });
            number.BindProperty(property.FindPropertyRelative(nameof(TagQuery.Magnitude)));
            root.Add(count);
            return root;
        }
    }
}
