using System.Collections.Generic;
using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    public interface ITarget : IAffiliated, ITagHandler, IValidationReady, IHasReadableDefinition
    {
        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity);

        /// <summary>Takes back <see cref="CommunicateTargetedIntent"/>, for a process no longer aimed here.</summary>
        public void WithdrawTargetedIntent(AbstractGameplayMonoProcess entity) { }
        //public List<Tag> GetAffiliation();
        public bool ApplyGameplayEffect(GameplayEffectSpec spec);

        /// <summary>
        /// Apply any <see cref="ICarrierApplication"/> to this target. Default implementation
        /// dispatches through <see cref="ApplyGameplayEffect"/> when the carrier is a
        /// <see cref="GameplayEffectSpec"/> — preserving every existing call site during the
        /// Origin/Carrier/Impacter cutover. Concrete <see cref="ITarget"/> implementations
        /// can override later to handle other carrier kinds natively.
        /// </summary>
        public bool ApplyCarrier(ICarrierApplication carrier)
            => carrier is GameplayEffectSpec spec && ApplyGameplayEffect(spec);

        public bool RemoveGameplayEffect(GameplayEffect effect);

        /// <summary>
        /// Removes the effect where this origin applied it, and leaves the same effect from anywhere else.
        /// </summary>
        public bool RemoveGameplayEffect(GameplayEffect effect, IEffectOrigin origin) => RemoveGameplayEffect(effect);

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
