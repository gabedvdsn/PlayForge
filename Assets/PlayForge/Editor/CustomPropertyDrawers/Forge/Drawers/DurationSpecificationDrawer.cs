using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(GameplayEffectDurationSpecification))]
    public class DurationSpecificationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            
            // ═══════════════════════════════════════════════════════════════════
            // Policy Section
            // ═══════════════════════════════════════════════════════════════════
            var policySection = CreateSection("Policy", Colors.SectionBlue);
            container.Add(policySection);
            
            var durationPolicyProp = property.FindPropertyRelative("DurationPolicy");
            var durationPolicyField = CreatePropertyField(durationPolicyProp, "Duration Policy");
            durationPolicyField.style.marginBottom = 4;
            policySection.Add(durationPolicyField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Duration Section
            // ═══════════════════════════════════════════════════════════════════
            var durationSection = CreateSection("Duration", Colors.SectionGreen);
            container.Add(durationSection);
            
            // Duration row
            var durationRow = new VisualElement();
            durationRow.style.flexDirection = FlexDirection.Row;
            durationRow.style.marginBottom = 4;
            
            var durationValueField = CreatePropertyField(property.FindPropertyRelative("Duration"), "Base Duration");
            durationValueField.style.flexGrow = 1;
            durationValueField.style.marginRight = 4;
            durationRow.Add(durationValueField);
            durationSection.Add(durationRow);
            
            var durationCalcOpField = CreatePropertyField(property.FindPropertyRelative("RealDuration"), "Calculation");
            durationCalcOpField.style.marginBottom = 2;
            durationSection.Add(durationCalcOpField);
            
            var durationCalcField = CreatePropertyField(property.FindPropertyRelative("DurationScaler"), "Duration Scaler");
            durationCalcField.style.marginBottom = 8;
            durationSection.Add(durationCalcField);
            
            // Delta time subsection
            durationSection.Add(CreateHintLabel("Delta Time Modification"));
            
            var deltaTimeCalcOpField = CreatePropertyField(property.FindPropertyRelative("RealDeltaTime"), "Calculation");
            deltaTimeCalcOpField.style.marginBottom = 2;
            durationSection.Add(deltaTimeCalcOpField);
            
            var deltaTimeField = CreatePropertyField(property.FindPropertyRelative("DeltaTimeScaler"), "Delta Time Scaler");
            durationSection.Add(deltaTimeField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Ticks Section
            // ═══════════════════════════════════════════════════════════════════
            var ticksSection = CreateSection("Ticks", Colors.SectionOrange);
            container.Add(ticksSection);
            
            // Enable periodic ticks toggle
            var enablePeriodicTicksProp = property.FindPropertyRelative("EnablePeriodicTicks");
            var enablePeriodicTicksField = CreatePropertyField(enablePeriodicTicksProp, "Enable Periodic Ticks", "Enable Periodic Ticks");
            enablePeriodicTicksField.tooltip = "When disabled, the effect will not tick periodically. TickOnApplication is still respected.";
            enablePeriodicTicksField.style.marginBottom = 4;
            ticksSection.Add(enablePeriodicTicksField);
            
            // Tick on application (always visible when ticks section is visible)
            var tickOnAppField = CreatePropertyField(property.FindPropertyRelative("TickOnApplication"), "Tick On Application", "Tick On Application");
            tickOnAppField.tooltip = "Execute effect impact immediately on application (before any periodic ticks).";
            tickOnAppField.style.marginBottom = 8;
            ticksSection.Add(tickOnAppField);
            
            // Periodic tick settings container (hidden when EnablePeriodicTicks is false)
            var periodicTickSettings = new VisualElement();
            periodicTickSettings.style.marginTop = 4;
            ticksSection.Add(periodicTickSettings);
            
            periodicTickSettings.Add(CreateHintLabel("Periodic Tick Configuration"));
            
            var ticksRow = new VisualElement();
            ticksRow.style.flexDirection = FlexDirection.Row;
            ticksRow.style.marginBottom = 4;
            
            // Ticks (for Durational)
            var ticksValueField = CreatePropertyField(property.FindPropertyRelative("Ticks"), "Ticks", "Ticks");
            ticksValueField.tooltip = "Number of ticks over the duration (Durational policy).";
            ticksValueField.style.flexGrow = 1;
            ticksValueField.style.marginRight = 4;
            ticksRow.Add(ticksValueField);
            
            // Tick Interval (for Infinite)
            var ticksIntervalValueField = CreatePropertyField(property.FindPropertyRelative("TickInterval"), "Tick Interval", "Tick Interval");
            ticksIntervalValueField.tooltip = "Time between ticks in seconds (Infinite policy).";
            ticksIntervalValueField.style.flexGrow = 1;
            ticksIntervalValueField.style.marginRight = 4;
            ticksRow.Add(ticksIntervalValueField);

            var roundingField = CreatePropertyField(property.FindPropertyRelative("Rounding"), "Rounding");
            roundingField.style.alignSelf = Align.Center;
            roundingField.style.minWidth = 100;
            ticksRow.Add(roundingField);
            
            periodicTickSettings.Add(ticksRow);
            
            var tickCalcOpField = CreatePropertyField(property.FindPropertyRelative("RealTicks"), "Calculation");
            tickCalcOpField.style.marginBottom = 2;
            periodicTickSettings.Add(tickCalcOpField);
            
            var tickCalcField = CreatePropertyField(property.FindPropertyRelative("TickScaler"), "Tick Scaler");
            periodicTickSettings.Add(tickCalcField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Execute Ticks Section
            // ═══════════════════════════════════════════════════════════════════
            var executeTicksSection = CreateSection("Execute Ticks", Colors.SectionOrange);
            container.Add(executeTicksSection);

            var executeTicksSectionHint = CreateHintLabel("Execute tick scaling magnitude computed as (1 + Additional)");
            executeTicksSection.Add(executeTicksSectionHint);
            
            var executeTicksSectionHint2 = CreateHintLabel("Note: Stacking behaviour will also impact this magnitude");
            executeTicksSection.Add(executeTicksSectionHint2);
            
            var additionalExecuteTicksField = CreatePropertyField(property.FindPropertyRelative("AdditionalExecuteTicks"), "Additional Execute Ticks", "Additional Execute Ticks");
            additionalExecuteTicksField.tooltip = "Extra ticks added to each periodic tick execution.";
            additionalExecuteTicksField.style.marginBottom = 4;
            executeTicksSection.Add(additionalExecuteTicksField);
            
            var executeTicksRow = new VisualElement();
            executeTicksRow.style.flexDirection = FlexDirection.Row;
            executeTicksRow.style.marginBottom = 4;
            
            var executeTicksCalcOpField = CreatePropertyField(property.FindPropertyRelative("RealExecuteTicks"), "Calculation");
            executeTicksCalcOpField.style.flexGrow = 1;
            executeTicksCalcOpField.style.marginRight = 4;
            executeTicksRow.Add(executeTicksCalcOpField);
            
            var executeTicksRoundingField = CreatePropertyField(property.FindPropertyRelative("ExecuteTicksRounding"), "Rounding");
            executeTicksRoundingField.style.minWidth = 100;
            executeTicksRow.Add(executeTicksRoundingField);
            
            executeTicksSection.Add(executeTicksRow);
            
            var executeTicksScalerField = CreatePropertyField(property.FindPropertyRelative("ExecuteTicksScaler"), "Execute Ticks Scaler");
            executeTicksSection.Add(executeTicksScalerField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Re-Application Section
            // ═══════════════════════════════════════════════════════════════════
            var reApplicationSection = CreateSection("Re-Application", Colors.SectionBlue);
            container.Add(reApplicationSection);
            
            var reApplicationPolicyProp = property.FindPropertyRelative("ReApplicationPolicy");
            var reApplicationPolicyField = CreatePropertyField(reApplicationPolicyProp, "Re-Application Policy");
            reApplicationPolicyField.tooltip = "How the effect behaves when applied again while container is already active.";
            reApplicationPolicyField.style.marginBottom = 4;
            reApplicationSection.Add(reApplicationPolicyField);
            
            var reApplicationInteractionField = CreatePropertyField(property.FindPropertyRelative("ReApplicationInteraction"), "Interaction", "Interaction");
            reApplicationInteractionField.tooltip = "Additional behavior on re-application.";
            reApplicationSection.Add(reApplicationInteractionField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Stacking Section
            // ═══════════════════════════════════════════════════════════════════
            var stackingSection = CreateSection("Stacking", Colors.SectionGreen);
            container.Add(stackingSection);
            
            var stackPolicyField = CreatePropertyField(property.FindPropertyRelative("StackPolicy"), "Stack Policy");
            stackPolicyField.tooltip = "How stacks are tracked (shared duration vs independent).";
            stackPolicyField.style.marginBottom = 4;
            stackingSection.Add(stackPolicyField);
            
            // Stack amount row
            var stackAmountRow = new VisualElement();
            stackAmountRow.style.flexDirection = FlexDirection.Row;
            stackAmountRow.style.marginBottom = 4;
            
            var stackAmountField = CreatePropertyField(property.FindPropertyRelative("StackAmount"), "Stack Amount", "Stack Amount");
            stackAmountField.tooltip = "Number of stacks added per application.";
            stackAmountField.style.flexGrow = 1;
            stackAmountField.style.marginRight = 4;
            stackAmountRow.Add(stackAmountField);
            
            var stackAmountRoundingField = CreatePropertyField(property.FindPropertyRelative("StackAmountRounding"), "Rounding");
            stackAmountRoundingField.style.minWidth = 100;
            stackAmountRow.Add(stackAmountRoundingField);
            
            stackingSection.Add(stackAmountRow);
            
            var stackAmountCalcOpField = CreatePropertyField(property.FindPropertyRelative("RealStackAmount"), "Calculation");
            stackAmountCalcOpField.style.marginBottom = 2;
            stackingSection.Add(stackAmountCalcOpField);
            
            var stackAmountScalerField = CreatePropertyField(property.FindPropertyRelative("StackAmountScaler"), "Stack Amount Scaler");
            stackAmountScalerField.style.marginBottom = 8;
            stackingSection.Add(stackAmountScalerField);
            
            // Stacking behaviours
            stackingSection.Add(CreateHintLabel("Stacking Behaviours"));
            
            var stackingBehavioursField = CreatePropertyField(property.FindPropertyRelative("StackingBehaviours"), "Behaviours");
            stackingSection.Add(stackingBehavioursField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Conditional Visibility
            // ═══════════════════════════════════════════════════════════════════
            void UpdateVisibility()
            {
                var policy = (EEffectDurationPolicy)durationPolicyProp.enumValueIndex;
                var enableTicks = enablePeriodicTicksProp.boolValue;
                var reAppPolicy = (EEffectReApplicationPolicy)reApplicationPolicyProp.enumValueIndex;
                
                bool isInstant = policy == EEffectDurationPolicy.Instant;
                bool isInfinite = policy == EEffectDurationPolicy.Infinite;
                bool isDurational = policy == EEffectDurationPolicy.Durational;
                bool showDurational = !isInstant;
                
                // Check if re-application policy involves stacking
                bool isStackingPolicy = reAppPolicy == EEffectReApplicationPolicy.StackExistingContainers;
                
                // Duration section - hide for Instant
                durationSection.style.display = showDurational ? DisplayStyle.Flex : DisplayStyle.None;
                
                // Within duration section, hide base duration fields for Infinite
                durationValueField.style.display = isInfinite ? DisplayStyle.None : DisplayStyle.Flex;
                durationCalcOpField.style.display = isInfinite ? DisplayStyle.None : DisplayStyle.Flex;
                durationCalcField.style.display = isInfinite ? DisplayStyle.None : DisplayStyle.Flex;
                
                // Ticks section - hide for Instant
                ticksSection.style.display = showDurational ? DisplayStyle.Flex : DisplayStyle.None;
                
                // Periodic tick settings - hide when EnablePeriodicTicks is false
                periodicTickSettings.style.display = enableTicks ? DisplayStyle.Flex : DisplayStyle.None;
                
                // Within periodic tick settings, toggle between Ticks (Durational) and TickInterval (Infinite)
                ticksValueField.style.display = isDurational ? DisplayStyle.Flex : DisplayStyle.None;
                ticksIntervalValueField.style.display = isInfinite ? DisplayStyle.Flex : DisplayStyle.None;
                roundingField.style.display = isDurational ? DisplayStyle.Flex : DisplayStyle.None;
                tickCalcOpField.style.display = isDurational ? DisplayStyle.Flex : DisplayStyle.None;
                tickCalcField.style.display = isDurational ? DisplayStyle.Flex : DisplayStyle.None;
                
                // Execute ticks section - hide for Instant or when periodic ticks disabled
                executeTicksSection.style.display = (showDurational && enableTicks) ? DisplayStyle.Flex : DisplayStyle.None;
                
                // Re-application section - hide for Instant
                reApplicationSection.style.display = showDurational ? DisplayStyle.Flex : DisplayStyle.None;
                
                // Stacking section - show only when re-application policy involves stacking
                stackingSection.style.display = (showDurational && isStackingPolicy) ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            // Initial visibility update
            container.schedule.Execute(UpdateVisibility).StartingIn(50);
            
            // Update on relevant property changes
            durationPolicyField.RegisterValueChangeCallback(_ => UpdateVisibility());
            enablePeriodicTicksField.RegisterValueChangeCallback(_ => UpdateVisibility());
            reApplicationPolicyField.RegisterValueChangeCallback(_ => UpdateVisibility());
            
            return container;
        }
    }
}