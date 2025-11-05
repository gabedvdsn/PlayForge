using System;

namespace FarEmerald.PlayForge
{
    public class MethodProcess : LazyRuntimeProcess
    {
        private Action action;

        public MethodProcess(Action action)
        {
            this.action = action;
        }
        
        public MethodProcess(string name, EProcessStepPriorityMethod priorityMethod, int stepPriority, EProcessStepTiming stepTiming, EProcessLifecycle lifecycle, Action action) : base(name, priorityMethod, stepPriority, stepTiming, lifecycle)
        {
            this.action = action;
        }

        public override void WhenUpdate(EProcessStepTiming timing, ProcessRelay relay)
        {
            action?.Invoke();
        }
    }
}
