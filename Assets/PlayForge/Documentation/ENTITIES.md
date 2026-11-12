# PlayForge — Entities

An **Entity** asset (`EntityIdentity`) describes one kind of entity, such as the hero or a goblin. An entity is anything
that runs a gameplay ability system (GAS). The asset is the entity's starting setup: its attributes, abilities, items,
tags, level and workers. Many entities can share one asset.

At runtime a GAS hub reads the asset when it initializes. The hub is either the `GameplayAbilitySystem` component on a
GameObject, or a `RuntimeGameplayAbilitySystem` made from code. It subscribes the entity's workers, registers its
level, gives its Starting Abilities, builds its attributes from its Attribute Set, gives its Starting Items, and then
applies its tags.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Hero gives `Entity.Hero`. The entity carries this tag and keeps its own level under it. Each section
below matches the inspector section of the same name. A collapsed section shows one line about what it holds, such as
the level range or how many starting items the entity equips.

## Identity

- **Name**: the entity's display name. It also gives the asset tag, so renaming the entity changes its tag. Update
  anything that names the old tag.
- **Description**: free text for your UI.
- **Asset Tag**: read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. The
  tag is stored when you edit the Name, and the inspector header shows it after the entity's type. If the stored tag
  differs from the one the Name gives, it shows in warning text with an **Update** button that stores the new one.
- **Textures**: icons, each paired with a tag. The texture tagged `Primary` is the entity's icon. Without one, the first
  texture is used.

## Leveling

An entity's level is kept under its asset tag. A level provider is an asset whose level range another asset follows.

- **Link Mode**: Standalone (default) keeps the entity's own range. Linked To Provider shows a provider's range
  instead.
- **Starting Level** (default 1): the entity's level when its GAS initializes, and its lowest level.
- **Max Level** (default 99): its highest level. The level always stays within this range.
- **Level Provider**, while linked: the asset whose range the section shows, an Entity, Ability, Effect, Item or
  Attribute Set. Right-click the field to pick from one type. A link that would make a loop is refused, with an error
  in the Console. While no provider is set, a warning says the entity uses its own range.
- **Link Items & Abilities** links this entity's Starting Items and Starting Abilities to it, so their per-level values
  follow its range. **Unlink Items & Abilities** removes the links that point at this entity. Each asks first, and one
  Undo takes it back.

The entity's level feeds per-level values: the effects it applies as an origin, its Max Abilities and item limits, and
the cached scalers in its attribute set. Code changes the level through the GAS, with `SetLevel` or `ModifyLevel` and
the entity's asset tag. At runtime, the entity's level range comes from its own Starting Level and Max Level, even while
it is linked.

## Tags

- **Affiliation**: the entity's team or faction tags. An effect's Affiliation check compares its origin's affiliation
  with its target's. Code that starts a GAS can pass affiliation tags in its registration data, and those replace this
  list; as the game runs, `SetAffiliation` on either hub sets them, as a side switch or a scripted betrayal (TW-18). The
  GAS also applies these tags to the entity, on either hub. An effect's Side Change goes over them while it lasts
  ([EFFECTS.md](EFFECTS.md)), and `GetSides` says which it's under.
- **Context Tags** describe the entity, such as `Undead`. Changes the entity makes as an origin itself carry them as
  context, and workers can filter by them.
- **Granted Tags** are always applied to the entity.

Code hears an entity's tags change through its tag cache. `gas.GetTagCache().Listen(tag, onChange)` hears the tag,
and every tag under it, come and go, as it happens; pass `ETagChangeKind.AnyCount` to hear every change to a tag's
weight. Each change says the tag and its weight before and after. When the entity rebuilds its tags from their
sources, as it does when an ability starts or ends, an item changes or a loose tag is granted, it tells only what the
rebuild changed. Dispose the listener to stop it. `TagChanged` hears every change to any tag.

```csharp
var listener = gas.GetTagCache().Listen(stunned, change => animator.SetBool("Stunned", change.Added));
listener.Dispose();
```

