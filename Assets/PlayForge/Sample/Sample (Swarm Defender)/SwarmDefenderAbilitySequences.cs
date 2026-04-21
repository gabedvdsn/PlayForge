using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.SwarmDefenderSample
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Local tag constants for the Swarm Defender sample.
    // Global Tags.GAMEOBJECT / Tags.ABILITIES / Tags.EFFECTS are reused from the
    // framework. Anything sample-specific lives here to avoid polluting the
    // shared Tags registry.
    // ═══════════════════════════════════════════════════════════════════════════
    public static class SwarmTags
    {
        public static readonly Tag HERO              = Tag.Generate("SD.Hero");
        public static readonly Tag ACTIVE_ENEMIES    = Tag.Generate("SD.ActiveEnemies");
        public static readonly Tag ENEMY_PREFABS     = Tag.Generate("SD.EnemyPrefabs");
        public static readonly Tag BOSS_PREFABS      = Tag.Generate("SD.BossPrefabs");
        public static readonly Tag SPAWN_RADIUS      = Tag.Generate("SD.SpawnRadius");
        public static readonly Tag ELAPSED_TIME      = Tag.Generate("SD.ElapsedTime");
        public static readonly Tag WAVE_INDEX        = Tag.Generate("SD.WaveIndex");
        public static readonly Tag LEVEL_UP_PENDING  = Tag.Generate("SD.LevelUpPending");
        public static readonly Tag LEVEL_UP_CHOICE   = Tag.Generate("SD.LevelUpChoiceMade");
        public static readonly Tag CHOSEN_ABILITY    = Tag.Generate("SD.ChosenAbility");
        public static readonly Tag CHOSE_NEW_ABILITY = Tag.Generate("SD.ChoseNewAbility");
        public static readonly Tag GAME_OVER         = Tag.Generate("SD.GameOver");
    }

    /// <summary>
    /// Authored task sequences that drive the Swarm Defender sample game loop:
    /// hero casting, enemy AI, spawning (constant / waves / boss rounds),
    /// level-up UI pause, and the overall game chain.
    ///
    /// Assumed data packet contents at start:
    ///   Tags.GAMEOBJECT      → Hero                              (primary)
    ///   Tags.ABILITIES       → List&lt;Ability&gt; preset          (TryGetLoadedAssets)
    ///   Tags.EFFECTS         → List&lt;GameplayEffect&gt; (cd+dmg amp buffs)
    ///   SwarmTags.ENEMY_PREFABS → List&lt;Character&gt; prefabs    (primary list)
    ///   SwarmTags.BOSS_PREFABS  → List&lt;Character&gt; prefabs
    ///   SwarmTags.SPAWN_RADIUS  → float (XZ offscreen radius)
    /// </summary>
    public static class SwarmDefenderAbilitySequences
    {
        private const float DEFAULT_SPAWN_RADIUS = 20f;
        private const float HERO_CAST_COOLDOWN   = 0.3f;    // min delay between cast attempts

        // ═══════════════════════════════════════════════════════════════════════
        // HERO — continuous ability casting
        // ═══════════════════════════════════════════════════════════════════════

        [TaskSequenceMethod("SD: Hero Routine")]
        public static TaskSequence HeroRoutine()
        {
            Hero hero = null;
            int cursor = 0;

            return TaskSequenceBuilder.Create("Hero Routine")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d => hero = d.GetPrimary<Hero>(SwarmTags.HERO))
                .Stage(s => s
                    .WithName("Cast Loop")
                    .WithRepeat(true)
                    .StopRepeatWhen(_ => hero == null || hero.IsDead)
                    .Task(async (d, t) =>
                    {
                        // Mandatory yield per iteration — prevents sync-loop freezes when every
                        // path below early-returns (e.g., if `hero` vanishes between iterations).
                        await UniTask.Yield(t);

                        Debug.Log(hero);
                        Debug.Log(hero is null ? "Null hero" : hero.IsDead);
                        Debug.Log(hero is null || hero.IsDead);
                        
                        if (hero == null || hero.IsDead) return;

                        int count = hero.AbilitySystem.AbilityCount;
                        if (count <= 0)
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(HERO_CAST_COOLDOWN), cancellationToken: t);
                            return;
                        }

                        // One ability per tick — walk the list so no ability starves.
                        for (int i = 0; i < count; i++)
                        {
                            int idx = (cursor + i) % count;
                            if (!hero.AbilitySystem.CanActivateAbility(idx))
                            {
                                Debug.Log("cant activate");
                                continue;
                            }
                            var req = hero.AbilitySystem.CreateActivationRequest(idx, ProcessDataPacket.Default());
                            if (hero.AbilitySystem.TryActivateAbility(req))
                            {
                                cursor = (idx + 1) % count;
                                break;
                            }
                        }

                        await UniTask.Delay(TimeSpan.FromSeconds(HERO_CAST_COOLDOWN), cancellationToken: t);
                    })
                )
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // ENEMIES — basic melee-explosion behaviour
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Walk toward the hero; once within AttackRadius, pause for AttackPause
        /// seconds, then "explode" — if the hero is still in range, the hero dies.
        /// </summary>
        [TaskSequenceMethod("SD: Enemy Behaviour")]
        public static TaskSequence EnemyBehaviour()
        {
            Character enemy = null;
            Hero hero = null;
            IAttribute speedAttr = AttributeRegistry.GetByName("MoveSpeed");
            IAttribute radiusAttr = AttributeRegistry.GetByName("AttackRadius");
            IAttribute pauseAttr = AttributeRegistry.GetByName("AttackPause");

            return TaskSequenceBuilder.Create("Enemy Behaviour")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d =>
                {
                    enemy = d.GetPrimary<Character>(Tags.GAMEOBJECT);
                    hero = d.GetPrimary<Hero>(SwarmTags.HERO);
                })
                .Stage(s => s
                    .WithName("Chase Hero")
                    .SkipWhen(_ => enemy == null || enemy.IsDead || hero == null || hero.IsDead)
                    .Task(async (d, t) =>
                    {
                        float speed = ReadAttr(enemy, speedAttr, fallback: 2f);
                        float radius = ReadAttr(enemy, radiusAttr, fallback: 1.5f);
                        await SequenceTaskLibrary.MoveTowards(
                            enemy.transform, hero.transform, speed, t, stoppingDistance: radius * 0.9f);
                    })
                )
                .Stage(s => s
                    .WithName("Arm & Explode")
                    .SkipWhen(_ => enemy == null || enemy.IsDead)
                    .Task(async (d, t) =>
                    {
                        float pause = ReadAttr(enemy, pauseAttr, fallback: 0.75f);
                        // Little telegraph: pulse scale up while armed
                        await SequenceTaskLibrary.PunchScale(enemy.transform, 0.4f, pause, t);
                    })
                    .Do(d =>
                    {
                        if (enemy == null || enemy.IsDead || hero == null || hero.IsDead) return;
                        float radius = ReadAttr(enemy, radiusAttr, fallback: 1.5f);
                        float sqr = (hero.transform.position - enemy.transform.position).sqrMagnitude;
                        if (sqr <= radius * radius) hero.MarkDead();
                        enemy.MarkDead();
                    })
                )
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // BOSS MOVEMENT VARIANTS
        // Each returns when the boss reaches the hero's position or the boss dies.
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>Boss jumps in arcs from spot to spot, closing in each jump.</summary>
        [TaskSequenceMethod("SD: Boss - Jumper")]
        public static TaskSequence BossJumper()
        {
            Character boss = null;
            Hero hero = null;

            return TaskSequenceBuilder.Create("Boss Jumper")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d =>
                {
                    boss = d.GetPrimary<Character>(Tags.GAMEOBJECT);
                    hero = d.GetPrimary<Hero>(SwarmTags.HERO);
                })
                .Stage(s => s
                    .WithName("Jump Toward Hero")
                    .WithRepeat(true)
                    .StopRepeatWhen(_ =>
                        boss == null || boss.IsDead || hero == null || hero.IsDead ||
                        (hero.transform.position - boss.transform.position).sqrMagnitude < 4f)
                    .Task(async (d, t) =>
                    {
                        Vector3 from = boss.transform.position;
                        Vector3 toward = Vector3.MoveTowards(from, hero.transform.position, 4f);
                        // Add lateral jitter so it looks boss-like
                        Vector3 lateral = UnityEngine.Random.insideUnitCircle.ToXZ() * 2f;
                        Vector3 dest = toward + lateral;

                        await SequenceTaskLibrary.ArcTo(boss.transform, dest, 0.55f, 3f, t);
                        await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: t);
                    })
                )
                .BuildSequence();
        }

        /// <summary>Boss slides along a sine wave toward the hero.</summary>
        [TaskSequenceMethod("SD: Boss - Sine Walker")]
        public static TaskSequence BossSineWalker()
        {
            Character boss = null;
            Hero hero = null;
            float elapsed = 0f;

            return TaskSequenceBuilder.Create("Boss Sine Walker")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d =>
                {
                    boss = d.GetPrimary<Character>(Tags.GAMEOBJECT);
                    hero = d.GetPrimary<Hero>(SwarmTags.HERO);
                })
                .Stage(s => s
                    .WithName("Sine Walk")
                    .WithRepeat(true)
                    .StopRepeatWhen(_ =>
                        boss == null || boss.IsDead || hero == null || hero.IsDead ||
                        (hero.transform.position - boss.transform.position).sqrMagnitude < 4f)
                    .Task(async (d, t) =>
                    {
                        float speed = 3f;
                        float amp   = 2f;
                        float freq  = 2f;

                        Vector3 toHero = hero.transform.position - boss.transform.position;
                        toHero.y = 0f;
                        Vector3 forward = toHero.sqrMagnitude < 0.0001f ? Vector3.forward : toHero.normalized;
                        Vector3 side = Vector3.Cross(Vector3.up, forward);

                        elapsed += Time.deltaTime;
                        Vector3 step = forward * speed * Time.deltaTime;
                        Vector3 lateral = side * (Mathf.Sin(elapsed * freq) * amp * Time.deltaTime);

                        boss.transform.position += step + lateral;
                        await UniTask.NextFrame(t);
                    })
                )
                .BuildSequence();
        }

        /// <summary>Boss pauses, then charges in a straight line; repeats.</summary>
        [TaskSequenceMethod("SD: Boss - Charger")]
        public static TaskSequence BossCharger()
        {
            Character boss = null;
            Hero hero = null;

            return TaskSequenceBuilder.Create("Boss Charger")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d =>
                {
                    boss = d.GetPrimary<Character>(Tags.GAMEOBJECT);
                    hero = d.GetPrimary<Hero>(SwarmTags.HERO);
                })
                .Stage(s => s
                    .WithName("Telegraph")
                    .WithRepeat(true)
                    .StopRepeatWhen(_ =>
                        boss == null || boss.IsDead || hero == null || hero.IsDead ||
                        (hero.transform.position - boss.transform.position).sqrMagnitude < 4f)
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.LookAt(boss.transform, hero.transform, 0.4f, t);
                        await SequenceTaskLibrary.PunchScale(boss.transform, 0.3f, 0.5f, t);

                        // Charge halfway to hero
                        Vector3 dest = Vector3.Lerp(boss.transform.position, hero.transform.position, 0.6f);
                        await SequenceTaskLibrary.MoveTo(boss.transform, dest, 0.4f, t);

                        await UniTask.Delay(TimeSpan.FromSeconds(0.35f), cancellationToken: t);
                    })
                )
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SPAWNERS — constant stream, waves, and boss rounds
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Spawns a single enemy at a random off-screen position on the XZ plane
        /// and fires off its behaviour sequence as fire-and-forget.
        /// </summary>
        [TaskSequenceMethod("SD: Spawn Enemy")]
        public static TaskSequence SpawnEnemy()
        {
            Hero hero = null;

            return TaskSequenceBuilder.Create("Spawn Enemy")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d =>
                {
                    hero = d.GetPrimary<Hero>(SwarmTags.HERO) ?? d.GetPrimary<Hero>(Tags.GAMEOBJECT);
                })
                .Task(async (d, t) =>
                {
                    if (hero == null || hero.IsDead) return;

                    var prefabs = ResolveCharacterList(d, SwarmTags.ENEMY_PREFABS);
                    if (prefabs.Count == 0) return;

                    float radius = d.GetPrimary<float>(SwarmTags.SPAWN_RADIUS);
                    if (radius <= 0f) radius = DEFAULT_SPAWN_RADIUS;

                    var prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
                    var pos = RandomOnRing(hero.transform.position, radius);
                    var enemy = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);

                    RegisterEnemy(d, enemy);
                    LaunchEnemyBehaviour(d, hero, enemy, t);
                    await UniTask.CompletedTask;
                })
                .BuildSequence();
        }

        /// <summary>
        /// Spawns a configurable wave of enemies in quick succession and waits
        /// for the wave to be cleared (or the hero to die).
        /// </summary>
        [TaskSequenceMethod("SD: Spawn Wave")]
        public static TaskSequence SpawnWave()
        {
            Hero hero = null;
            List<Character> activeEnemies = null;

            return TaskSequenceBuilder.Create("Spawn Wave")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d =>
                {
                    hero = d.GetPrimary<Hero>(SwarmTags.HERO);
                    activeEnemies = GetOrCreateEnemyList(d);
                })
                .Task(async (d, t) =>
                {
                    if (hero == null || hero.IsDead) return;

                    var prefabs = ResolveCharacterList(d, SwarmTags.ENEMY_PREFABS);
                    if (prefabs.Count == 0) return;

                    float radius = d.GetPrimary<float>(SwarmTags.SPAWN_RADIUS);
                    if (radius <= 0f) radius = DEFAULT_SPAWN_RADIUS;

                    // Wave scales with elapsed playtime.
                    float elapsed = d.GetPrimary<float>(SwarmTags.ELAPSED_TIME);
                    int waveSize = Mathf.Clamp(5 + Mathf.FloorToInt(elapsed / 20f), 5, 40);

                    for (int i = 0; i < waveSize; i++)
                    {
                        if (hero.IsDead) return;
                        var prefab = prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
                        var pos = RandomOnRing(hero.transform.position, radius);
                        var enemy = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                        RegisterEnemy(d, enemy);
                        LaunchEnemyBehaviour(d, hero, enemy, t);
                        await UniTask.Delay(TimeSpan.FromSeconds(0.12f), cancellationToken: t);
                    }
                })
                .Stage(s => s
                    .WithName("Wait for wave clear")
                    .DelayUntil(_ =>
                        hero == null || hero.IsDead ||
                        activeEnemies == null || activeEnemies.TrueForAll(e => e == null || e.IsDead))
                )
                .BuildSequence();
        }

        /// <summary>
        /// Spawns a single boss with a randomized movement variant, waits for it
        /// to die, then drops a random buff (damage amp or cooldown reduction)
        /// onto the hero.
        /// </summary>
        [TaskSequenceMethod("SD: Boss Round")]
        public static TaskSequence BossRound()
        {
            Hero hero = null;
            Character boss = null;

            return TaskSequenceBuilder.Create("Boss Round")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d => hero = d.GetPrimary<Hero>(SwarmTags.HERO))
                .Task(async (d, t) =>
                {
                    if (hero == null || hero.IsDead) return;

                    var bossPrefabs = ResolveCharacterList(d, SwarmTags.BOSS_PREFABS);
                    if (bossPrefabs.Count == 0) return;

                    float radius = d.GetPrimary<float>(SwarmTags.SPAWN_RADIUS);
                    if (radius <= 0f) radius = DEFAULT_SPAWN_RADIUS;

                    var prefab = bossPrefabs[UnityEngine.Random.Range(0, bossPrefabs.Count)];
                    var pos = RandomOnRing(hero.transform.position, radius + 4f);
                    boss = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
                    RegisterEnemy(d, boss);

                    // Pick a movement variant at random.
                    var bossData = ProcessDataPacket.Default();
                    bossData.SetPrimary(Tags.GAMEOBJECT, boss);
                    bossData.SetPrimary(SwarmTags.HERO, hero);

                    TaskSequence variant = UnityEngine.Random.Range(0, 3) switch
                    {
                        0 => BossJumper(),
                        1 => BossSineWalker(),
                        _ => BossCharger()
                    };

                    // Fire movement loop in parallel; it'll self-terminate when the
                    // boss dies via its StopRepeatWhen guard.
                    variant.Run(bossData, t).Forget();

                    // Announcement pulse so the player notices.
                    await SequenceTaskLibrary.PunchScale(boss.transform, 0.6f, 0.4f, t);
                })
                .Stage(s => s
                    .WithName("Wait for boss defeat")
                    .DelayUntil(_ => hero == null || hero.IsDead || boss == null || boss.IsDead)
                )
                .Stage(s => s
                    .WithName("Drop buff")
                    .SkipWhen(_ => hero == null || hero.IsDead)
                    .Do(d => ApplyRandomBuff(d, hero))
                )
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // XP / LEVEL-UP
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Flags the data packet that a level-up is pending, then waits for the
        /// UI to set SwarmTags.LEVEL_UP_CHOICE = true. The UI is responsible for
        /// setting CHOSEN_ABILITY (the Ability instance) and CHOSE_NEW_ABILITY
        /// (true = grant new, false = level up existing) before toggling the flag.
        /// </summary>
        [TaskSequenceMethod("SD: Level Up")]
        public static TaskSequence LevelUp()
        {
            Hero hero = null;

            return TaskSequenceBuilder.Create("Level Up")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d =>
                {
                    hero = d.GetPrimary<Hero>(SwarmTags.HERO) ?? d.GetPrimary<Hero>(Tags.GAMEOBJECT);
                    d.SetPrimary(SwarmTags.LEVEL_UP_PENDING, true);
                    d.SetPrimary(SwarmTags.LEVEL_UP_CHOICE, false);
                    d.SetPrimary<Ability>(SwarmTags.CHOSEN_ABILITY, null);

                        Debug.Log(hero);
                    Debug.Log("LEVEL UP");
                })
                .Stage(s => s
                    .WithName("Wait for UI choice")
                    .DelayUntil(d => d.GetPrimary<bool>(SwarmTags.LEVEL_UP_CHOICE))
                )
                .Do(d =>
                {
                    Debug.Log(hero);
                    if (hero == null) return;

                    var chosen = d.GetPrimary<Ability>(SwarmTags.CHOSEN_ABILITY);
                    bool isNew = d.GetPrimary<bool>(SwarmTags.CHOSE_NEW_ABILITY);

                    if (chosen != null)
                    {
                        if (isNew)
                        {
                            if (hero.AbilitySystem.CanGiveAbility(chosen))
                                hero.AbilitySystem.GiveAbility(chosen, chosen.StartingLevel, out _);
                            Debug.Log($"Given {chosen.GetName()} at level {hero.GetLevel(chosen.GetAssetTag()).CurrentValue.ToString()}");
                        }
                        else
                        {
                            // Level up existing — find the granted spec and bump its level.
                            LevelUpExistingAbility(hero, chosen);
                        }
                    }

                    // Bump the hero's own level and clear pending flag.\
                    hero.ModifyLevel(hero.GetAssetTag(), new IntValuePair(1, 0));
                    
                    // hero.SetLevel(hero.GetLevel() + 1);
                    d.SetPrimary(SwarmTags.LEVEL_UP_PENDING, false);
                    d.SetPrimary(SwarmTags.LEVEL_UP_CHOICE, false);
                })
                .BuildSequence();
        }

        /// <summary>
        /// Background watcher: if Experience ≥ threshold, drain it, fire the
        /// level-up sequence, wait for it to resolve, then resume.
        /// Threshold curve: 100 * currentLevel.
        /// </summary>
        [TaskSequenceMethod("SD: XP Watcher")]
        public static TaskSequence XpWatcher()
        {
            Hero hero = null;
            IAttribute xpAttr = AttributeRegistry.GetByName("Experience");

            return TaskSequenceBuilder.Create("XP Watcher")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d => hero = d.GetPrimary<Hero>(SwarmTags.HERO) ?? d.GetPrimary<Hero>(Tags.GAMEOBJECT))
                .Stage(s => s
                    .WithName("Watch XP")
                    .WithRepeat(true)
                    .StopRepeatWhen(_ => hero == null || hero.IsDead)
                    .Task(async (d, t) =>
                    {
                        // Mandatory yield per iteration — the `hero == null` path below is a
                        // synchronous return, which would otherwise tight-loop the stage repeat.
                        await UniTask.Yield(t);

                        if (hero == null || hero.IsDead) return;

                        float xp = ReadAttr(hero, xpAttr, fallback: 0f);
                        float threshold = 100f * Mathf.Max(1, hero.GetLevel().CurrentValue);

                        if (hero.TryGetAttributeValue(xpAttr, out var attrValue) && attrValue.RatioMinZero >= 1)
                        {
                            // Drain XP by the threshold amount so the overflow carries over.
                            WriteAttrDelta(hero, xpAttr, -threshold);

                            // Run the level-up flow inline and wait for it to finish.
                            await LevelUp().Run(d, t);
                        }
                        else await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: t);
                        
                        /*if (xp >= threshold)
                        {
                            // Drain XP by the threshold amount so the overflow carries over.
                            WriteAttrDelta(hero, xpAttr, -threshold);

                            // Run the level-up flow inline and wait for it to finish.
                            await LevelUp().Run(d, t);
                        }
                        else
                        {
                            await UniTask.Delay(TimeSpan.FromSeconds(0.25f), cancellationToken: t);
                        }*/
                    })
                )
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // MAIN GAME LOOP — repeats spawn modes, tracks elapsed time
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Main gameplay loop. Gives the hero an initial level-up, runs the XP
        /// watcher + hero cast loop in parallel, and alternates between constant
        /// spawning, waves, and boss rounds until the hero dies.
        /// </summary>
        [TaskSequenceMethod("SD: Main Game Loop")]
        public static TaskSequence MainGameLoop()
        {
            Hero hero = null;
            float modeTimer = 0f;
            int modeIndex = 0;

            return TaskSequenceBuilder.Create("Main Game Loop")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .InterruptWhen(d => d.GetPrimary<bool>(SwarmTags.GAME_OVER))
                .Do(d =>
                {
                    hero = d.GetPrimary<Hero>(Tags.GAMEOBJECT);
                    d.SetPrimary(SwarmTags.HERO, hero);
                    d.SetPrimary(SwarmTags.ELAPSED_TIME, 0f);
                    d.SetPrimary(SwarmTags.WAVE_INDEX, 0);
                    GetOrCreateEnemyList(d);

                    Debug.Log(hero);

                    Debug.Log($"Game Loop Initialized");
                })
                // Initial level-up at game start.
                .Task(async (d, t) => { await LevelUp().Run(d, t); })
                // Kick off the hero cast loop + XP watcher in parallel.
                .Do(d =>
                {
                    Debug.Log($"{d.GetPrimary<Hero>(SwarmTags.HERO)}");
                    HeroRoutine().RegisterAndRun(d);
                    XpWatcher().RegisterAndRun(d);
                })
                .Stage(s => s
                    .WithName("Spawn Mode Cycler")
                    .WithRepeat(true)
                    .StopRepeatWhen(_ => hero == null || hero.IsDead)
                    .Task(async (d, t) =>
                    {
                        // Unconditional yield per iteration — CheckInjectConditions returns sync
                        // when the predicate is false, and the short-interval path below can also
                        // complete sync (SpawnEnemy has no awaits). Without this, Unity freezes.
                        await UniTask.Yield(t);

                        if (hero is null || hero.IsDead)
                        {
                            d.Inject(SkipStageInjection.Instance);
                            return;
                        }

                        Debug.Log($"Enemy loop stage");
                        
                        // Tick elapsed time.
                        d.IncrementFloat(SwarmTags.ELAPSED_TIME, Time.deltaTime);
                        modeTimer += Time.deltaTime;

                        // Every ~15 seconds, advance to the next mode.
                        // 0 = constant spawn, 1 = wave, 2 = boss round
                        if (modeIndex == 0 || modeIndex == 2)
                        {
                            // Constant spawning: one enemy every ~0.75s for 15s.
                            float interval = Mathf.Lerp(1.2f, 0.35f,
                                Mathf.Clamp01(d.GetPrimary<float>(SwarmTags.ELAPSED_TIME) / 120f));
                            await SpawnEnemy().Run(d, t);
                            await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: t);

                            if (modeTimer >= 15f) { modeTimer = 0f; modeIndex += 1; }
                        }
                        else if (modeIndex == 1)
                        {
                            await SpawnWave().Run(d, t);
                            d.Increment(SwarmTags.WAVE_INDEX);
                            modeTimer = 0f;
                            modeIndex += 1;
                        }
                        else
                        {
                            await BossRound().Run(d, t);
                            modeTimer = 0f;
                            modeIndex = 0;
                        }
                    })
                )
                // Mark GAME_OVER when we fall out of the loop (hero died).
                .Do(d => d.SetPrimary(SwarmTags.GAME_OVER, true))
                .BuildSequence();
        }

        /// <summary>
        /// Idle sequence that keeps running until the UI resets the GAME_OVER flag
        /// (the UI handles the "play again" decision).
        /// </summary>
        [TaskSequenceMethod("SD: Game Over")]
        public static TaskSequence GameOver()
        {
            return TaskSequenceBuilder.Create("Game Over")
                .WithLifecycle(EProcessLifecycle.SelfTerminating, EProcessStepTiming.None)
                .Do(d =>
                {
                    // Clean up any surviving enemies.
                    var list = d.GetPrimary<List<Character>>(SwarmTags.ACTIVE_ENEMIES);
                    if (list != null)
                    {
                        foreach (var e in list)
                        {
                            if (e != null && !e.IsDead) UnityEngine.Object.Destroy(e.gameObject);
                        }
                        list.Clear();
                    }
                })
                .Stage(s => s
                    .WithName("Idle until restart")
                    .DelayUntil(d => !d.GetPrimary<bool>(SwarmTags.GAME_OVER))
                )
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // TOP-LEVEL CHAIN FACTORY
        // Not a [TaskSequenceMethod] — construction helper called from a MonoBehaviour.
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the top-level game chain: [MainGameLoop → GameOver], looping.
        /// When the hero dies, MainGameLoop marks GAME_OVER and falls through to
        /// GameOver. When the UI clears GAME_OVER, GameOver exits and (if the chain
        /// is set to repeat) the loop restarts.
        /// </summary>
        public static TaskSequenceChain BuildGameChain(bool repeat = true)
        {
            return new TaskSequenceChain(MainGameLoop())
                .Then(GameOver())
                .WithRepeat(repeat);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════

        private static void LaunchEnemyBehaviour(ProcessDataPacket parentData, Hero hero, Character enemy, CancellationToken parentToken)
        {
            /*
            var data = ProcessDataPacket.Default();
            */
            var data = new SequenceDataPacket(parentData);
            data.SetPrimary(Tags.GAMEOBJECT, enemy);
            data.SetPrimary(SwarmTags.HERO, hero);
            EnemyBehaviour().Run(data, parentToken).Forget();
        }

        private static List<Character> GetOrCreateEnemyList(ProcessDataPacket d)
        {
            return d.GetOrInit(SwarmTags.ACTIVE_ENEMIES, new List<Character>());
        }

        private static void RegisterEnemy(ProcessDataPacket d, Character enemy)
        {
            if (enemy == null) return;

            ProcessControl.Register(enemy, d, out _);
            var list = GetOrCreateEnemyList(d);
            list.Add(enemy);
        }

        private static void ApplyRandomBuff(ProcessDataPacket d, Hero hero)
        {
            if (hero == null) return;
            var buffs = ResolveEffectList(d, Tags.EFFECTS);
            if (buffs.Count == 0) return;

            var buff = buffs[UnityEngine.Random.Range(0, buffs.Count)];
            hero.ApplyGameplayEffect(hero, buff);
        }

        /// <summary>
        /// Resolves a list of Characters from the data packet regardless of whether the
        /// list was added as individual entries (AddPayload(tag, IEnumerable)) or as a
        /// single whole-list object (AddPayload(tag, List)) — overload resolution
        /// prefers the single-value generic when callers pass a List&lt;T&gt; directly.
        /// </summary>
        private static List<Character> ResolveCharacterList(ProcessDataPacket d, Tag key)
        {
            // Flattened storage (each prefab added as its own entry).
            var flat = d.GetAll<Character>(key).All;
            if (flat.Count > 0) return flat;

            // Single-entry storage (the whole List was added as one object).
            var asList = d.GetPrimary<List<Character>>(key);
            if (asList != null && asList.Count > 0) return asList;

            return new List<Character>();
        }

        private static List<GameplayEffect> ResolveEffectList(ProcessDataPacket d, Tag key)
        {
            var flat = d.GetAll<GameplayEffect>(key).All;
            if (flat.Count > 0) return flat;

            var asList = d.GetPrimary<List<GameplayEffect>>(key);
            if (asList != null && asList.Count > 0) return asList;

            return new List<GameplayEffect>();
        }

        private static void LevelUpExistingAbility(Hero hero, Ability ability)
        {
            hero.ModifyLevel(ability.GetAssetTag(), new IntValuePair(1, 0));

            Debug.Log($"{ability.GetName()} leveled up: {hero.GetLevel(ability.GetAssetTag()).CurrentValue.ToString()}");
            // Walk the ability cache to find the matching granted instance and bump its level.
            for (int i = 0; i < hero.AbilitySystem.AbilityCount; i++)
            {
                // NOTE: Exposing the granted Ability off the AbilitySystemComponent cache is
                // framework-level; if a helper isn't present, this bump falls through harmlessly.
                // If you add a GetAbilityAt(i) / SetAbilityLevel(i, lvl) pair, wire it here.
                // For now, we just increment the ability's configured starting level as a
                // best-effort until that accessor lands.
                // (Intentionally simple — replace with SetAbilityLevel(i, GetAbilityLevel(i) + 1)
                //  once that API is in place.)
            }
        }

        private static float ReadAttr(GameplayAbilitySystem owner, IAttribute attr, float fallback)
        {
            if (owner == null || attr == null) return fallback;
            return owner.TryGetAttributeValue(attr, out var value) ? value.CurrentValue : fallback;
        }

        private static void WriteAttrDelta(GameplayAbilitySystem owner, IAttribute attr, float delta)
        {
            if (owner == null || attr == null) return;
            var mod = SourcedModifiedAttributeValue.GenerateSimple(owner, attr, delta, 0f);
            owner.TryModifyAttribute(attr, mod);
        }

        private static Vector3 RandomOnRing(Vector3 center, float radius)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            return new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y,
                center.z + Mathf.Sin(angle) * radius);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Small extension — Vector2 → XZ plane
    // ═══════════════════════════════════════════════════════════════════════════
    internal static class SwarmVectorExtensions
    {
        public static Vector3 ToXZ(this Vector2 v) => new Vector3(v.x, 0f, v.y);
    }
}
