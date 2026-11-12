# The Creator Lab: hand-over note

Paused on 2026-11-11 at the user's word, to go back to the tooling wishlist (TW-7 onward); to be picked up later. The
design is `Docs/CREATOR_LAB_DESIGN.md` (Decision 44); the tracker rows are LAB-1 to LAB-18 under *The Creator Lab
(FEAT-35)* in `Docs/REFACTOR_PLAN.md`; the user's page is `Assets/PlayForge/Documentation/CREATOR_LAB.md`.

## Where it stands

| Phase | Rows | State |
|---|---|---|
| 1. The inside, without the lab | LVL-1 to LVL-4 | Done (FEAT-32) |
| 1. A Scaler asset to try | LVL-5 | **Open**: needs a caster and a target in the editor |
| 2. The lab's shell | LAB-1 to LAB-8 | Done: PlayForge `f7afb28`, Proving Grounds `ed3ca46` |
| 3. Battle | LAB-9, LAB-10 | Done: the commits after those, "LAB-9, LAB-10" |
| 4. The author's unit | LAB-11 | **Open**: designed and drafted below, not in the repo |
| 5. Showing its working | LAB-12 to LAB-17 | **Open** |
| 6. Forge Projects in the library | LAB-18 | **Open**; FEAT-34 is done, so nothing blocks it |

Every test of the lab passes: `CreatorLabTests` (13, PlayMode), `CreatorLabEditorTests` (2, EditMode), and the Demo's
nine, which play the Demo on the lab's kit.

## The code

Two assemblies, each left out of a build that defines `PLAYFORGE_NO_LAB`:

