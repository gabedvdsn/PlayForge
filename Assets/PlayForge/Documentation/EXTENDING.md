# PlayForge — Extending PlayForge

This guide covers each place a game's own code plugs into PlayForge.

PlayForge's rules are kinds a designer picks in an inspector: components, workers, tasks, scalers, conditions, stages,
cue handlers and more. A game adds its own by writing a class of the same base.

The editor finds the class, and its picker offers it beside PlayForge's own. It runs where they run, handed what they're
handed.

Each extension point here follows the same order, leaving out what doesn't apply to it:

1. What it's for, in a sentence.
2. The base to derive from, and what its hooks are handed.
3. Its hooks, and when PlayForge calls each.
4. How the kind reaches the inspector's picker.
5. A complete example.
6. Its traps.

Code that only uses PlayForge, as a script that casts an ability or reads an attribute, needs none of this. The kinds
here plug into what [WORKERS.md](WORKERS.md), [SCALERS.md](SCALERS.md), [PROCESSES.md](PROCESSES.md) and
[TASK_SEQUENCES.md](TASK_SEQUENCES.md) explain.

| I want to… | Read |
| --- | --- |
| Know what every kind of my own needs: its assembly, its constructor, its name, its fields | [Before you start](#before-you-start) |
| Give effects, abilities, entities or items a feature of my own | [Components](#components) |
| React to an attribute's change, a hit, a tag, a frame's events or an effect's life | [Workers](#workers) |
| Make a change wait for the entity's end of frame | [Deferred actions](#deferred-actions) |
| Give an ability a step of my own | [Ability tasks](#ability-tasks) |
| Choose an ability's target my own way | [Targeting tasks](#targeting-tasks) |
| Build sequences in code with tasks, injections and stage rules of my own | [Sequence tasks, injections and stage policies](#sequence-tasks-injections-and-stage-policies) |
| Run something over time under ProcessControl | [Processes](#processes) |
| Work out a number my own way, or test a condition | [Scalers, number kinds and conditions](#scalers-number-kinds-and-conditions) |
| Reshape a hit's value on its way in | [Value stages](#value-stages) |
| Play feedback: an animation, a sound, a shake | [Cue handlers](#cue-handlers) |
| Keep something for each entity as it runs | [Entity services](#entity-services) |
| Refuse an activation by a rule of my own | [Validation rules](#validation-rules) |
| Choose where an entity respawns, how charges come back, what a purge takes, a Local Data type | [Smaller kinds](#smaller-kinds) |
| Let my own character controller turn, move, shake the camera or pool a cue | [A game's own objects](#a-games-own-objects) |
| Show an entity's state in my UI | [Readers](#readers) |
| Drive PlayForge's time, random numbers or pointer input | [Time, randomness and input](#time-randomness-and-input) |
| Save data PlayForge's snapshot doesn't hold | [Saving a game's own data](#saving-a-games-own-data) |
| Name tags in code and offer them to the pickers | [Tags in code](#tags-in-code) |
| Check assets, draw a base type of my own, or start assets from templates | [The editor](#the-editor) |
| Test what I wrote | [Testing your extensions](#testing-your-extensions) |
| Drive the Creator Lab's units my own way | [The Creator Lab](#the-creator-lab) |

## Before you start

This section covers what every kind of a game's own shares: the assembly it lives in, how the editor finds it, how its
picker names and files it, how its fields show, where it keeps what it counts, and how it's renamed.

### The assembly

A kind of a game's own compiles in an assembly that references PlayForge's.

| Code | Where it lives | What it references |
| --- | --- | --- |
| Runtime code | Unity's default assembly, or a game's own assembly definition | `FarEmerald.PlayForge`, and `UniTask` for anything async: an ability task, a wind-up, a process or a sequence task |
| Editor code, as a validation rule or a drawer | An editor-only assembly | `FarEmerald.PlayForge.Editor` as well |

The runtime assembly, `FarEmerald.PlayForge`, is referenced automatically, so scripts in Unity's default assembly see
it. A game's own assembly definition lists it among its references.

Two of PlayForge's names clash with others:

- **`Attribute`.** PlayForge's attribute asset is `FarEmerald.PlayForge.Attribute`, and `System.Attribute` shares its
  name. A file with both `using System;` and `using FarEmerald.PlayForge;` that writes `Attribute` doesn't compile.
  - The examples here alias it: `using ForgeAttribute = FarEmerald.PlayForge.Attribute;`.
- **`Editor`.** The editor's namespace is `FarEmerald.PlayForge.Editor`. In code inside a namespace under
  `FarEmerald.PlayForge`, a bare `Editor` names that namespace, so write `UnityEditor.Editor` in full there.

### How a kind is found

The editor finds a kind of a game's own by its base, so nothing registers a kind: writing the class is enough.

A field a designer picks a kind for is a `[SerializeReference]` field of an abstract base or an interface. An
asset's **Components**, a stage's **Tasks**, a worker group's lists and a number field's **Kind** are such fields.

The field's picker lists every class that derives from the field's type, in any assembly the editor loads. It leaves out
a class that's abstract or an open generic.

Give each kind of your own these two things:

- **A public constructor that takes nothing.** The picker makes the kind with it as it's picked. The number, condition
  and Local Data menus list only classes that have one. Another picker lists a class without one, and fails as it's
  picked.
- **`[Serializable]`**, as PlayForge's own kinds are. The condition menu lists only serializable classes.

The constructor runs once for each pick, so it can set defaults of its own over its base's, as
`CantFallBelowAttributeWorker` sets its timing and its sign. Field initializers run with it.

### How a kind is named and filed

`[ForgeMenu]` on a kind's class says where its picker files it and what it's called.

| Argument | What it does |
| --- | --- |
| A path, as `[ForgeMenu("Effects")]` | Files the kind under a submenu. A slash nests them, as `"Effects/Areas"`. |
| `Name = "..."` | Replaces the class's name in the picker and in the field. |
| `Description = "..."` | What the picker says while the kind is highlighted. |

A path holds for the classes made from the one that names it. So a kind of `AbstractScaler`'s is filed under
its **Scaler** submenu, unless it names another. `[ForgeMenu("")]` puts a kind at the top, whatever its base names.

Without a `Name`, a picker shows the class's name spaced out, with its family's word taken off its end:

| Family | Words taken off | Example |
| --- | --- | --- |
| Components | `EffectComponent`, `AbilityComponent`, `EntityComponent`, `ItemComponent`, `Component` | `BountyMarkEffectComponent` is **Bounty Mark** |
| Ability tasks | `AbilityTask`, `Task` | `PulseTask` is **Pulse** |
| Targeting tasks | `TargetTask`, `Task` | `ClickNearestEnemyTargetTask` is **Click Nearest Enemy** |
| Workers, cue handlers, value stages, and the other kinds drawn as workers are | `Worker`, `Scaler`, `Handler`, `Behaviour`, `Policy`, `Rule`, `Config`, `Settings`, `Task`, `Cue`, and a few more | `GainOnHitWorker` is **Gain On Hit**, `AnimatorCue` is **Animator** |
| Scaler behaviours | `ScalerBehaviour`, `Behaviour` | `RoundToStepScalerBehaviour` is **Round To Step** |
| Variances | `Variance` | `TwoDiceVariance` is **Two Dice** |
| Respawn places | `RespawnPlace` | `NearHomeRespawnPlace` is **Near Home** |
| Injections | `Injection` | `SkipIfTaggedInjection` is **Skip If Tagged** |

A number kind takes the name its `[NumberKind]` gives, and a condition the one its `[GameplayCondition]` gives
([Scalers, number kinds and conditions](#scalers-number-kinds-and-conditions)).

### How its fields show

A kind's fields draw under its picker as Unity draws their types.

The picker draws a kind's public fields and its `[SerializeField]` ones, each with its `[Tooltip]`. `[HideInInspector]`
keeps one out.

Two attributes show a field only while another field of the same object says so:

| Attribute | Shows the field while | Read by |
| --- | --- | --- |
| `[ShownWhen(nameof(UsePhysics))]` | A bool is on | Tasks, components, workers, cue handlers and the other kinds a picker makes |
| `[ShownWhen(nameof(Mode), (int)EMode.Area)]` | An enum holds one of the values given | The same kinds |
| `[ForgeShowWhen(nameof(Mode), EMode.Curved)]` | The same as the two above | Scalers, number kinds and conditions |

A number field of a kind's own is a `ScalableFloat` or a `ScalableInt`. Limit it with `[NumberMin]`, `[NumberMax]` or
`[NumberRange]`: Unity's own `[Min]` and `[Range]` don't work on one
([SCALERS.md](SCALERS.md#a-number-field-in-your-own-code)).

A tag field takes `[ForgeTagContext(...)]` to say which tags its picker offers ([Tags in code](#tags-in-code)).

### One object for every holder

A kind is one object on its asset, shared by every entity the asset reaches.

One Burn effect's component serves every burning entity, and one ability's task serves every activation of it. So a kind
keeps nothing for one holder in its own fields.

Each family hands it a place to keep that:

| Family | Where it keeps what it counts for one holder |
| --- | --- |
| Components | `ctx.GetState<T>(this)`: one effect application's, one entity's ability's, one entity's, one entity's item's |
| Effect workers | `ctx.GetState<T>(this)` in its hooks, `spec.GetWorkerState<T>(this)` elsewhere: one application's |
| Ability tasks | `data.GetTaskState<T>(this)`: one activation's |
| Targeting tasks, sequence tasks | Locals of their run, or the run's data packet |
| Tag and analysis workers | The entity: an entity service, or its Local Data |
| Cue handlers | A dictionary keyed by `cue.Key ?? cue.Spec` |

A `[NonSerialized]` field that caches something of the kind's own, made from its settings, is fine, as Cast Range keeps
its Check Range.

### Renaming a kind

A kind is saved by its class's name, assembly and namespace, so renaming one needs care.

Renaming or moving the class needs Unity's `[MovedFrom]`, or every asset that holds one loses it. The Problems view
lists an asset that holds a class it can't find.

## Components

A component is a feature an asset carries in its **Components** section: an effect's, an ability's, an entity's or an
item's.

It hears its asset's moments through hooks, each a no-op it may override. It's the way to add a feature that needs more
than one moment, as a mark that pays its caster when the marked one dies.

Each family is a base in the runtime assembly, in `FarEmerald.PlayForge`:

| Base | Held by | Its context |
| --- | --- | --- |
| `AbstractEffectComponent` | an effect's **Components** | `EffectComponentContext` |
| `AbstractAbilityComponent` | an ability's **Components** | `AbilityComponentContext` |
| `AbstractEntityComponent` | an Entity asset's **Components**, or a runtime entity's | `EntityComponentContext` |
| `AbstractItemComponent` | an item's **Components** | `ItemComponentContext` |

Every component also has these members, from `AbstractForgeComponent`:

| Hook | When PlayForge calls it |
| --- | --- |
| `AllowsSeveral` | As the **Components** list offers kinds. False, for a kind that makes no sense twice, has the list offer it only while the asset holds none. |
| `Describe()` | As The Forge's summary and the breakdowns show the component: what it does in one line with its numbers, as "Range 6", beside its name. Null shows its name alone. |

Its protected `NameOf(asset)` gives an asset's name for that line: its PlayForge name, its file's, or "nothing".

Each kind lists its own traps, and Traps, last in this section, lists those every component shares.

### Effect components

An effect component adds a feature to an effect, heard at each moment of the effect's life.

Derive from `AbstractEffectComponent`. An effect holds it in its **Components**, and its hooks are called with an
`EffectComponentContext`:

| Member | Holds |
| --- | --- |
| `System` | The entity of the moment. |
| `Spec` | The effect's application. |
| `ExecuteTicks` | For a tick, how many ticks it executes. |
| `ImpactData` | For a hit, its impact. |
| `GetState<T>(this)` | What the component keeps for this application ([State a component keeps](#state-a-component-keeps)). |

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Lend(ctx)` | As a lasting effect starts to last on the one it's on: as it lands, before `Land`; as it takes another's place under its tag; and as a load or a move puts it back, where it doesn't land again. An instant effect lends nothing. |
| `TakeBack(ctx)` | As a lasting effect stops lasting there, matching each `Lend`: as it comes off, before `End`; as another takes its place; and as a load clears it or a move takes it away. |
| `Land(ctx)` | As the effect lands, with its workers' Application. |
| `Tick(ctx)` | As a lasting effect executes its period's tick. It isn't called on a tick that doesn't execute. |
| `Hit(ctx)` | As an impact the effect deals lands. `System` is then the dealer, the effect's caster. |
| `End(ctx)` | As the effect comes off: an instant one as it's done, a lasting one as it's removed. |
| `Death(ctx)` | As the one it's on dies while it's on them, before what the death ends comes off. |
| `Reapply(ctx)` | As another application lands on one it's on already and is taken in: a stack, a refresh, an extension or a replacement. `Spec` is the application that came. |
| `Refused(ctx, refusal)` | As an application is refused, as by an immunity or a missed chance. Nothing else of it is heard. |
| `StacksChanged(ctx, before, after)` | As its stacks change while it lasts. It isn't called as the effect lands, comes off or is loaded. |

`Lend` and `TakeBack` are for what an effect gives while it lasts and must give back exactly once, as an aura. A load
calls them where `Land` and `End` aren't called.

#### In the picker

An effect's **Components** list offers every class derived from `AbstractEffectComponent`, as
[How a kind is found](#how-a-kind-is-found) describes. The picker shows `BountyMarkEffectComponent` as **Bounty Mark**,
and `[ForgeMenu]` can name and file it otherwise.

#### Example

This component gives the effect's caster an effect as the one it's on dies while it lasts:

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Gives the effect's caster an effect as the one it's on dies while it lasts.")]
    public sealed class BountyMarkEffectComponent : AbstractEffectComponent
    {
        [Tooltip("The effect its caster takes as the marked one dies, from the marked one, so it reads the marked one's level")]
        public GameplayEffect Reward;

        public override bool AllowsSeveral => false;

        public override string Describe() => $"{NameOf(Reward)} to its caster as the marked one dies";

        public override void Death(EffectComponentContext ctx)
        {
            var marked = ctx.System;
            if (Reward == null || marked == null || ctx.Spec?.Source is not IGameplayAbilitySystem caster
                || ReferenceEquals(caster, marked) || caster.IsDead) return;
            var origin = IEffectOrigin.GenerateSourceDerivation(marked);
            caster.ApplyGameplayEffect(caster.GenerateEffectSpec(origin, Reward));
        }
    }
}
```

#### Traps

- **A load calls `Lend` and `TakeBack`** on a lasting effect's components. What a component applied as its effect landed
  comes back from the save on its own.

### Ability components

An ability component adds a feature to an ability: it can refuse an activation, make it wait as it winds up, and take
over how its cost and cooldown apply.

Derive from `AbstractAbilityComponent`. An ability holds it in its **Components**, and its hooks are called with an
`AbilityComponentContext`:

| Member | Holds |
| --- | --- |
| `Spec` | The ability as its entity holds it. |
| `Data` | The activation's data packet. It's null as the ability is granted or removed. |
| `Source` | The entity that holds it. |
| `GetState<T>(this)` | What the component keeps for this entity's ability. |

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Granted(ctx)`, `Removed(ctx)` | As an entity learns the ability, and loses it. |
| `CanStart(ctx)` | As an activation is asked for, after the ability's own Caster Checks. A reason refuses it, nothing of it paid; null lets it go on. |
| `CanTakeTarget(ctx)` | As its targeting ends, whichever task picked. A reason ends the activation there, before its wind-up, its cost and its stages. |
| `RefusedAs` | As a refusal from `CanStart` or `CanTakeTarget` is told: the refusal kind it's told as. It's `Rule` unless it says, as Cast Range says `Range`. |
| `WindUp(ctx, token)` | After its targeting and its Target Checks, before its cost and its first stage. The activation waits on it, with its entity's components' wind-ups and its other components', all together. |
| `Start(ctx)` | As an activation starts, its claim taken. |
| `ApplyUsage(ctx)` | In place of the ability applying its cost and its cooldown: true says it did, and false leaves it to the ability. Build one from `ctx.Spec.PayCosts(data)` and `ctx.Spec.SpendCooldown(data)`. |
| `Waives(ctx)` | The checks of its use it waives for this activation: `EUsageCheck.Cost`, `EUsageCheck.Cooldown` or both. A Toggle that's on waives them, so it can always turn off. |
| `Commit(ctx)` | As an activation's cost and cooldown apply. |
| `CategoryOf(ctx, effect)` | As a purge finds an effect its ability put on: the purge categories it's of, beside the ability's own. |
| `End(ctx, how)` | As an activation ends: `EAbilityEnd.Done`, `Cancelled` or `Failed`. |
| `Refused(ctx, refusal)` | As an activation is refused, nothing of it paid. |
| `DescribeUsage()` | What it adds to the ability's Activation line after its cost and cooldown, as "3 charges". |
| `TargetCheck`, `CasterCheck` | A check it stands behind, shown among the ability's Target Checks or Caster Checks while the ability holds it ([Validation rules](#validation-rules)). |

In `CanTakeTarget`, the pick is in `Data` under `Tags.TARGET`, not yet resolved to an entity, as a picked point has
none.

#### In the picker

An ability's **Components** list offers every class derived from `AbstractAbilityComponent`
([How a kind is found](#how-a-kind-is-found)). One whose `AllowsSeveral` is false, as the example's, is offered only
while the ability holds none.

#### Example

A component that enforces a rule says so in the ability's checks by returning an `AbstractComponentValidationRule<T>`
from `TargetCheck`. The check is the rule's place in the list, and it passes: the component does the refusing.

This example is a minimum range, the mirror of Cast Range:

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "How near the caster the ability's target may not be.")]
    public sealed class MinimumRangeAbilityComponent : AbstractAbilityComponent
    {
        public const string TooClose = "Its target is inside its Minimum Range";

        [Tooltip("How near its caster its target or point may not be, read at the ability's level")]
        [NumberMin(0f)] public ScalableFloat Least = 3f;

        // Its place among the ability's Target Checks, made as it's first asked for and kept with it
        [NonSerialized] private CheckMinimumRangeValidation check;

        public override bool AllowsSeveral => false;

        public override string Describe() => $"Minimum range {NumberText.Of(Least)}";

        public override IAbilityValidationRule TargetCheck => check ??= new CheckMinimumRangeValidation(this);

        // The pick its targeting made, whichever task made it, refused inside the range
        public override string CanTakeTarget(AbilityComponentContext ctx)
        {
            if (ctx.Data is not AbilityDataPacket data
                || !data.TryGetFirst(Tags.TARGET, out AbstractTargetingPacket picked) || picked == null
                || ctx.Source?.GetTargetingPacket() is not { } caster) return null;
            float least = ctx.Spec.Read(Least, data);
            var offset = picked.position - caster.position;
            offset.y = 0f;
            return offset.sqrMagnitude < least * least ? TooClose : null;
        }
    }

    [ValidationSide(EValidationSide.Target)]
    [ForgeMenu(Name = "Check Minimum Range", Description = "The Minimum Range component's check. It comes and goes with the component.")]
    public sealed class CheckMinimumRangeValidation : AbstractComponentValidationRule<MinimumRangeAbilityComponent>
    {
        public CheckMinimumRangeValidation(MinimumRangeAbilityComponent component) : base(component) { }

        public override string Describe() =>
            Component == null ? null : $"Its target at least {NumberText.Of(Component.Least)} from its caster";
    }
}
```

> [!TIP]
> A target or a point an AI hands in its request is refused soonest in `CanStart`, before targeting. Read
> `Tags.TARGET_REAL` and `Tags.POSITION` from the data, as Cast Range does: the request is then refused at once, with
> nothing of it run.

#### Traps

- **`CanStart` and `CanTakeTarget` are asked often**, an AI's every request among them. Keep them quick and free of
  allocation, and of side effects: a refusal must leave nothing behind.
- **A wind-up is cancelled through its token.** Await with it, and let `OperationCanceledException` pass: it ends the
  activation as cancelled, its cost unpaid. Any other exception fails the activation, which is written to the Console.
- **`Granted` and `Removed` have no data**: `ctx.Data` is null there.

### Entity components

An entity component adds a feature to an entity: it hears the entity register, die, revive and end, and can make its
abilities wait as they wind up.

Derive from `AbstractEntityComponent`. An Entity asset holds it in its **Components**, or a runtime entity does, and its
hooks are called with an `EntityComponentContext`:

| Member | Holds |
| --- | --- |
| `System` | The entity. |
| `Killer` | For its death, the source of the last loss it took from another. |
| `Identity` | Its settings as its GAS uses them. |
| `GetState<T>(this)` | What the component keeps for this entity. |

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Register(ctx)` | As the entity registers, its systems set up. |
| `Death(ctx)` | As it dies, whether it stays or ends. |
| `Revive(ctx)` | As a staying entity revives. |
| `End(ctx)` | As it ends. |
| `WindUp(ctx, ability, token)` | As an activation of one of its abilities winds up, with the ability's own components' wind-ups. `ability` is that activation's context. |

#### In the picker

An Entity asset's **Components** list offers every class derived from `AbstractEntityComponent`
([How a kind is found](#how-a-kind-is-found)).

#### Example

This component makes each of its entity's abilities wait a moment before its first stage, as a heavy unit braces:

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Each of its abilities waits a moment before its first stage, as a heavy unit braces.")]
    public sealed class BraceEntityComponent : AbstractEntityComponent
    {
        [Tooltip("How long it braces, in seconds of its own time, read at its level")]
        [NumberMin(0f)] public ScalableFloat Seconds = 0.25f;

        public override bool AllowsSeveral => false;

        public override string Describe() => $"Braces {NumberText.Of(Seconds)} s before each ability";

        public override async UniTask WindUp(EntityComponentContext ctx, AbilityComponentContext ability,
            CancellationToken token)
        {
            if (ctx.System == null) return;
            float wait = Seconds?.Evaluate(NumberContext.Of(ctx.System)) ?? 0f;
            var clock = ctx.System.GetClock();
            for (float waited = 0f; waited < wait; waited += clock.DeltaTime)
                await ForgeTick.Next(clock, token);
        }
    }
}
```

The wait counts the entity's own clock, so a slow on the entity slows its brace. `ForgeTick.Next(clock, token)` steps
with the game's fixed tick where one is on.

#### Traps

- **A wind-up is cancelled through its token**, as an ability component's is. Await with it, and let
  `OperationCanceledException` pass.

### Item components

An item component adds a feature to an item: it hears the item given, equipped, unequipped, removed and used.

Derive from `AbstractItemComponent`. An item holds it in its **Components**, and its hooks are called with an
`ItemComponentContext`:

| Member | Holds |
| --- | --- |
| `Container` | The item as its entity holds it. |
| `Source` | The entity. |
| `Restoring` | True where the moment comes from a load. |
| `GetState<T>(this)` | What the component keeps for this entity's item. |

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Given(ctx)` | As an entity is given the item in a slot of its own. |
| `Equipped(ctx)`, `Unequipped(ctx)` | As it's equipped and unequipped. Also as a load puts it back worn or takes it off, with `Restoring` true. |
| `Removed(ctx)` | As it's taken away. An equipped item is unequipped first. |
| `Used(ctx)` | As its holder's use of the item's ability ends, having paid what it costs. |

#### In the picker

An item's **Components** list offers every class derived from `AbstractItemComponent`
([How a kind is found](#how-a-kind-is-found)).

#### Example

A load puts back what a component's earlier moments did, the effects it applied and the loose tags it added among them.
So a component does nothing of that kind where `Restoring` is true, as this one shows:

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Its holder carries a tag while the item is equipped.")]
    public sealed class TagWhileEquippedItemComponent : AbstractItemComponent
    {
        [Tooltip("The tag its holder carries while it's equipped")]
        [ForgeTagContext(ForgeContext.Granted)]
        public Tag Tag;

        public override string Describe() => Tag.IsValid ? $"{Tag} while equipped" : null;

        public override void Equipped(ItemComponentContext ctx)
        {
            // A load puts back the loose tags its save held, this one among them
            if (ctx.Restoring || !Tag.IsValid || ctx.Source is not IGameplayAbilitySystem holder) return;
            holder.AddLooseTag(Tag);
        }

        public override void Unequipped(ItemComponentContext ctx)
        {
            if (ctx.Restoring || !Tag.IsValid || ctx.Source is not IGameplayAbilitySystem holder) return;
            holder.RemoveLooseTag(Tag);
        }
    }
}
```

#### Traps

- **A load calls `Equipped` and `Unequipped`** on an item's components, with `Restoring` true. What a component applied
  comes back from the save on its own.

### State a component keeps

A component keeps what it counts for one holder through its context, never in its own fields.

`ctx.GetState<T>(this)` makes a `T` the first time it's asked for, and keeps it for that holder:

| Component | Its holder |
| --- | --- |
| An effect's | One effect application |
| An ability's | One entity's ability |
| An entity's | One entity |
| An item's | One entity's item |

A state that needs more than a constructor with no parameters is made and kept through the holder's `ComponentStates`,
which `AbilitySpec`, `GameplayEffectSpec` and `ItemSpecContainer` each have:

1. `TryGet<T>(this, out state)` finds the state, where it's kept already.
2. Where it isn't, make it, then `Keep(this, state)`.

A save keeps no component state.

### Capabilities other code finds

A component can implement an interface that other code asks an asset for.

Other code finds a component through its asset's `Find<T>()`, by its class or by an interface it implements, as
`ability.Find<IAbilityReach>()` or `spec.Find<ICooldownModel>()`.

| Interface | What it answers | Who asks |
| --- | --- | --- |
| `IAbilityReach` | How far its ability reaches: `bool TryReadReach(AbilitySpec spec, ProcessDataPacket data, out float range, out bool flat)`. | Targeting tasks, the Creator Lab's bots and a game's AI, through `ForgeReach`. |
| `ICooldownModel` | How many uses its ability holds and how one is spent, in place of the single cooldown, as Charges does. | `AbilitySpec.CooldownModel` finds it. The Cooldown check and the ability's usage go through it. |

`IAuraComponent` is PlayForge's own components' mark, and nothing reads it. An aura of a component's own is given from
its `Lend` through `ForgeAuras.Provide`, and taken back from its `TakeBack` through `ForgeAuras.Remove`.

### Traps

These traps apply to every kind of component.

- **A component is shared.** A field it sets as one entity's moment comes is every entity's. Keep it in the context's
  state.
- **`Describe()` runs as The Forge draws**, often: no side effects.

## Workers

A worker is a small rule that reacts to what happens: an attribute's change, a hit it deals, its tags, its frame's
events, an effect's life.

An entity holds workers in a worker group, and an effect holds them in its **Workers**. This section covers writing each
kind. [WORKERS.md](WORKERS.md#the-map) maps when each runs and on whose system, and
[WORKERS.md](WORKERS.md#the-action-queue) covers the queue its actions wait in.

| Base | Picked in | Hooks |
| --- | --- | --- |
| `AbstractAttributeWorker`, `AbstractFocusedAttributeWorker` | a worker group's attribute workers | `Intercept`, `DeferredIntercept` |
| `AbstractImpactWorker`, `AbstractContextImpactWorker` | a worker group's impact workers | `ActivateInline`, `CreateDeferredActions` |
| `AbstractTagWorker` | a worker group's tag workers | `Activate`, `Tick`, `Resolve` |
| `AbstractAnalysisWorker` | a worker group's analysis workers | `Analyze` |
| `AbstractEffectWorker` | an effect's **Workers** | `OnEffectApplication`, `OnEffectTick`, `OnEffectRemoval`, `OnEffectImpact` |

A worker group (`StandardWorkerGroup`) sits on an attribute set, an Entity asset, a Forge Project's settings, an item,
an ability and an effect ([WORKERS.md](WORKERS.md#where-workers-come-from)). Code gives an entity one with
`group.ProvideWorkersTo(entity)`, and takes it back with `group.RemoveWorkersFrom(entity)`.

A deferred worker returns actions, `IEnumerable<IRootAction>`, which wait in its entity's action queue for its end of
frame ([Deferred actions](#deferred-actions)). Return `Array.Empty<IRootAction>()` for none.

Each kind lists its own traps, and Traps, last in this section, lists those every worker shares.

### Attribute workers

An attribute worker runs inside a change to one of its entity's attributes, where it can edit the change or queue
actions.

Derive from `AbstractFocusedAttributeWorker`. It watches one attribute, its **Target Attribute**, and registers itself.

It filters the changes it takes by its **Timing** (`TimingConfig`: Pre Change, Post Change or both),
its **Sign Policy**, **Allow Self Modification**, impact types and context tags.

`AbstractRelativeAttributeWorker` adds `GetRelative(ctx)`, the value of the attribute it's relative to.

Its hooks are handed a `WorkerContext`:

| Member | Holds |
| --- | --- |
| `System` | The entity whose attribute changes. |
| `AttributeCache` | Its attributes' values as they stand. |
| `Change` | The change. |
| `FrameSummary` | The frame's summary, which records its impacts. |
| `ActionQueue` | The entity's action queue. |

#### Hooks

A worker of `AbstractFocusedAttributeWorker` overrides `Execution` and its hook:

| Hook | When PlayForge calls it |
| --- | --- |
| `Execution` | Says which hook runs: `EWorkerExecution.Inline`, `EWorkerExecution.Deferred`, or `EWorkerExecution.InlineAndDeferred` for both. |
| `Intercept(WorkerContext ctx)` | Inside the change, for an inline worker. In Pre Change it edits `ctx.Change.Value`, and what it leaves is what commits. |
| `DeferredIntercept(WorkerContext ctx)` | During the change, for a deferred worker. It returns actions, which run at the end of frame. |

`AbstractAttributeWorker` itself, the base under it, leaves everything to the worker. A worker derived from it directly
also implements these:

| Hook | When PlayForge calls it |
| --- | --- |
| `Timing` | Says when in the change it runs. |
| `PreValidateWorkFor(ChangeValue)` | First, as a quick test of the change. |
| `ValidateWorkFor(WorkerContext)` | Then, with the whole context: whether it works on the change. |
| `RegisterWithHandler`, `DeRegisterFromHandler` | They add it to the pre- and post-change handlers with `handler.AddWorker(attribute, this)`, and take it off with `RemoveWorker`. |

#### In the picker

A worker group's attribute workers offer every class derived from `AbstractAttributeWorker`
([How a kind is found](#how-a-kind-is-found)). Its constructor can set defaults over its base's, as the example's sets
its timing and its sign.

#### Example

This worker keeps any one loss of its attribute to a share of the attribute's base, so no hit takes it all:

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Keeps any one loss of its attribute to a share of the attribute's base, so no hit takes it all.")]
    public class CapLossWorker : AbstractFocusedAttributeWorker
    {
        [Tooltip("The most one loss takes, as a share of the attribute's base: 0.25 for a quarter")]
        [NumberRange(0f, 1f)] public ScalableFloat MostShare = 0.25f;

        public CapLossWorker()
        {
            TimingConfig = EChangeEventTiming.PreChange;
            SignPolicy = ESignPolicyExtended.Negative;
        }

        public override EWorkerExecution Execution => EWorkerExecution.Inline;

        public override void Intercept(WorkerContext ctx)
        {
            if (TargetAttribute == null || !ctx.AttributeCache.TryGetValue(TargetAttribute, out var cached)) return;
            var change = ctx.Change.Value;
            float most = cached.ActiveValue.BaseValue * (MostShare?.Evaluate(NumberContext.Of(in ctx)) ?? 1f);
            if (most <= 0f || change.CurrentValue >= -most) return;
            // The same change, its carrier and its workable mark kept, taking no more than the most
            ctx.Change.Value = new SourcedModifiedAttributeValue(change, -most, change.BaseValue, change.ImpactIsWorkable);
        }
    }
}
```

#### Traps

- **Edit a change by building it again from itself**, as above. `ChangeValue.Add`, `Multiply` and `Override` mark the
  change workable whatever it was. So a system change they touch starts reaching impact workers and lifesteal
  ([WORKERS.md](WORKERS.md#workable-and-unworkable)).
- **An inline worker never changes an attribute itself**, which would run workers inside workers. It edits the change,
  or it's deferred.

### Impact workers

An impact worker runs as a change its holder dealt lands on another's attribute, for the attribute it subscribes to.

**It runs for the dealer**: its context's `System` is its holder, the one who dealt the change, and the one hit is
`ImpactData.Target`.

Derive from `AbstractContextImpactWorker`, which does the validation and the subscription from its fields, or from
`AbstractImpactWorker` for a worker that does them itself. Its hooks are handed an `ImpactWorkerContext`:

| Member | Holds |
| --- | --- |
| `System` | Its holder, the dealer. |
| `ImpactData` | The hit: `Target`, `Attribute`, `SourcedModifier`, `RealImpact`, `OldValue`. |
| `FrameSummary` | The frame's summary. |
| `ActionQueue` | The action queue. |

`AbstractContextImpactWorker`'s fields are **Impacted Attribute**, impact
types, **Impact Target**, **Impact Sign**, **Allow Self Impact**, context tags, and a **Work Attribute** for its work.

It starts narrow, **Any Context Tag** off and **Impact Sign** Positive, so it takes only gains carrying the context tags
it names. A worker of it that means losses from anything says so in its constructor, as the example does.

#### Hooks

`AbstractImpactWorker`'s members:

| Hook | When PlayForge calls it |
| --- | --- |
| `PreValidateWorkFor(ImpactData)` | First, as a quick test. |
| `ValidateWorkFor(ImpactData)` | Then, as the whole test. |
| `Execution` | `Deferred` unless it says. |
| `CreateDeferredActions(ctx)` | For a deferred worker: it returns its actions. |
| `ActivateInline(ImpactData)` | For an inline worker: it acts here. |
| `SubscribeToCache(ImpactWorkerCache)`, `UnsubscribeFromCache` | They give it to the cache under an attribute with `cache.ProvideWorker(attribute, this)`, and take it back with `RemoveWorker`. |
| `OncePerHit` | True runs it once for a hit, however many of it its holder has, its project's and its own. |
| **Accept Unworkable Impact** (`AcceptUnworkableImpact`) | Off, it hears only workable changes. |

#### In the picker

A worker group's impact workers offer every class derived from `AbstractImpactWorker`
([How a kind is found](#how-a-kind-is-found)). The picker shows `GainOnHitWorker` as **Gain On Hit**.

#### Example

This worker gives its holder some of its **Work Attribute** for each point of loss it deals another, as rage:

```csharp
using System;
using System.Collections.Generic;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Gives its holder some of its Work Attribute for each point of loss it deals another, as rage.")]
    public class GainOnHitWorker : AbstractContextImpactWorker
    {
        [Tooltip("What its holder gains for each point of loss it deals")]
        public ScalableFloat PerPoint = 0.5f;

        public GainOnHitWorker()
        {
            AnyContextTag = true;
            ImpactSign = ESignPolicy.Negative;
        }

        public override IEnumerable<IRootAction> CreateDeferredActions(ImpactWorkerContext ctx)
        {
            // Its holder dealt the hit: the context's System
            var holder = ctx.System;
            if (WorkAttribute == null || holder == null || holder.IsDead) return Array.Empty<IRootAction>();
            float gain = -ctx.ImpactData.RealImpact.CurrentValue * (PerPoint?.Evaluate(NumberContext.Of(in ctx)) ?? 0f);
            if (gain <= 0f) return Array.Empty<IRootAction>();
            return new IRootAction[]
            {
                new ModifyAttributeAction(holder, WorkAttribute,
                    SourcedModifiedAttributeValue.GenerateSimple(holder, WorkAttribute, gain, 0f)),
            };
        }
    }
}
```

`SourcedModifiedAttributeValue.GenerateSimple(source, attribute, current, base)` makes a change of the source's own, a
workable one.

#### Traps

- **An impact worker's System is the dealer** ([WORKERS.md](WORKERS.md#traps)).
- **A change made for a worker is never built over the hit's carrier**, `ctx.ImpactData.SourcedModifier`: that would
  count as the hit's effect again.

### Tag workers

A tag worker acts while its entity's tags meet its **Tag Requirements**.

Derive from `AbstractTagWorker`. Its hooks are handed the entity.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Activate(system)` | As its requirements come to be met. It runs as an action at the entity's end of frame, at `ActionPriority.TagWorker`. |
| `Resolve(system)` | As they no longer are. It runs as an action the same way. |
| `Tick(system)` | At each end of frame while they hold, after the activations, as its **Tick Pause** says. |
| `ValidateWorkFor(ITagHandler)` | Whether its requirements are met: by default, its packets' weights. |
| `EvaluateEveryFrame` | True has it asked in every end of frame, for a worker whose `ValidateWorkFor` reads more than tags. Otherwise it's asked only in a frame the entity's tags changed. |

**Tick Pause** (`TickPause`) is the frames between ticks, read at the entity's level. 0 ticks every frame, and -1, the
value it starts at, never ticks. It counts frames, not seconds.

#### In the picker

A worker group's tag workers offer every class derived from `AbstractTagWorker`
([How a kind is found](#how-a-kind-is-found)).

#### Example

This worker sends its entity an event as its tag requirements come to be met, and another as they stop:

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Sends its entity an event as its tag requirements come to be met, and another as they stop.")]
    public class TagEventsWorker : AbstractTagWorker
    {
        [Tooltip("The event its entity is sent as its requirements come to be met")]
        [ForgeTagContext(ForgeContext.Event)]
        public Tag OnMet;

        [Tooltip("The event its entity is sent as they stop being met")]
        [ForgeTagContext(ForgeContext.Event)]
        public Tag OnLost;

        public override void Activate(IGameplayAbilitySystem system)
        {
            if (OnMet.IsValid) system?.SendGameplayEvent(OnMet);
        }

        public override void Resolve(IGameplayAbilitySystem system)
        {
            if (OnLost.IsValid) system?.SendGameplayEvent(OnLost);
        }
    }
}
```

The events feed the entity's abilities' triggers and their Wait For Event tasks.

#### Traps

- **A tag worker is handed only its entity**, and it's one object for every entity that holds it. What it keeps for one
  entity goes in an entity service or the entity's Local Data.

### Analysis workers

An analysis worker reads its entity's frame as it ends, after the tag workers.

Derive from `AbstractAnalysisWorker`. Its hook is handed the entity and the frame's summary.

The summary holds the frame's impacts, dealt and received (`ImpactsDealt`, `ImpactsReceived`, and by attribute, target
and source). It also holds the effects applied and removed, the deaths, and the actions dropped and failed.

For a worker that fires once, the base keeps a mark in the entity's Local Data, under its own key, `MyKey`.
`HasTriggered(system)` reads the mark, `SetTriggered(system)` sets it, and `ResetTriggered(system)` clears it.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Analyze(IGameplayAbilitySystem system, FrameSummary frameSummary)` | At every end of frame of every entity that holds the worker, after the tag workers. It returns the actions it queues. |
| `Rearm(system)` | As the entity revives. It resets the mark. |

#### In the picker

A worker group's analysis workers offer every class derived from `AbstractAnalysisWorker`
([How a kind is found](#how-a-kind-is-found)).

#### Example

This worker applies an effect to its entity in a frame it deals enough hits to others:

```csharp
using System;
using System.Collections.Generic;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Applies an effect to its entity in a frame it deals enough hits to others.")]
    public class FlurryWorker : AbstractAnalysisWorker
    {
        [Tooltip("The losses it deals others in one frame that fire it")]
        [Min(1)] public int Hits = 3;

        [Tooltip("The effect its entity takes as it fires")]
        public GameplayEffect Effect;

        public override IEnumerable<IRootAction> Analyze(IGameplayAbilitySystem system, FrameSummary frameSummary)
        {
            if (Effect == null || system == null || frameSummary == null) return Array.Empty<IRootAction>();
            int dealt = 0;
            var impacts = frameSummary.ImpactsDealt;
            for (int i = 0; i < impacts.Count; i++)
            {
                var impact = impacts[i];
                // A workable loss to another: an entity's own changes are recorded as dealt to itself too
                if (impact.SourcedModifier.ImpactIsWorkable && impact.RealImpact.CurrentValue < 0f
                    && !ReferenceEquals(impact.Target, system)) dealt++;
            }
            if (dealt < Hits) return Array.Empty<IRootAction>();
            return new IRootAction[] { new ApplyEffectAction(system, Effect, IEffectOrigin.GenerateSourceDerivation(system)) };
        }
    }
}
```

#### Traps

- **`Analyze` runs at every end of frame** of every entity that holds the worker. A method that `yield return`s makes an
  object each call. So one that usually returns nothing returns `Array.Empty<IRootAction>()` instead, as above.

### Effect workers

An effect worker hears its effect's life: as it lands, as it lasts, as it comes off, and as a change it made lands.

Derive from `AbstractEffectWorker`. An effect holds it in its **Workers**. Each hook is handed an `EffectWorkerContext`
and returns actions:

| Member | Holds |
| --- | --- |
| `System` | The one it's on, or for `OnEffectImpact` the dealer. |
| `Carrier` | The effect's application, with `Spec` the same as a `GameplayEffectSpec`. |
| `ExecuteTicks` | For a tick, the ticks that frame executes. |
| `ImpactData` | For an impact, the change that landed. |
| `Impacts` | For an instant effect's removal, every impact it made, one for each attribute. Empty otherwise. |
| `FrameSummary` | The frame's summary. |
| `ActionQueue` | The action queue. |
| `GetState<T>(this)` | What the worker keeps for this application. |

Two bases under it add more:

- `AbstractAccumulatingEffectWorker` adds a running total of the effect's impacts, through `GetAccumulationData(ctx)`,
  which a worker of it implements to keep that total.
- `TargetAttributeThresholdEffectWorker` queues the actions its `OnThresholdMet(ctx)` returns as its target's attribute
  meets a threshold.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `OnEffectApplication(ctx)` | As the effect lands. `System` is the one it's on. |
| `OnEffectTick(ctx)` | Every frame a periodic effect lasts, with `ExecuteTicks`, the ticks that frame executes: most frames none. `System` is the one it's on. |
| `OnEffectRemoval(ctx)` | As it comes off. `System` is the one it's on. |
| `OnEffectImpact(ctx)` | As a change it made lands, with `ImpactData`. `System` is the dealer, its caster. |

#### In the picker

An effect's **Workers** list offers every class derived from `AbstractEffectWorker`
([How a kind is found](#how-a-kind-is-found)).

#### Example

This worker applies an effect from the caster as its effect comes off, once it has ticked enough:

```csharp
using System;
using System.Collections.Generic;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Applies an effect from the caster as its effect comes off, once it has ticked enough.")]
    public class PayoffWorker : AbstractEffectWorker
    {
        [Tooltip("The effect the one it's on takes as it comes off, from its caster")]
        public GameplayEffect Payoff;

        [Tooltip("The ticks its effect must have executed")]
        [Min(1)] public int Ticks = 3;

        // What it counts for one application of its effect
        private sealed class Count
        {
            public int Ticks;
        }

        public override IEnumerable<IRootAction> OnEffectTick(EffectWorkerContext ctx)
        {
            if (ctx.ExecuteTicks > 0 && ctx.GetState<Count>(this) is { } count) count.Ticks += ctx.ExecuteTicks;
            return Array.Empty<IRootAction>();
        }

        public override IEnumerable<IRootAction> OnEffectRemoval(EffectWorkerContext ctx)
        {
            var target = ctx.Carrier?.GetTarget();
            var caster = ctx.Carrier?.GetOrigin()?.GetSource();
            if (Payoff == null || target == null || caster == null || target.IsDead
                || ctx.GetState<Count>(this) is not { } count || count.Ticks < Ticks) return Array.Empty<IRootAction>();
            return new IRootAction[] { new ApplyEffectAction(target, Payoff, IEffectOrigin.GenerateSourceDerivation(caster)) };
        }
    }
}
```

#### Traps

- **`OnEffectTick` runs every frame** of a periodic effect, not on each tick. Act where `ExecuteTicks` is above 0.
- **`OnEffectImpact` runs on the dealer's side**, as an impact worker does.

### A worker that answers a number

A worker of any kind can answer number fields of the Worker kind, under a key.

Implement `INumberWorker` on the worker, beside its base.

| Hook | When PlayForge calls it |
| --- | --- |
| `bool TryGetNumber(Tag key, in NumberContext context, out float value)` | As a number field of the Worker kind asks it for its key ([SCALERS.md](SCALERS.md#asking-a-worker)). |

### Traps

These traps apply to every kind of worker.

- **A deferred worker's actions wait for the end of frame**, so what it reads as it builds them may have changed as they
  run. An action reads the state it acts on as it executes.
- **A worker is shared**: a tag or analysis worker keeps nothing for one entity in its fields.
- **Allocation.** Workers run in the frame's hot path, an attribute worker for every change. Hold back from new lists,
  closures and iterators in a hook that runs for every change, and return `Array.Empty<IRootAction>()` for nothing.

## Deferred actions

A deferred action is a piece of work queued to an entity that runs at its end of frame, by priority.

Deferred workers return them, and any code queues one with `entity.GetActionQueue().Enqueue(action)`. This section
covers writing one; [WORKERS.md](WORKERS.md#the-action-queue) covers when the queue runs and what it tells.

### Writing an action

An action of a game's own does a piece of work at an entity's end of frame, as sending it an event once the frame's
changes have run.

Implement `IRootAction`. For an action that holds only while its carrier's origin is active, derive from
`EffectScopedAction` instead:

- an ability's holds while a run of it is active;
- an item's holds while it's equipped;
- an entity's holds always.

`EffectScopedAction` takes the carrier in its constructor, keeps it as `carrier`, and leaves `Description` and `Execute`
to its kind.

#### Hooks

`IRootAction` has four members:

| Hook | When PlayForge calls it |
| --- | --- |
| `int Priority { get; }` | As the queue orders its actions. Higher runs first, and within one priority, in the order queued. |
| `bool IsValid { get; }` | As its turn comes. False drops it unrun. |
| `string Description { get; }` | Its line in the logs, the debugger and the effect journal. |
| `void Execute(ISource system)` | As its turn comes, where it's valid: its work. `system` is the entity whose queue runs it. |

`ActionPriority` names the levels: `Critical` (100), `High` (50), `Normal` (0), `Low` (-50), `TagWorker` (-100),
`Analysis` (-200), `Cleanup` (-300). `Enqueue(action, priorityOverride)` queues one at another priority.

#### In the picker

No picker offers an action. A deferred worker returns it, or code queues it with
`entity.GetActionQueue().Enqueue(action)`.

#### Example

This action sends an entity a gameplay event once the frame's changes have run:

```csharp
using FarEmerald.PlayForge;

namespace MyGame
{
    // Sends an entity a gameplay event once the frame's changes have run
    public sealed class SendEventAction : IRootAction
    {
        private readonly IGameplayAbilitySystem to;
        private readonly Tag eventTag;

        public SendEventAction(IGameplayAbilitySystem to, Tag eventTag, int priority = ActionPriority.Low)
        {
            this.to = to;
            this.eventTag = eventTag;
            Priority = priority;
        }

        public int Priority { get; }
        public bool IsValid => to != null && !to.IsDead && eventTag.IsValid;
        public string Description => $"Send {eventTag}";

        public void Execute(ISource system) => to.SendGameplayEvent(eventTag);
    }
}
```

#### Traps

- **The queue runs in the entity's end of frame**, which a Mono hub runs as it steps in LateUpdate. Its process timing
  takes LateUpdate, as Update And Late does, or its queue never runs.
- **An action queued to another entity runs at that entity's end of frame**: this frame if it's still to come, by the
  order the entities step in, or the next.
- **An action that throws is caught**: it's told as failed, written to the Console, and the rest of the queue runs.
- **An action the system makes for itself is used again** once the queue has run it. So a listener of the queue's events
  reads it during the call, and keeps nothing of it.
- **An action that queues another for good is cut off** at 10,000 a pass, with an error.

### PlayForge's own actions

PlayForge's own actions are each usable from a game's code.

| Action | Does |
| --- | --- |
| `ModifyAttributeAction(target, attribute, value, runEvents = true, priority = null)` | Changes an attribute by a `SourcedModifiedAttributeValue`. |
| `ApplyEffectAction(target, effect, origin, priority = null)` | Applies an effect from an origin. |
| `RemoveEffectAction(target, effect, priority = null)` | Removes an effect. |
| `LambdaAction(action, description, priority, isValid)` | Runs a function, while its validity check, if it's given one, holds. |
| `LogAction` | Writes a line through `ForgeLog`. |
| `DeathAction(system)` | Kills the entity, at `Critical`. |

## Ability tasks

An ability task is a step of an ability's stage: it's picked in a stage's **Tasks**, runs with the stage, and is handed
the activation's data.

This section covers writing one; [ABILITIES.md](ABILITIES.md#stages) covers stages.

### Stage tasks

A stage task is one step of an ability's stage, as applying an effect to the target several times.

Derive from `AbstractAbilityTask` (from `AbstractAbilityRelatedTask`). Its hooks are handed the activation's data, an
`AbilityDataPacket`, which holds the activation:

| Member | Holds |
| --- | --- |
| `data.EffectOrigin` | The ability's spec, whose `GetSource()` is the caster. |
| `data.AbilitySpec` | The same, as an `AbilitySpec`. |
| `data.TryGetFirstTarget(out ITarget target)`, `data.TryGetTarget(EDataTarget policy, out target)` | The entities its targeting picked, filed under `Tags.TARGET_REAL`. |
| `data.GetTargetingPacket(Tags.TARGET, out var packet, autoInterrupt: false)` | The pick's place, an entity's or a point's. With `autoInterrupt` on, its default, a missing pick interrupts the run. |
| `data.TryGet(Tags.POSITION, EDataTarget.Primary, out Vector3 point)`, `data.AddPayload(key, value)`, `data.TryGetFirst(key, out T value)` | Anything filed under a tag. |
| `data.GetTaskState<T>(this)` | What the task keeps for this activation. |

Its helpers are protected and static:

- `Read(number, data)`: a `ScalableFloat` or `ScalableInt` read at the ability's level.
- `EntityOf(data, NumberSide.Caster)` and `NumberSide.Target`: its caster, or the activation's first target.
- `ApplyTo(data, target, effect)`: applies an effect from the activation, with the magnitudes its caller set.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `IsCriticalSection` | Required. True holds the ability's claim while its stage runs. |
| `Prepare(data)` | Before its stage's tasks run. |
| `Activate(data, token)` | Required: its work, awaited. An interrupt cancels the token. |
| `Clean(data)` | After it ends, however it ended. |
| `Description` | Its line in the ability's breakdown. |

While a critical stage holds the claim, the entity's other activations wait or are refused, as their When Busy says. The
run takes only the injections its critical policy allows, an interrupt by default. A run with no critical stage lets its
claim go as it starts.

#### In the picker

A stage's **Tasks** offer every class derived from `AbstractAbilityTask` ([How a kind is found](#how-a-kind-is-found)).
The example's `[ForgeMenu("Effects")]` files it under the Effects submenu, where the picker shows `PulseTask`
as **Pulse**.

#### Example

This task applies its effect to the target several times, a pause apart:

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu("Effects", Description = "Applies its effect to the target several times, a pause apart.")]
    public class PulseTask : AbstractAbilityTask
    {
        [Tooltip("The effect each pulse applies to the activation's target")]
        public GameplayEffect Effect;

        [Tooltip("How many pulses, read at the ability's level")]
        [NumberMin(1f)] public ScalableInt Pulses = 3;

        [Tooltip("The pause between pulses, in seconds of the run's time")]
        [NumberMin(0f)] public ScalableFloat Interval = 0.5f;

        public override bool IsCriticalSection => false;

        public override string Description => "Pulses its effect on the target";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (Effect == null || !data.TryGetFirstTarget(out var target)) return;
            int pulses = Read(Pulses, data);
            float interval = Read(Interval, data);
            for (int i = 0; i < pulses; i++)
            {
                // The library's wait counts the run's clock and holds while the run is paused
                if (i > 0) await SequenceTaskLibrary.Delay(interval, token);
                if (!ApplyTo(data, target, Effect)) return;
            }
        }
    }
}
```

#### Traps

- **Wait through the task library or the run's clock.** `SequenceTaskLibrary.Delay(seconds, token)` and the library's
  other waits hold while the run is paused.
  - A loop of its own reads `TaskSequenceRuntime.ClockOfRun(token, data)` for the run's clock, and awaits
    `ForgeTick.Next(clock, token)` or `ForgeTick.NextFrame(token)`.
- **Await with the token.** An interrupt cancels it, and a task that ignores it runs on after its ability has ended.
- **One task serves every activation**: keep what one activation counts in `data.GetTaskState<T>(this)`.
- **A stage's policy is All or Any.** An ability compiles only those two. A stage policy of another type
  (`IAbilityProxyStagePolicy`) runs as All, with a warning.

### Asset loaders

An asset loader puts assets in the activation's data for its tasks to read.

Derive from `AbstractSetAssetsTask`, whose `Activate` does nothing. A loader overrides `IsCriticalSection`, and does its
work in `Prepare(data)`, with `data.AddPayload(tag, asset)`.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Prepare(data)` | Once an activation, after its targeting and before its first stage. The loader's work goes here. |
| `IsCriticalSection` | Required, as for every ability task. |

An ability's loaders run once an activation, after its targeting and before its first stage. Each is prepared, activated
and cleaned at once, so a loader's `Activate` isn't awaited.

#### In the picker

An ability's **Asset Loaders** offer every class derived from `AbstractSetAssetsTask`.

## Targeting tasks

A targeting task picks what an activation acts on before its stages run.

It's the ability's **Targeting**, one task, run as the activation starts. This section covers writing one, and what a
task must do while its ability waits on it. Traps, last in this section, lists a targeting task's traps.

### Writing a targeting task

A targeting task of a game's own picks an activation's target its own way, as a click near an enemy.

Derive from `AbstractTargetingAbilityTask` (from `AbstractAbilityRelatedTask`). It isn't marked `[Serializable]`, so
mark a task of your own.

`AbstractGasTargetingAbilityTask` adds **Requirements** (`AvoidRequireTagGroup`), which an entity's applied tags must
meet.

Its helpers:

- `TryGetDataTarget(data, out target)` and `TryGetDataPoint(data, out point)`: a target or a point the activation's data
  holds already, as an AI's request does.
- `WaitForPointerPress(button, token)`: waits for a press of the game's input, `ForgeInput.Current`. It gives the ray it
  aims along, or null with nothing to aim through.
- `PressWords(button)`: "Click", "Right-click" or "Middle-click", for a `Prompt`.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Activate(data, token)` | Required: it picks, and files the pick's targeting packet with `data.SetTargetingPacket(Tags.TARGET, packet)`, or several with `data.SetTargetingPackets(Tags.TARGET, targets)`, the first the primary. Or it calls `WhenTargetingInvalid(data)`. |
| `Prompt` | While the task waits for the player: the words a game shows, as "Click a point on the ground". Null for a task that doesn't wait for the player. |
| `TargetIsValid(...)` | Which targets it takes: an `ITarget`, a `GameObject`, a `Transform` or a point. |
| `WhenTargetingInvalid(data)` | Marks the targeting failed. With **Break Runtime On Invalid** on (`BreakRuntimeOnInvalid`), it throws `OperationCanceledException`, which ends the activation as cancelled. |
| `ConnectInputHandler(data)`, `DisconnectInputHandler(data)` | As it's prepared, to hook an input handler of its own, and as it's cleaned, to unhook it. A false from `ConnectInputHandler` fails the targeting. |
| `IsCriticalSection` | True: targeting holds the activation's claim. |

What follows a task's pick:

1. The ability's components hear it in `CanTakeTarget`, and may refuse it ([Components](#components)).
2. The pick resolves. The entity each packet under `Tags.TARGET`, or the ability's implicit tag, points at is filed
   under `Tags.TARGET_REAL`, where tasks and checks find it.
3. Where the pick is an entity, the caster's checks and the components' `CanStart` are asked again, then the Target
   Checks on the target.
4. Then come the wind-ups, the cost and the first stage.

#### In the picker

An ability's **Targeting** offers every class derived from `AbstractTargetingAbilityTask`
([How a kind is found](#how-a-kind-is-found)). The picker shows `ClickNearestEnemyTargetTask`
as **Click Nearest Enemy**.

#### Example

The player clicks the ground, and the enemy nearest the click is the target:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu("Picked", Description = "The player clicks the ground, and the enemy nearest the click is the target.")]
    public class ClickNearestEnemyTargetTask : AbstractTargetingAbilityTask
    {
        [Tooltip("How far from the click an enemy may stand, read at the ability's level")]
        [NumberMin(0f)] public ScalableFloat Radius = 2f;

        [Tooltip("The layers the click's ray hits")]
        public LayerMask Ground = ~0;

        [Tooltip("The pointer button that picks: 0 the primary, 1 the secondary, 2 the middle")]
        public int Button;

        public override string Prompt => $"{PressWords(Button)} near an enemy";

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            // An AI's activation hands its target: take it rather than wait for a click
            if (TryGetDataTarget(data, out var handed))
            {
                if (TargetIsValid(handed)) data.SetTargetingPacket(Tags.TARGET, handed.GetTargetingPacket());
                else WhenTargetingInvalid(data);
                return;
            }

            var ray = await WaitForPointerPress(Button, token);
            if (ray == null || !Physics.Raycast(ray.Value, out var hit, Mathf.Infinity, Ground))
            {
                WhenTargetingInvalid(data);
                return;
            }

            var caster = data.EffectOrigin.GetSource();
            var sides = data.EffectOrigin.GetAffiliation();
            var near = new List<ITarget>();
            ForgeEntities.Within(hit.point, Read(Radius, data), true, near, caster, false);

            ITarget best = null;
            float bestDistance = float.MaxValue;
            foreach (var each in near)
            {
                if (each.IsDead || !ForgeEntities.Takes(EAffiliationFilter.Enemies, caster, sides, each) || !TargetIsValid(each)) continue;
                float distance = ForgeEntities.Distance(each, hit.point, true);
                if (distance >= bestDistance) continue;
                best = each;
                bestDistance = distance;
            }

            if (best == null) WhenTargetingInvalid(data);
            else data.SetTargetingPacket(Tags.TARGET, best.GetTargetingPacket());
        }
    }
}
```

### While it waits

A task that waits for the player holds its ability in targeting, and the game shows it.

- **The slot says it's choosing.** `AbilitySpecContainer.IsTargeting` is true while a run of the ability waits on its
  targeting. A game shows the ability's targeting's `Prompt` beside its name while it is.
- A task that doesn't wait for the player returns a null `Prompt`.
- **Another ability can take its place.** Another of the entity's abilities may activate on a request that cancels
  targeting, as a player's press. An ability still choosing then gives way where its **While Targeting** says it yields.
  - **Yields** gives way.
  - **Entity's Rule** follows the entity's **Targeting Yields**.
  - **Holds** keeps it.
- A trigger's request and an activation at start don't cancel targeting: `AbilityActivationRequest.CancelsTargeting` is
  off for them. Code that makes a request chooses.
- **Giving way cancels the task's token.** Its claim lets go at once, so the other activation starts while the cancelled
  task winds down. Nothing of the activation is paid, it ends as cancelled, and the task's `Clean` runs.

So a task awaits with its token, as `WaitForPointerPress` does. Once its token is cancelled, it files nothing and
applies nothing: a pick filed after the token was cancelled lands in an activation that's gone.

### Traps

These traps apply to every targeting task.

- **An override of `Prepare` calls the base's last**, which connects the input handler.
- **No task state.** `data.GetTaskState<T>` takes an ability task. A targeting task keeps what it needs in its
  `Activate`'s locals.
- **Take a handed target first.** An AI's or a replay's activation hands its target in its data. A task that waits for a
  click whatever the data holds can't be cast by an AI.
- **An exception other than a cancellation fails the activation**, written to the Console.
- **The pick is a packet.** `SetTargetingPacket` files a place: an entity's packet points at its entity, and a
  `StaticTargetingPacket(point)` at none. An activation whose pick is a point has no target, so its Target Checks don't
  run.

## Sequence tasks, injections and stage policies

A task sequence runs stages of tasks, built in code with `TaskSequenceBuilder` ([TASK_SEQUENCES.md](TASK_SEQUENCES.md)).

This section covers what a game writes for one: a task, an injection that changes a run as it goes, a stage policy that
says when a stage ends, and a method the inspector's sequence pickers list.

### Tasks

A sequence task is one step of a sequence's stage, run in an async sequence or a synced one.

Implement `ISequenceTask`, or derive from one of two bases that fill in what a task doesn't need:

| Base | What it fills in |
| --- | --- |
| `SequenceTaskBase` | `Execute` is required, and its `Step` ends at once, so it does nothing in a synced sequence. |
| `SyncSequenceTaskBase` | `Step` is required. Its `Execute` steps it once a frame with the run's clock, holding while the run is paused, so one task serves both kinds. |

#### Hooks

`ISequenceTask` has four members:

| Hook | When PlayForge calls it |
| --- | --- |
| `Prepare(SequenceDataPacket data)` | Before its first step or its run. |
| `UniTask Execute(SequenceDataPacket data, CancellationToken token)` | Its run, in an async sequence. |
| `bool Step(SequenceDataPacket data, float deltaTime)` | Each frame in a synced sequence, with the run's time. True ends it. |
| `Clean(SequenceDataPacket data)` | After it ends, however it ended. |

#### In the picker

No picker offers a sequence task: code builds it into a stage with `TaskSequenceBuilder`, as the example below does.

#### Example

This task fades a canvas group to an alpha over a time, in an async sequence or a synced one:

```csharp
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    // Fades a canvas group to an alpha over a time, in an async sequence or a synced one
    public sealed class FadeTask : SyncSequenceTaskBase
    {
        private readonly CanvasGroup group;
        private readonly float to;
        private readonly float seconds;
        private float from;
        private float elapsed;

        public FadeTask(CanvasGroup group, float to, float seconds)
        {
            this.group = group;
            this.to = to;
            this.seconds = seconds;
        }

        public override void Prepare(SequenceDataPacket data)
        {
            from = group != null ? group.alpha : to;
            elapsed = 0f;
        }

        public override bool Step(SequenceDataPacket data, float deltaTime)
        {
            if (group == null) return true;
            elapsed += deltaTime;
            float t = seconds > 0f ? Mathf.Clamp01(elapsed / seconds) : 1f;
            group.alpha = Mathf.Lerp(from, to, t);
            return t >= 1f;
        }
    }

    public static class MenuSequences
    {
        public static TaskSequence FadeOut(CanvasGroup group) =>
            TaskSequenceBuilder.Create("Fade Out")
                .Stage(stage => stage.Task(new FadeTask(group, 0f, 0.4f)))
                .BuildSequence();
    }
}
```

`MenuSequences.FadeOut(group).RegisterAndRun(SequenceDataPacket.Default())` runs it under ProcessControl.

#### Traps

- **A task that keeps its state in its own fields serves one run at a time**, as this one does. A sequence that runs
  several at once builds a task for each, or keeps a run's state in its data packet.

### A sequence the inspector lists

A method marked `[TaskSequenceMethod]` offers its sequence to an ability's Run Sequence task and a Sequence cue.

Those two pick a sequence from a dropdown of the project's methods marked `[TaskSequenceMethod]`. Such a method is:

- static;
- taking no parameters;
- returning a `TaskSequence`, a `TaskSequenceChain` or a `TaskSequenceDefinition`.

#### In the picker

The dropdown names each by its `[TaskSequenceMethod("...")]` name, or its method's.

#### Example

```csharp
using System.Threading;
using FarEmerald.PlayForge;

namespace MyGame
{
    public static class GameSequences
    {
        [TaskSequenceMethod("Short Pause")]
        public static TaskSequence ShortPause() =>
            TaskSequenceBuilder.Create("Short Pause")
                .Stage(stage => stage.Task((CancellationToken token) => SequenceTaskLibrary.Delay(0.25f, token)))
                .BuildSequence();
    }
}
```

### Injections

An injection changes a run as it goes, as a skip, a jump or an interrupt.

Implement `ISequenceInjection`. Its hook is handed the run, a `TaskSequenceRuntime`, which gives it `Data`,
`StageIndex`, `CurrentStage`, `IsRunning`, `IsPaused`, `Pause()`, `Resume()` and `Inject`.

The skips, jumps and interrupts themselves are the built-in injections': `SkipStageInjection`, `JumpToStageInjection`,
`InterruptSequenceInjection` and the rest. An injection of a game's own composes them.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `bool Apply(TaskSequenceRuntime runtime)` | As it's injected into a run. It acts on the run, and says whether it took effect. |

#### In the picker

An injection reaches a run four ways:

- `runtime.Inject(injection)`, into one run;
- `sequence.Inject(injection)`, into every run of a sequence;
- the builder's `InjectWhen`;
- an ability's Inject task, whose picker lists a `[Serializable]` injection with a constructor that takes nothing. It
  shows `SkipIfTaggedInjection` as **Skip If Tagged**.

#### Example

This injection skips the current stage where the caster carries a tag:

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Name = "Skip If Tagged", Description = "Skips the current stage where the caster carries a tag.")]
    public class SkipIfTaggedInjection : ISequenceInjection
    {
        [Tooltip("The tag on the caster that skips the stage")]
        [ForgeTagContext(ForgeContext.Granted)]
        public Tag Tag;

        public bool Apply(TaskSequenceRuntime runtime)
        {
            if (!Tag.IsValid || runtime?.Data is not AbilityDataPacket data) return false;
            var caster = data.EffectOrigin?.GetSource();
            if (caster == null || caster.GetTagWeight(Tag) <= 0) return false;
            // This injection passed the run's checks as it came in; the skip it composes is part of it
            return runtime.Inject(SkipStageInjection.Instance, overridePermission: true);
        }
    }
}
```

#### Traps

- **Permissions name injections by their exact type.** A sequence or a stage that lists its allowed injections,
  `AllowInjections(typeof(SkipIfTaggedInjection))`, takes only those types, not types made from them.
- **A critical section takes only an interrupt** unless its critical policy lists more.
- **An injection is one object every run shares**: what it keeps for one run goes in the run's data.

### Stage policies

A stage policy says when a stage ends.

Implement `ISequenceStagePolicy`. A policy of a game's own is handed a token source of its own, linked to the stage's,
so cancelling it cuts off the stage's tasks and not the stage.

#### Hooks

`ISequenceStagePolicy` has one member:

| Hook | When PlayForge calls it |
| --- | --- |
| `UniTask AwaitCompletion(TaskSequenceRuntime runtime, SequenceStage stage, UniTask[] taskUnits, CancellationTokenSource stageCts)` | While the stage's tasks run. It awaits them as it sees fit, and the stage ends as it returns. |

#### In the picker

No picker offers a stage policy: `stage.WithPolicy(policy)` gives a stage one in code.

`TaskSequenceBuilder.Create("Search").Stage(stage => stage.Task(a).Task(b).WithPolicy(FirstSuccessStagePolicy.Instance))`
gives a stage the example's.

#### Example

This policy ends the stage as its first task finishes well. A task that throws ends it only where every task fails.

```csharp
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge;

namespace MyGame
{
    // The stage ends as its first task finishes well; a task that throws ends it only where every task fails
    public sealed class FirstSuccessStagePolicy : ISequenceStagePolicy
    {
        public static readonly FirstSuccessStagePolicy Instance = new();

        public async UniTask AwaitCompletion(TaskSequenceRuntime runtime, SequenceStage stage, UniTask[] taskUnits,
            CancellationTokenSource stageCts)
        {
            if (taskUnits.Length == 0) return;
            int left = taskUnits.Length;
            bool succeeded = false;
            Exception firstError = null;
            var done = new UniTaskCompletionSource();

            foreach (var task in taskUnits) Watch(task).Forget();
            await done.Task;

            // The others are cut off as the stage ends
            stageCts.Cancel();
            if (!succeeded && firstError != null) throw firstError;

            async UniTaskVoid Watch(UniTask task)
            {
                try
                {
                    await task;
                    succeeded = true;
                    done.TrySetResult();
                    return;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception error)
                {
                    firstError ??= error;
                }
                if (--left == 0) done.TrySetResult();
            }
        }
    }
}
```

The Task Sequence Examples sample has another, a lead task the stage lasts as long as.

#### Traps

- **A synced runner can't run a policy of a game's own.** It warns, and its stage waits for all its tasks.
- **An ability's stages take All or Any alone** ([Ability tasks](#ability-tasks)).
- **A policy that returns ends the stage.** One that waits on fewer than all its tasks cancels its source, so the rest
  stop with it.

## Processes

A process is anything that runs over time under ProcessControl: a MonoBehaviour or a plain C# object stepped from
Unity's player loop.

This section covers writing one; [PROCESSES.md](PROCESSES.md) covers lifecycles, registering, pausing and groups.

### Writing a process

A process of a game's own does work over time, as a regeneration that runs until its entity dies.

Derive from one of four bases:

| Base | Is |
| --- | --- |
| `AbstractMonoProcess` | A MonoBehaviour process, whose lifecycle and timing are fields of its inspector. |
| `LazyMonoProcess` | A MonoBehaviour process whose run lasts until it's paused or ended. |
| `AbstractRuntimeProcess` | A plain C# process, whose name, timing, priority and lifecycle are given to its base's constructor. |
| `LazyRuntimeProcess` | A plain C# process whose run lasts until it's paused or ended. |

A process reads its packet as `regData`, or `Data`, and its handle as `Relay`.

#### Hooks

Each hook is virtual.

| Hook | When PlayForge calls it |
| --- | --- |
| `RunProcess(token)` | The run, which ProcessControl awaits for a process that isn't Synchronous. Required under `AbstractMonoProcess` and `AbstractRuntimeProcess`. |
| `WhenInitialize()` | As it's registered. |
| `WhenUpdate()`, `WhenLateUpdate()`, `WhenFixedUpdate()` | At its timing. |
| `WhenWait()` | As it goes to Waiting. |
| `WhenTerminate()` | As it ends. |
| `WhenTerminateQueued()` | As a Terminate is asked of it while it runs. |
| `WhenDestroy()` | In place of `OnDestroy`, as a mono process's object is destroyed. |
| `TryHandlePause()`, `TryHandleResume()` | As it's paused and resumed. True holds a run through a pause ([PROCESSES.md](PROCESSES.md#pausing)). |

#### In the picker

No picker offers a process: code registers it with ProcessControl.
`ProcessControl.Register(new RegenProcess(hero, health, 5f)).HandledBy(hero).Run(out ProcessRelay relay)` starts the
example, and `relay.Terminate()` ends it.

#### Example

This process gives an entity an amount of an attribute each second of its own time, until it dies or the process ends:

```csharp
using FarEmerald.PlayForge;
using ForgeAttribute = FarEmerald.PlayForge.Attribute;

namespace MyGame
{
    // Gives an entity an amount of an attribute each second of its own time, until it dies or the process ends
    public sealed class RegenProcess : LazyRuntimeProcess
    {
        private readonly IGameplayAbilitySystem entity;
        private readonly ForgeAttribute attribute;
        private readonly float perSecond;
        private float waited;

        public RegenProcess(IGameplayAbilitySystem entity, ForgeAttribute attribute, float perSecond)
            : base("Regen", EProcessStepPriorityMethod.First, 0, EProcessStepTiming.Update, EProcessLifecycle.Synchronous)
        {
            this.entity = entity;
            this.attribute = attribute;
            this.perSecond = perSecond;
        }

        public override void WhenUpdate()
        {
            if (entity == null || entity.IsDead || attribute == null)
            {
                Relay.Terminate();
                return;
            }
            waited += entity.GetClock().DeltaTime;
            if (waited < 1f) return;
            waited -= 1f;
            entity.TryModifyAttribute(attribute, SourcedModifiedAttributeValue.GenerateSimple(entity, attribute, perSecond, 0f));
        }
    }
}
```

It changes the attribute once a second, not each frame. A change each frame would make a new change, and run the
attribute's workers, every frame.

#### Traps

- **Call the base** in an override of `WhenInitialize` or `WhenTerminate`.
- **`WhenDestroy` stands in for `OnDestroy`**, which a MonoBehaviour process of its own would hide.
- **A Synchronous process isn't awaited**: its run is its steps.
- **A mono process's object** comes from an instantiator, an `AbstractMonoProcessInstantiator`, as a pool hands one out
  ([PROCESSES.md](PROCESSES.md#registering-one)).

## Scalers, number kinds and conditions

A number field's **Kind** says how its number is worked out, and a Conditional's or a rule's condition says whether it
holds.

This section adds to [SCALERS.md](SCALERS.md#writing-your-own-scaler), which covers the kinds' names, their inspectors
and a number field in a class of a game's own. Here are their members, the variances and behaviours, and an example of
each. Traps, last in this section, lists the traps they share.

### Number kinds

A number kind is the simplest way to a number of a game's own: it reads its site through a `NumberContext`.

Derive from `NumberKind`. A kind is a scaler, so it works in an effect's magnitude and inside a Group or a Conditional
too. Its scaler `Evaluate` methods are sealed, and it reads none of a scaler's levels unless its `Parts` say.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Evaluate(in NumberContext context)`, protected | As the field is read: its number. |
| `Describe()` | Its line in the field's summary, as "the distance to the target". |
| `Reads` | What it reads of the site, from its `[NumberKind]`: `NumberReads.Level`, `Caster`, `Target`, `Caller`, `LocalData`, `Carrier`, `Data`, `Owner`. A field offers it only where its site provides them. |
| `CanEvaluate(in context)` | Whether the site has what it reads. Where it hasn't, the field reads `Fallback`, 0 unless it says, and logs why. |
| `WhyNot(in context)` | Why it can't be worked out, for that line. It's `protected internal`, so an override in another assembly is `protected override`. |
| `Explain(in context, NumberTrace trace)` | What its explanation lists, as the numbers it read. |

#### In the picker

A number field's **Kind** menu lists a number kind with a public constructor that takes nothing.
`[NumberKind("Name", Reads = NumberReads.Caster | NumberReads.Target)]` names it and says what it reads.

#### Example

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [NumberKind("Distance to Target", Reads = NumberReads.Caster | NumberReads.Target)]
    [ForgeMenu("Space", Description = "How far the target stands from the caster.")]
    public class DistanceToTargetKind : NumberKind
    {
        [Tooltip("Measures along the ground, leaving height out")]
        public bool Flat = true;

        protected override float Evaluate(in NumberContext context)
        {
            var from = context.Caster?.GetTargetingPacket();
            var to = context.Target?.GetTargetingPacket();
            if (from == null || to == null) return 0f;
            var offset = to.position - from.position;
            if (Flat) offset.y = 0f;
            return offset.magnitude;
        }

        public override string Describe() => Flat ? "the distance to the target, along the ground" : "the distance to the target";
    }
}
```

A number read in code is `number.Evaluate(context)`, with a `NumberContext` made `Of` what the code has: an effect's
spec, an activation's data, a worker's context, an entity, a cue or a process.
`NumberContext.For(caster, target, level)` makes one with nothing else, for a test.

### Conditions

A condition says whether something holds, as a Conditional's or a rule's does.

Derive from `GameplayCondition`, which has the same shape as a number kind. `condition.Holds(context)` reads it, and one
that can't be worked out where it's read doesn't hold.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Evaluate(in NumberContext context)`, protected | As it's read: whether it holds. |
| `Describe()`, `Reads`, `CanEvaluate`, `WhyNot`, `Explain` | As a number kind's. |
| `Initialize(carrier)` | As an effect's spec is made. It passes the spec on to the numbers it holds. |

#### In the picker

A condition is offered wherever one is picked: a Conditional, an effect's requirements, an On Hit, a modifier's rule, an
ability's Condition check.

`[GameplayCondition("Name", Reads = ...)]` names a condition. The condition menu lists only `[Serializable]` classes
with a constructor that takes nothing.

#### Example

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [GameplayCondition("Behind Target", Reads = NumberReads.Caster | NumberReads.Target)]
    [ForgeMenu("Space", Description = "Holds while the caster stands behind the target.")]
    public class BehindTargetCondition : GameplayCondition
    {
        [Tooltip("How wide the arc behind the target is, in degrees")]
        [NumberRange(0f, 360f)] public ScalableFloat Arc = 120f;

        protected override bool Evaluate(in NumberContext context)
        {
            var caster = context.Caster?.GetTargetingPacket();
            var target = context.Target?.GetTargetingPacket();
            if (caster == null || target == null) return false;
            var toCaster = caster.position - target.position;
            toCaster.y = 0f;
            var facing = target.rotation * Vector3.forward;
            facing.y = 0f;
            if (toCaster.sqrMagnitude < 0.0001f || facing.sqrMagnitude < 0.0001f) return false;
            float arc = Arc?.Evaluate(in context, this) ?? 120f;
            return Vector3.Angle(-facing, toCaster) <= arc * 0.5f;
        }

        public override void Initialize(IAttributeImpactCarrier carrier) => Arc?.Initialize(carrier);

        public override string Describe() => $"the caster within {NumberText.Of(Arc)}° behind the target";
    }
}
```

A kind or a condition reads its own numbers with itself as their owner, `Arc.Evaluate(in context, this)`, so a number of
the Relative to a Field kind can read its other fields.

### Scalers

A scaler of the full kind holds values for each level and behaviours, for a number that reads levels in a way of its
own.

Derive from `AbstractScaler`. Its protected helpers read its levels:

- `EvaluateFromSpec(carrier)`: its values at the carrier's level, exactly or spread, through its behaviours.
- `ApplyBehaviourEvaluation(carrier, value)`: a value through its behaviours. Its behaviours apply only where its
  `Evaluate` calls one of these two.
- `EvaluateAtLevel(level, relativeLevel)`, `GetEffectiveMaxLevel(carrier)`, `GetEffectiveRelativeLevel(carrier)` and
  `EvaluateScalingAtRelativeLevel(relativeLevel)`, for one that reads its levels itself.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Initialize(IAttributeImpactCarrier carrier)` | As an effect's spec is made. It readies the scaler for the spec. |
| `Evaluate(IAttributeImpactCarrier carrier)` | As its number is read: its value. |
| `Parts` | Which of every scaler's parts its inspector draws ([SCALERS.md](SCALERS.md#writing-your-own-scaler)). |
| `IsEmpty` | True reads it as no scaler at all. |

#### In the picker

A kind of `AbstractScaler`'s is filed under its **Scaler** submenu, unless it names another
([How a kind is named and filed](#how-a-kind-is-named-and-filed)).

#### Example

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Its values per level, eased toward a cap so each level adds less.")]
    public class SoftCapScaler : AbstractScaler
    {
        [Tooltip("The value it nears and never reaches; 0 or less, no cap")]
        public float Cap = 100f;

        public override void Initialize(IAttributeImpactCarrier carrier) { }

        public override float Evaluate(IAttributeImpactCarrier carrier)
        {
            float raw = EvaluateFromSpec(carrier);
            return Cap <= 0f ? raw : Cap * raw / (Cap + Mathf.Abs(raw));
        }
    }
}
```

### Cached scalers

A cached scaler works out an attribute set element's **Scaling** from the entity's cached attribute values.

Derive from `AbstractCachedScaler`. PlayForge's own, as `CachedAttributeScaler`, show its shape.

| Hook | When PlayForge calls it |
| --- | --- |
| `EvaluateActiveValue(blueprint, cache)` | Required. It gives the value. |

### Variances

A variance varies a Randomized scaler's value, handed a roll from 0 to 1.

Derive from `AbstractVariance`. Its protected `Read(number, carrier, fallback)` reads one of its numbers for the
carrier, with itself as their owner.

The roll comes from `ForgeRandom`, once for each spec where the scaler's **Roll Once Per Spec** is on.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Vary(value, roll, carrier)` | The value varied by the roll. |
| `Lowest(value, carrier)`, `Highest(value, carrier)` | The range a tooltip shows. |
| `Initialize(carrier)` | It readies its numbers for a spec. |
| `Numbers()` | Its numbers with their roles, for an explanation. |

#### In the picker

A Randomized scaler's **Variance** offers every class derived from `AbstractVariance`. The picker shows
`TwoDiceVariance` as **Two Dice**.

#### Example

```csharp
using System;
using System.Collections.Generic;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Name = "Two Dice", Description = "Up or down by a share of the value, the middle likeliest, as two dice fall.")]
    public class TwoDiceVariance : AbstractVariance
    {
        [Tooltip("The most it varies by either way, as a share of the value: 0.2 for 20%")]
        [NumberMin(0f)] public ScalableFloat Share = 0.2f;

        public override float Vary(float value, float roll, IAttributeImpactCarrier carrier)
        {
            // The roll through a triangle's inverse, from -1 to 1, the middle likeliest
            float centred = roll < 0.5f ? Mathf.Sqrt(2f * roll) - 1f : 1f - Mathf.Sqrt(2f * (1f - roll));
            return value * (1f + centred * Read(Share, carrier, 0f));
        }

        public override float Lowest(float value, IAttributeImpactCarrier carrier) => value * (1f - Read(Share, carrier, 0f));
        public override float Highest(float value, IAttributeImpactCarrier carrier) => value * (1f + Read(Share, carrier, 0f));
        public override void Initialize(IAttributeImpactCarrier carrier) => Share?.Initialize(carrier);
        public override IEnumerable<(string Role, ScalableFloat Number)> Numbers() { yield return ("Share", Share); }
    }
}
```

### Scaler behaviours

A scaler behaviour adjusts the value a scaler works out, as one of the scaler's **Behaviours**, in order.

Derive from `AbstractScalerBehaviour`. Its overloads that take a stacking container are never called.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Initialize(IAttributeImpactCarrier spec)` | Required. At each read, handed the carrier the value is read for. |
| `Evaluate(float magnitude, IAttributeImpactCarrier spec)` | Required. Next, handed the value so far: the value after it. |

#### In the picker

A scaler's **Behaviours** offer every class derived from `AbstractScalerBehaviour`. The picker shows
`RoundToStepScalerBehaviour` as **Round To Step**.

#### Example

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Rounds the number to a step, as 5 for multiples of 5.")]
    public class RoundToStepScalerBehaviour : AbstractScalerBehaviour
    {
        [Tooltip("The step it rounds to; 0 leaves the number as it is")]
        [Min(0f)] public float Step = 1f;

        public override void Initialize(IAttributeImpactCarrier spec) { }

        public override float Evaluate(float magnitude, IAttributeImpactCarrier spec) =>
            Step > 0f ? Mathf.Round(magnitude / Step) * Step : magnitude;
    }
}
```

### Traps

These traps apply to every kind in this section.

- **A number is read often**, as an effect's magnitude for every application and a tooltip each time it draws: no
  allocation and no side effects in `Evaluate`.
- **A kind is saved by its class's name** ([Before you start](#before-you-start)).
- **Draw rolls from `ForgeRandom`**, never `UnityEngine.Random`, so a test or a replay that puts in a seeded source
  repeats them ([Time, randomness and input](#time-randomness-and-input)).

## Value stages

A value stage reshapes a hit's value on its way to its attribute, as one of a Value Pipeline's **Stages**, which an
effect's impact names as its **Pipeline** ([VALUE_PIPELINES.md](VALUE_PIPELINES.md#stages)).

This section covers writing one.

### Writing a value stage

A value stage of a game's own reshapes each hit through its pipeline, as armor shrinking a loss.

Derive from `AbstractValueStage`. **Label** and **Enabled** are every stage's: off, the value passes it by.

Its hook is handed a `ValueRun`:

| Member | Holds |
| --- | --- |
| `Carrier` | The hit's effect spec. |
| `Context` | The hit's `NumberContext`: its caster, its target and its level. |
| `Breakdown` | What the run keeps of each stage, or null. |
| `Note(text)` | Notes what the stage did on its step in the breakdown, as "critical". |

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `float Apply(float value, in ValueRun run)` | For every hit through its pipeline: the value it passes on. A loss is negative. |
| `Describe()` | What it does, as "× 1.5", which the pipeline's line and a breakdown show. Its **Label** replaces it. |

#### In the picker

A Value Pipeline's **Stages** offer every class derived from `AbstractValueStage`
([How a kind is found](#how-a-kind-is-found)).

#### Example

This stage shrinks a loss by the armor of the one hit: as much armor as its Halves halves the loss.

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;
using ForgeAttribute = FarEmerald.PlayForge.Attribute;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Shrinks a loss by the armor of the one hit: as much armor as its Halves halves it.")]
    public class ArmorStage : AbstractValueStage
    {
        [Tooltip("The attribute that is the armor of the one hit")]
        public ForgeAttribute Armor;

        [Tooltip("The armor that halves a loss, read for the hit")]
        [NumberMin(1f)] public ScalableFloat Halves = 100f;

        public override float Apply(float value, in ValueRun run)
        {
            if (value >= 0f || Armor == null || !run.Context.TryGetAttribute(NumberSide.Target, Armor, out var armor)) return value;
            float halves = Mathf.Max(1f, Halves?.Evaluate(in run.Context, this) ?? 100f);
            float factor = halves / (halves + Mathf.Max(0f, armor.CurrentValue));
            if (factor < 1f) run.Note($"{armor.CurrentValue:0} armor");
            return value * factor;
        }

        public override string Describe() => $"armor, {NumberText.Of(Halves)} halving a loss";
    }
}
```

#### Traps

- **`Apply` runs for every hit** through the pipeline: no allocation. `run.Note` with a constant string is free. One
  built with `$"..."`, as above, allocates only where it notes.
- **A stage is shared** by every hit through its pipeline: it keeps nothing of one hit.

### Stop stages

A stop stage stops an effect outright, as a block or a dodge.

Derive from `AbstractStopStage`, whose **Chance**, **Roll** and **Stops** every stop has.

#### Hooks

A stop names three members:

| Hook | When PlayForge calls it |
| --- | --- |
| `StoppedEvent` | As it stops an effect: the event the one hit hears. |
| `DealerEvent` | The event its dealer hears. |
| `Verb` | Its verb, as "blocked". |

The effect system honours every enabled stop in a pipeline, wherever it sits in the list. As the effect lands, the first
stop that reaches it and rolls stops it.

#### In the picker

A Value Pipeline's **Stages** offer it, as they offer every stage.

## Cue handlers

A cue handler plays feedback, a shake, a sound, an animation, at an effect's, an ability's, an entity's or an item's
moments, or where a cue tag routes.

This section covers writing one; [CUES.md](CUES.md#handlers) covers where cues are raised and PlayForge's handlers.
Traps, last in this section, lists the traps of handlers and sinks.

### Writing a cue handler

A cue handler of a game's own plays feedback its own way, as setting a trigger on the Animator of the one it plays on.

Derive from `AbstractCueHandler`. Its hooks are handed a `CueContext`:

| Member | Holds |
| --- | --- |
| `Target` | The entity it plays on. |
| `Moment` | The moment that raised it. |
| `Tag` | The cue's tag, where one raised it. |
| `Source` | Who's behind it: the effect's or the ability's caster, or the entity itself. |
| `Origin` | What raised it: an effect's origin, an ability's spec, an item's, or the entity. |
| `Spec`, `Ticks` | For an effect: its spec, and for a tick's burst, how many of its ticks executed. |
| `Data` | An activation's data, for an ability's cue. |
| `Magnitude`, `Point`, `Normal` | Where its raiser gave a hit: a number to scale with, as the hit's damage, and the hit's point and normal. |
| `Key` | What pairs a lasting cue's beginning with its end. |
| `Lasting` | Whether it was raised to last. |
| `Transform`, `Position` | The target's object, and where it plays. |

`NumberContext.Of(in cue)` reads a handler's numbers for it.

#### Hooks

Each hook is a no-op it may override.

| Hook | When PlayForge calls it |
| --- | --- |
| `Burst(cue)` | At a moment that plays once. By default it calls `OnTicked` for a tick, `OnRemoved` for a removal and `OnApplied` for any other moment. |
| `OnApplied(cue)`, `OnTicked(cue)`, `OnRemoved(cue)` | From `Burst`. |
| `Begin(cue)`, `End(cue)` | As a lasting cue begins and ends, as a While Active one does. |

#### In the picker

Handlers sit in the Cues components' lists, the Play Cue task's, and a cue library's routes. The picker shows
`AnimatorCue` as **Animator**.

Code plays one with `ForgeCues.Burst`, `ForgeCues.Begin` and `ForgeCues.End`, handed a handler or a list and a
`CueContext`.

#### Example

This handler sets an Animator trigger on the one the cue plays on, and a bool while a lasting cue lasts:

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "Sets an Animator trigger on the one the cue plays on, and a bool while a lasting cue lasts.")]
    public class AnimatorCue : AbstractCueHandler
    {
        [Tooltip("The trigger set as it bursts")]
        public string Trigger = "Hit";

        [Tooltip("The bool on while a lasting cue lasts")]
        public string WhileActive = "Stunned";

        public override void OnApplied(CueContext cue)
        {
            if (!string.IsNullOrEmpty(Trigger) && AnimatorOf(cue) is { } animator) animator.SetTrigger(Trigger);
        }

        public override void Begin(CueContext cue)
        {
            if (!string.IsNullOrEmpty(WhileActive) && AnimatorOf(cue) is { } animator) animator.SetBool(WhileActive, true);
        }

        public override void End(CueContext cue)
        {
            if (!string.IsNullOrEmpty(WhileActive) && AnimatorOf(cue) is { } animator) animator.SetBool(WhileActive, false);
        }

        private static Animator AnimatorOf(in CueContext cue) =>
            cue.Transform != null ? cue.Transform.GetComponentInChildren<Animator>() : null;
    }
}
```

> [!NOTE]
> A runtime entity has no object, so `Transform` is null on one.

### Sinks

A sink decides whether a raise plays at all, as a server's that plays none, or a client's that plays only its own
player's.

Derive from `ForgeCueSink`. `raise.Cue` is the cue, and `raise.Source` what raised it.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `WillPlay(ECueEvent cueEvent, in CueRaise raise)` | Once for each raise of a list of cues, before any of them plays: whether it plays. |

#### In the picker

No picker offers a sink. It's given every world as `ForgeCues.Sink`, or one world with `ForgeCues.UseSink(world, sink)`.
`ForgeCues.Enabled = false` plays none.

### Traps

These traps apply to every cue handler and sink.

- **A handler is shared**: one keeps what a lasting cue placed under `cue.Key ?? cue.Spec`, which its `End` hands back,
  as PlayForge's Prefab cue does.
  - Two lasting cues on one entity each begin and end. So a bool the first `End` turns off is off for the second too, as
    in the example: count them where that matters.
- **`End` can come with no `Begin`.** A muted entity's cues, and every cue while `ForgeCues.Enabled` is off, don't
  begin, and still end, so what began before the mute is taken down. `End` does nothing where it finds nothing.
- **A sink is asked for `End` too**: it answers an `End` as it answered its `Begin`.
- **A cue is presentation alone.** A server may play none, so game state never depends on a cue. Rolls come from
  `ForgeCues.Random`, the cues' own random numbers, so a cue never moves the game's.
- **A handler that throws is caught** and written to the Console, and the cues after it play.

## Entity services

An entity service is an object of a game's own each entity keeps as it runs, as a combo count, a cooldown of the game's
own or a respawn's wait.

It's made the first time it's asked for, and stepped by the entity's own time. This section covers writing one.

### Writing a service

A service of a game's own keeps something for each entity, stepped with the entity.

Derive from `AbstractEntityService`. `System` is the entity it's for.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Step(float deltaTime)` | Each frame of the entity's step, with its own clock's time, after its effects. |
| `Revive()` | As a staying entity revives. |
| `End()` | As the entity ends, after which it keeps none. |

#### In the picker

No picker offers a service. `entity.GetService<T>()` gives an entity's `T`, making it the first time.
`entity.FindService<T>()` gives it only where it's made, or null.

#### Example

A combo count each entity keeps, back to 0 after a pause between hits and as it revives:

```csharp
using FarEmerald.PlayForge;

namespace MyGame
{
    // A combo count each entity keeps, back to 0 after a pause between hits and as it revives
    public sealed class ComboService : AbstractEntityService
    {
        public float Window = 2f;
        public int Count { get; private set; }
        private float sinceLast;

        public void Hit()
        {
            Count++;
            sinceLast = 0f;
        }

        public override void Step(float deltaTime)
        {
            if (Count == 0) return;
            sinceLast += deltaTime;
            if (sinceLast >= Window) Count = 0;
        }

        public override void Revive() => Count = 0;
    }
}
```

A worker or a component counts a hit with `ctx.System.GetService<ComboService>()?.Hit()`. A number kind reads it with
`context.SystemOf(NumberSide.Caster)?.FindService<ComboService>()?.Count ?? 0`.

#### Traps

- **A service has no inspector**: it's made with its constructor that takes nothing, and its settings come from code.
- **A save keeps no service.** A loaded entity makes its services again as it's asked for them.
- **Step runs every frame** for every entity that has the service. Keep it quick, as above, which returns at once while
  there's nothing to count down.

See [SERVICES.md](SERVICES.md) for PlayForge's other services.

## Validation rules

A validation rule refuses an ability's activation, as one of the ability's **Caster Checks**, asked of the caster, or
its **Target Checks**, asked of the target once its targeting has picked one.

This section covers writing one; [ABILITIES.md](ABILITIES.md#requirements) covers the checks.

### Writing a rule

A rule of a game's own refuses an activation by a test of its own, as whether the caster faces its target.

Implement `IAbilityValidationRule`. `AbstractAttributeValidationRule` and `AbstractTagValidationRule` are bases for a
rule about an attribute or a tag.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)` | As its list of checks is asked: whether it passes. `error` says why it refused, in words a player could read. |
| `string GetName()` | Its name, which a refusal names. |
| `RefusedAs` | The refusal kind it's told as among the Caster Checks: `Rule` unless it says, as the Cost check says `Cost`. A Target Check's refusal is always told as `Target`. |

`getSource()` gives the entity a rule checks: the caster among the Caster Checks, the target among the Target Checks.

#### In the picker

Both lists of checks offer a rule. `[ValidationSide(EValidationSide.Caster)]` or `EValidationSide.Target` offers it in
one list alone; without it, both lists offer it.

#### Example

This rule activates only where the caster faces the target within an angle:

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ValidationSide(EValidationSide.Target)]
    [ForgeMenu(Name = "Facing Target", Description = "Activates only where the caster faces the target within an angle.")]
    public class FacingTargetValidation : IAbilityValidationRule
    {
        [Tooltip("How far either side of straight ahead the target may be, in degrees")]
        [Range(0f, 180f)] public float HalfAngle = 45f;

        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            var caster = data?.EffectOrigin?.GetSource()?.GetTargetingPacket();
            var target = getSource?.Invoke()?.GetTargetingPacket();
            if (caster == null || target == null)
            {
                error = "There's no one to face";
                return false;
            }
            var toTarget = target.position - caster.position;
            toTarget.y = 0f;
            var forward = caster.rotation * Vector3.forward;
            forward.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f || Vector3.Angle(forward, toTarget) <= HalfAngle)
            {
                error = null;
                return true;
            }
            error = $"Its target is more than {HalfAngle:0}° from where its caster faces";
            return false;
        }

        public string GetName() => "Facing Target";
    }
}
```

#### Traps

- **A Caster Check is asked of every request**, an AI's among them, before anything is paid. Keep it quick, free of
  allocation where it passes, and free of side effects.
- **A condition of a game's own** does as much through PlayForge's **Condition** check, and serves effects' requirements
  and Conditionals as well ([Scalers, number kinds and conditions](#scalers-number-kinds-and-conditions)).
- **A rule a component enforces** is an `AbstractComponentValidationRule<T>`, which the component returns as its
  `TargetCheck` or `CasterCheck`. No picker offers one ([Components](#components)).

## Smaller kinds

This section covers the smaller kinds a game picks in one field each: where an entity respawns, how charges come back,
what a purge takes, how a modifier changes an effect, a Local Data value's type, and the clock a run takes.

### Respawn places

A respawn place says where an entity revives.

Derive from `AbstractRespawnPlace`.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Vector3? Where(EntityComponentContext context, Vector3? home)` | As the entity revives, handed where it stood as it registered: where it revives. Null revives it where it died. |
| `Describe()` | The component's line, as "at home". |

#### In the picker

A Respawn component's **At** offers every class derived from `AbstractRespawnPlace`. The picker shows
`NearHomeRespawnPlace` as **Near Home**.

#### Example

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    [ForgeMenu(Description = "A random point within a radius of where it registered.")]
    public class NearHomeRespawnPlace : AbstractRespawnPlace
    {
        [Tooltip("How far from home it may come back")]
        [Min(0f)] public float Radius = 3f;

        public override Vector3? Where(EntityComponentContext context, Vector3? home)
        {
            if (home == null) return null;
            var offset = ForgeRandom.InsideUnitCircle * Radius;
            return home.Value + new Vector3(offset.x, 0f, offset.y);
        }

        public override string Describe() => $"within {Radius:0.#} of home";
    }
}
```

### Charge recharges

A charge recharge says how an ability's spent charges come back.

Derive from `AbstractChargeRecharge`. Each hook has a default, so a recharge overrides only what it changes.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `InHand(AbilityCharges charges)` | The charges in hand. By default, its most less those cooling down. |
| `Spend(AbilityCharges charges, ProcessDataPacket data)` | As a charge is spent. By default, it starts one's cooldown. |
| `CountsASingleCharge` | Whether a single charge is still counted, rather than the ability being an ordinary cooldown. True unless it says; PlayForge's own recharges say false. |

#### In the picker

A Charges component's **Recharge** offers every class derived from `AbstractChargeRecharge`.

### Purge filters

A purge filter says which effects an effect's purge takes.

Derive from `AbstractPurgeFilter`. An effect's purge takes the effects its **Filters** let it
([EFFECTS.md](EFFECTS.md#purging)).

#### Hooks

Its one hook is `bool Takes(in PurgeContext
ctx)`, with `ctx.Container`, the effect it might take; `ctx.Purged`, the entity it's on; and `ctx.Purger`, who
purges, where an effect or code names one. It says whether the purge takes the effect.

#### In the picker

An effect's purge **Filters** offer every class derived from `AbstractPurgeFilter`.

### Effect changes

An effect change is one of the changes a modifier's rule makes to an effect it reaches.

Derive from `AbstractEffectChange`.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Apply(EffectChanges changes, GameplayEffectSpec spec, EffectModifierRule rule)` | As the rule reaches an effect. It writes what it changes into `changes`. |
| `Describe()` | Its line. |

#### In the picker

A modifier's rule offers it in its **Changes** ([EFFECTS.md](EFFECTS.md#modifiers)).

### Local Data values

A Local Data value is the type of a Local Data entry's value.

Derive from `LocalDataValue`, or for one that holds a single value, from `LocalDataValue<T>`, whose `Value` it holds.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Read(in NumberContext)` | As a packet takes it: its value. |
| `TryReadNumber(in NumberContext, out float)` | As a number reads it, where it's one. |

#### In the picker

The Local Data list offers a kind with a constructor that takes nothing, named without "Local".

#### Example

```csharp
using System;
using FarEmerald.PlayForge;

namespace MyGame
{
    public enum EElement { None, Fire, Frost, Storm }

    [Serializable]
    [ForgeMenu(Name = "Element", Description = "One of the game's elements.")]
    public class LocalElement : LocalDataValue<EElement>
    {
    }
}
```

`asset.TryGetLocalData(key, out DataWrapper entry) && entry.TryGet(out EElement element)` reads it.

#### Traps

- **An entry is the asset's own.** A value changed at run time changes the asset, so Local Data is read, not written, as
  the game runs.

### Clock choices

A clock choice says which clock a run takes.

Derive from `AbstractClockChoice`.

#### Hooks

Its one hook is `IForgeClock ClockFor(ProcessDataPacket
data)`, the clock a run on the packet takes, or null for the run's own. `OwnersClock(data)`, protected, gives the clock
of the entity the packet is for ([SERVICES.md](SERVICES.md)).

#### In the picker

A Run Sequence task's or a Sequence cue's **Clock** offers every class derived from `AbstractClockChoice`.

### Stack behaviours

An effect's **Stack Behaviours** are `AbstractStackingConfig`s.

A game can derive one, but its members work on the effect system's stacking containers, which this guide doesn't cover.
[EFFECTS.md](EFFECTS.md#stacking-stack-existing-only) covers the built-in ones.

## A game's own objects

PlayForge asks a game's own objects for a few things through interfaces: turning, moving, shaking the camera, and a
pooled cue's comings and goings.

This section covers each.

### Turners

A turner turns an entity's object as PlayForge orders, in place of PlayForge setting the object's rotation.

Implement **`IForgeTurner`** on an entity's object.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Face(Vector3 direction, float degreesPerSecond, float deltaTime)` | At each step of a turn PlayForge orders, as an ability's turn to face its target. |

#### Example

This turner turns its object toward the direction PlayForge asks, at the speed it asks:

```csharp
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    // On an entity's object: turns it toward the direction PlayForge asks, at the speed it asks
    public sealed class SmoothTurner : MonoBehaviour, IForgeTurner
    {
        public void Face(Vector3 direction, float degreesPerSecond, float deltaTime)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            var goal = Quaternion.LookRotation(direction);
            transform.rotation = degreesPerSecond <= 0f
                ? goal
                : Quaternion.RotateTowards(transform.rotation, goal, degreesPerSecond * deltaTime);
        }
    }
}
```

An animated character's turner sets its animator's turn here instead, and lets the animation turn the object.

#### Traps

- **PlayForge reads how far is left from the object's rotation**, so that's what the turner turns.

### Movers

A mover is a caster's own movement, which PlayForge's movement tasks hold and stop while they move the caster.

Implement **`IForgeMover`** on a caster's object.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Hold(bool held)`, `Stop()` | While a movement task, as Dash, moves the caster: it calls them on the caster's own movement. |

### Camera shakers

A camera shaker shakes the game's camera for PlayForge's Camera Shake cue.

Implement **`ICameraShaker`**, and set it as `ForgeCues.Shaker`.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Shake(float strength, float duration)` | As the Camera Shake cue plays, which calls it through `ForgeCues.Shaker`. |

### Pooled cues

A pooled cue hears its pool hand it out and take it back ([CUES.md](CUES.md#pooling)).

Implement **`IPooledCue`** on a pooled Prefab cue's prefab.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Taken()` | As its pool hands it out, placed and active. |
| `Returned()` | As it goes back. |

## Readers

A reader is a MonoBehaviour process that shows an entity's state, as a bar or a counter.

This section covers writing one.

### Writing a reader

A reader of a game's own shows a piece of an entity's state in the game's UI.

Derive from `AbstractReader`, a `LazyMonoProcess`. `Assign(gas)` gives it its entity: it keeps the entity as `Source`,
and calls `SubscribeIfOnChangePolicy()`. PlayForge's `AttributeReader` and `AbilityReader` are readers.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `SubscribeIfOnChangePolicy()` | As `Assign` gives it its entity. A reader implements it to hear the changes it shows through the entity's callbacks, or leaves it empty to read the entity in `WhenUpdate`. |
| `WhenUpdate` | At its timing, as for any process. |
| `WhenTerminate`, `WhenDestroy` | As it ends, and as its object is destroyed: where a reader that subscribed unsubscribes. |

#### In the picker

No picker offers a reader: it's a MonoBehaviour process on an object, which code gives its entity with `Assign(gas)`.

#### Example

This reader tells its listeners a tag's weight on its entity as it changes, as a stack counter shows it:

```csharp
using FarEmerald.PlayForge;
using UnityEngine;
using UnityEngine.Events;

namespace MyGame
{
    // Tells its listeners a tag's weight on its entity as it changes, as a stack counter shows it
    public class TagWeightReader : AbstractReader
    {
        [ForgeTagContext(ForgeContext.Granted)]
        public Tag Tag;

        public UnityEvent<int> Changed = new();

        private int shown = -1;

        public override void WhenUpdate()
        {
            if (Source == null || !Tag.IsValid) return;
            // An absent tag's weight is -1
            int weight = Mathf.Max(0, Source.GetTagWeight(Tag));
            if (weight == shown) return;
            shown = weight;
            Changed.Invoke(weight);
        }

        protected override void SubscribeIfOnChangePolicy() { }
    }
}
```

#### Traps

- **A reader that subscribes unsubscribes**, in `WhenTerminate` or `WhenDestroy`, and `Assign` called again subscribes
  again. A lambda subscribed can't be taken off: subscribe a method.
- **A reader runs as a process**: ProcessControl registers one already in a scene as it starts.

## Time, randomness and input

PlayForge reads its time, its random numbers and its pointer through sources a game can replace, so a game can run it on
a fixed step, repeat its rolls, or aim with a gamepad.

This section covers the interfaces; [SERVICES.md](SERVICES.md) covers clocks, rolls and input in full.

### Clocks

A clock is where PlayForge reads its time, so a game can run it on a step of its own.

Implement **`IForgeClock`**.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `DeltaTime`, `UnscaledDeltaTime`, `FixedDeltaTime`, `Time`, `UnscaledTime`, `FrameCount` | As PlayForge reads its time from the clock. |
| `FollowsTicks` | True unless it runs while the game's time stands. |

#### In the picker

No picker offers a clock: code puts one in.

- `ForgeTime.Use(clock)` puts one in for the game, and returns the one it replaced. Null puts Unity's back.
- A run takes one of its own through its packet's `Clock`.
- A sequence takes one through `TaskSequenceBuilder.WithClock(clock)`.

### Random sources

A random source is where PlayForge draws its random numbers, so a test or a replay can repeat its rolls.

Implement **`IForgeRandom`**.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Value` | A number from 0 to 1. |
| `InsideUnitCircle`, `InsideUnitSphere` | A point in a circle of radius 1, or in a sphere of radius 1. |

#### In the picker

No picker offers a random source. `ForgeRandom.Use(source)` puts one in and returns the one it replaced.
`SeededRandom(seed)` is one of PlayForge's.

The cues draw from their own, `ForgeCues.Random`.

### Input

An input source is where PlayForge reads the pointer, so a game can aim with a gamepad's cursor.

Implement **`IForgeInput`**.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `PointerPressed(int button)`, `TryGetPointerRay(out Ray ray)` | Required. As the click-targeting tasks and the sequence library's waits read `ForgeInput.Current`. |
| `PointerHeld`, `TryGetPointerPosition`, `ScrollDelta`, `KeyPressed`, `KeyHeld` | Optional. |

#### In the picker

No picker offers an input source: `ForgeInput.Use(input)` puts one in.

#### Example

This input is a pointer the game moves itself, as a gamepad's cursor:

```csharp
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    // A pointer the game moves itself, as a gamepad's cursor
    public sealed class CursorInput : IForgeInput
    {
        public Camera Camera;
        public Vector2 Position;
        public bool PressedThisFrame;

        public bool PointerPressed(int button) => button == 0 && PressedThisFrame;

        public bool TryGetPointerRay(out Ray ray) => ForgeInput.TryAimThrough(Camera, Position, out ray);

        public bool TryGetPointerPosition(out Vector2 position)
        {
            position = Position;
            return true;
        }
    }
}
```

`ForgeInput.Use(new CursorInput { Camera = Camera.main })` puts it in. The game sets `Position` as its cursor moves, and
`PressedThisFrame` for the one frame of a press.

## Saving a game's own data

A `ForgeSnapshot` holds what PlayForge saves of an entity, and nothing of a game's own: a game saves its own data beside
it.

This section covers how, and what a save leaves out; [SAVING.md](SAVING.md) covers saving and loading.

A snapshot is plain serializable types, so a class of the game's own holds it with the rest:

```csharp
using System;
using FarEmerald.PlayForge;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    public class GameSave
    {
        public ForgeSnapshot Hero;
        public int Gold;
        public int BestCombo;

        public static string Write(IGameplayAbilitySystem hero, ForgeAssetCatalog catalog, int gold, int bestCombo) =>
            JsonUtility.ToJson(new GameSave { Hero = ForgeSave.Capture(hero, catalog), Gold = gold, BestCombo = bestCombo });

        public static GameSave Read(string json, IGameplayAbilitySystem hero, ForgeAssetCatalog catalog)
        {
            var save = JsonUtility.FromJson<GameSave>(json);
            if (save?.Hero != null) ForgeSave.Restore(hero, save.Hero, catalog);
            return save;
        }
    }
}
```

What a save leaves out, which a game that needs it saves itself:

- **Local Data**, an entity's and its assets'.
- **Entity services**, which a loaded entity makes again.
- **Component states**, what `ctx.GetState<T>(this)` keeps.

What a game's code hears of a load:

- **`gas.GetCallbacks().OnRestored`** fires once a load is done ([SAVING.md](SAVING.md#what-a-load-does)).
- An effect component's **`Lend` and `TakeBack`**, and an item component's **`Equipped` and `Unequipped`** with
  `Restoring` true ([Components](#components)).

An asset made at run time, or one of the game's own a save names, goes in the catalog with `catalog.Add(id, asset)`
([SAVING.md](SAVING.md#the-asset-catalog)).

An effect from another entity is saved by that entity's id, through an `IForgeEntityIds` the game gives `Capture` and
`Restore` ([SAVING.md](SAVING.md#effects-from-other-entities)):

| Hook | When PlayForge calls it |
| --- | --- |
| `IdOf(entity)` | As `Capture` saves an effect from another entity: that entity's id, or null where it has none. |
| `Find(id)` | As `Restore` puts the effect back: the entity the id names, or null where there's none. |

## Tags in code

A game's code names tags for its kinds to read: a data key, an event, a state its components grant.

This section covers what a kind of a game's own does with tags; [TAGS.md](TAGS.md) covers tags in full.

- **A tag field's picker** offers the tags of the contexts its `[ForgeTagContext(...)]` names. They're PlayForge's
  (`ForgeContext.Granted`, `ForgeContext.Event`, `ForgeContext.DataKey`, `ForgeContext.ImpactType`,
  `ForgeContext.ContextTag`, `ForgeContext.AssetTag` and the rest), or a project's own, by name.
- **A tag written in code** is `Tag.GenerateAsUnique("State.Stunned")`, the tag the editor's New Tag makes of the path.
  `[ForgeTagExposed(context)]` on a static `Tag` field or property offers it to the pickers of that context.
- **An event** goes to an entity with `entity.SendGameplayEvent(eventTag, data)`, which its abilities' triggers and Wait
  For Event tasks hear. A game's own events are tags of the Event context.
- **A tag's weight** is `entity.GetTagWeight(tag)`. An absent tag's weight is -1, so a test for one is `> 0`.
- **A tag is shown by its `ToString()`**, its display name.

## The editor

A game's editor code can check PlayForge's assets, draw a base type of its own as PlayForge draws its kinds, and start
new assets from its own templates.

This section covers each. The code lives in an editor-only assembly that references `FarEmerald.PlayForge.Editor`.

### Validation rules

An editor validation rule checks PlayForge's assets, and the Problems view and each asset's inspector list what it finds
([EDITOR.md](EDITOR.md)).

Derive from `ForgeValidationRule`, with a public constructor that takes nothing. For a rule about one kind of component,
derive from `ForgeComponentRule<T>` instead.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `Check(Object asset, List<ForgeProblem> problems)` | Each time the validator runs, for each PlayForge asset. It adds what it finds in the asset. |
| `Reads(Object asset)` | True for an asset the rule reads to check others, so an edit to it has the others checked again. |
| `Name` | What the Problems view groups its problems under. By default, its class's name spaced out, without "Rule". |

`ForgeComponentRule<T>` hears each component of its kind an asset holds, whichever kind of asset holds it, through
`Check(Object asset, T component, int nth, string path, List<ForgeProblem>
problems)`, with `nth` the component's count among its kind and `path` its place, as "Components.Array.data[1]".

A problem is a `ForgeProblem(asset, severity, message, propertyPath)`:

- its severity is `ForgeProblemSeverity.Note`, `Warning` or `Error`;
- its `Fix` is a `ForgeQuickFix(name, apply)`, the button that fixes it.

#### In the picker

No picker offers a rule: the validator finds it.

#### Example

This rule warns of a Minimum Range component whose range of 0 refuses no target, and offers to fix it:

```csharp
using System.Collections.Generic;
using FarEmerald.PlayForge.Editor;
using MyGame;
using UnityEditor;
using Object = UnityEngine.Object;

namespace MyGame.EditorRules
{
    public sealed class MinimumRangeRule : ForgeComponentRule<MinimumRangeAbilityComponent>
    {
        public override string Name => "Minimum ranges that refuse nothing";

        protected override void Check(Object asset, MinimumRangeAbilityComponent component, int nth, string path,
            List<ForgeProblem> problems)
        {
            if (component.Least == null || !component.Least.IsConstant || component.Least.Constant > 0f) return;
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                "Its Minimum Range of 0 refuses no target. Give it a range, or take the component off.", path)
            {
                Fix = new ForgeQuickFix("Set its range to 1", () =>
                {
                    Undo.RecordObject(asset, "Set Minimum Range");
                    component.Least = 1f;
                    EditorUtility.SetDirty(asset);
                }),
            });
        }
    }
}
```

#### Traps

- **A rule is asked of every asset** each time the validator runs. Return at once for an asset it doesn't check.

### Drawing a base type of a game's own

A drawer of a game's own draws a field whose type is a base of the game's own, as PlayForge draws its kinds.

PlayForge draws its kinds' fields through drawers registered for their bases, so a kind of a game's own is drawn with no
drawer of its own. A field whose type is a base of the game's own, as a `[SerializeReference] AbstractWeatherRule` on a
component of its own, takes a drawer made from one of PlayForge's:

| Base | Draws |
| --- | --- |
| `AbstractTypeRefDrawer<T>` | The kind picker and the kind's fields. |
| `AbstractGenericDrawer<T>` | The same, folding to a line of its values, and naming kinds without the words in `TypeNameSuffixes`. |
| `AbstractWorkerDrawer<T>` | A generic drawer that offers no None. |
| `AbstractComponentDrawer<T>` | For a base made from `AbstractForgeComponent`: a worker drawer that offers a kind that allows one only while its list holds none. |

#### Hooks

`AbstractTypeRefDrawer<T>`'s virtual members say how it offers and names kinds:

| Hook | When PlayForge calls it |
| --- | --- |
| `FilterType`, `Offers` | Which kinds it offers. |
| `CleanTypeName`, `GetStringValue` | How it names them. |
| `AcceptClear` | Whether it offers None. |
| `OnTypeChanged` | What follows a pick. |

#### Example

In the game's runtime assembly, a base of its own and a field of it:

```csharp
using System;
using UnityEngine;

namespace MyGame
{
    [Serializable]
    public abstract class AbstractWeatherRule
    {
        public abstract float SpeedFactor { get; }
    }

    [Serializable]
    public class RainWeatherRule : AbstractWeatherRule
    {
        [Range(0f, 1f)] public float Slow = 0.2f;

        public override float SpeedFactor => 1f - Slow;
    }

    public class Weather : MonoBehaviour
    {
        [SerializeReference] public AbstractWeatherRule Rule = new RainWeatherRule();
    }
}
```

In its editor assembly, the drawer, whose picker shows the kind as **Rain Weather**, "Rule" taken off its name:

```csharp
using FarEmerald.PlayForge.Editor;
using UnityEditor;

namespace MyGame.EditorRules
{
    [CustomPropertyDrawer(typeof(AbstractWeatherRule), true)]
    public class WeatherRuleDrawer : AbstractGenericDrawer<AbstractWeatherRule>
    {
        protected override bool AcceptClear(SerializedProperty prop) => false;
    }
}
```

### Inspectors, presets and templates

PlayForge's inspectors share a base, and a game's own effect fields can start from the project's templates.

`BasePlayForgeEditor` is the base of PlayForge's asset inspectors. The Forge's presets and a project's templates are
where new assets start ([EDITOR.md](EDITOR.md)).

A `GameplayEffect` field of a game's own marked `[ForgeEffectTemplate("Cost")]` or `[ForgeEffectTemplate("Cooldown")]`
offers, in its right-click menu, to copy or use the project's Cost or Cooldown template. Those are the two keys it
knows.

## Testing your extensions

PlayForge's test kit runs a game's own kinds in a world of entities stepped frame by frame, in Unity's Test Runner.

This section covers what it offers a kind of a game's own; [TESTING.md](TESTING.md) covers setting it up and writing
tests.

| To test | What the kit offers |
| --- | --- |
| **A component, a worker or a scaler** | An asset the kit makes. `Make.Effect(name, new EffectConfig { ... })` takes `Workers`, effect workers of the game's own, and a `MagnitudeScaler`, a scaler of its own. |
| **An ability task** | `Make.Stage(new PulseTask { ... })`, and the stage goes into `Make.Ability(name, stage)`. `World.Cast(caster, ability, target)` casts it. |
| **A hit** | `World.Hit(source, target, effect)`. Its rolls are `World.Rolls(...)`, a scripted source put in for the test, and its time is `World.Clock(step)`. |
| **A number kind or a condition** | No world: `number.Evaluate(NumberContext.For(caster, target, level))` and `condition.Holds(context)`. |

The kit's assembly, `FarEmerald.PlayForge.Testing`, is referenced by the game's test assembly. It's compiled only where
Unity's Test Framework package is in the project.

## The Creator Lab

The Creator Lab plays a project's assets on units of its own ([CREATOR_LAB.md](CREATOR_LAB.md)).

This section covers driving its units the game's own way, through a driver.

### Writing a driver

A driver of a game's own moves, attacks with, casts with and stops the lab's units its own way, as through a NavMesh.

A driver is an `ILabUnitDriver`. Derive from `LabDriver`, a ScriptableObject that implements each member as the lab does
unless overridden.

#### Hooks

| Hook | When PlayForge calls it |
| --- | --- |
| `MoveTo(LabUnit unit, Vector3 point)` | As the lab moves a unit to a point. |
| `Attack(LabUnit unit, LabUnit target)` | As a unit attacks another. |
| `bool Cast(LabUnit unit, Ability ability, ProcessDataPacket data)` | As a unit casts an ability. |
| `Stop(LabUnit unit)` | As a unit stops. |

#### In the picker

`LabDriver` is made with `Create → PlayForge → Lab Driver`. A class of the game's own derives from it, and its asset is
named on the lab's rules' **Driver** ([CREATOR_LAB.md](CREATOR_LAB.md#your-unit)).

#### Example

This driver moves and stops its units through a NavMesh agent, where a unit has one on a NavMesh:

```csharp
using FarEmerald.PlayForge.Lab;
using UnityEngine;
using UnityEngine.AI;

namespace MyGame.Lab
{
    [CreateAssetMenu(menuName = "MyGame/NavMesh Lab Driver")]
    public class NavMeshLabDriver : LabDriver
    {
        public override void MoveTo(LabUnit unit, Vector3 point)
        {
            if (unit != null && unit.TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh) agent.SetDestination(point);
            else base.MoveTo(unit, point);
        }

        public override void Stop(LabUnit unit)
        {
            if (unit != null && unit.TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh) agent.ResetPath();
            base.Stop(unit);
        }
    }
}
```

#### Traps

- **The lab is its own assembly**, `FarEmerald.PlayForge.Lab`, left out where the project defines `PLAYFORGE_NO_LAB`. A
  driver's assembly references it and carries the same constraint, `!PLAYFORGE_NO_LAB` among its **Define Constraints**,
  so it's left out with it.
