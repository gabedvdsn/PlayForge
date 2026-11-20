# Task Sequences

A **task sequence** is a workflow over time, built in code. Its stages run one after another, and each stage is a set
of tasks run together.

Anything with steps can run as a task sequence: a projectile's flight, a channel, a cutscene, a tutorial, a boss's
phases. In the full package, an ability runs as one too.

A sequence runs as a process ([PROCESSES.md](PROCESSES.md)), so ProcessControl starts, pauses and ends it like any
other.

> [!TIP]
> The **Task Sequence Examples** sample shows every part of this guide in code. It also has a component that runs any
> example from a scene.

## The parts

A task sequence is built from five parts: sequences, stages, tasks, injections and chains.

| Part | What it is |
| --- | --- |
| **Sequence** | Its stages, in order, with what holds for all of them: its name, its conditions, a timeout or a longest run, whether it repeats, and the handlers that hear it end. |
| **Stage** | Its tasks, run together, and its **policy**, which says when the stage is done. |
| **Task** | One unit of work. |
| **Injection** | A change to the run from outside its tasks: interrupt it, skip a stage, jump to one, break a repeat. |
| **Chain** | Sequences run one after another with one data packet. |

A stage's policy says when the stage is done:

| Policy | The stage is done when |
| --- | --- |
| `WhenAll` (the default) | All its tasks are done. |
| `WhenAny` | Any one of its tasks is done. |
| `WhenN` | A number of its tasks are done. |

A stage can also hold stages of its own (`SubStage`), or be a **branch** that picks one stage by a condition.

A task is one of these:

| Task | How it runs |
| --- | --- |
| `Task` | Awaited. |
| `Do` | Done at once. |
| `SyncTask` | Stepped a frame at a time. |
| A class of its own | `ISequenceTask`, or `SequenceTaskBase` and `SyncSequenceTaskBase` to start from. |

A **condition** injects one injection when its predicate holds, checked each frame.

## Building one

You build a sequence in code, stage by stage, with `TaskSequenceBuilder`.

```csharp
var cast = TaskSequenceBuilder.Create("Fireball")
    .WithMaxDuration(5f)
    .Stage(stage => stage
        .WithName("Wind Up")
        .Task((data, token) => SequenceTaskLibrary.PlayAnimationState(animator, "Cast", token)))
    .Stage(stage => stage
        .WithName("Flight")
        .Task((data, token) => SequenceTaskLibrary.ArcTowards(fireball, target, 20f, 2f, token)))
    .Stage(stage => stage
        .WithName("Impact")
        .Do(data => ForgeLog.Info("Fireball lands")))
    .BuildSequence();

cast.RegisterAndRun(SequenceDataPacket.SceneRoot());
```

The builder ends with one of three methods:

