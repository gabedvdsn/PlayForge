using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Entity", fileName = "Entity_")]
    public class EntityIdentity : BaseForgeObject, IHasReadableDefinition
    {
        public string Name;
        public string Description;
        public List<TextureItem> Textures;
        
        public List<Tag> Affiliation;
        
        public Tag AssetTag;
        public List<Tag> GrantedTags;

        [FormerlySerializedAs("StartingLevel")] public int Level = 1;
        public bool CapAtMaxLevel = true;
        public int MaxLevel = 99;

        public float RelativeLevel => CapAtMaxLevel ? 1f : (Level - 1f) / (MaxLevel - 1f);
        
        public EAbilityActivationPolicy ActivationPolicy = EAbilityActivationPolicy.SingleActiveQueue;
        public int MaxAbilities = 99;
        public List<Ability> StartingAbilities = new();
        public bool AllowDuplicateAbilities;
        
        [SerializeReference] public List<AbstractImpactWorker> ImpactWorkers = new();
        
        [SerializeReference] public AttributeSet AttributeSet;
        [SerializeReference] public List<AbstractAttributeWorker> AttributeChangeEvents = new();
        
        public List<AbstractTagWorker> TagWorkers = new();
        public List<AbstractAnalysisWorker> AnalysisWorkers = new();
        
        [SerializeField]
        public List<DataWrapper> LocalData = new();
        
        public string GetName()
        {
            return Name;
        }
        public override HashSet<Tag> GetAllTags()
        {
            var set = new HashSet<Tag>();
            foreach (var t in Affiliation) set.Add(t);
            set.Add(AssetTag);
            foreach (var t in GrantedTags) set.Add(t);
            foreach (var ability in StartingAbilities)
            {
                foreach (var t in ability.GetAllTags()) set.Add(t);
            }

            foreach (var iw in ImpactWorkers)
            {
                foreach (var t in iw.GetAllTags()) set.Add(t);
            }

            foreach (var t in AttributeSet.GetAllTags()) set.Add(t);

            foreach (var ace in AttributeChangeEvents)
            {
                foreach (var t in ace.GetAllTags()) set.Add(t);
            }

            foreach (var tw in TagWorkers)
            {
                foreach (var t in tw.GetAllTags()) set.Add(t);
            }

            foreach (var aw in AnalysisWorkers)
            {
                foreach (var t in aw.GetAllTags()) set.Add(t);
            }

            foreach (var dw in LocalData)
            {
                set.Add(dw.Key);
                if (dw.tagValue != default) set.Add(dw.tagValue);
            }

            return set;

        }
        public string GetDescription()
        {
            return Description;
        }
        public Texture2D GetPrimaryIcon()
        {
            if (LocalData.TryGet(Tag.Generate("PrimaryIcon"), EDataWrapperType.Object, out var data)) return data.objectValue as Texture2D;
            return Textures.Count > 0 ? Textures[0].Texture : null;
        }
    }

    public class GameRootEntity : EntityIdentity
    {
        public GameRootEntity()
        {
            MaxAbilities = int.MaxValue;
            ActivationPolicy = EAbilityActivationPolicy.NoRestrictions;
        }
    }
}
