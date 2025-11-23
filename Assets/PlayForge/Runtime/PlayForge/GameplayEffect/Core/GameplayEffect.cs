using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class GameplayEffect : IHasReadableDefinition
    {
        public GameplayEffectDefinition Definition;
        public GameplayEffectTags Tags;
        
        public GameplayEffectImpactSpecification ImpactSpecification;
        public GameplayEffectDurationSpecification DurationSpecification;
        
        public List<AbstractEffectWorker> Workers;
        
        public TagRequirements SourceRequirements;
        public TagRequirements TargetRequirements;

        public GameplayEffect()
        {
        }

        public GameplayEffect(GameplayEffect o)
        {
            Definition = o.Definition;
            Tags = o.Tags;

            ImpactSpecification = new GameplayEffectImpactSpecification(o.ImpactSpecification);
            DurationSpecification = new GameplayEffectDurationSpecification(o.DurationSpecification);

            Workers = new List<AbstractEffectWorker>(o.Workers);
            
        }

        public GameplayEffectSpec Generate(IEffectOrigin origin, GASComponent target)
        {
            GameplayEffectSpec spec = new GameplayEffectSpec(this, origin, target);
            ImpactSpecification.ApplyImpactSpecifications(spec);
            
            return spec;
        }
        
        #region Effect Base

        public bool ValidateApplicationRequirements(GameplayEffectSpec spec)
        {
            var targetTags = spec.Target.TagCache.GetAppliedTags();
            var sourceTags = spec.Source.GetAppliedTags();
            return TargetRequirements.CheckApplicationRequirements(targetTags)
                   && !TargetRequirements.CheckRemovalRequirements(targetTags)
                   && SourceRequirements.CheckApplicationRequirements(sourceTags)
                   && !SourceRequirements.CheckRemovalRequirements(sourceTags);
        }
        public bool ValidateRemovalRequirements(GameplayEffectSpec spec)
        {
            return TargetRequirements.CheckRemovalRequirements(spec.Target.TagCache.GetAppliedTags())
                   && SourceRequirements.CheckRemovalRequirements(spec.Source.GetAppliedTags());
        }
        public bool ValidateOngoingRequirements(GameplayEffectSpec spec)
        {
            return TargetRequirements.CheckOngoingRequirements(spec.Target.TagCache.GetAppliedTags())
                   && SourceRequirements.CheckOngoingRequirements(spec.Source.GetAppliedTags());
        }
        public void ApplyDurationSpecifications(AbstractGameplayEffectShelfContainer container)
        {
            DurationSpecification.ApplyDurationSpecifications(container);
        }
        #endregion

        public override string ToString()
        {
            return $"GE-{Definition.Name}";
        }
        public string GetName()
        {
            return Definition.Name;
        }
        public string GetDescription()
        {
            return Definition.Description;
        }
        public Sprite GetPrimaryIcon()
        {
            return Definition.Icon;
        }
    }

    public class GameplayEffectDefinition
    {
        [ForgeLinkField("Name")] public string Name;
        [ForgeLinkField("Description")] public string Description;
        [ForgeCategory(Forge.Categories.Visibility)] public Tag Visibility;
        [ForgeLinkField("Icon")] public Sprite Icon;
    }

    public interface IHasReadableDefinition
    {
        public string GetName();
        public string GetDescription();
        public Sprite GetPrimaryIcon();
    }

    public struct GameplayEffectTags
    {
        [ForgeLinkTag("Name")]
        [ForgeCategory(Forge.Categories.Identifier)]
        public Tag AssetTag;
        
        [ForgeCategory(Forge.Categories.Context)]
        public List<Tag> ContextTags;
        public List<Tag> GrantedTags;
    }
    
    public enum EEffectReApplicationPolicy
    {
        Append,  // Create another instance of the effect independent of the existing one(s)
        Refresh,  // Refresh the duration of the effect
        Extend,  // Extend the duration of the effect
        Stack,  // Inject a duration-independent stack of the effect into the existing one 
        StackRefresh,  // Stack and refresh the duration of each stack
        StackExtend  // Stacks and extend the duration of each stack
    }

    /// <summary>
    /// Sources of Gameplay Effects
    /// </summary>
    public interface IEffectOrigin
    {
        public ISource GetOwner();
        public Tag[] GetContextTags();
        public Tag GetAssetTag();
        public int GetLevel();
        public void SetLevel(int level);
        public float GetRelativeLevel();
        public string GetName();
        public List<Tag> GetAffiliation();

        public static SourceEffectOrigin GenerateSourceDerivation(ISource source)
        {
            return new SourceEffectOrigin(source);
        }
    }

    public class SourceEffectOrigin : IEffectOrigin
    {
        private ISource Owner;

        public SourceEffectOrigin(ISource owner)
        {
            Owner = owner;
        }

        public ISource GetOwner()
        {
            return Owner;
        }
        public Tag[] GetContextTags()
        {
            return Owner.GetContextTags();
        }
        public Tag GetAssetTag()
        {
            return Owner.GetAssetTag();
        }
        public int GetLevel()
        {
            return Owner.GetLevel();
        }
        public void SetLevel(int level)
        {
            Owner.SetLevel(level);
        }
        public float GetRelativeLevel()
        {
            return (Owner.GetLevel() - 1) / (float)(Owner.GetMaxLevel() - 1);
        }
        public string GetName()
        {
            return Owner.GetName();
        }
        public List<Tag> GetAffiliation()
        {
            return Owner.GetAffiliation();
        }
    }

}
