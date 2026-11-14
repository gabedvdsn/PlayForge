using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// A fire on the ground, as Flame Wall leaves: for its lifetime it applies its effects, each interval, to whoever of
    /// the other side stands within its radius. Its radius, lifetime, interval and heat are attributes it carries
    /// (FEAT-24), read by role: its prefab's own values, which the spawning ability's level can raise, as Flame Wall's
    /// radius and heat grow with its level. Each effect it applies gets what it carries by role, so Burning reads its
    /// heat (FEAT-28).
    /// </summary>
    public class GroundFireProcess : AbstractGameplayMonoProcess
    {
        // Its burning is its run, so it starts Self Terminating, with no step timing, where a mono process starts
        // Synchronous, which has no run (PC-35)
        public GroundFireProcess()
        {
            ProcessLifecycle = EProcessLifecycle.SelfTerminating;
            ProcessTiming = EProcessStepTiming.None;
        }

        [Header("Roles of the attributes it carries")]
        [Tooltip("The role its radius is read by")]
        public Tag RadiusRole;
        [Tooltip("The role its lifetime, in seconds, is read by")]
        public Tag LifetimeRole;
        [Tooltip("The role the seconds between its burns are read by")]
        public Tag IntervalRole;

        [Header("Where it carries none")]
        public float Radius = 2.5f;
        public float Lifetime = 4f;
        public float Interval = 1f;

        [Header("What it does")]
        [Tooltip("The effects each burn applies")]
        public List<GameplayEffect> Effects = new();
        [Tooltip("Who it burns, by side against its caster's")]
        public EDemoAffects Affects = EDemoAffects.Enemies;
        [Tooltip("The layers whose colliders it finds")]
        public LayerMask Layers = ~0;
        [Tooltip("A child it scales to its radius, as the fire's look")]
        public Transform Look;

        /// <summary>Its radius now, from the attribute it carries.</summary>
        public float CurrentRadius => Role(RadiusRole, Radius);

        public override void WhenInitialize()
        {
            base.WhenInitialize();
            // Flat on the ground, sized to its radius
            var at = transform.position;
            transform.position = new Vector3(at.x, 0.02f, at.z);
            transform.rotation = Quaternion.identity;
            if (Look) Look.localScale = new Vector3(CurrentRadius * 2f, Look.localScale.y, CurrentRadius * 2f);
        }

        public override async UniTask RunProcess(CancellationToken token)
        {
            float lifetime = Role(LifetimeRole, Lifetime);
            float interval = Mathf.Max(0.1f, Role(IntervalRole, Interval));
            float elapsed = 0f, next = 0f;
            while (elapsed < lifetime)
            {
                token.ThrowIfCancellationRequested();
                if (elapsed >= next)
                {
                    Burn();
                    next += interval;
                }
                elapsed += ForgeTime.DeltaTime;
                await ForgeTick.NextFrame(token);
            }
        }

        // Its effects, from its origin, to each of the other side within its radius
        private void Burn()
        {
            var caster = Origin?.GetSource();
            if (caster == null) return;
            foreach (var target in DemoTeams.Around(transform.position, CurrentRadius, Layers))
            {
                if (!DemoTeams.Reaches(caster, target, Affects)) continue;
                foreach (var effect in Effects)
                {
                    if (effect == null) continue;
                    var spec = target.GenerateEffectSpec(Origin, effect);
                    if (spec == null) continue;
                    // Its caller's magnitudes, and what it carries by role: Burning reads its heat (FEAT-28)
                    GiveMagnitudes(spec);
                    target.ApplyGameplayEffect(spec);
                }
            }
        }

        private float Role(Tag role, float fallback) => role.IsValid && TryGetRole(role, out var value) ? value : fallback;
    }
}
