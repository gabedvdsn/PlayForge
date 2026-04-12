using System;
using System.Collections;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public struct AttributeValue
    {
        public float CurrentValue;
        public float BaseValue;

        public float Ratio => CurrentValue / BaseValue;
        public float RatioMinZero => BaseValue > 0 ? CurrentValue / BaseValue : 0f;
        public float LerpedRatio(float min) => Mathf.Lerp(min, BaseValue, CurrentValue);
        public bool ContainsImpact => CurrentValue != 0 || BaseValue != 0;

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

        public ModifiedAttributeValue ToModified => new ModifiedAttributeValue(CurrentValue, BaseValue);

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
        
        public static bool operator ==(AttributeValue a, AttributeValue b)
        {
            return Mathf.Approximately(a.CurrentValue, b.CurrentValue) 
                   && Mathf.Approximately(a.BaseValue, b.BaseValue);
        }
        public static bool operator !=(AttributeValue a, AttributeValue b)
        {
            return !(a == b);
        }
        
        public bool Equals(AttributeValue other)
        {
            return Mathf.Approximately(CurrentValue, other.CurrentValue) 
                   && Mathf.Approximately(BaseValue, other.BaseValue);
        }
        public override bool Equals(object obj)
        {
            return obj is AttributeValue other && Equals(other);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(CurrentValue, BaseValue);
        }

        public AttributeValue BaseAligned() => new AttributeValue(BaseValue, BaseValue);

        public static bool WithinLimits(AttributeValue value, AttributeValue floor, AttributeValue ceil)
        {
            return floor.CurrentValue <= value.CurrentValue 
                   && value.CurrentValue <= ceil.CurrentValue 
                   && floor.BaseValue <= value.BaseValue
                   && value.BaseValue <= ceil.BaseValue;
        }
        
        public override string ToString()
        {
            return $"{CurrentValue}/{BaseValue}";
        }
    }

    public struct AttributeValueClamped
    {
        public int CurrentValue;
        
        public int MinValue;
        public int MaxValue;

        public float Ratio => Mathf.Lerp(MinValue, MaxValue, CurrentValue);
        public bool ContainsImpact => CurrentValue != 0;

        public AttributeValueClamped(int value) : this()
        {
            CurrentValue = value;
            MinValue = value;
            MaxValue = value;
        }

        public AttributeValueClamped(int value, int maxValue) : this()
        {
            CurrentValue = value;
            MinValue = value;
            MaxValue = maxValue;
        }

        public AttributeValueClamped(int value, int minValue, int maxValue)
        {
            CurrentValue = value;
            MinValue = minValue;
            MaxValue = maxValue;
        }
    }
    
}
