using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Ability", fileName = "Ability_")]
    public class Ability : BaseForgeLinkProvider
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
        [ForgeEffectTemplate("Cooldown", "Cooldown")] 
        public GameplayEffect Cooldown;

        public StandardWorkerGroup WorkerGroup;

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
        private BaseForgeLinkProvider _linkedSource;
        
        /// <summary>
        /// Gets the raw linked ScriptableObject (for serialization/editor purposes).
        /// </summary>
        public override BaseForgeLinkProvider LinkedProvider
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
        public override bool LinkToProvider(BaseForgeLinkProvider provider)
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
            if (Cooldown && Cost.IsLinkedTo(this)) Cooldown.Unlink();
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
        
        public override Texture2D GetPrimaryIcon()
        {
            foreach (var ti in Definition.Textures)
            {
                if (ti.Tag == PlayForge.Tags.PRIMARY) return ti.Texture;
            }
            return Definition.Textures.Count > 0 ? Definition.Textures[0].Texture : null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ILevelProvider Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override int GetMaxLevel() => MaxLevel;
        
        public override int GetStartingLevel() => StartingLevel;
        
        public override string GetProviderName() => GetName();
        
        public override Tag GetProviderTag() => Tags.AssetTag;

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