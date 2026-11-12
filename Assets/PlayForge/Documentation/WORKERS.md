# PlayForge — Workers

Workers are the small rules that react to what happens to an entity: a floor that keeps Health above 1, a share of each
blow sent back, a rage at half Health, a burst as a tag comes off. This page maps each kind: when it runs, on whose
system and for whom, and what its context holds (TW-23). [RECIPES.md](RECIPES.md) builds common rules from them.

## Where workers come from

- **An attribute set's Worker Group**: the entity built from the set holds its workers as it lives.
- **An entity's Worker Group** (its Entity asset): the same, as the entity's own.
- **An item's, an ability's or an effect's Worker Group**: the entity holds them while the item is equipped, the ability
  known, or the effect lasts on it (TW-13). Two of an effect lasting give its group once.
- **An effect's Workers**: effect workers, which run for that effect's own events.
- **Built in**: an On Hit entry and a lifesteal give their holder an impact worker (TW-6, TW-15).

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

## Workable and unworkable

A change is **workable** unless it says it isn't (`SourcedModifiedAttributeValue.ImpactIsWorkable`). An unworkable one
is a change the system makes on its own account (`GenerateUnworkable`), as the pulse each attribute starts with, or a
transfer's two sides (TW-20). Only a workable change:

- reaches impact workers, unless a worker's **Accept Unworkable Impact** is on;
- is soaked by a barrier as a loss (TW-12), and scaled by its attribute's **Gains Scaled By** as a gain, as Healing
  Received (TW-15);
- sends the core's Impact Dealt and Impact Taken events (TW-5), and counts toward the kill: the killer is whoever dealt
  the last workable loss to the Health role's attribute;
- counts for lifesteal, and is returned by Return Damage (TW-15);
- is written in the combat log as a hit, taken or dealt (TW-22); another is a Change.

Attribute workers run for every change, workable or not; one that cares reads `ctx.Change.Value.ImpactIsWorkable`.

## Traps

- **An impact worker runs for the dealer.** Its context's System is the dealer, and the one hit is
  `ImpactData.Target`. A worker that took System for the one hit applied its holder's effects to the target, and read
  the target's items for its holder's (GAS-72).
- **An effect worker's Impact hook runs on the dealer's side too.** Its System is the effect's caster, where its
  Application, Tick and Removal hooks' System is the one the effect is on.
- **A stacking effect's periodic tick lands once for each stack, while its lasting change counts once.** A number that
  reads the effect's stacks on each tick multiplies again: Hangover, which read its stacks, took 100 a tick where 20 was
  meant. Read the stacks in a lasting change, or leave them out of a periodic one (FEAT-33 is to make a lasting change
  count each stack as a tick does). The Problems view warns of it (TW-25).
- **What a worker sets in motion waits for the frame's end.** Deferred actions, deaths and the kills they give, and an
  aura's effect coming off a dead holder all land at the entity's end of frame, so one area's hits resolve together
  ([EFFECTS.md](EFFECTS.md#resolve-order)).
