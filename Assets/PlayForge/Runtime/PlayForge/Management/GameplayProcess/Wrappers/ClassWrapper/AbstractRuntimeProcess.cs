using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractRuntimeProcess : IProxyTaskBehaviourCaller, IGameplayProcess
    {
        protected readonly string name;
        protected readonly EProcessStepPriorityMethod priorityMethod;
        protected readonly int stepPriority;
        protected readonly EProcessStepTiming stepTiming;
        protected readonly EProcessLifecycle lifecycle;

        protected ProcessDataPacket regData;
        protected bool processActive;
        
        public ProcessRelay ProcessRelay => Relay;
        public ProcessDataPacket Data => regData;
        
        private bool _initialized;
        public bool IsInitialized => _initialized;
        public ProcessRelay Relay;
        
        public readonly Dictionary<int, ProcessRelay> HandlerRelays = new();

        public AbstractMonoProcessInstantiator CustomInstantiator;
        
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
        public virtual Texture2D GetDefaultIcon()
        {
            return null;
        }
        
        #endregion
        
        public void SendProcessData(ProcessDataPacket data, ProcessRelay relay) => regData = data;

        public void WhenInitialize()
        {
            _initialized = true;
        }

        public void WhenUpdate()
        {
        }

        public void WhenFixedUpdate()
        {
        }

        public void WhenLateUpdate()
        {
        }
        
        public void WhenWait()
        {
            processActive = false;
        }

        public virtual bool HandlePause()
        {
            return false;
        }
        
        public bool TryHandleResume()
        {
            return false;
        }
        
        public void WhenDestroy()
        {
            // Does nothing
        }


        public void WhenTerminate()
        {
            processActive = false;
        }
        
        public abstract UniTask RunProcess(CancellationToken token);

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
        public ProcessRelay[] GetRelays()
        {
            return HandlerRelays.Values.ToArray();
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
        public AbstractMonoProcessInstantiator GetInstantiator(AbstractMonoProcess mono)
        {
            return CustomInstantiator;
        }
    }
}
