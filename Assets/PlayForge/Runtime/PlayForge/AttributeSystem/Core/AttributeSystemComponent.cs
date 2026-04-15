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
        private AttributeModificationRule Rule;
        
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
            Rule = new AttributeModificationRule();
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
            foreach (var attr in set.Attributes.Select(elem => elem.Attribute))
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
            foreach (var attr in set.Attributes.Select(elem => elem.Attribute))
            {
                ModifyAttribute(attr,
                    new SourcedModifiedAttributeValue(
                        IAttributeImpactDerivation.GenerateSourceDerivation(
                            Self, attr, 
                            Tags.IgnoreRetention, 
                            new List<Tag>(){ Tags.DisallowImpact }, AttributeCache[attr].Root),
                        0f, 0f,
                        false)
                );
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
            
            AttributeCache[attribute] = new CachedAttributeValue(blueprint);
            blueprint.Base.Scaling?.Regulate(attribute, Rule);
            
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
        
        public bool TryGetAttributeValue(IAttribute attribute, out CachedAttributeValue attributeValue)
            => AttributeCache.TryGetValue(attribute, out attributeValue);
        
        public bool TryGetAttributeValue(IAttribute attribute, out AttributeValue attributeValue)
        {
            if (AttributeCache.TryGetValue(attribute, out var cachedValue))
            {
                attributeValue = cachedValue.Value;
                return true;
            }
            
            attributeValue = default;
            return false;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ATTRIBUTE MODIFICATION (refactored for WorkerContext)
        // ═══════════════════════════════════════════════════════════════════════════
        
        public ImpactData ModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            if (!AttributeCache.ContainsKey(attribute)) return default;
            
            var change = new ChangeValue(sourcedModifiedValue);
            var ctx = new WorkerContext(Self, AttributeCache, change, _frameSummary, _actionQueue);

            Callbacks.AttributePreChange(attribute, change);
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 1: Pre-change workers
            // ═══════════════════════════════════════════════════════════════════════
            if (runEvents)
            {
                PreChangeHandler.RunWorkers(ctx);
            }
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 2: Snapshot and apply modification
            // ═══════════════════════════════════════════════════════════════════════
            var holdValue = AttributeCache[attribute].Value;
            AttributeCache[attribute].Add(sourcedModifiedValue.Derivation, change.Value.ToAttributeValue(), sourcedModifiedValue.Derivation.RetainImpact());
            
            if (runEvents) AttributeCache[attribute].ApplyBounds();
            change.Override(AttributeCache[attribute].Value - holdValue);

            if (runEvents)
            {
                AttributeCache[attribute].EnforceScaling(ctx);
            }
            
            AttributeCache[attribute].ApplyRounding();
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 3: Post-change workers
            // ═══════════════════════════════════════════════════════════════════════
                
            if (runEvents)
            {
                PostChangeHandler.RunWorkers(ctx);
            }
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 4: Calculate real impact and relay
            // ═══════════════════════════════════════════════════════════════════════
            
            change.Override(AttributeCache[attribute].Value - holdValue);
            //AttributeCache[attribute].UpdateHeld(change.Value.ToAttributeValue());
            
            Callbacks.AttributePostChange(attribute, change);
            
            var impactData = ImpactData.Generate(
                Self, 
                attribute, 
                sourcedModifiedValue, 
                change.Value.ToAttributeValue(), holdValue);
            
            // Record to frame summary
            _frameSummary?.RecordImpact(impactData);
            
            // Notify source system
            if (sourcedModifiedValue.Derivation.GetSource().FindAbilitySystem(out var abilSystem))
            {
                abilSystem.ProvideFrameImpactDealt(impactData);
            }
            
            // Fire callbacks
            Callbacks.AttributeChanged(impactData);

            return impactData;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ATTRIBUTE DERIVATION MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════════
        
        private HashSet<IAttribute> _refreshingAttributes = new();
        private void RefreshRegulatedAttributes(IAttribute contact)
        {
            if (!Rule.TryGetRelatedAttributes(contact, out var related)) return;
            
            foreach (var relative in related)
            {
                // Guard against circular dependencies
                if (_refreshingAttributes.Contains(relative)) continue;
                _refreshingAttributes.Add(relative);

                try
                {
                    if (!AttributeCache.TryGetValue(relative, out var cached)) continue;

                    var oldValue = cached.Value;
                    var delta = cached.RefreshDefaultValue(Self, AttributeCache);

                    if (delta.CurrentValue != 0 || delta.BaseValue != 0)
                    {
                        var realImpact = cached.Value - oldValue;
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
        
        public void RemoveAttributeDerivation(IAttributeImpactDerivation derivation, bool nullify, bool retainCurrent)
        {
            if (!AttributeCache.ContainsKey(derivation.GetAttribute())) return;
            AttributeCache[derivation.GetAttribute()].Remove(derivation, nullify, retainCurrent);
        }

        /// <summary>
        /// Used to clean derivations when it is removed from local system, but the impact is persisted
        /// </summary>
        /// <param name="key"></param>
        /// <param name="derivation"></param>
        public void NullifyAttributeDerivation(IAttributeImpactDerivation derivation)
        {
            if (!AttributeCache.ContainsKey(derivation.GetAttribute())) return;
            AttributeCache[derivation.GetAttribute()].NullifyDerivation(derivation);
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