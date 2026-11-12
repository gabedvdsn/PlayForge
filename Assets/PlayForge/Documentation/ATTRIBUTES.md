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
  - **Barrier** (TW-12), as a shield before Health: it absorbs for the attribute its **Absorbs For** names, each workable
    loss to that one coming off it first, as much as it holds, and the rest going on; two hits in a frame soak no more
    than it holds, and what it soaks comes off it at the frame's end. **Decay Per Second** takes it down by its holder's
    own time, a number a second or, with **Decay As Share Of Base**, that share of its base, as Topaz Brooch's barrier
    fades; **Fades With Effect** fades what a lasting effect gives it to 0 by the time that effect ends. A hit the
    barrier soaks whole is no loss to the other, so an On Hit counting it doesn't go off.

**Gains Scaled By** names an attribute that multiplies each workable gain to this one's current, on the same entity
(TW-15): Health's by Healing Received, so a curse at 0.5 halves heals and Ice Blast's 0 blocks them. A loss passes it by,
and a gain to the base, as more maximum, does too.

  An effect's impact can add its own way (**Add Behaviour**: as the attribute's role says, a plain Sum, or Diminishing),
  and a number can read its own way (× Attribute's **Read As**).

An attribute that fills one of the project's slots says so under its Role, as "This project's Health".

## The Project's Attributes

**Project Settings → PlayForge** names, under **Project Attributes**, the attributes PlayForge reads by their part in
the game (TW-28). **Create Project Attributes** makes the asset that holds them, at
`Assets/Resources/PlayForge Project Attributes.asset`, so a build has it too, and fills each slot with the project's
attribute of its name; **Fill by Name** fills the empty ones again. Each slot is optional, and an empty one leaves
things as they were. An asset's own setting always wins over the project's.

- **Health**: what an entity dies of as it reaches 0, where no death watcher of its own watches for its death and its
  Death doesn't say otherwise ([ENTITIES.md](ENTITIES.md#death)); what On Hit counts as a hit where an entry names no
  attribute ([ITEMS.md](ITEMS.md#on-hit)); and whose loss names the killer, for Kill and Died. An entity whose set has
  no Health doesn't die of it. A trigger's Attribute of None still takes every attribute.
- **Mana**: what an ability's **Mana Cost** spends, where it has no Cost effect ([ABILITIES.md](ABILITIES.md#activation)).
  A caster with less, or with no Mana, is refused with Cost.
- **Luck**: read on the caster of every chance that gives no luck of its own (TW-8).
- **Movement Speed**: what the Creator Lab's units walk at, where its rules name none ([CREATOR_LAB.md](CREATOR_LAB.md)).
- **Crowd Control**: not an attribute, but the project's all the same: the crowd control rules an entity's abilities
  follow where it names none of its own (TW-16, [ABILITIES.md](ABILITIES.md#crowd-control)).

Code reads them through `ForgeAttributes`, as `ForgeAttributes.Health`, each null where the project sets none. A test
uses attributes of its own with `ForgeAttributes.Use(attributes)`, which gives back the ones it replaced, to put back.
The Problems window warns of an ability whose Mana Cost has no Mana of the project's to pay it, an entity whose starting
abilities cost Mana its attribute set doesn't give, and an entity that stays when it dies whose set lacks the project's
Health, with no death watcher of its own.

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
