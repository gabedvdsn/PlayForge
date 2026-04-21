using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Gameplay Effect", fileName = "Effect_")]
    public class GameplayEffect : BaseForgeLevelProvider
    {
        public GameplayEffectDefinition Definition = new();
        public GameplayEffectTags Tags = new();
        
        public GameplayEffectImpact ImpactSpecification;
        public GameplayEffectDuration DurationSpecification;
        
        [SerializeReference]
        public List<AbstractEffectWorker> Workers;
        
        public EffectTagRequirements SourceRequirements;
        public EffectTagRequirements TargetRequirements;
        
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
        private BaseForgeLevelProvider _linkedSource;
        
        /// <summary>
        /// Gets the raw linked ScriptableObject (for serialization/editor purposes).
        /// </summary>
        public override BaseForgeLevelProvider LinkedProvider
        {
            get => _linkedSource;
            set => _linkedSource = value;
        }
        
        /// <summary>
        /// Returns true if this effect is linked to a level provider.
        /// </summary>
        public override bool IsLinked => LinkMode == EEffectLinkMode.LinkedToProvider && LinkedProvider != null;
        
        /// <summary>
        /// Links this effect to a level provider.
        /// </summary>
        /// <param name="provider">The ScriptableObject that implements ILevelProvider</param>
        /// <returns>True if successfully linked</returns>
        public override bool LinkToProvider(BaseForgeLevelProvider provider)
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
        public override bool IsLinkedTo(ScriptableObject provider)
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

        public void LinkAllContainedEffects()
        {
            if (ImpactSpecification.Packets is not null)
            {
                foreach (var packet in ImpactSpecification.Packets)
                {
                    packet.ContainedEffect.LinkToProvider(LinkedProvider);
                }
            }
        }
        
        public void LinkAllChildren()
        {
            LinkAllContainedEffects();
        }
        
        /// <summary>
        /// Unlinks all effects and abilities from this item.
        /// </summary>
        public void UnlinkAllChildren()
        {
            if (ImpactSpecification.Packets is not null)
            {
                foreach (var packet in ImpactSpecification.Packets)
                {
                    if (!packet.ContainedEffect.IsLinkedTo(LinkedProvider)) continue;
                    packet.ContainedEffect.Unlink();
                }
            }
        }

        public override int GetMaxLevel() => GetLinkedMaxLevel();
        public override int GetStartingLevel() => GetLinkedStartingLevel();
        public override string GetProviderName() => GetName();
        public override Tag GetAssetTag() => Tags.AssetTag;

        // ═══════════════════════════════════════════════════════════════════════════
        // Effect Generation
        // ═══════════════════════════════════════════════════════════════════════════

        public GameplayEffectSpec Generate(IEffectOrigin origin, ITarget target)
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
        
        public void ApplyDurationSpecifications(AbstractEffectContainer container)
        {
            if (DurationSpecification.DurationPolicy == EEffectDurationPolicy.Instant) return;
            
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
        
        public override string GetName()
        {
            return Definition.Name;
        }
        public override IEnumerable<Tag> GetGrantedTags()
        {
            return Tags.GrantedTags;
        }

        public override string GetDescription()
        {
            return Definition.Description;
        }
        
        public override Texture2D GetDefaultIcon()
        {
            return ForgeHelper.GetTextureItem(Definition.Textures, PlayForge.Tags.PRIMARY);
        }
    }

    [Serializable]
    public class GameplayEffectDefinition
    { 
        public string Name;
        public string Description;
        [ForgeTagContext(ForgeContext.Visibility)] public Tag Visibility;
        public List<TextureItem> Textures;
        [ForgeTagContext(ForgeContext.RetentionGroup)] public Tag RetentionGroup = Tags.IgnoreRetention;
    }

    [Serializable]
    public struct GameplayEffectTags
    {
        [ForgeTagContext(ForgeContext.AssetIdentifier)]
        public Tag AssetTag;
        
        [ForgeTagContext(ForgeContext.ContextIdentifier)] public List<Tag> ContextTags;
        [ForgeTagContext(ForgeContext.Granted)] public List<Tag> GrantedTags;
    }
    
    public enum EEffectReApplicationPolicy
    {
        DoNothing,  // Completely ignore new effect
        ReplaceOldContainer,
        AppendNewContainer,  // Create another instance of the effect independent of the existing one(s)
        StackExistingContainers  // Inject a duration-independent stack of the effect into the existing one 
    }

    public enum EEffectInteractionPolicy
    {
        DoNothing,
        RefreshContainerDuration,  // Refresh the duration of the effect
        ExtendContainerDuration,  // Extend the duration of the effect
    }

}