using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class LazyRuntimeProcess : AbstractRuntimeProcess
    {
        protected LazyRuntimeProcess()
        {
        }

        public LazyRuntimeProcess(string name, EProcessStepPriorityMethod priorityMethod, int stepPriority, EProcessStepTiming stepTiming, EProcessLifecycle lifecycle) : base(name, priorityMethod, stepPriority, stepTiming, lifecycle)
        {
        }
        
        public override void WhenInitialize(ProcessRelay relay)
        {
            
        }
        public override void WhenUpdate(ProcessRelay relay)
        {
            
        }
        public override async UniTask RunProcess(ProcessRelay relay, CancellationToken token)
        {
            processActive = true;
            await UniTask.WaitWhile(() => processActive, cancellationToken: token);
        }
        
        public override bool IsInitialized()
        {
            return true;
        }
        
        public override UniTask CallBehaviour(Tag cmd, AbstractProxyTaskBehaviour cb, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
        
        public override void RunCompositeBehaviour(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller)
        {
        }
        public override UniTask RunCompositeBehaviourAsync(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller, CancellationToken token)
        {
            return UniTask.CompletedTask;
        }
    }
}
