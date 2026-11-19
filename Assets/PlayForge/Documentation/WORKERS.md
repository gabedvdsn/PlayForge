# PlayForge — Workers

Workers are the small rules that react to what happens to an entity: a floor that keeps Health above 1, a share of
each blow sent back, a rage at half Health, a burst as a tag comes off.

This page maps each kind: when it runs, on whose system and for whom, and what its context holds.
[RECIPES.md](RECIPES.md) builds common rules from them.

## Where workers come from

An entity holds workers from each of these sources:

| Source | What the entity holds |
|---|---|
| **An attribute set's Worker Group** | The workers of the set it's built from, for as long as it lives. |
| **An entity's Worker Group** | The same, as the entity's own. The group is on its Entity asset. |
| **Its project's Workers** | The workers its Forge Project's settings list under **Workers**, in Project Settings → PlayForge. |
| **An item's, an ability's or an effect's Worker Group** | Their workers, while the item is equipped, the ability known, or the effect lasts on it. |
| **An effect's Workers** | Effect workers, which run for that effect's own events. |
| **Built in** | An impact worker, which an On Hit entry gives its holder. |

- Every entity in a Forge Project holds its project's workers after its own, as it registers.
- A project starts with the Lifesteal worker, whose Share is yours to name.
- Two of an effect lasting on an entity give its group once.

## Lifesteal

The **Lifesteal** worker, an impact worker, heals its holder a share of each workable loss it deals another.

| Property | Description |
|---|---|
| **Share** | The attribute whose value is the share it heals, as 0.15 for 15%. With none, it heals nothing. |
| **Receives** | The attribute whose losses count. With none, the Health role's. |
| **Heals** | The attribute it heals. With none, the Health role's. |
| **Impact Types** | The types of hit that count, as attacks. With none, any. |

- **Receives** and **Heals** are separate, so a mana burn can heal Health.
- An effect has no lifesteal of its own. An effect raises the holder's share while it should.
- A second worker on another attribute, of spells' types, makes a spell vamp.
- A worker that runs once for a hit, as this one, runs once however many of it its holder has, its project's and its
  own. The first of the kind to take the hit runs.
- It allocates nothing, healing or not.

## The map

The map shows, for each kind of worker, when it runs, whose system it runs on, and what its context holds.

| Kind | Hook | When it runs | For whom: its context's System | What its context holds |
|---|---|---|---|---|
| Attribute | Pre Change | Before a change to one of the entity's attributes lands | The entity whose attribute changes | `WorkerContext` |
| Attribute | Post Change | After the change lands | The entity whose attribute changed | `WorkerContext` |
| Impact | Validate, then actions | As a change the entity dealt lands on another's attribute | **The dealer** | `ImpactWorkerContext` |
| Tag | Activate, Tick, Resolve | At the entity's end of frame | The entity | The system |
| Analysis | Analyze | At the entity's end of frame, after its tag workers | The entity | The system and its **FrameSummary** |
| Effect | Application, Tick, Removal | As the effect lands, at each of its execute ticks, and as it comes off | **The entity the effect is on** | `EffectWorkerContext` |
| Effect | Impact | As a change the effect made lands | **The dealer, the effect's caster** | `EffectWorkerContext`, with **ImpactData** |

Each kind has a page of its own below.

### Attribute workers

An attribute worker runs as a change to one of its entity's attributes lands: before it, in Pre Change, or after it,
in Post Change.

A change is a commit, or a modifier registered or taken off. Its context, `WorkerContext`, holds:

| Member | What it holds |
|---|---|
| **System** | The entity whose attribute changes. |
| **AttributeCache** | The entity's attribute cache. |
| **Change** | In Pre Change, the incoming change. In Post Change, what the attribute changed by. |
| **FrameSummary** | The frame's summary ([Analysis workers](#analysis-workers)). |
| **ActionQueue** | The entity's action queue ([The action queue](#the-action-queue)). |

In Pre Change, an Inline worker's edit to the incoming change is what commits, before bounds and rounding. A
modifier's change it only reads.

**Inline or deferred.** An attribute worker's **Execution** says how it acts:

| Execution | How it acts |
|---|---|
| Inline | Runs inside the change, and can edit the change in Pre Change. It must not change attributes itself, which would run workers inside workers. |
| Deferred | Returns actions instead, as impact, analysis and effect workers do. The entity's action queue runs them at its end of frame, where they can change anything. |

