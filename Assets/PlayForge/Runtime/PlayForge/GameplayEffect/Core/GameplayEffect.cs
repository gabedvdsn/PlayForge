using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Gameplay Effect", fileName = "Effect_")]
    public class GameplayEffect : BaseForgeObject, IHasReadableDefinition
    {
        public GameplayEffectDefinition Definition;
        public GameplayEffectTags Tags;
        
        public GameplayEffectImpactSpecification ImpactSpecification;
        public GameplayEffectDurationSpecification DurationSpecification;
        
        [SerializeReference]
        public List<AbstractEffectWorker> Workers;
        
        public TagRequirements SourceRequirements;
        public TagRequirements TargetRequirements;

        [SerializeReference]
        public List<DataWrapper> LocalData;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Level Provider Linking
        // ═══════════════════════════════════════════════════════════════════════════

        public EEffectLinkMode LinkMode;
        
        /// <summary>
        /// Optional link to a level provider (Ability or EntityIdentity).
        /// When set, this effect can derive its max level from the provider
        /// for "Lock to Source" modifier scaling.
        /// </summary>
        [Tooltip("Link this effect to an Ability or Entity to derive max level from it")]
        [SerializeField]
        [LinkedSource]
        private BaseForgeLinkProvider _linkedSource;
        
        /// <summary>
        /// Gets the raw linked ScriptableObject (for serialization/editor purposes).
        /// </summary>
        public BaseForgeLinkProvider LinkedProvider
        {
            get => _linkedSource;
            set => _linkedSource = value;
        }
        
        /// <summary>
        /// Returns true if this effect is linked to a level provider.
        /// </summary>
        public bool IsLinked => LinkMode == EEffectLinkMode.LinkedToProvider && LinkedProvider != null;
        
        /// <summary>
        /// Links this effect to a level provider.
        /// </summary>
        /// <param name="provider">The ScriptableObject that implements ILevelProvider</param>
        /// <returns>True if successfully linked</returns>
        public bool LinkToProvider(BaseForgeLinkProvider provider)
        {
            if (provider == null)
            {
                Unlink();
                return true;
            }

            LinkedProvider = provider;
            LinkMode = EEffectLinkMode.LinkedToProvider;
            return true;
        }
        
        /// <summary>
        /// Removes any existing link.
        /// </summary>
        public void Unlink()
        {
            LinkedProvider = null;
            LinkMode = EEffectLinkMode.Standalone;
        }
        
        /// <summary>
        /// Checks if this effect is linked to a specific provider.
        /// </summary>
        public bool IsLinkedTo(ScriptableObject provider)
        {
            return IsLinked && LinkedProvider == provider;
        }
        
        /// <summary>
        /// Gets the max level from the linked provider, or a default value if not linked.
        /// </summary>
        /// <param name="defaultMaxLevel">Value to return if not linked (default: 1)</param>
        public int GetLinkedMaxLevel(int defaultMaxLevel = 1)
        {
            return LinkedProvider?.GetMaxLevel() ?? defaultMaxLevel;
        }
        
        /// <summary>
        /// Gets the starting level from the linked provider, or a default value if not linked.
        /// </summary>
        /// <param name="defaultStartLevel">Value to return if not linked (default: 0)</param>
        public int GetLinkedStartingLevel(int defaultStartLevel = 0)
        {
            return LinkedProvider?.GetStartingLevel() ?? defaultStartLevel;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Effect Generation
        // ═══════════════════════════════════════════════════════════════════════════

        public GameplayEffectSpec Generate(IEffectOrigin origin, IGameplayAbilitySystem target)
        {
            var spec = new GameplayEffectSpec(this, origin, target);
            ImpactSpecification.ApplyImpactSpecifications(spec);
            
            return spec;
        }
        
        #region Effect Base

        public bool ValidateApplicationRequirements(GameplayEffectSpec spec)
        {
            var targetTags = spec.Target.GetAppliedTags();
            var sourceTags = spec.Source.GetAppliedTags();
            bool result = TargetRequirements.CheckApplicationRequirements(targetTags)
                          && !TargetRequirements.CheckRemovalRequirements(targetTags)
                          && SourceRequirements.CheckApplicationRequirements(sourceTags)
                          && !SourceRequirements.CheckRemovalRequirements(sourceTags);
            return result;
        }
        
        public bool ValidateRemovalRequirements(GameplayEffectSpec spec)
        {
            return TargetRequirements.CheckRemovalRequirements(spec.Target.GetAppliedTags())
                   && SourceRequirements.CheckRemovalRequirements(spec.Source.GetAppliedTags());
        }
        
        public bool ValidateOngoingRequirements(GameplayEffectSpec spec)
        {
            return TargetRequirements.CheckOngoingRequirements(spec.Target.GetAppliedTags())
                   && SourceRequirements.CheckOngoingRequirements(spec.Source.GetAppliedTags());
        }
        
        public void ApplyDurationSpecifications(AbstractGameplayEffectShelfContainer container)
        {
            DurationSpecification.ApplyDurationSpecifications(container);
        }
        
        #endregion

        // ═══════════════════════════════════════════════════════════════════════════
        // IHasReadableDefinition Implementation
        // ═══════════════════════════════════════════════════════════════════════════

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
        
        public Texture2D GetPrimaryIcon()
        {
            if (LocalData.TryGet(Tag.Generate("PrimaryIcon"), EDataWrapperType.Object, out var data)) 
                return data.objectValue as Texture2D;
            return Definition.Textures.Count > 0 ? Definition.Textures[0].Texture : null;
        }
    }

    [Serializable]
    public class GameplayEffectDefinition
    { 
        public string Name;
        public string Description;
        [ForgeTagContext(ForgeContext.Visibility)] public Tag Visibility;
        public List<TextureItem> Textures;
    }

    public interface IHasReadableDefinition
    {
        public string GetName();
        public string GetDescription();
        public Texture2D GetPrimaryIcon();
    }

    [Serializable]
    public struct GameplayEffectTags
    {
        [ForgeTagContext(ForgeContext.AssetIdentifier)]
        public Tag AssetTag;
        
        [ForgeTagContext(ForgeContext.ContextIdentifier)] public List<Tag> ContextTags;
        [ForgeTagContext(ForgeContext.Granted)]public List<Tag> GrantedTags;
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
        public List<Tag> GetContextTags();
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
        public List<Tag> GetContextTags()
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
            float maxLevel = Owner.GetMaxLevel();
            return maxLevel > 1 ? (Owner.GetLevel() - 1) / (maxLevel - 1) : 1f;
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