using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// How a chance rolls (TW-8), as Unity's foldout of its fields: Pseudo-Random, Own Luck, and Luck while Own Luck is on,
    /// where it's read; without it the project's Luck role is read, so Luck is hidden (FN-23).
    /// </summary>
    [CustomPropertyDrawer(typeof(ChanceRoll))]
    public class ChanceRollDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new Foldout
            {
                name = "ChanceRoll",
                text = preferredLabel ?? property.displayName,
                // Unity's foldout of a field keeps whether it's open on the field, as it does for its own
                bindingPath = property.propertyPath,
            };
            root.Add(new PropertyField(property.FindPropertyRelative(nameof(ChanceRoll.PseudoRandom))) { name = "PseudoRandom" });
            root.Add(new PropertyField(property.FindPropertyRelative(nameof(ChanceRoll.OwnLuck))) { name = "OwnLuck" });
            var luck = new PropertyField(property.FindPropertyRelative(nameof(ChanceRoll.Luck))) { name = "Luck" };
            root.Add(luck);

            var own = $"{property.propertyPath}.{nameof(ChanceRoll.OwnLuck)}";
            ForgeShowWhen.Track(luck, property.serializedObject, serialized => ForgeShowWhen.IsOn(serialized.FindProperty(own)));
            return root;
        }
    }
}
