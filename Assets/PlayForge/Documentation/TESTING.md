# PlayForge — Testing Your Content

A game's abilities, effects and items are easiest to trust when a test plays them as the game does: a burn's ticks
landing over its second, a ring's armor while it's held, a cast paying its cost.

PlayForge's test kit is the assembly `FarEmerald.PlayForge.Testing`, in the package's `TestKit` folder, which a
project's own tests reference. For each test, the kit:

- brings up a live PlayForge, and steps it a frame at a time;
- makes entities on either hub, and plays them;
- checks afterwards what must always hold, and takes it all down again, so tests run in any order.

## Setting up

The kit is compiled wherever the **Test Framework** package (`com.unity.test-framework`) is installed, as the Test
Framework's own test runner is.

To use it from your project's tests:

1. Use a test assembly of the project's: an assembly definition whose **Define Constraints** hold `UNITY_INCLUDE_TESTS`,
   referencing `nunit.framework.dll`.
2. Add these to its **Assembly Definition References**:
   - `FarEmerald.PlayForge` and `FarEmerald.PlayForge.Testing`
   - `UniTask`
   - `UnityEngine.TestRunner` and `UnityEditor.TestRunner`
3. Give the assembly every platform, not the Editor alone.

The assembly needs every platform because the kit's tests are PlayMode tests, which step the real player loop.

## A test

A test class derives from `ForgeTest`. `ForgeTest` makes a `ForgeTestWorld` before each test, and audits and ends it
after the test.

A test is a `[UnityTest]` whose body runs through `Live`, from a frame's end:

```csharp
using System.Collections;
using FarEmerald.PlayForge;
using FarEmerald.PlayForge.Testing;
using NUnit.Framework;
using UnityEngine.TestTools;

public class BurnTests : ForgeTest
{
    [UnityTest]
    public IEnumerator A_burn_ticks_twice_in_its_second([Values] Hub hub) => Live(async () =>
    {
        var health = Make.Attribute("Health");
        var target = World.Entity(hub, Make.Set(Make.Pool(health, 100f, 100f)), "Target");
        var burn = Make.Effect("Burn", new EffectConfig
        {
            Policy = EEffectDurationPolicy.Durational, Target = health, Magnitude = -10f,
            Duration = 1f, PeriodicTicks = true, Ticks = 2,
        });

        Assert.IsTrue(World.Hit(World.Entity(hub, name: "Source"), target, burn));
        await Frames.Until(() => World.Value(target, health) <= 80f, 90, "its two ticks land");
        Assert.AreEqual(80f, World.Value(target, health), 0.001f);
    });
}
```

A test class whose tests need a base class of their own makes a world instead, and ends it, with `await using`:

```csharp
[UnityTest]
public IEnumerator My_test() => UniTask.ToCoroutine(async () =>
{
    await using var world = await ForgeTestWorld.Start();
    // ... the test, through world; then, if it wants the checks, await world.Audit()
});
```

## The world

The world makes a test's entities and assets, plays them, reads them, and controls its time, its chance and its
processes.

**Entities**: the world makes entities on either hub.

| Member | What it does |
|---|---|
| `World.Entity(hub, set, name)` | Makes an entity of an attribute set on either hub: `Hub.Runtime`, a `RuntimeGameplayAbilitySystem`, or `Hub.Mono`, a `GameplayAbilitySystem` on a GameObject of its own. |
| `World.FromIdentity(hub, identity)` | Makes an entity from an identity asset, the game's own or one `Make` built, with its set, its starting abilities and items, and its sides. |
| `RuntimeEntity`, `MonoEntity` | Make an entity on one hub. |
| `NewMonoEntity` | Makes a Mono hub's entity, not yet registered. |

**Playing**: each of these says whether it worked.

| Member | What it does |
|---|---|
| `World.Hit(source, target, effect)` | Applies an effect, as a hit. |
| `World.Cast(caster, ability, target, level)` | Gives an ability and asks it to start, as a player does. |
| `Cast(caster, slot, target)` | Starts the ability in a slot. |
| `World.Use(caster, ability, target)` | Starts an ability the entity has already, as an item's. |
| `World.Hold(holder, item, stacks)` | Gives and equips an item. |

**Reading**: each reads an entity, passed as `gas`.

