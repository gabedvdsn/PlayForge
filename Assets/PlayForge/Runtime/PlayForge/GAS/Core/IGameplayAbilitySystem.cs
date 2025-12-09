using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    public interface IGameplayAbilitySystem : ISource, IProxyTaskBehaviourCaller, IValidationReady
    {
        public AttributeSystemComponent GetAttributeSystem();
        public AbilitySystemComponent GetAbilitySystem();
        
        [CanBeNull] public GameplayAbilitySystem ToGASObject();
    }
}
