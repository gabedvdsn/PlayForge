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
  list. The GAS also applies these tags to the entity, on either hub.
- **Context Tags** describe the entity, such as `Undead`. Changes the entity makes as an origin itself carry them as
  context, and workers can filter by them.
- **Granted Tags** are always applied to the entity.

## Attributes

The entity's attributes come from its **Attribute Set**. Under the field, a table lists each attribute the set gives,
its sub-sets' included, with the value it starts at and its range. When the GAS initializes, it creates every attribute
in the set and its sub-sets, each at its starting Current and Base values. It also applies the set's asset tags to the entity and
subscribes the set's workers. Gameplay effects then change the values. [ATTRIBUTE_SETS.md](ATTRIBUTE_SETS.md) describes
the set itself.

An entity with no Attribute Set has no attributes. An effect that targets an attribute the entity lacks leaves it
unchanged.

## Abilities

- **When Busy** (default Queue): what the entity's abilities set to Entity's Policy do when one is asked to activate
  while another is busy. Activate Anyway starts it at once. Refuse starts it only when no ability under that choice is
  busy, and refuses it otherwise. Queue makes it wait in a queue while one is busy. [ABILITIES.md](ABILITIES.md)
  explains each.
- **Starting Abilities**: abilities the entity learns when its GAS initializes, each at its own Starting Level. Those
  set to Activate At Start then try to activate.
- **Max Abilities** (default 99), with **Real Max Abilities**, rounding and a scaler: the most abilities the entity can
  hold. A scaler can change the limit with the entity's level.
- **Allow Duplicates** (default off): let the entity hold the same ability more than once.

## Items

- **Starting Items**: items the entity is given when its GAS initializes, one row each. **Equip** equips the item as it
  is given.
- **Max Items** (default 99): the most items the inventory can hold.
- **Max Equipped Items** (default 99): the most items equipped at once.
- **Allow Duplicates** (default off): let the inventory hold the same item more than once.
- **Equipped Too** (default off): let more than one copy of the same item be equipped at once. It needs Allow
  Duplicates, since an inventory that holds one of each item has no second copy to equip.

Each Max field has a **Real** field, rounding and a scaler, which can change the limit with the entity's level. A give
or equip that breaks a limit fails.

## Workers

Workers are reusable behaviours, picked from a list, that react to what happens to an entity. This group is subscribed
when the entity's GAS initializes, and stays for the entity's lifetime. The attribute set's workers are subscribed at
the same time. **Name** labels the group. It holds four lists:

- **Attribute** workers run before or after an attribute changes, for example to reduce incoming damage.
- **Impact** workers inspect the changes the entity deals.
- **Tag** workers react when the entity's tags change.
- **Analysis** workers examine the frame at its end, for example to detect death.

The import button in the group's header copies a worker group from another asset.

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
