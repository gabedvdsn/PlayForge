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
        public async UniTask AwaitStageActivation(AbilityProxy proxy, AbilityTaskBehaviourStage stage, UniTask[] tasks, AbilityDataPacket data, AbilitySystemCallbacks callbacks,
            CancellationTokenSource stageCts)
        {
            var watched = proxy.GetWatchedTasks(tasks, stage, data, callbacks, false);
            await UniTask.WhenAny(watched);
            stageCts.Cancel();
        }
    }
    
    public class AllProxyStagePolicy : IAbilityProxyStagePolicy
    {
        public async UniTask AwaitStageActivation(AbilityProxy proxy, AbilityTaskBehaviourStage stage, UniTask[] tasks, AbilityDataPacket data, AbilitySystemCallbacks callbacks,
            CancellationTokenSource stageCts)
        {
            var watched = proxy.GetWatchedTasks(tasks, stage, data, callbacks, true);
            await UniTask.WhenAll(watched);
        }
    }
}
