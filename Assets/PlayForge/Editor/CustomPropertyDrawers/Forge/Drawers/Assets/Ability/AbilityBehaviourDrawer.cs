using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An ability's behaviour drawn whole, as an asset or a component of the user's own holds one (ED-66): Unity's
    /// foldout of the fields the ability inspector shows of it, its targeting, its asset loaders, what cancels it and
    /// what it blocks, and its stages. The lists are Unity's, where a new loader starts empty and each stage keeps its
    /// fold as one is removed or dragged; Implicit Tag shows under Use Implicit Targeting while it's on. It drew colored
    /// sections that folded, lists of its own, and badges.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbilityBehaviour))]
    public class AbilityBehaviourDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new Foldout
            {
                name = "AbilityBehaviour",
                text = preferredLabel ?? property.displayName,
                // Unity's foldout of a field keeps whether it's open on the field, as it does for its own
                bindingPath = property.propertyPath,
            };
            // The style sheet lays out the fields, wherever the behaviour is drawn (ED-20)
            ForgeTheme.Apply(root);

            var serialized = property.serializedObject;
            var path = property.propertyPath;
            string PathOf(string field) => $"{path}.{field}";

            root.Add(new PropertyField(property.FindPropertyRelative(nameof(AbilityBehaviour.Targeting)), "Targeting") { name = "Targeting" });

            var useImplicit = new Toggle("Use Implicit Targeting")
            {
                name = "UseImplicitTargeting",
                tooltip = "The ability's source is its target",
            };
            useImplicit.AddToClassList(Toggle.alignedFieldUssClassName);
            useImplicit.BindProperty(property.FindPropertyRelative(nameof(AbilityBehaviour.UseImplicitTargeting)));
            root.Add(useImplicit);
            var implicitTag = new PropertyField(property.FindPropertyRelative(nameof(AbilityBehaviour.ImplicitTag)), "Implicit Tag")
            {
                name = "ImplicitTag",
                tooltip = "The tag the implicit target is given",
            };
            implicitTag.AddToClassList("pf-dependent");
            root.Add(implicitTag);

            var loaders = new PropertyField(property.FindPropertyRelative(nameof(AbilityBehaviour.AssetLoaders)), "Asset Loaders")
            {
                name = "AssetLoaders",
                tooltip = "Tasks that load the assets the stages use, such as the effects they apply",
            };
            ForgeLists.AddsEmpty(loaders, serialized, PathOf(nameof(AbilityBehaviour.AssetLoaders)));
            root.Add(loaders);
            root.Add(new PropertyField(property.FindPropertyRelative(nameof(AbilityBehaviour.CancelledBy)), "Cancelled By")
            {
                name = "CancelledBy",
                tooltip = "Abilities, by tag, that cancel this one as they activate, in each stage that names none",
            });
            root.Add(new PropertyField(property.FindPropertyRelative(nameof(AbilityBehaviour.Blocks)), "Blocks")
            {
                name = "Blocks",
                tooltip = "Abilities, by tag, that can't activate while this one runs, in each stage that names none",
            });
            var stages = new PropertyField(property.FindPropertyRelative(nameof(AbilityBehaviour.Stages)), "Stages") { name = "Stages" };
            ForgeLists.KeepsStates(stages, serialized, PathOf(nameof(AbilityBehaviour.Stages)));
            root.Add(stages);

            // Implicit Tag follows the toggle, an Undo among its changes
            void Show()
            {
                SerializedProperty shown;
                try
                {
                    shown = serialized.targetObject != null ? serialized.FindProperty(PathOf(nameof(AbilityBehaviour.UseImplicitTargeting))) : null;
                }
                catch (Exception)
                {
                    return;
                }
                if (shown == null) return;
                implicitTag.EnableInClassList("pf-hidden", !shown.boolValue && !shown.hasMultipleDifferentValues);
            }
            root.TrackSerializedObjectValue(serialized, _ => Show());
            Show();
            return root;
        }
    }
}
