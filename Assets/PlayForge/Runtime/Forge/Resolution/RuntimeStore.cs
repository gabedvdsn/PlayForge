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
        
        
    }
}
