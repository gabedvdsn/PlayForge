using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Entity", fileName = "Entity_")]
    public class EntityIdentity : BaseForgeObject, IHasReadableDefinition
    {
        public GASIdentityData Identity = new();
        
        public EAbilityActivationPolicy ActivationPolicy = EAbilityActivationPolicy.SingleActiveQueue;
        public int MaxAbilities = 99;
        public List<Ability> StartingAbilities = new();
        public bool AllowDuplicateAbilities;
        
        [SerializeReference] public List<AbstractImpactWorker> ImpactWorkers = new();
        
        [SerializeReference] public AttributeSet AttributeSet = new();
        [SerializeReference] public List<AbstractAttributeWorker> AttributeChangeEvents = new();
        
        public List<AbstractTagWorker> TagWorkers = new();
        public List<AbstractAnalysisWorker> AnalysisWorkers = new();
        
        [SerializeField]
        public List<DataWrapper> LocalData = new();
        
        public string GetName()
        {
            return Identity.DistinctName;
        }
        public override HashSet<Tag> GetAllTags()
        {
            return new HashSet<Tag>();
        }
        public string GetDescription()
        {
            return Identity.NameTag.Name;
        }
        public Sprite GetPrimaryIcon()
        {
            if (LocalData.TryGet(Tag.Generate("PrimaryIcon"), EDataWrapperType.Object, out var data)) return data.objectValue as Sprite;
            return null;
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
