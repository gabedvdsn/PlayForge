using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Attribute Set", fileName = "AttributeSet_")]
    public class AttributeSet : BaseForgeObject, IHasReadableDefinition
    {
        public string Name;
        public string Description;
        public List<TextureItem> Textures = new();
        
        [ForgeTagContext(ForgeContext.AssetIdentifier)]
        public Tag AssetTag;
        
        [SerializeField]
        public List<AttributeSetElement> Attributes = new();
        
        [Space]
        
        [SerializeReference]
        public List<AttributeSet> SubSets;
        public EValueCollisionPolicy CollisionResolutionPolicy = EValueCollisionPolicy.UseMaximum;

        public StandardWorkerGroup WorkerGroup;
        
        public void Initialize(AttributeSystemComponent system)
        {
            AttributeSetMeta meta = new AttributeSetMeta(this);
            meta.InitializeAttributeSystem(system, this);

            WorkerGroup.ProvideWorkersTo(system.Root);
        }

        public HashSet<Attribute> GetUnique()
        {
            var attributes = new HashSet<Attribute>();
            foreach (var attr in Attributes)
            {
                attributes.Add(attr.Attribute);
            }

            if (SubSets is not null)
            {
                foreach (var subSet in SubSets)
                {
                    if (subSet is null || subSet == this) continue;
                    foreach (var unique in subSet.GetUnique())
                    {
                        attributes.Add(unique);
                    }
                }
            }

            return attributes;
        }
        public string GetName()
        {
            return Name;
        }
        public override IEnumerable<Tag> GetGrantedTags()
        {
            yield return AssetTag;
            foreach (var subset in SubSets)
            {
                foreach (var t in subset.GetGrantedTags()) yield return t;
            }
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
        
    }
    
    public enum ELimitedEffectImpactTarget
    {
        CurrentAndBase,
        Base
    }
    
    [Serializable]
    public class AttributeSetElement
    {
        // [Header("Attribute Declaration")]
        
        public Attribute Attribute;
        public float Magnitude;
        [SerializeReference] public AbstractCachedScaler Scaling;
        
        public ELimitedEffectImpactTarget Target;
        public AttributeOverflowData Overflow;

        [ForgeTagContext(ForgeContext.RetentionGroup)]
        public Tag RetentionGroup = Tags.DEFAULT;
        
        public EAttributeElementCollisionPolicy CollisionPolicy;

        public AttributeConstraints Constraints = new();

        public AttributeBlueprint ToAttributeBlueprint()
        {
            return Target switch
            {
                ELimitedEffectImpactTarget.CurrentAndBase => new AttributeBlueprint(new ModifiedAttributeValue(Magnitude, Magnitude), Overflow, Constraints, Scaling, RetentionGroup),
                ELimitedEffectImpactTarget.Base => new AttributeBlueprint(new ModifiedAttributeValue(0, Magnitude), Overflow, Constraints, Scaling, RetentionGroup),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public struct AttributeBlueprint
    {
        public ModifiedAttributeValue DefaultValue;
        public AttributeOverflowData Overflow;
        public AttributeConstraints Constraints;
        public AbstractCachedScaler Modifier;
        public Tag RetentionGroup;

        public AttributeBlueprint(ModifiedAttributeValue defaultValue, AttributeOverflowData overflow, AttributeConstraints constraints, AbstractCachedScaler modifier, Tag retentionGroup)
        {
            DefaultValue = defaultValue;
            Overflow = overflow;
            Constraints = constraints;
            Modifier = modifier;
            RetentionGroup = retentionGroup;
        }

        public AttributeBlueprint Combine(AttributeBlueprint other)
        {
            return new AttributeBlueprint(DefaultValue.Combine(other.DefaultValue), Overflow, Constraints, Modifier, RetentionGroup);
        }

        public AttributeValue ToAttributeValue() => DefaultValue.ToAttributeValue();
    }
    
    public enum EAttributeElementCollisionPolicy
    {
        UseCollisionSetting,
        UseThis,
        UseExisting,
        Combine
    }
    
    [Serializable]
    public struct AttributeOverflowData
    {
        public EAttributeOverflowPolicy Policy;
        public AttributeValue Floor;
        public AttributeValue Ceil;
    }
    
    [Serializable]
    public class AttributeConstraints
    {
        [Tooltip("Auto clamp attribute values that fall outside of bounds")]
        public bool AutoClamp = true;  // Use Overflow data for clamping
    
        [Tooltip("Auto scale current value to changes in base value")]
        public bool AutoScaleWithBase;
    
        [Tooltip("Round attribute values after changes are applied. Will artificially increase/decrease attribute impact.")]
        public EAttributeRoundingPolicy RoundingMode;
        public float SnapInterval;  // For Snap mode
    }

    public enum EAttributeOverflowPolicy
    {
        ZeroToBase,
        FloorToBase,
        ZeroToCeil,
        FloorToCeil,
        Unlimited
    }

    public enum EAttributeRoundingPolicy
    {
        None,
        ToFloor,
        ToCeil,
        Round,
        SnapTo
    }

    public class AttributeSetMeta
    {
        private Dictionary<Attribute, Dictionary<EAttributeElementCollisionPolicy, List<AttributeBlueprint>>> matrix; 

        public AttributeSetMeta(AttributeSet attributeSet)
        {
            matrix = new Dictionary<Attribute, Dictionary<EAttributeElementCollisionPolicy, List<AttributeBlueprint>>>();
            HandleAttributeSet(attributeSet);
        }

        private void HandleAttributeSet(AttributeSet attributeSet)
        {
            if (!attributeSet) return;
            
            foreach (AttributeSetElement element in attributeSet.Attributes)
            {
                if (!matrix.TryGetValue(element.Attribute, out var table))
                {
                    table = matrix[element.Attribute] = new();
                }

                if (!table.ContainsKey(element.CollisionPolicy))
                {
                    matrix[element.Attribute][element.CollisionPolicy] = new List<AttributeBlueprint>() { element.ToAttributeBlueprint() };
                }
                else matrix[element.Attribute][element.CollisionPolicy].Add(element.ToAttributeBlueprint());
            }
            
            foreach (AttributeSet subSet in attributeSet.SubSets) HandleAttributeSet(subSet);
        }

        public void InitializeAttributeSystem(AttributeSystemComponent system, AttributeSet attributeSet)
        {
            foreach (Attribute attribute in matrix.Keys)
            {
                if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseCollisionSetting, out var defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseThis, out defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.Combine, out defaults))
                {
                    var blueprint = new AttributeBlueprint();
                    foreach (var bp in defaults) blueprint = blueprint.Combine(bp);

                    system.ProvideAttribute(attribute, blueprint);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseExisting, out defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
            }
        }

        private void InitializeAggregatePolicy(AttributeSystemComponent system, Attribute attribute, List<AttributeBlueprint> defaults, EValueCollisionPolicy resolution)
        {
            switch (resolution)
            {
                case EValueCollisionPolicy.UseAverage:
                {
                    float _current = defaults.Average(mav => mav.DefaultValue.DeltaCurrentValue);
                    float _base = defaults.Average(mav => mav.DefaultValue.DeltaBaseValue);

                    system.ProvideAttribute(attribute,
                        new AttributeBlueprint(
                            new ModifiedAttributeValue(_current, _base),
                            defaults[0].Overflow, defaults[0].Constraints, defaults[0].Modifier, defaults[0].RetentionGroup)
                    );
                    break;
                }
                case EValueCollisionPolicy.UseMaximum:
                {
                    int idx = defaults.IndexMax(mav => mav.DefaultValue.DeltaBaseValue);
                    system.ProvideAttribute(attribute, defaults[idx]);
                    break;
                }
                case EValueCollisionPolicy.UseMinimum:
                {
                    int idx = defaults.IndexMin(mav => mav.DefaultValue.DeltaBaseValue);
                    system.ProvideAttribute(attribute, defaults[idx]);
                    break;
                }
                case EValueCollisionPolicy.UseFirst:
                {
                    var bp = defaults.First();
                    system.ProvideAttribute(attribute, bp);
                    break;
                }
                case EValueCollisionPolicy.UseLast:
                {
                    var bp = defaults.Last();
                    system.ProvideAttribute(attribute, bp);
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
            }
        }
    }
}
