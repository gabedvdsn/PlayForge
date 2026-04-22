using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Callbacks fired by <see cref="ProcessControl"/> for high-level observers
    /// (debuggers, recorders, GAS-aware observers, etc.).
    ///
    /// These are in addition to the per-process ProcessWatcher system — watchers
    /// subscribe to a specific ProcessRelay, whereas callbacks fire for every
    /// process managed by ProcessControl.
    /// </summary>
    public class ProcessControlCallbacks
    {
        // ═══════════════════════════════════════════════════════════════════════
        // PROCESS LIFECYCLE
        // ═══════════════════════════════════════════════════════════════════════

        public delegate void ProcessDelegate(ProcessRelay relay, ProcessDataPacket data);

        /// <summary>
        /// Fired immediately after a process is registered and added to _active.
        /// </summary>
        private ProcessDelegate _onProcessRegistered;
        public event ProcessDelegate OnProcessRegistered
        {
            add => _onProcessRegistered += value;
            remove => _onProcessRegistered -= value;
        }

        public void ProcessRegistered(ProcessRelay relay, ProcessDataPacket data)
            => _onProcessRegistered?.Invoke(relay, data);

        /// <summary>
        /// Fired immediately before a process is removed from _active.
        /// </summary>
        private ProcessDelegate _onProcessUnregistered;
        public event ProcessDelegate OnProcessUnregistered
        {
            add => _onProcessUnregistered += value;
            remove => _onProcessUnregistered -= value;
        }

        public void ProcessUnregistered(ProcessRelay relay, ProcessDataPacket data)
            => _onProcessUnregistered?.Invoke(relay, data);

        // ═══════════════════════════════════════════════════════════════════════
        // STATE TRANSITIONS
        // ═══════════════════════════════════════════════════════════════════════

        public delegate void ProcessStateDelegate(
            ProcessRelay relay,
            EProcessState oldState,
            EProcessState newState,
            ProcessDataPacket data);

        /// <summary>
        /// Fired when a process transitions between states.
        /// </summary>
        private ProcessStateDelegate _onProcessStateChanged;
        public event ProcessStateDelegate OnProcessStateChanged
        {
            add => _onProcessStateChanged += value;
            remove => _onProcessStateChanged -= value;
        }

        public void ProcessStateChanged(
            ProcessRelay relay,
            EProcessState oldState,
            EProcessState newState,
            ProcessDataPacket data)
            => _onProcessStateChanged?.Invoke(relay, oldState, newState, data);

        // ═══════════════════════════════════════════════════════════════════════
        // GAS-SPECIFIC LIFECYCLE
        // Fires only when the registering handler is an IGameplayAbilitySystem.
        // ═══════════════════════════════════════════════════════════════════════

        public delegate void GASProcessDelegate(IGameplayAbilitySystem gas, ProcessRelay relay, ProcessDataPacket data);

        private GASProcessDelegate _onGASProcessRegistered;
        public event GASProcessDelegate OnGASProcessRegistered
        {
            add => _onGASProcessRegistered += value;
            remove => _onGASProcessRegistered -= value;
        }

        public void GASProcessRegistered(IGameplayAbilitySystem gas, ProcessRelay relay, ProcessDataPacket data)
            => _onGASProcessRegistered?.Invoke(gas, relay, data);

        private GASProcessDelegate _onGASProcessUnregistered;
        public event GASProcessDelegate OnGASProcessUnregistered
        {
            add => _onGASProcessUnregistered += value;
            remove => _onGASProcessUnregistered -= value;
        }

        public void GASProcessUnregistered(IGameplayAbilitySystem gas, ProcessRelay relay, ProcessDataPacket data)
            => _onGASProcessUnregistered?.Invoke(gas, relay, data);

        // ═══════════════════════════════════════════════════════════════════════
        // CONTROL STATE
        // ═══════════════════════════════════════════════════════════════════════

        public delegate void ControlStateDelegate(EProcessControlState oldState, EProcessControlState newState);

        private ControlStateDelegate _onControlStateChanged;
        public event ControlStateDelegate OnControlStateChanged
        {
            add => _onControlStateChanged += value;
            remove => _onControlStateChanged -= value;
        }

        public void ControlStateChanged(EProcessControlState oldState, EProcessControlState newState)
            => _onControlStateChanged?.Invoke(oldState, newState);
    }
}
