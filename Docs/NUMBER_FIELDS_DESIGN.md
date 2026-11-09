# Number fields: the design

A number in a PlayForge asset is either a bare `float` or `int`, or, for an effect's numbers, a magnitude field: a
number, a scaler and how the two combine. This design makes one number type for every authored gameplay number. By
default it is a plain number, as now, but it can also be a value per level, a multiple of an attribute, a value its
caller sets, and more.

**Status:** decided 2026-11-08 (Decision 40, FEAT-29). The user answered the eight questions; the answers are under
Decisions at the end, and the sections below follow them.

## The problem

The user's case: an effect deals 10% of the target's max health when the target's health is below 30%.

- The damage half can be authored today. A magnitude field can be "Scaler × Number": the number −0.1 times an
  Attribute Backed scaler reading the target's base Health.
- The threshold half can't. `ConditionalScaler.ThresholdValue` is a bare float, compared with the attribute's value,
  so "below 30%" has to be a fixed number of health, right for one target and wrong for the next. FEAT-26 gave
  `AttributeValidation` a share-of-base switch for the same reason. That fixes one field at a time.

The general problem is that **only an effect's own numbers can scale.** A census of the runtime finds 121 numeric
fields. Most are runtime state, save data, ordering keys and settings. About 35 are numbers a designer authors, and
none of them can scale: scaler parameters, thresholds, ranges, delays, durations, multipliers and cue settings. A
further 19 already wrap a number: an effect's magnitude, duration, ticks, tick interval, chance and stack amount, a
process's attributes, an entity's limits, and an attribute set element's two sides.

## The idea

There is one number type for every authored gameplay number. It is a plain number by default, and it can be one of
these kinds:

| Kind | What it means | Example |
|---|---|---|
| **Constant** | The number. The default, and what every field is today. | `20` |
| **By Level** | A value for each level of the asset's level provider. | Range 8, 9, 10, 12 |
| **× Attribute** | The number times an attribute of the caster or the target, its current or its base value. | `0.3 × Target's Health (base)` |
| **Set By Caller** | The value the caller set under a key, or the number where it set none. | The AI's difficulty |
| **Local Data** | A value in the ability's, item's or entity's Local Data, under a key. | A hero's attack range |
| **Relative to a field** | The number times, or plus, another number field of the same object. | A Clamped scaler's Max as `2 × Min` |
| **Scaler** | Any scaler, and how it combines with the number. As a magnitude field does today. | A Conditional, a Group |

There are precedents for this:

- **Unreal's GAS**, which PlayForge models, has `FScalableFloat`, a number with an optional curve by level, used
  across abilities. Its effect magnitudes come in four kinds: scalable float, attribute based, set by caller and
  custom.
- **Unity's** particle system has `MinMaxCurve`: a value that is a constant, a curve or a random range, chosen from a
  small menu beside the field.
- **PlayForge's own** magnitude field (ED-33, ED-49) already draws a number with a kind menu (Constant or a scaler)
  and a Combine choice.

## The design

### One type, grown from the magnitude field

- `ScalerMagnitudeOperation` becomes the number type, `ScalableFloat`, and `ScalerIntegerMagnitudeOperation` becomes
  `ScalableInt`. They keep their saved field names, `Magnitude`, `RealMagnitude`, `Scaler` and `Rounding`, so every
  magnitude saved today loads unchanged. The old names stay one version as obsolete subclasses, so code that names
  them still compiles.
- The kinds are presets over what's already there, not a second hierarchy:
  - Constant is "Number Only".
  - By Level is a Simple scaler.
  - × Attribute is an Attribute Backed scaler, as "Scaler × Number".
  - Set By Caller is the Set By Caller scaler.
  - Local Data is a new Local Data scaler.
  - Relative to a field is the one kind that isn't a scaler, as it reads its own object (below).
  - Scaler is any scaler.

  The inspector names the kind it recognises, and the data stays what scalers already are.
