using System;
using System.Collections.Generic;
using System.Text;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>What an entity's combat log writes (TW-22), as the project subscribes to it.</summary>
    [Flags]
    public enum ECombatLogRecords
    {
        None = 0,

        /// <summary>Each hit it takes: a workable change another entity's effect makes to its attributes.</summary>
        HitsTaken = 1 << 0,

        /// <summary>Each hit it deals another.</summary>
        HitsDealt = 1 << 1,

        /// <summary>Each other change to its attributes: its own, a cost, regeneration, a transfer, an unworkable change.</summary>
        Changes = 1 << 2,
    }

    /// <summary>
    /// A project's combat log (TW-22, Decision 62): what each entity's writes, which the project subscribes to, and how
    /// many entries each keeps, its last.
    /// </summary>
    [Serializable]
    public class CombatLogSettings
    {
        [Tooltip("How many entries each entity keeps, its last; 0 keeps none, as for a build")]
        [Min(0)] public int Keep = 100;

        [Tooltip("What each entity's log writes: the hits it takes, the hits it deals, and its other attribute changes")]
        public ECombatLogRecords Records = ECombatLogRecords.HitsTaken | ECombatLogRecords.HitsDealt;

        [Tooltip("The gameplay events it writes as the entity is sent them, by tag, or one under it: Kill, Died, your own")]
        [ForgeTagContext(ForgeContext.Event)]
        public List<Tag> Events = new();
    }

    /// <summary>What a combat log entry is (TW-22).</summary>
    public enum ECombatLogKind
    {
        /// <summary>A hit the entity took.</summary>
        Taken,

        /// <summary>A hit the entity dealt another.</summary>
        Dealt,

        /// <summary>Another change to its attributes.</summary>
        Changed,

        /// <summary>A gameplay event it was sent.</summary>
        Event,
    }

    /// <summary>
    /// One entry of an entity's combat log (TW-22): when, what kind, who else, the effect behind it, the attribute, the value
    /// before and the change, its run through its pipeline (TW-2), and, for a hit, whether it killed.
    /// </summary>
    public readonly struct CombatLogEntry
    {
        private static readonly ValueStep[] NoSteps = Array.Empty<ValueStep>();
        private readonly ValueStep[] steps;

        internal CombatLogEntry(ECombatLogKind kind, float time, int frame, ITarget other, GameplayEffect effect,
            IHasReadableDefinition source, IAttribute attribute, AttributeValue before, AttributeValue change, Tag eventTag,
            ValueStep[] steps, bool killed)
        {
            Kind = kind;
            Time = time;
            Frame = frame;
            Other = other;
            Effect = effect;
            Source = source;
            Attribute = attribute;
            Before = before;
            Change = change;
            Event = eventTag;
            this.steps = steps;
            Killed = killed;
        }

        public ECombatLogKind Kind { get; }

        /// <summary>The game's time as it was written, in seconds.</summary>
        public float Time { get; }

        /// <summary>The frame it was written in.</summary>
        public int Frame { get; }

        /// <summary>Who else: the dealer of a hit taken, the one a hit dealt struck, an event's source, or the one an event names.</summary>
        public ITarget Other { get; }

        /// <summary>The effect the change came from, where it came from one; null otherwise.</summary>
        public GameplayEffect Effect { get; }

        /// <summary>What made the change, where it wasn't an effect, as a worker's or a cost's ability; or the effect again.</summary>
        public IHasReadableDefinition Source { get; }

        /// <summary>The attribute it changed; null for an event.</summary>
        public IAttribute Attribute { get; }

        /// <summary>The attribute's value before it.</summary>
        public AttributeValue Before { get; }

        /// <summary>What it changed the value by.</summary>
        public AttributeValue Change { get; }

        /// <summary>The attribute's value after it.</summary>
        public AttributeValue After => Before + Change;

        /// <summary>The event's tag, for an event.</summary>
        public Tag Event { get; }

        /// <summary>A hit's run through its pipeline, each stage's before and after (TW-2); empty where none ran.</summary>
        public IReadOnlyList<ValueStep> Steps => steps ?? NoSteps;

        /// <summary>Whether this hit killed the entity it struck.</summary>
        public bool Killed { get; }

        internal CombatLogEntry AsKilling() =>
            new(Kind, Time, Frame, Other, Effect, Source, Attribute, Before, Change, Event, steps, true);

        /// <summary>Its run in a line, as "-10 → × 2 -20", or empty.</summary>
        public string BreakdownText
        {
            get
            {
                if (steps == null || steps.Length == 0) return "";
                var text = new StringBuilder(Number(steps[0].Before));
                foreach (var step in steps)
                    text.Append($" → {step.Stage} {Number(step.After)}{(string.IsNullOrEmpty(step.Note) ? "" : $" ({step.Note})")}");
                return text.ToString();
            }
        }

        /// <summary>
        /// The entry in a line: "Took -20 Health from Attacker (Fireball)", "Dealt -20 Health to Dummy (Fireball)",
        /// "-5 Mana (Bolt)", "Died, from Attacker".
        /// </summary>
        public override string ToString()
        {
            string with = Name(Effect != null ? Effect : Source);
            string by = string.IsNullOrEmpty(with) ? "" : $" ({with})";
            string killed = Killed ? ", killing" : "";
            return Kind switch
            {
                ECombatLogKind.Taken => $"Took {ChangeText} from {Name(Other)}{by}{killed}",
                ECombatLogKind.Dealt => $"Dealt {ChangeText} to {Name(Other)}{by}{killed}",
                ECombatLogKind.Changed => $"{ChangeText}{by}",
                _ => Other != null ? $"{Event}, {Name(Other)}" : Event.ToString(),
            };
        }

        /// <summary>The change and its attribute, as "-20 Health"; a change to the base alone, as "-20 Health (base)".</summary>
        public string ChangeText
        {
            get
            {
                string name = Attribute?.GetName() ?? "";
                if (Mathf.Approximately(Change.CurrentValue, 0f) && !Mathf.Approximately(Change.BaseValue, 0f))
                    return $"{Signed(Change.BaseValue)} {name} (base)";
                return $"{Signed(Change.CurrentValue)} {name}";
            }
        }

        internal static string Name(IHasReadableDefinition named)
        {
            if (named == null) return "";
            if (named is UnityEngine.Object unity && !unity) return "";
            string name = named.GetName();
            return string.IsNullOrWhiteSpace(name) && named is UnityEngine.Object asset ? asset.name : name ?? "";
        }

        private static string Signed(float value) => value.ToString("+0.##;-0.##;0", System.Globalization.CultureInfo.InvariantCulture);
        private static string Number(float value) => value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// An entity's combat log (TW-22, Decision 62): the entries the project subscribes to, its last as many as the project
    /// keeps, oldest first; a test reads it as the game does, and the Debugger shows it.
    /// </summary>
    public sealed class CombatLog
    {
        private CombatLogEntry[] ring = Array.Empty<CombatLogEntry>();
        private int start;

        /// <summary>How many entries it holds.</summary>
        public int Count { get; private set; }

        /// <summary>How many entries it has written, those it no longer keeps among them.</summary>
        public int Written { get; private set; }

        /// <summary>Its entries, oldest first: a list of its own.</summary>
        public IReadOnlyList<CombatLogEntry> Entries
        {
            get
            {
                var entries = new List<CombatLogEntry>(Count);
                for (int i = 0; i < Count; i++) entries.Add(ring[(start + i) % ring.Length]);
                return entries;
            }
        }

        /// <summary>Lets every entry go.</summary>
        public void Clear()
        {
            start = 0;
            Count = 0;
            if (ring.Length > 0) Array.Clear(ring, 0, ring.Length);
        }

        /// <summary>Its entries of a kind, oldest first.</summary>
        public IEnumerable<CombatLogEntry> Of(ECombatLogKind kind)
        {
            foreach (var entry in Entries)
                if (entry.Kind == kind) yield return entry;
        }

        /// <summary>Its entries that an effect made, oldest first.</summary>
        public IEnumerable<CombatLogEntry> By(GameplayEffect effect)
        {
            foreach (var entry in Entries)
                if (effect != null && entry.Effect == effect) yield return entry;
        }

        /// <summary>Its entries with another entity, as a dealer or a target, oldest first.</summary>
        public IEnumerable<CombatLogEntry> With(ITarget other)
        {
            foreach (var entry in Entries)
                if (other != null && ReferenceEquals(entry.Other, other)) yield return entry;
        }

        /// <summary>What its entries of a kind changed an attribute by, together, as the hits it took to Health.</summary>
        public float Total(ECombatLogKind kind, IAttribute attribute)
        {
            float total = 0f;
            foreach (var entry in Entries)
                if (entry.Kind == kind && attribute != null && attribute.Equals(entry.Attribute)) total += entry.Change.CurrentValue;
            return total;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Writing, as the entity's core hears what happens to it
        // ═══════════════════════════════════════════════════════════════════════════

        // An entry, as the project keeps them: none where it keeps none, the oldest going where it's full
        internal void Write(in CombatLogEntry entry, int keep)
        {
            if (keep <= 0)
            {
                if (Count > 0) Clear();
                return;
            }
            if (ring.Length != keep) Resize(keep);
            if (Count < ring.Length)
            {
                ring[(start + Count) % ring.Length] = entry;
                Count++;
            }
            else
            {
                ring[start] = entry;
                start = (start + 1) % ring.Length;
            }
            Written++;
        }

        // Keeps its newest entries, as many as fit
        private void Resize(int keep)
        {
            var entries = Entries;
            ring = new CombatLogEntry[keep];
            start = 0;
            Count = 0;
            int from = Mathf.Max(0, entries.Count - keep);
            for (int i = from; i < entries.Count; i++) ring[Count++] = entries[i];
        }

        // A change to its attributes, as the project writes them: a workable hit another entity dealt it, or else any other
        // change that changed something
        internal void WriteReceived(in ImpactData impact, ITarget self)
        {
            var settings = ForgeCombatLog.Settings;
            if (settings == null) return;
            var dealer = impact.SourcedModifier.Source;
            if (impact.SourcedModifier.ImpactIsWorkable && dealer != null && !ReferenceEquals(dealer, self))
            {
                if ((settings.Records & ECombatLogRecords.HitsTaken) != 0) Write(Of(ECombatLogKind.Taken, dealer, impact), settings.Keep);
                return;
            }
            if ((settings.Records & ECombatLogRecords.Changes) == 0) return;
            if (Mathf.Approximately(impact.RealImpact.CurrentValue, 0f) && Mathf.Approximately(impact.RealImpact.BaseValue, 0f)) return;
            Write(Of(ECombatLogKind.Changed, null, impact), settings.Keep);
        }

        // A workable hit it dealt another entity
        internal void WriteDealt(in ImpactData impact, ITarget self)
        {
            var settings = ForgeCombatLog.Settings;
            if (settings == null || (settings.Records & ECombatLogRecords.HitsDealt) == 0) return;
            var target = impact.Target;
            if (!impact.SourcedModifier.ImpactIsWorkable || target == null || ReferenceEquals(target, self)) return;
            Write(Of(ECombatLogKind.Dealt, target, impact), settings.Keep);
        }

        // A gameplay event it was sent, naming its source where that's another, or else its subject
        internal void WriteEvent(Tag eventTag, ProcessDataPacket data, ITarget self)
        {
            var settings = ForgeCombatLog.Settings;
            if (settings == null) return;
            ITarget other = null;
            if (data != null)
            {
                if (data.TryGet(Tags.SOURCE, EDataTarget.Primary, out ITarget source) && !ReferenceEquals(source, self)) other = source;
                else if (data.TryGet(Tags.EVENT_SUBJECT, EDataTarget.Primary, out ITarget subject) && !ReferenceEquals(subject, self)) other = subject;
            }
            Write(new CombatLogEntry(ECombatLogKind.Event, ForgeTime.Time, ForgeTime.FrameCount, other, null, null, null,
                default, default, eventTag, null, false), settings.Keep);
        }

        // An entry for a change: the effect behind it, or what else carried it, and its run through its pipeline, copied,
        // as its spec keeps one run and fills it again with the next
        private static CombatLogEntry Of(ECombatLogKind kind, ITarget other, in ImpactData impact)
        {
            var carrier = impact.SourcedModifier.Carrier;
            var spec = carrier as GameplayEffectSpec;
            var source = carrier?.GetReadableDefinition();
            var effect = spec != null ? spec.Base : source as GameplayEffect;
            ValueStep[] steps = null;
            if (spec?.Breakdown is { } breakdown && breakdown.Steps.Count > 0)
            {
                steps = new ValueStep[breakdown.Steps.Count];
                for (int i = 0; i < steps.Length; i++) steps[i] = breakdown.Steps[i];
            }
            return new CombatLogEntry(kind, ForgeTime.Time, ForgeTime.FrameCount, other, effect, effect != null ? effect : source,
                impact.Attribute, impact.OldValue, impact.RealImpact, default, steps, false);
        }

        // Marks its newest entry of a kind with another as the blow that killed: the hit it took from its killer, or the
        // hit it dealt the one it killed
        internal void MarkKilling(ECombatLogKind kind, ITarget other)
        {
            for (int i = Count - 1; i >= 0; i--)
            {
                int at = (start + i) % ring.Length;
                var entry = ring[at];
                if (entry.Kind != kind || !ReferenceEquals(entry.Other, other)) continue;
                ring[at] = entry.AsKilling();
                return;
            }
        }
    }

    /// <summary>The current project's combat log settings, which each entity's log reads as it writes (TW-22).</summary>
    public static class ForgeCombatLog
    {
        /// <summary>The current project's settings, or null where there's no project.</summary>
        public static CombatLogSettings Settings => ForgeProject.Current is { } project ? project.Settings?.CombatLog : null;

        /// <summary>Whether entities keep a log at all.</summary>
        public static bool Keeps => Settings is { Keep: > 0 };

        /// <summary>Whether the project's logs write gameplay events under <paramref name="eventTag"/>, or a tag above it.</summary>
        public static bool Writes(Tag eventTag)
        {
            var settings = Settings;
            if (settings == null || settings.Keep <= 0 || settings.Events == null || !eventTag.IsValid) return false;
            for (int i = 0; i < settings.Events.Count; i++)
                if (settings.Events[i].IsValid && eventTag.MatchesOrIsChildOf(settings.Events[i])) return true;
            return false;
        }
    }
}
