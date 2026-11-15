using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FarEmerald.PlayForge
{
    /// <summary>Why an effect didn't land on an entity, as the entity's callbacks tell it (LAB-12).</summary>
    public enum EEffectRefusal
    {
        /// <summary>The entity is dead, and the effect isn't its own.</summary>
        Dead,

        /// <summary>Its affiliation policy doesn't take the entity, as an effect for foes on a friend.</summary>
        Affiliation,

        /// <summary>Its application requirements, the target's or the source's, don't hold.</summary>
        Requirements,

        /// <summary>An immunity on the entity names it (FEAT-8).</summary>
        Immunity,

        /// <summary>Its chance to apply missed (FEAT-8).</summary>
        Chance,

        /// <summary>A Block or Evade stage stopped it as it landed (TW-12).</summary>
        Stopped,

        /// <summary>It was asked for again inside its own application, as an effect that contains itself is (GAS-28).</summary>
        Cycle,
    }

    /// <summary>The moment of its effect an effect worker heard (LAB-12).</summary>
    public enum EEffectWorkerHook
    {
        Application,
        Tick,
        Removal,
        Impact,
    }

    /// <summary>
    /// Whose side a worker ran on (LAB-12): the entity its effect is on, or the one that dealt the hit, as an effect's
    /// workers hear its hits on their dealer's side and impact workers run for their dealer.
    /// </summary>
    public enum EWorkerSide
    {
        Target,
        Dealer,
    }

    /// <summary>
    /// An effect's worker hearing one of its effect's moments, as the entity it ran on tells it (LAB-12). Told only where
    /// something listens, so a worker no one hears costs nothing more.
    /// </summary>
    public readonly struct EffectWorkerRun
    {
        public readonly GameplayEffectSpec Spec;
        public readonly AbstractEffectWorker Worker;
        public readonly EEffectWorkerHook Hook;
        public readonly EWorkerSide Side;

        /// <summary>The entity it ran on: the effect's target, or for a hit, its dealer.</summary>
        public readonly IGameplayAbilitySystem System;

        /// <summary>The actions it queued: a list the entity uses again, which a listener reads during the call.</summary>
        public readonly IReadOnlyList<IRootAction> Queued;

        public EffectWorkerRun(GameplayEffectSpec spec, AbstractEffectWorker worker, EEffectWorkerHook hook, EWorkerSide side,
            IGameplayAbilitySystem system, IReadOnlyList<IRootAction> queued)
        {
            Spec = spec;
            Worker = worker;
            Hook = hook;
            Side = side;
            System = system;
            Queued = queued ?? Array.Empty<IRootAction>();
        }
    }

    /// <summary>One of a hit's dealer's impact workers, run for the hit, as the dealer tells it (LAB-12).</summary>
    public readonly struct ImpactWorkerRun
    {
        public readonly ImpactData Impact;
        public readonly AbstractImpactWorker Worker;
        public readonly EWorkerExecution Execution;

        /// <summary>The dealer, whose worker it is.</summary>
        public readonly IGameplayAbilitySystem System;

        /// <summary>The actions it queued: a list the entity uses again, which a listener reads during the call.</summary>
        public readonly IReadOnlyList<IRootAction> Queued;

        public ImpactWorkerRun(in ImpactData impact, AbstractImpactWorker worker, EWorkerExecution execution,
            IGameplayAbilitySystem system, IReadOnlyList<IRootAction> queued)
        {
            Impact = impact;
            Worker = worker;
            Execution = execution;
            System = system;
            Queued = queued ?? Array.Empty<IRootAction>();
        }
    }

    /// <summary>What a step of a change to an attribute was (LAB-13).</summary>
    public enum EChangeStepKind
    {
        /// <summary>An attribute worker heard it, before its commit or after, and may have changed it.</summary>
        Worker,

        /// <summary>A barrier soaked a loss (TW-12).</summary>
        Barrier,

        /// <summary>A gain was scaled by the attribute its attribute names, as Healing Received scales a heal (TW-15).</summary>
        Scaled,
    }

    /// <summary>
    /// One step a change to an attribute took between being made and landing (LAB-13): an attribute worker's, before its
    /// commit or after it, inline or queued; a barrier's soak; or a gain's scaling; with the change before it and after.
    /// </summary>
    public readonly struct ChangeStep
    {
        public readonly EChangeStepKind Kind;
        public readonly IAttribute Attribute;

        /// <summary>The worker, for a worker's step; null for another.</summary>
        public readonly AbstractAttributeWorker Worker;

        /// <summary>The step in a word or two: the worker's kind, "Barrier", or the attribute a gain was scaled by.</summary>
        public readonly string Name;

        /// <summary>For a worker's step, whether it heard the change before its commit or after.</summary>
        public readonly AbstractAttributeWorker.EChangeEventTiming Timing;

        /// <summary>For a worker's step, whether it ran inline, queued its actions, or both.</summary>
        public readonly EWorkerExecution Execution;

        /// <summary>The change before the step.</summary>
        public readonly AttributeValue Before;

        /// <summary>The change after it.</summary>
        public readonly AttributeValue After;

        /// <summary>How many actions it queued, as a deferred worker does.</summary>
        public readonly int Queued;

        public ChangeStep(EChangeStepKind kind, IAttribute attribute, AbstractAttributeWorker worker, string name,
            AbstractAttributeWorker.EChangeEventTiming timing, EWorkerExecution execution, AttributeValue before,
            AttributeValue after, int queued)
        {
            Kind = kind;
            Attribute = attribute;
            Worker = worker;
            Name = name;
            Timing = timing;
            Execution = execution;
            Before = before;
            After = after;
            Queued = queued;
        }

        /// <summary>Whether it changed the change.</summary>
        public bool Changed => Before != After;

        /// <summary>The step in a line: "Math -30 → -15 (inline)", "Answering (queued 1, after)", "Barrier -30 → -10".</summary>
        public override string ToString()
        {
            var text = new StringBuilder(Name);
            if (Changed) text.Append(' ').Append(Number(Before.CurrentValue)).Append(" → ").Append(Number(After.CurrentValue));
            if (Kind != EChangeStepKind.Worker) return text.ToString();

            var notes = new List<string>(2);
            if (Execution != EWorkerExecution.Deferred) notes.Add("inline");
            if (Execution != EWorkerExecution.Inline) notes.Add($"queued {Queued}");
            if (Timing == AbstractAttributeWorker.EChangeEventTiming.PostChange) notes.Add("after");
            return text.Append(" (").Append(string.Join(", ", notes)).Append(')').ToString();
        }

        internal static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>A worker's kind in a word or two, as a journal or a log names it: "Math" for a MathAttributeWorker.</summary>
    public static class ForgeWorkerNames
    {
        private static readonly string[] Suffixes =
            { "AttributeWorker", "EffectWorker", "ImpactWorker", "AnalysisWorker", "TagWorker", "Worker" };

        // Each kind's name, worked out once, so a journal or a log naming it each change allocates nothing more
        private static readonly Dictionary<Type, string> Named = new();

        public static string Of(object worker)
        {
            if (worker == null) return "";
            var type = worker.GetType();
            if (Named.TryGetValue(type, out var known)) return known;
            string name = type.Name;
            foreach (var suffix in Suffixes)
            {
                if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                    break;
                }
            }
            // "CantFallBelow" reads "Cant Fall Below"
            var spaced = new StringBuilder(name.Length + 4);
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1])) spaced.Append(' ');
                spaced.Append(name[i]);
            }
            return Named[type] = spaced.ToString();
        }
    }

    /// <summary>
    /// The steps of one commit in progress (LAB-13), recorded where the combat log keeps entries or anything listens to
    /// its entity's steps, and told to its entity's listeners as each is taken. Each commit's scratch holds one, so its
    /// list is used again.
    /// </summary>
    internal sealed class ChangeStepRecorder
    {
        private readonly List<ChangeStep> steps = new();
        private GameplayAbilitySystemCallbacks callbacks;

        /// <summary>Whether this commit's steps are recorded.</summary>
        public bool Recording { get; private set; }

        /// <summary>The change as it was made, before any step; set for an impact's commit, not a modifier's.</summary>
        public AttributeValue Made { get; private set; }

        /// <summary>Whether <see cref="Made"/> was set.</summary>
        public bool HasMade { get; private set; }

        public IReadOnlyList<ChangeStep> Steps => steps;

        /// <summary>Which of the commit's moments its workers are hearing now.</summary>
        public AbstractAttributeWorker.EChangeEventTiming Timing = AbstractAttributeWorker.EChangeEventTiming.PreChange;

        public void Begin(bool recording, GameplayAbilitySystemCallbacks told)
        {
            steps.Clear();
            Recording = recording;
            callbacks = told;
            Made = default;
            HasMade = false;
            Timing = AbstractAttributeWorker.EChangeEventTiming.PreChange;
        }

        public void MadeAs(AttributeValue made)
        {
            Made = made;
            HasMade = true;
        }

        public void Add(in ChangeStep step)
        {
            if (!Recording) return;
            steps.Add(step);
            callbacks?.AttributeChangeStep(in step);
        }

        public void End()
        {
            steps.Clear();
            Recording = false;
            callbacks = null;
        }
    }
}
