# PlayForge — Scalers

A **scaler** is a value source that a magnitude field reads at a level, such as a table of damage per level. The
level is the origin's, such as the level of the ability that applies an effect. Each section below matches what the
inspector shows.

## A magnitude field

A magnitude field, such as an effect's Magnitude or Duration, is a number field (`ScalableFloat`, or `ScalableInt` for
a whole number). It shows its name, then its **kind**, then what it reads, then its field menu, which opens as wide as
its items. The kind menu lists Constant, By Level, Shared, × Attribute and Set By Caller at its top, then the kinds
that read the game (below): Asset Part, Group, Group Impact, Local Data, Relative to a Field and Worker. It files the
rest by category: the scalers of every kind under **Scaler**, Effect Stacks and Tracked Impact under **Effect**, Item
Stacks under **Item**, and a kind of a project's own under the category it names. A field whose code declares what its
context provides, with `[NumberContext]`, offers only the kinds that can be worked out there: a targeting task's range,
which has no target yet, offers no kind that reads one.

- **Constant**: the field reads its number, with its unit beside it, as Tick Every's "1 s". A field that used to read
  its scaler, and has none, reads 0, and a warning says so.
- **By Level**: the field reads a table of values per level (below). The range it gives shows beside the kind, as
  "6–8 s", or "-25 to -10" where it goes below 0.
- **Shared**: the field reads a Scaler asset (below).
- The other kinds work out a value from the values or from the entity: **× Attribute** reads an attribute of the
  caster or the target, which the menu starts as the field's number times it, **Clamped** keeps a value within bounds,
  **Conditional** chooses between two scalers, **Randomized** rolls a random value, **Threshold** steps at thresholds,
  and **Fixed Value** is one number. **Effect Stacks** reads the stack count of the effect's own container, and **Item
  Stacks** that of the item that applied it ([ITEMS.md](ITEMS.md)); each reads 1 where there's nothing stacked. An
  attribute set's values use the cached kinds, which read other attributes as the entity's values change.
