using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Ability", fileName = "Ability_", order = 20)]
    public class Ability : BaseForgeLevelProvider, IWorkerGroupSource, IHasTextures
    {
        public AbilityDefinition Definition = new();
        public AbilityTags Tags = new();
        public AbilityBehaviour Behaviour = new();

        [SerializeReference] 
        public List<IAbilityValidationRule> SourceActivationRules = new()
        {
            new CooldownValidation(),
            new CostValidation(),
            new IsAliveValidation()
        };

        [SerializeReference]
        public List<IAbilityValidationRule> TargetActivationRules = new()
        {
            new IsAliveValidation()
        };

        /// <summary>
        /// Its Caster Checks: the rules it holds, then the checks its components stand behind, which the components
        /// enforce.
        /// </summary>
        public IEnumerable<IAbilityValidationRule> CasterChecks => ChecksOf(SourceActivationRules, false);

        /// <summary>
        /// Its Target Checks: the rules it holds, then the checks its components stand behind, as its Cast Range's Check
        /// Range, which the components enforce.
        /// </summary>
        public IEnumerable<IAbilityValidationRule> TargetChecks => ChecksOf(TargetActivationRules, true);

        // The rules a list holds, then the checks the components stand behind on its side
        private IEnumerable<IAbilityValidationRule> ChecksOf(List<IAbilityValidationRule> held, bool target)
        {
            if (held != null)
                foreach (var rule in held)
                    if (rule != null) yield return rule;
            if (Components == null) yield break;
            foreach (var component in Components)
                if ((target ? component?.TargetCheck : component?.CasterCheck) is { } check) yield return check;
        }

        [Min(0)] public int StartingLevel = 1;
        [Min(0)] public int MaxLevel = 4;
        public bool IgnoreWhenLevelZero = true;
        
        [ForgeEffectTemplate("Cost", "Cost")]
        public GameplayEffect Cost;

        [Tooltip("What it spends of attributes as it's used, beside its Cost effect: each the attribute it names, or the one " +
                 "playing the role it names in the project, an amount that can scale by level")]
        public List<AttributeCost> AttributeCosts = new();

        [ForgeEffectTemplate("Cooldown", "Cooldown")]
        public GameplayEffect Cooldown;

        /// <summary>
        /// Whether a stage marked Apply Usage Effects takes its cost and cooldown; where none does, they apply as it starts,
        /// after its targeting.
        /// </summary>
        public bool StageTakesUsage
        {
            get
            {
                var stages = Behaviour?.Stages;
                if (stages == null) return false;
                for (int i = 0; i < stages.Count; i++)
                    if (stages[i] != null && stages[i].ApplyUsageEffects) return true;
                return false;
            }
        }

        public StandardWorkerGroup WorkerGroup;

        [Tooltip("Effects its holder's hits apply, or a kill's, while it's known")]
        public List<OnHitEntry> OnHit = new();

        [Tooltip("Modifiers of other effects, its holder's own or those landing on it, while it's known")]
        public List<EffectModifierEntry> Modifiers = new();

        [Tooltip("Its components: features it carries, each a class with hooks into its life, as it's granted, starts, commits its cost and cooldown, ends and is removed; each may refuse an activation")]
        [SerializeReference]
        public List<AbstractAbilityComponent> Components = new();

        /// <summary>
        /// Its first component that is a <typeparamref name="T"/>, its kind or a capability it implements, as its reach,
        /// <see cref="IAbilityReach"/>; null where none is.
        /// </summary>
        public T Find<T>() where T : class => AbstractForgeComponent.Find<T>(Components);

        // ═══════════════════════════════════════════════════════════════════════════
        // Level Provider Linking
        // ═══════════════════════════════════════════════════════════════════════════

        public EAbilityLinkMode LinkMode;
        
        /// <summary>
        /// Optional link to a level provider (Item).
        /// When set, this ability can derive its max level from the provider
        /// for level scaling.
        /// </summary>
        [Tooltip("Link this ability to an Item to derive max level from it")]
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
        /// Returns true if this ability is linked to a level provider.
        /// </summary>
        public override bool IsLinked => LinkMode == EAbilityLinkMode.LinkedToProvider && LinkedProvider != null;
        
        /// <summary>
        /// Links this ability to a level provider.
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
            LinkMode = EAbilityLinkMode.LinkedToProvider;
            return true;
        }
        
        /// <summary>
        /// Removes any existing link.
        /// </summary>
        public void Unlink()
        {
            LinkedProvider = null;
            LinkMode = EAbilityLinkMode.Standalone;
        }
        
        /// <summary>
        /// Checks if this ability is linked to a specific provider.
        /// </summary>
        public override bool IsLinkedTo(ScriptableObject provider)
        {
            return IsLinked && LinkedProvider == provider;
        }
        
        /// <summary>
        /// Gets the max level from the linked provider, or uses own MaxLevel if not linked.
        /// </summary>
        /// <param name="useOwnIfNotLinked">If true, returns own MaxLevel when not linked</param>
        public int GetLinkedMaxLevel(bool useOwnIfNotLinked = true)
        {
            if (IsLinked)
            {
                return LinkedProvider.GetMaxLevel();
            }
            return useOwnIfNotLinked ? MaxLevel : 1;
        }
        
        /// <summary>
        /// Gets the starting level from the linked provider, or uses own StartingLevel if not linked.
        /// </summary>
        /// <param name="useOwnIfNotLinked">If true, returns own StartingLevel when not linked</param>
        public int GetLinkedStartingLevel(bool useOwnIfNotLinked = true)
        {
            if (IsLinked)
            {
                return LinkedProvider.GetStartingLevel();
            }
            return useOwnIfNotLinked ? StartingLevel : 0;
        }

        public void LinkAllUsageEffects()
        {
            if (Cost) Cost.LinkToProvider(this);
            if (Cooldown) Cooldown.LinkToProvider(this);
        }
        
        public void LinkAllChildren()
        {
            LinkAllUsageEffects();
        }
        
        /// <summary>
        /// Unlinks all effects and abilities from this item.
        /// </summary>
        public void UnlinkAllChildren()
        {
            if (Cost && Cost.IsLinkedTo(this)) Cost.Unlink();
            // The cooldown's own link. This asked the cost's, and threw on an ability with no cost
            if (Cooldown && Cooldown.IsLinkedTo(this)) Cooldown.Unlink();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Ability Generation
        // ═══════════════════════════════════════════════════════════════════════════

        public AbilitySpec Generate(ISource owner, int level)
        {
            return new AbilitySpec(owner, this, level);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // IHasReadableDefinition Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override string GetName()
        {
            return Definition.Name;
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

        // ═══════════════════════════════════════════════════════════════════════════
        // ILevelProvider Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override int GetMaxLevel() => MaxLevel;
        
        public override int GetStartingLevel() => StartingLevel;
        
        public override string GetProviderName() => GetName();
        
        public override Tag GetAssetTag() => Tags.AssetTag;

        // ═══════════════════════════════════════════════════════════════════════════
        // Utility Methods
        // ═══════════════════════════════════════════════════════════════════════════

        public override IEnumerable<Tag> GetGrantedTags()
        {
            yield return Tags.AssetTag;
            foreach (var t in Tags.PassiveGrantedTags) yield return t;
        }

        public override string ToString()
        {
            return Tags.AssetTag.GetName();
        }
        public void InitWorkers(ISource system)
        {
            WorkerGroup?.ProvideWorkersTo(system);
            OnHits.Provide(OnHit, system);
            EffectModifiers.Provide(Modifiers, system);
        }
        public void RemoveWorkers(ISource system)
        {
            WorkerGroup?.RemoveWorkersFrom(system);
            OnHits.Remove(OnHit, system);
            EffectModifiers.Remove(Modifiers, system);
        }
    }

    /// <summary>
    /// Defines how an ability is linked to its level provider.
    /// </summary>
    public enum EAbilityLinkMode
    {
        /// <summary>
        /// Ability operates independently with its own level tracking.
        /// </summary>
        Standalone,
        
        /// <summary>
        /// Ability is linked to an Item and derives level from it.
        /// </summary>
        LinkedToProvider
    }
}