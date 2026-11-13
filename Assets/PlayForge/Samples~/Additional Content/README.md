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

The recipes, the formulas and the starter set are made by Proving Grounds' books, which play each one as a test; its
`RecipeExport` saves them here in place, so a rebuild keeps their GUIDs.
