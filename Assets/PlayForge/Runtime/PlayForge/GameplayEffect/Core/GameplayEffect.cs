using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A Gameplay Effect asset: a change to an entity, as damage, a heal, a timed buff, a poison, a cost or a cooldown.
    /// Abilities, items and code apply it to a target: <c>Generate</c>, through the target's <c>GenerateEffectSpec</c>,
    /// makes a <c>GameplayEffectSpec</c> for one application, which <c>ApplyGameplayEffect</c> lands.
    ///
    /// <para>Its duration's Policy decides how long it lives: an Instant effect makes its change once, and a Durational
    /// or Infinite one goes on the target's effect shelf until its time runs out or something removes it. Its impacts
    /// change attributes, each a commit that stays or a modifier reversed as the effect ends; it can also grant tags
    /// and abilities, apply contained effects, run workers and carry components, and its requirements and conditions
    /// say whether it lands, goes on and comes off. It has no level of its own: it takes its origin's.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Gameplay Effect", fileName = "Effect_", order = 21)]
    public class GameplayEffect : BaseForgeLevelProvider, IHasTextures
    {
        public GameplayEffectDefinition Definition = new();
        public GameplayEffectTags Tags = new();

        [Tooltip("Its purge, as it lands on its target: the effects there at its strength's tier or under it, by the " +
                 "project's Purge Levels, of the kinds it names and applied by whom it says, come off")]
        public EffectPurge Purges = new();

        /// <summary>
        /// The abilities the target has while this effect is on it, each at the level of what applied the effect, and
        /// interrupted, if it's running, as the effect comes off. An instant effect lends none, as it's never on its
        /// target. An effect couldn't lend an ability, so a buff that lets you cast Fireball needed an item.
        /// </summary>
        public List<Ability> GrantedAbilities = new();

        /// <summary>Its side change: its Side Change component's, or none.</summary>
        public SideChangeEffectComponent SideChange => FirstComponent<SideChangeEffectComponent>();

        [Tooltip("Effects its holder's hits apply, or a kill's, while it lasts, the holder the one it's on")]
        public List<OnHitEntry> OnHit = new();

        [Tooltip("Modifiers of other effects, its holder's own or those landing on it, while it lasts, the holder the one it's on")]
        public List<EffectModifierEntry> Modifiers = new();

        /// <summary>Its area: its Area component's, or none, where it lands on its target alone.</summary>
        public EffectArea Area => FirstComponent<AreaEffectComponent>()?.Area;

        /// <summary>
        /// Its transfers: each of its Transfer components', in their order, a list read anew each time. A
        /// <see cref="TransferEffectComponent"/> adds one.
        /// </summary>
        public IReadOnlyList<AttributeTransfer> Transfers
        {
            get
            {
                var transfers = new List<AttributeTransfer>();
                if (Components == null) return transfers;
                foreach (var component in Components)
                    if (component is TransferEffectComponent { Transfer: { } transfer }) transfers.Add(transfer);
                return transfers;
            }
        }

        // Its first component of a kind, or null
        private T FirstComponent<T>() where T : AbstractEffectComponent
        {
            if (Components == null) return null;
            foreach (var component in Components)
                if (component is T found) return found;
            return null;
        }

        [Tooltip("Its own proc coefficient in place of its ability's: what its hits carry for their on-hit effects")]
        public bool OwnProcCoefficient;

        [Tooltip("What its hits carry for their on-hit effects, whose chance and duration scale by it; 0 sets nothing off")]
        [NumberMin(0f)] public ScalableFloat ProcCoefficient = 1f;

        public GameplayEffectImpact ImpactSpecification;
        public GameplayEffectDuration DurationSpecification;
        
        [SerializeReference]
        public List<AbstractEffectWorker> Workers;

        [Tooltip("Its components: features it carries, each a class with hooks into its life, as it lands, ticks, hits and " +
                 "ends")]
        [SerializeReference]
        public List<AbstractEffectComponent> Components = new();

        /// <summary>
        /// Its first component that is a <typeparamref name="T"/>, its kind or a capability it implements; null where none
        /// is.
        /// </summary>
        public T Find<T>() where T : class => AbstractForgeComponent.Find<T>(Components);

        [Tooltip("Workers it gives the one it's on while it lasts, and takes back as it goes: attribute, impact, tag and " +
                 "analysis workers, as a debuff that changes how its target takes damage, or shatters it below 13%")]
        public StandardWorkerGroup WorkerGroup;

        /// <summary>
        /// Its source's tag requirements: the tags the source must hold or lack for the effect to land and to stay on,
        /// and the tags that take the effect off.
        /// </summary>
        public EffectTagRequirements SourceRequirements;
        /// <summary>Its target's tag requirements, as its source's are.</summary>
        public EffectTagRequirements TargetRequirements;

        [Tooltip("Its conditions beside the tags of each phase: to land, to go on, and to come off")]
        public EffectConditions Conditions = new();

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

            // What its source's modifiers and its target's change of it, for this application alone
            EffectModifiers.Apply(spec);

            return spec;
        }
        
        #region Effect Base

        /// <summary>
        /// Whether it may land: its tags to apply pass, each of its Apply conditions holds, and its Removal phase isn't met
        /// already, its tags and conditions together.
        /// </summary>
        public bool ValidateApplicationRequirements(GameplayEffectSpec spec)
        {
            bool result = TargetRequirements.CheckApplicationRequirements(spec.Target.GetAppliedTags())
                          && SourceRequirements.CheckApplicationRequirements(spec.Source.GetAppliedTags());
            if (!result) return false;
            if (EffectConditions.Has(Conditions?.Application) && !EffectConditions.AllHold(Conditions.Application, NumberContext.Of(spec))) return false;
            return !ValidateRemovalRequirements(spec);
        }

        /// <summary>
        /// Whether it comes off: its Removal tags, where it names any, and its Removal conditions, where it has any, all say
        /// so; with neither, never. Its tags alone said so before its conditions were.
        /// </summary>
        public bool ValidateRemovalRequirements(GameplayEffectSpec spec)
        {
            bool hasTags = TargetRequirements.HasRemovalRequirements || SourceRequirements.HasRemovalRequirements;
            bool hasConditions = EffectConditions.Has(Conditions?.Removal);
            if (!hasTags && !hasConditions) return false;
            if (hasTags && !(TargetRequirements.CheckRemovalRequirements(spec.Target.GetAppliedTags())
                             || SourceRequirements.CheckRemovalRequirements(spec.Source.GetAppliedTags()))) return false;
            return !hasConditions || EffectConditions.AllHold(Conditions.Removal, NumberContext.Of(spec));
        }

        /// <summary>Whether it goes on: its Ongoing tags pass, and each of its Ongoing conditions holds.</summary>
        public bool ValidateOngoingRequirements(GameplayEffectSpec spec)
        {
            return TargetRequirements.CheckOngoingRequirements(spec.Target.GetAppliedTags())
                   && SourceRequirements.CheckOngoingRequirements(spec.Source.GetAppliedTags())
                   && (!EffectConditions.Has(Conditions?.Ongoing) || EffectConditions.AllHold(Conditions.Ongoing, NumberContext.Of(spec)));
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

        public IReadOnlyList<TextureItem> GetTextureItems() => Definition?.Textures;
    }

    /// <summary>
    /// An effect's identity: its Name, which gives its asset tag, its Description, its Visibility, a tag a UI can read
    /// to decide whether to show it, its Textures, whether its icon is its origin's (Inherit From Origin, by Origin
    /// Texture), and the Retention Group its changes count toward unless an impact names its own.
    /// </summary>
    [Serializable]
    public class GameplayEffectDefinition
    {
        public string Name;
        public string Description;
        [ForgeTagContext(ForgeContext.Visibility)] public Tag Visibility;
        public List<TextureItem> Textures;

        [Tooltip("Its icon is its origin's, the ability, item or entity that applied it: the origin's texture under Origin " +
                 "Texture, its own where the origin has none")]
        public bool InheritFromOrigin;

        [Tooltip("The origin's texture it takes, by its tag: Primary, or another")]
        [ForgeTagContext(ForgeContext.TextureSlot)]
        public Tag OriginTexture = Tags.PRIMARY;
        [ForgeTagContext(ForgeContext.RetentionGroup)] public Tag RetentionGroup = Tags.IgnoreRetention;
    }

    /// <summary>
    /// An effect's tags: its Asset Tag, by which the effect system finds it as it's applied again or removed, and which
    /// its target carries while it lasts; its Context Tags, which describe it and go with each change it makes, never
    /// applied to the entity; its Granted Tags, applied to its target while it's on; and the tags it cleanses (Removes
    /// Effects Tagged) and makes its target immune to (Blocks Effects Tagged). <c>Names</c> says whether an effect is
    /// tagged with a tag, or a child of one, by any of the three.
    /// </summary>
    [Serializable]
    public struct GameplayEffectTags
    {
        [ForgeTagContext(ForgeContext.AssetTag)]
        public Tag AssetTag;
        
        [ForgeTagContext(ForgeContext.ContextTag)] public List<Tag> ContextTags;
        [ForgeTagContext(ForgeContext.Granted)] public List<Tag> GrantedTags;

        /// <summary>
        /// A cleanse: effects on the target tagged with one of these, or a child of one, by their asset tag, a context
        /// tag or a tag they grant, come off as this effect lands.
        /// </summary>
        [ForgeTagContext(ForgeContext.AssetTag, ForgeContext.ContextTag, ForgeContext.Granted)] public List<Tag> RemovesEffectsTagged;

        /// <summary>
        /// An immunity: while this effect is on the target, an effect tagged with one of these, or a child of one, can't
        /// land on it.
        /// </summary>
        [ForgeTagContext(ForgeContext.AssetTag, ForgeContext.ContextTag, ForgeContext.Granted)] public List<Tag> BlocksEffectsTagged;

        /// <summary>
        /// Whether an effect is tagged with one of the tags, or a child of one, by its asset tag, a context tag or a tag
        /// it grants, as Unreal's queries match. A tag it grants counted for nothing, so "whatever makes
        /// you Burning" needed a context tag on every effect that burns.
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
        /// as <see cref="Names(List{Tag}, GameplayEffect)"/> matches each; allocates nothing.
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
    
    // The names the inspector shows say the choice, as Stack Existing and Refresh Duration do
    /// <summary>
    /// What a Durational or Infinite effect does as it's applied to a target that has it already, matched by its asset
    /// tag whoever applied it: Add Another, the default, adds a container of its own; Do Nothing takes nothing new in;
    /// Replace Existing gives the old container the new application's values and modifiers, keeping its stacks and past
    /// commits; Stack Existing adds stacks to it. All but Add Another then do with its time as the effect's
    /// <c>EEffectInteractionPolicy</c> says.
    /// </summary>
    public enum EEffectReApplicationPolicy
    {
        [InspectorName("Do Nothing")] DoNothing,  // Completely ignore new effect
        [InspectorName("Replace Existing")] ReplaceOldContainer,
        [InspectorName("Add Another")] AppendNewContainer,  // Create another instance of the effect independent of the existing one(s)
        [InspectorName("Stack Existing")] StackExistingContainers  // Inject a duration-independent stack of the effect into the existing one
    }

    /// <summary>
    /// What a lasting effect on the target does with its time as it takes in an application under Do Nothing, Replace
    /// Existing or Stack Existing: Do Nothing, the default, leaves it; Refresh Duration restarts it; Extend Duration
    /// adds the new application's duration to it, or, where stacks keep their own durations, adds a stack. Refresh and
    /// Extend run Tick On Application again.
    /// </summary>
    public enum EEffectInteractionPolicy
    {
        [InspectorName("Do Nothing")] DoNothing,
        [InspectorName("Refresh Duration")] RefreshContainerDuration,  // Refresh the duration of the effect
        [InspectorName("Extend Duration")] ExtendContainerDuration,  // Extend the duration of the effect
    }

}