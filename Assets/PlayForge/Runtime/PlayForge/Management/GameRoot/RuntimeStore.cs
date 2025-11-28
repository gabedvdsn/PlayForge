using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using Unity.VisualScripting.Dependencies.Sqlite;

namespace FarEmerald.PlayForge
{
    public partial class GameRoot
    {
        private FrameworkProject Fp;

        private static Dictionary<int, AttributeData> attributes;
        private static Dictionary<int, TagData> tags;

        private static Dictionary<int, AbilityData> abilities;
        private static Dictionary<int, AttributeSetData> attributeSets;

        private static Dictionary<int, EntityData> entities;
        private static Dictionary<Tag, int> entityMap;

        private static Dictionary<int, EffectData> effects;
        private static Dictionary<int, GameplayEffect> effectMap;

        public static void SetFramework(FrameworkProject fp)
        {
            Instance.Fp = fp;
            Instance.Prepare();
        }

        void Prepare()
        {
            attributes = Fp?.Attributes.ToDictionary(d => d.Id) ?? new Dictionary<int, AttributeData>();
        }

        public static Attribute ResolveAttribute(AttributeRef aref)
        {
            return Attribute.Generate(attributes[aref.Id].Name, attributes[aref.Id].Name);
        }
        
        public static Tag ResolveTag(TagRef aref)
        {
            return Tag.Generate(aref.Id, aref.Name);
        }

        public static Tag ResolveTag(string tagName)
        {
            // TODO FIXXXXMEEEEEE
            return Tag.Generate(0, tagName);
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
            var data = effects[aref.Id];
            return new GameplayEffect()
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
                    Identity = new GASIdentityData(){ Affiliation = new List<Tag>(){ Tags.AFFILIATION_NONE }, NameTag = Tag.Generate("Unresolved Entity"), MaxLevel = 1, Level = 1 },
                    ActivationPolicy = EAbilityActivationPolicy.NoRestrictions,
                    MaxAbilities = 0,
                    StartingAbilities = new List<Ability>(),
                    AllowDuplicateAbilities = false,
                    ImpactWorkers = new List<AbstractImpactWorker>(),
                    AttributeSet = new AttributeSet(){ Attributes = new List<AttributeSetElement>(), SubSets = new List<AttributeSet>(), CollisionResolutionPolicy = EValueCollisionPolicy.UseAverage },
                    AttributeChangeEvents = new List<AbstractAttributeWorker>(),
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

    /// <summary>
    /// Instead of loading all framework data, pass a LoadFrameworkSettings into the Framework Loader (Bootstrapper)
    /// to load only certain pieces of data. Keep memory overhead low.
    /// </summary>
    public class LoadFrameworkSettings
    {
        
    }
}
