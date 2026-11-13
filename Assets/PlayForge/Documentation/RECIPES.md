# PlayForge — Recipes

Ten common rules, each built from PlayForge's own assets and fields, with no code (TW-23). Each is in the **Additional
Content** sample (Package Manager → PlayForge → Samples), under `Recipes`, a folder each, and Proving Grounds plays each
as a test, built from the same recipe book as the sample's assets, so this page and the assets stay true. Each reads
the sample's **Health** attribute, in `Recipes/Attributes`; point its fields at your own. [WORKERS.md](WORKERS.md) maps
the workers some of them use.

## A curse that bursts

It deals 5 each second for 3 seconds, then 30 as it ends, whether it runs out or comes off.

- **Curse** (an effect): Durational, **Duration** 3, **Periodic Ticks** on, **Ticks** 3; its impact -5 Health, which
  lands each tick. Under its impact's contained effects, **Curse Burst**, **On Remove**, landing on its **Target**.
- **Curse Burst** (an effect): Instant, -30 Health.

## A counter

Each loss another entity deals its holder sets it off, and it strikes back at that one for 10.

- **Retaliate** (an ability): one stage, **Apply Effects** with Retaliation. Under Activation's **Triggers**, one trigger:
  **Gameplay Event**, the core's **Impact Taken**, **Attribute** Health, **Sign** Loss, **Target** Instigator.
- **Retaliation** (an effect): Instant, -10 Health.

Its own hit is a loss to the attacker, not to its holder, so it doesn't set itself off. For a chance, as Counter
Helix's 20%, set the trigger's **Chance**; to strike everyone near, give the ability Auto Target and leave the trigger's
Target at Own.

## A crit

A hit doubles on a 25% chance.

- **Critical Strike** (a Value Pipeline): one **Chance** stage, **Chance** 0.25, **Multiplier** 2, **Note** "critical".
- **Crit Blow** (an effect): Instant, -20 Health, its impact's **Pipeline** Critical Strike.

The hit's breakdown notes the crit, as the combat log shows it (TW-22). The stage's **Roll** sets pseudo-random and luck.

## An execute

A hit that would leave its target under a fifth of its Health takes the rest.

- **Execute** (a Value Pipeline): one **Execute** stage, **Share** 0.2, **Attribute** Health.
- **Culling Blow** (an effect): Instant, -15 Health, its impact's **Pipeline** Execute.

## Lifesteal

Its caster heals half of each loss it deals another.

- **Vampiric Strike** (an effect): Instant, -20 Health; its **Lifesteal**, **Share** 0.5, **Attribute** Health.

For lifesteal on every hit an item's or an ability's holder deals, use its **Lifesteal** list instead
([ITEMS.md](ITEMS.md#lifesteal)).

## Damage return

While it lasts, half of each loss its holder takes goes back to the one who dealt it.

- **Thorns** (an effect): Infinite; its **Worker Group**, one attribute worker, **Return Damage**, **Target Attribute**
  Health, **Share** 0.5.

What it returns is typed Returned, so nothing returns it again and no lifesteal heals from it.

## Can't die

While it lasts, no blow takes its target below 1, as Shallow Grave.

- **Undying** (an effect): Durational, **Duration** 5; its **Worker Group**, one attribute worker, **Can't Fall Below**,
  **Target Attribute** Health, **Floor** 1.

## On hit

While it's equipped, each hit its holder deals bites for 5 more.

- **Frost Blade** (an item): its **On Hit**, one entry, **Attribute** Health, **Effects** Frost Bite, **To** Target.
- **Frost Bite** (an effect): Instant, -5 Health.

Frost Bite's own hit sets nothing off again.

## On kill

While it's equipped, the hit that takes a target's Health to 0 feeds its holder 20.

- **Reaper's Mark** (an item): its **On Hit**, one entry, **Attribute** Health, **Effects** Feast, **To** Holder,
  **Only On Kill** on.
- **Feast** (an effect): Instant, +20 Health.

## A phase

As the boss's Health falls to half, it enrages, once.

- **Boss** (an attribute set): Health 1000; in its **Worker Group**, one analysis worker, **Threshold**, **Target**
  Health, **What** Share, **Threshold** 0.5, **Crossing** Falling, **Trigger Once** on, **Effects** Enrage.
- **Enrage** (an effect): Infinite, granting `Recipe.Enraged`.

The phase's own moves start from the tag: an ability with a **Tag Added** trigger on `Recipe.Enraged`, or a stage
condition that reads it. The worker's **Event** sends a gameplay event as well, for a trigger of its own.

## Formulas

Scaler assets for a genre's formulas, under `Formulas`, each named for what it computes (AC-3). A field reads one with
its **Shared** kind ([SCALERS.md](SCALERS.md)); each is a **Stacking Curve** of a count, with an **a**.

- **1 ÷ (1 + 0.06 × Armor)**: Reciprocal, **a** 0.06, of the hit's target's Armor. In a Value Pipeline's **Multiply**
  stage, a hit leaves all of itself at 0 Armor, a half at about 17, never none.
- **100 ÷ (100 + Armor)**: Reciprocal, **a** 0.01: a half at 100 Armor, a third at 200.
- **−Armor**: Linear, **a** −1. In an **Add** stage, a blow of 100 against 20 Armor lands as 80.
- **0.15 × Stacks**: Linear, **a** 0.15, of an item's stacks, a bonus that grows by as much with each.
- **1 − 1 ÷ (1 + 0.15 × Stacks)**: Hyperbolic, a chance that nears 1 and never reaches it: 13% with one stack, 60%
  with ten.
- **0.9 ^ Stacks**: Exponential, a share that falls by a tenth with each stack.

The Armor ones read the **Armor** attribute in `Formulas/Attributes`, as the hit lands; point them at your own.

## A starter set

Plain assets named for what they are, under `Starter Set` (AC-5); the recipes above hold the rest of a starter set, a
damage over time, an on hit, an on kill and a boss's phase. Each hit reads the recipes' Health.

- **Melee Attack** (an ability): **Apply Effects**, Melee Hit, on what it's cast at; **Cooldown** Melee Cooldown.
  **Melee Hit**: Instant, -10 Health. **Melee Cooldown**: Durational, 1 s.
- **Bolt** (an ability): the same, its **Bolt Hit** -25 Health, its **Bolt Cooldown** 3 s.
- **Slow** (an effect): Durational, 3 s, **Multiply** Movement Speed by 0.7, back as it ends.
- **Shield** (an effect): Durational, 5 s, +50 to Shield's current and base while it lasts; its **Modifiers**, one
  **Incoming** rule whose **Change Impact** on Health adds an **Absorb** stage into Shield, so each hit on its holder's
  Health comes off its Shield first. Give Shield's element a base of 0, between 0 and its base, so it's 0 without one.

They read the **Movement Speed** and **Shield** attributes in `Starter Set/Attributes`.
