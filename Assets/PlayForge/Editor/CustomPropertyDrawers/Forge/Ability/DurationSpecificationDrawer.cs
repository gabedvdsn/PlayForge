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
            
            // Main container with themed styling
            //var mainBox = CreateMainContainer(Colors.AccentBlue);
            //container.Add(mainBox);
            
            // Header
            //var header = CreateHeader(Icons.Duration, "Duration Specification", Colors.AccentBlue);
            //mainBox.Add(header);
            
            // ═══════════════════════════════════════════════════════════════════
            // Policy Section
            // ═══════════════════════════════════════════════════════════════════
            var policySection = CreateSection("Policy", Colors.SectionBlue);
            container.Add(policySection);
            
            var durationPolicyProp = property.FindPropertyRelative("DurationPolicy");
            var durationPolicyField = new PropertyField(durationPolicyProp, "Duration Policy");
            durationPolicyField.style.marginBottom = 2;
            policySection.Add(durationPolicyField);
            
            var stackableField = new PropertyField(property.FindPropertyRelative("StackableType"), "Stackable Type");
            stackableField.style.marginBottom = 2;
            policySection.Add(stackableField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Duration Section (conditional)
            // ═══════════════════════════════════════════════════════════════════
            var durationSection = CreateSection("Duration", Colors.SectionGreen);
            container.Add(durationSection);
            
            // Duration row (value + calculation)
            var durationRow = new VisualElement();
            durationRow.style.flexDirection = FlexDirection.Row;
            durationRow.style.marginBottom = 4;
            
            var durationValueField = new PropertyField(property.FindPropertyRelative("Duration"), "Base Duration");
            durationValueField.style.flexGrow = 1;
            durationValueField.style.marginRight = 4;
            durationRow.Add(durationValueField);
            
            durationSection.Add(durationRow);
            
            var durationCalcOpField = new PropertyField(property.FindPropertyRelative("DurationCalculationOperation"), "Calculation Mode");
            durationCalcOpField.style.marginBottom = 2;
            durationSection.Add(durationCalcOpField);
            
            var durationCalcField = new PropertyField(property.FindPropertyRelative("DurationCalculation"), "Duration Modifier");
            durationCalcField.style.marginBottom = 4;
            durationSection.Add(durationCalcField);
            
            var deltaTimeField = new PropertyField(property.FindPropertyRelative("DeltaTimeSource"), "Delta Time Source");
            durationSection.Add(deltaTimeField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Ticks Section (conditional)
            // ═══════════════════════════════════════════════════════════════════
            var ticksSection = CreateSection("Ticks", Colors.SectionOrange);
            container.Add(ticksSection);
            
            var ticksRow = new VisualElement();
            ticksRow.style.flexDirection = FlexDirection.Row;
            ticksRow.style.marginBottom = 4;
            
            var ticksValueField = new PropertyField(property.FindPropertyRelative("Ticks"), "Ticks");
            ticksValueField.style.flexGrow = 1;
            ticksValueField.style.marginRight = 4;
            ticksRow.Add(ticksValueField);
            
            var ticksIntervalValueField = new PropertyField(property.FindPropertyRelative("TickInterval"), "Tick Interval");
            ticksIntervalValueField.style.flexGrow = 1;
            ticksIntervalValueField.style.marginRight = 4;
            ticksRow.Add(ticksIntervalValueField);
            
            var roundingField = new PropertyField(property.FindPropertyRelative("Rounding"), "Rounding");
            roundingField.style.minWidth = 100;
            ticksRow.Add(roundingField);
            
            ticksSection.Add(ticksRow);
            
            var tickCalcOpField = new PropertyField(property.FindPropertyRelative("TickCalculationOperation"), "Calculation Mode");
            tickCalcOpField.style.marginBottom = 2;
            ticksSection.Add(tickCalcOpField);
            
            var tickCalcField = new PropertyField(property.FindPropertyRelative("TickCalculation"), "Tick Modifier");
            ticksSection.Add(tickCalcField);
            
            var tickOnAppField = new PropertyField(property.FindPropertyRelative("TickOnApplication"), "Tick On Application");
            tickOnAppField.tooltip = "Naturally increases number of Ticks by 1";
            ticksSection.Add(tickOnAppField);
            
            // ═══════════════════════════════════════════════════════════════════
            // Conditional Visibility
            // ═══════════════════════════════════════════════════════════════════
            void UpdateVisibility()
            {
                var policy = (EEffectDurationPolicy)durationPolicyProp.enumValueIndex;

                stackableField.style.display = policy == EEffectDurationPolicy.Instant ? DisplayStyle.None : DisplayStyle.Flex;
                
                bool showDuration = policy == EEffectDurationPolicy.Durational;
                bool showTicks = policy != EEffectDurationPolicy.Instant;

                ticksValueField.style.display = policy != EEffectDurationPolicy.Infinite ? DisplayStyle.Flex : DisplayStyle.None;
                ticksIntervalValueField.style.display = policy == EEffectDurationPolicy.Infinite ? DisplayStyle.Flex : DisplayStyle.None;
                roundingField.style.display = policy == EEffectDurationPolicy.Infinite ? DisplayStyle.None : DisplayStyle.Flex;
                
                durationSection.style.display = showDuration ? DisplayStyle.Flex : DisplayStyle.None;
                ticksSection.style.display = showTicks ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            // Initial visibility
            container.schedule.Execute(UpdateVisibility).StartingIn(50);
            
            // Update on policy change
            durationPolicyField.RegisterValueChangeCallback(_ => UpdateVisibility());
            
            return container;
        }
    }
}