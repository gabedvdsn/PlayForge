# PlayForge — Attribute Sets

An **Attribute Set** asset lists the attributes an entity starts with, and how each one starts: its Current and Base
values, its bounds and its rounding. An entity is anything that runs a gameplay ability system (GAS), such as the
player or an enemy. Its Entity asset names one set.

When the GAS initializes, it builds every attribute in the set and in its sub-sets. Each element becomes its
attribute's *root value*, the value before any effect. Gameplay effects then change the attribute on top of that root.

A root can follow the entity's level or another attribute, through a cached scaler. It is worked out again whenever the
attribute is recalculated.

Tags are names, such as `State.Stunned`, that an entity carries and that requirements check. Each asset stores a tag
made from its Name: Hero Stats gives `AttributeSet.HeroStats`. The entity carries the set's tag and the tags of its
sub-sets.

Each section below matches the inspector section of the same name. A collapsed section shows one line about what it
holds, such as the level range or how many attributes the set gives.

## Identity

The Identity section names the set, shows its asset tag and holds its icons.

| Property | Description |
|---|---|
| **Name** | The set's display name, which also gives the asset tag. Renaming the set changes its tag, so update anything that names the old tag. |
| **Description** | Free text for your UI. |
| **Asset Tag** | Read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. |
| **Textures** | Icons, each paired with a tag. The texture tagged `Primary` is the set's icon; without one, the first texture is used. |

The asset tag is stored when you edit the Name, and the inspector header shows it after the set's type. If the stored
tag differs from the one the Name gives, it shows in warning text with an **Update** button that stores the new one.

## Leveling

The Leveling section sets the level range the cached scalers on the set's elements are authored for. A cached scaler
is a value source for an element, such as a table with one value per level.

At runtime, those scalers follow the level of the entity that owns the set. Linking the set to that Entity asset gives
the scalers one value per entity level. A level provider is an asset whose level range another asset follows.

| Property | Description |
|---|---|
| **Link Mode** | **Standalone** (default): keeps the set's own range.<br>**Linked To Provider**: takes the range from a level provider instead. |
| **Starting Level** | The start of the set's own range, 1 by default. |
| **Max Level** | The end of the set's own range, 4 by default. Cached scalers on its elements whose Level Configuration is Lock To Level Provider use it to size their per-level values. |
| **Level Provider** | Shown while linked: the asset the range comes from, an Entity, Ability, Effect, Item or Attribute Set. |

- Right-click the **Level Provider** field to pick from one type.
- A link that would make a loop is refused, with an error in the Console.
- While no provider is set, a warning says the set uses its own range.
- While linked, Starting Level and Max Level show the range in use, read-only.

## Attributes

The Attributes section lists the set's elements, each of which sets up one attribute. Folded, an element shows one
line: what its attribute starts at, and its range.

| Property | Description |
|---|---|
| **Attribute** | The attribute asset, in the element's row. |
| **Starts At** | What the attribute starts at, at each of the set's levels. |
| **Base** | A magnitude field, as an effect's are: a number, or a cached scaler with how the number combines with it. |
| **Link Current to Base** | Current takes Base's number and scaler, and its own field hides. |
| **Current** | A magnitude field, as Base is. |
| **Bounds** | The range the values are held in, with the Floor and Ceiling the policy reads, each a Current and a Base. |
| **Auto Clamp** | Applies the Bounds. With it off, the Bounds are ignored at runtime. |
| **Scale With Base** | When Base changes, changes Current by the same share. A value of 80/100 that loses half its Base ends at 40/50. |
| **Rounding** | None, To Floor, To Ceil, Round, or Snap To with a **Snap Interval**, a number field read at its owner's level. |
| **Retention Group** | The group the root value counts toward. Groups split an attribute's value for queries and UI, such as base and bonus health; they never change the math. |
| **Collision** | What to do when this attribute appears more than once across the set and its sub-sets. See Collision Settings. |

- One kind of cached scaler follows the entity's level. Another follows a different attribute, and this attribute is
  then worked out again whenever that one changes.
- The Base and Current fields' menus each copy the other side's number and scaler into the field.
- Rounding applies after the Bounds, each time the value changes. So it can make a change bigger or smaller than its
  effect asked for.

### Starts At

**Starts At** shows what an element's attribute starts at, at each of the set's levels, with the levels on the right.

- One value a level, as "100/120/140/160".
- Current and Base apart, as "Current 50 · Base 80", when they differ.
- A range, past six levels.

