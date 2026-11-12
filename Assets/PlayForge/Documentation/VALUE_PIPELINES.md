# Value Pipelines

A **Value Pipeline** is an asset that takes a value as it lands, sends it through ordered stages, and gives what comes
out (TW-2). An effect's impact names one as its calculation, as an Unreal effect names its execution calculation: each
hit of that impact goes through the pipeline's stages, so a project writes once how a hit's damage is worked out, and a
new item that changes every hit is a stage, not an edit to each attack.

It's generic: the value is any number an impact lands with, a blow, a heal, a drain. Create one from
**Create > PlayForge > Value Pipeline**.

## Not a Scaler asset

A **Scaler asset** is a number worked out from its context: a level table, × an attribute, a condition. A number field
reads it wherever a number is read.

A **Value Pipeline** transforms a value it's given. It runs once for each hit, as the hit lands, with the caster and the
target both known, and its stages can do what a number can't: roll a chance once for the hit and keep it, route part of
the value into a barrier, and say what each stage did.

A stage's own numbers are number fields, so a stage reads × Attribute, conditions, curves and Scaler assets as any field
does.

## When it runs

It runs for what an impact commits: an instant impact's hit, and each tick of a periodic one. A lasting impact, as +10
Strength while a buff holds, is read again whenever its attribute changes, so a pipeline doesn't run it.

A modifier's **Change Impact** brings a stage of its own (TW-7, [EFFECTS.md](EFFECTS.md#modifiers)): a hit runs it after
its pipeline, or alone where it has none, and the breakdown keeps it with the pipeline's steps; a lasting impact takes it
on its magnitude.

## Stages

Each stage has a **Label**, the name a breakdown gives it, and **Enabled**, which lets the value pass it by.

- **Multiply**: the value times a number read for the hit. A bonus, × the caster's Damage Amplification read as 1 + x; a
  reduction, × the target's Damage Resistance read as what's left; an armor curve; a bonus that reads the target, as 75%
  more while the target is above 90% of its health.
- **Add**: a number added to the value's size. A flat bonus of 10 makes a blow of −100 one of −110, and a heal of 50 one
  of 60.
- **Chance**: on a chance, the value times a number: a critical hit. It rolls once for the hit, and the breakdown notes
  it. Its **Roll** makes it pseudo-random or lucky (TW-8, [EFFECTS.md](EFFECTS.md#requirements)).
- **Clamp**: the value's size kept between two numbers, its sign as it was.
- **Absorb**: a loss taken first from another attribute of the one hit, as a barrier or a shield takes a blow before
  health. What it soaks comes off that attribute at the frame's end, and the rest goes on. An attribute with the
  Barrier role soaks the losses to the one it absorbs for with no stage at all (TW-12, [ATTRIBUTES.md](ATTRIBUTES.md)).
- **Block** and **Evade** (TW-12): a **Chance**, read as the effect lands, that stops it. Each rolls once, before anything
  of the effect lands, and where it comes up nothing of it does, its damage, tags, contained effects and on-hits, and
  its ticks aren't rolled again; the one hit hears Blocked or Evaded, and the dealer Hit Blocked or Hit Evaded. A
  defender brings one through an incoming modifier's Change Impact, as Tougher Times blocks the hits landing on its
  holder by a stacking curve of its stacks ([EFFECTS.md](EFFECTS.md#modifiers)). As the hit commits, it passes the
  value on.
- **Execute** (TW-15): a hit that would leave the one hit with less than its **Share** of its **Attribute**'s base, the
  Health role's where it names none, takes the rest, as Culling Blade; the breakdown notes it.

A project writes its own stage by deriving from `AbstractValueStage` and overriding `Apply(value, in run)`. `run.Context`
reads the hit's caster, target and level, as any number does, and `run.Note` notes what the stage did.

## A typical damage pipeline

1. **Amplification**: Multiply by the caster's Damage Amplification (a Bonus Share, read as 1 + x).
2. **Critical**: Chance, the caster's Crit Chance, × 2.
3. **Armor**: Multiply by a curve of the target's Armor.
4. **Resistance**: Multiply by the target's Damage Resistance (a Resistance, read as 1 − x).
5. **Barrier**: Absorb into the target's Barrier.

Every damage effect names it, so a blow of 100 from a caster with 50% amplification, on a target with 20% resistance,
lands as 120.

## The breakdown

An effect's spec keeps its last run as a **Breakdown**: the value in, each stage's before and after with its note, and
the value out. `spec.Breakdown.ToString()` says it in a line: `-100 → Amplification -150 → Resistance -120`. A combat
log, a debugger or a test reads it.
