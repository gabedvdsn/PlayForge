using System;

namespace FarEmerald.PlayForge
{
    public class ActionProcess : LazyRuntimeProcess
    {
        private Action action;

        public ActionProcess(Action action)
        {
            this.action = action;
        }
        
        public ActionProcess(
            string name, EProcessStepPriorityMethod priorityMethod, int stepPriority, 
            EProcessStepTiming stepTiming, EProcessLifecycle lifecycle, Action action) 
            : base(name, priorityMethod, stepPriority, stepTiming, lifecycle)
        {
            this.action = action;
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            action?.Invoke();
        }
    }
    
    public class ActionProcess<T> : LazyRuntimeProcess where T : class
    {
        private Action<T> action;
        private T source;
        
        public ActionProcess(Action<T> action, T source)
        {
            this.action = action;
            this.source = source;
        }
        
        public ActionProcess(
            string name, EProcessStepPriorityMethod priorityMethod, int stepPriority, 
            EProcessStepTiming stepTiming, EProcessLifecycle lifecycle, Action<T> action) 
            : base(name, priorityMethod, stepPriority, stepTiming, lifecycle)
        {
            this.action = action;
        }

        public override void WhenUpdate(ProcessRelay relay)
        {
            action?.Invoke(source);
        }
    }
}
