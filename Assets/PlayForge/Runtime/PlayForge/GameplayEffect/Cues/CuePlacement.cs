using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Whom a cue places what it plays on.</summary>
    public enum ECueAt
    {
        /// <summary>
        /// The one the cue is on: an effect's target, an ability's caster, the entity, an item's holder; at the cue's
        /// point where its raiser gave one, as a hit's.
        /// </summary>
        Self,

        /// <summary>Who's behind it, as an effect's source or a death's killer; else the one it's on.</summary>
        Source,

        /// <summary>
        /// Whom an ability aimed at, its first target, or the place it targeted; for another's cue, the one it's on.
        /// </summary>
        Target,

        /// <summary>Each of an ability's targets, a place for each; else as Target.</summary>
        EachTarget,

        /// <summary>The cue's point, as a hit's, or the place an ability targeted; else the one it's on.</summary>
        Point,
    }

    /// <summary>Whether what a cue places follows what it's placed at.</summary>
    public enum ECueAttach
    {
        /// <summary>A lasting cue's follows; a burst's stays where it was placed.</summary>
        WhileLasting,

        /// <summary>Follows, a burst's as well.</summary>
        Always,

        /// <summary>Stays where it was placed.</summary>
        Never,
    }

    /// <summary>Which way what a cue places faces.</summary>
    public enum ECueFacing
    {
        /// <summary>As what it's placed at faces; at a point, along the cue's normal where it has one.</summary>
        Anchor,

        /// <summary>Along the cue's normal, as off a hit surface; else as Anchor.</summary>
        Normal,

        /// <summary>Away from the cue's source, as a spark flies from the blow; else as Anchor.</summary>
        AwayFromSource,

        /// <summary>Toward the cue's source; else as Anchor.</summary>
        TowardSource,

        /// <summary>The world's way, unturned.</summary>
        World,
    }

    /// <summary>One place a cue plays at: where, which way, what it follows, and the entity it's at.</summary>
    public readonly struct CuePlace
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        /// <summary>What it follows, where it's attached; else null.</summary>
        public readonly Transform Parent;

        /// <summary>The entity it's at; null at a point.</summary>
        public readonly IGameplayAbilitySystem On;

        public CuePlace(Vector3 position, Quaternion rotation, Transform parent, IGameplayAbilitySystem on)
        {
            Position = position;
            Rotation = rotation;
            Parent = parent;
            On = on;
        }
    }

    /// <summary>
    /// Where a cue places what it plays: on the one it's on, its source, an ability's target or each of its targets, or
    /// at the cue's point; at a socket of theirs, as a hand; moved by an offset in their space and scattered by a random
    /// amount; following them or not; and facing their way, the cue's normal, or from or toward its source.
    /// </summary>
    [Serializable]
    public class CuePlacement
    {
        [Tooltip("On whom it's placed: the one the cue is on, its source, an ability's target or each of its targets, or " +
                 "the cue's point")]
        public ECueAt At = ECueAt.Self;

        [Tooltip("A child of theirs it's placed at, by name, found at any depth, as a hand or a weapon's tip; them where " +
                 "it's empty or not found")]
        public string Socket;

        [Tooltip("Moved by this much, in the space of what it's placed at")]
        public Vector3 Offset;

        [Tooltip("And by a random amount, up to this much either way on each axis, in the same space")]
        public Vector3 Scatter;

        [Tooltip("Whether it follows what it's placed at: a lasting cue's only, always, or never")]
        public ECueAttach Attach = ECueAttach.WhileLasting;

        [Tooltip("Which way it faces: as what it's placed at, along the cue's normal, away from or toward its source, or " +
                 "the world's way")]
        public ECueFacing Facing = ECueFacing.Anchor;

        /// <summary>
        /// Where the cue plays: one place, or one for each of an ability's targets. A lasting cue's follows what it's
        /// placed at, as its Attach says.
        /// </summary>
        public List<CuePlace> Resolve(in CueContext cue, bool lasting)
        {
            var places = new List<CuePlace>(1);
            switch (At)
            {
                case ECueAt.Source:
                    AddAt(places, cue, cue.Source as IGameplayAbilitySystem ?? cue.Target, null, lasting);
                    break;
                case ECueAt.Target:
                    AddTarget(places, cue, lasting);
                    break;
                case ECueAt.EachTarget:
                    if (cue.Data != null && cue.Data.TryGetAll(Tags.TARGET_REAL, out DataValue<ITarget> targets))
                        foreach (var target in targets)
                            if (target is IGameplayAbilitySystem entity)
                                AddAt(places, cue, entity, null, lasting);
                    if (places.Count == 0) AddTarget(places, cue, lasting);
                    break;
                case ECueAt.Point:
                    AddAt(places, cue, cue.Target, cue.Point ?? TargetedPoint(cue), lasting);
                    break;
                default:
                    AddAt(places, cue, cue.Target, cue.Point, lasting);
                    break;
            }
            return places;
        }

        // An ability's first target where it's an entity; else the place it targeted; else the one the cue is on
        private void AddTarget(List<CuePlace> places, in CueContext cue, bool lasting)
        {
            if (cue.Data != null && cue.Data.TryGetFirst(Tags.TARGET_REAL, out ITarget target) && target is IGameplayAbilitySystem entity)
                AddAt(places, cue, entity, null, lasting);
            else AddAt(places, cue, cue.Target, TargetedPoint(cue), lasting);
        }

        // The place an ability's activation targeted, where it targeted one
        private static Vector3? TargetedPoint(in CueContext cue)
        {
            if (cue.Data is AbilityDataPacket ability && ability.GetTargetingPacket(Tags.TARGET, out var packet, autoInterrupt: false) &&
                packet != null)
                return packet.position;
            if (cue.Data != null && cue.Data.TryGet(Tags.POSITION, EDataTarget.Primary, out Vector3 position)) return position;
            return null;
        }

        // At a point, or at the entity's socket, moved and scattered in its space, facing as asked, following it as asked
        private void AddAt(List<CuePlace> places, in CueContext cue, IGameplayAbilitySystem on, Vector3? point, bool lasting)
        {
            var anchor = point.HasValue ? null : SocketOf(on);
            var position = point ?? (anchor != null ? anchor.position : Vector3.zero);
            var rotation = anchor != null ? anchor.rotation : Quaternion.identity;

            var moved = Offset;
            if (Scatter != Vector3.zero)
                moved += new Vector3(Spread(Scatter.x), Spread(Scatter.y), Spread(Scatter.z));
            position += rotation * moved;

            bool follows = anchor != null && (Attach == ECueAttach.Always || (Attach == ECueAttach.WhileLasting && lasting));
            places.Add(new CuePlace(position, FacingOf(cue, position, rotation, point.HasValue), follows ? anchor : null,
                point.HasValue ? null : on));
        }

        private Quaternion FacingOf(in CueContext cue, Vector3 position, Quaternion anchor, bool atPoint)
        {
            var normal = cue.Normal is { } given && given != Vector3.zero ? Quaternion.LookRotation(given) : (Quaternion?)null;
            var source = cue.Source is Component behind && behind != null ? behind.transform.position : (Vector3?)null;
            switch (Facing)
            {
                case ECueFacing.World:
                    return Quaternion.identity;
                case ECueFacing.Normal:
                    return normal ?? anchor;
                case ECueFacing.AwayFromSource when source.HasValue && position != source.Value:
                    return Quaternion.LookRotation(position - source.Value);
                case ECueFacing.TowardSource when source.HasValue && position != source.Value:
                    return Quaternion.LookRotation(source.Value - position);
                default:
                    return atPoint && normal.HasValue ? normal.Value : anchor;
            }
        }

        // A number up to this much either way, from the cues' own random numbers
        private static float Spread(float most) => most == 0f ? 0f : ForgeCues.Random.Value * 2f * most - most;

        // The entity's transform at its socket, found at any depth; itself where none is named or found; null for one
        // with no place
        private Transform SocketOf(IGameplayAbilitySystem on)
        {
            var root = on is Component component && component != null ? component.transform : null;
            if (root == null || string.IsNullOrEmpty(Socket)) return root;
            return Find(root, Socket) ?? root;
        }

        private static Transform Find(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var below = Find(child, name);
                if (below != null) return below;
            }
            return null;
        }
    }
}
