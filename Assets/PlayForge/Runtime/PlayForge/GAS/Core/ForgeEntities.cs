using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Every live entity, on both hubs: each joins as its core initializes and leaves as it ends. An area search goes
    /// through it, as Auto Target and Auto Target by Affinity do: with no range they searched the scene for MonoBehaviour
    /// entities alone, so a runtime entity was never found; and with a range they searched with physics alone, which finds
    /// no runtime entity, as it has no collider (GAS-75).
    /// </summary>
    public static class ForgeEntities
    {
        private static readonly List<IGameplayAbilitySystem> live = new();

        /// <summary>Every live entity, in the order each joined.</summary>
        public static IReadOnlyList<IGameplayAbilitySystem> Live => live;

        // Each session starts with none, as it did with a domain reload (SP-22)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => live.Clear();

        internal static void Add(IGameplayAbilitySystem entity)
        {
            if (entity != null && !live.Contains(entity)) live.Add(entity);
        }

        internal static void Remove(IGameplayAbilitySystem entity) => live.Remove(entity);

        /// <summary>
        /// Every live entity within <paramref name="radius"/> of <paramref name="centre"/>, by its position, on either hub,
        /// added to <paramref name="into"/>, with no physics, so an entity needs no collider (TW-10, Decision 50). Flat
        /// leaves height out. A radius of 0 or less takes every live entity. The searcher, <paramref name="owner"/>, only
        /// where <paramref name="includeOwner"/> says; GameRoot, the game's manager, never.
        /// </summary>
        public static void Within(Vector3 centre, float radius, bool flat, List<ITarget> into, ISource owner = null, bool includeOwner = true)
        {
            float most = radius * radius;
            foreach (var entity in live)
            {
                if (entity is not ITarget target) continue;
                // One destroyed without ending, which a MonoBehaviour can be
                if (entity is Object unityObject && !unityObject) continue;
                // The game's manager, GameRoot, is no area's to take: physics never found it, as it has no collider
                if (entity is IManagerial) continue;
                if (!includeOwner && ReferenceEquals(entity, owner)) continue;
                if (radius > 0f)
                {
                    var packet = entity.GetTargetingPacket();
                    if (packet == null) continue;
                    var offset = packet.position - centre;
                    if (flat) offset.y = 0f;
                    if (offset.sqrMagnitude > most) continue;
                }
                if (!into.Contains(target)) into.Add(target);
            }
        }

        /// <summary>
        /// Whether <paramref name="target"/> is of the sides <paramref name="filter"/> takes, from <paramref name="sides"/>,
        /// its searcher's: an ally shares a side, an enemy shares none. A searcher with no sides takes anyone (TW-10).
        /// </summary>
        public static bool Takes(EAffinityFilter filter, List<Tag> sides, ITarget target)
        {
            if (filter == EAffinityFilter.AlliesAndEnemies || sides == null || sides.Count == 0) return true;
            var theirs = target?.GetAffiliation();
            bool shares = false;
            if (theirs != null)
            {
                foreach (var side in sides)
                {
                    if (!theirs.Contains(side)) continue;
                    shares = true;
                    break;
                }
            }
            return filter == EAffinityFilter.Allies ? shares : !shares;
        }

        /// <summary>How far <paramref name="target"/> is from <paramref name="centre"/>, flat or not, or the most a float holds where it has no position.</summary>
        public static float Distance(ITarget target, Vector3 centre, bool flat)
        {
            var packet = target?.GetTargetingPacket();
            if (packet == null) return float.MaxValue;
            var offset = packet.position - centre;
            if (flat) offset.y = 0f;
            return offset.magnitude;
        }

        /// <summary>
        /// What an area search around <paramref name="origin"/> finds, added to <paramref name="into"/>. With no range, or
        /// no origin, every live entity. With a range, what physics finds on <paramref name="layers"/>, as it did, and
        /// each runtime entity whose targeting packet is within the range. The searcher, <paramref name="owner"/>, only
        /// where <paramref name="includeOwner"/> says.
        /// </summary>
        public static void Around(AbstractTargetingPacket origin, float range, LayerMask layers, ISource owner, bool includeOwner,
            List<ITarget> into) => Around(origin?.position, range, layers, owner, includeOwner, into);

        /// <summary>
        /// What an area search with physics around <paramref name="centre"/> finds, as <see cref="Around(AbstractTargetingPacket, float, LayerMask, ISource, bool, List{ITarget})"/>
        /// does around a packet: what physics finds on <paramref name="layers"/>, and each runtime entity by its position.
        /// </summary>
        public static void Around(Vector3? centre, float range, LayerMask layers, ISource owner, bool includeOwner, List<ITarget> into)
        {
            if (range > 0f && centre.HasValue)
            {
                var position = centre.Value;
                foreach (var collider in Physics.OverlapSphere(position, range, layers))
                {
                    foreach (var target in collider.GetComponents<ITarget>())
                    {
                        if (!includeOwner && ReferenceEquals(target, owner)) continue;
                        if (!into.Contains(target)) into.Add(target);
                    }
                }

                // A runtime entity has no collider for physics to find: it's found where its targeting packet is. A
                // MonoBehaviour entity is physics', on its layers, as before
                float most = range * range;
                foreach (var entity in live)
                {
                    if (entity is Object || entity is not ITarget target) continue;
                    if (!includeOwner && ReferenceEquals(entity, owner)) continue;
                    var packet = entity.GetTargetingPacket();
                    if (packet == null || (packet.position - position).sqrMagnitude > most) continue;
                    if (!into.Contains(target)) into.Add(target);
                }
                return;
            }

            foreach (var entity in live)
            {
                if (entity is not ITarget target) continue;
                // One destroyed without ending, which a MonoBehaviour can be
                if (entity is Object unityObject && !unityObject) continue;
                if (!includeOwner && ReferenceEquals(entity, owner)) continue;
                if (!into.Contains(target)) into.Add(target);
            }
        }
    }
}
