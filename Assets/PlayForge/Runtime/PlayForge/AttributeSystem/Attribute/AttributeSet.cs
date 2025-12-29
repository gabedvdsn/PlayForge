using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Attribute Set", fileName = "AttributeSet_")]
    public class AttributeSet : BaseForgeObject
    {
        [SerializeField]
        public List<AttributeSetElement> Attributes;
        
        [Space]
        
        [SerializeReference]
        public List<AttributeSet> SubSets;
        public EValueCollisionPolicy CollisionResolutionPolicy = EValueCollisionPolicy.UseMaximum;
        
        public void Initialize(AttributeSystemComponent system)
        {
            AttributeSetMeta meta = new AttributeSetMeta(this);
            meta.InitializeAttributeSystem(system, this);
        }

        public HashSet<Attribute> GetUnique()
        {
            var attributes = new HashSet<Attribute>();
            foreach (var attr in Attributes)
            {
                attributes.Add(attr.Attribute);
            }

            foreach (var subSet in SubSets)
            {
                if (subSet is null || subSet == this) continue;
                foreach (var unique in subSet.GetUnique())
                {
                    attributes.Add(unique);
                }
            }

            return attributes;
        }
        public override HashSet<Tag> GetAllTags()
        {
            var set = new HashSet<Tag>();
            foreach (var elem in Attributes)
            {
                foreach (var tag in elem.Modifier.GetAllTags()) set.Add(tag);
            }

            return set;
        }
    }
    
    public enum ELimitedEffectImpactTarget
    {
        CurrentAndBase,
        Base
    }

    public enum EValueCollisionPolicy
    {
        UseMaximum,
        UseMinimum,
        UseAverage
    }
    
    [Serializable]
    public struct AttributeSetElement
    {
        // [Header("Attribute Declaration")]
        
        public Attribute Attribute;
        public float Magnitude;
        public AbstractCachedMagnitudeModifier Modifier;
        
        public ELimitedEffectImpactTarget Target;
        public AttributeOverflowData Overflow;
        
        [Header("Multiple Set Collision")]
        
        public EAttributeElementCollisionPolicy CollisionPolicy;

        public DefaultAttributeValue ToDefaultAttribute()
        {
            return Target switch
            {
                ELimitedEffectImpactTarget.CurrentAndBase => new DefaultAttributeValue(new ModifiedAttributeValue(Magnitude, Magnitude), Overflow, Modifier),
                ELimitedEffectImpactTarget.Base => new DefaultAttributeValue(new ModifiedAttributeValue(0, Magnitude), Overflow, Modifier),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public struct DefaultAttributeValue
    {
        public ModifiedAttributeValue DefaultValue;
        public AttributeOverflowData Overflow;
        public AbstractCachedMagnitudeModifier Modifier;

        public DefaultAttributeValue(ModifiedAttributeValue defaultValue, AttributeOverflowData overflow, AbstractCachedMagnitudeModifier modifier)
        {
            DefaultValue = defaultValue;
            Overflow = overflow;
            Modifier = modifier;
        }

        public DefaultAttributeValue Combine(DefaultAttributeValue other)
        {
            return new DefaultAttributeValue(DefaultValue.Combine(other.DefaultValue), Overflow, Modifier);
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

    public enum EAttributeOverflowPolicy
    {
        ZeroToBase,
        FloorToBase,
        ZeroToCeil,
        FloorToCeil,
        Unlimited
    }

    public class AttributeSetMeta
    {
        private Dictionary<Attribute, Dictionary<EAttributeElementCollisionPolicy, List<DefaultAttributeValue>>> matrix; 

        public AttributeSetMeta(AttributeSet attributeSet)
        {
            matrix = new Dictionary<Attribute, Dictionary<EAttributeElementCollisionPolicy, List<DefaultAttributeValue>>>();
            HandleAttributeSet(attributeSet);
        }

        private void HandleAttributeSet(AttributeSet attributeSet)
        {
            foreach (AttributeSetElement element in attributeSet.Attributes)
            {
                if (!matrix.TryGetValue(element.Attribute, out var table))
                {
                    table = matrix[element.Attribute] = new Dictionary<EAttributeElementCollisionPolicy, List<DefaultAttributeValue>>();
                }

                if (!table.ContainsKey(element.CollisionPolicy))
                {
                    matrix[element.Attribute][element.CollisionPolicy] = new List<DefaultAttributeValue> { element.ToDefaultAttribute() };
                }
                else matrix[element.Attribute][element.CollisionPolicy].Add(element.ToDefaultAttribute());
            }
            
            foreach (AttributeSet subSet in attributeSet.SubSets) HandleAttributeSet(subSet);
        }

        public void InitializeAttributeSystem(AttributeSystemComponent system, AttributeSet attributeSet)
        {
            foreach (Attribute attribute in matrix.Keys)
            {
                if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseThis, out var defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.Combine, out defaults))
                {
                    DefaultAttributeValue defaultValue = new DefaultAttributeValue();
                    foreach (DefaultAttributeValue metaMav in defaults) defaultValue = defaultValue.Combine(metaMav);

                    system.ProvideAttribute(attribute, defaultValue);
                }
                else if (matrix[attribute].TryGetValue(EAttributeElementCollisionPolicy.UseExisting, out defaults))
                {
                    InitializeAggregatePolicy(system, attribute, defaults, attributeSet.CollisionResolutionPolicy);
                }
            }
        }

        private void InitializeAggregatePolicy(AttributeSystemComponent system, Attribute attribute, List<DefaultAttributeValue> defaults, EValueCollisionPolicy resolution)
        {
            switch (resolution)
            {
                case EValueCollisionPolicy.UseAverage:
                {
                    float _current = defaults.Average(mav => mav.DefaultValue.DeltaCurrentValue);
                    float _base = defaults.Average(mav => mav.DefaultValue.DeltaBaseValue);

                    system.ProvideAttribute(attribute, new DefaultAttributeValue(new ModifiedAttributeValue(_current, _base), defaults[0].Overflow, defaults[0].Modifier));
                    break;
                }
                case EValueCollisionPolicy.UseMaximum:
                {
                    float _current = defaults.Max(mav => mav.DefaultValue.DeltaCurrentValue);
                    float _base = defaults.Max(mav => mav.DefaultValue.DeltaBaseValue);

                    system.ProvideAttribute(attribute, new DefaultAttributeValue(new ModifiedAttributeValue(_current, _base), defaults[0].Overflow, defaults[0].Modifier));
                    break;
                }
                case EValueCollisionPolicy.UseMinimum:
                {
                    float _current = defaults.Min(mav => mav.DefaultValue.DeltaCurrentValue);
                    float _base = defaults.Min(mav => mav.DefaultValue.DeltaBaseValue);

                    system.ProvideAttribute(attribute, new DefaultAttributeValue(new ModifiedAttributeValue(_current, _base), defaults[0].Overflow, defaults[0].Modifier));
                    break;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
            }
        }
    }
}
