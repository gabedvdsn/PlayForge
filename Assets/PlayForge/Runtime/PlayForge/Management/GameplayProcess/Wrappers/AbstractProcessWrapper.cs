using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractProcessWrapper
    {
        public ProcessRelay Relay;
        
        public abstract void InitializeWrapper();
        public abstract void WhenInitialize(ProcessRelay relay);
        public abstract void WhenUpdate(EProcessStepTiming timing, ProcessRelay relay);
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
