using System.Collections.Generic;
using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    public interface ITarget : IAffiliated, ITagHandler, IValidationReady, IHasReadableDefinition
    {
        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity);
        //public List<Tag> GetAffiliation();
        public bool ApplyGameplayEffect(GameplayEffectSpec spec);
        public bool RemoveGameplayEffect(GameplayEffect effect);
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect GameplayEffect);
        public IntValuePairClamped GetLevel(Tag key);
        public bool FindLevelSystem(out SystemLevelsComponent lvlSystem);
        public bool FindItemSystem(out ItemSystemComponent itemSystem);
        public bool FindAttributeSystem(out AttributeSystemComponent attrSystem);
        public bool FindAbilitySystem(out AbilitySystemComponent abilSystem);
        public bool TryGetAttributeValue(IAttribute attribute, out AttributeValue value);
        public bool TryModifyAttribute(IAttribute attribute, SourcedModifiedAttributeValue sourcedModifiedValue, bool runEvents = true);
        public SystemComponentData ToGASComponentData() => new SystemComponentData(this);
        public IGameplayAbilitySystem ToGAS() => this is IGameplayAbilitySystem gas ? gas : null;
        public AbstractTargetingPacket GetTargetingPacket();
        public void MarkDead();
        public bool IsDead { get; }
    }
}
