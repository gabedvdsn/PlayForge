# Number fields: design for review

A number in a PlayForge asset is either a bare `float` or `int`, or, for an effect's numbers, a magnitude field: a
number, a scaler and how the two combine. This design makes one number type for every authored gameplay number. By
default it is a plain number, as now, but it can also be a value per level, a multiple of an attribute, a value its
caller sets, and more. Nothing is built until the user answers the questions at the end.

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
| **Scaler** | Any scaler, and how it combines with the number. As a magnitude field does today. | A Conditional, a Group |

There are precedents for this:

- **Unreal's GAS**, which PlayForge models, has `FScalableFloat`, a number with an optional curve by level, used
  across abilities. Its effect magnitudes come in four kinds: scalable float, attribute based, set by caller and
  custom.
- **Unity's** particle system has `MinMaxCurve`: a value that is a constant, a curve or a random range, chosen from a
  small menu beside the field.
- **PlayForge's own** magnitude field (ED-33, ED-49) already draws a number with a kind menu (Constant or a scaler)
  and a Combine choice.

## Recommended shape

### One type, grown from the magnitude field

- `ScalerMagnitudeOperation` becomes the number type, `ScalableFloat`, and `ScalerIntegerMagnitudeOperation` becomes
  `ScalableInt` (the name is question 1). They keep their saved field names, `Magnitude`, `RealMagnitude`, `Scaler`
  and `Rounding`, so every magnitude saved today loads unchanged. The old names stay one version as obsolete
  subclasses, so code that names them still compiles.
- The kinds are presets over what's already there, not a second hierarchy:
  - Constant is "Number Only".
  - By Level is a Simple scaler.
  - × Attribute is an Attribute Backed scaler, as "Scaler × Number".
  - Set By Caller is the Set By Caller scaler.
  - Local Data is a new Local Data scaler.
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
- **Units.** A field can declare a unit, as Duration declares "s". Whether "%" should show 0.3 as 30% is question 5.

## Where it applies

**Convert,** about 30 fields, each read where a context exists:

| Area | Fields |
|---|---|
| Scalers | Conditional's threshold, and its True and False branches; Clamped's Min and Max, and its Inner (FEAT-27); Randomized's Variance, Min and Max; Set By Caller's Default; Threshold's tier values and Default. |
| Ability tasks | Auto Target's and Auto Target By Affinity's Range, Max Targets and attribute condition; Select Gas Target In Range's range; Select Direction's Min and Max Distance; Channel's duration; Delay's and Apply Effects' delays; Run Process's Max Duration. |
| Validation | Attribute Validation's value. FEAT-26's Share Of Base becomes "× Attribute", and the switch is deprecated. |
| Workers | Threshold and Death Watcher's threshold, which makes a low-health flag possible; Relative attribute workers' multiplier; Target Attribute Threshold's threshold. |
| Stacking | Stack Bounds' Min and Max, so a burn's most stacks can grow with its level; Initialization's stacks. |
| Cues | Camera Shake's strength and duration, Prefab's lifetime, Sound's volume. |

**Already a number:** an effect's magnitude, duration, ticks, tick interval, extra ticks, stack amount and chance to
apply; a process's attributes; an entity's limits. They stay as they are, renamed. An attribute set element's sides
stay cached: see concern 6.

**Keep plain,** each for a reason:

- **Levels:** Starting and Max Level on abilities, items, entities, sets and scalers. A number by level can't
  define the levels it reads.
- **A scaler's own table and curve.** They are the By Level kind's data.
- **A Constant scaler's value.** It is the constant.
- **Capacities read with no context,** such as an item's Max Stack Size, which the item system and the UI read
  without an owner.
- **Ordering keys and priorities, and settings:**
  - Buffer Priority, Sort Order, a config's Priority and a process's step priority;
  - mouse buttons, the fixed tick rate and the most ticks a frame.
- **Tag weights in requirements,** such as a tag query's count or a tag worker's required weight. They are checked
  on a hot path with no context.
- **Attribute bounds:** Floor, Ceil and Snap Interval. A bound relative to another attribute needs the cached
  attribute system to track it (question 7).
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
3. **Inspector weight.** Thirty more fields gain a menu. A constant stays one row, and the menu offers only the kinds
   that resolve.
4. **Hidden coupling.** A number that reads the target's state makes an asset harder to reason about. The summary
   line and the per-level preview show it. A later step could show evaluated values in the Debugger.
5. **Cycles.** A number inside a scaler can hold a scaler, which can hold a number. The picker refuses an ancestor,
   and evaluation stops at a depth guard.
6. **Cached attribute sets.** An attribute set element and its cached scalers recompute when the attributes they
   declare change. A number there keeps to Constant and By Level, which the cache can track.
7. **Asset size and diffs.** A number saves about four YAML lines, not one. A constant writes its kind and an empty
   scaler.
8. **The API changes.** A public float field becomes a `ScalableFloat`, so code reading it needs
   `Evaluate(context)`. PlayForge isn't 1.0 yet. The CHANGELOG lists each field under Changed. The float conversion
   keeps assignments compiling.
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
   - the capability attribute, the inspector's kinds and the Problems rule;
   - the legacy-field pattern and the upgrade command.
2. **Scaler parameters,** starting with the user's case as the first red test.
3. **Tasks and validation.**
4. **Workers and stacking.**
5. **Cues, then adoption and docs.** The Demo and Battle adopt the type where it shows something, and the docs follow.

## Questions for the user

1. **The name:** `ScalableFloat` and `ScalableInt`, after Unreal's term, or `ForgeFloat` and `ForgeInt`, or something
   else?
2. **One type:** should the magnitude field become this type (recommended), or should a new, lighter type sit beside
   it?
3. **The kinds:** Constant, By Level, × Attribute, Set By Caller, Local Data and Scaler. Is "relative value" what
   you meant by × Attribute, or did you mean something else as well, such as relative to another field or to its own
   default? Should any kind be added, such as a random range, or dropped?
4. **Timing:** should a number be read once at its site's natural moment (recommended), or each time it's used?
5. **Percentages:** should a share field show 0.3 as "30%"?
6. **Migration:** should old floats move on load, with an upgrade command, and the legacy fields be removed after one
   version?
7. **Attribute bounds:** should a Floor or Ceil be able to be relative to another attribute, such as Health at most
   10 × Strength? Now, later, or never?
8. **Scope:** is there anything in the keep-plain list you want converted? For example, an item's Max Stack Size by
   level, or tag weights in requirements.