| Member | What it reads |
|---|---|
| `World.Value(gas, attribute)` | An attribute's current value, or NaN where the entity hasn't the attribute. |
| `World.Base(gas, attribute)` | An attribute's base value, or NaN where the entity hasn't the attribute. |
| `World.Has(gas, effect)` | Whether an effect is on the entity. |
| `World.Stacks(gas, effect)` | An effect's stacks on the entity. |
| `World.HasTag(gas, tag)` | Whether the entity has a tag, or one under it. |

**Time**: a test steps whole frames, each `Frames.DeltaTime` long, with one FixedUpdate. `Frames.DeltaTime` is 1/60 of a
second, or a class's `DeltaTime`.

| Member | What it does |
|---|---|
| `Frames.Step(n)` | Runs whole frames. |
| `Frames.Seconds(s)` | Runs as many frames as that time takes. |
| `Frames.Until(condition, within, what)` | Steps until the condition holds, failing past `within` frames. |
| `ExactlyIn` | Fails unless the condition holds exactly so many frames on. |
| `Holds` | Fails if the condition stops holding. |
| `World.Clock(step)` | Makes each frame take `step` seconds of PlayForge's own time (`ForgeTime`) instead. |

**Chance**: `World.Rolls(0.1f, 0.9f)` makes each roll come out as written, in turn and round again. It returns the
rolls, counting what's drawn (`Drawn`). A chance lands where it's above the roll, so a roll of 0 always lands.

**Processes**: the world ends each process it owns, if the test doesn't.

| Member | What it does |
|---|---|
| `World.Register(process)` | Registers a process, which the world ends if the test doesn't. |
| `World.Own(relay)` | Makes the world end a process registered some other way. |
| `World.Template<T>(name)` | Stands in for a prefab. |
| `World.Spawn(template)` | Registers a template, and returns the clone. |

**Assets**: `Make` builds them in code, and they're destroyed with the world. Its tag names are unique in the world. The
game's own assets are the test's to load.

| `Make` member | What it builds |
|---|---|
| `Attribute` | An attribute. |
| `Element`, `Pool`, `Stat` | An attribute set's element. A `Pool` stays between 0 and its base. |
| `Set`, `UnusedSet` | An attribute set. `UnusedSet` is a set of one attribute nothing reads. |
| `Identity`, `RuntimeIdentity` | An entity's identity: an asset, or a runtime GAS's. |
| `Effect` | An effect, as an `EffectConfig` says: one impact, its duration and ticks, how another application stacks, its impact types, granted tags and workers. |
| `Item`, `Ability`, `Stage` | An item, an ability, and an ability's stage. |

## What must hold

After each test that passed, the world's **audit** checks what must always hold:

1. It lets three frames settle.
2. It ends what the test left running of what the world began.
3. It fails the test where any of these broke:
   - an entity's attribute parts don't sum to its value;
   - what an entity dealt, read from its frame summaries, isn't what its targets received from it;
   - a process is still running that the world didn't begin.

The audit checks the entities the world makes.

- `World.Observe(gas)` adds an entity made elsewhere, as from a scene.
- An entity made with `observed: false` is left out, as a test that measures allocation wants.

A class whose tests leave processes running on purpose overrides `Audits` to false.

## Allocation

`AllocationProbe` measures what's allocated, with the profiler's counter, as the GC's own reads 0 under Mono.

| Member | What it measures |
|---|---|
| `Measure(action)` | What one run allocates. |
| `MeasureEach(action, runs, warmup)` | The mean of many runs, after a warm-up. |
| `await MeasureFrames(Frames, count)` | What whole frames allocate: the cleanest of up to three tries, as stray noise doesn't repeat and a leak does. |

Dispose the probe as the test ends.

## Traps

These are the mistakes a test most easily makes with the kit.

- **Step frames; never call a process's update by hand.** The world's ProcessControl steps what runs, as the game's
  does. Calling an update yourself runs it out of order, and hides what the frame does.
- **A tag converts to text.** NUnit's `Does.Contain(tag)` compares a list of tags as text, and fails. Check with
  `World.HasTag`, or with `Contains` on the list itself.
- **Unscaled time isn't stepped.** Anything timed on Unity's unscaled time stays on the wall clock.
- **One world at a time.** `ForgeTestWorld.Current` is the one a test is in. PlayForge's static state is the world's
  while it lives, and the world gives back the clock and the rolls as it ends.
