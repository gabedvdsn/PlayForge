using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// Who a process reaches: each live entity within a radius of a point that its caster's side takes, found by
    /// PlayForge's own area search, <see cref="ForgeEntities.Within"/>, by position on either hub and with no physics,
    /// so hundreds of enemies cost no colliders.
    /// </summary>
    public static class SurvivorsTeams
    {
        // The search's own list, used and emptied in one call
        private static readonly List<ITarget> found = new();

        /// <summary>
        /// Adds to <paramref name="into"/> each live entity within <paramref name="radius"/> of <paramref name="centre"/>,
        /// along the ground, that <paramref name="filter"/> takes by how it stands to <paramref name="caster"/>.
        /// </summary>
        public static void Around(ISource caster, Vector3 centre, float radius, EAffiliationFilter filter, List<ITarget> into)
        {
            // A caster gone from the scene reaches no one
            if (!Exists(caster)) return;
            found.Clear();
            bool itself = filter is EAffiliationFilter.Allies or EAffiliationFilter.Everyone or EAffiliationFilter.Self;
            ForgeEntities.Within(centre, radius, true, found, caster, itself);
            var sides = (caster as ITarget)?.GetAffiliation();
            // What the roster's Shield Tags shield, as a phased Wraith, nothing reaches, so a bolt isn't spent on it
            var shields = SurvivorsGame.Current && SurvivorsGame.Current.Roster ? SurvivorsGame.Current.Roster.ShieldTags : null;
            for (int i = 0; i < found.Count; i++)
            {
                var target = found[i];
                if (!Stands(target)) continue;
                if (shields is { Count: > 0 } && target is ISource entity && entity.GetTagCache() is { } tags && tags.HasAnyTag(shields)) continue;
                if (ForgeEntities.Takes(filter, caster, sides, target)) into.Add(target);
            }
            found.Clear();
        }

        /// <summary>
        /// Whether an entity is still in the scene: a fallen enemy's body is destroyed as the horde takes it out, while a
        /// volley, a homing bolt or a process may still hold it.
        /// </summary>
        public static bool Exists(object entity) => entity != null && !(entity is Object unity && !unity);

        /// <summary>Whether an entity stands: still in the scene, and alive.</summary>
        public static bool Stands(ITarget target) => Exists(target) && !target.IsDead;

        /// <summary>Where an entity stands on the ground, or the arena's middle for one that's gone.</summary>
        public static Vector3 PlaceOf(ITarget target) => Exists(target) && target.GetTargetingPacket() is { } packet ? packet.position : Vector3.zero;
    }
}
