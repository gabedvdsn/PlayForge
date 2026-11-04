using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An effect's duration, in Docs/EDITOR_STYLE.md's terms and the Effect mockup's names: Policy, then what the policy
    /// uses: Duration and its ticks for a Durational effect, Tick Every for an Infinite one, then Tick On Application,
    /// execute ticks, the clock, Re-application and Then, and stacking for Stack Existing. It drew its parts in tinted
    /// boxes with colored headings, under sentences that explained them, and showed or hid them 50 ms after it drew
    /// (ED-37). It follows the policy as it changes, an Undo among them.
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayEffectDuration))]
    public class GameplayEffectDurationDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "DurationRoot" };
            ForgeTheme.Apply(root);

            SerializedProperty Prop(string field) => property.FindPropertyRelative(field);
            var policyProp = Prop(nameof(GameplayEffectDuration.DurationPolicy));
            var periodicProp = Prop(nameof(GameplayEffectDuration.EnablePeriodicTicks));
            var onApplyProp = Prop(nameof(GameplayEffectDuration.TickOnApplication));
            var reapplyProp = Prop(nameof(GameplayEffectDuration.ReApplicationPolicy));

            PropertyField Field(string field, string label, string name, string tooltip)
            {
                var drawn = new PropertyField(Prop(field), label) { name = name, tooltip = tooltip };
                root.Add(drawn);
                return drawn;
            }

            Toggle Switch(SerializedProperty bound, string label, string name, string tooltip)
            {
                var toggle = new Toggle(label) { name = name, tooltip = tooltip };
                toggle.AddToClassList(Toggle.alignedFieldUssClassName);
                toggle.BindProperty(bound);
                root.Add(toggle);
                return toggle;
            }

            Field(nameof(GameplayEffectDuration.DurationPolicy), "Policy", "Policy",
                "Instant makes its change once. Durational lasts a set time, and Infinite until something removes it");
            var duration = Field(nameof(GameplayEffectDuration.DurationOperation), "Duration", "Duration",
                "How long the effect lasts, in seconds");
            var periodic = Switch(periodicProp, "Periodic Ticks", "PeriodicTicks", "Make the effect's change again on each tick");
            var ticks = Field(nameof(GameplayEffectDuration.TicksOperation), "Ticks", "Ticks",
                "How many ticks fit in the duration, evenly spaced; the last falls as it ends");
            ticks.AddToClassList("pf-dependent");
            var tickEvery = Field(nameof(GameplayEffectDuration.TickIntervalOperation), "Tick Every", "TickEvery",
                "The seconds between ticks");
            tickEvery.AddToClassList("pf-dependent");
            var onApply = Switch(onApplyProp, "Tick On Application", "TickOnApplication",
                "Tick once as the effect is applied, before its first periodic tick");
            var executions = Field(nameof(GameplayEffectDuration.AdditionalExecuteTicksOperation), "Additional Executions",
                "AdditionalExecutions", "How many more times each tick makes the change, past the container's own count");
            var realDelta = Field(nameof(GameplayEffectDuration.RealDeltaTime), "Real Delta Time", "RealDeltaTime",
                "How the clock's scaler combines with the frame time");
            var deltaScaler = Field(nameof(GameplayEffectDuration.DeltaTimeScaler), "Delta Time Scaler", "DeltaTimeScaler",
                "Runs the effect's clock faster or slower than the frame time");
            var reapply = Field(nameof(GameplayEffectDuration.ReApplicationPolicy), "Re-application", "ReApplication",
                "What happens when the effect is applied to a target that has it already");
            var then = Field(nameof(GameplayEffectDuration.ReApplicationInteraction), "Then", "Then",
                "What the effect that's there does with its time as it takes the new application in");
            then.AddToClassList("pf-dependent");
            var stacking = Field(nameof(GameplayEffectDuration.StackPolicy), "Stacking", "Stacking",
                "How the stacks keep their time");
            var behaviours = Field(nameof(GameplayEffectDuration.StackingConfigs), "Stack Behaviours", "StackBehaviours",
                "Rules the stacks follow, such as bounds on their count");
            var amount = Field(nameof(GameplayEffectDuration.StackAmountOperation), "Stack Amount", "StackAmount",
                "How many stacks each application adds");

            void Show(VisualElement element, bool shown) =>
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;

            void ShowPolicy()
            {
                var policy = (EEffectDurationPolicy)policyProp.intValue;
                bool lasting = policy != EEffectDurationPolicy.Instant;
                bool durational = policy == EEffectDurationPolicy.Durational;
                bool ticking = periodicProp.boolValue;
                var reapplication = (EEffectReApplicationPolicy)reapplyProp.intValue;

                Show(duration, durational);
                Show(periodic, lasting);
                Show(ticks, durational && ticking);
                Show(tickEvery, policy == EEffectDurationPolicy.Infinite && ticking);
                Show(onApply, lasting);
                Show(executions, lasting && (ticking || onApplyProp.boolValue));
                Show(realDelta, durational);
                Show(deltaScaler, durational);
                Show(reapply, lasting);
                // Then applies as the effect there takes the application in, so with every choice but Add Another
                Show(then, lasting && reapplication != EEffectReApplicationPolicy.AppendNewContainer);
                bool stacks = lasting && reapplication == EEffectReApplicationPolicy.StackExistingContainers;
                Show(stacking, stacks);
                Show(behaviours, stacks);
                Show(amount, stacks);
            }

            root.TrackSerializedObjectValue(property.serializedObject, _ => ShowPolicy());
            ShowPolicy();
            return root;
        }
    }
}
