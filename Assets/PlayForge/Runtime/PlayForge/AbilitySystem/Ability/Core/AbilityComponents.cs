using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Charges: how many uses an ability holds, each spending a charge that comes back as its Recharge says (TW-17,
    /// FN-14). Without one, an ability has one charge, an ordinary cooldown. An ability holds one at most.
    /// </summary>
    [Serializable]
    public sealed class ChargesAbilityComponent : AbstractAbilityComponent
    {
        [Tooltip("How many uses it holds, each spending a charge that comes back as its Recharge says; 1 is an ordinary " +
                 "cooldown (TW-17)")]
        public ScalableInt Charges = 2;

        [SerializeReference]
        [Tooltip("How its spent charges come back: each on a cooldown of its own, or all at once as one cooldown ends once " +
                 "they're all spent, or as a recharge of your own says (TW-17)")]
        public AbstractChargeRecharge Recharge = new EachChargeRecharge();

        public override bool AllowsSeveral => false;
    }

    /// <summary>
    /// Toggle: an effect an ability turns on its caster with one use, paying its cost, and off with the next, starting its
    /// cooldown (TW-20, FN-14). As its holder forgets the ability, the effect comes off, where it stayed on. An ability
    /// holds one at most.
    /// </summary>
    [Serializable]
    public sealed class ToggleAbilityComponent : AbstractAbilityComponent
    {
        [Tooltip("The effect it toggles on its caster: used, it puts the effect on, paying its cost; used again, it takes " +
                 "the effect off, paying and asking nothing, and starts its cooldown (TW-20)")]
        public GameplayEffect Effect;

        public override bool AllowsSeveral => false;

        public override void Removed(AbilityComponentContext ctx) => ctx.Spec?.ToggleOff();
    }

    /// <summary>
    /// Cast Range: how far from its caster an ability's target may be, the one range it has, read at its level (FN-14,
    /// Decision 77). An activation handed a target or a point beyond it is refused as out of range; targeting that picks
    /// one beyond it ends the activation there; a targeting task with a range of its own searches within it, from the
    /// caster; and the lab's bots cast it only once their foe is within it. 0 reaches anywhere. An ability holds one at
    /// most.
    /// </summary>
    [Serializable]
    public sealed class CastRangeAbilityComponent : AbstractAbilityComponent
    {
        [Tooltip("How far from its caster its target or point may be, read at the ability's level; 0 reaches anywhere")]
        public ScalableFloat Range = 6f;

        [Tooltip("Measures along the ground, leaving height out; off, a sphere")]
        public bool Flat = true;

        public override bool AllowsSeveral => false;

        // Handed a target or a point beyond it, as an AI's activation hands it, the activation doesn't start, so nothing of
        // it is paid
        public override string CanStart(AbilityComponentContext ctx) =>
            ctx.Data is AbilityDataPacket data && ForgeCastRange.HandedBeyond(data) ? ForgeCastRange.Refusal : null;
    }

    /// <summary>
    /// What reads an ability's Cast Range (FN-14): how far it reaches, at the ability's level, and whether a target or a
    /// point lies beyond it, from its caster.
    /// </summary>
    public static class ForgeCastRange
    {
        /// <summary>The reason an activation handed a target beyond its Cast Range is refused with.</summary>
        public const string Refusal = "Its target is beyond its Cast Range";

        /// <summary>An ability's Cast Range component, or null where it has none.</summary>
        public static CastRangeAbilityComponent Of(Ability ability)
        {
            if (ability == null || ability.Components == null) return null;
            foreach (var component in ability.Components)
                if (component is CastRangeAbilityComponent cast) return cast;
            return null;
        }

        /// <summary>
        /// How far an activation's ability reaches, read at its level in the activation's data, and whether flat; false
        /// where it has no Cast Range, or one that reaches anywhere.
        /// </summary>
        public static bool TryRead(AbilityDataPacket data, out float range, out bool flat)
        {
            range = 0f;
            flat = true;
            if (data?.EffectOrigin is not AbilitySpec spec || Of(spec.Base) is not { } cast) return false;
            range = spec.Read(cast.Range, data);
            flat = cast.Flat;
            return range > 0f;
        }

        /// <summary>How far an activation's ability reaches, as <see cref="TryRead(AbilityDataPacket, out float, out bool)"/>.</summary>
        public static bool TryRead(AbilityDataPacket data, out float range) => TryRead(data, out range, out _);

        /// <summary>
        /// How far an entity's ability reaches, at the entity's level of it; false where it has no Cast Range, one that
        /// reaches anywhere, or the entity doesn't know it.
        /// </summary>
        public static bool TryRead(IGameplayAbilitySystem caster, Ability ability, out float range)
        {
            range = 0f;
            if (Of(ability) is not { } cast || caster?.GetAbilitySystem() is not { } abilities
                || !abilities.TryGetAbilityContainer(ability, out var container) || container?.Spec == null) return false;
            range = container.Spec.Read(cast.Range);
            return range > 0f;
        }

        /// <summary>Whether a point lies beyond an activation's Cast Range from its caster; false where it has none.</summary>
        public static bool Beyond(AbilityDataPacket data, Vector3 point)
        {
            if (!TryRead(data, out float range, out bool flat)) return false;
            if (data.EffectOrigin.GetSource()?.GetTargetingPacket() is not { } caster) return false;
            var offset = point - caster.position;
            if (flat) offset.y = 0f;
            return offset.sqrMagnitude > range * range;
        }

        // Whether the target or the point the activation was handed, under Target Real or Position, lies beyond it
        internal static bool HandedBeyond(AbilityDataPacket data)
        {
            if (data.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out ITarget target) && target?.GetTargetingPacket() is { } packet)
                return Beyond(data, packet.position);
            return data.TryGet(Tags.POSITION, EDataTarget.Primary, out Vector3 point) && Beyond(data, point);
        }

        // Whether the target its targeting picked, its first under Target, lies beyond it
        internal static bool PickedBeyond(AbilityDataPacket data) =>
            data.TryGetFirst(Tags.TARGET, out AbstractTargetingPacket picked) && picked != null && Beyond(data, picked.position);
    }
}
