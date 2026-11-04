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
below matches the inspector section of the same name.

## Definition

- **Name**: the entity's display name. It also gives the asset tag, so renaming the entity changes its tag. Update
  anything that names the old tag.
- **Description**: free text for your UI. The inspector header shows it.
- **Textures**: icons, each paired with a tag. The texture tagged `Primary` is the entity's icon. Without one, the first
  texture is used.
- **Attribute Set**: the set that gives the entity its attributes (see Attributes).

## Level Source

An entity's level is kept under its asset tag. A level provider is an asset whose level range another asset follows.
Linking the entity to a provider makes the Leveling section show the provider's range.

- **Link Mode**: Standalone (default) uses the entity's own Leveling values. Linked To Provider shows the provider's
  range instead.
- **Level Provider**: the linked asset, or No Provider Linked.
- **Link to**: pick a provider of one type: Entity, Ability, Effect, Item or Attribute Set. A link that would make a
  loop is refused, with an error in the Console. The status box below shows the provider's type, name and range, with
  **Go to Provider** and **Unlink** buttons.
- **Child Assets**: **Link Ability & Effects** links this entity's Starting Abilities and Starting Items to it.
  **Unlink Ability & Effects** removes the links that point at this entity.

At runtime, the entity's level range comes from its own Starting Level and Max Level, even while it is linked.

## Tags

- The **asset tag** shows at the top, read-only. It is made from the Name: punctuation is dropped, and each word is
  capitalised and joined. The tag is stored when you edit the Name. If the stored tag differs from the one the Name
  gives, it shows in amber with an **Update** button that stores the new one.
- **Context Tags** describe the entity, such as `Undead`. Changes the entity makes as an origin itself carry them as
  context, and workers can filter by them.
- **Granted Tags** are always applied to the entity.
- **Affiliation**: the entity's team or faction tags. An effect's Affiliation check compares its origin's affiliation
  with its target's. Code that starts a GAS can pass affiliation tags in its registration data, and those replace this
  list. The GAS also applies these tags to the entity, on either hub.

## Leveling

- **Starting Level** (default 1): the entity's level when its GAS initializes, and its lowest level.
- **Max Level** (default 99): its highest level. The level always stays within this range.

The entity's level feeds per-level values: the effects it applies as an origin, its Max Abilities and item limits, and
the cached scalers in its attribute set. Code changes the level through the GAS, with `SetLevel` or `ModifyLevel` and
the entity's asset tag. When Link Mode is Linked To Provider, this section shows the provider's range, read-only.

## Abilities

- **Activation Policy** (default Queue Activation If Busy): the entity's own policy, used by its abilities that are set
  to Use Local Policy. Always Activate starts an ability at once. Activate If Idle starts it only when no ability under
  that policy is busy, and refuses it otherwise. Queue Activation If Busy makes it wait in a queue while one is busy.
  [ABILITIES.md](ABILITIES.md) explains each.
- **Starting Abilities**: abilities the entity learns when its GAS initializes, each at its own Starting Level. Those
  set to Activate Immediately then try to activate.
- **Max Abilities** (default 99), with **Real Max Abilities**, rounding and a scaler: the most abilities the entity can
  hold. A scaler can change the limit with the entity's level.
- **Allow Duplicate Abilities** (default off): let the entity hold the same ability more than once.

## Items

- **Starting Items**: items the entity is given when its GAS initializes. **Equip On Init** equips the item as it is
  given.
- **Max Items** (default 99): the most items the inventory can hold.
- **Allow Duplicate Items** (default off): let the inventory hold the same item more than once.
- **Max Equipped Items** (default 99): the most items equipped at once.
- **Allow Duplicate Equipped Items** (default off): let more than one copy of the same item be equipped at once.

Each Max field has a **Real** field, rounding and a scaler, which can change the limit with the entity's level. A give
or equip that breaks a limit fails.

## Attributes

The entity's attributes come from its **Attribute Set**. When the GAS initializes, it creates every attribute in the set
and its sub-sets, each at its starting Current and Base values. It also applies the set's asset tags to the entity and
subscribes the set's workers. Gameplay effects then change the values. [ATTRIBUTE_SETS.md](ATTRIBUTE_SETS.md) describes
the set itself.

An entity with no Attribute Set has no attributes. An effect that targets an attribute the entity lacks leaves it
unchanged.

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