- In code: `public ScalableFloat Range = 20f;` converts from a float, so field initialisers and the code that builds
  assets (tests, Battle, the Demo's builder) keep compiling. A read is `Range.Evaluate(context)`. `Range.IsConstant`
  and `Range.Constant` serve editor previews and setup code. There is no implicit conversion back to a float, as it
  would silently drop a kind.

### What a number is evaluated against

- **The current state.** Scalers evaluate against an impact carrier, and the carrier is almost always an effect spec.
  Most of the new sites have no spec:
  - a task has its activation's data;
  - an attribute worker has the change it's working on;
  - an analysis worker has only its entity;
  - a cue has its effect's spec.

  The one adapter, the leveler derivation that process attributes use, allocates on each call, and it reports its own
  source as its target.
- **The context.** A `NumberContext` struct carries what a site has: the source, the target, the level, a carrier if
  there is one, the caller's set-by-caller magnitudes, and the asset whose Local Data it reads. It is built from an
  effect spec, an activation's data, a worker's context, an entity, a cue's context or a process.
- **Evaluation.** A non-constant kind evaluates its scaler through the context's carrier, or through a reusable
  carrier the context fills, so nothing allocates.
- **A gap this closes.** Set By Caller reads only from an effect spec today (`SetByCallerScaler` checks
  `deriv is GameplayEffectSpec`). A task or a process that reads one gets its default. With the context, it reads
  from anything that holds magnitudes.

### What each field's context offers

- **The declaration.** Each converted field says what its context provides, much as tag fields declare their
  contexts with `[ForgeTagContext]`. For example, a targeting task's range has `[NumberContext(Level | Caster | Caller | LocalData)]`.
  It has no target yet, because the target is what it's looking for.
- **The inspector** offers only the kinds that can resolve at that field.
- **The Problems window** flags a number whose kind can't resolve where it is, for example an asset copied from
  another field.
- **At runtime,** a kind that can't resolve falls back to its number, and logs once.

### Relative to another field

- **What it is.** The number combines with another number field of the same object: times it, or plus it. A Clamped
  scaler's Max can be `2 × Min`, a Randomized scaler's Max `1.5 × Min`, a stack bound's Max `Min + 3`.
- **Picking.** The inspector offers the object's other number fields, plain or scalable, by name. It leaves out any
  field that would read this one back, so `Min` relative to `Max` relative to `Min` can't be made. The Problems window
  flags a name that no longer matches a field, as after a rename.
- **Evaluation.** The object's own code evaluates its numbers with itself as the owner,
  `MaxValue.Evaluate(context, this)`. A relative number finds its sibling by name through an accessor made once for
  each type and field, then cached, so it allocates nothing after the first read. The sibling is evaluated with the
  same context, so a relative Max follows a Min that scales by level. Without an owner, the number falls back to its
  own value and logs once. A depth guard stops a loop that saved data still holds.

### Attribute bounds

- **What changes.** An attribute set element's Floor and Ceil, the bounds its overflow policy clamps to, become
  numbers, each side current and base. Health can be at most `10 × Strength`, or a resistance at most a level's
  value.
- **Their context** is the entity itself: its own attributes and its level, as everything in a set is. There's no
  target and no caller.
- **Dependency tracking.** The cached attribute system already records which attributes a cached scaler reads
  (`AttributeRegulationCache.RegisterRelation`), and recomputes an attribute when one it reads changes. A bound
  registers the attributes it reads the same way, so when Strength changes, Health's bounds work out again and
  Health clamps to them at once.
- **Loops.** Health bounded by Strength while Strength is bounded by Health is a loop. The set refuses it as the entity
  starts, names both attributes, and keeps the constant bound. The inspector warns as the second bound is made.
- **Cost.** A bound works out again only when an attribute it reads changes, not on every change to the attribute it
  bounds. A constant bound costs nothing new.

### Tag weights and an item's stack size

- **Tag weights.** A tag query's count, as "at least 3 stacks of Burning", and a tag worker's required weight become
  whole numbers that can scale. Today a requirement check sees only the tags, so the checks gain a context:
  - an ability's requirements are checked with the ability's own context: its caster, its target and its level;
  - an effect's application, ongoing and removal requirements with the effect's spec;
  - a tag worker's with its entity;
  - a targeting task's with the activation's data;
  - a Conditional scaler's tag condition with its carrier.

  A constant count stays a field read, so the checks that run on every tag change cost what they do now. A shared
  Requirement Template works its numbers out in each user's context.
- **An item's Max Stack Size** becomes a whole number that can scale. Its context is the item's owner and the item's
  level, so a stack can grow with the item's level or the owner's attributes. A reader with no owner, as a shop's
  UI, reads its number. A stack that's bigger than its new size, as after a level goes down, keeps what it holds and
  takes no more until it's below the size.

### When a number is read

- **Once per moment.** Each site reads its numbers once, at a natural moment, and keeps the values:
  - an effect as it's applied, as it does now;
  - a task as it starts;
  - a cue as it fires;
  - a worker per change.

  A read every frame would make a Randomized scaler jitter and cost more each frame.
- **Rolls.** A Randomized scaler rolls once per context, which extends Decision 12.

### The inspector

- **The model** is the magnitude field.
  - Every number takes one row: its label, its number, and a kind menu.
  - A non-constant kind opens its parts below the row, with a one-line summary, such as "0.3 × Target's Health (base)".
  - Where levels apply, the row shows the value at each level.
- **A Constant** looks as a float field does now, plus the menu.
- **Units.** A field can declare a unit, as Duration declares "s". A field that declares "%" stores 0.3 and shows and
  takes it as 30%.

## Where it applies

**Convert,** about 35 fields, each read where a context exists:

| Area | Fields |
|---|---|
| Scalers | Conditional's threshold, and its True and False branches; Clamped's Min and Max, and its Inner (FEAT-27); Randomized's Variance, Min and Max; Set By Caller's Default; Threshold's tier values and Default. |
| Ability tasks | Auto Target's and Auto Target By Affinity's Range, Max Targets and attribute condition; Select Gas Target In Range's range; Select Direction's Min and Max Distance; Channel's duration; Delay's and Apply Effects' delays; Run Process's Max Duration. |
| Validation | Attribute Validation's value. FEAT-26's Share Of Base becomes "× Attribute", and the switch is deprecated. |
| Workers | Threshold and Death Watcher's threshold, which makes a low-health flag possible; Relative attribute workers' multiplier; Target Attribute Threshold's threshold. |
| Stacking | Stack Bounds' Min and Max, so a burn's most stacks can grow with its level; Initialization's stacks. |
| Cues | Camera Shake's strength and duration, Prefab's lifetime, Sound's volume. |
| Attribute bounds | A set element's Floor and Ceil, current and base, with dependency tracking (above). |
| Requirements | A tag query's count and a tag worker's required weight (above). |
| Items | An item's Max Stack Size (above). |

**Already a number:** an effect's magnitude, duration, ticks, tick interval, extra ticks, stack amount and chance to
apply; a process's attributes; an entity's limits. They stay as they are, renamed. An attribute set element's sides
stay cached: see concern 6.

**Keep plain,** each for a reason:

- **Levels:** Starting and Max Level on abilities, items, entities, sets and scalers. A number by level can't
  define the levels it reads.
- **A scaler's own table and curve.** They are the By Level kind's data.
- **A Constant scaler's value.** It is the constant.
- **Ordering keys and priorities, and settings:**
  - Buffer Priority, Sort Order, a config's Priority and a process's step priority;
  - mouse buttons, the fixed tick rate and the most ticks a frame;
  - an ability's input buffer window;
  - an attribute's Snap Interval, a rounding setting.
- **Values, not definitions:** runtime state, save data, Local Data payloads and introspection rows.

## Concerns

1. **Saved data.** A float field that becomes a number loses its saved value unless it's moved. Unity can't read a
   scalar into a class. The plan has three parts:
   - Each converted field keeps a hidden legacy float, renamed with `[FormerlySerializedAs]`. The owner's
     `OnAfterDeserialize` moves it into the number's constant. This works in the editor and in a build.
   - A command re-saves every PlayForge asset in a project, so the new form is written.
   - The legacy fields go after one version. Tests load old YAML fixtures.

   Magnitude data needs none of this.
2. **Hot paths and Phase 3's zero-allocation gate.**
   - A constant costs one branch.
   - Contexts are structs, and the bridge carrier is reused.
   - A threshold an analysis worker reads every frame gets an allocation probe, as `EffectCostTests` and
     `SequenceCostTests` have.
   - Requirement checks run on every tag change. A constant count must stay a field read there, and a probe holds it
     to that.
3. **Inspector weight.** Thirty more fields gain a menu. A constant stays one row, and the menu offers only the kinds
   that resolve.
4. **Hidden coupling.** A number that reads the target's state makes an asset harder to reason about. The summary
   line and the per-level preview show it. A later step could show evaluated values in the Debugger.
5. **Cycles.** A number inside a scaler can hold a scaler, which can hold a number. The picker refuses an ancestor,
   and evaluation stops at a depth guard.
6. **Cached attribute sets.** An attribute set element and its cached scalers recompute when the attributes they
   declare change. A number there, as a bound, registers what it reads in the same way (Attribute bounds, above). Its
   context has no target and no caller, so the inspector doesn't offer those kinds there.
7. **Asset size and diffs.** A number saves about four YAML lines, not one. A constant writes its kind and an empty
   scaler.
8. **The API changes.** A public float field becomes a `ScalableFloat`, so code reading it needs
   `Evaluate(context)`. The requirement checks take a context, for their tag weights. PlayForge isn't 1.0 yet. The
   CHANGELOG lists each change under Changed. The float conversion keeps assignments compiling.
9. **Templates.** A number is a composite. Template propagation tracks its number and kind as leaves and its scaler
   as one unit, as it does for magnitudes now.
10. **Integers.** A whole-number field rounds. The rounding choices today are Floor and Ceil; this adds Round.
    Counts that can't be negative, such as Max Targets, clamp at 0.
11. **Docs and tests.**
    - SCALERS.md gains a Numbers section.
    - Each converted field keeps its constant behaviour, which the existing suites hold.
    - Each converted field gets a test of one non-constant kind.

## The user's case, authored

The case is an effect, Execution. Its magnitude's scaler is a Conditional:

- **Condition:** Target Attribute Threshold, Health (current), less than **`0.3 × Target's Health (base)`**.
- **True:** **`−0.1 × Target's Health (base)`**.
- **False:** **`0`**.

The same threshold works for an ability that's usable only on a hurt target: an Attribute Validation of Health less
than `0.5 × Target's Health (base)`.

## Phasing

Each phase gets its own tracker rows, with red tests first, the full suites and the Demo's tests.

1. **Foundation.** This covers:
   - the type, the context, and the reusable carrier;
   - Set By Caller from any context, and the Local Data scaler;
   - Relative to a field, its accessor cache and its loop guard;
   - the capability attribute, the inspector's kinds, percent display and the Problems rules;
   - the legacy-field pattern and the upgrade command.
2. **Scaler parameters,** starting with the user's case as the first red test.
3. **Tasks and validation.**
4. **Workers, stacking and an item's stack size.**
5. **Tag weights in requirements:** the checks take a context, and the zero-allocation probes cover them.
6. **Attribute bounds:** Floor and Ceil as numbers, registered in the regulation cache, with the loop check.
7. **Cues, then adoption and docs.** The Demo and Battle adopt the type where it shows something, and the docs follow.

## Decisions (Decision 40)

The user's answers of 2026-11-08:

1. **The name:** `ScalableFloat` and `ScalableInt`.
2. **One type:** the magnitude field becomes this type.
3. **The kinds:** the six proposed, plus Relative to another field. × Attribute is the relative value meant: a
   multiple of an attribute.
4. **Timing:** a number is read once, at its site's natural moment.
5. **Percentages:** a field that declares "%" shows 0.3 as 30%.
6. **Migration:** old floats move on load through hidden legacy fields, an upgrade command re-saves a project's
   assets, and the legacy fields go after one version.
7. **Attribute bounds:** now, in this refactor, with dependency tracking.
8. **Scope:** an item's Max Stack Size and tag weights in requirements convert too. The rest of the keep-plain list
   stays plain.
