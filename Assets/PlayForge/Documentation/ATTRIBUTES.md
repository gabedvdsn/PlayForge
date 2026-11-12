# PlayForge — Attributes

An **Attribute** asset names one number an entity can have, such as Health, Mana or Strength. An entity is anything
that runs a gameplay ability system (GAS), such as the player or an enemy. The asset holds no value. An entity gets its
starting values for the attribute from its Attribute Set, and gameplay effects change them from there.

At runtime each entity holds two values for each of its attributes. **Base** is the value the attribute is measured
against, and **Current** is the value in play. For a pool such as Health, Base is the maximum and Current is what is
left. For a stat such as Strength, the two are often the same. The Attribute Set's bounds keep them in range (see
[ATTRIBUTE_SETS.md](ATTRIBUTE_SETS.md)).

Attribute sets, effects, validation rules and workers refer to the attribute asset itself. An attribute has no asset
tag. Each section below matches the inspector section of the same name. A collapsed section shows one line about what
it holds, such as the abbreviation or how many assets use the attribute.

## Identity

- **Name**: the attribute's display name. Give each attribute its own Name: some lookups compare attributes by Name,
  such as an attribute worker matching the attribute it watches.
- **Abbreviation**: a short form, such as HP. The collapsed section shows it.
- **Description**: free text for your UI.
- **Textures**: icons, each paired with a tag. The texture tagged `Primary` is the attribute's icon. Without one, the
  first texture is used.
- **Role** (TW-1): what the attribute is, which says how bonuses to it add up and how a number reads it:
  - **Value**, every attribute's unless it says: bonuses add and multiply as each effect says, and a number reads it as
    it is.
  - **Bonus Share**, as Damage Amplification at 0.2 for 20%: bonuses add, +20% and +30% making 0.5, and × Attribute
    reads it as a multiplier, 1 + x, so an attack of 100 that reads it deals 150.
  - **Multiplier**, as Damage Dealt at 1: as a Value, read as it is.
  - **Resistance**, a share taken off or a chance, as Damage Resistance: bonuses stack with diminishing returns, each on
    what's left, so two of 30% make 51% and none takes it past 1; × Attribute reads what's left, 1 − x. The lasting
    bonuses diminish; a permanent change, which an instant or periodic impact commits, adds as it is.

  An effect's impact can add its own way (**Add Behaviour**: as the attribute's role says, a plain Sum, or Diminishing),
  and a number can read its own way (× Attribute's **Read As**).

## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag), a **Type**, and a value of
that type: String, Int, Float, Bool, Tag, Attribute, Effect, Ability, Entity, Object, ScriptableObject or MonoBehaviour.
Object fields take project assets, not scene objects.

PlayForge doesn't read an attribute's Local Data itself. Your code and workers can, with
`attribute.TryGetLocalData(key, out var data)`. The first row with that key wins.

## Used By

The PlayForge assets that refer to this attribute, each with its type icon: the Attribute Sets that give its starting
values, the Gameplay Effects that change it, and the validation rules and ability costs that check or spend it. Click
a row to select the asset. The list follows the project as it changes.
