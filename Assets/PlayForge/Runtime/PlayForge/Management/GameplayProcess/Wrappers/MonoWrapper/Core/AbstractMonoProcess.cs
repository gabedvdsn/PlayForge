using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractMonoProcess : MonoBehaviour, IProxyTaskBehaviourCaller
    {
        [Header("Mono Gameplay Process")] 
        
        [HideInInspector] public EProcessLifecycle ProcessLifecycle;
        [HideInInspector] public EProcessStepTiming ProcessTiming;
        [HideInInspector] public EProcessStepPriorityMethod PriorityMethod = EProcessStepPriorityMethod.First;
        [HideInInspector] public int ProcessStepPriority;
        
        [Tooltip("Uses Object.Instantiate and Object.Destroy when null")]
        [HideInInspector] [SerializeReference] 
        public AbstractMonoProcessInstantiator Instantiator;
        
        protected ProcessDataPacket regData;
        protected bool processActive;

        private bool _initialized;
        public bool IsInitialized => _initialized;
        public ProcessRelay Relay;
        
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
                transform.position = gasPos.AsTransform().position;
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
                transform.rotation = gasRot.AsTransform().rotation;
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