It's worked out as the runtime works it out: an entity at each level, built from the set, with its sub-sets,
collisions, scalers, bounds and rounding, before any effect, item or worker acts on it.

- A note says when another element's value is the one used.
- A warning says when a scaler reads an attribute the set doesn't give, which reads 0.

### Bounds

An element's **Bounds** set the range its values are held in, with the Floor and Ceiling the policy reads, each a
Current and a Base.

| Policy | What it holds |
|---|---|
| **Zero To Base** | Current stays between 0 and Base. |
| **Floor To Base** | Current stays between the Floor's Current and Base. Base stays at or above the Floor's Base. |
| **Zero To Ceil** | Current stays between 0 and the Ceiling's Current. Base stays between 0 and the Ceiling's Base. |
| **Floor To Ceil** | Current and Base each stay between their Floor and Ceiling. |
| **Unlimited** | No bounds. |

- Right-click a Floor or a Ceiling for No Floor or No Ceiling.
- A magnitude you type is kept within the bounds.
- A stored magnitude outside them, as after the bounds change, shows a warning, and the runtime clamps it.
- With **Auto Clamp** off, the Bounds are ignored at runtime.

### Preview In

**Preview In** shows a set's elements as part of a set that holds it. It appears above the list while another set holds
this one as a sub-set.

- Choose This Set, or a set that holds it, directly or through another.
- An entity built from that set starts with these attributes as part of it. So the elements and All Attributes show
  their values there, with that set's collisions and the attributes it gives.
- The choice lasts until the editor closes.

## Sub-Sets

The Sub-Sets section lists other attribute sets this set includes.

| Property | Description |
|---|---|
| **Sub-Sets** | Other attribute sets this set includes, one a row. The entity gets their attributes as well, and those of their own sub-sets in turn. |

- Their asset tags are applied to the entity, and their workers are subscribed with this set's.
- When an attribute appears more than once, Collision Settings decide which value it starts with.
- Only the Collision Settings of the set the entity names are used.
- All Attributes lists each attribute the set gives, sub-sets included.

> [!NOTE]
> Don't let a set include itself, directly or through another set: nothing stops the loop.

## Collision Settings

These settings decide an attribute's root value when it appears in more than one element, across this set and its
sub-sets.

| Property | Description |
|---|---|
| **Resolution Policy** | How the deciding group picks a value, unless it's the Combine group. Use Maximum by default. |

The root value is decided in three steps:

1. Elements are taken in order: this set's elements first, then each sub-set's in turn.
2. Each element's **Collision** choice puts it in a group.
3. One group decides, picked in this order: Use Set Collision Setting, Use This, Combine, Use Existing. Elements in the
   other groups are ignored.

In the **Combine** group, the values are added up. Bounds, rounding and the retention group come from the first
element.

The other groups choose by the **Resolution Policy**:

| Policy | What it picks |
|---|---|
| **Use Maximum** (default) | The element with the highest Base magnitude. |
| **Use Minimum** | The element with the lowest Base magnitude. |
| **Use Average** | The average of the elements' values. Bounds and rounding come from the first element. |
| **Use First** and **Use Last** | The first or the last element. |
| **Ignore** | The first element stands. |

## All Attributes

All Attributes is a table of each attribute the set gives an entity, its sub-sets' included, once each. It shows the
value its Current starts at, at each level, and the range the set keeps it in.

- The values are the ones the elements show, in the set this set is previewed in.
- Hover over a value for its Current and Base at each level. Where several elements give an attribute, the hover also
  says whose value is used.

The Entity inspector shows the same table under the entity's Attribute Set, at the entity's levels.

## Workers

Workers are reusable behaviours, picked from a list, that react to what happens to an entity. This group is subscribed
when the entity's GAS initializes, with each sub-set's group, and stays for the entity's lifetime.

| Property | Description |
|---|---|
| **Name** | Labels the group. |
| **Attribute** | Workers that run before or after an attribute changes, for example to reduce incoming damage. |
| **Impact** | Workers that inspect the changes the entity deals. |
| **Tag** | Workers that react when the entity's tags change. |
| **Analysis** | Workers that examine the frame at its end, for example to detect death. |

Each list's **+** adds an empty worker to pick a kind for. The section's import button copies a worker group from
another asset.

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

PlayForge doesn't read an attribute set's Local Data itself. Your code and workers can, with
`set.TryGetLocalData(key, out var data)`. The first row with that key wins.

## Used By

The Used By section lists the PlayForge assets that refer to this set, each with its type icon, such as an entity
built from it or a set that holds it as a sub-set. Click a row to select the asset. The list follows the project as it
changes.
