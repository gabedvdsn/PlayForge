using System;

namespace FarEmerald.PlayForge
{
    public class ActionProcess : LazyRuntimeProcess
    {
        private Action action;

        public ActionProcess(Action action)
            : this("Action", EProcessStepPriorityMethod.First, 0, EProcessStepTiming.Update, EProcessLifecycle.SelfTerminating, action)
        {
        }
        
        public ActionProcess(
            string name, EProcessStepPriorityMethod priorityMethod, int stepPriority, 
            EProcessStepTiming stepTiming, EProcessLifecycle lifecycle, Action action) 
            : base(name, priorityMethod, stepPriority, stepTiming, lifecycle)
        {
            this.action = action;
        }

        public override void WhenUpdate()
        {
            action?.Invoke();
        }
    }
    
    public class ActionProcess<T> : LazyRuntimeProcess where T : class
    {
        private Action<T> action;
        private T source;
        
        public ActionProcess(Action<T> action, T source)
            : this("Action", EProcessStepPriorityMethod.First, 0, EProcessStepTiming.Update, EProcessLifecycle.SelfTerminating, action, source)
        {
        }
        
        public ActionProcess(
            string name, EProcessStepPriorityMethod priorityMethod, int stepPriority, 
            EProcessStepTiming stepTiming, EProcessLifecycle lifecycle, Action<T> action, T source)
            : base(name, priorityMethod, stepPriority, stepTiming, lifecycle)
        {
            this.action = action;
            this.source = source;
        }

        public override void WhenUpdate()
        {
            action?.Invoke(source);
        }
    }
}