| Method | Gives |
| --- | --- |
| `Build` | The definition, which a `TaskSequence` runs and a synced runner is built from. |
| `BuildSequence` | The sequence itself. |
| `BuildSyncRunner` | A synced runner ([Synced sequences](#synced-sequences)). |

A static method marked `[TaskSequenceMethod]` that returns a sequence, a chain or a definition appears in two places:

- in the full package, an ability's sequence dropdown;
- a `TaskSequenceReference` field.

In the full package, an ability runs a chain as a process too, which asks its conditions each frame.

### The data packet

A task reads and writes the run's **data packet**, a `SequenceDataPacket`.

- What started the run put its caster, targets and values there.
- A task can leave values for the stages after it (`SetPrimary`, `GetPrimary`).

## Waiting

A sequence can wait as a stage, inside a task, or on the player's input.

**Delays** wait as a stage, or inside one:

| Delay | What it does |
| --- | --- |
| `Delay(seconds)` | A stage of its own that waits. |
| `DelayUntil`, `DelayWhile` | Wait on a predicate. |
| `WaitUntil` | A stage that waits on a predicate. |
| `SyncDelay` | Waits inside a stage, as one of its tasks. |

**The library's waits** go inside a task:

| Wait | Waits for |
| --- | --- |
| `SequenceTaskLibrary.Delay` | A number of seconds. |
| `WaitFrames` | A number of frames. |
| `Yield` | One frame. |
| `WaitUntil` | A predicate to hold. |
| `WaitWhile` | A predicate to stop holding. |

**The player's input** is read through `ForgeInput`, so a game's own input or a test's stands in:

| Wait | Waits for |
| --- | --- |
| `WaitForKey` | A key to go down. |
| `WaitForAnyKey` | Any of the keys given, and returns the key pressed. |
| `WaitForPointer` | A pointer's button to go down. |

Each of the library's waits holds while its sequence is paused. A press while it's paused isn't heard.

A task that keeps time itself should read `SequenceTaskLibrary.DeltaTime(token)`. It's a step of its run's clock
([Clocks](#clocks)), and it's 0 while the sequence is paused.

### Linking a token

A task can link its token, to cancel a wait of its own without its stage.

- Link it through `TaskSequenceRuntime.CreateLinkedTokenSource(token)`. Its token belongs to the task's run, so a wait
  on it keeps the run's clock, and holds while the run is paused.
- A token that `CancellationTokenSource.CreateLinkedTokenSource` links belongs to no run. Its waits go by the game's
  clock, and don't hold.

Dispose of the source once you're done, as with any:

```csharp
.Task(async (data, token) =>
{
    // Cancelled with the stage, or on its own through charge.Cancel(), and by the run's clock either way
    using var charge = TaskSequenceRuntime.CreateLinkedTokenSource(token);
    await SequenceTaskLibrary.Delay(1.5f, charge.Token);
})
```

## Repeating

A sequence, or one of its stages, can repeat.

| Call | On | What it does |
| --- | --- | --- |
| `WithRepeat(true)` | The builder | Repeats the whole sequence until it's interrupted. |
| `WithRepeat(true)` | A stage | Repeats the stage until it's broken (`data.BreakStageRepeat()`, `StopRepeatWhen`), skipped or interrupted. |
| `WithRepeatAndDelay(seconds)` | A stage | Repeats the stage in the same way, and waits between repeats. |
| `WithRepeat(3)` | A stage | Runs the stage three times in all, then the sequence moves on. A break, a skip or an interrupt still ends it sooner. |

A stage's `OnRepeat` hears each repeat, and `runtime.GetRepeatCount(stage)` says how many runs are done.

## Changing the run

You can change a run from one of its tasks, from outside, or by a condition, as far as the sequence allows.

### From a task

A task changes its run through its data packet.

```csharp
data.SkipStage();               // end this stage, on to the next
data.SkipAndMaintain();         // on to the next, leaving this one's tasks running
data.JumpToStage("Impact");     // or by index
data.RestartSequence();
data.BreakStageRepeat();
data.Interrupt();               // end the run
data.SetStageTimeout(2f);       // this stage gets two seconds, then it's skipped
```

`SkipAndMaintain` keeps the stage that is current as it's called running on. Called again later, it keeps that later
stage running on as well.

### From outside

Code outside the run changes it through the sequence or its runtime.

| Call | What it does |
| --- | --- |
| `Inject(injection)` | Injects an injection. |
| `Interrupt()` | Interrupts the run. |
| `SkipCurrentStage()` | Skips the current stage. |

### By a condition

A condition injects when its predicate holds, and is checked each frame.

| Condition | Set on | What it injects |
| --- | --- | --- |
| `InterruptWhen` | The sequence | An interrupt. |
| `SkipStageWhen` | The sequence | A skip of the current stage. |
| `SkipWhen` | A stage | A skip of that stage. |
| `StopRepeatWhen` | A stage | A break of its repeat. |
| `InjectWhen` | The sequence or a stage | Any injection. |

A condition fires once or every time it holds.

A stage's condition is asked each frame by the process. So where a repeating stage's round ends, the next may have
begun before `StopRepeatWhen` hears its predicate hold. A break from a task, or from `OnRepeat`, stops at the round
exactly.

### What may change a run

What may change a run is the sequence's to say, on the sequence or on a stage.

| Call | Allows |
| --- | --- |
| `AllowInjections(types)` | Only the injection types given. |
| `AllowOnlyInterrupt()` | Only an interrupt. |
| `DisallowInjections()` | No injection. |

A **critical** stage (`WithCriticalFlag(true)`) refuses every injection but those it names
(`WithCriticalAllowedInjections`). `OnCriticalSectionExited` hears the last critical stage end.

`InjectStageLocal(injection, stage)` asks the given stage's permissions, and the injection acts on the current stage.

## Time limits and endings

A run can have time limits, and handlers that hear how it ends.

| Limit | What it does |
| --- | --- |
| `WithTimeout(seconds, injection)` | Injects when the time is up, a skip by default. |
| `WithMaxDuration(seconds)` | Ends the run, or the stage, at its longest. |

| Handler | What it hears |
| --- | --- |
| `OnTimeout` | The timeout. |
| `OnMaxDuration` | The longest run. |
| `OnException` | A task's throw. It says whether the run goes on. |
| `OnComplete` | The run end, and whether it finished. |
| `OnTerminate` | The run end, however it ends. |

A stage has its own `OnInit`, `OnRepeat` and `OnTerminate`.

## Clocks

A run's time is its **clock**'s. These all go by it:

- its waits and delays;
- its timeouts and longest runs;
- its `ElapsedTime`;
- the library's tweens;
- its synced tasks.

A run takes the first clock it finds:

1. Its own, `runtime.Clock`, which whatever runs it sets: a chain's `Clock`, a `TaskSequenceProcess`'s, or, in
   the full package, an ability's Run Sequence task's pick. So a packet others share keeps its own.
2. Its packet's, `data.Clock`, which whatever starts the run sets.
3. Its sequence's own, `WithClock(clock)` on the builder.
4. Its packet's owner's, the `ProcessClock` of an `IProcessOwner` that has one: in the full package, an entity's own
   clock, as an ability's caster's is, its time scale times the game's.
5. The game's, `GameClock.Instance`, which reads the clock in force, `ForgeTime.Clock`, as it's asked.

### Clocks you can use

Besides the game's clock, a run can go by an unscaled clock, a scaled clock, a clock of your own or, in the full
package, an entity's clock.

```csharp
// A pause menu's runs while the game's time stands
var menu = TaskSequenceBuilder.Create("Menu").WithClock(UnscaledClock.Instance) /* … */;

// A finisher's slow motion: every sequence on the clock slows, speeds up or stops with it
var slowMotion = new ScaledClock(0.25f);
var finisher = TaskSequenceBuilder.Create("Finisher").WithClock(slowMotion) /* … */;

// One run on another clock, from its packet: the finisher's here; in the full package, an entity's, gas.GetClock()
var data = SequenceDataPacket.Default();
data.Clock = slowMotion;
```

- `UnscaledClock.Instance` reads the game's time without its time scale.
- A `ScaledClock` runs at a `Scale` of its own over its `Source`, the clock in force unless it's given one. It counts
  its own `Time` through each change of scale.
- In the full package, an entity's `EntityClock` is a clock as any other is.
- A clock of your own implements `IForgeClock`, and is read as it is.

The clocks above read the clock in force, so a world of its own's steps and the fixed tick still drive them. A clock
that reads Unity's time itself doesn't follow them.

### Reading a run's clock

Code reads a run's clock from its runtime, from a task's token, or from a synced runner.

- `runtime.Clock` is a run's clock.
- `TaskSequenceRuntime.ClockOfRun(token)` is the clock of the run a task's token belongs to.
- A synced runner takes its sequence's clock (`runner.Clock`), and a packet's before it (`runner.ClockFor(data)`).
  `runner.Step(data)` steps by it.

### Picking a clock in an inspector

An inspector picks a clock with an `AbstractClockChoice` field, marked `[SerializeReference]`.

| Choice | The clock a run goes by |
| --- | --- |
| None | The clock it would have had. |
| **Caster's** | The caster's own. |
| **Game's** | The game's. |
| **Unscaled** | The game's time without its time scale. |
| **Scaled** | A pace of its own over the clock it picks: the caster's, unless it picks another. |

A `[Serializable]` class of your own made from it is offered beside them. Its `ClockFor(data)` gives the clock for a
run on a packet.

These have a clock choice:

- In the full package, an ability's Run Sequence task. It gives the sequence it runs a clock of its own, and leaves
  the ability's other tasks the caster's.
- The Task Sequence Examples' components.
- In the full package, a Sequence cue. Its run goes by its pick, else the sequence's own clock, else that of the one
  the cue plays on.

### Clocks and fixed ticks

Under ProcessControl's fixed tick, a sequence waits for the next tick. The ticks run on the game's time, so they stand
with it.

- A clock that runs while the game's time stands says so: its `FollowsTicks` is false, as the unscaled clock's is. Its
  sequences wait for Unity's frames instead.
- Code outside a sequence awaits `ForgeTick.Next(clock)` for the next step of a clock's time.
- A synced sequence run as a process steps with ProcessControl, by its ticks, whatever its clock.

## Running one

You can run a sequence as a process, in place, or through a runtime of your own.

| Way | What it does |
| --- | --- |
| `RegisterAndRun(data)`, or `ProcessControl.Register(sequence).With(data).Run()` | Runs it as a process: it shows in the Debugger, pauses with ProcessControl, and ends with its scene. |
| `await sequence.Run(data, token)` | Runs it in place, from code that's async already. `TryRun` says whether it finished. |
| `CreateRuntime` | Gives the runtime that runs it, to pause, resume, inject and read: its stage, its time, whether it finished, what it threw. |
| `TaskSequenceProcess` | Wraps a sequence, a chain or a synced runner as a process of a lifecycle and timing given. |

## Chains

A chain runs its sequences in order, with one data packet.

```csharp
// countdown and fight are TaskSequences; the last is built in place
var round = countdown.Then(fight).Then(builder => builder.WithName("Results").Stage(stage => stage.Do(ShowResults)));
await round.Run(SequenceDataPacket.SceneRoot());
```

A chain can:

- repeat;
- pause and resume;
- skip its current sequence;
- be interrupted.

It takes conditions of its own (`InterruptWhen`, `SkipSequenceWhen`). From inside, `data.Chain` is the chain, and
`data.InterruptChain()` ends it.

## Synced sequences

A **synced runner** is the same sequence stepped a frame at a time, with no async. A process with the Synchronous
lifecycle calls its `Step` each frame.

A synced runner suits what runs for good, as a patrol, a regeneration or a bob. It costs nothing but the calls.

```csharp
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

runner = TaskSequenceBuilder.Create("Regenerate")
    .WithRepeat(true)
    .Stage(stage => stage.Task(SyncSeq.Delay(1f)))
    .Stage(stage => stage.Do(_ => Heal(5)))
    .BuildSyncRunner();

// In the process's WhenUpdate: a step of its clock, or Step(data, deltaTime) for a time of your own
runner.Step(data);
```

A synced task works in an async sequence too: it's stepped once a frame, and held while the sequence is paused. Its
first step comes in the frame its stage starts, so `SyncSequenceTaskLibrary.Yield` waits no frame there. The async
`Yield` does.

### What a synced runner runs

A synced runner runs only part of what an async sequence can.

| Part of a sequence | Synced runner |
| --- | --- |
| Stages and their tasks | Runs it |
| The three policies | Runs it |
| Repeats, with or without a count | Runs it |
| Critical stages | Runs it |
| A stage's longest run | Runs it |
| A sub-stage | Can't |
| A branch | Can't |
| A condition | Can't |
| The sequence's own timeout or longest run | Can't |
| An async task | Can't |

Building a runner from a sequence that holds any it can't says so.

A synced runner is changed by hand: `Interrupt`, `SkipStage`, `JumpToStage`, `BreakStageRepeat` and `Reset`.

## The libraries

Two libraries hold ready-made tasks, one for async stages and one for synced ones.

- `SequenceTaskLibrary` has tasks to await inside a stage's `Task`, each taking the task's token.
- `SyncSequenceTaskLibrary` has the same tasks as `ISequenceTask`s, for a synced runner or for any stage.

| Kind | Tasks |
| --- | --- |
| Position | `MoveTo`, `MoveBy`, `MoveLocalTo`, `MoveAlongPath`, `ArcTo`, `Dash`; following a transform, `MoveTowards`, `ArcTowards`, `Orbit`, `MoveAlongPathToTarget` |
| Rotation | `RotateTo`, `RotateBy`, `LookAt`, and `LookAtTracking`, which follows |
| Scale | `ScaleTo`, `PunchScale`, and `TransformTo`, position, rotation and scale at once |
| Looks | `FadeAlpha`, `ColorTo`, `MaterialColorTo`, `MaterialFloatTo` |
| Sound and animation | `PlayAndWait`, `FadeVolume`, `PlayAnimationState`, `LerpAnimFloat` |
| UI Toolkit | `SlideElement`, `FadeElement`, `ScaleElement`, `ColorElement`, each with a `…To` from where the element is; `PunchScaleElement`, `WidthTo`, `HeightTo`, `TypeText`, `TypeTextSkippable` |
| Any value | `Lerp`, `LerpFloat`, `LerpVector3`, `LerpColor` |
| Waiting | `Delay`, `WaitFrames`, `Yield`, `WaitUntil`, `WaitWhile`, `WaitForKey`, `WaitForAnyKey`, `WaitForPointer` |

The two libraries differ in a few ways:

- Only the async library has `ArcForce`, which flies a Rigidbody by physics between FixedUpdates, whatever a runner's
  timing. `ArcTo` moves a transform along the same arc, in either library.
- `IsPaused(token)` and `DeltaTime(token)` read the run that holds the token. A synced task has no token.

A timed task takes an `AnimationCurve` for its easing. It ends exactly on its value, and ends at once for a time of 0
or less.

## Logging

What a sequence says goes through `ForgeLog`, under its own system, **Sequences**.

- A handler's throw is logged as an error.
- Where a sequence's or a chain's logging is on (`WithInjectionLogging`, a chain's `WithLogging`), what it does is
  logged as information.

**Preferences > PlayForge > Logging** sets the quietest message every system logs.
`ForgeLog.SetLevel(ForgeLog.ESystem.Sequences, …)` gives the sequences a level of their own, to hear more or less of
them than the rest.
