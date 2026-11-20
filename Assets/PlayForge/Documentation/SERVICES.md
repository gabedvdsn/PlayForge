# PlayForge — Time, Randomness, Input and Logging

PlayForge works through four services of its own:

| Service | What it does |
| --- | --- |
| `ForgeTime` | Reads the game's time. |
| `ForgeRandom` | Draws its random numbers. |
| `ForgeInput` | Reads the player's pointer. |
| `ForgeLog` | Says what PlayForge does. |

The first three read Unity's own until code puts another in, with `ForgeTime.Use`, `ForgeRandom.Use` or
`ForgeInput.Use`. A test, a replay, a server or a game with input of its own does this. Each returns the one it
replaced, to put back.

This guide is the reference behind the short sections of
[GETTING_STARTED.md](GETTING_STARTED.md#time-and-random-numbers) on time, logging and profiling. It covers:

- the clocks, and which one a run takes;
- the fixed tick;
- chances and luck;
- pointer input;
- log levels;
- the Profiler's markers and counters;
- an entity's own services;
- the small utilities a game's code uses.

## Time

`ForgeTime` is the clock PlayForge's runtime reads in place of Unity's `Time`. It's also where a game puts in a time of
its own.

`ForgeTime` reads the clock in force, `ForgeTime.Clock`, an `IForgeClock`. That's Unity's, `UnityClock.Instance`,
until code puts another in. Nothing else in the runtime reads Unity's `Time`.

Its properties read the clock in force as Unity's own do:

| `ForgeTime` | As Unity's |
| --- | --- |
| `DeltaTime` | `Time.deltaTime`: the frame's step, scaled |
| `UnscaledDeltaTime` | `Time.unscaledDeltaTime` |
| `FixedDeltaTime` | `Time.fixedDeltaTime`: the fixed step |
| `Time` | `Time.time`: the time since the game started, scaled |
| `UnscaledTime` | `Time.unscaledTime` |
| `FrameCount` | `Time.frameCount` |

### Putting a clock in

You replace the clock in force with `ForgeTime.Use`:

| Method | What it does |
| --- | --- |
| `ForgeTime.Use(clock)` | Puts a clock in, and returns the one it replaced. |
| `ForgeTime.Use(null)` | Puts Unity's back. |

Two things put a clock of their own in while they step, and put the one before back after:

- each of ProcessControl's fixed ticks ([Fixed ticks](#fixed-ticks));
- a world of its own: a `ProcessWorld` its owner steps with `Step(deltaTime)`, whose clock reads that step.

This class puts in a scaled clock over Unity's before the first scene loads:

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
with no source each read the clock in force, so each, put in as it, would read itself.

`ForgeTime.Use` refuses the `GameClock`. A `ScaledClock` over `UnityClock.Instance`, as above, scales Unity's.

What waits until a time keeps `ForgeTime.Time`, as a pooled cue's burst does. So a clock put in while the game runs
should carry its `Time` on from the one it replaces.

> [!NOTE]
> A clock put in stays until code puts another in, past the end of play mode where domain reload is off. So code that
> puts one in puts back the one it replaced.

### An owner's clock

An owner's time is its own, where it has one.

| Method | What it gives |
| --- | --- |
| `ForgeTime.ClockOf(owner)` | An entity's own clock, for an entity, an `IGameplayAbilitySystem`. The game's, `GameClock.Instance`, for anything else. |
| `ForgeTime.DeltaTimeFor(owner)` | The owner's step this frame. |
| `ForgeTime.ClockFor(data)` | The clock a data packet names, `data.Clock`, or else its owner's: an ability's caster's, or the game's. |
| `ForgeTime.DeltaTimeFor(data)` | The step of the packet's clock. |

## Clocks

A clock is an `IForgeClock`, a source of time. PlayForge has one for Unity's time, one for the game's, an unscaled one,
a scaled one and each entity's own, and a game can write its own.

| Clock | Its time |
| --- | --- |
| `UnityClock` | Unity's `Time`, through `UnityClock.Instance`. It's the clock in force until code puts another in. |
| `GameClock` | The game's, through `GameClock.Instance`: the clock in force, read each time it's asked. So it can be kept and handed on, and still follows a clock put in later. A sequence on it runs by the game's time, whatever its packet's owner's is. |
| `UnscaledClock` | The game's without its time scale, through `UnscaledClock.Instance`. Its step is the clock in force's unscaled step, and its `Time` the unscaled time. So a sequence on it runs while the game's time is slowed or stopped, as a pause menu's does. |
| `ScaledClock` | A scale of its own, `ScaledClock.Scale`, over another clock, its `ScaledClock.Source` ([Scaled clocks](#scaled-clocks)). |
| `EntityClock` | An entity's own, `gas.GetClock()`: the game's, at the entity's `TimeScale` ([Entity clocks](#entity-clocks)). |

Each has what `ForgeTime` reads: `DeltaTime`, `UnscaledDeltaTime`, `FixedDeltaTime`, `Time`, `UnscaledTime` and
`FrameCount`. Each also has `FollowsTicks`: whether a sequence on it waits for ProcessControl's ticks, where its tick
rate is set.

`FollowsTicks` is true for a clock of the game's time. The ticks run on the game's time, and stand with it.

So `UnscaledClock`, which runs while that time stands, says false, and its sequences wait for Unity's frames instead.
`GameClock` and a `ScaledClock` say as the clock they read does.

### Scaled clocks

A `ScaledClock` runs at a scale of its own against another clock. `new ScaledClock(scale, source)` makes one.

| Property | Description |
| --- | --- |
| `ScaledClock.Scale` | How fast it runs against its source: 1 as that one, 0.5 at half, 2 at double, 0 stopped. A scale below 0 is taken as 0. |
| `ScaledClock.Source` | The clock it's given, or, with none, the clock in force. |

Its `DeltaTime` and `FixedDeltaTime` are its source's times its scale. Its unscaled times and its frame are its
source's.

Its `Time` counts from the first time it's read: its source's time, at each scale it had. So a change of its scale or
its source carries on from where it was.

Making one reads no clock, so a component's field can make one where it's declared.

With no source, it counts whichever clock is in force as it's read. Give it a source to count by where a world of its
own steps.

The sequences that share one slow, speed up or stop together as it's set.

### Entity clocks

An entity's clock, `gas.GetClock()`, runs at the entity's `TimeScale`: `OwnScale` times `AttributeScale`.

| Property | Description |
| --- | --- |
| `OwnScale` | The scale code sets, as a hit-stop's 0. |
| `AttributeScale` | The scale the entity's **Time Scale** attribute gives, read as each of the entity's steps begins. It's 1 where its identity names none ([ENTITIES.md](ENTITIES.md#attributes)). |

Setting `TimeScale` sets `OwnScale`. So a hit-stop holds under a slow, and the slow is still there after.

Its `DeltaTime` and `FixedDeltaTime` are the game's times its scale. Its `Time` is its steps added up since it
started. Its unscaled times and its frame are the game's.

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

A class of your own that implements `IForgeClock` is read as any clock is. You can:

- put it in with `ForgeTime.Use`;
- name it by a packet's `Clock`, or a sequence's `WithClock`;
- give it by a clock pick of your own ([Picking a clock in an inspector](#picking-a-clock-in-an-inspector)).

Its `FollowsTicks` is true unless it says otherwise.

`GameClock`, `UnscaledClock`, `ScaledClock` and `EntityClock` read the clock in force. So a world of its own's steps
and the fixed tick drive them. A clock that reads Unity's `Time` itself doesn't follow them.

## Which clock a run takes

Everything PlayForge times goes by a clock, most by their owner's. An effect goes by the entity it's on, and a
sequence's run by the first clock it finds.

- **An effect** counts its time by the clock of the entity it's on, its duration and its period both. So a slowed
  entity's effects, its cooldowns among them, last longer. Its **Delta Time Scaler** changes it further
  ([EFFECTS.md](EFFECTS.md#duration)).
- **An entity's own step** goes by its clock: its input buffer's windows, its services, its turn and its barriers'
  decay.
- **A sequence's run**, an ability's among them, takes the first clock it finds, in the order below.
- **A synced runner** steps by its packet's clock, else its sequence's, else its packet's owner's
  (`runner.ClockFor(data)`).
- **A cue**'s packet carries its owner's clock: the activation's for an ability's cue, else that of the one it plays
  on. A Sequence cue's run goes by its **Clock** pick, else the sequence's own, else that one
  ([CUES.md](CUES.md#data-packets)).
- **Code of your own** reads its owner's step with `ForgeTime.DeltaTimeFor(owner)`, as `ProjectileMonoProcess` moves by
  its handler's. A task reads its run's with `SequenceTaskLibrary.DeltaTime(token)`, 0 while the run is paused.
  `TaskSequenceRuntime.ClockOfRun(token)` is the run's clock.

Anything else goes by the game's time, the clock in force.

### The clock a sequence's run takes

A sequence's run, an ability's among them, takes the first clock it finds, in this order:

1. its own, `runtime.Clock`, which what runs it sets: a `TaskSequenceProcess`'s `Clock`, a chain's `Clock` or an
   ability's Run Sequence task's pick;
2. its packet's, `data.Clock`;
3. its sequence's, `WithClock` on the builder;
4. its packet's owner's: an ability's caster's own clock;
5. else the game's, `GameClock.Instance`.

Its waits, delays, timeouts, longest runs and `ElapsedTime` go by it, and so do its tasks' tweens and its synced tasks
([TASK_SEQUENCES.md](TASK_SEQUENCES.md#clocks)).

An ability runs on its activation's packet. So its waits go by its caster's clock, unless the packet names another.

## Picking a clock in an inspector

An inspector picks a clock with an `AbstractClockChoice` field, as an ability's Run Sequence task's **Clock** does.
Each kind of pick gives the clock for a run on a packet, as the run starts.

| Pick | Kind | The clock a run gets |
| --- | --- | --- |
| None | The field left empty | The clock it would have had |
| **Caster's** | `CasterClockChoice` | An ability's caster's own time, its time scale times the game's; the game's for any other packet |
| **Game's** | `GameClockChoice` | The game's, whatever an ability's caster's is: a slowed caster's sequence on it isn't slowed |
| **Unscaled** | `UnscaledClockChoice` | The game's without its time scale, which runs while the game's time is slowed or stopped |
| **Scaled** | `ScaledClockChoice` | A `ScaledClock` made for each run, at its **Scale** (1 as the clock it scales, 0.5 at half, 2 at double), over the clock its **Of** picks, or the caster's where it picks none |

A pick's `ClockFor(data)` gives its clock for a run on the packet. The base gives null, which leaves the run the clock it
would have had.

**Caster's** reads the packet's owner, not a clock the packet names, since a pick is what names one.

### Where the picks are offered

Three things offer the clock picks:

- An ability's Run Sequence task. Its pick is its run's alone, so the ability's other tasks keep the caster's
  ([ABILITIES.md](ABILITIES.md#stages)).
- A Sequence cue. Its run goes by its pick, else the sequence's own clock, else its packet's: the clock of the cue's
  owner ([CUES.md](CUES.md#data-packets)).
- A component of your own, through a `[SerializeReference]` field. Its pick sets its packet's clock.

This component runs its sequence on the clock its inspector picks:

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

### A pick of your own

You can offer a clock pick of your own. A `[Serializable]` class of your own made from `AbstractClockChoice` is offered
beside PlayForge's picks.

It's offered by its `[ForgeMenu]` name, or else by its class's name without "ClockChoice". The picker makes it with its
parameterless constructor.

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

ProcessControl's fixed tick steps the game in fixed steps of its time. A frame runs as many as its time holds, so a run
comes out the same at any frame rate.

Set it under **Fixed Tick** in ProcessControl's inspector:

| Property | Description |
| --- | --- |
| **Fixed Tick Rate** | The ticks in a second of game time, as 60. 0, the default, steps once a frame. In code, it's `ProcessControl.FixedTickRate`. |
| **Max Ticks Per Frame** | The most ticks a frame runs, 8 by default. A frame too long for them drops the rest of its time, so the game doesn't fall further behind. |

### Inside a tick

A tick steps the game in this order:

1. What waits for a tick goes on.
2. Every process steps at Update.
3. Every process steps at LateUpdate, its end of frame with it.

While a tick runs, the clock in force is the tick's. `ForgeTime.DeltaTime` is its step, and `ForgeTime.FrameCount` the
ticks so far.

A process that steps at FixedUpdate steps with Unity's physics, as ever.

The ticks run on the game's time, so they stand while it does.

### Waiting on ticks

Code waits on the ticks through `ForgeTick`:

| `ForgeTick` | Waits for |
| --- | --- |
| `Next(token)` | The next tick, or, with ticks off, Unity's next frame, as a sequence's waits do |
| `Next(timing, token)` | The same at a frame's timing: the Update family waits for a tick, and FixedUpdate, the physics step's, for Unity's |
| `NextFrame(token)` | The next tick, or, with ticks off, Unity's next frame as `UniTask.NextFrame` waits for it |
| `WaitUntil(condition, token)` and `WaitWhile(condition, token)` | A condition, asked once a tick, or once a frame with ticks off |
| `Next(clock, token)` | The next step of a clock's time. For a clock whose `FollowsTicks` is false, as `UnscaledClock`'s is, that's Unity's next frame, where the scene ticks. For any other clock, it waits as `Next(token)` does |

| Member | What it says |
| --- | --- |
| `ForgeTick.Ticking` | Whether the tick in force ticks. |
| `ForgeTick.Ticks` | How many ticks it has run. |
| `ForgeTick.Scene` | The scene's tick, a `ForgeTickSource`, which ProcessControl's rate turns on. |
| `ForgeTick.Current` | The tick in force: a world of its own's while it steps or one of its processes changes state, the scene's otherwise. |

A `ProcessWorld` its owner steps ticks once for each `Step(deltaTime)`. So its sequences wait on its steps.

## Random numbers

`ForgeRandom` is where PlayForge draws its random numbers. The game's rolls and random picks all draw from its source,
an `IForgeRandom`.

| Member | What it draws |
| --- | --- |
| `ForgeRandom.Value` | A number from 0 to 1, as `Random.value`. |
| `InsideUnitCircle`, `InsideUnitSphere` | A point within a radius of 1. |
| `Range(min, max)` | A float from the first to the second. |
| `Range(min, maxExclusive)` | A whole number, up to but not including the second. Where the range is empty, the first itself. |

### The source

`ForgeRandom` draws from Unity's source, `UnityRandom`, through `UnityRandom.Instance`, until code puts another in. It
reads `UnityEngine.Random`, so `Random.InitState(seed)` seeds it, and anything else that draws from Unity's moves it.

A `SeededRandom`, made with a seed, draws numbers of its own that nothing else draws from. Two runs from one seed draw
the same numbers.

| Method | What it does |
| --- | --- |
| `ForgeRandom.Use(source)` | Puts a source in, and returns the one it replaced. |
| `ForgeRandom.Use(null)` | Puts Unity's back. |
| `ForgeRandom.Source` | The source drawn from now. |

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

An `IForgeRandom` of your own gives `Value`, from 0 to 1, and `InsideUnitCircle` and `InsideUnitSphere`, a point in a
circle and in a sphere of radius 1.

> [!NOTE]
> A source put in stays until code puts another in, past the end of play mode where domain reload is off. So code that
> puts one in puts back the one it replaced.

### What draws from it

These draw from `ForgeRandom`:

- every chance PlayForge rolls ([Chance](#chance));
- auto targeting whose **Mode** is **Random**;
- a **Randomized** scaler's roll ([SCALERS.md](SCALERS.md#a-magnitude-field));
- `ForgeCollections`' random picks and `ForgeHelper`'s points ([ForgeHelper](#forgehelper));
- the Creator Lab.

Cues don't. A placement's scatter and a floating text's random velocity draw from `ForgeCues.Random`, a `SeededRandom`
of their own, so a cue never moves the game's rolls ([CUES.md](CUES.md#placement)).

## Chance

`ForgeChance` rolls every chance PlayForge has. Each rolls by its `ChanceRoll`, which says whether it's pseudo-random
and whose luck it reads.

PlayForge's chances are:

- an effect's **Chance to Apply**;
- an ability's **Chance to Activate**;
- a trigger's **Chance**;
- a Value Pipeline's **Chance**, **Block** and **Evade** stages;
- the **Chance** condition.

Each has a `ChanceRoll` beside it, its **Chance Roll**, **Activation Roll** or **Roll**, which says how it rolls
([EFFECTS.md](EFFECTS.md#requirements)):

| Property | Description |
| --- | --- |
| **Pseudo Random** | Each miss raises the next roll's odds, and a hit starts them again ([Pseudo-random rolls](#pseudo-random-rolls)). |
| **Own Luck** | The roll reads the chance's own **Luck**, in place of the caster's attribute in the project's Luck role ([Luck](#luck)). |
| **Luck** | The chance's own luck, a number read for the roll. |

### Rolling a chance

`ForgeChance.Roll(chance, in context, roll, key)` says whether a chance, from 0 to 1, comes up for the caster its
`NumberContext` gives.

A chance of 1 or more always comes up, and one of 0 or less never does. Neither draws, so a chance that can't miss
leaves the numbers others draw as they were.

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

### Pseudo-random rolls

A pseudo-random roll evens out streaks: each miss raises the next roll's odds, and a hit starts them again. The chance
over many rolls stays the same.

The odds are `ForgeChance.Constant(chance)` times one more than the misses in a row, at most 1. That constant is the
one whose distribution's mean is the chance, as Dota's pseudo-random distribution is.

Each caster keeps its own streak for each chance, by the chance's key. PlayForge's own chances key by what holds them:
the effect, the ability, the trigger, the stage or the condition.

A pseudo-random roll with no key or no caster rolls its chance as it is.

A caster's streaks go with it. `ForgeChance.ForgetStreaks(caster)` forgets them, as a test does to start clean.

### Luck

A roll's luck, `ForgeChance.LuckOf(in context, roll)`, is the first of these that applies:

1. With **Own Luck**, the chance's own **Luck**, a number read for the roll.
2. Else, the current value of the caster's attribute playing the project's Luck role, `ForgeChance.ProjectLuck`
   ([ATTRIBUTES.md](ATTRIBUTES.md#roles)).
3. Else, 0.

Either is rounded to a whole number.

Above 0, the roll draws that many more times and keeps the best. Below 0, it keeps the worst. A draw below the odds
comes up.

### The Chance condition

The **Chance** condition, `ChanceCondition`, holds on a roll of its **Chance**, rolled by its **Roll**. Its **Chance**
is a number of any kind, as a critical strike chance read from an attribute.

Read in a hit, it rolls once and keeps the roll, so the hit's preview and its commit read the same. Read with no hit, it
rolls each time it's read.

## Repeatable runs

A run that must come out the same twice needs the same time and the same rolls each time it runs. A replay, a rollback
and a lockstep match are such runs.

- **Time**: step the game in fixed steps, with ProcessControl's **Fixed Tick Rate** ([Fixed ticks](#fixed-ticks)). Or
  step a world of its own by a fixed time, `world.Step(deltaTime)`. A clock of the game's time follows either.
- **Rolls**: put in a `SeededRandom`, `ForgeRandom.Use(new SeededRandom(seed))`, seeded for the run, or for each step
  as a rollback does ([SAVING.md](SAVING.md#rolling-back)). Unity's random numbers, the default, move whenever anything
  else in the game draws from them.
- **Cues** draw from `ForgeCues.Random`, numbers of their own. So a run that plays its cues rolls as one that doesn't,
  with `ForgeCues.Enabled` off, as on a server or while a simulation runs ahead.
- **Your own code** reads time through `ForgeTime` and draws through `ForgeRandom`, as PlayForge does. Code that reads
  Unity's `Time` or `Random` itself follows neither a clock nor a source put in.

## Time and rolls in tests

The test kit makes time and chance a test's own, through the same two services ([TESTING.md](TESTING.md#the-world)).

| Method | What it does |
| --- | --- |
| `World.Clock(step)` | Puts in a `SteppingClock`, and returns it. |
| `World.Rolls(0.1f, 0.9f)` | Puts in a `ScriptedRandom`. |
| `ForgeChance.ForgetStreaks(caster)` | Starts a caster's pseudo-random streaks clean. |

A `SteppingClock` gives `step` seconds of PlayForge's time each frame: its `DeltaTime`, `UnscaledDeltaTime` and
`FixedDeltaTime`. Its `Time`, `UnscaledTime` and `FrameCount` stay Unity's. Its `Step` can change.

A `ScriptedRandom` gives each draw the next of its rolls, in turn and round again, and its `Drawn` counts them. Its
points in a circle and in a sphere are zero.

A chance comes up where it's above the roll. So a roll of 0 always lands, and one of 0.99 almost never does. A roll with
luck draws more than one.

The world gives back the clock and the source it replaced as it ends. Outside it,
`ForgeTime.Use(new SteppingClock(step))` and `ForgeRandom.Use(new ScriptedRandom(0f))` do the same, put back by hand.

## Pointer input

`ForgeInput` is what PlayForge reads of the player's input:

- the press and aim of click targeting;
- the task libraries' waits for a key or a press;
- the Creator Lab's controls.

`ForgeInput.Current` is the input read now, an `IForgeInput`. It's the project's own, `ForgeInput.Default()`, until code
puts another in.

| Method | What it does |
| --- | --- |
| `ForgeInput.Use(input)` | Puts an input in, and returns the one it replaced. |
| `ForgeInput.Use(null)` | Puts the project's own back. |

The project's own input is the first of these that applies:

1. `InputSystemPointerInput`, where the Input System package is installed and the Player settings'
   **Active Input Handling** takes it, as it does where it takes both.
2. Else `LegacyPointerInput`, where it takes the Input Manager.
3. Else an input that never presses and aims nowhere.

An `IForgeInput` has seven members:

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

Three parts of PlayForge read `ForgeInput`:

- **Click targeting**, the Select tasks (Position, Direction, GAS Target, GAS Or Position and GAS Target In Range),
  waits for `PointerPressed` of its **Mouse Button**. Then it aims along `TryGetPointerRay`
  ([ABILITIES.md](ABILITIES.md#activation)).
- **The task libraries' waits**: `WaitForKey` and `WaitForAnyKey` read `KeyPressed`, and `WaitForPointer` reads
  `PointerPressed`, in `SequenceTaskLibrary` and `SyncSequenceTaskLibrary` alike
  ([TASK_SEQUENCES.md](TASK_SEQUENCES.md#waiting)).
- **The Creator Lab**: its presses and keys, the pointer's place, the middle button held to pan, and the wheel to zoom
  ([CREATOR_LAB.md](CREATOR_LAB.md)).

### The defaults

The project's own inputs are `LegacyPointerInput` and `InputSystemPointerInput`. Each aims through its `Camera`, or
`Camera.main` where that's null.

| Input | What it reads |
| --- | --- |
| `LegacyPointerInput` | The Input Manager's mouse and keys. It has no pointer position where no mouse is present. |
| `InputSystemPointerInput` | The Input System's. A mouse presses each of its three buttons, and a pen or a touch the primary. Its `ScrollDelta` is the mouse's scroll over 120, a notch on most mice. |

`InputSystemPointerInput` reads these keys:

- the letters;
- the top row's digits;
- F1 to F12;
- Escape, Space, Return, Tab, Backspace and Delete;
- the arrows;
- left and right Shift, Control and Alt.

Any other `KeyCode` reads as never pressed.

Each is compiled only where its input handling is on. So code that names one sits under the same condition:

| Input | Condition |
| --- | --- |
| `LegacyPointerInput` | `#if ENABLE_LEGACY_INPUT_MANAGER` |
| `InputSystemPointerInput` | `#if ENABLE_INPUT_SYSTEM`, with the Input System package installed |

#### Aiming through a camera

`ForgeInput.TryAimThrough(camera, screenPoint, out ray)` gives the ray from a camera through a point on the screen: the
camera given, or else `Camera.main`. With neither, it logs a warning of the **Input** system, and returns false.

`ForgeInput.CameraOr(camera)` is the camera given, or else `Camera.main`, as a floating text faces.

### Input of your own

A game with a crosshair, a gamepad's reticle or a controller's ray writes an `IForgeInput`, and puts it in.

Give it all seven members, passing those it doesn't change on to the project's own. An input that writes only the two
click targeting reads has no key, no pointer position and no scroll.

Through such an input, `WaitForKey`, `WaitForAnyKey` and the Creator Lab's keys, clicks and camera hear nothing.

> [!NOTE]
> An input put in stays until code puts another in, past the end of play mode where domain reload is off. So code that
> puts one in puts back the one it replaced.

This input aims from the crosshair in the middle of the screen, and passes the rest on to the project's own:

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

`ForgeLog` is where everything PlayForge says goes. Each message has a level and a system, and a gate on the two
decides what reaches Unity's Console.

- **Levels**, `ForgeLog.ELevel`, from the most said to the least: `Verbose`, `Info`, `Warning`, `Error`, and `None`.
  `None` lets nothing through, errors neither ([Log levels](#log-levels)).
- **Systems**, `ForgeLog.ESystem`: General, Effects, Abilities, Attributes, Workers, Numbers, Processes, Tags, Items,
  Saves, Input, Lab and Sequences. Each can take a level of its own.

Its messages reach the Console with `[PlayForge]` before them: a warning as a warning, and an error as an error.

Its methods are marked `[HideInCallstack]`. So the Console's **Strip Logging Callstack** leaves them out of the stack,
and a double-click on a message opens the code that logged it.

### Logging from code

Your code logs through `ForgeLog` as PlayForge does.

| Method | What it does |
| --- | --- |
| `ForgeLog.Verbose`, `ForgeLog.Info`, `ForgeLog.Warning`, `ForgeLog.Error` | Take a message and the Unity object the Console points at, its `context`, as a message of **General**. Given a system first, they take the entity the message is about too, its `subject`. |
| `ForgeLog.Exception(system, exception, context, subject)` | Logs an exception as Unity's `Debug.LogException` does, its stack in the Console, where an error is let through. |
| `ForgeLog.Allows(system, level)` | Says whether a message would be let through, so a costly one is built only when it will be shown. |

A message of a system names the system first, as `ForgeLog.Warning(ForgeLog.ESystem.Abilities, "No target", this, gas)`.

### Hearing the log

`ForgeLog.Logged` hears each message let through, as a `ForgeLogEntry`:

| Field | Description |
| --- | --- |
| `Level`, `System` | The message's level and system. |
| `Message` | The message, without the prefix. |
| `Context`, `Subject` | The object the Console points at, and the entity the message is about. |
| `Time`, `Frame` | The game's time and frame as it was logged. |

An exception is heard as its type and its message.

An entry's `ToString()` writes it in a line, as "Effects, warning: …". The Creator Lab's Log page hears the messages
so.

This component keeps an in-game console of the warnings and errors PlayForge lets through:

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

A message is let through where its level is at least its system's level. That's the system's own, where it has one,
or else `ForgeLog.MinimumLevel`, Info by default.

| Method | What it does |
| --- | --- |
| `ForgeLog.SetLevel(system, level)` | Gives a system a level of its own. |
| `ForgeLog.SetLevel(system, null)` | Gives the system back to `ForgeLog.MinimumLevel`. |
| `ForgeLog.LevelOf(system)` | Gives the system's level now. |
| `ForgeLog.HasOwnLevel(system)` | Says whether the system has a level of its own. |
| `ForgeLog.ClearLevels()` | Gives every system back. |

> [!NOTE]
> What's said only when asked is information, of level Info: its switch makes it, and the level shows it
> ([Process and sequence logs](#process-and-sequence-logs)). A level above Info hides it, however it's switched on.

### In the editor

In the editor, you set the level in PlayForge's preferences. `Edit → Preferences → PlayForge` sets
`ForgeLog.MinimumLevel` with **Minimum Log Level**, under **Logging**.

The level is yours, not the project's. It's kept in the editor's preferences, and put back on each domain reload.

**Use Defaults** puts every preference on the page back, the level to Info. Editor code sets the level with
`ForgeLogSettingsBridge.SetLevel(level)`.

The Creator Lab's **Log** page gives a system a level of its own, with **System** and **Level**. **As all** gives it
back to the preference's ([CREATOR_LAB.md](CREATOR_LAB.md#showing-its-working)).

### In a build

A build reads no preference. It logs Info and above, unless its code sets the level.

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

## Process and sequence logs

ProcessControl and task sequences have switches of their own, which decide whether they say what they do. Most of what
they say is information, which the level must let through to show.

### ProcessControl's logs

**ProcessControl** logs as the **Processes** system, by two switches ([PROCESSES.md](PROCESSES.md#logging)):

| Property | Description |
| --- | --- |
| **Output Logs** | Off by default. Logs each process registered and unregistered, the scene's processes it collects as it starts, and the hierarchy it makes of them. Logs, as a warning, a change of state its rules don't cover. |
| **Detailed Logs** | On by default. Adds to those while **Output Logs** is on: each process's handler, each process collected, and each process's state as it's queued, set and forced. |

A world of its own has the same switches, `ProcessWorld.OutputLogs` and `ProcessWorld.DetailedLogs`. A scene's world's
are its ProcessControl's.

### Sequence logs

**Sequences** log as the **Sequences** system ([TASK_SEQUENCES.md](TASK_SEQUENCES.md#logging)). They log their errors
unless told otherwise:

- A run that fails is logged as an error, with what it threw, unless its sequence's `WithErrorLogging(false)` says
  otherwise.
- A stage whose `OnException` hears a throw and lets it on logs it too, unless the stage's `WithErrorLogging(false)`
  says otherwise.
- A handler that throws, as an `OnComplete`, is logged as an error.

Two switches log more:

- `WithInjectionLogging(true)`, on a sequence or a stage, logs each injection as it's applied: its kind, where it came
  from and the stage.
- A chain's `WithLogging(true)` logs its progress: each sequence it starts, finishes or skips, the conditions that fire,
  its repeats and its interruptions.

### Debug tool logs

**Debug tools** say what they find as information too:

| Tool | System |
| --- | --- |
| `TagCache.LogWeights()` | **Tags** |
| The Threshold Debugger effect worker, `ThresholdDebuggerEffectWorker` | **Workers** |
| A queued `LogAction` | **General** |

## Profiling

PlayForge's work shows in Unity's Profiler ([GETTING_STARTED.md](GETTING_STARTED.md#profiling)):

- markers that time it;
- counters that say how much of it there is;
- each recorded frame's costliest processes.

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

`ForgeCounters` holds PlayForge's counters, in the Profiler's PlayForge category. The category's name is
`ForgeCounters.CategoryName`, and the category itself is `ForgeCounters.Category`.

Each counter is of one of two kinds:

| Kind | How it counts |
| --- | --- |
| Level | It holds until it changes. |
| Per frame | Its count starts from 0 each frame, and counts every world's work in the frame. |

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

Two Profiler modules chart the counters. Turn them on in the Profiler window's list of modules:

- **PlayForge Processes** charts the process and sequence counters.
- **PlayForge GAS** charts the rest.

### The costliest processes

While the Profiler records, each frame's costliest processes go into its capture as the frame's metadata, so a saved
capture keeps them. The scene's ProcessControl puts them in as its frame ends.

The **PlayForge Processes** module's details list the selected frame's: each process's ID, its type and the time its
steps took.

| Member | Description |
| --- | --- |
| `ForgeFrameCosts.Most` | How many a frame keeps: 10. |
| `ForgeFrameCosts.LastFrame` | The last recorded frame's, the costliest first, each a `ForgeProcessCost`. |
| `ForgeFrameCosts.Recording` | Whether this frame's steps are timed: whether the Profiler recorded as the last frame ended. While it doesn't, nothing is timed for them. |

A `ForgeProcessCost` holds:

- its `Process`, the process's ID;
- the `Nanoseconds` its steps took in the frame, every timing's.

#### Reading the costs in a tool

A tool of your own reads the costs from a frame's data, as the module does:

- the costs, under the ID `ForgeFrameCosts.MetaDataId` and the tag `ForgeFrameCosts.Tag`;
- the type's name of the cost at `i`, in UTF-8, under the tag `ForgeFrameCosts.Tag + 1 + i`.

### Reading them in code

A `ProfilerRecorder` reads a marker or a counter by its name, in the editor or in a development build. Unity compiles
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

An entity service is a per-entity object that an entity's components share. It's made the first time it's asked for,
and stepped by the entity's own time.

The auras an entity runs, `AuraSet`, and the wait a Respawn counts down, `RespawnTimer`, are two of them.

| Method | What it does |
| --- | --- |
| `gas.GetService<T>()` | Gives the entity's service of a kind, made the first time it's asked for. |
| `gas.FindService<T>()` | Gives it without making one, or null. |

A service derives from `AbstractEntityService`, with a parameterless constructor. It overrides the hooks it needs, each
a no-op otherwise. Its `System` is the entity it's of.

| Method | When it runs |
| --- | --- |
| `Step(deltaTime)` | Each frame, by the entity's own time, after its effects tick and before its turn. |
| `Revive()` | As an entity that stays revives, before anything else of the revive. |
| `End()` | As the entity ends. The entity keeps none of its services after. |

[ENTITIES.md](ENTITIES.md#services) has the rest.

This service keeps a heat meter that cools by its entity's own time:

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

`ForgeHelper` is a static class of the checks and small sums PlayForge's own systems share. A game's workers,
conditions and tools can call it too.

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
| Points | `RandomPointWithinCircle`, `RandomPointWithinSphere` | A point at random within a radius of the origin, drawn from `ForgeRandom`. The circle's lies in the XY plane, a `Vector3` with a z of 0, and one nearer than its `minRadius` is moved out to it |
| Icons | `GetTextureItem`, `TextureUnder` | An icon from an asset's **Textures**: `GetTextureItem` the texture under a tag, else the first that has one, and `TextureUnder` the one under exactly that tag, or null |

### ForgeCollections

The collection helpers are `ForgeCollections`', beside `ForgeHelper`: extension methods, so `list.RandomChoice()`
reads as the list's own.

| Members | What they do |
| --- | --- |
| `RandomChoice`, `Shuffle`, `WeightedSample`, `SafeAdd`, `SafeAddRange`, `SafeRemove`, `ContainsAll`, `TrueForAny` | A list's item at random; the list shuffled in place; N items picked by their weights, an item as often as it's drawn; a dictionary's entries added, or added to a key's list or set, and removed; and whether a sequence holds every item of another, or has an item a condition holds for. The random ones draw from `ForgeRandom` |

## Packet helpers

`ProcessTaskUtil` holds extension methods for a data packet's targets and the effects it carries. It also holds three
loops for async code, which wait through `ForgeTick`.

### Targets and effects

These extension methods read and file a data packet's targets, and apply the effects it carries.

| Method | What it does |
| --- | --- |
| `data.GetTargetingPacket(tag, out packet)` | Gives the primary `AbstractTargetingPacket` under a tag. Where there's none, it's false, and it interrupts the packet's sequence, where it's a `SequenceDataPacket`, unless it's called with `autoInterrupt: false`. |
| `data.SetTargetingPacket(tag, packet)` | Files a packet as the primary. |
| `data.SetTargetingPackets(tag, targets)` | Files the targeting packet of each target, in their order and in place of what was there, so the first is the primary. |
| `data.TryApplyEffects(target, tag)` | Applies each effect loaded under the tag to a target, an `ITarget` or a `GameObject` that has one. Each is made from the ability's origin, with the magnitudes its caller set. |
| `data.TryGetLoadedAssets<T>(tag, out assets)`, `data.GetLoadedAssets<T>(tag)` | Give every value of a kind under a tag, as a `DataValue<T>`, empty where there's none. |

`data.TryApplyEffects(target, tag)` is false where:

- the packet isn't an ability's, an `AbilityDataPacket`;
- it holds no effects under the tag;
- there's no target.

### Async loops

`ProcessTaskUtil` has three loops for async code, which wait a tick at a time through `ForgeTick`.

| Method | What it does |
| --- | --- |
| `ProcessTaskUtil.WhileAsync(condition, token)` | Waits while a condition holds, a tick at a time. |
| `ProcessTaskUtil.DoWhileAsync(body, condition, token)` | Awaits the body, then a tick, while the condition holds, the body at least once. |
| `ProcessTaskUtil.DoWhile(body, duration, token)` | Does so until the duration has passed, by `ForgeTime.Time`, or by its unscaled time where its `useUnscaledTime` says. |

Each takes a `PlayerLoopTiming`, Update by default, and waits as `ForgeTick.Next` does ([Fixed ticks](#fixed-ticks)).

## The attribute registry

`AttributeRegistry` finds an attribute by its name, among those the game's entities have been given.

An attribute is registered under its name:

- as an entity's attribute system takes it;
- as a Bootstrapper brings PlayForge up, for each attribute of the sets its **Register Sets** lists.

The first of a name keeps it: `AttributeRegistry.Add(attribute)` is false where the name is taken.

Names compare by `AttributeRegistry.RefactorByNamingConvention(name)`: in capitals, with each space an underscore. So
"Attack Speed", "attack speed" and "ATTACK_SPEED" are one name.

| Method | What it does |
| --- | --- |
| `AttributeRegistry.TryGetByName(name, out attribute)` | Finds an attribute by its name. |
| `AttributeRegistry.GetByName(name)` | Gives the attribute, or null. With `createIfFalse: true`, a name none has gives a new `RuntimeAttribute` of that name, which isn't registered. |
| `AttributeRegistry.Contains` | Asks by a name, or by an attribute's name. |
| `AttributeRegistry.GetAll()` | Lists the attributes registered. |
| `AttributeRegistry.Reset()` | Empties the registry, as the test kit's world does as it ends. |

The registry starts empty each time the game starts, play mode with domain reload off among them.

## Lists

`ReadOnlyListView<T>` and `ForgeUtils` are two small list helpers:

- `ReadOnlyListView<T>` is a view of a list that `foreach` walks without allocating.
- `ForgeUtils` gives the index of a list's least or greatest item.

`ReadOnlyListView<T>` is a read-only struct over a `List<T>`. It has a `Count`, an indexer, and a `GetEnumerator` that
hands `foreach` the list's own struct enumerator, so walking it allocates nothing.

Walked as an `IEnumerable<T>`, as LINQ walks it, its enumerator is boxed, as any list's is. A view of no list reads as
empty. An effect spec's `GetImpactedAttributes(delivery)` returns one.

| Method | What it gives |
| --- | --- |
| `ForgeUtils.IndexMin(list, selector)` | The index of the item whose selected value is least. |
| `ForgeUtils.IndexMax(list, selector)` | The index of the item whose selected value is greatest. |

Each gives the first of equals, and 0 for an empty list.

## Sign and math policies

Three enums say how PlayForge reads the sign of a change and turns a number into a factor: `ESignPolicy`,
`ESignPolicyExtended` and `EMathApplicationPolicy`.

`ESignPolicy` is a change's sign:

| Option | What it means |
| --- | --- |
| `Positive` | The change is positive. |
| `Negative` | The change is negative. |
| `ZeroBiased` | Its parts sum to 0 but aren't all 0, as +5 to an attribute's current value and −5 to its base. |
| `ZeroNeutral` | Every part is 0. |

`ForgeHelper.SignPolicy` reads it from one number, which is never `ZeroBiased`, or from a change's current and base
parts. A change's own `SignPolicy`, as a `SourcedModifiedAttributeValue`'s, is its parts read so.

An impact worker made from `AbstractContextImpactWorker` takes the hits of one sign, its **Impact Sign**.

`ESignPolicyExtended` adds `Any`, which takes every sign. A focused attribute worker's **Sign Policy** takes every sign
by default ([WORKERS.md](WORKERS.md#the-map)).

`EMathApplicationPolicy` turns a number into a factor:

| Option | What it does |
| --- | --- |
| `AsIs` | The number as it is. |
| `OnePlus` | One plus it: a bonus of 0.2 multiplies by 1.2. |
| `OneMinus` | One minus it: a resistance of 0.2 leaves 0.8. |

`AttenuateScalerBehaviour`'s **Policy** and `MathAttributeWorker`'s **Operation Policy** read it.
