using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Component for managing attributes and their modifications.
    /// Refactored to support WorkerContext and deferred execution.
    /// </summary>
    public class AttributeSystemComponent
    {
        protected AttributeSet attributeSet;
        protected List<AbstractAttributeWorker> attributeChangeWorkers = new();
        
        private AttributeChangeMomentHandler PreChangeHandler = new();
        private AttributeChangeMomentHandler PostChangeHandler = new();
        
        private Dictionary<Attribute, CachedAttributeValue> AttributeCache;
        private AttributeModificationRule Rule;
        
        public AttributeSystemCallbacks Callbacks;
        
        public readonly IGameplayAbilitySystem Root;
        
        // References for deferred execution
        private ActionQueue _actionQueue;
        private FrameSummary _frameSummary;
        
        public AttributeSystemComponent(IGameplayAbilitySystem root)
        {
            Root = root;
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
            AttributeCache = new Dictionary<Attribute, CachedAttributeValue>();
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
        }
        
        private void InitializeAttributeSets()
        {
            if (attributeSet == null) return;
            
            attributeSet.Initialize(this);
            Root.GetTagCache()?.AddTag(attributeSet.AssetTag);
            
            foreach (var attr in AttributeCache.Keys)
            {
                ModifyAttribute(attr,
                    new SourcedModifiedAttributeValue(
                        IAttributeImpactDerivation.GenerateSourceDerivation(Root, attr, Tags.IGNORE, Tags.NONE),
                        0f, 0f,
                        false)
                );
            }
        }
        
        /// <summary>
        /// Set the deferred execution context.
        /// </summary>
        public void SetDeferredContext(ActionQueue actionQueue, FrameSummary frameSummary)
        {
            _actionQueue = actionQueue;
            _frameSummary = frameSummary;
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
        
        public void ProvideAttribute(Attribute attribute, AttributeBlueprint blueprint)
        {
            if (AttributeCache.ContainsKey(attribute)) return;
            
            AttributeCache[attribute] = new CachedAttributeValue(attribute, Root, blueprint);
            blueprint.Modifier?.Regulate(attribute, Rule);
            
            AttributeLibrary.Add(attribute);
            Callbacks.AttributeRegister(attribute);
        }
        
        public bool DefinesAttribute(Attribute attribute) 
            => attribute == null || AttributeCache.ContainsKey(attribute);
        
        public bool TryGetAttributeValue(Attribute attribute, out CachedAttributeValue attributeValue)
            => AttributeCache.TryGetValue(attribute, out attributeValue);
        
        public bool TryGetAttributeValue(Attribute attribute, out AttributeValue attributeValue)
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
        
        public ImpactData ModifyAttribute(Attribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            if (!AttributeCache.ContainsKey(attribute)) return default;

            var cached = AttributeCache[attribute];
            var change = new ChangeValue(sourcedModifiedValue);
            var ctx = new WorkerContext(Root, AttributeCache, change, _frameSummary, _actionQueue);
            
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
            AttributeCache[attribute].Add(sourcedModifiedValue.BaseDerivation, change.Value.ToModified());
            
            if (runEvents) AttributeCache[attribute].ApplyBounds();
            change.Override(AttributeCache[attribute].Value - holdValue);
            
            if (runEvents)
            {
                AttributeCache[attribute].EnforceScaling(ctx);
                
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 3: Post-change workers
            // ═══════════════════════════════════════════════════════════════════════
                
                PostChangeHandler.RunWorkers(ctx);
            }
            
            // ═══════════════════════════════════════════════════════════════════════
            // PHASE 4: Calculate real impact and relay
            // ═══════════════════════════════════════════════════════════════════════
            AttributeCache[attribute].ApplyRounding();
            change.Override(AttributeCache[attribute].Value - holdValue);
            
            var impactData = ImpactData.Generate(
                Root, 
                attribute, 
                sourcedModifiedValue, 
                change.Value.ToAttributeValue());
            
            // Record to frame summary
            _frameSummary?.RecordImpact(impactData);
            
            // Notify source system
            if (sourcedModifiedValue.BaseDerivation.GetSource().FindAbilitySystem(out var abilSystem))
            {
                abilSystem.ProvideFrameImpactDealt(impactData);
            }
            
            // Fire callbacks
            Callbacks.AttributeChanged(impactData);
            Callbacks.AttributeImpacted(impactData);

            return impactData;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ATTRIBUTE DERIVATION MANAGEMENT
        // ═══════════════════════════════════════════════════════════════════════════
        
        public void RefreshAttributes(Attribute contact)
        {
            // AttributeCache[contact].Modifier.Initialize();
        }
        
        public void RemoveAttributeDerivation(IAttributeImpactDerivation derivation)
        {
            if (!AttributeCache.ContainsKey(derivation.GetAttribute())) return;
            AttributeCache[derivation.GetAttribute()].Remove(derivation);
        }
        
        public void RemoveAttributeDerivations(List<IAttributeImpactDerivation> derivations)
        {
            foreach (var derivation in derivations)
            {
                if (!AttributeCache.ContainsKey(derivation.GetAttribute())) continue;
                AttributeCache[derivation.GetAttribute()].Remove(derivation);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════════
        
        public IReadOnlyDictionary<Attribute, CachedAttributeValue> GetAttributeCache() 
            => AttributeCache;
        
        public List<Attribute> GetDefinedAttributes() 
            => AttributeCache.Keys.ToList();
    }
}