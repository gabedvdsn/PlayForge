# PlayForge — Items

An **Item** asset describes something an entity can hold and equip, such as a sword or a ring. An entity is anything
that runs a gameplay ability system (GAS), such as the player or an enemy. Giving an item puts it in the entity's
inventory. Equipping it applies what it grants: its Granted Effects, its Active Ability, its Granted Tags and its
workers. Unequipping the item, or removing it from the inventory, takes them away again.

An entity gets items from its Entity asset's Starting Items, or from code through its `ItemSystemComponent`, with
`GiveItem`, `EquipItem`, `UnequipItem` and `RemoveItem`. The entity's limits on items are set on its Entity asset.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Iron Sword gives `Item.IronSword`. The entity carries this tag while the item is in its inventory,
equipped or not, and keeps the item's level under it. Each section below matches the inspector section of the same
name. A collapsed section shows one line about what it holds, such as the level range or how many effects the item
grants.

## Identity

- **Name**: the item's display name. It also gives the asset tag, so renaming the item changes its tag. Update anything
  that names the old tag.
- **Description**: free text for your UI.
- **Asset Tag**: read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. The
  tag is stored when you edit the Name, and the inspector header shows it after the item's type. If the stored tag
  differs from the one the Name gives, it shows in warning text with an **Update** button that stores the new one.
- **Visibility**: a tag your UI can read for filtering, such as `Always Visible` or `Not Visible`. The Forge lists it.
  The runtime doesn't act on it.
- **Textures**: icons, each paired with a tag. The texture tagged `Primary` is the item's icon. Without one, the first
  texture is used.
- **Max Stack Size**: how many of the item one inventory slot holds. 1 or less, and each is a slot of its own.

Whether an inventory may hold an item more than once is the entity's choice, through its **Allow Duplicate Items**
setting (see [ENTITIES.md](ENTITIES.md)).

### Stacking

A give fills the item's stack first, up to its Max Stack Size, and takes no new slot. A full stack opens another
only with Allow Duplicate Items on; without it, what doesn't fit isn't taken. `ItemSystemComponent.GiveItem(item,
count)` takes what fits and returns how many it took, and `GiveItem(item, out index)` gives one, into the stack if
there's room. `RemoveItem(item, count)` takes stacks off, from the last slot first, and a slot whose last stack comes
off goes; `RemoveItem(item)` still takes the whole slot. A slot's count is its container's `ItemStacks`.

A stack applies its effects and its ability once. An **Item Stacks** scaler reads the count of the item that applied
the effect, so a magnitude of 5 with Multiply With Scaler grants +5 a stack. As the count changes, an equipped
item's effects read it again.

## Leveling

An item has its own level on each entity that holds it, kept under the item's asset tag. Its Granted Effects take this
level, and its Active Ability is granted at it. A level provider is an asset whose level range another asset follows.

- **Link Mode**: Standalone (default) keeps the item's own range. Linked To Provider takes the range from a level
  provider instead.
- **Starting Level** and **Max Level**: the item's own range, by default 1 and 4. Both are at least 1, and the
  Starting Level is kept within the range.
- **Level Provider**, while linked: the asset the range comes from, an Entity, Ability, Effect, Item or Attribute Set.
  Right-click the field to pick from one type. A link that would make a loop is refused, with an error in the Console.
  While no provider is set, a warning says the item uses its own range. While linked, Starting Level and Max Level
  show the range in use, read-only.
- **Link Ability & Effects** links the Granted Effects and the Active Ability to this item, so their per-level values
  follow its range. **Unlink Ability & Effects** removes the links that point at this item. Each asks first, and one
  Undo takes it back.

A given item starts at its Starting Level, unless code gives it at another level. Its level then stays between that
level and the Max Level. Code changes it through the entity's GAS, with `SetLevel` or `ModifyLevel` and the item's asset
tag.

