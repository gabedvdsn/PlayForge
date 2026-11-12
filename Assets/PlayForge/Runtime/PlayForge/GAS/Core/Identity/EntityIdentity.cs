using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Entity", fileName = "Entity_")]
    public class EntityIdentity : BaseForgeLevelProvider, IWorkerGroupSource
    {
        public string Name;
        public string Description;
        public List<TextureItem> Textures;
        
        [ForgeTagContext(ForgeContext.Affiliation)]
        public List<Tag> Affiliation;
        
        [ForgeTagContext(ForgeContext.AssetIdentifier)]
        public Tag AssetTag;
        [ForgeTagContext(ForgeContext.Granted)] public List<Tag> GrantedTags;
        [ForgeTagContext(ForgeContext.ContextIdentifier)] public List<Tag> ContextTags;

        // The level always stays between these two; Cap At Max, which nothing read, is gone (GAS-45)
        public int StartingLevel = 1;
        public int MaxLevel = 99;
        
        public EAbilityActivationPolicy ActivationPolicy = EAbilityActivationPolicy.QueueActivationIfBusy;
        
        // These had no default, so an identity that didn't set them could hold nothing: a new asset's were 0 (GAS-23)
        [ScalerOperationKeyword("Max Abilities")]
        public ScalableInt MaxAbilitiesOperation = new() { Magnitude = 99 };
        public List<Ability> StartingAbilities = new();
        public bool AllowDuplicateAbilities;

        [ScalerOperationKeyword("Max Items")]
        public ScalableInt MaxItemsOperation = new() { Magnitude = 99 };
        [ScalerOperationKeyword("Max Equipped Items")]
        public ScalableInt MaxEquippedItemsOperation = new() { Magnitude = 99 };
        public List<StartingItemContainer> StartingItems = new();
        public bool AllowDuplicateItems;
        public bool AllowDuplicateEquippedItems;
        
        public AttributeSet AttributeSet;

        /// <summary>
        /// The attribute its time runs at, 1 as the game's: an effect that lowers it slows the entity, its effects,
        /// cooldowns, sequences and waits with it, and one that raises it hastes it (FEAT-22). None, its time scale is
        /// its game's code's to set, through its clock.
        /// </summary>
        [Tooltip("The attribute its time runs at, 1 as the game's: an effect that lowers it slows the entity, and one that raises it hastes it. None, the game's code sets its time scale")]
        public Attribute TimeScaleAttribute;

        public StandardWorkerGroup WorkerGroup;

        [Tooltip("Modifiers of other effects, its holder's own or those landing on it, while it lives (TW-7)")]
        public List<EffectModifierEntry> Modifiers = new();

        [Tooltip("Auras it holds on those near it, while it lives (TW-10)")]
        public List<AuraEntry> Auras = new();

        [Tooltip("It heals a share of each loss it deals, while it lives (TW-15)")]
        public List<LifestealEntry> Lifesteal = new();

        [Tooltip("What its death does: End, as an entity always did, or Stay, dead, until it revives, with the effects its " +
                 "death ends and takes, and its body (TW-4)")]
        public EntityDeath Death = new();
        
        public override string GetName()
        {
            return Name;
        }
        public override string GetDescription()
        {
            return Description;
        }
        public override Texture2D GetDefaultIcon()
        {
            return ForgeHelper.GetTextureItem(Textures, PlayForge.Tags.PRIMARY);
        }
        public override int GetMaxLevel()
        {
            return MaxLevel;
        }
        public override int GetStartingLevel()
        {
            return StartingLevel;
        }
        public override string GetProviderName()
        {
            return GetName();
        }
        public override Tag GetAssetTag()
        {
            return AssetTag;
        }

        public override IEnumerable<Tag> GetGrantedTags()
        {
            yield return AssetTag;
            foreach (var _tag in Affiliation) yield return _tag;
            foreach (var tag in GrantedTags) yield return tag;
        }
        
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
        
        public void LinkAllChildren()
        {
            if (StartingItems is not null)
            {
                foreach (var item in StartingItems)
                {
                    if (item.Item) item.Item.LinkToProvider(this);
                }
            }
            
            if (StartingAbilities is not null)
            {
                foreach (var ability in StartingAbilities)
                {
                    if (ability) ability.LinkToProvider(this);
                }
            }
        }
        
        /// <summary>
        /// Unlinks all effects and abilities from this item.
        /// </summary>
        public void UnlinkAllChildren()
        {
            // Unlinked. This linked them again (GAS-30)
            if (StartingItems is not null)
            {
                foreach (var item in StartingItems)
                {
                    if (item.Item && item.Item.IsLinkedTo(this)) item.Item.Unlink();
                }
            }

            if (StartingAbilities is not null)
            {
                foreach (var ability in StartingAbilities)
                {
                    if (ability && ability.IsLinkedTo(this)) ability.Unlink();
                }
            }
        }
        public void InitWorkers(ISource system)
        {
            WorkerGroup?.ProvideWorkersTo(system);
        }
        public void RemoveWorkers(ISource system)
        {
            WorkerGroup?.RemoveWorkersFrom(system);
        }
    }

    [Serializable]
    public class StartingItemContainer
    {
        public Item Item;
        [Tooltip("Will attempt to equip the item when the item is given.")]
        public bool EquipOnInit;
    }
}
