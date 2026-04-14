using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(GameplayEffectImpact))]
    public class GameplayEffectImpactDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            
            // Main container with themed styling
            //var mainBox = CreateMainContainer(Colors.AccentRed);
            //container.Add(mainBox);
            
            // Header
            //var header = CreateHeader(Icons.Impact, "Impact Specification", Colors.AccentRed);
            //mainBox.Add(header);
            
            // ═══════════════════════════════════════════════════════════════════
            // Target Section
            // ═══════════════════════════════════════════════════════════════════
            var targetSection = CreateSection("Target", Colors.SectionOrange);
            container.Add(targetSection);
            
            var attrTargetField = new PropertyField(property.FindPropertyRelative(nameof(GameplayEffectImpact.AttributeTarget)), "Attribute");
            attrTargetField.style.marginBottom = 2;
            targetSection.Add(attrTargetField);
            
            var targetRow = new VisualElement();
            targetRow.style.flexDirection = FlexDirection.Row;
            targetRow.style.marginBottom = 2;
            
            var targetImpactField = new PropertyField(property.FindPropertyRelative(nameof(GameplayEffectImpact.TargetImpact)), "Target");
            targetImpactField.style.flexGrow = 1;
            targetImpactField.style.marginRight = 4;
            targetRow.Add(targetImpactField);
            
            var impactOpField = new PropertyField(property.FindPropertyRelative(nameof(GameplayEffectImpact.ImpactOperation)), "Operation");
            impactOpField.style.flexGrow = 1;
            targetRow.Add(impactOpField);
            
            targetSection.Add(targetRow);

            var affSection = CreateSubsection("Affiliation", "Affiliation", Colors.SectionRed);
            
            var affiliationField = new PropertyField(property.FindPropertyRelative(nameof(GameplayEffectImpact.AffiliationPolicy)), "Policy");
            affSection.Add(affiliationField);
            
            var affiliationComparisonField = new PropertyField(property.FindPropertyRelative(nameof(GameplayEffectImpact.AffiliationComparison)), "Comparison");
            affSection.Add(affiliationComparisonField);
            
            var affiliationListField = new PropertyField(property.FindPropertyRelative(nameof(GameplayEffectImpact.Affiliations)), "Affiliations");
            affSection.Add(affiliationListField);
            
            affiliationField.RegisterValueChangeCallback(evt =>
            {
                EAffiliationPolicy policy = (EAffiliationPolicy)evt.changedProperty.enumValueIndex;
                affiliationListField.style.display = policy == EAffiliationPolicy.UseAffiliationList ? DisplayStyle.Flex : DisplayStyle.None;
            });
            
            targetSection.Add(affSection);
            
            // ═══════════════════════════════════════════════════════════════════
            // Magnitude Section
            // ═══════════════════════════════════════════════════════════════════
            var magnitudeSection = CreateSection("Impact Magnitude", Colors.SectionBlue);
            container.Add(magnitudeSection);
            
            var magnitudeField = new PropertyField(property.FindPropertyRelative(nameof(GameplayEffectImpact.MagnitudeOperation)), "");
            magnitudeField.style.marginBottom = 2;
            magnitudeSection.Add(magnitudeField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Behavior Section
            // ═══════════════════════════════════════════════════════════════════
            var behaviorSection = CreateSection("Behavior", Colors.SectionPurple);
            container.Add(behaviorSection);
            
            var impactTypesField = new PropertyField(property.FindPropertyRelative(nameof(GameplayEffectImpact.ImpactTypes)), "Impact Types");
            impactTypesField.style.marginBottom = 4;
            behaviorSection.Add(impactTypesField);
            
            var behaviorRow = new VisualElement();
            behaviorRow.style.flexDirection = FlexDirection.Row;
            behaviorRow.style.alignItems = Align.Center;
            behaviorRow.style.marginBottom = 2;
            
            var reverseField = new PropertyField(property.FindPropertyRelative(nameof(GameplayEffectImpact.ReverseImpactOnRemoval)), "Reverse On Removal");
            reverseField.style.flexGrow = 1;
            behaviorRow.Add(reverseField);
            
            behaviorSection.Add(behaviorRow);
            
            // ═══════════════════════════════════════════════════════════════════
            // Contained Effects Section
            // ═══════════════════════════════════════════════════════════════════
            var packetsSection = CreateSection("Contained Effects", Colors.SectionGreen);
            container.Add(packetsSection);
            
            var packetsHint = CreateHintLabel("Effects triggered on Apply, Tick, or Remove");
            packetsSection.Add(packetsHint);
            
            var packetsField = new PropertyField(property.FindPropertyRelative(nameof(GameplayEffectImpact.Packets)), "");
            packetsSection.Add(packetsField);
            
            return container;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════
    // ContainedEffectPacket Drawer
    // ═══════════════════════════════════════════════════════════════════════════
    [CustomPropertyDrawer(typeof(ContainedEffectPacket))]
    public class ContainedEffectPacketDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 2;
            container.style.paddingLeft = 4;
            container.style.paddingRight = 4;
            container.style.paddingTop = 2;
            container.style.paddingBottom = 2;
            container.style.backgroundColor = Colors.ItemBackground;
            container.style.borderTopLeftRadius = 3;
            container.style.borderTopRightRadius = 3;
            container.style.borderBottomLeftRadius = 3;
            container.style.borderBottomRightRadius = 3;
            
            // Policy indicator with color coding
            var policyProp = property.FindPropertyRelative(nameof(ContainedEffectPacket.Policy));
            var policyField = new PropertyField(policyProp, "");
            policyField.style.minWidth = 90;
            policyField.style.maxWidth = 90;
            policyField.style.marginRight = 8;
            container.Add(policyField);
            
            // Policy color indicator
            var colorIndicator = CreateColorIndicator(Colors.AccentGray);
            container.Insert(0, colorIndicator);
            
            // Effect reference
            var effectField = new PropertyField(property.FindPropertyRelative(nameof(ContainedEffectPacket.ContainedEffect)), "");
            effectField.style.flexGrow = 1;
            container.Add(effectField);
            
            // Update color indicator based on policy
            void UpdateColorIndicator()
            {
                var policy = (EApplyTickRemove)policyProp.enumValueIndex;
                colorIndicator.style.backgroundColor = policy switch
                {
                    EApplyTickRemove.OnApply => Colors.PolicyGreen,
                    EApplyTickRemove.OnTick => Colors.PolicyYellow,
                    EApplyTickRemove.OnRemove => Colors.PolicyRed,
                    _ => Colors.AccentGray
                };
            }
            
            container.schedule.Execute(UpdateColorIndicator).StartingIn(50);
            policyField.RegisterValueChangeCallback(_ => UpdateColorIndicator());
            
            return container;
        }
    }
}