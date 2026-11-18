using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A runtime process whose run lasts until it's paused or ended: its <c>RunProcess</c> waits a tick at a time while
    /// it's active, so what it does goes in its steps and hooks, <c>WhenUpdate</c> and the rest. The base of most
    /// runtime processes, a group, a runtime GAS and a task sequence's process among them; its constructor takes a
    /// name, a step priority, a timing and a lifecycle.
    /// </summary>
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
            // Kept alive tick by tick: in a world of its own, its steps; elsewhere, Unity's frames, as the wait was
            while (processActive) await ForgeTick.Next(token);
        }
    }
}
