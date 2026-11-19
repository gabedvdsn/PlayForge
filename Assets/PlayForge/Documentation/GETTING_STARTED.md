# PlayForge — Getting Started

PlayForge is a Gameplay Ability System (GAS) framework for Unity 6+. In it:

- entities own attributes;
- gameplay effects modify them;
- abilities orchestrate effects through staged task pipelines;
- tags glue everything together for requirements, filtering and UI.

## Requirements

PlayForge needs Unity 6 and UniTask, and installs a little differently from the Asset Store and from git.

- **Unity 6.0+**, built and tested on 6000.0.
- **UniTask** 2.5.11 (Cysharp, MIT). How you get it depends on how you install PlayForge, as below.

From the Asset Store:

- PlayForge installs in `Assets/Plugins/PlayForge`, and your Forge Projects go in `Assets/PlayForge`, so the two never
  share a folder.
- PlayForge includes UniTask, in `PlayForge/ThirdParty/UniTask`. A project that already has UniTask from Package
  Manager deletes that folder.
- The samples are in `PlayForge/Samples`.

Installed as a package from git:

- PlayForge needs UniTask added in Package Manager by git URL, pinned to that release:
  `https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11`.
- A package can't declare a git dependency, so the project's manifest has to name UniTask.
- The samples import from Package Manager's Samples tab.

The samples need nothing more:

- **Battle**: a battle that plays itself.
- **Demo**: a hero arena to pick heroes, give items and fight. It reads the Input System where the project has it, and
  the Input Manager otherwise.
- **Task Sequence Examples** and **Process Group Examples**.

The samples render in the **Built-in Render Pipeline** or **URP**. HDRP isn't supported.

## The pieces in one paragraph

This section shows how PlayForge's pieces fit together.

An **EntityIdentity** asset configures an entity:

- its **AttributeSet**: attributes with starting Current/Base values, clamping and regulation;
- its starting **Abilities** and starting **Items**;
- its tags and its level range.

At runtime, a `GameplayAbilitySystem` (a MonoBehaviour) or a `RuntimeGameplayAbilitySystem` (a plain class)
instantiates that identity.

**GameplayEffect** assets change attributes. Each effect's impact entry has a *delivery*, which decides everything
about its lifecycle:

| Delivery | Meaning | Reversal |
|---|---|---|
| **Continuous** | modifier while the effect is active ("+20% speed") | automatic, exact |
| **OnApply** | one permanent commit ("take 30 damage") | never |
| **Periodic** | one permanent commit per tick ("5 HP/s") | never |

**Abilities** run staged task lists (targeting → apply effects → run code-driven sequences). Their cost and cooldown
are expressed as effects. [EFFECT_PIPELINE.md](EFFECT_PIPELINE.md) is the full pipeline reference.

To go further:

- [RECIPES.md](RECIPES.md) builds ten common rules from these pieces with no code, as a crit, an execute and a counter,
  each in the Additional Content sample.
- [WORKERS.md](WORKERS.md) maps when each kind of worker runs, and for whom.
- [CUES.md](CUES.md) brings together what effects, abilities, entities and items show and sound: their cues, and the
  Cue Library that routes cue tags to them.

## Quickstart: a damage ability in ~10 minutes

This quickstart builds a damage ability, Fireball, from its attribute up to a scene that runs it.

You can make each asset below from **Window → PlayForge → The Forge**, with its Create menu. You can also use the
`Create → PlayForge` asset menu, which lists the Forge Project, then the assets The Forge lists, in its order, then the
numbers, rules and the Tag Library.

1. **Attribute**: create an `Attribute` asset named "Health".
2. **AttributeSet**: create a set, and add an element for Health:
   - Base/Current `100`;
   - Overflow policy `ZeroToBase`, so Current can never exceed Base (pool-style).
3. **EntityIdentity**: create one. Assign the AttributeSet, and set its level range.
4. **GameplayEffect "Fireball Damage"**: set its Duration policy to `Instant`, and give it an impact:
   - Attribute = Health, Operation = `Add`, Magnitude = `-30`;
   - or attach a scaler for per-level values, like −20/−30/−40/−50.
5. **Ability "Fireball"**:
   - Add a targeting task, for example `SelectGasTargetTask`.
   - Add one stage, with an `ApplyEffectsAbilityTask` referencing the damage effect.
   - Assign Cost and Cooldown effects, if you want them.
   - Set the level range, and link the damage effect's scaler with `LockToLevelProvider`.