A tag worker is evaluated at the end of a frame in which the entity's tags changed. One whose `ValidateWorkFor`
reads more than the tags overrides `EvaluateEveryFrame` to be evaluated every frame.

## Attributes

The entity's attributes come from its **Attribute Set**. Under the field, a table lists each attribute the set gives,
its sub-sets' included, with the value it starts at and its range. When the GAS initializes, it creates every attribute
in the set and its sub-sets, each at its starting Current and Base values. It also applies the set's asset tags to the entity and
subscribes the set's workers. Gameplay effects then change the values. [ATTRIBUTE_SETS.md](ATTRIBUTE_SETS.md) describes
the set itself.

An entity with no Attribute Set has no attributes. An effect that targets an attribute the entity lacks leaves it
unchanged.

**Time Scale** names the attribute the entity's time runs at, 1 as the game's. Its effects, cooldowns, sequences and
waits run by that time, so an effect that lowers the attribute slows the entity, one that raises it hastes it, and
as each comes off the entity's time is the game's again. Give the attribute a Current of 1 in the set. With no Time
Scale, the entity's time scale is its game's code's to set, through `GetClock().TimeScale`.

## Abilities

- **When Busy** (default Queue): what the entity's abilities set to Entity's Policy do when one is asked to activate
  while another is busy. Activate Anyway starts it at once. Refuse starts it only when no ability under that choice is
  busy, and refuses it otherwise. Queue makes it wait in a queue while one is busy. [ABILITIES.md](ABILITIES.md)
  explains each.
- **Starting Abilities**: abilities the entity learns when its GAS initializes, each at its own Starting Level. Those
  set to Activate At Start then try to activate.
- **Max Abilities** (default 99): the most abilities the entity can hold. Its kind can make it a scaler, which changes
  the limit with the entity's level ([SCALERS.md](SCALERS.md)).
- **Allow Duplicates** (default off): let the entity hold the same ability more than once.
- **Crowd Control** (TW-16): the crowd control rules its abilities follow, as a stun stops every ability and Silenced
  spells, or, where it names none, the project's ([ABILITIES.md](ABILITIES.md#crowd-control)). A runtime entity's
  identity names them too.

## Items

- **Starting Items**: items the entity is given when its GAS initializes, one row each. **Equip** equips the item as it
  is given.
- **Max Items** (default 99): the most items the inventory can hold.
- **Max Equipped Items** (default 99): the most items equipped at once.
- **Allow Duplicates** (default off): let the inventory hold the same item more than once.
- **Equipped Too** (default off): let more than one copy of the same item be equipped at once. It needs Allow
  Duplicates, since an inventory that holds one of each item has no second copy to equip.

Each Max field is a number, or, by its kind, a scaler that changes the limit with the entity's level, with its rounding
([SCALERS.md](SCALERS.md)). A give or equip that breaks a limit fails.

## Workers

Workers are reusable behaviours, picked from a list, that react to what happens to an entity. This group is subscribed
when the entity's GAS initializes, and stays for the entity's lifetime. The attribute set's workers are subscribed at
the same time. **Name** labels the group. It holds four lists:

- **Attribute** workers run before or after an attribute changes, for example to reduce incoming damage.
- **Impact** workers inspect the changes the entity deals.
- **Tag** workers react when the entity's tags change.
- **Analysis** workers examine the frame at its end, for example to detect death.

Each list's **+** adds an empty worker to pick a kind for. The section's import button copies a worker group
from another asset.

