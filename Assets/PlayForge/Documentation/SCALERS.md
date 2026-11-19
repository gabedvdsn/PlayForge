# PlayForge — Scalers

A **scaler** is a value source that a magnitude field reads at a level, such as a table of damage per level. The level
is the origin's, such as the level of the ability that applies an effect.

Each section below matches what the inspector shows.

## A magnitude field

A magnitude field, such as an effect's Magnitude or Duration, is a number field. It's a `ScalableFloat`, or a
`ScalableInt` for a whole number.

The field's row shows its name, then its **kind**, then what it reads, then its field menu. The field menu opens as
wide as its items.

The sections below cover where number fields appear, how a field picks its kind, each kind in turn, and the field's
tools.

### Where number fields appear

Every number an asset authors is a number field, read at the level of what holds it.

| What holds it | Its number fields | Read at |
|---|---|---|
| An ability | Its Buffer Window, and its tasks' numbers | The ability's level |
| An effect | Its Stack Bounds' Min and Max, its Starting Stacks, and a Stacks change's number | The effect's level |
| A cue | Its Strength, Duration, Lifetime and Volume | For its effect, as it lands |
| An item | Its Max Stack Size | The item's own level |
| A set bonus | Its Pieces | Its holder's level |
| An entity | A barrier's Decay Per Second, an attribute set's Snap Interval, an aura's Interval, and a worker's Threshold, Floor, Share, Every Ticks, Tick Pause or Required Weight | The entity's level, or the change's |

A level, a pointer's button, an order, a priority among configs and a log's size stay whole numbers. So do a kind's
and a scaler's own numbers.

A folded field's line shows a number field by what it reads, as "Uses 3".

An attribute set's values use the cached kinds, which read other attributes as the entity's values change.

### The kind menu

The kind menu says where a field's number comes from. It lists its kinds in three groups:

1. At its top: Constant, Table, By Level, Shared, × Attribute and Set By Caller.
2. Then the kinds that read the game: Asset Part, Group, Group Impact, Local Data, Relative to a Field and Worker.
3. Then the rest, filed by category.

| Category | Its kinds |
|---|---|
| **Scaler** | The scalers of every kind: Clamped, Conditional, Fixed Value, Randomized, Stacking Curve and Threshold |
| **Effect** | Effect Stacks and Tracked Impact |
| **Item** | Item Stacks |
| A project's own | A kind of a project's own goes under the category it names |

A field whose code declares what its context provides, with `[NumberContext]`, offers only the kinds that can be
worked out there. A targeting task's range, which has no target yet, offers no kind that reads one.

Choosing a kind is one Undo step. A constant made a Table or By Level keeps its number as its first level's value.

### Combine and Rounding

A kind other than Constant shows a one-line summary of what the field reads under its row, as "0.3 × Target's Health
(base)". Under the summary sit the field's **Combine** and, on a whole-number field, its **Rounding**.

| Property | Description |
|---|---|
| **Combine** | How the field joins its number with the scaler:<br>**Scaler** reads the scaler's value.<br>**Scaler + Number** adds the field's number.<br>**Scaler × Number** multiplies by the field's number.<br>**Number Only** reads the number. |
| **Rounding** | On a whole-number field, such as Ticks: Floor, Ceil, or Round, to the nearest, a half away from zero. |

- A field whose unit is "%" stores 0.3, and shows and takes it as 30%.
- A kind its field can't work out says so under the field, as one copied from a field that has a target into one that
  hasn't. The Problems window lists it, and the field reads its number.

### Constant

A **Constant** field reads its number, with its unit beside it, as Tick Every's "1 s".

A field that used to read its scaler, and has none, reads 0, and a warning says so.

### Table

A **Table** reads one value for each level, exactly.

