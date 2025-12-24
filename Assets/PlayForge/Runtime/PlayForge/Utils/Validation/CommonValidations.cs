using System;
using System.Buffers;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class CostValidation : IAbilityValidationRule
    {
        public bool Validate(AbilityDataPacket data, out string error)
        {
            error = "";
            if (data.Spec is not AbilitySpec ability) return false;
            if (ability.Base.Cost is null) return true;
            if (!ability.Owner.FindAttributeSystem(out var attrSys) ||
                !attrSys.TryGetAttributeValue(ability.Base.Cost.ImpactSpecification.AttributeTarget,
                    out AttributeValue attrVal)) return false;
            return attrVal.CurrentValue >=
                   ability.Base.Cost.ImpactSpecification.GetMagnitude(
                       ability.Owner.GenerateEffectSpec(ability, ability.Base.Cost));
        }
        public string GetName()
        {
            return "Cost Validation";
        }
    }

    public class CooldownValidation : IAbilityValidationRule
    {
        public bool Validate(AbilityDataPacket data, out string error)
        {
            error = "";
            if (data.Spec is not AbilitySpec ability) return false;
            if (ability.Base.Cooldown is null) return true;
            if (ability.Base.Cooldown.Tags.GrantedTags.Count <= 0) return true;
            return ability.Owner.GetLongestDurationFor(ability.Base.Cooldown.Tags.GrantedTags).DurationRemaining <= 0;
        }
        public string GetName()
        {
            return "Cooldown Validation";
        }
    }

    [Serializable]
    public class SourceAttributeValidation : AbstractAttributeValidationRule
    {
        public EComparisonOperator Comparison;
        public float Value;
        
        public override bool Validate(AbilityDataPacket data, out string error)
        {
            error = "";
            if (data.Spec is not AbilitySpec ability) return false;

            if (!ability.Owner.FindAttributeSystem(out var attrSys) ||
                !attrSys.TryGetAttributeValue(Attribute, out AttributeValue attrVal))
                return false;
            
            return Comparison switch
            {

                EComparisonOperator.GreaterThan => attrVal.CurrentValue > Value,
                EComparisonOperator.LessThan => attrVal.CurrentValue < Value,
                EComparisonOperator.GreaterOrEqualTo => attrVal.CurrentValue >= Value,
                EComparisonOperator.LessOrEqualTo => attrVal.CurrentValue <= Value,
                EComparisonOperator.Equal => Mathf.Approximately(attrVal.CurrentValue, Value),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        public override string GetName()
        {
            return "Source Attribute Validation";
        }
    }
    
    [Serializable]
    public class TargetAttributeValidation : AbstractAttributeValidationRule
    {
        public EComparisonOperator Comparison;
        public float Threshold;
        
        public override bool Validate(AbilityDataPacket data, out string error)
        {
            error = "";
            
            if (!data.TryGetTarget(EProxyDataValueTarget.Primary, out var targetObj) || !targetObj.FindAttributeSystem(out var attrSys) ||
                !attrSys.TryGetAttributeValue(Attribute, out AttributeValue attrVal))
                return false;
            
            return Comparison switch
            {

                EComparisonOperator.GreaterThan => attrVal.CurrentValue > Threshold,
                EComparisonOperator.LessThan => attrVal.CurrentValue < Threshold,
                EComparisonOperator.GreaterOrEqualTo => attrVal.CurrentValue >= Threshold,
                EComparisonOperator.LessOrEqualTo => attrVal.CurrentValue <= Threshold,
                EComparisonOperator.Equal => Mathf.Approximately(attrVal.CurrentValue, Threshold),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        public override string GetName()
        {
            return "Target Attribute Validation";
        }
    }

    public abstract class AbstractTagValidationRule : IAbilityValidationRule
    {
        public Tag Tag;

        public virtual DataWrapper GetValue(AbilityDataPacket data)
        {
            // By default, query value from TagCache
            var source = data.Spec.GetOwner();
            if (source.GetTagCache().TryGetWeight(Tag, out var value))
            {
                var wrapper = new DataWrapper
                {
                    floatValue = value,
                    intValue = value
                };
                return wrapper;
            }
            
            // Otherwise, search for tag in LocalData on Ability
            if (data.Spec is not AbilitySpec ability) return default;

            return !ability.Base.TryGetLocalData(Tag, out var dataWrapper) ? default : dataWrapper;

        }
        
        public abstract bool Validate(AbilityDataPacket data, out string error);
        public abstract string GetName();
    }
    
    public class RangeValidation : AbstractTagValidationRule
    {
        public override bool Validate(AbilityDataPacket data, out string error)
        {
            error = "";
            if (data.Spec is not AbilitySpec ability) return false;

            var value = GetValue(data);

            if (!data.TryGetTarget(EProxyDataValueTarget.Primary, out var targetObj)) return false;
            var target = targetObj.AsGAS()?.ToGASObject();
            if (target is null) return false;
            
            var source = data.Spec.GetOwner().AsGAS()?.ToGASObject();
            if (source is null) return false;
            
            return Vector3.Distance(source.transform.position, target.transform.position) <= value.floatValue;
        }
        public override string GetName()
        {
            return "Range Validation";
        }
    }
}