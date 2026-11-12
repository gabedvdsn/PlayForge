using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A crowd control rule (TW-16): its tag, what it stops, and, where it stops the abilities carrying one of its tags,
    /// those tags. A stun stops every ability, so its row names none.
    /// </summary>
    [CustomPropertyDrawer(typeof(CrowdControlRule))]
    public class CrowdControlRuleDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var rule = new VisualElement { name = "CrowdControlRule" };
            rule.Add(new PropertyField(property.FindPropertyRelative(nameof(CrowdControlRule.Tag)), "Tag") { name = nameof(CrowdControlRule.Tag) });

            var stops = property.FindPropertyRelative(nameof(CrowdControlRule.Stops));
            rule.Add(new PropertyField(stops, "Stops") { name = nameof(CrowdControlRule.Stops) });

            var abilityTags = new PropertyField(property.FindPropertyRelative(nameof(CrowdControlRule.AbilityTags)), "Ability Tags")
            {
                name = nameof(CrowdControlRule.AbilityTags),
            };
            rule.Add(abilityTags);

            // The property the tracker hands back, not the one built with, which a list's reorder can leave stale (ED-12)
            void Show(SerializedProperty current) => abilityTags.style.display =
                current.enumValueIndex == (int)ECrowdControlStops.AbilitiesTagged ? DisplayStyle.Flex : DisplayStyle.None;
            Show(stops);
            rule.TrackPropertyValue(stops, Show);
            return rule;
        }
    }
}
