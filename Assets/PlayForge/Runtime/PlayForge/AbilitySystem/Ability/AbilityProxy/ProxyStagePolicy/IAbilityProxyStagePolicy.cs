using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public interface IAbilityProxyStagePolicy
    {
        public UniTask AwaitStageActivation(AbilityProxy proxy, AbilityTaskBehaviourStage stage, UniTask[] tasks, AbilityDataPacket data, AbilitySystemCallbacks callbacks,
            CancellationTokenSource stageCts);
        
    }

    public class AnyProxyStagePolicy : IAbilityProxyStagePolicy
    {
        public UniTask AwaitStageActivation(AbilityProxy proxy, AbilityTaskBehaviourStage stage, UniTask[] tasks, AbilityDataPacket data, AbilitySystemCallbacks callbacks,
            CancellationTokenSource stageCts)
        {
            return UniTask.CompletedTask;
        }
    }
    
    public class AllProxyStagePolicy : IAbilityProxyStagePolicy
    {
        public UniTask AwaitStageActivation(AbilityProxy proxy, AbilityTaskBehaviourStage stage, UniTask[] tasks, AbilityDataPacket data, AbilitySystemCallbacks callbacks,
            CancellationTokenSource stageCts)
        {
            return UniTask.CompletedTask;
        }
    }
}
