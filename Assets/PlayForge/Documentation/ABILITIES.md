# PlayForge — Abilities

An **Ability** asset describes one thing an entity can do, such as a fireball, a dash or a heal. An entity is anything
that runs a gameplay ability system (GAS), such as the player or an enemy. An entity learns an ability from its Entity
asset's Starting Abilities, from an equipped Item, or from code (`AbilitySystemComponent.GiveAbility`). It keeps the
ability until the ability is removed.

Each time the ability activates, the GAS checks that it may run, lets a targeting task pick a target, then runs the
ability's stages of tasks. The ability's cost and cooldown are gameplay effects, assets that change an entity, which
it applies to its caster. Abilities run as processes under `ProcessControl`, so the scene needs one.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Fire Ball gives `Ability.FireBall`. The entity carries this tag while it has the ability, and keeps
the ability's level under it. Each section below matches the inspector section of the same name.

## Identity

- **Name**: the ability's display name. It also gives the asset tag, so renaming the ability changes its tag. Update
  anything that names the old tag.
- **Description**: free text for your UI. The inspector header shows it.
- **Asset Tag**: read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. The
  tag is stored when you edit the Name. If the stored tag differs from the one the Name gives, it shows in warning
  text with an **Update** button that stores the new one.
- **Textures**: icons, each paired with a tag. The texture tagged `Primary` is the ability's icon. Without one, the
  first texture is used.

## Leveling

An ability has its own level on each entity that learns it, kept under the ability's asset tag. Effects the ability
applies take this level, so their scalers can pick per-level values. A scaler is a value source, such as a table with
one value per level. A level provider is an asset whose level range another asset follows.

- **Link Mode**: Standalone (default) keeps the ability's own range. Linked To Provider takes the range from a level
  provider instead.
- **Starting Level** (default 1) and **Max Level** (default 4): the ability's own range. An entity's Starting Abilities
  begin at the Starting Level, and the ability's level never goes below it.
- **Level Provider**, while linked: the asset the range comes from, an Entity, Ability, Effect, Item or Attribute Set.
  Right-click the field to pick from one type. A link that would make a loop is refused, with an error in the Console.
  While no provider is set, a warning says the ability uses its own range. While linked, Starting Level and Max Level
  show the range in use, read-only.
- **Ignore When Level Zero** (default on): the ability can't activate while its level is 0. With a Starting Level of 0,
  the entity must raise the ability's level before it can use it.
- **Link Cost & Cooldown** links this ability's cost and cooldown effects to it, so their per-level values follow its
  range. **Unlink Cost & Cooldown** removes the links that point at this ability. Each asks first, and one Undo takes
  it back.

Code changes an ability's level through the entity's GAS, with `SetLevel` or `ModifyLevel` and the ability's asset
tag. An ability that an Item grants starts at the item's level, kept within this range.

The link serves the inspector and the tooltips that `AbilityInspector` builds. At runtime, the ability's level stays
within its own Starting Level and Max Level, even while it is linked.

## Tags

- **Context Tags** describe the ability, such as `Spell` or `Melee`. The changes made by effects this ability applies
  carry them as context, and workers can filter by them. They are not applied to the entity.
- **Passive Tags** are applied to the entity while it has the ability.
- **Active Tags** are applied to the entity while an activation of the ability is running.

## Activation

This section is how the ability starts. One activation goes through these steps:

1. The caster's tag requirements and Caster Checks are checked (see Requirements), and the ability's level (see
   Leveling).
2. The Targeting task runs, if there is one.
3. If targeting found a target, every check and requirement is checked again, the target's included.
4. The Asset Loaders run (see Stages).
5. The Stages run, one after another.

These steps start inside the call that asks for the ability, `AbilitySystemComponent.TryActivateAbility`, and run until
a task waits. So an ability whose tasks all finish at once has ended, and its callbacks have fired, by the time the call
returns. An ability asked for again while that call still runs, from its own callbacks or from another ability's that
it started, starts in the next frame instead. When the entity ends, its running abilities end with it, critical or not,
and it takes no more activations.

- **When Busy**: what happens when the ability is asked to activate while another ability is busy. An activation is
  busy, or holds its *claim*, while its targeting and its critical tasks run. A critical task is one that must not
  overlap another ability's critical work, such as an attack animation. The inspector marks critical tasks and stages
  Critical. An ability only waits on abilities that use the same choice.
  - **Entity's Policy** (default): use the entity's When Busy, set in the Entity asset's Abilities section.
  - **Activate Anyway**: start at once. It is refused only when it has critical tasks and another Activate Anyway
    ability with critical tasks holds its claim.
  - **Refuse**: start only when no Refuse ability holds its claim. Otherwise the request is refused.
  - **Queue**: when a Queue ability holds its claim, wait in a queue. A queued request is tried again when a claim is
    released.

  Whatever the choice, an ability can't start again while its own activation holds its claim.
