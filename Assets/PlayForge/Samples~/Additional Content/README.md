# Additional Content

Content past PlayForge's core, to import where you want it.

## Recipes

Ten common rules, built from PlayForge's own assets and fields, with no code: a curse that bursts, a counter, a crit,
an execute, lifesteal, damage return, can't die, on hit, on kill, and a boss's phase. Each has a folder of its own under
`Recipes`; PlayForge's `Documentation/RECIPES.md` describes each, field by field. They read the Health attribute in
`Recipes/Attributes`: point their fields at your own, or use it.

## Formulas

Scaler assets for a genre's formulas, each named for what it computes (AC-3), under `Formulas`. A field reads one with
its **Shared** kind; a Value Pipeline's stage reads one for each hit.

- `Armor`: **1 ÷ (1 + 0.06 × Armor)** and **100 ÷ (100 + Armor)**, what a hit leaves of itself against its target's
  Armor, for a Multiply stage; **−Armor**, a flat reduction, for an Add stage. They read the Armor attribute in
  `Formulas/Attributes`.
- `Stacking`: an item's stacks through a line, **0.15 × Stacks**; a hyperbola that nears 1, **1 − 1 ÷ (1 + 0.15 ×
  Stacks)**; and a power that falls, **0.9 ^ Stacks**.

## Starter Set

Plain assets named for what they are (AC-5), under `Starter Set`: a **Melee Attack** (10 to the one it's cast at, once
a second) and a **Bolt** (25, once in three seconds), each with its hit and its cooldown; a **Slow** (70% of Movement
Speed for 3 seconds); and a **Shield** (50 that takes its holder's hits on Health first, for 5 seconds, as an Absorb
stage it adds to each effect landing on its holder). They read the recipes' Health and the Movement Speed and Shield
attributes in `Starter Set/Attributes`. The recipes hold the rest of a starter set: a damage over time, an on hit, an
on kill and a boss's phase.

## Feedback

Components and cues that show what PlayForge does (AC-2), in the sample's own assembly,
`FarEmerald.PlayForge.AdditionalContent`, which reads PlayForge, uGUI and TextMeshPro, and which nothing in PlayForge
references (AC-1). TextMeshPro asks to import its Essential Resources the first time one of them draws.

- On an entity, beside its hub, each showing that entity's changes to one attribute, the project's Health role's where
  none is set, with nothing set on any effect:
  - **Floating Numbers**: a number rising over it for each loss and gain, each in its color. A critical hit floats
    larger, in its own color, marked, where the hit's breakdown carries one of its notes, as a Value Pipeline's Chance
    stage leaves "critical".
  - **Hit Flash**: its renderers tinted on each loss, fading back, through property blocks, so their materials stay
    shared.
  - **Hit Sound**: one of its clips on each loss, at most once in a short interval; with none, a click made in code.
  - **Status Bar**: an icon for each effect on it that shows, its own or the first letters of its name, its stacks, and
    a shade of the time it has used; an effect marked Not Visible, as a cooldown, shows none.
- On the camera, **Camera Shaker**: what PlayForge's Camera Shake cue shakes, moving the camera only while it renders.
- On an effect, two cues: **Floating Word**, a word over the entity it lands on, as "Slowed"; and **Flash**, a flash in
  its own color.

An entity of a runtime hub, which no component sits on, is shown through a component's `Watch(entity)`.

## Sparring

`Sparring/Sparring.unity`: two dummies, each a Sparring Dummy with every feedback component on, trade the starter set's
melee attack and bolt, land its slow on each other and its shield on themselves, and mend before they fall. The
starter set's Bolt flashes its target blue and shakes the camera; its Slow and Shield float their words. Its `Sparring`
component is the dummies' own.

The recipes, the formulas, the starter set and the Sparring scene are made by Proving Grounds' books, which play each
one as a test; its `RecipeExport` saves them here in place, so a rebuild keeps their GUIDs. The sample's own tests,
in `Scripts/Tests`, play its feedback and its scene.
