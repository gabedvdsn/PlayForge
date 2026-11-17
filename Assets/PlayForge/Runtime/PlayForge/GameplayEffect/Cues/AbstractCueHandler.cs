using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What raised a cue: one of an effect's moments, an ability's, an entity's or an item's, or a game's own code.
    /// </summary>
    public enum ECueMoment
    {
        /// <summary>The game's own code raised it.</summary>
        Raised,

        /// <summary>An effect landed, a new application or one an effect on already took in.</summary>
        Applied,

        /// <summary>An effect's ticks executed.</summary>
        Ticked,

        /// <summary>An effect came off.</summary>
        Removed,

        /// <summary>An ability activated.</summary>
        Activated,

        /// <summary>An ability paid its cost and started its cooldown.</summary>
        Committed,

        /// <summary>An ability ended.</summary>
        Ended,

        /// <summary>An ability was refused.</summary>
        Refused,

        /// <summary>An entity came into the game.</summary>
        Spawned,

        /// <summary>An entity died.</summary>
        Died,

        /// <summary>An entity came back to life.</summary>
        Revived,

        /// <summary>An entity took on a tag, or let it go.</summary>
        Tagged,

        /// <summary>An item was equipped, or taken off.</summary>
        Equipped,

        /// <summary>An item was used.</summary>
        Used,

        /// <summary>An ability's stage came to its Play Cue task.</summary>
        Stage,
    }

    /// <summary>How a cue plays: a burst, or a lasting cue's beginning or its end.</summary>
    public enum ECueEvent
    {
        Burst,
        Begin,
        End,
    }

    /// <summary>
    /// A gameplay cue: what something shows and sounds, apart from what it does. An effect lists its cues as it lists
    /// its workers, and so do an ability's, an entity's and an item's Cues components, each for its own moments, and
    /// <see cref="ForgeCues"/> plays them all. A burst plays as a moment comes; a lasting cue begins as its moment
    /// comes and ends as it's over, as an effect that lands and later comes off, and a restore ends it and begins it
    /// again, playing no burst. Each hook is a no-op until a handler overrides it. An effect changed attributes and
    /// tags and nothing else, so what it showed was each game's own code.
    /// </summary>
    [Serializable]
    public abstract class AbstractCueHandler
    {
        /// <summary>
        /// A burst, as a moment comes: an effect landing, ticking or coming off, an ability committing, an entity
        /// dying. An effect's moments play the hooks below that name them, and any other moment plays <see
        /// cref="OnApplied"/>, so a handler written for effects plays wherever it's listed. Override it to play every
        /// burst one way.
        /// </summary>
        public virtual void Burst(CueContext cue)
        {
            switch (cue.Moment)
            {
                case ECueMoment.Ticked: OnTicked(cue); break;
                case ECueMoment.Removed: OnRemoved(cue); break;
                default: OnApplied(cue); break;
            }
        }

        /// <summary>As the effect lands, a burst: a new application, or one an effect on already takes in.</summary>
        public virtual void OnApplied(CueContext cue) { }

        /// <summary>As the effect's ticks execute, a burst, with how many executed.</summary>
        public virtual void OnTicked(CueContext cue) { }

        /// <summary>As the effect comes off, a burst.</summary>
        public virtual void OnRemoved(CueContext cue) { }

        /// <summary>
        /// The cue's moment has come and lasts: the effect is on, from its landing or its coming back with a restore,
        /// or what a Cues component lists as lasting has begun. A lasting cue begins.
        /// </summary>
        public virtual void Begin(CueContext cue) { }

        /// <summary>
        /// The cue's moment is over: the effect is off, as it goes or as a restore lets go of it. A lasting cue ends.
        /// </summary>
        public virtual void End(CueContext cue) { }
    }

    /// <summary>
    /// What a cue plays for: the entity it plays on, what raised it and why, and where. An effect's cue has its spec,
    /// and for a tick, how many executed; another's has its origin, as an ability's spec or an item's, and the
    /// activation's data where there is one. A lasting cue's beginning and its end share a key.
    /// </summary>
    public readonly struct CueContext
    {
        /// <summary>The entity the cue plays on.</summary>
        public readonly IGameplayAbilitySystem Target;

        /// <summary>The effect's spec, for an effect's cue.</summary>
        public readonly GameplayEffectSpec Spec;

        /// <summary>How many of the effect's ticks executed, for a tick's burst.</summary>
        public readonly int Ticks;

        /// <summary>What raised the cue.</summary>
        public readonly ECueMoment Moment;

        /// <summary>The cue's tag, where it was raised by one.</summary>
        public readonly Tag Tag;

        /// <summary>Who's behind it: the effect's or the ability's caster, or the entity itself.</summary>
        public readonly ISource Source;

        /// <summary>What raised it: an effect's origin, an ability's spec, an item's, or the entity.</summary>
        public readonly IEffectOrigin Origin;

        /// <summary>The activation's data, for an ability's cue.</summary>
        public readonly ProcessDataPacket Data;

        /// <summary>
        /// A number the raiser gives it, as a hit's damage, for a cue to scale with; 0 where it gave none.
        /// </summary>
        public readonly float Magnitude;

        /// <summary>Where it plays, where the raiser says: a hit's point; else the target's place.</summary>
        public readonly Vector3? Point;

        /// <summary>The direction it faces, where the raiser says: a hit's normal.</summary>
        public readonly Vector3? Normal;

        /// <summary>What pairs a lasting cue's beginning with its end, as an effect's spec.</summary>
        public readonly object Key;

        /// <summary>
        /// Whether it was raised to last: by a Cues component's list of lasting cues, as an ability's While Active, so
        /// a handler that could burst or last lasts. An effect's cues say for themselves, as a Prefab cue's While
        /// Active.
        /// </summary>
        public readonly bool Lasting;

        public CueContext(IGameplayAbilitySystem target, GameplayEffectSpec spec, int ticks = 0)
            : this(target, ECueMoment.Raised, spec: spec, ticks: ticks) { }

        public CueContext(IGameplayAbilitySystem target, ECueMoment moment, Tag tag = default, ISource source = null,
            IEffectOrigin origin = null, GameplayEffectSpec spec = null, ProcessDataPacket data = null, int ticks = 0,
            float magnitude = 0f, Vector3? point = null, Vector3? normal = null, object key = null, bool lasting = false)
        {
            Target = target;
            Moment = moment;
            Tag = tag;
            Source = source;
            Origin = origin;
            Spec = spec;
            Data = data;
            Ticks = ticks;
            Magnitude = magnitude;
            Point = point;
            Normal = normal;
            Key = key;
            Lasting = lasting;
        }

        /// <summary>
        /// Where the target is: a MonoBehaviour hub's transform, or null for a runtime GAS, which has no place.
        /// </summary>
        public Transform Transform => Target is Component component && component != null ? component.transform : null;

        /// <summary>
        /// Where it plays: the point its raiser gave, or else the target's position, or the world's origin.
        /// </summary>
        public Vector3 Position => Point ?? (Transform != null ? Transform.position : Vector3.zero);

        /// <summary>The same cue, under another tag, as a route hands it on.</summary>
        public CueContext WithTag(Tag tag) =>
            new(Target, Moment, tag, Source, Origin, Spec, Data, Ticks, Magnitude, Point, Normal, Key, Lasting);

        /// <summary>The same cue, raised to last or not.</summary>
        public CueContext AsLasting(bool lasting) =>
            new(Target, Moment, Tag, Source, Origin, Spec, Data, Ticks, Magnitude, Point, Normal, Key, lasting);
    }
}