- **Set By Caller** reads a value the code passes in. It checks one thing: whether what the number is read in holds a
  value under exactly its **Key**. Code sets one on an effect's spec before applying it (`spec.SetByCaller(tag,
  value)`), on an ability activation's data (`data.SetByCaller(tag, value)`), which each effect the ability makes
  takes and the ability's tasks read, or on the data that spawns a process ([EFFECTS.md](EFFECTS.md)). Only the key
  itself matches, not a tag above or below it in the tag tree, and a value set to 0 is a value. Where none is set, the
  field reads the kind's **Default**; its summary says both, as "the caller's Damage, else 0".
- A **Threshold** lists its thresholds, each a **Level**, the **Value** it reads from that level up, and an optional
  **Tier** name.
- A **Group** joins several numbers as one. It lists its members, each an **Operation** then its **Scaler**, which
  can be any kind. The members that **Add** are added up, that sum is multiplied by each member that **Multiplies**,
  then each **Flat Bonus** is added on, so `(5 + Caster's Strength) × 2 + 3` is one number. A member that
  **Overrides** gives the group's value alone; where several do, the group's policy picks the highest, the lowest,
  their average, the first or the last, and Ignore sets them aside.

Kinds that read the game, each with a summary that says what it reads:

- **Local Data** reads the number under a **Key** in Local Data: **Its Own**, what the number belongs to, as its
  ability's or its item's asset, or the **Caster**'s or the **Target**'s as the entity holds it. Where there's no
  number under the key it reads its **Default**. A hero's attack range kept in its Local Data is one field read by
  every ability.
- **Relative to a Field** reads another number field of the same object, picked from a menu of them, and the field's
  number joins it as Combine says: a Clamped scaler's Max can be 2 × its Min, and a stack bound's Max its Min + 3. The
  other field is read in the same context, so a relative Max follows a Min that scales by level. A field offers this
  kind where its code hands its object over as it reads (below).
- **Tracked Impact**, under Effect, reads what an effect has dealt: its **Last** impact, its **Total**, or its impact
  **Count**. Whose: **This Effect**, as a burn that grows by its own tick count; **An Effect** on the caster or the
  target, by its asset; or **Effects Tagged** there, added up. An impact is as it landed, after the target's workers,
  and signed, so damage is negative; **By Size** reads it positive, as a heal of half a blow wants. Before an effect's
  first impact, and after a load, it reads 0.
- **Group Impact** reads one retention group's part of an attribute on the caster or the target, as "Caster's Strength
  (Bonus)": the Strength items add, and none of the hero's own.
- **Asset Part** finds effects, abilities or items on the caster or the target by a tag, and reads a part of them.
  An effect matches by its asset tag, a context tag or a tag it grants, and an ability or an item by its asset tag or
  a context tag; a tag under the one given matches too.
  - Of an **Effect**: its Count, Stacks, Level, Last Impact, Total Impact, Impact Count, Duration Remaining, Duration,
    Time Until Tick, or a Worker Value, what that effect's workers answer under a key.
  - Of an **Ability**: its Count, Level, Is Active, Is on Cooldown, or Cooldown Remaining.
  - Of an **Item**: its Count, Stacks, Level, or Is Equipped.
  - **Among** says how several that match read as one: their Sum, the First, the Highest or the Lowest. A true-or-false
    part reads 1 or 0, so its sum is how many are so. Where none matches, a count reads 0 and any other part the
    **Default**.
- **Worker** asks workers for a number under a **Key**: **This Effect**'s own workers, or the **Caster**'s or the
  **Target**'s. Where none answers it reads its **Default**. See Asking a worker, below.

Choosing a kind is one Undo step. A constant made By Level keeps its number as its first level's value.

With more than one asset selected, the field edits each asset's own field. Kinds that differ show as mixed, and choosing
one gives it to each field, from that field's own value; a field of that kind keeps its scaler. Levels and Make Constant
work on each field's own values, and a count, range or curve the fields disagree on shows as mixed. Save as Scaler Asset
waits for one asset.

A kind other than Constant shows a one-line summary of what the field reads under its row, as "0.3 × Target's Health
(base)", then **Combine**, how the field joins its number with the scaler: **Scaler** reads the scaler's value,
**Scaler + Number** and **Scaler × Number** add or multiply the field's number, and **Number Only** reads the number. A
whole-number field, such as Ticks, also shows its **Rounding**: Floor, Ceil, or Round, to the nearest, a half away
from zero. A field whose unit is "%" stores 0.3 and shows and takes it as 30%. A kind its field can't work out, as one
copied from a field that has a target into one that hasn't, says so under the field, and the Problems window lists it;
it reads the field's number.

The field menu holds:

- **Save as Scaler Asset…**: a name and a folder, then **Save and Use**, which saves the field's scaler as a Scaler
  asset and makes the field share it.
- **Use a Scaler Asset…**: a list of the project's Scaler assets. The field shares the one you pick.
- **Import Values…**: a window that lists the Scaler assets and the other assets' fields with values per level. Pick
  one to compare its values with the field's, level by level, with both curves, then **Copy Values**. A shorter table's
  last value holds for the levels past its end, and the window says which levels those are. One Undo takes it back.
- **Quick Fill…**: fills the table from a pattern: **Same Value**, **Straight Line**, **Slow Then Fast** and **Fast
  Then Slow** with a **Strength**, **Steps**, **Add Each Level**, **Multiply Each Level**, **Ease In and Out**, or a
  **Custom Curve** you draw, from one value to another. **Rounding** makes the values whole numbers or rounds them
  to a **Step**. A preview shows the values as the table will, with their curve, and one Undo takes them back.
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
    between them. Three values, 10, 20 and 30, spread over levels 1 to 5 read 10, 15, 20, 25 and 30. The points are
    numbered, and under them each level shows what it reads. A scaler you make in the inspector starts Per Level. The scalers saved before the choice
    existed spread, so they read as they did.
- **Levels**, Per Level: an asset with a level provider, as an ability, or an effect linked to one, follows the
  provider's levels, as **Fireball's levels (1–4)**, with a value for each. A level past the scaler's values shows the
  last in secondary text; type there to give it a value of its own. **Own levels** gives the scaler a count of its own.
  An asset whose levels come from whatever uses it, as an unlinked effect, an attribute set or a Scaler asset, shows
  the count alone. A new level takes the last value.
- **Over Levels**, spread: the provider's levels, **Own levels** up to the scaler's own **Max Level**, or the
  provider's **up to a level**: its levels, up to the Max Level, so the values reach the last at the lower of the
  Max Level and the provider's last level, and a level past it reads the last value. **Points**: how many values.
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

A **kind of number** of your own is simpler: derive from `NumberKind` and override its one method,
`Evaluate(in NumberContext context)`, which reads what the field's site has: `context.Caster`, `context.Target`,
`context.Level`, the caller's magnitudes, Local Data, an attribute on either side, and more; `context.Has(...)` says
which. `Describe()` gives the field's summary, as "the distance to the target". `[NumberKind("Distance to Target",
Category = "Space", Reads = NumberReads.Caster | NumberReads.Target)]` names it, files it in the kind menu, and says
what it reads, so a field offers it only where it can be worked out; where it can't, the field reads its number. A kind
is a scaler, so it also works in an effect's magnitude and inside a Group or a Conditional, and its fields draw as a
scaler's do. Test one without an effect: `number.Evaluate(NumberContext.For(caster, target, level))`. A kind is saved
by its class's name, so renaming the class needs Unity's `[MovedFrom]`. PlayForge's own kinds, Local Data, Tracked
Impact, Asset Part and the rest, are written this way, in `Runtime/PlayForge/Numbers/Kinds`.

## A number field in your own code

A field of your own is one line, and a float converts to it:

```csharp
[Tooltip("How far it reaches")]
[NumberContext(NumberReads.Level | NumberReads.Caster, Unit = "m")]
[NumberMin(0)]
public ScalableFloat Range = 20f;
```

- Read it with `Range.Evaluate(context)`, with a `NumberContext` built `Of` what the code has: an effect's spec, an
  activation's data, a worker's context, an entity, a cue or a process. `Range.Evaluate(context, this)` also hands the
  field's object over, which is what Relative to a Field reads; a field read that way declares `NumberReads.Owner`, or
  `NumberReads.All`, and its kind menu then offers that kind.
- `[Tooltip]` shows on the field's name.
- `[NumberMin(0)]`, `[NumberMax(10)]` and `[NumberRange(0, 1)]` limit the constant typed in the inspector, and a range
  sets it on a slider. On a field whose unit is "%" the limit is in what the field stores, so `[NumberRange(0, 1)]` is
  0% to 100%, and a whole-number field takes the whole numbers inside the limits. They limit the constant alone: a
  number of another kind is worked out as the game runs, so where what it reads must stay inside limits, give it the
  Clamped kind or clamp what the code reads.
- Unity's own `[Min]` and `[Range]` don't work on a number field: Unity gives the field to that attribute's drawer,
  which draws "Use Range with float or int" where the number was. As scripts load, the console names each number
  field that has one, with the attribute to use in its place.

## Asking a worker

A number can't count or remember; a worker can. So a number of the **Worker** kind asks workers for a value under a
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

- An **effect's workers** answer about one application of their effect. A worker is one object on its effect's asset,
  shared by every application, so what it counts for each is kept on the spec: `ctx.GetState<State>(this)` in its
  events, and `spec.GetWorkerState<State>(this)` as it answers.
- An **entity's workers**, those its worker groups give it, answer about the entity. A worker group installs the
  ones that answer as it's given, and code that adds a worker itself calls `NumberWorkers.Provide(entity, worker)`.
- The first worker that answers the key gives the value. Where none does, the number reads its Default.
- Any number field can ask: an effect's magnitude, a task's range, a threshold. Code can ask too, with
  `NumberWorkers.TryAsk`. An Asset Part's Worker Value asks the workers of each effect it finds.

The **Attribute Change** effect worker is one that answers. It notes an attribute as each application of its effect
begins, and answers how much the attribute has **Lost**, **Gained** or **Changed** since, or what it was **At
Start**. With **Every Ticks** above 0 it answers only on each Nth tick, and 0 on the others. A curse that deals 5 a
second, and every fourth second a quarter of the health its target has lost since it landed, from every source, is one
periodic effect: its first impact is −5, and its second is a Worker number, −0.25 × the worker's answer, with the
worker watching the target's Health, Lost, Every Ticks 4.

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
