using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of every MonoBehaviour process: a component, on a prefab or a scene object, that ProcessControl
    /// registers, steps and ends, as a GAS hub or a projectile. Its lifecycle, step timing and step priority are fields
    /// of its inspector, Synchronous in Update and LateUpdate at the first priority by default, and as it initializes
    /// it takes the parent, position and rotation its packet gives. ProcessControl registers those already in a scene
    /// as it starts.
    ///
    /// <para>Derive from it, or from <c>LazyMonoProcess</c> for a run that lasts until it's paused or ended, and
    /// override <c>RunProcess</c>, the run ProcessControl awaits, and the hooks you need: <c>WhenInitialize</c>;
    /// <c>WhenUpdate</c>, <c>WhenLateUpdate</c> and <c>WhenFixedUpdate</c> at its timing; <c>WhenWait</c>,
    /// <c>WhenTerminate</c> and <c>WhenDestroy</c>; and <c>TryHandlePause</c> and <c>TryHandleResume</c> to keep a run
    /// through a pause. An override of <c>WhenInitialize</c> or <c>WhenTerminate</c> calls the base's, and
    /// <c>WhenDestroy</c> stands in for <c>OnDestroy</c>, which a class of its own would hide.</para>
    ///
    /// <para>Its object comes from its registration's instantiator, its handler's or its own <c>Instantiator</c>, or is
    /// cloned from its prefab, and is given back or destroyed as the process ends. Processes on its child objects pause
    /// and end with it, as ProcessControl cascades by default.</para>
    /// </summary>
    public abstract class AbstractMonoProcess : MonoBehaviour, IProxyTaskBehaviourCaller, IGameplayProcess
    {
        [Header("Mono Gameplay Process")] 
        
        [HideInInspector] public EProcessLifecycle ProcessLifecycle = EProcessLifecycle.Synchronous;
        [HideInInspector] public EProcessStepTiming ProcessTiming = EProcessStepTiming.UpdateAndLate;
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

        // As its process ends: its object runs no more, and a pooled one can run again
        internal void MarkEnded() => _initialized = false;
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
        
        public void SendProcessData(ProcessDataPacket processData, ProcessRelay relay)
        {
            regData = processData;
            Relay = relay;
        }

        /// <summary>
        /// Should always be called by
        /// </summary>
        public virtual void WhenInitialize()
        {
            _initialized = true;
            
            // Transform
            if (regData.TryGetFirst<Transform>(Tags.TRANSFORM, out var pt))
            {
                transform.SetParent(pt);
            }
            
            // Position
            if (regData.TryGetFirst<Vector3>(Tags.POSITION, out var pos))
            {
                transform.position = pos;
            }
            else if (regData.TryGetFirst<IProcessOwner>(Tags.POSITION, out var ownerPos))
            {
                transform.position = ownerPos.ProcessPosition;
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
            else if (regData.TryGetFirst<IProcessOwner>(Tags.ROTATION, out var ownerRot))
            {
                transform.rotation = ownerRot.ProcessRotation;
            }
            else if (regData.TryGetFirst<Transform>(Tags.ROTATION, out var tRot))
            {
                transform.rotation = tRot.rotation;
            }
            
        }
        
        public virtual void WhenUpdate() { }

        public virtual void WhenLateUpdate() { }
        
        public virtual void WhenFixedUpdate() { }
        
        public virtual void WhenWait()
        {
            processActive = false;
        }
        
        public virtual void WhenTerminate()
        {
            processActive = false;

            // What this process handles ends with it, each where it's held. Through the scene's ProcessControl by its
            // cache index, one in a world of its own ran on, and the scene's process under that index ended
            foreach (var _relay in HandlerRelays.Values.ToArray())
            {
                _relay.TerminateImmediate();
            }
        }
        
        public virtual void WhenTerminateQueued()
        {
            processActive = false;
        }

        public abstract UniTask RunProcess(CancellationToken token);
        public virtual bool TryHandlePause()
        {
            return false;
        }
        public virtual bool TryHandleResume()
        {
            return false;
        }
        public AbstractMonoProcessInstantiator GetInstantiator(AbstractMonoProcess mono)
        {
            return Instantiator;
        }

        public virtual void WhenDestroy()
        {
            
        }

        private void OnDestroy()
        {
            // Destroyed from outside, with its scene, or by a process sharing the object, its block would stay
            // registered with nothing behind it. After a normal end the block is already gone
            if (Relay != null && ProcessControl.Instance) ProcessControl.Instance.EndDestroyedProcess(Relay);

            WhenDestroy();
        }

        // A later SetParent files the process again under its new ancestors, as a cascade follows them
        private void OnTransformParentChanged()
        {
            if (Relay != null && ProcessControl.Instance) ProcessControl.Instance.ProcessReparented(Relay);
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
    }
}
