# PlayForge — Workers

Workers are the small rules that react to what happens to an entity: a floor that keeps Health above 1, a share of each
blow sent back, a rage at half Health, a burst as a tag comes off. This page maps each kind: when it runs, on whose
system and for whom, and what its context holds. [RECIPES.md](RECIPES.md) builds common rules from them.

## Where workers come from

- **An attribute set's Worker Group**: the entity built from the set holds its workers as it lives.
- **An entity's Worker Group** (its Entity asset): the same, as the entity's own.
- **Its project's Workers**: every entity in a Forge Project holds the workers its settings list, under **Workers** in
  Project Settings → PlayForge, after its own, as it registers. A project starts with the Lifesteal worker,
  whose Share is yours to name.
- **An item's, an ability's or an effect's Worker Group**: the entity holds them while the item is equipped, the ability
  known, or the effect lasts on it. Two of an effect lasting give its group once.
- **An effect's Workers**: effect workers, which run for that effect's own events.
- **Built in**: an On Hit entry gives its holder an impact worker.

## Lifesteal

The **Lifesteal** worker, an impact worker, heals its holder a share of each workable loss it deals another: the
value of its holder's **Share**, the attribute it names, as 0.15 for 15%; with none it heals nothing. The losses that
count are to its **Receives** attribute, and it heals its **Heals** attribute, each the Health role's where it names
none, so a mana burn can heal Health. An effect raises the share while it should, as an effect has no lifesteal
of its own. Only hits of the **Impact Types** it names count, as attacks; with none, any; so a second worker on another attribute, of spells' types,
makes a spell vamp. A worker that runs once for a hit, as this one, runs once however many of it its holder has, its
project's and its own: the first of the kind to take the hit. It allocates nothing, healing or not.

## The map

