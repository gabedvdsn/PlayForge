# PlayForge — Attributes

An **Attribute** asset names one number an entity can have, such as Health, Mana or Strength. An entity is anything
that runs a gameplay ability system (GAS), such as the player or an enemy.

The asset holds no value. An entity gets its starting values for the attribute from its Attribute Set, and gameplay
effects change them from there.

At runtime, each entity holds two values for each of its attributes:

| Value | What it is |
|---|---|
| **Base** | The value the attribute is measured against. For a pool such as Health, Base is the maximum. |
| **Current** | The value in play. For a pool such as Health, Current is what is left. |

For a stat such as Strength, the two are often the same. The Attribute Set's bounds keep them in range (see
[ATTRIBUTE_SETS.md](ATTRIBUTE_SETS.md)).

Attribute sets, effects, validation rules and workers refer to the attribute asset itself. An attribute has no asset
tag.

Each section below matches the inspector section of the same name. A collapsed section shows one line about what it
holds, such as the abbreviation or how many assets use the attribute.

## Identity

The Identity section names the attribute and says what it is.

| Property | Description |
|---|---|
| **Name** | The attribute's display name. Give each attribute its own Name: some lookups compare attributes by Name, such as an attribute worker matching the attribute it watches. |
| **Abbreviation** | A short form, such as HP. The collapsed section shows it. |
| **Description** | Free text for your UI. |
| **Role** | What the attribute is, which says how bonuses to it add up and how a number reads it. |
| **Gains Scaled By** | An attribute that multiplies each workable gain to this one's current, on the same entity. |
| **Textures** | Icons, each paired with a tag. The texture tagged `Primary` is the attribute's icon; without one, the first texture is used. |

An attribute that plays one of its project's roles says so in this section, as "Plays Health in Arena".

### Role

An attribute's **Role** says what it is: how bonuses to it add up, and how a number reads it. **Value** is every
attribute's role unless it says otherwise.

| Role | How bonuses add up | How a number reads it |
|---|---|---|
| **Value** | Bonuses add and multiply as each effect says. | As it is. |
| **Bonus Share**, such as Damage Amplification at 0.2 for 20% | Bonuses add: +20% and +30% make 0.5. | × Attribute reads it as a multiplier, 1 + x, so an attack of 100 that reads it deals 150. |
| **Multiplier**, such as Damage Dealt at 1 | As a Value. | As it is. |
| **Resistance**, a share taken off or a chance, such as Damage Resistance | Bonuses stack with diminishing returns, each on what's left: two of 30% make 51%, and none takes it past 1. | × Attribute reads what's left, 1 − x. |
| **Barrier**, such as a shield before Health | It absorbs losses for the attribute its Absorbs For names. | — |

For a Resistance, the lasting bonuses diminish. A permanent change, which an instant or periodic impact commits, adds as
it is.

A role sets the defaults, which an effect or a number can override:

- An effect's impact can add its own way, with its **Add Behaviour**: as the attribute's role says, a plain Sum, or
  Diminishing.
- A number can read its own way, with × Attribute's **Read As**.

### Barrier

A **Barrier** attribute is a shield before another attribute, such as Health. While the Role is Barrier, the Identity
section shows its settings.

| Property | Description |
|---|---|
| **Absorbs For** | The attribute it absorbs for. Each workable loss to that one comes off the barrier first, as much as it holds, and the rest goes on. |
| **Decay Per Second** | A number field, read at its holder's level, that takes the barrier down by its holder's own time: a number a second. Topaz Brooch's barrier fades so. |
| **Decay As Share Of Base** | Reads Decay Per Second as that share of the barrier's base. |
| **Fades With Effect** | Fades what a lasting effect gives the barrier to 0 by the time that effect ends. |

- Two hits in a frame soak no more than it holds.
- What it soaks comes off it at the frame's end.
- A hit the barrier soaks whole is no loss to the other attribute, so an On Hit counting it doesn't go off.

### Gains Scaled By

