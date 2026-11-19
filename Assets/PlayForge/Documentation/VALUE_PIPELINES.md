# Value Pipelines

A **Value Pipeline** is an asset that takes a value as it lands, sends it through ordered stages, and gives what comes
out.

An effect's impact names one as its calculation, as an Unreal effect names its execution calculation. Each hit of that
impact goes through the pipeline's stages. So a project writes once how a hit's damage is worked out, and a new item
that changes every hit is a stage, not an edit to each attack.

It's generic: the value is any number an impact lands with, a blow, a heal, a drain.

You create one in either of two ways:

- From **Create > PlayForge > Value Pipeline**.
- With **New** beside an impact's Pipeline field, which makes one named for the effect and uses it.

Once the field names one, **Open** shows it. A Crowd Control Rules or Effect Modifier field has the same two buttons.

## Not a Scaler asset

A Value Pipeline and a Scaler asset both give numbers, but they work differently.

A **Scaler asset** is a number worked out from its context: a level table, × an attribute, a condition. A number field
reads it wherever a number is read.

A **Value Pipeline** transforms a value it's given. It runs once for each hit, as the hit lands, with the caster and
the target both known. Its stages can do what a number can't:

- roll a chance once for the hit, and keep it;
- route part of the value into a barrier;
- say what each stage did.

A stage's own numbers are number fields, so a stage reads × Attribute, conditions, curves and Scaler assets as any
field does.

## When it runs

A pipeline runs for what an impact commits: an instant impact's hit, and each tick of a periodic one.

A lasting impact, as +10 Strength while a buff holds, is read again whenever its attribute changes. So a pipeline
doesn't run it.

A modifier's **Change Impact** brings a stage of its own ([EFFECTS.md](EFFECTS.md#modifiers)):

- A hit runs it after its pipeline, or alone where it has none.
- The breakdown keeps it with the pipeline's steps.
- A lasting impact takes it on its magnitude.

## Stages

A pipeline's stages take the value in order, each giving what goes on to the next. Every stage has these fields:

| Property | Description |
|---|---|
| **Label** | The name a breakdown gives it, as Amplification, Armor or Critical. Left empty, the breakdown says what the stage does. |
| **Enabled** | Off, the value passes it by. |

The stages PlayForge ships are below, and a project can write its own.

### Multiply

A **Multiply** stage multiplies the value by a number read for the hit.

| Property | Description |
|---|---|
| **By** | What the value is multiplied by, read for the hit. |

It suits any factor:

- A bonus: × the caster's Damage Amplification, read as 1 + x.
- A reduction: × the target's Damage Resistance, read as what's left.
- An armor curve.
- A bonus that reads the target, as 75% more while the target is above 90% of its health.

### Add

An **Add** stage adds a number to the value's size.

| Property | Description |
|---|---|
| **Amount** | What's added to the value's size, read for the hit. |

A flat bonus of 10 makes a blow of −100 one of −110, and a heal of 50 one of 60.

### Chance

A **Chance** stage multiplies the value by a number on a chance: a critical hit.

