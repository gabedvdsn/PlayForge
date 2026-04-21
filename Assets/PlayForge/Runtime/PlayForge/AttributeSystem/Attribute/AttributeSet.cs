using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Attribute Set", fileName = "AttributeSet_")]
    public class AttributeSet : BaseForgeAsset
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

            WorkerGroup.ProvideWorkersTo(system.Self);
        }

        public HashSet<IAttribute> GetUnique()
        {
            var attributes = new HashSet<IAttribute>();
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
        public override string GetName()
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
        public override string GetDescription()
        {
            return Description;
        }
        public override Texture2D GetDefaultIcon()
        {
            return ForgeHelper.GetTextureItem(Textures, PlayForge.Tags.PRIMARY);
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
        public Attribute Attribute;
        public float Magnitude;
        
        public ELimitedEffectImpactTarget Target;
        public AttributeOverflowData Overflow;
        
        [ScalerRootAssignment(typeof(AbstractCachedScaler))]
        [SerializeReference] public AbstractCachedScaler Scaling;
        public EMagnitudeOperation RealMagnitude;

        [ForgeTagContext(ForgeContext.RetentionGroup)]
        public Tag RetentionGroup = Tags.DEFAULT;
        
        public EAttributeElementCollisionPolicy CollisionPolicy;

        public AttributeConstraints Constraints = new();

        public AttributeValue RootValue => Target switch
        {
            ELimitedEffectImpactTarget.CurrentAndBase => new AttributeValue(Magnitude, Magnitude),
            ELimitedEffectImpactTarget.Base => new AttributeValue(0f, Magnitude),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public enum EAttributeElementCollisionPolicy
    {
        UseSetCollisionSetting,
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
        private Dictionary<IAttribute, Dictionary<EAttributeElementCollisionPolicy, List<AttributeBlueprint>>> matrix; 

        public AttributeSetMeta(AttributeSet attributeSet)
        {
            matrix = new Dictionary<IAttribute, Dictionary<EAttributeElementCollisionPolicy, List<AttributeBlueprint>>>();
            HandleAttributeSet(attributeSet);
        }

        private void HandleAttributeSet(AttributeSet attributeSet)
        {
            if (!attributeSet) return;
            
            foreach (AttributeSetElement element in attributeSet.Attributes)
            {
                Debug.Log($"Handling {element.Attribute.GetName()}");
                if (!matrix.TryGetValue(element.Attribute, out var table))
                {
                    table = matrix[element.Attribute] = new();
                }

                if (!table.ContainsKey(element.CollisionPolicy))
                {
                    matrix[element.Attribute][element.CollisionPolicy] = new List<AttributeBlueprint>() { new AttributeBlueprint(element) };
                }
                else matrix[element.Attribute][element.CollisionPolicy].Add(new AttributeBlueprint(element));
            }
            
            foreach (AttributeSet subSet in attributeSet.SubSets) HandleAttributeSet(subSet);
        }

        public void InitializeAttributeSystem(AttributeSystemComponent system, AttributeSet attributeSet)
        {
            foreach (IAttribute attribute in matrix.Keys)
            {
                if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseSetCollisionSetting, out var defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseThis, out defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.Combine, out defaults))
                {
                    AttributeBlueprint blueprint = null;
                    if (defaults.Count > 0)
                    {
                        blueprint = defaults[0];
                        foreach (var bp in defaults) blueprint.Combine(bp);
                        for (int i = 1; i < defaults.Count; i++)
                        {
                            blueprint.Combine(defaults[i]);
                        }
                    }

                    system.ProvideAttribute(attribute, blueprint);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseExisting, out defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
            }
        }

        private void InitializeAggregatePolicy(AttributeSystemComponent system, IAttribute attribute, List<AttributeBlueprint> defaults, EValueCollisionPolicy resolution)
        {
            switch (resolution)
            {
                case EValueCollisionPolicy.UseAverage:
                {
                    float _current = defaults.Average(mav => mav.RootValue.CurrentValue);
                    float _base = defaults.Average(mav => mav.RootValue.BaseValue);

                    system.ProvideAttribute(attribute,
                        new AttributeBlueprint(defaults[0].Base)
                    );
                    break;
                }
                case EValueCollisionPolicy.UseMaximum:
                {
                    int idx = defaults.IndexMax(mav => mav.RootValue.BaseValue);
                    system.ProvideAttribute(attribute, defaults[idx]);
                    break;
                }
                case EValueCollisionPolicy.UseMinimum:
                {
                    int idx = defaults.IndexMin(mav => mav.RootValue.BaseValue);
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