A **Threshold**, an analysis worker, acts as an attribute crosses a value, without code (TW-11): Megaera rages as her
Health falls past half, and Ice Blast shatters below 13%. It reads its **Target** attribute's **What**: its current, its
base, what's missing of it, or its share of its base, so a **Threshold** of 0.5 on its share is half. **Crossing** says
when it fires: while its **Comparison** holds, as it always did; or as what it reads falls to the threshold or below,
rises above it, or either. **Trigger Once** fires at its first crossing alone; off, each time. As it fires it applies its
**Effects** to the entity, from the entity itself, or, with **Applied By** set to Crosser, from the one whose change
crossed it this frame, so a shatter is its caster's, kill and all; and it sends its **Event**, a gameplay event its
abilities' triggers hear, its source the one who crossed it and its subject the entity. It checks at the frame's end,
as the death watcher, a Threshold that kills, does, so the frame's hits and heals net out. An entity that revives re-arms
it, and its first check after notes its side again.

Two attribute workers keep the common rules (TW-15). **Can't Fall Below** holds its attribute at its **Floor**: a loss
that would take it lower takes it to the floor, as Shallow Grave keeps its target at 1 Health, in a buff's worker group
while it lasts. **Return Damage** sends its **Share** of each workable loss its attribute takes from another back to the
one who dealt it, typed Returned, so a return on the other side doesn't send it back again and no lifesteal heals from it,
as Blade Mail.

**Modifiers**, above the workers, change other effects as they're made for as long as the entity lives: its own, as a
Frost Mage's every Chill granting Frozen, or those landing on it (TW-7). Each entry is a rule of its own or names a
shared Effect Modifier asset ([EFFECTS.md](EFFECTS.md#modifiers)).

**Auras**, after Modifiers, hold an effect on those near the entity as long as it lives, as a captain's command aura
(TW-10, [EFFECTS.md](EFFECTS.md#auras)). An entity that dies takes its auras' effects off, and one that stays and
revives holds them again.

**Lifesteal**, after Auras, heals the entity for the hits it deals as long as it lives (TW-15,
[ITEMS.md](ITEMS.md#lifesteal)).

## Death

What the entity's death does (TW-4). **Policy** is **End** by default: as it dies, its hub ends it at its frame's end,
as an entity always did. **Stay** keeps it, dead, until it revives: a MOBA hero, or anyone who dies and comes back.

An entity dies as a death watcher among its analysis workers, or its attribute set's, says it does. Where none watches
and an attribute plays its project's Health role ([ATTRIBUTES.md](ATTRIBUTES.md#roles)), it dies as that attribute reaches 0
(TW-28, TW-29). **Dies At Zero Health**, on by default, says it does; off, as for a training dummy, that attribute
doesn't kill it.

- A staying entity dies at its frame's end and stays registered, with its items, levels and lasting effects. Its
  abilities are refused while it's dead.
- **Ends Effects Tagged**: the effects its death ends, by an asset, context or granted tag, as a cleanse ends them: its
  buffs and debuffs. The rest go on, as its cooldowns.
- **On Death**: the effects it takes as it dies. A respawn timer is a durational effect with a **Revive On Removal**
  worker: as it ends, the entity revives, where it was or at a point.
- **On Revive**: the effects it takes as it revives, as its health back.
- **Body**: a MonoBehaviour process, a prefab, that it spawns where it is as it starts and as it revives, and ends as it
  dies. A runtime entity takes its position from its body, so an area search finds it there; `GetBody()` returns it.

`Revive(at)` on either hub brings a staying entity back, at a point or where it was; it re-arms the entity's analysis
workers, so its death watcher sees its next death. Callbacks hear `OnEntityRevived`, as they hear `OnEntityDied`.

## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag), a **Type**, and a value of
that type: String, Int, Float, Bool, Tag, Attribute, Effect, Ability, Entity, Object, ScriptableObject or MonoBehaviour.
Object fields take project assets, not scene objects.

When a `GameplayAbilitySystem` component initializes, it loads the entity's Local Data into a store of its own. Code and
workers read and write that store through the GAS, with `TryGetLocalData` and `SetLocalData`. Threshold analysis
workers, for example, note there that they have fired. A `RuntimeGameplayAbilitySystem` starts with an empty store.

## Used By

The PlayForge assets that refer to this entity, each with its type icon, such as the abilities and items linked to it.
Click a row to select the asset. The list follows the project as it changes.
