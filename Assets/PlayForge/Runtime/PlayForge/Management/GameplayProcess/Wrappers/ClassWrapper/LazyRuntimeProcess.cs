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
        
        public override void WhenUpdate()
        {
            
        }
        public override async UniTask RunProcess(CancellationToken token)
        {
            processActive = true;
            // Kept alive tick by tick: in a world of its own, its steps; elsewhere, Unity's frames, as the wait was (FEAT-10)
            while (processActive) await ForgeTick.Next(token);
        }
    }
}
