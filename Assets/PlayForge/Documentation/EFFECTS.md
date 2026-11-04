# PlayForge — Gameplay Effects

A **Gameplay Effect** asset describes a change to an entity: damage, a heal, a timed buff, a poison, a cost or a
cooldown. An entity is anything that runs a gameplay ability system (GAS), such as the player or an enemy. Abilities
and items apply effects to a target entity, and so can your code. The ability, item or entity that applies an effect
is its *origin*. An effect can change attributes (numbers such as Health), grant tags, apply other effects and run
workers.

The Duration Policy decides how long an effect lives. An Instant effect makes its change once and is done. A Durational
or Infinite effect goes on the target's effect *shelf*, its list of active effects, and stays until its time runs out or
something removes it. The kind of change follows from that:

| Effect | Delivery | The change |
|---|---|---|
| Instant | OnApply | one permanent commit, such as "take 30 damage" |
| Durational or Infinite, no ticks | Continuous | a modifier while the effect is active, reversed exactly when it ends |
| Durational or Infinite, periodic ticks | Periodic | one permanent commit per tick, such as "5 Health per second" |

A commit stays after the effect ends. A modifier doesn't. [EFFECT_PIPELINE.md](EFFECT_PIPELINE.md) has the full model.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Fireball Damage gives `Effect.FireballDamage`. Each section below matches the inspector section of
the same name.

## Definition

- **Name**: the effect's display name. It also gives the asset tag, so renaming the effect changes its tag. Update
  anything that names the old tag.
- **Description**: free text for your UI. The inspector header shows it.
- **Retention Group** (default `Ignore`): the group this effect's changes count toward. Groups split an attribute's
  value for queries and UI, such as base and bonus health. They never change the math.
- **Visibility**: a tag your UI can read to decide whether to show the effect, such as `Always Visible` or
  `Not Visible`. The Forge lists it. The runtime doesn't act on it.
- **Textures**: icons, each paired with a tag. The texture tagged `Primary` is the effect's icon. Without one, the
  first texture is used.

## Level Source

An effect has no level of its own. At runtime it takes the level of its origin. Its scalers use that level to pick
per-level values, such as 20/30/40/50 damage at levels 1 to 4. A scaler is a value source, such as a table with one
value per level.

A level provider is the asset whose level range an effect is authored for, normally its origin. Link the effect to it,
and a scaler set to Lock To Level Provider shows one value per level of the provider. The ability, item and entity
inspectors offer this link when you assign an effect to them.

- **Link Mode**: Standalone (default) or Linked To Provider.
- **Level Provider**: the linked asset, or No Provider Linked.
- **Link to**: pick a provider of one type: Entity, Ability, Effect, Item or Attribute Set. A link that would make a
  loop is refused, with an error in the Console. The status box below shows the provider's type, name and range, with
  **Go to Provider** and **Unlink** buttons.
- **Child Assets**: **Link Contained Effects** links every contained effect (see Impact Specification) to this effect's
  level provider. **Unlink Contained Effects** removes those links.

## Tags

- The **asset tag** shows at the top, read-only. It is made from the Name: punctuation is dropped, and each word is
  capitalised and joined. The tag is stored when you edit the Name. If the stored tag differs from the one the Name
  gives, it shows in amber with an **Update** button that stores the new one.

  The effect system finds an effect by this tag when it is applied again or removed, whoever applied it. While a
  Durational or Infinite effect is active, its target carries the tag. Cooldowns work this way: an ability's Cooldown
  rule looks for the cooldown effect's tag on the caster.
- **Context Tags** describe the effect, such as `Fire`. Each change the effect makes carries these and its origin's
  context tags, and workers can filter by them. They are not applied to the entity.
- **Granted Tags** are applied to the target while the effect is active. An Instant effect grants them only for the
  moment it applies.

## Impact Specification

The impact is the change the effect makes to an attribute. The fields below are its first impact, whose delivery the
duration gives it. Additional Impacts change more attributes, or the same one again, each with a delivery of its own.

### Target

- **Attribute**: the attribute to change. Leave it empty for an effect that only grants tags, applies other effects or
  runs workers. A target that lacks the attribute isn't changed.
- **Operation**: how the impact changes the attribute, and which of its two values, in one choice, such as Add to
  Current or Multiply Base. **Base** is the value the attribute is measured against, and **Current** is the value in
  play. For Health, Base is the maximum and Current is what is left. Add adds the magnitude. Use a negative magnitude to
  subtract. Multiply multiplies by it. Override sets the value to it. Flat Bonus adds too, but as a modifier it lands
  after the attribute's multipliers, so they don't scale it.
- **Multiply Behaviour** (Multiply only): Multiplicative (default) compounds with other multipliers, so ×1.5 and ×1.5
  give ×2.25. Additive Bonus adds the bonuses first, so +50% and +50% give ×2.0.
- **Delivery**, read-only: when the impact lands, which the duration gives it (see the table above). On applying,
  While the effect lasts, or Each tick, which adds ", and on applying" when Tick On Application is on. A note says when
  Tick On Application alone keeps a multiply or override as a modifier, so it reverses as the effect ends.
- **Commits**, read-only, for a periodic impact: how many times it lands at the effect's first level, as in "7 over
  6 s, level 1". For an Infinite effect, it says how far apart the ticks are.
- **Affiliation** decides which targets the effect can be applied to. An entity's affiliation tags are its team or
  faction, set on its Entity asset. If the check fails, the effect isn't applied. Always Allow (default) allows any
  target. Affiliated allows targets that share the origin's affiliation, and Unaffiliated allows those that don't. Use
  Affiliation List compares the target with the **Affiliations** list instead.
  - **Comparison** (all but Always Allow): Any (default) needs one tag to match. All needs every tag to match.

