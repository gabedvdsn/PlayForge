using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A Threshold scaler's threshold, on one row (ED-66): the level it holds from, what the scaler reads from there, and
    /// the tier's name, Unity's fields bound to them, so an Undo shows in them. In a list it needs no label of its own.
    /// It drew a tinted row with "Lv" in blue and an arrow as text, and its fields wrote as they changed and followed
    /// nothing after.
    /// </summary>
    [CustomPropertyDrawer(typeof(LevelThreshold))]
    public class LevelThresholdDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var inList = property.propertyPath.EndsWith("]");
            var row = new ForgePartsField(inList ? null : preferredLabel ?? property.displayName) { name = "LevelThreshold" };
            // The style sheet lays out the row, wherever the threshold is drawn (ED-20)
            ForgeTheme.Apply(row);

            var level = row.Part(new IntegerField("Level") { name = "Level", tooltip = "The level the threshold holds from" });
            level.BindProperty(property.FindPropertyRelative(nameof(LevelThreshold.Level)));
            var value = row.Part(new FloatField("Value") { name = "Value", tooltip = "What the scaler reads from this level up, until the next threshold" });
            value.BindProperty(property.FindPropertyRelative(nameof(LevelThreshold.Value)));
            var tier = row.Part(new TextField("Tier") { name = "Tier", tooltip = "A name for the tier, such as Bronze or Silver; optional" });
            tier.BindProperty(property.FindPropertyRelative(nameof(LevelThreshold.TierName)));
            return row;
        }
    }

    /// <summary>
    /// A Scaler Group's member, cached or not, as Unity's fields (ED-66): how its result combines with the members'
    /// before it, then its scaler, whose own drawer shows its kind and its settings. It drew a stripe and a symbol in the
    /// operation's color, the scaler's kind beside them, and, for a cached member, a "CACHED" badge.
    /// </summary>
    internal static class GroupMemberFields
    {
        public static VisualElement Build(SerializedProperty property)
        {
            var root = new VisualElement { name = "GroupMember" };
            ForgeTheme.Apply(root);

            var operation = new EnumField("Operation")
            {
                name = "Operation",
                tooltip = "How the member's result combines with the members' before it",
            };
            operation.AddToClassList(BaseField<System.Enum>.alignedFieldUssClassName);
            operation.BindProperty(property.FindPropertyRelative(nameof(MagnitudeModifierGroupMember.RelativeOperation)));
            root.Add(operation);

            root.Add(new PropertyField(property.FindPropertyRelative(nameof(MagnitudeModifierGroupMember.Calculation)), "Scaler")
            {
                name = "Scaler",
            });
            return root;
        }
    }

    /// <inheritdoc cref="GroupMemberFields"/>
    [CustomPropertyDrawer(typeof(MagnitudeModifierGroupMember))]
    public class MagnitudeModifierGroupMemberDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) => GroupMemberFields.Build(property);
    }

    /// <inheritdoc cref="GroupMemberFields"/>
    [CustomPropertyDrawer(typeof(CachedMagnitudeModifierGroupMember))]
    public class CachedMagnitudeModifierGroupMemberDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property) => GroupMemberFields.Build(property);
    }
}
