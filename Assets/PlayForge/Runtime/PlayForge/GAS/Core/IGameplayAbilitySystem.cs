using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    public interface IGameplayAbilitySystem : ISource, IProxyTaskBehaviourCaller, IValidationReady
    {
        public AttributeSystemComponent GetAttributeSystem();
        public AbilitySystemComponent GetAbilitySystem();

        public AnalysisWorkerCache GetAnalysisCache();
        
        [CanBeNull] public GameplayAbilitySystem ToGASObject();
        
        public bool IsDead { get; }
        
        public bool TryGetEffectContainer(GameplayEffect effect, out AbstractEffectContainer container);
        public bool TryGetEffectContainers(GameplayEffect effect, out AbstractEffectContainer[] containers);
    }
}
