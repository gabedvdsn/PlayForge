namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Provides external access to ProcessControlBlock data without exposing internal state management.
    /// </summary>
    public class ProcessRelay
    {
        private readonly ProcessControlBlock _pcb;
        public bool ProcessActive => ProcessControl.Instance.IsRegistered(_pcb.CacheIndex);
        
        public ProcessRelay(ProcessControlBlock pcb)
        {
            _pcb = pcb;
        }

        public int CacheIndex => _pcb.CacheIndex;
        public AbstractProcessWrapper Wrapper => _pcb.Process;
        public IGameplayProcessHandler Handler => _pcb.Handler;
        public EProcessState State => _pcb.State;
        public EProcessState QueuedState => _pcb.QueuedState;
        public float UnscaledLifetime => _pcb.UnscaledLifetime;
        public float Lifetime => _pcb.Lifetime;
        public float UpdateTime => _pcb.TotalUpdateTime;

        public bool TryGetProcess<T>(out T process)
        {
            return _pcb.Process.TryGetProcess(out process);
        }

        /// <summary>
        /// Calculates remaining runtime based on elapsed update time.
        /// </summary>
        /// <param name="runtime">Total runtime in milliseconds</param>
        /// <param name="multiplier">Conversion multiplier (default 1000 for ms)</param>
        /// <param name="unscaled"></param>
        public int RemainingRuntime(int runtime, int multiplier = 1000, bool unscaled = true) 
            => runtime - (unscaled ? (int)UnscaledLifetime : (int)Lifetime) * multiplier;
        
        /// <summary>
        /// Calculates remaining runtime based on elapsed update time.
        /// </summary>
        /// <param name="runtime">Total runtime in milliseconds</param>
        /// <param name="unscaled"></param>
        public float RemainingRuntime(float runtime, bool unscaled = true) 
            => runtime - (unscaled ? UnscaledLifetime : Lifetime);

        /// <summary>
        /// Pauses this process and optionally its children.
        /// </summary>
        public bool Pause(bool cascade = true) 
            => ProcessControl.Instance.Pause(CacheIndex, cascade);

        /// <summary>
        /// Resumes this process and optionally its children.
        /// </summary>
        public bool Unpause(bool cascade = true) 
            => ProcessControl.Instance.Unpause(CacheIndex, cascade);

        /// <summary>
        /// Terminates this process and optionally its children.
        /// </summary>
        public bool Terminate(bool cascade = true) 
            => ProcessControl.Instance.Terminate(CacheIndex, cascade);
        
        /// <summary>
        /// Immediately terminates this process and optionally its children.
        /// </summary>
        public bool TerminateImmediate(bool cascade = true) 
            => ProcessControl.Instance.TerminateImmediate(CacheIndex, cascade);
    }
}