6. **Scene**:
   - Add a `GameplayAbilitySystem` to a GameObject, and assign the EntityIdentity.
   - Bring PlayForge up with a `Bootstrapper`: the prefab PlayForge makes in `Assets/PlayForge/Bootstrap` as your
     first Forge Project is made, or a GameObject with the component, naming the Forge Project the scene plays.
   - The `Bootstrapper` makes ProcessControl and the GameRoot, which runs as the project's Game Root entity.
   - Grant and activate the ability through the `AbilitySystemComponent`.

Instead of a GameObject, you can drive everything from code with `RuntimeGameplayAbilitySystem`, which needs none.
`ProcessControl.Register(gas).Run()` starts it.

### Code-driven abilities (sequences)

For behaviour beyond declarative tasks, an ability runs a code-driven sequence.

1. Add a `RunSequenceTask` pointing at a static `[TaskSequenceMethod]` builder.
2. Load the effects the sequence applies with a `SetEffectsTask` asset loader.
3. In the builder, **annotate the sequence**, so tooltips and analysis can decipher it:

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

You run a process or a sequence from code through `ProcessControl.Register`. It starts a registration, and `Run` makes
it.

| Call | What it does |
|---|---|
| `ProcessControl.Register` | Starts a registration of a process: a MonoBehaviour or runtime process, a `TaskSequence`, or a chain of them. |
| `HandledBy` | Names what the process belongs to, which ends it as it ends. |
| `With` | Names the packet the process carries. |
| `Run` | Registers the process, and starts it. |

Left out, `HandledBy` follows the packet: an ability's packet hands the process to the packet's source, and anything else
hands it to GameRoot. A refused registration says why:

```csharp
var result = ProcessControl.Register(projectile).HandledBy(gas).With(data).Run();
if (!result) ForgeLog.Warning($"Not registered: {result.Refusal}");
```

A process that requires control waits after it registers. `relay.Run()` starts it.

#### Processes an ability spawns

A process an ability's **Create Mono Process** task spawns takes the ability as its origin. So the effects it applies
are the caster's, at the ability's level.

The task's **Spawn At** says where the process appears:

| Spawn At | Where the process appears |
|---|---|
| **At the Caster** | At the caster, as a projectile does. |
| **At the Target** | At the ability's target, as a fire on the ground does. |
| **As Its Data Says** | Where the data's Position says. |

A gameplay mono process, as `ProjectileMonoProcess`, carries attributes of its own in **Attributes**. Each entry has:

- an attribute;
- the role it's read by, as a projectile reads its speed as ProjectileSpeed;
- where it starts: its own value, the caster's attribute, or the caster's where the caster has it.

Its own value is a number, or a scaler read at the spawning ability's level. The data that spawns the process can set
an attribute under its role. The process's code reads and sets its attributes with `TryGetRole`, `SetRole`,
`TryGetAttribute` and `SetAttribute`.

To go further:

- [PROCESSES.md](PROCESSES.md) covers processes, their lifecycles, pausing and groups.
- [TASK_SEQUENCES.md](TASK_SEQUENCES.md) covers task sequences: their stages, tasks, repeats, injections, chains, synced
  runners and libraries.

### Time and random numbers

This section covers where PlayForge's time and random numbers come from, and the clocks an entity or a sequence runs
by.

PlayForge reads time through `ForgeTime`, and draws random numbers through `ForgeRandom`. They read Unity's `Time` and
`Random` unless you put in your own:

| Call | What you put in |
|---|---|
| `ForgeTime.Use(clock)` | A clock of your own, an `IForgeClock`. |
| `ForgeRandom.Use(new SeededRandom(seed))` | Seeded rolls, which repeat from their seed whatever else draws from Unity's `Random`. |

Each `Use` returns what it replaced, so you can put it back. Each entity numbers its effects' applications
(`GameplayEffectSpec.ApplicationId`) and its abilities' activations.

#### An entity's clock

Each entity also has a clock of its own, `gas.GetClock()`: the game's time, scaled by the entity's `TimeScale`.

- The entity's effects run by it, its cooldowns among them.
- Its abilities' waits and sequences run by it too.

So `TimeScale = 0.5f` slows one entity and no other. A scale of 0 holds the entity's time still, but it isn't a pause.

#### A sequence's clock

