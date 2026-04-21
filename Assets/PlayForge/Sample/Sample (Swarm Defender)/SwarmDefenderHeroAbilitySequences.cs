using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.SwarmDefenderSample
{
    /// <summary>
    /// Hero ability task sequences for the Swarm Defender sample.
    ///
    /// Every ability assumes:
    ///   • It runs inside an AbilityDataPacket (provides EffectOrigin for effect specs).
    ///   • The caster is the hero — resolved via the System on the AbilityDataPacket.
    ///   • Effects to apply on hit are stored in `Tags.EFFECTS` (from the ability asset).
    ///   • Targets are discovered by Physics.OverlapSphere around the caster,
    ///     filtered to live `Character` components that aren't the caster itself.
    ///
    /// Targeting contract:
    ///   • Projectile / directed abilities (Grenade, Bullet, Chain Lightning, Slash Wave,
    ///     Homing Missile, Orbital Strike) bail via `d.Interrupt()` during setup if no
    ///     enemy is within the ability's search radius — no visuals are spawned, so
    ///     the activation essentially no-ops.
    ///   • Caster-centered AoEs (Frost Nova, Whirlwind) bail if no enemy is within
    ///     the AoE radius.
    ///
    /// Effects applied via `data.TryApplyEffects(gameObject, Tags.EFFECTS)`.
    /// Visuals are built from Unity primitives so the sample runs with no art deps.
    /// </summary>
    public static class SwarmDefenderHeroAbilitySequences
    {
        private const int MAX_OVERLAP = 64;
        private static readonly Collider[] s_overlapBuffer = new Collider[MAX_OVERLAP];

        // ═══════════════════════════════════════════════════════════════════════
        // GRENADE — lobbed projectile, AoE explosion on landing
        // ═══════════════════════════════════════════════════════════════════════

        [TaskSequenceMethod("Ability: Grenade")]
        public static TaskSequence Grenade()
        {
            GameObject grenade = null;
            GameObject shockRing = null;
            Vector3 landingPos = Vector3.zero;
            Transform casterT = null;

            float flightTimer = 0f;
            const float flightDuration = 0.9f;
            const float arcHeight = 6f;
            const float searchRadius = 18f;
            const float explosionRadius = 5f;
            const float ringDuration = 0.35f;
            float ringTimer = 0f;
            Vector3 launchPos = Vector3.zero;

            return TaskSequenceBuilder.Create("Grenade")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s.WithName("Setup").Do(d =>
                {
                    // No target → no spawn, no cast.
                    if (!TryAcquireDirectedTarget(d, searchRadius, out casterT, out var target))
                        return;

                    landingPos = target.transform.position;
                    launchPos = casterT.position + Vector3.up * 1.2f;
                    grenade = MakePrimitive(PrimitiveType.Sphere, 0.4f, new Color(0.3f, 0.9f, 0.3f));
                    grenade.transform.position = launchPos;
                }))
                // Phase: arc toward landing position
                .SyncTask((d, dt) =>
                {
                    if (grenade == null) return true;
                    flightTimer += dt;
                    float t = Mathf.Clamp01(flightTimer / flightDuration);

                    Vector3 flat = Vector3.Lerp(launchPos, landingPos, t);
                    float parabola = 4f * arcHeight * t * (1f - t);
                    grenade.transform.position = flat + Vector3.up * parabola;
                    grenade.transform.Rotate(Vector3.right * 720f * dt);

                    return t >= 1f;
                })
                // Phase: detonate — spawn shockwave ring, overlap-apply effects, cleanup
                .SyncTask((d, dt) =>
                {
                    if (grenade == null && shockRing == null) return true;

                    if (grenade != null)
                    {
                        UnityEngine.Object.Destroy(grenade);
                        grenade = null;

                        int hits = Physics.OverlapSphereNonAlloc(
                            landingPos, explosionRadius, s_overlapBuffer);
                        for (int i = 0; i < hits; i++)
                        {
                            if (s_overlapBuffer[i] == null) continue;
                            if (s_overlapBuffer[i].transform == casterT) continue;
                            d.TryApplyEffects(s_overlapBuffer[i].gameObject, Tags.EFFECTS);
                        }

                        shockRing = MakePrimitive(
                            PrimitiveType.Cylinder, 0.1f, new Color(1f, 0.6f, 0.2f, 0.8f));
                        shockRing.transform.position = landingPos;
                        shockRing.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                    }

                    ringTimer += dt;
                    float tExp = Mathf.Clamp01(ringTimer / ringDuration);
                    if (shockRing != null)
                    {
                        float r = Mathf.Lerp(0.1f, explosionRadius * 2f, tExp);
                        shockRing.transform.localScale = new Vector3(r, 0.1f, r);
                    }
                    return ringTimer >= ringDuration;
                })
                .OnTerminate((ctx, ok) =>
                {
                    if (grenade != null) UnityEngine.Object.Destroy(grenade);
                    if (shockRing != null) UnityEngine.Object.Destroy(shockRing);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // CHAIN LIGHTNING — jumps to N nearest enemies with shrinking range
        // ═══════════════════════════════════════════════════════════════════════

        [TaskSequenceMethod("Ability: Chain Lightning")]
        public static TaskSequence ChainLightning()
        {
            Transform casterT = null;
            Character firstTarget = null;
            List<GameObject> bolts = null;

            const int maxJumps = 4;
            const float initialRange = 14f;
            const float jumpRange = 6f;
            const float boltLinger = 0.18f;
            const float hopDelay = 0.08f;

            return TaskSequenceBuilder.Create("Chain Lightning")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d =>
                {
                    if (!TryAcquireDirectedTarget(d, initialRange, out casterT, out firstTarget))
                        return;

                    bolts = new List<GameObject>();
                })
                .Task(async (d, t) =>
                {
                    if (firstTarget == null || bolts == null) return;

                    var prev = casterT;
                    var visited = new HashSet<Character>();
                    Character next = firstTarget;
                    float range = initialRange;

                    for (int jump = 0; jump < maxJumps; jump++)
                    {
                        if (next == null) break;
                        visited.Add(next);

                        var bolt = MakeBolt(prev.position, next.transform.position,
                            Color.Lerp(new Color(0.7f, 0.9f, 1f), new Color(1f, 1f, 0.5f),
                                jump / (float)maxJumps));
                        bolts.Add(bolt);

                        d.TryApplyEffects(next.gameObject, Tags.EFFECTS);

                        prev = next.transform;
                        range = jumpRange;

                        next = FindNearestEnemy(prev.position, range,
                            excludeTransform: prev, exclude: visited);

                        await UniTask.Delay(TimeSpan.FromSeconds(hopDelay), cancellationToken: t);
                    }

                    await UniTask.Delay(TimeSpan.FromSeconds(boltLinger), cancellationToken: t);
                })
                .OnTerminate((ctx, ok) =>
                {
                    if (bolts == null) return;
                    foreach (var b in bolts) if (b != null) UnityEngine.Object.Destroy(b);
                    bolts.Clear();
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BULLET — fast linear projectile, single-target on hit
        // ═══════════════════════════════════════════════════════════════════════

        [TaskSequenceMethod("Ability: Bullet")]
        public static TaskSequence Bullet()
        {
            GameObject bullet = null;
            Transform casterT = null;
            Transform targetT = null;
            Vector3 fallbackDir = Vector3.forward;
            const float speed = 45f;
            const float maxLifetime = 1.5f;
            const float hitRadius = 0.8f;
            const float searchRadius = 25f;
            float life = 0f;

            return TaskSequenceBuilder.Create("Bullet")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s.WithName("Setup").Do(d =>
                {
                    if (!TryAcquireDirectedTarget(d, searchRadius, out casterT, out var target))
                        return;

                    targetT = target.transform;
                    fallbackDir = (targetT.position - casterT.position).WithY(0).normalized;
                    if (fallbackDir.sqrMagnitude < 0.0001f) fallbackDir = casterT.forward;

                    bullet = MakePrimitive(PrimitiveType.Capsule, 0.25f, new Color(1f, 0.95f, 0.5f));
                    bullet.transform.localScale = new Vector3(0.2f, 0.45f, 0.2f);
                    bullet.transform.position = casterT.position + Vector3.up * 1.2f + fallbackDir * 1f;
                    bullet.transform.rotation = Quaternion.LookRotation(fallbackDir) * Quaternion.Euler(90, 0, 0);
                }))
                .SyncTask((d, dt) =>
                {
                    if (bullet == null) return true;
                    life += dt;

                    Vector3 dir = targetT != null
                        ? (targetT.position - bullet.transform.position).normalized
                        : fallbackDir;

                    bullet.transform.position += dir * speed * dt;
                    if (dir.sqrMagnitude > 0.0001f)
                        bullet.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);

                    // Proximity hit
                    if (targetT != null &&
                        Vector3.Distance(bullet.transform.position, targetT.position) < hitRadius)
                    {
                        d.TryApplyEffects(targetT.gameObject, Tags.EFFECTS);
                        return true;
                    }

                    // Collision-clip safety
                    int hits = Physics.OverlapSphereNonAlloc(
                        bullet.transform.position, hitRadius, s_overlapBuffer);
                    for (int i = 0; i < hits; i++)
                    {
                        if (s_overlapBuffer[i] == null) continue;
                        if (s_overlapBuffer[i].transform == casterT) continue;
                        if (!s_overlapBuffer[i].TryGetComponent<Character>(out var c)) continue;
                        if (c is Hero) continue;
                        if (c.IsDead) continue;
                        d.TryApplyEffects(c.gameObject, Tags.EFFECTS);
                        return true;
                    }

                    return life >= maxLifetime;
                })
                .OnTerminate((ctx, ok) =>
                {
                    if (bullet != null) UnityEngine.Object.Destroy(bullet);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SLASH WAVE — wide crescent that sweeps outward from the caster
        // ═══════════════════════════════════════════════════════════════════════

        [TaskSequenceMethod("Ability: Slash Wave")]
        public static TaskSequence SlashWave()
        {
            GameObject blade = null;
            Transform casterT = null;
            Vector3 forward = Vector3.forward;
            float elapsed = 0f;
            const float travel = 10f;
            const float duration = 0.55f;
            const float halfWidth = 3.5f;
            const float searchRadius = 20f;
            HashSet<Collider> hitOnce = null;

            return TaskSequenceBuilder.Create("Slash Wave")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s.WithName("Setup").Do(d =>
                {
                    if (!TryAcquireDirectedTarget(d, searchRadius, out casterT, out var target))
                        return;

                    hitOnce = new HashSet<Collider>();
                    forward = (target.transform.position - casterT.position).WithY(0).normalized;
                    if (forward.sqrMagnitude < 0.0001f) forward = casterT.forward;

                    blade = MakePrimitive(PrimitiveType.Cube, 1f, new Color(0.9f, 0.4f, 0.9f, 0.9f));
                    blade.transform.localScale = new Vector3(halfWidth * 2f, 0.25f, 0.6f);
                    blade.transform.position = casterT.position + Vector3.up * 0.3f + forward * 1.5f;
                    blade.transform.rotation = Quaternion.LookRotation(forward);
                }))
                .SyncTask((d, dt) =>
                {
                    if (blade == null) return true;
                    elapsed += dt;
                    float t = Mathf.Clamp01(elapsed / duration);

                    float dist = Mathf.Lerp(1.5f, travel, t);
                    Vector3 center = casterT.position + Vector3.up * 0.3f + forward * dist;
                    blade.transform.position = center;

                    float widthScale = Mathf.Sin(t * Mathf.PI);
                    blade.transform.localScale = new Vector3(
                        halfWidth * 2f * (0.6f + 0.4f * widthScale), 0.25f, 0.6f);

                    Vector3 halfExtents = blade.transform.localScale * 0.5f;
                    int hits = Physics.OverlapBoxNonAlloc(
                        center, halfExtents, s_overlapBuffer, blade.transform.rotation);
                    for (int i = 0; i < hits; i++)
                    {
                        var col = s_overlapBuffer[i];
                        if (col == null) continue;
                        if (col.transform == casterT) continue;
                        if (!hitOnce.Add(col)) continue;
                        d.TryApplyEffects(col.gameObject, Tags.EFFECTS);
                    }

                    return t >= 1f;
                })
                .OnTerminate((ctx, ok) =>
                {
                    if (blade != null) UnityEngine.Object.Destroy(blade);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // FROST NOVA — expanding ring AoE centered on the caster
        // ═══════════════════════════════════════════════════════════════════════

        [TaskSequenceMethod("Ability: Frost Nova")]
        public static TaskSequence FrostNova()
        {
            GameObject ring = null;
            Transform casterT = null;
            float elapsed = 0f;
            const float duration = 0.75f;
            const float maxRadius = 8f;
            bool applied = false;

            return TaskSequenceBuilder.Create("Frost Nova")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s.WithName("Setup").Do(d =>
                {
                    // AoE centered on caster — only fire if something is in the ring.
                    if (!TryAcquireAoE(d, maxRadius, out casterT))
                        return;

                    ring = MakePrimitive(PrimitiveType.Cylinder, 0.05f,
                        new Color(0.5f, 0.85f, 1f, 0.75f));
                    ring.transform.position = casterT.position;
                    ring.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
                }))
                .SyncTask((d, dt) =>
                {
                    if (ring == null) return true;
                    elapsed += dt;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float eased = 1f - (1f - t) * (1f - t);
                    float r = Mathf.Lerp(0.1f, maxRadius, eased);
                    ring.transform.position = casterT.position;
                    ring.transform.localScale = new Vector3(r, 0.1f, r);

                    if (!applied && t >= 0.35f)
                    {
                        applied = true;
                        int hits = Physics.OverlapSphereNonAlloc(
                            casterT.position, maxRadius, s_overlapBuffer);
                        for (int i = 0; i < hits; i++)
                        {
                            var col = s_overlapBuffer[i];
                            if (col == null) continue;
                            if (col.transform == casterT) continue;
                            d.TryApplyEffects(col.gameObject, Tags.EFFECTS);
                        }
                    }

                    return t >= 1f;
                })
                .OnTerminate((ctx, ok) =>
                {
                    if (ring != null) UnityEngine.Object.Destroy(ring);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HOMING MISSILE — slow-turning projectile that tracks the nearest enemy
        // ═══════════════════════════════════════════════════════════════════════

        [TaskSequenceMethod("Ability: Homing Missile")]
        public static TaskSequence HomingMissile()
        {
            GameObject missile = null;
            Transform casterT = null;
            Character target = null;
            Vector3 velocity = Vector3.zero;
            float life = 0f;
            const float maxLifetime = 3.5f;
            const float speed = 14f;
            const float turnRate = 720f;
            const float hitRadius = 1.1f;
            const float splashRadius = 3f;
            const float searchRadius = 30f;

            return TaskSequenceBuilder.Create("Homing Missile")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s.WithName("Setup").Do(d =>
                {
                    if (!TryAcquireDirectedTarget(d, searchRadius, out casterT, out target))
                        return;

                    Vector3 launchDir = (target.transform.position - casterT.position).WithY(0).normalized;
                    if (launchDir.sqrMagnitude < 0.0001f) launchDir = casterT.forward;

                    missile = MakePrimitive(PrimitiveType.Capsule, 0.5f, new Color(1f, 0.4f, 0.3f));
                    missile.transform.localScale = new Vector3(0.3f, 0.6f, 0.3f);
                    missile.transform.position = casterT.position + Vector3.up * 1.3f;
                    missile.transform.rotation = Quaternion.LookRotation(launchDir) * Quaternion.Euler(90, 0, 0);
                    velocity = launchDir * speed;
                }))
                .SyncTask((d, dt) =>
                {
                    if (missile == null) return true;
                    life += dt;

                    // Re-acquire if target died
                    if (target == null || target.IsDead)
                        target = FindNearestEnemy(missile.transform.position, 20f, casterT);

                    Vector3 desiredDir = target != null
                        ? (target.transform.position - missile.transform.position).normalized
                        : velocity.normalized;

                    Vector3 currentDir = velocity.sqrMagnitude > 0.001f
                        ? velocity.normalized
                        : desiredDir;

                    Vector3 newDir = Vector3.RotateTowards(
                        currentDir, desiredDir, turnRate * Mathf.Deg2Rad * dt, 0f);
                    velocity = newDir * speed;

                    missile.transform.position += velocity * dt;
                    if (velocity.sqrMagnitude > 0.0001f)
                        missile.transform.rotation =
                            Quaternion.LookRotation(velocity) * Quaternion.Euler(90, 0, 0);

                    if (target != null &&
                        Vector3.Distance(missile.transform.position, target.transform.position) < hitRadius)
                    {
                        int hits = Physics.OverlapSphereNonAlloc(
                            missile.transform.position, splashRadius, s_overlapBuffer);
                        for (int i = 0; i < hits; i++)
                        {
                            if (s_overlapBuffer[i] == null) continue;
                            if (s_overlapBuffer[i].transform == casterT) continue;
                            d.TryApplyEffects(s_overlapBuffer[i].gameObject, Tags.EFFECTS);
                        }
                        return true;
                    }

                    return life >= maxLifetime;
                })
                .OnTerminate((ctx, ok) =>
                {
                    if (missile != null) UnityEngine.Object.Destroy(missile);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ORBITAL STRIKE — telegraphed ground target, beam falls and explodes
        // ═══════════════════════════════════════════════════════════════════════

        [TaskSequenceMethod("Ability: Orbital Strike")]
        public static TaskSequence OrbitalStrike()
        {
            GameObject marker = null;
            GameObject beam = null;
            Transform casterT = null;
            Vector3 landingPos = Vector3.zero;
            const float telegraphDuration = 1.0f;
            const float beamDuration = 0.25f;
            const float aoeRadius = 6f;
            const float searchRadius = 25f;
            float elapsed = 0f;
            bool applied = false;

            return TaskSequenceBuilder.Create("Orbital Strike")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s.WithName("Setup & Telegraph").Do(d =>
                {
                    if (!TryAcquireDirectedTarget(d, searchRadius, out casterT, out var target))
                        return;

                    landingPos = target.transform.position;

                    marker = MakePrimitive(PrimitiveType.Cylinder, 0.05f,
                        new Color(1f, 0.2f, 0.2f, 0.55f));
                    marker.transform.position = landingPos;
                    marker.transform.localScale = new Vector3(aoeRadius * 2f, 0.05f, aoeRadius * 2f);
                }))
                .SyncTask((d, dt) =>
                {
                    if (marker == null) return true;
                    elapsed += dt;
                    float pulse = 0.8f + 0.2f * Mathf.Sin(elapsed * 12f);
                    marker.transform.localScale =
                        new Vector3(aoeRadius * 2f * pulse, 0.05f, aoeRadius * 2f * pulse);
                    return elapsed >= telegraphDuration;
                })
                .SyncTask((d, dt) =>
                {
                    if (marker == null) return true;

                    if (beam == null)
                    {
                        beam = MakePrimitive(PrimitiveType.Cylinder, 1f,
                            new Color(1f, 1f, 0.6f, 0.9f));
                        beam.transform.position = landingPos + Vector3.up * 30f;
                        beam.transform.localScale = new Vector3(aoeRadius * 0.4f, 30f, aoeRadius * 0.4f);
                    }

                    elapsed += dt;
                    float t = Mathf.Clamp01((elapsed - telegraphDuration) / beamDuration);
                    if (beam != null)
                        beam.transform.localScale =
                            new Vector3(aoeRadius * 0.4f * (1f - t), 30f, aoeRadius * 0.4f * (1f - t));

                    if (!applied)
                    {
                        applied = true;
                        int hits = Physics.OverlapSphereNonAlloc(
                            landingPos, aoeRadius, s_overlapBuffer);
                        for (int i = 0; i < hits; i++)
                        {
                            if (s_overlapBuffer[i] == null) continue;
                            if (s_overlapBuffer[i].transform == casterT) continue;
                            d.TryApplyEffects(s_overlapBuffer[i].gameObject, Tags.EFFECTS);
                        }
                    }

                    return t >= 1f;
                })
                .OnTerminate((ctx, ok) =>
                {
                    if (marker != null) UnityEngine.Object.Destroy(marker);
                    if (beam != null) UnityEngine.Object.Destroy(beam);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // WHIRLWIND — rotating AoE tied to the caster for a short duration
        // ═══════════════════════════════════════════════════════════════════════

        [TaskSequenceMethod("Ability: Whirlwind")]
        public static TaskSequence Whirlwind()
        {
            GameObject disc = null;
            Transform casterT = null;
            const float duration = 1.8f;
            const float radius = 4.5f;
            const float tickInterval = 0.2f;
            float elapsed = 0f;
            float tickAccum = 0f;

            return TaskSequenceBuilder.Create("Whirlwind")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s.WithName("Setup").Do(d =>
                {
                    if (!TryAcquireAoE(d, radius, out casterT))
                        return;

                    disc = MakePrimitive(PrimitiveType.Cylinder, 0.08f,
                        new Color(0.7f, 0.3f, 0.9f, 0.55f));
                    disc.transform.position = casterT.position + Vector3.up * 0.1f;
                    disc.transform.localScale = new Vector3(radius * 2f, 0.08f, radius * 2f);
                }))
                .SyncTask((d, dt) =>
                {
                    if (disc == null || casterT == null) return true;
                    elapsed += dt;
                    tickAccum += dt;

                    disc.transform.position = casterT.position + Vector3.up * 0.1f;
                    disc.transform.Rotate(Vector3.up * 720f * dt);

                    if (tickAccum >= tickInterval)
                    {
                        tickAccum = 0f;
                        int hits = Physics.OverlapSphereNonAlloc(
                            casterT.position, radius, s_overlapBuffer);
                        for (int i = 0; i < hits; i++)
                        {
                            if (s_overlapBuffer[i] == null) continue;
                            if (s_overlapBuffer[i].transform == casterT) continue;
                            d.TryApplyEffects(s_overlapBuffer[i].gameObject, Tags.EFFECTS);
                        }
                    }
                    return elapsed >= duration;
                })
                .OnTerminate((ctx, ok) =>
                {
                    if (disc != null) UnityEngine.Object.Destroy(disc);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Resolves the caster transform and finds the nearest enemy within
        /// `searchRadius`. Returns false and calls `d.Interrupt()` if either
        /// the caster can't be resolved or no valid enemy is in range — the
        /// ability will cancel before any GameObjects are instantiated.
        /// </summary>
        private static bool TryAcquireDirectedTarget(
            SequenceDataPacket d, float searchRadius,
            out Transform casterT, out Character target)
        {
            target = null;
            casterT = GetCasterTransform(d);
            if (casterT == null) { d.Interrupt(); return false; }

            target = FindNearestEnemy(casterT.position, searchRadius, casterT);
            if (target == null) { d.Interrupt(); return false; }
            return true;
        }

        /// <summary>
        /// Resolves the caster transform and ensures at least one enemy is within
        /// `aoeRadius`. Used for caster-centered AoE abilities — prevents them from
        /// visually firing when no enemies are around.
        /// </summary>
        private static bool TryAcquireAoE(
            SequenceDataPacket d, float aoeRadius,
            out Transform casterT)
        {
            casterT = GetCasterTransform(d);
            if (casterT == null) { d.Interrupt(); return false; }

            if (!AnyEnemyInRange(casterT.position, aoeRadius, casterT))
            {
                d.Interrupt();
                return false;
            }
            return true;
        }

        private static bool AnyEnemyInRange(Vector3 origin, float radius, Transform exclude)
        {
            int hits = Physics.OverlapSphereNonAlloc(origin, radius, s_overlapBuffer);
            for (int i = 0; i < hits; i++)
            {
                var col = s_overlapBuffer[i];
                if (col == null) continue;
                if (exclude != null && col.transform == exclude) continue;
                if (!col.TryGetComponent<Character>(out var c)) continue;
                if (c is Hero) continue;
                if (c.IsDead) continue;
                return true;
            }
            return false;
        }

        private static Transform GetCasterTransform(ProcessDataPacket data)
        {
            if (data is AbilityDataPacket adp && adp.System != null)
            {
                var gas = adp.System.Self.ToGASObject();
                if (gas != null) return gas.transform;
            }
            // Fallback: packet might carry the hero directly.
            var hero = data.GetPrimary<Hero>(SwarmTags.HERO) ?? data.GetPrimary<Hero>(Tags.GAMEOBJECT);
            return hero != null ? hero.transform : null;
        }

        private static Character FindNearestEnemy(
            Vector3 origin, float radius,
            Transform excludeTransform = null,
            HashSet<Character> exclude = null)
        {
            int hits = Physics.OverlapSphereNonAlloc(origin, radius, s_overlapBuffer);
            Character best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < hits; i++)
            {
                var col = s_overlapBuffer[i];
                if (col == null) continue;
                if (excludeTransform != null && col.transform == excludeTransform) continue;
                if (!col.TryGetComponent<Character>(out var c)) continue;
                if (c is Hero) continue;
                if (c.IsDead) continue;
                if (exclude != null && exclude.Contains(c)) continue;

                float sqr = (c.transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = c;
                }
            }
            return best;
        }

        private static GameObject MakePrimitive(PrimitiveType type, float scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.localScale = Vector3.one * scale;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(rend.sharedMaterial);
                mat.color = color;
                rend.material = mat;
            }
            return go;
        }

        private static GameObject MakeBolt(Vector3 from, Vector3 to, Color color)
        {
            var bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var col = bolt.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);

            Vector3 mid = (from + to) * 0.5f;
            Vector3 diff = to - from;
            float len = diff.magnitude;

            bolt.transform.position = mid;
            bolt.transform.rotation = diff.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(diff.normalized)
                : Quaternion.identity;
            bolt.transform.localScale = new Vector3(0.15f, 0.15f, Mathf.Max(0.01f, len));

            var rend = bolt.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(rend.sharedMaterial);
                mat.color = color;
                rend.material = mat;
            }
            return bolt;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Small Vector3 helper (scoped internal so it doesn't collide with other utils)
    // ═══════════════════════════════════════════════════════════════════════════
    internal static class SwarmVector3Extensions
    {
        public static Vector3 WithY(this Vector3 v, float y) => new Vector3(v.x, y, v.z);
    }
}
