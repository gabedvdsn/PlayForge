using System.Collections.Generic;
using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    public interface ITarget : ITagHandler, IValidationReady
    {
        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity);
        public List<Tag> GetAffiliation();
        public bool ApplyGameplayEffect(GameplayEffectSpec spec);
        public bool RemoveGameplayEffect(GameplayEffect effect);
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect GameplayEffect);
        public bool FindLevelSystem(out SystemLevelsComponent lvlSystem);
        public bool FindItemSystem(out ItemSystemComponent itemSystem);
        public bool FindAttributeSystem(out AttributeSystemComponent attrSystem);
        public bool FindAbilitySystem(out AbilitySystemComponent abilSystem);
        public bool TryGetAttributeValue(IAttribute attribute, out AttributeValue value);
        public bool TryModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true);
        public SystemComponentData AsData() => new SystemComponentData(this);
        public IGameplayAbilitySystem AsGAS() => this is IGameplayAbilitySystem gas ? gas : null;
        public AbstractTransformPacket AsTransform();
        public void MarkDead();
        public bool IsDead { get; }
    }
}
