using System.Collections.Generic;
using UnityEditor;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public struct SourcedModifiedAttributeValue
    {
        public IAttributeImpactDerivation Derivation;
        
        public float CurrentValue;
        public float BaseValue;

        public bool ImpactIsWorkable;

        public ESignPolicy SignPolicy => ForgeHelper.SignPolicy(CurrentValue, BaseValue);

        public SourcedModifiedAttributeValue(SourcedModifiedAttributeValue derivation, float currentValue, float baseValue, bool impactIsWorkable = true)
        {
            Derivation = derivation.Derivation;
            CurrentValue = currentValue;
            BaseValue = baseValue;

            ImpactIsWorkable = impactIsWorkable;
        }
        
        public SourcedModifiedAttributeValue(IAttributeImpactDerivation derivation, float currentValue, float baseValue, bool impactIsWorkable = true)
        {
            Derivation = derivation;
            
            CurrentValue = currentValue;
            BaseValue = baseValue;

            ImpactIsWorkable = impactIsWorkable;
        }

        public static SourcedModifiedAttributeValue GenerateSimple(ISource source, IAttribute attribute, float currentValue, float baseValue)
        {
            var derivation = IAttributeImpactDerivation.GenerateSourceDerivation(source, attribute, Tags.IgnoreRetention, new List<Tag>() { Tags.DisallowImpact });
            return new SourcedModifiedAttributeValue(derivation, currentValue, baseValue, false);
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
                CurrentValue + other.CurrentValue, 
                BaseValue + other.BaseValue
            );
        }

        public SourcedModifiedAttributeValue Negate()
        {
            return new SourcedModifiedAttributeValue(
                Derivation,
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
                CurrentValue + operand.CurrentValue,
                BaseValue + operand.BaseValue
            );
        }
        
        public SourcedModifiedAttributeValue Multiply(AttributeValue operand)
        {
            return new SourcedModifiedAttributeValue(
                Derivation,
                CurrentValue * operand.CurrentValue,
                BaseValue * operand.BaseValue
            );
        }
        
        public SourcedModifiedAttributeValue Override(AttributeValue operand)
        {
            return new SourcedModifiedAttributeValue(
                Derivation,
                operand.CurrentValue,
                operand.BaseValue
            );
        }

        #endregion
        
        public override string ToString()
        {
            //if (Derivation is null && BaseDerivation is null) return $"[ SMAV-INSTANT ] {CurrentValue}/{BaseValue}";
            //if (Derivation is null) return $"[ SMAV-{BaseDerivation.GetEffectDerivation().GetName()} ] {CurrentValue}/{BaseValue}";
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