A sequence can run by a clock of its own:

- `WithClock(UnscaledClock.Instance)` runs a pause menu's sequence while the game's time stands still.
- A `ScaledClock` runs a sequence at a pace of its own.
- A packet's `Clock`, an entity's among them, comes before either.

An ability's Run Sequence task picks a clock in its inspector, with its **Clock**. [TASK_SEQUENCES.md](TASK_SEQUENCES.md)
has the order a run takes them in.

#### Fixed ticks

For a run that comes out the same at any frame rate, set ProcessControl's Fixed Tick Rate, as 60. The game then steps
in ticks, each a fixed step of its time, as many a frame as the frame's time holds:

- Every process steps once a tick, its end of frame with it.
- Each sequence's wait goes on by the tick.
- Code of your own that waits for a sequence's next frame awaits `ForgeTick.Next(token)`.

0, the default, steps once a frame. The ticks stand still with the game's time, so a sequence on the unscaled clock
waits for Unity's frames instead.

### Tooltips: what does this ability do?

`AbilityInspector.Build(ability)` describes what an ability does, for a tooltip. It returns a full static breakdown:

- every effect, with its role, its target scope, and its per-level numbers, evaluated through the real scaler pipeline;
- the ability's components;
- what sets it off on its own, and its chance to activate;
- its Attribute Costs.

```csharp
var breakdown = AbilityInspector.Build(fireball);
foreach (var line in breakdown.Describe(level))   // "Deals 30 Health to target", "Cooldown: 8s"
    tooltip.AppendLine(line);

var impact = breakdown.GameplayUsages.First().Series.Impacts[0];
string perLevel = impact.FormatTotal();           // "20/30/40/50"
```

## Tooling tour

This tour says what each of PlayForge's editor tools is for, and how to open it. [EDITOR.md](EDITOR.md) covers each
tool in depth.

### The Creator Lab

**The Creator Lab** is a space to try the project's own assets in.

- Open it from `Window → PlayForge → Creator Lab`. It opens in a new scene, and plays.
- Put units and test dummies down, and drop effects, abilities and items on them.
- Read any asset's numbers, level by level.
- Its time pauses, steps and speeds up on its own.

