using UnityEditor;

namespace FarEmerald.PlayForge
{
    public struct SourcedModifiedAttributeValue
    {
        public IAttributeImpactDerivation Derivation;
        public IAttributeImpactDerivation BaseDerivation;
        
        public float CurrentValue;
        public float BaseValue;

        public bool Workable;

        public ESignPolicy SignPolicy => ForgeHelper.SignPolicy(CurrentValue, BaseValue);

        public SourcedModifiedAttributeValue(SourcedModifiedAttributeValue derivation, float currentValue, float baseValue, bool workable = true)
        {
            Derivation = derivation.Derivation;
            BaseDerivation = derivation.BaseDerivation;
            CurrentValue = currentValue;
            BaseValue = baseValue;

            Workable = workable;
        }
        
        public SourcedModifiedAttributeValue(IAttributeImpactDerivation derivation, float currentValue, float baseValue, bool workable = true)
        {
            Derivation = derivation;
            BaseDerivation = derivation;
            
            CurrentValue = currentValue;
            BaseValue = baseValue;

            Workable = workable;
        }

        public SourcedModifiedAttributeValue(IAttributeImpactDerivation derivation, IAttributeImpactDerivation baseDerivation, float currentValue, float baseValue,
            bool workable = true)
        {
            Derivation = derivation;
            BaseDerivation = baseDerivation;
            
            CurrentValue = currentValue;
            BaseValue = baseValue;

            Workable = workable;
        }

        #region Helpers
        
        public SourcedModifiedAttributeValue Combine(SourcedModifiedAttributeValue other, bool allowMismatchedDerivation = false)
        {
            if (other.Derivation != Derivation && !allowMismatchedDerivation)
            {
                return this;
            }
            
            return new SourcedModifiedAttributeValue(
                Derivation,
                BaseDerivation,
                CurrentValue + other.CurrentValue, 
                BaseValue + other.BaseValue
            );
        }

        public SourcedModifiedAttributeValue Negate()
        {
            return new SourcedModifiedAttributeValue(
                Derivation,
                BaseDerivation,
                -CurrentValue,
                -BaseValue
            );
        }
        
        public ModifiedAttributeValue ToModified() => new(CurrentValue, BaseValue);

        public AttributeValue ToAttributeValue() => new(CurrentValue, BaseValue);
        
        #endregion
        
        #region Operations
        
        public SourcedModifiedAttributeValue Add(AttributeValue operand)
        {
            return new SourcedModifiedAttributeValue(
                Derivation,
                BaseDerivation,
                CurrentValue + operand.CurrentValue,
                BaseValue + operand.BaseValue
            );
        }
        
        public SourcedModifiedAttributeValue Multiply(AttributeValue operand)
        {
            return new SourcedModifiedAttributeValue(
                Derivation,
                BaseDerivation,
                CurrentValue * operand.CurrentValue,
                BaseValue * operand.BaseValue
            );
        }
        
        public SourcedModifiedAttributeValue Override(AttributeValue operand)
        {
            return new SourcedModifiedAttributeValue(
                Derivation,
                BaseDerivation,
                operand.CurrentValue,
                operand.BaseValue
            );
        }

        #endregion
        
        public override string ToString()
        {
            if (Derivation is null && BaseDerivation is null) return $"[ SMAV-INSTANT ] {CurrentValue}/{BaseValue}";
            if (Derivation is null) return $"[ SMAV-{BaseDerivation.GetEffectDerivation().GetName()} ] {CurrentValue}/{BaseValue}";
            return $"[ SMAV-{Derivation.GetEffectDerivation().GetName()} ] {CurrentValue}/{BaseValue}";
        }
    }

    public class ChangeValue
    {
        public SourcedModifiedAttributeValue Value;
        public ChangeValue(SourcedModifiedAttributeValue value)
        {
            Value = value;
        }
        
        public void Add(AttributeValue operand)
        {
            Value = Value.Add(operand);
        }
        
        public void Multiply(AttributeValue operand)
        {
            Value = Value.Multiply(operand);
        }
        
        public void Override(AttributeValue operand)
        {
            Value = Value.Override(operand);
        }

        public override string ToString()
        {
            return Value.ToString();
        }
    }
}
