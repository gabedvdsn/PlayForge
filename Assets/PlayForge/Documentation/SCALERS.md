# PlayForge — Scalers

A **scaler** is a value source that a magnitude field reads at a level, such as a table of damage per level. A field
such as an effect's Magnitude or Duration combines its own number with its scaler through its Real Magnitude: Add
Scaler adds them, Multiply With Scaler multiplies them, Use Magnitude ignores the scaler and Use Scaler ignores the
number ([EFFECTS.md](EFFECTS.md)). The level is the origin's, such as the level of the ability that applies an effect.

## Values per level

- **Level Values**: one value per level. Level 1 reads the first value, level 4 the 4th, and a level past the end reads
  the last.
- **Stretch Over Levels**: on, the values spread over a level range instead, and a level between two values reads
  between them. Three values, 10, 20 and 30, stretched over levels 1 to 5 read 10, 15, 20, 25 and 30. A scaler you make
  in the inspector starts with it off. The scalers saved before the option existed have it on, so they read as they
  did.
  - **Level Configuration**, while it stretches, names the range: Lock To Level Provider uses the provider's levels,
    Unlocked the scaler's own **Max Level**, and Clamped the lower of the Max Level and the provider's level.
  - **Interpolation** says how a level between two values reads: Constant, Linear or Smooth.

Nothing changes a scaler's values while it's drawn: its table keeps its length until you change it.

## Kinds

A field's kind decides how its scaler works out a value. **Simple** reads its values at the level. **Shared** reads a
Scaler asset's scaler (below). The others build on the values or on the entity: a scaler can read an attribute, keep a
value within bounds, choose between two scalers, roll a random value, combine scalers, or step at thresholds. An
attribute set's values use the cached kinds, which read other attributes as the entity's values change.

## Scaler assets

A **Scaler asset** (`Create → PlayForge → Scaler`) holds one scaler that many fields can use. A field of the
**Shared** kind reads its **Asset**'s scaler, so an edit to the asset changes every field that uses it, with nothing
to copy or propagate. A field adjusts the shared values only through its own number and Real Magnitude: Multiply With
Scaler with a number of 1.5 reads the asset's values times 1.5, as in Fire Duration × 1.5.

- Effects' and abilities' fields can share a Scaler asset. An attribute set's cached kinds can't yet.
- A field whose asset is missing reads as a field with no scaler. A chain of shared scalers that comes back to its own
  asset reads 0, rather than run forever.

The Scaler asset was the Scaler Template, whose link and sync settings nothing read. A field that shares an asset reads
it directly, so those settings are gone.
