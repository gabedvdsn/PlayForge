using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Callbacks for attribute system events.
    /// Used for monitoring attribute changes without modifying behavior.
    /// </summary>
    public class AttributeSystemCallbacks
    {
        public delegate void AttributeDelegate(Attribute attribute);
        public delegate void AttributeImpactDelegate(ImpactData data);
        public delegate void AttributeChangeDelegate(Attribute attribute, ChangeValue change);
        
        // ═══════════════════════════════════════════════════════════════
        // ATTRIBUTE REGISTRATION
        // ═══════════════════════════════════════════════════════════════
        
        #region On Attribute Register
        private AttributeDelegate _onAttributeRegister;
        public event AttributeDelegate OnAttributeRegister
        {
            add => _onAttributeRegister = AddUnique(_onAttributeRegister, value);
            remove => _onAttributeRegister -= value;
        }
        public void AttributeRegister(Attribute attribute) => _onAttributeRegister?.Invoke(attribute);
        #endregion
        
        #region On Attribute Unregister
        private AttributeDelegate _onAttributeUnregister;
        public event AttributeDelegate OnAttributeUnregister
        {
            add => _onAttributeUnregister = AddUnique(_onAttributeUnregister, value);
            remove => _onAttributeUnregister -= value;
        }
        public void AttributeUnregister(Attribute attribute) => _onAttributeUnregister?.Invoke(attribute);
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // ATTRIBUTE MODIFICATION
        // ═══════════════════════════════════════════════════════════════
        
        #region On Attribute Pre-Change (before workers)
        private AttributeChangeDelegate _onAttributePreChange;
        public event AttributeChangeDelegate OnAttributePreChange
        {
            add => _onAttributePreChange = AddUnique(_onAttributePreChange, value);
            remove => _onAttributePreChange -= value;
        }
        public void AttributePreChange(Attribute attribute, ChangeValue change) => _onAttributePreChange?.Invoke(attribute, change);
        #endregion
        
        #region On Attribute Post-Change (after workers, before impact relay)
        private AttributeChangeDelegate _onAttributePostChange;
        public event AttributeChangeDelegate OnAttributePostChange
        {
            add => _onAttributePostChange = AddUnique(_onAttributePostChange, value);
            remove => _onAttributePostChange -= value;
        }
        public void AttributePostChange(Attribute attribute, ChangeValue change) => _onAttributePostChange?.Invoke(attribute, change);
        #endregion
        
        #region On Attribute Changed (legacy - after full modification)
        private AttributeImpactDelegate _onAttributeChanged;
        public event AttributeImpactDelegate OnAttributeChanged
        {
            add => _onAttributeChanged = AddUnique(_onAttributeChanged, value);
            remove => _onAttributeChanged -= value;
        }
        public void AttributeChanged(ImpactData data) => _onAttributeChanged?.Invoke(data);
        #endregion
        
        #region On Attribute Impacted (final impact data after all processing)
        private AttributeImpactDelegate _onAttributeImpacted;
        public event AttributeImpactDelegate OnAttributeImpacted
        {
            add => _onAttributeImpacted = AddUnique(_onAttributeImpacted, value);
            remove => _onAttributeImpacted -= value;
        }
        public void AttributeImpacted(ImpactData data) => _onAttributeImpacted?.Invoke(data);
        #endregion
        
        #region On Attribute Damaged (negative current impact)
        private AttributeImpactDelegate _onAttributeDamaged;
        public event AttributeImpactDelegate OnAttributeDamaged
        {
            add => _onAttributeDamaged = AddUnique(_onAttributeDamaged, value);
            remove => _onAttributeDamaged -= value;
        }
        public void AttributeDamaged(ImpactData data)
        {
            if (data.RealImpact.CurrentValue < 0)
                _onAttributeDamaged?.Invoke(data);
        }
        #endregion
        
        #region On Attribute Healed (positive current impact)
        private AttributeImpactDelegate _onAttributeHealed;
        public event AttributeImpactDelegate OnAttributeHealed
        {
            add => _onAttributeHealed = AddUnique(_onAttributeHealed, value);
            remove => _onAttributeHealed -= value;
        }
        public void AttributeHealed(ImpactData data)
        {
            if (data.RealImpact.CurrentValue > 0)
                _onAttributeHealed?.Invoke(data);
        }
        #endregion
        
        // ═══════════════════════════════════════════════════════════════
        // ATTRIBUTE STATE CHANGES
        // ═══════════════════════════════════════════════════════════════
        
        public delegate void AttributeStateDelegate(Attribute attribute);
        public delegate void AttributeValueChangeDelegate(Attribute attribute, AttributeValue oldValue, AttributeValue newValue);
        
        #region On Attribute Base Changed
        private AttributeValueChangeDelegate _onAttributeBaseChanged;
        public event AttributeValueChangeDelegate OnAttributeBaseChanged
        {
            add => _onAttributeBaseChanged = AddUnique(_onAttributeBaseChanged, value);
            remove => _onAttributeBaseChanged -= value;
        }
        public void AttributeBaseChanged(Attribute attribute, AttributeValue oldValue, AttributeValue newValue)
        {
            if (System.Math.Abs(oldValue.BaseValue - newValue.BaseValue) > 0.001f)
                _onAttributeBaseChanged?.Invoke(attribute, oldValue, newValue);
        }
        #endregion
        
        #region On Attribute Current Zero
        private AttributeStateDelegate _onAttributeCurrentZero;
        public event AttributeStateDelegate OnAttributeCurrentZero
        {
            add => _onAttributeCurrentZero = AddUnique(_onAttributeCurrentZero, value);
            remove => _onAttributeCurrentZero -= value;
        }
        public void AttributeCurrentZero(Attribute attribute) => _onAttributeCurrentZero?.Invoke(attribute);
        #endregion
        
        #region On Attribute Current Full
        private AttributeStateDelegate _onAttributeCurrentFull;
        public event AttributeStateDelegate OnAttributeCurrentFull
        {
            add => _onAttributeCurrentFull = AddUnique(_onAttributeCurrentFull, value);
            remove => _onAttributeCurrentFull -= value;
        }
        public void AttributeCurrentFull(Attribute attribute) => _onAttributeCurrentFull?.Invoke(attribute);
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