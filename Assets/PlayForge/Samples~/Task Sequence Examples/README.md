# PlayForge Task Sequence Examples

Examples of PlayForge's task sequences, written to be read and run: how a sequence is built, what each of its parts
does, gameplay mechanics made from those parts, and every helper of the two task libraries. They compile into their
own assembly, `FarEmerald.PlayForge.Samples.TaskSequences`, and need nothing beyond PlayForge.

A sequence is a list of stages, run one after another. A stage is a set of tasks, run together, which ends as its
policy says. A run's data travels in its packet, a `SequenceDataPacket`, and each task is handed a cancellation token,
which is how a skip, an interrupt or a timeout stops it. The package's `Documentation/TASK_SEQUENCES.md` describes the
framework; these examples show it working.

## The files

| File | What it shows |
| --- | --- |
| `TaskSequenceExamples.cs` | The framework a feature at a time: building, every kind of task, delays and waits, stage policies, sub-stages, branches, repeats, conditions and injections and the permissions over them, critical sections, timeouts and max durations, clocks of a sequence's own, exceptions, lifecycle hooks, metadata, the data packet, every way to run a sequence, chains, pausing, and the runtime's own members. It ends with a task, a stage policy and an injection of your own. |
| `TaskSequenceExamples_Mechanics.cs` | Gameplay mechanics built from those features: a channeled beam, charges, a recall, a combo chain, a respawn timer with a buy-back, a boss that enrages, a spell shield, a toggled drain, ground targeting, a charged shot, a recast, a meteor shower, and a turn-based duel. |
| `TaskSequenceLibraryExamples.cs` | Every helper of `SequenceTaskLibrary` and `SyncSequenceTaskLibrary`, each async helper beside its synced twin. |
| `TaskSequenceLibraryShowcase.cs` | The Library Showcase component, which runs them on a scene's objects. |
| `SyncSequenceExamples.cs` | What the synced scene components share, `SyncedSequenceComponent`, a `LazyMonoProcess` with the Synchronous lifecycle, and `SyncedChoreography`, a `LazyRuntimeProcess` that code starts. |
| `SyncedTicker.cs` to `SyncedSignal.cs` | Synced sequences as scene components, each in a script of its own name, as Unity adds and saves a component by its script: a ticker, a patrol, a chase, a bob, an orbit, a burn, a pulse and a signal. |
| `TaskSequenceExampleRunner.cs` | A component that runs any example from a scene, on a key. |

Each method marked `[TaskSequenceMethod]` builds a sequence or a chain and returns it without running it. Their names
begin with what they show, Basics, Tasks, Waits, Stages, Flow, Timers, Errors, Hooks, Data, Running, Chains, Mechanic
and Library, so a dropdown sorts them together.

## Running them

Every way needs a ProcessControl up in the scene, as a **Process Bootstrapper** brings it. A **Bootstrapper** brings
one too, with the GameRoot that entities and abilities need. Each example says what it does, and which keys it waits
for, in the Console.

### From an ability

With the full package, an ability's **Sequence** task lists every `[TaskSequenceMethod]` in its dropdown. Pick an
example there, and the ability runs it as it activates, on the ability's own packet. PlayForge Core has no abilities,
so it runs them the other ways.

### With the runner

Add **Example Runner** (Add Component > PlayForge > Samples > Task Sequences) to any object, pick an example in its
Sequence field, enter Play mode, and press **R** to run it, **P** to pause and resume it, and **Backspace** to stop it.
The keys are its fields. Each run takes a fresh sequence and a fresh packet. A sequence runs through ProcessControl;
a chain runs on the component, which asks its conditions each frame.

### The library

Add **Library Showcase** (Add Component > PlayForge > Samples > Task Sequences) to any object, give it the objects its
group needs, a Subject and a Target to move, a sprite, an audio source and clip, an animator, a rigidbody or a UI
document, pick the group, and press **Return**. The Lerps and the Timing and Input groups need nothing, and are in the
sequence dropdown as well.

### The synced components

Add any of the **Synced** components (Add Component > PlayForge > Samples > Task Sequences) to an object and fill in
its fields. ProcessControl registers every process in the scene as it starts, so there's nothing more to do: each runs
in Play mode. Their Lifecycle and Timing are in each one's Process section. `SyncedChoreography` is a runtime process,
with no object, so code starts it: `SyncedChoreography.Start(transform)`.

## Input, logs and time

The examples read keys and the pointer through `ForgeInput`, so they work with the Input System or the Input Manager,
and with a game's own input put in with `ForgeInput.Use`. They log through `ForgeLog`; a few chatty lines are Verbose,
which show once its level is lowered in Preferences > PlayForge > Logging. They roll through `ForgeRandom`, so a seeded source gives
the same rolls each run. Inside a task they keep time through `SequenceTaskLibrary`, whose waits hold while their
sequence is paused and which go by the run's clock; outside one, through `ForgeTime`. The Timers: Clocks example runs
a sequence on the unscaled clock and two on a `ScaledClock` the S key slows. The synced components and the Example
Runner pick a clock in their Clock field, as Unscaled for a menu's, which their run's packet carries.
