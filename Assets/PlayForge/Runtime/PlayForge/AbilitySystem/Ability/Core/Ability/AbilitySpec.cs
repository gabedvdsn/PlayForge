using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class AbilitySpec : IEffectOrigin, ITagReadable, IValidationReady
    {
        public ISource Owner;
        public Ability Base;
        public int Level;
        public float RelativeLevel => Base.MaxLevel > 1 ? (Level - 1) / (float)(Base.MaxLevel - 1) : 1f;
        
        public AbilitySpec(ISource owner, Ability ability, int level)
        {
            Owner = owner;
            Base = ability;
            Level = level;
        }

        public void ApplyUsageEffects()
        {
            // Apply cost and cooldown effects
            if (Base.Cooldown is not null) 
                Owner.ApplyGameplayEffect(Owner.GenerateEffectSpec(this, Base.Cooldown));

            if (Base.Cost is not null) 
                Owner.ApplyGameplayEffect(Owner.GenerateEffectSpec(this, Base.Cost));
        }
            
        public bool ValidateSourceActivationRequirements(AbilityDataPacket data)
        {
            return Base.SourceActivationRules.All(rule => rule.Validate(data, data.Spec.GetOwner, out _));
            /*return !(GetCooldown().DurationRemaining > 0f)
                   && CanCoverCost()
                   && Base.Tags.ValidateSourceRequirements(Owner);*/
        }

        public bool ValidateAllActivationRequirements(ITarget target, AbilityDataPacket data)
        {
            return ValidateSourceActivationRequirements(data)
                   && Base.TargetActivationRules.All(rule => rule.Validate(data, () => target, out _))
                   && Base.Tags.ValidateTargetRequirements(target) ;
        }

        public ISource GetOwner() => Owner;
        public List<Tag> GetContextTags()
        {
            return Base.Tags.ContextTags;
        }
        public Tag GetAssetTag()
        {
            return Base.Tags.AssetTag;
        }
        public int GetLevel() => Level;
        public void SetLevel(int level) => Level = level;
        public float GetRelativeLevel() => RelativeLevel;
        public string GetName() => Base.GetName();
        public List<Tag> GetAffiliation()
        {
            return Owner.GetAffiliation();
        }
        public bool IsActive()
        {
            return Owner.FindAbilitySystem(out var ab) && ab.TryGetAbilityContainer(Base, out var container) && container.IsActive;
        }

        public override string ToString()
        {
            return Base.ToString();
        }
        public ITagReadableReport Read()
        {
            return new AbilityReport(this, Owner.GetLongestDurationFor(Base.Tags.AssetTag));
        }
    }

    public struct AbilityReport : ITagReadableReport
    {
        public AbilitySpec Spec;
        public GameplayEffectDuration Cooldown;

        public AbilityReport(AbilitySpec spec, GameplayEffectDuration cooldown)
        {
            Spec = spec;
            Cooldown = cooldown;
        }
    }
}
