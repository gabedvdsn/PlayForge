using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Bounty: an effect an entity's killer takes as it dies, from it, so the effect reads its level, as gold or experience;
    /// and the killer's allies near it too, where it shares, as an assist (FN-14, Decision 77). A death no one dealt gives
    /// none. An entity holds as many as it gives, each its own.
    /// </summary>
    [Serializable]
    public sealed class BountyEntityComponent : AbstractEntityComponent
    {
        [Tooltip("The effect its killer takes as it dies, from it, so the effect reads its level: gold or experience")]
        public GameplayEffect Effect;

        [Tooltip("Its killer's allies within this of it take the effect too, as an assist, read at its level; 0, its killer alone")]
        public ScalableFloat ShareRadius = 0f;

        [Tooltip("Measures its share along the ground, leaving height out; off, a sphere")]
        public bool Flat = true;

        public override void Death(EntityComponentContext ctx)
        {
            var dead = ctx.System;
            if (Effect == null || dead == null || ctx.Killer is not IGameplayAbilitySystem killer || ReferenceEquals(killer, dead)) return;
            var origin = IEffectOrigin.GenerateSourceDerivation(dead);
            killer.ApplyGameplayEffect(killer.GenerateEffectSpec(origin, Effect));

            float radius = ShareRadius == null ? 0f : ShareRadius.Evaluate(NumberContext.Of(dead));
            if (radius <= 0f || dead.GetTargetingPacket() is not { } here) return;
            var near = new List<ITarget>();
            ForgeEntities.Within(here.position, radius, Flat, near);
            var sides = killer.GetAffiliation();
            foreach (var other in near)
            {
                if (ReferenceEquals(other, killer) || ReferenceEquals(other, dead) || other.IsDead
                    || other is not IGameplayAbilitySystem ally || !ForgeEntities.Takes(EAffinityFilter.Allies, sides, other)) continue;
                ally.ApplyGameplayEffect(ally.GenerateEffectSpec(origin, Effect));
            }
        }
    }

    /// <summary>Where a Respawn brings its entity back (FN-14).</summary>
    public enum ERespawnPlace
    {
        /// <summary>Where it stood as it registered: its spawn.</summary>
        Home,

        /// <summary>Where it died.</summary>
        WhereItDied,

        /// <summary>At the Respawn's point.</summary>
        Point,
    }

    /// <summary>
    /// Respawn: revives an entity that stays as it dies a while after, in its own time, at its home, where it died, or a
    /// point, as a hero comes back at its fountain (FN-14, Decision 77). Its Death's On Revive effects land as it does, as
    /// any revive's do; a revive before its time, as a buyback, ends the wait. An entity that ends as it dies has nothing to
    /// revive, which the Problems view says. An entity holds one at most.
    /// </summary>
    [Serializable]
    public sealed class RespawnEntityComponent : AbstractEntityComponent
    {
        [Tooltip("How long after it dies it revives, in seconds of its own time, read at its level")]
        public ScalableFloat Delay = 5f;

        [Tooltip("Where it revives: where it stood as it registered, where it died, or the point below")]
        public ERespawnPlace At = ERespawnPlace.Home;

        [Tooltip("Where it revives, where At is Point")]
        public Vector3 Point;

        public override bool AllowsSeveral => false;

        // Where its entity stood as it registered
        private sealed class Spawn
        {
            public bool Known;
            public Vector3 Home;
        }

        public override void Register(EntityComponentContext ctx)
        {
            if (ctx.System?.GetTargetingPacket() is not { } packet) return;
            var spawn = ctx.GetState<Spawn>(this);
            spawn.Known = true;
            spawn.Home = packet.position;
        }

        public override void Death(EntityComponentContext ctx)
        {
            var core = GASCore.Of(ctx.System);
            if (core == null || !EntityDeath.Stays(core.Identity?.Death)) return;
            float delay = Delay == null ? 0f : Delay.Evaluate(NumberContext.Of(ctx.System));
            var spawn = ctx.GetState<Spawn>(this);
            Vector3? where = At switch
            {
                ERespawnPlace.Home when spawn.Known => spawn.Home,
                ERespawnPlace.Point => Point,
                _ => null,
            };
            core.ReviveIn(delay, where);
        }
    }
}
