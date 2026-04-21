using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// ScriptableObject defining an item that can be added to an entity's inventory.
    /// Items can grant effects, abilities, and tags when equipped.
    /// Supports level scaling for linked effects and abilities.
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Item", fileName = "Item_")]
    public class Item : BaseForgeLevelProvider
    {
        public ItemDefinition Definition = new();
        public ItemTags Tags = new();

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Configuration
        // ═══════════════════════════════════════════════════════════════════════════
        
        [Min(1)] 
        [Tooltip("The level this item starts at when first acquired.")]
        public int StartingLevel = 1;
        
        [Min(1)] 
        [Tooltip("The maximum level this item can reach.")]
        public int MaxLevel = 4;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Level Provider Linking
        // ═══════════════════════════════════════════════════════════════════════════
        
        public EItemLinkMode LinkMode;
        
        /// <summary>
        /// Optional link to a level provider (Entity, Ability).
        /// When set, this item can derive its max level from the provider
        /// for level scaling.
        /// </summary>
        [Tooltip("Link this item to an Entity or Ability to derive max level from it")]
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
        /// Returns true if this item is linked to a level provider.
        /// </summary>
        public override bool IsLinked => LinkMode == EItemLinkMode.LinkedToProvider && LinkedProvider != null;
        
        /// <summary>
        /// Links this item to a level provider.
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
            LinkMode = EItemLinkMode.LinkedToProvider;
            return true;
        }
        
        /// <summary>
        /// Removes any existing link.
        /// </summary>
        public void Unlink()
        {
            LinkedProvider = null;
            LinkMode = EItemLinkMode.Standalone;
        }
        
        /// <summary>
        /// Checks if this item is linked to a specific provider.
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
            return useOwnIfNotLinked ? StartingLevel : 1;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Granted Effects & Abilities
        // ═══════════════════════════════════════════════════════════════════════════
        
        [Tooltip("Effects that are applied while this item is equipped.\n" +
                 "These effects can be linked to this item for level scaling.")]
        public List<GameplayEffect> GrantedEffects = new();

        [Tooltip("The active ability provided by this item when equipped.\n" +
                 "Can be linked to this item for level scaling.")]
        public Ability ActiveAbility;

        [Tooltip("Worker group providing additional behaviors while equipped.")]
        public StandardWorkerGroup WorkerGroup;

        // ═══════════════════════════════════════════════════════════════════════════
        // Generation
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Generates a runtime ItemSpec from this item definition.
        /// </summary>
        public ItemSpec Generate(ISource owner, int level)
        {
            return new ItemSpec(owner, this, level);
        }

        /// <summary>
        /// Generates a runtime ItemSpec at the starting level.
        /// </summary>
        public ItemSpec Generate(ISource owner)
        {
            return Generate(owner, GetStartingLevel());
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // IHasReadableDefinition Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override string GetName()
        {
            return string.IsNullOrEmpty(Definition.Name) ? name : Definition.Name;
        }
        
        public override string GetDescription()
        {
            return Definition.Description ?? string.Empty;
        }
        
        public override Texture2D GetDefaultIcon()
        {
            return ForgeHelper.GetTextureItem(Definition.Textures, PlayForge.Tags.PRIMARY);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ILevelProvider Implementation (BaseForgeLinkProvider)
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override int GetMaxLevel() => IsLinked ? GetLinkedMaxLevel() : MaxLevel;
        
        public override int GetStartingLevel() => IsLinked ? GetLinkedStartingLevel() : StartingLevel;
        
        public override string GetProviderName() => GetName();
        
        public override Tag GetAssetTag() => Tags.AssetTag;

        // ═══════════════════════════════════════════════════════════════════════════
        // Granted Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override IEnumerable<Tag> GetGrantedTags()
        {
            yield return Tags.AssetTag;
            
            if (Tags.PassiveGrantedTags != null)
            {
                foreach (var t in Tags.PassiveGrantedTags)
                {
                    yield return t;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Linking Utility Methods (for child assets)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Links all granted effects to this item as their level provider.
        /// Call this in OnValidate or via editor button.
        /// </summary>
        public void LinkAllGrantedEffects()
        {
            if (GrantedEffects == null) return;
            
            foreach (var effect in GrantedEffects)
            {
                if (effect != null)
                {
                    effect.LinkToProvider(this);
                }
            }
        }
        
        /// <summary>
        /// Links the active ability to this item as its level provider.
        /// </summary>
        public void LinkActiveAbility()
        {
            if (ActiveAbility != null)
            {
                ActiveAbility.LinkToProvider(this);
            }
        }
        
        /// <summary>
        /// Links all effects and abilities to this item as their level provider.
        /// </summary>
        public void LinkAllChildren()
        {
            LinkAllGrantedEffects();
            LinkActiveAbility();
        }
        
        /// <summary>
        /// Unlinks all effects and abilities from this item.
        /// </summary>
        public void UnlinkAllChildren()
        {
            if (GrantedEffects != null)
            {
                foreach (var effect in GrantedEffects)
                {
                    if (effect != null && effect.IsLinkedTo(this))
                    {
                        effect.Unlink();
                    }
                }
            }
            
            if (ActiveAbility != null && ActiveAbility.IsLinkedTo(this))
            {
                ActiveAbility.Unlink();
            }
        }
        
        /// <summary>
        /// Returns all effects that are linked to this item.
        /// </summary>
        public IEnumerable<GameplayEffect> GetLinkedEffects()
        {
            if (GrantedEffects == null) yield break;
            
            foreach (var effect in GrantedEffects)
            {
                if (effect != null && effect.IsLinkedTo(this))
                {
                    yield return effect;
                }
            }
        }
        
        /// <summary>
        /// Returns the active ability if it is linked to this item.
        /// </summary>
        public Ability GetLinkedAbility()
        {
            if (ActiveAbility != null && ActiveAbility.IsLinkedTo(this))
            {
                return ActiveAbility;
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Validation
        // ═══════════════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Ensure StartingLevel is within bounds
            if (MaxLevel < 1) MaxLevel = 1;
            StartingLevel = Mathf.Clamp(StartingLevel, 1, MaxLevel);
        }
#endif

        public override string ToString()
        {
            return $"Item[{GetName()}]";
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // Enums
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Defines how an item is linked to its level provider.
    /// </summary>
    public enum EItemLinkMode
    {
        /// <summary>
        /// Item operates independently with its own level tracking.
        /// </summary>
        Standalone,
        
        /// <summary>
        /// Item is linked to another provider (Entity, Ability) and derives level from it.
        /// </summary>
        LinkedToProvider
    }
}