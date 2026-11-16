using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>What a stop watches for (LAB-14, Decision 88).</summary>
    public enum ELabStopKind
    {
        /// <summary>An effect lands: the one named, or any.</summary>
        EffectLands,

        /// <summary>An effect is refused, by a requirement, an immunity, its chance, or anything else.</summary>
        EffectRefused,

        /// <summary>A worker runs for an effect: the one named, or any.</summary>
        WorkerRuns,

        /// <summary>An attribute crosses a value, either way.</summary>
        AttributeCrosses,

        /// <summary>A tag comes.</summary>
        TagComes,

        /// <summary>A tag goes.</summary>
        TagGoes,

        /// <summary>An ability is refused: the one named, or any.</summary>
        AbilityRefused,
    }

    /// <summary>
    /// A stop (LAB-14): what it watches for, on the selected unit or any, and what it names, an effect, an ability, a tag,
    /// or an attribute and a value. The lab's world pauses as one fires, and says which.
    /// </summary>
    [Serializable]
    public sealed class LabStop
    {
        public ELabStopKind Kind;

        [Tooltip("On the selected unit alone; off, on any")]
        public bool OnSelected = true;

        [Tooltip("For an effect's stop, the effect; with none, any")]
        public GameplayEffect Effect;

        [Tooltip("For an ability's stop, the ability; with none, any")]
        public Ability Ability;

        [Tooltip("For a tag's stop, the tag")]
        [ForgeTagContext(ForgeContext.Granted)]
        public Tag Tag;

        [Tooltip("For an attribute's stop, the attribute")]
        public Attribute Attribute;

        [Tooltip("For an attribute's stop, the value it crosses")]
        public float Value;

        [Tooltip("Off, it watches for nothing, and stays in the list")]
        public bool Enabled = true;

        /// <summary>The stop in a line: "Burn lands, on the selected unit", "Health crosses 50, on any unit".</summary>
        public override string ToString()
        {
            string what = Kind switch
            {
                ELabStopKind.EffectLands => $"{Named(Effect, "An effect")} lands",
                ELabStopKind.EffectRefused => $"{Named(Effect, "An effect")} is refused",
                ELabStopKind.WorkerRuns => $"A worker of {Named(Effect, "any effect")} runs",
                ELabStopKind.AttributeCrosses => $"{(Attribute != null ? Attribute.GetName() : "An attribute")} crosses {Number(Value)}",
                ELabStopKind.TagComes => $"{(Tag.IsValid ? Tag.ToString() : "A tag")} comes",
                ELabStopKind.TagGoes => $"{(Tag.IsValid ? Tag.ToString() : "A tag")} goes",
                _ => $"{Named(Ability, "An ability")} is refused",
            };
            return $"{what}, {(OnSelected ? "on the selected unit" : "on any unit")}";
        }

        private static string Named(UnityEngine.Object asset, string none) => asset ? asset.name : none;

        internal static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// What the lab watches its units for, and the journals it keeps while it does (LAB-12 to LAB-15, Decision 88): its
    /// effect journal over every unit, its stops, which pause its world as one fires and say which, and its watches,
    /// sampled over the last minute of its time. The lab steps it after each step of its world.
    /// </summary>
    public sealed class LabShowing : IDisposable
    {
        /// <summary>How often a watch takes its value, in seconds of the lab's time.</summary>
        public const float SampleEvery = 0.1f;

        /// <summary>How long a watch's line is, in seconds of the lab's time: its last minute.</summary>
        public const float Window = 60f;

        /// <summary>How long Step Tick goes at most, in seconds of the lab's time, before it stops waiting for a tick.</summary>
        public const float MostTickWait = 10f;

        /// <summary>How many of ForgeLog's messages the lab's log keeps, its last.</summary>
        public const int MostLogged = 300;

        private readonly CreatorLab lab;
        private readonly List<IGameplayAbilitySystem> entities = new();
        private readonly Dictionary<IGameplayAbilitySystem, AbilitySystemCallbacks> abilityCallbacks = new();
        private readonly Dictionary<(LabUnit, Attribute), float> lastValues = new();
        private readonly Dictionary<(LabUnit, Tag), bool> lastTags = new();
        private readonly Dictionary<AbilitySystemCallbacks, AbilitySystemCallbacks.AbilityRefusedDelegate> refusedBy = new();
        private readonly List<IGameplayAbilitySystem> goneEntities = new();
        private float sinceSample;
        private bool waitingForTick;
        private bool ticked;
        private bool stoppedWhileWaiting;

        public LabShowing(CreatorLab lab)
        {
            this.lab = lab;
            Journal = new EffectJournal();
            Journal.Wrote += Heard;
            ForgeLog.Logged += Logged;
        }

        /// <summary>ForgeLog's messages as they were let through while the lab ran, oldest first, its last few hundred (LAB-16).</summary>
        public List<ForgeLogEntry> Log { get; } = new();

        /// <summary>How many messages the log has heard, those it no longer keeps among them.</summary>
        public int LogWritten { get; private set; }

        /// <summary>Whether a message is about a unit: it names the unit's entity, or points at the unit.</summary>
        public static bool About(in ForgeLogEntry entry, LabUnit unit)
        {
            if (!unit) return false;
            if (entry.Subject != null && (ReferenceEquals(entry.Subject, unit.Gas) || ReferenceEquals(entry.Subject, unit))) return true;
            return entry.Context != null && (entry.Context == unit.gameObject || entry.Context == unit || entry.Context == unit.Gas);
        }

        private void Logged(ForgeLogEntry entry)
        {
            Log.Add(entry);
            LogWritten++;
            if (Log.Count > MostLogged) Log.RemoveRange(0, Log.Count - MostLogged);
        }

        /// <summary>The effect journal over every unit of the lab, its caster's among them (LAB-12).</summary>
        public EffectJournal Journal { get; }

        /// <summary>The stops, in the order they were added (LAB-14).</summary>
        public List<LabStop> Stops { get; } = new();

        /// <summary>The watches, in the order they were pinned (LAB-15).</summary>
        public List<LabWatch> Watches { get; } = new();

        /// <summary>The stop that paused the lab's world last, while it stays paused; null otherwise.</summary>
        public LabStop StoppedBy { get; private set; }

        /// <summary>What fired it, in a line: "Burn landed on Target, from Caster".</summary>
        public string StoppedFor { get; private set; } = "";

        /// <summary>Raised as a stop pauses the lab's world.</summary>
        public event Action<LabStop, string> Stopped;

        /// <summary>Adds a stop, and starts watching for it.</summary>
        public LabStop Add(LabStop stop)
        {
            if (stop == null) return null;
            Stops.Add(stop);
            Prime(stop);
            return stop;
        }

        /// <summary>Takes a stop away.</summary>
        public bool Remove(LabStop stop) => stop != null && Stops.Remove(stop);

        /// <summary>Pins a watch, and takes its first value.</summary>
        public LabWatch Pin(LabWatch watch)
        {
            if (watch == null) return null;
            Watches.Add(watch);
            watch.Take(lab.Elapsed);
            return watch;
        }

        /// <summary>Unpins a watch.</summary>
        public bool Unpin(LabWatch watch) => watch != null && Watches.Remove(watch);

        /// <summary>Lets go of the stop that paused the world, as it plays on.</summary>
        public void Resume()
        {
            StoppedBy = null;
            StoppedFor = "";
        }

        /// <summary>
        /// Steps the lab a frame at a time until an effect ticks on the selected unit, or on any where none is, up to
        /// <see cref="MostTickWait"/> of its time; true where one did.
        /// </summary>
        public bool StepTick()
        {
            waitingForTick = true;
            ticked = false;
            stoppedWhileWaiting = false;
            Resume();
            try
            {
                float waited = 0f;
                float step = Mathf.Max(0.001f, lab.StepSeconds);
                // A stop that fires on the way ends it there, as it would a run
                while (!ticked && !stoppedWhileWaiting && waited < MostTickWait)
                {
                    lab.Step(step);
                    waited += step;
                }
                return ticked;
            }
            finally
            {
                waitingForTick = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // As the lab steps
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Hears the lab's units as they are now, and watches each for what the stops name.</summary>
        internal void Follow()
        {
            entities.Clear();
            foreach (var unit in lab.Units)
                if (unit && unit.Gas != null) entities.Add(unit.Gas);
            if (lab.Caster && lab.Caster.Gas != null) entities.Add(lab.Caster.Gas);
            Journal.Follow(entities);
            FollowAbilities();
        }

        /// <summary>After a step of the lab's world: its stops' crossings and tags, and its watches' values.</summary>
        internal void Stepped(float deltaTime)
        {
            foreach (var stop in Stops)
            {
                if (!stop.Enabled) continue;
                switch (stop.Kind)
                {
                    case ELabStopKind.AttributeCrosses:
                        if (stop.Attribute == null) break;
                        foreach (var unit in Watched(stop))
                        {
                            float now = unit.Value(stop.Attribute);
                            if (lastValues.TryGetValue((unit, stop.Attribute), out float was) && Crossed(was, now, stop.Value))
                                Fire(stop, $"{unit.Name}'s {stop.Attribute.GetName()} crossed {LabStop.Number(stop.Value)}: " +
                                           $"{LabStop.Number(was)} to {LabStop.Number(now)}");
                            lastValues[(unit, stop.Attribute)] = now;
                        }
                        break;
                    case ELabStopKind.TagComes:
                    case ELabStopKind.TagGoes:
                        if (!stop.Tag.IsValid) break;
                        foreach (var unit in Watched(stop))
                        {
                            bool has = unit.Has(stop.Tag);
                            if (lastTags.TryGetValue((unit, stop.Tag), out bool had) && had != has
                                && has == (stop.Kind == ELabStopKind.TagComes))
                                Fire(stop, $"{stop.Tag} {(has ? "came to" : "went from")} {unit.Name}");
                            lastTags[(unit, stop.Tag)] = has;
                        }
                        break;
                }
            }

            sinceSample += deltaTime;
            if (sinceSample + 0.0001f < SampleEvery) return;
            sinceSample = 0f;
            for (int i = Watches.Count - 1; i >= 0; i--)
            {
                var watch = Watches[i];
                if (!watch.Unit) Watches.RemoveAt(i);
                else watch.Take(lab.Elapsed);
            }
        }

        // An attribute's or a tag's first value, so its first step finds no crossing in what came before it was added
        private void Prime(LabStop stop)
        {
            foreach (var unit in Watched(stop))
            {
                if (stop.Kind == ELabStopKind.AttributeCrosses && stop.Attribute != null)
                    lastValues[(unit, stop.Attribute)] = unit.Value(stop.Attribute);
                if ((stop.Kind == ELabStopKind.TagComes || stop.Kind == ELabStopKind.TagGoes) && stop.Tag.IsValid)
                    lastTags[(unit, stop.Tag)] = unit.Has(stop.Tag);
            }
        }

        private static bool Crossed(float was, float now, float value) =>
            was < value && now >= value || was > value && now <= value;

        // The units a stop watches: the selected one, or every one
        private IEnumerable<LabUnit> Watched(LabStop stop)
        {
            if (stop.OnSelected)
            {
                if (lab.SelectedUnit) yield return lab.SelectedUnit;
                yield break;
            }
            foreach (var unit in lab.Units)
                if (unit) yield return unit;
        }

        private bool Concerns(LabStop stop, IGameplayAbilitySystem entity)
        {
            if (!stop.OnSelected) return true;
            return lab.SelectedUnit && ReferenceEquals(lab.SelectedUnit.Gas, entity);
        }

        // What the journal hears: a stop's effect landing or refused, or a worker run; and a tick, for Step Tick
        private void Heard(EffectJournalEntry entry)
        {
            if (waitingForTick && entry.Kind == EEffectJournalKind.Ticked)
            {
                var on = lab.SelectedUnit ? lab.SelectedUnit.Gas : null;
                if (on == null || ReferenceEquals(entry.Entity, on)) ticked = true;
            }
            foreach (var stop in Stops)
            {
                if (!stop.Enabled || !Concerns(stop, entry.Entity)) continue;
                if (stop.Effect != null && entry.Effect != stop.Effect) continue;
                bool fires = stop.Kind switch
                {
                    ELabStopKind.EffectLands => entry.Kind == EEffectJournalKind.Applied,
                    ELabStopKind.EffectRefused => entry.Kind == EEffectJournalKind.Refused,
                    ELabStopKind.WorkerRuns => entry.Kind == EEffectJournalKind.Worker,
                    _ => false,
                };
                if (fires) Fire(stop, entry.ToString());
            }
        }

        private void AbilityRefused(IGameplayAbilitySystem entity, ActivationRefusal refusal)
        {
            foreach (var stop in Stops)
            {
                if (!stop.Enabled || stop.Kind != ELabStopKind.AbilityRefused || !Concerns(stop, entity)) continue;
                if (stop.Ability != null && refusal.Ability != stop.Ability) continue;
                Fire(stop, $"{(refusal.Ability ? refusal.Ability.GetName() : "An ability")} refused on {NameOf(entity)}: {refusal}");
            }
        }

        private void FollowAbilities()
        {
            // Each entity's ability callbacks, heard while it's the lab's
            goneEntities.Clear();
            foreach (var entity in abilityCallbacks.Keys)
                if (!entities.Contains(entity)) goneEntities.Add(entity);
            foreach (var entity in goneEntities)
            {
                var callbacks = abilityCallbacks[entity];
                if (callbacks != null && refusedBy.Remove(callbacks, out var heard)) callbacks.OnAbilityRefused -= heard;
                abilityCallbacks.Remove(entity);
            }
            foreach (var entity in entities)
            {
                if (abilityCallbacks.ContainsKey(entity)) continue;
                // A unit just put down has no ability system until its world registers it: it's heard once it has one
                var callbacks = entity.GetAbilitySystem()?.Callbacks;
                if (callbacks == null) continue;
                abilityCallbacks[entity] = callbacks;
                var who = entity;
                AbilitySystemCallbacks.AbilityRefusedDelegate heard = refusal => AbilityRefused(who, refusal);
                refusedBy[callbacks] = heard;
                callbacks.OnAbilityRefused += heard;
            }
        }

        // A unit's name, where the entity is a unit's, or the entity's own
        private string NameOf(IGameplayAbilitySystem entity)
        {
            foreach (var unit in lab.Units)
                if (unit && ReferenceEquals(unit.Gas, entity)) return unit.Name;
            return entity?.GetName() ?? "";
        }

        // A stop fired: the world pauses, and says which
        private void Fire(LabStop stop, string why)
        {
            StoppedBy = stop;
            StoppedFor = why;
            if (waitingForTick) stoppedWhileWaiting = true;
            lab.Paused = true;
            Stopped?.Invoke(stop, why);
        }

        public void Dispose()
        {
            ForgeLog.Logged -= Logged;
            Journal.Wrote -= Heard;
            Journal.Dispose();
            foreach (var pair in refusedBy) pair.Key.OnAbilityRefused -= pair.Value;
            refusedBy.Clear();
            abilityCallbacks.Clear();
        }
    }

    /// <summary>What a watch reads (LAB-15).</summary>
    public enum ELabWatchKind
    {
        /// <summary>An attribute's current value, or its base.</summary>
        Attribute,

        /// <summary>A tag's count, its weight on the unit.</summary>
        TagCount,

        /// <summary>An effect's stacks on the unit.</summary>
        EffectStacks,

        /// <summary>An effect's time left on the unit, in seconds.</summary>
        EffectTime,

        /// <summary>A Scaler asset read on the unit, as its caster and its target.</summary>
        Number,
    }

    /// <summary>
    /// A watch (LAB-15): a unit's attribute, a tag's count, an effect's stacks or time, or a number read on it, pinned, and
    /// its values over the last minute of the lab's time, one each tenth of a second.
    /// </summary>
    public sealed class LabWatch
    {
        private readonly float[] times;
        private readonly float[] values;
        private int start;

        public LabWatch()
        {
            int most = Mathf.CeilToInt(LabShowing.Window / LabShowing.SampleEvery) + 1;
            times = new float[most];
            values = new float[most];
        }

        public ELabWatchKind Kind;
        public LabUnit Unit;
        public Attribute Attribute;

        /// <summary>For an attribute, its base rather than its current value.</summary>
        public bool Base;

        [ForgeTagContext(ForgeContext.Granted)]
        public Tag Tag;
        public GameplayEffect Effect;
        public ScalerAsset Number;

        /// <summary>How many values it holds.</summary>
        public int Count { get; private set; }

        /// <summary>Its newest value; 0 before it takes one.</summary>
        public float Latest => Count == 0 ? 0f : values[(start + Count - 1) % values.Length];

        /// <summary>Its i-th value and when it was taken, oldest first.</summary>
        public (float time, float value) this[int i] => (times[(start + i) % times.Length], values[(start + i) % values.Length]);

        /// <summary>What it reads now.</summary>
        public float Read()
        {
            if (!Unit || Unit.Gas == null) return 0f;
            switch (Kind)
            {
                case ELabWatchKind.Attribute:
                    return Attribute == null ? 0f : Base ? Unit.Base(Attribute) : Unit.Value(Attribute);
                case ELabWatchKind.TagCount:
                    return Tag.IsValid && Unit.Gas.GetTagCache() is { } tags ? Mathf.Max(0, tags.GetWeight(Tag)) : 0f;
                case ELabWatchKind.EffectStacks:
                    // A container that doesn't stack holds one application
                    return Effect != null && Unit.Gas.TryGetEffectContainer(Effect, out var stacked)
                        ? stacked is AbstractStackingEffectContainer stacking ? stacking.Stacks : 1f
                        : 0f;
                case ELabWatchKind.EffectTime:
                    return Effect != null && Unit.Gas.TryGetEffectContainer(Effect, out var timed) ? Mathf.Max(0f, timed.DurationRemaining) : 0f;
                default:
                    // Read as the unit's own number, the unit its caster and its target
                    return Number != null && Number.Scaler != null ? NumberTrace.Of(Number.Scaler, NumberContext.Of(Unit.Gas)).Value : 0f;
            }
        }

        // Takes its value now, the oldest going once it holds a minute's
        internal void Take(float time)
        {
            float value = Read();
            if (Count < values.Length)
            {
                times[(start + Count) % times.Length] = time;
                values[(start + Count) % values.Length] = value;
                Count++;
            }
            else
            {
                times[start] = time;
                values[start] = value;
                start = (start + 1) % values.Length;
            }
        }

        /// <summary>The watch in a line: "Target's Health", "Target's Burn, its stacks".</summary>
        public override string ToString()
        {
            string unit = Unit ? Unit.Name : "No unit";
            return Kind switch
            {
                ELabWatchKind.Attribute => $"{unit}'s {(Attribute != null ? Attribute.GetName() : "attribute")}{(Base ? " (base)" : "")}",
                ELabWatchKind.TagCount => $"{unit}'s {(Tag.IsValid ? Tag.ToString() : "tag")}",
                ELabWatchKind.EffectStacks => $"{unit}'s {(Effect ? Effect.name : "effect")}, its stacks",
                ELabWatchKind.EffectTime => $"{unit}'s {(Effect ? Effect.name : "effect")}, its time",
                _ => $"{(Number ? Number.name : "A number")} on {unit}",
            };
        }
    }
}
