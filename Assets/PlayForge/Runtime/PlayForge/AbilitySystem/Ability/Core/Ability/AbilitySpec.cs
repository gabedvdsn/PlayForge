using System;
using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge
{
    public class AbilitySpec : IEffectOrigin, ITagReadable, IValidationReady
    {
        public ISource Owner;
        public Ability Base;
        public int Level;
        public float RelativeLevel => (Level - 1) / (float)(Base.MaxLevel - 1);
        
        public AbilitySpec(ISource owner, Ability ability, int level)
        {
            Owner = owner;
            Base = ability;
            Level = level;
        }

        public void ApplyUsageEffects()
        {
            // Apply cost and cooldown effects
            if (Base.Cooldown is not null && Base.Cooldown.Tags.GrantedTags.Count > 0) 
                Owner.ApplyGameplayEffect(Owner.GenerateEffectSpec(this, Base.Cooldown));

            if (Base.Cost is not null) 
                Owner.ApplyGameplayEffect(Owner.GenerateEffectSpec(this, Base.Cost));
        }
            
        public bool ValidateActivationRequirements()
        {
            return !(GetCooldown().DurationRemaining > 0f)
                   && CanCoverCost()
                   && Base.Tags.ValidateSourceRequirements(Owner);
        }

        public bool ValidateActivationRequirements(ITarget target)
        {
            return ValidateActivationRequirements()
                   && Base.Tags.ValidateTargetRequirements(target);
        }

        public GameplayEffectDuration GetCooldown()
        {
            if (Base.Cooldown is null || !(Base.Cooldown.Tags.GrantedTags.Count > 0)) return default;
            return Owner.GetLongestDurationFor(Base.Cooldown.Tags.GrantedTags);
        }

        public bool CanCoverCost()
        {
            if (Base.Cost is null) return true;
            if (!Owner.FindAttributeSystem(out var attr) || !attr.TryGetAttributeValue(Base.Cost.ImpactSpecification.AttributeTarget, out AttributeValue attributeValue)) return false;
            return attributeValue.CurrentValue >= Base.Cost.ImpactSpecification.GetMagnitude(Owner.GenerateEffectSpec(this, Base.Cost));
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
