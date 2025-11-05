using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public struct AttributeValue
    {
        public float CurrentValue;
        public float BaseValue;

        public float Ratio => CurrentValue / BaseValue;

        public AttributeValue(float currentValue, float baseValue)
        {
            CurrentValue = currentValue;
            BaseValue = baseValue;
        }

        public AttributeValue ApplyModified(ModifiedAttributeValue modifiedAttributeValue)
        {
            return new AttributeValue(
                CurrentValue + modifiedAttributeValue.DeltaCurrentValue,
                BaseValue + modifiedAttributeValue.DeltaBaseValue
            );
        }

        public AttributeValue Combine(AttributeValue other) => this + other;

        public AttributeValue Negate() => this * -1;
        
        public static AttributeValue operator +(AttributeValue a, AttributeValue b)
        {
            return new AttributeValue(a.CurrentValue + b.CurrentValue, a.BaseValue + b.BaseValue);
        }
        
        public static AttributeValue operator +(float v, AttributeValue a)
        {
            return new AttributeValue(v + a.CurrentValue, v + a.BaseValue);
        }
        
        public static AttributeValue operator -(AttributeValue a, AttributeValue b)
        {
            return new AttributeValue(a.CurrentValue - b.CurrentValue, a.BaseValue - b.BaseValue);
        }

        public static AttributeValue operator -(float v, AttributeValue a)
        {
            return new AttributeValue(v - a.CurrentValue, v - a.BaseValue);
        }
        
        public static AttributeValue operator /(AttributeValue a, AttributeValue b)
        {
            return new AttributeValue(a.CurrentValue / b.CurrentValue, a.BaseValue / b.BaseValue);
        }
        
        public static AttributeValue operator /(AttributeValue a, float b)
        {
            return new AttributeValue(a.CurrentValue / b, a.BaseValue / b);
        }
        
        public static AttributeValue operator *(AttributeValue a, float b)
        {
            return new AttributeValue(a.CurrentValue * b, a.BaseValue * b);
        }
        
        public static AttributeValue operator *(AttributeValue a, AttributeValue b)
        {
            return new AttributeValue(a.CurrentValue * b.CurrentValue, a.BaseValue * b.BaseValue);
        }

        public AttributeValue BaseAligned() => new AttributeValue(BaseValue, BaseValue);

        public static bool WithinLimits(AttributeValue value, AttributeValue floor, AttributeValue ceil)
        {
            return floor.CurrentValue <= value.CurrentValue && value.CurrentValue <= ceil.CurrentValue
                                                            && floor.BaseValue <= value.BaseValue && value.BaseValue <= ceil.BaseValue;
        }
        
        public override string ToString()
        {
            return $"{CurrentValue}/{BaseValue}";
        }
    }

    public class CachedAttributeValue
    {
        public Dictionary<IAttributeImpactDerivation, AttributeValue> DerivedValues = new();
        public AttributeValue Value;
        public AttributeOverflowData Overflow;
        public AbstractCachedMagnitudeModifier Modifier;

        public CachedAttributeValue(AttributeOverflowData overflow, AbstractCachedMagnitudeModifier modifier)
        {
            Overflow = overflow;
            Modifier = modifier;
        }

        public CachedAttributeValue(Attribute attribute, ISource source, DefaultAttributeValue defaultValue)
        {
            Overflow = defaultValue.Overflow;
            Modifier = defaultValue.Modifier;
            
            Add(IAttributeImpactDerivation.GenerateSourceDerivation(source, attribute, Tags.RETENTION_DECLARED, Tags.GEN_NOT_APPLICABLE), defaultValue.ToAttributeValue());
        }

        public void Refresh()
        {
            
        }

        public void Add(IAttributeImpactDerivation derivation, AttributeValue attributeValue)
        {
            if (derivation.AttributeRetention() != Tags.RETENTION_IGNORE)
            {
                if (DerivedValues.ContainsKey(derivation)) DerivedValues[derivation] += attributeValue;
                else DerivedValues[derivation] = attributeValue;
            }

            Value += attributeValue;
        }

        public void Add(IAttributeImpactDerivation derivation, ModifiedAttributeValue modifiedAttributeValue)
        {
            if (derivation.AttributeRetention() != Tags.RETENTION_IGNORE)
            {
                if (DerivedValues.ContainsKey(derivation)) DerivedValues[derivation] = DerivedValues[derivation].ApplyModified(modifiedAttributeValue);
                else DerivedValues[derivation] = modifiedAttributeValue.ToAttributeValue();
            }

            Value += modifiedAttributeValue.ToAttributeValue();
        }

        public void Remove(IAttributeImpactDerivation derivation)
        {
            if (!DerivedValues.ContainsKey(derivation)) return;
            
            Value -= DerivedValues[derivation];
            DerivedValues.Remove(derivation);
        }

        public void Set(IAttributeImpactDerivation derivation, AttributeValue attributeValue)
        {
            if (!DerivedValues.ContainsKey(derivation)) return;
            AttributeValue difference = attributeValue - DerivedValues[derivation];
            DerivedValues[derivation] = attributeValue;
            Value += difference;

            // if (attributeValue.CurrentValue == 0f && attributeValue.BaseValue == 0f) Remove(derivation);
        }

        public void Clean()
        {
            List<IAttributeImpactDerivation> toRemove = new List<IAttributeImpactDerivation>();
            foreach (IAttributeImpactDerivation derivation in DerivedValues.Keys)
            {
                if (DerivedValues[derivation].CurrentValue == 0f && DerivedValues[derivation].BaseValue == 0f) toRemove.Add(derivation);
            }

            foreach (IAttributeImpactDerivation derivation in toRemove) Remove(derivation);
        }
        
        public void Clamp(AttributeValue ceil)
        {
            if (0 <= Value.CurrentValue && Value.CurrentValue <= ceil.CurrentValue && 0 <= Value.BaseValue &&
                Value.BaseValue <= ceil.BaseValue) return;
            
            float currDelta = 0f;
            if (Value.CurrentValue < 0) currDelta = -Value.CurrentValue;
            else if (Value.CurrentValue > ceil.CurrentValue) currDelta = ceil.CurrentValue - Value.CurrentValue;

            float baseDelta = 0f;
            if (Value.BaseValue < 0) baseDelta = -Value.BaseValue;
            else if (Value.BaseValue > ceil.BaseValue) baseDelta = ceil.BaseValue - Value.BaseValue;

            Value += new AttributeValue(currDelta, baseDelta);
        }

        public void Clamp(AttributeValue floor, AttributeValue ceil)
        {
            if (floor.CurrentValue <= Value.CurrentValue && Value.CurrentValue <= ceil.CurrentValue && floor.BaseValue <= Value.BaseValue &&
                Value.BaseValue <= ceil.BaseValue) return;
            
            float currDelta = 0f;
            if (Value.CurrentValue < floor.CurrentValue) currDelta = floor.CurrentValue - Value.CurrentValue;
            else if (Value.CurrentValue > ceil.CurrentValue) currDelta = ceil.CurrentValue - Value.CurrentValue;

            float baseDelta = 0f;
            if (Value.BaseValue < floor.BaseValue) baseDelta = floor.BaseValue - Value.BaseValue;
            else if (Value.BaseValue > ceil.BaseValue) baseDelta = ceil.BaseValue - Value.BaseValue;

            Value += new AttributeValue(currDelta, baseDelta);
        }

        public string FormattedString(Attribute attribute)
        {
            string s = $"[ CACHED-{attribute} ]\n";
            foreach (IAttributeImpactDerivation derivation in DerivedValues.Keys)
            {
                s += $"\t{derivation.GetEffectDerivation().GetName()} -> {DerivedValues[derivation]}\n";
            }

            return s;
        }
    }

    [Serializable]
    public struct AttributeOverflowData
    {
        public EAttributeOverflowPolicy Policy;
        public AttributeValue Floor;
        public AttributeValue Ceil;
    }

    public enum EAttributeOverflowPolicy
    {
        ZeroToBase,
        FloorToBase,
        ZeroToCeil,
        FloorToCeil,
        Unlimited
    }
}
