using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Entity", fileName = "Entity_")]
    public class EntityIdentity : BaseForgeLinkProvider
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

        [FormerlySerializedAs("Level")] public int StartingLevel = 1;
        public bool CapAtMaxLevel = true;
        public int MaxLevel = 99;

        public float RelativeLevel => MaxLevel > 1 ? (StartingLevel - 1f) / (MaxLevel - 1f) : 1f;
        
        public EAbilityActivationPolicy ActivationPolicy = EAbilityActivationPolicy.QueueActivationIfBusy;
        
        public int MaxAbilities = 99;
        public List<Ability> StartingAbilities = new();
        public bool AllowDuplicateAbilities;

        public int MaxItems = 99;
        public List<Item> StartingItems = new();
        public bool AllowDuplicateItems;
        
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
        public override Texture2D GetPrimaryIcon()
        {
            foreach (var ti in Textures)
            {
                if (ti.Tag == PlayForge.Tags.PRIMARY) return ti.Texture;
            }
            return Textures.Count > 0 ? Textures[0].Texture : null;
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
        public override Tag GetProviderTag()
        {
            return AssetTag;
        }

        public override IEnumerable<Tag> GetGrantedTags()
        {
            yield return AssetTag;
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
        /// Returns true if this item is linked to a level provider.
        /// </summary>
        public override bool IsLinked => LinkMode == EItemLinkMode.LinkedToProvider && LinkedProvider != null;
        
        /// <summary>
        /// Links this item to a level provider.
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
                    if (item) item.LinkToProvider(this);
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
                    if (item && item.IsLinkedTo(this)) item.LinkToProvider(this);
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
    }
}
