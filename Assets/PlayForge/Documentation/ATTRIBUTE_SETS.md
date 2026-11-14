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
sub-sets. Each section below matches the inspector section of the same name. A collapsed section shows one line
about what it holds, such as the level range or how many attributes the set gives.

## Identity

- **Name**: the set's display name. It also gives the asset tag, so renaming the set changes its tag. Update anything
  that names the old tag.
- **Description**: free text for your UI.
- **Asset Tag**: read-only, made from the Name: punctuation is dropped, and each word is capitalised and joined. The
  tag is stored when you edit the Name, and the inspector header shows it after the set's type. If the stored tag
  differs from the one the Name gives, it shows in warning text with an **Update** button that stores the new one.
- **Textures**: icons, each paired with a tag. The texture tagged `Primary` is the set's icon. Without one, the first
  texture is used.

## Leveling

The set's level range is the one the cached scalers on its elements are authored for. A cached scaler is a value source
for an element, such as a table with one value per level. At runtime those scalers follow the level of the entity that
owns the set. Linking the set to that Entity asset gives the scalers one value per entity level. A level provider is an
asset whose level range another asset follows.

- **Link Mode**: Standalone (default) keeps the set's own range. Linked To Provider takes the range from a level
  provider instead.
- **Starting Level** (default 1) and **Max Level** (default 4): the set's own range. Cached scalers on its elements
  whose Level Configuration is Lock To Level Provider use the Max Level to size their per-level values.
- **Level Provider**, while linked: the asset the range comes from, an Entity, Ability, Effect, Item or Attribute Set.
  Right-click the field to pick from one type. A link that would make a loop is refused, with an error in the Console.
  While no provider is set, a warning says the set uses its own range. While linked, Starting Level and Max Level show
  the range in use, read-only.

## Attributes

Each element sets up one attribute. Folded, an element shows one line: what its attribute starts at, and its range.

- **Preview In**, above the list while another set holds this one as a sub-set: This Set, or a set that holds it,
  directly or through another. An entity built from that set starts with these attributes as part of it, so the
  elements and All Attributes show their values there, with that set's collisions and the attributes it gives. The
  choice lasts until the editor closes.
- **Attribute** (in the element's row): the attribute asset.
- **Starts At**: what the attribute starts at, at each of the set's levels, as "100/120/140/160", with the levels on the
  right; Current and Base apart, as "Current 50 · Base 80", when they differ; or a range past six levels. It is worked
  out as the runtime works it out: an entity at each level, built from the set, with its sub-sets, collisions, scalers,
  bounds and rounding, before any effect, item or worker acts on it. A note says when another element's value is the
  one used, and a warning says when a scaler reads an attribute the set doesn't give, which reads 0.
- **Base** and **Current**: each a magnitude field, as an effect's are: a number, or a cached scaler with how the number
  combines with it. One kind of scaler follows the entity's level. Another follows a different attribute, and this
  attribute is then worked out again whenever that one changes. Each field's menu copies the other side's number and
  scaler into it.
- **Link Current to Base**: Current takes Base's number and scaler, and its own field hides.
- **Bounds**: the range the values are held in, with the Floor and Ceiling the policy reads, each a Current and a Base.
  - **Zero To Base**: Current stays between 0 and Base.
  - **Floor To Base**: Current stays between the Floor's Current and Base. Base stays at or above the Floor's Base.
  - **Zero To Ceil**: Current stays between 0 and the Ceiling's Current. Base stays between 0 and the Ceiling's Base.
  - **Floor To Ceil**: Current and Base each stay between their Floor and Ceiling.
  - **Unlimited**: no bounds.

  Right-click a Floor or a Ceiling for No Floor or No Ceiling. A magnitude you type is kept within the bounds. A stored
  magnitude outside them, as after the bounds change, shows a warning, and the runtime clamps it.
- **Auto Clamp**: apply the Bounds. With it off, the Bounds are ignored at runtime.
- **Scale With Base**: when Base changes, change Current by the same share. A value of 80/100 that loses half its Base
  ends at 40/50.
- **Rounding**: None, To Floor, To Ceil, Round, or Snap To with a **Snap Interval**, a number field read at its owner's
  level (FN-19). Rounding applies after the Bounds,
  each time the value changes, so it can make a change bigger or smaller than its effect asked for.
- **Retention Group**: the group the root value counts toward. Groups split an attribute's value for queries and UI,
  such as base and bonus health. They never change the math.
- **Collision**: what to do when this attribute appears more than once across the set and its sub-sets. See Collision
  Settings.

## Sub-Sets

Other attribute sets this set includes. The entity gets their attributes as well, and those of their own sub-sets in
turn. Their asset tags are applied to the entity, and their workers are subscribed with this set's. When an attribute
appears more than once, Collision Settings decide which value it starts with. Only the Collision Settings of the set
the entity names are used.

Don't let a set include itself, directly or through another set: nothing stops the loop. All Attributes lists each
attribute the set gives, sub-sets included.

## Collision Settings

These settings decide an attribute's root value when it appears in more than one element, across this set and its
sub-sets. Elements are taken in order: this set's elements first, then each sub-set's in turn.

First, each element's **Collision** choice puts it in a group. One group decides, picked in this order: Use Set
Collision Setting, Use This, Combine, Use Existing. Elements in the other groups are ignored.

- In the **Combine** group, the values are added up. Bounds, rounding and the retention group come from the first
  element.
- The other groups choose by the **Resolution Policy** here:
  - **Use Maximum** (default): the element with the highest Base magnitude.
  - **Use Minimum**: the element with the lowest Base magnitude.
  - **Use Average**: the average of the elements' values. Bounds and rounding come from the first element.
  - **Use First** and **Use Last**: the first or the last element.
  - **Ignore**: the first element stands.

## All Attributes

A table of each attribute the set gives an entity, its sub-sets' included, once each: the value its Current starts at,
at each level, and the range the set keeps it in. The values are the ones the elements show, in the set this set is
previewed in. Hover over a value for its Current and Base at each level, and, where several elements give an
attribute, whose value is used. The Entity inspector shows the same table under the entity's Attribute Set, at the
entity's levels.

## Workers

Workers are reusable behaviours, picked from a list, that react to what happens to an entity. This group is subscribed
when the entity's GAS initializes, with each sub-set's group, and stays for the entity's lifetime. **Name** labels the
group. It holds four lists:

- **Attribute** workers run before or after an attribute changes, for example to reduce incoming damage.
- **Impact** workers inspect the changes the entity deals.
- **Tag** workers react when the entity's tags change.
- **Analysis** workers examine the frame at its end, for example to detect death.

Each list's **+** adds an empty worker to pick a kind for. The section's import button copies a worker group
from another asset.

## Local Data

Local Data is a list of named values stored on the asset. Each row has a **Key** (a tag), a **Type**, and a value of
that type: String, Int, Float, Bool, Tag, Attribute, Effect, Ability, Entity, Object, ScriptableObject or MonoBehaviour.
Object fields take project assets, not scene objects.

PlayForge doesn't read an attribute set's Local Data itself. Your code and workers can, with
`set.TryGetLocalData(key, out var data)`. The first row with that key wins.

## Used By

The PlayForge assets that refer to this set, each with its type icon, such as an entity built from it or a set that
holds it as a sub-set. Click a row to select the asset. The list follows the project as it changes.
