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

    public class CachedAttributeValue
    {
        public Dictionary<IAttributeImpactDerivation, AttributeValue> DerivedValues = new();
        public AttributeValue Value;
        public AttributeBlueprint Blueprint;

        public static CachedAttributeValue GenerateNull()
        {
            var cav = new CachedAttributeValue(null, null, default);
            cav.DerivedValues = null;

            return cav;
        }
        
        public CachedAttributeValue(Attribute attribute, ISource source, AttributeBlueprint blueprint)
        {
            Blueprint = blueprint;
            
            Add(IAttributeImpactDerivation.GenerateSourceDerivation(source, attribute, blueprint.RetentionGroup, Tags.IGNORE), blueprint.ToAttributeValue());
        }
        
        public void Add(IAttributeImpactDerivation derivation, AttributeValue attributeValue)
        {
            if (derivation.AttributeRetention() != Tags.IGNORE)
            {
                if (DerivedValues.ContainsKey(derivation)) DerivedValues[derivation] += attributeValue;
                else DerivedValues[derivation] = attributeValue;
            }

            Value += attributeValue;
        }

        public void Add(IAttributeImpactDerivation derivation, ModifiedAttributeValue modifiedAttributeValue)
        {
            if (derivation.AttributeRetention() != Tags.IGNORE)
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

        /// <summary>
        /// </summary>
        public void ApplyBounds()
        {
            var newValue = Value;
            
            if (Blueprint.Constraints.AutoClamp)
            {
                switch (Blueprint.Overflow.Policy)
                {
                    case EAttributeOverflowPolicy.ZeroToBase:
                        newValue = new AttributeValue(
                            Mathf.Clamp(Value.CurrentValue, 0, Value.BaseValue),
                            Value.BaseValue);
                        break;
                    case EAttributeOverflowPolicy.FloorToBase:
                        newValue = new AttributeValue(
                            Mathf.Clamp(Value.CurrentValue, Blueprint.Overflow.Floor.CurrentValue, Value.BaseValue),
                            Value.BaseValue);
                        break;
                    case EAttributeOverflowPolicy.ZeroToCeil:
                        newValue = new AttributeValue(
                            Mathf.Clamp(Value.CurrentValue, 0, Blueprint.Overflow.Ceil.CurrentValue),
                            Mathf.Clamp(Value.BaseValue, 0, Blueprint.Overflow.Ceil.BaseValue)
                        );
                        break;
                    case EAttributeOverflowPolicy.FloorToCeil:
                        newValue = new AttributeValue(
                            Mathf.Clamp(Value.CurrentValue, Blueprint.Overflow.Floor.CurrentValue, Blueprint.Overflow.Ceil.CurrentValue),
                            Mathf.Clamp(Value.BaseValue, Blueprint.Overflow.Floor.BaseValue, Blueprint.Overflow.Ceil.BaseValue)
                        );
                        break;
                    case EAttributeOverflowPolicy.Unlimited:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            Value = newValue;
        }

        public void EnforceScaling(WorkerContext ctx)
        {
            if (!Blueprint.Constraints.AutoScaleWithBase || Value.BaseValue == 0) return;

            float oldBase = Value.BaseValue - ctx.Change.Value.BaseValue;
            if (Mathf.Approximately(oldBase, 0f)) return;
            float proportion = ctx.Change.Value.BaseValue / oldBase;  // change / oldBase
            
            float delta = proportion * Value.CurrentValue;
            
            var derivation = IAttributeImpactDerivation.GenerateSourceDerivation(
                ctx.Change.Value, Tags.IGNORE, Tags.IGNORE);
            var scaleAmount = new SourcedModifiedAttributeValue(derivation, delta, 0f, false);
            
            ctx.ActionQueue.Enqueue(new ModifyAttributeAction(
                ctx.System, ctx.Change.Value.BaseDerivation.GetAttribute(), scaleAmount, false)
            );
        }

        public void ApplyRounding()
        { 
            var newValue = Blueprint.Constraints.RoundingMode switch
            {
                EAttributeRoundingPolicy.None => Value,
                EAttributeRoundingPolicy.ToFloor => new AttributeValue(
                    Mathf.Floor(Value.CurrentValue),
                    Mathf.Floor(Value.BaseValue)),
                EAttributeRoundingPolicy.ToCeil => new AttributeValue(
                    Mathf.Ceil(Value.CurrentValue),
                    Mathf.Ceil(Value.BaseValue)),
                EAttributeRoundingPolicy.Round => new AttributeValue(
                    Mathf.Round(Value.CurrentValue),
                    Mathf.Round(Value.BaseValue)),
                EAttributeRoundingPolicy.SnapTo => Mathf.Approximately(Blueprint.Constraints.SnapInterval, 0f)
                ? Value
                : new AttributeValue(
                    Mathf.Round(Value.CurrentValue / Blueprint.Constraints.SnapInterval) * Blueprint.Constraints.SnapInterval,
                    Mathf.Round(Value.BaseValue / Blueprint.Constraints.SnapInterval) * Blueprint.Constraints.SnapInterval),
                _ => throw new ArgumentOutOfRangeException()
            };

            Value = newValue;
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
}
