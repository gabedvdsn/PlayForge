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

        public int StartingLevel = 1;
        public bool CapAtMaxLevel = true;
        public int MaxLevel = 99;
        
        public EAbilityActivationPolicy ActivationPolicy = EAbilityActivationPolicy.QueueActivationIfBusy;
        
        [ScalerOperationKeyword("Max Abilities")]
        public ScalerIntegerMagnitudeOperation MaxAbilitiesOperation;
        public List<Ability> StartingAbilities = new();
        public bool AllowDuplicateAbilities;

        [ScalerOperationKeyword("Max Items")]
        public ScalerIntegerMagnitudeOperation MaxItemsOperation;
        [ScalerOperationKeyword("Max Equipped Items")]
        public ScalerIntegerMagnitudeOperation MaxEquippedItemsOperation;
        public List<StartingItemContainer> StartingItems = new();
        public bool AllowDuplicateItems;
        public bool AllowDuplicateEquippedItems;
        
        [SerializeReference] public AttributeSet AttributeSet;

        public StandardWorkerGroup WorkerGroup;
        
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
            if (StartingItems is not null)
            {
                foreach (var item in StartingItems)
                {
                    if (item.Item && item.Item.IsLinkedTo(this)) item.Item.LinkToProvider(this);
                }
            }
            
            if (StartingAbilities is not null)
            {
                foreach (var ability in StartingAbilities)
                {
                    if (ability && ability.IsLinkedTo(this)) ability.LinkToProvider(this);
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
