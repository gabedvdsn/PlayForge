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
        private StaticTargetingPacket packet;

        public DisjointTarget(ITarget original, StaticTargetingPacket packet)
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
        public int GetTagWeight(Tag _tag)
        {
            return 0;
        }
        public bool QueryTags(TagQuery query)
        {
            return false;
        }
        public void CompileGrantedTags()
        {
            
        }
        public bool ApplyGameplayEffect(GameplayEffectSpec spec)
        {
            return false;
        }
        public bool RemoveGameplayEffect(GameplayEffect effect)
        {
            return false;
        }
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect GameplayEffect)
        {
            return original.GenerateEffectSpec(origin, GameplayEffect);
        }
        public bool FindLevelSystem(out SystemLevelsComponent lvlSystem)
        {
            return original.FindLevelSystem(out lvlSystem);
        }
        public bool FindItemSystem(out ItemSystemComponent itemSystem)
        {
            return original.FindItemSystem(out itemSystem);
        }
        public bool FindAttributeSystem(out AttributeSystemComponent attrSystem)
        {
            return original.FindAttributeSystem(out attrSystem);
        }
        public bool FindAbilitySystem(out AbilitySystemComponent abilSystem)
        {
            return original.FindAbilitySystem(out abilSystem);
        }
        public bool TryGetAttributeValue(IAttribute attribute, out AttributeValue value)
        {
            value = default;
            return false;
        }
        public bool TryModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true)
        {
            return false;
        }
        public AbstractTargetingPacket GetTargetingPacket()
        {
            return packet;
        }
        public void MarkDead()
        {
            // Does nothing
        }
        public bool IsDead => true;
        public static DisjointTarget Generate(ITarget _target)
        {
            return new DisjointTarget(
                _target,
                new StaticTargetingPacket(_target.GetTargetingPacket())
            );
        }
    }

}
