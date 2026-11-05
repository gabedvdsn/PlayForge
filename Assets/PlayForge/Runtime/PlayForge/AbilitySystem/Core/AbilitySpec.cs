using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilitySpec : BasicEffectOrigin
    {
        public Ability Base;
        
        public AbilitySpec(ISource source, Ability ability, int level) : base(source, new IntValuePairClamped(level, ability.StartingLevel, ability.MaxLevel), ability.Tags.AssetTag)
        {
            Base = ability;
        }

        public void ApplyUsageEffects()
        {
            // Apply cost and cooldown effects
            if (Base.Cooldown is not null)
                Source.ApplyCarrier(Source.GenerateEffectSpec(this, Base.Cooldown));

            if (Base.Cost is not null)
                Source.ApplyCarrier(Source.GenerateEffectSpec(this, Base.Cost));
        }
            
        public bool ValidateSourceActivationRequirements(AbilityDataPacket data) => SourceRefusal(data, out _) == EActivationRefusal.None;

        /// <summary>
        /// What of the caster refuses the activation, or None: its tags against the ability's source requirements, then
        /// each caster check, which a cooldown, a cost or a death names, and any other by its name (FEAT-17).
        /// </summary>
        internal EActivationRefusal SourceRefusal(AbilityDataPacket data, out string rule)
        {
            rule = null;
            var owner = data.EffectOrigin.GetSource();

            // The ability's own source tag requirements ("can't use while stunned") are part of
            // activation, alongside the validation rules. Missing groups mean no requirement.
            if (!(Base.Tags?.TagRequirements?.CheckSourceRequirements(owner.GetAppliedTags()) ?? true)) return EActivationRefusal.Requirements;

            // An empty rule slot, which a list's "+" can leave, asks nothing; it threw (ED-12)
            foreach (var check in Base.SourceActivationRules)
            {
                if (check == null || check.Validate(data, () => owner, out _)) continue;
                switch (check)
                {
                    case CooldownValidation: return EActivationRefusal.Cooldown;
                    case CostValidation: return EActivationRefusal.Cost;
                    case IsAliveValidation: return EActivationRefusal.Dead;
                    default:
                        rule = check.GetName();
                        return EActivationRefusal.Rule;
                }
            }
            return EActivationRefusal.None;
        }

        public bool ValidateAllActivationRequirements(ITarget target, AbilityDataPacket data)
        {
            return ValidateSourceActivationRequirements(data)
                   && Base.TargetActivationRules.All(rule => rule == null || rule.Validate(data, () => target, out _))
                   && Base.Tags.ValidateTargetRequirements(target) ;
        }

        public override ISource GetSource() => Source;
        public override IHasReadableDefinition GetReadableDefinition()
        {
            return Base;
        }
        public override List<Tag> GetContextTags()
        {
            return Base.Tags.ContextTags;
        }
        public override Tag GetAssetTag()
        {
            return Base.Tags.AssetTag;
        }
        public override IntValuePairClamped GetLevel() => Source.GetLevel(GetAssetTag());
        public override List<Tag> GetAffiliation()
        {
            return Source.GetAffiliation();
        }
        public override bool IsActive()
        {
            return Source.FindAbilitySystem(out var ab) && ab.TryGetAbilityContainer(Base, out var container) && container.IsActive;
        }
        public bool RetainEffectImpact()
        {
            return false;
        }

        public override string ToString()
        {
            return Base.ToString();
        }
    }
}
