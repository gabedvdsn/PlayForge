using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Provides external access to ProcessControlBlock data without exposing internal state management.
    /// </summary>
    public class ProcessRelay
    {
        private readonly ProcessControlBlock _pcb;
        private IGameplayProcessHandler handler;
        
        /// <summary>Whether the control that holds the process still does.</summary>
        public bool ProcessActive => _pcb.Control != null && _pcb.Control.IsRegistered(_pcb.CacheIndex);

        /// <summary>
        /// The control that holds the process. The relay's calls go there; they went to ProcessControl.Instance,
        /// whichever control held the process (FEAT-10).
        /// </summary>
        public ProcessControl Control => _pcb.Control;
        
        public ProcessRelay(ProcessControlBlock pcb)
        {
            _pcb = pcb;
        }

        public int CacheIndex => _pcb.CacheIndex;
        public bool IsRunning => State == EProcessState.Running;
        public bool IsWaiting => State == EProcessState.Waiting;
        
        public AbstractProcessWrapper Wrapper => _pcb.Wrapper;
        public IGameplayProcessHandler Handler => _pcb.Handler;
        public EProcessState State => _pcb.State;
        public EProcessState QueuedState => _pcb.QueuedState;
        public bool IsMono => _pcb.isMono;
        public float UnscaledLifetime => _pcb.UnscaledLifetime;
        public float Lifetime => _pcb.Lifetime;
        public float UpdateTime => _pcb.TotalUpdateTime;

        public bool TryGetProcess<T>(out T process)
        {
            return _pcb.Wrapper.TryGetProcess(out process);
        }

        /// <summary>
        /// Completes as the process terminates and is unregistered, in that frame, or at once if it has. Cancelling
        /// <paramref name="token"/> stops the wait, not the process. Polling <see cref="State"/> found the end a frame
        /// later, and each relay waited on that way added a frame (TS-Z).
        /// </summary>
        public UniTask WhenTerminated(CancellationToken token = default)
        {
            var terminated = _pcb.WhenTerminated();
            return token.CanBeCanceled ? terminated.AttachExternalCancellation(token) : terminated;
        }

        /// <summary>
        /// Calculates remaining runtime based on elapsed update time.
        /// </summary>
        /// <param name="runtime">Total runtime in milliseconds</param>
        /// <param name="multiplier">Conversion multiplier (default 1000 for ms)</param>
        /// <param name="unscaled"></param>
        // Converted, then cut to whole units. Cut first, it lost up to a second (PC-25)
        public int RemainingRuntime(int runtime, int multiplier = 1000, bool unscaled = false)
            => runtime - (int)((unscaled ? UnscaledLifetime : Lifetime) * multiplier);
        
        /// <summary>
        /// Calculates remaining runtime based on elapsed update time.
        /// </summary>
        /// <param name="runtime">Total runtime in milliseconds</param>
        /// <param name="unscaled"></param>
        public float RemainingRuntime(float runtime, bool unscaled = false) 
            => runtime - (unscaled ? UnscaledLifetime : Lifetime);

        /// <summary>
        /// Pauses this process and optionally its children, at once. Returns false for a process that's been
        /// told to terminate, or one that's already paused.
        /// </summary>
        public bool Pause(bool cascade = true) 
            => _pcb.Control != null && _pcb.Control.Pause(CacheIndex, cascade);

        /// <summary>
        /// Resumes this process and optionally its children. A process that held its run carries on, and one
        /// that didn't starts a new run. Returns false while ProcessControl itself is paused.
        /// </summary>
        public bool Unpause(bool cascade = true) 
            => _pcb.Control != null && _pcb.Control.Unpause(CacheIndex, cascade);

        /// <summary>
        /// Runs this process, and optionally its children, where it waits, as one that requires control does until
        /// it's told. ProcessControl could run a process by its index, and the relay that held it couldn't (FEAT-15).
        /// Returns false for a process that can't run from where it is.
        /// </summary>
        public bool Run(bool cascade = false)
            => _pcb.Control != null && _pcb.Control.Run(CacheIndex, cascade);

        /// <summary>
        /// Terminates this process and optionally its children.
        /// </summary>
        public bool Terminate(bool cascade = true) 
            => _pcb.Control != null && _pcb.Control.Terminate(CacheIndex, cascade);
        
        /// <summary>
        /// Immediately terminates this process and optionally its children.
        /// </summary>
        public bool TerminateImmediate(bool cascade = true) 
            => _pcb.Control != null && _pcb.Control.TerminateImmediate(CacheIndex, cascade);
    }
}