The link serves the inspector: the Leveling section, and the scalers of effects linked to this item, read the
provider's range. At runtime, the item's level comes from its own Starting Level and Max Level, even while it is
linked.

## Tags

- **Context Tags** describe the item, such as `Weapon`. The changes made by effects this item applies carry them as
  context, and workers can filter by them. They are not applied to the entity.
- **Granted Tags** are applied to the entity while the item is equipped.

## Effects

**Granted Effects** are applied to the entity while the item is equipped. Each is applied with the item as its origin,
so it takes the item's level. Unequipping removes the effects this item applied. It leaves the same effect alone where
something else applied it.

Use Infinite effects for bonuses that last while the item is equipped. An Instant effect commits once, when the item is
equipped, and unequipping doesn't undo it. A Durational effect ends when its time runs out, even while the item is still
equipped.

A line under each effect says how it takes its levels, and **Link** makes it take this item's. **New**, beside an empty
field, makes an effect linked to this item.

## Abilities

- **Active Ability**: an ability the entity learns while the item is equipped. It is granted at the item's level when
  the item is equipped, and removed when the item is unequipped. If the entity can't take it, because of its Max
  Abilities or Allow Duplicate Abilities settings, the item is still equipped, without the ability.

To make the ability's levels follow the item's, use **Link Ability & Effects** in Leveling.

## Workers

Workers are reusable behaviours, picked from a list, that react to what happens to an entity. This group is subscribed
to the entity when the item is equipped, and removed when it is unequipped. **Name** labels the group. It holds four
lists:

- **Attribute** workers run before or after an attribute changes, for example to reduce incoming damage.
- **Impact** workers inspect the changes the entity deals.
- **Tag** workers react when the entity's tags change.
- **Analysis** workers examine the frame at its end, for example to detect death.

Each list's **+** adds an empty worker to pick a kind for. The section's import button copies a worker group
from another asset.

### On Hit

**On Hit**, above the workers, holds the effects the item applies as its holder's hits land, while it's equipped: a
dagger's bleed, a hammer's armor break, a barrier on a kill (TW-6). Each entry has:

- **Attribute**: what a hit lowers, as Health. A hit is a loss to it that the holder deals another entity. None
  counts the project's Health ([ATTRIBUTES.md](ATTRIBUTES.md#the-projects-attributes)), where it names one (TW-28).
- **Effects**: what it applies. Each effect's own Chance to Apply rolls as it lands, and its hits set the entry off no
  more, so a shock it applies doesn't shock again. A hit's proc coefficient scales their chance and duration, and 0 sets
  nothing off ([EFFECTS.md](EFFECTS.md#on-hit)).
- **To**: the one hit (**Target**), or the **Holder**, as a barrier on a kill does.
- **Only On Kill**: only the hit that takes the attribute from above 0 to 0 or below.
- **Impact Types**: only hits of one of these types, as attacks, so a bleed's ticks, typed apart, set off nothing.
  None takes any hit.
- **When**: a condition read at the hit, the holder its caster and the one hit its target.


### Modifiers

**Modifiers**, under On Hit, change other effects as they're made, while it's equipped: its holder's own, as a boon that adds a
burst to its holder's every Chill, or those landing on it, as a cloak that halves the stuns its wearer takes (TW-7).
Each entry is a rule of its own or names a shared Effect Modifier asset ([EFFECTS.md](EFFECTS.md#modifiers)).
## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag), a **Type**, and a value of
that type: String, Int, Float, Bool, Tag, Attribute, Effect, Ability, Entity, Object, ScriptableObject or MonoBehaviour.
Object fields take project assets, not scene objects.

PlayForge doesn't read an item's Local Data itself. Your code and workers can, with
`item.TryGetLocalData(key, out var data)`. The first row with that key wins.

## Used By

The PlayForge assets that refer to this item, each with its type icon, such as an entity that starts with it or an
effect linked to it. Click a row to select the asset. The list follows the project as it changes.
