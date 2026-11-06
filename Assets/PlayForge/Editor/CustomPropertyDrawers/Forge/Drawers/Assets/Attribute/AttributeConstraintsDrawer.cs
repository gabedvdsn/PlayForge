using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An attribute's constraints (ED-49): Auto Clamp, Scale With Base and Rounding, Unity's fields in the label column,
    /// with Snap Interval indented under Rounding while it snaps. They sat in a tinted, rounded box with a yellow left
    /// border, under a 10 px bold heading with colored letter badges, from styles set in C#.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeConstraints))]
    public class AttributeConstraintsDrawer : PropertyDrawer
    {
        private const string Hidden = "pf-hidden";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "Constraints" };
            ForgeTheme.Apply(root);

            var clampProp = property.FindPropertyRelative(nameof(AttributeConstraints.AutoClamp));
            var scaleProp = property.FindPropertyRelative(nameof(AttributeConstraints.AutoScaleWithBase));
            var roundingProp = property.FindPropertyRelative(nameof(AttributeConstraints.RoundingMode));
            var snapProp = property.FindPropertyRelative(nameof(AttributeConstraints.SnapInterval));

            var clamp = new Toggle("Auto Clamp") { name = "AutoClamp", tooltip = "Keep Current and Base within the bounds" };
            clamp.AddToClassList(Toggle.alignedFieldUssClassName);
            clamp.BindProperty(clampProp);
            root.Add(clamp);

            var scale = new Toggle("Scale With Base")
            {
                name = "AutoScaleWithBase",
                tooltip = "Change Current by the share Base changes by, as 80 of 100 becomes 40 of 50",
            };
            scale.AddToClassList(Toggle.alignedFieldUssClassName);
            scale.BindProperty(scaleProp);
            root.Add(scale);

            var rounding = new EnumField("Rounding")
            {
                name = "RoundingMode",
                tooltip = "How Current and Base are rounded after each change, which can make a change larger or smaller",
            };
            rounding.AddToClassList(EnumField.alignedFieldUssClassName);
            rounding.BindProperty(roundingProp);
            root.Add(rounding);

            var snap = new FloatField("Snap Interval") { name = "SnapInterval", tooltip = "The step values snap to, as 0.5" };
            snap.AddToClassList(FloatField.alignedFieldUssClassName);
            snap.AddToClassList("pf-dependent");
            snap.BindProperty(snapProp);
            root.Add(snap);

            // Snap Interval shows while Rounding snaps
            void Show()
            {
                if (roundingProp.serializedObject.targetObject == null) return;
                snap.EnableInClassList(Hidden, roundingProp.hasMultipleDifferentValues ||
                                               (EAttributeRoundingPolicy)roundingProp.enumValueIndex != EAttributeRoundingPolicy.SnapTo);
            }
            rounding.RegisterValueChangedCallback(_ => Show());
            root.TrackPropertyValue(roundingProp, _ => Show());
            Show();
            return root;
        }
    }
}
