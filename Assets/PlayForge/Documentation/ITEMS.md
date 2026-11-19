# PlayForge — Items

An **Item** asset describes something an entity can hold and equip, such as a sword or a ring. An entity is anything
that runs a gameplay ability system (GAS), such as the player or an enemy.

- Giving an item puts it in the entity's inventory.
- Equipping it applies what it grants: its Granted Effects, its Active Ability, its Granted Tags and its workers.
- Unequipping the item, or removing it from the inventory, takes them away again.

An entity gets items from its Entity asset's Starting Items, or from code through its `ItemSystemComponent`, with
`GiveItem`, `EquipItem`, `UnequipItem` and `RemoveItem`. The entity's limits on items are set on its Entity asset.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Iron Sword gives `Item.IronSword`. The entity carries this tag while the item is in its inventory,
equipped or not, and keeps the item's level under it.

Each section below matches the inspector section of the same name. A collapsed section shows one line about what it
holds, such as the level range or how many effects the item grants.

The inspector's header shows the item's one-line summary, as The Forge's Summary column does. The line gives what each
effect it gives changes while it's equipped, by its first impact across its levels, then the ability it grants:
"+20 Armor; Blink".

## Identity

The Identity section names the item, shows its asset tag, and sets how many of it one inventory slot holds.

| Property | Description |
|---|---|
| **Name** | The item's display name, which also gives the asset tag. Renaming the item changes its tag, so update anything that names the old tag. |
| **Description** | Free text for your UI. |
| **Asset Tag** | Read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. |
| **Visibility** | A tag your UI can read for filtering, such as `Always Visible` or `Not Visible`. The Forge lists it, and the runtime doesn't act on it. |
| **Max Stack Size** | How many of the item one inventory slot holds: a number field read at the item's level, as `StackSizeOf(spec)` reads it. At 1 or less, each is a slot of its own. |
| **Textures** | Icons, each paired with a tag. The texture tagged `Primary` is the item's icon; without one, the first texture is used. |

The asset tag is stored when you edit the Name, and the inspector header shows it after the item's type. If the stored
tag differs from the one the Name gives, it shows in warning text with an **Update** button that stores the new one.

Whether an inventory may hold an item more than once is the entity's choice, through its **Allow Duplicate Items**
setting (see [ENTITIES.md](ENTITIES.md)).

### Stacking

An item stacks in one inventory slot, up to its Max Stack Size.

- A give fills the item's stack first, up to its Max Stack Size, and takes no new slot.
- A full stack opens another only with Allow Duplicate Items on. Without it, what doesn't fit isn't taken.
- A slot's count is its container's `ItemStacks`.

Code gives and takes stacks through the entity's item system:

| Method | What it does |
|---|---|
| `ItemSystemComponent.GiveItem(item, count)` | Takes what fits, and returns how many it took. |
| `GiveItem(item, out index)` | Gives one, into the stack if there's room. |
| `RemoveItem(item, count)` | Takes stacks off, from the last slot first. A slot whose last stack comes off goes. |
| `RemoveItem(item)` | Still takes the whole slot. |

A stack applies its effects and its ability once.

- An **Item Stacks** scaler reads the count of the item that applied the effect. So a magnitude of 5 with Multiply
  With Scaler grants +5 a stack.
- As the count changes, an equipped item's effects read it again.
- An effect that reads an item's stacks where no item in the project applies it reads 1, and the Problems view says so.

## Leveling

The Leveling section sets the item's level range. An item has its own level on each entity that holds it, kept under
the item's asset tag. Its Granted Effects take this level, and its Active Ability is granted at it.

A level provider is an asset whose level range another asset follows.

| Property | Description |
|---|---|
| **Link Mode** | **Standalone** (default): keeps the item's own range.<br>**Linked To Provider**: takes the range from a level provider instead. |
| **Starting Level** | The start of the item's own range, 1 by default. It's kept within the range. |
| **Max Level** | The end of the item's own range, 4 by default. |
| **Level Provider** | Shown while linked: the asset the range comes from, an Entity, Ability, Effect, Item or Attribute Set. |

Both levels are at least 1. While linked, Starting Level and Max Level show the range in use, read-only.

- Right-click the **Level Provider** field to pick from one type.
- A link that would make a loop is refused, with an error in the Console.
- While no provider is set, a warning says the item uses its own range.

Two buttons link the item's Granted Effects and Active Ability to it, or unlink them. Each asks first, and one Undo
takes it back.

