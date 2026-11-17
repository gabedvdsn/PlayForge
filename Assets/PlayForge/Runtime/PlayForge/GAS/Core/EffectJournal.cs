using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>What an effect journal's entry tells.</summary>
    public enum EEffectJournalKind
    {
        /// <summary>The effect landed on the entity.</summary>
        Applied,

        /// <summary>It didn't, and why.</summary>
        Refused,

        /// <summary>Its ticks executed, as many as it says.</summary>
        Ticked,

        /// <summary>A change it made to one of the entity's attributes.</summary>
        Impact,

        /// <summary>It came off.</summary>
        Removed,

        /// <summary>One of its workers, or one of its dealer's impact workers, heard one of its moments.</summary>
        Worker,
    }

    /// <summary>Where an action a worker queued stands.</summary>
    public enum EJournalActionState
    {
        /// <summary>Queued, and not run yet.</summary>
        Queued,

        /// <summary>Its queue ran it.</summary>
        Ran,

        /// <summary>
        /// Its queue dropped it unrun: no longer valid as its turn came, taken off the queue, or its entity had ended.
        /// </summary>
        Dropped,

        /// <summary>Its queue tried to run it, and it threw.</summary>
        Failed,
    }

    /// <summary>An action a worker queued, as a journal keeps it: what it said it was, and whether it ran.</summary>
    public sealed class EffectJournalAction
    {
        internal EffectJournalAction(IRootAction action)
        {
            Action = action;
            Description = action?.Description ?? "";
        }

        public IRootAction Action { get; }

        /// <summary>What it said it was as it was queued.</summary>
        public string Description { get; }

        public EJournalActionState State { get; internal set; }

        public override string ToString() => State == EJournalActionState.Queued ? Description : $"{Description} ({State.ToString().ToLowerInvariant()})";
    }

    /// <summary>
    /// One entry of an effect journal: when, of what kind, on whom, the effect and who applied it, and what the kind
    /// says: why it was refused, how many ticks, the change it made, or which worker ran, at which of its effect's moments,
    /// on whose side, with the actions it queued.
    /// </summary>
    public sealed class EffectJournalEntry
    {
        private static readonly IReadOnlyList<EffectJournalAction> NoActions = Array.Empty<EffectJournalAction>();

        public EEffectJournalKind Kind { get; internal set; }

        /// <summary>The game's time as it was written, in seconds.</summary>
        public float Time { get; internal set; }

        /// <summary>The frame it was written in.</summary>
        public int Frame { get; internal set; }

        /// <summary>The entity it happened on: the one the effect is on, or for a worker on the dealer's side, the dealer.</summary>
        public IGameplayAbilitySystem Entity { get; internal set; }

        /// <summary>The effect; null for an impact worker's run on a change no effect made.</summary>
        public GameplayEffect Effect { get; internal set; }

        /// <summary>Its spec, the one application of it, where there was one: a removal names the effect alone.</summary>
        public GameplayEffectSpec Spec { get; internal set; }

        /// <summary>Who applied it, or dealt the hit.</summary>
        public ISource Source { get; internal set; }

        /// <summary>For a refusal, why.</summary>
        public EEffectRefusal Refusal { get; internal set; }

        /// <summary>For a tick, how many ticks executed.</summary>
        public int Ticks { get; internal set; }

        /// <summary>For an impact, the attribute it changed.</summary>
        public IAttribute Attribute { get; internal set; }

        /// <summary>For an impact, what it changed the attribute by.</summary>
        public AttributeValue Change { get; internal set; }

        /// <summary>For an impact, its run through its pipeline, in a line; empty where none ran.</summary>
        public string Breakdown { get; internal set; } = "";

        /// <summary>For an application, its first impact's number read as it landed, part by part.</summary>
        public NumberTrace Trace { get; internal set; }

        /// <summary>For a worker's run, the worker: an effect worker, or the dealer's impact worker.</summary>
        public object Worker { get; internal set; }

        /// <summary>For a worker's run, its kind in a word or two.</summary>
        public string WorkerName { get; internal set; } = "";

        /// <summary>For a worker's run, the moment of its effect it heard.</summary>
        public EEffectWorkerHook Hook { get; internal set; }

        /// <summary>For a worker's run, whose side it ran on.</summary>
        public EWorkerSide Side { get; internal set; }

        /// <summary>For a worker's run, the actions it queued, each with where it stands.</summary>
        public IReadOnlyList<EffectJournalAction> Actions { get; internal set; } = NoActions;

        /// <summary>
        /// The entry in a line: "Burn landed on Target, from Caster", "Poison refused by Target: immunity", "Burn ticked
        /// on Target", "Burn: -5 Health to Target", "Burn came off Target", "Burn's Noting heard it tick on Target,
        /// queuing Ticked".
        /// </summary>
        public override string ToString()
        {
            string effect = Effect != null ? CombatLogEntry.Name(Effect) : "A hit";
            string on = CombatLogEntry.Name(Entity);
            string from = Source != null && !ReferenceEquals(Source, Entity) ? $", from {CombatLogEntry.Name(Source)}" : "";
            switch (Kind)
            {
                case EEffectJournalKind.Applied: return $"{effect} landed on {on}{from}";
                case EEffectJournalKind.Refused: return $"{effect} refused by {on}: {Refused(Refusal)}";
                case EEffectJournalKind.Ticked: return Ticks == 1 ? $"{effect} ticked on {on}" : $"{effect} ticked {Ticks} times on {on}";
                case EEffectJournalKind.Impact:
                    return $"{effect}: {(Change.CurrentValue == 0f && Change.BaseValue != 0f ? Signed(Change.BaseValue) + " (base)" : Signed(Change.CurrentValue))} " +
                           $"{Attribute?.GetName()} to {on}";
                case EEffectJournalKind.Removed: return $"{effect} came off {on}";
                default:
                    string queued = Actions.Count == 0 ? "" : $", queuing {string.Join(", ", Actions)}";
                    string side = Side == EWorkerSide.Dealer ? $" (its dealer, {on})" : $" on {on}";
                    return $"{effect}'s {WorkerName} heard {Heard(Hook)}{side}{queued}";
            }
        }

        private static string Refused(EEffectRefusal refusal) => refusal switch
        {
            EEffectRefusal.Dead => "dead",
            EEffectRefusal.Affiliation => "not its affiliation",
            EEffectRefusal.Requirements => "a requirement didn't hold",
            EEffectRefusal.Immunity => "immunity",
            EEffectRefusal.Chance => "its chance missed",
            EEffectRefusal.Stopped => "blocked or evaded",
            _ => "applied inside its own application",
        };

        private static string Heard(EEffectWorkerHook hook) => hook switch
        {
            EEffectWorkerHook.Application => "it land",
            EEffectWorkerHook.Tick => "it tick",
            EEffectWorkerHook.Removal => "it come off",
            _ => "it hit",
        };

        private static string Signed(float value) => value.ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// An effect journal: each application, refusal (by requirement, immunity or chance), tick, impact and removal
    /// of an effect on the entities it listens to, with its number's trace as it lands, and each worker that ran for it, by
    /// its hook and its side, with the actions it queued and whether they ran. It keeps its last <see cref="Keep"/>
    /// entries, oldest first. It records only while it listens, and an entity no journal listens to pays nothing: the
    /// Creator Lab's bottom panel and the Debugger's Timeline read one.
    /// </summary>
    public sealed class EffectJournal : IDisposable
    {
        /// <summary>How many entries a journal keeps unless it's told.</summary>
        public const int DefaultKeep = 500;

        private readonly List<EffectJournalEntry> entries = new();
        private readonly Dictionary<IGameplayAbilitySystem, Hearing> heard = new();
        private readonly Dictionary<IRootAction, EffectJournalAction> waiting = new();
        private readonly List<IGameplayAbilitySystem> gone = new();
        private int keep;

        public EffectJournal(int keep = DefaultKeep) => Keep = keep;

        /// <summary>How many entries it keeps, its last; at least one.</summary>
        public int Keep
        {
            get => keep;
            set
            {
                keep = Math.Max(1, value);
                Trim();
            }
        }

        /// <summary>Its entries, oldest first.</summary>
        public IReadOnlyList<EffectJournalEntry> Entries => entries;

        /// <summary>How many entries it has written, those it no longer keeps among them.</summary>
        public int Written { get; private set; }

        /// <summary>What it has heard, so a view knows when to read it again: an entry written, or an action run or dropped.</summary>
        public int Version { get; private set; }

        /// <summary>Raised as it writes each entry, for a stop to watch for.</summary>
        public event Action<EffectJournalEntry> Wrote;

        /// <summary>The entities it listens to.</summary>
        public IEnumerable<IGameplayAbilitySystem> Listened => heard.Keys;

        /// <summary>Whether it listens to an entity.</summary>
        public bool Listens(IGameplayAbilitySystem entity) => entity != null && heard.ContainsKey(entity);

        /// <summary>Its entries of an effect, oldest first.</summary>
        public IEnumerable<EffectJournalEntry> Of(GameplayEffect effect)
        {
            foreach (var entry in entries)
                if (effect != null && entry.Effect == effect) yield return entry;
        }

        /// <summary>Its entries on an entity, oldest first.</summary>
        public IEnumerable<EffectJournalEntry> On(IGameplayAbilitySystem entity)
        {
            foreach (var entry in entries)
                if (entity != null && ReferenceEquals(entry.Entity, entity)) yield return entry;
        }

        /// <summary>Lets every entry go, and goes on listening.</summary>
        public void Clear()
        {
            entries.Clear();
            waiting.Clear();
            Version++;
        }

        /// <summary>Listens to these entities and no others: each new one from now on, and each gone no more.</summary>
        public void Follow(IReadOnlyList<IGameplayAbilitySystem> entities)
        {
            foreach (var entity in entities) Listen(entity);
            gone.Clear();
            foreach (var entity in heard.Keys)
            {
                bool listed = false;
                for (int i = 0; i < entities.Count && !listed; i++) listed = ReferenceEquals(entities[i], entity);
                if (!listed) gone.Add(entity);
            }
            foreach (var entity in gone) Forget(entity);
        }

        /// <summary>Listens to an entity: its effects, their refusals and their workers, and the actions it runs.</summary>
        public void Listen(IGameplayAbilitySystem entity)
        {
            if (entity == null || heard.ContainsKey(entity)) return;
            var callbacks = entity.GetCallbacks();
            if (callbacks == null) return;
            var hearing = new Hearing(this, entity, callbacks);
            heard[entity] = hearing;
            hearing.Start();
        }

        /// <summary>Stops listening to an entity. What it wrote of it stays.</summary>
        public void Forget(IGameplayAbilitySystem entity)
        {
            if (entity == null || !heard.Remove(entity, out var hearing)) return;
            hearing.Stop();
        }

        /// <summary>Stops listening to every entity. What it wrote stays.</summary>
        public void Dispose()
        {
            gone.Clear();
            gone.AddRange(heard.Keys);
            foreach (var entity in gone) Forget(entity);
            waiting.Clear();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Writing
        // ═══════════════════════════════════════════════════════════════════════════

        private EffectJournalEntry Entry(EEffectJournalKind kind, IGameplayAbilitySystem entity, GameplayEffect effect,
            GameplayEffectSpec spec, ISource source) => new()
        {
            Kind = kind, Time = ForgeTime.Time, Frame = ForgeTime.FrameCount, Entity = entity, Effect = effect, Spec = spec,
            Source = source,
        };

        private void Write(EffectJournalEntry entry)
        {
            entries.Add(entry);
            Written++;
            Version++;
            Trim();
            Wrote?.Invoke(entry);
        }

        // Its oldest go once it holds more than it keeps, and an action of theirs is waited on no more
        private void Trim()
        {
            int over = entries.Count - keep;
            if (over <= 0) return;
            for (int i = 0; i < over; i++)
                foreach (var action in entries[i].Actions)
                    if (action.Action != null) waiting.Remove(action.Action);
            entries.RemoveRange(0, over);
        }

        private IReadOnlyList<EffectJournalAction> Waited(IReadOnlyList<IRootAction> queued)
        {
            if (queued == null || queued.Count == 0) return Array.Empty<EffectJournalAction>();
            var actions = new EffectJournalAction[queued.Count];
            for (int i = 0; i < queued.Count; i++)
            {
                actions[i] = new EffectJournalAction(queued[i]);
                if (queued[i] != null) waiting[queued[i]] = actions[i];
            }
            return actions;
        }

        private void Settled(IRootAction action, EJournalActionState state)
        {
            if (action == null || !waiting.Remove(action, out var kept)) return;
            kept.State = state;
            Version++;
        }

        // What it hears of one entity, kept to stop hearing it
        private sealed class Hearing
        {
            private readonly EffectJournal journal;
            private readonly IGameplayAbilitySystem entity;
            private readonly GameplayAbilitySystemCallbacks callbacks;
            private readonly GameplayAbilitySystemCallbacks.EffectDelegate applied;
            private readonly GameplayAbilitySystemCallbacks.EffectRefusedDelegate refused;
            private readonly GameplayAbilitySystemCallbacks.EffectTickDelegate ticked;
            private readonly GameplayAbilitySystemCallbacks.ImpactDelegate impact;
            private readonly GameplayAbilitySystemCallbacks.EffectRemovedDelegate removed;
            private readonly GameplayAbilitySystemCallbacks.EffectWorkerDelegate effectWorker;
            private readonly GameplayAbilitySystemCallbacks.ImpactWorkerDelegate impactWorker;
            private readonly GameplayAbilitySystemCallbacks.ActionQueuedDelegate ran, dropped;
            private readonly GameplayAbilitySystemCallbacks.ActionFailedDelegate failed;

            public Hearing(EffectJournal journal, IGameplayAbilitySystem entity, GameplayAbilitySystemCallbacks callbacks)
            {
                this.journal = journal;
                this.entity = entity;
                this.callbacks = callbacks;
                applied = Applied;
                refused = Refused;
                ticked = Ticked;
                impact = Impacted;
                removed = Removed;
                effectWorker = EffectWorkerRan;
                impactWorker = ImpactWorkerRan;
                ran = action => journal.Settled(action, EJournalActionState.Ran);
                dropped = action => journal.Settled(action, EJournalActionState.Dropped);
                failed = (action, _) => journal.Settled(action, EJournalActionState.Failed);
            }

            public void Start()
            {
                callbacks.OnEffectApplied += applied;
                callbacks.OnEffectRefused += refused;
                callbacks.OnEffectTick += ticked;
                callbacks.OnImpactReceived += impact;
                callbacks.OnEffectRemoved += removed;
                callbacks.OnEffectWorkerRan += effectWorker;
                callbacks.OnImpactWorkerRan += impactWorker;
                callbacks.OnActionExecuted += ran;
                callbacks.OnActionInvalidated += dropped;
                callbacks.OnActionFailed += failed;
            }

            public void Stop()
            {
                callbacks.OnEffectApplied -= applied;
                callbacks.OnEffectRefused -= refused;
                callbacks.OnEffectTick -= ticked;
                callbacks.OnImpactReceived -= impact;
                callbacks.OnEffectRemoved -= removed;
                callbacks.OnEffectWorkerRan -= effectWorker;
                callbacks.OnImpactWorkerRan -= impactWorker;
                callbacks.OnActionExecuted -= ran;
                callbacks.OnActionInvalidated -= dropped;
                callbacks.OnActionFailed -= failed;
            }

            private void Applied(GameplayEffectSpec spec)
            {
                if (spec?.Base == null) return;
                var entry = journal.Entry(EEffectJournalKind.Applied, entity, spec.Base, spec, spec.Source);
                // Its first impact's number, read as it landed, part by part
                var impactSpec = spec.Base.ImpactSpecification;
                if (impactSpec?.AttributeTarget != null && impactSpec.MagnitudeOperation != null)
                    entry.Trace = NumberTrace.Of(impactSpec.MagnitudeOperation, NumberContext.Of(spec), spec.Base);
                journal.Write(entry);
            }

            private void Refused(GameplayEffectSpec spec, EEffectRefusal why)
            {
                if (spec?.Base == null) return;
                var entry = journal.Entry(EEffectJournalKind.Refused, entity, spec.Base, spec, spec.Source);
                entry.Refusal = why;
                journal.Write(entry);
            }

            private void Ticked(GameplayEffectSpec spec, int ticks)
            {
                if (spec?.Base == null) return;
                var entry = journal.Entry(EEffectJournalKind.Ticked, entity, spec.Base, spec, spec.Source);
                entry.Ticks = ticks;
                journal.Write(entry);
            }

            // A change an effect made: another carrier's is the combat log's, not an effect's journal
            private void Impacted(ImpactData data)
            {
                if (data.SourcedModifier.Carrier is not GameplayEffectSpec spec || spec.Base == null) return;
                var entry = journal.Entry(EEffectJournalKind.Impact, entity, spec.Base, spec, data.SourcedModifier.Source);
                entry.Attribute = data.Attribute;
                entry.Change = data.RealImpact;
                if (spec.Breakdown is { Steps: { Count: > 0 } } breakdown) entry.Breakdown = breakdown.ToString();
                journal.Write(entry);
            }

            private void Removed(GameplayEffect effect)
            {
                if (effect == null) return;
                journal.Write(journal.Entry(EEffectJournalKind.Removed, entity, effect, null, null));
            }

            private void EffectWorkerRan(in EffectWorkerRun run)
            {
                if (run.Spec?.Base == null) return;
                var entry = journal.Entry(EEffectJournalKind.Worker, entity, run.Spec.Base, run.Spec, run.Spec.Source);
                entry.Worker = run.Worker;
                entry.WorkerName = ForgeWorkerNames.Of(run.Worker);
                entry.Hook = run.Hook;
                entry.Side = run.Side;
                entry.Actions = journal.Waited(run.Queued);
                journal.Write(entry);
            }

            private void ImpactWorkerRan(in ImpactWorkerRun run)
            {
                var spec = run.Impact.SourcedModifier.Carrier as GameplayEffectSpec;
                var entry = journal.Entry(EEffectJournalKind.Worker, entity, spec?.Base, spec, run.Impact.SourcedModifier.Source);
                entry.Worker = run.Worker;
                entry.WorkerName = ForgeWorkerNames.Of(run.Worker);
                entry.Hook = EEffectWorkerHook.Impact;
                entry.Side = EWorkerSide.Dealer;
                entry.Attribute = run.Impact.Attribute;
                entry.Change = run.Impact.RealImpact;
                entry.Actions = journal.Waited(run.Queued);
                journal.Write(entry);
            }
        }
    }
}
