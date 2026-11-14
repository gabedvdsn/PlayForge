using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Ability", fileName = "Ability_", order = 20)]
    public class Ability : BaseForgeLevelProvider, IWorkerGroupSource, ISerializationCallbackReceiver, IMovesToComponents
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
        
        [Min(0)] public int StartingLevel = 1;
        [Min(0)] public int MaxLevel = 4;
        public bool IgnoreWhenLevelZero = true;
        
        [ForgeEffectTemplate("Cost", "Cost")]
        public GameplayEffect Cost;

        [Tooltip("What it spends of attributes as it's used, beside its Cost effect: each the attribute it names, or the one " +
                 "playing the role it names in the project, an amount that can scale by level (TW-29)")]
        public List<AttributeCost> AttributeCosts = new();

        [ForgeEffectTemplate("Cooldown", "Cooldown")]
        public GameplayEffect Cooldown;

        // Its charges, recharge and toggle as a save before components kept them, each moved into its component as it
        // loads (FN-14)
        [SerializeField, HideInInspector, FormerlySerializedAs("Charges")] private ScalableInt legacyCharges = 1;
        [SerializeReference, HideInInspector, FormerlySerializedAs("Recharge")] private AbstractChargeRecharge legacyRecharge = new EachChargeRecharge();
        [SerializeField, HideInInspector, FormerlySerializedAs("Toggles")] private GameplayEffect legacyToggles;

        /// <summary>Its charges: its Charges component's, or one, an ordinary cooldown (TW-17, FN-14).</summary>
        public ScalableInt Charges => FirstComponent<ChargesAbilityComponent>() is { Charges: { } charges } ? charges : OneCharge;

        /// <summary>How its spent charges come back: its Charges component's, or each on a cooldown of its own (TW-17, FN-14).</summary>
        public AbstractChargeRecharge Recharge => FirstComponent<ChargesAbilityComponent>() is { Recharge: { } recharge } ? recharge : EachCharge;

        /// <summary>The effect it toggles on its caster: its Toggle component's, or none, where it doesn't toggle (TW-20, FN-14).</summary>
        public GameplayEffect Toggles => FirstComponent<ToggleAbilityComponent>()?.Effect;

        private static readonly ScalableInt OneCharge = 1;
        private static readonly AbstractChargeRecharge EachCharge = new EachChargeRecharge();

        // Its first component of a kind, or null
        private T FirstComponent<T>() where T : AbstractAbilityComponent
        {
            if (Components == null) return null;
            foreach (var component in Components)
                if (component is T found) return found;
            return null;
        }

        /// <summary>
        /// Whether a stage marked Apply Usage Effects takes its cost and cooldown; where none does, they apply as it starts,
        /// after its targeting (TW-17).
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

        [Tooltip("Effects its holder's hits apply, or a kill's, while it's known (TW-6)")]
        public List<OnHitEntry> OnHit = new();

        [Tooltip("Modifiers of other effects, its holder's own or those landing on it, while it's known (TW-7)")]
        public List<EffectModifierEntry> Modifiers = new();

        // Its auras as a save before components kept them, each moved into an Aura component as it loads (FN-14)
        [SerializeField, HideInInspector] private List<AuraEntry> Auras = new();

        [Tooltip("Its components: features it carries, each a class with hooks into its life, as it's granted, starts, commits its cost and cooldown, ends and is removed; each may refuse an activation (FN-14)")]
        [SerializeReference]
        public List<AbstractAbilityComponent> Components = new();

        /// <summary>Whether it moved an older save's fields into its components as it loaded, which a save keeps (FN-14).</summary>
        public bool MovedToComponents { get; set; }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        // An older save's fields, each moved into a component as it loads, in the editor and in a build (FN-14, Decision 76)
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (Auras is { Count: > 0 })
            {
                Components ??= new List<AbstractAbilityComponent>();
                foreach (var aura in Auras)
                    if (aura != null) Components.Add(new AuraAbilityComponent { Aura = aura });
                Auras.Clear();
                MovedToComponents = true;
            }

            // Charges more than one, or that scale, or a recharge of another kind: each kept plain data, so a load, which
            // mustn't ask Unity anything, can tell
            bool charged = legacyCharges != null && (legacyCharges.Magnitude > 1 || !AbstractScaler.IsNone(legacyCharges.Scaler));
            bool recharged = legacyRecharge != null && legacyRecharge.GetType() != typeof(EachChargeRecharge);
            if (charged || recharged)
            {
                Components ??= new List<AbstractAbilityComponent>();
                Components.Add(new ChargesAbilityComponent { Charges = legacyCharges ?? 1, Recharge = legacyRecharge ?? new EachChargeRecharge() });
                legacyCharges = 1;
                legacyRecharge = new EachChargeRecharge();
                MovedToComponents = true;
            }

            // A toggle, by its reference alone, which a load can read
            if (!ReferenceEquals(legacyToggles, null))
            {
                Components ??= new List<AbstractAbilityComponent>();
                Components.Add(new ToggleAbilityComponent { Effect = legacyToggles });
                legacyToggles = null;
                MovedToComponents = true;
            }
        }

        [Tooltip("Its holder heals a share of each loss it deals, while it's known (TW-15)")]
        public List<LifestealEntry> Lifesteal = new();

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
            // The cooldown's own link. This asked the cost's, and threw on an ability with no cost (GAS-30)
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
            ForgeAuras.Provide(ForgeAuras.Of(Components), system);
            Lifesteals.Provide(Lifesteal, system);
        }
        public void RemoveWorkers(ISource system)
        {
            WorkerGroup?.RemoveWorkersFrom(system);
            OnHits.Remove(OnHit, system);
            EffectModifiers.Remove(Modifiers, system);
            ForgeAuras.Remove(ForgeAuras.Of(Components), system);
            Lifesteals.Remove(Lifesteal, system);
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

    public enum EDataWrapperType
    {
        MonoBehaviour,
        ScriptableObject,
        Object,
        String,
        Int,
        Float,
        Bool,
        Tag,
        Attribute,
        Effect,
        Ability,
        Entity
    }
    
    [Serializable]
    public class DataWrapper
    {
        [ForgeTagContext(ForgeContext.LocalDataKey)]
        public Tag Key;
        public EDataWrapperType Type;

        public string stringValue;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public Object objectValue;
        public MonoBehaviour monoBehaviourValue;
        public ScriptableObject scriptableObjectValue;
        public Tag tagValue;
        public Attribute attributeValue;
        public GameplayEffect effectValue;
        public Ability abilityValue;
        public EntityIdentity entityValue;
    }
}