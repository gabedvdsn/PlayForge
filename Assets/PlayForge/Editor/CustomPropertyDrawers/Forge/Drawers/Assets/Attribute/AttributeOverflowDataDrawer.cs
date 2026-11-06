using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An attribute's bounds (ED-49): the policy, then the floor and the ceiling it reads, each indented under it, as a
    /// field that depends on another is. A bound's right-click menu sets it to no limit. It drew the floor and ceiling with
    /// 10 px labels and buttons in an accent color, from styles set in C#, and showed them 50 ms after it drew.
    /// </summary>
    [CustomPropertyDrawer(typeof(AttributeOverflowData))]
    public class AttributeOverflowDataDrawer : PropertyDrawer
    {
        private const string Hidden = "pf-hidden";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "Bounds" };
            ForgeTheme.Apply(root);

            var policyProp = property.FindPropertyRelative(nameof(AttributeOverflowData.Policy));
            var floorProp = property.FindPropertyRelative(nameof(AttributeOverflowData.Floor));
            var ceilProp = property.FindPropertyRelative(nameof(AttributeOverflowData.Ceil));

            var policy = new EnumField(preferredLabel ?? "Bounds")
            {
                name = "Policy",
                tooltip = "What keeps Current and Base within bounds: 0, a floor, Base or a ceiling",
            };
            policy.AddToClassList(EnumField.alignedFieldUssClassName);
            policy.BindProperty(policyProp);
            root.Add(policy);

            var floor = AttributeValueDrawer.Build(floorProp, "Floor", "Floor");
            floor.tooltip = "The lowest Current and Base can be";
            floor.AddToClassList("pf-dependent");
            floor.AddManipulator(new ContextualMenuManipulator(menu =>
                menu.menu.AppendAction("No Floor", _ => SetBoth(floorProp, Mathf.NegativeInfinity))));
            root.Add(floor);

            var ceil = AttributeValueDrawer.Build(ceilProp, "Ceiling", "Ceil");
            ceil.tooltip = "The highest Current and Base can be";
            ceil.AddToClassList("pf-dependent");
            ceil.AddManipulator(new ContextualMenuManipulator(menu =>
                menu.menu.AppendAction("No Ceiling", _ => SetBoth(ceilProp, Mathf.Infinity))));
            root.Add(ceil);

            // A floor shows for the policies that read one, and a ceiling likewise
            void Show()
            {
                if (policyProp.serializedObject.targetObject == null) return;
                var shown = (EAttributeOverflowPolicy)policyProp.enumValueIndex;
                bool mixed = policyProp.hasMultipleDifferentValues;
                floor.EnableInClassList(Hidden, mixed || shown is not (EAttributeOverflowPolicy.FloorToBase or EAttributeOverflowPolicy.FloorToCeil));
                ceil.EnableInClassList(Hidden, mixed || shown is not (EAttributeOverflowPolicy.ZeroToCeil or EAttributeOverflowPolicy.FloorToCeil));
            }
            policy.RegisterValueChangedCallback(_ => Show());
            root.TrackPropertyValue(policyProp, _ => Show());
            Show();
            return root;
        }

        // Both of a bound's values, as one edit
        private static void SetBoth(SerializedProperty bound, float value)
        {
            bound.FindPropertyRelative(nameof(AttributeValue.CurrentValue)).floatValue = value;
            bound.FindPropertyRelative(nameof(AttributeValue.BaseValue)).floatValue = value;
            bound.serializedObject.ApplyModifiedProperties();
        }
    }
}
