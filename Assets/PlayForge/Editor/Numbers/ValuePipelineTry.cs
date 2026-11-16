using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A Value Pipeline tried in the editor (EP-23): a value put through its stages at each level of a range, against the
    /// stand-ins a Scaler asset's try reads against, a caster's and a target's attributes, typed or filled from an entity,
    /// and the values a caller sets. Each chance a stage rolls comes up where it can, or misses where it can, as
    /// <see cref="ChancesComeUp"/> says, so a read gives the same each time. A Block or Evade stage rolls first, as it does
    /// as an effect lands, and one that comes up stops the hit before the pipeline runs: nothing of it lands.
    /// <para>
    /// No effect is behind a try, so a stage that reads the hit's carrier reads none, and what an Absorb stage soaks comes
    /// off no one. A pipeline's try, from <see cref="For"/>, is kept for the session with <see cref="Keep"/>, and never
    /// written into the asset.
    /// </para>
    /// </summary>
    public sealed class ValuePipelineTry
    {
        /// <summary>The most levels a read takes at once, as a Scaler asset's try.</summary>
        public const int MostLevels = ScalerTry.MostLevels;

        /// <summary>A level's run: each stage's step and what came out, or the stop that stopped the hit.</summary>
        public sealed class Run
        {
            /// <summary>The level it ran at.</summary>
            public int Level { get; internal set; }

            /// <summary>What came out, what the hit lands with: 0 where a stop stopped it, and NaN where the run threw.</summary>
            public float Out { get; internal set; }

            /// <summary>Each stage that ran, in order, with what it took and gave and its note.</summary>
            public IReadOnlyList<ValueStep> Steps { get; internal set; } = Array.Empty<ValueStep>();

            /// <summary>The Block or Evade stage whose chance came up, where one did: nothing of the hit lands.</summary>
            public AbstractStopStage StoppedBy { get; internal set; }

            /// <summary>What the run threw, where it did, so the section says so rather than breaking.</summary>
            public string Error { get; internal set; }
        }

        /// <summary>What a read gave: the value it put in, its levels, and the run at each.</summary>
        public sealed class Reading
        {
            /// <summary>The value it put in.</summary>
            public float In { get; internal set; }

            /// <summary>The first level it read.</summary>
            public int From { get; internal set; }

            /// <summary>The last level it read.</summary>
            public int To { get; internal set; }

            /// <summary>The run at each level, from the first.</summary>
            public Run[] Levels { get; internal set; } = Array.Empty<Run>();

            /// <summary>The first level's error, where a run threw.</summary>
            public string Error { get; internal set; }
        }

        /// <summary>The value the hit lands with, going in: a loss below 0, as a blow of 100 is −100; a gain above.</summary>
        public float Value = -100f;

        /// <summary>
        /// Whether each chance a stage rolls comes up where it can, as a critical strike or a block, or misses where it can.
        /// A chance of 1 always comes up and one of 0 never does.
        /// </summary>
        public bool ChancesComeUp;

        /// <summary>The caster, the target, the caller and the range of levels, held as a Scaler asset's try holds them.</summary>
        public ScalerTry Numbers { get; private set; } = new();

        /// <summary>The first level a read takes.</summary>
        public int From
        {
            get => Numbers.From;
            set => Numbers.From = value;
        }

        /// <summary>The last level a read takes, at most <see cref="MostLevels"/> after the first.</summary>
        public int To
        {
            get => Numbers.To;
            set => Numbers.To = value;
        }

        // The session key it's kept under, a pipeline's; none for a try made apart
        private string key;

        /// <summary>
        /// Puts <see cref="Value"/> through a pipeline at each level from <see cref="From"/> to <see cref="To"/>, at most
        /// <see cref="MostLevels"/>, the sides and the caller as they stand, adding a row for each attribute and key its
        /// stages ask for that it hadn't.
        /// </summary>
        public Reading Read(ValuePipeline pipeline)
        {
            int from = Mathf.Max(1, From);
            int to = Mathf.Clamp(To, from, from + MostLevels - 1);
            var reading = new Reading { In = Value, From = from, To = to, Levels = new Run[to - from + 1] };
            var caster = new NumberStandIn(Numbers.Caster, "The caster");
            var target = new NumberStandIn(Numbers.Target, "The target");
            var values = new CallerValues(Numbers);

            // Each draw the same: one of 0 is under any chance above 0, and one of 1 under none below 1
            var previous = ForgeRandom.Use(new SameDraw(ChancesComeUp ? 0f : 1f));
            try
            {
                for (int level = from; level <= to; level++)
                {
                    var run = new Run { Level = level, Out = Value };
                    reading.Levels[level - from] = run;
                    if (pipeline == null) continue;
                    try
                    {
                        var context = NumberContext.For(caster, target, level, to).WithMagnitudes(values);
                        run.StoppedBy = StoppedBy(pipeline, Value, in context);
                        if (run.StoppedBy != null)
                        {
                            run.Out = 0f;
                            continue;
                        }
                        var breakdown = new ValueBreakdown();
                        run.Out = pipeline.Run(Value, in context, breakdown);
                        run.Steps = breakdown.Steps.ToArray();
                    }
                    catch (Exception error)
                    {
                        // A stage that breaks on a stand-in breaks no panel: the read says so, and goes on
                        run.Out = float.NaN;
                        run.Error = $"{error.GetType().Name}: {error.Message}";
                        reading.Error ??= $"Level {level}: {run.Error}";
                    }
                }
            }
            finally
            {
                ForgeRandom.Use(previous);
            }
            return reading;
        }

        // The first Block or Evade stage whose chance comes up, as the effect lands, before the pipeline runs; one that stops
        // losses alone rolls only for a loss, so a heal lands past it unrolled
        private static AbstractStopStage StoppedBy(ValuePipeline pipeline, float value, in NumberContext context)
        {
            if (pipeline.Stages == null) return null;
            foreach (var stage in pipeline.Stages)
            {
                if (stage is not AbstractStopStage { Enabled: true } stop) continue;
                if (stop.Stops != EStopImpacts.AnyImpact && value >= 0f) continue;
                if (ForgeChance.Roll(stop.Chance?.Evaluate(in context, stop) ?? 0f, in context, stop.Roll, stop)) return stop;
            }
            return null;
        }

        // A source of random numbers that draws the same each time
        private sealed class SameDraw : IForgeRandom
        {
            private readonly float draw;

            public SameDraw(float draw) => this.draw = draw;

            public float Value => draw;
            public Vector2 InsideUnitCircle => Vector2.zero;
            public Vector3 InsideUnitSphere => Vector3.zero;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Kept for the session
        // ═══════════════════════════════════════════════════════════════════════════

        [Serializable]
        private sealed class Kept
        {
            public float Value = -100f;
            public bool ChancesComeUp;
        }

        /// <summary>A pipeline's try, as it was left this session, or a new one.</summary>
        public static ValuePipelineTry For(ValuePipeline pipeline)
        {
            string key = ScalerTry.SessionKey(pipeline, nameof(ValuePipelineTry));
            var tried = new ValuePipelineTry { key = key, Numbers = ScalerTry.ForKey(NumbersKey(key)) };
            var json = key == null ? null : SessionState.GetString(key, null);
            if (string.IsNullOrEmpty(json)) return tried;
            var kept = JsonUtility.FromJson<Kept>(json);
            if (kept == null) return tried;
            tried.Value = kept.Value;
            tried.ChancesComeUp = kept.ChancesComeUp;
            return tried;
        }

        /// <summary>Keeps it for the session, for <see cref="For"/> to give again; never in the asset.</summary>
        public void Keep()
        {
            if (key == null) return;
            Numbers.Keep();
            SessionState.SetString(key, JsonUtility.ToJson(new Kept { Value = Value, ChancesComeUp = ChancesComeUp }));
        }

        /// <summary>Forgets a pipeline's try, so the next starts anew.</summary>
        public static void Forget(ValuePipeline pipeline)
        {
            string key = ScalerTry.SessionKey(pipeline, nameof(ValuePipelineTry));
            if (key == null) return;
            SessionState.EraseString(key);
            ScalerTry.ForgetKey(NumbersKey(key));
        }

        private static string NumbersKey(string key) => key == null ? null : $"{key}.Numbers";
    }
}
