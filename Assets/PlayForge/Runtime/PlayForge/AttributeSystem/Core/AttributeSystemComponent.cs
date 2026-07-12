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
                ModifyAttribute(SourcedModifiedAttributeValue.GenerateSimple(Self, attr, 0f, 0f), false);
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
            blueprint.SetElement.Base?.Scaling?.RegulateContactWith(attribute, RegulationCache);
            if (!blueprint.SetElement.LinkCurrentToBase)
                blueprint.SetElement.Current?.Scaling?.RegulateContactWith(attribute, RegulationCache);
            
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
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ATTRIBUTE MODIFICATION (refactored for WorkerContext)
        // ═══════════════════════════════════════════════════════════════════════════

        public ImpactData ModifyAttribute(SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            return ModifyAttribute(sourcedModifiedValue.Carrier.GetAttribute(), sourcedModifiedValue, runEvents);
        }
        
        public ImpactData ModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
            => CommitWithCarrier(attribute, sourcedModifiedValue, runEvents, isTick: false);

        /// <summary>
        /// Tick-path entry point — used by periodic-tick handlers in
        /// <c>RuntimeGameplayAbilitySystem.ApplyDurationGameplayEffectAsInstant</c>. Bypasses
        /// the apply-side retention rule and idempotency guard so each tick of a retained
        /// spec correctly commits its impacters as one-shot history. Without this path,
        /// ticks of any retained spec collapse into no-op refreshes (the apply path's
        /// idempotency catches the re-applied carrier instance).
        /// </summary>
        public ImpactData TickAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
            => CommitWithCarrier(attribute, sourcedModifiedValue, runEvents, isTick: true);

        public ImpactData TickAttribute(SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
            => TickAttribute(sourcedModifiedValue.Carrier.GetAttribute(), sourcedModifiedValue, runEvents);

        /// <summary>
        /// Shared commit pipeline used by both <see cref="ModifyAttribute"/> (apply path,
        /// retention rule applies) and <see cref="TickAttribute"/> (tick path, always one-shot).
        /// All worker / callback / impact-relay machinery lives here so the two entry points
        /// stay perfectly symmetric except for the dispatch decision in PHASE 2.
        /// </summary>
        private ImpactData CommitWithCarrier(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents, bool isTick)
        {
            // Null attribute = carrier with no attribute impact (e.g. a pure tag-granting
            // effect). Nothing to commit — Dictionary.ContainsKey(null) would throw.
            if (attribute is null || !AttributeCache.ContainsKey(attribute)) return default;

            var change = new ChangeValue(sourcedModifiedValue);
            var ctx = new WorkerContext(Self, AttributeCache, change, _frameSummary, _actionQueue);

            Callbacks.AttributePreChange(attribute, change);

            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 1: Pre-change workers
            // ═══════════════════════════════════════════════════════════════════════
            if (runEvents) PreChangeHandler.RunWorkers(ctx);

            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 2: Resolve carrier → Apply / Tick → recompute
            //
            // Resolve the carrier whose impacters drive this modification.
            //  • Carrier already exposes impacters (real GameplayEffectSpec → ScalerImpacter,
            //    or any new carrier kind built directly with ConstantImpacter): apply it
            //    as-is. Magnitudes get freshly evaluated by the impacter pipeline,
            //    decoupling from any stale precomputed delta on the SourcedModifiedAttributeValue.
            //
            //  • Carrier has no impacters (legacy "hand-rolled delta" callsite — e.g.
            //    EnforceScaling propagation, the synthetic zero-impact pulse fired on
            //    attribute-set initialisation, third-party wrappers): synthesize a
            //    SourceAttributeImpact carrying ConstantImpacter(s) from the precomputed
            //    (CurrentValue, BaseValue) delta so the recompute aggregator can apply
            //    bounds + rounding + frame-impact tracking uniformly.
            // ═══════════════════════════════════════════════════════════════════════
            var holdValue = AttributeCache[attribute].ActiveValue;

            var carrier = sourcedModifiedValue.Carrier;
            if (carrier == null || carrier.GetImpacters().Count == 0)
            {
                carrier = SourceAttributeImpact.FromAdditiveDelta(
                    sourcedModifiedValue.Carrier?.GetOrigin()?.GetSource() ?? Self,
                    attribute,
                    sourcedModifiedValue.CurrentValue,
                    sourcedModifiedValue.BaseValue,
                    sourcedModifiedValue.Carrier?.GetRetentionGroup() ?? Tags.IgnoreRetention,
                    sourcedModifiedValue.Carrier?.GetImpactTypes(),
                    sourcedModifiedValue.Carrier,
                    sourcedModifiedValue.Carrier?.RetainImpact() ?? false);
            }

            // Tick path always one-shots (preserves history in committedOneShotDelta).
            // Apply path runs the retention rule (carrier.IsSustainedModifier()):
            // Mode A → retain + Recompute; Mode B → one-shot now (instant/synthetic) or
            // no-op (durational tick-delivered, which commits via the tick path instead).
            // Both internally handle bounds + rounding via ApplyOneShotImpacters or Recompute.
            if (isTick) AttributeCache[attribute].TickCarrier(carrier, AttributeCache);
            else        AttributeCache[attribute].ApplyCarrier(carrier, AttributeCache);

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

            return impactData;
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
                        var sourcedModifier = new SourcedModifiedAttributeValue(cached.Root, realImpact.CurrentValue, realImpact.BaseValue);
                        var derivedImpact = ImpactData.Generate(Self, relative, sourcedModifier, realImpact, oldValue);
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
            // Removal IS nullification under the recompute model — a removed carrier
            // contributes nothing whether it's in the list as "dead" or simply absent.
            AttributeCache[attribute].RemoveCarrier(carrier, AttributeCache);
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