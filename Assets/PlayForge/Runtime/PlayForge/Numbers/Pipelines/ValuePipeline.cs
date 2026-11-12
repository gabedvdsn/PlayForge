using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A value through ordered stages as it lands (TW-2, Decision 46). An effect names one as its impact's calculation,
    /// as an Unreal effect names its execution calculation, so each hit goes through the stages a project writes once: the
    /// caster's bonuses, those that read the target, a critical roll, the target's reductions, a barrier; and what comes
    /// out lands. A new item that changes every hit is a stage, not an edit to each attack. Generic: the value is any
    /// number an impact lands with, a blow, a heal, a drain.
    /// <para>
    /// It isn't a Scaler asset. A scaler is a number worked out from its context, read wherever a number is; a pipeline
    /// transforms a value it's given, once, as it lands, with both sides known, and its stages can do what a number can't:
    /// roll once for the hit and keep it, route a part of it into a barrier, say what each did.
    /// </para>
    /// <para>
    /// It runs for what an impact commits: an instant impact's hit, each tick of a periodic one. A lasting impact's value is
    /// read again as its attribute changes, so nothing runs it.
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "New Value Pipeline", menuName = "PlayForge/Value Pipeline", order = 101)]
    public class ValuePipeline : ScriptableObject
    {
        [Tooltip("What the pipeline is for")]
        [TextArea(2, 4)]
        public string Description;

        [Tooltip("The stages a value goes through, in order")]
        [SerializeReference]
        public List<AbstractValueStage> Stages = new();

        /// <summary>
        /// <paramref name="value"/> through each stage in turn, each reading the hit's context: its caster, its target, its
        /// level. <paramref name="breakdown"/>, where given, keeps what each stage took and gave.
        /// </summary>
        public float Run(float value, IAttributeImpactCarrier carrier, ValueBreakdown breakdown = null)
        {
            breakdown?.Begin(this, value);
            var run = new ValueRun(carrier, NumberContext.Of(carrier), breakdown);
            if (Stages != null)
            {
                for (int i = 0; i < Stages.Count; i++)
                {
                    var stage = Stages[i];
                    if (stage == null || !stage.Enabled) continue;
                    float before = value;
                    value = stage.Apply(value, in run);
                    breakdown?.Step(stage, before, value);
                }
            }
            breakdown?.End(value);
            return value;
        }

        /// <summary>
        /// Stages a value goes through after its pipeline, as modifiers add them (TW-7): kept in the breakdown after the
        /// pipeline's own steps where one ran, as a breakdown of their own where none did.
        /// </summary>
        public static float RunAfter(IReadOnlyList<AbstractValueStage> stages, float value, IAttributeImpactCarrier carrier,
            ValueBreakdown breakdown, bool pipelineRan)
        {
            if (stages == null || stages.Count == 0) return value;
            if (!pipelineRan) breakdown?.Begin(null, value);
            var run = new ValueRun(carrier, NumberContext.Of(carrier), breakdown);
            for (int i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];
                if (stage == null || !stage.Enabled) continue;
                float before = value;
                value = stage.Apply(value, in run);
                breakdown?.Step(stage, before, value);
            }
            breakdown?.End(value);
            return value;
        }

        /// <summary>Its stages, in a line: "× (1 + Caster's Damage Amplification), then × (1 − Target's Damage Resistance)".</summary>
        public string Describe()
        {
            if (Stages == null || Stages.Count == 0) return "no stages";
            var parts = new List<string>();
            foreach (var stage in Stages)
                if (stage != null && stage.Enabled) parts.Add(stage.Name());
            return parts.Count == 0 ? "no stages" : string.Join(", then ", parts);
        }
    }

    /// <summary>What a stage reads in a run: the hit's carrier, its number context, and the breakdown, if one's kept.</summary>
    public readonly struct ValueRun
    {
        /// <summary>The hit: its effect's spec, whose origin is the caster.</summary>
        public readonly IAttributeImpactCarrier Carrier;

        /// <summary>The hit's numbers: its caster, its target, its level.</summary>
        public readonly NumberContext Context;

        /// <summary>What the run keeps of each stage, or null.</summary>
        public readonly ValueBreakdown Breakdown;

        public ValueRun(IAttributeImpactCarrier carrier, NumberContext context, ValueBreakdown breakdown)
        {
            Carrier = carrier;
            Context = context;
            Breakdown = breakdown;
        }

        /// <summary>Notes what the stage running did, as a critical hit, for its step in the breakdown.</summary>
        public void Note(string note) => Breakdown?.NoteNext(note);
    }

    /// <summary>One stage's part of a run: what it took and what it gave, and a note.</summary>
    public readonly struct ValueStep
    {
        public readonly string Stage;
        public readonly float Before;
        public readonly float After;
        public readonly string Note;

        public ValueStep(string stage, float before, float after, string note)
        {
            Stage = stage;
            Before = before;
            After = after;
            Note = note;
        }

        public override string ToString() => string.IsNullOrEmpty(Note) ? $"{Stage}: {Before:0.##} → {After:0.##}" : $"{Stage}: {Before:0.##} → {After:0.##} ({Note})";
    }

    /// <summary>
    /// A run's stages, each with the value it took and gave and a note: what a combat log, a debugger and a test read of a
    /// hit (TW-2). One is kept for each effect spec and filled again by each run.
    /// </summary>
    public sealed class ValueBreakdown
    {
        private readonly List<ValueStep> steps = new();
        private string pendingNote;

        /// <summary>The pipeline that ran.</summary>
        public ValuePipeline Pipeline { get; private set; }

        /// <summary>The value it was given.</summary>
        public float In { get; private set; }

        /// <summary>The value it gave.</summary>
        public float Out { get; private set; }

        /// <summary>Each stage that ran, in order.</summary>
        public IReadOnlyList<ValueStep> Steps => steps;

        internal void Begin(ValuePipeline pipeline, float value)
        {
            Pipeline = pipeline;
            In = Out = value;
            steps.Clear();
            pendingNote = null;
        }

        internal void NoteNext(string note) => pendingNote = note;

        internal void Step(AbstractValueStage stage, float before, float after)
        {
            steps.Add(new ValueStep(stage.Name(), before, after, pendingNote));
            pendingNote = null;
        }

        internal void End(float value) => Out = value;

        /// <summary>The run in a line: "-100 → × (1 + Caster's Damage Amplification) -150 → … → -120".</summary>
        public override string ToString()
        {
            var text = new StringBuilder($"{In:0.##}");
            foreach (var step in steps) text.Append($" → {step.Stage} {step.After:0.##}{(string.IsNullOrEmpty(step.Note) ? "" : $" ({step.Note})")}");
            return text.ToString();
        }
    }
}
