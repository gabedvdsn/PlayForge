# PlayForge — Scalers

A **scaler** is a value source that a magnitude field reads at a level, such as a table of damage per level. The
level is the origin's, such as the level of the ability that applies an effect. Each section below matches what the
inspector shows.

## A magnitude field

A magnitude field, such as an effect's Magnitude or Duration, shows its name, then its **kind**, then what it reads,
then its field menu, which opens as wide as its items:

- **Constant**: the field reads its number, with its unit beside it, as Tick Every's "1 s". A field that used to read
  its scaler, and has none, reads 0, and a warning says so.
- **By Level**: the field reads a table of values per level (below). The range it gives shows beside the kind, as
  "6–8 s", or "-25 to -10" where it goes below 0.
- **Shared**: the field reads a Scaler asset (below).
- The other kinds work out a value from the values or from the entity: **Attribute Backed** reads an attribute,
  **Clamped** keeps a value within bounds, **Conditional** chooses between two scalers, **Randomized** rolls a random
  value, **Group** combines scalers, **Threshold** steps at thresholds, and **Fixed Value** is one number. **Effect
  Stacks** reads the stack count of the effect's own container, and **Item Stacks** that of the item that applied it
  ([ITEMS.md](ITEMS.md)); each reads 1 where there's nothing stacked. **Set By Caller** reads the magnitude the
  code set under its **Key** as the effect was applied ([EFFECTS.md](EFFECTS.md)), or its **Default** where none was
  set. An attribute set's values use the cached kinds,
  which read other attributes as the entity's values change.

Choosing a kind is one Undo step. A constant made By Level keeps its number as its first level's value.

With more than one asset selected, the field edits each asset's own field. Kinds that differ show as mixed, and choosing
one gives it to each field, from that field's own value; a field of that kind keeps its scaler. Levels and Make Constant
work on each field's own values, and a count, range or curve the fields disagree on shows as mixed. Save as Scaler Asset
waits for one asset.

A kind other than Constant shows **Combine**, how the field joins its number with the scaler: **Scaler** reads the
scaler's value, **Scaler + Number** and **Scaler × Number** add or multiply the field's number, and **Number Only**
reads the number. A whole-number field, such as Ticks, also shows its **Rounding**.

The field menu holds:

- **Save as Scaler Asset…**: a name and a folder, then **Save and Use**, which saves the field's scaler as a Scaler
  asset and makes the field share it.
- **Use a Scaler Asset…**: a list of the project's Scaler assets. The field shares the one you pick.
- **Import Values…**: a window that lists the Scaler assets and the other assets' fields with values per level. Pick
  one to compare its values with the field's, level by level, with both curves, then **Copy Values**. A shorter table's
  last value holds for the levels past its end, and the window says which levels those are. One Undo takes it back.
- **Quick Fill…**: fills the table from a pattern, such as a straight line from a start to an end.
- **Make Constant**: the field reads a number again, the value its first level read.

## Values per level

A scaler's settings sit on a shaded block under its field, so they read as the scaler's among the fields around them.
A kind shows only what it reads: a **Fixed Value** shows its value, and a **Randomized** scaler its variance, then the
values its base value comes from. A kind's field that applies only to some of its settings shows only for them, as a
Randomized scaler's **Min Multiplier** and **Max Multiplier** for its Multiplier mode.

- **Values**: **Per Level** or **Spread Over Levels**.
  - **Per Level**: one value per level, ten levels to a row. Level 1 reads the first value, level 4 the 4th, and a
    level past the end reads the last.
  - **Spread Over Levels**: the values are points on a curve over a level range, and a level between two points reads
    between them. Three values, 10, 20 and 30, spread over levels 1 to 5 read 10, 15, 20, 25 and 30. Each value says
    the level it sits at. A scaler you make in the inspector starts Per Level. The scalers saved before the choice
    existed spread, so they read as they did.
- **Levels**, Per Level: an asset with a level provider, as an ability, or an effect linked to one, follows the
  provider's levels, as **Fireball's levels (1–4)**, with a value for each. A level past the scaler's values shows the
  last in secondary text; type there to give it a value of its own. **Own levels** gives the scaler a count of its own.
  An asset whose levels come from whatever uses it, as an unlinked effect, an attribute set or a Scaler asset, shows
  the count alone. A new level takes the last value.
- **Over Levels**, spread: the provider's levels, **Own levels** up to the scaler's own **Max Level**, or the
  provider's **up to a level**, the lower of the Max Level and the provider's level. **Points**: how many values.
- **Between Points**, spread: **Steps**, **Straight Line** or **Smooth Curve**.
- A read-only curve beside the table shows the values, and spread, the curve they make. Its tooltip reads them, as "6
  at level 1, rising to 8 at level 5".
- **Behaviours**: rules that adjust the value the scaler works out, in order.

Quick Fill fills the levels the table shows. Nothing changes a scaler's values while it's drawn: its table keeps its
length until you change it.

## Writing your own scaler

A scaler of your own derives from `AbstractScaler`, or from `AbstractCachedScaler` for an attribute set's values, and
has a public constructor with no parameters. The Kind menu lists it by its name without "Scaler", as **Damage Curve**
for `DamageCurveScaler`. Its inspector draws:

- Each field it adds, public or `[SerializeField]`, as Unity draws the field's type, through any property drawer you
  write for that type. The fields of an abstract base of your own draw too. `[HideInInspector]` keeps a field out, and
  `[ForgeShowWhen(nameof(Mode), EMode.Curved)]` shows one only while another field, an enum or a bool, holds one of
  the values given.
- The parts every scaler has, as its `Parts` say: its values per level (`EScalerParts.LevelValues`), the level range
  they spread over (`EScalerParts.LevelRange`), and its behaviours (`EScalerParts.Behaviours`). Unless you override it,
  a scaler reads all three, or its behaviours alone where its `UseScalingOptions()` returns false. Override `Parts` to
  say what yours reads, as a scaler that reads only an attribute returns `EScalerParts.Behaviours`. `Parts` can follow
  the scaler's own fields, as a Conditional reads its own values only while it has no True scaler.

## Scaler assets

A **Scaler asset** (`Create → PlayForge → Scaler`) holds one scaler that many fields can use. A field of the
**Shared** kind reads its **Asset**'s scaler, so an edit to the asset changes every field that uses it, with nothing
to copy or propagate. **Open** shows the asset in a window of its own. Under the field, the asset's curve shows beside
its range, as "6–8, levels 1–5", and how many fields share it. A field adjusts the shared values only through its own
number and Combine: Scaler × Number with a number of 1.5 reads the asset's values times 1.5, as in Fire Duration × 1.5.

- The asset's own inspector shows its name, its kind and how many fields use it, its **Description**, its **Values**,
  and **Used By**: each field that shares it, by its asset and field. Click a row to select the asset.
- Any magnitude field can share a Scaler asset. An attribute set's element shares one whose scaler is a cached kind,
  and its Use a Scaler Asset lists only those. A Scaler asset can't hold a Shared scaler of its own.
- A field whose asset is missing reads as a field with no scaler, and an element whose asset is missing reads its root
  value. A chain of shared scalers that comes back to its own asset stops after eight steps, rather than run forever.

The Scaler asset was the Scaler Template, whose link and sync settings nothing read, so those settings are gone. So
are the old scaler window's curve editor and import window: a scaler's values are its table, and Import Values
compares them before it copies.
