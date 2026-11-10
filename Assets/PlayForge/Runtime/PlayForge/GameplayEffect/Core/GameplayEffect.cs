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

        /// <summary>
        /// The abilities the target has while this effect is on it, each at the level of what applied the effect, and
        /// interrupted, if it's running, as the effect comes off. An instant effect lends none, as it's never on its
        /// target. An effect couldn't lend an ability, so a buff that lets you cast Fireball needed an item (FEAT-8).
        /// </summary>
        public List<Ability> GrantedAbilities = new();
        
        public GameplayEffectImpact ImpactSpecification;
        public GameplayEffectDuration DurationSpecification;
        
        [SerializeReference]
        public List<AbstractEffectWorker> Workers;

        /// <summary>
        /// What the effect shows and sounds as it lands, ticks and goes, and while it lasts, apart from what it changes, as
        /// its workers are listed: a camera shake, a sound, a prefab. An effect showed nothing of its own (FEAT-1).
        /// </summary>
        [SerializeReference]
        public List<AbstractCueHandler> Cues = new();
        
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
            // Entries are DERIVED from the authored impact + duration flags on every
            // generation (never serialized) — inspector edits always take effect on the
            // next application, and delivery always reflects the CURRENT duration policy.
            ImpactSpecification.RebuildEntries(DurationSpecification);

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
                   || SourceRequirements.CheckRemovalRequirements(spec.Source.GetAppliedTags());
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

        /// <summary>
        /// A cleanse: effects on the target tagged with one of these, or a child of one, by their asset tag, a context
        /// tag or a tag they grant, come off as this effect lands (FEAT-8).
        /// </summary>
        [ForgeTagContext(ForgeContext.ContextIdentifier)] public List<Tag> RemovesEffectsTagged;

        /// <summary>
        /// An immunity: while this effect is on the target, an effect tagged with one of these, or a child of one, can't
        /// land on it (FEAT-8).
        /// </summary>
        [ForgeTagContext(ForgeContext.ContextIdentifier)] public List<Tag> BlocksEffectsTagged;

        /// <summary>
        /// Whether an effect is tagged with one of the tags, or a child of one, by its asset tag, a context tag or a tag
        /// it grants, as Unreal's queries match (Decision 30). A tag it grants counted for nothing, so "whatever makes
        /// you Burning" needed a context tag on every effect that burns (FEAT-8).
        /// </summary>
        public static bool Names(List<Tag> tags, GameplayEffect effect)
        {
            if (tags == null || tags.Count == 0 || effect == null) return false;
            foreach (var tag in tags)
            {
                if (!tag.IsValid) continue;
                if (effect.Tags.AssetTag.MatchesOrIsChildOf(tag)) return true;
                if (AnyMatches(effect.Tags.ContextTags, tag) || AnyMatches(effect.Tags.GrantedTags, tag)) return true;
            }
            return false;
        }

        /// <summary>
        /// Whether an effect is tagged with the tag, or a child of it, by its asset tag, a context tag or a tag it grants,
        /// as <see cref="Names(List{Tag}, GameplayEffect)"/> matches each; allocates nothing (FEAT-29).
        /// </summary>
        public static bool Names(Tag tag, GameplayEffect effect)
        {
            if (!tag.IsValid || effect == null) return false;
            return effect.Tags.AssetTag.MatchesOrIsChildOf(tag) || AnyMatches(effect.Tags.ContextTags, tag) || AnyMatches(effect.Tags.GrantedTags, tag);
        }

        // By index over the list, which allocates nothing
        private static bool AnyMatches(List<Tag> own, Tag tag)
        {
            if (own == null) return false;
            for (int i = 0; i < own.Count; i++)
                if (own[i].MatchesOrIsChildOf(tag)) return true;
            return false;
        }
    }
    
    // The names the inspector shows say the choice, as the Effect mockup's Stack Existing and Refresh Duration do (ED-37)
    public enum EEffectReApplicationPolicy
    {
        [InspectorName("Do Nothing")] DoNothing,  // Completely ignore new effect
        [InspectorName("Replace Existing")] ReplaceOldContainer,
        [InspectorName("Add Another")] AppendNewContainer,  // Create another instance of the effect independent of the existing one(s)
        [InspectorName("Stack Existing")] StackExistingContainers  // Inject a duration-independent stack of the effect into the existing one
    }

    public enum EEffectInteractionPolicy
    {
        [InspectorName("Do Nothing")] DoNothing,
        [InspectorName("Refresh Duration")] RefreshContainerDuration,  // Refresh the duration of the effect
        [InspectorName("Extend Duration")] ExtendContainerDuration,  // Extend the duration of the effect
    }

}