| Property | Description |
|---|---|
| **Chance** | The chance, from 0 to 1, read for the hit, as the caster's Crit Chance. |
| **Multiplier** | What the value is multiplied by when the roll comes up: 2 for a double. |
| **Note** | What the breakdown notes when it comes up: critical. |
| **Roll** | How it rolls: pseudo-random, or lucky ([EFFECTS.md](EFFECTS.md#requirements)). |

It rolls once for the hit, and the breakdown notes it.

### Clamp

A **Clamp** stage keeps the value's size between two numbers, its sign as it was.

| Property | Description |
|---|---|
| **Least** | The least its size can be, read for the hit. |
| **Most** | The most its size can be, read for the hit. |

### Absorb

An **Absorb** stage takes a loss first from another attribute of the one hit, as a barrier or a shield takes a blow
before health.

| Property | Description |
|---|---|
| **Into** | The attribute that takes the loss first: a barrier, a shield. |
| **On** | Whose attribute: the one hit's, or the caster's. |

- What it soaks comes off that attribute at the frame's end, and the rest goes on.
- A gain passes it by.
- An attribute with the Barrier role soaks the losses to the one it absorbs for with no stage at all
  ([ATTRIBUTES.md](ATTRIBUTES.md)).

### Block and Evade

A **Block** or **Evade** stage is a **Chance**, read as the effect lands, that stops it.

| Property | Description |
|---|---|
| **Chance** | The chance it stops the effect, from 0 to 1. It's a number of any kind, as the one hit's Block Chance. |
| **Roll** | How it rolls: pseudo-random, and its luck. |
| **Stops** | What it can stop: **Losses**, the default, or **Any Impact** (below). |

- Each rolls once, before anything of the effect lands.
- Where it comes up, nothing of the effect lands: its damage, tags, contained effects and on-hits. Its ticks aren't
  rolled again.
- The one hit hears Blocked or Evaded, and the dealer Hit Blocked or Hit Evaded.
- As the hit commits, it passes the value on.

Its **Stops** has two options:

| Stops | What it can stop |
|---|---|
| **Losses** | An effect that lowers its attribute: an add below 0, a multiplier below 1, or an override below where it stands. So a heal lands past it, unrolled. |
| **Any Impact** | Any effect, heals and buffs too. |

A defender brings one through an incoming modifier's Change Impact ([EFFECTS.md](EFFECTS.md#modifiers)). Tougher
Times, for example, blocks the hits landing on its holder by a stacking curve of its stacks.

### Execute

An **Execute** stage finishes off a hit's target, as Culling Blade. A hit that would leave the one hit with less than
its **Share** of its **Attribute**'s base takes the rest.

| Property | Description |
|---|---|
| **Share** | The share of its base below which a hit takes the rest, as 0.13. |
| **Attribute** | The attribute it reads, the one hit's. With none, the one in the project's Health role. |

The breakdown notes it.

### Writing your own stage

A project writes its own stage by deriving from `AbstractValueStage` and overriding `Apply(value, in run)`.

- `run.Context` reads the hit's caster, target and level, as any number does.
- `run.Note` notes what the stage did.

## A typical damage pipeline

A typical damage pipeline has five stages:

1. **Amplification**: Multiply by the caster's Damage Amplification (a Bonus Share, read as 1 + x).
2. **Critical**: Chance, the caster's Crit Chance, × 2.
3. **Armor**: Multiply by a curve of the target's Armor.
4. **Resistance**: Multiply by the target's Damage Resistance (a Resistance, read as 1 − x).
5. **Barrier**: Absorb into the target's Barrier.

Every damage effect names it. So a blow of 100 from a caster with 50% amplification, on a target with 20% resistance,
lands as 120.

The Demo's blows go through two such pipelines, **Physical Damage** and **Magic Damage**, in its `Effects/Pipelines`.
Each multiplies a blow by its dealer's Attack or Magic Amplification, a Stacking Curve of it read as 1 + it. Then it
multiplies by what its target's defence leaves:

| Pipeline | Then multiplies by |
|---|---|
| **Physical Damage** | What its target's Armor leaves, 100 ÷ (100 + Armor) |
| **Magic Damage** | Its target's Magic Resistance, 1 − it, kept between 0 and 1 |

Its Physical and Magic Strike templates name them, and so does each blow that takes one. So a new attack takes its
kind's pipeline from its template.

## The breakdown

An effect's spec keeps its last run as a **Breakdown**: the value in, each stage's before and after with its note, and
the value out.

`spec.Breakdown.ToString()` says it in a line: `-100 → Amplification -150 → Resistance -120`. A combat log, a debugger
or a test reads it.

## Trying a pipeline

A Value Pipeline's inspector has a **Try** section under its stages. It puts a value through them at each level of a
range, as a Scaler asset's Try reads a scaler.

| Property | Description |
|---|---|
| **Value In** | The value a hit lands with, going in: a loss below 0, as a blow of 100 is −100, and a gain above. |
| **Chances Come Up** | Settles each roll, so a try gives the same each time (below). |
| **Levels** | The hit's levels, 1 to 5 to start, for a stage whose number scales by level. |
| **Caster**, **Target** and **Caller** | The same stand-ins as a Scaler asset's Try (below). |

**Chances Come Up** settles each chance a stage rolls:

- On, each comes up where it can: a critical strike doubles the hit, and a block stops it.
- Off, each misses where it can.
- A chance of 1 always comes up, and one of 0 never does.

The **Caster** and **Target** have a row for each attribute a stage reads on that side, as a Multiply stage's Damage
Amplification or an Absorb stage's Barrier. You type its values, or fill them from an **Entity**. The **Caller** has a
row for each key a caller sets.

Its **Out** row says what lands at each level. Click one to see its steps under the table: the value in, each stage's
value in and out with its note, as "critical", and what comes out.

- A Block or Evade stage rolls first, as it does as an effect lands. One that comes up stops the hit: nothing of it
  lands, and its level shows "—".
- No effect is behind a try. So what an Absorb stage soaks comes off no one, and a stage of your own that reads
  `run.Carrier` reads none.
- What's typed is kept for the session, never in the asset.

`ValuePipelineTry` does the same in code: `new ValuePipelineTry { Value = -100f }.Read(pipeline)`.
`pipeline.Run(value, in context)` puts a value through a pipeline against a number context of your own, with no effect
behind it.
