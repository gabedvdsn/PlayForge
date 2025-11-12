using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FarEmerald.PlayForge.Extended;

namespace FarEmerald.PlayForge
{
    public static class RuntimeStore
    {
        private static FrameworkProject fp;
        
        // Value Types -> resolved each generation
        static Dictionary<int, AttributeData> attributes;
        static Dictionary<int, TagData> tags;
        
        // Generated once per source
        static Dictionary<int, AbilityData> abilities;
        static Dictionary<int, AttributeSetData> attributeSets;
        
        static Dictionary<int, EntityData> entities;
        private static Dictionary<Tag, int> entityMap;
        
        /// <summary>
        /// EffectSpecs are generated each call
        /// Store GameplayEffect bases
        /// </summary>
        static Dictionary<int, EffectData> effects;
        static Dictionary<int, GameplayEffect> effectBases;
        
        public static void SetFramework(FrameworkProject framework)
        {
            fp = framework;
            
            Prepare();
        }

        static void Prepare()
        {
            // Value types
            attributes = fp?.Attributes?.ToDictionary(d => d.Id) ?? new Dictionary<int, AttributeData>();
            tags = fp?.Tags?.ToDictionary(d => d.Id) ?? new();
            
            // Single generation
            abilities = fp?.Abilities?.ToDictionary(d => d.Id) ?? new();
            entities = fp?.Entities?.ToDictionary(d => d.Id) ?? new();
            attributeSets = fp?.AttributeSets?.ToDictionary(d => d.Id) ?? new();
            
            // Per-call generation
            effects = fp?.Effects?.ToDictionary(d => d.Id) ?? new();
            
            effectBases = new Dictionary<int, GameplayEffect>();
            foreach (var kvp in effects)
            {
                var data = effects[kvp.Key];
                effectBases[kvp.Key] = new GameplayEffect()
                {
                    Definition = data.Definition,
                    Tags = data.Tags,
                    ImpactSpecification = data.ImpactSpecification,
                    DurationSpecification = data.DurationSpecification,
                    Workers = data.Workers,
                    SourceRequirements = data.SourceRequirements,
                    TargetRequirements = data.TargetRequirements
                };
            }
            
            // Create entity map
            entityMap = new Dictionary<Tag, int>();
            foreach (var d in entities) entityMap[d.Value.Identity.NameTag] = d.Key;
        }
        
        public static Attribute ResolveAttribute(AttributeRef aref)
        {
            return Attribute.Generate(attributes[aref.Id].Name, attributes[aref.Id].Name);
        }
        
        public static Tag ResolveTag(TagRef aref)
        {
            return Tag.Generate(tags[aref.Id].Name);
        }

        public static Ability ResolveAbility(AbilityRef aref)
        {
            var data = abilities[aref.Id];
            return new Ability()
            {
                Definition = data.Definition,
                Tags = data.Tags,
                Proxy = data.Proxy,
                StartingLevel = data.StartingLevel,
                MaxLevel = data.MaxLevel,
                IgnoreWhenLevelZero = data.IgnoreWhenLevelZero,
                Cost = data.Cost,
                Cooldown = data.Cooldown
            };
        }
        
        public static GameplayEffect ResolveEffect(EffectRef aref)
        {
            return effectBases[aref.Id];
        }
        
        public static EntityIdentity ResolveEntity(EntityRef aref)
        {
            var data = entities[aref.Id];
            return new EntityIdentity()
            {
                Identity = data.Identity,
                ActivationPolicy = data.ActivationPolicy,
                MaxAbilities = data.MaxAbilities,
                StartingAbilities = data.StartingAbilities,
                AllowDuplicateAbilities = data.AllowDuplicateAbilities,
                ImpactWorkers = data.ImpactWorkers,
                AttributeSet = data.AttributeSet,
                AttributeChangeEvents = data.AttributeChangeEvents,
                TagWorkers = data.TagWorkers
            };
        }

        public static EntityIdentity ResolveEntity(Tag assetTag)
        {
            var data = entities[entityMap[assetTag]];
            if (data is null)
                return new EntityIdentity()
                {
                    Identity = new GASIdentityData(){ Affiliation = Tags.AFFILIATION_NONE, NameTag = Tag.Generate("Unresolved Entity"), MaxLevel = 1, Level = 1 },
                    ActivationPolicy = EAbilityActivationPolicy.NoRestrictions,
                    MaxAbilities = 0,
                    StartingAbilities = new List<Ability>(),
                    AllowDuplicateAbilities = false,
                    ImpactWorkers = new List<AbstractImpactWorker>(),
                    AttributeSet = new AttributeSet(){ Attributes = new List<AttributeSetElement>(), SubSets = new List<AttributeSet>(), CollisionResolutionPolicy = EValueCollisionPolicy.UseAverage },
                    AttributeChangeEvents = new List<AbstractAttributeChangeEvent>(),
                    TagWorkers = new List<AbstractTagWorker>()
                };
            
            return new EntityIdentity()
            {
                Identity = data.Identity,
                ActivationPolicy = data.ActivationPolicy,
                MaxAbilities = data.MaxAbilities,
                StartingAbilities = data.StartingAbilities,
                AllowDuplicateAbilities = data.AllowDuplicateAbilities,
                ImpactWorkers = data.ImpactWorkers,
                AttributeSet = data.AttributeSet,
                AttributeChangeEvents = data.AttributeChangeEvents,
                TagWorkers = data.TagWorkers
            }; 
        }
        
        public static AttributeSet ResolveAttributeSet(AttributeSetRef aref)
        {
            var data = attributeSets[aref.Id];
            return new AttributeSet()
            {
                Attributes = data.Attributes,
                SubSets = data.SubSets,
                CollisionResolutionPolicy = data.CollisionResolutionPolicy
            };
        }
    }
}
