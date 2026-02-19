using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(GameplayEffectDuration))]
    public class GameplayEffectDurationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            
            // ═══════════════════════════════════════════════════════════════════
            // Policy Section
            // ═══════════════════════════════════════════════════════════════════
            var policyProp = property.FindPropertyRelative("DurationPolicy");
            var policyField = new PropertyField(policyProp, "Duration Policy");
            policyField.style.marginBottom = 4;
            container.Add(policyField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Duration Section
            // ═══════════════════════════════════════════════════════════════════
            var durationSection = CreateSection("Duration", Colors.SectionBlue);
            durationSection.name = "DurationSection";
            container.Add(durationSection);
            
            var durationOpField = new PropertyField(property.FindPropertyRelative("DurationOperation"), "");
            durationSection.Add(durationOpField);
            
            // Delta Time subsection
            var deltaTimeSubsection = CreateSubsection("", "Delta Time", Colors.SectionCyan);
            deltaTimeSubsection.name = "DeltaTimeSubsection";
            durationSection.Add(deltaTimeSubsection);
            
            var realDeltaTimeField = new PropertyField(property.FindPropertyRelative("RealDeltaTime"), "Real Delta Time");
            realDeltaTimeField.style.marginBottom = 2;
            deltaTimeSubsection.Add(realDeltaTimeField);
            
            var deltaTimeScalerField = new PropertyField(property.FindPropertyRelative("DeltaTimeScaler"));
            deltaTimeSubsection.Add(deltaTimeScalerField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Ticks Section
            // ═══════════════════════════════════════════════════════════════════
            var ticksSection = CreateSection("Periodic Ticks", Colors.SectionOrange);
            ticksSection.name = "TicksSection";
            container.Add(ticksSection);
            
            var enableTicksProp = property.FindPropertyRelative("EnablePeriodicTicks");
            var enableTicksField = new PropertyField(enableTicksProp, "Enable");
            enableTicksField.style.flexGrow = 1;
            enableTicksField.style.marginRight = 4;
            ticksSection.Add(enableTicksField);
            
            var tickOnAppField = new PropertyField(property.FindPropertyRelative("TickOnApplication"), "Tick On Application");
            tickOnAppField.style.flexGrow = 1;
            ticksSection.Add(tickOnAppField);
            
            // Ticks Operation (Durational)
            var ticksOpContainer = new VisualElement { name = "TicksOpContainer" };
            ticksOpContainer.style.marginTop = 4;
            ticksSection.Add(ticksOpContainer);
            
            var ticksOpHint = CreateHintLabel("Number of ticks over the duration");
            ticksOpContainer.Add(ticksOpHint);
            
            var ticksOpField = new PropertyField(property.FindPropertyRelative("TicksOperation"), "");
            ticksOpContainer.Add(ticksOpField);
            
            // Tick Interval Operation (Infinite)
            var tickIntervalContainer = new VisualElement { name = "TickIntervalContainer" };
            tickIntervalContainer.style.marginTop = 4;
            ticksSection.Add(tickIntervalContainer);
            
            var tickIntervalHint = CreateHintLabel("Fixed interval between ticks (seconds)");
            tickIntervalContainer.Add(tickIntervalHint);
            
            var tickIntervalOpField = new PropertyField(property.FindPropertyRelative("TickIntervalOperation"), "");
            tickIntervalContainer.Add(tickIntervalOpField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Execute Ticks Section
            // ═══════════════════════════════════════════════════════════════════
            var executeTicksSection = CreateSection("Execute Ticks", Colors.SectionPurple);
            executeTicksSection.name = "ExecuteTicksSection";
            container.Add(executeTicksSection);
            
            var executeTicksHint = CreateHintLabel("Execute ticks computed as: (N + Additional) \u2218 Scaler\nwhere N is defined by the container (typically N=1)");
            executeTicksSection.Add(executeTicksHint);
            
            var executeTicksOpField = new PropertyField(property.FindPropertyRelative("AdditionalExecuteTicksOperation"), "");
            executeTicksSection.Add(executeTicksOpField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Re-Application Section
            // ═══════════════════════════════════════════════════════════════════
            var reappSection = CreateSection("Re-Application", Colors.SectionCyan);
            reappSection.name = "ReApplicationSection";
            container.Add(reappSection);
            
            var reappPolicyProp = property.FindPropertyRelative("ReApplicationPolicy");
            var reappPolicyField = new PropertyField(reappPolicyProp, "Policy");
            reappPolicyField.style.marginBottom = 2;
            reappSection.Add(reappPolicyField);
            
            var reappInteractionField = new PropertyField(property.FindPropertyRelative("ReApplicationInteraction"), "Interaction");
            reappSection.Add(reappInteractionField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Stacking Section
            // ═══════════════════════════════════════════════════════════════════
            var stackingSection = CreateSection("Stacking", Colors.SectionYellow);
            stackingSection.name = "StackingSection";
            container.Add(stackingSection);
            
            var stackPolicyField = new PropertyField(property.FindPropertyRelative("StackPolicy"), "Policy");
            stackPolicyField.style.marginBottom = 4;
            stackingSection.Add(stackPolicyField);
            
            var stackBehavioursField = new PropertyField(property.FindPropertyRelative("StackingConfigs"), "Behaviours");
            stackBehavioursField.style.marginBottom = 4;
            stackingSection.Add(stackBehavioursField);
            
            var stackAmountOpField = new PropertyField(property.FindPropertyRelative("StackAmountOperation"), "");
            stackingSection.Add(stackAmountOpField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Visibility Updates
            // ═══════════════════════════════════════════════════════════════════
            container.schedule.Execute(() => UpdateVisibility(container, property)).ExecuteLater(50);
            
            policyField.RegisterValueChangeCallback(_ => UpdateVisibility(container, property));
            enableTicksField.RegisterValueChangeCallback(_ => UpdateVisibility(container, property));
            tickOnAppField.RegisterValueChangeCallback(_ => UpdateVisibility(container, property));
            reappPolicyField.RegisterValueChangeCallback(_ => UpdateVisibility(container, property));
            
            return container;
        }
        
        private void UpdateVisibility(VisualElement root, SerializedProperty property)
        {
            var policy = (EEffectDurationPolicy)property.FindPropertyRelative(nameof(GameplayEffectDuration.DurationPolicy)).enumValueIndex;
            var enableTicks = property.FindPropertyRelative(nameof(GameplayEffectDuration.EnablePeriodicTicks)).boolValue;
            var reappPolicy = (EEffectReApplicationPolicy)property.FindPropertyRelative(nameof(GameplayEffectDuration.ReApplicationPolicy)).enumValueIndex;
            var tickOnApp = property.FindPropertyRelative(nameof(GameplayEffectDuration.TickOnApplication)).boolValue;
            
            bool isInstant = policy == EEffectDurationPolicy.Instant;
            bool isDurational = policy == EEffectDurationPolicy.Durational;
            bool isInfinite = policy == EEffectDurationPolicy.Infinite;
            
            // Duration section - Durational only
            var durationSection = root.Q("DurationSection");
            if (durationSection != null)
                durationSection.style.display = isDurational ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Ticks section - non-Instant
            var ticksSection = root.Q("TicksSection");
            if (ticksSection != null)
                ticksSection.style.display = !isInstant ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Ticks operation - Durational + ticks enabled
            var ticksOpContainer = root.Q("TicksOpContainer");
            if (ticksOpContainer != null)
                ticksOpContainer.style.display = isDurational && enableTicks ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Tick interval - Infinite + ticks enabled
            var tickIntervalContainer = root.Q("TickIntervalContainer");
            if (tickIntervalContainer != null)
                tickIntervalContainer.style.display = isInfinite && enableTicks ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Execute ticks - non-Instant
            var executeTicksSection = root.Q("ExecuteTicksSection");
            if (executeTicksSection != null)
                executeTicksSection.style.display = !isInstant && (enableTicks || tickOnApp) ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Re-application - non-Instant
            var reappSection = root.Q("ReApplicationSection");
            if (reappSection != null)
                reappSection.style.display = !isInstant ? DisplayStyle.Flex : DisplayStyle.None;
            
            // Stacking - non-Instant and not AppendNewContainer
            var stackingSection = root.Q("StackingSection");
            if (stackingSection != null)
                stackingSection.style.display = !isInstant && reappPolicy == EEffectReApplicationPolicy.StackExistingContainers 
                    ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}