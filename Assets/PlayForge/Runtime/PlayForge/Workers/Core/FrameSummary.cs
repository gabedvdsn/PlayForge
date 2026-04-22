using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Captures all modifications and events that occurred during a frame for a single GAS.
    ///
    /// Impact tracking is split by perspective:
    ///   • Impact RECEIVED — indexed by ISource (who attacked me)
    ///   • Impact DEALT    — indexed by ITarget (who I attacked)
    ///
    /// A self-impact (source == target on the same system) is recorded on both sides.
    /// </summary>
    public class FrameSummary
    {
        // ═══════════════════════════════════════════════════════════════
        // IMPACT STORAGE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// All attribute impacts that involved this GAS this frame (dealt + received, in order).
        /// </summary>
        public List<ImpactData> Impacts { get; } = new();

        /// <summary>
        /// All impacts this GAS DEALT this frame (this GAS is the source).
        /// </summary>
        public List<ImpactData> ImpactsDealt { get; } = new();

        /// <summary>
        /// All impacts this GAS RECEIVED this frame (this GAS is the target).
        /// </summary>
        public List<ImpactData> ImpactsReceived { get; } = new();

        /// <summary>
        /// All impacts this GAS was involved in, indexed by the attribute they modified.
        /// </summary>
        public Dictionary<IAttribute, List<ImpactData>> ImpactsByAttribute { get; } = new();

        /// <summary>
        /// Impacts this GAS dealt, indexed by the ITarget they were dealt to.
        /// </summary>
        public Dictionary<ITarget, List<ImpactData>> ImpactsDealtByTarget { get; } = new();

        /// <summary>
        /// Impacts this GAS received, indexed by the ISource that dealt them.
        /// </summary>
        public Dictionary<ISource, List<ImpactData>> ImpactsReceivedBySource { get; } = new();

        // ═══════════════════════════════════════════════════════════════
        // NON-IMPACT STORAGE (unchanged)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Actions that were queued but invalidated before execution.</summary>
        public List<IRootAction> InvalidatedActions { get; } = new();

        /// <summary>Tag workers that activated this frame.</summary>
        public List<AbstractTagWorker> ActivatedTagWorkers { get; } = new();

        /// <summary>Tag workers that resolved (deactivated) this frame.</summary>
        public List<AbstractTagWorker> ResolvedTagWorkers { get; } = new();

        /// <summary>Effects that were applied this frame.</summary>
        public List<GameplayEffectSpec> AppliedEffects { get; } = new();

        /// <summary>Effects that were removed this frame.</summary>
        public List<GameplayEffect> RemovedEffects { get; } = new();

        /// <summary>GAS deaths this frame.</summary>
        public List<IGameplayAbilitySystem> Deaths { get; } = new();

        /// <summary>Ability injections this frame.</summary>
        public List<AbilityCallbackStatus> AbilityInjections { get; } = new();

        // ═══════════════════════════════════════════════════════════════
        // RECORDING METHODS — IMPACTS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Record an impact RECEIVED by this GAS (this GAS is the target).
        /// Keyed by the ISource that produced the impact.
        /// </summary>
        public void RecordImpactReceived(ImpactData impact)
        {
            Impacts.Add(impact);
            ImpactsReceived.Add(impact);

            if (!ImpactsByAttribute.TryGetValue(impact.Attribute, out var byAttr))
            {
                byAttr = new List<ImpactData>();
                ImpactsByAttribute[impact.Attribute] = byAttr;
            }
            byAttr.Add(impact);

            var source = impact.SourcedModifier.Derivation?.GetSource();
            if (source != null)
            {
                if (!ImpactsReceivedBySource.TryGetValue(source, out var list))
                {
                    list = new List<ImpactData>();
                    ImpactsReceivedBySource[source] = list;
                }
                list.Add(impact);
            }
        }

        /// <summary>
        /// Record an impact DEALT by this GAS (this GAS is the source).
        /// Keyed by the ITarget that received the impact.
        /// </summary>
        public void RecordImpactDealt(ImpactData impact)
        {
            Impacts.Add(impact);
            ImpactsDealt.Add(impact);

            if (!ImpactsByAttribute.TryGetValue(impact.Attribute, out var byAttr))
            {
                byAttr = new List<ImpactData>();
                ImpactsByAttribute[impact.Attribute] = byAttr;
            }
            byAttr.Add(impact);

            if (impact.Target != null)
            {
                if (!ImpactsDealtByTarget.TryGetValue(impact.Target, out var list))
                {
                    list = new List<ImpactData>();
                    ImpactsDealtByTarget[impact.Target] = list;
                }
                list.Add(impact);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // RECORDING METHODS — OTHER
        // ═══════════════════════════════════════════════════════════════

        public void RecordAbilityInjection(AbilityCallbackStatus status)
        {
            AbilityInjections.Add(status);
        }

        /// <summary>Record an invalidated action.</summary>
        public void RecordInvalidatedAction(IRootAction action) => InvalidatedActions.Add(action);

        /// <summary>Record a tag worker activation.</summary>
        public void RecordTagWorkerActivated(AbstractTagWorker worker) => ActivatedTagWorkers.Add(worker);

        /// <summary>Record a tag worker resolution.</summary>
        public void RecordTagWorkerResolved(AbstractTagWorker worker) => ResolvedTagWorkers.Add(worker);

        /// <summary>Record an effect application.</summary>
        public void RecordEffectApplied(GameplayEffectSpec spec) => AppliedEffects.Add(spec);

        /// <summary>Record an effect removal.</summary>
        public void RecordEffectRemoved(GameplayEffect effect) => RemovedEffects.Add(effect);

        public void RecordDeath(IGameplayAbilitySystem system) => Deaths.Add(system);

        // ═══════════════════════════════════════════════════════════════
        // QUERY HELPERS — PER-ATTRIBUTE TOTALS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Total magnitude of negative impacts (damage) to an attribute this frame.</summary>
        public float GetTotalReduction(IAttribute attribute)
        {
            if (!ImpactsByAttribute.TryGetValue(attribute, out var impacts)) return 0f;
            return impacts.Where(i => i.RealImpact.CurrentValue < 0).Sum(i => -i.RealImpact.CurrentValue);
        }

        /// <summary>Total magnitude of positive impacts (healing) to an attribute this frame.</summary>
        public float GetTotalIncrease(IAttribute attribute)
        {
            if (!ImpactsByAttribute.TryGetValue(attribute, out var impacts)) return 0f;
            return impacts.Where(i => i.RealImpact.CurrentValue > 0).Sum(i => i.RealImpact.CurrentValue);
        }

        /// <summary>Net (signed) impact to an attribute this frame.</summary>
        public float GetNetImpact(IAttribute attribute)
        {
            if (!ImpactsByAttribute.TryGetValue(attribute, out var impacts)) return 0f;
            return impacts.Sum(i => i.RealImpact.CurrentValue);
        }

        // ═══════════════════════════════════════════════════════════════
        // QUERY HELPERS — DEALT (this GAS is source)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>All impacts this GAS dealt to a specific target this frame.</summary>
        public IEnumerable<ImpactData> GetImpactsDealtTo(ITarget target)
        {
            return ImpactsDealtByTarget.TryGetValue(target, out var list)
                ? list
                : Enumerable.Empty<ImpactData>();
        }

        /// <summary>Total reduction (damage) this GAS dealt this frame, optionally filtered by attribute.</summary>
        public float GetTotalReductionDealt(IAttribute attribute = null)
        {
            var query = ImpactsDealt.Where(i => i.RealImpact.CurrentValue < 0);
            if (attribute != null) query = query.Where(i => i.Attribute.Equals(attribute));
            return query.Sum(i => -i.RealImpact.CurrentValue);
        }

        /// <summary>Total increase (healing) this GAS dealt this frame, optionally filtered by attribute.</summary>
        public float GetTotalIncreaseDealt(IAttribute attribute = null)
        {
            var query = ImpactsDealt.Where(i => i.RealImpact.CurrentValue > 0);
            if (attribute != null) query = query.Where(i => i.Attribute.Equals(attribute));
            return query.Sum(i => i.RealImpact.CurrentValue);
        }

        /// <summary>Unique targets this GAS dealt reductions (damage) to this frame.</summary>
        public IEnumerable<ITarget> GetReductionTargets()
        {
            return ImpactsDealt
                .Where(i => i.RealImpact.CurrentValue < 0 && i.Target != null)
                .Select(i => i.Target)
                .Distinct();
        }

        /// <summary>Unique targets this GAS dealt increases (healing) to this frame.</summary>
        public IEnumerable<ITarget> GetIncreaseTargets()
        {
            return ImpactsDealt
                .Where(i => i.RealImpact.CurrentValue > 0 && i.Target != null)
                .Select(i => i.Target)
                .Distinct();
        }

        // ═══════════════════════════════════════════════════════════════
        // QUERY HELPERS — RECEIVED (this GAS is target)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>All impacts this GAS received from a specific source this frame.</summary>
        public IEnumerable<ImpactData> GetImpactsReceivedFrom(ISource source)
        {
            return ImpactsReceivedBySource.TryGetValue(source, out var list)
                ? list
                : Enumerable.Empty<ImpactData>();
        }

        /// <summary>Total reduction (damage) this GAS received this frame, optionally filtered by attribute.</summary>
        public float GetTotalReductionReceived(IAttribute attribute = null)
        {
            var query = ImpactsReceived.Where(i => i.RealImpact.CurrentValue < 0);
            if (attribute != null) query = query.Where(i => i.Attribute.Equals(attribute));
            return query.Sum(i => -i.RealImpact.CurrentValue);
        }

        /// <summary>Total increase (healing) this GAS received this frame, optionally filtered by attribute.</summary>
        public float GetTotalIncreaseReceived(IAttribute attribute = null)
        {
            var query = ImpactsReceived.Where(i => i.RealImpact.CurrentValue > 0);
            if (attribute != null) query = query.Where(i => i.Attribute.Equals(attribute));
            return query.Sum(i => i.RealImpact.CurrentValue);
        }

        /// <summary>True if this GAS received any damage this frame, optionally filtered by attribute.</summary>
        public bool HasReductionReceived(IAttribute attribute = null)
        {
            return ImpactsReceived.Any(i =>
                i.RealImpact.CurrentValue < 0 &&
                (attribute == null || i.Attribute.Equals(attribute)));
        }

        /// <summary>True if this GAS received any healing this frame, optionally filtered by attribute.</summary>
        public bool HasIncreaseReceived(IAttribute attribute = null)
        {
            return ImpactsReceived.Any(i =>
                i.RealImpact.CurrentValue > 0 &&
                (attribute == null || i.Attribute.Equals(attribute)));
        }

        /// <summary>Unique sources that damaged this GAS this frame.</summary>
        public IEnumerable<ISource> GetReductionSources()
        {
            return ImpactsReceived
                .Where(i => i.RealImpact.CurrentValue < 0)
                .Select(i => i.SourcedModifier.Derivation?.GetSource())
                .Where(s => s != null)
                .Distinct();
        }

        /// <summary>Unique sources that healed this GAS this frame.</summary>
        public IEnumerable<ISource> GetIncreaseSources()
        {
            return ImpactsReceived
                .Where(i => i.RealImpact.CurrentValue > 0)
                .Select(i => i.SourcedModifier.Derivation?.GetSource())
                .Where(s => s != null)
                .Distinct();
        }

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Clear all recorded data for the next frame.</summary>
        public void Clear()
        {
            Impacts.Clear();
            ImpactsDealt.Clear();
            ImpactsReceived.Clear();
            ImpactsByAttribute.Clear();
            ImpactsDealtByTarget.Clear();
            ImpactsReceivedBySource.Clear();
            InvalidatedActions.Clear();
            ActivatedTagWorkers.Clear();
            ResolvedTagWorkers.Clear();
            AppliedEffects.Clear();
            RemovedEffects.Clear();
            Deaths.Clear();
            AbilityInjections.Clear();
        }

        /// <summary>Create a snapshot copy of the current state.</summary>
        public FrameSummarySnapshot CreateSnapshot(int executed)
        {
            return new FrameSummarySnapshot(this, executed);
        }
    }

    /// <summary>
    /// Immutable snapshot of a FrameSummary for passing to callbacks.
    /// </summary>
    public class FrameSummarySnapshot
    {
        public IReadOnlyList<ImpactData> Impacts { get; }
        public IReadOnlyList<ImpactData> ImpactsDealt { get; }
        public IReadOnlyList<ImpactData> ImpactsReceived { get; }
        public IReadOnlyList<IRootAction> InvalidatedActions { get; }
        public IReadOnlyList<AbstractTagWorker> ActivatedTagWorkers { get; }
        public IReadOnlyList<AbstractTagWorker> ResolvedTagWorkers { get; }

        public int Executed { get; }
        public int Invalidated { get; }

        public FrameSummarySnapshot(FrameSummary summary, int executed)
        {
            Impacts = summary.Impacts.ToList();
            ImpactsDealt = summary.ImpactsDealt.ToList();
            ImpactsReceived = summary.ImpactsReceived.ToList();
            InvalidatedActions = summary.InvalidatedActions.ToList();
            ActivatedTagWorkers = summary.ActivatedTagWorkers.ToList();
            ResolvedTagWorkers = summary.ResolvedTagWorkers.ToList();

            Executed = executed;
            Invalidated = summary.InvalidatedActions.Count;
        }
    }
}
