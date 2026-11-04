# PlayForge — Getting Started

PlayForge is a Gameplay Ability System (GAS) framework for Unity 6+: entities own
attributes, gameplay effects modify them, abilities orchestrate effects through staged
task pipelines, and tags glue everything together for requirements, filtering and UI.

## Requirements

- **Unity 6.0+** (built and tested on 6000.0)
- **UniTask** 2.5.11 (Cysharp, MIT): add it in Package Manager by git URL, pinned to that release:
  `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`.
  A package can't declare a git dependency, so the project's manifest has to name it.
- The samples import from Package Manager's Samples tab. **Demos** needs the **Input System** package, with
  Active Input Handling set to Input System or Both; without it the sample's scripts don't compile and its
  scenes do nothing. **Code Examples** needs nothing more.
- Samples use the **Built-in Render Pipeline**.

## The pieces in one paragraph

An **EntityIdentity** asset configures an entity: its **AttributeSet** (attributes with
starting Current/Base values, clamping, regulation), starting **Abilities**, starting
**Items**, tags and level range. At runtime a `GameplayAbilitySystem` (MonoBehaviour) or
`RuntimeGameplayAbilitySystem` (plain class) instantiates that identity. **GameplayEffect**
assets change attributes; each effect's impact entry has a *delivery* that decides
everything about its lifecycle:

| Delivery | Meaning | Reversal |
|---|---|---|
| **Continuous** | modifier while the effect is active ("+20% speed") | automatic, exact |
| **OnApply** | one permanent commit ("take 30 damage") | never |
| **Periodic** | one permanent commit per tick ("5 HP/s") | never |

**Abilities** run staged task lists (targeting → apply effects → run code-driven
sequences), with cost and cooldown expressed as effects. Full pipeline reference:
[EFFECT_PIPELINE.md](EFFECT_PIPELINE.md).

## Quickstart: a damage ability in ~10 minutes

Everything below can be done from **Window → PlayForge → The Forge** (Create tab), or via
the `Create → PlayForge` asset menu.

1. **Attribute** — create `Attribute` asset "Health".
2. **AttributeSet** — create a set; add an element for Health with Base/Current `100`,
   Overflow policy `ZeroToBase` (Current can never exceed Base — pool-style).
3. **EntityIdentity** — create one; assign the AttributeSet, set level range.
4. **GameplayEffect "Fireball Damage"** — Duration policy `Instant`; impact: Attribute =
   Health, Operation = `Add`, Magnitude = `-30` (or attach a scaler for per-level values
   like −20/−30/−40/−50).
5. **Ability "Fireball"** — add a targeting task (e.g. `SelectGasTargetTask`), one stage
   with an `ApplyEffectsAbilityTask` referencing the damage effect; assign Cost/Cooldown
   effects if desired; set the level range and link the damage effect's scaler
   `LockToLevelProvider`.
6. **Scene** — add a `GameplayAbilitySystem` to a GameObject, assign the EntityIdentity,
   make sure a `ProcessControl` exists in the scene. Grant + activate the ability through
   the `AbilitySystemComponent`, or drive everything from code with
   `RuntimeGameplayAbilitySystem` (no GameObject needed).

### Code-driven abilities (sequences)

For behavior beyond declarative tasks, use `RunSequenceTask` pointing at a static
`[TaskSequenceMethod]` builder. Load the effects it applies with a `SetEffectsTask`
asset loader, and **annotate the sequence** so tooltips/analysis can decipher it:

```csharp
[TaskSequenceMethod("Ability: Chain Lightning")]
public static TaskSequence ChainLightning() =>
    TaskSequenceBuilder.Create("Chain Lightning")
        .WithAppliedEffects(Tags.EFFECTS, maxApplications: 4, "per enemy struck")
        .WithAnnotation("Chains to up to 4 nearby enemies")
        .Task(async (d, t) => { /* ... d.TryApplyEffects(target, Tags.EFFECTS) ... */ })
        .BuildSequence();
```

### Tooltips: what does this ability do?

`AbilityInspector.Build(ability)` returns a full static breakdown — every effect, its
role, target scope, and per-level numbers evaluated through the real scaler pipeline:

```csharp
var breakdown = AbilityInspector.Build(fireball);
foreach (var line in breakdown.Describe(level))   // "Deals 30 Health to target", "Cooldown: 8s"
    tooltip.AppendLine(line);

var impact = breakdown.GameplayUsages.First().Series.Impacts[0];
string perLevel = impact.FormatTotal();           // "20/30/40/50"
```

## Tooling tour

- **The Forge** (`Window → PlayForge → The Forge`) — create/browse/analyze every asset:
  level-range columns, ability description dropdowns, per-asset visualizers
  (double-click a row), balance analysis, tag declaration (Create → Tags).
- **GAS Debugger** (`Window → PlayForge → GAS Debugger`) — live entity
  state in play mode: attributes, active effects, abilities, frame impacts.
- **Golden tests** (the Test Runner's EditMode tab, `FarEmerald.PlayForge.Tests.Golden`) — the framework's behavioral
  contract; run after upgrading.
- **Audit** (`Window → PlayForge → Audit Effect Impact Deliveries`) — reports every effect's
  derived delivery and flags review-worthy authoring shapes.

## Logging

Framework diagnostics route through `ForgeLog`, controlled in **The Forge → Settings →
Logging** (Verbose / Info / Warning / Error / None). In builds, set
`ForgeLog.MinimumLevel` from code. `ProcessControl` has its own inspector toggles for
process lifecycle logs.
