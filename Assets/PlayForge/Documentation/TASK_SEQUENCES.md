# Task Sequences

A **task sequence** is a workflow over time, built in code: stages run one after another, each a set of tasks run
together. An ability runs as one, and so can anything with steps: a projectile's flight, a channel, a cutscene, a
tutorial, a boss's phases. A sequence runs as a process ([PROCESSES.md](PROCESSES.md)), so ProcessControl starts,
pauses and ends it like any other.

The **Task Sequence Examples** sample shows every part of this page in code, and a component that runs any example from
a scene.

## The parts

- **Sequence**: its stages, in order, with what holds for all of them: its name, its conditions, a timeout or a longest
  run, whether it repeats, and the handlers that hear it end.
- **Stage**: its tasks, run together, and its **policy**, which says when the stage is done: when all its tasks are
  (`WhenAll`, the default), any one (`WhenAny`), or a number of them (`WhenN`). A stage can hold stages of its own
  (`SubStage`), or be a **branch** that picks one stage by a condition.
- **Task**: one unit of work, awaited (`Task`), done at once (`Do`), stepped a frame at a time (`SyncTask`), or a class of
  its own (`ISequenceTask`, or `SequenceTaskBase` and `SyncSequenceTaskBase` to start from).
- **Injection**: a change to the run from outside its tasks: interrupt it, skip a stage, jump to one, break a repeat.
  A **condition** injects one when its predicate holds, checked each frame.
- **Chain**: sequences run one after another with one data packet.

## Building one

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

`Build` gives the definition, which a `TaskSequence` runs and a synced runner is built from; `BuildSequence` gives the
sequence itself, and `BuildSyncRunner` a synced runner (below). A static method marked `[TaskSequenceMethod]` that
returns a sequence or a chain appears in an ability's sequence dropdown, and in a `TaskSequenceReference` field.

A task reads and writes the run's **data packet**, a `SequenceDataPacket`: what started the run put its caster, targets
and values there, and a task can leave values for the stages after it (`SetPrimary`, `GetPrimary`).

## Waiting

- **Delays**: `Delay(seconds)` is a stage of its own that waits; `DelayUntil` and `DelayWhile` wait on a predicate, and
  `WaitUntil` is a stage that does. `SyncDelay` waits inside a stage, as one of its tasks.
- **The library's waits**, inside a task: `SequenceTaskLibrary.Delay`, `WaitFrames`, `Yield`, `WaitUntil` and `WaitWhile`.
- **The player's input**, read through `ForgeInput`, so a game's own input or a test's stands in: `WaitForKey`,
  `WaitForAnyKey`, which returns the key pressed, and `WaitForPointer`.

Each of the library's waits holds while its sequence is paused, and a press while it's paused isn't heard. A task that
keeps time itself should read `SequenceTaskLibrary.DeltaTime(token)`, which is 0 while the sequence is paused and runs at
the caster's own time scale.

## Repeating

- `WithRepeat(true)` on the builder repeats the whole sequence until it's interrupted.
- `WithRepeat(true)` on a stage repeats the stage until it's broken (`data.BreakStageRepeat()`, `StopRepeatWhen`),
  skipped or interrupted; `WithRepeatAndDelay(seconds)` waits between repeats.
- `WithRepeat(3)` on a stage runs it three times in all, then the sequence moves on. A break, a skip or an interrupt
  still ends it sooner.

A stage's `OnRepeat` hears each repeat, and `runtime.GetRepeatCount(stage)` says how many runs are done.

## Changing the run

From a task, through its data packet:

```csharp
data.SkipStage();               // end this stage, on to the next
data.SkipAndMaintain();         // on to the next, leaving this one's tasks running
data.JumpToStage("Impact");     // or by index
data.RestartSequence();
data.BreakStageRepeat();
data.Interrupt();               // end the run
data.SetStageTimeout(2f);       // this stage gets two seconds
```

From outside, through the sequence or its runtime: `Inject(injection)`, `Interrupt()`, `SkipCurrentStage()`.

By a condition, checked each frame: `InterruptWhen`, `SkipStageWhen` on the sequence, `SkipWhen` and `StopRepeatWhen` on
a stage, and `InjectWhen` for any injection. A condition fires once or every time it holds.

