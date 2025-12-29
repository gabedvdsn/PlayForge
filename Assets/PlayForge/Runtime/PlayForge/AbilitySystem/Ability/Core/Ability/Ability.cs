using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Ability", fileName = "Ability_")]
    public class Ability : BaseForgeObject, IHasReadableDefinition
    {
        public AbilityDefinition Definition;
        public AbilityTags Tags;
        public AbilityBehaviour Proxy;

        [SerializeReference] 
        public List<IAbilityValidationRule> SourceActivationRules = new List<IAbilityValidationRule>()
        {
            new CooldownValidation(),
            new CostValidation(),
            new RangeValidation(),
            new SourceAttributeValidation(),
            new TargetAttributeValidation()
        };
        
        [SerializeReference] 
        public List<IAbilityValidationRule> TargetActivationRules = new();
        
        //[SerializeReference]
        [SerializeField]
        public List<DataWrapper> LocalData = new();
        
        [Min(0)] public int StartingLevel = 0;
        [Min(0)] public int MaxLevel = 4;
        public bool IgnoreWhenLevelZero = true;
        
        public GameplayEffect Cost;
        public GameplayEffect Cooldown;

        
        [SerializeReference] public List<AbstractAttributeWorker> AttributeWorkers;
        [SerializeReference] public List<AbstractTagWorker> TagWorkers;
        [SerializeReference] public List<AbstractImpactWorker> ImpactWorkers;
        [SerializeReference] public List<AbstractAnalysisWorker> AnalysisWorkers;

        public AbilitySpec Generate(ISource owner, int level)
        {
            return new AbilitySpec(owner, this, level);
        }
        
        public override HashSet<Tag> GetAllTags()
        {
            var tags = new HashSet<Tag>()
            {
                Tags.AssetTag
            };
            
            foreach (var tag in Tags.ContextTags)
            {
                tags.Add(tag);
            }
            
            foreach (var tag in Tags.ActiveGrantedTags)
            {
                tags.Add(tag);
            }
            
            foreach (var tag in Tags.PassiveGrantedTags)
            {
                tags.Add(tag);
            }
            
            foreach (var tag in Tags.SourceRequirements.AvoidTags) tags.Add(tag.Tag);
            foreach (var tag in Tags.SourceRequirements.RequireTags) tags.Add(tag.Tag);
            foreach (var tag in Tags.TargetRequirements.AvoidTags) tags.Add(tag.Tag);
            foreach (var tag in Tags.TargetRequirements.RequireTags) tags.Add(tag.Tag);

            return tags;
        }

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
            if (LocalData.TryGet(Tag.Generate("PrimaryIcon"), EDataWrapperType.Object, out var data)) return data.objectValue as Texture2D;
            return Definition.Textures.Count > 0 ? Definition.Textures[0].Texture : null;
        }

        public bool TryGetLocalData(Tag key, out DataWrapper data)
        {
            data = LocalData.FirstOrDefault(ld => ld.Key == key);
            return data != null;
        }

        public override string ToString()
        {
            return Tags.AssetTag.GetName();
        }
    }

    public enum EDataWrapperType
    {
        Object,
        String,
        Int,
        Float,
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
        public int intValue;
        public float floatValue;
        public Object objectValue;
        public Tag tagValue;
        public Attribute attributeValue;
        public GameplayEffect effectValue;
        public Ability abilityValue;
        public EntityIdentity entityValue;
    }
}
