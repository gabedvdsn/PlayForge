using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractRuntimeProcess : IProxyTaskBehaviourCaller, IGameplayProcess
    {
        public readonly string name;
        public readonly EProcessStepPriorityMethod priorityMethod;
        public readonly int stepPriority;
        public readonly EProcessStepTiming stepTiming;
        public readonly EProcessLifecycle lifecycle;

        protected ProcessDataPacket regData;
        protected bool processActive;
        
        public ProcessRelay ProcessRelay => Relay;
        public ProcessDataPacket Data => regData;
        
        private bool _initialized;
        public bool IsInitialized => _initialized;
        public ProcessRelay Relay;
        
        public readonly Dictionary<int, ProcessRelay> HandlerRelays = new();

        
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

        #region Readable Definition
        
        public virtual string GetName()
        {
            return Relay.Wrapper.ProcessName;
        }
        public virtual string GetDescription()
        {
            return $"[{Relay.Wrapper.ProcessName}] This is a MonoProcess.";
        }
        public virtual Texture2D GetPrimaryIcon()
        {
            return null;
        }
        
        #endregion
        
        public void SendProcessData(ProcessDataPacket processData) => regData = processData;

        public virtual void WhenInitialize(ProcessRelay relay)
        {
            _initialized = true;
            Relay = relay;
        }

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

        public virtual bool HandlePause(ProcessRelay relay)
        {
            return false;
        }
        
        public virtual bool HandleResume(ProcessRelay relay)
        {
            return false;
        }

        /// <summary>
        /// Called via ProcessControl when the process is set to Terminated
        /// </summary>
        /// <param name="relay">Process Relay</param>
        public virtual void WhenTerminate(ProcessRelay relay)
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

        public string ProcessName => string.IsNullOrEmpty(name) ? "AnonymousClassProcess" : name;
        public virtual EProcessStepPriorityMethod PriorityMethod => priorityMethod;
        public virtual EProcessStepTiming StepTiming => stepTiming;
        public virtual EProcessLifecycle Lifecycle => lifecycle;
        
        public virtual int StepPriority => stepPriority;
        public virtual bool BehaviourIsApplicable(AbstractProxyTaskBehaviour behaviour) => false;
        public async UniTask ApplyBehaviour(AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser user, CancellationToken token)
        {
            await cb.RunAsync(this, user, token);
            cb.End();
        }
        public async UniTask ApplyBehaviour(AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser[] users, CancellationToken token)
        {
            var tasks = users.Select(user => ApplyBehaviour(cb.CreateInstance(), user, token)).ToArray();
            await UniTask.WhenAll(tasks);
        }
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler)
        {
            return (AbstractRuntimeProcess)handler == this;
        }
        public bool HandlerProcessIsSubscribed(ProcessRelay relay)
        {
            return HandlerRelays.ContainsKey(relay.CacheIndex);
        }
        public void HandlerSubscribeProcess(ProcessRelay relay)
        {
            HandlerRelays.Add(relay.CacheIndex, relay);
        }
        public bool HandlerVoidProcess(ProcessRelay relay)
        {
            return HandlerRelays.Remove(relay.CacheIndex);
        }
    }
}