What may change a run is the sequence's to say: `AllowInjections(types)`, `AllowOnlyInterrupt()` and
`DisallowInjections()`, on the sequence or a stage. A **critical** stage (`WithCriticalFlag(true)`) refuses every
injection but those it names (`WithCriticalAllowedInjections`), and `OnCriticalSectionExited` hears the last critical
stage end.

## Time limits and endings

- `WithTimeout(seconds, injection)` injects when the time is up, a skip by default; `WithMaxDuration(seconds)` ends the
  run, or the stage, at its longest. `OnTimeout` and `OnMaxDuration` hear them.
- `OnException` hears a task's throw, and says whether the run goes on.
- `OnComplete` hears the run end and whether it finished; `OnTerminate` hears it end however it does. A stage has its
  own `OnInit`, `OnRepeat` and `OnTerminate`.

## Running one

- `RegisterAndRun(data)`, or `ProcessControl.Register(sequence).With(data).Run()`, runs it as a process: it shows in the
  Debugger, pauses with ProcessControl, and ends with its scene.
- `await sequence.Run(data, token)` runs it in place, from code that's async already, and `TryRun` says whether it
  finished.
- `CreateRuntime` gives the runtime that runs it, to pause, resume, inject and read: its stage, its time, whether it
  finished, what it threw.
- `TaskSequenceProcess` wraps a sequence, a chain or a synced runner as a process of a lifecycle and timing given.

## Chains

```csharp
// countdown and fight are TaskSequences; the last is built in place
var round = countdown.Then(fight).Then(builder => builder.WithName("Results").Stage(stage => stage.Do(ShowResults)));
await round.Run(SequenceDataPacket.SceneRoot());
```

A chain runs its sequences in order with one data packet; it can repeat, pause and resume, skip its current sequence, or
be interrupted, and takes conditions of its own (`InterruptWhen`, `SkipSequenceWhen`). From inside, `data.Chain` is the
chain, and `data.InterruptChain()` ends it.

## Synced sequences

A **synced runner** is the same sequence stepped a frame at a time, with no async: a process with the Synchronous
lifecycle calls its `Step` each frame. It suits what runs for good, a patrol, a regeneration, a bob, and costs nothing
but the calls.

```csharp
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

runner = TaskSequenceBuilder.Create("Regenerate")
    .WithRepeat(true)
    .Stage(stage => stage.Task(SyncSeq.Delay(1f)))
    .Stage(stage => stage.Do(_ => Heal(5)))
    .BuildSyncRunner();

// In the process's WhenUpdate
runner.Step(data, ForgeTime.DeltaTime);
```

A synced runner runs stages, their tasks, the three policies, repeats with or without a count, critical stages and a
stage's longest run. A sub-stage, a branch, a condition, the sequence's own timeout or longest run, or an async task it
can't, and building a runner from a sequence that holds any says so. It's changed by hand: `Interrupt`, `SkipStage`, `JumpToStage`, `BreakStageRepeat` and `Reset`.

## The libraries

`SequenceTaskLibrary` has tasks to await inside a stage's `Task`, each taking the task's token, and
`SyncSequenceTaskLibrary` the same tasks as `ISequenceTask`s for a synced runner, or for any stage:

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

Only the async library has `ArcForce`, which flies a Rigidbody by physics between FixedUpdates, whatever a runner's
timing; `ArcTo` moves a transform along the same arc, in either. `IsPaused(token)` and `DeltaTime(token)` read the run
that holds the token, which a synced task has none of.

A timed task takes an `AnimationCurve` for its easing, ends exactly on its value, and ends at once for a time of 0 or less.

## Logging

What a sequence says goes through `ForgeLog`, under its own system, **Sequences**: a handler's throw as an error, and,
where a sequence's or a chain's logging is on (`WithInjectionLogging`, a chain's `WithLogging`), what it does as
information. **Preferences > PlayForge > Logging** sets the quietest message every system logs, and
`ForgeLog.SetLevel(ForgeLog.ESystem.Sequences, …)` gives the sequences a level of their own, to hear more or less of
them than the rest.
