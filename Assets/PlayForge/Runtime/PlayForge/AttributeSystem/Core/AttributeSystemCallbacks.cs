using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Callbacks for attribute system events.
    /// Used for monitoring attribute changes without modifying behavior.
    /// </summary>
    public class AttributeSystemCallbacks
    {
        public delegate void AttributeDelegate(IAttribute attribute);
        public delegate void AttributeImpactDelegate(ImpactData data);
        public delegate void AttributeChangeDelegate(IAttribute attribute, ChangeValue change);
        
        // ═══════════════════════════════════════════════════════════════
        // ATTRIBUTE REGISTRATION
        // ═══════════════════════════════════════════════════════════════
        
        #region On IAttribute Register
        private AttributeDelegate _onAttributeRegister;
        public event AttributeDelegate OnAttributeRegister
        {
            add => _onAttributeRegister = AddUnique(_onAttributeRegister, value);
            remove => _onAttributeRegister -= value;
        }
        public void AttributeRegister(IAttribute attribute) => _onAttributeRegister?.Invoke(attribute);
        #endregion
        
        #region On IAttribute Unregister
        private AttributeDelegate _onAttributeUnregister;
        public event AttributeDelegate OnAttributeUnregister
        {
            add => _onAttributeUnregister = AddUnique(_onAttributeUnregister, value);
            remove => _onAttributeUnregister -= value;
        }
        public void AttributeUnregister(IAttribute attribute) => _onAttributeUnregister?.Invoke(attribute);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // ATTRIBUTE MODIFICATION
        // ═══════════════════════════════════════════════════════════════
        
        #region On IAttribute Pre-Change (before workers)
        private AttributeChangeDelegate _onAttributePreChange;
        public event AttributeChangeDelegate OnAttributePreChange
        {
            add => _onAttributePreChange = AddUnique(_onAttributePreChange, value);
            remove => _onAttributePreChange -= value;
        }
        public void AttributePreChange(IAttribute attribute, ChangeValue change) => _onAttributePreChange?.Invoke(attribute, change);
        #endregion
        
        #region On IAttribute Post-Change (after workers, before impact relay)
        private AttributeChangeDelegate _onAttributePostChange;
        public event AttributeChangeDelegate OnAttributePostChange
        {
            add => _onAttributePostChange = AddUnique(_onAttributePostChange, value);
            remove => _onAttributePostChange -= value;
        }
        public void AttributePostChange(IAttribute attribute, ChangeValue change) => _onAttributePostChange?.Invoke(attribute, change);
        #endregion
        
        #region On IAttribute Impacted (final impact data after all processing)
        private AttributeImpactDelegate _onAttributeChanged;
        public event AttributeImpactDelegate OnAttributeChanged
        {
            add => _onAttributeChanged = AddUnique(_onAttributeChanged, value);
            remove => _onAttributeChanged -= value;
        }

        public void AttributeChanged(ImpactData data)
        {
            _onAttributeChanged?.Invoke(data);
            
            switch (ForgeHelper.SignPolicy(data.RealImpact.CurrentValue, data.RealImpact.BaseValue))
            {
                case ESignPolicy.Positive:
                    AttributeIncreased(data);
                    break;
                case ESignPolicy.Negative:
                    AttributeReduced(data);
                    break;
                case ESignPolicy.ZeroBiased:
                    break;
                case ESignPolicy.ZeroNeutral:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            if (data.RealImpact.BaseValue != 0f) AttributeBaseChanged(data.Attribute, data.OldValue, data.OldValue + data.RealImpact);
            if (data.RealImpact.CurrentValue != 0 && data.Target.TryGetAttributeValue(data.Attribute, out AttributeValue currValue))
            {
                if (Mathf.Approximately(currValue.RatioMinZero, 1f)) AttributeCurrentFull(data.Attribute);
                if (Mathf.Approximately(currValue.RatioMinZero, 0f)) AttributeCurrentZero(data.Attribute);
            }
            
            
        }
        #endregion
        
        #region On IAttribute Reduced (any negative impact)
        private AttributeImpactDelegate _onAttributeReduced;
        public event AttributeImpactDelegate OnAttributeReduced
        {
            add => _onAttributeReduced = AddUnique(_onAttributeReduced, value);
            remove => _onAttributeReduced -= value;
        }
        public void AttributeReduced(ImpactData data)
        {
            if (data.RealImpact.CurrentValue < 0)
                _onAttributeReduced?.Invoke(data);
        }
        #endregion
        
        #region On IAttribute Increased (any positive impact)
        private AttributeImpactDelegate _onAttributeIncreased;
        public event AttributeImpactDelegate OnAttributeIncreased
        {
            add => _onAttributeIncreased = AddUnique(_onAttributeIncreased, value);
            remove => _onAttributeIncreased -= value;
        }
        public void AttributeIncreased(ImpactData data)
        {
            if (data.RealImpact.CurrentValue > 0)
                _onAttributeIncreased?.Invoke(data);
        }
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // ATTRIBUTE STATE CHANGES
        // ═══════════════════════════════════════════════════════════════
        
        public delegate void AttributeStateDelegate(IAttribute attribute);
        public delegate void AttributeValueChangeDelegate(IAttribute attribute, AttributeValue oldValue, AttributeValue newValue);
        
        #region On IAttribute Base Changed
        private AttributeValueChangeDelegate _onAttributeBaseChanged;
        public event AttributeValueChangeDelegate OnAttributeBaseChanged
        {
            add => _onAttributeBaseChanged = AddUnique(_onAttributeBaseChanged, value);
            remove => _onAttributeBaseChanged -= value;
        }
        public void AttributeBaseChanged(IAttribute attribute, AttributeValue oldValue, AttributeValue newValue)
        {
            if (System.Math.Abs(oldValue.BaseValue - newValue.BaseValue) > 0.001f)
                _onAttributeBaseChanged?.Invoke(attribute, oldValue, newValue);
        }
        #endregion
        
        #region On IAttribute Current Zero
        private AttributeStateDelegate _onAttributeCurrentZero;
        public event AttributeStateDelegate OnAttributeCurrentZero
        {
            add => _onAttributeCurrentZero = AddUnique(_onAttributeCurrentZero, value);
            remove => _onAttributeCurrentZero -= value;
        }
        public void AttributeCurrentZero(IAttribute attribute) => _onAttributeCurrentZero?.Invoke(attribute);
        #endregion
        
        #region On IAttribute Current Full
        private AttributeStateDelegate _onAttributeCurrentFull;
        public event AttributeStateDelegate OnAttributeCurrentFull
        {
            add => _onAttributeCurrentFull = AddUnique(_onAttributeCurrentFull, value);
            remove => _onAttributeCurrentFull -= value;
        }
        public void AttributeCurrentFull(IAttribute attribute) => _onAttributeCurrentFull?.Invoke(attribute);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════
        
        private static AttributeDelegate AddUnique(AttributeDelegate existing, AttributeDelegate toAdd)
        {
            if (existing == null) return toAdd;
            if (Array.IndexOf(existing.GetInvocationList(), toAdd) == -1)
                return existing + toAdd;
            return existing;
        }
        
        private static AttributeImpactDelegate AddUnique(AttributeImpactDelegate existing, AttributeImpactDelegate toAdd)
        {
            if (existing == null) return toAdd;
            if (Array.IndexOf(existing.GetInvocationList(), toAdd) == -1)
                return existing + toAdd;
            return existing;
        }
        
        private static AttributeChangeDelegate AddUnique(AttributeChangeDelegate existing, AttributeChangeDelegate toAdd)
        {
            if (existing == null) return toAdd;
            if (Array.IndexOf(existing.GetInvocationList(), toAdd) == -1)
                return existing + toAdd;
            return existing;
        }
        
        private static AttributeStateDelegate AddUnique(AttributeStateDelegate existing, AttributeStateDelegate toAdd)
        {
            if (existing == null) return toAdd;
            if (Array.IndexOf(existing.GetInvocationList(), toAdd) == -1)
                return existing + toAdd;
            return existing;
        }
        
        private static AttributeValueChangeDelegate AddUnique(AttributeValueChangeDelegate existing, AttributeValueChangeDelegate toAdd)
        {
            if (existing == null) return toAdd;
            if (Array.IndexOf(existing.GetInvocationList(), toAdd) == -1)
                return existing + toAdd;
            return existing;
        }
    }
}