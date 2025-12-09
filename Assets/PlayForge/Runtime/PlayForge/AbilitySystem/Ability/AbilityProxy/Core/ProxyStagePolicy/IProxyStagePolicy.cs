using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public interface IProxyStagePolicy
    {
        public UniTask AwaitStageActivation(AbilityProxy proxy, AbilityTaskBehaviourStage stage, UniTask[] tasks, AbilityDataPacket data, AbilitySystemCallbacks callbacks,
            CancellationTokenSource stageCts);
        
    }

    public class AnyProxyStagePolicy : IProxyStagePolicy
    {
        public async UniTask AwaitStageActivation(AbilityProxy proxy, AbilityTaskBehaviourStage stage, UniTask[] tasks, AbilityDataPacket data, AbilitySystemCallbacks callbacks,
            CancellationTokenSource stageCts)
        {
            var watched = proxy.GetWatchedTasks(tasks, stage, data, callbacks, false);
            await UniTask.WhenAny(watched);
            stageCts.Cancel();
        }
    }
    
    public class AllProxyStagePolicy : IProxyStagePolicy
    {
        public async UniTask AwaitStageActivation(AbilityProxy proxy, AbilityTaskBehaviourStage stage, UniTask[] tasks, AbilityDataPacket data, AbilitySystemCallbacks callbacks,
            CancellationTokenSource stageCts)
        {
            var watched = proxy.GetWatchedTasks(tasks, stage, data, callbacks, true);
            await UniTask.WhenAll(watched);
        }
    }
}
