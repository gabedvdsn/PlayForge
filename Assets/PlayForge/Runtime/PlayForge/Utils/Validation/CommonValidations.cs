using System.Buffers;
using UnityEngine;

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
    }

    public class SourceIsAliveValidation : IAbilityValidationRule
    {
        public bool Validate(AbilityDataPacket data, out string error)
        {
            error = "";
            if (data.Spec is not AbilitySpec ability) return false;

            if (!ability.Owner.FindAttributeSystem(out var attrSys) ||
                !attrSys.TryGetAttributeValue(Attribute.Generate("Health", ""), out AttributeValue attrVal))
                return false;
            return attrVal.CurrentValue > 0;
        }
    }
    
    public class TargetIsAliveValidation : IAbilityValidationRule
    {
        public bool Validate(AbilityDataPacket data, out string error)
        {
            error = "";
            if (data.Spec is not AbilitySpec ability) return false;

            if (!data.TryGetTarget(EProxyDataValueTarget.Primary, out var targetObj) || !targetObj.FindAttributeSystem(out var attrSys) ||
                !attrSys.TryGetAttributeValue(Attribute.Generate("Health", ""), out AttributeValue attrVal))
                return false;
            return attrVal.CurrentValue > 0;
        }
    }
    
    public class RangeValidation : IAbilityValidationRule
    {
        public bool Validate(AbilityDataPacket data, out string error)
        {
            error = "";
            if (data.Spec is not AbilitySpec ability) return false;
            
            var tag = GameRoot.ResolveTag("Range");
            if (!ability.Base.LocalData.TryGetValue(tag, out var rangeObj)) return true;
            if (rangeObj is not float range) return false;

            if (!data.TryGetTarget(EProxyDataValueTarget.Primary, out var targetObj)) return false;
            var target = targetObj.AsGAS()?.ToGASObject();
            if (target is null) return false;
            
            var source = data.Spec.GetOwner().AsGAS()?.ToGASObject();
            if (source is null) return false;
            
            return Vector3.Distance(source.transform.position, target.transform.position) <= range;
        }
    }
}