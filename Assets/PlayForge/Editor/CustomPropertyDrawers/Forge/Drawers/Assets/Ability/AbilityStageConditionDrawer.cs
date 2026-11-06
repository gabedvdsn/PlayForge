using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A stage's condition (ED-64): its tag, a tag field in the label column, and, once it names one, where the stage looks
    /// for it and whether it runs while the tag is missing instead, each indented under it. With no tag, the stage always
    /// runs, and Unity's foldout showed all three.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbilityStageCondition))]
    public class AbilityStageConditionDrawer : PropertyDrawer
    {
        private const string Hidden = "pf-hidden";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "StageCondition" };
            ForgeTheme.Apply(root);
            var tagProp = property.FindPropertyRelative(nameof(AbilityStageCondition.Tag));
            var onProp = property.FindPropertyRelative(nameof(AbilityStageCondition.On));
            var missingProp = property.FindPropertyRelative(nameof(AbilityStageCondition.WhenMissing));

            root.Add(new PropertyField(tagProp, preferredLabel ?? "Condition") { name = "ConditionTag" });

            var on = new EnumField("Looked For On")
            {
                name = "ConditionOn",
                tooltip = "Where the tag is looked for: the activation's data, where an Apply Effects task records a landing, or the caster's or target's tags",
            };
            on.AddToClassList(EnumField.alignedFieldUssClassName);
            on.AddToClassList("pf-dependent");
            on.BindProperty(onProp);
            root.Add(on);

            var missing = new Toggle("When Missing")
            {
                name = "ConditionWhenMissing",
                tooltip = "Run the stage while the tag is missing instead, as a miss's recovery does",
            };
            missing.AddToClassList(Toggle.alignedFieldUssClassName);
            missing.AddToClassList("pf-dependent");
            missing.BindProperty(missingProp);
            root.Add(missing);

            // Where it looks, and When Missing, show once the condition names a tag. The tag is found again by its path,
            // as a list's elements move (ED-49)
            var serialized = property.serializedObject;
            var tagPath = tagProp.propertyPath;
            void Show()
            {
                SerializedProperty tag;
                try
                {
                    tag = serialized.targetObject != null ? serialized.FindProperty(tagPath) : null;
                }
                catch (Exception)
                {
                    return;
                }
                if (tag == null) return;
                bool named = !string.IsNullOrEmpty(tag.FindPropertyRelative(nameof(Tag.Name))?.stringValue);
                on.EnableInClassList(Hidden, !named);
                missing.EnableInClassList(Hidden, !named);
            }
            // The asset is followed, not the tag, which a removed stage takes with it (ED-64)
            root.TrackSerializedObjectValue(serialized, _ => Show());
            Show();
            return root;
        }
    }
}
