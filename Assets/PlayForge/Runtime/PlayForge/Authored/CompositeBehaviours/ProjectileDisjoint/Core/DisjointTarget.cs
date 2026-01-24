using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Modifications applied to disjoint targets are
    /// </summary>
    public class DisjointTarget : ITarget
    {
        private ITarget original;
        private PlaceholderTransformPacket packet;

        public DisjointTarget(ITarget original, PlaceholderTransformPacket packet)
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
        public void CompileGrantedTags()
        {
            
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
                new PlaceholderTransformPacket(_target.AsTransform())
            );
        }
    }

}