A focused attribute worker, one that watches a single attribute, sets these among its fields:

| Property | Description |
|---|---|
| **Timing** | When it runs: Pre Change, Post Change or both. |
| **Sign Policy** | The changes it takes: losses, gains or either. |
| **Allow Self Modification** | Whether it takes changes whose source and target are the same entity. |

### Impact workers

An impact worker runs as a change its entity dealt lands on another's attribute, for the attribute the worker
subscribes to.

Its hook validates the hit, then makes its actions. It runs for the dealer, not the one hit
([Traps](#traps)). Its context, `ImpactWorkerContext`, holds:

| Member | What it holds |
|---|---|
| **System** | The dealer. The one hit is `ImpactData.Target`. |
| **ImpactData** | The hit: its Target, its Attribute, the sourced change, its RealImpact and its OldValue. |
| **FrameSummary** | The frame's summary. |
| **ActionQueue** | The action queue ([The action queue](#the-action-queue)). |

| Property | Description |
|---|---|
| **Accept Unworkable Impact** | Also takes unworkable changes ([Workable and unworkable](#workable-and-unworkable)). |

### Tag workers

A tag worker acts while its entity's tags meet its requirements.

| Hook | When it runs |
|---|---|
| Activate | As its tag requirements come to be met. |
| Tick | While they hold. |
| Resolve | As they no longer do. |

Each runs at the entity's end of frame. A tag worker is read in a frame the entity's tags changed, or every frame where
its **Evaluate Every Frame** says. Its context is the system, the entity.

| Property | Description |
|---|---|
| **Requirements** | Its tag requirements, each a tag, Require or Avoid, and a required weight. With none, it's always active. |
| **Tick Pause** | Frames between its ticks, read at its entity's level: 0 for every frame, or −1, the default, for none. |

Require holds while the tag's weight is at least the required weight, and Avoid while it's below.

### Analysis workers

An analysis worker looks over its entity at its end of frame, after its tag workers.

Its hook is Analyze. Its context is the system and its **FrameSummary**, which holds:

- the frame's impacts dealt and received, by attribute, target and source;
- the effects applied and removed;
- the deaths.

It returns actions, which the entity's action queue runs ([The action queue](#the-action-queue)).

### Effect workers

An effect worker runs for its own effect's events, as the effect lands, ticks, comes off and deals an impact.

| Hook | When it runs | Its context's System |
|---|---|---|
| Application | As the effect lands | **The entity the effect is on** |
| Tick | At each of its execute ticks, with how many | The entity the effect is on |
| Removal | As the effect comes off | The entity the effect is on |
| Impact | As a change the effect made lands | **The dealer, the effect's caster**, not the one it's on. The one hit is `ImpactData.Target`. |

Its context, `EffectWorkerContext`, holds:

| Member | What it holds |
|---|---|
| **System** | The entity it runs on, as above. |
| **Carrier** | The effect's spec. |
| **FrameSummary** | The frame's summary. |
| **ActionQueue** | The action queue ([The action queue](#the-action-queue)). |
| **ExecuteTicks** | For a tick, how many ticks executed. |
| **ImpactData** | For the Impact hook, the change that landed. |

`GetState<T>(worker)` keeps what it counts for this application.

## The action queue

What a deferred worker returns, and whatever else is queued to an entity (`GetActionQueue().Enqueue`), waits in the
entity's action queue for its end of frame.

**When it runs.** Four passes run in each end of frame, in this order:

1. The actions queued since the last end of frame.
2. Those the tag workers' activations and resolutions queue.
3. Those their ticks queue.
4. Those the analysis workers, the Health watch and the effects' conditions queue.

An action queued as a pass runs joins that pass.

**In what order.** By priority, higher first (`ActionPriority`, from Critical's 100 down to Cleanup's −300), and in the
order queued within one priority. `Enqueue(action, priorityOverride)` puts one at another priority.

**What's heard.** Each action is heard as it's queued (`OnActionQueued`), then once more as it's done with:

| Outcome | Event | When |
|---|---|---|
| Run | `OnActionExecuted` | It ran. |
| Dropped unrun | `OnActionInvalidated` | Its `IsValid` said no as its turn came, it was taken off, or its entity had ended. |
| Failed | `OnActionFailed`, with what it threw | Its `IsValid` or its `Execute` threw. It's written to the Console as well, and the rest of the queue runs on. |

The queue's events and the entity's callbacks both tell them. The frame's summary keeps the dropped and the failed,
and an `EffectJournal` settles a worker's action as Ran, Dropped or Failed.

**Taken off.** `Remove(action)` takes one off, `RemoveWhere(match)` every one it picks, and `Clear()` all of them.
Each is heard as dropped, and the rest keep their order.

**An entity that ends.** Its queue closes (`IsClosed`). What waited is dropped and heard, and an action queued to it
after is dropped at once, so nothing waits on it for good.

**Another entity's queue.** An action queued to another entity runs at that entity's end of frame. That's this frame
if its end of frame is still to come, by the order the entities step in, or else the next.

**The guard.** A pass runs 10,000 actions at most, so an action that queues another for good can't hold the frame.
Those left past it wait for the next pass, and the queue writes an error.

## Hearing workers run

An entity tells its workers' runs to whoever listens to its callbacks, and to no one else. So a worker no one hears
costs nothing more.

| Callback | What it tells |
|---|---|
| `OnEffectWorkerRan` | One of its effects' workers heard one of the effect's moments (below). |
| `OnImpactWorkerRan` | One of its impact workers ran for a hit it dealt, with its **Execution** and what it queued. |
| `OnAttributeChangeStep` | A step a change to one of its attributes took (below). |

`OnEffectWorkerRan` tells:

- its **Hook**: Application, Tick, Removal or Impact;
- its **Side**: the **Target**, or the **Dealer** for a hit, as the map says;
- the **System** it ran on, and the actions it **Queued**.

A tick, which its workers hear every frame, is told only as one executes or a worker queues something.

`OnAttributeChangeStep` tells one of three steps:

- an attribute worker's, with its **Timing**, its **Execution**, the change **Before** and **After**, and what it
  **Queued**;
- a barrier's soak;
- a gain's scaling.

The combat log keeps each hit's steps ([ENTITIES.md](ENTITIES.md#combat-log)). An `EffectJournal` hears all of it for
the entities it's given ([ENTITIES.md](ENTITIES.md#effect-journal)).

## Workable and unworkable

A change is **workable** unless it says it isn't (`SourcedModifiedAttributeValue.ImpactIsWorkable`).

An unworkable one is a change the system makes on its own account (`GenerateUnworkable`), as the pulse each attribute
starts with, or a transfer's two sides.

Only a workable change:

- reaches impact workers, unless a worker's **Accept Unworkable Impact** is on;
- is soaked by a barrier as a loss, and scaled by its attribute's **Gains Scaled By** as a gain, as Healing Received;
- sends the core's Impact Dealt and Impact Taken events;
- counts toward the kill: the killer is whoever dealt the last workable loss to the Health role's attribute;
- counts for lifesteal, and is returned by Return Damage;
- is written in the combat log as a hit, taken or dealt. Another change is written as a Change.

Attribute workers run for every change, workable or not. One that cares reads `ctx.Change.Value.ImpactIsWorkable`.

## Traps

These are the mistakes workers invite most often.

- **An impact worker runs for the dealer.**
  - Its context's System is the dealer, and the one hit is `ImpactData.Target`.
  - A worker that took System for the one hit applied its holder's effects to the target, and read the target's items
    for its holder's.
- **An effect worker's Impact hook runs on the dealer's side too.**
  - Its System is the effect's caster.
  - Its Application, Tick and Removal hooks' System is the one the effect is on.
- **A stacking effect's periodic tick lands once for each stack, while its lasting change counts once.**
  - A number that reads the effect's stacks on each tick multiplies again. Hangover, which read its stacks, took 100 a
    tick where 20 was meant.
  - Read the stacks in a lasting change, or leave them out of a periodic one.
  - The Problems view warns of it.
- **What a worker sets in motion waits for the frame's end.**
  - Deferred actions, deaths and the kills they give, and an aura's effect coming off a dead holder, all land at the
    entity's end of frame.
  - One area's hits therefore resolve together ([EFFECTS.md](EFFECTS.md#resolve-order)).
