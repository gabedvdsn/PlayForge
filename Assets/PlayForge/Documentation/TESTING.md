# PlayForge — Testing Your Content

A game's abilities, effects and items are easiest to trust when a test plays them as the game does: a burn's ticks
landing over its second, a ring's armor while it's held, a cast paying its cost. PlayForge's test kit is the assembly
`FarEmerald.PlayForge.Testing`, in the package's `TestKit` folder, which a project's own tests reference (TW-26). It
brings up a live PlayForge for each test, steps it a frame at a time, makes entities on either hub, and plays them; and
after each test it checks what must always hold, and takes it all down again, so tests run in any order.

## Setting up

The kit is compiled wherever the **Test Framework** package (`com.unity.test-framework`) is installed, as its own test
runner is. In a test assembly of the project's (an assembly definition whose **Define Constraints** hold
`UNITY_INCLUDE_TESTS`, referencing `nunit.framework.dll`), add to **Assembly Definition References**:

- `FarEmerald.PlayForge` and `FarEmerald.PlayForge.Testing`;
- `UniTask`;
- `UnityEngine.TestRunner` and `UnityEditor.TestRunner`.

The kit's tests are PlayMode tests, as they step the real player loop: give the assembly every platform, not the Editor
alone.

## A test

A test class derives from `ForgeTest`, which makes a `ForgeTestWorld` before each test and audits and ends it after.
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

- **Entities**: `World.Entity(hub, set, name)` makes an entity of an attribute set on either hub, `Hub.Runtime` (a
  `RuntimeGameplayAbilitySystem`) or `Hub.Mono` (a `GameplayAbilitySystem` on a GameObject of its own);
  `World.FromIdentity(hub, identity)` makes one from an identity asset, the game's own or one `Make` built, with its
  set, its starting abilities and items, and its sides. `RuntimeEntity`, `MonoEntity` and `NewMonoEntity` (made, not
  yet registered) make one hub's.
- **Playing**: `World.Hit(source, target, effect)` applies an effect, as a hit; `World.Cast(caster, ability, target,
  level)` gives an ability and asks it to start, as a player does, and `Cast(caster, slot, target)` starts the ability in
  a slot; `World.Use(caster, ability, target)` starts an ability the entity has already, as an item's; and
  `World.Hold(holder, item, stacks)` gives and equips an item. Each says whether it worked.
- **Reading**: `World.Value(gas, attribute)` and `World.Base(gas, attribute)`, NaN where the entity hasn't it;
  `World.Has(gas, effect)` and `World.Stacks(gas, effect)`; `World.HasTag(gas, tag)`, a tag or one under it.
- **Time**: `Frames.Step(n)` runs whole frames, each `Frames.DeltaTime` long (1/60 of a second, or a class's
  `DeltaTime`) with one FixedUpdate; `Frames.Seconds(s)` as many as that time; `Frames.Until(condition, within, what)`
  steps until it holds, failing past `within` frames; `ExactlyIn` fails unless it holds exactly so many frames on;
  `Holds` fails if it stops holding. `World.Clock(step)` makes each frame take `step` seconds of PlayForge's own time
  (`ForgeTime`) instead.
- **Chance**: `World.Rolls(0.1f, 0.9f)` makes each roll come out as written, in turn and round again, and returns them,
  counting what's drawn (`Drawn`). A chance lands where it's above the roll: a roll of 0 always lands.
- **Processes**: `World.Register(process)` registers one the world ends if the test doesn't; `World.Own(relay)` makes
  it end one registered some other way; `World.Template<T>(name)` stands in for a prefab, and `World.Spawn(template)`
  registers it, returning the clone.
- **Assets**: `Make` builds them in code, destroyed with the world: `Attribute`, `Element`, `Pool` (between 0 and its
  base) and `Stat`, `Set` and `UnusedSet`, `Identity` and `RuntimeIdentity`, `Effect` (as an `EffectConfig` says: one
  impact, its duration and ticks, how another application stacks, its impact types, granted tags and workers), `Item`,
  `Ability` and `Stage`. Its tag names are unique in the world. The game's own assets are the test's to load.

## What must hold

After each test that passed, the world's **audit** lets three frames settle, ends what the test left running of what it
began, and fails the test where any of these broke:

- an entity's attribute parts don't sum to its value;
- what an entity dealt, read from its frame summaries, isn't what its targets received from it;
- a process is still running that the world didn't begin.

The world checks the entities it makes; `World.Observe(gas)` adds one made elsewhere, as from a scene, and an entity made
with `observed: false` is left out, as a test that measures allocation wants. A class whose tests leave processes
running on purpose overrides `Audits` to false.

## Allocation

`AllocationProbe` measures what's allocated, with the profiler's counter, as the GC's own reads 0 under Mono:
`Measure(action)` what one run allocates, `MeasureEach(action, runs, warmup)` the mean of many after a warm-up, and
`await MeasureFrames(Frames, count)` what whole frames allocate, the cleanest of up to three tries, as stray noise
doesn't repeat and a leak does. Dispose it as the test ends.

## Traps

- **Step frames; never call a process's update by hand.** The world's ProcessControl steps what runs, as the game's
  does; calling an update yourself runs it out of order and hides what the frame does.
- **A tag converts to text.** NUnit's `Does.Contain(tag)` compares a list of tags as text and fails; check with
  `World.HasTag`, or `Contains` on the list itself.
- **Unscaled time isn't stepped.** Anything timed on Unity's unscaled time stays on the wall clock.
- **One world at a time.** `ForgeTestWorld.Current` is the one a test is in; PlayForge's static state is the world's
  while it lives, and the world gives back the clock and the rolls as it ends.