- `Assets/PlayForge/Runtime/Lab/` (`FarEmerald.PlayForge.Lab`):
  - `CreatorLab.cs`: the lab. It brings PlayForge up where the scene has none (as the Battle sample does), builds the
    ground, a light, its camera, a `ProcessWorld` of its own and its own caster ("The Lab", on the Neutral side, no
    body), and adds `LabPanels`. It steps its world by its own time in `Update` (`Paused`, `Speed`, `StepFrame`,
    `Step(dt)`), then each unit's body, dummy and bot. Its API is what the panels call and the tests drive: `AddUnit`,
    `AddDummy`, `AddBot`, `QuickBattle`, `Play`, `ResetBattle`, `ApplyEffect`, `CastAt`, `GiveItem`, `TakeItem`,
    `Teach`, `Forget`, `SetAbilityLevel`, `Drop`/`DropAt`, `Select`, `SetMode`, `Clear`, `ResetDummies`, `TryPick`.
    A battle's bots each have a `LabSeat` (entity, side, level, home, and what the author gave since), which a reset
    makes the bot anew from.
  - `LabUnit.cs`: a unit, the Demo's made general: a body (capsule, nose, ring), a Mono GAS from a copy of the entity
    (or one made for an attribute set), registered on its side, in the lab's world. Walks in straight lines, attacks,
    casts with its target in the data (`DataAt`), holds items and levels. `Create<T>` for a subclass (the Demo's).
    `SteppedByLab` (the lab steps it) or it steps itself by `ForgeTime`. `Stays` in its spec makes the entity's copy
    stay as it dies (dummies, bots).
  - `LabRules.cs`: `ILabRules` (Health, Movement Speed, Walk Speed, Stops, Hides, Shields, Attack Range key and
    default, Attack Slot, and the bots' Cast Order, Cast When and Reach keys) and the `LabRules` asset, which finds
    Health and Movement Speed by name where they're unset. The Demo's roster implements `ILabRules`.
  - `LabCamera.cs`, `LabDummy.cs`, `LabBot.cs`, `LabSides.cs` (Blue, Red, Green, Gold, Neutral, as affiliation tags
    made with `Tag.GenerateAsUnique`), `LabEffects.cs` (the dummy's mend, an effect made in code), `LabLibrary.cs`
    (`ILabLibrary`, `ListLibrary`, `LabAssets`: kinds, names, colors), `LabSummary.cs` (the middle dock's lines, from
    `AbilityInspector.BuildEffectSeries` and `Build`), `LabPanels.cs` (the UI Toolkit panels).
  - `Resources/PlayForge Lab/`: `Lab Theme.tss` (Unity's default runtime theme) and `Lab.uss`. `LabPanels` makes its
    `PanelSettings` in code and loads these.
- `Assets/PlayForge/Editor/Lab/` (`FarEmerald.PlayForge.Lab.Editor`): `CreatorLabMenu.cs`, the menu (a new scene with
  the lab, played) and `EditorLabLibrary` (every PlayForge asset of the project's folders), hooked in through
  `CreatorLab.DefaultLibrary` and `CreatorLab.OpenInForge`.
- `Runtime/PlayForge/Utils/ForgeInput.cs`: `IForgeInput` gained `PointerHeld`, `TryGetPointerPosition`, `ScrollDelta`
  and `KeyPressed(KeyCode)`, each with a default body, so a project's own input still compiles.
- The Demo (`Samples~/Demo/Scripts/Game`): `DemoUnit`, `DemoDummy`, `DemoCamera` and `DemoAI` are thin subclasses of
  the lab's (its seat, the dummy's damage kind, the camera's offset, the AI's foe); the roster is its `ILabRules`;
  `DemoPlayer` reads `ForgeInput`; `DemoInput` is gone.

## Traps found on the way

- **The runtime scans** (`RuntimeSourceTests`) cover `Runtime/Lab` too: no `Time.*`, `Random.*`, `Input.*`,
  `Camera.main` or `Keyboard/Mouse.current` there. Read time through `ForgeTime`, random numbers through
  `ForgeRandom`, input through `ForgeInput.Current`, and aim through the lab's own camera or
  `ForgeInput.Current.TryGetPointerRay`.
- **Tear the panels down explicitly.** `LabPanels.OnDestroy` unregisters its root's callbacks, clears its tree, sets the
  document's `panelSettings` to null and destroys its `PanelSettings` at once. Destroyed late (a test's `[TearDown]`,
  after the harness's world ended), they otherwise left 1216 bytes a frame, which 24 allocation probes after them
  caught. A bare panel, or the lab without panels, left nothing.
- **Tests step the lab themselves**: `lab.Paused = true` and `lab.Step(dt)`, since Unity's frame time in a batch run is
  tiny and uneven. Where a test needs frames to pass (the camera's follow, `Update`'s speed), it puts a stepping clock
  in with `ForgeTime.Use`. `lab.Play()` unpauses, so a test pauses again after it.
- **Identical bots fall together**: a battle of alike sides can end with no one standing; `Ended` is true and `Winner`
  null. A test of a winner makes its sides unequal.
- **The lab's sides are tags**; `TagRegistry.Reset` (each test's teardown) clears them, so `LabSides.Forget` runs as a
  lab is destroyed and they're made again when next asked.
- **MSBuild has no csproj for the lab's assemblies** until Unity writes them (the user keeps the editor on Proving
  Grounds, not PlayForge). The session made its own from the runtime's and the Editor's: copy
  `FarEmerald.PlayForge.csproj` to `FarEmerald.PlayForge.Lab.csproj` with its `RootNamespace`, `ProjectGuid`,
  `AssemblyName` and `OutputPath` changed, its `<Compile>` items replaced by `Runtime/Lab`'s files and a
  `<ProjectReference>` to `FarEmerald.PlayForge.csproj`; and the same from `FarEmerald.PlayForge.Editor.csproj` for
  `FarEmerald.PlayForge.Lab.Editor.csproj`, referencing the runtime, the Editor and the lab's.
- **The Unity editor open on Proving Grounds imports the package's new files**, and writes metas for any that have
  none: write a file's meta with the file.

## What's next

### LVL-5: a Scaler asset to try

The Forge's Scaler asset panel: the author sets what the scaler reads (the level, an attribute's value on each side, a
caller's value) and reads what it gives, with its parts (`NumberTrace`). It needs a caster and a target in edit mode.
Two ways: a pair of `RuntimeGameplayAbilitySystem`s made from attribute sets the panel names, if they can live in edit
mode with no ProcessControl (check first); or a `NumberContext` over plain values, without entities. The second is
lighter and is what the panel needs.

### LAB-11: the author's unit (drafted)

Decided by Decision 44: `ILabUnitDriver` (move to a point, attack a unit, cast an ability with its data, stop), a
project's own named on its rules, or the lab's, which walks the unit in straight lines; and the Demo's controls as the
lab's, with R. The draft:

- `ILabUnitDriver { MoveTo(unit, point); Attack(unit, target); bool Cast(unit, ability, data); Stop(unit); }`.
- `LabDriver`: a ScriptableObject that implements it, with each order a virtual that does as the lab's does, which a
  project subclasses (Create → PlayForge → Lab Driver) and names on its `LabRules.Driver`. `ILabRules` gains
  `ILabUnitDriver Driver { get; }`; the Demo's roster gives null.
- `LabBodyDriver.Instance`: the lab's own, calling `LabUnit.MoveTo`, `AttackUnit`, `Cast` and `Stop`.
- `LabControls` (a MonoBehaviour, the Demo's `DemoPlayer` made general): each frame, through `ForgeInput.Current`, a
  right-click raycasts (its `Camera`, or `ForgeInput.Current.TryGetPointerRay`) and orders an attack on a foe it can
  target or a move to the ground; Q, W, E and R cast the unit's `Casts()` in order, with no data, so click targeting
  waits for a left-click; 1 to 6 cast `ItemAbility(unit, i)` (virtual: the held items' active abilities, the Demo's by
  its seat); S stops; Esc cancels. Its unit is `Unit`, or `ControlledUnit()` (virtual: the Demo's hero); its driver
  is `Driver`, or its unit's rules', or the lab's. `PointerOverPanel` keeps a click on a panel the panel's.
- `DemoPlayer : LabControls`, keeping its scene's `Camera` and `Layers` fields by their names, with `ControlledUnit()`
  the Demo's hero, `ItemAbility` its seat's, and `PointerOverPanel = DemoHud.PointerOverPanel`.
- `CreatorLab`: `ControlledUnit`, `PutDownOwnUnit(entity, side, at)`, `TakeControl(unit)` (a bot taken over stops
  fighting until it's let go; `SetFighting` passes it over) and `ReleaseControl()`; the Lab panel's **Your unit**
  section: Put down, Take control (of the selected unit), Release, and Follow (the camera follows it).
- Tests: a fake `IForgeInput` with a pointer position, a pressed button and a pressed key, put in with
  `ForgeInput.Use`: a right-click on the ground moves the unit; one on a foe sets it on the foe; Q casts its first
  ability; a project's driver, a `LabDriver` subclass that records its orders, takes them.

### LAB-12 to LAB-17: showing its working

The journals record only while something listens. `GameplayAbilitySystemCallbacks` already raises effect applied,
ticked and removed, impacts dealt and received, actions queued, executed and invalidated, tag workers, death and
revival. Missing, and needed: an effect's refusal by requirement, immunity or chance (`EffectSystemComponent`'s apply
path, around its `ChanceToApply` roll); each effect worker's hook as it runs, and which side; each attribute worker's
change to a value, before and after, inline or queued. Then the journal (LAB-12), the combat log (LAB-13), stopping
on an event and stepping (LAB-14: the lab's world is its own, so stopping is `Paused`), watches over the last minute
(LAB-15), ForgeLog by system in the bottom panel (LAB-16, a ForgeLog listener), and the same journals in the Debugger
(LAB-17: `Editor/Debugger/TimelineRecorder.cs`, `RuntimeDebugger.cs`).

### LAB-18: Forge Projects in the library

`EditorLabLibrary` takes folders: give it the active Forge Project's (FEAT-34's `ForgeProjects` in the Editor
assembly), with a project switch in the library's header; with no projects, every asset. In a build, a library of
what a project names, which needs a project to keep its assets' list for a build.
