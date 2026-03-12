using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FarEmerald.PlayForge.Examples
{
    /// <summary>
    /// Example sequences demonstrating TaskSequence features.
    /// Mark methods with [TaskSequenceMethod] to appear in dropdown.
    /// All examples use SequenceDataPacket for direct sequence/stage control.
    /// </summary>
    public static class TaskSequenceExamples
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // COMMON TAGS FOR EXAMPLES
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static readonly Tag ROUND = Tag.Generate("Sequence.Round");
        private static readonly Tag SCORE = Tag.Generate("Sequence.Score");
        private static readonly Tag HEALTH = Tag.Generate("Sequence.Health");
        private static readonly Tag MAX_HEALTH = Tag.Generate("Sequence.MaxHealth");
        private static readonly Tag ENEMIES_ALIVE = Tag.Generate("Sequence.EnemiesAlive");
        private static readonly Tag ENEMIES_TO_SPAWN = Tag.Generate("Sequence.EnemiesToSpawn");
        private static readonly Tag PLAYER_ALIVE = Tag.Generate("Sequence.PlayerAlive");
        private static readonly Tag POWER_UP = Tag.Generate("Sequence.PowerUp");
        private static readonly Tag DIFFICULTY = Tag.Generate("Sequence.Difficulty");
        private static readonly Tag BOSS_ACTIVE = Tag.Generate("Sequence.BossActive");
        private static readonly Tag COUNTDOWN = Tag.Generate("Sequence.Countdown");
        private static readonly Tag BOSS_HEALTH = Tag.Generate("Sequence.BossHealth");
        private static readonly Tag BOSS_PHASE = Tag.Generate("Sequence.BossPhase");
        private static readonly Tag WAVE_COUNT = Tag.Generate("Sequence.WaveCount");
        private static readonly Tag COMBO_COUNT = Tag.Generate("Sequence.ComboCount");
        private static readonly Tag COMBO_ACTIVE = Tag.Generate("Sequence.ComboActive");
        private static readonly Tag TURN = Tag.Generate("Sequence.Turn");
        private static readonly Tag ENEMY_HEALTH = Tag.Generate("Sequence.EnemyHealth");
        private static readonly Tag PLAYER_MANA = Tag.Generate("Sequence.PlayerMana");
        private static readonly Tag PLAYER_BLOCK = Tag.Generate("Sequence.PlayerBlock");
        private static readonly Tag LAST_INPUT = Tag.Generate("Sequence.LastInput");
        private static readonly Tag READY = Tag.Generate("Sequence.Ready");
        private static readonly Tag DIALOGUE_CHOICE = Tag.Generate("Sequence.DialogueChoice");
        private static readonly Tag GAME_STATE = Tag.Generate("Sequence.GameState");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // BASIC EXAMPLES
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("Simple Sequential")]
        public static TaskSequence SimpleSequential()
        {
            return TaskSequenceBuilder.Create("Simple Sequential")
                .Task(async (data, token) =>
                {
                    Debug.Log("Step 1");
                    await UniTask.Delay(500, cancellationToken: token);
                })
                .Task(async (data, token) =>
                {
                    Debug.Log("Step 2");
                    await UniTask.Delay(500, cancellationToken: token);
                })
                .Task(data => Debug.Log("Step 3"))
                .BuildSequence();
        }
        
        [TaskSequenceMethod("Parallel WhenAll")]
        public static TaskSequence ParallelTasks()
        {
            return TaskSequenceBuilder.Create("Parallel")
                .Stage(s => s
                    .Task(async (d, t) => { await UniTask.Delay(1000, cancellationToken: t); Debug.Log("A done"); })
                    .Task(async (d, t) => { await UniTask.Delay(800, cancellationToken: t); Debug.Log("B done"); })
                    .Task(async (d, t) => { await UniTask.Delay(1200, cancellationToken: t); Debug.Log("C done"); })
                    .WhenAll())
                .Task(d => Debug.Log("All done!"))
                .BuildSequence();
        }
        
        [TaskSequenceMethod("Race WhenAny")]
        public static TaskSequence RaceCondition()
        {
            return TaskSequenceBuilder.Create("Race")
                .Stage(s => s
                    .Task(async (d, t) => { await UniTask.Delay(2000, cancellationToken: t); d.SetPrimary(Tags.PRIMARY, "A"); })
                    .Task(async (d, t) => { await UniTask.Delay(1500, cancellationToken: t); d.SetPrimary(Tags.PRIMARY, "B"); })
                    .WhenAny())
                .Task(d => Debug.Log($"Winner: {d.GetPrimary<string>(Tags.PRIMARY)}"))
                .BuildSequence();
        }
        
        [TaskSequenceMethod("Repeating")]
        public static TaskSequence Repeating()
        {
            return TaskSequenceBuilder.Create("Repeating")
                .WithMaxDuration(15f)
                .OnMaxDuration(ctx =>
                {
                    Debug.Log($"[Repeating] Interrupted at sequence's max duration (~{ctx.Runtime}).");
                    ctx.Runtime.Inject(InterruptSequenceInjection.Instance, true);
                })
                .Task(async (d, t) =>
                {
                    await UniTask.Delay(250, cancellationToken: t); 
                    d.Increment(SCORE);
                    Debug.Log($"[Repeating] Esc to end... ({d.GetPrimary<int>(SCORE).ToString()})");
                })
                .InterruptWhen(d => Input.GetKeyDown(KeyCode.Escape))
                .WithRepeat(true)
                .WithConditionCheckTiming()
                .BuildSequence();
        }
        
        [TaskSequenceMethod("Basic Chain")]
        public static TaskSequenceChain BasicChain()
        {
            var a = TaskSequenceBuilder.Create("A").Task(d => Debug.Log("A")).Delay(0.5f).BuildSequence();
            var b = TaskSequenceBuilder.Create("B").Task(d => Debug.Log("B")).Delay(0.5f).BuildSequence();
            return a.Then(b);
        }
        
        [TaskSequenceMethod("Basic Chain (Inline)")]
        public static TaskSequenceChain BasicChainInline()
        {
            return TaskSequenceBuilder.Create("A")
                .Task(d => Debug.Log("A"))
                .Delay(0.5f)
                .BuildSequence()
                .Then(seq => seq
                    .Task(d => Debug.Log("B"))
                    .Delay(0.5f)
                    .BuildSequence());
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DATA PACKET CONTROL EXAMPLES
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("Data Packet Control")]
        public static TaskSequence DataPacketControl()
        {
            return TaskSequenceBuilder.Create("Data Packet Control")
                .Task(d =>
                {
                    d.SetPrimary(HEALTH, 100);
                    d.SetPrimary(SCORE, 0);
                    d.SetPrimary(ROUND, 1);
                    Debug.Log($"[Init] Health: {d.GetPrimary<int>(HEALTH)}, Score: {d.GetPrimary<int>(SCORE)}");
                })
                .Stage(s => s
                    .WithName("Game Loop")
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(500, cancellationToken: t);
                        int health = d.Decrement(HEALTH, 25);
                        Debug.Log($"[Damage] Health: {health}");
                        if (health < 30)
                        {
                            Debug.Log("[Warning] Health critical! Skipping to heal...");
                            d.SkipStage();
                        }
                    })
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(300, cancellationToken: t);
                        int score = d.Increment(SCORE, 100);
                        Debug.Log($"[Score] +100 = {score}");
                    })
                    .WhenAll())
                .Stage(s => s
                    .WithName("Heal Phase")
                    .Task(async (d, t) =>
                    {
                        await UniTask.Delay(500, cancellationToken: t);
                        int health = d.Increment(HEALTH, 50);
                        Debug.Log($"[Heal] +50 = {health}");
                    }))
                .Task(d => Debug.Log($"[Final] Health: {d.GetPrimary<int>(HEALTH)}, Score: {d.GetPrimary<int>(SCORE)}"))
                .BuildSequence();
        }
        
        [TaskSequenceMethod("Jump To Stage")]
        public static TaskSequence JumpToStageExample()
        {
            return TaskSequenceBuilder.Create("Jump To Stage")
                .Stage(s => s
                    .WithName("Menu")
                    .Task(d =>
                    {
                        Debug.Log("[Menu] Press G to start game, B for boss fight");
                        d.SetPrimary(BOSS_ACTIVE, false);
                    }))
                .Stage(s => s
                    .WithName("Game")
                    .Task(async (d, t) =>
                    {
                        Debug.Log("[Game] Playing...");
                        await UniTask.Delay(1000, cancellationToken: t);
                        if (Random.value > 0.5f)
                        {
                            Debug.Log("[Game] Boss appears!");
                            d.SetPrimary(BOSS_ACTIVE, true);
                            d.JumpToStage("Boss");
                        }
                    }))
                .Stage(s => s
                    .WithName("Boss")
                    .Task(async (d, t) =>
                    {
                        Debug.Log("[Boss] Fighting boss!");
                        await UniTask.Delay(1500, cancellationToken: t);
                        Debug.Log("[Boss] Boss defeated!");
                        d.SetPrimary(BOSS_ACTIVE, false);
                    }))
                .Stage(s => s
                    .WithName("Victory")
                    .Task(d => Debug.Log("[Victory] You win!")))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DELAY UNTIL - KEY PRESS TO CONTINUE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Demonstrates basic DelayUntil for "press key to continue" patterns.
        /// Each step waits for a specific keypress before advancing.
        /// Uses the Func&lt;bool&gt; overload for external checks that don't need data.
        /// </summary>
        [TaskSequenceMethod("DelayUntil: Key Press to Continue")]
        public static TaskSequence DelayUntilKeyPress()
        {
            return TaskSequenceBuilder.Create("Key Press to Continue")
                .WithErrorLogging(true)
                .WithMaxDuration(30)
                .Task(d => Debug.Log("=== PRESS KEY TO CONTINUE DEMO ==="))
                
                // Wait for Space
                .Task(d => Debug.Log("Step 1: Press [Space] to continue..."))
                .DelayUntil(_ => Input.GetKeyDown(KeyCode.Space))
                
                // Wait for Enter
                .Task(d => Debug.Log("Step 2: Press [Return] to continue..."))
                .DelayUntil(_ => Input.GetKeyDown(KeyCode.Return))
                
                // Wait for numbered choice - uses data packet to store result
                .Task(d =>
                {
                    d.SetPrimary(READY, false);
                    Debug.Log("Step 3: Press [Q], [W], or [E] to choose...");
                })
                .DelayUntil(d =>
                {
                    if (Input.GetKeyDown(KeyCode.Q))  { d.SetPrimary(DIALOGUE_CHOICE, "Q"); return true; }
                    if (Input.GetKeyDown(KeyCode.W))  { d.SetPrimary(DIALOGUE_CHOICE, "W"); return true; }
                    if (Input.GetKeyDown(KeyCode.E))  { d.SetPrimary(DIALOGUE_CHOICE, "E"); return true; }
                    return false;
                })
                
                .Task(d => Debug.Log($"You chose option [{d.GetPrimary<string>(DIALOGUE_CHOICE)}]!"))
                .Task(d => Debug.Log("=== COMPLETE ==="))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DELAY UNTIL - SCORE THRESHOLD (+/- to meet target)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Player presses +/- keys to adjust score. Sequence advances when threshold is met.
        /// Uses a parallel stage: input poller runs alongside DelayUntil.
        /// Also shows DelayWhile as the inverse pattern.
        /// </summary>
        [TaskSequenceMethod("DelayUntil: Score Threshold")]
        public static TaskSequence DelayUntilScoreThreshold()
        {
            return TaskSequenceBuilder.Create("Score Threshold")
                .Task(d =>
                {
                    d.SetPrimary(SCORE, 0);
                    Debug.Log("=== SCORE THRESHOLD DEMO ===");
                    Debug.Log("Press [W] +10 | Press [S] -10");
                    Debug.Log("Reach 100 points to advance!");
                })
                
                // Parallel stage: input handler runs alongside the DelayUntil
                .Stage(s => s
                    .WithName("Score Input")
                    .Task(async (d, t) =>
                    {
                        while (!t.IsCancellationRequested)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, t);
                            
                            if (Input.GetKeyDown(KeyCode.W))
                            {
                                int score = d.Increment(SCORE, 10);
                                Debug.Log($"[+10] Score: {score}/100");
                            }
                            else if (Input.GetKeyDown(KeyCode.S))
                            {
                                int score = d.Decrement(SCORE, 10);
                                Debug.Log($"[-10] Score: {score}/100");
                            }
                        }
                    })
                    // When this completes, WhenAny cancels the input poller
                    .SkipWhen(d => d.GetPrimary<int>(SCORE) >= 100))
                
                .Task(d =>
                {
                    Debug.Log($"Threshold reached! Final score: {d.GetPrimary<int>(SCORE)}");
                    Debug.Log("=== PHASE 1 COMPLETE ===");
                })
                
                // Second threshold: DelayWhile (inverse) - waits while score > 0
                .Task(d => Debug.Log("Now press [S] to drain score back to 0..."))
                .Stage(s => s
                    .WithName("Drain Score")
                    .Task(async (d, t) =>
                    {
                        while (!t.IsCancellationRequested)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, t);
                            if (Input.GetKeyDown(KeyCode.S))
                            {
                                int score = d.Decrement(SCORE, 10);
                                Debug.Log($"[-10] Score: {score}");
                            }
                        }
                    })
                    .SkipWhen(d => d.GetPrimary<int>(SCORE) <= 0))
                
                .Task(d => Debug.Log("Score drained! === COMPLETE ==="))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DELAY UNTIL - WITH TIMEOUT (Quick Time Events)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Player must press a key within a time limit or the stage auto-skips.
        /// Shows how DelayUntil respects cancellation from stage timeouts.
        /// </summary>
        [TaskSequenceMethod("DelayUntil: With Timeout")]
        public static TaskSequence DelayUntilWithTimeout()
        {
            return TaskSequenceBuilder.Create("DelayUntil With Timeout")
                .Task(d =>
                {
                    d.SetPrimary(READY, true);
                    Debug.Log("=== TIMED INPUT DEMO ===");
                })
                
                // QTE: 5 seconds to press Space
                .Task(d => Debug.Log("Press [Space] within 5 seconds!"))
                .Stage(s => s
                    .WithName("Timed Input")
                    .WithTimeout(5f)
                    .OnTimeout(ctx =>
                    {
                        ctx.Data.SetPrimary(READY, false);
                        Debug.Log("[Timeout!] Too slow!");
                    })
                    //.OnTerminate(() => Debug.Log("Terminated!"))
                    .DelayUntil(_ => Input.GetKeyDown(KeyCode.Space)))
                
                .Branch(b => b
                    .If(d => d.GetPrimary<bool>(READY), s => s
                        .Task(d => Debug.Log("You passed the quick-time event!")))
                    .Default(s => s
                        .Task(d => Debug.Log("You failed the quick-time event..."))))
                
                // Rapid taps within MaxDuration
                .Task(d =>
                {
                    d.SetPrimary(SCORE, 0);
                    Debug.Log("Press [Space] 5 times within 10 seconds!");
                })
                .Stage(s => s
                    .WithName("Rapid Taps")
                    .WithMaxDuration(10f, SkipStageInjection.Instance)
                    .OnMaxDuration(ctx =>
                    {
                        Debug.Log($"[Time's up!] Only got {ctx.Data.GetPrimary<int>(SCORE)} taps.");
                    })
                    .Task(async (d, t) =>
                    {
                        while (d.GetPrimary<int>(SCORE) < 5)
                        {
                            await UniTask.Yield(PlayerLoopTiming.Update, t);
                            if (Input.GetKeyDown(KeyCode.Space))
                            {
                                int taps = d.Increment(SCORE);
                                Debug.Log($"[Tap {taps}/5]");
                            }
                        }
                        Debug.Log("[Success!] All taps registered!");
                    }))
                
                .Task(d => Debug.Log("=== COMPLETE ==="))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DELAY UNTIL - REPEATING STAGE WITH GATE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Each repeat iteration waits for player input before proceeding.
        /// Combines WithRepeat + DelayUntil inside substages.
        /// Stage breaks out when a cumulative condition is met.
        /// </summary>
        [TaskSequenceMethod("DelayUntil: Repeating with Gate")]
        public static TaskSequenceChain DelayUntilRepeatingGate()
        {
            var main = TaskSequenceBuilder.Create("Repeating Gate")
                .Task(d =>
                {
                    d.SetPrimary(WAVE_COUNT, 0);
                    d.SetPrimary(SCORE, 0);
                    Debug.Log("=== REPEATING GATE DEMO ===");
                    Debug.Log("Each wave requires [Space] to start.");
                    Debug.Log("Press [Space] to begin!.");
                })
                .DelayUntil(_ => Input.GetKeyDown(KeyCode.Space))
                .BuildSequence();

            var wave = TaskSequenceBuilder.Create()
                
                // Wait to begin wave stage...
                .Stage(s => s
                    .Task(d =>
                    {
                        int wave = d.GetPrimary<int>(WAVE_COUNT) + 1;
                        Debug.Log($"Wave {wave} ready. Press [Space] to begin...");
                    })
                    .DelayUntil(_ => Input.GetKeyDown(KeyCode.Space)))
                
                // Fight wave stage...
                .Stage(s => s
                    .Task(async (d, t) =>
                    {
                        int wave = d.Increment(WAVE_COUNT);
                        int enemies = 2 + wave;
                        Debug.Log($"[Wave {wave}] Fighting {enemies} enemies...");
                                
                        for (int i = 0; i < enemies; i++)
                        {
                            await UniTask.Delay(300, cancellationToken: t);
                            d.Increment(SCORE, 25);
                            Debug.Log($"  Enemy {i + 1}/{enemies} defeated!");
                        }

                        if (wave >= 5)
                        {
                            d.Inject(InterruptSequenceInjection.Instance, true);
                        }
                    }))
                
                // Wave complete stage...
                .Stage(s => s
                    .Task(d =>
                    {
                        Debug.Log($"-- Wave {d.GetPrimary<int>(WAVE_COUNT)} complete " +
                                  $"| Total score: {d.GetPrimary<int>(SCORE)} --\n");
                    }))
                
                .OnTerminate((ctx, _) =>
                {
                    var score = ctx.Data.GetPrimary<int>(SCORE);

                    Debug.Log(ctx.Data.GetPrimary<int>(WAVE_COUNT) < 5 
                        ? $"[Defeat!] Score: {score}" 
                        : $"[Victory!] Score: {score}");
                })
                
                // Config
                .WithRepeat(true)
                .InterruptWhen(_ => Input.GetKeyDown(KeyCode.Escape))
                .BuildSequence();

            return main.Then(wave);
            
            /*return TaskSequenceBuilder.Create("Repeating Gate")
                .Task(d =>
                {
                    d.SetPrimary(WAVE_COUNT, 0);
                    d.SetPrimary(SCORE, 0);
                    Debug.Log("=== REPEATING GATE DEMO ===");
                    Debug.Log("Each wave requires [Space] to start.\n");
                })
                .Stage(s => s
                    .WithName("Gated Waves")
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(WAVE_COUNT) >= 5)
                    .OnRepeat(ctx =>
                    {
                        Debug.Log($"-- Wave {ctx.Data.GetPrimary<int>(WAVE_COUNT)} complete " +
                                  $"| Total score: {ctx.Data.GetPrimary<int>(SCORE)} --\n");
                    })
                    // Wait for player to confirm ready
                    .SubStage(sub => sub
                        .WithName("Wait for Ready")
                        .Task(d =>
                        {
                            int wave = d.GetPrimary<int>(WAVE_COUNT) + 1;
                            Debug.Log($"Wave {wave} ready. Press [Space] to begin...");
                        })
                        .DelayUntil(_ => Input.GetKeyDown(KeyCode.Space))
                        // Run the wave
                        .SubStage(_sub => _sub
                            .WithName("Wave Action")
                            .Task(async (d, t) =>
                            {
                                int wave = d.Increment(WAVE_COUNT);
                                int enemies = 2 + wave;
                                Debug.Log($"[Wave {wave}] Fighting {enemies} enemies...");
                                
                                for (int i = 0; i < enemies; i++)
                                {
                                    await UniTask.Delay(300, cancellationToken: t);
                                    d.Increment(SCORE, 25);
                                    Debug.Log($"  Enemy {i + 1}/{enemies} defeated!");
                                }
                            }))))
                .Task(d =>
                {
                    Debug.Log($"All waves cleared! Final score: {d.GetPrimary<int>(SCORE)}");
                    Debug.Log("=== COMPLETE ===");
                })
                .BuildSequence();*/
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DELAY UNTIL - STAGE BUILDER USAGE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Demonstrates DelayUntil inside StageBuilder for inline gating.
        /// Parallel tasks: stage waits until BOTH loading AND keypress complete.
        /// </summary>
        [TaskSequenceMethod("DelayUntil: Inside StageBuilder")]
        public static TaskSequence DelayUntilInStageBuilder()
        {
            return TaskSequenceBuilder.Create("StageBuilder DelayUntil")
                .Task(d =>
                {
                    d.SetPrimary(READY, false);
                    Debug.Log("=== STAGE BUILDER DELAY UNTIL DEMO ===\n");
                })
                
                // WhenAll: must BOTH finish loading AND press space
                .Stage(s => s
                    .WithName("Parallel Gate")
                    .Task(async (d, t) =>
                    {
                        Debug.Log("[Background] Loading assets...");
                        await UniTask.Delay(2000, cancellationToken: t);
                        d.SetPrimary(READY, true);
                        Debug.Log("[Background] Assets loaded! Press [Space]...");
                    })
                    .DelayUntil(d => d.GetPrimary<bool>(READY) && Input.GetKeyDown(KeyCode.Space))
                    .WhenAll())
                
                .Task(d => Debug.Log("Both conditions met! Proceeding..."))
                
                // Sequential substage with DelayUntil gate
                .Stage(s => s
                    .WithName("Sequential Gate")
                    .SubStage(sub => sub
                        .Task(async (d, t) =>
                        {
                            Debug.Log("[Setup] Preparing environment...");
                            await UniTask.Delay(1000, cancellationToken: t);
                            Debug.Log("[Setup] Done! Press [E] to enter...");
                        })
                        .DelayUntil(_ => Input.GetKeyDown(KeyCode.E))))
                
                .Task(d => Debug.Log("=== COMPLETE ==="))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DELAY WHILE - HOLD TO CHARGE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Demonstrates DelayWhile for "hold button to charge" patterns.
        /// Charge builds while Space is held, fires on release.
        /// </summary>
        [TaskSequenceMethod("DelayWhile: Hold to Charge")]
        public static TaskSequence DelayWhileHoldToCharge()
        {
            return TaskSequenceBuilder.Create("Hold to Charge")
                .Task(d =>
                {
                    d.SetPrimary(SCORE, 0);
                    Debug.Log("=== HOLD TO CHARGE DEMO ===");
                    Debug.Log("Hold [Space] to charge, release to fire!\n");
                })
                
                // Wait for player to start holding
                .Task(d => Debug.Log("Press and hold [Space]..."))
                .DelayUntil(_ => Input.GetKey(KeyCode.Space))
                
                // Charge while held
                .Stage(s => s
                    .WithName("Charging")
                    .Task(async (d, t) =>
                    {
                        Debug.Log("[Charging!]");
                        while (!t.IsCancellationRequested)
                        {
                            await UniTask.Delay(100, cancellationToken: t);
                            int charge = d.Increment(SCORE, 5);
                            if (charge >= 100)
                            {
                                d.SetPrimary(SCORE, 100);
                                Debug.Log("[Charge: MAX!]");
                                break;
                            }
                            Debug.Log($"[Charge: {charge}%]");
                        }
                    })
                    .DelayWhile(d => Input.GetKey(KeyCode.Space) && d.GetPrimary<int>(SCORE) < 100)
                    .WhenAny())
                
                .Task(d =>
                {
                    int charge = d.GetPrimary<int>(SCORE);
                    string power = charge >= 80 ? "DEVASTATING" : charge >= 50 ? "Strong" : "Weak";
                    Debug.Log($"[FIRE!] {power} shot at {charge}% power!");
                    Debug.Log("=== COMPLETE ===");
                })
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // GAME MECHANIC: TURN-BASED COMBAT
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Full turn-based combat system.
        /// Features: WithRepeat, DelayUntil for player input, Branch for resolution,
        /// StopRepeatWhen for victory/defeat, OnRepeat callbacks, substages for phases.
        /// </summary>
        [TaskSequenceMethod("Game: Turn-Based Combat")]
        public static TaskSequence TurnBasedCombat()
        {
            return TaskSequenceBuilder.Create("Turn-Based Combat")
                .WithInjectionLogging(true)
                
                .Task(d =>
                {
                    d.SetPrimary(HEALTH, 100);
                    d.SetPrimary(PLAYER_MANA, 50);
                    d.SetPrimary(PLAYER_BLOCK, false);
                    d.SetPrimary(ENEMY_HEALTH, 120);
                    d.SetPrimary(TURN, 0);
                    d.SetPrimary(PLAYER_ALIVE, true);
                    d.SetPrimary(LAST_INPUT, "none");
                    
                    Debug.Log("+=== TURN-BASED COMBAT ===+");
                    Debug.Log("| [1] Attack  (10-20 dmg)             |");
                    Debug.Log("| [2] Heavy   (25-40 dmg, costs 20mp) |");
                    Debug.Log("| [3] Block   (halves next hit)        |");
                    Debug.Log("| [4] Heal    (30hp, costs 15mp)       |");
                    Debug.Log("+======================================+\n");
                })
                
                // Combat Loop
                .Stage(s => s
                    .WithName("Combat Loop")
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ENEMY_HEALTH) <= 0)
                    .StopRepeatWhen(d => !d.GetPrimary<bool>(PLAYER_ALIVE))
                    .WithMaxDuration(120f, InterruptSequenceInjection.Instance)
                    .OnRepeat(ctx =>
                    {
                        var d = ctx.Data;
                        Debug.Log($"-- Turn {d.GetPrimary<int>(TURN)} end " +
                                  $"| HP:{d.GetPrimary<int>(HEALTH)} " +
                                  $"MP:{d.GetPrimary<int>(PLAYER_MANA)} " +
                                  $"| Enemy:{d.GetPrimary<int>(ENEMY_HEALTH)} --\n");
                    })
                    
                    // Player turn: wait for valid input
                    .SubStage(sub => sub
                        .WithName("Player Turn")
                        .Task(d =>
                        {
                            int turn = d.Increment(TURN);
                            d.SetPrimary(LAST_INPUT, "none");
                            Debug.Log($"--- TURN {turn} ---");
                            Debug.Log($"  HP: {d.GetPrimary<int>(HEALTH)} | MP: {d.GetPrimary<int>(PLAYER_MANA)} | Enemy: {d.GetPrimary<int>(ENEMY_HEALTH)}");
                            Debug.Log("  Choose: [1] Attack [2] Heavy [3] Block [4] Heal");
                        })
                        .DelayUntil(d =>
                        {
                            if (Input.GetKeyDown(KeyCode.Alpha1))      { d.SetPrimary(LAST_INPUT, "attack"); return true; }
                            else if (Input.GetKeyDown(KeyCode.Alpha2))
                            {
                                if (d.GetPrimary<int>(PLAYER_MANA) >= 20) { d.SetPrimary(LAST_INPUT, "heavy"); return true; }
                                Debug.Log("  Not enough mana!");
                            }
                            else if (Input.GetKeyDown(KeyCode.Alpha3)) { d.SetPrimary(LAST_INPUT, "block"); return true; }
                            else if (Input.GetKeyDown(KeyCode.Alpha4))
                            {
                                if (d.GetPrimary<int>(PLAYER_MANA) >= 15) { d.SetPrimary(LAST_INPUT, "heal"); return true; }
                                Debug.Log("  Not enough mana!");
                            }
                            return false;
                        }))
                    
                    // Resolve player action
                    .SubStage(sub => sub
                        .WithName("Resolve Action")
                        .Task(async (d, t) =>
                        {
                            string action = d.GetPrimary<string>(LAST_INPUT);
                            await UniTask.Delay(300, cancellationToken: t);
                            
                            switch (action)
                            {
                                case "attack":
                                    int dmg = Random.Range(10, 21);
                                    int enemyHp = d.Decrement(ENEMY_HEALTH, dmg);
                                    Debug.Log($"  >> Attack! -{dmg} dmg | Enemy HP: {enemyHp}");
                                    break;
                                case "heavy":
                                    int heavyDmg = Random.Range(25, 41);
                                    d.Decrement(PLAYER_MANA, 20);
                                    int eHp = d.Decrement(ENEMY_HEALTH, heavyDmg);
                                    Debug.Log($"  >> Heavy strike! -{heavyDmg} dmg (-20mp) | Enemy HP: {eHp}");
                                    break;
                                case "block":
                                    d.SetPrimary(PLAYER_BLOCK, true);
                                    Debug.Log("  >> Blocking! Next hit halved.");
                                    break;
                                case "heal":
                                    d.Decrement(PLAYER_MANA, 15);
                                    int hp = d.Increment(HEALTH, 30);
                                    Debug.Log($"  >> Heal! +30hp (-15mp) | HP: {hp}");
                                    break;
                            }
                        }))
                    
                    // Enemy turn
                    .SubStage(sub => sub
                        .WithName("Enemy Turn")
                        .Task(async (d, t) =>
                        {
                            if (d.GetPrimary<int>(ENEMY_HEALTH) <= 0) return;
                            
                            await UniTask.Delay(500, cancellationToken: t);
                            int baseDmg = Random.Range(8, 18);
                            bool isBlocking = d.GetPrimary<bool>(PLAYER_BLOCK);
                            int actualDmg = isBlocking ? baseDmg / 2 : baseDmg;
                            int hp = d.Decrement(HEALTH, actualDmg);
                            
                            string blockMsg = isBlocking ? " (BLOCKED!)" : "";
                            Debug.Log($"  << Enemy attacks! -{actualDmg} dmg{blockMsg} | HP: {hp}");
                            d.SetPrimary(PLAYER_BLOCK, false);
                            
                            int mana = d.Increment(PLAYER_MANA, 5);
                            Debug.Log($"  [+5 mana regen] MP: {mana}");
                            
                            if (hp <= 0) d.SetPrimary(PLAYER_ALIVE, false);
                        })))
                
                // Result
                .Branch(b => b
                    .If(d => d.GetPrimary<int>(ENEMY_HEALTH) <= 0, s => s
                        .Task(d =>
                        {
                            Debug.Log("+== VICTORY! ==+");
                            Debug.Log($"  Turns: {d.GetPrimary<int>(TURN)} | HP: {d.GetPrimary<int>(HEALTH)} | MP: {d.GetPrimary<int>(PLAYER_MANA)}");
                        }))
                    .Default(s => s
                        .Task(d =>
                        {
                            Debug.Log("+== DEFEAT... ==+");
                            Debug.Log($"  Enemy HP remaining: {d.GetPrimary<int>(ENEMY_HEALTH)}");
                        })))
                .OnComplete((ctx, success) => Debug.Log($"[Combat] Complete: {success}"))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // GAME MECHANIC: STATE OBSERVER
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Uses global + stage-local conditions as automatic state observers.
        /// Features: Global InjectWhen, stage-local InjectWhen, AllowInjections,
        /// DisallowInjections, ConditionCheckTiming, DelayUntil for state-driven flow.
        /// </summary>
        [TaskSequenceMethod("Game: State Observer")]
        public static TaskSequence GameStateObserver()
        {
            return TaskSequenceBuilder.Create("State Observer")
                .WithConditionCheckTiming(EProcessStepTiming.Update)
                
                // Global: death = interrupt entire sequence
                .InjectWhen(
                    d => d.GetPrimary<int>(HEALTH) <= 0,
                    InterruptSequenceInjection.Instance,
                    fireOnce: true)
                
                .Task(d =>
                {
                    d.SetPrimary(HEALTH, 100);
                    d.SetPrimary(SCORE, 0);
                    d.SetPrimary(ENEMIES_ALIVE, 8);
                    d.SetPrimary(PLAYER_ALIVE, true);
                    d.SetPrimary(GAME_STATE, "exploring");
                    Debug.Log("=== STATE OBSERVER DEMO ===\n");
                })
                
                // Exploration: stage-local condition auto-skips when score threshold hit
                .Stage(s => s
                    .WithName("Exploration")
                    .InjectWhen(
                        d => d.GetPrimary<int>(SCORE) >= 200,
                        SkipStageInjection.Instance,
                        fireOnce: true)
                    .WithTimeout(15f)
                    .OnTimeout((_, _) => Debug.Log("[Explore] Time limit reached!"))
                    .Task(async (d, t) =>
                    {
                        Debug.Log("[Exploring] Defeat enemies for score. 200 to find boss.");
                        while (!t.IsCancellationRequested)
                        {
                            await UniTask.Delay(800, cancellationToken: t);
                            if (d.GetPrimary<int>(ENEMIES_ALIVE) > 0 && Random.value > 0.3f)
                            {
                                d.Decrement(ENEMIES_ALIVE);
                                int score = d.Increment(SCORE, 30);
                                Debug.Log($"[Explore] Enemy defeated! Score: {score}/200");
                            }
                            if (Random.value > 0.7f)
                            {
                                int hp = d.Decrement(HEALTH, 8);
                                Debug.Log($"[Explore] Trap! -8 HP = {hp}");
                            }
                        }
                    }))
                
                .Task(d =>
                {
                    d.SetPrimary(BOSS_ACTIVE, true);
                    d.SetPrimary(GAME_STATE, "boss");
                    Debug.Log($"[!] BOSS ENCOUNTER! HP: {d.GetPrimary<int>(HEALTH)}\n");
                })
                
                // Boss: protected stage - only interrupt allowed
                .Stage(s => s
                    .WithName("Boss Fight")
                    .AllowOnlyInterrupt()
                    .WithMaxDuration(30f, InterruptSequenceInjection.Instance)
                    .OnMaxDuration(ctx => Debug.Log("[Boss] Fight too long - retreating!"))
                    .Task(async (d, t) =>
                    {
                        d.SetPrimary(BOSS_HEALTH, 200);
                        while (d.GetPrimary<int>(BOSS_HEALTH) > 0 && !t.IsCancellationRequested)
                        {
                            await UniTask.Delay(500, cancellationToken: t);
                            int dmg = Random.Range(15, 30);
                            int bossHp = d.Decrement(BOSS_HEALTH, dmg);
                            Debug.Log($"[Boss] Hit! -{dmg} | Boss HP: {bossHp}");
                            if (Random.value > 0.4f)
                            {
                                int pDmg = Random.Range(12, 22);
                                int hp = d.Decrement(HEALTH, pDmg);
                                Debug.Log($"[Boss] Retaliation! -{pDmg} | HP: {hp}");
                            }
                        }
                    }))
                
                .Branch(b => b
                    .If(d => d.GetPrimary<int>(HEALTH) > 0 && d.GetPrimary<int>(BOSS_HEALTH) <= 0, s => s
                        .Task(d =>
                        {
                            d.SetPrimary(GAME_STATE, "victory");
                            Debug.Log($"=== VICTORY! HP: {d.GetPrimary<int>(HEALTH)}, Score: {d.GetPrimary<int>(SCORE)} ===");
                        }))
                    .Default(s => s
                        .Task(d =>
                        {
                            d.SetPrimary(GAME_STATE, "defeat");
                            Debug.Log("=== DEFEAT... ===");
                        })))
                .OnTerminate((ctx, success) =>
                    Debug.Log($"[Observer] Final state: {ctx.Data.GetPrimary<string>(GAME_STATE)}"))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // GAME MECHANIC: COMBO TRACKER
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Fighting game combo system with timed input windows.
        /// Features: DelayUntil for each hit input, stage timeout as combo drop timer,
        /// WithRepeat + StopRepeatWhen for combo chain, parallel feedback,
        /// exponential scoring, finisher mechanic.
        /// </summary>
        [TaskSequenceMethod("Game: Combo Tracker")]
        public static TaskSequence ComboTracker()
        {
            return TaskSequenceBuilder.Create("Combo Tracker")
                .WithErrorLogging(true)
                
                .Task(d =>
                {
                    d.SetPrimary(COMBO_COUNT, 0);
                    d.SetPrimary(SCORE, 0);
                    d.SetPrimary(COMBO_ACTIVE, true);
                    d.SetPrimary(ENEMY_HEALTH, 500);
                    
                    Debug.Log("+======= COMBO SYSTEM =======+");
                    Debug.Log("| [J] Light    (+1 combo)     |");
                    Debug.Log("| [K] Medium   (+1 combo)     |");
                    Debug.Log("| [L] Finisher (ends combo)   |");
                    Debug.Log("| Hit within 1.5s to chain!   |");
                    Debug.Log("| [Escape] to end             |");
                    Debug.Log("+=============================+\n");
                })
                
                // Combo Loop (repeats for multiple combos)
                .Stage(s => s
                    .WithName("Combo Round")
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ENEMY_HEALTH) <= 0)
                    .StopRepeatWhen(d => !d.GetPrimary<bool>(COMBO_ACTIVE))
                    .OnRepeat(ctx =>
                    {
                        var d = ctx.Data;
                        int combo = d.GetPrimary<int>(COMBO_COUNT);
                        if (combo > 0)
                        {
                            int bonus = combo * combo * 10;
                            d.Increment(SCORE, bonus);
                            Debug.Log($"  -- {combo}-hit combo! +{bonus} bonus --\n");
                        }
                        d.SetPrimary(COMBO_COUNT, 0);
                    })
                    
                    // Wait for first hit
                    .SubStage(sub => sub
                        .WithName("Await First Hit")
                        .Task(d =>
                        {
                            int enemyHp = d.GetPrimary<int>(ENEMY_HEALTH);
                            Debug.Log($"[Ready] Enemy HP: {enemyHp} | Press [J] or [K]...");
                        })
                        .DelayUntil(d =>
                        {
                            if (Input.GetKeyDown(KeyCode.Escape))
                            {
                                d.SetPrimary(COMBO_ACTIVE, false);
                                return true;
                            }
                            return Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.K);
                        })
                        .Task(d =>
                        {
                            if (!d.GetPrimary<bool>(COMBO_ACTIVE)) return;
                            int combo = d.Increment(COMBO_COUNT);
                            int dmg = 10;
                            int enemyHp = d.Decrement(ENEMY_HEALTH, dmg);
                            Debug.Log($"  [{combo} HIT] -{dmg} dmg | Enemy: {enemyHp}");
                        }))
                    
                    // Chain hits with timed window
                    .SubStage(sub => sub
                        .WithName("Combo Chain")
                        .WithRepeat(true)
                        .StopRepeatWhen(d => !d.GetPrimary<bool>(COMBO_ACTIVE))
                        .StopRepeatWhen(d => d.GetPrimary<int>(ENEMY_HEALTH) <= 0)
                        .WithTimeout(1.5f, BreakStageRepeatInjection.Instance)
                        .OnTimeout((_, _) => Debug.Log("  [Combo dropped! Too slow!]"))
                        .Task(async (d, t) =>
                        {
                            bool gotInput = false;
                            bool isFinisher = false;
                            
                            while (!gotInput && !t.IsCancellationRequested)
                            {
                                await UniTask.Yield(PlayerLoopTiming.Update, t);
                                
                                if (Input.GetKeyDown(KeyCode.Escape))
                                {
                                    d.SetPrimary(COMBO_ACTIVE, false);
                                    return;
                                }
                                if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.K))
                                {
                                    gotInput = true;
                                }
                                else if (Input.GetKeyDown(KeyCode.L))
                                {
                                    gotInput = true;
                                    isFinisher = true;
                                }
                            }
                            
                            if (!gotInput) return;
                            
                            int combo = d.Increment(COMBO_COUNT);
                            int baseDmg = isFinisher ? 15 + combo * 5 : 10;
                            int enemyHp = d.Decrement(ENEMY_HEALTH, baseDmg);
                            
                            if (isFinisher)
                            {
                                Debug.Log($"  [{combo} HIT - FINISHER!] -{baseDmg} dmg | Enemy: {enemyHp}");
                                d.BreakStageRepeat();
                            }
                            else
                            {
                                Debug.Log($"  [{combo} HIT] -{baseDmg} dmg | Enemy: {enemyHp}");
                            }
                        })))
                
                .Branch(b => b
                    .If(d => d.GetPrimary<int>(ENEMY_HEALTH) <= 0, s => s
                        .Task(d =>
                        {
                            Debug.Log("+== K.O.! ==+");
                            Debug.Log($"  Total Score: {d.GetPrimary<int>(SCORE)}");
                        }))
                    .Default(s => s
                        .Task(d =>
                        {
                            Debug.Log($"[End] Score: {d.GetPrimary<int>(SCORE)} | Enemy HP: {d.GetPrimary<int>(ENEMY_HEALTH)}");
                        })))
                .OnTerminate((ctx, success) => Debug.Log("[Combo System] Terminated"))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // GAME MECHANIC: ROUND-BASED ARENA (Chain)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Multi-phase arena game using Chain: lobby -> rounds -> results.
        /// Features: Chain for sequencing phases, DelayUntil for lobby ready-up,
        /// sequence repeat for round cycling, InterruptWhen at chain level,
        /// shared state across chained sequences.
        /// </summary>
        [TaskSequenceMethod("Game: Round-Based Arena")]
        public static TaskSequenceChain RoundBasedArena()
        {
            // Lobby
            var lobby = TaskSequenceBuilder.Create("Arena Lobby")
                .Task(d =>
                {
                    d.SetPrimary(HEALTH, 150);
                    d.SetPrimary(PLAYER_MANA, 100);
                    d.SetPrimary(SCORE, 0);
                    d.SetPrimary(ROUND, 0);
                    d.SetPrimary(PLAYER_ALIVE, true);
                    d.SetPrimary(DIFFICULTY, 1.0f);
                    Debug.Log("+== ARENA LOBBY ==+");
                    Debug.Log("Press [Space] when ready!\n");
                })
                .DelayUntil(_ => Input.GetKeyDown(KeyCode.Space))
                .Task(d => Debug.Log("[!] Entering arena...\n"))
                .Delay(1f)
                .BuildSequence();
            
            // Combat Rounds (repeats)
            var rounds = TaskSequenceBuilder.Create("Arena Rounds")
                .InterruptWhen(d => !d.GetPrimary<bool>(PLAYER_ALIVE))
                
                // Round intro
                .Stage(s => s
                    .WithName("Round Start")
                    .Task(async (d, t) =>
                    {
                        int round = d.Increment(ROUND);
                        float diff = d.GetPrimary<float>(DIFFICULTY);
                        int enemyHp = Mathf.RoundToInt(80 * diff);
                        d.SetPrimary(ENEMY_HEALTH, enemyHp);
                        Debug.Log($"=== ROUND {round} (Difficulty: {diff:F1}x) ===");
                        Debug.Log($"  Enemy HP: {enemyHp}");
                        for (int i = 3; i > 0; i--)
                        {
                            Debug.Log($"  {i}...");
                            await UniTask.Delay(800, cancellationToken: t);
                        }
                        Debug.Log("  FIGHT!\n");
                    }))
                
                // Combat turns
                .Stage(s => s
                    .WithName("Combat")
                    .WithRepeat(true)
                    .StopRepeatWhen(d => d.GetPrimary<int>(ENEMY_HEALTH) <= 0)
                    .StopRepeatWhen(d => !d.GetPrimary<bool>(PLAYER_ALIVE))
                    .WithMaxDuration(45f, BreakStageRepeatInjection.Instance)
                    .SubStage(sub => sub
                        .Task(d => Debug.Log("  [1] Attack  [2] Fireball (30mp)  [3] Block"))
                        .DelayUntil(d =>
                        {
                            if (Input.GetKeyDown(KeyCode.Alpha1))      { d.SetPrimary(LAST_INPUT, "attack"); return true; }
                            else if (Input.GetKeyDown(KeyCode.Alpha2))
                            {
                                if (d.GetPrimary<int>(PLAYER_MANA) >= 30) { d.SetPrimary(LAST_INPUT, "fireball"); return true; }
                                Debug.Log("    Not enough mana!");
                            }
                            else if (Input.GetKeyDown(KeyCode.Alpha3)) { d.SetPrimary(LAST_INPUT, "block"); return true; }
                            return false;
                        }))
                    .SubStage(sub => sub
                        .Task(async (d, t) =>
                        {
                            await UniTask.Delay(200, cancellationToken: t);
                            string action = d.GetPrimary<string>(LAST_INPUT);
                            float diff = d.GetPrimary<float>(DIFFICULTY);
                            
                            switch (action)
                            {
                                case "attack":
                                    int dmg = Random.Range(15, 25);
                                    Debug.Log($"  >> Attack! -{dmg}");
                                    d.Decrement(ENEMY_HEALTH, dmg);
                                    break;
                                case "fireball":
                                    int fbDmg = Random.Range(35, 55);
                                    d.Decrement(PLAYER_MANA, 30);
                                    Debug.Log($"  >> Fireball! -{fbDmg} (-30mp)");
                                    d.Decrement(ENEMY_HEALTH, fbDmg);
                                    break;
                                case "block":
                                    d.SetPrimary(PLAYER_BLOCK, true);
                                    int mp = d.Increment(PLAYER_MANA, 10);
                                    Debug.Log($"  >> Block! (+10mp = {mp})");
                                    break;
                            }
                            
                            if (d.GetPrimary<int>(ENEMY_HEALTH) > 0)
                            {
                                int eDmg = Mathf.RoundToInt(Random.Range(10, 20) * diff);
                                bool blocked = d.GetPrimary<bool>(PLAYER_BLOCK);
                                if (blocked) eDmg /= 2;
                                int hp = d.Decrement(HEALTH, eDmg);
                                Debug.Log($"  << Enemy! -{eDmg}{(blocked ? " (blocked)" : "")} | HP: {hp}");
                                d.SetPrimary(PLAYER_BLOCK, false);
                                if (hp <= 0) d.SetPrimary(PLAYER_ALIVE, false);
                            }
                            Debug.Log($"  [Enemy:{d.GetPrimary<int>(ENEMY_HEALTH)} You:{d.GetPrimary<int>(HEALTH)}hp {d.GetPrimary<int>(PLAYER_MANA)}mp]");
                        })))
                
                // Between rounds
                .Branch(b => b
                    .If(d => d.GetPrimary<int>(ENEMY_HEALTH) <= 0 && d.GetPrimary<bool>(PLAYER_ALIVE), s => s
                        .Task(async (d, t) =>
                        {
                            int roundScore = d.GetPrimary<int>(ROUND) * 100;
                            d.Increment(SCORE, roundScore);
                            d.IncrementFloat(DIFFICULTY, 0.15f);
                            int heal = d.Increment(HEALTH, 30);
                            int mp = d.Increment(PLAYER_MANA, 20);
                            Debug.Log($"  [Round Clear!] +{roundScore}pts | HP:{heal} MP:{mp}");
                            Debug.Log("  Press [Space] for next round...\n");
                            await UniTask.WaitUntil(() => Input.GetKeyDown(KeyCode.Space), cancellationToken: t);
                        }))
                    .Default(s => s
                        .Task(d =>
                        {
                            d.SetPrimary(PLAYER_ALIVE, false);
                            Debug.Log("  [Defeated!]");
                        })))
                .WithRepeat(true)
                .BuildSequence();
            
            // Results
            var results = TaskSequenceBuilder.Create("Arena Results")
                .Task(d =>
                {
                    Debug.Log("+== ARENA RESULTS ==+");
                    Debug.Log($"  Rounds: {d.GetPrimary<int>(ROUND)}");
                    Debug.Log($"  Score:  {d.GetPrimary<int>(SCORE)}");
                    Debug.Log($"  Final HP: {d.GetPrimary<int>(HEALTH)}");
                    Debug.Log("+====================+");
                })
                .BuildSequence();
            
            return lobby.Then(rounds).Then(results);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // GAME MECHANIC: BRANCHING DIALOGUE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Branching dialogue system using DelayUntil for player choice selection.
        /// Features: DelayUntil for numbered choices, nested Branch for dialogue tree,
        /// state tracking (reputation), conditional dialogue outcomes.
        /// </summary>
        [TaskSequenceMethod("Game: Branching Dialogue")]
        public static TaskSequence BranchingDialogue()
        {
            return TaskSequenceBuilder.Create("Dialogue System")
                .Task(d =>
                {
                    d.SetPrimary(DIALOGUE_CHOICE, 0);
                    d.SetPrimary(SCORE, 0); // Reputation
                })
                
                // NPC Introduction
                .Task(async (d, t) =>
                {
                    Debug.Log("+== THE MERCHANT ==+\n");
                    await UniTask.Delay(500, cancellationToken: t);
                    Debug.Log("Merchant: \"Welcome, traveler! I have a job");
                    Debug.Log("  that requires... discretion.\"");
                    await UniTask.Delay(1000, cancellationToken: t);
                    Debug.Log("[1] \"I'm listening.\"");
                    Debug.Log("[2] \"Not interested.\"");
                    Debug.Log("[3] \"What's the pay?\"");
                })
                
                .DelayUntil(d =>
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1))      { d.SetPrimary(DIALOGUE_CHOICE, 1); return true; }
                    else if (Input.GetKeyDown(KeyCode.Alpha2))  { d.SetPrimary(DIALOGUE_CHOICE, 2); return true; }
                    else if (Input.GetKeyDown(KeyCode.Alpha3))  { d.SetPrimary(DIALOGUE_CHOICE, 3); return true; }
                    return false;
                })
                
                // Branch on first choice
                .Branch(b => b
                    .If(d => d.GetPrimary<int>(DIALOGUE_CHOICE) == 1, s => s
                        .Task(async (d, t) =>
                        {
                            d.Increment(SCORE, 10);
                            Debug.Log("Merchant: \"Good. I need a rare herb from Darkwood.\"");
                            await UniTask.Delay(1000, cancellationToken: t);
                            Debug.Log("[1] \"Consider it done.\" (Accept)");
                            Debug.Log("[2] \"The Darkwood is dangerous...\" (Negotiate)");
                        })
                        .DelayUntil(d =>
                        {
                            if (Input.GetKeyDown(KeyCode.Alpha1))      { d.SetPrimary(DIALOGUE_CHOICE, 10); return true; }
                            else if (Input.GetKeyDown(KeyCode.Alpha2)) { d.SetPrimary(DIALOGUE_CHOICE, 11); return true; }
                            return false;
                        }))
                    .If(d => d.GetPrimary<int>(DIALOGUE_CHOICE) == 2, s => s
                        .Task(async (d, t) =>
                        {
                            d.Decrement(SCORE, 5);
                            Debug.Log("Merchant: \"Hmph. Your loss, traveler.\"");
                            await UniTask.Delay(800, cancellationToken: t);
                            d.SetPrimary(DIALOGUE_CHOICE, -1);
                        }))
                    .Default(s => s
                        .Task(async (d, t) =>
                        {
                            d.Increment(SCORE, 5);
                            Debug.Log("Merchant: \"500 gold for a rare herb.\"");
                            await UniTask.Delay(1000, cancellationToken: t);
                            Debug.Log("[1] \"Deal.\" (Accept)");
                            Debug.Log("[2] \"I want 800.\" (Haggle)");
                        })
                        .DelayUntil(d =>
                        {
                            if (Input.GetKeyDown(KeyCode.Alpha1))      { d.SetPrimary(DIALOGUE_CHOICE, 10); return true; }
                            else if (Input.GetKeyDown(KeyCode.Alpha2)) { d.SetPrimary(DIALOGUE_CHOICE, 20); return true; }
                            return false;
                        })))
                
                // Resolution
                .Branch(b => b
                    .If(d => d.GetPrimary<int>(DIALOGUE_CHOICE) == -1, s => s
                        .Task(d => Debug.Log("[Quest declined.]")))
                    .If(d => d.GetPrimary<int>(DIALOGUE_CHOICE) == 10, s => s
                        .Task(d =>
                        {
                            d.Increment(SCORE, 10);
                            Debug.Log("[Quest accepted: Retrieve Darkwood Herb]");
                            Debug.Log($"[Reputation: {d.GetPrimary<int>(SCORE)}]");
                        }))
                    .If(d => d.GetPrimary<int>(DIALOGUE_CHOICE) == 11, s => s
                        .Task(async (d, t) =>
                        {
                            d.Increment(SCORE, 15);
                            Debug.Log("Merchant: \"Fine, I'll add a healing potion.\"");
                            await UniTask.Delay(800, cancellationToken: t);
                            Debug.Log("[Quest accepted + bonus potion]");
                            Debug.Log($"[Reputation: {d.GetPrimary<int>(SCORE)}]");
                        }))
                    .Default(s => s
                        .Task(async (d, t) =>
                        {
                            bool success = d.GetPrimary<int>(SCORE) >= 10;
                            if (success)
                            {
                                Debug.Log("Merchant: \"Fine... 700 gold.\"");
                                Debug.Log("[Quest accepted: 700 gold]");
                            }
                            else
                            {
                                d.Decrement(SCORE, 10);
                                Debug.Log("Merchant: \"Don't push your luck.\"");
                                Debug.Log("[Quest accepted: 500 gold (haggle failed)]");
                            }
                            await UniTask.Delay(500, cancellationToken: t);
                            Debug.Log($"[Final Reputation: {d.GetPrimary<int>(SCORE)}]");
                        })))
                
                .Task(d => Debug.Log("=== DIALOGUE COMPLETE ==="))
                .OnComplete((ctx, success) => Debug.Log($"[Dialogue] Complete: {success}"))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SURVIVAL GAME - SIMPLE ROUND-BASED
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("Survival: Basic Round")]
        public static TaskSequence SurvivalBasicRound()
        {
            return TaskSequenceBuilder.Create("Basic Survival Round")
                .Task(d =>
                {
                    int round = d.GetOrInit(ROUND, 1);
                    d.SetPrimary(ENEMIES_ALIVE, 5 + round * 2);
                    d.SetPrimary(PLAYER_ALIVE, true);
                    d.SetPrimary(HEALTH, 50);
                    Debug.Log($"=== ROUND {round} START ===");
                    Debug.Log($"Enemies: {d.GetPrimary<int>(ENEMIES_ALIVE)}");
                })
                .Stage(s => s
                    .WithName("Combat")
                    .InterruptWhen(data => Input.GetKeyDown(KeyCode.Escape))
                    .WithMaxDuration(30f)
                    .OnMaxDuration(ctx => Debug.Log("[Timeout] Round taking too long!"))
                    .Task(async (d, t) =>
                    {
                        while (d.GetPrimary<int>(ENEMIES_ALIVE) > 0)
                        {
                            await UniTask.Delay(500, cancellationToken: t);
                            if (Random.value > 0.3f)
                            {
                                int enemies = d.Decrement(ENEMIES_ALIVE);
                                int score = d.Increment(SCORE, 50);
                                Debug.Log($"[Kill] Remaining: {enemies}, Score: {score}");
                            }
                            else
                            {
                                int health = d.Decrement(HEALTH, 10);
                                Debug.Log($"[Hit] Health: {health}");
                                if (health <= 0)
                                {
                                    d.SetPrimary(PLAYER_ALIVE, false);
                                    Debug.Log("[Death] Player died!");
                                    d.Interrupt();
                                }
                            }
                        }
                    }))
                .Stage(stage => stage
                    .Task(async (d, t) =>
                    {
                        Debug.Log("Waiting...");
                        await UniTask.Delay(1500, cancellationToken: t);
                    }))
                .Branch(b => b
                    .If(d => d.GetPrimary<bool>(PLAYER_ALIVE), s => s
                        .Task(d =>
                        {
                            int round = d.Increment(ROUND);
                            int health = d.Increment(HEALTH, 25);
                            Debug.Log($"=== ROUND {round - 1} COMPLETE === HP: {health}");
                        }))
                    .Default(s => s
                        .Task(d =>
                        {
                            Debug.Log($"=== GAME OVER === Score: {d.GetPrimary<int>(SCORE)}, Rounds: {d.GetPrimary<int>(ROUND)}");
                        })))
                .WithRepeat(true)
                .OnComplete((ctx, success) => Debug.Log($"Game complete: {success}"))
                .OnTerminate((ctx, success) => Debug.Log($"Game terminated: {success}"))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TIMEOUT & MAX DURATION / EXCEPTION HANDLING / RETRY
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("Timeout Callbacks")]
        public static TaskSequence TimeoutCallbacks()
        {
            return TaskSequenceBuilder.Create("Timeout Callbacks")
                .WithMaxDuration(10f)
                .OnMaxDuration(ctx => Debug.LogWarning($"[Sequence] Max duration at {ctx.ElapsedTime:F2}s"))
                .Stage(s => s
                    .WithName("Network Request")
                    .WithTimeout(3f)
                    .OnTimeout((_, _) => Debug.LogWarning("[Stage] Network timed out after 3s"))
                    .Task(async (d, t) =>
                    {
                        Debug.Log("Starting network request...");
                        await UniTask.Delay(5000, cancellationToken: t);
                    }))
                .Stage(s => s
                    .WithName("Database Query")
                    .WithMaxDuration(2f)
                    .OnMaxDuration(ctx => Debug.LogWarning("[Stage] DB query exceeded max duration"))
                    .Task(async (d, t) =>
                    {
                        Debug.Log("Querying database...");
                        await UniTask.Delay(3000, cancellationToken: t);
                    }))
                .Task(d => Debug.Log("Done"))
                .BuildSequence();
        }
        
        [TaskSequenceMethod("Exception Handling")]
        public static TaskSequence ExceptionHandling()
        {
            return TaskSequenceBuilder.Create("Exception Handling")
                .OnException((ctx, ex) =>
                {
                    Debug.LogWarning($"[Sequence] Caught: {ex.Message}");
                    return ex is TimeoutException;
                })
                .OnComplete((ctx, success) => Debug.Log($"[Sequence] Complete: {(success ? "SUCCESS" : "FAILED")}"))
                .Stage(s => s
                    .WithName("Risky Stage 1")
                    .OnException((ctx, ex) =>
                    {
                        Debug.LogWarning($"[Stage 1] {ex.Message}");
                        return true; // Suppress
                    })
                    .Task(d =>
                    {
                        Debug.Log("Stage 1 throwing...");
                        throw new InvalidOperationException("Error in stage 1");
                    }))
                .Stage(s => s
                    .WithName("Risky Stage 2")
                    .OnException((ctx, ex) =>
                    {
                        Debug.LogWarning($"[Stage 2] {ex.Message}");
                        return false; // Propagate
                    })
                    .Task(d =>
                    {
                        Debug.Log("Stage 2 - runs because stage 1 was suppressed");
                        throw new ArgumentException("Error in stage 2");
                    }))
                .Task(d => Debug.Log("This won't run"))
                .BuildSequence();
        }
        
        [TaskSequenceMethod("Retry Pattern")]
        public static TaskSequence RetryPattern()
        {
            int attempts = 0;
            const int maxAttempts = 3;
            
            return TaskSequenceBuilder.Create("Retry Pattern")
                .Task(async (d, t) =>
                {
                    await UniTask.Delay(500, cancellationToken: t);
                    if (Random.value < 0.7f)
                        throw new Exception("Random failure");

                    attempts++;
                    Debug.Log($"[Success] Attempt: {attempts}/{maxAttempts}!");
                    d.SetPrimary(SCORE, true);
                    d.Interrupt();
                })
                .OnException((ctx, ex) =>
                {
                    Debug.Log($"[Failed] Attempt {attempts}/{maxAttempts} (Caught exception)");
                    return attempts < maxAttempts;
                })
                .OnTerminate((ctx, success) =>
                {
                    var msg = ctx.Data.GetPrimary(SCORE, false) ? "Victory!" : "Failure!";
                    Debug.Log($"[{msg}] Took {attempts}/{maxAttempts} attempts!");
                })
                
                .WithRepeat(true)
                .WithMaxDuration(10f)
                .WithErrorLogging(false)
                
                .BuildSequence();
        }
        
        [TaskSequenceMethod("Main Thread UI")]
        public static TaskSequence MainThreadUI()
        {
            return TaskSequenceBuilder.Create("Main Thread UI")
                .Task(async (d, t) =>
                {
                    Debug.Log("Background work...");
                    await UniTask.Delay(1000, cancellationToken: t);
                    d.SetPrimary(Tags.PRIMARY, 42);
                })
                .OnMainThread(d => Debug.Log($"UI Update: {d.GetPrimary<int>(Tags.PRIMARY)}"))
                .Task(async (d, t) =>
                {
                    Debug.Log("More background...");
                    await UniTask.Delay(500, cancellationToken: t);
                })
                .OnMainThread(d => Debug.Log("Final UI update"))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // STAGE REPEAT EXAMPLES
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("Stage Repeat: WithRepeat")]
        public static TaskSequence StageRepeatWithRepeat()
        {
            return TaskSequenceBuilder.Create("Stage Repeat Demo")
                .Task(d =>
                {
                    d.SetPrimary(WAVE_COUNT, 0);
                    Debug.Log("[Init] Starting repeating stage demo");
                })
                .Stage(s => s
                    .WithName("Wave Spawner")
                    .WithRepeat(true)
                    .OnRepeat(ctx => Debug.Log($"[Wave] Iteration {ctx.Stage.RepeatCount + 1} complete"))
                    .Task(async (d, t) =>
                    {
                        int wave = d.Increment(WAVE_COUNT);
                        Debug.Log($"[Wave {wave}] Spawning enemies...");
                        await UniTask.Delay(1000, cancellationToken: t);
                        if (wave >= 5)
                        {
                            Debug.Log("[Wave] Max waves reached, breaking repeat");
                            d.BreakStageRepeat();
                        }
                    }))
                .Task(d => Debug.Log($"[Complete] Spawned {d.GetPrimary<int>(WAVE_COUNT)} waves total"))
                .BuildSequence();
        }
        
        [TaskSequenceMethod("Stage Repeat: RepeatUntilSkipped Policy")]
        public static TaskSequence StageRepeatUntilSkippedPolicy()
        {
            return TaskSequenceBuilder.Create("RepeatUntilSkipped Demo")
                .Task(d =>
                {
                    d.SetPrimary(SCORE, 0);
                    Debug.Log("[Init] Using RepeatUntilSkippedPolicy");
                })
                .Stage(s => s
                    .WithName("Score Loop")
                    .WithRepeatAndDelay(0.5f)
                    .StopRepeatWhen(d => d.GetPrimary<int>(SCORE) >= 500)
                    .Task(async (d, t) =>
                    {
                        int score = d.Increment(SCORE, 100);
                        Debug.Log($"[Score] +100 = {score}");
                        await UniTask.Delay(200, cancellationToken: t);
                    }))
                .Task(d => Debug.Log($"[Complete] Final score: {d.GetPrimary<int>(SCORE)}"))
                .BuildSequence();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // STAGE-LOCAL INJECTION EXAMPLES
        // ═══════════════════════════════════════════════════════════════════════════
        
        [TaskSequenceMethod("Stage-Local Injections")]
        public static TaskSequence StageLocalInjections()
        {
            return TaskSequenceBuilder.Create("Stage-Local Injections Demo")
                .Task(d => Debug.Log("[Init] Demonstrating stage-local injection permissions"))
                .Stage(s => s
                    .WithName("Protected Stage")
                    .AllowInjections(typeof(SkipStageInjection))
                    .InjectWhen(d => d.GetPrimary<int>(SCORE) > 100, InterruptSequenceInjection.Instance)
                    .InjectWhen(d => d.GetPrimary<int>(SCORE) > 50, SkipStageInjection.Instance)
                    .Task(async (d, t) =>
                    {
                        Debug.Log("[Protected] Starting - only SkipStage allowed");
                        for (int i = 0; i < 5; i++)
                        {
                            int score = d.Increment(SCORE, 25);
                            Debug.Log($"[Protected] Score: {score}");
                            await UniTask.Delay(500, cancellationToken: t);
                        }
                        Debug.Log("[Protected] Completed normally");
                    }))
                .Stage(s => s
                    .WithName("Critical Stage")
                    .DisallowInjections()
                    .Task(async (d, t) =>
                    {
                        Debug.Log("[Critical] Cannot be interrupted!");
                        await UniTask.Delay(1000, cancellationToken: t);
                        Debug.Log("[Critical] Done");
                    }))
                .Task(d => Debug.Log("[Complete]"))
                .BuildSequence();
        }
        
        [TaskSequenceMethod("Maintained Repeating Stage")]
        public static TaskSequence MaintainedRepeatingStage()
        {
            return TaskSequenceBuilder.Create("Maintained Repeating Stage")
                .Task(d =>
                {
                    d.SetPrimary(SCORE, 0);
                    Debug.Log("[Init] Background scorer continues while foreground runs");
                })
                .Stage(s => s
                    .WithName("Background Scorer")
                    .WithRepeatAndDelay(0.5f)
                    .Task(async (d, t) =>
                    {
                        int score = d.Increment(SCORE, 10);
                        Debug.Log($"[BG] +10 score = {score}");
                        await UniTask.Delay(100, cancellationToken: t);
                    })
                    .Task(d =>
                    {
                        Debug.Log("[BG] Scorer is maintained");
                        d.SkipAndMaintain();
                    }))
                .Task(async (d, t) =>
                {
                    Debug.Log("[FG] Doing foreground work...");
                    await UniTask.Delay(2000, cancellationToken: t);
                })
                .Task(async (d, t) =>
                {
                    Debug.Log("[FG] More foreground work...");
                    await UniTask.Delay(1500, cancellationToken: t);
                })
                .Task(d =>
                {
                    Debug.Log("[FG] Stopping background scorer...");
                    d.Inject(StopMaintainedAllInjection.Instance);
                })
                .Task(d => Debug.Log($"[Complete] Total background score: {d.GetPrimary<int>(SCORE)}"))
                .BuildSequence();
        }
    }
}