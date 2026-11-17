using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Bounty: an effect an entity's killer takes as it dies, from it, so the effect reads its level, as gold or experience;
    /// and the killer's allies near it too, where it shares, as an assist. A death no one dealt gives
    /// none. An entity holds as many as it gives, each its own.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "An effect the entity's killer takes as it dies, as gold or experience.")]
    public sealed class BountyEntityComponent : AbstractEntityComponent
    {
        /// <summary>What it does, in one line.</summary>
        public override string Describe() => $"{NameOf(Effect)} to its killer";

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

    /// <summary>Where a Respawn brings its entity back.</summary>
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
    /// point, as a hero comes back at its fountain. Its Death's On Revive effects land as it does, as
    /// any revive's do; a revive before its time, as a buyback, ends the wait. The wait is its entity's
    /// <see cref="RespawnTimer"/>, a service a HUD can read. An entity that ends as it dies has nothing to
    /// revive, which the Problems view says. An entity holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Brings the entity back a while after it dies.")]
    public sealed class RespawnEntityComponent : AbstractEntityComponent
    {
        /// <summary>What it does, in one line.</summary>
        public override string Describe() => $"Respawns after {NumberText.Of(Delay)} s, {System.Text.RegularExpressions.Regex.Replace(At.ToString(), "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant()}";

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

        // Dead and staying, it waits its delay by its own time, then revives where it says
        public override void Death(EntityComponentContext ctx)
        {
            if (ctx.System == null || !EntityDeath.Stays(ctx.Identity?.Death)) return;
            float delay = Delay == null ? 0f : Delay.Evaluate(NumberContext.Of(ctx.System));
            var spawn = ctx.GetState<Spawn>(this);
            Vector3? where = At switch
            {
                ERespawnPlace.Home when spawn.Known => spawn.Home,
                ERespawnPlace.Point => Point,
                _ => null,
            };
            ctx.System.GetService<RespawnTimer>()?.ReviveIn(delay, where);
        }
    }

    /// <summary>
    /// An entity's wait to revive, as its Respawn starts it: the time left, by the entity's own clock, and where it
    /// revives, at the end of the frame its time runs out. A revive before its time, as a buyback, ends the wait. A service
    /// of the entity, so a HUD reads it as <c>gas.FindService&lt;RespawnTimer&gt;()</c>.
    /// </summary>
    public sealed class RespawnTimer : AbstractEntityService
    {
        // The time left, NaN while none waits, and where
        private float left = float.NaN;
        private Vector3? at;

        /// <summary>Whether a revive waits.</summary>
        public bool Waiting => !float.IsNaN(left);

        /// <summary>The seconds of the entity's own time left before it revives; 0 where none waits.</summary>
        public float TimeLeft => Waiting ? Mathf.Max(0f, left) : 0f;

        /// <summary>
        /// Revives the entity, where it stays dead, once <paramref name="seconds"/> of its own time pass, at
        /// <paramref name="where"/> or where it is, at the end of that frame; in place of a wait it had.
        /// </summary>
        public void ReviveIn(float seconds, Vector3? where)
        {
            left = Mathf.Max(0f, seconds);
            at = where;
        }

        // The wait counted down; at its end, the revive queued for the frame's end, as a respawn timer's worker revives
        public override void Step(float deltaTime)
        {
            if (float.IsNaN(left)) return;
            left -= deltaTime;
            if (left > 0f) return;
            left = float.NaN;
            QueueRevive(at);
        }

        // A revive, whatever brought it, ends the wait
        public override void Revive() => left = float.NaN;

        // The revive queued in a method of its own: a closure in the step's own body was made as the step began, every
        // frame, whether it revived or not
        private void QueueRevive(Vector3? where)
        {
            var entity = System;
            entity?.GetActionQueue()?.Enqueue(new LambdaAction(_ => entity.Revive(where), "Respawn"));
        }
    }
}
