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
        
        public override void WhenInitialize()
        {
            
        }
        public override void WhenUpdate()
        {
            
        }
        public override async UniTask RunProcess(CancellationToken token)
        {
            processActive = true;
            await UniTask.WaitWhile(() => processActive, cancellationToken: token);
        }
    }
}
