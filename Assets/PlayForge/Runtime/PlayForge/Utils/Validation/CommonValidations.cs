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

            // Its attribute costs, each paid beside its Cost effect (TW-29)
            if (!ability.CanPayAttributeCosts(data)) return false;
            if (ability.Base.Cost is null) return true;

            // Generating the spec rebuilds the cost's impacts, and each attribute it spends is checked: a cost can spend
            // more than one, and only the first was (ED-3). The caster must have each attribute
            var spec = ability.Source.GenerateEffectSpec(ability, ability.Base.Cost);
            var impacts = ability.Base.Cost.ImpactSpecification.Entries;
            if (impacts.Count == 0) return false;
            foreach (var impact in impacts)
            {
                if (impact == null) continue;
                if (!ability.Source.TryGetAttributeValue(impact.AttributeTarget, out var attrVal)) return false;

                // A cost's magnitude is the signed change it makes, so what matters is what that leaves. Comparing
                // the value with the magnitude itself passed every cost, as a cost is negative (GAS-7)
                float change = impact.Magnitude?.Evaluate(spec) ?? 0f;
                if (attrVal.CurrentValue + change < 0f) return false;
            }
            return true;
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

            // With its charges counted, it's refused only with none in hand, whatever else carries its cooldown's tag (TW-17)
            if (ability.Charges.Counted) return ability.Charges.InHand > 0;
            return !ability.OnCooldown;
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

        /// <summary>
        /// Compares the attribute's share of its base, from 0 to 1, in place of its value: "a target below half its
        /// health" whatever its health. It compared the value alone, right for one entity and wrong for the next (FEAT-26).
        /// </summary>
        [Tooltip("Compare the attribute's share of its base, from 0 to 1, in place of its value")]
        public bool ShareOfBase;
        
        public override bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            error = "Failed Attribute Validation";
            var source = getSource?.Invoke();
            if (source is null) return false;
            
            if (!source.TryGetAttributeValue(Attribute, out var attrVal))
                return false;

            // Its share of its base, where the rule asks for one (FEAT-26)
            float value = !ShareOfBase ? attrVal.CurrentValue
                : attrVal.BaseValue > 0f ? attrVal.CurrentValue / attrVal.BaseValue : 0f;
            return Comparison switch
            {
                EComparisonOperator.GreaterThan => value > Value,
                EComparisonOperator.LessThan => value < Value,
                EComparisonOperator.GreaterOrEqual => value >= Value,
                EComparisonOperator.LessOrEqual => value <= Value,
                EComparisonOperator.Equal => Mathf.Approximately(value, Value),
                EComparisonOperator.NotEqual => !Mathf.Approximately(value, Value),
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