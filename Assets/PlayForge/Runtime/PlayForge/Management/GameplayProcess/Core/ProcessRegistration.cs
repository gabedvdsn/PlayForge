namespace FarEmerald.PlayForge
{
    /// <summary>Why ProcessControl refused a registration, or None when it took the process.</summary>
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

        /// <summary>The process's WhenInitialize threw, so nothing of it stays.</summary>
        InitializeThrew,
    }

    /// <summary>
    /// What a registration did: the relay of the process ProcessControl took, or why it refused it. It reads as true when
    /// the process was taken. A refused call returned false and a null relay, and said nothing of why.
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
    /// carries, the ProcessControl that takes it, and, for a MonoBehaviour process, what makes its object.
    /// <see cref="ProcessControl.Register(AbstractRuntimeProcess)"/> and its
    /// siblings start one, and <see cref="Run()"/> registers the process and starts it, as registering always has:
    /// <code>
    /// var result = ProcessControl.Register(projectile).HandledBy(gas).With(data).Run();
    /// if (!result) Debug.Log(result.Refusal);
    /// </code>
    /// What isn't given takes its default. The handler follows the packet: one with a source, as an ability's is, hands
    /// the process to its source (<see cref="ProcessDataPacket.Source"/>), and any other to ProcessControl's default
    /// handler, the GameRoot where there is one, or to none in a world of its own, which reaches for nothing of the
    /// scene's. ProcessControl had 28 static Register overloads, and the argument's static type picked the handler, so
    /// an ability's packet passed as a SequenceDataPacket filed its sequence under GameRoot, and the sequence didn't end
    /// with its entity.
    /// </summary>
    public readonly struct ProcessRegistration
    {
        // A mono or runtime process, a task sequence or a chain of them
        private readonly object process;
        private readonly IGameplayProcessHandler handler;
        private readonly bool handlerGiven;
        private readonly ProcessDataPacket data;
        private readonly ProcessWorld world;
        private readonly AbstractMonoProcessInstantiator instantiator;

        internal ProcessRegistration(object process) : this(process, null, false, null, null, null) { }

        private ProcessRegistration(object process, IGameplayProcessHandler handler, bool handlerGiven, ProcessDataPacket data,
            ProcessWorld world, AbstractMonoProcessInstantiator instantiator)
        {
            this.process = process;
            this.handler = handler;
            this.handlerGiven = handlerGiven;
            this.data = data;
            this.world = world;
            this.instantiator = instantiator;
        }

        /// <summary>
        /// What handles the process: what it belongs to, which ends it as it ends. A hub ends its abilities' processes as
        /// it goes. Null, it has no handler at all.
        /// </summary>
        public ProcessRegistration HandledBy(IGameplayProcessHandler processHandler) =>
            new(process, processHandler, true, data, world, instantiator);

        /// <summary>
        /// The packet the process carries. A sequence's is a <see cref="SequenceDataPacket"/>. Left out, a MonoBehaviour
        /// process takes the scene root's, which parents it under <see cref="ProcessControl.DefaultParent"/>, the
        /// GameRoot where there is one, a runtime process the default, and a sequence the scene root's sequence packet.
        /// In a world of its own, each takes the default, parented nowhere.
        /// </summary>
        public ProcessRegistration With(ProcessDataPacket packet) => new(process, handler, handlerGiven, packet, world, instantiator);

        /// <summary>The ProcessControl that takes the process. Left out, <see cref="ProcessControl.Instance"/> does.</summary>
        public ProcessRegistration In(ProcessControl processControl) => In(processControl != null ? processControl.World : null);

        /// <summary>The world that takes the process, a world of its own among them. Left out, the scene's does.</summary>
        public ProcessRegistration In(ProcessWorld processWorld) => new(process, handler, handlerGiven, data, processWorld, instantiator);

        /// <summary>
        /// What makes a MonoBehaviour process's object for this registration and takes it back as the process ends, ahead
        /// of its handler's and the object's own, as a pooled cue's pool. Left out, theirs does, or the process is cloned
        /// and destroyed.
        /// </summary>
        public ProcessRegistration InstantiatedBy(AbstractMonoProcessInstantiator madeBy) =>
            new(process, handler, handlerGiven, data, world, madeBy);

        /// <summary>Registers the process and starts it, or says why it can't.</summary>
        public ProcessRegistrationResult Run()
        {
            if (process == null) return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.NoProcess);
            var taker = world ?? (ProcessControl.Instance != null ? ProcessControl.Instance.World : null);
            if (taker == null) return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.NoProcessControl);

            // A world of its own reaches for nothing of the scene's. GameRoot took what it held with no handler given, and
            // kept it for as long as the scene lasted, its world gone or not, and the scene root's packet needed a
            // GameRoot, so with none a sequence's registration threw
            bool ownWorld = taker.Control is null;
            var by = handlerGiven ? handler : DefaultHandler(data, ownWorld);
            switch (process)
            {
                case AbstractMonoProcess mono:
                    return taker.RegisterMono(mono, by, data ?? (ownWorld ? ProcessDataPacket.Default() : ProcessDataPacket.SceneRoot()),
                        instantiator);
                case AbstractRuntimeProcess runtime:
                    return taker.RegisterRuntime(runtime, by, data ?? ProcessDataPacket.Default());
                case TaskSequence sequence:
                    return RegisterSequence(taker, new TaskSequenceProcess(sequence), by, ownWorld);
                case TaskSequenceChain chain:
                    return RegisterSequence(taker, new TaskSequenceProcess(chain), by, ownWorld);
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

        private ProcessRegistrationResult RegisterSequence(ProcessWorld taker, TaskSequenceProcess sequence, IGameplayProcessHandler by,
            bool ownWorld)
        {
            if (data != null && data is not SequenceDataPacket)
                return new ProcessRegistrationResult(null, EProcessRegistrationRefusal.NotASequencePacket);
            return taker.RegisterRuntime(sequence, by,
                (SequenceDataPacket)data ?? (ownWorld ? SequenceDataPacket.Default() : SequenceDataPacket.SceneRoot()));
        }

        // A packet with a source hands its process to it, as an ability's does to its caster; any other packet, or none,
        // to ProcessControl's default handler, the GameRoot where there is one, or in a world of its own to no handler,
        // as its owner ends what it holds
        private static IGameplayProcessHandler DefaultHandler(ProcessDataPacket packet, bool ownWorld)
        {
            if (packet?.Source is { } source) return source;
            return ownWorld ? null : ProcessControl.DefaultHandler;
        }
    }
}