| Property | Description |
|---|---|
| **Levels** | Whose levels it has a value for: its asset's level provider's, or **Own levels** ([Per Level](#per-level)). |
| **Behaviours** | Rules that adjust the value the scaler works out, in order. |

- Level 1 reads the first value, level 4 the 4th, and a level past the last reads the last, as Laguna Blade's 450, 600
  and 750.
- The range it gives shows beside the kind, as "6–8 s", or "-25 to -10" where it goes below 0.
- Its values sit in a row under it, with no curve.

### By Level

A **By Level** field reads its values spread over a level range, as points on a curve.

| Property | Description |
|---|---|
| **Over Levels** | The level range its values spread over ([Spread Over Levels](#spread-over-levels)). |
| **Points** | How many values. |
| **Between Points** | How a level between two points reads: **Steps**, **Straight Line** or **Smooth Curve**. |
| **Behaviours** | Rules that adjust the value the scaler works out, in order. |

- A level between two points reads between them ([Values per level](#values-per-level)).
- It's the same number by level as a Table, read another way. So choosing one for the other keeps its values.
- One saved reading exactly shows as a Table, and nothing saved changes.
- [SPREADSHEETS.md](SPREADSHEETS.md) writes a project's Tables and By Levels to a spreadsheet and reads them back.

### Shared

A **Shared** field reads a Scaler asset, one scaler that many fields can use.

| Property | Description |
|---|---|
| **Asset** | The Scaler asset the field reads. |

An edit to the asset changes every field that shares it. See [Scaler assets](#scaler-assets).

### × Attribute

**× Attribute** reads an attribute of the caster or the target.

| Property | Description |
|---|---|
| **Capture Attribute** | The attribute it reads. |
| **Capture From** | Whose attribute: Source, the caster's, or Target, the target's. |
| **Capture When** | On Creation captures the value once, as the effect's spec is made, and reads that snapshot after. On Application reads the live value each time. |
| **Capture What** | Its current, its base, what's missing of it (base − current), or its share of its base (current ÷ base). |
| **Groups** | All of it, or only some of its retention groups ([Retention groups](#retention-groups)). |
| **Read As** | How it reads the value: as its attribute's role says, as it is, as a multiplier (1 + x), or as what's left (1 − x). |

- The menu starts it as the field's number times it.
- By its attribute's role, it reads a Bonus Share as 1 + x and a Resistance as 1 − x. Its Read As can say otherwise.

### Set By Caller

**Set By Caller** reads a value the code passes in.

| Property | Description |
|---|---|
| **Key** | The tag the value is set under. Only the key itself matches, not a tag above or below it in the tag tree. |
| **Default** | What the field reads where no value is set under the key. It's a number of any kind, so it can read a value by level. |

It checks one thing: whether what the number is read in holds a value under exactly its **Key**.

Code sets a value in one of three places: on an effect's spec before applying it (`spec.SetByCaller(tag,
value)`), on an ability activation's data (`data.SetByCaller(tag, value)`), or on the data that spawns a process
([EFFECTS.md](EFFECTS.md)).

- Each effect the ability makes takes the activation's values, and the ability's tasks read them.
- A value set to 0 is a value.
- Its summary says both the key and the default, as "the caller's Damage, else 0".

### Asset Part

**Asset Part** finds effects, abilities or items on the caster or the target by a tag, and reads a part of them.

| Property | Description |
|---|---|
| **What** | What it looks for: effects on the entity, abilities it knows, or items it holds. |
| **Tagged** | The tag they carry. A tag under the one given matches too. |
| **On** | Whose: the caster's or the target's. |
| **Effect Part**, **Ability Part**, **Item Part** | What it reads of each one it finds (below). Each shows only for its kind of asset. |
| **Worker Key** | For an effect's Worker Value, the key it asks that effect's workers for. |
| **Among** | How several that match read as one: their Sum, the First, the Highest or the Lowest. |
| **Default** | What a part other than a count reads where none matches. |

An effect matches by its asset tag, a context tag or a tag it grants. An ability or an item matches by its asset tag
or a context tag.

| Of an | The parts it reads |
|---|---|
| **Effect** | Count, Stacks, Level, Last Impact, Total Impact, Impact Count, Duration Remaining, Duration, Time Until Tick, or a Worker Value: what that effect's workers answer under a key |
| **Ability** | Count, Level, Is Active, Is on Cooldown, or Cooldown Remaining |
| **Item** | Count, Stacks, Level, or Is Equipped |

- A true-or-false part reads 1 or 0, so its sum is how many are so.
- Where none matches, a count reads 0, and any other part the **Default**.

### Group

A **Group** joins several numbers as one.

| Property | Description |
|---|---|
| **Calculations** | Its members. Each is an **Operation**, then its **Scaler**, which can be any kind. |
| **Operation** | How the member joins the others: Add, Multiply, Divide, Flat Bonus or Override. |
| **Override Member Collision Policy** | Where several members override, which one gives the group's value (below). |

Its members join in this order, whatever order they're listed in:

1. The members that **Add** are added up.
2. That sum is multiplied by each member that **Multiplies**, and divided by each that **Divides**. One that reads 0
   divides nothing.
3. Then each **Flat Bonus** is added on.

So `(5 + Caster's Strength) × 2 + 3` is one number, and `1.7 ÷ Caster's Attack Speed` another. A first member needn't
add.

- Members that multiply or divide with none that adds work on 0, and the Group says so.
- A member that **Overrides** gives the group's value alone. Where several do, the group's policy picks the highest,
  the lowest, their average, the first or the last. Ignore sets them aside.
- A Group has no values per level.
- With no members, or none that holds a kind, it's no kind at all. Its field then reads its number, as a Constant does.

### Group Impact

**Group Impact** reads one retention group's part of an attribute on the caster or the target.

| Property | Description |
|---|---|
| **Attribute** | The attribute whose part it reads. |
| **Of** | Whose attribute: the caster's or the target's. |
| **Group** | The retention group whose part it reads, as the group items add to. |
| **Part** | The part's current value, or its base. |

"Caster's Strength (Bonus)", for example, reads the Strength items add, and none of the hero's own. See
[Retention groups](#retention-groups).

### Local Data

**Local Data** reads the number under a **Key** in Local Data.

| Property | Description |
|---|---|
| **Key** | The key the number is under. |
| **From** | Whose Local Data: **Its Own**, what the number belongs to, as its ability's or its item's asset. Or the **Caster**'s or the **Target**'s, as the entity holds it. |
| **Default** | What it reads where there's no number under the key. |

- A hero's attack range kept in its Local Data is one field read by every ability.
- The entry's number can scale too, read where the kind reads it. Its Own reads it in the number's context. The
  Caster's or the Target's reads it at that entity's own level.
- One that reads its own key stops at the Default.

### Relative to a Field

**Relative to a Field** reads another number field of the same object, picked from a menu of them.

| Property | Description |
|---|---|
| **Field** | The other number field of the same object it reads. |

The field's number joins the other field's as **Combine** says. A Clamped scaler's Max can be 2 × its Min, and a stack
bound's Max its Min + 3.

- The other field is read in the same context, so a relative Max follows a Min that scales by level.
- A field offers this kind where its code hands its object over as it reads
  ([A number field in your own code](#a-number-field-in-your-own-code)).
- A scaler's own numbers read the scaler as their object. So a Clamped's Max Value can be Relative to a Field, 2 × its
  Min Value, whatever field the Clamped is in.

### Worker

**Worker** asks workers for a number under a **Key**.

| Property | Description |
|---|---|
| **Key** | The key it asks for. |
| **From** | Whose workers: **This Effect**'s own, or the **Caster**'s or the **Target**'s. |
| **Default** | What it reads where no worker answers. |

See [Asking a worker](#asking-a-worker).

### Clamped

A **Clamped** keeps a number within bounds.

| Property | Description |
|---|---|
| **Value** | What it clamps. |
| **Min Value** | The least it reads. |
| **Max Value** | The most it reads. |

- It keeps its **Value** between its **Min Value** and **Max Value**, three numbers of any kind.
- One made in the inspector clamps a value by level.
- Its numbers read the Clamped as their object, so its Max Value can be Relative to a Field, 2 × its Min Value.

### Conditional

A **Conditional** reads one number where a condition holds, and another where it doesn't.

| Property | Description |
|---|---|
| **When** | Its condition (below). |
| **True Value** | What it reads where the condition holds. |
| **False Value** | What it reads where the condition doesn't hold, or where it has none. |

Its summary says all three, as "−0.1 × Target's Health (base) if Target's Health < 0.3 × Target's Health (base), else
0".

#### Conditions

A condition picks its kind from a menu, as a number does, and says itself in a line.

| Condition | What it checks |
|---|---|
| **Tag** | Whether the caster or the target holds a tag, counted by its weight, so a tag granted twice counts twice. |
| **Attribute** | An attribute of the caster or the target compared with a number, as 0.3 × the target's base Health. It reads its current, its base, what's missing of it or its share of its base. |
| **Level** | The level, or how far up its range it is, from 0 to 1, compared with a number. |
| **Compare Numbers** | One number compared with another. |
| **Always** and **Never** | Nothing: Always holds wherever it's read, and Never holds nowhere. |
| **All Of**, **Any Of** and **Not** | Under Logic, these join others. All Of none holds; Any Of none doesn't. |

An Attribute condition says "above 90%" as a share above 0.9.

Where what the number is read in lacks what a condition reads, the condition doesn't hold, and neither does a Not
around it. A target's tag where there's no target is one example.

### Fixed Value

A **Fixed Value** is one number, whatever the level.

| Property | Description |
|---|---|
| **Value** | The number it reads. |

- Its settings show its value alone ([Values per level](#values-per-level)).
- Make Constant turns a scaler field of its own, with no number, into a Fixed Value ([The field menu](#the-field-menu)).

### Randomized

A **Randomized** scaler rolls a random value.

| Property | Description |
|---|---|
| **Variance** | How its roll varies its value (below). With none, it doesn't vary. |
| **Roll Once Per Spec** | On, it rolls once per effect spec and keeps the roll, so every tick of one application deals the same. Off, it rolls on every evaluation. |

Its **Variance** is a kind of its own:

| Variance | How it varies the value |
|---|---|
| **Percentage** | Up or down by a **Share** of its value. |
| **Flat** | Up or down by an **Amount**. |
| **Multiplier** | Times one between its **Min** and its **Max**. |
| One of your own | As two dice: a `[Serializable]` class made from `AbstractVariance` that overrides `Vary`, `Lowest` and `Highest`. |

- A variance's numbers read the variance as their object. So a Share can widen by level, and a Max can be 3 × its Min.
- Its settings show its variance, then the values its base value comes from.

### Stacking Curve

**Stacking Curve** turns a **Count**, as an item's stacks or an attribute, into its number by a **Curve** and its
**Coefficient**, a.

| Property | Description |
|---|---|
| **Count** | What it counts, n. |
| **Curve** | How the count turns into the number (below). |
| **Coefficient** | The curve's a. |
| **Plus** | Added after the curve. 1 makes a share a multiplier. |

| Curve | Formula | For example |
|---|---|---|
| **Linear** | a × n | 15% a stack |
| **Hyperbolic** | 1 − 1 / (1 + a × n) | A chance that nears 1 and never reaches it: 13% with one stack and 60% with ten, at 0.15 |
| **Reciprocal** | 1 / (1 + a × n) | What Dota's armor leaves of a hit, at 0.06 |
| **Inverse** | a / n | A wind-up an attack speed shortens |
| **Exponential** | a to the n | A chance that falls by a share each stack, as 0.9 to the n |

### Threshold

A **Threshold** steps at thresholds: it reads one value for each tier of levels.

| Property | Description |
|---|---|
| **Thresholds** | Its thresholds. Each has a **Level** and an optional **Tier** name, with the **Value** it reads from that level up under them, a number of any kind. |
| **Default Value** | What it reads below every threshold. |

Of the thresholds at or below a level, the highest gives the value.

### Effect Stacks

**Effect Stacks** reads the stack count of the effect's own container.

- It reads 1 where there's nothing stacked.
- It has no settings of its own.
- The kind menu files it under **Effect**.

### Tracked Impact

**Tracked Impact** reads what an effect has dealt.

| Property | Description |
|---|---|
| **What** | Its **Last** impact, its **Total**, or its impact **Count**. |
| **Of** | Whose impacts: **This Effect**; **An Effect** on the caster or the target, by its asset; or **Effects Tagged** there, added up. |
| **Effect** | For An Effect, the effect whose impacts it reads. |
| **Tagged** | For Effects Tagged, the tag the effects carry. |
| **Part** | The impact's current part, or its base. |
| **By Size** | Reads the impact positive, as a heal of half a blow wants. |

- This Effect suits a burn that grows by its own tick count.
- An impact is as it landed, after the target's workers, and signed, so damage is negative.
- Before an effect's first impact, and after a load, it reads 0.
- The kind menu files it under Effect.

### Item Stacks

**Item Stacks** reads the stack count of the item that applied the effect ([ITEMS.md](ITEMS.md)).

- It reads 1 where there's nothing stacked.
- The kind menu files it under **Item**.

### Retention groups

An attribute's value is the sum of its retention groups' parts: the base its set gives, what items add in their
group, and what a buff adds in its.

**Retention groups in any attribute read** work alike. × Attribute, the Attribute condition, a relative attribute
worker's Relative To and the Threshold worker each have *Groups*, which say which parts the read takes:

| Groups | What the read takes |
|---|---|
| **All** | The whole value, as before. |
| **Only These** | Only the groups named, as a bonus of the items' Strength reads their group. |
| **All But These** | Every group but those named, as a bonus of a hero's own damage leaves its items' out. |

- A group names the groups under it too, so *Items* takes *Items.Ring*.
- The read says which: "Caster's Strength (Items)", "Caster's Attack Damage (all but Items)".
- [Group Impact](#group-impact) reads one group's part alone.

### The field menu

The field menu holds a number field's tools. A scaler field of its own, as a Group member's, has the same menu, and its
kind menu files its kinds by category, as a number field's does.

| Item | What it does |
|---|---|
| **Save as Scaler Asset…** | Asks for a name and a folder. **Save and Use** then saves the field's scaler as a Scaler asset and makes the field share it. |
| **Scaler Assets** | A submenu of the project's Scaler assets, the one the field shares checked. The field shares the one you pick. |
| **Import Values…** | Copies another asset's values per level (below). |
| **Quick Fill…** | Fills the table from a pattern (below). |
| **Make Constant** | The field reads a number again: the value its first level read. A scaler field of its own, with no number, becomes a **Fixed Value** of that value. |

#### Import Values

**Import Values…** opens a window that lists the Scaler assets, and the other assets' fields with values per level.

1. Pick one to compare its values with the field's, level by level, with both curves.
2. Click **Copy Values**.

A shorter table's last value holds for the levels past its end, and the window says which levels those are. One Undo
takes it back.

#### Quick Fill

**Quick Fill…** fills the table from a pattern, from one value to another.

Its patterns are **Same Value**, **Straight Line**, **Slow Then Fast** and **Fast
Then Slow** with a **Strength**, **Steps**, **Add Each Level**, **Multiply Each Level**, **Ease In and Out**, or a
**Custom Curve** you draw.

- **Rounding** makes the values whole numbers, or rounds them to a **Step**.
- A preview shows the values as the table will, with their curve. One Undo takes them back.
- On a scaler spread over levels, it fills the points, as many as **Points** says. The preview then shows what each
  level reads between them.

### Editing several assets

With more than one asset selected, the field edits each asset's own field.

- Kinds that differ show as mixed.
- Choosing a kind gives it to each field, from that field's own value. A field of that kind keeps its scaler.
- Levels and Make Constant work on each field's own values.
- A count, range or curve the fields disagree on shows as mixed.
- Save as Scaler Asset waits for one asset.

## Values per level

A scaler's settings sit on a shaded block under its field, so they read as the scaler's among the fields around them.

A kind shows only what it reads. A **Fixed Value** shows its value, and a **Randomized** scaler its variance, then the
values its base value comes from.

A kind's field that applies only to some of its settings shows only for them, as an Asset Part's worker key for an
effect's Worker Value.

| Property | Description |
|---|---|
| **Values** | **Per Level** or **Spread Over Levels**. A Table reads Per Level and By Level spreads, as their kinds say, so they have no choice here. A scaler of another kind with values for each level chooses. |
| **Behaviours** | Rules that adjust the value the scaler works out, in order. |

- A read-only curve beside the table shows the values, and spread, the curve they make. Its tooltip reads them, as "6
  at level 1, rising to 8 at level 5".
- Quick Fill fills the levels the table shows.
- Nothing changes a scaler's values while it's drawn: its table keeps its length until you change it.

### Per Level

With **Per Level**, the scaler has one value per level, ten levels to a row.

| Property | Description |
|---|---|
| **Levels** | Whose levels it has a value for: its asset's level provider's, or its **Own levels**. |

- Level 1 reads the first value, level 4 the 4th, and a level past the end reads the last.
- An asset with a level provider, as an ability, or an effect linked to one, follows the provider's levels, as
  **Fireball's levels (1–4)**, with a value for each.
- A level past the scaler's values shows the last in secondary text. Type there to give it a value of its own.
- **Own levels** gives the scaler a count of its own.
- An asset whose levels come from whatever uses it shows the count alone. An unlinked effect, an attribute set and a
  Scaler asset do.
- A new level takes the last value.
- A scaler you make in the inspector starts Per Level.

### Spread Over Levels

With **Spread Over Levels**, the values are points on a curve over a level range, and a level between two points reads
between them.

| Property | Description |
|---|---|
| **Over Levels** | The level range the values spread over (below). |
| **Points** | How many values. |
| **Between Points** | **Steps**, **Straight Line** or **Smooth Curve**. |

**Over Levels** takes one of three ranges:

| Over Levels | The range |
|---|---|
| The provider's levels | The levels of its asset's level provider. |
| **Own levels** | Levels up to the scaler's own **Max Level**. |
| The provider's **up to a level** | The provider's levels, up to the Max Level. The values reach the last at the lower of the Max Level and the provider's last level, and a level past it reads the last value. |

- Three values, 10, 20 and 30, spread over levels 1 to 5 read 10, 15, 20, 25 and 30.
- The points are numbered, and under them each level shows what it reads.
- The scalers saved before the choice existed spread, so they read as they did.

## Writing your own scaler

You can add three kinds of your own: a scaler, a kind of number, and a condition for a Conditional.

### A scaler of your own

A scaler of your own derives from `AbstractScaler`, or from `AbstractCachedScaler` for an attribute set's values. It
has a public constructor with no parameters.

The Kind menu lists it by its name without "Scaler", as **Damage Curve** for `DamageCurveScaler`. Its inspector draws:

- Each field it adds, public or `[SerializeField]`, as Unity draws the field's type, through any property drawer you
  write for that type.
- The fields of an abstract base of your own too.
- A field marked `[ForgeShowWhen(nameof(Mode), EMode.Curved)]` only while another field, an enum or a bool, holds one
  of the values given.
- The parts every scaler has, as its `Parts` say (below).

`[HideInInspector]` keeps a field out.

#### Parts

A scaler's `Parts` say which of the parts every scaler has it reads.

| Part | What it is |
|---|---|
| `EScalerParts.LevelValues` | Its values per level |
| `EScalerParts.LevelRange` | The level range they spread over |
| `EScalerParts.Behaviours` | Its behaviours |

- Unless you override it, a scaler reads all three, or its behaviours alone where its `UseScalingOptions()` returns
  false.
- Override `Parts` to say what yours reads. A scaler that reads only an attribute returns `EScalerParts.Behaviours`.
- `Parts` can follow the scaler's own fields, as one that reads its values per level in one mode alone.

### A kind of number of your own

A **kind of number** of your own is simpler than a scaler.

1. Derive from `NumberKind`.
2. Override its one method, `Evaluate(in NumberContext context)`.
3. Name it, and say what it reads, with `[NumberKind("Distance to Target", Reads = NumberReads.Caster | NumberReads.Target)]`.

`Evaluate` reads what the field's site has: `context.Caster`, `context.Target`, `context.Level`, the caller's
magnitudes, Local Data, an attribute on either side, and more. `context.Has(...)` says which.

- `Describe()` gives the field's summary, as "the distance to the target".
- A field offers the kind only where what it reads can be worked out. Where it can't, the field reads its number.
- `[ForgeMenu("Space")]` files it in the kind menu.
- A kind is a scaler, so it also works in an effect's magnitude, and inside a Group or a Conditional. Its fields draw
  as a scaler's do.
- Test one without an effect: `number.Evaluate(NumberContext.For(caster, target, level))`.
- A kind is saved by its class's name, so renaming the class needs Unity's `[MovedFrom]`.

PlayForge's own kinds, Local Data, Tracked Impact, Asset Part and the rest, are written this way, in
`Runtime/PlayForge/Numbers/Kinds`.

### A condition of your own

A **condition** of your own, for a Conditional, is made the same way.

1. Derive from `GameplayCondition`.
2. Override `Evaluate(in NumberContext context)` to say whether it holds, and `Describe()` for its line.
3. Name it with `[GameplayCondition("Target Is Caster", Reads = NumberReads.Caster | NumberReads.Target)]`.

The condition menu offers it, and `condition.Holds(context)` reads it.

A kind or a condition that holds numbers reads them with itself as their owner, `MaxValue.Evaluate(context, this)`.
It passes `Initialize(carrier)` on to them, so a number in it captures what it captures as an effect's spec is made.

PlayForge's conditions are in `Runtime/PlayForge/Numbers/Conditions`.

## A number field in your own code

A field of your own is one line, and a float converts to it:

```csharp
[Tooltip("How far it reaches")]
[NumberContext(NumberReads.Level | NumberReads.Caster, Unit = "m")]
[NumberMin(0)]
public ScalableFloat Range = 20f;
```

Read it with `Range.Evaluate(context)`, with a `NumberContext` built `Of` what the code has: an effect's spec, an
activation's data, a worker's context, an entity, a cue or a process.

`Range.Evaluate(context, this)` also hands the field's object over, which is what Relative to a Field reads. A field
read that way declares `NumberReads.Owner`, or `NumberReads.All`, and its kind menu then offers that kind.

| Attribute | What it does |
|---|---|
| `[Tooltip]` | Shows on the field's name. |
| `[NumberMin(0)]`, `[NumberMax(10)]` and `[NumberRange(0, 1)]` | Limit the constant typed in the inspector. A range sets it on a slider. |
| `[ShownWhen(nameof(UsePhysics))]` | Shows a field only while a toggle of the same object is on. |
| `[ShownWhen(nameof(Mode), (int)EMode.Area)]` | Shows a field only while an enum is one of the values. |
| `[ForgeMenu("Path/Sub", Name = "...", Description = "...")]` | Files a kind of your own in its picker. |

### Limits

The limits apply to the constant alone.

- On a field whose unit is "%", the limit is in what the field stores, so `[NumberRange(0, 1)]` is 0% to 100%.
- A whole-number field takes the whole numbers inside the limits.
- A number of another kind is worked out as the game runs. Where what it reads must stay inside limits, give it the
  Clamped kind, or clamp what the code reads.

> [!NOTE]
> Unity's own `[Min]` and `[Range]` don't work on a number field. Unity gives the field to that attribute's drawer,
> which draws "Use Range with float or int" where the number was. As scripts load, the console names each number field
> that has one, with the attribute to use in its place.

### In a kind of your own

`[ShownWhen]` works in a kind of your own, as an ability task, a targeting task or a scaler. A targeting task's
Detection Layers, for example, show under Use Physics.

A task reads its numbers with `Read(number, data)`, at its ability's level.

Every kind of your own takes `[ForgeMenu(...)]`: a task, a worker, a component, a cue, a kind of number or a condition.

- Its path files it under the submenus the path names, a slash nesting them.
- Name replaces its class's name in the picker and in its field.
- Description is what the picker's Context says while it's highlighted.
- A path holds for the classes made from the one that names it.
- `[ForgeMenu("")]` puts a kind at the top, whatever its base names.

## Asking a worker

A number can't count or remember, but a worker can. So a number of the **Worker** kind asks workers for a value under a
key, and any worker answers by implementing `INumberWorker`:

```csharp
public class OverkillWorker : AbstractEffectWorker, INumberWorker
{
    public Tag Key;

    public bool TryGetNumber(Tag key, in NumberContext context, out float value)
    {
        value = 0f;
        if (key != Key) return false;   // not a key it knows: the next worker is asked
        var state = (context.Carrier as GameplayEffectSpec)?.GetWorkerState<State>(this);
        value = state?.Overkill ?? 0f;
        return true;
    }
}
```

- An **effect's workers** answer about one application of their effect.
  - A worker is one object on its effect's asset, shared by every application. So what it counts for each is kept on
    the spec.
  - It reads that with `ctx.GetState<State>(this)` in its events, and `spec.GetWorkerState<State>(this)` as it answers.
- An **entity's workers**, those its worker groups give it, answer about the entity.
  - A worker group installs the ones that answer as it's given.
  - Code that adds a worker itself calls `NumberWorkers.Provide(entity, worker)`.
- The first worker that answers the key gives the value. Where none does, the number reads its Default.
- Any number field can ask: an effect's magnitude, a task's range, a threshold. Code can ask too, with
  `NumberWorkers.TryAsk`.
- An Asset Part's Worker Value asks the workers of each effect it finds.

### The Attribute Change worker

The **Attribute Change** effect worker is one that answers. It notes an attribute as each application of its effect
begins.

It answers how much the attribute has **Lost**, **Gained** or **Changed** since, or what it was **At
Start**. With **Every Ticks** above 0, it answers only on each Nth tick, and 0 on the others.

| Property | Description |
|---|---|
| **Key** | The key it answers under. |
| **Attribute** | The attribute it watches. |
| **Of** | Whose attribute: the target's, the entity the effect is on, or the caster's. |
| **What** | What it answers: Lost, Gained, Change or At Start. |
| **Part** | The attribute's current value, or its base. |
| **Every Ticks** | At 0, it answers at every read. Above 0, only on each Nth tick of its effect. |

A curse that deals 5 a second, and every fourth second a quarter of the health its target has lost since it landed,
from every source, is one periodic effect:

- Its first impact is −5.
- Its second is a Worker number, −0.25 × the worker's answer.
- The worker watches the target's Health, Lost, Every Ticks 4.

## Scaler assets

A **Scaler asset** (`Create → PlayForge → Scaler`) holds one scaler that many fields can use.

A field of the **Shared** kind reads its **Asset**'s scaler. An edit to the asset changes every field that uses it,
with nothing to copy or propagate.

**Open** shows the asset in a window of its own. Under the field, the asset's curve shows beside its range, as "6–8,
levels 1–5", and how many fields share it.

A field adjusts the shared values only through its own number and Combine. Scaler × Number with a number of 1.5 reads
the asset's values times 1.5, as in Fire Duration × 1.5.

> [!NOTE]
> The Scaler asset was the Scaler Template, whose link and sync settings nothing read, so those settings are gone. So
> are the old scaler window's curve editor and import window: a scaler's values are its table, and Import Values
> compares them before it copies.

### The asset's inspector

The asset's own inspector shows its name, its kind and how many fields use it.

| Property | Description |
|---|---|
| **Name** | Renames it, as The Forge and the field menus list it. |
| **Description** | What the scaler is for. |
| **Values** | Its scaler's values ([Values per level](#values-per-level)). |
| **Used By** | Each field that shares it, by its asset and field. Click a row to select the asset. |

In The Forge, its summary says its description, its kind, what it reads and how many fields use it.

### Trying a scaler

The asset's **Try** section reads it at each level of a range, Levels 1 to 5 to start, as an effect's Levels does. It
shows a row of what it gives at each level, and under it the parts behind the level picked.

It reads against a stand-in **Caster** and **Target**, and a **Caller**. Each has a row for what the scaler asks of
it, found as it reads.

| Stand-in | Its rows |
|---|---|
| **Caster** and **Target** | An attribute on its side: whether the side **Has** it, its current and its base. |
| **Caller** | A key the caller sets: whether it's **Set**, and its value. Unset reads the scaler's default. |

- A side's **Entity** fills its attributes from what that entity's attribute set starts each at, at its starting
  level. One the set doesn't give, it lacks.
- A stand-in holds attributes alone. So a number that reads an effect, an item, a tag, a retention group's part or
  Local Data reads as on an entity with none of them.
- What's typed is kept for the session, never in the asset.
- `ScalerTry` does the same in code: `new ScalerTry { From = 1, To = 5 }.Read(scaler)`.

### What can share one

Any magnitude field can share a Scaler asset, within a few rules.

- An attribute set's element shares one whose scaler is a cached kind, and its **Scaler Assets** lists only those.
- A Scaler asset can't hold a Shared scaler of its own.
- A field whose asset is missing reads as a field with no scaler. An element whose asset is missing reads its root
  value.
- A chain of shared scalers that comes back to its own asset stops after eight steps, rather than run forever.
