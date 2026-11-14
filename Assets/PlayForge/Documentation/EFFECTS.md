# PlayForge — Gameplay Effects

A **Gameplay Effect** asset describes a change to an entity: damage, a heal, a timed buff, a poison, a cost or a
cooldown. An entity is anything that runs a gameplay ability system (GAS), such as the player or an enemy. Abilities
and items apply effects to a target entity, and so can your code. The ability, item or entity that applies an effect
is its *origin*. An effect can change attributes (numbers such as Health), grant tags, apply other effects and run
workers.

The duration's Policy decides how long an effect lives. An Instant effect makes its change once and is done. A
Durational or Infinite effect goes on the target's effect *shelf*, its list of active effects, and stays until its time
runs out or something removes it. The kind of change follows from that:

| Effect | Delivery | The change |
|---|---|---|
| Instant | OnApply | one permanent commit, such as "take 30 damage" |
| Durational or Infinite, no ticks | Continuous | a modifier while the effect is active, reversed exactly when it ends |
| Durational or Infinite, periodic ticks | Periodic | one permanent commit per tick, such as "5 Health per second" |

A commit stays after the effect ends. A modifier doesn't. [EFFECT_PIPELINE.md](EFFECT_PIPELINE.md) has the full model.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Fireball Damage gives `Effect.FireballDamage`. Each section below matches the inspector section of
the same name.

## Identity

- **Name**: the effect's display name. It also gives the asset tag, so renaming the effect changes its tag. Update
  anything that names the old tag.
- **Description**: free text for your UI.
- **Asset Tag**: read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. The
  tag is stored when you edit the Name. If the stored tag differs from the one the Name gives, it shows in warning
  text with an **Update** button that stores the new one.

  The effect system finds an effect by this tag when it is applied again or removed, whoever applied it. While a
  Durational or Infinite effect is active, its target carries the tag. Cooldowns work this way: an ability's Cooldown
  check looks for the cooldown effect's tag on the caster.
- **Visibility**: a tag your UI can read to decide whether to show the effect, such as `Always Visible` or
  `Not Visible`. The Forge lists it. The runtime doesn't act on it.
- **Textures**: icons, each paired with a tag. The texture tagged `Primary` is the effect's icon. Without one, the
  first texture is used.
- **Inherit From Origin** (FN-25): its icon is its origin's, the ability, item or entity that applied it: the origin's
  texture under **Origin Texture**, a tag, `Primary` to start, which shows under it. Where the origin has none under
  that tag, the effect's own stands. A spec reads it, `spec.GetIcon()`, as the Demo's bar and the Additional Content's
  status bar do; an origin holds textures through `IHasTextures`, as abilities, items, effects, entities and both hubs
  do.

## Leveling

An effect has no level of its own. At runtime it takes the level of its origin. Its scalers use that level to pick
per-level values, such as 20/30/40/50 damage at levels 1 to 4. A scaler is a value source, such as a table with one
value per level. A level provider is the asset whose level range an effect is authored for, normally its origin.

- **Link Mode**: Standalone (default): the effect takes the level of what applies it, as a note says. Linked To
  Provider takes the range from a level provider.
- **Level Provider**, while linked: the asset the range comes from, an Entity, Ability, Effect, Item or Attribute Set.
  Right-click the field to pick from one type. A link that would make a loop is refused, with an error in the Console.
  While no provider is set, a warning says the effect takes the level of what applies it. Starting Level and Max Level
  show the provider's range, read-only.
- **Link Contained Effects**, while linked, links every contained effect (see Impact) to this effect's provider, so
  their per-level values follow its range. **Unlink Contained Effects** removes the links that point at it. Each asks
  first, and one Undo takes it back.

The link serves the inspector: a scaler set to Lock To Level Provider shows one value per level of the provider. An
effect field in another inspector, such as an ability's Cost, says how the effect takes its levels, and its **Link**
links the effect to that asset.

## Tags

- **Granted Tags** are applied to the target while the effect is active. An Instant effect grants them only for the
  moment it applies.
- **Granted Abilities** are given to the target as the effect lands, each at the level of what applied it, and taken
  back as it comes off. One that's running then is interrupted. An ability the target can't take, such as one it
  knows already when duplicates aren't allowed, isn't lent, so the target keeps its own as the effect comes off. An
  Instant effect lends none, as it's never on the target.
