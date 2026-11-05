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

Everything below can be done from **Window → PlayForge → The Forge** (its Create menu), or via
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
   `RuntimeGameplayAbilitySystem` (no GameObject needed), which `ProcessControl.Register(gas).Run()` starts.

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

### Running a process or a sequence

`ProcessControl.Register` starts a registration, and `Run` makes it: it registers the process, a MonoBehaviour or
runtime process, a `TaskSequence` or a chain of them, and starts it. `HandledBy` names what the process belongs to,
which ends it as it ends, and `With` the packet it carries. Left out, an ability's packet hands the process to the
packet's source, and anything else to GameRoot. A refused registration says why:

```csharp
var result = ProcessControl.Register(projectile).HandledBy(gas).With(data).Run();
if (!result) Debug.LogWarning($"Not registered: {result.Refusal}");
```

A process that requires control waits after it registers; `relay.Run()` starts it. The `Register` overloads that take
an `out` relay still work, marked obsolete, and go before the first Asset Store release.

### Time and random numbers

PlayForge reads time through `ForgeTime` and draws random numbers through `ForgeRandom`, which read Unity's `Time` and
`Random` unless you put in your own: `ForgeTime.Use(clock)` with an `IForgeClock`, and `ForgeRandom.Use(new
SeededRandom(seed))`, whose rolls repeat from their seed whatever else draws from Unity's. Each `Use` returns what it
replaced, to put back. Each entity numbers its effects' applications (`GameplayEffectSpec.ApplicationId`) and its
abilities' activations.

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

- **The Forge** (`Window → PlayForge → The Forge`, or Ctrl+Alt+Shift+F, which focuses its search) — every
  PlayForge asset in one list beside a detail pane. The toolbar holds Create, a filter for each type, Tags, the search
  and the problem count. The list's columns sort; a click shows the asset's inspector in the detail pane, under a
  summary of its sections, and a double click shows it in the Project window. Tags lists the project's tags and what
  uses each; declare tags in a Tag Library asset (`Create → PlayForge → Tag Library`).
- **Debugger** (`Window → PlayForge → Debugger`) — the game as it plays, in three views the toolbar switches
  between. **Entities** lists every entity, of either kind, beside the one selected: its attributes with what changed
  them, its effects with the time each has left and where it came from, its abilities with their state and cooldown,
  and its tags with what grants them. It acts on the entity too: select a row, and the foot row under its table acts
  on it. Set an attribute's base and current values (a double-click on a value goes to its field), apply an effect
  or take one off, grant an ability and activate or interrupt it, or add a tag by name or from the project's tags (a
  loose tag, which Remove takes back). With Follow Selection on, it shows the entity
  you select in the scene. **Processes** lists every process with its state and how long its steps take, and
  pauses, resumes or terminates the one selected. **Tags** shows every tag the entities hold, as a tree, and who
  holds the one selected. The window menu sets how often it reads the game.
- **Asset fields** — every reference to a PlayForge asset is Unity's object field. **Open** beside it shows the asset
  in a window of its own; **New**, beside an empty one, makes the asset where Project Settings → PlayForge puts its type, named
  for the asset that holds the field. An effect field's right-click menu has its template, a pick of the effects
  nothing links, and New Linked Effect.
- **Import and Clear** — each inspector section's header has Import, Clear and Help. Import opens the Import window:
  choose a source on the left, the type's defaults first, then the assets of the type; the right lists the fields that differ,
  each checked, beside their values now and the source's. Uncheck a field to leave it. A list is replaced, unless its
  row's **Add** is on, which adds the source's items after its own. **Import N Changes** is one Undo step. The asset
  menu's **Import from Another Asset...** does the same for the whole asset, which keeps its name, description, icons,
  asset tag, template and level link. A worker group's and a requirement group's import buttons open the same window,
  listing every asset that has one. **Clear** resets the section's fields at once, and Undo takes it back.
- **Templates** — an asset's menu **Set Template...** makes it a child of a template asset of its type. A child
  takes the template's fields when the template is pushed, with the template's asset menu **Push to Its Children**, as
  one Undo step, and keeps each field it overrides: an edit to a child's field makes it the child's own, and its
  inspector marks it as Unity marks a prefab instance's override. A marked field's right-click menu has **Revert to
  Template**. Saving a template changes no child.
- **Settings** — **Project Settings → PlayForge** holds what the project shares, in
  `ProjectSettings/PlayForgeSettings.asset`, under version control: where each type's new assets go and how their files
  are named, each type's defaults (the asset a new one starts as a copy of, and the assets Import lists first), and the
  effects a new Cost or Cooldown copies. **Preferences → PlayForge** holds your own view toggles and the log level. The
  Forge's window menu opens both.
- **Golden tests** (the Test Runner's EditMode tab, `FarEmerald.PlayForge.Tests.Golden`) — the framework's behavioral
  contract; run after upgrading.
- **Problems** (`Window → PlayForge → Problems`) — every problem the validator finds in the project's PlayForge
  assets, such as a link with no provider, an impact that never applies or a field that reads a scaler it hasn't got,
  with its severity and its asset; a click selects the asset. An inspector's header counts its asset's problems. In
  batch mode, `ForgeValidator.LogAll` writes them to the Console. Add a check of your own by deriving from
  `ForgeValidationRule`.

## Logging

Framework diagnostics route through `ForgeLog`, controlled in **Preferences → PlayForge →
Logging** (Verbose / Info / Warning / Error / None).In builds, set
`ForgeLog.MinimumLevel` from code. `ProcessControl` has its own inspector toggles for
process lifecycle logs.
