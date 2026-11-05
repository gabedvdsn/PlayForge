using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>What a track records: an entity's ability, or an effect on it.</summary>
    internal enum TimelineKind
    {
        Ability,
        Effect,
    }

    /// <summary>A stage's span within an ability's run: when it started, when it ended, and whether it held.</summary>
    internal sealed class TimelineStage
    {
        public int Index;
        public float Start;
        public float End = float.NaN;
        public bool Succeeded;
        public bool Open => float.IsNaN(End);
    }

    /// <summary>
    /// A span on a track: an ability's run, stage by stage, or an effect's time on the shelf. An instant one, a refused
    /// activation or an instant effect's landing, ends as it starts.
    /// </summary>
    internal sealed class TimelineSpan
    {
        public float Start;
        public float End = float.NaN;
        public bool Refused;
        public string Note = "";
        public readonly List<TimelineStage> Stages = new();
        public bool Open => float.IsNaN(End);
        public bool Instant => !Open && Mathf.Approximately(End, Start);
        public float Length(float now) => (Open ? now : End) - Start;
    }

    /// <summary>One entity's ability, or an effect on it, with what the recorder kept of it, the oldest first.</summary>
    internal sealed class TimelineTrack
    {
        public IGameplayAbilitySystem Entity;
        public string EntityName;
        public ScriptableObject Asset;
        public TimelineKind Kind;
        public readonly List<TimelineSpan> Spans = new();
        public string Name => Asset != null ? Asset.name : "Unknown";
    }

    /// <summary>
    /// The Debugger's recorder (FEAT-12, Decision 30): it hears the entities it's given, and keeps each ability's runs,
    /// stage by stage, its refusals, and each effect's time on the shelf, by the game's time, for the Timeline view. The
    /// Debugger gives it the entities it lists as it samples the game, in Play mode, so it records only there. It keeps the
    /// last <see cref="MostSpans"/> of each track.
    /// </summary>
    internal sealed class TimelineRecorder : IDisposable
    {
        public const int MostSpans = 100;

        /// <summary>The tracks, in the order their first span came.</summary>
        public readonly List<TimelineTrack> Tracks = new();

        /// <summary>The changes it has heard, so a view knows when to draw again.</summary>
        public int Version { get; private set; }

        /// <summary>Whether it records what it hears.</summary>
        public bool Recording = true;

        private readonly Dictionary<(IGameplayAbilitySystem, ScriptableObject), TimelineTrack> tracks = new();
        private readonly Dictionary<AbilityDataPacket, TimelineSpan> runs = new();
        private readonly Dictionary<IGameplayAbilitySystem, Hearing> heard = new();
        private readonly List<IGameplayAbilitySystem> gone = new();

        // The game's time, which a run's and an effect's spans are measured by
        private static float Now => ForgeTime.Time;

        // What it hears of one entity, kept to stop hearing it
        private sealed class Hearing
        {
            public AbilitySystemCallbacks Abilities;
            public GameplayAbilitySystemCallbacks Entity;
            public AbilitySystemCallbacks.AbilitySystemCallbackDelegate Activated, StageStarted, StageEnded, Ended;
            public AbilitySystemCallbacks.AbilityRefusedDelegate Refused;
            public GameplayAbilitySystemCallbacks.EffectDelegate Applied;
            public GameplayAbilitySystemCallbacks.EffectRemovedDelegate Removed;
        }

        /// <summary>Hears these entities and no others: each new one from now on, and each gone no more.</summary>
        public void Follow(IReadOnlyList<IGameplayAbilitySystem> entities)
        {
            foreach (var gas in entities)
                if (gas != null && !heard.ContainsKey(gas)) Hear(gas);
            gone.Clear();
            foreach (var gas in heard.Keys)
            {
                bool listed = false;
                for (int i = 0; i < entities.Count && !listed; i++) listed = ReferenceEquals(entities[i], gas);
                if (!listed) gone.Add(gas);
            }
            foreach (var gas in gone) Forget(gas);
        }

        private void Hear(IGameplayAbilitySystem gas)
        {
            var hearing = new Hearing { Abilities = gas.GetAbilitySystem()?.Callbacks, Entity = gas.GetCallbacks() };
            hearing.Activated = status => Activated(gas, status);
            hearing.StageStarted = status => StageStarted(status);
            hearing.StageEnded = status => StageEnded(status);
            hearing.Ended = status => Ended(status);
            hearing.Refused = refusal => Refused(gas, refusal);
            hearing.Applied = spec => Applied(gas, spec);
            hearing.Removed = effect => Removed(gas, effect);
            if (hearing.Abilities != null)
            {
                hearing.Abilities.OnAbilityActivate += hearing.Activated;
                hearing.Abilities.OnAbilityStageActivate += hearing.StageStarted;
                hearing.Abilities.OnAbilityStageEnd += hearing.StageEnded;
                hearing.Abilities.OnAbilityEnd += hearing.Ended;
                hearing.Abilities.OnAbilityRefused += hearing.Refused;
            }
            if (hearing.Entity != null)
            {
                hearing.Entity.OnEffectApplied += hearing.Applied;
                hearing.Entity.OnEffectRemoved += hearing.Removed;
            }
            heard[gas] = hearing;
        }

        /// <summary>Stops hearing an entity. What it recorded of it stays.</summary>
        public void Forget(IGameplayAbilitySystem gas)
        {
            if (!heard.Remove(gas, out var hearing)) return;
            if (hearing.Abilities != null)
            {
                hearing.Abilities.OnAbilityActivate -= hearing.Activated;
                hearing.Abilities.OnAbilityStageActivate -= hearing.StageStarted;
                hearing.Abilities.OnAbilityStageEnd -= hearing.StageEnded;
                hearing.Abilities.OnAbilityEnd -= hearing.Ended;
                hearing.Abilities.OnAbilityRefused -= hearing.Refused;
            }
            if (hearing.Entity != null)
            {
                hearing.Entity.OnEffectApplied -= hearing.Applied;
                hearing.Entity.OnEffectRemoved -= hearing.Removed;
            }
        }

        /// <summary>Forgets what it recorded, and goes on hearing.</summary>
        public void Clear()
        {
            Tracks.Clear();
            tracks.Clear();
            runs.Clear();
            Version++;
        }

        public void Dispose()
        {
            gone.Clear();
            gone.AddRange(heard.Keys);
            foreach (var gas in gone) Forget(gas);
        }

        private TimelineTrack TrackOf(IGameplayAbilitySystem gas, ScriptableObject asset, TimelineKind kind)
        {
            if (tracks.TryGetValue((gas, asset), out var track)) return track;
            track = new TimelineTrack { Entity = gas, EntityName = RuntimeDebugger.NameOf(gas), Asset = asset, Kind = kind };
            tracks[(gas, asset)] = track;
            Tracks.Add(track);
            return track;
        }

        // A span added, the oldest ended one going once the track holds its most
        private void Add(TimelineTrack track, TimelineSpan span)
        {
            track.Spans.Add(span);
            if (track.Spans.Count > MostSpans)
            {
                int oldest = track.Spans.FindIndex(each => !each.Open);
                if (oldest >= 0) track.Spans.RemoveAt(oldest);
            }
            Version++;
        }

        private void Activated(IGameplayAbilitySystem gas, AbilityCallbackStatus status)
        {
            var ability = status.Ability?.Base;
            if (!Recording || ability == null || status.Data == null) return;
            var run = new TimelineSpan { Start = Now };
            Add(TrackOf(gas, ability, TimelineKind.Ability), run);
            runs[status.Data] = run;
        }

        private void StageStarted(AbilityCallbackStatus status)
        {
            if (status.Data == null || !runs.TryGetValue(status.Data, out var run)) return;
            var stages = status.Ability?.Base?.Behaviour?.Stages;
            run.Stages.Add(new TimelineStage { Index = stages != null ? stages.IndexOf(status.Stage) : run.Stages.Count, Start = Now });
            Version++;
        }

        private void StageEnded(AbilityCallbackStatus status)
        {
            if (status.Data == null || !runs.TryGetValue(status.Data, out var run)) return;
            var stages = status.Ability?.Base?.Behaviour?.Stages;
            int index = stages != null ? stages.IndexOf(status.Stage) : -1;
            for (int i = run.Stages.Count - 1; i >= 0; i--)
            {
                var stage = run.Stages[i];
                if (!stage.Open || (index >= 0 && stage.Index != index)) continue;
                stage.End = Now;
                stage.Succeeded = status.Success;
                break;
            }
            Version++;
        }

        private void Ended(AbilityCallbackStatus status)
        {
            if (status.Data == null || !runs.Remove(status.Data, out var run)) return;
            float now = Now;
            foreach (var stage in run.Stages)
                if (stage.Open) stage.End = now;
            run.End = now;
            Version++;
        }

        private void Refused(IGameplayAbilitySystem gas, ActivationRefusal refusal)
        {
            if (!Recording || refusal.Ability == null) return;
            float now = Now;
            var reason = refusal.Reason == EActivationRefusal.Rule && !string.IsNullOrEmpty(refusal.Rule)
                ? refusal.Rule
                : UnityEditor.ObjectNames.NicifyVariableName(refusal.Reason.ToString());
            Add(TrackOf(gas, refusal.Ability, TimelineKind.Ability), new TimelineSpan { Start = now, End = now, Refused = true, Note = reason });
        }

        private void Applied(IGameplayAbilitySystem gas, GameplayEffectSpec spec)
        {
            var effect = spec?.Base;
            if (!Recording || effect == null) return;
            float now = Now;
            var track = TrackOf(gas, effect, TimelineKind.Effect);
            if (effect.DurationSpecification?.DurationPolicy == EEffectDurationPolicy.Instant)
            {
                Add(track, new TimelineSpan { Start = now, End = now });
                return;
            }

            // Once on the shelf, an effect's span runs until no container of it is left there: a stack or a refresh
            // applies it again within the one span
            if (track.Spans.Count == 0 || !track.Spans[^1].Open) Add(track, new TimelineSpan { Start = now });
        }

        private void Removed(IGameplayAbilitySystem gas, GameplayEffect effect)
        {
            if (effect == null || !tracks.TryGetValue((gas, effect), out var track)) return;
            if (track.Spans.Count == 0 || !track.Spans[^1].Open) return;
            var shelf = gas.GetEffectSystem()?.Shelf;
            if (shelf != null)
                for (int i = 0; i < shelf.Count; i++)
                    if (shelf[i].Spec.Base == effect)
                        return;
            track.Spans[^1].End = Now;
            Version++;
        }
    }
}