- **Side Change** (TW-18), on, changes the target's sides while the effect lasts: to its caster's, as they are as it
  lands (**Sides From** Caster, a mind control), or to the **Sides** it names (Named, a pacify's Neutral). Of several on
  one entity, the highest **Priority** holds, a tie going to the latest, and as the last ends its own sides come back.
  Its tags follow, so whom effects and areas take changes with it. **Interrupts** breaks what the target runs as the
  change lands or ends, where it changes the sides, not where a change of higher priority keeps them.
- **Context Tags** describe the effect, such as `Fire`. Each change the effect makes carries these and its origin's
  context tags, and workers can filter by them. They are not applied to the entity.
- **Removes Effects Tagged** makes the effect a cleanse. As it lands, every other effect on the target tagged with one
  of these, or a child of one, by its asset tag, a context tag or a tag it grants, comes off, at the next tick, as a
  removed effect does. `Status.Debuff` takes off `Status.Debuff.Poison` and `Status.Debuff.Burn`, and `State.Burning`
  takes off whatever grants it.
- **Purges** makes the effect a purge, by the project's purge levels ([Purging](#purging)): as it lands, the effects on the
  target its strength reaches, of the kinds it names and applied by whom it says, come off.
- **Blocks Effects Tagged** makes the effect an immunity. While it's on the target and its ongoing requirements hold,
  an effect tagged with one of these, or a child of one, the same way, can't land, and applying it returns false. It
  leaves the effects already on the target; add the same tags to Removes Effects Tagged to clear them as it lands.

## Purging

A project's **Purge Levels**, under Forge Project in Project Settings → PlayForge, rank its purges (FN-16). Each **Tier**
is a tag, weakest first, as Purge.Basic, Purge.Strong; an effect is at the first tier whose tag it carries, as its asset
tag or a context tag, or a child of one. A purge of a tier takes the effects at that tier or under it, as a Strong
dispel takes the Basic too, and an effect at no tier is never purged, but by death (Decision 80).

A purge, an effect's **Purges** or `ForgePurge.Purge(entity, purge)` in code, has a **Strength**, a tier's tag; its
**Kinds**, tags an effect must carry one of to be taken, as Status.Debuff, or any where it names none; **Applied By**,
whom it takes the effects of, by who applied each against the one purged: itself, its allies (its buffs) or its
enemies (its debuffs), an applier sharing none of its sides an enemy; and **Filters** of a project's own, each an
`AbstractPurgeFilter` whose `Takes` says yes or no, as **Except Tagged** spares the effects carrying the tags it names.
An effect a purge takes comes off at the next tick, as a removed effect does, and a purge never takes the effect doing
it. `ForgePurge.Purge(entity, strength)` purges by a strength alone, of every kind.

A staying entity's death purges its effects too, all but those it keeps: each tier's **Kept On Death** keeps the
effects at it, and the table's **Kept On Death** categories keep theirs whatever their tiers, an ability's cooldowns,
lent effects and toggles as a project starts ([ENTITIES.md](ENTITIES.md#death)).

## Duration

The fields show as the Policy uses them.

- **Policy**: Instant (default) makes the change once. Durational stays for a set time. Infinite stays until
  something removes it.
- **Duration** (Durational): the effect's length in seconds, with its scaler.
- **Periodic Ticks** (Durational and Infinite): a tick makes the effect's change again, as a new commit.
  - **Ticks** (Durational): how many ticks fit in the duration, evenly spaced. The last falls as the duration ends, so
    5 ticks over 10 seconds tick every 2 seconds.
  - **Tick Every** (Infinite): the seconds between ticks.
- **Tick On Application**: run one tick as soon as the effect is applied, before the first periodic tick. It runs
  again when re-application refreshes or extends the effect. Use it with Periodic Ticks. On its own, it is a shape that
  the Problems view flags.
- **Additional Executions** (while it ticks): each tick executes the change a number of times, N plus this, combined
  with its scaler. N comes from the effect's container and is normally 1. A stacking container that shares one
  duration, or takes it from one stack, uses its stack count.
- **Real Delta Time** and **Delta Time Scaler** (Durational) change how fast the effect's clock runs. With no scaler,
  the clock follows Unity's frame time. With one, Real Delta Time combines the two. Multiply With Scaler, for example,
  slows the clock down or speeds it up.

A tick only executes while the While On requirements hold (see Requirements).

### Re-application (Durational and Infinite)

What happens when the effect is applied to a target that already has it. Each application on the shelf lives in a
*container*. The effect is matched by its asset tag, whoever applied it.

- **Re-application**: Add Another (default) adds a separate container, and both apply. Do Nothing adds nothing new.
  Replace Existing gives the old container the new application's values and modifiers, and keeps its stack count. Past
  commits stay. Stack Existing adds stacks to the old container (see Stacking).
- **Then** (all but Add Another) applies as the old container takes in the application. Do Nothing (default) leaves its
  time alone. Refresh Duration restarts it. Extend Duration adds the new duration to it. Refresh and Extend also run
  Tick On Application again.

### Stacking (Stack Existing only)

- **Stacking**: Stacks Share One Duration (default) runs one timer for all stacks, and adding a stack doesn't reset it.
  Stacks Have Independent Durations gives each stack its own timer and ticks. Stacks drop off one at a time, and the
  effect lasts as long as its longest stack. Duration Taken From One Stack runs one stack's timer at a time. Each time
  it runs out, one stack is lost and the timer restarts.
- **Stack Behaviours**: optional stacking rules, such as Stack Bounds Config, which keeps the stack count between a
  Min and a Max. An application that would take the effect past the Max overflows: its Overflow Effects land, from
  the same origin, as a bleed's fourth stack hemorrhages, on the target or, with **Overflow Lands On** set to Caster,
  on its caster, as a frenzy (TW-14); Deny Refresh On Overflow keeps that application from refreshing or extending the
  effect; and Clear Stacks On Overflow takes the effect's stacks off.
- **Stack Amount**, with its scaler: the stacks each application adds. 0 counts as 1.

A change reads the stack count through an **Effect Stacks** scaler ([SCALERS.md](SCALERS.md)): a magnitude of −5 with
Multiply With Scaler takes 5 a stack. A Continuous one updates whenever the count changes. A Periodic one lands once for
each stack already, so one that reads its stacks counts them twice, 3 stacks hitting 9 times as hard as 1: the Problems
view warns of it, and its quick fix, **Count Stacks Once**, gives the number one stack gives (TW-25).

### Moving an effect to another entity

`gas.GetEffectSystem().MoveTo(effect, other)` moves each of the effect's containers on the entity onto another, as a
possession hands a body's effects over. Each keeps its stacks, its time left and what applied it. A move is neither
a removal nor an application: none of either's workers, commits or contained effects run, and neither is told. Its
lasting changes, its granted tags, the abilities it lends and its lasting cues go with it.

## Impact

The impact is the change the effect makes to an attribute. The first fields are its first impact, whose delivery the
duration gives it. Additional Impacts change more attributes, or the same one again, each with a delivery of its own.

- **Attribute**: the attribute to change. Leave it empty for an effect that only grants tags, applies other effects or
  runs workers. A target that lacks the attribute isn't changed.
- **Operation**: how the impact changes the attribute, and which of its two values, in one choice, such as Add to
  Current or Multiply Base. **Base** is the value the attribute is measured against, and **Current** is the value in
  play. For Health, Base is the maximum and Current is what is left. Add adds the magnitude. Use a negative magnitude to
  subtract. Multiply multiplies by it. Override sets the value to it. Flat Bonus adds too, but as a modifier it lands
  after the attribute's multipliers, so they don't scale it.
- **Multiply Behaviour** (Multiply only): Multiplicative (default) compounds with other multipliers, so ×1.5 and ×1.5
  give ×2.25. Additive Bonus adds the bonuses first, so +50% and +50% give ×2.0.
- **Add Behaviour** (Add only, shown where it matters): as the attribute's role says, a Resistance's adds diminishing; a
  plain Sum; or Diminishing, on what's left ([ATTRIBUTES.md](ATTRIBUTES.md), TW-1).
- **Pipeline**: the calculation each hit of the impact goes through as it lands, a Value Pipeline: the caster's bonuses,
  the target's reductions, a critical roll, a barrier. It runs for an instant impact's hit and each tick of a periodic
  one ([VALUE_PIPELINES.md](VALUE_PIPELINES.md), TW-2).
- **Magnitude**: the amount: a Constant number, or by its kind a scaler's value at the origin's level, such as a By
  Level table of values per level, with Combine joining the scaler and the number ([SCALERS.md](SCALERS.md)). A
  Continuous modifier works its magnitude out again whenever the attribute is recalculated. A commit works it out
  once. A **Set By Caller** scaler reads a magnitude the code sets as it applies the effect, under a tag: on the spec
  with `SetByCaller(tag, magnitude)`, or on an ability activation's data, which each effect the ability applies takes
  as it's made, its cost, cooldown and contained effects among them. Only the tag itself matches, and where none is
  set it reads its Default. A magnitude can also read what the effect has dealt, a part of another effect, ability or
  item on either side, or what a worker answers; SCALERS.md lists each kind.
- **Delivery**, read-only: when the impact lands, which the duration gives it (see the table above). On applying,
  While the effect lasts, or Each tick, which adds ", and on applying" when Tick On Application is on. A note says when
  Tick On Application alone keeps a multiply or override as a modifier, so it reverses as the effect ends.
- **Commits**, read-only, for a periodic impact: how many times it lands at the effect's first level, as in "7 over
  6 s, level 1". For an Infinite effect, it says how far apart the ticks are.
- **Impact Types**: tags that label the kind of change, such as `Damage.Fire`. Workers can accept or skip changes by
  type. `Never Allow` hides the change from workers that check types. `Always Allow` passes every type check.
- **Affiliation** decides which targets the effect can be applied to. An entity's affiliation tags are its team or
  faction, set on its Entity asset. If the check fails, the effect isn't applied. Always Allow (default) allows any
  target. Affiliated allows targets that share the origin's affiliation, and Unaffiliated allows those that don't. Use
  Affiliation List compares the target with the **Affiliations** list instead.
  - **Comparison** (all but Always Allow): Any (default) needs one tag to match. All needs every tag to match.

The inspector no longer shows Reverse On Removal. A Continuous modifier always reverses when the effect ends, and a
commit never does, so the flag changes nothing. The Problems view still flags a periodic effect that sets it.

### Additional Impacts

Each additional impact changes an attribute of its own, or the same one again, with a delivery of its own. One effect
can lower Armor while it lasts and burn Health on each tick. **Add Impact** adds one with a delivery the duration can
make: Continuous on a Durational or Infinite effect, On Apply on an Instant one. While it's folded, an impact shows one
line, such as "Mana −10 · Continuous", and its trash button removes it. Each has:

- **Attribute**, **Operation**, **Multiply Behaviour** and **Magnitude**, as above.
- **Delivery**: Continuous lasts while the effect does, then reverses. On Apply lands once, as the effect is applied.
  Periodic lands on each tick, and as the effect is applied when Tick On Application is on. It offers what the duration
  can make: an Instant effect makes only On Apply impacts, and Periodic needs ticks. If the duration changes so that an
  impact's delivery can't happen, a warning under it says so, and the impact never applies.
- **Order** (Override only): among overrides of one attribute, the lowest order wins.
- **Retention Group**: the group the change counts toward. Empty, it takes the effect's.
- **Impact Types**, as above.

An impact with no Attribute changes nothing. The Cost check counts every attribute a cost spends (see
[ABILITIES.md](ABILITIES.md)).

### Contained Effects

Other effects this one applies, with the same origin. Each row pairs a moment with an effect: On Apply (when this effect
is applied), On Tick (on each execute tick) or On Remove (when this effect is removed); and says whom it lands on (TW-14):
the **Target**, this effect's, as it always did, or the **Caster**, as a lifesteal's heal or a curse's rebound as it ends.
An Instant effect has no ticks and no removal, so only its On Apply rows run. An effect that is asked to apply again while
it is still applying is refused, with a warning, so an effect that contains itself doesn't loop forever.

### Retention Group

The group this effect's changes count toward (default `Ignore`), unless an impact names its own. Groups split an
attribute's value for queries and UI, such as base and bonus health. They never change the math.

## Levels

What the effect does, level by level, read from the effect as it is now: a row for its duration, its ticks and its
chance where it has them, then one for each impact, its own and its contained effects', with its value at each level.
**Levels** sets the first and last level shown, ten at most. Select a cell to see the parts behind it, each with what it
read and what it gave:

```
Health as it lands, level 2:
-1 × (Caster's Attack Damage + 80 if Caster's Jingu Charges > 0, else 0) = -100
  Group = 100
    Add: Caster's Attack Damage = 100
    Add: Conditional = 0
      When: Caster's Jingu Charges > 0: doesn't hold
      False: 0 = 0
```

It reads with no caster and no target, so a part that reads one gives what its field falls back to, and its row is
marked `*` with what it needs, as "the target".

The section's line, which the effect's header and The Forge's Summary column show too, is its first impact by level, an
add signed, as "-10/-20/-30 Health" or "+20 Armor", at its provider's levels where it's linked to one and at levels 1 to
5 where it isn't (TW-21). `ForgeAssetLines.Of(asset)` gives any asset's line in the editor.

The same reading is code's, for a tooltip or a game's own screen: `AbilityInspector.BuildEffectSeries(effect, 1, 5,
caster, target)` reads the effect at levels 1 to 5 against a caster and a target, either of which may be null. Each
impact gives its `Magnitude` and `Total` at each level, its `Line`, as its field's summary reads it, its `Parts` at each
level, and what it `Needs`; the effect gives its `Duration`, `Ticks`, `Interval`, `Chance` and `Contained` effects, and
`Describe(level)` says what it does at a level, an impact to a line: "-15 Health each tick, 12 ticks over 12 s".
`NumberTrace.Of(number, context)` explains any one number the same way. A kind or a condition of your own adds its
parts by overriding `Explain`: `trace.Add(Min, context, this, "Min")`.

## Cues

What the effect shows and sounds, apart from what it changes, listed as its workers are. Pick each cue's kind from the
list:

- **Camera Shake**: shakes the camera as the effect lands, and on each tick with **On Each Tick**, through the shaker
  the game gives `ForgeCues.Shaker`, an `ICameraShaker`. With none given, it shakes nothing.
- **Sound**: plays its clip at the target as the effect lands, and on each tick with **On Each Tick**.
- **Prefab**: places its prefab at the target as the effect lands, for **Lifetime** seconds, or, with **While Active**,
  for as long as the effect lasts, following the target.

A cue plays its bursts as the effect lands, as its ticks execute and as it comes off, and a lasting cue, such as a
While Active prefab, while the effect is on. A re-application that an effect on already takes in plays its burst
again. A save's load ends each lasting cue and begins it again as the effect leaves and comes back, and plays no burst.
Write a cue of your own by deriving from `AbstractCueHandler` and overriding the moments it plays at.

## Requirements

**Chance to Apply** comes first: how likely the effect is to land, from 0 to 1, which can scale by level. It gates
the whole application, as the Applying rows do. Each application draws once from PlayForge's random source
(`ForgeRandom`), and a miss is refused, so applying returns false, as an immunity's refusal does. 1, the default,
always lands, and 0 never does; neither draws, so an effect without a chance leaves the numbers others draw as they
were. A contained effect draws its own: a strike with a bleed at 0.3 lands every time, and its bleed about one time
in three. The section's summary shows a chance short of 1.

**Chance Roll**, under it, says how the chance rolls, as every chance PlayForge rolls can (TW-8), and shows for a
chance that can miss (FN-23):

- **Pseudo Random**: each miss raises the next roll's odds by a constant and a hit starts them again, so streaks even
  out and the chance over many rolls stays the same, as Dota's pseudo-random distribution: a 25% chance's first roll
  has about 8.5%, its next about 17%, and so on. Each caster keeps its own streak for each chance.
- **Luck**: the attribute playing the project's Luck role ([ATTRIBUTES.md](ATTRIBUTES.md#roles)), read on the caster, or with
  **Own Luck** a number of the chance's own, its **Luck**, shown under Own Luck alone (FN-23): above 0 it rolls that
  many more times and keeps the best, below 0 the worst.

A chance of 1 or more always comes up and one of 0 or less never does, and neither draws. The same roll is on a
trigger's Chance, the Chance stage of a Value Pipeline, an ability's Chance to Activate, and the **Chance** condition,
which holds on a roll of its number, as a critical strike chance read from an attribute: in a hit it rolls once and keeps
the roll, so a hit read twice, its preview and its commit, reads the same.

Under it are tag checks against the effect's target, and against its source, the entity behind its origin. The
switch shows one side's table at a time: **On the target** first, then **On the source**. Each table has a row for
each group:

- **Applying**: must pass for the effect to be applied. The effect is also refused if its Removed By row is already
  met.
- **While on**: while it fails, the effect stays, but its ticks don't execute. Its Continuous modifiers stay in place.
- **Removed by**: when it's met, the effect is removed.

Each row has two cells. **Requires** lists the tags the entity must have, and **Must not have** the tags it must not
have, or says None. A row passes when the entity has every tag it requires and none it must not have. Click a cell to
edit its list under the table, with the row's **Template**, a shared Requirement Template, and **Sync With Template**,
which keeps the row's tags the same as its template's. A template's **Category** is a tag of its own context,
Requirement Category, which groups templates. Each entry is a tag query: a **Tag**, a **Match** mode and a
comparison with a number, where Greater Than 0 means "the tag is present".

Empty Applying and While On rows always pass. An empty Removed By row never removes anything. While On and Removed By
are checked whenever an effect is applied to or removed from the target. While On is also checked before each tick.

Under the tables are its **Conditions** for each phase (TW-19), each a condition read in the effect's context, its
caster's and its target's numbers in reach, as "the target below 30% Health". **Apply Conditions** must each hold for it
to land; **Ongoing Conditions** pause it while one fails, as While On does; and **Removal Conditions** take it off once
each holds, with its Removed By rows where it names any, as Attribute Shift stops at its floor. A phase passes only when
its tags pass and its conditions all hold. The Ongoing and Removal conditions are checked at the entity's end of frame
while the effect lasts, as no tag needs to change for an attribute they read to, and before each tick as well.

## Components

An effect's components are features it carries, each a class with hooks into its life, added with the list's **+**
(FN-14). A component hears the effect **Land** on the entity it's on, **Tick** each period, **Hit** as an impact it deals lands, and **End** as it comes off: an instant effect at once, a lasting one as it's removed, at the moments its workers do; and
**Death** as the one it's on dies while it's on them, whether the entity stays or ends, before what the death ends comes
off. A kind that makes no sense twice is offered only while the effect holds none of it.

A component of your own derives from `AbstractEffectComponent` and overrides the hooks it needs, each a no-op
otherwise. It's the effect's, shared by everything that holds it, so what it keeps for one application of it it keeps through its
context: `ctx.GetState<T>(this)`.

### Auras

An **Aura** component holds an effect on those near the one it's on, while it lasts (TW-10). Items, abilities and
entities have one too, each its own kind (FN-14). Each has the **Effect** it holds, a **Radius** from its holder, read at each check, **Flat**,
whom it **Takes** from its holder's sides, its holder too where **Include Holder** says, and an **Interval**, 0.25 s by
default, at which it checks who's near, by its holder's own time. It puts its effect on each that comes near, from an
origin of its own, and takes it off as they leave, die or end, as its holder dies or ends, or as the aura goes; an
effect of its that ends on its own, a durational one, goes on again at its next check. An Infinite effect holds as long
as they're near. Two of an item hold two, as their effect's stacking says.

### Area

An **Area** component lands the effect on those near its target as it lands (TW-10), as Storm Hammer stuns everyone near
the one it hits: each within its **Radius** of the target, read as it lands, along the ground unless **Flat** is off;
of the sides it **Takes** from its caster's (enemies, allies, or both; a caster with no sides takes anyone); its caster
too where **Include Caster** says; and at most **Max Targets** besides the target, the nearest first, or every one at 0.
Each is found by its position, with no collider. Each takes the effect from the same origin, so its modifiers and its
chance to apply are its own, and an effect that lands by an area spreads no further. A Radius of 0 is no area. It lands
on its target and those it takes in the project's resolve order, its target among them, unless its **Use Resolve
Order** is off (TW-31; see [Resolve Order](#resolve-order)). An effect holds one at most; without one, it lands on its
target alone (FN-14).

### Transfers

A **Transfer** component moves an amount from one attribute to another (TW-20), as Essence Shift
steals Agility or Attribute Shift moves Agility into Strength. **From**, the effect's caster or its target, gives its
**From Attribute**, and **To** gets its **To Attribute**, each side its own, so an entity can give to itself. **Slot**
says which values move: Current, as a pool's Mana; Base; or Current And Base, as a stat's Strength. **Amount** can scale
by level. Only what the giver has moves, so one with 3 of a steal of 5 gives 3, and nothing is made. **Delivery** says
when: **On Apply** moves once as the effect lands and **Periodic** each tick, both for good; **Continuous** moves as the
effect lands and holds while it lasts, a modifier on each side, read again only where a stack or a level changes its
amount, and gives back as the effect ends, as a load or a move takes it, or as the entity it's on goes. Each side's
change is exact: no worker takes it for a hit, no barrier soaks it and nothing scales it, so the getter gets what the
giver gave, as far as its own bounds let it; what its maximum can't hold is lost, as an overheal is. The Problems view
warns of a transfer whose Delivery the effect's duration can't make, as it does an additional impact's, and of one that
names no attribute to give or get, each numbered among the effect's transfers. An effect holds as many as it
moves, each its own (FN-14).

### Spread

A **Spread** component spreads its effect, or the **Effect** it names, from the one it's on to those near them, at the
moments its **When** names, any of them: as the effect **Land**s, each **Tick**, as it comes off (**End**), however it
ends, or as the one it's on dies (**Death**), as a plague jumps from its victim to the enemies near it (FN-14). It takes
each within its **Radius** of the one it's on, read at the effect's level, along the ground unless **Flat** is off; of
the sides it **Takes** from its caster's; its caster too where **Include Caster** says; at most **Max Targets** each
time, the nearest first, or every one at 0; and passes over those who have the effect it spreads already, unless **Skip
Holders** is off. Each takes the effect from the same origin, its caster's, so its modifiers and its chance to land
are its own, in the project's resolve order unless **Use Resolve Order** is off. A landing it made spreads on only so
far as its **Hops** allow: at 0, it spreads only from where the effect landed of its own; at 1, once more from each it
spread to; and on. An effect holds as many as it has moments to spread at, each its own.

## Workers

Effect workers react to this effect's own events. A worker can act when the effect is applied, when it ticks, when it
is removed, and when it deals a change to an attribute ([WORKERS.md](WORKERS.md) maps each, and on whose side it runs). Workers return actions, which run at the end of the frame. An
Instant effect runs its apply and remove workers within its one application.

A worker can also answer numbers: a magnitude of the Worker kind asks the effect's workers for a value under a key,
and one that implements `INumberWorker` answers, as the **Attribute Change** worker answers how much health the target
has lost since the effect began. What a worker keeps for one application of its effect goes in
`ctx.GetState<T>(this)`, since the worker itself is shared by every application
([SCALERS.md](SCALERS.md#asking-a-worker)).

**Worker Group**, after them, gives the one the effect is on more workers while it lasts, as an item's group does while
it's equipped (TW-13): attribute workers, as a debuff that changes how its target takes damage; impact, tag and
analysis workers, as Ice Blast's debuff shattering its target as its Health falls below 13% by a Threshold. They come
back as it goes. Two of the effect on one target give the group once, and the last of them to go takes it back. An
Instant effect doesn't last, so its group does nothing.

### On Hit

**On Hit**, above the workers, holds the effects the hits of the one this effect is on apply while it lasts: a
poisoned blade, a boon's Weak (TW-6). Its entries are an item's: an attribute, the effects, to the one hit or the
holder, only on a kill, impact types and a condition ([ITEMS.md](ITEMS.md#on-hit)). An Instant effect doesn't last, so
its On Hit does nothing. Another effect that replaces it under its tag takes it off, and a load puts it back once.

**Proc Coefficient**, in Impact, is what the effect's own hits carry for On Hit effects (TW-8): theirs scale their chance
and their duration by it, so a damage over time at 0.25 sets them off a quarter as often, for a quarter as long, and 0
sets nothing off. With **Own Proc Coefficient** off, its ability's stands, or else 1, and Proc Coefficient is hidden
(FN-23).

An effect has no lifesteal of its own (FN-22): what its hits heal its caster is the caster's **Lifesteal**, the attribute
playing the project's Lifesteal role, which the Lifesteal worker heals by, the one the project gives every entity or the
caster's own ([WORKERS.md](WORKERS.md#lifesteal)). For hits that steal more, an effect raises its caster's Lifesteal
while they should: a lasting effect of +0.3 Lifesteal whose **Removal** condition takes it off, as a charged attack's buff
comes off as its last charge is spent. The heal is a gain like any other, so Healing Received scales it, and damage a
return sends back heals nothing.

### Resolve Order

The project's **Resolve Order**, under Forge Project in Project Settings → PlayForge, decides the order an area's
effects land on those it takes (TW-31), so specific behaviour can be coded, as a totem whose aura keeps its allies alive
resolving after them. Each entry is a **Tag** and an **Order**: one carrying the tag, or one under it, resolves at its
Order, lower first, and one carrying none of the entries' tags at 0. One carrying several takes the first entry it
matches, so the list's order is its precedence. An entity carries a tag on its identity, as a totem always does, or
while something grants it, as an aura can grant its holder one while it holds. The area's own order breaks ties.

Apply Effects and Apply/Remove Effects land on an ability's targets in it, though the targeting keeps its own order and
its primary target; an effect's **Area** component lands on its target and those it takes in it, its target among them; and an
aura puts its effect on those that came near in it. The tasks' and the Area's **Use Resolve Order**, on by default,
turns it off for that one. Code reads it through `ForgeResolveOrder`: `OrderOf(target)`, `Sort(targets)`, and
`TargetsOf(data)` for an activation's targets in order.

A death waits for its entity's end of frame, and an aura comes off a dead holder at its next check or as the holder
ends, so one area's hits resolve together: a blow that kills the totem doesn't take its aura off the hero the same
explosion strikes next. The order matters for what changes as each lands, as an effect that lands on the caster with
each hit (TW-14) or a transfer (TW-20).

### Modifiers

**Modifiers**, under On Hit, change other effects as they're made, for that application alone (TW-7): a boon that adds
a burst to every Chill its holder applies, a ring that makes its holder's debuffs last longer, a cloak that halves the
stuns landing on its wearer. An effect's hold on the one it's on while it lasts; an item's, an ability's and an entity's
modifiers are the same entries. Each entry is a rule of its own, or names a shared **Effect Modifier** asset (**Create →
PlayForge → Effect Modifier**), whose rule it uses in its place.

A rule has:

- **Direction**: **Outgoing**, its holder's own effects as it applies them, on another or itself; **Incoming**, the
  effects landing on its holder, whoever applies them; or **Both**.
- **Effect Tags**: the effects it changes, each with one of these among its asset tag and context tags, or a child of
  one, so `Status.Chill` picks every Chill. None picks every effect.
- **When**: a condition read as the effect is made, its caster the caster and its target the target.
- **Changes**, each of a kind:
  - **Add Contained Effect**: an effect that lands as this one lands, each tick, or as it ends, on the target or, with
    **Lands On** set to Caster, on its caster (TW-14).
  - **Change Impact**: a Value Pipeline stage its impacts on an attribute go through, of some impact types where it
    names any. An impact that commits, a hit or a tick, runs it after its own pipeline, and its breakdown keeps it; a
    lasting impact, which no pipeline runs on, takes it on its magnitude, as Weak's −30% made −40%.
  - **Add Overflow Effect**: an effect that lands as an application would take it past its most stacks, on the target
    or its caster as its **Lands On** says, and **Clear Stacks**, which takes its stacks off with it. It needs a stack
    bound of the effect's own to overflow.
  - **Change Duration**: its time multiplied, then more added; its ticks spread over the time it has.
  - **Change Stacks**: more it can hold at most, where a stack bound of its own sets a most, and more a landing gives.
  - **Change Chance to Apply**: multiplied, then added to.
  - **Grant Tags**: more tags it grants while it's on.

An effect a rule adds, and what that effect contains, isn't changed by that rule again, so a Chill that adds a Chill
adds one. Two of an item modify twice.

## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag), a **Type**, and a value of
that type: String, Int, Float, Bool, Tag, Attribute, Effect, Ability, Entity, Object, ScriptableObject or MonoBehaviour.
Object fields take project assets, not scene objects.

PlayForge doesn't read an effect's Local Data itself. Your code and workers can, with
`effect.TryGetLocalData(key, out var data)`. The first row with that key wins.

## Used By

The PlayForge assets that refer to this effect, each with its type icon, such as an ability whose cost it is or an
item that grants it. Click a row to select the asset. The list follows the project as it changes.
