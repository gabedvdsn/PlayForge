using System.Collections.Generic;
using FarEmerald.PlayForge;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Item", fileName = "Item_")]
    public class Item : BaseForgeLinkProvider, IHasReadableDefinition
    {
        public ItemDefinition Definition = new();
        public ItemTags Tags = new();

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling Configuration
        // ═══════════════════════════════════════════════════════════════════════════
        
        [Min(1)] public int StartingLevel = 1;
        [Min(1)] public int MaxLevel = 1;

        // ═══════════════════════════════════════════════════════════════════════════
        // Granted Effects & Abilities
        // ═══════════════════════════════════════════════════════════════════════════
        
        [Tooltip("Effects that are granted while this item is equipped. These effects can link to this item for level scaling.")]
        public List<GameplayEffect> GrantedEffects = new();

        [Tooltip("The active ability provided by this item. Can link to this item for level scaling.")]
        public Ability ActiveAbility;

        public StandardWorkerGroup WorkerGroup;

        // ═══════════════════════════════════════════════════════════════════════════
        // IHasReadableDefinition Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public string GetName()
        {
            return Definition.Name;
        }
        
        public string GetDescription()
        {
            return Definition.Description;
        }
        
        public Texture2D GetPrimaryIcon()
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
        // Granted Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override IEnumerable<Tag> GetGrantedTags()
        {
            yield return Tags.AssetTag;
            foreach (var t in Tags.PassiveGrantedTags) yield return t;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Utility Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Links all granted effects to this item as their level provider.
        /// </summary>
        public void LinkAllGrantedEffects()
        {
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
            //LinkAllPassiveAbilities();
        }
        
        /// <summary>
        /// Returns all effects that are linked to this item.
        /// </summary>
        public IEnumerable<GameplayEffect> GetLinkedEffects()
        {
            foreach (var effect in GrantedEffects)
            {
                if (effect != null && effect.IsLinkedTo(this))
                {
                    yield return effect;
                }
            }
        }
        
        /// <summary>
        /// Returns all abilities that are linked to this item.
        /// </summary>
        public IEnumerable<Ability> GetLinkedAbilities()
        {
            if (ActiveAbility != null && ActiveAbility.IsLinkedTo(this))
            {
                yield return ActiveAbility;
            }
        }

        public override string ToString()
        {
            return $"Item-{Definition.Name}";
        }
    }

    [System.Serializable]
    public class ItemDefinition
    {
        public string Name;
        [TextArea(2, 4)]
        public string Description;
        
        [ForgeTagContext(ForgeContext.Visibility)] 
        public Tag Visibility;
        
        public List<TextureItem> Textures = new();
    }

    [System.Serializable]
    public class ItemTags
    {
        [ForgeTagContext(ForgeContext.AssetIdentifier)]
        public Tag AssetTag;
        
        [ForgeTagContext(ForgeContext.ContextIdentifier)]
        public List<Tag> ContextTags = new();
        
        [ForgeTagContext(ForgeContext.Granted)]
        [Tooltip("Tags that are granted as long as this item is equipped")]
        public List<Tag> PassiveGrantedTags = new();
    }
}