using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// Gameplay mechanics built from the features the other examples show, each a small sequence of its own that logs
    /// what happens and reads its keys through ForgeInput. Rolls go through ForgeRandom, so a game that puts in a
    /// SeededRandom gets the same rolls each run.
    /// </summary>
    public static partial class TaskSequenceExamples
    {
        private static readonly Tag TARGET_HEALTH = Tag.GenerateAsUnique("Examples.Target Health");
        private static readonly Tag STUNNED = Tag.GenerateAsUnique("Examples.Stunned");
        private static readonly Tag CHARGES = Tag.GenerateAsUnique("Examples.Charges");
        private static readonly Tag COMBO = Tag.GenerateAsUnique("Examples.Combo");
        private static readonly Tag DAMAGE = Tag.GenerateAsUnique("Examples.Damage");
        private static readonly Tag DROPPED = Tag.GenerateAsUnique("Examples.Dropped");
        private static readonly Tag FINISHED = Tag.GenerateAsUnique("Examples.Finished");
        private static readonly Tag GOLD = Tag.GenerateAsUnique("Examples.Gold");
        private static readonly Tag BOUGHT_BACK = Tag.GenerateAsUnique("Examples.Bought Back");
        private static readonly Tag BOSS_HEALTH = Tag.GenerateAsUnique("Examples.Boss Health");
        private static readonly Tag BLOCKED = Tag.GenerateAsUnique("Examples.Blocked");
        private static readonly Tag RADIUS = Tag.GenerateAsUnique("Examples.Radius");
        private static readonly Tag DRAW = Tag.GenerateAsUnique("Examples.Draw");
        private static readonly Tag MARKED = Tag.GenerateAsUnique("Examples.Marked");
        private static readonly Tag DASHING = Tag.GenerateAsUnique("Examples.Dashing");
        private static readonly Tag FOE_HEALTH = Tag.GenerateAsUnique("Examples.Foe Health");
        private static readonly Tag GUARDING = Tag.GenerateAsUnique("Examples.Guarding");

        private static readonly KeyCode[] FirstHitKeys = { KeyCode.J, KeyCode.K };
        private static readonly KeyCode[] ComboKeys = { KeyCode.J, KeyCode.K, KeyCode.L };
        private static readonly KeyCode[] DuelKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

        /// <summary>
        /// A channeled beam: a short wind-up that can't be cut short, then a beam that ticks every half second for up to
        /// six seconds, ending early if its target falls. A stun (S) breaks the channel but not the wind-up: the stun is a
        /// condition on the whole sequence, and the wind-up a critical stage, which only an interrupt gets into.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Channeled Beam")]
        public static TaskSequence ChanneledBeam()
        {
            return TaskSequenceBuilder.Create("Channeled Beam")
                .InjectWhen(_ => Pressed(KeyCode.S), new DelegateSequenceInjection(runtime =>
                {
                    runtime.Data.SetPrimary(STUNNED, true);
                    ForgeLog.Info("[Beam] Stunned!");
                    return runtime.Data.SkipStage();
                }))
                .Do(d =>
                {
                    d.SetPrimary(TARGET_HEALTH, 500);
                    d.SetPrimary(STUNNED, false);
                    ForgeLog.Info("[Beam] Winding up. S stuns you.");
                })
                .Stage(s => s
                    .WithName("Wind-up")
                    .WithCriticalFlag(true)
                    .Delay(0.6f))
                .Stage(s => s
                    .WithName("Channel")
                    .WithRepeat(true)
                    .WithMaxDuration(6f)
                    .OnMaxDuration(ctx => ForgeLog.Info($"[Beam] The beam ran its full length, {ctx.Runtime.GetRepeatCount(ctx.Stage)} ticks."))
                    .StopRepeatWhen(d => d.GetPrimary<int>(TARGET_HEALTH) <= 0)
                    // Hits first and waits after, so the target's fall is heard while its round still runs
                    .Task(async (d, t) =>
                    {
                        int damage = ForgeRandom.Range(40, 61);
                        ForgeLog.Info($"[Beam] A tick for {damage}: the target is at {Mathf.Max(0, d.Decrement(TARGET_HEALTH, damage))}.");
                        await SequenceTaskLibrary.Delay(0.5f, t);
                    }))
                .Do(d =>
                {
                    if (d.GetPrimary<int>(TARGET_HEALTH) <= 0) ForgeLog.Info("[Beam] The target falls.");
                    else if (d.GetPrimary<bool>(STUNNED)) ForgeLog.Info("[Beam] The channel broke.");
                    else ForgeLog.Info("[Beam] The beam fades.");
                })
                .BuildSequence();
        }

        /// <summary>
        /// An ability with charges: three at most, each cast (Q) spending one, and one coming back every two seconds. The
        /// recharge is a stage kept running behind the casts, maintained, for as long as the run lasts. Escape ends it.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Charges")]
        public static TaskSequence Charges()
        {
            const int most = 3;
            return TaskSequenceBuilder.Create("Charges")
                .InterruptWhen(_ => Pressed(KeyCode.Escape))
                .Do(d =>
                {
                    d.SetPrimary(CHARGES, most);
                    ForgeLog.Info($"[Charges] {most} charges. Q casts, Escape stops.");
                })
                .Stage(s => s
                    .WithName("Recharge")
                    .Task(async (d, t) =>
                    {
                        // Moves the run on to the casts, and stays running behind them
                        d.SkipAndMaintain();
                        while (true)
                        {
                            await SequenceTaskLibrary.WaitUntil(() => d.GetPrimary<int>(CHARGES) < most, t);
                            await SequenceTaskLibrary.Delay(2f, t);
                            ForgeLog.Info($"[Charges] A charge comes back: {d.Increment(CHARGES)} of {most}.");
                        }
                    }))
                .Stage(s => s
                    .WithName("Casts")
                    .WithRepeat(true)
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.WaitForKey(KeyCode.Q, t);
                        if (d.GetPrimary<int>(CHARGES) <= 0) ForgeLog.Info("[Charges] No charges left.");
                        else ForgeLog.Info($"[Charges] Cast! {d.Decrement(CHARGES)} left.");
                    }))
                .BuildSequence();
        }

        /// <summary>
        /// A recall: four seconds of channel, then home. Taking a hit (D) or moving (an arrow key) cuts it short at any
        /// point, by a condition on the sequence; nothing else gets in by hand, as it allows only interrupts. OnTerminate
        /// says how it went, as it's called however the run ends.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Recall")]
        public static TaskSequence Recall()
        {
            return TaskSequenceBuilder.Create("Recall")
                .AllowOnlyInterrupt()
                .InterruptWhen(_ => Pressed(KeyCode.D) || Moved())
                .Do(_ => ForgeLog.Info("[Recall] Recalling. D takes a hit, an arrow key moves."))
                .Stage(s => s
                    .WithName("Channel")
                    .Task(async (d, t) =>
                    {
                        for (int quarter = 1; quarter <= 4; quarter++)
                        {
                            await SequenceTaskLibrary.Delay(1f, t);
                            ForgeLog.Info($"[Recall] {quarter * 25}%");
                        }
                    }))
                .OnTerminate((ctx, completed) => ForgeLog.Info(completed ? "[Recall] Home." : "[Recall] Cut short."))
                .BuildSequence();
        }

        private static bool Moved() =>
            Pressed(KeyCode.UpArrow) || Pressed(KeyCode.DownArrow) || Pressed(KeyCode.LeftArrow) || Pressed(KeyCode.RightArrow);

        /// <summary>
        /// A combo: J lands a light hit, K a heavy one, and L a finisher that ends the combo, worth more for each hit before
        /// it. Each hit after the first must land within a second of the last, or the combo drops. The window is a
        /// sub-stage with a timeout, inside a repeating stage: a sub-stage starts again each round, its timeout with it,
        /// and OnRepeat ends the combo between rounds.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Combo Chain")]
        public static TaskSequence ComboChain()
        {
            return TaskSequenceBuilder.Create("Combo Chain")
                .Do(d =>
                {
                    d.SetPrimary(COMBO, 0);
                    d.SetPrimary(DAMAGE, 0);
                    d.SetPrimary(DROPPED, false);
                    d.SetPrimary(FINISHED, false);
                    ForgeLog.Info("[Combo] J light, K heavy, L finisher. Start with J or K.");
                })
                .Task(async (d, t) => Hit(d, await SequenceTaskLibrary.WaitForAnyKey(FirstHitKeys, t)))
                .Stage(s => s
                    .WithName("Combo")
                    .WithRepeat(true)
                    .OnRepeat(ctx =>
                    {
                        if (ctx.Data.GetPrimary<bool>(DROPPED) || ctx.Data.GetPrimary<bool>(FINISHED)) ctx.Data.BreakStageRepeat(ctx.Stage);
                    })
                    .SubStage(window => window
                        .WithName("Window")
                        .WithTimeout(1f)
                        .OnTimeout(ctx =>
                        {
                            ctx.Data.SetPrimary(DROPPED, true);
                            ForgeLog.Info("[Combo] Too slow: the combo drops.");
                        })
                        .Task(async (d, t) => Hit(d, await SequenceTaskLibrary.WaitForAnyKey(ComboKeys, t)))))
                .Do(d => ForgeLog.Info($"[Combo] {d.GetPrimary<int>(COMBO)} hits for {d.GetPrimary<int>(DAMAGE)}."))
                .BuildSequence();
        }

        // One hit of the combo
        private static void Hit(SequenceDataPacket d, KeyCode key)
        {
            int hits = d.Increment(COMBO);
            int damage = key switch
            {
                KeyCode.K => 20,
                KeyCode.L => 15 * hits,
                _ => 10
            };
            d.Increment(DAMAGE, damage);
            if (key == KeyCode.L) d.SetPrimary(FINISHED, true);
            ForgeLog.Info($"[Combo] {(key == KeyCode.L ? "Finisher" : "Hit")} {hits}, for {damage}.");
        }

        /// <summary>
        /// Death, then a respawn timer: ten seconds, or buy back at once (B) for 300 gold, if you have it. The stage races
        /// the timer against the buy-back, WhenAny, and a branch reads which won.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Respawn Timer")]
        public static TaskSequence RespawnTimer()
        {
            const int price = 300;
            return TaskSequenceBuilder.Create("Respawn Timer")
                .Do(d =>
                {
                    d.SetPrimary(GOLD, ForgeRandom.Range(150, 600));
                    d.SetPrimary(BOUGHT_BACK, false);
                    ForgeLog.Info($"[Respawn] You died, with {d.GetPrimary<int>(GOLD)} gold. B buys you back for {price}.");
                })
                .Stage(s => s
                    .WithName("Dead")
                    .WhenAny()
                    .Task(async (d, t) =>
                    {
                        for (int left = 10; left > 0; left--)
                        {
                            if (left <= 3 || left % 5 == 0) ForgeLog.Info($"[Respawn] {left}...");
                            await SequenceTaskLibrary.Delay(1f, t);
                        }
                    })
                    .Task(async (d, t) =>
                    {
                        while (true)
                        {
                            await SequenceTaskLibrary.WaitForKey(KeyCode.B, t);
                            if (d.GetPrimary<int>(GOLD) >= price) break;
                            ForgeLog.Info("[Respawn] Not enough gold.");
                        }
                        d.Decrement(GOLD, price);
                        d.SetPrimary(BOUGHT_BACK, true);
                    }))
                .Branch(b => b
                    .If(d => d.GetPrimary<bool>(BOUGHT_BACK), bought => bought
                        .Do(d => ForgeLog.Info($"[Respawn] Bought back, with {d.GetPrimary<int>(GOLD)} gold left.")))
                    .Default(waited => waited
                        .Do(_ => ForgeLog.Info("[Respawn] Respawned."))))
                .BuildSequence();
        }

        /// <summary>
        /// A boss fight in phases, Space your attack. Phase one lasts until the boss is at half health, phase two until it
        /// falls, and then straight to victory. Twenty seconds in, if it still stands, it enrages, whatever phase it's in,
        /// and wipes the party ten seconds later unless it falls first. The enrage is a condition that reads the runtime
        /// and jumps by index; phase two's end jumps by name.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Boss Enrage")]
        public static TaskSequence BossEnrage()
        {
            const int enraged = 3;
            return TaskSequenceBuilder.Create("Boss Enrage")
                .InjectWhen((d, runtime) => runtime.ElapsedTime >= 20f && runtime.StageIndex < enraged, new JumpToStageInjection(enraged), fireOnce: true)
                // 0
                .Do(d =>
                {
                    d.SetPrimary(BOSS_HEALTH, 1000);
                    ForgeLog.Info("[Boss] The fight begins. Space attacks.");
                })
                // 1
                .Stage(s => s
                    .WithName("Phase one")
                    .SkipWhen(d => d.GetPrimary<int>(BOSS_HEALTH) <= 500)
                    .Task((d, t) => Brawl(d, 50, 80, t)))
                // 2
                .Stage(s => s
                    .WithName("Phase two")
                    .OnInit(_ => ForgeLog.Info("[Boss] Phase two: its hide hardens."))
                    .InjectWhen(d => d.GetPrimary<int>(BOSS_HEALTH) <= 0,
                        new DelegateSequenceInjection(runtime => runtime.Data.JumpToStage("Victory")), fireOnce: true)
                    .Task((d, t) => Brawl(d, 30, 60, t)))
                // 3
                .Stage(s => s
                    .WithName("Enraged")
                    .OnInit(_ => ForgeLog.Warning("[Boss] It enrages! Ten seconds to finish it."))
                    .WithMaxDuration(10f, InterruptSequenceInjection.Instance)
                    .OnMaxDuration(ctx => ForgeLog.Warning($"[Boss] The party is wiped, {ctx.ElapsedTime:F0}s in."))
                    .SkipWhen(d => d.GetPrimary<int>(BOSS_HEALTH) <= 0)
                    .Task((d, t) => Brawl(d, 40, 70, t)))
                // 4
                .Stage(s => s
                    .WithName("Victory")
                    .Do(d => ForgeLog.Info($"[Boss] Victory, {d.ElapsedTime:F1}s in.")))
                .BuildSequence();
        }

        // Your side of the fight: each Space is a blow. It never ends by itself; the fight's conditions end its stage
        private static async UniTask Brawl(SequenceDataPacket d, int least, int most, CancellationToken token)
        {
            while (true)
            {
                await SequenceTaskLibrary.WaitForKey(KeyCode.Space, token);
                int damage = ForgeRandom.Range(least, most + 1);
                ForgeLog.Info($"[Boss] You hit for {damage}: it's at {Mathf.Max(0, d.Decrement(BOSS_HEALTH, damage))}.");
            }
        }

        /// <summary>
        /// A spell shield: for a second and a half, the next spell to hit you (Space) is blocked, and the block gives back
        /// mana. The window races the shield's time against a hit, WhenAny. The block's answer is a critical branch case, so
        /// nothing cuts it short, and the sequence hears its critical section end as the next stage starts, whichever case
        /// ran.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Spell Shield")]
        public static TaskSequence SpellShield()
        {
            var sequence = TaskSequenceBuilder.Create("Spell Shield")
                .Do(d =>
                {
                    d.SetPrimary(MANA, 100);
                    d.SetPrimary(BLOCKED, false);
                    ForgeLog.Info("[Shield] Shielded for 1.5s. Space is a spell hitting you.");
                })
                .Stage(s => s
                    .WithName("Shield up")
                    .WhenAny()
                    .Delay(1.5f)
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.WaitForKey(KeyCode.Space, t);
                        d.SetPrimary(BLOCKED, true);
                    }))
                .Branch(b => b
                    .If(d => d.GetPrimary<bool>(BLOCKED), blocked => blocked
                        .WithName("Blocked")
                        .WithCriticalFlag(true)
                        .Task(async (d, t) =>
                        {
                            ForgeLog.Info("[Shield] Blocked!");
                            await SequenceTaskLibrary.Delay(0.3f, t);
                            ForgeLog.Info($"[Shield] The block gives back 40 mana: {d.Increment(MANA, 40)}.");
                        }))
                    .Default(faded => faded
                        .Do(_ => ForgeLog.Info("[Shield] The shield fades unused."))))
                .Do(_ => ForgeLog.Info("[Shield] Its cooldown starts."))
                .BuildSequence();

            sequence.OnCriticalSectionExited = () => ForgeLog.Info("[Shield] Past the block's answer.");
            return sequence;
        }

        /// <summary>
        /// A toggled aura: while it's on, it drains 10 mana a second for 5 health, until you turn it off (T) or the mana runs
        /// out. The drain is a stage that repeats with a second between rounds, ended by either of two conditions.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Toggled Drain")]
        public static TaskSequence ToggledDrain()
        {
            return TaskSequenceBuilder.Create("Toggled Drain")
                .Do(d =>
                {
                    d.SetPrimary(MANA, 60);
                    d.SetPrimary(HEALTH, 50);
                    ForgeLog.Info("[Aura] On: 10 mana a second for 5 health. T turns it off.");
                })
                .Stage(s => s
                    .WithName("Aura")
                    .WithRepeatAndDelay(1f)
                    .SkipWhen(_ => Pressed(KeyCode.T))
                    .SkipWhen(d => d.GetPrimary<int>(MANA) <= 0)
                    .Do(d => ForgeLog.Info($"[Aura] Mana {d.Decrement(MANA, 10)}, health {d.Increment(HEALTH, 5)}.")))
                .Do(d => ForgeLog.Info(d.GetPrimary<int>(MANA) <= 0 ? "[Aura] Out of mana: off." : "[Aura] Turned off."))
                .BuildSequence();
        }

        /// <summary>
        /// A ground-targeted spell: aim with the pointer, scroll to size the area, click to cast, right-click or Escape to
        /// cancel. The ray is the game's input's, through its camera, so a gamepad's reticle stands in where a game puts
        /// one in. Aiming is one step task, called each frame until a click or a cancel settles it.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Ground Target")]
        public static TaskSequence GroundTarget()
        {
            return TaskSequenceBuilder.Create("Ground Target")
                .Do(d =>
                {
                    d.SetPrimary(RADIUS, 3f);
                    d.SetPrimary(CANCELLED, false);
                    ForgeLog.Info("[Aim] Click a collider to cast, scroll to size the area, right-click or Escape to cancel.");
                })
                .SyncTask((d, deltaTime) =>
                {
                    var input = ForgeInput.Current;
                    if (input.KeyPressed(KeyCode.Escape) || input.PointerPressed(1))
                    {
                        d.SetPrimary(CANCELLED, true);
                        return true;
                    }

                    if (input.ScrollDelta != 0f)
                    {
                        float radius = Mathf.Clamp(d.GetPrimary<float>(RADIUS) + input.ScrollDelta * 0.5f, 1f, 8f);
                        d.SetPrimary(RADIUS, radius);
                        ForgeLog.Info($"[Aim] Radius {radius:F1}.");
                    }

                    if (!input.PointerPressed(0)) return false;
                    if (input.TryGetPointerRay(out var ray) && Physics.Raycast(ray, out var hit, 1000f))
                    {
                        d.SetPrimary(Tags.TARGET_POS, hit.point);
                        return true;
                    }

                    ForgeLog.Info("[Aim] Nothing there to aim at.");
                    return false;
                })
                .Branch(b => b
                    .If(d => d.GetPrimary<bool>(CANCELLED), cancelled => cancelled
                        .Do(_ => ForgeLog.Info("[Aim] Cancelled.")))
                    .Default(cast => cast
                        .Do(d => ForgeLog.Info($"[Aim] Cast at {d.GetPrimary<Vector3>(Tags.TARGET_POS)}, radius {d.GetPrimary<float>(RADIUS):F1}."))))
                .BuildSequence();
        }

        /// <summary>
        /// A charged shot: hold Space or the left button to draw, and let go to loose it. Its damage grows with the draw, up
        /// to two seconds; drawn that long, it looses itself. The draw stage races the draw against the release, WhenAny.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Charged Shot")]
        public static TaskSequence ChargedShot()
        {
            const float full = 2f;
            return TaskSequenceBuilder.Create("Charged Shot")
                .Do(_ => ForgeLog.Info("[Shot] Hold Space or the left button to draw; let go to loose."))
                .DelayUntil(() => Drawing())
                .Stage(s => s
                    .WithName("Draw")
                    .WhenAny()
                    .OnInit(d => d.SetPrimary(DRAW, 0f))
                    .SyncTask((d, deltaTime) => d.IncrementFloat(DRAW, deltaTime) >= full)
                    .DelayWhile(() => Drawing()))
                .Do(d =>
                {
                    float draw = Mathf.Min(d.GetPrimary<float>(DRAW), full);
                    int damage = Mathf.RoundToInt(Mathf.Lerp(20f, 100f, draw / full));
                    ForgeLog.Info(draw >= full
                        ? $"[Shot] Fully drawn, it looses itself, for {damage}."
                        : $"[Shot] Loosed after {draw:F2}s, for {damage}.");
                })
                .BuildSequence();
        }

        private static bool Drawing() => ForgeInput.Current.KeyHeld(KeyCode.Space) || ForgeInput.Current.PointerHeld(0);

        /// <summary>
        /// A two-part ability: the first cast (Q) throws a mark, and for three seconds after it lands, casting again (Q)
        /// dashes to the mark for more damage. Each part is a branch case: the window, with a timeout, if the mark landed,
        /// and the dash, a critical stage, if the recast came.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Recast")]
        public static TaskSequence Recast()
        {
            return TaskSequenceBuilder.Create("Recast")
                .Do(d =>
                {
                    d.SetPrimary(MARKED, false);
                    d.SetPrimary(DASHING, false);
                    ForgeLog.Info("[Recast] Q throws the mark.");
                })
                .Task(async (d, t) =>
                {
                    await SequenceTaskLibrary.WaitForKey(KeyCode.Q, t);
                    bool landed = ForgeRandom.Value < 0.7f;
                    d.SetPrimary(MARKED, landed);
                    ForgeLog.Info(landed ? "[Recast] The mark lands. Q again within three seconds to dash." : "[Recast] The mark misses.");
                })
                .Branch(b => b
                    .If(d => d.GetPrimary<bool>(MARKED), window => window
                        .WithName("Recast window")
                        .WithTimeout(3f)
                        .OnTimeout(() => ForgeLog.Info("[Recast] The window closes."))
                        .Task(async (d, t) =>
                        {
                            await SequenceTaskLibrary.WaitForKey(KeyCode.Q, t);
                            d.SetPrimary(DASHING, true);
                        })))
                .Branch(b => b
                    .If(d => d.GetPrimary<bool>(DASHING), dash => dash
                        .WithName("Dash")
                        .WithCriticalFlag(true)
                        .Task(async (d, t) =>
                        {
                            ForgeLog.Info("[Recast] Dashing to the mark...");
                            await SequenceTaskLibrary.Delay(0.25f, t);
                            ForgeLog.Info($"[Recast] The dash hits for {ForgeRandom.Range(60, 91)}.");
                        })))
                .BuildSequence();
        }

        /// <summary>
        /// Meteors falling over an area: each a cube of its own, run by a sequence of its own that arcs it down as it spins,
        /// and destroys it as the run ends, however that is. The shower's stage runs once a meteor. One meteor sequence
        /// serves them all: each run has its own runtime and packet, and the tasks keep nothing of their own. Each run is
        /// registered with ProcessControl, so it goes on after the stage that started it.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Meteor Shower")]
        public static TaskSequence MeteorShower()
        {
            var meteor = Meteor();
            return TaskSequenceBuilder.Create("Meteor Shower")
                .Do(_ => ForgeLog.Info("[Meteors] The sky falls."))
                .Stage(s => s
                    .WithName("Shower")
                    .WithRepeat(12)
                    .Task(async (d, t) =>
                    {
                        Vector2 spot = ForgeRandom.InsideUnitCircle * 6f;
                        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.name = "Meteor";
                        cube.transform.position = new Vector3(spot.x, 12f, spot.y) + ForgeRandom.InsideUnitSphere * 3f;

                        var data = SequenceDataPacket.Default();
                        data.SetPrimary(Tags.GAMEOBJECT, cube);
                        data.SetPrimary(Tags.TARGET_POS, new Vector3(spot.x, 0.5f, spot.y));
                        if (meteor.RegisterAndRun(data) == null) UnityEngine.Object.Destroy(cube);

                        await SequenceTaskLibrary.Delay(ForgeRandom.Range(0.15f, 0.45f), t);
                    }))
                .Do(_ => ForgeLog.Info("[Meteors] The shower ends."))
                .BuildSequence();
        }

        private static TaskSequence Meteor()
        {
            return TaskSequenceBuilder.Create("Meteor")
                // Nothing steers a meteor once it falls. Its process's end still ends it, by its token
                .DisallowInjections()
                .Stage(s => s
                    .WithName("Fall")
                    .Task(async (d, t) => await SequenceTaskLibrary.ArcTo(CubeOf(d), d.GetPrimary<Vector3>(Tags.TARGET_POS), 1.2f, 3f, t))
                    .Task(async (d, t) => await SequenceTaskLibrary.RotateBy(CubeOf(d), new Vector3(360f, 180f, 0f), 1.2f, t)))
                .Task(async (d, t) => await SequenceTaskLibrary.PunchScale(CubeOf(d), 0.6f, 0.3f, t))
                .Delay(0.5f)
                .OnTerminate((ctx, completed) =>
                {
                    var cube = ctx.Data.GetPrimary<GameObject>(Tags.GAMEOBJECT);
                    if (cube) UnityEngine.Object.Destroy(cube);
                })
                .BuildSequence();
        }

        private static Transform CubeOf(SequenceDataPacket data) => data.GetPrimary<GameObject>(Tags.GAMEOBJECT).transform;

        /// <summary>
        /// A turn-based duel, a turn each time round a repeating sequence. Stages run in turn, so each half of a turn is a
        /// stage of its own, and the sequence goes round until it interrupts itself as a side falls. It's the middle of a
        /// chain: an interrupted sequence ends, the chain goes on to the next, and that one says who won.
        /// </summary>
        [TaskSequenceMethod("Mechanic: Turn-Based Duel")]
        public static TaskSequenceChain TurnBasedDuel()
        {
            var setup = TaskSequenceBuilder.Create("Duel: Setup")
                .Do(d =>
                {
                    d.SetPrimary(HEALTH, 40);
                    d.SetPrimary(FOE_HEALTH, 50);
                    d.SetPrimary(GUARDING, false);
                    ForgeLog.Info("[Duel] Each turn, 1 strikes, 2 guards and 3 heals.");
                })
                .BuildSequence();

            var turns = TaskSequenceBuilder.Create("Duel: Turns")
                .WithRepeat(true)
                .Stage(s => s
                    .WithName("Your turn")
                    .Task(async (d, t) =>
                    {
                        ForgeLog.Info($"[Duel] You {d.GetPrimary<int>(HEALTH)}, the foe {d.GetPrimary<int>(FOE_HEALTH)}. Your move?");
                        switch (await SequenceTaskLibrary.WaitForAnyKey(DuelKeys, t))
                        {
                            case KeyCode.Alpha1:
                                int damage = ForgeRandom.Range(8, 15);
                                ForgeLog.Info($"[Duel] You strike for {damage}.");
                                d.Decrement(FOE_HEALTH, damage);
                                break;
                            case KeyCode.Alpha2:
                                ForgeLog.Info("[Duel] You guard.");
                                d.SetPrimary(GUARDING, true);
                                break;
                            default:
                                ForgeLog.Info($"[Duel] You heal, to {d.Increment(HEALTH, 10)}.");
                                break;
                        }
                    }))
                .Stage(s => s
                    .WithName("The foe's turn")
                    .Task(async (d, t) =>
                    {
                        if (d.GetPrimary<int>(FOE_HEALTH) <= 0) return;
                        await SequenceTaskLibrary.Delay(0.6f, t);
                        int damage = ForgeRandom.Range(5, 12);
                        if (d.GetPrimary<bool>(GUARDING)) damage /= 2;
                        d.SetPrimary(GUARDING, false);
                        ForgeLog.Info($"[Duel] The foe hits you for {damage}.");
                        d.Decrement(HEALTH, damage);
                    }))
                .Do(d =>
                {
                    if (d.GetPrimary<int>(HEALTH) <= 0 || d.GetPrimary<int>(FOE_HEALTH) <= 0) d.Interrupt();
                })
                .BuildSequence();

            var result = TaskSequenceBuilder.Create("Duel: Result")
                .Branch(b => b
                    .If(d => d.GetPrimary<int>(FOE_HEALTH) <= 0, won => won.Do(_ => ForgeLog.Info("[Duel] You win.")))
                    .Default(lost => lost.Do(_ => ForgeLog.Info("[Duel] You fall."))))
                .BuildSequence();

            return setup.Then(turns).Then(result);
        }
    }
}