- **Link Ability & Effects** links the Granted Effects and the Active Ability to this item, so their per-level values
  follow its range.
- **Unlink Ability & Effects** removes the links that point at this item.

A given item starts at its Starting Level, unless code gives it at another level. Its level then stays between that
level and the Max Level. Code changes it through the entity's GAS, with `SetLevel` or `ModifyLevel` and the item's
asset tag.

The link serves the inspector: the Leveling section, and the scalers of effects linked to this item, read the
provider's range.

> [!NOTE]
> At runtime, the item's level comes from its own Starting Level and Max Level, even while it is linked.

## Tags

The Tags section sets the tags that describe the item and the tags it grants.

| Property | Description |
|---|---|
| **Context Tags** | Tags that describe the item, such as `Weapon`. The changes made by effects this item applies carry them as context, and workers can filter by them. |
| **Granted Tags** | Tags applied to the entity while the item is equipped. |

Context Tags are not applied to the entity.

## Effects

The Effects section lists the effects the item applies to the entity while it's equipped.

| Property | Description |
|---|---|
| **Granted Effects** | Effects applied to the entity while the item is equipped. Each is applied with the item as its origin, so it takes the item's level. |

Unequipping removes the effects this item applied. It leaves the same effect alone where something else applied it.

An effect's duration decides how it behaves on an equipped item:

| Duration | What it does |
|---|---|
| Infinite | Lasts while the item is equipped. Use it for bonuses that last while the item is equipped. |
| Instant | Commits once, when the item is equipped. Unequipping doesn't undo it. |
| Durational | Ends when its time runs out, even while the item is still equipped. |

A line under each effect says how it takes its levels, and **Link** makes it take this item's. **New**, beside an empty
field, makes an effect linked to this item.

## Abilities

The Abilities section names the ability the item lends its holder.

| Property | Description |
|---|---|
| **Active Ability** | An ability the entity learns while the item is equipped. It's granted at the item's level when the item is equipped, and removed when the item is unequipped. |

If the entity can't take the ability, because of its Max Abilities or Allow Duplicate Abilities settings, the item is
still equipped, without the ability.

To make the ability's levels follow the item's, use **Link Ability & Effects** in Leveling.

## Components

An item's components are features it carries, each a class with hooks into the item's life. Add one with the list's
**+**. A kind that makes no sense twice is offered only while the item holds none of it.

A component hears these moments:

| Hook | When it runs |
|---|---|
| **Given** | As the item is given to an entity. |
| **Equipped** | As the item is equipped. |
| **Unequipped** | As the item is unequipped. |
| **Removed** | As the item is removed. |
| **Used** | As its holder's use of the item's Active Ability ends, having paid what it costs. |

Its context's `Restoring` says a moment comes from a load. A load puts back what the component's earlier moments did,
such as the effects it applied.

### Auras

An **Aura** component holds an effect on those near its holder while the item is equipped, such as a banner that
inspires the allies round its bearer.

| Property | Description |
|---|---|
| **Effect** | The effect it holds on each one it takes while they're near. |
| **Radius** | How far it reaches from its holder, read at each check. |
| **Flat** | Measures along the ground, leaving height out. Off, it measures a sphere. |
| **Takes** | Whom it takes, by how each stands to its holder, its holder too or not: **Allies** (the default, itself among them), **Other Allies**, **Enemies**, **Self**, **All But Self** or **Everyone**. |
| **Interval** | How often it checks who's near, by its holder's own time: 0.25 s by default. |

