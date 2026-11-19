# PlayForge — Gameplay Effects

A **Gameplay Effect** asset describes a change to an entity: damage, a heal, a timed buff, a poison, a cost or a
cooldown. An effect can change attributes (numbers such as Health), grant tags, apply other effects and run workers.

An entity is anything that runs a gameplay ability system (GAS), such as the player or an enemy. Abilities and items
apply effects to a target entity, and so can your code. The ability, item or entity that applies an effect is its
*origin*.

The duration's Policy decides how long an effect lives. An Instant effect makes its change once and is done.

A Durational or Infinite effect goes on the target's effect *shelf*, its list of active effects. It stays until its time
runs out or something removes it.

The kind of change follows from the Policy:

| Effect | Delivery | The change |
|---|---|---|
| Instant | OnApply | one permanent commit, such as "take 30 damage" |
| Durational or Infinite, no ticks | Continuous | a modifier while the effect is active, reversed exactly when it ends |
| Durational or Infinite, periodic ticks | Periodic | one permanent commit per tick, such as "5 Health per second" |

A commit stays after the effect ends. A modifier doesn't. [EFFECT_PIPELINE.md](EFFECT_PIPELINE.md) has the full model.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Fireball Damage gives `Effect.FireballDamage`.

Each section below matches the inspector section of the same name.

## Identity

The Identity section names the effect, gives its asset tag, and holds what your UI shows of it.

| Property | Description |
|---|---|
| **Name** | The effect's display name. It also gives the asset tag, so renaming the effect changes its tag. |
| **Description** | Free text for your UI. |
| **Asset Tag** | Read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. The tag is stored when you edit the Name. |
| **Visibility** | A tag your UI can read to decide whether to show the effect, such as `Always Visible` or `Not Visible`. The Forge lists it, but the runtime doesn't act on it. |
| **Textures** | Icons, each paired with a tag. The texture tagged `Primary` is the effect's icon; without one, the first texture is used. |
| **Inherit From Origin** | Takes the effect's icon from its origin, the ability, item or entity that applied it. |
| **Origin Texture** | Shown under Inherit From Origin. A tag, `Primary` to start, that names the origin's texture to take. |

> [!NOTE]
> Renaming an effect changes its asset tag. Update anything that names the old tag.

If the stored asset tag differs from the one the Name gives, it shows in warning text with an **Update** button that
stores the new one.

The effect system finds an effect by its asset tag when it is applied again or removed, whoever applied it. While a
Durational or Infinite effect is active, its target carries the tag.

Cooldowns work this way: an ability's Cooldown check looks for the cooldown effect's tag on the caster.

With **Inherit From Origin** on, the effect's icon is the origin's texture under **Origin Texture**. Where the origin
has none under that tag, the effect's own stands.

A spec reads the icon with `spec.GetIcon()`, as the Demo's bar and the Additional Content's status bar do. An origin
holds textures through `IHasTextures`, as abilities, items, effects, entities and both hubs do.

## Leveling

An effect has no level of its own. At runtime it takes the level of its origin, and its scalers use that level to pick
per-level values, such as 20/30/40/50 damage at levels 1 to 4.

A scaler is a value source, such as a table with one value per level. A level provider is the asset whose level range an
effect is authored for, normally its origin.