### Impact Magnitude

- **Magnitude**: the amount.
- **Real Magnitude**: how Magnitude and the scaler combine. Add Scaler (default) adds them, Multiply With Scaler
  multiplies them, Use Magnitude ignores the scaler, and Use Scaler ignores Magnitude. With no scaler, the scaler
  counts as 0.
- **Scaler**: an optional value source, such as a per-level table read at the origin's level.

A Continuous modifier works its magnitude out again whenever the attribute is recalculated. A commit works it out once.

### Behavior

- **Impact Types**: tags that label the kind of change, such as `Damage.Fire`. Workers can accept or skip changes by
  type. `Never Allow` hides the change from workers that check types. `Always Allow` passes every type check.

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

An impact with no Attribute changes nothing. The Cost rule checks every attribute a cost spends (see
[ABILITIES.md](ABILITIES.md)).

### Contained Effects

Other effects this one applies to the same target, with the same origin. Each row pairs a moment with an effect: On
Apply (when this effect is applied), On Tick (on each execute tick) or On Remove (when this effect is removed). An
Instant effect has no ticks and no removal, so only its On Apply rows run. An effect that is asked to apply again while
it is still applying is refused, with a warning, so an effect that contains itself doesn't loop forever.

## Duration Specification

- **Duration Policy**: Instant (default) makes the change once. Durational stays for a set time. Infinite stays until
  something removes it.

### Duration (Durational only)

- **Duration**, with **Real Duration** and a scaler: the effect's length in seconds.
- **Delta Time**: **Real Delta Time** and **Delta Time Scaler** change how fast the effect's clock runs. With no
  scaler, the clock follows Unity's frame time. With one, Real Delta Time combines the two. Multiply With Scaler, for
  example, slows the clock down or speeds it up.

### Periodic Ticks (Durational and Infinite)

A tick makes the effect's change again, as a new commit.

- **Enable**: turn periodic ticks on.
- **Tick On Application**: run one tick as soon as the effect is applied, before the first periodic tick. It runs again
  when re-application refreshes or extends the effect. Use it with Enable. On its own, it is a shape that the Audit
  Effect Impact Deliveries menu item flags.
- **Ticks** (Durational): how many ticks fit in the duration, evenly spaced. The last falls as the duration ends, so 5
  ticks over 10 seconds tick every 2 seconds.
- **Tick Interval** (Infinite): the seconds between ticks.

A tick only executes while the Ongoing requirements hold (see Requirements).

### Execute Ticks

Each tick executes the change a number of times: N plus **Additional Execute Ticks**, combined with the scaler. N comes
from the effect's container and is normally 1. A stacking container that shares one duration, or takes it from one
stack, uses its stack count.

### Re-Application (Durational and Infinite)

What happens when the effect is applied to a target that already has it. Each application on the shelf lives in a
*container*. The effect is matched by its asset tag, whoever applied it.

- **Policy**: Append New Container (default) adds a separate container, and both apply. Do Nothing adds nothing new.
  Replace Old Container gives the old container the new application's values and modifiers, and keeps its stack count.
  Past commits stay. Stack Existing Containers adds stacks to the old container (see Stacking).
- **Interaction** applies when the old container takes in the application, so with every policy but Append New
  Container. Do Nothing (default) leaves its time alone. Refresh Container Duration restarts it. Extend Container
  Duration adds the new duration to it. Refresh and Extend also run Tick On Application again.

### Stacking (Stack Existing Containers only)

- **Policy**: Stacks Share One Duration (default) runs one timer for all stacks, and adding a stack doesn't reset it.
  Stacks Have Independent Durations gives each stack its own timer and ticks. Stacks drop off one at a time, and the
  effect lasts as long as its longest stack. Duration Taken From One Stack runs one stack's timer at a time. Each time
  it runs out, one stack is lost and the timer restarts.
- **Behaviours**: optional stacking rules, such as Stack Bounds Config, which keeps the stack count between a Min and
  a Max.
- **Stack Amount**, with **Real Stack Amount** and a scaler: the stacks each application adds. 0 counts as 1.

A Continuous change that reads the stack count updates whenever the count changes.

## Workers

Effect workers react to this effect's own events. A worker can act when the effect is applied, when it ticks, when it
is removed, and when it deals a change to an attribute. Workers return actions, which run at the end of the frame. An
Instant effect runs its apply and remove workers within its one application.

## Requirements

Tag checks against the effect's source, the entity behind its origin (**Source (Caster) Requirements**), and against
its target (**Target Requirements**). Each holds three groups. A group passes when the entity matches every
**Require** entry and no **Avoid** entry. Each entry is a tag query: a **Tag**, a **Match** mode and a comparison with
a number, where Greater Than 0 means "the tag is present".

- **Apply**: must pass for the effect to be applied. The effect is also refused if its Remove group is already met.
- **Ongoing**: while it fails, the effect stays, but its ticks don't execute. Its Continuous modifiers stay in place.
- **Remove**: when met, the effect is removed.

Empty Apply and Ongoing groups always pass. An empty Remove group never removes anything. Ongoing and Remove are
checked whenever an effect is applied to or removed from the target. Ongoing is also checked before each tick.

## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag), a **Type**, and a value of
that type: String, Int, Float, Bool, Tag, Attribute, Effect, Ability, Entity, Object, ScriptableObject or MonoBehaviour.
Object fields take project assets, not scene objects.

PlayForge doesn't read an effect's Local Data itself. Your code and workers can, with
`effect.TryGetLocalData(key, out var data)`. The first row with that key wins.
