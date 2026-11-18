using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A runtime process that calls an action in each of its Update steps, until it's ended. Made with the action
    /// alone, it's named Action, SelfTerminating, and stepped in Update at the first priority; the longer constructor
    /// gives its name, priority, timing and lifecycle.
    /// </summary>
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
    
    /// <summary>
    /// An <c>ActionProcess</c> whose action is handed the same object, its source, each time it's called.
    /// </summary>
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