| Property | Description |
|---|---|
| **Link Mode** | Standalone (default): the effect takes the level of what applies it, as a note says.<br>Linked To Provider: the effect takes its range from a level provider. |
| **Level Provider** | Shown while linked. The asset the range comes from: an Entity, Ability, Effect, Item or Attribute Set. |
| **Link Contained Effects** | Shown while linked. Links every contained effect (see [Contained Effects](#contained-effects)) to this effect's provider, so their per-level values follow its range. |
| **Unlink Contained Effects** | Removes the contained effects' links that point at the provider. |

- Right-click the **Level Provider** field to pick from one type.
- A link that would make a loop is refused, with an error in the Console.
- While no provider is set, a warning says the effect takes the level of what applies it.
- While linked, Starting Level and Max Level show the provider's range, read-only.
- Link Contained Effects and Unlink Contained Effects each ask first, and one Undo takes it back.

The link serves the inspector: a scaler set to Lock To Level Provider shows one value per level of the provider.

An effect field in another inspector, such as an ability's Cost, says how the effect takes its levels. Its **Link**
links the effect to that asset.

## Tags

The Tags section sets what the effect grants its target while it's on, and which other effects it removes, purges or
blocks.

| Property | Description |
|---|---|
| **Granted Tags** | Applied to the target while the effect is active. An Instant effect grants them only for the moment it applies. |
| **Granted Abilities** | Given to the target as the effect lands, each at the level of what applied it, and taken back as it comes off. |
| **Context Tags** | Describe the effect, such as `Fire`. They are not applied to the entity. |
| **Removes Effects Tagged** | Makes the effect a cleanse. |
| **Purges** | Makes the effect a purge, by the project's purge levels ([Purging](#purging)). |
| **Blocks Effects Tagged** | Makes the effect an immunity. |

A change of the target's sides while the effect lasts is a **Side Change** component ([Side Change](#side-change)).

A granted ability that's running as the effect comes off is interrupted. An ability the target can't take, such as one
it knows already when duplicates aren't allowed, isn't lent, so the target keeps its own as the effect comes off.

An Instant effect lends no abilities, as it's never on the target.

Each change the effect makes carries its context tags and its origin's context tags, and workers can filter by them.

As a cleanse lands, every other effect on the target tagged with one of its tags, or a child of one, comes off at the
next tick, as a removed effect does. An effect counts as tagged by its asset tag, a context tag or a tag it grants.

For example, `Status.Debuff` takes off `Status.Debuff.Poison` and `Status.Debuff.Burn`, and `State.Burning` takes off
whatever grants it.

As a purge lands, the effects on the target its strength reaches, of the kinds it names and applied by whom it says,
come off.

While an immunity is on the target and its ongoing requirements hold, an effect tagged with one of its tags, or a child
of one, can't land. Tags match the same way as for a cleanse, and applying a blocked effect returns false.

An immunity leaves the effects already on the target. Add the same tags to Removes Effects Tagged to clear them as it
lands.

## Purging

Purging takes effects off an entity by strength. A project's **Purge Levels**, under Forge Project in Project Settings →
PlayForge, rank its purges.

Purge Levels holds the project's tiers, weakest first:

| Property | Description |
|---|---|
| **Tier** | A tag, as Purge.Basic or Purge.Strong. An effect is at the first tier whose tag it carries, as its asset tag or a context tag, or a child of one. |
| **Kept On Death** (on a tier) | A death keeps the effects at this tier. |
| **Kept On Death** (on Purge Levels) | Categories a death keeps whatever their tiers. A project starts with an ability's cooldowns, lent effects and toggles. |

- A purge of a tier takes the effects at that tier or under it, as a Strong dispel takes the Basic too.
- An effect at no tier is never purged, but by death.

A purge, an effect's **Purges** or `ForgePurge.Purge(entity, purge)` in code, has these fields:

| Property | Description |
|---|---|
| **Strength** | A tier's tag. The purge takes the effects at that tier or under it. |
| **Kinds** | Tags an effect must carry one of to be taken, as Status.Debuff. Where it names none, it takes any kind. |
| **Applied By** | Whom it takes the effects of, by who applied each, against the one purged: itself, its allies (its buffs) or its enemies (its debuffs). An applier sharing none of its sides is an enemy. |
| **Filters** | Filters of a project's own, each an `AbstractPurgeFilter` whose `Takes` says yes or no. **Except Tagged** spares the effects carrying the tags it names. |

- An effect a purge takes comes off at the next tick, as a removed effect does.
- A purge never takes the effect doing it.
- `ForgePurge.Purge(entity, strength)` purges by a strength alone, of every kind.

A staying entity's death purges its effects too, all but those it keeps ([ENTITIES.md](ENTITIES.md#death)). Each tier's
**Kept On Death** keeps the effects at that tier, and the categories in Purge Levels' own **Kept On Death** keep theirs
whatever their tiers.

## Duration

The Duration section sets how long the effect lasts, how it ticks, and what another application of it does. Its fields
show as the Policy uses them.

| Property | Description |
|---|---|
| **Policy** | Instant (default) makes the change once.<br>Durational stays for a set time.<br>Infinite stays until something removes it. |
| **Duration** | Durational only. The effect's length in seconds, with its scaler. |
| **Periodic Ticks** | Durational and Infinite. A tick makes the effect's change again, as a new commit. |
| **Ticks** | Durational, with Periodic Ticks. How many ticks fit in the duration, evenly spaced. |
| **Tick Every** | Infinite, with Periodic Ticks. The seconds between ticks. |
| **Tick On Application** | Durational and Infinite. Runs one tick as soon as the effect is applied, before the first periodic tick. |
| **Additional Executions** | While it ticks. Adds to the number of times each tick executes the change. |
| **Real Delta Time** | Durational only. With a scaler, combines the scaler and the frame time. Multiply With Scaler, for example, slows the clock down or speeds it up. |
| **Delta Time Scaler** | Durational only. Changes how fast the effect's clock runs. With no scaler, the clock follows Unity's frame time. |
| **Re-application** | Durational and Infinite. What happens when the effect is applied to a target that already has it. |
| **Then** | All but Add Another. What the old container does with its time as it takes in the application. |
| **Stacking**, **Stack Behaviours**, **Stack Amount** | Stack Existing only. How the stacks keep their time, the rules they follow, and how many each application adds. |

### Ticks

A tick makes a lasting effect's change again, as a new commit.

- **Ticks** (Durational) spaces its ticks evenly over the duration. The last falls as the duration ends, so 5 ticks over
  10 seconds tick every 2 seconds.
- **Tick Every** (Infinite) sets the seconds between ticks.
- **Tick On Application** runs one tick as soon as the effect is applied, before the first periodic tick. It runs again
  when re-application refreshes or extends the effect.
- Use Tick On Application with Periodic Ticks. On its own, it is a shape that the Problems view flags.

**Additional Executions** applies while the effect ticks. Each tick executes the change a number of times, N plus this,
combined with its scaler.

N comes from the effect's container and is normally 1. A stacking container that shares one duration, or takes it from
one stack, uses its stack count.

A tick only executes while the While On requirements hold (see [Requirements](#requirements)).

### Re-application (Durational and Infinite)

Re-application decides what happens when the effect is applied to a target that already has it.

Each application on the shelf lives in a *container*. The effect is matched by its asset tag, whoever applied it.

**Re-application** offers four choices:

| Choice | What it does |
|---|---|
| Add Another (default) | Adds a separate container, and both apply. |
| Do Nothing | Adds nothing new. |
| Replace Existing | Gives the old container the new application's values and modifiers, and keeps its stack count. Past commits stay. |
| Stack Existing | Adds stacks to the old container (see Stacking). |

**Then** (all but Add Another) applies as the old container takes in the application:

| Choice | What it does |
|---|---|
| Do Nothing (default) | Leaves its time alone. |
| Refresh Duration | Restarts its time. |
| Extend Duration | Adds the new duration to its time. |

Refresh and Extend also run Tick On Application again.

### Stacking (Stack Existing only)

Stacking sets how a Stack Existing effect's stacks keep their time, and how many stacks each application adds.

| Property | Description |
|---|---|
| **Stacking** | How the stacks keep their time. |
| **Stack Behaviours** | Optional stacking rules, such as Stack Bounds Config and Starting Stacks. |
| **Stack Amount** | With its scaler, the stacks each application adds. 0 counts as 1. |

**Stacking** offers three choices:

| Choice | What it does |
|---|---|
| Stacks Share One Duration (default) | Runs one timer for all stacks. Adding a stack doesn't reset it. |
| Stacks Have Independent Durations | Gives each stack its own timer and ticks. Stacks drop off one at a time, and the effect lasts as long as its longest stack. |
| Duration Taken From One Stack | Runs one stack's timer at a time. Each time it runs out, one stack is lost and the timer restarts. |

#### Stack behaviours

Stack behaviours are optional rules that the stacks follow.

- Stack Bounds Config keeps the stack count between a Min and a Max.
- Starting Stacks sets the stacks a new effect starts with.
- The Min, the Max and Starting Stacks are each a number field read at the effect's level.
- Starting Stacks is a single number field, where a number, a scaler, a combine and a rounding of its own were once
  written out.

An application that would take the effect past the Max overflows:

- Its Overflow Effects land, from the same origin, as a bleed's fourth stack hemorrhages.
- They land on the target, or, with **Overflow Lands On** set to Caster, on its caster, as a frenzy.
- Deny Refresh On Overflow keeps that application from refreshing or extending the effect.
- Clear Stacks On Overflow takes the effect's stacks off.

#### Reading the stack count

A change reads the stack count through an **Effect Stacks** scaler ([SCALERS.md](SCALERS.md)). A magnitude of −5 with
Multiply With Scaler takes 5 a stack.

- A Continuous change updates whenever the count changes.
- A Periodic change already lands once for each stack. One that also reads its stacks counts them twice, so 3 stacks hit
  9 times as hard as 1.
- The Problems view warns of it. Its quick fix, **Count Stacks Once**, gives the number one stack gives.

### Moving an effect to another entity

An effect on one entity can move to another, as a possession hands a body's effects over.

`gas.GetEffectSystem().MoveTo(effect, other)` moves each of the effect's containers on the entity onto another.

- Each container keeps its stacks, its time left and what applied it.
- A move is neither a removal nor an application. None of either's workers, commits or contained effects run, and
  neither is told.
- Its lasting changes, its granted tags, the abilities it lends and its lasting cues go with it.

## Impact

The impact is the change the effect makes to an attribute. The first fields are its first impact, whose delivery the
duration gives it.

Additional Impacts change more attributes, or the same one again, each with a delivery of its own.

| Property | Description |
|---|---|
| **Attribute** | The attribute to change. Leave it empty for an effect that only grants tags, applies other effects or runs workers. |
| **Operation** | How the impact changes the attribute, and which of its values, in one choice, such as Add to Current or Multiply Base. See [Operations](#operations). |
| **Multiply Behaviour** | Multiply only. How this multiply combines with other multipliers. |
| **Add Behaviour** | Add only, shown where it matters. How this add combines with others on the attribute. |
| **Magnitude** | The amount. See [Magnitude](#magnitude). |
| **Pipeline** | The calculation each hit of the impact goes through as it lands, a Value Pipeline: the caster's bonuses, the target's reductions, a critical roll, a barrier. It runs for an instant impact's hit and each tick of a periodic one ([VALUE_PIPELINES.md](VALUE_PIPELINES.md)). |
| **Delivery** | Read-only. When the impact lands, which the duration gives it. See [Delivery and commits](#delivery-and-commits). |
| **Commits** | Read-only, for a periodic impact. How many times it lands. |
| **Impact Types** | Tags that label the kind of change, such as `Damage.Fire`. Workers can accept or skip changes by type. |
| **Affiliation** | Which targets the effect can be applied to. See [Affiliation](#affiliation). |
| **Comparison** | All but Always Allow. Whether one affiliation tag matching is enough, or every tag must match. |
| **Affiliations** | Use Affiliation List only. The tags the target is compared with. |

- A target that lacks the attribute isn't changed.
- `Never Allow`, as an impact type, hides the change from workers that check types. `Always Allow` passes every type
  check.

The section also holds the effect's **Retention Group** ([Retention Group](#retention-group)) and its
**Proc Coefficient** ([On Hit](#on-hit)).

### Operations

The **Operation** sets how the impact changes the attribute, and which of its values it changes.

**Base** is the value the attribute is measured against, and **Current** is the value in play. For Health, Base is the
maximum and Current is what is left.

| Operation | What it does |
|---|---|
| Add | Adds the magnitude. Use a negative magnitude to subtract. |
| Multiply | Multiplies by the magnitude. |
| Override | Sets the value to the magnitude. |
| Flat Bonus | Adds too, but as a modifier it lands after the attribute's multipliers, so they don't scale it. |

#### Multiply Behaviour

**Multiply Behaviour** (Multiply only) sets how a multiply combines with other multipliers:

| Choice | What it does |
|---|---|
| Multiplicative (default) | Compounds with other multipliers, so ×1.5 and ×1.5 give ×2.25. |
| Additive Bonus | Adds the bonuses first, so +50% and +50% give ×2.0. |

#### Add Behaviour

**Add Behaviour** (Add only, shown where it matters) sets how an add combines with the others on the attribute
([ATTRIBUTES.md](ATTRIBUTES.md)):

| Choice | What it does |
|---|---|
| Attribute Role (default) | As the attribute's role says: a Resistance's adds diminish. |
| Sum | A plain sum. |
| Diminishing | On what's left. |

### Magnitude

The **Magnitude** is the amount the impact applies.

It is a Constant number, or, by its kind, a scaler's value at the origin's level, such as a By Level table of values per
level. Combine joins the scaler and the number ([SCALERS.md](SCALERS.md)).

- A Continuous modifier works its magnitude out again whenever the attribute is recalculated.
- A commit works it out once.

#### Set By Caller

A **Set By Caller** scaler reads a magnitude that your code sets, under a tag, as it applies the effect. You set it in
one of two places:

- on the spec, with `SetByCaller(tag, magnitude)`;
- on an ability activation's data, which each effect the ability applies takes as it's made, its cost, cooldown and
  contained effects among them.

Only the tag itself matches. Where none is set, the scaler reads its Default.

#### Other kinds

A magnitude can also read what the effect has dealt, a part of another effect, ability or item on either side, or what a
worker answers. SCALERS.md lists each kind.

### Delivery and commits

The read-only **Delivery** and **Commits** rows say when, and how often, the first impact lands.

The duration gives the delivery:

| Delivery reads | For | When the impact lands |
|---|---|---|
| On applying | An Instant effect | Once, as it's applied. |
| While the effect lasts | A Durational or Infinite effect with no ticks | As a modifier while the effect is active. |
| Each tick | A Durational or Infinite effect with periodic ticks | Once per tick. It reads "Each tick, and on applying" when Tick On Application is on. |

- A note says when Tick On Application alone keeps a multiply or override as a modifier, so it reverses as the effect
  ends.
- **Commits**, for a periodic impact, says how many times it lands at the effect's first level, as in
  "7 over 6 s, level 1".
- For an Infinite effect, Commits says how far apart the ticks are.

> [!NOTE]
> The inspector no longer shows Reverse On Removal. A Continuous modifier always reverses when the effect ends, and a
> commit never does, so the flag changes nothing. The Problems view still flags a periodic effect that sets it.

### Affiliation

**Affiliation** decides which targets the effect can be applied to. If the check fails, the effect isn't applied.

An entity's affiliation tags are its team or faction, set on its Entity asset.

| Choice | What it allows |
|---|---|
| Always Allow (default) | Any target. |
| Affiliated | Targets that share the origin's affiliation. |
| Unaffiliated | Targets that don't share it. |
| Use Affiliation List | Compares the target with the **Affiliations** list instead. |

**Comparison** (all but Always Allow): Any (default) needs one tag to match. All needs every tag to match.

### Additional Impacts

Each additional impact changes an attribute of its own, or the same one again, with a delivery of its own. One effect
can lower Armor while it lasts and burn Health on each tick.

**Add Impact** adds one with a delivery the duration can make: Continuous on a Durational or Infinite effect, On Apply
on an Instant one.

While it's folded, an impact shows one line, such as "Mana −10 · Continuous", and its trash button removes it.

| Property | Description |
|---|---|
| **Attribute**, **Operation**, **Multiply Behaviour**, **Add Behaviour**, **Magnitude**, **Pipeline** | As for the first impact ([Impact](#impact)). |
| **Delivery** | When the impact lands. It offers what the duration can make. |
| **Commits** | Read-only, for a Periodic impact, as for the first impact. |
| **Order** | Override only. Among overrides of one attribute, the lowest order wins. |
| **Retention Group** | The group the change counts toward. Empty, it takes the effect's. |
| **Impact Types** | As for the first impact. |

| Delivery | When it lands |
|---|---|
| Continuous | While the effect lasts. Then it reverses. |
| On Apply | Once, as the effect is applied. |
| Periodic | On each tick, and as the effect is applied when Tick On Application is on. |

- An Instant effect makes only On Apply impacts, and Periodic needs ticks.
- If the duration changes so that an impact's delivery can't happen, a warning under it says so, and the impact never
  applies.
- An impact with no Attribute changes nothing.
- The Cost check counts every attribute a cost spends (see [ABILITIES.md](ABILITIES.md)).

### Contained Effects

Contained effects are other effects this one applies, with the same origin.

Each row pairs a moment with an effect, and says whom it lands on:

| Column | Description |
|---|---|
| Moment | On Apply (when this effect is applied), On Tick (on each execute tick) or On Remove (when this effect is removed). |
| Effect | The effect to apply. |
| Lands on | The **Target**, this effect's, as it always did, or the **Caster**, as a lifesteal's heal or a curse's rebound as it ends. |

- An Instant effect has no ticks and no removal, so only its On Apply rows run.
- An effect that is asked to apply again while it is still applying is refused, with a warning. So an effect that
  contains itself doesn't loop forever.

### Retention Group

The **Retention Group** is the group this effect's changes count toward (default `Ignore`), unless an impact names its
own.

Groups split an attribute's value for queries and UI, such as base and bonus health. They never change the math.

## Levels

The effect's levels say what it does, level by level.

Its line is its first impact by level, with an add signed, as "-10/-20/-30 Health" or "+20 Armor". The effect's header
and The Forge's Summary column show it.

The line reads at its provider's levels where it's linked to one, and at levels 1 to 5 where it isn't.
`ForgeAssetLines.Of(asset)` gives any asset's line in the editor.

The whole reading is code's, for a tooltip or a game's own screen.
`AbilityInspector.BuildEffectSeries(effect, 1, 5, caster, target)` reads the effect at levels 1 to 5 against a caster
and a target, either of which may be null.

| Member | What it gives |
|---|---|
| `Magnitude`, `Total` | Each impact's magnitude and total at each level. |
| `Line` | Each impact's line, as its field's summary reads it. |
| `Parts` | Each impact's parts at each level. |
| `Needs` | What each impact needs. |
| `Duration`, `Ticks`, `Interval`, `Chance` | The effect's duration, ticks, interval and chance. |
| `Contained` | The effect's contained effects. |
| `Describe(level)` | What the effect does at a level, an impact to a line: "-15 Health each tick, 12 ticks over 12 s". |

`NumberTrace.Of(number, context)` explains any one number the same way, each part with what it read and what it gave:

```
Health as it lands, level 2:
-1 × (Caster's Attack Damage + 80 if Caster's Combo Charges > 0, else 0) = -100
  Group = 100
    Add: Caster's Attack Damage = 100
    Add: Conditional = 0
      When: Caster's Combo Charges > 0: doesn't hold
      False: 0 = 0
```

A read given no caster or no target gives, for a part that reads one, what its field falls back to. It says what it
needs, as "the target".

A kind or a condition of your own adds its parts by overriding `Explain`: `trace.Add(Min, context, this, "Min")`.

## Cues

What the effect shows and sounds, apart from what it changes, plays from its **Cues** component. Add it from the
Components list's **+**. An effect holds one at most.

The component has a list for each of the effect's moments:

| Moment | When its cues play |
|---|---|
| **On Apply** | As the effect lands. |
| **On Reapply** | As an application it takes in lands, as a stack or a refresh. |
| **On Tick** | As each tick executes. |
| **On Hit** | As an impact it deals lands, on the one it lands on. Its magnitude is the value it changed, as for a damage number. |
| **On Remove** | As a lasting effect comes off. |
| **While Active** | While it lasts: from as it lands, or a load puts it back, to as it comes off. Where another application replaced it, it ends as it began. |
| **On Refused** | As an application is refused, as by an immunity, on the one it was aimed at. |
| **On Stack Change** | As its stacks change while it lasts. Its magnitude is the stacks it has. |

A cue plays at each moment whose list holds it. A sound on each tick is a Sound in On Tick, and one as the effect lands
as well is in On Apply too.

An applier can say where an application hit, `spec.WithHit(point, normal, magnitude)`, and its cues play there
([CUES.md](CUES.md#raises-and-sinks)).

A save's load ends each While Active cue and begins it again as the effect leaves and comes back, and plays no burst.

[CUES.md](CUES.md) brings cues together: where each is raised, the library's routes, what each tag plays, and pooling.

### Cue kinds

A cue's kind decides what it plays. Each list holds its cues as the Workers list holds workers, each cue's kind picked
from the list.

| Kind | What it plays |
|---|---|
| **Camera Shake** | Shakes the camera through the shaker the game gives `ForgeCues.Shaker`, an `ICameraShaker`. With none given, it shakes nothing. |
| **Sound** | Plays its clip where its **Placement** says, at the target by default ([CUES.md](CUES.md#placement)). |
| **Prefab** | Places its prefab where its **Placement** says, at the target by default, for **Lifetime** seconds. |
| **Floating Text** | Floats a text where its **Placement** says, as a hit's damage or a status's name ([CUES.md](CUES.md#floating-text)). |
| **Process** | Runs a MonoBehaviour process where its **Placement** says, on a data packet loaded for the cue, as a shield's while it lasts ([CUES.md](CUES.md#data-packets)). |
| **Sequence** | Runs a task sequence on a data packet loaded for the cue, as a flicker while the effect lasts ([CUES.md](CUES.md#handlers)). |
| **Cue Tag** | Plays what its tag routes to ([CUES.md](CUES.md#routes)). |

A Camera Shake's **Strength** and **Duration** are number fields read for its effect as it plays, as are Prefab's
Lifetime and Sound's Volume.

A Prefab cue has more to it:

- In While Active, it stays for as long as the effect lasts, following the target.
- A cue raised at a point, as a Play Cue task's, places it there.
- With **Pooled**, it takes its objects from a pool, `ForgeCuePool`, and gives them back.
- The pool makes **Preallocate** of them ahead as it first plays.
- An `IPooledCue` on the prefab resets it as it's taken and given back.

Write a cue of your own by deriving from `AbstractCueHandler` and overriding the moments it plays at. Or override
`Burst` to play every burst one way, reading the moment that raised it from `cue.Moment`.

### Playing cues from code

Every cue plays through `ForgeCues`, an effect's as any other's.

A game's own code raises one there too, as sparks where a ray hit:
`ForgeCues.Burst(sparks, new CueContext(entity, ECueMoment.Raised, point: hit.point))`.

A cue's context says what raised it, who's behind it and where it plays. For a lasting cue, it also holds the key its
beginning and its end share.

- `ForgeCues.Enabled = false` silences every cue, as on a server or while a simulation runs ahead.
- `ForgeCues.Mute(entity, true)` silences one entity's, as one off screen.
- A lasting cue that began still ends.
- `ForgeCues.Played` hears each cue as it plays, for a debugger or a recorder.

### Cue Tag routing

A **Cue Tag** cue plays what its tag routes to, in place of naming what it shows. So an effect, an ability and an entity
can each raise Cue.Hit.Fire, and one place says how it looks.

- The project's **Cue Library**, an asset its settings name, plays the route for the tag, or else for its nearest
  parent.
- A route that doesn't **Override** plays its parent's after it.
- An entity's **Cue Receiver** component plays a tag its own way first, in place of the library's, or before it with
  **Pass Through**.
- The receiver's **Variant** plays the library's route under its name, as Cue.Hit.Golem for Cue.Hit, where the library
  has one.

## Requirements

The Requirements section decides whether the effect lands, keeps working and comes off. It holds the effect's chance to
apply, its tag checks on the target and the source, and its conditions.

| Property | Description |
|---|---|
| **Chance to Apply** | How likely the effect is to land, from 0 to 1, which can scale by level. See [Chance to Apply](#chance-to-apply). |
| **Chance Roll** | How the chance rolls. It shows for a chance that can miss. |
| **On the target**, **On the source** | A switch between the two sides' tag tables. See [Tag requirements](#tag-requirements). |
| **Apply Conditions**, **Ongoing Conditions**, **Removal Conditions** | Conditions for each phase. See [Conditions](#conditions). |

### Chance to Apply

**Chance to Apply** comes first: how likely the effect is to land, from 0 to 1, which can scale by level. It gates the
whole application, as the Applying rows do.

- Each application draws once from PlayForge's random source (`ForgeRandom`).
- A miss is refused, so applying returns false, as an immunity's refusal does.
- The target's callbacks hear a miss, `OnEffectRefused`, with why ([ENTITIES.md](ENTITIES.md#effect-journal)).
- 1, the default, always lands, and 0 never does. Neither draws, so an effect without a chance leaves the numbers others
  draw as they were.
- A contained effect draws its own: a strike with a bleed at 0.3 lands every time, and its bleed about one time in
  three.
- The section's summary shows a chance short of 1.

#### Chance Roll

**Chance Roll**, under it, says how the chance rolls, as every chance PlayForge rolls can. It shows for a chance that
can miss.

| Property | Description |
|---|---|
| **Pseudo Random** | Each miss raises the next roll's odds by a constant, and a hit starts them again. So streaks even out, and the chance over many rolls stays the same. |
| **Own Luck** | Uses a number of the chance's own, its **Luck**, in place of the luck attribute. |
| **Luck** | Shown under Own Luck alone. The chance's own luck. |

- Pseudo Random works as Dota's pseudo-random distribution: a 25% chance's first roll has about 8.5%, its next about
  17%, and so on.
- With Pseudo Random, each caster keeps its own streak for each chance.
- Without Own Luck, the luck is the attribute playing the project's Luck role ([ATTRIBUTES.md](ATTRIBUTES.md#roles)),
  read on the caster.
- Above 0, luck rolls that many more times and keeps the best. Below 0, it keeps the worst.

A chance of 1 or more always comes up, and one of 0 or less never does. Neither draws.

The same roll is on a trigger's Chance, the Chance stage of a Value Pipeline, an ability's Chance to Activate, and the
**Chance** condition.

The **Chance** condition holds on a roll of its number, as a critical strike chance read from an attribute. In a hit it
rolls once and keeps the roll, so a hit read twice, its preview and its commit, reads the same.

### Tag requirements

The tag requirements check tags on the effect's target, and on its source, the entity behind its origin.

The switch shows one side's table at a time: **On the target** first, then **On the source**. Each table has a row for
each group:

| Row | What it does |
|---|---|
| **Applying** | Must pass for the effect to be applied. The effect is also refused if its Removed By row is already met. |
| **While on** | While it fails, the effect stays, but its ticks don't execute. Its Continuous modifiers stay in place. |
| **Removed by** | When it's met, the effect is removed. |

Each row has two cells:

| Cell | Description |
|---|---|
| **Requires** | The tags the entity must have. |
| **Must not have** | The tags it must not have, or None. |

A row passes when the entity has every tag it requires and none it must not have.

#### Editing a row

Click a cell to edit its list under the table, with the row's template settings:

| Property | Description |
|---|---|
| **Template** | A shared Requirement Template for the row. |
| **Sync With Template** | Keeps the row's tags the same as its template's. |

A template's **Category** is a tag of its own context, Requirement Category, which groups templates.

Each entry is a tag query: a **Tag**, a **Match** mode and a comparison with a number. Greater Than 0 means
"the tag is present".

#### When rows are checked

- Empty Applying and While On rows always pass.
- An empty Removed By row never removes anything.
- While On and Removed By are checked whenever an effect is applied to or removed from the target.
- While On is also checked before each tick.

### Conditions

Under the tag tables are the effect's **Conditions** for each phase. Each is a condition read in the effect's context,
its caster's and its target's numbers in reach, as "the target below 30% Health".

| Property | Description |
|---|---|
| **Apply Conditions** | Must each hold for the effect to land. |
| **Ongoing Conditions** | Pause the effect while one fails, as While On does. |
| **Removal Conditions** | Take the effect off once each holds, with its Removed By rows where it names any, as Attribute Shift stops at its floor. |

- A phase passes only when its tags pass and its conditions all hold.
- The Ongoing and Removal conditions are checked at the entity's end of frame while the effect lasts, as no tag needs to
  change for an attribute they read to.
- They are checked before each tick as well.

## Components

An effect's components are features it carries. Each is a class with hooks into the effect's life.

Add a component with the Components list's **+**. A kind that makes no sense twice is offered only while the effect
holds none of it.

| Component | What it does |
|---|---|
| [Aura](#auras) | Holds an effect on those near the one it's on, while it lasts. |
| [Area](#area) | Lands the effect on those near its target as it lands. |
| [Transfer](#transfers) | Moves an amount from one attribute to another. |
| [Side Change](#side-change) | Changes the target's sides while the effect lasts. |
| [Spread](#spread) | Spreads the effect from the one it's on to those near them. |
| [Cues](#cues) | Shows and sounds the effect's moments. |

### Auras

An **Aura** component holds an effect on those near the one it's on, while it lasts.

| Property | Description |
|---|---|
| **Effect** | The effect it holds. |
| **Radius** | How far from its holder it reaches, read at each check. |
| **Flat** | Measures along the ground. Off, it measures in a sphere. |
| **Takes** | Whom it takes, by how each stands to its holder. Its **Allies**, itself among them, to start. |
| **Interval** | How often it checks who's near, by its holder's own time. 0.25 s by default. |

- It puts its effect on each that comes near, from an origin of its own.
- It takes the effect off as they leave, die or end, as its holder dies or ends, or as the aura goes.
- An effect of its that ends on its own, a durational one, goes on again at its next check. An Infinite effect holds as
  long as they're near.
- Two of an item hold two, as their effect's stacking says.
- A purge counts an aura's effect Lent.

Items, abilities and entities have an Aura component too, each its own kind. Each gives its aura in its own hooks:

- an effect's as it lends (see [Component hooks](#component-hooks));
- an ability's as it's granted;
- an item's as it's equipped;
- an entity's as it registers.

An entity's auras are its `AuraSet`, `gas.GetService<AuraSet>()`, a service it steps
([ENTITIES.md](ENTITIES.md#services)). `ForgeAuras.Provide(entry, holder)` and `Remove` give one of your own.

### Area

An **Area** component lands the effect on those near its target as it lands, as Storm Hammer stuns everyone near the one
it hits.

| Property | Description |
|---|---|
| **Radius** | How far from the target it reaches, read as it lands. A Radius of 0 is no area. |
| **Flat** | Measures along the ground. Off, it measures in a sphere. |
| **Takes** | Whom it takes, by how each stands to its caster. |
| **Max Targets** | The most it takes besides the target, the nearest first. At 0, it takes every one. |
| **Use Resolve Order** | Lands on its target and those it takes in the project's resolve order. On by default. |

**Takes** chooses among its **Enemies**, its **Allies** (itself among them), its **Other
Allies**, **Self**, **All But Self** or **Everyone**. A caster with no sides takes everyone as its allies and enemies.

- Each is found by its position, with no collider.
- Each takes the effect from the same origin, so its modifiers and its chance to apply are its own.
- An effect that lands by an area spreads no further.
- It lands on its target and those it takes in the project's resolve order, its target among them, unless its
  **Use Resolve Order** is off (see [Resolve Order](#resolve-order)).
- An effect holds one at most. Without one, it lands on its target alone.

### Transfers

A **Transfer** component moves an amount from one attribute to another, as Essence Shift steals Agility or Attribute
Shift moves Agility into Strength.

| Property | Description |
|---|---|
| **Delivery** | When it moves. |
| **From** | Who gives: the effect's caster or its target. |
| **From Attribute** | The attribute it gives. |
| **To** | Who gets: the effect's caster or its target. |
| **To Attribute** | The attribute it gets. |
| **Slot** | Which values move: Current, as a pool's Mana; Base; or Current And Base, as a stat's Strength. |
| **Amount** | How much it moves. It can scale by level. |

| Delivery | When it moves |
|---|---|
| **On Apply** | Once, as the effect lands, for good. |
| **Periodic** | Each tick, for good. |
| **Continuous** | As the effect lands, and holds while it lasts, a modifier on each side. It gives back as the effect ends, as a load or a move takes it, or as the entity it's on goes. |

- Each side is its own, so an entity can give to itself.
- Only what the giver has moves, so one with 3 of a steal of 5 gives 3, and nothing is made.
- A Continuous transfer is read again only where a stack or a level changes its amount.
- Each side's change is exact: no worker takes it for a hit, no barrier soaks it and nothing scales it.
- So the getter gets what the giver gave, as far as its own bounds let it. What its maximum can't hold is lost, as an
  overheal is.
- An effect holds as many as it moves, each its own.

The Problems view warns of a transfer whose Delivery the effect's duration can't make, as it does an additional
impact's. It also warns of one that names no attribute to give or get. Each is numbered among the effect's transfers.

### Side Change

A **Side Change** component changes the target's sides while the effect lasts.

| Property | Description |
|---|---|
| **Source** | Whose sides it gives:<br>Caster: its caster's, as they are as it lands, as a mind control.<br>Named: the **Sides** it names. |
| **Sides** | Shown under Named alone. The sides it gives, as a pacify's Neutral. |
| **Priority** | Of several on one entity, the highest holds, read as it lands. A tie goes to the latest. |
| **Interrupts** | Breaks what the target runs as the change lands or ends. |

- As the last one ends, the entity's own sides come back.
- Its tags follow, so whom effects and areas take changes with it.
- **Interrupts** acts where it changes the sides, not where a change of higher priority keeps them.
- An Instant effect doesn't last, so it changes nothing.
- An effect holds one at most. It was a field of the effect's own, Side Change in its Tags section, with a switch.

### Spread

A **Spread** component spreads its effect, or the **Effect** it names, from the one it's on to those near them, as a
plague jumps from its victim to the enemies near it.

| Property | Description |
|---|---|
| **When** | The moments it spreads at, any of them:<br>**Land**: as the effect lands.<br>**Tick**: each tick.<br>**End**: as it comes off, however it ends.<br>**Death**: as the one it's on dies. |
| **Effect** | The effect it spreads. With none, it spreads its own effect. |
| **Radius** | How far from the one it's on it reaches, read at the effect's level. |
| **Flat** | Measures along the ground. Off, it measures in a sphere. |
| **Takes** | Whom it takes, by how each stands to its caster, as an Area's. Its Enemies to start. |
| **Max Targets** | The most it takes each time, the nearest first. At 0, it takes every one. |
| **Hops** | How far a landing it made spreads on. |
| **Skip Holders** | Passes over those who have the effect it spreads already. On by default. |
| **Use Resolve Order** | Lands on those it takes in the project's resolve order. On by default. |

- Each takes the effect from the same origin, its caster's, so its modifiers and its chance to land are its own.
- A landing it made spreads on only so far as its **Hops** allow. At 0, it spreads only from where the effect landed of
  its own.
- At 1, it spreads once more from each it spread to, and so on.
- An effect holds as many as it has moments to spread at, each its own.

### Component hooks

A component hears its effect's moments through hooks, at the moments its workers do.

| Hook | When the component hears it |
|---|---|
| **Land** | As the effect lands on the entity it's on. |
| **Tick** | Each period, as a tick executes. |
| **Hit** | As an impact the effect deals lands. |
| **End** | As it comes off: an instant effect at once, a lasting one as it's removed. |
| **Death** | As the one it's on dies while it's on them, whether the entity stays or ends, before what the death ends comes off. |
| **Lend** | A lasting effect only: as it starts to last, to lend what it gives while it lasts. |
| **TakeBack** | A lasting effect only: as it stops lasting. |
| **Reapply** | As an application it takes in lands, as a stack or a refresh. |
| **Refused** | As an application of it is refused, with why, on the one it was aimed at. |
| **StacksChanged** | As its stacks change while it lasts, with the counts before and after. |

A lasting effect's components hear **Lend** as it starts to last:

- as it lands, before its Land;
- as it takes another's place under its tag;
- as a load or a move puts it back, where it doesn't land again.

They hear **TakeBack** as it stops:

- as it comes off, before its End;
- as another takes its place;
- as a load clears it or a move takes it away.

An Aura lends its aura so. A component hears a tick only as one executes: one its ongoing condition stops isn't heard.

### Custom components

A component of your own derives from `AbstractEffectComponent` and overrides the hooks it needs. Each hook is a no-op
otherwise.

A component is the effect's, shared by everything that holds it. What it keeps for one application of the effect, it
keeps through its context: `ctx.GetState<T>(this)`.

**Describe** says what it does in one line with its numbers, as an Area's "Lands on enemies within 300". The Components
section's line and The Forge's summary say it beside its name.

## Workers

The Workers section holds what runs for the effect beside its impacts. It also covers the project's resolve order.

| Part | What it does |
|---|---|
| [Effect workers](#effect-workers) | React to this effect's own events. |
| [Worker Group](#worker-group) | Gives the one the effect is on more workers while it lasts. |
| [On Hit](#on-hit) | Holds the effects that the hits of the one this effect is on apply while it lasts. |
| [Modifiers](#modifiers) | Change other effects as they're made. |
| [Resolve Order](#resolve-order) | The order an area's effects land on those it takes. |

### Effect workers

Effect workers react to this effect's own events.

| Event | When the worker can act |
|---|---|
| Application | When the effect is applied. |
| Tick | When it ticks. |
| Removal | When it is removed. |
| Impact | When it deals a change to an attribute. |

- [WORKERS.md](WORKERS.md) maps each, and on whose side it runs.
- Workers return actions, which run at the end of the frame.
- An Instant effect runs its apply and remove workers within its one application.

A worker can also answer numbers. A magnitude of the Worker kind asks the effect's workers for a value under a key, and
one that implements `INumberWorker` answers.

For example, the **Attribute Change** worker answers how much health the target has lost since the effect began.

What a worker keeps for one application of its effect goes in `ctx.GetState<T>(this)`, since the worker itself is shared
by every application ([SCALERS.md](SCALERS.md#asking-a-worker)).

### Worker Group

**Worker Group**, after the effect workers, gives the one the effect is on more workers while it lasts, as an item's
group does while it's equipped.

| Property | Description |
|---|---|
| **Attribute Workers** | Run before and after an attribute changes, as a debuff that changes how its target takes damage. |
| **Impact Workers** | Run on each impact the entity deals. |
| **Tag Workers** | Run as the entity's tags change. |
| **Analysis Workers** | Look over the frame's gathered data at its end. |

The impact, tag and analysis lists serve rules such as Ice Blast's debuff, which shatters its target, by a Threshold, as
its Health falls below 13%.

- The effect takes its workers back as it goes.
- Two of the effect on one target give the group once, and the last of them to go takes it back.
- An Instant effect doesn't last, so its group does nothing.

### On Hit

**On Hit**, above the effect workers, holds the effects that the hits of the one this effect is on apply while it lasts,
as a poisoned blade or a boon's Weak.

Its entries are an item's ([ITEMS.md](ITEMS.md#on-hit)):

| Property | Description |
|---|---|
| **Attribute** | The attribute a hit lowers, as Health. |
| **Effects** | The effects it applies. |
| **To** | Whom they land on: the one hit or the holder. |
| **Only On Kill** | Applies them only on a kill. |
| **Impact Types** | Only hits of these impact types count. |
| **When** | A condition that must hold. |

- An Instant effect doesn't last, so its On Hit does nothing.
- Another effect that replaces it under its tag takes it off, and a load puts it back once.

#### Proc Coefficient

**Proc Coefficient**, in Impact, is what the effect's own hits carry for On Hit effects.

| Property | Description |
|---|---|
| **Own Proc Coefficient** | Uses the effect's own Proc Coefficient. Off, its ability's stands, or else 1, and Proc Coefficient is hidden. |
| **Proc Coefficient** | What the effect's hits carry. |

On Hit effects scale their chance and their duration by it. A damage over time at 0.25 sets them off a quarter as often,
for a quarter as long, and 0 sets nothing off.

#### Lifesteal

An effect has no lifesteal of its own. What its hits heal its caster is the caster's **Lifesteal**.

- Lifesteal is the attribute the Lifesteal worker names as its Share, which the worker heals by
  ([WORKERS.md](WORKERS.md#lifesteal)).
- The worker is the one the project gives every entity, or the caster's own.
- For hits that steal more, an effect raises its caster's Lifesteal while they should.
- For example, a lasting effect of +0.3 Lifesteal whose **Removal** condition takes it off, as a charged attack's buff
  comes off as its last charge is spent.
- The heal is a gain like any other, so Healing Received scales it.
- Damage a return sends back heals nothing.

### Modifiers

**Modifiers**, under On Hit, change other effects as they're made, for that application alone.

For example: a boon that adds a burst to every Chill its holder applies, a ring that makes its holder's debuffs last
longer, or a cloak that halves the stuns landing on its wearer.

An effect's modifiers hold on the one it's on while it lasts. An item's, an ability's and an entity's modifiers are the
same entries.

Each entry is a rule of its own, or names a shared **Effect Modifier** asset (**Create → PlayForge → Effect Modifier**),
whose rule it uses in its place. A rule has:

| Property | Description |
|---|---|
| **Direction** | **Outgoing**: its holder's own effects as it applies them, on another or itself.<br>**Incoming**: the effects landing on its holder, whoever applies them.<br>**Both**. |
| **Effect Tags** | The effects it changes: each with one of these among its asset tag and context tags, or a child of one. None picks every effect. |
| **When** | A condition read as the effect is made, its caster the caster and its target the target. |
| **Changes** | What it changes, each change of a kind. |

With **Effect Tags**, `Status.Chill` picks every Chill.

| Change | What it does |
|---|---|
| **Add Contained Effect** | An effect that lands as this one lands, each tick, or as it ends. It lands on the target or, with **Lands On** set to Caster, on its caster. |
| **Change Impact** | A Value Pipeline stage its impacts on an attribute go through, of some impact types where it names any. |
| **Add Overflow Effect** | An effect that lands as an application would take it past its most stacks, on the target or its caster as its **Lands On** says. **Clear Stacks** takes its stacks off with it. |
| **Change Duration** | Its time multiplied, then more added. Its ticks spread over the time it has. |
| **Change Stacks** | More it can hold at most, where a stack bound of its own sets a most, and more a landing gives. |
| **Change Chance to Apply** | Its chance multiplied, then added to. |
| **Grant Tags** | More tags it grants while it's on. |

- Under Change Impact, an impact that commits, a hit or a tick, runs the stage after its own pipeline, and its breakdown
  keeps it.
- A lasting impact, which no pipeline runs on, takes the stage on its magnitude, as Weak's −30% made −40%.
- Add Overflow Effect needs a stack bound of the effect's own to overflow.
- An effect a rule adds, and what that effect contains, isn't changed by that rule again. So a Chill that adds a Chill
  adds one.
- Two of an item modify twice.

### Resolve Order

The project's **Resolve Order**, under Forge Project in Project Settings → PlayForge, decides the order an area's
effects land on those it takes. With it, specific behaviour can be coded, as a totem whose aura keeps its allies alive
resolving after them.

| Property | Description |
|---|---|
| **Tag** | One carrying the tag, or one under it, resolves at the entry's Order. |
| **Order** | Lower resolves first. One carrying none of the entries' tags is at 0. |

- One carrying several tags takes the first entry it matches, so the list's order is its precedence.
- An entity carries a tag on its identity, as a totem always does, or while something grants it, as an aura can grant
  its holder one while it holds.
- The area's own order breaks ties.

These land in the resolve order:

- Apply Effects and Apply/Remove Effects land on an ability's targets in it, though the targeting keeps its own order
  and its primary target.
- An effect's **Area** component lands on its target and those it takes in it, its target among them.
- An aura puts its effect on those that came near in it.

The tasks' and the Area's **Use Resolve Order**, on by default, turns it off for that one. Code reads it through
`ForgeResolveOrder`: `OrderOf(target)`, `Sort(targets)`, and `TargetsOf(data)` for an activation's targets in order.

A death waits for its entity's end of frame, and an aura comes off a dead holder at its next check or as the holder
ends. So one area's hits resolve together: a blow that kills the totem doesn't take its aura off the hero the same
explosion strikes next.

The order matters for what changes as each lands, as an effect that lands on the caster with each hit, or a transfer.

## Local Data

Local Data is a list of named values stored on the asset.

| Property | Description |
|---|---|
| **Key** | A tag that names the row. |
| Value | A value of a kind: Float, Int, String, Bool, Tag, Attribute, Effect, Ability, Entity, Object, MonoBehaviour or ScriptableObject, or a kind of your own. |

- A kind of your own is a `[Serializable]` class made from `LocalDataValue<T>`, as a point or a colour. The row's kinds
  offer it beside PlayForge's.
- A row saves its own value alone.
- Object fields take project assets, not scene objects.

Code reads a value by its type, `data.TryGet(out float range)` or `data.TryGet(out MyPoint point)`. It reads a number, a
Float or an Int, which can scale, with `data.Value.TryReadNumber(context, out float number)`.

PlayForge doesn't read an effect's Local Data itself. Your code and workers can, with
`effect.TryGetLocalData(key, out var data)`. The first row with that key wins.

## Used By

Used By lists the PlayForge assets that refer to this effect, each with its type icon, such as an ability whose cost it
is or an item that grants it.

Click a row to select the asset. The list follows the project as it changes.
