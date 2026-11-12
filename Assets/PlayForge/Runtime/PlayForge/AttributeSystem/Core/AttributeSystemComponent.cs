using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Component for managing attributes and their modifications.
    /// Refactored to support WorkerContext and deferred execution.
    /// </summary>
    public class AttributeSystemComponent : DeferredContextSystem
    {
        protected AttributeSet attributeSet;
        protected List<AbstractAttributeWorker> attributeChangeWorkers = new();
        
        private AttributeChangeMomentHandler PreChangeHandler = new();
        private AttributeChangeMomentHandler PostChangeHandler = new();
        
        private Dictionary<IAttribute, CachedAttributeValue> AttributeCache;
        private AttributeRegulationCache RegulationCache;
        
        public AttributeSystemCallbacks Callbacks;
        
        public readonly IGameplayAbilitySystem Self;
        
        public AttributeSystemComponent(IGameplayAbilitySystem self)
        {
            Self = self;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        public virtual void Setup(AttributeSet attrSet)
        {
            attributeSet = attrSet;
            attributeChangeWorkers = new List<AbstractAttributeWorker>();
            
            SetupCaches();
            SetupWorkers();
        }
        
        private void SetupCaches()
        {
            AttributeCache = new Dictionary<IAttribute, CachedAttributeValue>();
            RegulationCache = new AttributeRegulationCache();
            Callbacks = new AttributeSystemCallbacks();
        }
        
        private void SetupWorkers()
        {
            PreChangeHandler = new AttributeChangeMomentHandler();
            PostChangeHandler = new AttributeChangeMomentHandler();
            
            foreach (var worker in attributeChangeWorkers)
            {
                worker.RegisterWithHandler(PreChangeHandler, PostChangeHandler);
            }
        }

        public void Initialize()
        {
            InitializeAttributeSets();

            // Initial re-calculation after init
            foreach (var attr in RegulationCache.GetAllContactAttributes())
            {
                RefreshRegulatedAttributes(attr);
            }
            
            // Subscribe refresh to attribute change callback
            Callbacks.OnAttributeChanged += data =>
            {
                RefreshRegulatedAttributes(data.Attribute);
            };
        }
        
        private void InitializeAttributeSets()
        {
            if (attributeSet == null) return;
            
            ProvideAttributeSet(attributeSet);
        }

        public void ProvideAttributeSet(AttributeSet set)
        {
            set.Initialize(this);
            Self.GetTagCache()?.AddTag(set.AssetTag);
    
            // Phase 2: Initialize with values (now all attributes exist in cache)
            var failedAttributes = new List<IAttribute>();
            foreach (var attr in AttributeCache.Select(elem => elem.Key))
            {
                if (!AttributeCache[attr].Initialize(attr, Self, AttributeCache))
                {
                    failedAttributes.Add(attr);
                }
            }
    
            // Remove failed
            foreach (var attr in failedAttributes)
            {
                RemoveAttribute(attr);
            }
            
            // Phase 3: Fire initial modification events
            foreach (var attr in AttributeCache.Select(elem => elem.Key))
            {
                ModifyAttribute(SourcedModifiedAttributeValue.GenerateUnworkable(Self, attr, 0f, 0f), false);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // WORKER MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════════
        
        public bool ProvideWorker(AbstractAttributeWorker worker)
        {
            return worker.RegisterWithHandler(PreChangeHandler, PostChangeHandler);
        }
        
        public bool RemoveWorker(AbstractAttributeWorker worker)
        {
            return worker.DeRegisterFromHandler(PreChangeHandler, PostChangeHandler);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ATTRIBUTE MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════════
        
        public void ProvideAttribute(IAttribute attribute, AttributeBlueprint blueprint)
        {
            if (AttributeCache.ContainsKey(attribute)) return;
            
            AttributeCache[attribute] = CachedAttributeValue.Generate(blueprint);
            // Regulate the Base scaler always; regulate Current's scaler only when it isn't
            // linked-to-Base (otherwise we'd register the same scaler instance twice for the
            // same owning attribute).
            //
            // Every combined part's too, so one scaled from another attribute refreshes with it (GAS-27)
            foreach (var part in blueprint.Parts)
            {
                part.SetElement.Base?.Scaling?.RegulateContactWith(attribute, RegulationCache);
                if (!part.SetElement.LinkCurrentToBase)
                    part.SetElement.Current?.Scaling?.RegulateContactWith(attribute, RegulationCache);
            }
            
            AttributeRegistry.Add(attribute);
            Callbacks.AttributeRegister(attribute);
        }

        public void ProvideAttribute(IAttribute attribute, AttributeValue value)
        {
            if (AttributeCache.ContainsKey(attribute)) return;
            
            AttributeCache[attribute] = CachedAttributeValue.GenerateGeneric(attribute, Self, AttributeCache, Tags.IgnoreRetention, value);
            AttributeRegistry.Add(attribute);
            Callbacks.AttributeRegister(attribute);
        }

        public void RemoveAttribute(IAttribute attribute)
        {
            if (!AttributeCache.ContainsKey(attribute)) return;

            AttributeCache.Remove(attribute);
            Callbacks.AttributeUnregister(attribute);
        }
        
        public bool DefinesAttribute(IAttribute attribute) 
            => attribute == null || AttributeCache.ContainsKey(attribute);

        public bool TryGetCachedValue(IAttribute attribute, out CachedAttributeValue attributeValue)
        {
            if (attribute is not null && AttributeCache.TryGetValue(attribute, out attributeValue)) return true;
            
            attributeValue = default;
            return false;
        }
        
        public bool TryGetValue(IAttribute attribute, out AttributeValue attributeValue)
        {
            if (attribute is not null && AttributeCache.TryGetValue(attribute, out var cachedValue))
            {
                attributeValue = cachedValue.ActiveValue;
                return true;
            }

            attributeValue = default;
            return false;
        }

        // ─── Per-group attribution surface (IMPACT_MODEL_REFACTOR.md §2.3) ─────────
        // Groups partition an attribute's pool for UX/consumer queries (base vs bonus
        // health, base vs bonus damage). Slices always sum exactly to ActiveValue.

        /// <summary>This group's slice of the attribute (committed history + attributed modifier effect).</summary>
        public bool TryGetGroupValue(IAttribute attribute, Tag group, out AttributeValue value)
        {
            if (attribute is not null && AttributeCache.TryGetValue(attribute, out var cached))
            {
                value = cached.GetGroupValue(group);
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Ordered per-group slices of the attribute's ActiveValue.</summary>
        public IReadOnlyList<GroupContribution> GetBreakdown(IAttribute attribute)
        {
            if (attribute is not null && AttributeCache.TryGetValue(attribute, out var cached))
            {
                return cached.GetBreakdown();
            }

            return System.Array.Empty<GroupContribution>();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ATTRIBUTE MODIFICATION — the four-verb surface (IMPACT_MODEL_REFACTOR.md §2.4)
        //
        //   RegisterModifier   — Continuous entries; transient, derived, auto-reversing.
        //   UnregisterModifier — the ONLY reversal mechanism. Runs through the same
        //                        pipeline as every other change, so workers, callbacks and
        //                        the regulation refresh all observe removals (fixes the
        //                        stale-derived-attribute bug).
        //   CommitImpact       — OnApply/Periodic entries; permanent per-group history.
        //   ApplyDelta         — synthetic writes (init pulse, auto-scale propagation,
        //                        external systems) with a precomputed (current, base) delta.
        //
        // All four run the identical 4-phase machinery (pre-workers → mutate →
        // post-workers → impact relay); they differ only in the PHASE-2 dispatch.
        // ═══════════════════════════════════════════════════════════════════════════

        private enum ECommitMode
        {
            RegisterModifier,
            UnregisterModifier,
            CommitImpact,
        }

        public ImpactData RegisterModifier(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, IReadOnlyList<IAttributeImpacter> impacters, bool runEvents = true)
            => CommitWithCarrier(attribute, sourcedModifiedValue, runEvents, ECommitMode.RegisterModifier, impacters);

        public ImpactData UnregisterModifier(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
            => CommitWithCarrier(attribute, sourcedModifiedValue, runEvents, ECommitMode.UnregisterModifier, null);

        public ImpactData CommitImpact(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, IReadOnlyList<IAttributeImpacter> impacters, bool runEvents = true)
            => CommitWithCarrier(attribute, sourcedModifiedValue, runEvents, ECommitMode.CommitImpact, impacters);

        // ─── ApplyDelta — synthetic/direct writes ────────────────────────────────
        // `ModifyAttribute` is the historical public name; semantically this is the
        // ApplyDelta verb: a permanent commit of a precomputed (current, base) delta or
        // of the carrier's own impacters. Gameplay-effect specs never route here — the
        // effect lifecycle uses Register/Unregister/Commit with per-entry impacters.

        public ImpactData ModifyAttribute(SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            return ModifyAttribute(sourcedModifiedValue.Carrier.GetAttribute(), sourcedModifiedValue, runEvents);
        }

        public ImpactData ModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
            => CommitWithCarrier(attribute, sourcedModifiedValue, runEvents, ECommitMode.CommitImpact, null);

        /// <summary>
        /// The shared 4-phase pipeline. Every attribute mutation — registration,
        /// removal, commit, synthetic delta — funnels through here so worker phases,
        /// callbacks, frame-impact relays and the regulation refresh behave identically
        /// regardless of what kind of change occurred.
        /// </summary>
        private ImpactData CommitWithCarrier(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents, ECommitMode mode, IReadOnlyList<IAttributeImpacter> impacters)
        {
            // Null attribute = carrier with no attribute impact (e.g. a pure tag-granting
            // effect). Nothing to commit — Dictionary.ContainsKey(null) would throw.
            if (attribute is null || !AttributeCache.ContainsKey(attribute)) return default;

            var cached = AttributeCache[attribute];
            var scratch = RentScratch(sourcedModifiedValue);
            var change = scratch.Change;
            var ctx = new WorkerContext(Self, AttributeCache, change, _frameSummary, _actionQueue);

            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 0: A commit proposes its incoming delta.
            //
            // Carriers with no impacters (hand-rolled delta callsites — EnforceScaling
            // propagation, the synthetic zero-impact init pulse, third-party wrappers)
            // commit their precomputed (CurrentValue, BaseValue) delta as constant Adds,
            // so every path flows through the same impacter machinery (bounds + rounding
            // + attribution). The Adds are frozen in the commit's own impacters; they came
            // in a SourceAttributeImpact made to wrap them, on every raw delta (GAS-P8).
            //
            // The impacters are evaluated ONCE, frozen as constants, and previewed: the
            // pre-change phase sees the delta they propose (effect specs arrive as (0,0)
            // transport) and may change it. Unconstrained, so armor acts on incoming
            // damage and the clamp applies after.
            //
            // Register/Unregister keep the transport value. Modifiers are re-derived on
            // every recompute, so a one-off pre-change adjustment has nowhere to live.
            // ═══════════════════════════════════════════════════════════════════════
            var carrier = sourcedModifiedValue.Carrier;
            IReadOnlyList<IAttributeImpacter> commitImpacters = null;
            AttributeValue incoming = default;
            if (mode == ECommitMode.CommitImpact)
            {
                // Delta-only carriers take this path for the COMMIT only. Register/Unregister
                // are identity-based — substituting a synthetic wrapper would register
                // under the wrong identity and make later unregistration miss.
                if (carrier == null || carrier.GetImpacters().Count == 0)
                {
                    var group = sourcedModifiedValue.Carrier?.GetRetentionGroup() ?? Tags.IgnoreRetention;
                    if (impacters == null)
                    {
                        // The delta, as the wrapper's Adds were. The commit only checks that it has a carrier and
                        // evaluates through it, which a constant ignores, so a change with none commits through the root
                        scratch.FreezeDelta(sourcedModifiedValue.ToAttributeValue(), group);
                        carrier ??= cached.Root;
                    }
                    else
                    {
                        // Impacters handed to a carrier with none of its own evaluate through a wrapper, as they did
                        carrier = WrapDelta(attribute, sourcedModifiedValue, sourcedModifiedValue.ToAttributeValue(), group);
                        scratch.Freeze(carrier, impacters);
                    }
                }
                else
                {
                    scratch.Freeze(carrier, impacters ?? carrier.GetImpacters());
                }
                commitImpacters = scratch.Frozen;
                incoming = cached.PreviewCommit(carrier, commitImpacters);
                change.Value = new SourcedModifiedAttributeValue(sourcedModifiedValue,
                    incoming.CurrentValue, incoming.BaseValue, sourcedModifiedValue.ImpactIsWorkable);
            }

            Callbacks.AttributePreChange(attribute, change);

            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 1: Pre-change workers
            // ═══════════════════════════════════════════════════════════════════════
            if (runEvents) PreChangeHandler.RunWorkers(ctx);

            // A barrier takes a workable loss first, as much as it holds (TW-12)
            if (mode == ECommitMode.CommitImpact && sourcedModifiedValue.ImpactIsWorkable && change.Value.CurrentValue < 0f)
                SoakIntoBarriers(attribute, change);

            // A workable gain to its current, multiplied by the attribute it names, as Healing Received scales a heal and 0
            // blocks it (TW-15)
            if (mode == ECommitMode.CommitImpact && sourcedModifiedValue.ImpactIsWorkable && change.Value.CurrentValue > 0f
                && attribute is Attribute { GainsScaledBy: { } scaledBy } && AttributeCache.TryGetValue(scaledBy, out var scale))
                change.Value = new SourcedModifiedAttributeValue(change.Value,
                    change.Value.CurrentValue * Mathf.Max(0f, scale.ActiveValue.CurrentValue), change.Value.BaseValue, change.Value.ImpactIsWorkable);

            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 2: Dispatch per mode.
            // ═══════════════════════════════════════════════════════════════════════
            var holdValue = cached.ActiveValue;

            switch (mode)
            {
                case ECommitMode.RegisterModifier:
                    cached.RegisterModifier(carrier, impacters ?? carrier.GetImpacters(), AttributeCache);
                    break;
                case ECommitMode.UnregisterModifier:
                    cached.UnregisterModifier(carrier, AttributeCache);
                    break;
                case ECommitMode.CommitImpact:
                    // Untouched, the frozen impacters commit as authored (op semantics and
                    // per-impacter groups intact). Adjusted, the new delta commits as one
                    // constant Add in the impacters' group. Either way CachedAttributeValue
                    // records the post-constraint delta and recomputes.
                    //
                    // The Adds are frozen in the commit's own impacters. They came in a carrier made to wrap them, which
                    // only the commit read, on every commit a worker adjusted (GAS-P6)
                    var adjusted = change.Value.ToAttributeValue();
                    if (adjusted != incoming)
                    {
                        scratch.FreezeDelta(adjusted, AdjustedCommitGroup(commitImpacters, sourcedModifiedValue.Carrier));
                    }
                    cached.CommitImpact(carrier, commitImpacters, AttributeCache);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            change.Override(AttributeCache[attribute].ActiveValue - holdValue);

            if (runEvents) AttributeCache[attribute].EnforceScaling(ctx);

            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 3: Post-change workers
            // ═══════════════════════════════════════════════════════════════════════
            if (runEvents) PostChangeHandler.RunWorkers(ctx);

            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 4: Calculate real impact and relay
            // ═══════════════════════════════════════════════════════════════════════
            change.Override(AttributeCache[attribute].ActiveValue - holdValue);
            Callbacks.AttributePostChange(attribute, change);

            var impactData = ImpactData.Generate(
                Self,
                attribute,
                sourcedModifiedValue,
                change.Value.ToAttributeValue(), holdValue);

            // Record RECEIVED impact on this (target) GAS — fires target-side callbacks
            Self.RecordFrameImpactReceived(impactData);

            // Notify source system — source records DEALT impact + runs source-side workers/callbacks
            if ((sourcedModifiedValue.Carrier?.GetOrigin()?.GetSource()?.FindAbilitySystem(out var abilSystem) ?? false))
            {
                abilSystem.ProvideFrameImpactDealt(impactData);
            }

            Callbacks.AttributeChanged(impactData);

            ReturnScratch(scratch);
            return impactData;
        }

        /// <summary>A synthetic carrier committing <paramref name="delta"/> as constant Adds, attributed to <paramref name="group"/>.</summary>
        private SourceAttributeImpact WrapDelta(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, AttributeValue delta, Tag group)
        {
            return SourceAttributeImpact.FromAdditiveDelta(
                sourcedModifiedValue.Carrier?.GetOrigin()?.GetSource() ?? Self,
                attribute,
                delta.CurrentValue,
                delta.BaseValue,
                group,
                sourcedModifiedValue.Carrier?.GetImpactTypes(),
                sourcedModifiedValue.Carrier,
                sourcedModifiedValue.Carrier?.RetainImpact() ?? false);
        }

        // ─── A commit's scratch (GAS-P6) ──────────────────────────────────────────
        // A commit made a new change, a new list, and a constant impacter for each of its impacters, so every tick that
        // committed allocated. A commit now rents what it works with and gives it back, one set for each commit in
        // progress, since a worker or a listener can commit inside another

        private readonly Stack<CommitScratch> spareScratch = new();

        private CommitScratch RentScratch(SourcedModifiedAttributeValue value)
        {
            var scratch = spareScratch.Count > 0 ? spareScratch.Pop() : new CommitScratch();
            scratch.Change.Value = value;
            return scratch;
        }

        private void ReturnScratch(CommitScratch scratch)
        {
            scratch.Release();
            spareScratch.Push(scratch);
        }

        /// <summary>What one commit works with: its change, and its impacters frozen.</summary>
        private sealed class CommitScratch
        {
            public ChangeValue Change = new(default);

            /// <summary>The commit's impacters, each frozen at the magnitude it has now.</summary>
            public readonly List<IAttributeImpacter> Frozen = new();

            // Frozen's impacters, kept by place: the n-th one frozen is always the n-th made
            private readonly List<FrozenImpacter> made = new();

            /// <summary>
            /// Evaluates each impacter once and freezes it at that magnitude, so the delta the pre-change phase sees is
            /// the delta that commits, even for a randomized scaler.
            /// </summary>
            public void Freeze(IAttributeImpactCarrier carrier, IReadOnlyList<IAttributeImpacter> impacters)
            {
                Frozen.Clear();

                // By index: a foreach through the interface boxed the list's enumerator
                for (int i = 0; i < impacters.Count; i++)
                {
                    var imp = impacters[i];
                    if (imp == null) continue;

                    float magnitude = imp.Evaluate(carrier);
                    // Through its calculation, once for the hit, so a roll in it is drawn once (TW-2)
                    if (imp.Pipeline != null) magnitude = imp.Pipeline.Run(magnitude, carrier, (carrier as GameplayEffectSpec)?.TakeBreakdown());
                    // Then its holders' modifiers' stages, in the same breakdown (TW-7)
                    if (imp.Stages != null && imp.Operation != ECalculationOperation.Override)
                        magnitude = ValuePipeline.RunAfter(imp.Stages, magnitude, carrier, (carrier as GameplayEffectSpec)?.TakeBreakdown(), imp.Pipeline != null);
                    Next().Set(magnitude, imp.Operation, imp.TargetSlot, imp.SortOrder, imp.MultiplyBehaviour, imp.Group);
                }
            }

            /// <summary>
            /// Freezes <paramref name="delta"/> as constant Adds in <paramref name="group"/>, in the order
            /// <see cref="SourceAttributeImpact.FromAdditiveDelta"/> gives them. A commit clamps after every step, and
            /// Current is bounded by Base, so a growing Base goes first and a shrinking one last (PF-005).
            /// </summary>
            public void FreezeDelta(AttributeValue delta, Tag group)
            {
                Frozen.Clear();

                if (delta.BaseValue > 0f) Next().Set(delta.BaseValue, ECalculationOperation.Add, EAttributeTarget.Base, 0, EMultiplyBehaviour.Multiplicative, group);
                if (delta.CurrentValue != 0f) Next().Set(delta.CurrentValue, ECalculationOperation.Add, EAttributeTarget.Current, 0, EMultiplyBehaviour.Multiplicative, group);
                if (delta.BaseValue < 0f) Next().Set(delta.BaseValue, ECalculationOperation.Add, EAttributeTarget.Base, 0, EMultiplyBehaviour.Multiplicative, group);
            }

            private FrozenImpacter Next()
            {
                if (Frozen.Count == made.Count) made.Add(new FrozenImpacter());
                var next = made[Frozen.Count];
                Frozen.Add(next);
                return next;
            }

            /// <summary>
            /// Lets go of the commit's carrier, so a spare doesn't hold it. A change a deferred worker kept stays with
            /// that worker's actions, and the next commit gets a new one.
            /// </summary>
            public void Release()
            {
                Frozen.Clear();
                if (Change.Kept) Change = new ChangeValue(default);
                else Change.Value = default;
            }
        }

        /// <summary>
        /// An impacter frozen for one commit: a magnitude with the op, slot, sort order, multiply behaviour and group of
        /// the impacter it was taken from. What <see cref="ConstantImpacter"/> is, made to be reused: it never leaves
        /// the commit.
        /// </summary>
        private sealed class FrozenImpacter : IAttributeImpacter
        {
            private float magnitude;

            public ECalculationOperation Operation { get; private set; }
            public EAttributeTarget TargetSlot { get; private set; }
            public int SortOrder { get; private set; }
            public EMultiplyBehaviour MultiplyBehaviour { get; private set; }
            public Tag Group { get; private set; }

            public float Evaluate(IAttributeImpactCarrier carrier) => magnitude;

            public void Set(float value, ECalculationOperation operation, EAttributeTarget slot, int sortOrder,
                EMultiplyBehaviour multiplyBehaviour, Tag group)
            {
                magnitude = value;
                Operation = operation;
                TargetSlot = slot;
                SortOrder = sortOrder;
                MultiplyBehaviour = multiplyBehaviour;
                Group = group.IsValid ? group : Tags.IgnoreRetention;
            }
        }

        /// <summary>
        /// Where a pre-change-adjusted commit is recorded: the group its impacters share,
        /// else (mixed groups, or none) the carrier's retention group.
        /// </summary>
        private static Tag AdjustedCommitGroup(IReadOnlyList<IAttributeImpacter> impacters, IAttributeImpactCarrier carrier)
        {
            var fallback = carrier?.GetRetentionGroup() ?? Tags.IgnoreRetention;
            Tag shared = default;

            // By index: a foreach through the interface boxed the list's enumerator (GAS-P6)
            for (int i = 0; i < impacters.Count; i++)
            {
                var imp = impacters[i];
                if (imp == null) continue;
                var group = imp.Group.IsValid ? imp.Group : Tags.IgnoreRetention;
                if (!shared.IsValid) shared = group;
                else if (shared != group) return fallback;
            }
            return shared.IsValid ? shared : fallback;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ATTRIBUTE DERIVATION MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════════
        
        private HashSet<IAttribute> _refreshingAttributes = new();
        private void RefreshRegulatedAttributes(IAttribute contact)
        {
            if (!RegulationCache.TryGetRelatedAttributes(contact, out var related)) return;
            
            foreach (var relative in related)
            {
                // Guard against circular dependencies
                if (_refreshingAttributes.Contains(relative)) continue;
                _refreshingAttributes.Add(relative);

                try
                {
                    if (!AttributeCache.TryGetValue(relative, out var cached)) continue;

                    // Recompute walks the full retained-carrier list AND re-evaluates the
                    // cached scaler root via Blueprint.GetActiveValue(cache). The legacy
                    // RefreshActiveValue only updated the root; this captures both.
                    var oldValue = cached.ActiveValue;
                    cached.Recompute(AttributeCache);
                    var realImpact = cached.ActiveValue - oldValue;

                    if (realImpact.CurrentValue != 0 || realImpact.BaseValue != 0)
                    {
                        // The system's own change, carried by the attribute's root, which is typed DisallowImpact, so a
                        // worker that checks types passes it by. It takes the post-change half of the pipeline and
                        // reaches the frame summary; it fired OnAttributeChanged alone (GAS-27, Decision 14)
                        var sourcedModifier = new SourcedModifiedAttributeValue(cached.Root, realImpact.CurrentValue, realImpact.BaseValue);
                        var scratch = RentScratch(sourcedModifier);
                        var ctx = new WorkerContext(Self, AttributeCache, scratch.Change, _frameSummary, _actionQueue);
                        cached.EnforceScaling(ctx);
                        PostChangeHandler.RunWorkers(ctx);
                        Callbacks.AttributePostChange(relative, scratch.Change);
                        ReturnScratch(scratch);

                        var derivedImpact = ImpactData.Generate(Self, relative, sourcedModifier, realImpact, oldValue);
                        Self.RecordFrameImpactReceived(derivedImpact);

                        // Dealt as well, self to self, so what an entity deals still matches what's dealt to it. Not
                        // ProvideFrameImpactDealt, whose tracking would keep every refresh for the attribute's life
                        Self.RecordFrameImpactDealt(derivedImpact);
                        Callbacks.AttributeChanged(derivedImpact);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
                finally
                {
                    _refreshingAttributes.Remove(relative);
                }
            }
        }
       

        /// <summary>
        /// Mark a carrier as no longer alive — kept in retention for identity but skipped
        /// during recompute. Used when the carrier exits the local GAS but its impact still
        /// has bookkeeping reasons to linger (e.g. tracked-impact reversal in flight).
        /// </summary>
        public void RemoveAttributeDerivation(IAttributeImpactCarrier carrier)
        {
            if (carrier == null) return;
            var attribute = carrier.GetAttribute();
            if (attribute is null || !AttributeCache.ContainsKey(attribute)) return;

            // Route through the full pipeline (not a silent RemoveCarrier) so workers,
            // callbacks and the regulation refresh observe the removal — otherwise
            // attributes derived from this one keep their stale value.
            UnregisterModifier(attribute, new SourcedModifiedAttributeValue(carrier, 0f, 0f));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // RESTORE — a load's quiet paths (FEAT-9)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Registers a carrier's modifiers with nothing told: no workers, callbacks or frame impacts. A restore puts back
        /// what a save had, and tells once as it ends.
        /// </summary>
        internal void RegisterQuietly(IAttribute attribute, IAttributeImpactCarrier carrier, IReadOnlyList<IAttributeImpacter> impacters)
        {
            if (attribute is null || AttributeCache == null || !AttributeCache.TryGetValue(attribute, out var cached)) return;
            cached.RegisterModifier(carrier, impacters ?? carrier.GetImpacters(), AttributeCache);
        }

        /// <summary>Takes a carrier's modifiers off with nothing told, as a restore lets go of what the entity had.</summary>
        internal void UnregisterQuietly(IAttribute attribute, IAttributeImpactCarrier carrier)
        {
            if (attribute is null || AttributeCache == null || !AttributeCache.TryGetValue(attribute, out var cached)) return;
            cached.UnregisterModifier(carrier, AttributeCache);
        }

        /// <summary>
        /// Recomputes every attribute until none changes, so one derived from another reads it as it now is, and each
        /// modifier the level it reads. A restore's last step, which nothing hears.
        /// </summary>
        internal void RecomputeAll()
        {
            if (AttributeCache == null) return;

            // A pass for each attribute at most: one derived from another settles a pass after it
            for (int pass = 0; pass <= AttributeCache.Count; pass++)
            {
                bool changed = false;
                foreach (var cached in AttributeCache.Values)
                {
                    var before = cached.ActiveValue;
                    if (cached.Recompute(AttributeCache) != before) changed = true;
                }
                if (!changed) return;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // BARRIERS (TW-12)
        // ═══════════════════════════════════════════════════════════════════════════

        // The entity's barriers, kept as its attributes were when last counted
        private List<Attribute> barriers;
        private int barriersCounted = -1;

        // What each barrier has soaked this frame and not yet given up, so a second hit soaks no more than it holds
        private Dictionary<IAttribute, float> soakedThisFrame;

        // Each barrier's decay carrier, made once, so its decay allocates nothing
        private Dictionary<IAttribute, IAttributeImpactCarrier> decayCarriers;

        private List<Attribute> Barriers()
        {
            if (barriers != null && barriersCounted == AttributeCache.Count) return barriers;
            barriers ??= new List<Attribute>();
            barriers.Clear();
            foreach (var key in AttributeCache.Keys)
                if (key is Attribute attribute && attribute.Role == EAttributeRole.Barrier) barriers.Add(attribute);
            barriersCounted = AttributeCache.Count;
            return barriers;
        }

        // A workable loss to an attribute, taken first from each barrier that absorbs for it, as much as each holds; what a
        // barrier soaks comes off it at the frame's end, unworkable, and the rest goes on
        private void SoakIntoBarriers(IAttribute attribute, ChangeValue change)
        {
            var all = Barriers();
            if (all.Count == 0) return;
            float loss = -change.Value.CurrentValue;
            float soaked = 0f;
            for (int i = 0; i < all.Count && soaked < loss; i++)
            {
                var barrier = all[i];
                if (barrier.AbsorbsFor == null || !barrier.AbsorbsFor.Equals(attribute)) continue;
                if (!AttributeCache.TryGetValue(barrier, out var cached)) continue;
                float already = soakedThisFrame != null && soakedThisFrame.TryGetValue(barrier, out var pending) ? pending : 0f;
                float held = cached.ActiveValue.CurrentValue - already;
                if (held <= 0f) continue;
                float soak = Mathf.Min(held, loss - soaked);
                soaked += soak;
                (soakedThisFrame ??= new Dictionary<IAttribute, float>())[barrier] = already + soak;

                var taken = barrier;
                _actionQueue?.Enqueue(new LambdaAction(_ =>
                {
                    if (soakedThisFrame != null && soakedThisFrame.TryGetValue(taken, out var left))
                        soakedThisFrame[taken] = Mathf.Max(0f, left - soak);
                    ModifyAttribute(taken, SourcedModifiedAttributeValue.GenerateUnworkable(Self, taken, -soak, 0f));
                }, $"{taken.GetName()} soaks {soak:0.##}"));
            }
            if (soaked > 0f)
                change.Value = new SourcedModifiedAttributeValue(change.Value, change.Value.CurrentValue + soaked,
                    change.Value.BaseValue, change.Value.ImpactIsWorkable);
        }

        /// <summary>
        /// Each barrier's decay by <paramref name="deltaTime"/> of the entity's own time: its Decay Per Second, or that
        /// share of its base, unworkable, to 0 and no further.
        /// </summary>
        internal void StepBarriers(float deltaTime)
        {
            if (deltaTime <= 0f || AttributeCache == null) return;
            var all = Barriers();
            for (int i = 0; i < all.Count; i++)
            {
                var barrier = all[i];
                if (barrier.DecayPerSecond <= 0f || !AttributeCache.TryGetValue(barrier, out var cached)) continue;
                var value = cached.ActiveValue;
                if (value.CurrentValue <= 0f) continue;
                float rate = barrier.DecayAsShareOfBase ? barrier.DecayPerSecond * value.BaseValue : barrier.DecayPerSecond;
                float amount = Mathf.Min(value.CurrentValue, rate * deltaTime);
                if (amount <= 0f) continue;
                decayCarriers ??= new Dictionary<IAttribute, IAttributeImpactCarrier>();
                if (!decayCarriers.TryGetValue(barrier, out var carrier))
                    decayCarriers[barrier] = carrier = IAttributeImpactCarrier.GenerateSourceDerivation(Self, barrier);
                ModifyAttribute(barrier, new SourcedModifiedAttributeValue(carrier, -amount, 0f, false));
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════════
        
        public IReadOnlyDictionary<IAttribute, CachedAttributeValue> GetAttributeCache() 
            => AttributeCache;
        
        public List<IAttribute> GetDefinedAttributes() 
            => AttributeCache.Keys.ToList();
    }
}