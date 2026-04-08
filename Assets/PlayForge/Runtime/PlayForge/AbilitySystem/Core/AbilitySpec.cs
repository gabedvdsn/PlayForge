using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilitySpec : BasicEffectOrigin, IEffectOrigin
    {
        public Ability Base;
        
        public AbilitySpec(ISource source, Ability ability, int level) : base(source, new AttributeValueClamped(level, ability.MaxLevel))
        {
            Base = ability;
        }

        public void ApplyUsageEffects()
        {
            // Apply cost and cooldown effects
            if (Base.Cooldown is not null) 
                Source.ApplyGameplayEffect(Source.GenerateEffectSpec(this, Base.Cooldown));

            if (Base.Cost is not null) 
                Source.ApplyGameplayEffect(Source.GenerateEffectSpec(this, Base.Cost));
        }
            
        public bool ValidateSourceActivationRequirements(AbilityDataPacket data)
        {
            var owner = data.Spec.GetOwner();
            return Base.SourceActivationRules.All(rule => rule.Validate(data, () => owner, out _));
        }

        public bool ValidateAllActivationRequirements(ITarget target, AbilityDataPacket data)
        {
            return ValidateSourceActivationRequirements(data)
                   && Base.TargetActivationRules.All(rule => rule.Validate(data, () => target, out _))
                   && Base.Tags.ValidateTargetRequirements(target) ;
        }

        public override ISource GetOwner() => Source;
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
        public override int GetLevel() => GetLeveler().Level.CurrentValue;
        public override float GetRelativeLevel() => GetLeveler().Level.Ratio;
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
