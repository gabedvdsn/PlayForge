using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    public interface IGameplayAbilitySystem : ISource, ILocalDataUser, IAnalysisUser, IProxyTaskBehaviourCaller
    {
        public AttributeSystemComponent GetAttributeSystem();
        public AbilitySystemComponent GetAbilitySystem();
        public ItemSystemComponent GetItemSystem();
        
        [CanBeNull] public GameplayAbilitySystem ToGASObject();

        public bool RemoveGameplayEffect(Tag identifier);
        
        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container);
        public bool TryGetEffectContainers(GameplayEffect effect, out AbstractEffectContainer[] containers);

        public GameplayAbilitySystemCallbacks GetCallbacks();

        /// <summary>
        /// Record an impact this GAS DEALT (this GAS is ISource).
        /// </summary>
        public void RecordFrameImpactDealt(ImpactData impact);

        /// <summary>
        /// Record an impact this GAS RECEIVED (this GAS is ITarget).
        /// </summary>
        public void RecordFrameImpactReceived(ImpactData impact);
    }

}
