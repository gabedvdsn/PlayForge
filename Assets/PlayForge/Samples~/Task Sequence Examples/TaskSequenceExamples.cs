using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// The task sequence framework, a feature at a time. Each method marked [TaskSequenceMethod] builds a sequence or a
    /// chain and returns it unrun: an ability's Sequence task lists it in its dropdown, and the Task Sequence Example
    /// Runner runs it from a scene. Each says what it does through ForgeLog, and reads the keys it waits for through
    /// ForgeInput.
    /// <para>
    /// A sequence is a list of stages, run one after another. A stage is a set of tasks, run together, which ends as its
    /// policy says: by default, once they all have. What a run knows travels in its packet, a SequenceDataPacket, which
    /// every task is handed with a cancellation token. The token is how a skip, an interrupt or a timeout stops a task,
    /// so a task passes it to everything it waits on.
    /// </para>
    /// </summary>
    public static partial class TaskSequenceExamples
    {
        // What the examples keep in a run's packet, each under a tag. A tag is made once and kept: two made with one name
        // are the same tag
        private static readonly Tag COUNT = Tag.GenerateAsUnique("Examples.Count");
        private static readonly Tag TIMER = Tag.GenerateAsUnique("Examples.Timer");
        private static readonly Tag FRAMES = Tag.GenerateAsUnique("Examples.Frames");
        private static readonly Tag LATE_TIME = Tag.GenerateAsUnique("Examples.Late Time");
        private static readonly Tag READY = Tag.GenerateAsUnique("Examples.Ready");
        private static readonly Tag CANCELLED = Tag.GenerateAsUnique("Examples.Cancelled");
        private static readonly Tag PROGRESS = Tag.GenerateAsUnique("Examples.Progress");
        private static readonly Tag ROLL = Tag.GenerateAsUnique("Examples.Roll");
        private static readonly Tag POINTS = Tag.GenerateAsUnique("Examples.Points");
        private static readonly Tag KNOCKS = Tag.GenerateAsUnique("Examples.Knocks");
        private static readonly Tag HEADS = Tag.GenerateAsUnique("Examples.Heads");
        private static readonly Tag RUNS = Tag.GenerateAsUnique("Examples.Runs");
        private static readonly Tag HEAT = Tag.GenerateAsUnique("Examples.Heat");
        private static readonly Tag HEALTH = Tag.GenerateAsUnique("Examples.Health");
        private static readonly Tag MANA = Tag.GenerateAsUnique("Examples.Mana");
        private static readonly Tag SCORE = Tag.GenerateAsUnique("Examples.Score");
        private static readonly Tag ATTEMPTS = Tag.GenerateAsUnique("Examples.Attempts");
        private static readonly Tag DONE = Tag.GenerateAsUnique("Examples.Done");
        private static readonly Tag NAME = Tag.GenerateAsUnique("Examples.Name");
        private static readonly Tag INVENTORY = Tag.GenerateAsUnique("Examples.Inventory");
        private static readonly Tag SPEED = Tag.GenerateAsUnique("Examples.Speed");
        private static readonly Tag LEVEL = Tag.GenerateAsUnique("Examples.Level");
        private static readonly Tag LAPS = Tag.GenerateAsUnique("Examples.Laps");
        private static readonly Tag CHAIN = Tag.GenerateAsUnique("Examples.Chain");

        private static readonly KeyCode[] NumberKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };
        private static readonly KeyCode[] MenuKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Escape };
        private static readonly KeyCode[] PlayKeys = { KeyCode.Space, KeyCode.Tab };

        // Whether a key went down this frame, read through the game's input. A press reads as down for the whole frame it
        // happened in; the library's waits for a key look from the next frame on, so one doesn't take a press another has
        private static bool Pressed(KeyCode key) => ForgeInput.Current.KeyPressed(key);

        // ═══════════════════════════════════════════════════════════════════════════
        // BASICS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The smallest sequence worth running. Each call on the builder adds a stage of one task, and the stages run in
        /// turn: Do runs an action at once, Delay waits, and Task runs async code of your own. BuildSequence builds it, and
        /// nothing runs until something runs it.
        /// </summary>
        [TaskSequenceMethod("Basics: First Sequence")]
        public static TaskSequence FirstSequence()
        {
            return TaskSequenceBuilder.Create("First Sequence")
                .Do(_ => ForgeLog.Info("[First] Stage one runs at once."))
                .Delay(1f)
                .Task(async (data, token) =>
                {
                    ForgeLog.Info("[First] Stage three, a second in, waits half a second of its own.");
                    // The library's waits count the sequence's time, and stop with their stage
                    await SequenceTaskLibrary.Delay(0.5f, token);
                })
                .Do(data => ForgeLog.Info($"[First] Done, {data.ElapsedTime:F1}s after it started."))
                .BuildSequence();
        }

        /// <summary>
        /// A stage's tasks run together, and the sequence goes on when the stage ends: by default, when all its tasks have.
        /// So the first stage here lasts as long as its slowest task. A stage can be built by hand as a SequenceStage too,
        /// its tasks a list and its policy a property, and handed to Stage as it is.
        /// </summary>
        [TaskSequenceMethod("Basics: Stages")]
        public static TaskSequence Stages()
        {
            var handMade = new SequenceStage { Policy = WhenAnyStagePolicy.Instance };
            handMade.Tasks.Add(new DelegateSequenceTask(async (data, token) =>
            {
                await SequenceTaskLibrary.Delay(0.25f, token);
                ForgeLog.Info("[Stages] The hand-made stage's quick task ends it, as its policy is WhenAny.");
            }));
            handMade.Tasks.Add(new DelegateSequenceTask(async (data, token) => await SequenceTaskLibrary.Delay(5f, token)));

            return TaskSequenceBuilder.Create("Stages")
                .Stage(s => s
                    .WithName("Three at once")
                    .Task(async (d, t) => { await SequenceTaskLibrary.Delay(0.5f, t); ForgeLog.Info("[Stages] A ends at 0.5s."); })
                    .Task(async (d, t) => { await SequenceTaskLibrary.Delay(1.5f, t); ForgeLog.Info("[Stages] B ends at 1.5s."); })
                    .Task(async (d, t) => { await SequenceTaskLibrary.Delay(1f, t); ForgeLog.Info("[Stages] C ends at 1s."); }))
                .Do(d => ForgeLog.Info($"[Stages] The next stage starts as the slowest ends, {d.ElapsedTime:F1}s in."))
                .Stage(handMade)
                .Do(_ => ForgeLog.Info("[Stages] Done."))
                .BuildSequence();
        }

        /// <summary>
        /// What a sequence says about itself. A name and a description, on the sequence and on each stage, show in logs, in
        /// a failed run's path and in the debugger. Annotations say what the sequence does, for tools that can't read its
        /// code: WithAppliedEffects tells the ability inspector that it applies the effects a loader put under a tag, and
        /// how many times. Create(name) is Create().WithName(name).
        /// </summary>
        [TaskSequenceMethod("Basics: Names and Annotations")]
        public static TaskSequence NamesAndAnnotations()
        {
            return TaskSequenceBuilder.Create()
                .WithName("Names and Annotations")
                .WithDescription("Reads its own name, description and annotations back.")
                .WithAnnotation("Logs what it says about itself")
                .WithAppliedEffects(Tags.EFFECTS, 3, "to up to three targets")
                .Stage(s => s
                    .WithName("Introduce")
                    .WithDescription("Reads the run's metadata through the packet")
                    .Do(d =>
                    {
                        var metadata = d.Definition.Metadata;
                        ForgeLog.Info($"[Names] '{d.SequenceName}': {metadata.Description}");
                        ForgeLog.Info($"[Names] Stage {d.StageIndex}, '{d.StageName}': {d.CurrentStage.Metadata.Description}");
                        foreach (var annotation in metadata.Annotations)
                        {
                            ForgeLog.Info(annotation.AppliesLoadedEffects
                                ? $"[Names] Applies the effects under {annotation.AppliedEffectsTag}, {annotation.MaxApplications} times at most, {annotation.Text}."
                                : $"[Names] {annotation.Text}.");
                        }
                    }))
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // TASKS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Every way to hand the builder a task. At the sequence's level each is a stage of its own, so they run in turn;
        /// the last stage takes them together. An async delegate takes the packet and the token, or the token alone; an
        /// action given to Task runs as its stage starts. Do runs an action too, and runs it in a synced sequence as well,
        /// where an action given to Task does nothing. OnMainThread runs an action on Unity's thread, for code that touches
        /// Unity's objects. SyncTask takes a step, called each frame with the frame's time until it returns true, and
        /// WaitUntil a condition, asked as its stage starts and then each frame. A task can be an object too: your own, the
        /// library's, or one made of delegates, with Prepare and Clean around each run of it.
        /// </summary>
        [TaskSequenceMethod("Tasks: Every Kind of Task")]
        public static TaskSequence EveryKindOfTask()
        {
            // A closure's state belongs to the sequence, not to a run, so every run of it shares it. Reset it as the run
            // reaches it, as the Do before the step does, or keep it in the packet
            float turned = 0f;

            return TaskSequenceBuilder.Create("Every Kind of Task")
                .Task(async (data, token) =>
                {
                    await SequenceTaskLibrary.Delay(0.25f, token);
                    ForgeLog.Info("[Kinds] Async, with the packet and the token.");
                })
                .Task(async (CancellationToken token) =>
                {
                    await SequenceTaskLibrary.WaitFrames(3, token);
                    ForgeLog.Info("[Kinds] Async, with the token alone, three frames later.");
                })
                .Task(data => ForgeLog.Info("[Kinds] An action given to Task."))
                .OnMainThread(data => ForgeLog.Info($"[Kinds] An action on the main thread, frame {ForgeTime.FrameCount}."))
                .Do(data => ForgeLog.Info($"[Kinds] An action given to Do, count {data.Increment(COUNT)}."))
                .SyncTask((data, deltaTime) => data.IncrementFloat(TIMER, deltaTime) >= 0.5f)
                .Do(_ => turned = 0f)
                .SyncTask(deltaTime => (turned += 360f * deltaTime) >= 180f)
                .Do(_ => ForgeLog.Info("[Kinds] Two steps done. Press Space."))
                .WaitUntil(() => Pressed(KeyCode.Space))
                .WaitUntil(data => data.ElapsedTime >= 2f)
                .Task(new CountdownTask(3))
                .Task(new TallyTask(0.5f))
                .SyncTask(SyncSequenceTaskLibrary.Delay(0.25f))
                .Task(new DelegateSequenceTask(
                    async (data, token) => await SequenceTaskLibrary.Delay(0.25f, token),
                    prepare: data => ForgeLog.Info("[Kinds] A delegate task, prepared."),
                    clean: data => ForgeLog.Info("[Kinds] A delegate task, cleaned.")))
                .Task(new MainThreadSequenceTask(
                    data => ForgeLog.Info("[Kinds] A main-thread task runs."),
                    prepare: data => ForgeLog.Info("[Kinds] A main-thread task, prepared."),
                    clean: data => ForgeLog.Info("[Kinds] A main-thread task, cleaned.")))
                .Task(new DelegateSyncTask(
                    step: (data, deltaTime) => data.Increment(FRAMES) >= 10,
                    prepare: data => data.SetPrimary(FRAMES, 0),
                    clean: data => ForgeLog.Info("[Kinds] A delegate step ran ten frames.")))
                // A stage takes the same tasks, and runs them together: this one ends with its slowest, the countdown
                .Stage(s => s
                    .WithName("All together")
                    .OnInit(d => d.SetPrimary(TIMER, 0f))
                    .Task(async (CancellationToken token) => await SequenceTaskLibrary.Delay(0.5f, token))
                    .Task(data => ForgeLog.Info("[Kinds] Together: an action."))
                    .OnMainThread(data => ForgeLog.Info("[Kinds] Together: on the main thread."))
                    .Do(data => ForgeLog.Info("[Kinds] Together: Do."))
                    .SyncTask((data, deltaTime) => data.IncrementFloat(TIMER, deltaTime) >= 1f)
                    .SyncTask(deltaTime => deltaTime > 0f)
                    .WaitUntil(data => data.GetPrimary<float>(TIMER) >= 1f)
                    .WaitUntil(() => !ForgeInput.Current.KeyHeld(KeyCode.Space))
                    .Task(new CountdownTask(2)))
                .Do(_ => ForgeLog.Info("[Kinds] Done."))
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // WAITS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Waiting, between stages and inside one. Delay counts the sequence's own time, which stops while it's paused.
        /// DelayUntil and DelayWhile ask a condition once a frame, at a player loop timing of your choosing. WaitUntil is a
        /// step task: it asks as its stage starts and then each frame, and it runs in a synced sequence too, as SyncDelay
        /// does. Inside a stage, each wait is one of its tasks, and the stage ends as its policy says.
        /// </summary>
        [TaskSequenceMethod("Waits: Delays and Conditions")]
        public static TaskSequence DelaysAndConditions()
        {
            return TaskSequenceBuilder.Create("Delays and Conditions")
                .Do(_ => ForgeLog.Info("[Waits] One second..."))
                .Delay(1f)
                .Do(_ => ForgeLog.Info("[Waits] Press Space."))
                .DelayUntil(() => Pressed(KeyCode.Space))
                .Do(d =>
                {
                    d.SetPrimary(COUNT, 0);
                    ForgeLog.Info("[Waits] Press Tab three times.");
                })
                // Asked at the end of each frame, after every Update has run
                .DelayUntil(d => Pressed(KeyCode.Tab) && d.Increment(COUNT) >= 3, PlayerLoopTiming.PostLateUpdate)
                .Do(_ => ForgeLog.Info("[Waits] Hold Left Shift, then let go."))
                .DelayWhile(() => !ForgeInput.Current.KeyHeld(KeyCode.LeftShift))
                .WaitUntil(() => !ForgeInput.Current.KeyHeld(KeyCode.LeftShift))
                .Do(_ => ForgeLog.Info("[Waits] Until the run is eight seconds old..."))
                .WaitUntil(d => d.ElapsedTime >= 8f)
                .Stage(s => s
                    .WithName("Loading gate")
                    .OnInit(d =>
                    {
                        d.SetPrimary(READY, false);
                        ForgeLog.Info("[Waits] Loading for two seconds: press Space once it has loaded.");
                    })
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.Delay(2f, t);
                        d.SetPrimary(READY, true);
                        ForgeLog.Info("[Waits] Loaded.");
                    })
                    // DelayWhile waits while its condition holds, the opposite of DelayUntil
                    .DelayWhile(d => !d.GetPrimary<bool>(READY))
                    .DelayUntil(d => d.GetPrimary<bool>(READY) && Pressed(KeyCode.Space))
                    .WaitUntil(d => d.GetPrimary<bool>(READY))
                    // However quick the rest, the stage lasts three seconds
                    .SyncDelay(3f))
                .Stage(s => s
                    .WithName("First of four")
                    .WhenAny()
                    .OnInit(_ => ForgeLog.Info("[Waits] Press Space, click, hold Left Shift, or wait four seconds."))
                    .DelayUntil(() => Pressed(KeyCode.Space))
                    .WaitUntil(() => ForgeInput.Current.PointerPressed(0))
                    .DelayWhile(() => !ForgeInput.Current.KeyHeld(KeyCode.LeftShift))
                    .Delay(4f))
                .Do(_ => ForgeLog.Info("[Waits] Done."))
                .BuildSequence();
        }

        /// <summary>
        /// Waiting for the player inside a task's own code, through the library: a key, one of several keys, a pointer's
        /// button, or any condition, each asked once a frame and held while the sequence is paused. Input comes through
        /// ForgeInput: the Input System's or the Input Manager's, whichever the project has, or one a game puts in.
        /// </summary>
        [TaskSequenceMethod("Waits: Keys and the Pointer")]
        public static TaskSequence KeysAndPointer()
        {
            return TaskSequenceBuilder.Create("Keys and the Pointer")
                .Task(async (d, t) =>
                {
                    var input = ForgeInput.Current;

                    ForgeLog.Info("[Input] Press Space.");
                    await SequenceTaskLibrary.WaitForKey(KeyCode.Space, t);

                    ForgeLog.Info("[Input] Press 1, 2 or 3.");
                    KeyCode pick = await SequenceTaskLibrary.WaitForAnyKey(NumberKeys, t);
                    ForgeLog.Info($"[Input] You pressed {pick}. Now click.");

                    await SequenceTaskLibrary.WaitForPointer(0, t);
                    if (input.TryGetPointerPosition(out var position)) ForgeLog.Info($"[Input] Clicked at {position} on the screen.");

                    ForgeLog.Info("[Input] Hold the right button, then let go.");
                    await SequenceTaskLibrary.WaitUntil(() => input.PointerHeld(1), t);
                    await SequenceTaskLibrary.WaitWhile(() => input.PointerHeld(1), t);

                    ForgeLog.Info("[Input] Scroll three notches, either way.");
                    float scrolled = 0f;
                    while (Mathf.Abs(scrolled) < 3f)
                    {
                        await SequenceTaskLibrary.Yield(t);
                        scrolled += input.ScrollDelta;
                    }

                    ForgeLog.Info("[Input] Done.");
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // STAGES
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A stage's policy says when it ends. WhenAll, the default, waits for every task; WhenAny ends with the first, and
        /// cuts the rest off; WhenN ends as that many have. WithPolicy takes a policy as an object: a built-in one, or one
        /// of your own, as LeadTaskStagePolicy is. A task learns it was cut off from its token, as an
        /// OperationCanceledException out of whatever it awaits.
        /// </summary>
        [TaskSequenceMethod("Stages: Policies")]
        public static TaskSequence Policies()
        {
            return TaskSequenceBuilder.Create("Policies")
                .Stage(s => s
                    .WithName("When all")
                    .WhenAll()
                    .Task(Racer("All, A", 0.5f))
                    .Task(Racer("All, B", 1f))
                    .Task(Racer("All, C", 1.5f)))
                .Stage(s => s
                    .WithName("When any")
                    .WhenAny()
                    .Task(Racer("Any, A", 0.5f))
                    .Task(Racer("Any, B", 1f))
                    .Task(Racer("Any, C", 1.5f)))
                .Stage(s => s
                    .WithName("When two")
                    .WhenN(2)
                    .Task(Racer("Two, A", 0.5f))
                    .Task(Racer("Two, B", 1f))
                    .Task(Racer("Two, C", 1.5f)))
                // The same as objects. The built-in policies keep nothing of a run's, so one instance serves every stage
                .Stage(s => s
                    .WithName("WhenAllStagePolicy")
                    .WithPolicy(WhenAllStagePolicy.Instance)
                    .Task(Racer("Object all, A", 0.25f))
                    .Task(Racer("Object all, B", 0.5f)))
                .Stage(s => s
                    .WithName("WhenAnyStagePolicy")
                    .WithPolicy(WhenAnyStagePolicy.Instance)
                    .Task(Racer("Object any, A", 0.25f))
                    .Task(Racer("Object any, B", 0.5f)))
                .Stage(s => s
                    .WithName("WhenNStagePolicy")
                    .WithPolicy(new WhenNStagePolicy(1))
                    .Task(Racer("Object one, A", 0.25f))
                    .Task(Racer("Object one, B", 0.5f)))
                // A policy of your own: the stage lasts as long as its first task, and the others are cut off with it
                .Stage(s => s
                    .WithName("Lead task")
                    .WithPolicy(LeadTaskStagePolicy.Instance)
                    .Task(Racer("Lead", 1f))
                    .Task(Racer("Quick flourish", 0.25f))
                    .Task(Racer("Slow flourish", 3f)))
                .Do(_ => ForgeLog.Info("[Policies] Done."))
                .BuildSequence();
        }

        // A task that waits its time and says so, or says it was cut off
        private static Func<SequenceDataPacket, CancellationToken, UniTask> Racer(string name, float seconds)
        {
            return async (data, token) =>
            {
                try
                {
                    await SequenceTaskLibrary.Delay(seconds, token);
                    ForgeLog.Info($"[Policies] {name} ends, after {seconds}s.");
                }
                catch (OperationCanceledException)
                {
                    ForgeLog.Info($"[Policies] {name} is cut off.");
                    throw;
                }
            };
        }

        /// <summary>
        /// A sub-stage is a stage inside a stage: a track of its own that runs alongside its stage's tasks and other
        /// sub-stages, with its own policy, timers, conditions and repeat. Its stage counts it as one task. Here a cast bar
        /// fills while a watcher waits for Escape, and the stage, WhenAny, ends with whichever ends first. A Branch inside a
        /// stage is a sub-stage too, which picks its case as the stage starts.
        /// </summary>
        [TaskSequenceMethod("Stages: Sub-Stages")]
        public static TaskSequence SubStages()
        {
            // A sub-stage built by hand: a spark every half second, for as long as its stage lasts. Its log is Verbose,
            // which ForgeLog drops unless its level is lowered to show it
            var sparks = new SequenceStage { Repeat = true };
            sparks.Tasks.Add(new DelegateSequenceTask(async (data, token) =>
            {
                await SequenceTaskLibrary.Delay(0.5f, token);
                ForgeLog.Verbose("[Sub-stages] A spark.");
            }));

            return TaskSequenceBuilder.Create("Sub-Stages")
                .Do(d =>
                {
                    d.SetPrimary(PROGRESS, 0);
                    d.SetPrimary(CANCELLED, false);
                    ForgeLog.Info("[Sub-stages] Casting for two seconds: Escape cancels.");
                })
                .Stage(s => s
                    .WithName("Cast")
                    .WhenAny()
                    .SubStage(bar => bar
                        .WithName("Cast bar")
                        .WithRepeat(4)
                        .Task(async (d, t) =>
                        {
                            await SequenceTaskLibrary.Delay(0.5f, t);
                            ForgeLog.Info($"[Sub-stages] {d.Increment(PROGRESS, 25)}%");
                        }))
                    .SubStage(watcher => watcher
                        .WithName("Escape watcher")
                        .DelayUntil(d =>
                        {
                            if (!Pressed(KeyCode.Escape)) return false;
                            d.SetPrimary(CANCELLED, true);
                            return true;
                        }))
                    .SubStage(sparks))
                .Stage(s => s
                    .WithName("Result")
                    .Do(d => ForgeLog.Info($"[Sub-stages] The bar reached {d.GetPrimary<int>(PROGRESS)}%."))
                    .Branch(b => b
                        .If(d => d.GetPrimary<bool>(CANCELLED), cancelled => cancelled.Do(_ => ForgeLog.Info("[Sub-stages] Cancelled.")))
                        .Default(cast => cast.Do(_ => ForgeLog.Info("[Sub-stages] Cast.")))))
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // FLOW
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A branch is a stage that picks one case as it starts: the first whose condition holds, else its default, else
        /// nothing. The case runs as a stage, with whatever settings it was given. A case or a default can be a
        /// SequenceStage built by hand.
        /// </summary>
        [TaskSequenceMethod("Flow: Branches")]
        public static TaskSequence Branches()
        {
            var natural = new SequenceStage();
            natural.Tasks.Add(new DelegateSyncTask(d => ForgeLog.Info("[Branches] A natural 20: a critical hit!")));

            var steady = new SequenceStage();
            steady.Tasks.Add(new DelegateSyncTask(d => ForgeLog.Info("[Branches] No fumble.")));

            return TaskSequenceBuilder.Create("Branches")
                .Do(d =>
                {
                    d.SetPrimary(ROLL, ForgeRandom.Range(1, 21));
                    ForgeLog.Info($"[Branches] Rolled {d.GetPrimary<int>(ROLL)}.");
                })
                .Branch(b => b
                    .If(d => d.GetPrimary<int>(ROLL) == 20, natural)
                    .If(d => d.GetPrimary<int>(ROLL) >= 10, hit => hit
                        .WithName("Hit")
                        .Do(_ => ForgeLog.Info("[Branches] A hit.")))
                    .Default(miss => miss
                        .WithName("Miss")
                        .Do(_ => ForgeLog.Info("[Branches] A miss."))))
                .Branch(b => b
                    .If(d => d.GetPrimary<int>(ROLL) == 1, fumble => fumble
                        .WithName("Fumble")
                        .Task(async (d, t) =>
                        {
                            ForgeLog.Info("[Branches] A fumble: the sword slips...");
                            await SequenceTaskLibrary.Delay(1f, t);
                        }))
                    .Default(steady))
                // With no case that holds and no default, a branch does nothing
                .Branch(b => b.If(_ => false, never => never.Do(_ => ForgeLog.Info("[Branches] Never."))))
                .Do(_ => ForgeLog.Info("[Branches] Done."))
                .BuildSequence();
        }

        /// <summary>
        /// A stage can run more than once. WithRepeat(3) runs it three times; WithRepeat(true) until its repeat is broken;
        /// WithRepeatAndDelay until it's skipped, with a pause between rounds. A break lets the round in progress finish,
        /// and the sequence goes on; a skip ends the stage at once. A condition is asked each frame, so one that comes true
        /// as a round ends can let one more round start: a break from a task of the round, or from OnRepeat, called after
        /// each round and before the next, stops the next round for certain.
        /// </summary>
        [TaskSequenceMethod("Flow: Repeating Stages")]
        public static TaskSequence RepeatingStages()
        {
            return TaskSequenceBuilder.Create("Repeating Stages")
                .Stage(s => s
                    .WithName("Three times")
                    .WithRepeat(3)
                    .Task(async (d, t) =>
                    {
                        ForgeLog.Info($"[Repeats] Knock {d.Increment(KNOCKS)}.");
                        await SequenceTaskLibrary.Delay(0.4f, t);
                    }))
                // Until a condition breaks it, when the round in progress has finished
                .Stage(s => s
                    .WithName("Ticking")
                    .WithRepeat(true)
                    .OnInit(_ => ForgeLog.Info("[Repeats] Ticking until you press Space."))
                    .StopRepeatWhen(_ => Pressed(KeyCode.Space))
                    .OnRepeat(ctx => ForgeLog.Info($"[Repeats] Tick {ctx.Runtime.GetRepeatCount(ctx.Stage) + 1}."))
                    .OnTerminate((ctx, completed) => ForgeLog.Info($"[Repeats] Ticking stops, its repeat broken: {ctx.Runtime.IsRepeatBroken(ctx.Stage)}."))
                    .Delay(0.5f))
                // Until a task breaks it, in time for its own round's end
                .Stage(s => s
                    .WithName("Five points")
                    .WithRepeat(true)
                    .OnInit(d =>
                    {
                        d.SetPrimary(POINTS, 0);
                        ForgeLog.Info("[Repeats] Press Tab five times.");
                    })
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.WaitForKey(KeyCode.Tab, t);
                        int points = d.Increment(POINTS);
                        ForgeLog.Info($"[Repeats] {points} of 5, in round {d.StageRepeatCount + 1}.");
                        if (points >= 5) d.BreakStageRepeat();
                    }))
                // Until OnRepeat breaks it, between two rounds
                .Stage(s => s
                    .WithName("Coin flips")
                    .WithRepeat(true)
                    .OnRepeat(ctx =>
                    {
                        if (!ctx.Data.GetPrimary<bool>(HEADS)) return;
                        ForgeLog.Info("[Repeats] Heads: no more flips.");
                        ctx.Data.BreakStageRepeat(ctx.Stage);
                    })
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.Delay(0.3f, t);
                        bool heads = ForgeRandom.Value < 0.5f;
                        d.SetPrimary(HEADS, heads);
                        ForgeLog.Info(heads ? "[Repeats] Heads." : "[Repeats] Tails.");
                    }))
                // Until it's skipped, a second between rounds
                .Stage(s => s
                    .WithName("Pulse")
                    .WithRepeatAndDelay(1f)
                    .OnInit(_ => ForgeLog.Info("[Repeats] Pulsing each second until you press Escape."))
                    .SkipWhen(_ => Pressed(KeyCode.Escape))
                    .Do(d => ForgeLog.Info($"[Repeats] Pulse, {d.ElapsedTime:F1}s in.")))
                // The same policy as an object, broken by an injection from its own task
                .Stage(s => s
                    .WithName("Quick pulse")
                    .WithPolicy(new RepeatUntilSkippedPolicy(0.25f))
                    .OnInit(d => d.SetPrimary(COUNT, 0))
                    .Do(d =>
                    {
                        int pulses = d.Increment(COUNT);
                        ForgeLog.Info($"[Repeats] Quick pulse {pulses}.");
                        if (pulses >= 4) d.Inject(BreakStageRepeatInjection.Instance);
                    }))
                .Do(_ => ForgeLog.Info("[Repeats] Done."))
                .BuildSequence();
        }

        /// <summary>
        /// WithRepeat(true) on the sequence runs it again each time it completes, on the same packet, until something
        /// interrupts it or a run fails. OnComplete hears each run end, and OnTerminate the last.
        /// </summary>
        [TaskSequenceMethod("Flow: Repeating Sequence")]
        public static TaskSequence RepeatingSequence()
        {
            return TaskSequenceBuilder.Create("Repeating Sequence")
                .WithRepeat(true)
                .InterruptWhen(_ => Pressed(KeyCode.Escape))
                .Do(d => ForgeLog.Info($"[Repeat] Run {d.Increment(RUNS)}: Escape stops it."))
                .Delay(1f)
                .OnComplete((ctx, completed) => ForgeLog.Info($"[Repeat] Run {ctx.Data.GetPrimary<int>(RUNS)} {(completed ? "completed" : "was cut short")}."))
                .OnTerminate((ctx, completed) => ForgeLog.Info($"[Repeat] Stopped after {ctx.Data.GetPrimary<int>(RUNS)} runs."))
                .BuildSequence();
        }

        /// <summary>
        /// A condition is asked each frame and applies an injection when it holds: once, with fireOnce, or each frame it
        /// holds. The sequence's own conditions are asked for its whole run, a stage's only while that stage runs. Whatever
        /// runs the sequence asks them: ProcessControl does, at the timing WithConditionCheckTiming sets, Update unless set.
        /// A predicate can read the runtime as well as the packet.
        /// </summary>
        [TaskSequenceMethod("Flow: Conditions")]
        public static TaskSequence Conditions()
        {
            return TaskSequenceBuilder.Create("Conditions")
                .WithConditionCheckTiming(EProcessStepTiming.LateUpdate)
                .InterruptWhen(_ => Pressed(KeyCode.Escape))
                .SkipStageWhen(_ => Pressed(KeyCode.Tab))
                .InjectWhen((d, runtime) => runtime.ElapsedTime >= 60f, InterruptSequenceInjection.Instance, fireOnce: true)
                .Do(d =>
                {
                    d.SetPrimary(HEAT, 0);
                    ForgeLog.Info("[Conditions] H adds heat, and five heat moves on. Tab skips a stage, Escape ends the run.");
                })
                .Stage(s => s
                    .WithName("Heat up")
                    // An injection can do anything with the run: this one only adds heat
                    .InjectWhen(_ => Pressed(KeyCode.H), new DelegateSequenceInjection(runtime =>
                    {
                        ForgeLog.Info($"[Conditions] Heat {runtime.Data.Increment(HEAT)}.");
                        return true;
                    }))
                    .SkipWhen(d => d.GetPrimary<int>(HEAT) >= 5)
                    .InjectWhen((d, runtime) => runtime.ElapsedTime >= 30f, SkipStageInjection.Instance, fireOnce: true)
                    // Never ends by itself: a condition ends it
                    .Task((d, t) => UniTask.Never(t)))
                .Stage(s => s
                    .WithName("Cool down")
                    .WithRepeat(true)
                    .OnInit(_ => ForgeLog.Info("[Conditions] Cooling: H now overheats it, ending the run."))
                    .InterruptWhen(_ => Pressed(KeyCode.H))
                    .StopRepeatWhen(d => d.GetPrimary<int>(HEAT) <= 0)
                    // Cools first and waits after, so the condition is asked while the round that met it still runs
                    .Task(async (d, t) =>
                    {
                        int heat = Mathf.Max(0, d.GetPrimary<int>(HEAT) - 1);
                        d.SetPrimary(HEAT, heat);
                        ForgeLog.Info($"[Conditions] Cooling: {heat}.");
                        await SequenceTaskLibrary.Delay(0.5f, t);
                    }))
                .Do(_ => ForgeLog.Info("[Conditions] Cooled."))
                .BuildSequence();
        }

        /// <summary>
        /// A task can steer its own run through its packet: skip the stage it's in, jump to another by index or by name,
        /// start over, or end the run. Each applies an injection as it's called, and the stage the task is in ends there,
        /// so the task returns after it. Inject takes any injection, your own among them. CheckInjectConditions injects
        /// when a condition holds, a frame later, and says whether it did.
        /// </summary>
        [TaskSequenceMethod("Flow: Injecting by Hand")]
        public static TaskSequence InjectingByHand()
        {
            return TaskSequenceBuilder.Create("Injecting by Hand")
                // 0
                .Stage(s => s
                    .WithName("Menu")
                    .Task(async (d, t) =>
                    {
                        ForgeLog.Info("[Menu] 1 plays, 2 options, 3 credits, 4 holds a second and starts over, Escape quits.");
                        switch (await SequenceTaskLibrary.WaitForAnyKey(MenuKeys, t))
                        {
                            case KeyCode.Alpha1:
                                d.JumpToStage("Play");
                                break;
                            case KeyCode.Alpha2:
                                d.JumpToStage(3);
                                break;
                            case KeyCode.Alpha3:
                                d.Inject(new JumpToStageInjection(4));
                                break;
                            case KeyCode.Alpha4:
                                d.Inject(new HoldInjection(1f));
                                d.RestartSequence();
                                break;
                            default:
                                d.Interrupt();
                                break;
                        }
                    }))
                // 1
                .Stage(s => s
                    .WithName("Play")
                    .Task(async (d, t) =>
                    {
                        d.SetPrimary(SCORE, 0);
                        ForgeLog.Info("[Menu] Playing: Space scores, three wins. Tab gives up.");
                        while (true)
                        {
                            KeyCode key = await SequenceTaskLibrary.WaitForAnyKey(PlayKeys, t);
                            if (key == KeyCode.Tab)
                            {
                                // Ends this stage, and the run goes on with the next
                                d.SkipStage();
                                return;
                            }

                            ForgeLog.Info($"[Menu] Score {d.Increment(SCORE)}.");
                            if (await d.CheckInjectConditions(() => d.GetPrimary<int>(SCORE) >= 3, new JumpToStageInjection(5), t)) return;
                        }
                    }))
                // 2
                .Stage(s => s
                    .WithName("Gave up")
                    .Do(d =>
                    {
                        ForgeLog.Info("[Menu] You gave up. Back to the menu.");
                        d.RestartSequence();
                    }))
                // 3
                .Stage(s => s
                    .WithName("Options")
                    .Task(async (d, t) =>
                    {
                        ForgeLog.Info("[Menu] No options here.");
                        await SequenceTaskLibrary.Delay(1f, t);
                        d.JumpToStage("Menu");
                    }))
                // 4
                .Stage(s => s
                    .WithName("Credits")
                    .Task(async (d, t) =>
                    {
                        ForgeLog.Info("[Menu] Made with PlayForge.");
                        await SequenceTaskLibrary.Delay(1f, t);
                        d.JumpToStage(0);
                    }))
                // 5
                .Stage(s => s
                    .WithName("Won")
                    .Do(_ => ForgeLog.Info("[Menu] You win.")))
                .BuildSequence();
        }

        /// <summary>
        /// Skip and maintain moves the run on while the stage stays running behind it: a stage that should outlive its
        /// turn, as an aura that lasts while an ability goes on. The run waits for its maintained stages before it ends, so
        /// something must stop one that never ends by itself. StopMaintainedLast stops the oldest, StopMaintainedAtIndex
        /// the one at a stage's index, and StopMaintainedAll every one.
        /// </summary>
        [TaskSequenceMethod("Flow: Maintained Stages")]
        public static TaskSequence MaintainedStages()
        {
            return TaskSequenceBuilder.Create("Maintained Stages")
                // 0
                .Stage(s => s
                    .WithName("Aura")
                    .Task(async (d, t) =>
                    {
                        // Called while this is the run's current stage, so it's this stage that's maintained. A maintained
                        // stage's task that called it again later would maintain whatever stage was current then
                        d.SkipAndMaintain();
                        await Pulse("Aura", 0.6f, t);
                    }))
                // 1
                .Stage(s => s
                    .WithName("Regeneration")
                    .Task(async (d, t) =>
                    {
                        d.Inject(SkipAndMaintainInjection.Instance);
                        await Pulse("Regeneration", 0.8f, t);
                    }))
                // 2: maintained by its own condition, once, the frame after it starts
                .Stage(s => s
                    .WithName("Shield")
                    .InjectWhen(_ => true, SkipAndMaintainInjection.Instance, fireOnce: true)
                    .Task((d, t) => Pulse("Shield", 1f, t)))
                .Delay(2f)
                .Do(d =>
                {
                    ForgeLog.Info("[Maintained] Stopping the oldest, the aura.");
                    d.Inject(StopMaintainedLastInjection.Instance);
                })
                .Delay(1.5f)
                .Do(d =>
                {
                    ForgeLog.Info("[Maintained] Stopping the one at index 2, the shield.");
                    d.Inject(new StopMaintainedAtIndexInjection(2));
                })
                .Delay(1.5f)
                .Do(d =>
                {
                    ForgeLog.Info("[Maintained] Stopping the rest.");
                    d.Inject(StopMaintainedAllInjection.Instance);
                })
                .Do(_ => ForgeLog.Info("[Maintained] Done."))
                .BuildSequence();
        }

        // Says it's still there now and then, until its stage stops it
        private static async UniTask Pulse(string name, float interval, CancellationToken token)
        {
            try
            {
                while (true)
                {
                    await SequenceTaskLibrary.Delay(interval, token);
                    ForgeLog.Info($"[Maintained] {name}...");
                }
            }
            finally
            {
                ForgeLog.Info($"[Maintained] {name} stops.");
            }
        }

        /// <summary>
        /// A sequence can refuse injections. AllowInjections names the kinds it takes, AllowOnlyInterrupt takes interrupts
        /// alone, and DisallowInjections takes none; a stage's own setting holds while it runs, in place of the sequence's.
        /// A refused injection does nothing and returns false. Inject with overridePermission skips the check.
        /// InjectStageLocal checks a stage's own setting, and the sequence's where the stage has none. A stage's
        /// conditions keep to its setting; timers apply their injections without asking.
        /// </summary>
        [TaskSequenceMethod("Flow: Injection Permissions")]
        public static TaskSequence InjectionPermissions()
        {
            return TaskSequenceBuilder.Create("Injection Permissions")
                .AllowInjections(typeof(SkipStageInjection), typeof(InterruptSequenceInjection), typeof(HoldInjection))
                .Stage(s => s
                    .WithName("The sequence's rules")
                    .Task(async (d, t) =>
                    {
                        Report("A jump", d.Inject(new JumpToStageInjection(3)));
                        Report("A hold", d.Inject(new HoldInjection(0.5f)));
                        await SequenceTaskLibrary.Delay(1f, t);
                        Report("A skip", d.SkipStage());
                    }))
                .Stage(s => s
                    .WithName("Skips only")
                    .AllowInjections(typeof(SkipStageInjection))
                    .Task(async (d, t) =>
                    {
                        Report("A hold, here", d.Inject(new HoldInjection(0.5f)));
                        await SequenceTaskLibrary.Delay(0.5f, t);
                        Report("A skip, here", d.SkipStage());
                    }))
                .Stage(s => s
                    .WithName("Interrupts only")
                    .AllowOnlyInterrupt()
                    .Task(async (d, t) =>
                    {
                        Report("A skip, here", d.SkipStage());
                        Report("A hold, by this stage's own rules", d.InjectStageLocal(new HoldInjection(0.5f)));
                        await SequenceTaskLibrary.Delay(0.5f, t);
                        Report("A skip that overrides the rules", d.Inject(SkipStageInjection.Instance, overridePermission: true));
                    }))
                .Stage(s => s
                    .WithName("Nothing gets in")
                    .DisallowInjections()
                    .WithTimeout(1.5f)
                    .OnTimeout(() => ForgeLog.Info("[Permissions] Its timeout skipped it all the same: a timer doesn't ask."))
                    .Task(async (d, t) =>
                    {
                        Report("An interrupt, here", d.Interrupt());
                        Report("A skip, by the rules of the stage given", d.InjectStageLocal(SkipStageInjection.Instance, d.CurrentStage));
                        await UniTask.Never(t);
                    }))
                .Stage(s => s
                    .WithName("No rules of its own")
                    .Task(async (d, t) =>
                    {
                        // The stage has none, so the sequence's hold
                        Report("A jump, by this stage's rules", d.InjectStageLocal(new JumpToStageInjection(0)));
                        await SequenceTaskLibrary.Delay(0.5f, t);
                    }))
                .BuildSequence();
        }

        private static void Report(string what, bool taken) =>
            ForgeLog.Info($"[Permissions] {what}: {(taken ? "taken" : "refused")}.");

        /// <summary>
        /// A critical section is a part of a run that mustn't be pushed around: while it runs, only the injections it
        /// allows get in, interrupts alone unless WithCriticalAllowedInjections names others. A stage flagged critical is
        /// critical while it runs. OnCriticalSectionExited fires once, as the last critical part ends: an ability's
        /// Sequence task sets it itself, to free the ability there. In each stage, try Tab, a skip, H, a hold, and Escape,
        /// an interrupt.
        /// </summary>
        [TaskSequenceMethod("Flow: Critical Stages")]
        public static TaskSequence CriticalStages()
        {
            var sequence = TaskSequenceBuilder.Create("Critical Stages")
                .InterruptWhen(_ => Pressed(KeyCode.Escape))
                .SkipStageWhen(_ => Pressed(KeyCode.Tab))
                .InjectWhen(_ => Pressed(KeyCode.H), new HoldInjection(1f))
                .Stage(s => s
                    .WithName("Wind-up")
                    .WithCriticalFlag(true)
                    .OnInit(_ => ForgeLog.Info($"[Critical] Wind-up, critical ({s.IsCritical}): only Escape gets in."))
                    .Delay(3f))
                .Stage(s => s
                    .WithName("Channel")
                    .WithCriticalAllowedInjections(typeof(InterruptSequenceInjection), typeof(HoldInjection))
                    .OnInit(_ => ForgeLog.Info("[Critical] Channel, critical: Escape and H get in."))
                    .Delay(3f))
                .Stage(s => s
                    .WithName("Recovery")
                    .OnInit(d => ForgeLog.Info($"[Critical] Recovery, critical: {d.IsCriticalSection}. Anything gets in."))
                    .Delay(3f))
                .OnTerminate((ctx, completed) => ForgeLog.Info(completed ? "[Critical] Done." : "[Critical] Cut short."))
                .BuildSequence();

            sequence.OnCriticalSectionExited = () => ForgeLog.Info("[Critical] The critical section is over.");
            return sequence;
        }

        /// <summary>
        /// A whole sequence can be critical, for its whole run. WithCriticalAllowedInjections on the sequence names what
        /// gets in, here interrupts and skips, and flags it critical as well. Its critical section ends as its run does.
        /// Tab and Escape get in; H doesn't.
        /// </summary>
        [TaskSequenceMethod("Flow: Critical Sequence")]
        public static TaskSequence CriticalSequence()
        {
            var sequence = TaskSequenceBuilder.Create("Critical Sequence")
                .WithCriticalFlag(true)
                .WithCriticalAllowedInjections(typeof(InterruptSequenceInjection), typeof(SkipStageInjection))
                .InterruptWhen(_ => Pressed(KeyCode.Escape))
                .SkipStageWhen(_ => Pressed(KeyCode.Tab))
                .InjectWhen(_ => Pressed(KeyCode.H), new HoldInjection(1f))
                .Do(d => ForgeLog.Info($"[Critical sequence] Critical: {d.IsCriticalSection}. Tab skips a stage, Escape ends it, H does nothing."))
                .Delay(3f)
                .Delay(3f)
                .BuildSequence();

            sequence.OnCriticalSectionExited = () => ForgeLog.Info("[Critical sequence] Its critical section ended with its run.");
            return sequence;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // TIMERS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Timers on a stage, counted from its start in the run's time, which stops while it's paused. A timeout applies its
        /// injection when it's up, a skip unless given another, and a stage can have several; OnTimeout hears which one
        /// fired. A max duration is one more, with a hook of its own. A task can start a timer on its stage as it runs, with
        /// SetStageTimeout, when only it knows how long the stage should last.
        /// </summary>
        [TaskSequenceMethod("Timers: Stage Timers")]
        public static TaskSequence StageTimers()
        {
            return TaskSequenceBuilder.Create("Stage Timers")
                .Stage(s => s
                    .WithName("Answer")
                    .OnInit(_ => ForgeLog.Info("[Timers] Press Space within six seconds."))
                    // An alarm at three seconds, whose injection changes nothing, and a skip at six
                    .WithTimeout(3f, new DelegateSequenceInjection { applyFunc = runtime => true })
                    .WithTimeout(6f)
                    .OnTimeout((ctx, timeout) => ForgeLog.Info(timeout.Seconds < 6f
                        ? $"[Timers] {timeout.Seconds}s gone, three to go."
                        : $"[Timers] Too slow: skipped at {timeout.Seconds}s, {ctx.ElapsedTime:F1}s into the run."))
                    .DelayUntil(() => Pressed(KeyCode.Space)))
                .Stage(s => s
                    .WithName("Brace")
                    .WithMaxDuration(2f)
                    .OnMaxDuration(() => ForgeLog.Info("[Timers] Bracing's two seconds are up."))
                    .Task((d, t) => UniTask.Never(t)))
                .Stage(s => s
                    .WithName("Set as it runs")
                    .OnTimeout(() => ForgeLog.Info("[Timers] The timer the task set is up."))
                    .Task(async (d, t) =>
                    {
                        float seconds = ForgeRandom.Range(1f, 3f);
                        // A timer set this way skips its stage when it's up, as WithTimeout's does, unless given another
                        d.SetStageTimeout(seconds);
                        ForgeLog.Info($"[Timers] This stage lasts {seconds:F1}s.");
                        await UniTask.Never(t);
                    }))
                .Stage(s => s
                    .WithName("Requested")
                    .OnInit(d => d.Runtime.RequestStartStageTimeout(new SequenceTimeout { Seconds = 1f, Injection = SkipStageInjection.Instance }))
                    .OnTimeout(ctx => ForgeLog.Info($"[Timers] '{ctx.Stage.Metadata.Name}', stage {ctx.StageIndex}, timed out."))
                    .Task((d, t) => UniTask.Never(t)))
                .Do(_ => ForgeLog.Info("[Timers] Done."))
                .BuildSequence();
        }

        /// <summary>
        /// Timers on the whole run, counted from its start. Each timeout fires once and applies its injection to whatever
        /// stage runs then, a skip unless given another; the max duration interrupts the run unless given another. Here
        /// three stages that never end by themselves are skipped at two and four seconds, and the run is interrupted at
        /// six.
        /// </summary>
        [TaskSequenceMethod("Timers: The Run's Timers")]
        public static TaskSequence RunTimers()
        {
            return TaskSequenceBuilder.Create("The Run's Timers")
                .WithTimeout(2f)
                .WithTimeout(4f, SkipStageInjection.Instance)
                .OnTimeout((ctx, timeout) => ForgeLog.Info($"[Run timers] {timeout.Seconds}s: skipping '{ctx.Stage?.Metadata?.Name}'."))
                .WithMaxDuration(6f, InterruptSequenceInjection.Instance)
                .OnMaxDuration(ctx => ForgeLog.Warning($"[Run timers] The max duration, at {ctx.ElapsedTime:F1}s: interrupting stage {ctx.StageIndex}."))
                .OnTerminate((ctx, completed) => ForgeLog.Info($"[Run timers] Ended, completed: {completed}."))
                .Stage(s => s.WithName("First").Task((d, t) => UniTask.Never(t)))
                .Stage(s => s.WithName("Second").Task((d, t) => UniTask.Never(t)))
                .Stage(s => s.WithName("Third").Task((d, t) => UniTask.Never(t)))
                .BuildSequence();
        }

        // The clock the pulses below share, which the S key slows and speeds up: every sequence on it goes with it
        private static readonly ScaledClock PulseTime = new(1f);

        /// <summary>
        /// A run's time is its clock's: its waits, delays, timers, elapsed time, tweens and synced tasks all go by it. A
        /// packet's Clock comes first, then the sequence's own, WithClock's, then the packet's owner's, an ability's
        /// caster's, and then the game's. The unscaled clock runs a menu's sequence while the game's time stands, and a
        /// ScaledClock runs one at a pace of its own: sequences that share one slow down, speed up and stop together. Under
        /// ProcessControl's fixed tick, a sequence on the unscaled clock waits for Unity's frames, as the ticks stand with
        /// the game's time. Here two pulses share a clock, S halves and restores its pace, and a third pulse's packet names
        /// a clock twice as fast, which comes before the one its sequence names.
        /// </summary>
        [TaskSequenceMethod("Timers: Clocks")]
        public static TaskSequence Clocks()
        {
            // A pulse a second of its clock, four times
            TaskSequence Pulse(string name) => TaskSequenceBuilder.Create(name)
                .WithClock(PulseTime)
                .Stage(s => s
                    .WithRepeat(4)
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.Delay(1f, t);
                        ForgeLog.Info($"[Clocks] {name}: a second of its clock, {d.ElapsedTime:F1}s of it in; " +
                                      $"its clock steps {TaskSequenceRuntime.ClockOfRun(t).DeltaTime:F3}s a frame.");
                    }))
                .BuildSequence();

            // Each pulse runs on a packet of its own, so each run's elapsed time is its own
            UniTask RunPulse(string name, CancellationToken t) => Pulse(name).Run(SequenceDataPacket.Default(), t);

            return TaskSequenceBuilder.Create("Clocks")
                // A menu's: its run's own waits go on while the game is paused
                .WithClock(UnscaledClock.Instance)
                .Do(d =>
                {
                    PulseTime.Scale = 1f;
                    ForgeLog.Info($"[Clocks] This run is on the {(d.Runtime.Clock is UnscaledClock ? "unscaled" : "game's")} clock. " +
                                  "Press S to slow the pulses to half and back.");
                })
                .Stage(s => s
                    .WithName("Pulses")
                    // The pulses end the stage; the key's wait, which never ends, is cut off with them
                    .WhenN(3)
                    .Task((d, t) => RunPulse("Left", t))
                    .Task((d, t) => RunPulse("Right", t))
                    .Task((d, t) =>
                    {
                        // A packet of its own, whose clock comes before the one the sequence names
                        var fast = SequenceDataPacket.Default();
                        fast.Clock = new ScaledClock(2f);
                        return Pulse("Fast").Run(fast, t);
                    })
                    .Task(async (d, t) =>
                    {
                        while (true)
                        {
                            await SequenceTaskLibrary.WaitForKey(KeyCode.S, t);
                            PulseTime.Scale = PulseTime.Scale < 1f ? 1f : 0.5f;
                            ForgeLog.Info($"[Clocks] The pulses' clock runs at {PulseTime.Scale}, {PulseTime.Time:F1}s on it so far.");
                        }
                    }))
                .Do(d => ForgeLog.Info($"[Clocks] Done, {d.ElapsedTime:F1}s of unscaled time in."))
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ERRORS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A task that throws fails its stage. The stage's OnException hears it first: true takes the error, and the run
        /// goes on with the next stage; false passes it on. The sequence's OnException hears what its stages pass on: true
        /// counts the run a success, false fails it, and the run ends there. A failure is logged with the path to where it
        /// happened, unless error logging is off.
        /// </summary>
        [TaskSequenceMethod("Errors: Exceptions")]
        public static TaskSequence Exceptions()
        {
            return TaskSequenceBuilder.Create("Exceptions")
                .WithErrorLogging(true)
                .OnException((ctx, exception) =>
                {
                    ForgeLog.Warning($"[Errors] The sequence hears, from stage {ctx.StageIndex}: {exception.Message}.");
                    return false;
                })
                .OnComplete((ctx, completed) =>
                    ForgeLog.Info($"[Errors] Completed: {completed}. Failed with: {ctx.Runtime.FailureException?.Message ?? "nothing"}."))
                .Stage(s => s
                    .WithName("Handled")
                    .OnException((ctx, exception) =>
                    {
                        ForgeLog.Info($"[Errors] '{ctx.Stage.Metadata.Name}' takes the error: {exception.Message}.");
                        return true;
                    })
                    .Do(_ => throw new InvalidOperationException("the first stage broke")))
                .Do(_ => ForgeLog.Info("[Errors] The run goes on past a handled error."))
                .Stage(s => s
                    .WithName("Passed on")
                    // The stage logs nothing of its own, and the sequence logs the failure
                    .WithErrorLogging(false)
                    .OnException((ctx, exception) => false)
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.Delay(0.5f, t);
                        throw new TimeoutException("the second stage broke");
                    }))
                .Do(_ => ForgeLog.Info("[Errors] Never reached: the run failed."))
                .BuildSequence();
        }

        /// <summary>
        /// A retry: the sequence repeats, and its OnException takes each failure but the last. A taken error counts the run
        /// a success, which is what lets a repeating sequence go round again. A run that gets through ends the repeat
        /// itself, by interrupting it.
        /// </summary>
        [TaskSequenceMethod("Errors: Retry")]
        public static TaskSequence Retry()
        {
            const int tries = 4;
            return TaskSequenceBuilder.Create("Retry")
                .WithRepeat(true)
                .WithErrorLogging(false)
                .OnException((ctx, exception) =>
                {
                    int attempt = ctx.Data.GetPrimary<int>(ATTEMPTS);
                    ForgeLog.Warning($"[Retry] Attempt {attempt} of {tries} failed: {exception.Message}.");
                    return attempt < tries;
                })
                .OnTerminate((ctx, completed) => ForgeLog.Info(ctx.Data.GetPrimary<bool>(DONE) ? "[Retry] Connected." : "[Retry] Gave up."))
                .Task(async (d, t) =>
                {
                    ForgeLog.Info($"[Retry] Connecting, attempt {d.Increment(ATTEMPTS)}...");
                    await SequenceTaskLibrary.Delay(0.5f, t);
                    if (ForgeRandom.Value < 0.6f) throw new InvalidOperationException("no answer");

                    d.SetPrimary(DONE, true);
                    d.Interrupt();
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // HOOKS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The hooks a sequence and its stages call as they run, in the order they come: a stage's OnInit as it starts,
        /// OnRepeat after each round, OnTerminate as it ends, with whether it got to its end; the sequence's OnComplete as
        /// each run ends, and OnTerminate after its last. WithInjectionLogging logs each injection as it's applied: set on
        /// a stage, it decides for that stage's own timers and conditions.
        /// </summary>
        [TaskSequenceMethod("Hooks: Lifecycle")]
        public static TaskSequence Lifecycle()
        {
            return TaskSequenceBuilder.Create("Lifecycle")
                .WithInjectionLogging(true)
                .OnComplete((ctx, completed) => ForgeLog.Info($"[Hooks] The sequence's OnComplete: {completed}, {ctx.ElapsedTime:F1}s in."))
                .OnTerminate((ctx, completed) => ForgeLog.Info($"[Hooks] The sequence's OnTerminate: {completed}."))
                .Stage(s => s
                    .WithName("Twice")
                    .WithRepeat(true)
                    .OnInit(_ => ForgeLog.Info("[Hooks] Twice: OnInit."))
                    .OnRepeat(() => ForgeLog.Info("[Hooks] Twice: OnRepeat."))
                    .OnTerminate((ctx, completed) => ForgeLog.Info($"[Hooks] Twice: OnTerminate, {completed}, after {ctx.Runtime.GetRepeatCount(ctx.Stage) + 1} rounds."))
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.Delay(0.5f, t);
                        if (d.StageRepeatCount >= 1) d.BreakStageRepeat();
                    }))
                .Stage(s => s
                    .WithName("Timed out")
                    // Its timer's skip isn't logged, though the sequence logs its injections
                    .WithInjectionLogging(false)
                    .WithTimeout(0.5f)
                    .OnInit(_ => ForgeLog.Info("[Hooks] Timed out: OnInit."))
                    .OnTerminate(() => ForgeLog.Info("[Hooks] Timed out: OnTerminate."))
                    .Task((d, t) => UniTask.Never(t)))
                .Stage(s => s
                    .WithName("Skipped by hand")
                    .OnTerminate(() => ForgeLog.Info("[Hooks] Skipped by hand: OnTerminate."))
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.Delay(0.5f, t);
                        // Logged, as the sequence logs its injections
                        d.SkipStage();
                    }))
                .BuildSequence();
        }

        /// <summary>
        /// A sequence can run synced: ProcessControl steps it each frame rather than awaiting it, with no async work at all.
        /// WithLifecycle(Synchronous) asks for that, and WithStepTiming picks the loops it steps in. Synced, a sequence runs
        /// its step tasks, Do, SyncTask, WaitUntil, SyncDelay and the synced library's, and its async tasks finish at once
        /// and do nothing. OnStep is called at each timing it steps at, with the frame's time.
        /// </summary>
        [TaskSequenceMethod("Hooks: Stepped Every Frame")]
        public static TaskSequence SteppedEveryFrame()
        {
            return TaskSequenceBuilder.Create("Stepped Every Frame")
                .WithLifecycle(EProcessLifecycle.Synchronous)
                .WithStepTiming(EProcessStepTiming.UpdateAndLate)
                .OnStep(EProcessStepTiming.Update, (data, deltaTime) => data.Increment(FRAMES))
                .OnStep(EProcessStepTiming.LateUpdate, (data, deltaTime) => data.IncrementFloat(LATE_TIME, deltaTime))
                .OnComplete((ctx, completed) => ForgeLog.Info($"[Stepped] OnComplete: {completed}."))
                .OnTerminate((ctx, completed) => ForgeLog.Info($"[Stepped] OnTerminate: {completed}."))
                .Do(_ => ForgeLog.Info("[Stepped] Counting frames: press Space to stop."))
                .WaitUntil(_ => Pressed(KeyCode.Space))
                .Stage(s => s
                    .WithName("Settle")
                    .OnInit(_ => ForgeLog.Info("[Stepped] Settling for half a second."))
                    .SyncDelay(0.5f))
                .Do(d => ForgeLog.Info($"[Stepped] {d.GetPrimary<int>(FRAMES)} frames; LateUpdate counted {d.GetPrimary<float>(LATE_TIME):F2}s."))
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // DATA
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The packet carries a run's data, each piece under a tag, and its controls. What one task puts in, the next reads.
        /// It also says where the run is: its stage, its time, its chain. New packets come from SequenceDataPacket: Default's
        /// is empty, SceneRoot's puts what a run makes under GameRoot, and SceneLocal's under a transform you name. A copy
        /// takes another's data, for a run of its own.
        /// </summary>
        [TaskSequenceMethod("Data: The Packet")]
        public static TaskSequence ThePacket()
        {
            return TaskSequenceBuilder.Create("The Packet")
                .Stage(s => s
                    .WithName("Write")
                    .Do(d =>
                    {
                        d.SetPrimary(NAME, "Wren");
                        d.SetPrimary(HEALTH, 100);
                        d.AddPayload(INVENTORY, "rope");
                        d.AddPayload(INVENTORY, "lantern");
                        d.Increment(SCORE, 10);
                        d.IncrementFloat(SPEED, 1.5f);
                        d.Toggle(READY);
                        d.GetOrInit(LEVEL, 1);
                    }))
                .Stage(s => s
                    .WithName("Read")
                    .Do(d =>
                    {
                        ForgeLog.Info($"[Packet] {d.GetPrimary<string>(NAME)}: health {d.GetPrimary<int>(HEALTH)}, score {d.GetPrimary<int>(SCORE)}, " +
                                      $"speed {d.GetPrimary<float>(SPEED)}, ready {d.GetPrimary<bool>(READY)}, level {d.GetPrimary<int>(LEVEL)}.");
                        ForgeLog.Info($"[Packet] Carrying {d.Count(INVENTORY)}: {d.GetAll<string>(INVENTORY)}.");
                        ForgeLog.Info($"[Packet] Mana, which nothing set, reads {d.GetPrimary(MANA, 50)} by its fallback.");
                        ForgeLog.Info($"[Packet] Stage {d.StageIndex}, '{d.StageName}', of '{d.SequenceName}', {d.ElapsedTime:F2}s in. In a chain: {d.IsInChain}.");
                    }))
                .Stage(s => s
                    .WithName("New packets")
                    .Do(d =>
                    {
                        var copy = new SequenceDataPacket(d);
                        var empty = SequenceDataPacket.Default();
                        var underRoot = SequenceDataPacket.SceneRoot();
                        var parent = d.GetPrimary<Transform>(Tags.TRANSFORM);
                        var underParent = parent ? SequenceDataPacket.SceneLocal(parent) : SequenceDataPacket.Default();
                        ForgeLog.Info($"[Packet] The copy carries {copy.GetPrimary<string>(NAME)}; the empty one, {empty.GetPrimary(NAME, "no one")}.");
                        ForgeLog.Info($"[Packet] SceneRoot's parent: {ParentOf(underRoot)}. SceneLocal's: {ParentOf(underParent)}.");
                    }))
                .BuildSequence();
        }

        private static string ParentOf(ProcessDataPacket data)
        {
            var parent = data.GetPrimary<Transform>(Tags.TRANSFORM);
            return parent ? parent.name : "none";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // RUNNING
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Every way to run a sequence, each in turn on a small one. Through ProcessControl, as RegisterAndRun and Register
        /// do, a run steps with the game: its conditions are asked each frame, its process can pause it, and its owner's end
        /// ends it. Run directly, with Run or TryRun, it's yours to await, and nothing asks its conditions unless you call
        /// CheckConditions. A synced runner is yours to step.
        /// </summary>
        [TaskSequenceMethod("Running: Every Way")]
        public static TaskSequence EveryWay()
        {
            return TaskSequenceBuilder.Create("Every Way")
                // Through ProcessControl, with the relay to its process
                .Task(async (d, t) =>
                {
                    var relay = Small("RegisterAndRun").RegisterAndRun(SequenceDataPacket.SceneRoot());
                    if (relay != null) await relay.WhenTerminated(t);
                })
                // The same, saying why when it's refused, as with no ProcessControl in the scene
                .Task(async (d, t) =>
                {
                    var result = ProcessControl.Register(Small("Register")).With(SequenceDataPacket.Default()).Run();
                    if (result) await result.Relay.WhenTerminated(t);
                    else ForgeLog.Warning($"[Every way] Refused: {result.Refusal}.");
                })
                // A chain, through ProcessControl
                .Task(async (d, t) =>
                {
                    var chain = Small("Registered chain, first").Then(Small("Registered chain, second"));
                    if (ProcessControl.Register(chain).With(SequenceDataPacket.Default()).Run(out var relay)) await relay.WhenTerminated(t);
                })
                // A process made by hand, with the sequence's own lifecycle and timing...
                .Task(async (d, t) => await RegisterAndWait(new TaskSequenceProcess(Small("TaskSequenceProcess")), t))
                // ...or with others in their place: this one runs synced, stepped in LateUpdate
                .Task(async (d, t) => await RegisterAndWait(new TaskSequenceProcess(Small("Synced process"), EProcessLifecycle.Synchronous, EProcessStepTiming.LateUpdate), t))
                // A synced runner's process
                .Task(async (d, t) => await RegisterAndWait(new TaskSequenceProcess(SmallBuilder("Synced runner").BuildSyncRunner(), "Synced runner", EProcessStepTiming.Update), t))
                // A chain's process
                .Task(async (d, t) => await RegisterAndWait(new TaskSequenceProcess(Small("Chain process, first").Then(Small("Chain process, second")), "Chain process"), t))
                // A sequence whose definition asks to run synced
                .Task(async (d, t) =>
                {
                    var synced = SmallBuilder("Synced by its own say").WithLifecycle(EProcessLifecycle.Synchronous, EProcessStepTiming.Update).BuildSequence();
                    var relay = synced.RegisterAndRun(SequenceDataPacket.Default());
                    if (relay != null) await relay.WhenTerminated(t);
                })
                // Awaited directly, on a packet of its own
                .Task(async (d, t) => await Small("Run").Run(new SequenceDataPacket(d), t))
                // Awaited for its result: true if it completed
                .Task(async (d, t) =>
                {
                    bool completed = await Small("TryRun").TryRun(new SequenceDataPacket(d), t);
                    ForgeLog.Info($"[Every way] TryRun completed: {completed}.");
                })
                // On a runtime of your own, to reach that run and no other: here, to ask its conditions by hand
                .Task(async (d, t) =>
                {
                    var sequence = Escapable("CreateRuntime");
                    var runtime = sequence.CreateRuntime();
                    var run = sequence.Run(runtime, new SequenceDataPacket(d), t);
                    while (!run.Status.IsCompleted())
                    {
                        runtime.CheckConditions();
                        await SequenceTaskLibrary.Yield(t);
                    }
                    await run;
                    ForgeLog.Info($"[Every way] Its runtime says it completed: {runtime.CompletedSuccessfully}.");
                })
                // Directly, with the sequence asking its runs' conditions, and conditions from outside it too
                .Task(async (d, t) =>
                {
                    var sequence = Escapable("CheckConditions");
                    var outside = new List<SequenceCondition>
                    {
                        new SequenceCondition(_ => Pressed(KeyCode.Tab), SkipStageInjection.Instance, fireOnce: true)
                    };
                    var run = sequence.Run(new SequenceDataPacket(d), t);
                    while (!run.Status.IsCompleted())
                    {
                        sequence.CheckConditions();
                        sequence.CheckConditions(outside);
                        await SequenceTaskLibrary.Yield(t);
                    }
                    await run;
                })
                // A runtime made from a definition, with no TaskSequence at all
                .Task(async (d, t) => await new TaskSequenceRuntime(SmallBuilder("TaskSequenceRuntime").Build()).Run(new SequenceDataPacket(d), t))
                // A synced runner, stepped by hand: Step says when it has finished
                .Task(async (d, t) =>
                {
                    var runner = SmallBuilder("Stepped by hand").BuildSyncRunner();
                    var packet = SequenceDataPacket.Default();
                    while (!runner.Step(packet, SequenceTaskLibrary.DeltaTime(t))) await SequenceTaskLibrary.Yield(t);
                })
                // Through a reference to a sequence method, as an ability's Sequence task runs one
                .Task(async (d, t) =>
                {
                    var reference = new TaskSequenceReference(typeof(TaskSequenceExamples).GetMethod(nameof(FirstSequence)));
                    ForgeLog.Info($"[Every way] The reference names '{reference.GetDisplayName()}'. A chain: {reference.IsChain()}.");
                    await reference.GetActiveSequence().Run(new SequenceDataPacket(d), t);
                })
                .Do(_ => ForgeLog.Info("[Every way] Done."))
                .BuildSequence();
        }

        // Registers a sequence's process with ProcessControl, and waits for it to end
        private static async UniTask RegisterAndWait(TaskSequenceProcess process, CancellationToken token)
        {
            if (ProcessControl.Register(process).With(SequenceDataPacket.Default()).Run(out var relay)) await relay.WhenTerminated(token);
            else ForgeLog.Warning($"[Every way] ProcessControl refused {process.GetName()}.");
        }

        // A small sequence for the examples that run one: it says it runs, waits half a second, and says it's done. Its
        // tasks are step tasks, so it runs synced as well
        private static TaskSequenceBuilder SmallBuilder(string name)
        {
            return TaskSequenceBuilder.Create(name)
                .Do(_ => ForgeLog.Info($"[{name}] Runs."))
                .Stage(s => s.SyncDelay(0.5f))
                .Do(_ => ForgeLog.Info($"[{name}] Done."));
        }

        private static TaskSequence Small(string name) => SmallBuilder(name).BuildSequence();

        // A small sequence with a condition, that Escape ends early
        private static TaskSequence Escapable(string name)
        {
            return TaskSequenceBuilder.Create(name)
                .InterruptWhen(_ => Pressed(KeyCode.Escape))
                .Do(_ => ForgeLog.Info($"[{name}] Runs for two seconds: Escape ends it."))
                .Delay(2f)
                .Do(_ => ForgeLog.Info($"[{name}] Done."))
                .BuildSequence();
        }

        /// <summary>
        /// A paused run holds where it is: no stage starts or ends, and its timers and the library's waits stop counting. A
        /// task's own code can't be stopped from outside, so a task that keeps its own time keeps it by the library's
        /// DeltaTime, 0 while paused, or asks whether it's paused. Here one run is paused and resumed through its runtime,
        /// and another through its process's relay, as the Example Runner's pause key does.
        /// </summary>
        [TaskSequenceMethod("Running: Pause and Resume")]
        public static TaskSequence PauseAndResume()
        {
            var ticking = TaskSequenceBuilder.Create("Ticking")
                .Stage(s => s
                    .WithName("Tick")
                    // Waits through the library, which holds
                    .Task(async (d, t) =>
                    {
                        for (int i = 1; i <= 6; i++)
                        {
                            await SequenceTaskLibrary.Delay(0.5f, t);
                            ForgeLog.Info($"[Pause] Tick {i}, {d.ElapsedTime:F1}s of the run.");
                        }
                    })
                    // Waits through the runtime, which holds too
                    .Task(async (d, t) =>
                    {
                        await d.Runtime.Delay(1.5f, t);
                        ForgeLog.Info("[Pause] The runtime's delay is up.");
                    })
                    // Keeps its own time, and finds the pause from its token alone, as a helper of your own would
                    .Task(async (d, t) =>
                    {
                        var data = TaskSequenceRuntime.DataOfRun(t);
                        float waited = 0f;
                        bool told = false;
                        while (waited < 2f)
                        {
                            await SequenceTaskLibrary.Yield(t);
                            waited += SequenceTaskLibrary.DeltaTime(t);
                            if (told || !TaskSequenceRuntime.IsPausedRun(t)) continue;

                            told = true;
                            ForgeLog.Info($"[Pause] Holding. The packet agrees: {data.IsPaused}, and the library: {SequenceTaskLibrary.IsPaused(t)}.");
                        }
                    }))
                .BuildSequence();

            return TaskSequenceBuilder.Create("Pause and Resume")
                .Task(async (d, t) =>
                {
                    var runtime = ticking.CreateRuntime();
                    var run = ticking.Run(runtime, new SequenceDataPacket(d), t);
                    await SequenceTaskLibrary.Delay(1.2f, t);

                    runtime.Pause();
                    ForgeLog.Info($"[Pause] Paused the run: {runtime.IsPaused}. Space resumes it.");
                    await SequenceTaskLibrary.WaitForKey(KeyCode.Space, t);
                    runtime.Resume();
                    await run;
                })
                .Task(async (d, t) =>
                {
                    var relay = ticking.RegisterAndRun(SequenceDataPacket.SceneRoot());
                    if (relay == null) return;
                    await SequenceTaskLibrary.Delay(1.2f, t);

                    relay.Pause();
                    ForgeLog.Info($"[Pause] Paused its process: {relay.IsWaiting}. Space resumes it.");
                    await SequenceTaskLibrary.WaitForKey(KeyCode.Space, t);
                    relay.Unpause();
                    await relay.WhenTerminated(t);
                })
                .Do(_ => ForgeLog.Info("[Pause] Done."))
                .BuildSequence();
        }

        /// <summary>
        /// A run's runtime: where it is, how long it has run, how it ended, and the controls the packet's calls go through.
        /// The packet reaches it as Runtime, and a hook's context as Runtime too.
        /// </summary>
        [TaskSequenceMethod("Running: The Runtime")]
        public static TaskSequence TheRuntime()
        {
            return TaskSequenceBuilder.Create("The Runtime")
                .Stage(s => s
                    .WithName("Where it is")
                    .Do(d =>
                    {
                        var runtime = d.Runtime;
                        ForgeLog.Info($"[Runtime] Running: {runtime.IsRunning}. Stage {runtime.StageIndex}, '{runtime.CurrentStage.Metadata.Name}'. " +
                                      $"{runtime.ElapsedTime:F2}s in, at frame {ForgeTime.FrameCount}. Paused: {runtime.IsPaused}.");
                    }))
                .Stage(s => s
                    .WithName("Counted")
                    .WithRepeat(true)
                    .Task(async (d, t) =>
                    {
                        var runtime = d.Runtime;
                        await runtime.Delay(0.3f, t);
                        int round = runtime.GetRepeatCount(runtime.CurrentStage) + 1;
                        if (round >= 3) runtime.Inject(BreakStageRepeatInjection.Instance);
                        ForgeLog.Info($"[Runtime] Round {round}. Its repeat broken: {runtime.IsRepeatBroken(runtime.CurrentStage)}.");
                    }))
                .Stage(s => s
                    .WithName("Sealed")
                    .DisallowInjections()
                    .Task(async (d, t) =>
                    {
                        var runtime = d.Runtime;
                        ForgeLog.Info($"[Runtime] A skip, by the stage's rules: {(runtime.InjectStageLocal(SkipStageInjection.Instance) ? "taken" : "refused")}.");
                        // A timer applies its injection without asking, so this one ends the stage in a second
                        runtime.RequestStartStageTimeout(new SequenceTimeout { Seconds = 1f, Injection = SkipStageInjection.Instance });
                        await UniTask.Never(t);
                    }))
                .Stage(s => s
                    .WithName("Asked from outside")
                    .Task(async (d, t) =>
                    {
                        var runtime = d.Runtime;
                        var outside = new List<SequenceCondition>
                        {
                            new SequenceCondition((data, run) => Pressed(KeyCode.Space) && run.ElapsedTime > 0f, SkipStageInjection.Instance, fireOnce: true)
                        };
                        ForgeLog.Info("[Runtime] Space skips this stage, by a condition its definition doesn't have.");
                        while (true)
                        {
                            runtime.CheckConditions(outside);
                            await SequenceTaskLibrary.Yield(t);
                        }
                    }))
                .OnComplete((ctx, completed) =>
                {
                    var runtime = ctx.Runtime;
                    ForgeLog.Info($"[Runtime] Ended. Completed: {runtime.CompletedSuccessfully}. Failure: {runtime.FailureException?.Message ?? "none"}. {runtime.ElapsedTime:F1}s.");
                })
                .BuildSequence();
        }

        /// <summary>
        /// One TaskSequence can run any number of times at once, each run with a runtime of its own. Inject, Interrupt and
        /// SkipCurrentStage on the sequence act on every run of it, and Runtime is the newest run's; to act on one run, hold
        /// its runtime. The tasks of a definition are shared by all its runs, so a task object that keeps state of its own
        /// suits one run at a time: these keep theirs in each run's packet.
        /// </summary>
        [TaskSequenceMethod("Running: Every Run at Once")]
        public static TaskSequence EveryRunAtOnce()
        {
            var looping = TaskSequenceBuilder.Create("Looping")
                .Stage(s => s
                    .WithName("Waiting")
                    .Task((d, t) => UniTask.Never(t)))
                .Stage(s => s
                    .WithName("Looping")
                    .WithRepeat(true)
                    .Task(async (d, t) =>
                    {
                        await SequenceTaskLibrary.Delay(0.5f, t);
                        ForgeLog.Info($"[Runs] {d.GetPrimary<string>(NAME)} loops.");
                    }))
                .BuildSequence();

            return TaskSequenceBuilder.Create("Every Run at Once")
                .Task(async (d, t) =>
                {
                    var first = SequenceDataPacket.Default();
                    first.SetPrimary(NAME, "First");
                    var second = SequenceDataPacket.Default();
                    second.SetPrimary(NAME, "Second");

                    var runs = UniTask.WhenAll(looping.Run(first, t), looping.Run(second, t));
                    ForgeLog.Info($"[Runs] Running: {looping.IsRunning}. The newest run is at stage {looping.Runtime.StageIndex}.");

                    await SequenceTaskLibrary.Delay(1f, t);
                    ForgeLog.Info($"[Runs] Skipping both runs' stage: {looping.SkipCurrentStage()}.");
                    await SequenceTaskLibrary.Delay(1.6f, t);
                    ForgeLog.Info($"[Runs] Holding both for a second: {looping.Inject(new HoldInjection(1f))}.");
                    await SequenceTaskLibrary.Delay(2.1f, t);
                    ForgeLog.Info($"[Runs] Interrupting both: {looping.Interrupt()}.");
                    await runs;
                    ForgeLog.Info($"[Runs] Running: {looping.IsRunning}.");
                })
                .BuildSequence();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // CHAINS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A chain runs sequences one after another on one packet, so what one leaves in it the next reads. Then on a
        /// sequence starts a chain, and Then on a chain adds to it: a sequence, or one built in place.
        /// </summary>
        [TaskSequenceMethod("Chains: Then")]
        public static TaskSequenceChain ChainsThen()
        {
            var gather = TaskSequenceBuilder.Create("Gather")
                .Do(d =>
                {
                    d.SetPrimary(SCORE, 3);
                    ForgeLog.Info("[Chain] Gathered three herbs.");
                })
                .Delay(0.5f)
                .BuildSequence();

            var brew = TaskSequenceBuilder.Create("Brew")
                .Do(d => ForgeLog.Info($"[Chain] Brewing {d.GetPrimary<int>(SCORE)} herbs: sequence {d.ChainSequenceIndex + 1} of {d.ChainSequenceCount}."))
                .Delay(0.5f)
                .BuildSequence();

            var sell = TaskSequenceBuilder.Create("Sell")
                .Do(d => ForgeLog.Info($"[Chain] Sold for {d.GetPrimary<int>(SCORE) * 10} gold."))
                .BuildSequence();

            return gather
                .Then(brew)
                .Then(next => next
                    .WithName("Bottle")
                    .Do(_ => ForgeLog.Info("[Chain] Bottled."))
                    .Delay(0.5f))
                .Then(sell);
        }

        /// <summary>
        /// A chain's own conditions, asked each frame as a sequence's are: InterruptWhen ends the whole chain, and so does a
        /// ChainInterruptInjection; SkipSequenceWhen ends the running sequence and goes on with the next; InjectWhen applies
        /// any other injection to the running sequence, without asking its permissions. Inside, the packet reaches the
        /// chain: SkipSequence and InterruptChain. WithRepeat runs the chain again each time it gets through, and
        /// WithLogging logs each step it takes.
        /// </summary>
        [TaskSequenceMethod("Chains: Conditions and Control")]
        public static TaskSequenceChain ChainControl()
        {
            return Leg("Out")
                .Then(next => next
                    .WithName("Turn")
                    .Do(_ => ForgeLog.Info("[Laps] Turning."))
                    .Delay(1f))
                .Then(Leg("Back"))
                .Then(next => next
                    .WithName("Lap")
                    .Do(d =>
                    {
                        int laps = d.Increment(LAPS);
                        ForgeLog.Info($"[Laps] Lap {laps}, the chain's sequence {d.Chain.CurrentSequenceIndex + 1} of {d.Chain.SequenceCount} " +
                                      $"({d.Chain.CurrentSequence?.Definition.Metadata?.Name}). The chain runs: {d.Chain.IsRunning}, " +
                                      $"on this packet: {d.Chain.Data == d}.");
                        if (laps >= 3) d.InterruptChain();
                    }))
                .WithRepeat(true)
                .WithLogging(true)
                .WithConditionCheckTiming(EProcessStepTiming.Update)
                .InterruptWhen(_ => Pressed(KeyCode.Escape))
                .InjectWhen(_ => Pressed(KeyCode.Delete), ChainInterruptInjection.Instance, fireOnce: true)
                .SkipSequenceWhen(_ => Pressed(KeyCode.Tab))
                .InjectWhen(_ => Pressed(KeyCode.H), new HoldInjection(1f))
                .InjectWhen((d, runtime) => runtime != null && runtime.ElapsedTime > 1f && Pressed(KeyCode.Space), SkipStageInjection.Instance);
        }

        // A leg of the laps
        private static TaskSequence Leg(string name)
        {
            return TaskSequenceBuilder.Create(name)
                .Do(_ => ForgeLog.Info($"[Laps] {name}: Tab skips the leg, Space its wait after a second, H holds it, Escape or Delete end the laps."))
                .Task(async (d, t) =>
                {
                    await SequenceTaskLibrary.Delay(3f, t);
                    // A sequence can leave the rest of itself to the chain's next
                    if (ForgeRandom.Value >= 0.25f) return;
                    ForgeLog.Info($"[Laps] {name} cuts itself short.");
                    d.SkipSequence();
                })
                .Do(d => ForgeLog.Info($"[Laps] {name} done, the chain at its sequence {d.Chain?.CurrentIndex}."))
                .BuildSequence();
        }

        /// <summary>
        /// A chain run directly, as the Example Runner runs one: TryRun or Run awaits it, and whatever runs it asks its
        /// conditions each frame with CheckConditions, as ProcessControl would. Pause holds it and Resume lets it go;
        /// SkipCurrentSequence moves it on, Inject reaches its running sequence, and Interrupt ends it all.
        /// </summary>
        [TaskSequenceMethod("Chains: Run by Hand")]
        public static TaskSequence ChainByHand()
        {
            return TaskSequenceBuilder.Create("A Chain by Hand")
                .Stage(s => s
                    .WithName("Run the chain")
                    .OnInit(d => d.SetPrimary(CHAIN, new TaskSequenceChain()
                        .Then(Escapable("Chained, one"))
                        .Then(Escapable("Chained, two"))
                        .Then(Escapable("Chained, three"))
                        .Then(Escapable("Chained, four"))))
                    // Runs it, asking its conditions each frame
                    .Task(async (d, t) =>
                    {
                        var chain = d.GetPrimary<TaskSequenceChain>(CHAIN);
                        var run = chain.TryRun(new SequenceDataPacket(d), t);
                        while (!run.Status.IsCompleted())
                        {
                            chain.CheckConditions();
                            await SequenceTaskLibrary.Yield(t);
                        }
                        bool through = await run;
                        ForgeLog.Info($"[Chain by hand] Got through: {through}. Running: {chain.IsRunning}.");
                    })
                    // Works it from outside as it runs
                    .Task(async (d, t) =>
                    {
                        var chain = d.GetPrimary<TaskSequenceChain>(CHAIN);
                        await SequenceTaskLibrary.Delay(1f, t);
                        chain.Pause();
                        ForgeLog.Info("[Chain by hand] Paused for a second.");
                        await SequenceTaskLibrary.Delay(1f, t);
                        chain.Resume();
                        await SequenceTaskLibrary.Delay(0.5f, t);
                        ForgeLog.Info($"[Chain by hand] Skipping its running sequence: {chain.SkipCurrentSequence()}.");
                        await SequenceTaskLibrary.Delay(0.5f, t);
                        ForgeLog.Info($"[Chain by hand] Holding its running sequence: {chain.Inject(new HoldInjection(1f))}.");
                        await SequenceTaskLibrary.Delay(2f, t);
                        ForgeLog.Info($"[Chain by hand] Interrupting the chain: {chain.Interrupt()}.");
                    }))
                .BuildSequence();
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TASKS, A POLICY AND AN INJECTION OF YOUR OWN
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// An async-first task of your own: Execute is its work, and Prepare and Clean bracket each run of it, Clean however
    /// the run ended. SequenceTaskBase gives a Step that finishes at once, so in a synced sequence this task does nothing;
    /// override Step for one that should run there too. Its state is the task's own, so it suits one run at a time.
    /// </summary>
    public sealed class CountdownTask : SequenceTaskBase
    {
        private readonly int _from;
        private int _left;

        public CountdownTask(int from) => _from = from;

        public override void Prepare(SequenceDataPacket data) => _left = _from;

        public override async UniTask Execute(SequenceDataPacket data, CancellationToken token)
        {
            for (; _left > 0; _left--)
            {
                ForgeLog.Info($"[Countdown] {_left}...");
                await SequenceTaskLibrary.Delay(1f, token);
            }
        }

        public override void Clean(SequenceDataPacket data)
        {
            if (_left > 0) ForgeLog.Info($"[Countdown] Stopped with {_left} to go.");
        }
    }

    /// <summary>
    /// A sync-first task of your own: Step is its work, called with each frame's time until it returns true.
    /// SyncSequenceTaskBase runs Step once a frame in an async sequence, so the one task serves both kinds.
    /// </summary>
    public sealed class TallyTask : SyncSequenceTaskBase
    {
        private readonly float _seconds;
        private float _elapsed;
        private int _frames;

        public TallyTask(float seconds) => _seconds = seconds;

        public override void Prepare(SequenceDataPacket data)
        {
            _elapsed = 0f;
            _frames = 0;
        }

        public override bool Step(SequenceDataPacket data, float deltaTime)
        {
            _elapsed += deltaTime;
            _frames++;
            return _elapsed >= _seconds;
        }

        public override void Clean(SequenceDataPacket data) => ForgeLog.Info($"[Tally] {_frames} frames in {_elapsed:F2}s.");
    }

    /// <summary>
    /// A stage policy of your own: the stage lasts as long as its first task, the lead, and its others are cut off as
    /// the lead ends. It suits work with flourishes around it, an animation or a sound, that shouldn't hold the stage
    /// up. The runtime hands a policy it doesn't know a token source of its own, so cancelling it cuts off the
    /// stage's tasks and not the stage.
    /// </summary>
    public sealed class LeadTaskStagePolicy : ISequenceStagePolicy
    {
        public static readonly LeadTaskStagePolicy Instance = new();

        public async UniTask AwaitCompletion(TaskSequenceRuntime runtime, SequenceStage stage, UniTask[] taskUnits,
            CancellationTokenSource stageCts)
        {
            if (taskUnits.Length == 0) return;

            try
            {
                await taskUnits[0];
            }
            finally
            {
                // The others are cut off and let go: Forget reports an error of theirs, and lets a cancellation pass
                stageCts.Cancel();
                for (int i = 1; i < taskUnits.Length; i++) taskUnits[i].Forget();
            }
        }
    }

    /// <summary>
    /// An injection of your own: holds the run for a moment, then lets it go, as a hit-stop does. An injection is handed
    /// the runtime and can do anything with it; it returns true when it took effect. Permissions name injections by
    /// type, so AllowInjections(typeof(HoldInjection)) lets this one in. It resumes the run whatever paused it, so it
    /// doesn't mix with other pauses.
    /// </summary>
    public sealed class HoldInjection : ISequenceInjection
    {
        public readonly float Seconds;

        public HoldInjection(float seconds) => Seconds = seconds;

        public bool Apply(TaskSequenceRuntime runtime)
        {
            // Already held, it adds nothing
            if (runtime.IsPaused) return false;

            runtime.Pause();
            ReleaseLater(runtime).Forget();
            return true;
        }

        // Counted on the unscaled clock, so a hold lasts its length while the game's time is slowed, and a step of it is
        // Unity's next frame where the fixed tick would stand with the game's time
        private async UniTaskVoid ReleaseLater(TaskSequenceRuntime runtime)
        {
            float held = 0f;
            while (held < Seconds && runtime.IsRunning)
            {
                await ForgeTick.Next(UnscaledClock.Instance);
                held += UnscaledClock.Instance.DeltaTime;
            }
            runtime.Resume();
        }
    }
}
