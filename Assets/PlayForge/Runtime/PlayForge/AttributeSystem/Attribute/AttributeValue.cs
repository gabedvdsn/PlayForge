using System;
using System.Collections;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An attribute's two values as a pair: Base, the value it's measured against, and Current, the value in play, as a
    /// pool's maximum and what's left of it. An entity's attribute reads as one, and so do a change, a bound and a
    /// threshold; its operators work on both values at once, two are equal where each value is approximately, and
    /// <c>Ratio</c>, <c>ClampedRatio</c> and <c>LerpedRatio</c> read Current against Base.
    /// </summary>
    [Serializable]
    public struct AttributeValue
    {
        public float CurrentValue;
        public float BaseValue;

        public float Ratio => CurrentValue / BaseValue;
        public float ClampedRatio => BaseValue > 0 ? Mathf.Clamp01(CurrentValue / BaseValue) : 0f;
        public float LerpedRatio(float min = 0f) => Mathf.InverseLerp(min, BaseValue, CurrentValue);
        public bool ContainsImpact => CurrentValue != 0 || BaseValue != 0;

        public AttributeValue(float currentValue, float baseValue)
        {
            CurrentValue = currentValue;
            BaseValue = baseValue;
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

}
