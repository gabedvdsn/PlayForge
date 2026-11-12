using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Testing
{
    /// <summary>
    /// What must hold after every test, whatever it did (TW-26). <see cref="ForgeTest"/> checks these after each test, so a
    /// test gets them without asking, and a test about something else still catches a leak: every observed entity's
    /// attribute parts sum to its value, what each dealt is what its targets received, and no process is left running.
    /// </summary>
    public sealed class Invariants
    {
        private const float Tolerance = 0.001f;

        private readonly List<IGameplayAbilitySystem> observed = new();
        private readonly Dictionary<(ISource, IAttribute), AttributeValue> dealt = new();
        private readonly Dictionary<(ISource, IAttribute), AttributeValue> received = new();

        /// <summary>
        /// Includes an entity in the attribute checks, listening for its frame summaries. The world observes each entity it
        /// makes unless told not to; observe one made elsewhere here.
        /// </summary>
        public void Observe(IGameplayAbilitySystem gas)
        {
            if (gas == null || observed.Contains(gas)) return;
            observed.Add(gas);
            gas.GetCallbacks().OnFrameComplete += summary => Tally(gas, summary);
        }

        private void Tally(IGameplayAbilitySystem gas, FrameSummarySnapshot summary)
        {
            foreach (var impact in summary.ImpactsDealt) Add(dealt, gas, impact);

            foreach (var impact in summary.ImpactsReceived)
            {
                var source = impact.SourcedModifier.Carrier?.GetOrigin()?.GetSource();
                if (source != null) Add(received, source, impact);
            }
        }

        private static void Add(Dictionary<(ISource, IAttribute), AttributeValue> tally, ISource source, ImpactData impact)
        {
            var key = (source, impact.Attribute);
            tally.TryGetValue(key, out var total);
            tally[key] = total + impact.RealImpact;
        }

        /// <summary>
        /// Every observed entity's attribution slices sum to its value, and what each dealt, read from its frame summaries,
        /// equals what its targets received from it. Totals are compared over the whole test, not each frame: a source and
        /// a target that end their frames apart can record one impact in neighbouring frames.
        /// </summary>
        public List<string> CheckAttributes()
        {
            var violations = new List<string>();

            foreach (var gas in observed)
            {
                var attributes = gas.GetAttributeSystem();
                if (attributes == null) continue;

                foreach (var attribute in attributes.GetDefinedAttributes())
                {
                    if (!attributes.TryGetValue(attribute, out var active)) continue;

                    AttributeValue sum = default;
                    foreach (var slice in attributes.GetBreakdown(attribute)) sum += slice.Value;

                    if (!Near(sum, active))
                        violations.Add($"{gas.GetName()}'s {attribute.GetName()} slices sum to {Show(sum)}, but its value is {Show(active)}");
                }
            }

            foreach (var key in AllKeys())
            {
                if (!observed.Contains(key.Item1 as IGameplayAbilitySystem)) continue;

                dealt.TryGetValue(key, out var gave);
                received.TryGetValue(key, out var got);

                if (!Near(gave, got))
                    violations.Add($"{key.Item1.GetName()} dealt {Show(gave)} to {key.Item2.GetName()}, but its targets received {Show(got)}");
            }

            return violations;
        }

        /// <summary>Control blocks still registered that weren't there when the world began.</summary>
        public List<string> CheckBlocks(ProcessControl control, ISet<int> baseline)
        {
            var violations = new List<string>();

            foreach (var (index, block) in control.FetchActiveProcesses())
            {
                if (baseline.Contains(index)) continue;
                violations.Add($"control block #{index} leaked: {block.Wrapper.ProcessName}, {block.State}, queued {block.QueuedState}");
            }

            return violations;
        }

        private HashSet<(ISource, IAttribute)> AllKeys()
        {
            var keys = new HashSet<(ISource, IAttribute)>(dealt.Keys);
            keys.UnionWith(received.Keys);
            return keys;
        }

        private static bool Near(AttributeValue a, AttributeValue b) =>
            Mathf.Abs(a.CurrentValue - b.CurrentValue) <= Tolerance && Mathf.Abs(a.BaseValue - b.BaseValue) <= Tolerance;

        private static string Show(AttributeValue value) => $"{value.CurrentValue}/{value.BaseValue}";
    }
}
