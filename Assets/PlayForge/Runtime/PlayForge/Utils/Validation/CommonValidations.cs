using System;
using System.Buffers;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class CostValidation : IAbilityValidationRule
    {
        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = "";
            if (data.EffectOrigin is not AbilitySpec ability) return false;
            if (ability.Base.Cost is null) return true;
            if (!ability.Source.TryGetAttributeValue(ability.Base.Cost.ImpactSpecification.AttributeTarget,
                    out var attrVal)) return false;

            // A cost's magnitude is the signed change it makes, so what matters is what that leaves. Comparing
            // the value with the magnitude itself passed every cost, as a cost is negative (GAS-7)
            float change = ability.Base.Cost.ImpactSpecification.GetMagnitude(
                ability.Source.GenerateEffectSpec(ability, ability.Base.Cost));

            return attrVal.CurrentValue + change >= 0f;
        }
        public string GetName()
        {
            return "Cost Validation";
        }
    }

    public class CooldownValidation : IAbilityValidationRule
    {
        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = "";
            if (data.EffectOrigin is not AbilitySpec ability) return false;
            if (ability.Base.Cooldown is null) return true;
            return !ability.Source.GetTagCache().TryGetWeight(ability.Base.Cooldown.Tags.AssetTag, out _);
        }
        public string GetName()
        {
            return "Cooldown Validation";
        }
    }

    [Serializable]
    public class IsAliveValidation : IAbilityValidationRule
    {

        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = "";
            return !getSource?.Invoke().IsDead ?? false;
        }
        public string GetName()
        {
            return "Is Alive Validation";
        }
    }

    [Serializable]
    public class AttributeValidation : AbstractAttributeValidationRule
    {
        public EComparisonOperator Comparison;
        public float Value;
        
        public override bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = "Failed Attribute Validation";
            var source = getSource?.Invoke();
            if (source is null) return false;
            
            if (!source.TryGetAttributeValue(Attribute, out var attrVal))
                return false;
            
            return Comparison switch
            {
                EComparisonOperator.GreaterThan => attrVal.CurrentValue > Value,
                EComparisonOperator.LessThan => attrVal.CurrentValue < Value,
                EComparisonOperator.GreaterOrEqual => attrVal.CurrentValue >= Value,
                EComparisonOperator.LessOrEqual => attrVal.CurrentValue <= Value,
                EComparisonOperator.Equal => Mathf.Approximately(attrVal.CurrentValue, Value),
                EComparisonOperator.NotEqual => !Mathf.Approximately(attrVal.CurrentValue, Value),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        public override string GetName()
        {
            return "Source Attribute Validation";
        }
    }

    public abstract class AbstractTagValidationRule : IAbilityValidationRule
    {
        [ForgeTagContext(ForgeContext.Required, ForgeContext.Granted, ForgeContext.AssetIdentifier)]
        public Tag Tag;

        public virtual DataWrapper GetValue(AbilityDataPacket data)
        {
            // By default, query value from TagCache
            var source = data.EffectOrigin.GetSource();
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
            if (data.EffectOrigin is not AbilitySpec ability) return default;

            return !ability.Base.TryGetLocalData(Tag, out var dataWrapper) ? default : dataWrapper;

        }
        
        public abstract bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error);
        public abstract string GetName();
    }
    
    public class CheckRangeValidation : AbstractTagValidationRule
    {
        public override bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = "";
            if (data.EffectOrigin is not AbilitySpec ability) return false;

            var value = GetValue(data);
            if (value is null) return false;

            if (!data.TryGetTarget(EDataTarget.Primary, out var targetObj)) return false;

            // From the targeting packets, which both hubs have. From their GameObjects, a runtime GAS at either end,
            // which has none, failed every check (GAS-29)
            var target = targetObj?.GetTargetingPacket();
            if (target is null) return false;

            var source = data.EffectOrigin.GetSource()?.GetTargetingPacket();
            if (source is null) return false;

            return Vector3.Distance(source.position, target.position) <= value.floatValue;
        }
        public override string GetName()
        {
            return "Range Validation";
        }
    }
}