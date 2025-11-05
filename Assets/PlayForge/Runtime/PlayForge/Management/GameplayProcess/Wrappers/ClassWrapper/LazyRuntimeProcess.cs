using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public class LazyRuntimeProcess : AbstractRuntimeProcess
    {
        public LazyRuntimeProcess()
        {
        }

        public LazyRuntimeProcess(string name, EProcessStepPriorityMethod priorityMethod, int stepPriority, EProcessStepTiming stepTiming, EProcessLifecycle lifecycle) : base(name, priorityMethod, stepPriority, stepTiming, lifecycle)
        {
        }
        
        public override void WhenInitialize(ProcessRelay relay)
        {
            
        }
        public override void WhenUpdate(EProcessStepTiming timing, ProcessRelay relay)
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
        
    }
}
