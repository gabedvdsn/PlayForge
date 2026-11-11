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
        /// What an area search around <paramref name="origin"/> finds, added to <paramref name="into"/>. With no range, or
        /// no origin, every live entity. With a range, what physics finds on <paramref name="layers"/>, as it did, and
        /// each runtime entity whose targeting packet is within the range. The searcher, <paramref name="owner"/>, only
        /// where <paramref name="includeOwner"/> says.
        /// </summary>
        public static void Around(AbstractTargetingPacket origin, float range, LayerMask layers, ISource owner, bool includeOwner,
            List<ITarget> into)
        {
            if (range > 0f && origin != null)
            {
                foreach (var collider in Physics.OverlapSphere(origin.position, range, layers))
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
                    if (packet == null || (packet.position - origin.position).sqrMagnitude > most) continue;
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
