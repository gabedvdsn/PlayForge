# PlayForge — Recipes

These ten recipes build common rules from PlayForge's own assets and fields, with no code.

- Each is in the **Additional Content** sample (Package Manager → PlayForge → Samples), in a folder of its own under
  `Recipes`.
- Each reads the sample's **Health** attribute, in `Recipes/Attributes`. Point its fields at your own.
- [WORKERS.md](WORKERS.md) maps the workers some of them use.

## A curse that bursts

This curse deals 5 each second for 3 seconds, then 30 as it ends, whether it runs out or comes off.

- **Curse** (an effect):
  - Durational, with **Duration** 3.
  - **Periodic Ticks** on, with **Ticks** 3.
  - Its impact: -5 Health, which lands each tick.
  - Under its impact's contained effects: **Curse Burst**, **On Remove**, landing on its **Target**.
- **Curse Burst** (an effect): Instant, -30 Health.

## A counter

A counter answers each loss another entity deals to its holder: it strikes back at that entity for 10.

- **Retaliate** (an ability):
  - One stage, with **Apply Effects** applying Retaliation.
  - Under Activation's **Triggers**, one trigger: **Gameplay Event**, the core's **Impact Taken**, with **Attribute**
    Health, **Sign** Loss and **Target** Instigator.
- **Retaliation** (an effect): Instant, -10 Health.

The counter's own hit is a loss to the attacker, not to its holder, so it doesn't set itself off.

- For a chance, as Counter Helix's 20%, set the trigger's **Chance**.
- To strike everyone near, give the ability Auto Target, and leave the trigger's Target at Own.

## A crit

A crit doubles a hit on a 25% chance.

- **Critical Strike** (a Value Pipeline): one **Chance** stage, with **Chance** 0.25, **Multiplier** 2 and **Note**
  "critical".
- **Crit Blow** (an effect): Instant, -20 Health, with its impact's **Pipeline** set to Critical Strike.

The hit's breakdown notes the crit, as the combat log shows it. The stage's **Roll** sets pseudo-random and luck.

## An execute

With an execute, a hit that would leave its target under a fifth of its Health takes the rest.

- **Execute** (a Value Pipeline): one **Execute** stage, with **Share** 0.2 and **Attribute** Health.
- **Culling Blow** (an effect): Instant, -15 Health, with its impact's **Pipeline** set to Execute.

## Lifesteal

While Vampirism lasts, its holder heals half of each loss it deals another.

- **Lifesteal** (an attribute): the **Share** of the Lifesteal worker in your Forge Project's **Workers**. Your Forge
  Project has Health in its **Health** role.
- **Vampirism** (an effect): Infinite, +0.5 Lifesteal.

The Lifesteal worker heals by Lifesteal. It's the one your Forge Project's Workers give every entity as a project
starts, and it heals the Health role's attribute ([WORKERS.md](WORKERS.md#lifesteal)).

An effect has no lifesteal of its own:

- For a blow that steals more than others, an effect raises its caster's Lifesteal while it should.
- An item gives Lifesteal with such an effect, as one of its Granted Effects ([ITEMS.md](ITEMS.md#lifesteal)).

## Damage return

While Thorns lasts, half of each loss its holder takes goes back to the one who dealt it.

- **Thorns** (an effect): Infinite. Its **Worker Group** holds one attribute worker, **Return Damage**, with
  **Target Attribute** Health and **Share** 0.5.

What it returns is typed Returned, so nothing returns it again, and no lifesteal heals from it.

## Can't die

While Undying lasts, no blow takes its target below 1, as Shallow Grave does.

- **Undying** (an effect): Durational, **Duration** 5. Its **Worker Group** holds one attribute worker,
  **Can't Fall Below**, with **Target Attribute** Health and **Floor** 1.

## On hit

While Frost Blade is equipped, each hit its holder deals bites for 5 more.

- **Frost Blade** (an item): its **On Hit** has one entry, with **Attribute** Health, **Effects** Frost Bite and
  **To** Target.
- **Frost Bite** (an effect): Instant, -5 Health.

Frost Bite's own hit sets nothing off again.

## On kill

While Reaper's Mark is equipped, the hit that takes a target's Health to 0 feeds its holder 20.

- **Reaper's Mark** (an item): its **On Hit** has one entry, with **Attribute** Health, **Effects** Feast, **To**
  Holder and **Only On Kill** on.
- **Feast** (an effect): Instant, +20 Health.

## A phase

As the boss's Health falls to half, it enrages, once.

- **Boss** (an attribute set):
  - Health 1000.
  - In its **Worker Group**, one analysis worker, **Threshold**, with **Target** Health, **What** Share,
    **Threshold** 0.5, **Crossing** Falling, **Trigger Once** on and **Effects** Enrage.
- **Enrage** (an effect): Infinite, granting `Recipe.Enraged`.

The phase's own moves start from the tag:

- an ability with a **Tag Added** trigger on `Recipe.Enraged`;
- or a stage condition that reads the tag.

The worker's **Event** sends a gameplay event as well, for a trigger of its own.

## Formulas

The sample holds Scaler assets for a genre's formulas, under `Formulas`, each named for what it computes. A field reads
one with its **Shared** kind ([SCALERS.md](SCALERS.md)). Each is a **Stacking Curve** of a count, with an **a**.

| Scaler asset | Curve | What it gives |
|---|---|---|
| **1 ÷ (1 + 0.06 × Armor)** | Reciprocal, **a** 0.06, of the hit's target's Armor | In a Value Pipeline's **Multiply** stage, a hit leaves all of itself at 0 Armor, a half at about 17, and never none. |
| **100 ÷ (100 + Armor)** | Reciprocal, **a** 0.01 | A half at 100 Armor, a third at 200. |
| **−Armor** | Linear, **a** −1 | In an **Add** stage, a blow of 100 against 20 Armor lands as 80. |
| **0.15 × Stacks** | Linear, **a** 0.15, of an item's stacks | A bonus that grows by as much with each stack. |
| **1 − 1 ÷ (1 + 0.15 × Stacks)** | Hyperbolic | A chance that nears 1 and never reaches it: 13% with one stack, 60% with ten. |
| **0.9 ^ Stacks** | Exponential | A share that falls by a tenth with each stack. |

The Armor ones read the **Armor** attribute in `Formulas/Attributes`, as the hit lands. Point them at your own.

## A starter set

The sample holds plain assets named for what they are, under `Starter Set`. The recipes above hold the rest of a starter
set: a damage over time, an on hit, an on kill and a boss's phase. Each hit reads the recipes' Health.

- **Melee Attack** (an ability): **Apply Effects** with Melee Hit, on what it's cast at, and **Cooldown** Melee
  Cooldown.
- **Melee Hit** (an effect): Instant, -10 Health.
- **Melee Cooldown** (an effect): Durational, 1 s.
- **Bolt** (an ability): the same as Melee Attack, with its **Bolt Hit** of -25 Health and its **Bolt Cooldown** of
  3 s.
- **Slow** (an effect): Durational, 3 s: a **Multiply** of Movement Speed by 0.7, undone as it ends.
- **Shield** (an effect):
  - Durational, 5 s.
  - +50 to Shield's current and base while it lasts.
  - Its **Modifiers** hold one **Incoming** rule, whose **Change Impact** on Health adds an **Absorb** stage into
    Shield. Each hit on its holder's Health then comes off its Shield first.

Give Shield's element a base of 0, between 0 and its base, so Shield is 0 without the effect.

The starter set reads the **Movement Speed** and **Shield** attributes in `Starter Set/Attributes`.
