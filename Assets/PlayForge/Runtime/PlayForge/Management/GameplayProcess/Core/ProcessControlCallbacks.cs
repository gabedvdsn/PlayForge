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

        /// <summary>
        /// A handler of <c>OnProcessRegistered</c> and <c>OnProcessUnregistered</c>, ProcessControl's callbacks,
        /// invoked for every process once it has registered and initialized, and just before it's removed, and handed
        /// its relay and its data packet.
        /// </summary>
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

        /// <summary>
        /// A handler of <c>OnProcessStateChanged</c>, ProcessControl's callback, invoked as any process moves from one
        /// state to another, and handed its relay, the state it left, the state it entered and its data packet.
        /// </summary>
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
        // OWNED PROCESSES
        // Fires only for a process with an owner, an IProcessOwner.
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A handler of <c>OnOwnedProcessRegistered</c> and <c>OnOwnedProcessUnregistered</c>, ProcessControl's callbacks,
        /// invoked for a process with an owner, one registered with the owner as its handler or with a packet whose source
        /// is the owner, as an ability's packet's is its caster, as it registers and just before it's removed, and handed
        /// the owner, the process's relay and its data packet.
        /// </summary>
        public delegate void OwnedProcessDelegate(IProcessOwner owner, ProcessRelay relay, ProcessDataPacket data);

        private OwnedProcessDelegate _onOwnedProcessRegistered;
        public event OwnedProcessDelegate OnOwnedProcessRegistered
        {
            add => _onOwnedProcessRegistered += value;
            remove => _onOwnedProcessRegistered -= value;
        }

        public void OwnedProcessRegistered(IProcessOwner owner, ProcessRelay relay, ProcessDataPacket data)
            => _onOwnedProcessRegistered?.Invoke(owner, relay, data);

        private OwnedProcessDelegate _onOwnedProcessUnregistered;
        public event OwnedProcessDelegate OnOwnedProcessUnregistered
        {
            add => _onOwnedProcessUnregistered += value;
            remove => _onOwnedProcessUnregistered -= value;
        }

        public void OwnedProcessUnregistered(IProcessOwner owner, ProcessRelay relay, ProcessDataPacket data)
            => _onOwnedProcessUnregistered?.Invoke(owner, relay, data);

        // ═══════════════════════════════════════════════════════════════════════
        // CONTROL STATE
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A handler of <c>OnControlStateChanged</c>, ProcessControl's callback, invoked as ProcessControl's own state
        /// changes, and handed the state it left and the state it entered.
        /// </summary>
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