[EFFECTS.md](EFFECTS.md#auras) describes how an aura holds its effect.

### Uses

A **Uses** component counts the uses of the item's Active Ability, such as a potion's sips or a wand's charges. Each
use is one that paid what it costs.

| Property | Description |
|---|---|
| **Uses** | How many uses the item has, read at the item's level. |

- With none left, one of the item goes, at the frame's end:
  - a stack of it, its uses whole again for the next;
  - or the item with its last, and its ability with it.
- A load gives its uses whole.
- An item with no Active Ability never counts any, which the Problems view says.
- An item holds one at most.

### Set Bonus

A **Set Bonus** component gives its holder a bonus effect while it has enough of a set's pieces equipped. For example,
two pieces of a set give fire resistance, and four a flame aura.

| Property | Description |
|---|---|
| **Set** | The set the item is a piece of, named by a tag. |
| **Pieces** | How many of the set's pieces the holder needs equipped: a number field read at its holder's level. |
| **Bonus** | The effect the holder has while it has that many equipped. |

Each piece names its Set and carries the set's bonuses, each a Set Bonus component with the Pieces it needs.

- The holder's equipped pieces count each item once.
- A bonus holds while they're as many as it needs: once for the holder, however many pieces carry it.
- A bonus comes from one of the pieces, and moves to another as that one leaves while enough stay.
- A load puts the bonus back with the other effects, and weighs each set once.

### Cues

A **Cues** component plays what the item shows and sounds on its holder.

| Property | Description |
|---|---|
| **On Equip** | Plays as the item is equipped. |
| **While Equipped** | Plays for as long as it's equipped. |
| **On Use** | Plays as it's used, as the ability it lends is. |

- A load that puts an equipped item back begins its While Equipped cues and plays no burst.
- Each list holds cues as an effect's does ([EFFECTS.md](EFFECTS.md#cues)).
- An item holds one at most.

### Writing your own component

A component of your own derives from `AbstractItemComponent` and overrides the hooks it needs. Each hook is a no-op
otherwise.

A component is the item's, shared by every entity that holds the item. So what it keeps for one entity's item, it
keeps through its context: `ctx.GetState<T>(this)`.

## Workers

Workers are reusable behaviours, picked from a list, that react to what happens to an entity. This group is subscribed
to the entity when the item is equipped, and removed when it is unequipped.

| Property | Description |
|---|---|
| **Name** | Labels the group. |
| **Attribute** | Workers that run before or after an attribute changes, for example to reduce incoming damage. |
| **Impact** | Workers that inspect the changes the entity deals. |
| **Tag** | Workers that react when the entity's tags change. |
| **Analysis** | Workers that examine the frame at its end, for example to detect death. |

Each list's **+** adds an empty worker to pick a kind for. The section's import button copies a worker group from
another asset. Above the worker group, the section holds the item's **On Hit** and **Modifiers**.

### On Hit

**On Hit**, above the workers, holds the effects the item applies as its holder's hits land, while it's equipped.
Examples include a dagger's bleed, a hammer's armor break and a barrier on a kill.

| Property | Description |
|---|---|
| **Attribute** | What a hit lowers, such as Health: a hit is a loss to it that the holder deals another entity. None counts the attribute playing the project's Health role ([ATTRIBUTES.md](ATTRIBUTES.md#roles)), where it has one. |
| **Effects** | What it applies. Each effect's own Chance to Apply rolls as it lands, and its hits set the entry off no more, so a shock it applies doesn't shock again. |
| **To** | Whom they land on: the one hit (**Target**), or the **Holder**, as a barrier on a kill does. |
| **Only On Kill** | Only the hit that takes the attribute from above 0 to 0 or below. |
| **Impact Types** | Only hits of one of these types, such as attacks, so a bleed's ticks, typed apart, set off nothing. None takes any hit. |
| **When** | A condition read at the hit, the holder its caster and the one hit its target. |

- A hit's proc coefficient scales the effects' chance and duration, and 0 sets nothing off
  ([EFFECTS.md](EFFECTS.md#on-hit)).
- The Problems view warns of an impact type no effect in the project deals, which never matches.

### Modifiers

**Modifiers**, under On Hit, change other effects as they're made, while the item is equipped.

- They change its holder's own effects, such as a boon that adds a burst to its holder's every Chill.
- They change those landing on it, such as a cloak that halves the stuns its wearer takes.

Each entry is a rule of its own, or names a shared Effect Modifier asset ([EFFECTS.md](EFFECTS.md#modifiers)).

### Lifesteal

An item gives its holder lifesteal through an effect, one of its Granted Effects while it's equipped. The effect raises
the holder's **Lifesteal**: the attribute the project's Lifesteal worker names as its Share, which the Lifesteal worker
heals by ([WORKERS.md](WORKERS.md#lifesteal)).

For example, a vampiric mask's Infinite effect of +0.15 Lifesteal heals its wearer 15% of each blow it deals.

> [!NOTE]
> The item's Lifesteal list is gone.

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

PlayForge doesn't read an item's Local Data itself. Your code and workers can, with
`item.TryGetLocalData(key, out var data)`. The first row with that key wins.

## Used By

The Used By section lists the PlayForge assets that refer to this item, each with its type icon, such as an entity
that starts with it or an effect linked to it. Click a row to select the asset. The list follows the project as it
changes.