**Gains Scaled By** names an attribute that multiplies each workable gain to this one's current, on the same entity.
Health's gains are scaled by Healing Received, so a curse at 0.5 halves heals and Ice Blast's 0 blocks them.

- A loss passes it by.
- A gain to the base, such as more maximum, passes it by too.

## Roles

A game's settings live on its **Forge Project**, the asset at the root of its folder that The Forge's projects are.
Among them are its **Roles**: the part each attribute plays in the game, each a **role tag** and the attribute that
plays it.

PlayForge's own features read the roles it ships, and a game's code reads its own by their tags, so no attribute's name
is built in.

| Role | Tag | What reads it |
|---|---|---|
| **Health** | `Tags.ROLE_HEALTH` | What an entity dies of as it reaches 0, where no death watcher of its own watches for its death and its Death doesn't say otherwise ([ENTITIES.md](ENTITIES.md#death)).<br>What On Hit counts, lifesteal heals and Execute reads, where they name no attribute.<br>Whose loss names the killer, for Kill and Died. |
| **Luck** | `Tags.ROLE_LUCK` | It's read on the caster of every chance that gives no luck of its own. |
| **Movement Speed** | `Tags.ROLE_MOVEMENT_SPEED` | What the Creator Lab's units walk at, where its rules name none. |
| **A role of your own** | Any tag, such as `Role.Energy`, in the same list | Code reads it with `ForgeRoles.AttributeFor(tag)`, and an ability's attribute cost can spend it ([ABILITIES.md](ABILITIES.md#activation)). |

An entity whose set has no attribute in the Health role doesn't die of it. Each role is optional, and an empty one
leaves things as they were.

### Project Settings

**Project Settings → PlayForge** shows the active project's settings under **Forge Project**.

- **Fill Roles by Name** fills each of PlayForge's roles that no attribute plays yet with the project's attribute of its
  name.
- **Create Forge Project** makes the default project, where there's none.
- The settings also hold the project's default **Crowd Control** ([ABILITIES.md](ABILITIES.md#crowd-control)).

### The project a game reads

A running game reads its settings from `ForgeProject.Current`. That's the project code sets with
`ForgeProject.Use(project)`, as a test or a game with no GameRoot does, or else the one its **GameRoot** names in its
**Project** field.

- A build has the project because the GameRoot names it.
- Play mode reads it as a build does, not The Forge's active project.
- In the editor, the inspectors and the Problems window read each asset against its own project: the one whose folder
  holds it.

### What the Problems window checks

The Problems window checks attribute costs, roles and deaths against each asset's project. It warns of:

- an ability's attribute cost that spends nothing, or one that spends a role no attribute plays in its project;
- an entity whose starting abilities spend attributes its set doesn't give;
- an entity that stays when it dies whose set lacks the Health role's attribute, with no death watcher of its own.

## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag) and a value of a kind.

| Column | Description |
|---|---|
| **Key** | A tag that names the row. |
| **Kind** | The kind of value: Float, Int, String, Bool, Tag, Attribute, Effect, Ability, Entity, Object, MonoBehaviour or ScriptableObject, or a kind of your own. |
| **Value** | The row's value. A row saves its own value alone. |

- A kind of your own is a `[Serializable]` class made from `LocalDataValue<T>`, such as a point or a colour. The
  row's kinds offer it beside PlayForge's.
- Object fields take project assets, not scene objects.
- Code reads a value by its type, with `data.TryGet(out float range)` or `data.TryGet(out MyPoint point)`.
- A number, a Float or an Int, can scale. Code reads it with `data.Value.TryReadNumber(context, out float number)`.

PlayForge doesn't read an attribute's Local Data itself. Your code and workers can, with
`attribute.TryGetLocalData(key, out var data)`. The first row with that key wins.

## Used By

The Used By section lists the PlayForge assets that refer to this attribute, each with its type icon:

- the Attribute Sets that give its starting values;
- the Gameplay Effects that change it;
- the validation rules and ability costs that check or spend it.

Click a row to select the asset. The list follows the project as it changes.
