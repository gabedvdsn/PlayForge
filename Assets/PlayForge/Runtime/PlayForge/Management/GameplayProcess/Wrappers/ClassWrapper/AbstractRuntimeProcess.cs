using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractRuntimeProcess : IProxyTaskBehaviourCaller
    {
        protected bool processActive;

        public readonly string name;
        public readonly EProcessStepPriorityMethod priorityMethod;
        public readonly int stepPriority;
        public readonly EProcessStepTiming stepTiming;
        public readonly EProcessLifecycle lifecycle;

        protected ProcessDataPacket regData;

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

        public void SendProcessData(ProcessDataPacket processData) => regData = processData;

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
        public abstract void RunCompositeBehaviour(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller);
        public abstract UniTask RunCompositeBehaviourAsync(Tag command, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourCaller caller, CancellationToken token);
        public abstract UniTask CallBehaviour(Tag cmd, AbstractProxyTaskBehaviour cb, CancellationToken token);
        public async UniTask CallBehaviour(Tag cmd, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser user, CancellationToken token)
        {
            var run = cb.RunAsync(token);
            await user.RunCompositeBehaviourAsync(cmd, cb, this, token);
            await run;
            cb.End();
        }
        public async UniTask CallBehaviour(Tag cmd, AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser[] users, CancellationToken token)
        {
            var tasks = users.Select(user => CallBehaviour(cmd, cb.CreateInstance(), user, token)).ToArray();
            await UniTask.WhenAll(tasks);
        }
    }
}
