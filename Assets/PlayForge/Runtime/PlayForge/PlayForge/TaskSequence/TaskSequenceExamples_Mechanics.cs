using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;
using static FarEmerald.PlayForge.SequenceTaskLibrary;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Examples
{
    /// <summary>
    /// Additional examples demonstrating TaskSequence features via
    /// real gameplay mechanics drawn from MOBA and ARPG titles.
    /// </summary>
    public static partial class TaskSequenceExamples
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // ADDITIONAL TAGS
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static readonly Tag CHANNEL_TICKS = Tag.GenerateAsUnique("Sequence.ChannelTicks");
        private static readonly Tag CHANNEL_ACTIVE = Tag.GenerateAsUnique("Sequence.ChannelActive");
        private static readonly Tag CHARGES = Tag.GenerateAsUnique("Sequence.Charges");
        private static readonly Tag MAX_CHARGES = Tag.GenerateAsUnique("Sequence.MaxCharges");
        private static readonly Tag STACKS = Tag.GenerateAsUnique("Sequence.Stacks");
        private static readonly Tag COOLDOWN = Tag.GenerateAsUnique("Sequence.Cooldown");
        private static readonly Tag COOLDOWN_ACTIVE = Tag.GenerateAsUnique("Sequence.CooldownActive");
        private static readonly Tag SHIELD = Tag.GenerateAsUnique("Sequence.Shield");
        private static readonly Tag TARGET_HEALTH = Tag.GenerateAsUnique("Sequence.TargetHealth");
        private static readonly Tag GOLD = Tag.GenerateAsUnique("Sequence.Gold");
        private static readonly Tag RESPAWN_TIME = Tag.GenerateAsUnique("Sequence.RespawnTime");
        private static readonly Tag DEAD = Tag.GenerateAsUnique("Sequence.Dead");
        private static readonly Tag TEAM_DPS = Tag.GenerateAsUnique("Sequence.TeamDps");
        private static readonly Tag OBJECTIVE_HEALTH = Tag.GenerateAsUnique("Sequence.ObjectiveHealth");
        private static readonly Tag ABILITY_PHASE = Tag.GenerateAsUnique("Sequence.AbilityPhase");
        private static readonly Tag DAMAGE_TAKEN = Tag.GenerateAsUnique("Sequence.DamageTaken");
        private static readonly Tag ITERATIONS = Tag.GenerateAsUnique("Sequence.Iterations");

        public static TaskSequence KunkkaTorrentStorm(AnimationCurve c = null)
        {
            /*
             * Torrent storm around a certain point within a radius
             * N torrents over D duration
             * Each torrent starts its own torrent sequence separate from storm sequence
             */
            var D = Tag.GenerateAsUnique("Duration of Storm");
            var R = Tag.GenerateAsUnique("Storm Radius");
            
            var N = Tag.GenerateAsUnique("Number of Torrents");
            var tD = Tag.GenerateAsUnique("Duration of Torrent");
            var yD = Tag.GenerateAsUnique("Torrent Y Delta");
            
            var stormSequence = TaskSequenceBuilder.Create("Torrent Storm Sequence")
                .Task(d =>
                {
                    // Setup our parameters
                    d.SetPrimary(D, 10f);
                    d.SetPrimary(R, 7.5f);
                    d.SetPrimary(N, 15);
                    d.SetPrimary(tD, 2f);
                    d.SetPrimary(yD, 15f);
                    
                    d.SetPrimary(ITERATIONS, d.GetPrimary<int>(N));
                })
                .Task(d =>
                {
                    // Setup camera in good spot
                    /*var cPos = new Vector3(0, 30, 0);
                    Camera.main.transform.position = cPos;
                    Camera.main.transform.rotation = Quaternion.Euler(new Vector3(90, 0, 0));*/
                    Debug.Log($"Storm sequence");
                })
                .Stage(s => s
                    .WithName("Torrent Storm")
                    .WithDescription("Create a storm of torrents around (0, 0, 0)")
                    .WithRepeat(true)
                    //.StopRepeatWhen(d => d.GetPrimary(ITERATIONS, 0) <= 0)
                    .Task(d =>
                    {
                        if (d.GetPrimary<int>(ITERATIONS) <= 0)
                        {
                            d.Inject(BreakStageRepeatInjection.Instance, true);
                            return;
                        }

                        Debug.Log($"Create storm ({d.GetPrimary<int>(ITERATIONS)})");
                        
                        // Create the torrent
                        var torrent = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        var rPos = Random.insideUnitCircle * d.GetPrimary<float>(R);
                        var pos = new Vector3(rPos.x, 1f, rPos.y);
                        torrent.transform.position = pos;
                        
                        var torrentData = new SequenceDataPacket(d);
                        torrentData.SetPrimary(Tags.DATA, torrent);

                        ProcessControl.Register(TorrentSequence2(), torrentData, out _);

                        d.Decrement(ITERATIONS);
                    })
                    .Task(async (d, t) =>
                    {
                        var waitDuration = d.GetPrimary<float>(tD) / d.GetPrimary<int>(N);
                        await UniTask.Delay(TimeSpan.FromSeconds(waitDuration), cancellationToken: t);
                    }))
                .OnTerminate((ctx, success) =>
                {
                    Debug.Log($"Finished torrent storm! ({success})");
                })
                .BuildSequence();

            return stormSequence;

            TaskSequence TorrentSequence()
            {
                return TaskSequenceBuilder.Create("Torrent")
                    .Task(async (d, t) =>
                    {
                        var torrent = d.GetPrimary<GameObject>(Tags.DATA);
                        var delta = new Vector3(0f, d.GetPrimary<float>(yD), 0f);
                        var duration = d.GetPrimary<float>(tD) * .5f;

                        var moveUp = MoveBy(torrent.transform, delta, duration, t, c);
                        var rotateUp = RotateBy(torrent.transform, delta * 40f, duration, t);
                        var upTasks = new[] { moveUp, rotateUp };
                        await UniTask.WhenAll(upTasks);
                    
                        var moveDown = MoveBy(torrent.transform, -delta, duration, t, c);
                        var rotateDown = RotateBy(torrent.transform, delta * 40f, duration, t);
                        var downTasks = new[] { moveDown, rotateDown };
                        await UniTask.WhenAll(downTasks);
                    })
                    .OnTerminate((ctx, success) =>
                    {
                        var torrent = ctx.Data.GetPrimary<GameObject>(Tags.DATA);
                        Object.Destroy(torrent);
                    })
                    .BuildSequence();
            }
            
            TaskSequence TorrentSequence2()
            {
                return TaskSequenceBuilder.Create("Individual Torrent")
                    .Task(async (d, t) =>
                    {
                        var torrent = d.GetPrimary<GameObject>(Tags.DATA);
                        var delta = d.GetPrimary<float>(yD);
                        var duration = d.GetPrimary<float>(tD);

                        var x = delta * Mathf.Cos(Random.Range(0, 360)) - delta * Mathf.Sin(Random.Range(0, 360));
                        var z = delta * Mathf.Sin(Random.Range(0, 360)) + delta * Mathf.Cos(Random.Range(0, 360));
                        var destination = new Vector3(x, torrent.transform.position.y, z);
                        
                        var arcTo = ArcTo(torrent.transform, destination, duration, delta, t); 
                        var rotate = RotateBy(torrent.transform, new Vector3(0f, delta * 50f, 0f), duration, t);
                        
                        var torrentTask = new[] { arcTo, rotate };
                        await UniTask.WhenAll(torrentTask);
                    })
                    .OnTerminate((ctx, success) =>
                    {
                        var torrent = ctx.Data.GetPrimary<GameObject>(Tags.DATA);
                        Object.Destroy(torrent);
                    })
                    .BuildSequence();
            }
        }
        
        public static TaskSequence CharacterChangeCameraEffect()
        {
            return TaskSequenceBuilder.Create("Character Change Camera Effect")
                .Task(d =>
                {
                    var obj = d.GetPrimary<GameObject>(Tags.DATA);
                    Camera.main.transform.position = obj.transform.position + new Vector3(0, 10, 0);
                    Camera.main.transform.rotation = Quaternion.Euler(new Vector3(90, 0, 0));    
                    
                    d.SetPrimary(ITERATIONS, 3);
                    d.AddPayload(Tags.POSITION, obj.transform.position);
                    d.AddPayload(Tags.TARGET_POS, new Vector3(0, 25, 0));
                    d.AddPayload(Tags.DEBUG,
                        (d.GetPrimary<Vector3>(Tags.TARGET_POS).y - d.GetPrimary<Vector3>(Tags.POSITION).y) / d.GetPrimary<int>(ITERATIONS)
                    );
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ITERATIONS) <= 0, true)
                    .Task(async (d, t) =>
                    {
                        var delta = d.GetPrimary<float>(Tags.DEBUG);
                        Camera.main.transform.position += new Vector3(0f, delta, 0f);
                        
                        await UniTask.Delay(800, cancellationToken: t);

                        Debug.Log($"{d.GetPrimary<int>(ITERATIONS)}: Moved camera out!");
                        d.Decrement(ITERATIONS);
                    }))
                .Task(async (d, t) =>
                {
                    Debug.Log($"Pausing between movement");
                    await UniTask.Delay(2200, cancellationToken: t);
                    d.SetPrimary(ITERATIONS, 3);
                    Debug.Log($"Pause Complete");
                })
                .Stage(s => s
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ITERATIONS) <= 0, true)
                    .Task(async (d, t) =>
                    {
                        var delta = -d.GetPrimary<float>(Tags.DEBUG);
                        Camera.main.transform.position += new Vector3(0f, delta, 0f);
                        
                        await UniTask.Delay(1500, cancellationToken: t);
                        
                        Debug.Log($"{d.GetPrimary<int>(ITERATIONS)}: Moved camera in!");
                        d.Decrement(ITERATIONS);
                    }))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CHANNELED ABILITY (Witch Doctor's Death Ward / Katarina's Death Lotus)
        //
        // Cast begins a channel that ticks damage every interval.
        // Channel is interruptible by external damage (simulated via key press).
        // Uses: WithCriticalFlag(false) so it CAN be interrupted,
        //       WithRepeat for tick loop, WithTimeout for max channel duration,
        //       StopRepeatWhen for early cancellation.
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("MOBA: Channeled Ability")]
        public static TaskSequence ChanneledAbility()
        {
            return TaskSequenceBuilder.Create("Death Ward")
                .Task(d =>
                {
                    d.SetPrimary(CHANNEL_TICKS, 0);
                    d.SetPrimary(CHANNEL_ACTIVE, true);
                    d.SetPrimary(TARGET_HEALTH, 1500);
                    d.SetPrimary(DAMAGE_TAKEN, false);
                    
                    Debug.Log("=== CHANNELED ABILITY: DEATH WARD ===");
                    Debug.Log("Press [Space] to simulate getting stunned (interrupts channel)");
                })
                
                // Cast animation (non-interruptible via critical)
                .Stage(s => s
                    .WithName("Cast Point")
                    .WithCriticalFlag(true)
                    .Task(async (d, t) =>
                    {
                        Debug.Log("[Cast] Raising the ward... (0.3s cast point)");
                        await UniTask.Delay(300, cancellationToken: t);
                        Debug.Log("[Cast] Ward placed!");
                    }))
                
                // Channel phase - ticks damage, interruptible
                .Stage(s => s
                    .WithName("Channel")
                    .WithRepeat(true)
                    .WithTimeout(8f, SkipStageInjection.Instance)
                    .OnTimeout(ctx => Debug.Log("[Channel] Max duration reached."))
                    .InjectWhen(d => Input.GetKeyDown(KeyCode.Space), new DelegateSequenceInjection(runtime =>
                    {
                        runtime.Data.SetPrimary(CHANNEL_ACTIVE, false);
                        Debug.Log("  [!] STUNNED - Channel interrupted!");
                        return true;
                    }))
                    .StopRepeatWhen(d => !d.GetPrimary<bool>(CHANNEL_ACTIVE))
                    .StopRepeatWhen(d => d.GetPrimary<int>(TARGET_HEALTH) <= 0)
                    .OnRepeat(ctx =>
                    {
                        int ticks = ctx.Data.GetPrimary<int>(CHANNEL_TICKS);
                        int hp = ctx.Data.GetPrimary<int>(TARGET_HEALTH);
                        Debug.Log($"  -- tick {ticks} | target HP: {hp} --");
                    })
                    .Task(d =>
                    {
                        // Tick damage
                        int dmg = Random.Range(60, 90);
                        int hp = d.Decrement(TARGET_HEALTH, dmg);
                        d.Increment(CHANNEL_TICKS);
                        Debug.Log($"  [Ward] Bolt! -{dmg} | Target HP: {Mathf.Max(0, hp)}");
                    })
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(400, cancellationToken: t);
                    }))
                    /*.Task(async (d, t) =>
                    {
                        // Check for interrupt (stun)
                        if (Input.GetKeyDown(KeyCode.Space))
                        {
                            d.SetPrimary(CHANNEL_ACTIVE, false);
                            Debug.Log("  [!] STUNNED - Channel interrupted!");
                            return;
                        }
                        
                        await UniTask.Delay(400, cancellationToken: t);
                        
                        // Tick damage
                        int dmg = Random.Range(60, 90);
                        int hp = d.Decrement(TARGET_HEALTH, dmg);
                        d.Increment(CHANNEL_TICKS);
                        Debug.Log($"  [Ward] Bolt! -{dmg} | Target HP: {Mathf.Max(0, hp)}");
                    })
                    // Parallel interrupt listener
                    .Task(async (d, t) =>
                    {
                        while (!t.IsCancellationRequested)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, t);
                            if (!Input.GetKeyDown(KeyCode.Space)) continue;
                            
                            d.SetPrimary(CHANNEL_ACTIVE, false);
                            Debug.Log("  [!] STUNNED - Channel interrupted!");
                            return;
                        }
                    })*/
                    //.WhenAll())
                
                // Result
                .Task(d =>
                {
                    int ticks = d.GetPrimary<int>(CHANNEL_TICKS);
                    int hp = d.GetPrimary<int>(TARGET_HEALTH);
                    bool killed = hp <= 0;
                    
                    Debug.Log($"[Ward expires] {ticks} bolts fired.");
                    Debug.Log(killed
                        ? "[KILL] Target eliminated!"
                        : $"[End] Target survived with {hp} HP.");
                })
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // CHARGE-BASED ABILITY (Riven's Broken Wings / Fire Spirit's Remnants)
        //
        // Ability has N charges. Each use consumes a charge and must be used
        // within a window or remaining charges expire. Cooldown starts after
        // all charges consumed or expired.
        // Uses: WithRepeat for charge usage loop, WithTimeout for charge expiry,
        //       DelayUntil for input gating, OnRepeat for charge tracking.
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("MOBA: Charge-Based Ability")]
        public static TaskSequence ChargeBasedAbility()
        {
            return TaskSequenceBuilder.Create("Broken Wings")
                .Task(d =>
                {
                    d.SetPrimary(CHARGES, 3);
                    d.SetPrimary(MAX_CHARGES, 3);
                    d.SetPrimary(ABILITY_PHASE, 0);
                    d.SetPrimary(TARGET_HEALTH, 600);
                    
                    Debug.Log("=== CHARGE-BASED ABILITY: BROKEN WINGS ===");
                    Debug.Log("3 charges. Press [Q] to use each charge.");
                    Debug.Log("2s window between charges or they expire.");
                })
                
                // Charge usage loop
                .Stage(s => s
                    .WithName("Charge Loop")
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(CHARGES) <= 0)
                    .StopRepeatWhen(d => d.GetPrimary<int>(TARGET_HEALTH) <= 0)
                    .OnRepeat(ctx =>
                    {
                        int charges = ctx.Data.GetPrimary<int>(CHARGES);
                        if (charges > 0)
                            Debug.Log($"  [{charges} charges left] Press [Q] within 2s...");
                    })
                    
                    // Wait for Q within timeout window
                    .SubStage(sub => sub
                        .WithName("Await Input")
                        .WithTimeout(2f, BreakStageRepeatInjection.Instance)
                        .OnTimeout(ctx =>
                        {
                            ctx.Data.SetPrimary(CHARGES, 0);
                            Debug.Log("  [Expired] Took too long between casts!");
                        })
                        .Task(d =>
                        {
                            int phase = d.GetPrimary<int>(ABILITY_PHASE) + 1;
                            Debug.Log($"  [Ready] Q{phase} - Press [Q]...");
                        })
                        .DelayUntil(() => Input.GetKeyDown(KeyCode.Q)))
                    
                    // Execute the charge
                    .SubStage(sub => sub
                        .WithName("Execute Charge")
                        .Task(async (d, t) =>
                        {
                            int phase = d.Increment(ABILITY_PHASE);
                            int charges = d.Decrement(CHARGES);
                            
                            // Each phase does escalating damage
                            int baseDmg = phase switch
                            {
                                1 => Random.Range(30, 50),
                                2 => Random.Range(40, 60),
                                3 => Random.Range(80, 120), // Third hit is a knockup
                                _ => 30
                            };
                            
                            int hp = d.Decrement(TARGET_HEALTH, baseDmg);
                            
                            string extra = phase == 3 ? " + KNOCKUP!" : "";
                            Debug.Log($"  [Q{phase}] Slash! -{baseDmg} dmg{extra} | Target: {Mathf.Max(0, hp)} HP");
                            
                            await UniTask.Delay(200, cancellationToken: t); // Animation
                        })))
                
                // Cooldown
                .Stage(s => s
                    .WithName("Cooldown")
                    .Task(async (d, t) =>
                    {
                        float cd = 12f;
                        Debug.Log($"[Cooldown] {cd}s...");
                        // In a real game you'd await the full CD; here we abbreviate
                        await UniTask.Delay(1000, cancellationToken: t);
                        Debug.Log("[Ready] Ability available again.");
                    }))
                
                .Task(d =>
                {
                    int hp = d.GetPrimary<int>(TARGET_HEALTH);
                    Debug.Log(hp <= 0
                        ? "[KILL] Target eliminated!"
                        : $"[End] Target HP: {hp}");
                })
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ON-HIT STACKING PROC (Vayne's Silver Bolts / Ursa's Fury Swipes)
        //
        // Each hit adds a stack on the target. At N stacks, a bonus proc fires
        // dealing %HP damage, then stacks reset. Stacks expire after a timeout.
        // Uses: WithRepeat for attack loop, GetOrInit for stack tracking,
        //       parallel timeout watcher to expire stacks.
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("MOBA: On-Hit Stack Proc")]
        public static TaskSequence OnHitStackProc()
        {
            return TaskSequenceBuilder.Create("Silver Bolts")
                .Task(d =>
                {
                    d.SetPrimary(STACKS, 0);
                    d.SetPrimary(TARGET_HEALTH, 2000);
                    d.SetPrimary(PLAYER_ALIVE, true);
                    d.SetPrimary(SCORE, 0); // Total bonus damage dealt
                    
                    Debug.Log("=== ON-HIT STACK PROC: SILVER BOLTS ===");
                    Debug.Log("Every 3rd hit deals 8% max HP as bonus true damage.");
                    Debug.Log("Stacks expire after 3s without attacking.");
                })
                
                // Attack loop
                .Stage(s => s
                    .WithName("Combat")
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(TARGET_HEALTH) <= 0)
                    .StopRepeatWhen(d => !d.GetPrimary<bool>(PLAYER_ALIVE))
                    .Task(async (d, t) =>
                    {
                        // Attack speed delay
                        await UniTask.Delay(600, cancellationToken: t);
                        
                        // Base attack damage
                        int baseDmg = Random.Range(55, 75);
                        int stacks = d.Increment(STACKS);
                        
                        int targetMaxHp = 2000;
                        int hp = d.Decrement(TARGET_HEALTH, baseDmg);
                        
                        Debug.Log($"  [AA] Hit! -{baseDmg} ({stacks}/3 stacks) | Target: {Mathf.Max(0, hp)} HP");
                        
                        // Proc on 3rd stack
                        if (stacks >= 3)
                        {
                            int bonusDmg = Mathf.RoundToInt(targetMaxHp * 0.08f);
                            hp = d.Decrement(TARGET_HEALTH, bonusDmg);
                            d.Increment(SCORE, bonusDmg);
                            d.SetPrimary(STACKS, 0);
                            Debug.Log($"  [PROC!] Silver Bolts! -{bonusDmg} true dmg | Target: {Mathf.Max(0, hp)} HP");
                        }
                        
                        // Simulate enemy fighting back
                        if (Random.value > 0.6f)
                        {
                            int eDmg = Random.Range(40, 70);
                            int pHp = d.Decrement(HEALTH, eDmg);
                            if (pHp <= 0) d.SetPrimary(PLAYER_ALIVE, false);
                        }
                    }))
                
                .Task(d =>
                {
                    int targetHp = d.GetPrimary<int>(TARGET_HEALTH);
                    int bonusTotal = d.GetPrimary<int>(SCORE);
                    Debug.Log(targetHp <= 0
                        ? $"[KILL] Total Silver Bolts damage: {bonusTotal}"
                        : $"[Died] Target survived with {targetHp} HP");
                })
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TP SCROLL / RECALL (Dota TP / LoL Recall)
        //
        // Channeled cast to teleport. Any damage taken during channel interrupts it.
        // Uses: WithCriticalFlag(false) so damage CAN interrupt, parallel listener
        //       for incoming damage, DelayUntil for cast completion, stage timeout
        //       as channel duration.
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("MOBA: TP Scroll")]
        public static TaskSequence TpScroll()
        {
            return TaskSequenceBuilder.Create("TP Scroll")
                .Task(d =>
                {
                    d.SetPrimary(CHANNEL_ACTIVE, true);
                    d.SetPrimary(DAMAGE_TAKEN, false);
                    d.SetPrimary(READY, false);
                    
                    Debug.Log("=== TP SCROLL ===");
                    Debug.Log("Channeling for 3 seconds...");
                    Debug.Log("Press [Space] to simulate taking damage (cancels TP)");
                })
                
                // Channel
                .Stage(s => s
                    .WithName("Channel")
                    .Task(async (d, t) =>
                    {
                        float channelTime = 3f;
                        float elapsed = 0f;
                        
                        while (elapsed < channelTime && !t.IsCancellationRequested)
                        {
                            await UniTask.Delay(500, cancellationToken: t);
                            elapsed += 0.5f;
                            
                            int pct = Mathf.RoundToInt((elapsed / channelTime) * 100f);
                            Debug.Log($"  [Channeling] {pct}%...");
                        }
                        
                        if (!t.IsCancellationRequested)
                        {
                            d.SetPrimary(READY, true);
                        }
                    })
                    // Damage interrupt listener
                    .Task(async (d, t) =>
                    {
                        while (!t.IsCancellationRequested)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, t);
                            
                            if (!Input.GetKeyDown(KeyCode.Space)) continue;
                            
                            d.SetPrimary(DAMAGE_TAKEN, true);
                            d.SetPrimary(CHANNEL_ACTIVE, false);
                            Debug.Log("  [!] DAMAGE TAKEN - TP cancelled!");
                            d.SkipStage();
                            return;
                        }
                    })
                    .WhenAny())
                
                // Result
                .Branch(b => b
                    .If(d => d.GetPrimary<bool>(READY), s => s
                        .Task(d =>
                        {
                            Debug.Log("[SUCCESS] Teleported to base!");
                            Debug.Log("[Healed] HP and Mana restored.");
                        }))
                    .Default(s => s
                        .Task(d =>
                        {
                            Debug.Log("[FAILED] TP was interrupted.");
                            Debug.Log("[On Cooldown] 70s cooldown starts.");
                        })))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ABILITY COMBO CHAIN (Lee Sin Q-Q / Invoker QWE combinations)
        //
        // First cast marks the target. Second cast (within window) dashes to them
        // for bonus damage. If window expires, no follow-up.
        // Uses: DelayUntil for second cast, WithTimeout for follow-up window,
        //       Branch for hit/miss, critical flag on the dash.
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("MOBA: Two-Part Ability")]
        public static TaskSequence TwoPartAbility()
        {
            return TaskSequenceBuilder.Create("Resonating Strike")
                .Task(d =>
                {
                    d.SetPrimary(TARGET_HEALTH, 500);
                    d.SetPrimary(ABILITY_PHASE, 0);
                    d.SetPrimary(READY, false);
                    
                    Debug.Log("=== TWO-PART ABILITY: RESONATING STRIKE ===");
                    Debug.Log("[Q1] fires a skillshot. [Q2] dashes to the marked target.");
                    Debug.Log("Press [Q] for each phase.");
                })
                
                // Phase 1: Skillshot
                .Task(d => Debug.Log("[Ready] Press [Q] to fire skillshot..."))
                .DelayUntil(() => Input.GetKeyDown(KeyCode.Q))
                .Stage(s => s
                    .WithName("Skillshot")
                    .Task(async (d, t) =>
                    {
                        d.SetPrimary(ABILITY_PHASE, 1);
                        Debug.Log("  [Q1] Sonic Wave fired!");
                        await UniTask.Delay(300, cancellationToken: t);
                        
                        // Simulate hit/miss
                        bool hit = Random.value > 0.3f;
                        d.SetPrimary(READY, hit);
                        
                        if (hit)
                        {
                            int dmg = Random.Range(60, 90);
                            int hp = d.Decrement(TARGET_HEALTH, dmg);
                            Debug.Log($"  [HIT] Sonic Wave connects! -{dmg} | Target: {hp} HP");
                            Debug.Log("  Press [Q] within 3s to dash...");
                        }
                        else
                        {
                            Debug.Log("  [MISS] Sonic Wave missed!");
                        }
                    }))
                
                // Phase 2: Follow-up dash (only if hit)
                .Branch(b => b
                    .If(d => d.GetPrimary<bool>(READY), s => s
                        // Wait for second Q press within window
                        .SubStage(sub => sub
                            .WithName("Await Recast")
                            .WithTimeout(3f, SkipStageInjection.Instance)
                            .OnTimeout(ctx =>
                            {
                                ctx.Data.SetPrimary(ABILITY_PHASE, 0);
                                Debug.Log("  [Expired] Recast window closed.");
                            })
                            .DelayUntil(() => Input.GetKeyDown(KeyCode.Q))
                            .Task(d => d.SetPrimary(ABILITY_PHASE, 2)))
                        
                        // Execute dash if recast happened
                        .SubStage(sub => sub
                            .WithName("Dash")
                            .WithCriticalFlag(true) // Dash is non-interruptible
                            .Task(async (d, t) =>
                            {
                                if (d.GetPrimary<int>(ABILITY_PHASE) != 2) return;
                                
                                Debug.Log("  [Q2] Resonating Strike! Dashing...");
                                await UniTask.Delay(200, cancellationToken: t);
                                
                                // Execute damage scales with target missing HP
                                int targetHp = d.GetPrimary<int>(TARGET_HEALTH);
                                float missingHpPct = 1f - (targetHp / 500f);
                                int baseDmg = Random.Range(60, 90);
                                int bonusDmg = Mathf.RoundToInt(baseDmg * missingHpPct);
                                int totalDmg = baseDmg + bonusDmg;
                                
                                int hp = d.Decrement(TARGET_HEALTH, totalDmg);
                                Debug.Log($"  [Q2] Impact! -{totalDmg} ({baseDmg} base + {bonusDmg} execute) | Target: {Mathf.Max(0, hp)} HP");
                            })))
                    .Default(s => s
                        .Task(d => Debug.Log("  [No follow-up available]"))))
                
                .Task(d =>
                {
                    int hp = d.GetPrimary<int>(TARGET_HEALTH);
                    Debug.Log(hp <= 0 ? "[KILL] Target eliminated!" : $"[End] Target: {hp} HP remaining.");
                })
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // RESPAWN TIMER + BUYBACK (Dota 2 death & buyback mechanic)
        //
        // On death, a respawn timer counts down. Player can press [B] to buyback
        // (costs gold, longer CD next time). Timer scales with game time/level.
        // Uses: DelayUntil for buyback input, parallel timer + input listener,
        //       WhenAny to race timer vs buyback, branch for outcome.
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("MOBA: Death & Respawn")]
        public static TaskSequence DeathAndRespawn()
        {
            return TaskSequenceBuilder.Create("Death Timer")
                .Task(d =>
                {
                    int level = 15;
                    float respawnTime = 5f + level * 2f; // 35s at level 15
                    
                    d.SetPrimary(HEALTH, 0);
                    d.SetPrimary(DEAD, true);
                    d.SetPrimary(GOLD, 3000);
                    d.SetPrimary(RESPAWN_TIME, respawnTime);
                    d.SetPrimary(READY, false);
                    
                    Debug.Log("=== DEATH & RESPAWN ===");
                    Debug.Log($"[SLAIN] You have been killed!");
                    Debug.Log($"Respawn in {respawnTime}s. Press [B] to buyback (cost: 1500g).");
                })
                
                // Respawn phase: race between timer expiring and buyback
                .Stage(s => s
                    .WithName("Death Timer")
                    // Timer countdown
                    .Task(async (d, t) =>
                    {
                        float total = d.GetPrimary<float>(RESPAWN_TIME);
                        float remaining = total;
                        
                        while (remaining > 0 && !t.IsCancellationRequested)
                        {
                            await UniTask.Delay(1000, cancellationToken: t);
                            remaining -= 1f;
                            
                            if (remaining > 0 && ((int)remaining % 5 == 0 || remaining <= 3))
                            {
                                Debug.Log($"  [Dead] Respawning in {remaining}s...");
                            }
                        }
                        
                        if (!t.IsCancellationRequested)
                        {
                            d.SetPrimary(DEAD, false);
                            Debug.Log("  [Timer] Respawn timer expired.");
                        }
                    })
                    // Buyback listener
                    .Task(async (d, t) =>
                    {
                        while (!t.IsCancellationRequested)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, t);
                            if (!Input.GetKeyDown(KeyCode.B)) continue;
                            
                            int gold = d.GetPrimary<int>(GOLD);
                            if (gold >= 1500)
                            {
                                d.Decrement(GOLD, 1500);
                                d.SetPrimary(DEAD, false);
                                d.SetPrimary(READY, true); // Flag that buyback was used
                                Debug.Log("  [BUYBACK] Spent 1500g! Respawning immediately!");
                                return;
                            }
                            Debug.Log($"  [Buyback] Not enough gold! Need 1500, have {gold}.");
                        }
                    })
                    .WhenAny())
                
                // Respawn
                .Task(async (d, t) =>
                {
                    Debug.Log("[ALIVE] You have respawned!");
                    d.SetPrimary(HEALTH, 1800);
                    d.SetPrimary(PLAYER_MANA, 1000);
                    Debug.Log($"  HP: {d.GetPrimary<int>(HEALTH)} | MP: {d.GetPrimary<int>(PLAYER_MANA)} | Gold: {d.GetPrimary<int>(GOLD)}");
                    
                    if (d.GetPrimary<bool>(READY))
                    {
                        Debug.Log("  [Warning] Buyback on cooldown for 480s.");
                    }
                    
                    await UniTask.Delay(500, cancellationToken: t);
                })
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // OBJECTIVE FIGHT (Roshan / Baron Nashor)
        //
        // Team DPS race against a powerful neutral objective with phase transitions.
        // Objective has enrage timer, AoE slams, and drops reward on death.
        // Uses: WithRepeat for DPS loop, StopRepeatWhen for kill/wipe,
        //       WithMaxDuration as enrage timer, parallel AoE + DPS tasks,
        //       Branch for loot phase.
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("MOBA: Objective Fight")]
        public static TaskSequence ObjectiveFight()
        {
            return TaskSequenceBuilder.Create("Roshan Fight")
                .Task(d =>
                {
                    d.SetPrimary(OBJECTIVE_HEALTH, 7500);
                    d.SetPrimary(HEALTH, 2800);    // Team total HP
                    d.SetPrimary(PLAYER_ALIVE, true);
                    d.SetPrimary(SCORE, 0);         // Damage dealt
                    d.SetPrimary(ROUND, 0);         // Slam count
                    
                    Debug.Log("=== OBJECTIVE FIGHT: ROSHAN ===");
                    Debug.Log("HP: 7500 | Enrage: 20s | Slams every ~3s");
                })
                
                // Fight
                .Stage(s => s
                    .WithName("Fight")
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(OBJECTIVE_HEALTH) <= 0)
                    .StopRepeatWhen(d => !d.GetPrimary<bool>(PLAYER_ALIVE))
                    .WithMaxDuration(20f, BreakStageRepeatInjection.Instance)
                    .OnMaxDuration(ctx =>
                    {
                        Debug.Log("  [ENRAGE] Roshan is enraged! Wiping party...");
                        ctx.Data.SetPrimary(PLAYER_ALIVE, false);
                    })
                    
                    // Team DPS
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(500, cancellationToken: t);
                        
                        // 5 heroes hitting
                        int totalDmg = 0;
                        for (int i = 0; i < 5; i++)
                        {
                            totalDmg += Random.Range(60, 120);
                        }
                        
                        int hp = d.Decrement(OBJECTIVE_HEALTH, totalDmg);
                        d.Increment(SCORE, totalDmg);
                        Debug.Log($"  [Team] -{totalDmg} dmg | Roshan: {Mathf.Max(0, hp)} HP");
                    })
                    
                    // Roshan attacks back
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(500, cancellationToken: t);
                        
                        int roshanDmg = Random.Range(100, 180);
                        int teamHp = d.Decrement(HEALTH, roshanDmg);
                        
                        // Periodic slam
                        int slamCount = d.Increment(ROUND);
                        if (slamCount % 6 == 0) // Every ~3s
                        {
                            int slamDmg = Random.Range(200, 350);
                            teamHp = d.Decrement(HEALTH, slamDmg);
                            Debug.Log($"  [SLAM!] Roshan slams! -{slamDmg} AoE | Team HP: {Mathf.Max(0, teamHp)}");
                        }
                        
                        if (teamHp <= 0)
                        {
                            d.SetPrimary(PLAYER_ALIVE, false);
                            Debug.Log("  [WIPE] Team has been killed!");
                        }
                    })
                    .WhenAll())
                
                // Result
                .Branch(b => b
                    .If(d => d.GetPrimary<int>(OBJECTIVE_HEALTH) <= 0, s => s
                        .Task(d =>
                        {
                            Debug.Log("=== ROSHAN HAS FALLEN! ===");
                            Debug.Log($"  Total damage dealt: {d.GetPrimary<int>(SCORE)}");
                            Debug.Log("  [Loot] Aegis of the Immortal dropped!");
                            Debug.Log("  [Loot] +300 gold per hero.");
                        }))
                    .Default(s => s
                        .Task(d =>
                        {
                            Debug.Log("=== ROSHAN FIGHT FAILED ===");
                            Debug.Log(d.GetPrimary<bool>(PLAYER_ALIVE)
                                ? "  [Retreated] Enrage timer forced retreat."
                                : "  [Wiped] Team was killed.");
                        })))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SHIELD + RETALIATE (Sivir Spell Shield / Counterspell / Linken's Sphere)
        //
        // Activates a spell shield for a short window. If an ability hits during
        // the window, it is blocked and the user gains a buff. If nothing hits,
        // the shield just expires.
        // Uses: DelayUntil + WhenAny to race shield duration vs incoming spell,
        //       Branch for block/expire, critical flag on the block reaction.
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("MOBA: Spell Shield")]
        public static TaskSequence SpellShield()
        {
            return TaskSequenceBuilder.Create("Spell Shield")
                .Task(d =>
                {
                    d.SetPrimary(SHIELD, true);
                    d.SetPrimary(DAMAGE_TAKEN, false);
                    d.SetPrimary(PLAYER_MANA, 200);
                    
                    Debug.Log("=== SPELL SHIELD ===");
                    Debug.Log("[E] Spell Shield activated! (1.5s duration)");
                    Debug.Log("Press [Space] to simulate enemy spell hitting you.");
                })
                
                // Shield window
                .Stage(s => s
                    .WithName("Shield Active")
                    // Duration timer
                    .Task(async (d, t) =>
                    {
                        Debug.Log("  [Shield] Active...");
                        await UniTask.Delay(500, cancellationToken: t);
                        Debug.Log("  [Shield] Active... (1.0s left)");
                        await UniTask.Delay(500, cancellationToken: t);
                        Debug.Log("  [Shield] Active... (0.5s left)");
                        await UniTask.Delay(500, cancellationToken: t);
                        
                        // Timer expired naturally
                        d.SetPrimary(SHIELD, false);
                    })
                    // Spell impact listener
                    .Task(async (d, t) =>
                    {
                        while (!t.IsCancellationRequested)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, t);
                            if (!Input.GetKeyDown(KeyCode.Space)) continue;
                            
                            d.SetPrimary(DAMAGE_TAKEN, true);
                            return;
                        }
                    })
                    .WhenAny())
                
                // Outcome
                .Branch(b => b
                    .If(d => d.GetPrimary<bool>(DAMAGE_TAKEN), s => s
                        .WithName("Blocked")
                        .WithCriticalFlag(true) // Block reaction is non-interruptible
                        .Task(async (d, t) =>
                        {
                            Debug.Log("  [BLOCKED!] Spell Shield absorbed the ability!");
                            int mana = d.Increment(PLAYER_MANA, 80);
                            Debug.Log($"  [Mana restored] +80 MP (total: {mana})");
                            Debug.Log("  [Buff] Attack speed increased for 3s!");
                            await UniTask.Delay(200, cancellationToken: t);
                        }))
                    .Default(s => s
                        .Task(d =>
                        {
                            Debug.Log("  [Expired] Shield wore off. No spell blocked.");
                        })))
                
                .Task(d =>
                {
                    d.SetPrimary(SHIELD, false);
                    Debug.Log("[Cooldown] Spell Shield on cooldown (22s).");
                })
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TOGGLE ABILITY WITH DRAIN (Medusa Mana Shield / Voodoo Restoration)
        //
        // Toggled ability that drains mana per second while active.
        // Provides a benefit while on, turns off when mana depleted or toggled off.
        // Uses: WithRepeat for drain loop, DelayUntil for toggle-off input,
        //       StopRepeatWhen for mana depletion, parallel drain + listener.
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("MOBA: Toggle Drain")]
        public static TaskSequence ToggleDrain()
        {
            return TaskSequenceBuilder.Create("Mana Shield")
                .Task(d =>
                {
                    d.SetPrimary(PLAYER_MANA, 400);
                    d.SetPrimary(HEALTH, 1200);
                    d.SetPrimary(CHANNEL_ACTIVE, true);
                    
                    Debug.Log("=== TOGGLE ABILITY: MANA SHIELD ===");
                    Debug.Log("[ON] Absorbs 60% of damage, converting it to mana cost.");
                    Debug.Log("Press [E] to toggle off. Drains 25 mana/s while active.");
                })
                
                // Shield active loop
                .Stage(s => s
                    .WithName("Shield Loop")
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(PLAYER_MANA) <= 0)
                    .StopRepeatWhen(d => !d.GetPrimary<bool>(CHANNEL_ACTIVE))
                    .OnRepeat(ctx =>
                    {
                        var d = ctx.Data;
                        Debug.Log($"  -- HP: {d.GetPrimary<int>(HEALTH)} | MP: {d.GetPrimary<int>(PLAYER_MANA)} --");
                    })
                    
                    // Drain + incoming damage simulation
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(1000, cancellationToken: t);
                        
                        // Passive mana drain
                        int mp = d.Decrement(PLAYER_MANA, 25);
                        
                        // Simulate incoming damage
                        if (Random.value > 0.4f)
                        {
                            int rawDmg = Random.Range(80, 150);
                            int absorbed = Mathf.RoundToInt(rawDmg * 0.6f);
                            int hpDmg = rawDmg - absorbed;
                            int manaCost = Mathf.RoundToInt(absorbed * 0.5f);
                            
                            d.Decrement(HEALTH, hpDmg);
                            mp = d.Decrement(PLAYER_MANA, manaCost);
                            
                            Debug.Log($"  [Hit] -{rawDmg} raw -> -{hpDmg} HP, -{manaCost} MP (shield absorbed {absorbed})");
                        }
                        
                        if (mp <= 0)
                        {
                            d.SetPrimary(PLAYER_MANA, 0);
                            d.SetPrimary(CHANNEL_ACTIVE, false);
                            Debug.Log("  [!] Mana depleted! Shield disabled.");
                        }
                    })
                    
                    // Toggle-off listener
                    .Task(async (d, t) =>
                    {
                        while (!t.IsCancellationRequested)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, t);
                            if (!Input.GetKeyDown(KeyCode.E)) continue;
                            
                            d.SetPrimary(CHANNEL_ACTIVE, false);
                            Debug.Log("  [Toggle] Mana Shield deactivated.");
                            return;
                        }
                    })
                    .WhenAll())
                
                .Task(d =>
                {
                    Debug.Log($"[OFF] Mana Shield disabled.");
                    Debug.Log($"  Final HP: {d.GetPrimary<int>(HEALTH)} | MP: {d.GetPrimary<int>(PLAYER_MANA)}");
                })
                .BuildSequence();
        }
    }
}
