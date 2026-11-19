using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>How a <see cref="SurvivorsProjectile"/> flies.</summary>
    public enum ESurvivorsFlight
    {
        /// <summary>In a line, the way it's aimed.</summary>
        Straight,

        /// <summary>Turning toward the target it's handed, while that one stands.</summary>
        Homing,

        /// <summary>Up and over, as a thrown axe, its whole path the way it's aimed.</summary>
        Arc,
    }

    /// <summary>
    /// A projectile that flies until it's spent, applying its effects to each enemy it passes, as many as its pierce lets
    /// it: its own, and its caster's Projectile Pierce read by role. It's aimed by its registration's Aim, or at the target
    /// it's handed, and flies at its Projectile Speed, an attribute it carries, or its own speed. Its effects come from
    /// its origin, the ability, so they hit at the ability's level, through the game's damage pipeline.
    /// </summary>
    public class SurvivorsProjectile : AbstractEffectingMonoProcess
    {
        // Its flight is its run, so it starts Self Terminating, with no step timing of its own
        public SurvivorsProjectile()
        {
            ProcessLifecycle = EProcessLifecycle.SelfTerminating;
            ProcessTiming = EProcessStepTiming.None;
        }

        [Header("Flight")]
        public ESurvivorsFlight Flight = ESurvivorsFlight.Straight;

        [Tooltip("Its speed where it carries no Projectile Speed")]
        [Min(0.1f)] public float Speed = 14f;

        [Tooltip("How far it flies before it's spent")]
        [Min(0.1f)] public float Range = 16f;

        [Tooltip("How far around it reaches to hit")]
        [Min(0.05f)] public float HitRadius = 0.6f;

        [Tooltip("How many enemies it passes through past the first")]
        [Min(0)] public int Pierce;

        [Tooltip("Degrees a second a homing one turns toward its target")]
        [Min(0f)] public float Turn = 540f;

        [Tooltip("How high an arcing one rises")]
        [Min(0f)] public float ArcHeight = 4f;

        [Tooltip("Whom it hits, by how each stands to its caster")]
        public EAffiliationFilter Hits = EAffiliationFilter.Enemies;

        private Vector3 direction;
        private ITarget homing;
        private readonly HashSet<ITarget> struck = new();
        private readonly List<ITarget> near = new();

        public override void WhenInitialize()
        {
            base.WhenInitialize();
            struck.Clear();
            homing = null;
            var at = transform.position;
            if (regData.TryGet(SurvivorsTags.Aim, EDataTarget.Primary, out Vector3 aim)) direction = aim;
            else if (regData.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out ITarget target) && target != null)
            {
                homing = target;
                direction = SurvivorsTeams.PlaceOf(target) - at;
            }
            else direction = transform.forward;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
            if (Flight == ESurvivorsFlight.Homing && homing == null
                && regData.TryGet(Tags.TARGET_REAL, EDataTarget.Primary, out ITarget aimedAt)) homing = aimedAt;
            transform.SetPositionAndRotation(new Vector3(at.x, 1f, at.z), Quaternion.LookRotation(direction));
        }

        public override async UniTask RunProcess(CancellationToken token)
        {
            float speed = TryGetRole(Tags.PROJECTILE_SPEED, out var carried) && carried > 0f ? carried : Speed;
            int pierce = Pierce + (TryGetRole(SurvivorsTags.Pierce, out var extra) ? Mathf.Max(0, Mathf.FloorToInt(extra)) : 0);
            var caster = Origin?.GetSource();
            float travelled = 0f;
            while (travelled < Range)
            {
                token.ThrowIfCancellationRequested();
                // A shot whose caster is gone, as a fallen Archer's arrow, goes with it
                if (!SurvivorsTeams.Exists(caster)) return;
                float delta = ForgeTime.DeltaTimeFor(ProcessRelay?.Handler);
                if (Flight == ESurvivorsFlight.Homing && SurvivorsTeams.Stands(homing))
                {
                    var toward = SurvivorsTeams.PlaceOf(homing) - transform.position;
                    toward.y = 0f;
                    if (toward.sqrMagnitude > 0.01f)
                        direction = Vector3.RotateTowards(direction, toward.normalized, Turn * Mathf.Deg2Rad * delta, 0f);
                }
                float step = speed * delta;
                travelled += step;
                var flat = new Vector3(transform.position.x, 0f, transform.position.z) + direction * step;
                float height = Flight == ESurvivorsFlight.Arc
                    ? 1f + ArcHeight * 4f * (travelled / Range) * (1f - travelled / Range)
                    : 1f;
                transform.position = new Vector3(flat.x, height, flat.z);
                if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);

                // Each enemy it passes, once, until its pierce is spent
                near.Clear();
                SurvivorsTeams.Around(caster, flat, HitRadius, Hits, near);
                for (int i = 0; i < near.Count; i++)
                {
                    if (!struck.Add(near[i])) continue;
                    ApplyEffects(near[i]);
                    if (struck.Count > pierce) return;
                }
                await ForgeTick.NextFrame(token);
            }
        }
    }
}
