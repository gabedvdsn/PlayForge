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
    than it holds, and what it soaks comes off it at the frame's end. **Decay Per Second**, a number field read at its holder's level (FN-19), takes it down by its holder's
    own time, a number a second or, with **Decay As Share Of Base**, that share of its base, as Topaz Brooch's barrier
    fades; **Fades With Effect** fades what a lasting effect gives it to 0 by the time that effect ends. A hit the
    barrier soaks whole is no loss to the other, so an On Hit counting it doesn't go off.

**Gains Scaled By** names an attribute that multiplies each workable gain to this one's current, on the same entity
(TW-15): Health's by Healing Received, so a curse at 0.5 halves heals and Ice Blast's 0 blocks them. A loss passes it by,
and a gain to the base, as more maximum, does too.

  An effect's impact can add its own way (**Add Behaviour**: as the attribute's role says, a plain Sum, or Diminishing),
  and a number can read its own way (× Attribute's **Read As**).

An attribute that plays one of its project's roles says so, as "Plays Health in Arena".

## Roles

A game's settings live on its **Forge Project** (TW-29), the asset at the root of its folder that The Forge's projects
are. Among them are its **Roles**: the part each attribute plays in the game, each a **role tag** and the attribute that
plays it. PlayForge's own features read the roles it ships, and a game's code reads its own by their tags, so no
attribute's name is built in:

- **Health** (`Tags.ROLE_HEALTH`): what an entity dies of as it reaches 0, where no death watcher of its own watches
  for its death and its Death doesn't say otherwise ([ENTITIES.md](ENTITIES.md#death)); what On Hit counts, lifesteal
  heals and Execute reads where they name no attribute; and whose loss names the killer, for Kill and Died. An entity
  whose set has no attribute in this role doesn't die of it.
- **Luck** (`Tags.ROLE_LUCK`): read on the caster of every chance that gives no luck of its own (TW-8).
- **Movement Speed** (`Tags.ROLE_MOVEMENT_SPEED`): what the Creator Lab's units walk at, where its rules name none.
- **Lifesteal** (`Tags.ROLE_LIFESTEAL`): the share of each loss an entity deals that the Lifesteal worker heals it, as
  0.15 for 15% (FN-15, [WORKERS.md](WORKERS.md#lifesteal)). An item or a buff gives lifesteal by an effect on it.
- **A role of your own**: any tag, as `Role.Energy`, in the same list. Code reads it with `ForgeRoles.AttributeFor(tag)`,
  and an ability's attribute cost can spend it ([ABILITIES.md](ABILITIES.md#activation)).

Each role is optional, and an empty one leaves things as they were. **Project Settings → PlayForge** shows the active
project's settings under **Forge Project**: **Fill Roles by Name** fills each of PlayForge's roles no attribute plays yet
with the project's attribute of its name, and **Create Forge Project** makes the default project where there's none.
The settings also hold the project's default **Crowd Control** ([ABILITIES.md](ABILITIES.md#crowd-control)).

A running game reads its settings from `ForgeProject.Current`: the project code sets with `ForgeProject.Use(project)`,
as a test or a game with no GameRoot does, or else the one its **GameRoot** names in its **Project** field. A build has
the project because the GameRoot names it, and play mode reads it as a build does, not The Forge's active project. In
the editor, the inspectors and the Problems window read each asset against its own project, the one whose folder holds
it. The Problems window warns of an attribute cost that spends nothing, or a role no attribute plays in its project; an
entity whose starting abilities spend attributes its set doesn't give; and an entity that stays when it dies whose set
lacks the Health role's attribute, with no death watcher of its own.

TW-28 kept fixed Health, Mana, Luck and Movement Speed slots in a Resources asset, read through `ForgeAttributes`; TW-29
replaced them.

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
