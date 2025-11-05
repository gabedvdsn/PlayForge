using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractRuntimeProcess
    {
        protected bool processActive;

        public string name;
        public EProcessStepPriorityMethod priorityMethod;
        public int stepPriority;
        public EProcessStepTiming stepTiming;
        public EProcessLifecycle lifecycle;

        protected AbstractRuntimeProcess()
        {
            name = $"Anon-{GetType()}";
            priorityMethod = EProcessStepPriorityMethod.First;
            stepPriority = 0;
            stepTiming = EProcessStepTiming.None;
            lifecycle = EProcessLifecycle.SelfTerminating;
        }

        protected AbstractRuntimeProcess(string name, EProcessStepPriorityMethod priorityMethod, int stepPriority, EProcessStepTiming stepTiming, EProcessLifecycle lifecycle)
        {
            this.name = name;
            this.priorityMethod = priorityMethod;
            this.stepPriority = stepPriority;
            this.stepTiming = stepTiming;
            this.lifecycle = lifecycle;
        }

        public abstract void WhenInitialize(ProcessRelay relay);

        /// <summary>
        /// Called via Step in ProcessControl as determined by the process's StepUpdateTiming
        /// </summary>
        /// <param name="timing">Step timing</param>
        /// <param name="relay">Process Relay</param>
        public abstract void WhenUpdate(EProcessStepTiming timing, ProcessRelay relay);
        
        /// <summary>
        /// Called via ProcessControl when the process is set to Waiting
        /// </summary>
        /// <param name="relay">Process Relay</param>
        public virtual void WhenWait(ProcessRelay relay)
        {
            processActive = false;
        }

        /// <summary>
        /// Called via ProcessControl when the process is set to Terminated
        /// </summary>
        /// <param name="relay">Process Relay</param>
        public virtual void WhenTerminate(ProcessRelay relay)
        {
            processActive = false;
        }
        
        public void WhenTerminateSafe(ProcessRelay relay)
        {
            processActive = false;
        }

        /// <summary>
        /// Called via ProcessControl when the process is set to Running
        /// </summary>
        /// <param name="relay">Process Relay</param>
        /// <param name="token">Cancellation token</param>
        /// <returns></returns>
        public abstract UniTask RunProcess(ProcessRelay relay, CancellationToken token);

        public bool TryGetProcess<T>(out T process)
        {
            if (this is T cast)
            {
                process = cast;
                return true;
            }

            process = default;
            return false;
        }
        public abstract bool IsInitialized();

        public string ProcessName => string.IsNullOrEmpty(name) ? "AnonymousClassProcess" : name;
        public virtual EProcessStepPriorityMethod PriorityMethod => priorityMethod;
        public virtual EProcessStepTiming StepTiming => stepTiming;
        public virtual EProcessLifecycle Lifecycle => lifecycle;
        
        public virtual int StepPriority => stepPriority;
    }
}
