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
        // Lists handed out for the by-attribute and by-source groupings, kept across frames so a commit
        // doesn't allocate fresh ones every frame (PF-007)
        private readonly Stack<List<ImpactData>> _spareLists = new();

        private List<ImpactData> RentList() => _spareLists.Count > 0 ? _spareLists.Pop() : new List<ImpactData>();

        private void ReturnLists<TKey>(Dictionary<TKey, List<ImpactData>> groups)
        {
            foreach (var list in groups.Values)
            {
                list.Clear();
                _spareLists.Push(list);
            }
            groups.Clear();
        }

        public void RecordImpactReceived(ImpactData impact)
        {
            Impacts.Add(impact);
            ImpactsReceived.Add(impact);

            if (!ImpactsByAttribute.TryGetValue(impact.Attribute, out var byAttr))
            {
                byAttr = RentList();
                ImpactsByAttribute[impact.Attribute] = byAttr;
            }
            byAttr.Add(impact);

            var source = impact.SourcedModifier.Carrier?.GetOrigin().GetSource();
            if (source != null)
            {
                if (!ImpactsReceivedBySource.TryGetValue(source, out var list))
                {
                    list = RentList();
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
                byAttr = RentList();
                ImpactsByAttribute[impact.Attribute] = byAttr;
            }
            byAttr.Add(impact);

            if (impact.Target != null)
            {
                if (!ImpactsDealtByTarget.TryGetValue(impact.Target, out var list))
                {
                    list = RentList();
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
                .Select(i => i.SourcedModifier.Carrier?.GetOrigin().GetSource())
                .Where(s => s != null)
                .Distinct();
        }

        /// <summary>Unique sources that healed this GAS this frame.</summary>
        public IEnumerable<ISource> GetIncreaseSources()
        {
            return ImpactsReceived
                .Where(i => i.RealImpact.CurrentValue > 0)
                .Select(i => i.SourcedModifier.Carrier?.GetOrigin().GetSource())
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
            ReturnLists(ImpactsByAttribute);
            ReturnLists(ImpactsDealtByTarget);
            ReturnLists(ImpactsReceivedBySource);
            InvalidatedActions.Clear();
            ActivatedTagWorkers.Clear();
            ResolvedTagWorkers.Clear();
            AppliedEffects.Clear();
            RemovedEffects.Clear();
            Deaths.Clear();
            AbilityInjections.Clear();
        }

        /// <summary>A snapshot copy of the current state, made for the caller to keep.</summary>
        public FrameSummarySnapshot CreateSnapshot(int executed)
        {
            return new FrameSummarySnapshot(this, executed);
        }

        private FrameSummarySnapshot _listenersSnapshot;

        /// <summary>
        /// The snapshot this frame's listeners read: one per summary, refilled each frame, so it holds this frame only
        /// during the call. A new one each frame copied ten lists, 504 bytes a frame for an entity anyone listened to
        /// (GAS-P10).
        /// </summary>
        internal FrameSummarySnapshot TakeSnapshot(int executed)
        {
            _listenersSnapshot ??= new FrameSummarySnapshot();
            _listenersSnapshot.Fill(this, executed);
            return _listenersSnapshot;
        }
    }

    /// <summary>
    /// A frame's summary as its listeners read it. The one <see cref="GameplayAbilitySystemCallbacks.OnFrameComplete"/>
    /// hands a listener is its entity's own and is refilled each frame, so it holds that frame only during the call
    /// (GAS-P10). <see cref="FrameSummary.CreateSnapshot"/> makes one to keep.
    /// </summary>
    public class FrameSummarySnapshot
    {
        private readonly List<ImpactData> _impacts = new();
        private readonly List<ImpactData> _impactsDealt = new();
        private readonly List<ImpactData> _impactsReceived = new();
        private readonly List<IRootAction> _invalidatedActions = new();
        private readonly List<AbstractTagWorker> _activatedTagWorkers = new();
        private readonly List<AbstractTagWorker> _resolvedTagWorkers = new();
        private readonly List<GameplayEffectSpec> _appliedEffects = new();
        private readonly List<GameplayEffect> _removedEffects = new();
        private readonly List<IGameplayAbilitySystem> _deaths = new();
        private readonly List<AbilityCallbackStatus> _abilityInjections = new();

        public IReadOnlyList<ImpactData> Impacts => _impacts;
        public IReadOnlyList<ImpactData> ImpactsDealt => _impactsDealt;
        public IReadOnlyList<ImpactData> ImpactsReceived => _impactsReceived;
        public IReadOnlyList<IRootAction> InvalidatedActions => _invalidatedActions;
        public IReadOnlyList<AbstractTagWorker> ActivatedTagWorkers => _activatedTagWorkers;
        public IReadOnlyList<AbstractTagWorker> ResolvedTagWorkers => _resolvedTagWorkers;
        public IReadOnlyList<GameplayEffectSpec> AppliedEffects => _appliedEffects;
        public IReadOnlyList<GameplayEffect> RemovedEffects => _removedEffects;
        public IReadOnlyList<IGameplayAbilitySystem> Deaths => _deaths;
        public IReadOnlyList<AbilityCallbackStatus> AbilityInjections => _abilityInjections;

        public int Executed { get; private set; }
        public int Invalidated { get; private set; }

        internal FrameSummarySnapshot() { }

        public FrameSummarySnapshot(FrameSummary summary, int executed) => Fill(summary, executed);

        /// <summary>Takes the summary's state as it is now, into lists this snapshot keeps from frame to frame.</summary>
        internal void Fill(FrameSummary summary, int executed)
        {
            Refill(_impacts, summary.Impacts);
            Refill(_impactsDealt, summary.ImpactsDealt);
            Refill(_impactsReceived, summary.ImpactsReceived);
            Refill(_invalidatedActions, summary.InvalidatedActions);
            Refill(_activatedTagWorkers, summary.ActivatedTagWorkers);
            Refill(_resolvedTagWorkers, summary.ResolvedTagWorkers);

            // The rest of what the frame recorded, which FrameComplete listeners never saw (GAS-39)
            Refill(_appliedEffects, summary.AppliedEffects);
            Refill(_removedEffects, summary.RemovedEffects);
            Refill(_deaths, summary.Deaths);
            Refill(_abilityInjections, summary.AbilityInjections);

            Executed = executed;
            Invalidated = summary.InvalidatedActions.Count;
        }

        private static void Refill<T>(List<T> into, List<T> from)
        {
            into.Clear();
            into.AddRange(from);
        }
    }
}
