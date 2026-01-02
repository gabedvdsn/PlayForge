using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Ability", fileName = "Ability_")]
    public class Ability : BaseForgeLinkProvider, IHasReadableDefinition
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
        
        [SerializeField]
        public List<DataWrapper> LocalData = new();
        
        [Min(0)] public int StartingLevel = 0;
        [Min(0)] public int MaxLevel = 4;
        public bool IgnoreWhenLevelZero = true;
        
        [ForgeEffectTemplate("Cost", "Cost")] public GameplayEffect Cost;
        [ForgeEffectTemplate("Cooldown", "Cooldown")]public GameplayEffect Cooldown;

        
        [SerializeReference] public List<AbstractAttributeWorker> AttributeWorkers;
        [SerializeReference] public List<AbstractTagWorker> TagWorkers;
        [SerializeReference] public List<AbstractImpactWorker> ImpactWorkers;
        [SerializeReference] public List<AbstractAnalysisWorker> AnalysisWorkers;

        public AbilitySpec Generate(ISource owner, int level)
        {
            return new AbilitySpec(owner, this, level);
        }

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
            if (LocalData.TryGet(Tag.Generate("PrimaryIcon"), EDataWrapperType.Object, out var data)) 
                return data.objectValue as Texture2D;
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