using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractMonoProcess : MonoBehaviour, IProxyTaskBehaviourCaller, IGameplayProcess
    {
        [Header("Mono Gameplay Process")] 
        
        [HideInInspector] public EProcessLifecycle ProcessLifecycle;
        [HideInInspector] public EProcessStepTiming ProcessTiming;
        [HideInInspector] public EProcessStepPriorityMethod PriorityMethod = EProcessStepPriorityMethod.First;
        [HideInInspector] public int ProcessStepPriority;
        
        [Tooltip("Uses Object.Instantiate and Object.Destroy when null")]
        [HideInInspector] [SerializeReference]
        public AbstractMonoProcessInstantiator Instantiator;
        public bool UseHandlerInstantiator;

        [Tooltip("When false, this process won't be affected by sibling pause/terminate cascade")]
        [HideInInspector]
        public bool ParticipateInSiblingCascade = true;
        
        protected ProcessDataPacket regData;
        protected bool processActive;

        public ProcessDataPacket Data => regData;
        public ProcessRelay ProcessRelay => Relay;

        private bool _initialized;
        public bool IsInitialized => _initialized;
        protected ProcessRelay Relay;

        private readonly Dictionary<int, ProcessRelay> HandlerRelays = new();
        
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
        
        public void SendProcessData(ProcessDataPacket processData)
        {
            regData = processData;
        }

        /// <summary>
        /// Called via ProcessControl after the process is Created
        /// </summary>
        /// <param name="relay">Process Relay</param>
        public virtual void WhenInitialize(ProcessRelay relay)
        {
            _initialized = true;
            Relay = relay;
            
            // Transform
            if (regData.TryGetFirst<Transform>(Tags.PARENT_TRANSFORM, out var pt))
            {
                transform.SetParent(pt);
            }
            
            // Position
            if (regData.TryGetFirst<Vector3>(Tags.POSITION, out var pos))
            {
                transform.position = pos;
            }
            else if (regData.TryGetFirst<IGameplayAbilitySystem>(Tags.POSITION, out var gasPos))
            {
                transform.position = gasPos.GetTargetingPacket().position;
            }
            else if (regData.TryGetFirst<Transform>(Tags.POSITION, out var tPos))
            {
                transform.position = tPos.position;
            }
            
            // Rotation
            if (regData.TryGetFirst<Quaternion>(Tags.ROTATION, out var rot))
            {
                transform.rotation = rot;
            }
            else if (regData.TryGetFirst<IGameplayAbilitySystem>(Tags.ROTATION, out var gasRot))
            {
                transform.rotation = gasRot.GetTargetingPacket().rotation;
            }
            else if (regData.TryGetFirst<Transform>(Tags.ROTATION, out var tRot))
            {
                transform.rotation = tRot.rotation;
            }
            
        }
        
        /// <summary>
        /// Called via Step in ProcessControl as determined by the process's StepUpdateTiming
        /// </summary>
        /// <param name="relay">Process Relay</param>
        public abstract void WhenUpdate(ProcessRelay relay);

        public virtual void WhenLateUpdate(ProcessRelay relay) { }
        
        public virtual void WhenFixedUpdate(ProcessRelay relay) { }
        
        /// <summary>
        /// Called via ProcessControl when the process is set to Waiting
        /// </summary>
        /// <param name="relay">Process Relay</param>
        public virtual void WhenWait(ProcessRelay relay)
        {
            processActive = false;
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
            
            foreach (var _relay in HandlerRelays.Values.ToArray())
            {
                ProcessControl.Instance.TerminateImmediate(_relay.CacheIndex);
            }
        }
        
        /// <summary>
        /// Called via ProcessControl when the process is set to Running
        /// </summary>
        /// <param name="relay">Process Relay</param>
        /// <param name="token">Cancellation token</param>
        /// <returns></returns>
        public abstract UniTask RunProcess(ProcessRelay relay, CancellationToken token);

        protected virtual void WhenDestroy()
        {
            
        }

        private void OnDestroy()
        {
            WhenDestroy();
        }

        public override string ToString()
        {
            return name;
        }

        public virtual bool BehaviourIsApplicable(AbstractProxyTaskBehaviour behaviour) => true;
        
        public async UniTask ApplyBehaviour(AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser user, CancellationToken token)
        {
            await cb.RunAsync(this, user, token);
            cb.End();
        }
        public async UniTask ApplyBehaviour(AbstractProxyTaskBehaviour cb, IProxyTaskBehaviourUser[] users, CancellationToken token)
        {
            var tasks = users
                .Select(user => ApplyBehaviour(cb.CreateInstance(), user, token))
                .ToArray();
            await UniTask.WhenAll(tasks);
        }
        public ProcessRelay[] GetRelays()
        {
            return HandlerRelays.Values.ToArray();
        }
        public bool HandlerValidateAgainst(IGameplayProcessHandler handler)
        {
            return (AbstractMonoProcess)handler == this;
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
            return Instantiator;
        }
    }
}
