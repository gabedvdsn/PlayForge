using System.Collections.Generic;
using UnityEngine;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Examples
{
    /// <summary>
    /// Synchronous ability sequence tasks for the demo scene.
    /// Each method is marked with [TaskSequenceMethod] so it appears in the ability runtime dropdown.
    ///
    /// All sequences use Synchronous lifecycle with Update step timing.
    /// The ability system registers them via ProcessControl; the TaskSequenceProcess
    /// auto-detects the sync metadata and drives each frame via Step().
    ///
    /// Each visual phase is its own top-level .SyncTask() call so that phases
    /// execute sequentially as individual stages. Setup runs as a .Do() inside
    /// a .Stage() since it completes instantly.
    ///
    /// Targeting task recommendations are noted in each method's XML doc.
    /// Assign the indicated targeting task on the Ability ScriptableObject.
    /// </summary>
    public static class DemoAbilitySequences
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // FIREBALL — Projectile chases or moves to target, explodes on arrival
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Targeting: SelectGasOrPositionTargetTask (left click to select entity or ground position)
        /// </summary>
        [TaskSequenceMethod("Fireball")]
        public static TaskSequence FireballSequence()
        {
            GameObject fireball = null;
            Transform liveTarget = null;
            Vector3 targetPos = Vector3.zero;
            bool hasTarget = false;
            float speed = 35f;
            float impactTimer = 0f;
            float shrinkTimer = 0f;

            return TaskSequenceBuilder.Create("Fireball")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .WithCriticalFlag(true)
                .Stage(s => s
                    .WithName("Setup")
                    .Do(d =>
                    {
                        fireball = DemoSequences.CreatePrim(PrimitiveType.Sphere).Scale(Vector3.one * 3f);
                        d.SetPrimary(Tags.DATA, fireball);

                        if (d.GetTargetingPacket(Tags.TARGET, out var target, false))
                        {
                            hasTarget = true;
                            liveTarget = target.transform;
                            targetPos = target.position;
                        }
                    })
                )
                // Phase: Fly toward target
                .SyncTask((d, dt) =>
                {
                    if (!fireball || !hasTarget) return true;

                    var dest = liveTarget != null ? liveTarget.position : targetPos;
                    fireball.transform.position = Vector3.MoveTowards(
                        fireball.transform.position, dest, speed * dt);

                    if (Vector3.Distance(fireball.transform.position, dest) < 0.5f)
                        return true;

                    return false;
                })
                // Phase: Impact punch and apply effects
                .SyncTask((d, dt) =>
                {
                    impactTimer += dt;
                    float punchDuration = 0.4f;
                    float t1 = Mathf.Clamp01(impactTimer / punchDuration);
                    float punch = 1f + 1.6f * Mathf.Sin(t1 * Mathf.PI);
                    fireball.transform.localScale = Vector3.one * (3f * punch);

                    if (impactTimer >= punchDuration)
                    {
                        var hits = new Collider[6];
                        var size = Physics.OverlapSphereNonAlloc(fireball.transform.position, 3f * punch, hits);
                        for (int i = 0; i < size; i++)
                        {
                            d.TryApplyEffects(hits[i].gameObject, Tags.EFFECTS);
                        }
                        return true;
                    }
                    return false;
                })
                // Phase: Shrink and disappear
                .SyncTask((d, dt) =>
                {
                    shrinkTimer += dt;
                    float shrinkDuration = 0.2f;
                    float t2 = Mathf.Clamp01(shrinkTimer / shrinkDuration);
                    fireball.transform.localScale = Vector3.Lerp(Vector3.one * 3f, Vector3.zero, t2);

                    if (shrinkTimer >= shrinkDuration) return true;

                    return false;
                })
                .OnTerminate((ctx, success) =>
                {
                    if (fireball) Object.Destroy(fireball);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ICE LANCE — Fast projectile that shatters into fragments on impact
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Targeting: SelectGasOrPositionTargetTask (left click to select entity or ground position)
        /// </summary>
        [TaskSequenceMethod("Ice Lance")]
        public static TaskSequence IceLanceSequence()
        {
            GameObject lance = null;
            List<GameObject> shards = new();
            Vector3 targetPos = Vector3.zero;
            Transform liveTarget = null;
            bool hasTarget = false;
            float speed = 55f;

            float shatterTimer = 0f;
            float shatterDuration = 0.6f;
            Vector3[] shardDirs = null;
            Vector3[] shardStarts = null;

            return TaskSequenceBuilder.Create("Ice Lance")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s
                    .WithName("Setup")
                    .Do(d =>
                    {
                        lance = DemoSequences.CreatePrim(PrimitiveType.Capsule).Scale(new Vector3(0.4f, 2f, 0.4f));
                        d.SetPrimary(Tags.DATA, lance);

                        if (d.GetTargetingPacket(Tags.TARGET, out var target, false))
                        {
                            hasTarget = true;
                            liveTarget = target.transform;
                            targetPos = target.position;
                        }
                    })
                )
                // Phase: Fly toward target
                .SyncTask((d, dt) =>
                {
                    if (!hasTarget || !lance) return true;

                    var dest = liveTarget != null ? liveTarget.position : targetPos;
                    var dir = (dest - lance.transform.position).normalized;
                    if (dir != Vector3.zero)
                        lance.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0);

                    lance.transform.position = Vector3.MoveTowards(
                        lance.transform.position, dest, speed * dt);

                    if (Vector3.Distance(lance.transform.position, dest) < 0.8f)
                    {
                        // Shatter: replace lance with fragments
                        var impactPos = lance.transform.position;
                        Object.Destroy(lance);
                        lance = null;

                        int shardCount = 8;
                        shardDirs = new Vector3[shardCount];
                        shardStarts = new Vector3[shardCount];
                        for (int i = 0; i < shardCount; i++)
                        {
                            var shard = DemoSequences.CreatePrim(PrimitiveType.Cube, withPos: false)
                                .Scale(Vector3.one * 0.35f);
                            shard.transform.position = impactPos;
                            shards.Add(shard);

                            float angle = (360f / shardCount) * i * Mathf.Deg2Rad;
                            shardDirs[i] = new Vector3(
                                Mathf.Cos(angle), Random.Range(0.3f, 1.2f), Mathf.Sin(angle));
                            shardStarts[i] = impactPos;
                        }
                        
                        if (liveTarget) d.TryApplyEffects(liveTarget.gameObject, Tags.EFFECTS);

                        return true;
                    }

                    return false;
                })
                // Phase: Shards fly outward and shrink
                .SyncTask((d, dt) =>
                {
                    shatterTimer += dt;
                    float t = Mathf.Clamp01(shatterTimer / shatterDuration);

                    for (int i = 0; i < shards.Count; i++)
                    {
                        if (!shards[i]) continue;

                        shards[i].transform.position = shardStarts[i] + shardDirs[i] * (t * 6f);
                        // Gravity arc
                        var pos = shards[i].transform.position;
                        pos.y += -9.8f * t * t * 0.5f;
                        shards[i].transform.position = pos;

                        float scale = Mathf.Lerp(0.35f, 0f, t);
                        shards[i].transform.localScale = Vector3.one * scale;

                        shards[i].transform.Rotate(Vector3.one * (360f * dt));
                    }

                    return shatterTimer >= shatterDuration;
                })
                .OnTerminate((ctx, success) =>
                {
                    if (lance) Object.Destroy(lance);
                    foreach (var shard in shards)
                        if (shard) Object.Destroy(shard);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // HEAL PULSE — Expanding ring of light radiates from the caster
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Targeting: AutoTargetSelfTask (auto-targets the caster, no input needed)
        /// </summary>
        [TaskSequenceMethod("Heal Pulse")]
        public static TaskSequence HealPulseSequence()
        {
            GameObject ring = null;
            float expandDuration = 0.8f;
            float holdDuration = 0.3f;
            float contractDuration = 0.5f;
            float totalDuration;
            float timer = 0f;
            Vector3 origin = Vector3.zero;
            bool appliedEffect = false;

            totalDuration = expandDuration + holdDuration + contractDuration;

            return TaskSequenceBuilder.Create("Heal Pulse")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s
                    .WithName("Setup")
                    .Do(d =>
                    {
                        ring = DemoSequences.CreatePrim(PrimitiveType.Cylinder, withPos: false)
                            .Scale(new Vector3(0.01f, 0.05f, 0.01f));

                        if (d.GetTargetingPacket(Tags.TARGET, out var target, false))
                            origin = target.transform != null ? target.transform.position : target.position;
                        else
                            origin = DemoManager.Instance.Player.transform.position;

                        ring.transform.position = origin;
                        d.SetPrimary(Tags.DATA, ring);
                    })
                )
                // Phase: Pulse (expand, hold, contract)
                .SyncTask((d, dt) =>
                {
                    if (!ring) return true;

                    timer += dt;

                    float maxRadius = 12f;
                    float currentRadius;

                    if (timer < expandDuration)
                    {
                        float t = timer / expandDuration;
                        t = 1f - (1f - t) * (1f - t); // EaseOut
                        currentRadius = Mathf.Lerp(0.01f, maxRadius, t);
                    }
                    else if (timer < expandDuration + holdDuration)
                    {
                        currentRadius = maxRadius;
                        if (!appliedEffect)
                        {
                            
                            appliedEffect = true;
                        }
                    }
                    else
                    {
                        float t = (timer - expandDuration - holdDuration) / contractDuration;
                        currentRadius = Mathf.Lerp(maxRadius, 0f, t);
                    }

                    ring.transform.localScale = new Vector3(currentRadius, 0.05f, currentRadius);

                    // Gentle float upward during pulse
                    var pos = ring.transform.position;
                    pos.y = origin.y + Mathf.Sin(timer * 3f) * 0.5f;
                    ring.transform.position = pos;

                    return timer >= totalDuration;
                })
                .OnTerminate((ctx, success) =>
                {
                    if (ring) Object.Destroy(ring);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // GROUND SLAM — Ring of cubes erupts upward around the caster, then falls
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Targeting: AutoTargetSelfTask (auto-targets the caster, no input needed)
        /// </summary>
        [TaskSequenceMethod("Ground Slam")]
        public static TaskSequence GroundSlamSequence()
        {
            List<GameObject> debris = new();
            int debrisCount = 16;
            float radius = 8f;
            float launchHeight = 10f;
            float launchDuration = 0.5f;
            float fallDuration = 0.8f;
            float shrinkDuration = 0.3f;
            float timer = 0f;
            Vector3 origin = Vector3.zero;

            Vector3[] launchPositions;
            float[] heights;

            launchPositions = new Vector3[debrisCount];
            heights = new float[debrisCount];

            return TaskSequenceBuilder.Create("Ground Slam")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s
                    .WithName("Setup")
                    .Do(d =>
                    {
                        if (d.GetTargetingPacket(Tags.TARGET, out var target, false))
                            origin = target.transform != null ? target.transform.position : target.position;
                        else
                            origin = DemoManager.Instance.Player.transform.position;

                        for (int i = 0; i < debrisCount; i++)
                        {
                            float angle = (360f / debrisCount) * i * Mathf.Deg2Rad;
                            var pos = origin + new Vector3(
                                Mathf.Cos(angle) * radius,
                                0f,
                                Mathf.Sin(angle) * radius);

                            var cube = DemoSequences.CreatePrim(PrimitiveType.Cube, withPos: false)
                                .Scale(Vector3.one * Random.Range(0.6f, 1.4f));
                            cube.transform.position = pos;
                            debris.Add(cube);

                            launchPositions[i] = pos;
                            heights[i] = launchHeight * Random.Range(0.6f, 1.0f);
                        }
                    })
                )
                // Phase: Launch upward
                .SyncTask((d, dt) =>
                {
                    timer += dt;
                    float tUp = Mathf.Clamp01(timer / launchDuration);
                    float easeUp = 1f - (1f - tUp) * (1f - tUp); // EaseOut

                    for (int i = 0; i < debris.Count; i++)
                    {
                        if (!debris[i]) continue;
                        var pos = launchPositions[i];
                        pos.y += heights[i] * easeUp;
                        debris[i].transform.position = pos;
                        debris[i].transform.Rotate(Vector3.one * 200f * dt);
                    }

                    return timer >= launchDuration;
                })
                .Do(d =>
                {
                    timer = 0f;
                })
                // Phase: Fall back down
                .SyncTask((d, dt) =>
                {

                    timer += dt;
                    float tDown = Mathf.Clamp01(timer / fallDuration);
                    float easeFall = tDown * tDown; // EaseIn (accelerating fall)

                    for (int i = 0; i < debris.Count; i++)
                    {
                        if (!debris[i]) continue;
                        var peak = launchPositions[i];
                        peak.y += heights[i];
                        var ground = launchPositions[i];
                        debris[i].transform.position = Vector3.Lerp(peak, ground, easeFall);
                        debris[i].transform.Rotate(Vector3.one * 400f * dt);
                    }

                    return timer >= fallDuration;
                })
                .Do(d =>
                {
                    timer = 0f;
                })
                // Phase: Shrink and disappear
                .SyncTask((d, dt) =>
                {
                    timer += dt;

                    for (int i = 0; i < debris.Count; i++)
                    {
                        if (!debris[i]) continue;
                        debris[i].transform.localScale *= (1f - dt * 5f);
                    }

                    return timer >= shrinkDuration;
                })
                .OnTerminate((ctx, success) =>
                {
                    foreach (var d in debris)
                        if (d) Object.Destroy(d);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // METEOR STRIKE — Sphere falls from the sky, impact creates expanding crater ring
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Targeting: SelectPositionTargetTask (left click to select a ground position)
        /// </summary>
        [TaskSequenceMethod("Meteor Strike")]
        public static TaskSequence MeteorStrikeSequence()
        {
            GameObject meteor = null;
            List<GameObject> craterRing = new();
            Vector3 targetPos = Vector3.zero;
            float dropHeight = 60f;
            float fallSpeed = 80f;

            float expandDuration = 0.6f;
            float fadeDuration = 0.5f;
            float impactTimer = 0f;
            int craterCount = 20;
            float craterRadius = 10f;
            Vector3[] craterDirs;

            craterDirs = new Vector3[craterCount];

            return TaskSequenceBuilder.Create("Meteor Strike")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s
                    .WithName("Setup")
                    .Do(d =>
                    {
                        if (d.GetTargetingPacket(Tags.TARGET, out var target, false))
                        {
                            targetPos = target.transform != null ? target.transform.position : target.position;
                        }
                        else
                            targetPos = DemoManager.Instance.Player.transform.position + Vector3.forward * 10f;

                        meteor = DemoSequences.CreatePrim(PrimitiveType.Sphere, withPos: false)
                            .Scale(Vector3.one * 4f);
                        meteor.transform.position = targetPos + Vector3.up * dropHeight;
                        d.SetPrimary(Tags.DATA, meteor);

                        for (int i = 0; i < craterCount; i++)
                        {
                            float angle = (360f / craterCount) * i * Mathf.Deg2Rad;
                            craterDirs[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                        }
                    })
                )
                // Phase: Fall from sky
                .SyncTask((d, dt) =>
                {
                    if (!meteor) return true;

                    meteor.transform.position = Vector3.MoveTowards(
                        meteor.transform.position, targetPos, fallSpeed * dt);
                    meteor.transform.Rotate(Vector3.forward * 300f * dt);

                    // Grow slightly during fall for dramatic effect
                    float distT = 1f - Vector3.Distance(meteor.transform.position, targetPos) / dropHeight;
                    meteor.transform.localScale = Vector3.one * (4f + distT * 2f);

                    if (Vector3.Distance(meteor.transform.position, targetPos) < 1f)
                    {
                        // Impact — destroy meteor, spawn crater ring
                        Object.Destroy(meteor);
                        meteor = null;

                        for (int i = 0; i < craterCount; i++)
                        {
                            var piece = DemoSequences.CreatePrim(PrimitiveType.Cube, withPos: false)
                                .Scale(Vector3.one * Random.Range(0.5f, 1.2f));
                            piece.transform.position = targetPos;
                            craterRing.Add(piece);
                        }
                        
                        var hits = new Collider[6];
                        var size = Physics.OverlapSphereNonAlloc(targetPos, 4f, hits);
                        for (int i = 0; i < size; i++)
                        {
                            d.TryApplyEffects(hits[i].gameObject, Tags.EFFECTS);
                        }
                        
                        return true;
                    }

                    return false;
                })
                // Phase: Crater ring expands outward
                .SyncTask((d, dt) =>
                {
                    impactTimer += dt;
                    float tExpand = Mathf.Clamp01(impactTimer / expandDuration);
                    float easeExpand = 1f - (1f - tExpand) * (1f - tExpand);

                    for (int i = 0; i < craterRing.Count; i++)
                    {
                        if (!craterRing[i]) continue;
                        var pos = targetPos + craterDirs[i] * (craterRadius * easeExpand);
                        pos.y += Mathf.Sin(tExpand * Mathf.PI) * 4f; // Arc upward
                        craterRing[i].transform.position = pos;
                        craterRing[i].transform.Rotate(Vector3.one * 300f * dt);
                    }

                    if (impactTimer >= expandDuration)
                    {
                        impactTimer = 0f;
                        return true;
                    }

                    return false;
                })
                // Phase: Fade/shrink away
                .SyncTask((d, dt) =>
                {
                    impactTimer += dt;
                    float tFade = Mathf.Clamp01(impactTimer / fadeDuration);

                    for (int i = 0; i < craterRing.Count; i++)
                    {
                        if (!craterRing[i]) continue;
                        craterRing[i].transform.localScale *= (1f - dt * 4f);

                        // Drop down
                        var pos = craterRing[i].transform.position;
                        pos.y -= 8f * dt;
                        craterRing[i].transform.position = pos;
                    }

                    return impactTimer >= fadeDuration;
                })
                .OnTerminate((ctx, success) =>
                {
                    if (meteor) Object.Destroy(meteor);
                    foreach (var piece in craterRing)
                        if (piece) Object.Destroy(piece);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // WHIRLWIND — Spinning cubes orbit the caster for a duration
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Targeting: AutoTargetSelfTask (auto-targets the caster, no input needed)
        /// </summary>
        [TaskSequenceMethod("Whirlwind")]
        public static TaskSequence WhirlwindSequence()
        {
            List<GameObject> blades = new();
            int bladeCount = 9;
            float orbitRadius = 14f;
            float orbitSpeed = 360f; // degrees per second
            float duration = 4f;
            float timer = 0f;
            Transform caster = null;

            return TaskSequenceBuilder.Create("Whirlwind")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s
                    .WithName("Setup")
                    .Do(d =>
                    {
                        caster = DemoManager.Instance.Player.transform;

                        for (int i = 0; i < bladeCount; i++)
                        {
                            var blade = DemoSequences.CreatePrim(PrimitiveType.Cube, withPos: false)
                                .Scale(new Vector3(0.3f, 3f, 0.3f));
                            blade.transform.position = caster.position;
                            blades.Add(blade);
                        }
                    })
                )
                // Phase: Spin (single continuous animation)
                .SyncTask((d, dt) =>
                {
                    timer += dt;
                    if (!caster) return true;

                    float angleDeg = timer * orbitSpeed;
                    float heightOscillation = Mathf.Sin(timer * 4f) * 0.8f;

                    // Orbit radius expands then contracts
                    float lifeT = timer / duration;
                    float currentRadius = orbitRadius * Mathf.Sin(lifeT * Mathf.PI);

                    for (int i = 0; i < blades.Count; i++)
                    {
                        if (!blades[i]) continue;

                        float bladeAngle = (angleDeg + (360f / bladeCount) * i) * Mathf.Deg2Rad;
                        var offset = new Vector3(
                            Mathf.Cos(bladeAngle) * currentRadius,
                            1.5f + heightOscillation + Mathf.Sin(bladeAngle * 2f) * 0.5f,
                            Mathf.Sin(bladeAngle) * currentRadius);

                        blades[i].transform.position = caster.position + offset;
                        blades[i].transform.Rotate(Vector3.up * 600f * dt);
                    }

                    return timer >= duration;
                })
                .OnTerminate((ctx, success) =>
                {
                    foreach (var blade in blades)
                        if (blade) Object.Destroy(blade);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // CHAIN LIGHTNING — Line renderers chain between spawned targets
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Targeting: AutoTargetTask (Mode: Nearest, MaxTargets: 3, Range: 30)
        ///
        /// Falls back to spawning dummy targets for demo purposes when no GAS targets exist.
        /// </summary>
        [TaskSequenceMethod("Chain Lightning")]
        public static TaskSequence ChainLightningSequence()
        {
            List<GameObject> bolts = new();
            List<LineRenderer> lines = new();
            Vector3 origin = Vector3.zero;
            Vector3[] chainPoints;
            int chainCount = 4;
            float zapDuration = 0.15f;
            int zapCount = 3;
            int currentZap = 0;
            float timer = 0f;
            float fadeDuration = 0.3f;

            chainPoints = new Vector3[chainCount + 1]; // origin + targets

            return TaskSequenceBuilder.Create("Chain Lightning")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s
                    .WithName("Setup")
                    .Do(d =>
                    {
                        origin = DemoManager.Instance.Player.transform.position + Vector3.up * 1.5f;
                        chainPoints[0] = origin;

                        // Generate chain target positions radiating outward
                        var forward = DemoManager.Instance.Player.transform.forward;
                        for (int i = 1; i <= chainCount; i++)
                        {
                            float angle = Random.Range(-40f, 40f) * Mathf.Deg2Rad;
                            var dir = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up) * forward;
                            chainPoints[i] = chainPoints[i - 1] + dir * Random.Range(5f, 10f);
                            chainPoints[i].y = origin.y + Random.Range(-1f, 2f);
                        }

                        // Create line renderers for each chain segment
                        for (int i = 0; i < chainCount; i++)
                        {
                            var obj = new GameObject($"Lightning_{i}");
                            var lr = obj.AddComponent<LineRenderer>();
                            lr.material = new Material(Shader.Find("Sprites/Default"));
                            lr.startColor = new Color(0.4f, 0.7f, 1f);
                            lr.endColor = Color.white;
                            lr.startWidth = 0.3f;
                            lr.endWidth = 0.15f;
                            lr.positionCount = 2;
                            lr.SetPosition(0, chainPoints[i]);
                            lr.SetPosition(1, chainPoints[i]);
                            lr.enabled = false;

                            bolts.Add(obj);
                            lines.Add(lr);

                            // Marker sphere at each target point
                            var marker = DemoSequences.CreatePrim(PrimitiveType.Sphere, withPos: false)
                                .Scale(Vector3.one * 0.6f);
                            marker.transform.position = chainPoints[i + 1];
                            bolts.Add(marker);
                        }
                    })
                )
                // Phase: Progressive chain zap cycles
                .SyncTask((d, dt) =>
                {
                    timer += dt;

                    // Progressive chain reveal
                    float zapProgress = timer / zapDuration;
                    int activeSegments = Mathf.Min(Mathf.FloorToInt(zapProgress) + 1, chainCount);

                    for (int i = 0; i < activeSegments && i < lines.Count; i++)
                    {
                        if (!lines[i]) continue;
                        lines[i].enabled = true;

                        // Add jitter to mid-points for electric effect
                        var from = chainPoints[i];
                        var to = chainPoints[i + 1];
                        var mid = Vector3.Lerp(from, to,
                            Mathf.Clamp01((zapProgress - i) / 1f));

                        // Jitter
                        mid += Random.insideUnitSphere * 0.4f;

                        lines[i].positionCount = 3;
                        lines[i].SetPosition(0, from);
                        lines[i].SetPosition(1, mid);
                        lines[i].SetPosition(2, to);
                    }

                    if (activeSegments >= chainCount && timer >= zapDuration * chainCount)
                    {
                        currentZap++;
                        if (currentZap >= zapCount)
                        {
                            return true;
                        }

                        timer = 0f; // Reset for next zap cycle
                    }

                    return false;
                })
                // Phase: Fade out all lines
                .SyncTask((d, dt) =>
                {
                    // Reset timer for fade phase on first frame
                    if (timer > 0f) timer = 0f;

                    timer += dt;
                    float tFade = Mathf.Clamp01(timer / fadeDuration);
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (!lines[i]) continue;
                        float width = Mathf.Lerp(0.3f, 0f, tFade);
                        lines[i].startWidth = width;
                        lines[i].endWidth = width * 0.5f;
                    }

                    return timer >= fadeDuration;
                })
                .OnTerminate((ctx, success) =>
                {
                    foreach (var bolt in bolts)
                        if (bolt) Object.Destroy(bolt);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // DASH STRIKE — Rapid movement to target, leaves after-image trail
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Targeting: SelectGasOrPositionTargetTask (left click to select entity or ground position)
        /// </summary>
        [TaskSequenceMethod("Dash Strike")]
        public static TaskSequence DashStrikeSequence()
        {
            List<GameObject> afterImages = new();
            GameObject striker = null;
            Vector3 startPos = Vector3.zero;
            Vector3 targetPos = Vector3.zero;
            float dashDuration = 0.25f;
            float trailFadeDuration = 0.8f;
            float timer = 0f;

            float impactTimer = 0f;
            int imageCount = 6;

            return TaskSequenceBuilder.Create("Dash Strike")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s
                    .WithName("Setup")
                    .Do(d =>
                    {
                        startPos = DemoManager.Instance.Player.transform.position;

                        if (d.GetTargetingPacket(Tags.TARGET, out var target, false))
                            targetPos = target.transform != null ? target.transform.position : target.position;
                        else
                            targetPos = startPos + DemoManager.Instance.Player.transform.forward * 15f;

                        striker = DemoSequences.CreatePrim(PrimitiveType.Capsule, withPos: false)
                            .Scale(new Vector3(0.8f, 1f, 0.8f));
                        striker.transform.position = startPos;

                        // Look at target
                        var dir = (targetPos - startPos).normalized;
                        if (dir != Vector3.zero)
                            striker.transform.rotation = Quaternion.LookRotation(dir);

                        d.SetPrimary(Tags.DATA, striker);
                    })
                )
                // Phase: Dash toward target, spawning after-images
                .SyncTask((d, dt) =>
                {
                    timer += dt;
                    float tDash = Mathf.Clamp01(timer / dashDuration);
                    float easeDash = tDash * tDash; // EaseIn

                    if (striker)
                        striker.transform.position = Vector3.Lerp(startPos, targetPos, easeDash);

                    // Spawn after-images along the path
                    int expectedImages = Mathf.FloorToInt(tDash * imageCount);
                    while (afterImages.Count < expectedImages)
                    {
                        float imgT = (float)afterImages.Count / imageCount;
                        var imgPos = Vector3.Lerp(startPos, targetPos, imgT);

                        var image = DemoSequences.CreatePrim(PrimitiveType.Capsule, withPos: false)
                            .Scale(new Vector3(0.8f, 1f, 0.8f));
                        image.transform.position = imgPos;
                        if (striker)
                            image.transform.rotation = striker.transform.rotation;
                        afterImages.Add(image);
                    }

                    return timer >= dashDuration;
                })
                // Phase: Impact punch at target
                .SyncTask((d, dt) =>
                {
                    impactTimer += dt;
                    float punchDur = 0.3f;
                    float tPunch = Mathf.Clamp01(impactTimer / punchDur);

                    if (striker)
                    {
                        float punch = 1f + 0.8f * Mathf.Sin(tPunch * Mathf.PI);
                        striker.transform.localScale = new Vector3(0.8f, 1f, 0.8f) * punch;
                    }

                    if (impactTimer >= punchDur)
                    {
                        if (striker) Object.Destroy(striker);
                        striker = null;
                        
                        var hits = new Collider[6];
                        var size = Physics.OverlapSphereNonAlloc(targetPos, 1f, hits);
                        for (int i = 0; i < size; i++)
                        {
                            d.TryApplyEffects(hits[i].gameObject, Tags.EFFECTS);
                        }
                        
                        return true;
                    }

                    return false;
                })
                .Do(d => timer = 0f)
                // Phase: Trail fades away
                .SyncTask((d, dt) =>
                {
                    timer += dt;
                    float tFade = Mathf.Clamp01(timer / trailFadeDuration);

                    for (int i = 0; i < afterImages.Count; i++)
                    {
                        if (!afterImages[i]) continue;
                        // Staggered fade: earlier images fade first
                        float stagger = (float)i / afterImages.Count;
                        float localT = Mathf.Clamp01((tFade - stagger * 0.3f) / 0.7f);
                        float scale = Mathf.Lerp(1f, 0f, localT);
                        afterImages[i].transform.localScale = new Vector3(0.8f, 1f, 0.8f) * scale;
                    }

                    return timer >= trailFadeDuration;
                })
                .OnTerminate((ctx, success) =>
                {
                    if (striker) Object.Destroy(striker);
                    foreach (var img in afterImages)
                        if (img) Object.Destroy(img);
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ARCANE BARRAGE — Multiple projectiles launch in a spread and converge on target
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Targeting: SelectGasOrPositionTargetTask (left click to select entity or ground position)
        /// </summary>
        [TaskSequenceMethod("Arcane Barrage")]
        public static TaskSequence ArcaneBarrageSequence()
        {
            List<GameObject> orbs = new();
            int orbCount = 9;
            float staggerInterval = 0.22f;
            float arcHeight = 16f;
            float travelDuration = 0.8f;
            float timer = 0f;
            Vector3 origin = Vector3.zero;
            Vector3 targetPos = Vector3.zero;
            Transform liveTarget = null;

            // Each orb has its own launch offset and progress
            Vector3[] launchOffsets;
            float[] launchTimes;

            launchOffsets = new Vector3[orbCount];
            launchTimes = new float[orbCount];

            return TaskSequenceBuilder.Create("Arcane Barrage")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.Update)
                .Stage(s => s
                    .WithName("Setup")
                    .Do(d =>
                    {
                        origin = DemoManager.Instance.Player.transform.position + Vector3.up * 2f;

                        if (d.GetTargetingPacket(Tags.TARGET, out var target, false))
                        {
                            liveTarget = target.transform;
                            targetPos = target.position;
                        }
                        else
                        {
                            targetPos = origin + DemoManager.Instance.Player.transform.forward * 15f;
                        }

                        for (int i = 0; i < orbCount; i++)
                        {
                            var orb = DemoSequences.CreatePrim(PrimitiveType.Sphere, withPos: false)
                                .Scale(Vector3.one * 1.2f);
                            orb.transform.position = origin;
                            orbs.Add(orb);

                            // Spread the launch offsets in an arc above the caster
                            float spread = ((float)i / (orbCount - 1) - 0.5f) * 2f;
                            launchOffsets[i] = new Vector3(spread * 4f, Random.Range(2f, 5f), spread * 2f);
                            launchTimes[i] = i * staggerInterval;
                        }
                    })
                )
                // Phase: Launch and converge (single continuous animation)
                .SyncTask((d, dt) =>
                {
                    timer += dt;
                    var dest = liveTarget != null ? liveTarget.position : targetPos;

                    bool anyAlive = false;
                    for (int i = 0; i < orbs.Count; i++)
                    {
                        if (!orbs[i]) continue;

                        float orbTime = timer - launchTimes[i];
                        if (orbTime < 0f)
                        {
                            anyAlive = true;
                            continue;
                        }

                        float t = Mathf.Clamp01(orbTime / travelDuration);

                        // Phase 1 (0-0.3): rise to launch offset
                        // Phase 2 (0.3-1.0): arc down to target
                        Vector3 pos;
                        if (t < 0.3f)
                        {
                            float riseT = t / 0.3f;
                            float easeRise = 1f - (1f - riseT) * (1f - riseT);
                            pos = Vector3.Lerp(origin, origin + launchOffsets[i], easeRise);
                        }
                        else
                        {
                            float convergeT = (t - 0.3f) / 0.7f;
                            float easeCurve = convergeT * convergeT;
                            var peak = origin + launchOffsets[i];
                            pos = Vector3.Lerp(peak, dest, easeCurve);
                            pos.y += Mathf.Sin(convergeT * Mathf.PI) * arcHeight * (1f - convergeT);
                        }

                        orbs[i].transform.position = pos;
                        orbs[i].transform.localScale = Vector3.one * (1.2f - t * 0.6f);

                        if (t >= 1f)
                        {
                            Object.Destroy(orbs[i]);
                            orbs[i] = null;
                            
                            var hits = new Collider[6];
                            var size = Physics.OverlapSphereNonAlloc(dest, .5f, hits);
                            for (int j = 0; j < size; j++)
                            {
                                d.TryApplyEffects(hits[j].gameObject, Tags.EFFECTS);
                            }
                            
                        }
                        else
                        {
                            anyAlive = true;
                        }
                    }

                    return !anyAlive;
                })
                .OnTerminate((ctx, success) =>
                {
                    foreach (var orb in orbs)
                        if (orb) Object.Destroy(orb);
                })
                .BuildSequence();
        }
    }
}
