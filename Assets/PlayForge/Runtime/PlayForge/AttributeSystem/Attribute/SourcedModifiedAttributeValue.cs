using System.Collections.Generic;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public struct SourcedModifiedAttributeValue
    {
        public IAttributeImpactCarrier Carrier;
        
        public float CurrentValue;
        public float BaseValue;

        public bool ImpactIsWorkable;

        public ESignPolicy SignPolicy => ForgeHelper.SignPolicy(CurrentValue, BaseValue);
        public ISource Source => Carrier.GetOrigin().GetSource();
        public ITarget Target => Carrier.GetTarget();

        public SourcedModifiedAttributeValue(SourcedModifiedAttributeValue derivation, float currentValue, float baseValue, bool impactIsWorkable = true)
        {
            Carrier = derivation.Carrier;
            CurrentValue = currentValue;
            BaseValue = baseValue;

            ImpactIsWorkable = impactIsWorkable;
        }
        
        public SourcedModifiedAttributeValue(IAttributeImpactCarrier carrier, float currentValue, float baseValue, bool impactIsWorkable = true)
        {
            this.Carrier = carrier;
            
            CurrentValue = currentValue;
            BaseValue = baseValue;

            ImpactIsWorkable = impactIsWorkable;
        }

        /// <summary>
        /// A raw delta: a change made directly, with no effect behind it. Workers see it as they see an effect's change,
        /// with no impact type, so a worker that takes any type acts on it and one that wants a type doesn't. It was
        /// unworkable and typed DisallowImpact, so no worker ever saw one (GAS-25, Decision 9).
        /// </summary>
        public static SourcedModifiedAttributeValue GenerateSimple(ISource source, IAttribute attribute, float currentValue, float baseValue)
        {
            var derivation = IAttributeImpactCarrier.GenerateSourceDerivation(source, attribute, Tags.IgnoreRetention, new List<Tag>());
            return new SourcedModifiedAttributeValue(derivation, currentValue, baseValue);
        }

        /// <summary>A change the system makes to itself, such as the pulse each attribute starts with. No worker sees it.</summary>
        public static SourcedModifiedAttributeValue GenerateUnworkable(ISource source, IAttribute attribute, float currentValue, float baseValue)
        {
            var derivation = IAttributeImpactCarrier.GenerateSourceDerivation(source, attribute, Tags.IgnoreRetention, new List<Tag>() { Tags.DisallowImpact });
            return new SourcedModifiedAttributeValue(derivation, currentValue, baseValue, false);
        }
        
        #region Helpers
        
        public SourcedModifiedAttributeValue Combine(SourcedModifiedAttributeValue other, bool allowMismatchedDerivation = false)
        {
            if (other.Carrier != Carrier && !allowMismatchedDerivation)
            {
                return this;
            }
            
            return new SourcedModifiedAttributeValue(
                Carrier,
                CurrentValue + other.CurrentValue, 
                BaseValue + other.BaseValue
            );
        }

        public SourcedModifiedAttributeValue Negate()
        {
            return new SourcedModifiedAttributeValue(
                Carrier,
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
                Carrier,
                CurrentValue + operand.CurrentValue,
                BaseValue + operand.BaseValue
            );
        }
        
        public SourcedModifiedAttributeValue Multiply(AttributeValue operand)
        {
            return new SourcedModifiedAttributeValue(
                Carrier,
                CurrentValue * operand.CurrentValue,
                BaseValue * operand.BaseValue
            );
        }
        
        public SourcedModifiedAttributeValue Override(AttributeValue operand)
        {
            return new SourcedModifiedAttributeValue(
                Carrier,
                operand.CurrentValue,
                operand.BaseValue
            );
        }

        #endregion
        
        public override string ToString()
        {
            //if (Derivation is null && BaseDerivation is null) return $"[ SMAV-INSTANT ] {CurrentValue}/{BaseValue}";
            //if (Derivation is null) return $"[ SMAV-{BaseDerivation.GetEffectDerivation().GetName()} ] {CurrentValue}/{BaseValue}";
            return $"[ SMAV-{Carrier.GetOrigin().GetReadableDefinition().GetName()} ] {CurrentValue}/{BaseValue}";
        }
    }

    /// <summary>
    /// A change on its way through the attribute pipeline, which workers and listeners may adjust. The attribute system
    /// reuses it once the commit it belongs to is done, so read it during the call you're handed it in, or copy its
    /// <see cref="Value"/>. A deferred worker's actions may keep it: a change handed to one is left to them (GAS-P6).
    /// </summary>
    public class ChangeValue
    {
        public SourcedModifiedAttributeValue Value;

        // Set once a deferred worker is handed the change, so its commit leaves it to that worker's actions
        internal bool Kept;

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
