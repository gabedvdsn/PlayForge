using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// How far from its caster an ability's target may be: a capability an ability's component implements, as Cast Range
    /// does. The targeting tasks that search from their caster, the Creator Lab's bots and a game's own code ask an
    /// ability's reach through <see cref="ForgeReach"/>, never the component, so a reach of your own is asked as Cast
    /// Range is. A component that reaches enforces its reach itself, as Cast Range refuses a target beyond it.
    /// </summary>
    public interface IAbilityReach
    {
        /// <summary>
        /// How far the ability reaches, read at its level in an activation's data, or at its holder's level of it where
        /// the data is null, and whether along the ground, leaving height out; false where it reaches anywhere.
        /// </summary>
        bool TryReadReach(AbilitySpec spec, ProcessDataPacket data, out float range, out bool flat);
    }

    /// <summary>
    /// What reads an ability's reach, its first <see cref="IAbilityReach"/> component's: how far it reaches, and whether a
    /// target or a point lies beyond it, from its caster. An ability with none reaches anywhere. Each read that takes a
    /// reach reads that one, as a component of more than one reach asks its own.
    /// </summary>
    public static class ForgeReach
    {
        /// <summary>
        /// How far an activation's ability reaches, read at its level in the activation's data, and whether along the
        /// ground; false where it has no reach, or one that reaches anywhere.
        /// </summary>
        public static bool TryRead(AbilityDataPacket data, out float range, out bool flat) =>
            TryRead(ReachOf(data), data, out range, out flat);

        /// <summary>How far an activation's ability reaches, as <see cref="TryRead(AbilityDataPacket, out float, out bool)"/>.</summary>
        public static bool TryRead(AbilityDataPacket data, out float range) => TryRead(ReachOf(data), data, out range, out _);

        /// <summary>
        /// How far a reach reaches for an activation of its ability, read at its level in the activation's data, and
        /// whether along the ground; false where it reaches anywhere, or the data is no activation's.
        /// </summary>
        public static bool TryRead(IAbilityReach reach, AbilityDataPacket data, out float range, out bool flat)
        {
            range = 0f;
            flat = true;
            return reach != null && data?.EffectOrigin is AbilitySpec spec && reach.TryReadReach(spec, data, out range, out flat);
        }

        /// <summary>
        /// How far an entity's ability reaches, at the entity's level of it; false where it has no reach, one that reaches
        /// anywhere, or the entity doesn't know it.
        /// </summary>
        public static bool TryRead(IGameplayAbilitySystem caster, Ability ability, out float range)
        {
            range = 0f;
            if (ability == null || ability.Find<IAbilityReach>() is not { } reach || caster?.GetAbilitySystem() is not { } abilities
                || !abilities.TryGetAbilityContainer(ability, out var container) || container?.Spec == null) return false;
            return reach.TryReadReach(container.Spec, null, out range, out _);
        }

        /// <summary>Whether a point lies beyond an activation's reach from its caster; false where it has none.</summary>
        public static bool Beyond(AbilityDataPacket data, Vector3 point) => Beyond(ReachOf(data), data, point);

        /// <summary>Whether a point lies beyond a reach, from the caster of the activation it's read for; false where it reaches anywhere.</summary>
        public static bool Beyond(IAbilityReach reach, AbilityDataPacket data, Vector3 point)
        {
            if (!TryRead(reach, data, out float range, out bool flat)) return false;
            if (data.EffectOrigin.GetSource()?.GetTargetingPacket() is not { } caster) return false;
            var offset = point - caster.position;
            if (flat) offset.y = 0f;
            return offset.sqrMagnitude > range * range;
        }

        /// <summary>
        /// Whether the target or the point an activation was handed, under Target Real or Position, as an AI hands it,
        /// lies beyond a reach; false where it was handed neither.
        /// </summary>
        public static bool HandedBeyond(IAbilityReach reach, AbilityDataPacket data)
        {
            if (reach == null || data == null) return false;
            if (data.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out ITarget target) && target?.GetTargetingPacket() is { } packet)
                return Beyond(reach, data, packet.position);
            return data.TryGet(Tags.POSITION, EDataTarget.Primary, out Vector3 point) && Beyond(reach, data, point);
        }

        /// <summary>
        /// Whether the target its targeting picked, its first under Target, lies beyond a reach; false where it picked
        /// none.
        /// </summary>
        public static bool PickedBeyond(IAbilityReach reach, AbilityDataPacket data) =>
            reach != null && data != null && data.TryGetFirst(Tags.TARGET, out AbstractTargetingPacket picked) && picked != null
            && Beyond(reach, data, picked.position);

        // An activation's ability's reach, or null
        private static IAbilityReach ReachOf(AbilityDataPacket data) =>
            data?.EffectOrigin is AbilitySpec spec ? spec.Base?.Find<IAbilityReach>() : null;
    }
}
