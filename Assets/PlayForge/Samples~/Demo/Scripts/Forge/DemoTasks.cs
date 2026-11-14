using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>Who an area's effects reach, by their side against the caster's.</summary>
    public enum EDemoAffects
    {
        Enemies,
        Allies,
        Everyone,
    }

    /// <summary>The sides of the arena, by affiliation, and what stands in an area.</summary>
    public static class DemoTeams
    {
        private static readonly Collider[] hits = new Collider[64];

        /// <summary>Whether two share a side: an affiliation tag in common.</summary>
        public static bool AreAllies(IAffiliated one, IAffiliated other)
        {
            if (one == null || other == null) return false;
            if (ReferenceEquals(one, other)) return true;
            var mine = one.GetAffiliation();
            var theirs = other.GetAffiliation();
            if (mine == null || theirs == null) return false;
            foreach (var tag in mine)
                if (theirs.Contains(tag)) return true;
            return false;
        }

        /// <summary>Whether an area of the caster's reaches a target, by their sides.</summary>
        public static bool Reaches(ISource caster, ITarget target, EDemoAffects affects) => affects switch
        {
            EDemoAffects.Enemies => !AreAllies(caster, target),
            EDemoAffects.Allies => AreAllies(caster, target),
            _ => true,
        };

        /// <summary>The entities whose colliders are within a radius of a point, each once, the dead left out.</summary>
        public static List<ITarget> Around(Vector3 center, float radius, LayerMask layers)
        {
            var found = new List<ITarget>();
            int count = Physics.OverlapSphereNonAlloc(center, radius, hits, layers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = hits[i] ? hits[i].GetComponentInParent<ITarget>() : null;
                if (target != null && !target.IsDead && !found.Contains(target)) found.Add(target);
            }
            return found;
        }

        /// <summary>
        /// Applies an effect from an ability's data to a target, with the magnitudes its caller set, as the ability's
        /// own effects take them.
        /// </summary>
        public static bool Apply(AbilityDataPacket data, ITarget target, GameplayEffect effect)
        {
            if (effect == null || target == null || target.IsDead) return false;
            var spec = target.GenerateEffectSpec(data.EffectOrigin, effect);
            if (spec == null) return false;
            spec.TakeSetByCaller(data.SetByCallerMagnitudes);
            return target.ApplyGameplayEffect(spec);
        }
    }

    // The Demo's Area Effect, Apply To Caster, Dash, Blink and Consume Item are the framework's now (FN-24); its units
    // follow their use of items through their item systems' Item Consumed (DemoUnit)
}
