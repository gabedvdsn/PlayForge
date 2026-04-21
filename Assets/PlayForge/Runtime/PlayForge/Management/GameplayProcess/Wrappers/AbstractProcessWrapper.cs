using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractProcessWrapper
    {
        public ProcessRelay Relay;
        public ProcessDataPacket Data;
        public IGameplayProcessHandler Handler;

        public string getProcessName = null;
        protected Func<EProcessStatus> getStatus = () => EProcessStatus.Unknown;
        public EProcessStatus Status => getStatus?.Invoke() ?? EProcessStatus.Unknown;
        
        /// <summary>
        /// When false, this process is excluded from sibling cascade operations.
        /// It will NOT be paused/terminated when a sibling process is paused/terminated.
        /// Parent-to-child cascade is unaffected by this flag.
        /// </summary>
        public bool ParticipateInSiblingCascade = true;

        protected AbstractProcessWrapper(ProcessDataPacket data, IGameplayProcessHandler handler)
        {
            Data = data;
            Handler = handler;
        }

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

        public abstract void WhenUpdate(ProcessRelay relay);
        public abstract void WhenFixedUpdate(ProcessRelay relay);
        public abstract void WhenLateUpdate(ProcessRelay relay);
        
        public abstract void WhenWait(ProcessRelay relay);
        public abstract bool TryHandlePause(ProcessRelay relay);
        public abstract bool TryHandleResume(ProcessRelay relay);
        public abstract void WhenTerminate(ProcessRelay relay);
        public abstract void WhenTerminateSafe(ProcessRelay relay);
        
        public abstract UniTask RunProcess(ProcessRelay relay, CancellationToken token);
        public abstract bool TryGetProcess<T>(out T process);
        
        public abstract bool IsInitialized();
        public virtual string ProcessName => getProcessName ?? "[Unknown Process]";
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
