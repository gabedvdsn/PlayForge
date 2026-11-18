# PlayForge — Time, Randomness, Input and Logging

PlayForge reads the game's time, draws its random numbers and reads the player's pointer through services of its own,
`ForgeTime`, `ForgeRandom` and `ForgeInput`, and says what it does through a fourth, `ForgeLog`. The first three read
Unity's own until code puts another in, with `ForgeTime.Use`, `ForgeRandom.Use` or `ForgeInput.Use`, as a test, a
replay, a server or a game with input of its own does, and each returns the one it replaced, to put back. This guide is
the reference behind the short sections of [GETTING_STARTED.md](GETTING_STARTED.md#time-and-random-numbers) on time,
logging and profiling: the clocks and which one a run takes, the fixed tick, chances and luck, pointer input, log
levels, the Profiler's markers and counters, an entity's own services, and the small utilities a game's code uses.

## Time

`ForgeTime` is the clock PlayForge's runtime reads in place of Unity's `Time`, and where a game puts in a time of its
own.

It reads the clock in force, `ForgeTime.Clock`, an `IForgeClock`: Unity's, `UnityClock.Instance`, until code puts
another in. Nothing else in the runtime reads Unity's `Time`. Its properties read that clock as Unity's own do:

| `ForgeTime` | As Unity's |
| --- | --- |
| `DeltaTime` | `Time.deltaTime`: the frame's step, scaled |
| `UnscaledDeltaTime` | `Time.unscaledDeltaTime` |
| `FixedDeltaTime` | `Time.fixedDeltaTime`: the fixed step |
| `Time` | `Time.time`: the time since the game started, scaled |
| `UnscaledTime` | `Time.unscaledTime` |
| `FrameCount` | `Time.frameCount` |

`ForgeTime.Use(clock)` puts a clock in and returns the one it replaced, and `ForgeTime.Use(null)` puts Unity's back. Two
things put a clock of their own in while they step, and the one before back after: each of ProcessControl's fixed ticks
([Fixed ticks](#fixed-ticks)), and a world of its own, a `ProcessWorld` its owner steps with `Step(deltaTime)`, whose
clock reads that step.

An owner's time is its own where it has one:

- `ForgeTime.ClockOf(owner)` is an entity's own clock for an entity, an `IGameplayAbilitySystem`, and the game's,
  `GameClock.Instance`, for anything else. `ForgeTime.DeltaTimeFor(owner)` is its step this frame.
- `ForgeTime.ClockFor(data)` is the clock a data packet names, `data.Clock`, or else its owner's: an ability's caster's,
  or the game's. `ForgeTime.DeltaTimeFor(data)` is its step.

```csharp
using FarEmerald.PlayForge;
using UnityEngine;

// PlayForge's time apart from Unity's: at a Scale of 0 every effect, cooldown and sequence on the game's time stands
// still, while Unity's own time, and all that reads it, runs on, and so does a sequence on the unscaled clock
public static class GameplayTime
{
    public static ScaledClock Clock { get; private set; }

    // Put in before the first scene loads, so all that keeps a time keeps it by this clock from the start
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PutIn()
    {
        Clock = new ScaledClock(1f, UnityClock.Instance);
        ForgeTime.Use(Clock);
    }
}

// A menu that stops the game's time sets GameplayTime.Clock.Scale to 0 as it opens, and back to 1 as it closes
```

A clock put in reads its time from somewhere else. `GameClock`, `UnscaledClock`, an `EntityClock` and a `ScaledClock`
with no source each read the clock in force, so put in as it, each would read itself: `ForgeTime.Use` refuses the
`GameClock`, and a `ScaledClock` over `UnityClock.Instance`, as above, scales Unity's. What waits until a time, as a
pooled cue's burst does, keeps `ForgeTime.Time`, so a clock put in while the game runs should carry its `Time` on from
the one it replaces. A clock put in stays until code puts another in, past the end of play mode where domain reload is
off, so code that puts one in puts back the one it replaced.

## Clocks

A clock is an `IForgeClock`, a source of time: PlayForge has one for Unity's time, one for the game's, an unscaled one,
a scaled one and each entity's own, and a game can write its own.

| Clock | Its time |
| --- | --- |
| `UnityClock` | Unity's `Time`, through `UnityClock.Instance`: the clock in force until code puts another in |
| `GameClock` | The game's, through `GameClock.Instance`: the clock in force, read each time it's asked, so it can be kept and handed on and still follows a clock put in later. A sequence on it runs by the game's time whatever its packet's owner's is |
| `UnscaledClock` | The game's without its time scale, through `UnscaledClock.Instance`: its step is the clock in force's unscaled step and its `Time` the unscaled time, so a sequence on it runs while the game's time is slowed or stopped, as a pause menu's does |
| `ScaledClock` | A scale of its own, `ScaledClock.Scale`, over another clock, its `ScaledClock.Source` ([Scaled clocks](#scaled-clocks)) |
| `EntityClock` | An entity's own, `gas.GetClock()`: the game's, at the entity's `TimeScale` ([Entity clocks](#entity-clocks)) |

Each has what `ForgeTime` reads, `DeltaTime`, `UnscaledDeltaTime`, `FixedDeltaTime`, `Time`, `UnscaledTime` and
`FrameCount`, and `FollowsTicks`: whether a sequence on it waits for ProcessControl's ticks where its tick rate is set.
It's true for a clock of the game's time. The ticks run on the game's time and stand with it, so `UnscaledClock`, which
runs while that time stands, says false, and its sequences wait for Unity's frames instead. `GameClock` and a
`ScaledClock` say as the clock they read does.

### Scaled clocks

`new ScaledClock(scale, source)` runs at its `ScaledClock.Scale` against its `ScaledClock.Source`: 1 as that one, 0.5 at
half, 2 at double, 0 stopped; a scale below 0 is taken as 0. Its source is the clock it's given, or, with none, the
clock in force. Its `DeltaTime` and `FixedDeltaTime` are its source's times its scale, and its unscaled times and its
frame are its source's.

Its `Time` counts from the first time it's read, its source's time at each scale it had, so a change of its scale or its
source carries on from where it was. Making one reads no clock, so a component's field can make one where it's declared.
With no source, it counts whichever clock is in force as it's read: give it one to count by where a world of its own
steps. The sequences that share one slow, speed up or stop together as it's set.

### Entity clocks

An entity's clock, `gas.GetClock()`, runs at its `TimeScale`: `OwnScale`, the scale code sets, as a hit-stop's 0, times
`AttributeScale`, the one its **Time Scale** attribute gives, read as each of the entity's steps begins, and 1 where its
identity names none ([ENTITIES.md](ENTITIES.md#attributes)). Setting `TimeScale` sets `OwnScale`, so a hit-stop holds
under a slow, and the slow is still there after. Its `DeltaTime` and `FixedDeltaTime` are the game's times its scale,
its `Time` its steps added up since it started, and its unscaled times and its frame the game's.

A scale of 0 holds the entity's time still: its effects, its cooldowns among them, and its abilities' waits and
sequences stand. It isn't a pause, which ProcessControl's is ([PROCESSES.md](PROCESSES.md#pausing)).

```csharp
// A hit-stop on the entity struck: its own time held, its slows and hastes kept for after
var clock = gas.GetClock();
clock.OwnScale = 0f;
// ... a moment later
clock.OwnScale = 1f;
```

### A clock of your own

A class that implements `IForgeClock` is read as any clock is: put in with `ForgeTime.Use`, named by a packet's `Clock`
or a sequence's `WithClock`, or given by a clock pick of your own
([Picking a clock in an inspector](#picking-a-clock-in-an-inspector)). Its `FollowsTicks` is true unless it says
otherwise. `GameClock`, `UnscaledClock`, `ScaledClock` and `EntityClock` read the clock in force, so a world of its
own's steps and the fixed tick drive them; a clock that reads Unity's `Time` itself doesn't follow them.

## Which clock a run takes

Everything PlayForge times goes by a clock, most by their owner's: an effect by the entity it's on, and a sequence's run
by the first it finds.

- **An effect** counts its time by the clock of the entity it's on, its duration and its period both, so a slowed
  entity's effects, its cooldowns among them, last longer. Its **Delta Time Scaler** changes it further
  ([EFFECTS.md](EFFECTS.md#duration)).
- **An entity's own step** goes by its clock: its input buffer's windows, its services, its turn and its barriers'
  decay.
- **A sequence's run**, an ability's among them, takes the first clock it finds: its own, `runtime.Clock`, which what
  runs it sets, as a `TaskSequenceProcess`'s `Clock`, a chain's `Clock` or an ability's Run Sequence task's pick; its
  packet's, `data.Clock`; its sequence's, `WithClock` on the builder; its packet's owner's, an ability's caster's own
  clock; and else the game's, `GameClock.Instance`. Its waits, delays, timeouts, longest runs and `ElapsedTime`, its
  tasks' tweens and its synced tasks all go by it ([TASK_SEQUENCES.md](TASK_SEQUENCES.md#clocks)). An ability runs on
  its activation's packet, so its waits go by its caster's clock unless the packet names another.
- **A synced runner** steps by its packet's clock, else its sequence's, else its packet's owner's
  (`runner.ClockFor(data)`).
- **A cue**'s packet carries its owner's clock: the activation's for an ability's cue, else that of the one it plays on.
  A Sequence cue's run goes by its **Clock** pick, else the sequence's own, else that one
  ([CUES.md](CUES.md#data-packets)).
- **Code of your own** reads its owner's step with `ForgeTime.DeltaTimeFor(owner)`, as `ProjectileMonoProcess` moves by
  its handler's. A task reads its run's with `SequenceTaskLibrary.DeltaTime(token)`, 0 while the run is paused, and
  `TaskSequenceRuntime.ClockOfRun(token)` is the run's clock.

Anything else goes by the game's time, the clock in force.

## Picking a clock in an inspector

An inspector picks a clock with an `AbstractClockChoice` field, as an ability's Run Sequence task's **Clock** does: each
kind gives the clock for a run on a packet as the run starts.

| Pick | Kind | The clock a run gets |
| --- | --- | --- |
| None | The field left empty | The clock it would have had |
| **Caster's** | `CasterClockChoice` | An ability's caster's own time, its time scale times the game's; the game's for any other packet |
| **Game's** | `GameClockChoice` | The game's, whatever an ability's caster's is: a slowed caster's sequence on it isn't slowed |
| **Unscaled** | `UnscaledClockChoice` | The game's without its time scale, which runs while the game's time is slowed or stopped |
| **Scaled** | `ScaledClockChoice` | A `ScaledClock` made for each run, at its **Scale** (1 as the clock it scales, 0.5 at half, 2 at double), over the clock its **Of** picks, or the caster's where it picks none |

A pick's `ClockFor(data)` gives its clock for a run on the packet; the base gives null, which leaves the run the clock
it would have had. **Caster's** reads the packet's owner, not a clock the packet names, as a pick is what names one.

The picks are offered by:

- an ability's Run Sequence task, whose pick is its run's alone, so the ability's other tasks keep the caster's
  ([ABILITIES.md](ABILITIES.md#stages));
- a Sequence cue, whose run goes by its pick, else the sequence's own clock, else its packet's, the clock of the cue's
  owner ([CUES.md](CUES.md#data-packets));
- a component of your own, through a `[SerializeReference]` field, whose pick sets its packet's clock.

```csharp
using FarEmerald.PlayForge;
using UnityEngine;

// Runs its sequence on the clock its inspector picks, or on the one it would have had
public class IntroSequence : MonoBehaviour
{
    [SerializeReference] public AbstractClockChoice Clock;

    private void Start()
    {
        var data = SequenceDataPacket.SceneRoot();
        data.Clock = Clock?.ClockFor(data);
        TaskSequenceBuilder.Create("Intro")
            .Delay(2f)
            .Do(_ => ForgeLog.Info("The intro's two seconds are up"))
            .BuildSequence()
            .RegisterAndRun(data);
    }
}
```

A `[Serializable]` class of your own made from `AbstractClockChoice` is offered beside them, by its `[ForgeMenu]` name,
or else by its class's name without "ClockChoice". The picker makes it with its parameterless constructor.

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

// One slow-motion clock the game sets, shared by every run that picks it
[Serializable]
[ForgeMenu(Name = "Bullet Time", Description = "The game's shared slow-motion clock.")]
public class BulletTimeClockChoice : AbstractClockChoice
{
    public static ScaledClock Shared { get; private set; } = new ScaledClock(1f);

    // A fresh one each time the game starts, as statics outlive play mode where domain reload is off
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Fresh() => Shared = new ScaledClock(1f);

    public override IForgeClock ClockFor(ProcessDataPacket data) => Shared;
}

// The game slows every run that picked it: BulletTimeClockChoice.Shared.Scale = 0.2f
```

## Fixed ticks

ProcessControl's fixed tick steps the game in fixed steps of its time, as many a frame as the frame's time holds, so a
run comes out the same at any frame rate.

- **Fixed Tick Rate**, under **Fixed Tick** in ProcessControl's inspector, or `ProcessControl.FixedTickRate` in code, is
  the ticks in a second of game time, as 60; 0, the default, steps once a frame. **Max Ticks Per Frame**, 8 by default,
  is the most a frame runs: a frame too long for them drops the rest of its time, so the game doesn't fall further
  behind.
- In each tick, what waits for a tick goes on, then every process steps at Update and then at LateUpdate, its end of
  frame with it. While a tick runs, the clock in force is the tick's: `ForgeTime.DeltaTime` is its step, and
  `ForgeTime.FrameCount` the ticks so far. A process that steps at FixedUpdate steps with Unity's physics, as ever.
- The ticks run on the game's time, so they stand while it does.

Code waits on them through `ForgeTick`:

| `ForgeTick` | Waits for |
| --- | --- |
| `Next(token)` | The next tick, or, with ticks off, Unity's next frame, as a sequence's waits do |
| `Next(timing, token)` | The same at a frame's timing: the Update family waits for a tick, and FixedUpdate, the physics step's, for Unity's |
| `NextFrame(token)` | The next tick, or, with ticks off, Unity's next frame as `UniTask.NextFrame` waits for it |
| `WaitUntil(condition, token)` and `WaitWhile(condition, token)` | A condition, asked once a tick, or once a frame with ticks off |
| `Next(clock, token)` | The next step of a clock's time: Unity's next frame, where the scene ticks, for a clock whose `FollowsTicks` is false, as `UnscaledClock`'s is |

`ForgeTick.Ticking` says whether the tick in force ticks, and `ForgeTick.Ticks` how many it has run. `ForgeTick.Scene`
is the scene's tick, a `ForgeTickSource`, which ProcessControl's rate turns on, and `ForgeTick.Current` is the tick in
force: a world of its own's while it steps or one of its processes changes state, the scene's otherwise. A
`ProcessWorld` its owner steps ticks once for each `Step(deltaTime)`, so its sequences wait on its steps.

## Random numbers

`ForgeRandom` is where PlayForge draws its random numbers: the game's rolls and random picks all draw from its source,
an `IForgeRandom`.

- The source is Unity's, `UnityRandom`, through `UnityRandom.Instance`, until code puts another in. It reads
  `UnityEngine.Random`, so `Random.InitState(seed)` seeds it, and anything else that draws from Unity's moves it. A
  `SeededRandom`, made with a seed, draws numbers of its own that nothing else draws from: two runs from one seed draw
  the same numbers.
- `ForgeRandom.Use(source)` puts a source in and returns the one it replaced, and `ForgeRandom.Use(null)` puts Unity's
  back. `ForgeRandom.Source` is the one drawn from now.
- It draws `ForgeRandom.Value`, from 0 to 1, as `Random.value`; `InsideUnitCircle` and `InsideUnitSphere`, a point
  within a radius of 1; `Range(min, max)` for floats; and `Range(min, maxExclusive)` for whole numbers, up to but not
  including the second, and the first itself where the range is empty.

What draws from it: every chance PlayForge rolls ([Chance](#chance)); auto targeting whose **Mode** is **Random**; a
**Randomized** scaler's roll ([SCALERS.md](SCALERS.md#a-magnitude-field)); `ForgeHelper`'s random picks and points
([ForgeHelper](#forgehelper)); and the Creator Lab. Cues don't: a placement's scatter and a floating text's random
velocity draw from `ForgeCues.Random`, a `SeededRandom` of their own, so a cue never moves the game's rolls
([CUES.md](CUES.md#placement)).

An `IForgeRandom` of your own gives `Value`, from 0 to 1, and `InsideUnitCircle` and `InsideUnitSphere`, a point in a
circle and in a sphere of radius 1. A source put in stays until code puts another in, past the end of play mode where
domain reload is off, so code that puts one in puts back the one it replaced.

```csharp
using FarEmerald.PlayForge;

// A match's rolls from its seed: its replay, from the same seed, rolls the same
int seed = 20261118;
IForgeRandom previous = ForgeRandom.Use(new SeededRandom(seed));

int die = ForgeRandom.Range(1, 7);          // 1 to 6
float spread = ForgeRandom.Range(-5f, 5f);

// ... and, as the match ends
ForgeRandom.Use(previous);
```

## Chance

`ForgeChance` rolls every chance PlayForge has, each by its `ChanceRoll`, which says whether it's pseudo-random and
whose luck it reads.

PlayForge's chances are an effect's **Chance to Apply**, an ability's **Chance to Activate**, a trigger's **Chance**, a
Value Pipeline's **Chance**, **Block** and **Evade** stages, and the **Chance** condition.
`ForgeChance.Roll(chance, in context, roll, key)` says whether a chance, from 0 to 1, comes up for the caster its
`NumberContext` gives. A chance of 1 or more always comes up and one of 0 or less never does, and neither draws, so a
chance that can't miss leaves the numbers others draw as they were. Each of PlayForge's chances has a `ChanceRoll`
beside it, its **Chance Roll**, **Activation Roll** or **Roll**, which says how it rolls
([EFFECTS.md](EFFECTS.md#requirements)):

- **Pseudo Random**: each miss raises the next roll's odds and a hit starts them again, so streaks even out while the
  chance over many rolls stays the same. The odds are `ForgeChance.Constant(chance)` times one more than the misses in a
  row, at most 1: the constant whose distribution's mean is the chance, as Dota's pseudo-random distribution is. Each
  caster keeps its own streak for each chance, by the chance's key, and PlayForge's own chances key by what holds them:
  the effect, the ability, the trigger, the stage or the condition. A pseudo-random roll with no key or no caster rolls
  its chance as it is. A caster's streaks go with it, and `ForgeChance.ForgetStreaks(caster)` forgets them, as a test
  does to start clean.
- **Luck**: the roll's luck, `ForgeChance.LuckOf(in context, roll)`. With **Own Luck**, it's the chance's own **Luck**,
  a number read for the roll; else it's the current value of the caster's attribute playing the project's Luck role,
  `ForgeChance.ProjectLuck` ([ATTRIBUTES.md](ATTRIBUTES.md#roles)); else 0. Either is rounded to a whole number. Above
  0, the roll draws that many more times and keeps the best; below 0, the worst. A draw below the odds comes up.

The **Chance** condition, `ChanceCondition`, holds on a roll of its **Chance**, a number of any kind, as a critical
strike chance read from an attribute, rolled by its **Roll**. Read in a hit, it rolls once and keeps the roll, so the
hit's preview and its commit read the same; read with no hit, it rolls each time it's read.

A chance of the game's own rolls the same way:

```csharp
using FarEmerald.PlayForge;

public static class Lockpicking
{
    // The key the thief's pseudo-random streak for this chance is kept under
    private static readonly object Key = new object();

    // Pseudo-random, and lucky by the thief's attribute in the project's Luck role
    private static readonly ChanceRoll Roll = new ChanceRoll { PseudoRandom = true };

    public static bool Picks(IGameplayAbilitySystem thief, float chance)
    {
        var context = NumberContext.Of(thief);
        return ForgeChance.Roll(chance, in context, Roll, Key);
    }
}
```

## Repeatable runs

A run that must come out the same twice, as a replay, a rollback or a lockstep match must, needs the same time and the
same rolls each time it runs.

- **Time**: step the game in fixed steps, with ProcessControl's **Fixed Tick Rate** ([Fixed ticks](#fixed-ticks)), or
  step a world of its own by a fixed time, `world.Step(deltaTime)`. A clock of the game's time follows either.
- **Rolls**: put in a `SeededRandom`, `ForgeRandom.Use(new SeededRandom(seed))`, seeded for the run, or for each step as
  a rollback does ([SAVING.md](SAVING.md#rolling-back)). Unity's random numbers, the default, move whenever anything
  else in the game draws from them.
- **Cues** draw from `ForgeCues.Random`, numbers of their own, so a run that plays its cues rolls as one that doesn't,
  as on a server or while a simulation runs ahead, with `ForgeCues.Enabled` off.
- **Your own code** reads time through `ForgeTime` and draws through `ForgeRandom`, as PlayForge does: code that reads
  Unity's `Time` or `Random` itself follows neither a clock nor a source put in.

## Time and rolls in tests

The test kit makes time and chance a test's own through the same two services ([TESTING.md](TESTING.md#the-world)).

- `World.Clock(step)` puts in a `SteppingClock`: each frame `step` seconds of PlayForge's time, its `DeltaTime`,
  `UnscaledDeltaTime` and `FixedDeltaTime`, while its `Time`, `UnscaledTime` and `FrameCount` stay Unity's. It returns
  the clock, whose `Step` can change.
- `World.Rolls(0.1f, 0.9f)` puts in a `ScriptedRandom`: each draw is the next of its rolls, in turn and round again, and
  its `Drawn` counts them; its points in a circle and in a sphere are zero. A chance comes up where it's above the roll,
  so a roll of 0 always lands and one of 0.99 almost never does. A roll with luck draws more than one.
- The world gives back the clock and the source it replaced as it ends. Outside it,
  `ForgeTime.Use(new SteppingClock(step))` and `ForgeRandom.Use(new ScriptedRandom(0f))` do the same, put back by hand.
- `ForgeChance.ForgetStreaks(caster)` starts a caster's pseudo-random streaks clean.

## Pointer input

`ForgeInput` is what PlayForge reads of the player's input: the press and aim of click targeting, the task libraries'
waits for a key or a press, and the Creator Lab's controls.

`ForgeInput.Current` is the input read now, an `IForgeInput`: the project's own, `ForgeInput.Default()`, until code puts
another in with `ForgeInput.Use(input)`, which returns the one it replaced; `ForgeInput.Use(null)` puts the project's
own back. The project's own is:

- `InputSystemPointerInput`, where the Input System package is installed and the Player settings'
  **Active Input Handling** takes it, as it does where it takes both;
- else `LegacyPointerInput`, where it takes the Input Manager;
- else an input that never presses and aims nowhere.

| `IForgeInput` | What it says |
| --- | --- |
| `PointerPressed(button)` | Whether a button went down this frame: 0 the primary, the left button or a touch; 1 the secondary, the right; 2 the middle |
| `TryGetPointerRay(out ray)` | The ray the pointer aims along, or false with nothing to aim through, as with no camera |
| `PointerHeld(button)` | Whether a button is down now |
| `TryGetPointerPosition(out position)` | Where the pointer is on the screen, in pixels from its bottom left, or false where there's none |
| `ScrollDelta` | How far the scroll wheel turned this frame, in notches, away from the player positive |
| `KeyPressed(key)` | Whether a key went down this frame |
| `KeyHeld(key)` | Whether a key is down now, pressed this frame or held from an earlier one |

Only the first two must be written. The rest read as nothing held, no position, no scroll and no key, unless an input
gives its own.

### What reads it

- **Click targeting**, the Select tasks (Position, Direction, GAS Target, GAS Or Position and GAS Target In Range),
  waits for `PointerPressed` of its **Mouse Button**, then aims along `TryGetPointerRay`
  ([ABILITIES.md](ABILITIES.md#activation)).
- **The task libraries' waits**: `WaitForKey` and `WaitForAnyKey` read `KeyPressed`, and `WaitForPointer` reads
  `PointerPressed`, in `SequenceTaskLibrary` and `SyncSequenceTaskLibrary` alike
  ([TASK_SEQUENCES.md](TASK_SEQUENCES.md#waiting)).
- **The Creator Lab**: its presses and keys, the pointer's place, the middle button held to pan and the wheel to zoom
  ([CREATOR_LAB.md](CREATOR_LAB.md)).

### The defaults

Each of the project's own aims through its `Camera`, or `Camera.main` where that's null.

- `LegacyPointerInput` reads the Input Manager's mouse and keys. It has no pointer position where no mouse is present.
- `InputSystemPointerInput` reads the Input System's: a mouse presses each of its three buttons, and a pen or a touch
  the primary. Its `ScrollDelta` is the mouse's scroll over 120, a notch on most mice. Its keys are the letters, the top
  row's digits, F1 to F12, Escape, Space, Return, Tab, Backspace, Delete, the arrows, and left and right Shift, Control
  and Alt; any other `KeyCode` reads as never pressed.

Each is compiled only where its input handling is on, so code that names one sits under the same condition:
`#if ENABLE_LEGACY_INPUT_MANAGER` for `LegacyPointerInput`, and `#if ENABLE_INPUT_SYSTEM`, with the Input System package
installed, for `InputSystemPointerInput`.

`ForgeInput.TryAimThrough(camera, screenPoint, out ray)` gives the ray from a camera through a point on the screen: the
camera given, or else `Camera.main`. With neither, it logs a warning of the **Input** system and returns false.
`ForgeInput.CameraOr(camera)` is the camera given, or else `Camera.main`, as a floating text faces.

### Input of your own

A game with a crosshair, a gamepad's reticle or a controller's ray writes an `IForgeInput` and puts it in. Give it all
seven members, passing those it doesn't change on to the project's own: an input that writes only the two click
targeting reads has no key, no pointer position and no scroll, so `WaitForKey`, `WaitForAnyKey` and the Creator Lab's
keys, clicks and camera hear nothing through it. An input put in stays until code puts another in, past the end of play
mode where domain reload is off, so code that puts one in puts back the one it replaced.

```csharp
using FarEmerald.PlayForge;
using UnityEngine;

// A third-person game's targeting: it presses as the project's own input does, but aims from the crosshair in the
// middle of the screen, through the player's camera
public sealed class CrosshairInput : IForgeInput
{
    private readonly IForgeInput device = ForgeInput.Default();

    public Camera Camera { get; set; }

    public bool PointerPressed(int button) => device.PointerPressed(button);

    public bool TryGetPointerRay(out Ray ray) =>
        ForgeInput.TryAimThrough(Camera, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out ray);

    // The rest as the device reads them, so the task libraries' waits and the Creator Lab still hear it
    public bool PointerHeld(int button) => device.PointerHeld(button);
    public bool TryGetPointerPosition(out Vector2 position) => device.TryGetPointerPosition(out position);
    public float ScrollDelta => device.ScrollDelta;
    public bool KeyPressed(KeyCode key) => device.KeyPressed(key);
    public bool KeyHeld(KeyCode key) => device.KeyHeld(key);
}

// Puts it in while the player's camera is on, and gives back the input it replaced
public class CrosshairTargeting : MonoBehaviour
{
    public Camera PlayerCamera;
    private IForgeInput previous;

    private void OnEnable() => previous = ForgeInput.Use(new CrosshairInput { Camera = PlayerCamera });
    private void OnDisable() => ForgeInput.Use(previous);
}
```

## Logging

`ForgeLog` is where everything PlayForge says goes: each message has a level and a system, and a gate on the two decides
what reaches Unity's Console.

- **Levels**, `ForgeLog.ELevel`, from the most said to the least: `Verbose`, `Info`, `Warning`, `Error`, and `None`,
  which lets nothing through, errors neither ([Log levels](#log-levels)).
- **Systems**, `ForgeLog.ESystem`: General, Effects, Abilities, Attributes, Workers, Numbers, Processes, Tags, Items,
  Saves, Input, Lab and Sequences. Each can take a level of its own.

Code logs through it as PlayForge does:

- `ForgeLog.Verbose`, `ForgeLog.Info`, `ForgeLog.Warning` and `ForgeLog.Error` take a message and the Unity object the
  Console points at, its `context`, as a message of **General**. Given a system first, they take the entity the message
  is about too, its `subject`, as `ForgeLog.Warning(ForgeLog.ESystem.Abilities, "No target", this, gas)`.
- `ForgeLog.Exception(system, exception, context, subject)` logs an exception as Unity's `Debug.LogException` does, its
  stack in the Console, where an error is let through.
- `ForgeLog.Allows(system, level)` says whether a message would be let through, so a costly one is built only when it
  will be shown.

Its messages reach the Console with `[PlayForge]` before them, a warning as a warning and an error as an error. Its
methods are marked `[HideInCallstack]`, so the Console's **Strip Logging Callstack** leaves them out of the stack, and a
double-click on a message opens the code that logged it.

`ForgeLog.Logged` hears each message let through, as a `ForgeLogEntry`: its `Level`, `System` and `Message`, without the
prefix; its `Context` and `Subject`; and the game's `Time` and `Frame` as it was logged. An exception is heard as its
type and its message. An entry's `ToString()` writes it in a line, as "Effects, warning: …". The Creator Lab's Log page
hears the messages so.

```csharp
using System.Collections.Generic;
using FarEmerald.PlayForge;
using UnityEngine;

// An in-game console: the last 50 warnings and errors PlayForge let through, newest last
public class ForgeConsole : MonoBehaviour
{
    private readonly Queue<string> lines = new Queue<string>();

    private void OnEnable() => ForgeLog.Logged += Heard;
    private void OnDisable() => ForgeLog.Logged -= Heard;

    private void Heard(ForgeLogEntry entry)
    {
        if (entry.Level < ForgeLog.ELevel.Warning) return;
        lines.Enqueue($"[{entry.Frame}] {entry}");
        while (lines.Count > 50) lines.Dequeue();
    }

    private void OnGUI()
    {
        foreach (var line in lines) GUILayout.Label(line);
    }
}
```

## Log levels

A message is let through where its level is at least its system's level: the system's own, where it has one, or else
`ForgeLog.MinimumLevel`, Info by default.

- `ForgeLog.SetLevel(system, level)` gives a system a level of its own, and `ForgeLog.SetLevel(system, null)` gives it
  back. `ForgeLog.LevelOf(system)` is its level now, `ForgeLog.HasOwnLevel(system)` says whether it has one of its own,
  and `ForgeLog.ClearLevels()` gives every system back.
- In the editor, `Edit → Preferences → PlayForge` sets `ForgeLog.MinimumLevel` with **Minimum Log Level**, under
  **Logging**. The level is yours, not the project's: it's kept in the editor's preferences and put back on each domain
  reload. **Use Defaults** puts every preference on the page back, the level to Info. Editor code sets it with
  `ForgeLogSettingsBridge.SetLevel(level)`.
- The Creator Lab's **Log** page gives a system a level of its own with **System** and **Level**, and **As all** gives
  it back to the preference's ([CREATOR_LAB.md](CREATOR_LAB.md#showing-its-working)).
- A build reads no preference: it logs Info and above unless its code sets the level.

```csharp
using FarEmerald.PlayForge;
using UnityEngine;

public static class BuildLogging
{
    // A build reads no preference: problems only, but every message about effects
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
        if (Application.isEditor) return;
        ForgeLog.MinimumLevel = ForgeLog.ELevel.Warning;
        ForgeLog.SetLevel(ForgeLog.ESystem.Effects, ForgeLog.ELevel.Verbose);
    }
}
```

What's said only when asked is information, of level Info: its switch makes it, and the level shows it
([Process and sequence logs](#process-and-sequence-logs)). A level above Info hides it however it's switched on.

## Process and sequence logs

ProcessControl and task sequences have switches of their own that decide whether they say what they do, and most of what
they say is information, which the level must let through to show.

**ProcessControl**, of the **Processes** system ([PROCESSES.md](PROCESSES.md#logging)):

- **Output Logs**, off by default, logs each process registered and unregistered, the scene's processes it collects as
  it starts and the hierarchy it makes of them, and, as a warning, a change of state its rules don't cover.
- **Detailed Logs**, on by default, adds to those while Output Logs is on: each process's handler, each process
  collected, and each process's state as it's queued, set and forced.
- A world of its own has the same switches, `ProcessWorld.OutputLogs` and `ProcessWorld.DetailedLogs`; a scene's world's
  are its ProcessControl's.

**Sequences**, of the **Sequences** system ([TASK_SEQUENCES.md](TASK_SEQUENCES.md#logging)):

- A run that fails is logged as an error, with what it threw, unless its sequence's `WithErrorLogging(false)` says
  otherwise; a stage whose `OnException` hears a throw and lets it on logs it too, unless the stage's
  `WithErrorLogging(false)` says otherwise. A handler that throws, as an `OnComplete`, is logged as an error.
- `WithInjectionLogging(true)`, on a sequence or a stage, logs each injection as it's applied: its kind, where it came
  from and the stage.
- A chain's `WithLogging(true)` logs its progress: each sequence it starts, finishes or skips, the conditions that fire,
  its repeats and its interruptions.

**Debug tools** say what they find as information too: `TagCache.LogWeights()`, of **Tags**; the Threshold Debugger
effect worker, `ThresholdDebuggerEffectWorker`, of **Workers**; and a queued `LogAction`, of **General**.

## Profiling

PlayForge's work shows in Unity's Profiler: markers that time it, counters that say how much of it there is, and each
recorded frame's costliest processes ([GETTING_STARTED.md](GETTING_STARTED.md#profiling)).

### Markers

`ForgeProfiler` holds PlayForge's markers, each a sample in the Profiler's Scripts category:

| Marker | `ForgeProfiler` | What it times |
| --- | --- | --- |
| `PlayForge.Update`, `PlayForge.LateUpdate`, `PlayForge.FixedUpdate` | `StepUpdate`, `StepLateUpdate`, `StepFixedUpdate` | The step loop at each timing: each process that steps then, in step order |
| `PlayForge.ProcessStep` | `ProcessStep` | One process's step |
| `PlayForge.Step.` and the process's type's name | `StepOf(type)` | The same step under its type's own marker, inside `PlayForge.ProcessStep`, with the process's ID as its metadata; made the first time a process of the type steps |
| `PlayForge.GASStep` | `GASStep` | A GAS's step: its clock, its effects' ticks, its abilities' While Active checks, its input buffer, its services, its turn and its barriers |
| `PlayForge.EndOfFrame` | `EndOfFrame` | A GAS's end of frame: its queued actions, its tag and analysis workers, and its frame's summary |
| `PlayForge.ApplyEffect` | `ApplyEffect` | An effect's application, its contained effects' inside it |
| `PlayForge.ActivateAbility` | `ActivateAbility` | An ability's activation, up to the first of its tasks that waits |
| `PlayForge.TagCompile` | `TagCompile` | A GAS's tag compile: its tags rebuilt from their sources |
| `PlayForge.Recompute` | `Recompute` | An attribute's recompute, from its root through its modifiers to its bounds |
| `PlayForge.Stage` | `Stage` | A sequence stage's start, an ability's among them: its tasks, up to the first that waits |

### Counters

`ForgeCounters` holds PlayForge's counters, in the Profiler's PlayForge category (`ForgeCounters.CategoryName`, and
`ForgeCounters.Category`). A level holds until it changes. A frame's count starts from 0 each frame, and counts every
world's work in the frame.

| Counter | `ForgeCounters` | Kind | What it counts |
| --- | --- | --- | --- |
| Processes Running | `ProcessesRunning` | Level | The scene's processes running, as its ProcessControl's LateUpdate loop last ended |
| Processes Waiting | `ProcessesWaiting` | Level | The scene's processes waiting |
| Processes Created | `ProcessesCreated` | Level | The processes the scene's ProcessControl has taken |
| Registrations | `Registrations` | Per frame | Processes registered |
| Terminations | `Terminations` | Per frame | Processes unregistered as they ended |
| Steps | `Steps` | Per frame | Process steps, one for each process at each timing it steps in |
| State Changes | `StateChanges` | Per frame | Processes' changes of state |
| Step Time | `StepTime` | Per frame | The time in the step loops, every timing's, in nanoseconds |
| Mono Entities | `MonoEntities` | Level | The MonoBehaviour entities running, GameRoot left out |
| Runtime Entities | `RuntimeEntities` | Level | The runtime entities running, in every world |
| Effects Active | `EffectsActive` | Level | The effects on the entities' shelves, as each last stepped |
| Effects Applied | `EffectsApplied` | Per frame | Effect applications, a contained effect's among them |
| Effects Removed | `EffectsRemoved` | Per frame | Effects taken off a shelf |
| Impacts | `Impacts` | Per frame | Impacts, as each entity received them |
| Activations | `Activations` | Per frame | Ability activations |
| Refused Activations | `RefusedActivations` | Per frame | Refused activations, as each was told |
| Tag Compiles | `TagCompiles` | Per frame | Tag compiles |
| Recomputes | `Recomputes` | Per frame | Attribute recomputes |
| Sequences Running | `SequencesRunning` | Level | The sequences running, an ability's among them |
| Stages Started | `StagesStarted` | Per frame | Sequence stages started |

Two Profiler modules chart them, turned on in the Profiler window's list of modules: **PlayForge Processes** charts the
process and sequence counters, and **PlayForge GAS** the rest.

### The costliest processes

While the Profiler records, each frame's costliest processes go into its capture as the frame's metadata, so a saved
capture keeps them, and the **PlayForge Processes** module's details list the selected frame's: each process's ID, its
type and the time its steps took. The scene's ProcessControl puts them in as its frame ends.

- `ForgeFrameCosts.Most`, 10, is how many a frame keeps. `ForgeFrameCosts.LastFrame` is the last recorded frame's, the
  costliest first, each a `ForgeProcessCost`: its `Process`, the process's ID, and the `Nanoseconds` its steps took in
  the frame, every timing's.
- `ForgeFrameCosts.Recording` says whether this frame's steps are timed: whether the Profiler recorded as the last frame
  ended. While it doesn't, nothing is timed for them.
- A tool of your own reads them from a frame's data as the module does: the costs under the ID
  `ForgeFrameCosts.MetaDataId` and the tag `ForgeFrameCosts.Tag`, and the type's name of the cost at `i`, in UTF-8,
  under the tag `ForgeFrameCosts.Tag + 1 + i`.

### Reading them in code

A `ProfilerRecorder` reads a marker or a counter by its name, in the editor or in a development build; Unity compiles
them out of a release build.

```csharp
using FarEmerald.PlayForge;
using Unity.Profiling;
using UnityEngine;

// The effects applied each frame, and the time PlayForge's Update loop took, on screen in a development build
public class ForgeStats : MonoBehaviour
{
    private ProfilerRecorder applied, update;

    private void OnEnable()
    {
        applied = ProfilerRecorder.StartNew(ForgeCounters.Category, "Effects Applied");
        update = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "PlayForge.Update");
    }

    private void OnDisable()
    {
        applied.Dispose();
        update.Dispose();
    }

    private void OnGUI()
    {
        GUILayout.Label($"Effects applied: {applied.LastValue}");
        GUILayout.Label($"PlayForge.Update: {update.LastValue / 1e6:0.00} ms");
    }
}
```

## Entity services

An entity service is a per-entity object an entity's components share, made the first time it's asked for and stepped by
the entity's own time. The auras an entity runs, `AuraSet`, and the wait a Respawn counts down, `RespawnTimer`, are two.

- `gas.GetService<T>()` gives the entity's service of a kind, made the first time it's asked for, and
  `gas.FindService<T>()` gives it without making one, or null.
- A service derives from `AbstractEntityService`, with a parameterless constructor, and overrides the hooks it needs,
  each a no-op otherwise: `Step(deltaTime)`, each frame by the entity's own time, after its effects tick and before its
  turn; `Revive()`, as an entity that stays revives, before anything else of the revive; and `End()`, as the entity
  ends, which keeps none after. Its `System` is the entity it's of.

[ENTITIES.md](ENTITIES.md#services) has the rest.

```csharp
using FarEmerald.PlayForge;
using UnityEngine;

// A heat meter each entity keeps, which cools by the entity's own time: a slowed entity cools slower
public class Heat : AbstractEntityService
{
    public float Value { get; private set; }
    public float CoolingPerSecond = 10f;

    public void Add(float heat) => Value += heat;

    public override void Step(float deltaTime) => Value = Mathf.Max(0f, Value - CoolingPerSecond * deltaTime);

    // Back from the dead, cold
    public override void Revive() => Value = 0f;
}

public static class Overheat
{
    public static void Fire(IGameplayAbilitySystem gunner) => gunner.GetService<Heat>().Add(25f);

    public static float HeatOf(IGameplayAbilitySystem gunner) => gunner.FindService<Heat>()?.Value ?? 0f;
}
```

## ForgeHelper

`ForgeHelper` is a static class of the checks and small sums PlayForge's own systems share, which a game's workers,
conditions and tools can call too.

| Group | Members | What they do |
| --- | --- | --- |
| Tags | `HasTag`, `HasAnyTag`, `HasAllTags`, `HasNoneOfTags`, `CountMatchingTags`, `GetMatchingTags`, `MatchesTag` | Look for tags in a list by an `ETagMatchMode`: `Exact`; `IncludeChildren`, the tag or one under it; `IncludeParents`, the tag or one above it; or `SameRoot` |
| Requirements | `ValidateAvoidRequire`, `ValidateAvoidRequireQueries`, `ValidateTagQuery`, `ValidateAllQueries`, `ValidateAnyQuery` | Whether tags, a list or a `TagCache`, hold every required tag and none to avoid, or pass lists of `TagQuery`. A null query passes, and so does an empty list |
| Tag paths | `IsValidTagPath`, `BuildTagPath`, `GetUniqueRoots`, `GroupByRoot`, `GetCommonAncestor` | Whether a path is well formed; a path from its parts, joined by dots; tags' roots; and the deepest tag two tags both are or are under, or null |
| Effects | `ValidateEffectApplicationRequirements`, `ValidateEffectOngoingRequirements`, `ValidateEffectRemovalRequirements`, `ValidateAffiliationPolicy` | An effect spec's requirements as it applies, while it lasts and to remove it, and its affiliation policy against its target's sides |
| Worker filters | `ValidateImpactTypes`, `ValidateContextTags`, `ValidateSelfModification`, `ValidateImpactTargets`, `ValidateSignPolicy` | The filters a worker puts a change through: its impact types, its context tags, whether its source is its target, which part of the attribute it changed, and its sign |
| Signs | `SignPolicy`, `SignPolicyExtended`, `SignInt`, `SignFloat`, `AlignedSignInt`, `AlignedSignFloat`, `AlignToSign` | A change's sign as an `ESignPolicy` ([Sign and math policies](#sign-and-math-policies)); a sign as −1, 0 or 1, alone or several multiplied; and a value's size given a sign, negative for `Negative` and positive for the rest |
| Sums | `PerformCalculationOperation`, `MagnitudeAndScalerOperation`, `PerformScalerRelativeOperation`, `AttributeMathEvent` | An `ECalculationOperation` on two values; a magnitude with its scaler's value by an `EMagnitudeOperation`, with an `ERoundingOperation` for a whole number, whose Round takes a half away from zero; and an operation on attribute values whose operand an `EMathApplicationPolicy` turns first |
| Enums | `Translate` | One enum read as another: an `EAbilityActivationPolicyExtended` as an `EAbilityActivationPolicy`, an `EMagnitudeOperation` as an `ECalculationOperation`, an `EAttributeTargetLimited` as an `EAttributeTarget` |
| Collections | `RandomChoice`, `Shuffle`, `WeightedSample`, `SafeAdd`, `SafeAddRange`, `SafeRemove`, `ContainsAll`, `TrueForAny` | A list's item at random; the list shuffled in place; N items picked by their weights, an item as often as it's drawn; a dictionary's entries added, or added to a key's list or set, and removed; and whether a sequence holds every item of another, or has an item a condition holds for. The random ones draw from `ForgeRandom` |
| Points | `RandomPointWithinCircle`, `RandomPointWithinSphere` | A point at random within a radius of the origin, drawn from `ForgeRandom`. The circle's lies in the XY plane, a `Vector3` with a z of 0, and one nearer than its `minRadius` is moved out to it |
| Icons | `GetTextureItem`, `TextureUnder` | An icon from an asset's **Textures**: `GetTextureItem` the texture under a tag, else the first that has one, and `TextureUnder` the one under exactly that tag, or null |

## Packet helpers

`ProcessTaskUtil` holds extension methods for a data packet's targets and the effects it carries, and three loops for
async code that wait through `ForgeTick`.

- `data.GetTargetingPacket(tag, out packet)` gives the primary `AbstractTargetingPacket` under a tag, or false where
  there's none, and then interrupts the packet's sequence, where it's a `SequenceDataPacket`, unless it's called with
  `autoInterrupt: false`.
- `data.SetTargetingPacket(tag, packet)` files one as the primary, and `data.SetTargetingPackets(tag, targets)` files
  the targeting packet of each target, in their order and in place of what was there, so the first is the primary.
- `data.TryApplyEffects(target, tag)` applies each effect loaded under the tag to a target, an `ITarget` or a
  `GameObject` that has one, each made from the ability's origin with the magnitudes its caller set. It's false where
  the packet isn't an ability's, an `AbilityDataPacket`, where it holds no effects under the tag, or where there's no
  target.
- `data.TryGetLoadedAssets<T>(tag, out assets)` and `data.GetLoadedAssets<T>(tag)` give every value of a kind under a
  tag, as a `DataValue<T>`, empty where there's none.
- `ProcessTaskUtil.WhileAsync(condition, token)` waits while a condition holds, a tick at a time;
  `ProcessTaskUtil.DoWhileAsync(body, condition, token)` awaits the body, then a tick, while the condition holds, the
  body at least once; and `ProcessTaskUtil.DoWhile(body, duration, token)` does so until the duration has passed, by
  `ForgeTime.Time`, or by its unscaled time where its `useUnscaledTime` says. Each takes a `PlayerLoopTiming`, Update by
  default, and waits as `ForgeTick.Next` does ([Fixed ticks](#fixed-ticks)).

## The attribute registry

`AttributeRegistry` finds an attribute by its name, among those the game's entities have been given.

- An attribute is registered under its name as an entity's attribute system takes it, and each attribute of the sets a
  Bootstrapper's **Register Sets** lists is registered as it brings PlayForge up. The first of a name keeps it:
  `AttributeRegistry.Add(attribute)` is false where the name is taken.
- Names compare by `AttributeRegistry.RefactorByNamingConvention(name)`, in capitals with each space an underscore, so
  "Attack Speed", "attack speed" and "ATTACK_SPEED" are one name.
- `AttributeRegistry.TryGetByName(name, out attribute)` finds one, and `AttributeRegistry.GetByName(name)` gives it, or
  null; with `createIfFalse: true`, a name none has gives a new `RuntimeAttribute` of that name, which isn't registered.
  `AttributeRegistry.Contains` asks by a name or by an attribute's name, and `AttributeRegistry.GetAll()` lists them.
- It starts empty each time the game starts, play mode with domain reload off among them, and
  `AttributeRegistry.Reset()` empties it, as the test kit's world does as it ends.

## Lists

`ReadOnlyListView<T>` and `ForgeUtils` are two small list helpers: a view of a list that `foreach` walks without
allocating, and the index of a list's least or greatest item.

- `ReadOnlyListView<T>` is a read-only struct over a `List<T>`: its `Count`, an indexer, and a `GetEnumerator` that
  hands `foreach` the list's own struct enumerator, so walking it allocates nothing. Walked as an `IEnumerable<T>`, as
  LINQ walks it, its enumerator is boxed, as any list's is. A view of no list reads as empty. An effect spec's
  `GetImpactedAttributes(delivery)` returns one.
- `ForgeUtils.IndexMin(list, selector)` and `ForgeUtils.IndexMax(list, selector)` give the index of the item whose
  selected value is least or greatest, the first of equals, and 0 for an empty list.

## Sign and math policies

Three enums say how PlayForge reads the sign of a change and turns a number into a factor: `ESignPolicy`,
`ESignPolicyExtended` and `EMathApplicationPolicy`.

- `ESignPolicy` is a change's sign: `Positive`; `Negative`; `ZeroBiased`, where its parts sum to 0 but aren't all 0, as
  +5 to an attribute's current value and −5 to its base; and `ZeroNeutral`, where every part is 0.
  `ForgeHelper.SignPolicy` reads it from one number, which is never `ZeroBiased`, or from a change's current and base
  parts, and a change's own `SignPolicy`, as a `SourcedModifiedAttributeValue`'s, is its parts read so. An impact worker
  made from `AbstractContextImpactWorker` takes the hits of one sign, its **Impact Sign**.
- `ESignPolicyExtended` adds `Any`, which takes every sign, as a focused attribute worker's **Sign Policy** does by
  default ([WORKERS.md](WORKERS.md#the-map)).
- `EMathApplicationPolicy` turns a number into a factor: `AsIs`; `OnePlus`, one plus it, as a bonus of 0.2 multiplies by
  1.2; or `OneMinus`, one minus it, as a resistance of 0.2 leaves 0.8. `AttenuateScalerBehaviour`'s **Policy** and
  `MathAttributeWorker`'s **Operation Policy** read it.
