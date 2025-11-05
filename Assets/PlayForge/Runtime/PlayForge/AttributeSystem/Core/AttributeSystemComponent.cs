using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AttributeSystemComponent : MonoBehaviour
    {
        protected AttributeSet attributeSet;
        protected List<AbstractAttributeChangeEvent> attributeChangeEvents = new();

        private AttributeChangeMomentHandler PreChangeHandler;
        private AttributeChangeMomentHandler PostChangeHandler;
        
        private Dictionary<Attribute, CachedAttributeValue> AttributeCache;
        private AttributeModificationRule Rule;
        public AttributeSystemCallbacks Callbacks;
        
        private GASComponent Root;
        
        #region Initialization
        
        public virtual void Initialize(GASComponent system)
        {
            Root = system;

            attributeSet = Root.Data.AttributeSet;
            attributeChangeEvents = Root.Data.AttributeChangeEvents;

            InitializeCaches();
            InitializePriorityChangeEvents();
            InitializeAttributeSets();
        }
        
        private void InitializeCaches()
        {
            AttributeCache = new Dictionary<Attribute, CachedAttributeValue>();
        }

        private void InitializeAttributeSets()
        {
            if (attributeSet is null) return;
            
            attributeSet.Initialize(this);

            foreach (var attr in AttributeCache.Keys)
            {
                ModifyAttribute(attr,
                    new SourcedModifiedAttributeValue(
                        IAttributeImpactDerivation.GenerateSourceDerivation(Root, attr, Tags.RETENTION_BONUS, Tags.GEN_NOT_APPLICABLE),
                        0f, 0f,
                        false)
                );
            }
        }

        private void InitializePriorityChangeEvents()
        {
            PreChangeHandler = new AttributeChangeMomentHandler();
            PostChangeHandler = new AttributeChangeMomentHandler();
            foreach (AbstractAttributeChangeEvent changeEvent in attributeChangeEvents) changeEvent.RegisterWithHandler(PreChangeHandler, PostChangeHandler);
        }
        
        #endregion
        
        #region Management
        
        public bool ProvideChangeEvent(AbstractAttributeChangeEvent changeEvent)
        {
            return changeEvent.RegisterWithHandler(PreChangeHandler, PostChangeHandler);
        }

        public bool RescindChangeEvent(AbstractAttributeChangeEvent changeEvent)
        {
            return changeEvent.DeRegisterFromHandler(PreChangeHandler, PostChangeHandler);
        }

        public void ProvideAttribute(Attribute attribute, DefaultAttributeValue defaultValue)
        {
            if (AttributeCache.ContainsKey(attribute)) return;
            
            AttributeCache[attribute] = new CachedAttributeValue(attribute, Root, defaultValue);
            
            //AttributeCache[attribute] = new CachedAttributeValue(defaultValue.Overflow, defaultValue.Modifier);
            //AttributeCache[attribute].Add(IAttributeImpactDerivation.GenerateSourceDerivation(Root, attribute), defaultValue.ToAttributeValue());

            defaultValue.Modifier.Regulate(attribute, Rule);
            
            // Good practice to introduce attribute to library wherever/whenever registration occurs
            AttributeLibrary.Add(attribute);
        }
        
        #endregion
        
        #region Helpers
        
        public bool DefinesAttribute(Attribute attribute) => AttributeCache.ContainsKey(attribute);
        
        public bool TryGetAttributeValue(Attribute attribute, out CachedAttributeValue attributeValue)
        {
            return AttributeCache.TryGetValue(attribute, out attributeValue);
        }

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
        
        #endregion
        
        #region Attribute Modification
        
        public void ModifyAttribute(Attribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            if (!AttributeCache.ContainsKey(attribute)) return;

            // Create a temp value to track during change events
            ChangeValue change = new ChangeValue(sourcedModifiedValue);
            if (runEvents) PreChangeHandler.RunEvents(attribute, Root, AttributeCache, change);
            
            // Hold version of previous attribute value & apply changes
            AttributeValue holdValue = AttributeCache[attribute].Value;
            AttributeCache[attribute].Add(sourcedModifiedValue.BaseDerivation, change.Value.ToModified());
            
            // Note that post-change events receive change values that may fall outside logical bounds, such as an attribute being 150/100 when it should be bound to base as ceil
            if (runEvents) PostChangeHandler.RunEvents(attribute, Root, AttributeCache, change);
            
            // Override the temp value to reflect real impact
            change.Override(AttributeCache[attribute].Value - holdValue);

            // Relay impact to source
            var impactData = AbilityImpactData.Generate(Root, attribute, sourcedModifiedValue, change.Value.ToAttributeValue());
            
            Callbacks.AttributeImpacted(impactData);
            if (sourcedModifiedValue.BaseDerivation.GetSource().FindAbilitySystem(out var attr)) attr.ProvideFrameImpactDealt(impactData);
        }

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
            foreach (IAttributeImpactDerivation derivation in derivations)
            {
                if (!AttributeCache.ContainsKey(derivation.GetAttribute())) continue;
                AttributeCache[derivation.GetAttribute()].Remove(derivation);
            }
        }

        #endregion
        
    }
    
    public class AttributeSystemCallbacks
    {
        public delegate void AttributeDelegate(Attribute attribute);
        public delegate void AttributeImpactDelegate(AbilityImpactData data);
        
        #region Callbacks

        public void AttributeRegister(Attribute attribute) => _onAttributeRegister?.Invoke(attribute);
        private AttributeDelegate _onAttributeRegister;
        public event AttributeDelegate OnAttributeRegister
        {
            add
            {
                if (Array.IndexOf(_onAttributeRegister.GetInvocationList(), value) == -1) _onAttributeRegister += value;
            }
            remove => _onAttributeRegister -= value;
        }

        public void AttributeUnregister(Attribute attribute) => _onAttributeUnregister?.Invoke(attribute);
        private AttributeDelegate _onAttributeUnregister;
        public event AttributeDelegate OnAttributeUnregister
        {
            add
            {
                if (Array.IndexOf(_onAttributeUnregister.GetInvocationList(), value) == -1) _onAttributeUnregister += value;
            }
            remove => _onAttributeUnregister -= value;
        }
        
        public void AttributeChanged(AbilityImpactData data) => _onAttributeChanged?.Invoke(data);
        private AttributeImpactDelegate _onAttributeChanged;
        public event AttributeImpactDelegate OnAttributeChanged
        {
            add
            {
                if (Array.IndexOf(_onAttributeChanged.GetInvocationList(), value) == -1) _onAttributeChanged += value;
            }
            remove => _onAttributeChanged -= value;
        }
        
        public void AttributeImpacted(AbilityImpactData data) => _onAttributeImpacted?.Invoke(data);
        private AttributeImpactDelegate _onAttributeImpacted;
        public event AttributeImpactDelegate OnAttributeImpacted
        {
            add
            {
                if (Array.IndexOf(_onAttributeImpacted.GetInvocationList(), value) == -1) _onAttributeImpacted += value;
            }
            remove => _onAttributeImpacted -= value;
        }

        #endregion
    }
}
