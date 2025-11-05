namespace FarEmerald.PlayForge
{
    public interface  ITarget
    {
        public void CommunicateTargetedIntent(AbstractGameplayMonoProcess entity);
        public Tag GetAffiliation();
        public Tag[] GetAppliedTags();
        public bool ApplyGameplayEffect(GameplayEffectSpec spec);
        public GameplayEffectSpec GenerateEffectSpec(IEffectOrigin origin, GameplayEffect GameplayEffect);
        public bool FindAttributeSystem(out AttributeSystemComponent attrSystem);
        public bool FindAbilitySystem(out AbilitySystemComponent abilSystem);
        public SystemComponentData AsData()
        {
            return new SystemComponentData(this);
        }
        public GASComponent AsGAS() => this is GASComponent gas ? gas : null;
        public AbstractTransformPacket AsTransform();
    }
}
