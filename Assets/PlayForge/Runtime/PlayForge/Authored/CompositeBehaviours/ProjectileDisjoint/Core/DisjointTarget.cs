using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Modifications applied to disjoint targets are
    /// </summary>
    public class DisjointTarget : ITarget
    {
        private ITarget original;
        private DisjointTransformPacket packet;

        public DisjointTarget(ITarget original, DisjointTransformPacket packet)
        {
            this.original = original;
            this.packet = packet;
        }

        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity)
        {
            
        }
        public List<Tag> GetAffiliation()
        {
            return original.GetAffiliation();
        }
        public List<Tag> GetAppliedTags()
        {
            return new List<Tag>();
        }
        public int GetWeight(Tag _tag)
        {
            return 0;
        }
        public bool ApplyGameplayEffect(GameplayEffectSpec spec)
        {
            return false;
        }
        public void RemoveGameplayEffect(GameplayEffect effect)
        {
            
        }
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect GameplayEffect)
        {
            return original.GenerateEffectSpec(origin, GameplayEffect);
        }
        public bool FindAttributeSystem(out AttributeSystemComponent attrSystem)
        {
            return original.FindAttributeSystem(out attrSystem);
        }
        public bool FindAbilitySystem(out AbilitySystemComponent abilSystem)
        {
            return original.FindAbilitySystem(out abilSystem);
        }
        public bool TryGetAttributeValue(Attribute attribute, out AttributeValue value)
        {
            value = default;
            return false;
        }
        public bool TryModifyAttribute(Attribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            return false;
        }
        public AbstractTransformPacket AsTransform()
        {
            return packet;
        }
        public static DisjointTarget Generate(ITarget _target)
        {
            return new DisjointTarget(
                _target,
                new DisjointTransformPacket(_target.AsTransform())
            );
        }
    }

    public abstract class AbstractTransformPacket
    {
        public abstract Vector3 position { get; set; }
        public abstract Quaternion rotation { get; set; }
        public abstract Vector3 scale { get; set; }
    }

    public class DefaultTransformPacket : AbstractTransformPacket
    {
        private Transform transform;

        public DefaultTransformPacket(Transform transform)
        {
            this.transform = transform;
        }

        public override Vector3 position
        {
            get => transform.position;
            set => transform.position = value;
        }

        public override Quaternion rotation
        {
            get => transform.rotation;
            set => transform.rotation = value;
        }
            
        public override Vector3 scale
        {
            get => transform.localScale;
            set => transform.localScale = value;
        }
    }

    public class DisjointTransformPacket : AbstractTransformPacket
    {
        private Vector3 _position;
        private Quaternion _rotation;
        private Vector3 _scale;
        
        public DisjointTransformPacket(Vector3 _pos, Quaternion _rot, Vector3 _scale)
        {
            _position = _pos;
            _rotation = _rot;
            this._scale = _scale;
        }

        public DisjointTransformPacket(Transform transform)
        {
            _position = transform.position;
            _rotation = transform.rotation;
            _scale = transform.localScale;
        }

        public DisjointTransformPacket(AbstractTransformPacket other)
        {
            _position = other.position;
            _rotation = other.rotation;
            _scale = other.scale;
        }

        public override Vector3 position
        {
            get => _position;
            set => _position = value;
        }

        public override Quaternion rotation
        {
            get => _rotation;
            set => _rotation = value;
        }
            
        public override Vector3 scale
        {
            get => _scale;
            set => _scale = value;
        }
    }
    
}