- **Activate At Start**: when the ability is one of the entity's Starting Abilities, it tries to activate once, as the
  entity's GAS starts. An ability that an item or code grants later doesn't try it.
- **Cost** and **Cooldown**: gameplay effects ([EFFECTS.md](EFFECTS.md)) applied to the caster when a stage applies
  usage effects (see Stages). A cost is usually an Instant effect that lowers a resource such as Mana. A cooldown is
  usually a Durational effect: while it is active, the caster carries its asset tag, and the Cooldown check refuses the
  ability. A line under each field says how its effect takes its levels, and **Link** makes it take this ability's.
  **New**, beside an empty field, makes an effect linked to this ability. If The Forge's settings name a template for
  costs or cooldowns, the field's right-click menu copies it or uses it.
- **Targeting**: the task that picks what the ability acts on, such as an entity the player clicks, or the caster
  itself. Targeting holds the activation's claim. If the task fails while its **Break Runtime On Invalid** is on, or it
  doesn't finish, the activation ends. Leave Targeting empty to skip this step.
- **Use Implicit Targeting** (default on): before targeting, record the caster as the activation's target, under
  **Implicit Tag** (default `Target`), which shows while this is on. A targeting task that sets a target replaces it.

## Stages

This section is what the ability does once it has started.

- **Asset Loaders**: run once per activation, after targeting. Each loader adds assets to the activation's data under
  a tag. A Set Effects loader, for example, adds effects under `Effects`. Code-driven tasks read them from there.
- **Stages**: each stage starts all its tasks together. The next stage starts when it ends.
  - **Completion Policy**: All ends the stage when every task has finished. Any ends it when the first task finishes,
    and cancels the rest. A stage with no policy set behaves as All.
  - **Apply Usage Effects**: apply the ability's cost and cooldown in this stage. They apply once per activation, in
    the first stage that does this. If no stage does, they are never applied.
  - **Apply Usage At Start**: on, apply them as the stage starts. Off, apply them as it ends, and only if it succeeded.
  - **Tasks**: the work, such as Apply Effects, Delay or Run Sequence. A stage that holds a critical task is critical.
    The activation keeps its claim until its last critical stage ends. With no critical stage, it lets the claim go
    as the stages start.

## Requirements

Requirements are what an activation must pass. If one fails, the ability doesn't run.

- **Ability Requirements** holds tag checks. **Source** checks the caster: it must match every **Require** entry and no
  **Avoid** entry, for example Avoid `State.Stunned`. Source is checked with the Caster Checks. **Target** checks the
  target the same way, after targeting. Each entry is a tag query: a **Tag**, a **Match** mode such as Include
  Children, and a comparison with a number. Greater Than 0 means "the tag is present". An empty group always passes.
- **Caster Checks** check the caster. They run when the ability is asked to activate, before targeting, and again after
  targeting. The defaults are Cooldown, Cost and Is Alive. You pick each check's type from a list.
- **Target Checks** check the target, after targeting. The default is Is Alive. If targeting found no target, they are
  skipped.

The check types:

- **Cooldown**: passes when the ability has no cooldown, or the caster doesn't carry the cooldown effect's asset tag.
- **Cost**: passes when the ability has no cost, or the cost would leave its attribute at 0 or more. The caster must
  have that attribute.
- **Is Alive**: passes when the checked entity isn't dead.
- **Attribute**: compares the checked entity's Current value of **Attribute** with **Value**, using **Comparison**.
- **Check Range**: passes when the target is within range of the caster. The range is the number of times the caster
  carries the check's **Tag**. If the caster doesn't carry it, the range is the Float stored under that key in this
  ability's Local Data. Use Check Range as a Target Check: as a Caster Check it runs before there is a target, and
  fails.

The section's Clear button empties the tag requirements and leaves the checks, since an ability without them would
skip its cost and cooldown.

## Workers

Workers are reusable behaviours, picked from a list, that react to what happens to an entity. This group is subscribed
to the entity when it learns the ability, and removed when the ability is removed. **Name** labels the group. It holds
four lists:

- **Attribute** workers run before or after an attribute changes, for example to reduce incoming damage.
- **Impact** workers inspect the changes this entity deals.
- **Tag** workers react when the entity's tags change.
- **Analysis** workers examine the frame at its end, for example to detect death.

The import button in the group's header copies a worker group from another asset.

## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag), a **Type**, and a value of
that type: String, Int, Float, Bool, Tag, Attribute, Effect, Ability, Entity, Object, ScriptableObject or MonoBehaviour.
Object fields take project assets, not scene objects.

Code reads a value with `ability.TryGetLocalData(key, out var data)`. The first row with that key wins. The Check Range
check reads its range from here (see Requirements).

## Used By

The PlayForge assets that refer to this ability, each with its type icon, such as an entity that starts with it, an
item that grants it, or a cost linked to it. Click a row to select the asset. The list follows the project as it
changes.
