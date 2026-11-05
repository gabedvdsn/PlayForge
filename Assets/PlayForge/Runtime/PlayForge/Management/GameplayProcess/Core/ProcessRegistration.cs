namespace FarEmerald.PlayForge
{
    /// <summary>Why ProcessControl refused a registration, or None when it took the process (FEAT-15).</summary>
    public enum EProcessRegistrationRefusal
    {
        /// <summary>ProcessControl took the process.</summary>
        None,

        /// <summary>There was no process to register.</summary>
        NoProcess,

        /// <summary>No ProcessControl was there to take it, as when no scene has one.</summary>
        NoProcessControl,

        /// <summary>The process is registered already, or was once.</summary>
        AlreadyRegistered,

        /// <summary>ProcessControl is closed or terminated, so it takes no new processes.</summary>
        ProcessControlClosed,

        /// <summary>A sequence's packet is a <see cref="SequenceDataPacket"/>, and this one isn't.</summary>
        NotASequencePacket,

        /// <summary>The process's WhenInitialize threw, so nothing of it stays (PC-15).</summary>
        InitializeThrew,
    }

    /// <summary>
    /// What a registration did: the relay of the process ProcessControl took, or why it refused it. It reads as true when
    /// the process was taken. A refused call returned false and a null relay, and said nothing of why (FEAT-15).
    /// </summary>
    public readonly struct ProcessRegistrationResult
    {
        /// <summary>The registered process's relay, or null when the registration was refused.</summary>
        public readonly ProcessRelay Relay;

        /// <summary>Why ProcessControl refused the process, or None.</summary>
        public readonly EProcessRegistrationRefusal Refusal;

        public ProcessRegistrationResult(ProcessRelay relay, EProcessRegistrationRefusal refusal)
        {
            Relay = refusal == EProcessRegistrationRefusal.None ? relay : null;
            Refusal = refusal;
        }

        /// <summary>Whether ProcessControl took the process.</summary>
        public bool Registered => Refusal == EProcessRegistrationRefusal.None;

        public static implicit operator bool(ProcessRegistrationResult result) => result.Registered;

        public override string ToString() => Registered ? $"Registered as {Relay.CacheIndex}" : $"Refused: {Refusal}";
    }

    /// <summary>
    /// A process's registration with ProcessControl, built a step at a time: what handles the process, the packet it
    /// carries, and the ProcessControl that takes it. <see cref="ProcessControl.Register(AbstractRuntimeProcess)"/> and its
    /// siblings start one, and <see cref="Run()"/> registers the process and starts it, as registering always has:
    /// <code>
    /// var result = ProcessControl.Register(projectile).HandledBy(gas).With(data).Run();
    /// if (!result) Debug.Log(result.Refusal);
    /// </code>
    /// What isn't given takes its default. The handler follows the packet's run-time type: an ability's packet hands the
    /// process to its source, and any other to GameRoot. ProcessControl had 28 static Register overloads, and the
    /// argument's static type picked the handler, so an ability's packet passed as a SequenceDataPacket filed its
    /// sequence under GameRoot, and the sequence didn't end with its entity (FEAT-15, GAS-52). Those overloads are
    /// wrappers of this now, marked obsolete, and go before the first Asset Store submission (Decision 30).
    /// </summary>
    public readonly struct ProcessRegistration
    {
        // A mono or runtime process, a task sequence or a chain of them
        private readonly object process;
        private readonly IGameplayProcessHandler handler;
        private readonly bool handlerGiven;
        private readonly ProcessDataPacket data;
        private readonly ProcessControl control;

        internal ProcessRegistration(object process) : this(process, null, false, null, null) { }

        private ProcessRegistration(object process, IGameplayProcessHandler handler, bool handlerGiven, ProcessDataPacket data,
            ProcessControl control)
        {
            this.process = process;
            this.handler = handler;
            this.handlerGiven = handlerGiven;
            this.data = data;
            this.control = control;
        }

        /// <summary>
        /// What handles the process: what it belongs to, which ends it as it ends. A hub ends its abilities' processes as
        /// it goes (GAS-52). Null, it has no handler at all.
        /// </summary>
        public ProcessRegistration HandledBy(IGameplayProcessHandler processHandler) =>
            new(process, processHandler, true, data, control);

        /// <summary>
        /// The packet the process carries. A sequence's is a <see cref="SequenceDataPacket"/>. Left out, a MonoBehaviour
        /// process takes the scene root's, which parents it under GameRoot, a runtime process the default, and a
        /// sequence the scene root's sequence packet.
        /// </summary>
        public ProcessRegistration With(ProcessDataPacket packet) => new(process, handler, handlerGiven, packet, control);

        /// <summary>The ProcessControl that takes the process. Left out, <see cref="ProcessControl.Instance"/> does.</summary>
        public ProcessRegistration In(ProcessControl processControl) => new(process, handler, handlerGiven, data, processControl);

        /// <summary>Registers the process and starts it, or says why it can't.</summary>
        public ProcessRegistrationResult Run()
        {
            if (process == null) return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.NoProcess);
            var taker = control != null ? control : ProcessControl.Instance;
            if (taker == null) return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.NoProcessControl);

            var by = handlerGiven ? handler : DefaultHandler(data);
            switch (process)
            {
                case AbstractMonoProcess mono:
                    return taker.RegisterMono(mono, by, data ?? ProcessDataPacket.SceneRoot());
                case AbstractRuntimeProcess runtime:
                    return taker.RegisterRuntime(runtime, by, data ?? ProcessDataPacket.Default());
                case TaskSequence sequence:
                    return RegisterSequence(taker, new TaskSequenceProcess(sequence), by);
                case TaskSequenceChain chain:
                    return RegisterSequence(taker, new TaskSequenceProcess(chain), by);
                default:
                    return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.NoProcess);
            }
        }

        /// <summary>Registers the process and starts it, as <see cref="Run()"/> does, with its relay apart.</summary>
        public bool Run(out ProcessRelay relay)
        {
            var result = Run();
            relay = result.Relay;
            return result.Registered;
        }

        private ProcessRegistrationResult RegisterSequence(ProcessControl taker, TaskSequenceProcess sequence, IGameplayProcessHandler by)
        {
            if (data != null && data is not SequenceDataPacket)
                return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.NotASequencePacket);
            return taker.RegisterRuntime(sequence, by, (SequenceDataPacket)data ?? SequenceDataPacket.SceneRoot());
        }

        // An ability's packet hands its process to the packet's source; any other packet, or none, to GameRoot
        private static IGameplayProcessHandler DefaultHandler(ProcessDataPacket packet)
        {
            if (packet is AbilityDataPacket ability && ability.EffectOrigin?.GetSource() is { } source) return source;
            return GameRoot.Instance;
        }
    }
}
