using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Entity", fileName = "Entity_")]
    public class EntityIdentity : BaseForgeLinkProvider, IHasReadableDefinition
    {
        public string Name;
        public string Description;
        public List<TextureItem> Textures;
        
        [ForgeTagContext(ForgeContext.Affiliation)]
        public List<Tag> Affiliation;
        
        [ForgeTagContext(ForgeContext.AssetIdentifier)]
        public Tag AssetTag;
        [ForgeTagContext(ForgeContext.Granted)] public List<Tag> GrantedTags;

        public int Level = 1;
        public bool CapAtMaxLevel = true;
        public int MaxLevel = 99;

        public float RelativeLevel => CapAtMaxLevel ? 1f : (MaxLevel > 1 ? (Level - 1f) / (MaxLevel - 1f) : 1f);
        
        public EAbilityActivationPolicy ActivationPolicy = EAbilityActivationPolicy.QueueActivationIfBusy;
        
        public int MaxAbilities = 99;
        public List<Ability> StartingAbilities = new();
        public bool AllowDuplicateAbilities;

        public int MaxItems = 99;
        public List<Item> StartingItems = new();
        public bool AllowDuplicateItems;
        
        [SerializeReference] public AttributeSet AttributeSet;

        public StandardWorkerGroup WorkerGroup;
        
        [SerializeField]
        public List<DataWrapper> LocalData = new();
        
        public string GetName()
        {
            return Name;
        }
        public string GetDescription()
        {
            return Description;
        }
        public Texture2D GetPrimaryIcon()
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
            return Level;
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
    }
}