| Kind | Hook | When it runs | For whom: its context's System | What its context holds |
|---|---|---|---|---|
| Attribute | Pre Change | Before a change to one of the entity's attributes lands: a commit, or a modifier registered or taken off | The entity whose attribute changes | `WorkerContext`: **System**, **AttributeCache**, **Change** (the incoming change: an Inline worker's edit is what commits, before bounds and rounding; a modifier's it only reads), **FrameSummary**, **ActionQueue** |
| Attribute | Post Change | After the change lands | The entity whose attribute changed | The same, **Change** being what it changed by |
| Impact | Validate, then actions | As a change the entity dealt lands on another's attribute, for the attribute the worker subscribes to | **The dealer**; the one hit is `ImpactData.Target` | `ImpactWorkerContext`: **System** (the dealer), **ImpactData** (Target, Attribute, the sourced change, RealImpact, OldValue), **FrameSummary**, **ActionQueue** |
| Tag | Activate, Tick, Resolve | At the entity's end of frame: Activate as its tag requirements come to be met, Tick while they hold, Resolve as they no longer do. Read in a frame the entity's tags changed, or every frame where **Evaluate Every Frame** says | The entity | The system |
| Analysis | Analyze | At the entity's end of frame, after its tag workers | The entity | The system and its **FrameSummary**: the frame's impacts dealt and received, by attribute, target and source; the effects applied and removed; the deaths |
| Effect | Application, Tick, Removal | As the effect lands, at each of its execute ticks (with how many), and as it comes off | **The entity the effect is on** | `EffectWorkerContext`: **System**, **Carrier** (the effect's spec), **FrameSummary**, **ActionQueue**, **ExecuteTicks**; `GetState<T>(worker)` keeps what it counts for this application |
| Effect | Impact | As a change the effect made lands | **The dealer, the effect's caster**, not the one it's on; the one hit is `ImpactData.Target` | The same, with **ImpactData** |

**Inline or deferred.** An attribute worker's **Execution** says how it acts. An Inline one runs inside the change, can
edit the change in Pre Change, and must not change attributes itself, which would run workers inside workers. A
Deferred one returns actions instead, as impact, analysis and effect workers do; the entity's action queue runs them at
its end of frame, where they can change anything. A focused attribute worker, one that watches a single attribute,
sets its **Timing** (Pre Change, Post Change or both), its **Sign Policy** (the changes it takes: losses, gains or
either), and **Allow Self Modification** (whether it takes changes whose source and target are the same entity).

## The action queue

What a deferred worker returns, and whatever else is queued to an entity (`GetActionQueue().Enqueue`), waits in the
entity's action queue for its end of frame.

- **When it runs.** Four passes in each end of frame: the actions queued since the last; then those the tag workers'
  activations and resolutions queue; then those their ticks queue; then those the analysis workers, the Health watch
  and the effects' conditions queue. An action queued as a pass runs joins that pass.
- **In what order.** By priority, higher first (`ActionPriority`, from Critical's 100 down to Cleanup's −300), and in
  the order queued within one. `Enqueue(action, priorityOverride)` puts one at another priority.
- **What's heard.** Each action is heard as it's queued (`OnActionQueued`), then once more as it's done with:
  - run (`OnActionExecuted`);
  - dropped unrun (`OnActionInvalidated`): its `IsValid` said no as its turn came, it was taken off, or its entity had
    ended;
  - failed (`OnActionFailed`, with what it threw): its `IsValid` or its `Execute` threw. It's written to the Console
    as well, and the rest of the queue runs on.

  The queue's events and the entity's callbacks both tell them. The frame's summary keeps the dropped and the failed,
  and an `EffectJournal` settles a worker's action as Ran, Dropped or Failed.
- **Taken off.** `Remove(action)` takes one off, `RemoveWhere(match)` every one it picks, and `Clear()` all of them,
  each heard as dropped; the rest keep their order.
- **An entity that ends.** Its queue closes (`IsClosed`): what waited is dropped and heard, and an action queued to it
  after is dropped at once, so nothing waits on it for good.
- **Another entity's queue.** An action queued to another entity runs at that entity's end of frame: this frame if its
  end of frame is still to come, by the order the entities step in, or else the next.
- **The guard.** A pass runs 10,000 actions at most, so an action that queues another for good can't hold the frame.
  Those left past it wait for the next pass, and the queue writes an error.

## Hearing workers run

An entity tells its workers' runs to whoever listens to its callbacks, and to no one else, so a worker no one hears
costs nothing more:

- `OnEffectWorkerRan`: one of its effects' workers heard one of the effect's moments, its **Hook** (Application, Tick,
  Removal or Impact), on its **Side** (the **Target**, or the **Dealer** for a hit, as the map says), with the **System**
  it ran on and the actions it **Queued**. A tick, which its workers hear every frame, is told only as one executes or a
  worker queues something.
- `OnImpactWorkerRan`: one of its impact workers ran for a hit it dealt, with its **Execution** and what it queued.
- `OnAttributeChangeStep`: a step a change to one of its attributes took, an attribute worker's, with its **Timing**,
  its **Execution**, the change **Before** and **After** and what it **Queued**; a barrier's soak; or a gain's scaling.
  The combat log keeps each hit's steps ([ENTITIES.md](ENTITIES.md#combat-log)).

An `EffectJournal` hears all of it for the entities it's given ([ENTITIES.md](ENTITIES.md#effect-journal)).

## Workable and unworkable

A change is **workable** unless it says it isn't (`SourcedModifiedAttributeValue.ImpactIsWorkable`). An unworkable one
is a change the system makes on its own account (`GenerateUnworkable`), as the pulse each attribute starts with, or a
transfer's two sides. Only a workable change:

- reaches impact workers, unless a worker's **Accept Unworkable Impact** is on;
- is soaked by a barrier as a loss, and scaled by its attribute's **Gains Scaled By** as a gain, as Healing
  Received;
- sends the core's Impact Dealt and Impact Taken events, and counts toward the kill: the killer is whoever dealt
  the last workable loss to the Health role's attribute;
- counts for lifesteal, and is returned by Return Damage;
- is written in the combat log as a hit, taken or dealt; another is a Change.

Attribute workers run for every change, workable or not; one that cares reads `ctx.Change.Value.ImpactIsWorkable`.

## Traps

- **An impact worker runs for the dealer.** Its context's System is the dealer, and the one hit is
  `ImpactData.Target`. A worker that took System for the one hit applied its holder's effects to the target, and read
  the target's items for its holder's.
- **An effect worker's Impact hook runs on the dealer's side too.** Its System is the effect's caster, where its
  Application, Tick and Removal hooks' System is the one the effect is on.
- **A stacking effect's periodic tick lands once for each stack, while its lasting change counts once.** A number that
  reads the effect's stacks on each tick multiplies again: Hangover, which read its stacks, took 100 a tick where 20 was
  meant. Read the stacks in a lasting change, or leave them out of a periodic one. The Problems view warns
  of it.
- **What a worker sets in motion waits for the frame's end.** Deferred actions, deaths and the kills they give, and an
  aura's effect coming off a dead holder all land at the entity's end of frame, so one area's hits resolve together
  ([EFFECTS.md](EFFECTS.md#resolve-order)).
