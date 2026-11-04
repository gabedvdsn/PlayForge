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
- **Context Tags** describe the effect, such as `Fire`. Each change the effect makes carries these and its origin's
  context tags, and workers can filter by them. They are not applied to the entity.

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
  the Audit Effect Impact Deliveries menu item flags.
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
  Min and a Max.
- **Stack Amount**, with its scaler: the stacks each application adds. 0 counts as 1.

A Continuous change that reads the stack count updates whenever the count changes.

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
- **Magnitude**: the amount, with **Real Magnitude**, how Magnitude and the scaler combine, and an optional
  **Scaler**, such as a per-level table read at the origin's level. Add Scaler (default) adds them, Multiply With
  Scaler multiplies them, Use Magnitude ignores the scaler, and Use Scaler ignores Magnitude. With no scaler, the
  scaler counts as 0. A Continuous modifier works its magnitude out again whenever the attribute is recalculated. A
  commit works it out once.
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
commit never does, so the flag changes nothing. The Audit Effect Impact Deliveries menu item still flags a periodic
effect that sets it.

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

Other effects this one applies to the same target, with the same origin. Each row pairs a moment with an effect: On
Apply (when this effect is applied), On Tick (on each execute tick) or On Remove (when this effect is removed). An
Instant effect has no ticks and no removal, so only its On Apply rows run. An effect that is asked to apply again while
it is still applying is refused, with a warning, so an effect that contains itself doesn't loop forever.

### Retention Group

The group this effect's changes count toward (default `Ignore`), unless an impact names its own. Groups split an
attribute's value for queries and UI, such as base and bonus health. They never change the math.

## Requirements

Tag checks against the effect's target, and against its source, the entity behind its origin. The switch at the top
shows one side's table at a time: **On the target** first, then **On the source**. Each table has a row for each
group:

- **Applying**: must pass for the effect to be applied. The effect is also refused if its Removed By row is already
  met.
- **While on**: while it fails, the effect stays, but its ticks don't execute. Its Continuous modifiers stay in place.
- **Removed by**: when it's met, the effect is removed.

Each row has two cells. **Requires** lists the tags the entity must have, and **Must not have** the tags it must not
have, or says None. A row passes when the entity has every tag it requires and none it must not have. Click a cell to
edit its list under the table, with the row's **Template**, a shared Requirement Template, and **Sync With Template**,
which keeps the row's tags the same as its template's. Each entry is a tag query: a **Tag**, a **Match** mode and a
comparison with a number, where Greater Than 0 means "the tag is present".

Empty Applying and While On rows always pass. An empty Removed By row never removes anything. While On and Removed By
are checked whenever an effect is applied to or removed from the target. While On is also checked before each tick.

## Workers

Effect workers react to this effect's own events. A worker can act when the effect is applied, when it ticks, when it
is removed, and when it deals a change to an attribute. Workers return actions, which run at the end of the frame. An
Instant effect runs its apply and remove workers within its one application.

## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag), a **Type**, and a value of
that type: String, Int, Float, Bool, Tag, Attribute, Effect, Ability, Entity, Object, ScriptableObject or MonoBehaviour.
Object fields take project assets, not scene objects.

PlayForge doesn't read an effect's Local Data itself. Your code and workers can, with
`effect.TryGetLocalData(key, out var data)`. The first row with that key wins.

## Used By

The PlayForge assets that refer to this effect, each with its type icon, such as an ability whose cost it is or an
item that grants it. Click a row to select the asset. The list follows the project as it changes.
