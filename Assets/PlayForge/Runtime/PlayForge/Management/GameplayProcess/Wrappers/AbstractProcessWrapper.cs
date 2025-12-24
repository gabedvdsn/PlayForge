using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractProcessWrapper
    {
        public ProcessRelay Relay;
        
        /// <summary>
        /// Initializing the process wrapper, as opposed to the process itself
        /// </summary>
        public abstract void InitializeWrapper();
        
        /// <summary>
        /// Initializing the process, as opposed to the process wrapper
        /// 
        /// </summary>
        /// <param name="relay"></param>
        public abstract void WhenInitialize(ProcessRelay relay);
        
        public virtual void WhenUpdate(ProcessRelay relay)
        {
            
        }
        public virtual void WhenFixedUpdate(ProcessRelay relay)
        {
            
        }
        public virtual void WhenLateUpdate(ProcessRelay relay)
        {
            
        }
        
        public abstract void WhenWait(ProcessRelay relay);
        public abstract void WhenTerminate(ProcessRelay relay);
        public abstract void WhenTerminateSafe(ProcessRelay relay);
        
        public abstract UniTask RunProcess(ProcessRelay relay, CancellationToken token);
        public abstract bool TryGetProcess<T>(out T process);
        
        public abstract bool IsInitialized();
        public abstract string ProcessName { get; }
        public abstract EProcessStepPriorityMethod PriorityMethod { get; }
        public abstract int StepPriority { get; }
        public abstract EProcessStepTiming StepTiming { get; }
        public abstract EProcessLifecycle Lifecycle { get; }
        
        public override string ToString()
        {
            return "AbstractProcessWrapper";
        }
    }
}