[CREATOR_LAB.md](CREATOR_LAB.md) covers the lab, and [EDITOR.md](EDITOR.md#the-creator-lab) its editor parts.

### The Forge

**The Forge** is PlayForge's main window: every PlayForge asset of the active **Forge Project**, in one list beside a
detail pane.

- Open it from `Window → PlayForge → The Forge`, or with Alt+Shift+F, which focuses its search.
- Its toolbar holds Back and Forward, the project menu, Create, a filter for each asset type, Tags, the search and the
  problem count.
- A click on an asset shows its summary and its inspector in the detail pane. A double click shows it in the Project
  window.
- **Create** makes an asset, blank or from a preset: a **Damage**, **Damage over Time**, **Buff**, **Debuff**, **Stun**
  or **Aura** effect, an **On-Hit** or **Active** item, a **Passive** ability, or a **Hero** entity.
- The project's own **Templates** follow the presets in Create, each a new asset's copy.
- The asset menu renames an asset, as F2 on the list does, and moves or copies the selected assets into another
  project.

[EDITOR.md](EDITOR.md#the-forge) covers The Forge in depth, and [EDITOR.md](EDITOR.md#new-assets-and-presets) its
presets.

### Forge Projects

A Forge Project holds one game's PlayForge assets, and its settings.

- It's a folder with a project asset at its root (**Create → PlayForge → Forge Project**). It holds the assets under it
  that no project inside it does.
- The Forge's project menu switches projects, and makes a new one. With no project, The Forge asks for the first.
- The active project is the one The Forge shows. Every asset picker, an object field's among them, offers the active
  project's assets alone, and the tag picker the tags they use.
- The Demo's assets are a project of their own.

[EDITOR.md](EDITOR.md#forge-projects) covers projects, the active project, and moving assets between projects.

### The Tags view

The Forge's **Tags** view lists the project's tags, and what uses each.

- Open it with **Tags**, in The Forge's toolbar.
- A context is a kind of tag, as Granted, Event, Impact Type or Local Data Key. A tag field's picker offers the kinds it
  takes.
- A tag's **Declared For** declares it for contexts, so their fields' pickers offer it. A Tag Library asset
  (`Create → PlayForge → Tag Library`) keeps the declarations.
- The view renames a tag, as F2 or a row's **Rename…** does, and the tags under its path with it, everywhere they're
  stored. An asset's own tag follows its asset's name instead.

[EDITOR.md](EDITOR.md#the-tags-view) covers the view, and [TAGS.md](TAGS.md) covers tags, their contexts and the Tag
Library.

### Spreadsheets

The **Spreadsheets** commands write the project's numbers by level, or its items' stats, to CSV, and read a sheet back.

- Open them from The Forge's window menu, under **Spreadsheets**.
- Reading a sheet back lists its changes before it makes them.

[SPREADSHEETS.md](SPREADSHEETS.md) covers spreadsheets in full, and [EDITOR.md](EDITOR.md#spreadsheets) names their
editor parts.

### The Debugger

The **Debugger** shows the game as it plays, in four views the toolbar switches between.

- Open it from `Window → PlayForge → Debugger`.
- **Entities** lists every entity, of either kind, with its attributes, effects, abilities, tags and combat log. A foot
  row under each table acts on the entity: set values, apply or remove effects, grant and activate abilities, add tags.
- **Processes** lists every process with its state and how long its steps take. It pauses, resumes or terminates the
  one selected.
- **Tags** shows every tag the entities hold, as a tree, and who holds the one selected.
- **Timeline** records each run of an ability, stage by stage, each effect's time on the shelf, and the refusals, and
  draws them over the last seconds.

[EDITOR.md](EDITOR.md#the-debugger) covers each view in depth.

### The Problems view

The **Problems** view lists every problem the validator finds in the project's PlayForge assets, with its severity and
its asset.

- Open it from `Window → PlayForge → Problems`, or from a problem count in The Forge's toolbar or an inspector's
  header.
- A problem is, for example, a link with no provider, an impact that never applies, or a field that reads a scaler it
  hasn't got. A click selects the asset.
- **By Rule**, in its toolbar, groups the problems under the rule each breaks. Where one step fixes a problem, its row
  has a Quick Fix, as **Store the Tag**.
- In batch mode, `ForgeValidator.LogAll` writes the problems to the Console.
- Add a check of your own by deriving from `ForgeValidationRule`: override `Name` to name its group, and give a problem
  a step that fixes it with its `Fix`.

[EDITOR.md](EDITOR.md#the-problems-view-and-validation) covers the view, PlayForge's rules and the validator.

### The settings pages

PlayForge's **Settings** are on two pages: the project's, and your own.

- **Project Settings → PlayForge** shows the active Forge Project's settings, kept in its asset under version control,
  each game its own.
- They say where new assets go and what they start as, and what the game reads through its GameRoot as it runs, as the
  roles its attributes play ([ATTRIBUTES.md](ATTRIBUTES.md#roles)).
- The page's **PlayForge Folder** is where a new Forge Project goes.
- **Preferences → PlayForge** holds your own view toggles, the most PlayForge's dropdowns grow to (**Menu Width** and
  **Menu Height**), and the log level.
- The Forge's window menu opens both.

[EDITOR.md](EDITOR.md#preferences-and-project-settings) covers both pages in depth.

### Asset fields

**Asset fields** are Unity's object fields: every reference to a PlayForge asset is one.

- **Open**, beside a field, shows the asset in a window of its own.
- **New**, beside an empty field, asks for the new asset's name, then makes it where Project Settings → PlayForge puts
  its type.
- An effect field's right-click menu has the field's template, where it has one, a pick of the effects nothing links,
  and New Linked Effect.

[EDITOR.md](EDITOR.md#fields-and-drawers) covers asset fields and PlayForge's other fields.

### Import and Clear

**Import and Clear** sit on each inspector section's header, beside Help.

- Import opens the Import window. Choose a source on the left: the type's defaults first, then the assets of the type.
- The right lists the fields that differ, each checked, beside their values now and the source's. Uncheck a field to
  leave it.
- **Import N Changes** imports the checked fields, as one Undo step.
- The asset menu's **Import from Another Asset...** does the same for the whole asset.
- **Clear** resets the section's fields at once, and Undo takes it back.

[EDITOR.md](EDITOR.md#import-and-clear) covers both in depth.

### Templates

**Templates** let an asset take its fields from another asset of its type, its template.

- An asset's menu **Set Template...** picks, from a searchable list of the other assets of its type, the template it
  becomes a child of.
- A child takes the template's fields when the template is pushed, with the template's asset menu
  **Push to Its Children**, as one Undo step. Saving a template changes no child.
- A child keeps each field it overrides. An edit to a child's field makes it the child's own, and its inspector marks
  it as Unity marks a prefab instance's override.

[EDITOR.md](EDITOR.md#templates) covers templates in depth.

### Golden tests

The **Golden tests** are the framework's behavioural contract.

- Find them in the Test Runner's EditMode tab, under `FarEmerald.PlayForge.Tests.Golden`.
- Run them after upgrading.

### The test kit

**The test kit** (`FarEmerald.PlayForge.Testing`) plays your own abilities, effects and items in your own tests.

- Each test gets a live world, stepped a frame at a time, with entities on either hub.
- A test plays them with Cast, Use, Hold and Hit, with scripted time and rolls.
- After each test, the kit checks that nothing leaked.

[TESTING.md](TESTING.md) sets it up.

## Logging

All the runtime says goes through `ForgeLog`, whose level you control.

- In the editor, set the level in **Preferences → PlayForge → Logging**: Verbose, Info, Warning, Error or None.
- In builds, set `ForgeLog.MinimumLevel` from code.

Some output is said only when asked, and it's information:

- `ProcessControl`'s lifecycle logs, while its inspector's toggles are on;
- a sequence's or a chain's logging;
- a debug tool's output.

For such output, the toggle makes it, and the level shows it.

`ForgeLog.Exception` logs an exception as Unity's own `LogException` does, its stack in the console.

A double-click on a message ForgeLog wrote opens the code that logged it, not ForgeLog. ForgeLog's methods are marked
`[HideInCallstack]`, so the Console's **Strip Logging Callstack** leaves them out of the stack. A logging wrapper of
your own marked so is skipped too.

Each message is of a system (`ForgeLog.ESystem`): effects, abilities, numbers, processes, sequences, saves, input and
the rest.

- A system can take a level of its own, as
  `ForgeLog.SetLevel(ForgeLog.ESystem.Effects, ForgeLog.ELevel.Verbose)`, or give it back with null.
- `ForgeLog.Logged` hears each message let through, with its system, and the entity it's about where it names one. The
  Creator Lab's Log page hears messages this way.

## Profiling

PlayForge's work shows in Unity's Profiler: markers time it, counters say how much of it there is, and two modules chart
the counters.

The markers are in the Profiler's Scripts category (`ForgeProfiler`):

| Marker | What it times |
|---|---|
| `PlayForge.Update`, `PlayForge.LateUpdate`, `PlayForge.FixedUpdate` | The step loop at each timing. |
| `PlayForge.ProcessStep` | One process's step. Inside it, `PlayForge.Step.` and the process's type's name, with the process's ID as the marker's metadata. |
| `PlayForge.GASStep` | A GAS's step, where its effects tick. |
| `PlayForge.EndOfFrame` | A GAS's end of frame, where its queued actions and its tag and analysis workers run. |
| `PlayForge.ApplyEffect` | An effect's application, with its contained effects' inside it. |
| `PlayForge.ActivateAbility` | An ability's activation, up to the first of its tasks that waits. |
| `PlayForge.TagCompile` | A GAS's tag compile. |
| `PlayForge.Recompute` | An attribute's recompute. |
| `PlayForge.Stage` | A sequence stage's start, an ability's among them: its tasks, up to the first that waits. |

The counters are in the Profiler's PlayForge category (`ForgeCounters`):

| Counters | What they count |
|---|---|
| ProcessControl's | The processes running, waiting and created, and each frame's registrations, terminations, steps, state changes and step time. |
| The GAS's | The entities by hub and the effects active, and each frame's effects applied and removed, impacts, activations, refused activations, tag compiles and recomputes. |
| TaskSequence's | The sequences running, and each frame's stages started. |

Two Profiler modules chart the counters. Turn them on in the Profiler window's list of modules:

| Module | What it charts |
|---|---|
| **PlayForge Processes** | ProcessControl's and TaskSequence's counters. Its details list the selected frame's costliest processes, by their steps' time. |
| **PlayForge GAS** | The GAS's counters. |

PlayForge puts the costliest processes into the capture while the Profiler records, so a saved capture shows them too.

A `ProfilerRecorder` reads a marker or a counter by its name, in the editor or in a development build. Unity compiles
the markers and counters out of release builds.
