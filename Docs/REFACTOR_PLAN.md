# PlayForge refactor plan

This file is the working plan and bug tracker for fixing, upgrading and testing PlayForge. It was created on
2026-10-29 from the **PlayForge Framework Review** (a Claude Doc):
https://claude.ai/artifact/1GsrCxW3Wff2KvXM6fnai9

The review explains every finding in depth, with diagrams. This file is the short, editable version to work
from. It lives outside `Assets/` so it never ships in the package.

## How to use this file

- Read it before changing anything. Check **Decisions** before acting on an open question.
- Every finding has an ID: `PC-n` (ProcessControl), `TS-n` and `TS-A`…`TS-Z` (TaskSequence), `GAS-n` (GAS core),
  `ED-n` (editor and UI), `SP-n` (samples and packaging), `FEAT-n` (missing features). `PF-nnn`, `C-nn` and
  `B-nn` refer to Arcade Fighter's log.
- **Red first.** Write a failing test for an ID before fixing it. Name the test after the ID, for example
  `PC_03_PrefabCascade_KillsOnlyOne`.
- **Update the Status column in the same commit as the fix:** `open` → `red` (test written) → `fixed`
  (test green) → `verified` (gate passed). Use `wontfix` or `moved` with a note when needed.
- **Commit messages cite IDs**, for example `Fix GAS-07: cost validation compares magnitude`.
- **Work on `main`** (Decision 19). Phases 0 to 2 each had a branch of their own: `refactor/phase-0-upstream`,
  `refactor/phase-1-correctness` and `refactor/phase-2-release`.
- `file:line` references were verified on 2026-10-29 against commit `01258b8`. Re-check before editing.
- `Docs/CODE_TOUR.md` walks the code the refactor changed, by subsystem, with a `path:line` for each place to read.
  Add each new phase's high-impact fixes to it.

## Environment

- **Unity:** 6000.0.33f1. Repo root `C:\Users\17074\Documents\GitHub\PlayForge`; the package is
  `Assets/PlayForge`.
- **Assemblies:** `FarEmerald.PlayForge` (Runtime), `FarEmerald.PlayForge.Editor` (Editor),
  `FarEmerald.PlayForge.Tests` (Tests, editor-only).
- **Dependencies:** UniTask (UPM git) is the only runtime dependency. `com.unity.test-framework` 1.4.5 is
  already in `Packages/manifest.json`.
- **Compile check:** MSBuild 17.14 from **VS 2022 Build Tools** (not Community); there is no dotnet SDK. Two
  builds cover everything, since MSBuild takes one project per call:
  ```
  "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" FarEmerald.PlayForge.Editor.csproj -t:Rebuild -nologo -v:minimal
  "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" FarEmerald.PlayForge.Tests.csproj -t:Rebuild -nologo -v:minimal
  ```
  - `FarEmerald.PlayForge.Editor.csproj` builds Runtime and Editor; the Tests project references Runtime only.
    Output goes to `Temp\Bin\Debug\`. `Assembly-CSharp.csproj` has had no sources since the samples moved to
    `Samples~` (SP-20), so building it checks nothing.
  - Two `MSB3277` reference-conflict warnings come from the code-coverage and collab-proxy package DLLs. They
    are not ours; count only `CS` diagnostics.
  - Do not build `Assembly-CSharp-Editor.csproj`. It is stale (July 2026, from before the Editor asmdef) and
    compiles the Editor sources itself.
  - Unity regenerates the csproj files only while its editor is open. New files may need a manual `<Compile>`
    entry for MSBuild until then. On 2026-10-29 the three `FarEmerald.PlayForge*` projects matched the sources
    on disk exactly.
- **Golden tests:** the custom runner in `Assets/PlayForge/Tests/Editor/PlayForgeGoldenTests.cs`. Three ways to
  run it:
  - **Test Runner:** the EditMode tab's `FarEmerald.PlayForge.Tests.Golden` assembly, one test per scenario
    (SP-18). Its menu item left with ED-19.
  - **Batch mode, editor closed** (used for the baseline):
    ```
    "C:\Program Files\Unity\Hub\Editor\6000.0.33f1\Editor\Unity.exe" -batchmode -nographics -projectPath "C:\Users\17074\Documents\GitHub\PlayForge" -executeMethod FarEmerald.PlayForge.Tests.PlayForgeGoldenTests.RunAll -logFile <path>
    ```
    `RunAll` calls `EditorApplication.Exit` itself in batch mode: 0 when nothing fails, 1 otherwise. No `-quit`
    is needed. The report is in the log file, after `PlayForge Golden Tests`.
    - A run takes 10 to 35 seconds with `Library` warm.
    - Batch mode regenerates the csproj files (with CRLF), so a run also adds new files' `<Compile>` entries.
    - S17 and S18 measure heap growth. Arcade Fighter reported S18 green in the open editor while batch mode
      measured 47 bytes per round here (see the Phase 0 log), so trust batch mode for allocation checks.
  - **Unity CLI, editor open** (this is how Arcade Fighter runs it):
    `unity command eval --code 'FarEmerald.PlayForge.Tests.PlayForgeGoldenTests.RunAll(); return "ran";'`, then
    `unity command console --tail 3`.
- **Arcade Fighter:** `C:\Users\17074\Documents\GitHub\Arcade Fighter`.
  - Its patched PlayForge copy is at `Assets/Internal/PlayForge`.
  - Its log is `Docs/PlayForge-Log.md`, with entries PF-001 to PF-013, considerations C01 to C19, and backlog
    items.
  - Its baseline on 2026-10-18 was 22 of 22 golden tests passing, and no warnings.
  - Its copy was taken in its commit `3ae8838` (2026-10-06). That copy matches canonical `01258b8` except for
    line endings (Arcade Fighter's files are CRLF; canonical's working tree is mostly LF) and one line:
    Arcade Fighter's `GameRoot.Awake` adds `DontDestroyOnLoad(gameObject)` (PC-21, Phase 1). Its fixes are
    therefore the diff `3ae8838..HEAD` under `Assets/Internal/PlayForge`.
- **PlayForge Proving Grounds:** `C:\Users\17074\Documents\GitHub\PlayForge Proving Grounds`, a separate project
  in its own git repo, public at github.com/gabedvdsn/PlayForge-Proving-Grounds since 2026-11-05 (Decision 35). Push
  its `main` after each batch, as PlayForge's, fast-forward only. It consumes `Assets/PlayForge` as the package
  `com.faremerald.playforge` 0.1.0. It was created with
  `Unity.exe -batchmode -nographics -createProject "<path>" -quit -logFile <path>`.
  - **Manifest:** three lines on top of Unity's default project. Relative `file:` paths resolve from the project's
    `Packages` folder, so Arcade Fighter, which also sits next to PlayForge, can use the same lines (Decision 3).
    ```
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    "com.faremerald.playforge": "file:../../PlayForge/Assets/PlayForge",
    "com.unity.test-framework": "1.4.5",
    ```
    - A `package.json` can't declare a git dependency, so every consumer adds UniTask itself (SP-8).
    - `com.unity.ugui` arrives through `package.json`; the default project doesn't have it.
    - The golden runner doesn't use the Test Framework. It's there for the harness and SP-18.
  - **Compile check, editor closed.** Exit code 0 means it compiled. There are no csproj files, because the project
    has no IDE package, so MSBuild doesn't apply here. After a compile error Unity stops before importing assets,
    so fix those first.
    ```
    "C:\Program Files\Unity\Hub\Editor\6000.0.33f1\Editor\Unity.exe" -batchmode -nographics -projectPath "C:\Users\17074\Documents\GitHub\PlayForge Proving Grounds" -quit -logFile <path>
    ```
  - **Golden tests, editor closed.** A run takes about 6 seconds with `Library` warm.
    ```
    "C:\Program Files\Unity\Hub\Editor\6000.0.33f1\Editor\Unity.exe" -batchmode -nographics -projectPath "C:\Users\17074\Documents\GitHub\PlayForge Proving Grounds" -executeMethod FarEmerald.PlayForge.Tests.PlayForgeGoldenTests.RunAll -logFile <path>
    ```
  - **Clean import:** delete the project's `Library`, then run the compile check. It takes about 30 seconds and
    needs network access for UniTask. **Use this for packaging checks:** with `Library` warm, the same project
    logged 0 import warnings instead of 47, so a warm run proves nothing about Phase 2's gate.
  - **All its tests, editor closed.** Exit code 0 means they passed and 2 means tests failed. A run takes about
    10 seconds. `python Tools/test-report.py <xml>` lists known reds apart from new failures. The project's
    `README.md` covers how to write a test.
    ```
    "C:\Program Files\Unity\Hub\Editor\6000.0.33f1\Editor\Unity.exe" -batchmode -nographics -projectPath "C:\Users\17074\Documents\GitHub\PlayForge Proving Grounds" -runTests -testPlatform PlayMode -testResults <xml> -logFile <path>
    ```
  - **The green gate.** The same command with `-testCategory "!Red"`. A test for a bug not fixed yet carries the
    `Red` category, so a full run stays red until the bug is fixed, and this run shows nothing else broke. Use it
    after any runtime change, alongside the golden tests. When a fix turns a red test green, the report says so:
    remove its category in the same commit as the fix.
  - **Planted failures, after changing the harness.** The same command with `-testCategory Planted`. Every test
    there must fail, so exit code 2 is correct; `python Tools/check-planted.py <xml>` checks each failed for its
    own reason.
  - **Batch-mode facts,** each measured:
    - `WaitForEndOfFrame` never resumes under `-nographics`, so the frame stepper waits on the last player loop
      stage instead.
    - `Time.captureDeltaTime` makes `Time.time` exact, but `Time.unscaledTime` stays on the wall clock. Anything
      timed on unscaled time (C02) can't be stepped.
    - The GC API returns 0 under Mono (C15).
    - The counter "GC Allocated In Frame" works with the profiler off, but its frame total is noise: 450 to 532
      bytes a frame with nothing running, moving at random. All of that is outside the player loop, except 40
      bytes from the test runner's coroutine tick. `AllocationProbe` measures only the player loop, less that
      tick, so a clean frame reads exactly 0. **Don't go back to a calibrated baseline:** one taken while the
      noise ran high hides real allocation.
    - Now and then a burst of about 1 KB still lands on a few frames, at random. It was seen in a world with no
      spawned entities, a collection before measuring doesn't stop it, and its source isn't known. It doesn't
      repeat, so `MeasureFrames` retakes a measure that isn't clean, up to 3 times. A real leak fails every attempt.
  - **Live sources:** a `file:` package is read in place, so edits to `Assets/PlayForge` reach the project on its
    next run. There is nothing to copy or sync.
  - **With the project open in the editor,** batch mode can't lock it and exits 21 with "another Unity instance is
    running". Run a private copy instead of closing the editor: mirror `Assets`, `Packages`, `ProjectSettings` and
    `Library/PackageCache` to a short path (Unity's cache paths run close to MAX_PATH), point the package at an
    absolute `file:` path in the copy's manifest, drop it from the copy's lock file, and mirror `Assets` again before
    each run. A run takes about 30 seconds. A new `.cs` file's `.meta` has to be copied back to the real project.
- **Ignore** `.claude/worktrees/` at the repo root and under `Assets/PlayForge/.claude/`. They hold stale git
  worktrees (about 1.1 GB) and are not in `.gitignore` yet (SP-10).
- **Working tree at handoff:** branch `refactor/handoff` had the user's uncommitted changes: an edit to
  `PUBLISHING.md`, QuickOutline moved into `Assets/PlayForge/Samples/QuickOutline`, a new
  `Editor/Tools/BuildVerification.cs` and a `UnityConnectSettings.asset` change. At the user's request they
  were committed on 2026-10-29 as `455dd44`, after the handoff files (`33c04f6`). `Assets/PlayForge/.claude/`
  stays untracked.
- **Corrections to older notes:**
  - There is no `StepOnly` lifecycle. The lifecycles are SelfTerminating, RunThenWait, RequiresControl and
    Synchronous.
  - The note that "PlayForge is in Assembly-CSharp, so tests can't use NUnit" is stale. The asmdefs exist, so
    the Unity Test Framework works.

## Decisions

**Made:**
- The refactor follows the review's roadmap, phases 0 to 5, in order.
- Tests are written red first.
- Phase 0, the Arcade Fighter port, comes first.

- Decision 1, 2026-10-29: the pending changes were committed on `refactor/handoff` (see Environment).
- Phase 0, 2026-10-29: commit locally once per fix on the phase branch (red test, fix and Status together).
  Do not push.
- Decision 2, 2026-10-29: PlayForge Proving Grounds is a **separate Unity project** that references PlayForge
  as a local UPM package. That needed a `package.json` in `Assets/PlayForge`, added on 2026-10-30.
- Decision 3, 2026-10-30: Arcade Fighter switches to upstream PlayForge, consuming it as the same local UPM
  package as Proving Grounds, once that package works. Until then it keeps its copy in
  `Assets/Internal/PlayForge`.
- Proving Grounds setup, 2026-10-30: the project sits next to PlayForge, in its own local git repo with no
  remote. The package is `com.faremerald.playforge` 0.1.0. Commit locally as work goes; do not push.
- Decision 8, 2026-10-30: a test for an unfixed bug carries the `Red` category. A full run stays red until the
  bug is fixed, `-testCategory "!Red"` is the green gate, and a red test loses the category in its fix's commit.
- Decision 5, 2026-10-30: pause means **hold in place**.
  - A pause takes effect at once: the process goes to Waiting and stops stepping.
  - A process that can hold its run (`TryHandlePause` returns true) keeps it, and `TryHandleResume` picks it up. A
    process that can't has its run cancelled, and a new run starts on resume.
  - ProcessControl's own pause (Waiting, ClosedWaiting) stops what is running and, when it ends, sends back exactly
    that. A process paused on its own, or Waiting for any other reason, stays where it was.
  - `Unpause` is refused while ProcessControl is paused. A requested Terminate outranks a Pause.
  - A sequence holds its run: no stage starts or ends, timeouts and built-in delays stop counting, and conditions
    aren't checked. A task's own code holds if it times itself through `SequenceTaskLibrary`.
- Decision 9, 2026-11-03: a raw delta, one handed to the GAS without an effect, reaches attribute and impact
  workers, with no impact type: a worker that takes any type acts on it, and one that wants a type doesn't. The
  changes the system makes to itself keep `DisallowImpact`, and no worker sees them. Golden S15 now expects 42.5,
  since armor halves what the pre-change callback leaves of the raw -30.
- Decision 10, 2026-11-03: a disjoint stops homing shots only, as in Dota. A skill shot, one that hits whatever it
  crosses, registers no intent with a target and lands as usual. The disjoint's docs say so (GAS-36).
- Decision 4, 2026-11-03: a commit lands on the current value as it was measured. Committed history comes
  after Add, Multiply and FlatBonus, and before Override, so a 30 hit under a ×1.5 aura takes 30, a +10 tome
  under it is worth 10, and an override still pins the value. Golden S6 now expects 85, not 90.
- Decision 11, 2026-11-03: the process hierarchy follows transforms everywhere, GameRoot included (PC-28). A Mono
  process spawned at SceneRoot is GameRoot's child, so ending GameRoot ends it first, and pausing GameRoot pauses it.
- Decision 12, 2026-11-03: `RandomizedScaler` rolls once per effect spec (GAS-27). A buff keeps its roll, and every
  tick of one application deals the same. A per-scaler switch, `RollOncePerSpec`, goes back to rolling on every
  evaluation, buffs re-rolling included.
- Decision 13, 2026-11-03: elements set to Combine add up (GAS-27). Each element's resolved Current and Base are
  summed, and bounds, rounding and retention group come from the first.
- Decision 14, 2026-11-03: a derived attribute's refresh is the system's own change (GAS-27). It takes the
  post-change half of the pipeline (AutoScaleWithBase, post-change workers, `AttributePostChange`) and reaches the
  frame summary, typed `DisallowImpact` as the startup pulse is, so a worker that checks types skips it (Decision 9).
- Decision 6, 2026-11-03: PlayForge ships under the Unity Asset Store EULA (SP-13). The MIT `LICENSE` goes.
- Decision 7, 2026-11-03: the two material packs, `WorldMaterialsFree` and `YughuesFreeConcreteMaterials`, are
  untracked and ignored, and stay on disk (SP-17). History isn't rewritten, so the repo keeps their past size.
- Decision 15, 2026-11-03: QuickOutline, whose header reserves all rights, is replaced by an outline of PlayForge's own
  in the Demo (SP-5).
- Decision 16, 2026-11-03: the samples move to `Samples~`, Unity's layout for package samples (SP-20). Package Manager
  lists them and imports one on request, and a fresh import contains none. Editing one in this repo means importing
  it first. The Asset Store export copies them in on the packaging branch, beside the UniTask embedding.
- Decision 17, 2026-11-03: PlayForge is submitted from 6000.0, the oldest Editor it supports, as `PUBLISHING.md`
  says. SP-22 and SP-23 are done anyway, so a buyer on URP or with domain reloading off meets no surprise.
- Decision 18, 2026-11-03: the UI interview's 37 questions are answered, and the doc's Answers section lists every
  answer. Each one took the doc's lean except Q18, which only the user could answer, and Q34, which adds a
  first-ability wizard. What Phase 2 takes from it:
  - ED-19: the main window is The Forge, and every window's title matches its menu item (Q31). The menu moves to
    `Window/PlayForge/` (Q32). Run Golden Tests leaves the menu, since the Test Runner runs the scenarios (SP-18) and
    batch mode still calls `RunAll`. The Audit stays until Phase 4's Problems view.
  - ED-16: the help buttons open the package's own `Documentation/*.md`, through one table of links (Q33).
  - ED-8: Undo is to cover everything (Q16), so deleting an asset moves it to the trash.
  - ED-12's Phase 2 part: an asset's tag is written only when its name is edited, in the same Undo step, and the
    asset is marked dirty. Viewing never writes; a stale tag shows a warning with an Update button.
  - Phase 4 is a redesign, one surface at a time (Q3): native to Unity (Q2), legible in both themes (Q10), and built
    from a kit of C# controls styled in USS (Q13). Undo through SerializedObject, multi-editing and no polling come
    first (Q35). The editor code becomes internal before 1.0 (Q37). The smoke suite proves, in each theme, that every
    window opens without an exception and scripted edits undo (Q36).
- Decision 19, 2026-11-03: work goes on `main`, with no new phase branches. The user switched the checkout to it, and
  `main` holds Phases 0 to 2; `refactor/phase-2-release` stays at `901c974`. The user's word, the same day: push as
  you go. So each commit whose checks pass is pushed to `origin/main`, fast-forward only, never forced; a rejected
  push stops work until it's reported. This replaces Phase 0's "Do not push". Proving Grounds has no remote and
  stays local.
- Decision 20, 2026-11-03: Phase 4's editor style is recorded in `Docs/EDITOR_STYLE.md`. It comes from the style
  mockups the user reviewed, a private design canvas: https://claude.ai/artifact/PSDAMf8GYvhzZDb1uoF5gr.
  - Windows use Unity's own controls and neutrals. An asset type's color goes in its type icon and nowhere else.
  - No colored borders, tinted or floating cards, shadows, or small letter-spaced headings.
  - Each inspector has one label column, and section headers keep Import, Clear and Help visible.
  - Every column clips its own text, so the columns on the right always show in full.
  - Problems show in place, under what they are about.
  - The type icons are placeholders, to revisit.
- Decision 21, 2026-11-03: the system keeps the carriers and reuses the actions it makes for the changes it makes on
  someone's behalf (GAS-P9). A carrier is kept for each source and attribute, in a table keyed weakly by source so a
  source that's gone isn't held. A `ModifyAttributeAction` the system makes goes back to it once the queue has run
  it, so a listener of `OnActionQueued` or `OnActionExecuted` reads an action during the call; one the queue
  invalidates stays with the frame summary. The user chose this when asked whether to keep carriers per source and
  reuse actions on those terms.
- Decision 22, 2026-11-03: tags are compared and hashed by an ID each is given at run time, and nothing new is
  serialized (GAS-P7). A tag takes its ID as it's made, or as an asset holding it loads (`OnAfterDeserialize`). An ID
  is never given to another name, and `TagRegistry.Reset` leaves them. Two tags are equal exactly when their names
  are, as before, and a tag whose `Name` is changed in place takes its new name's ID. The user chose this over an ID
  serialized with each tag, which would have meant migrating every asset.
- Decision 23, 2026-11-03: `PooledMonoProcessInstantiator`, a stub nothing used, is removed rather than built into a
  pool (PC-24). Pooling Mono processes is left to an instantiator that extends `AbstractMonoProcessInstantiator`.
- Decision 24, 2026-11-03: sequences and abilities end in the frame their last task does (TS-Z). `Task(Action)` runs
  its action at once, a run waits a frame only for maintained stages it has, and waiting on a process ends in the
  frame it ends, through `ProcessRelay.WhenTerminated`. An instant ability ends in the frame it starts, even inside
  `TryActivateAbility`. A repeating sequence that takes no frame still takes one per repeat, as a stage does (TS-M).
  The user chose all of it over fixing `Task(Action)` alone.
- Decision 25, 2026-11-03: activation stays synchronous, as Unreal's is (TS-8). `TryActivateAbility` runs an ability
  until a task waits, so an instant ability ends inside it. An ability asked for again while its own activation call
  still runs, from its own callbacks or from another ability's that it started, starts next frame, so a loop of
  instant activations goes round once a frame, as a repeating sequence does. `TryActivateAbility` returns true for
  such a request, which is tried in full next frame while the ability's slot still holds it. The user chose this over
  deferring every activation's start to the next frame, the review's suggestion, which would cost every ability a
  frame.
- Decision 26, 2026-11-04: the frame summary an `OnFrameComplete` listener gets is its entity's own and is refilled
  each frame (GAS-P10), so a listener reads it during the call, or copies what it needs, as with actions (Decision
  21). `FrameSummary.CreateSnapshot` still makes a copy to keep. The user chose this over a new copy each frame.
- Decision 27, 2026-11-04: Phase 3 closes on its local gates. Its exit gate asks for zero steady-state allocation,
  enforced in CI. Every steady-state frame measured allocates nothing, and the allocation tests run in the full gates
  before every push, but the repo has no CI. The user chose closing the phase over setting up CI first. CI is now
  SP-24, which needs two things only the user can provide: tests that CI can reach, and a Unity license stored as
  GitHub secrets.
- Decision 28, 2026-11-04: the scaler and import workflow, from the user's answers to nine interview questions on the
  design canvas's new boards (https://claude.ai/artifact/PSDAMf8GYvhzZDb1uoF5gr, second row).
  - A scaler lives in its field by default. One needed in more places is saved as a shared Scaler asset, which fields
    reference; editing it changes every field that uses it, so nothing propagates (ED-33).
  - A field adjusts a shared scaler only through its Magnitude multiplier, as in Fire Duration × 1.5.
  - A scaler's values are its per-level table, and every view of a scaler shows a read-only curve preview beside it.
    Quick Fill is one step that fills the table.
  - Levels map exactly: level 4 reads the 4th value, and past the end the last value holds. Stretching over an
    asset's levels is an option on each scaler. Nothing resizes a table while it is drawn (ED-34).
  - Scalers saved before this decision keep Stretch Over Levels on, as the code already does, so saved projects read
    as they did (the user's answer, "Keep it on", 2026-11-04).
  - All eight kinds stay, grouped in the Kind menu.
  - One Import window serves sections, scalers, requirements and workers (ED-32). It lists the fields that differ,
    each with a checkbox, pre-checked. A list is replaced by default, and each list can choose Add to the List. One
    Undo takes an import back.
  - Clear empties its section at once, with no confirmation; Undo takes it back.
- Decision 29, 2026-11-04: the asset header stays at the top of the inspector while the content scrolls under it.
  The user's call after seeing ED-20, which reverses Q17's answer, that the header scrolls with the content. It
  follows the Inspector's scroll view by its events and polls nothing, so ED-11 holds (ED-36).
- Decision 30, 2026-11-04: Phase 5's direction, from the user's answers to a 16-question interview built on a
  read-only survey of the FEAT rows. The user took the recommended answer every time.
  - **Scope:** every FEAT row but networking. FEAT-13 gets only two changes that keep it possible: one injectable
    clock and one seedable random source, which Runtime reads instead of `Time` and `UnityEngine.Random`; and IDs
    scoped to an entity (a `HandleId` counter per ability system, an ID for each effect application, and `Tag.Id`
    never serialized).
  - **Order:** foundations first, then the labs one at a time. The foundations are the Register builder (FEAT-15),
    tag-change events (FEAT-3, with GAS-56), the time source (FEAT-7, with TS-6), and the IDs and random source
    above. Each lab then brings the features it finds missing.
  - **The gate:** all eight labs. A lab is test-only at first: assets built in code and played by a bot in Proving
    Grounds. Its scene, and whether it ships in Samples, come once its bot is green (open decision 18).
  - **Cancel and block (FEAT-5):** per stage, with an ability-wide default. A stage lists the tags that cancel it
    and the abilities it blocks, and stages that list none take the default. A critical stage can't be cancelled.
    A "While active" Caster group interrupts the ability when it fails.
  - **Input buffer (FEAT-6):** one slot per ability. A new press refreshes its slot; the highest buffer priority
    fires first, and ties go to the newest. The Queue policy becomes the buffer, with a window per ability that a
    request can override, on scaled time.
  - **Item stacking (FEAT-14):** a give fills the existing stack first, and a full stack opens a second only with
    Allow Duplicate Items on. A stack applies its effects and abilities once, and a scaler can read its count.
    `GiveItem(item, count)` takes what fits and returns how many it took.
  - **Time (FEAT-7):** each GAS owns its clock. A fixed-rate tick steps every GAS and sequence a fixed number of
    times per frame.
  - **Save and load (FEAT-9):** an entity at rest: permanent commits, effects with their time left, levels, granted
    abilities, items and cooldowns. Running abilities end on load. A catalog the editor builds finds assets by GUID.
  - **Headless (FEAT-10):** ProcessControl's scheduling moves into a plain C# class that the MonoBehaviour wraps.
    Headless code owns one and steps it, and several worlds can exist.
  - **Register (FEAT-15):** one fluent builder. The 28 overloads become `[Obsolete]` wrappers, removed before the
    first Asset Store submission.
  - **Cues (FEAT-1):** handlers listed on the effect, as its workers are. Camera shake goes through an interface the
    game implements.
  - **Ability graph (FEAT-12):** a read-only view. The timeline is recorded from real runs, and the recorder runs in
    editor Play mode only, as a view of the Debugger.
  - **Defaults the user accepted:** tag events fire at once, and a listener on a parent tag hears its children;
    triggers fire on gameplay events and on a tag being added; cleanse and immunity match an effect's asset,
    context and granted tags; overflow at max stacks gets Overflow Effects, Deny Refresh and Clear Stacks;
    SetByCaller values are copied into the spec when it's made; targeting reads an input interface, with legacy
    and Input System defaults.
- Decision 31, 2026-11-04: the Debugger's edits sit in each section's foot row, under a thin rule, as the design
  canvas's Entities board draws them (version 22), which answers open decision 19. Attributes: the selected
  attribute's name, then Base and Current fields that set it on Enter; a double-click on a value goes to its field.
  Effects: Remove for the selected row, then a picker and Apply. Abilities: Activate and Interrupt for the selected
  row, then a picker, Level and Grant. Tags: Remove for the selected tag, a loose one only, then a tag field and Add
  Tag. An action that doesn't apply stays in place, disabled, so nothing shifts, and a section's selected row takes
  Unity's unfocused selection grey, so the entity list keeps the blue. Double-click alone wasn't enough: the style
  doc keeps every control visible.
- Decision 32, 2026-11-04: performance tools. The user asked that users can record and analyze ProcessControl's
  performance over time, and chose markers, counters and modules of three options. Unity's Profiler records frames,
  saves captures and, with Profile Analyzer, compares them, so PlayForge adds only what Unity can't know.
  - **Markers** on the step loop for each timing, each process's step (named by its type, with its process ID as
    metadata), the GAS's end of frame, effect application, ability activation, the tag compile, attribute recomputes
    and sequence stages. The Profiler's CPU view and Profile Analyzer then show each one's time and allocations over
    frames.
  - **Counters,** from `com.unity.profiling.core`:
    - ProcessControl: processes running, waiting and created; registrations, terminations, steps and state changes
      per frame; the step loop's time.
    - GAS: entities by hub; effects active, applied and removed per frame; impacts, activations, refused activations,
      tag compiles and recomputes per frame.
    - TaskSequence: sequences running, and stages started per frame.
  - **Two Profiler modules,** PlayForge Processes and PlayForge GAS, chart the counters, ten at most each. The
    Processes module's detail panel lists each frame's costliest processes, from frame metadata
    (`Profiler.EmitFrameMetaData`, into a reused array) emitted only while the Profiler records.
  - Unity compiles markers and counters out of release builds, so the tools work in the editor and development builds.
    A stats API for release builds was offered and not chosen.
  - The Debugger keeps its live Step and Peak step for each process.
  - **Notes for the build:** zero steady-state allocation holds (Phase 3): markers and counters are static, made
    once, a marker per process type is cached the first time that type steps, and the metadata array is reused. The
    Debugger's per-process timing and the step markers measure the same span, so one place can feed both. An
    EditMode test can read the counters back through `ProfilerRecorder`, as Proving Grounds' AllocationProbe reads
    allocations. Unity's docs: profiler-creating-custom-counters, profiler-add-counters-code and
    profiler-create-modules in the 6000 Manual, and `ProfilerCounterValue` in com.unity.profiling.core 1.0.
- Decision 33, 2026-11-05: the labs' scenes. The user asked to see the labs in a scene, and all eight bots were green,
  so each lab has one in Proving Grounds, `Assets/ProvingGrounds/Scenes/Labs/<Lab> Lab.unity`. Its `LabScene` runner
  plays the lab's bot, the rounds its tests play, slowly enough to watch (12 frames a second, with Slower and
  Faster), each round in a world of its own as its test runs it. Each round shows as passed, failed with the reason,
  or skipped. Each entity in the scene gets a capsule and a label, and every entity, of either hub, a card of its
  attributes, effects and tags. The scenes are editor-only, since the runner lives in the test assembly beside the
  labs. Whether the labs ship in Samples stays open.
- Decision 34, 2026-11-05: the labs and Samples, and the review's UI kit, the user's answers once Phase 5 closed.
  - **The labs** stay in Proving Grounds, but the Battle lab, the one with a real battle, ships with the package as a
    sample, Battle. Demos, from before the refactor and maybe too little or out of date, stays beside it and Code
    Examples until the user has been through it. The user is compiling a list of the UI that still needs work.
  - **No runtime UI kit.** A game makes its own UI. What PlayForge owes it is an API that's easy to build UI on.
- Decision 35, 2026-11-05: CI (SP-24). Proving Grounds is public on GitHub, at
  github.com/gabedvdsn/PlayForge-Proving-Grounds, the user's choice over a private repo, whose runs would count
  against the account's Actions minutes, and over moving it into PlayForge's repo.
  - **Its `Gates` workflow** runs the full gates: the PlayMode suite in three orders, the planted failures and the
    EditMode suite, in GameCI's Unity 6000.0.33f1 container, with PlayForge checked out beside Proving Grounds,
    where its package reference expects it. It runs on a push to `main`, on a pull request, by hand, each day,
    and when PlayForge's own workflow, `proving-grounds.yml`, asks after a push to PlayForge's `main`.
  - **What only the user can give it:** a Unity license in Proving Grounds' secrets (`UNITY_EMAIL`,
    `UNITY_PASSWORD`, and `UNITY_LICENSE` for Personal or `UNITY_SERIAL` for Pro or Plus), and, for each PlayForge
    push to run it, a token in PlayForge's (`PROVING_GROUNDS_DISPATCH_TOKEN`). Until the license is there, the
    jobs are skipped with a warning. Proving Grounds' README says how to add them.
- Decision 36, 2026-11-06: the user's UI list, Phase 6. The user went through the editor and listed what still needs
  work: in the Debugger, the asset inspectors, the drawers, The Forge and the older inspectors (ED-45 to ED-61), and
  a question about what an ability spawns (FEAT-24).
  - **Every drawer and inspector older than Phase 4** is redone in `Docs/EDITOR_STYLE.md`'s style: the tag, task,
    stage, worker group and asset loader drawers among them, and inspectors such as the Mono Process one. Many
    can't fill in all they hold, or are confusing.
  - **The scaler drawer may step outside the style** to make plain which fields are the scaler's. The user counts
    it the most important field, and it must be legible. It may indent its fields, or set them on a light shaded
    backing. The rest of the style holds, its Never list above all: no emoji, and none of generated UI's usual
    look.
- Decision 37, 2026-11-06: the demo refresh, Phase 7, once Phase 6's UI fixes are in. The demo content, the labs
  aside, predates the refactor: it's scattered, uses little of what scalers do, and misrepresents the framework. All
  of it goes, Swarm Defender with it, and a new demo takes its place, made to show PlayForge as a whole approach to a
  GAS-based game. The user's spec:
  - **Heroes:** five, of different abilities and strengths. Each has a default attack and two or three other
    abilities. An ability deals magic, physical or pure damage, as its damage type.
  - **Attributes:** Health, Mana, Armor (resists incoming physical damage), Movement Speed, Agility, Strength,
    Intelligence, Lifesteal, Magic Amplification (amplifies all magic damage dealt), Attack Amplification
    (amplifies all physical damage dealt), Magic Resistance (resists incoming magic damage) and Cooldown Reduction.
    Health, Mana and Armor derive as a per-hero base plus X times Strength, Intelligence and Agility respectively,
    X a constant from 8 to 12. Every hero starts with 0% cooldown reduction.
  - **Items:** at least ten, each giving attributes, an ability, or both.
  - **Effects, workers and cues** as the abilities need.
  - **In the scene:** a UI to choose a hero to play as or to fight against, and to give or remove items; and more,
    as fits, to show the framework's reach.
  - **Other assets:** attribute sets, effects, templates and level providers, made and assigned as fits; a level
    provider is often a template too.
  - **Rules:** every asset a ScriptableObject, each using its features as fits, as sub-sets in attribute sets. Use
    all of PlayForge's asset features: workers, cues, textures, level providers, templates, scalers basic and
    complex, Local Data, tag logic (as blocking an ability's activation, or configuring effects), ability tasks
    (targeting and asset loading among them), ability requirements, retention groups, effect visibility, and the
    rest.

**Open. Ask the user; do not assume:**
1. ~~**Uncommitted changes**~~: decided, see above.
2. ~~**The test project, "PlayForge Proving Grounds"**~~: decided, see above.
3. ~~**Arcade Fighter after Phase 0**~~: decided, see above.
4. ~~**Commit space (GAS-15)**~~: decided, see above.
5. ~~**Pause (TS-Y, PC-7, PC-11)**~~: decided, see above.
6. ~~**Licence (SP-13)**~~: decided, see above.
7. ~~**Material packs (SP-17)**~~: decided, see above.
8. ~~**Known-red tests**~~: decided, see above.
9. ~~**Raw deltas and workers (GAS-25)**~~: decided, see above.
10. ~~**Disjoint and skill shots (GAS-36)**~~: decided, see above.
11. ~~**GameRoot and the hierarchy (PC-28)**~~: decided, see above.
12. ~~**When a random scaler rolls (GAS-27)**~~: decided, see above.
13. ~~**What Combine does (GAS-27)**~~: decided, see above.
14. ~~**Derived refreshes (GAS-27)**~~: decided, see above.
15. ~~**QuickOutline (SP-5)**~~: decided, see above.
16. ~~**Sample layout (SP-20)**~~: decided, see above.
17. ~~**The submission Editor (SP-22, SP-23)**~~: decided, see above.
18. ~~**The labs' scenes**~~: decided, see above (Decision 30).
19. ~~**Where the Debugger's edits go (ED-40)**~~: decided, see above (Decision 31).
20. **What a spawned thing knows (FEAT-24)**: an arrow an ability fires takes its speed from its archer's Projectile
    Speed attribute, read by tag on each frame of its flight, and fails if the archer doesn't have it. Should the
    arrow carry its own speed, set as it spawns? The user's note ends mid-sentence.
21. **The demo's play (Decision 37)**, to ask as Phase 7 starts: how a fight is played (the player's input, and how
    the opponent acts: an AI, a training dummy, or a second player); where the textures come from (placeholders made
    in code, or the user's art); whether the demo is one sample to import or several; and whether the Battle sample,
    which the labs use, stays.

## Roadmap

| Phase | Scope | Exit gate |
|---|---|---|
| 0 | Port Arcade Fighter's 13 fixes and 22 golden tests; make three one-line fixes | Golden tests green, none masked |
| 1 | Runtime correctness: exception isolation, queued state, every critical and high bug | Fault-injection and framework suites pass |
| 2 | Release readiness: samples, packaging, docs, Unity Test Framework, CI | Clean import into a fresh Unity 6 project: zero warnings, zero missing types |
| 3 | Performance and structure: caching, pooling, one GAS core, a Register builder | Zero steady-state allocation, enforced in CI. Closed on the local gates (Decision 27); CI enforces it since 2026-11-05 (SP-24) |
| 4 | Editor UX: Undo, theming, no polling, a debugger that can act | Editor smoke suite passes in both themes |
| 5 | Missing GAS features (FEAT-n) | Every test-project lab green |
| 6 | The user's UI list: the Debugger, The Forge, the scaler and attribute set drawers, and every drawer and inspector older than Phase 4's style (Decision 36) | Every row verified; the editor smoke suite passes in both themes |
| 7 | The demo refresh: five heroes, their abilities, items, attributes, effects, workers and cues, using every asset feature, with a scene to pick heroes and give items, in place of the old demos and Swarm Defender (Decision 37) | Every feature Decision 37 lists is used and shown; the old demos are gone; the smoke suite sweeps the new demo's assets |

## Phase 0 checklist

- [x] **Baseline.** Build, run the 12 golden tests, and record the result here with the date. Change nothing.
  Done 2026-10-29; see the Log.
- [x] Port each Arcade Fighter fix: diff Arcade Fighter's copy against canonical, and keep canonical's later
  changes where the two have diverged. Canonical had no later changes (see Environment). After the port,
  canonical differs from Arcade Fighter's `HEAD` only by GAS-3, the extra TS-2 README fixes, the golden test
  names and S23 onward, and Arcade Fighter's `GameRoot` `DontDestroyOnLoad` (PC-21, not ported).

  | Fix | Canonical location | Tracker ID | Status |
  |---|---|---|---|
  | PF-001 Resume set `Time.timeScale = 1` | `TaskSequenceProcess.cs:234` | TS-1 | verified |
  | PF-002 Ongoing requirements checked before tags compiled | `EffectSystemComponent.cs:127, 330-334, 372` | GAS-10 | verified |
  | PF-003 Source requirements never read | `AbilitySpec.cs:27-31`, `AbilityTags.cs:30-33` | GAS-11 | verified |
  | PF-004 Pre-change workers got a copy (frozen `ConstantImpacter.Snapshot` + `PreviewCommit`) | `AttributeSystemComponent.cs:275-341` | GAS-2 | verified |
  | PF-005 Current applied before Base | `IAttributeImpactCarrier.cs:138-139` | GAS-12 | verified |
  | PF-006 `HasActions` allocated; snapshot built every frame | `ActionQueue.cs:41, 46, 156`; both `EndOfFrame` files | GAS-P1 | verified |
  | PF-007 Tag and FrameSummary allocations (`GrantedTagCompiler`, pooling) | `TagCache.cs:60`, `TagQuery.cs:89-136`, `FrameSummary.cs:306-308` | GAS-P2 | verified |
  | PF-008 Bare `catch { }` in `AwaitAbility` | `AbilitySpecContainer.cs:113-116` | TS-4 | verified |
  | PF-009 Three editor warnings | `GameplayEffectDrawer.cs:159-176`, `AbilityEditor.cs:138-143`, `TheForge.ViewTab.cs:1640` | ED-1 | verified |
  | PF-010 README names types that do not exist | `Runtime/PlayForge/Core/TaskSequence/README.md` | TS-2 | verified |
  | PF-011 Step exceptions stalled the frame | `ProcessControlBlock.cs:225-247`, `ProcessControl.cs:337-344` | PC-1 | verified |
  | PF-012 Proxies recompiled every activation (`AbilityProxy.IsIdle`, `CompiledProxies`) | `AbilityActivationHandle.cs:26`, `AbilityProxy.cs:62-74` | TS-3 | verified |
  | PF-013 `OnSystemInitialized` could not be observed | `GameplayAbilitySystem.EndOfFrame.cs:34`, `RuntimeGameplayAbilitySystem.cs:137, 149` | GAS-13 | verified |

- [x] Port Arcade Fighter's extra 10 golden scenarios, taking the suite from 12 to 22. S13–S22 carry their
  tracker IDs in their names; S23–S25 are canonical's own (TS-1, GAS-3, GAS-1).
- [x] GAS-1: give `RuntimeGameplayAbilitySystem` the timing `EProcessStepTiming.UpdateAndLate`
  (`RuntimeGameplayAbilitySystem.cs:61`, and the second constructor at `:92`).
- [x] GAS-3: initialize `ItemShelf` (`ItemSystemComponent.cs:11`). Done ahead of PF-007, whose S18 needs it.
  The finding's other three bugs are now GAS-32 (Phase 1).
- [x] SP-16: remove the `targetSemantics: true` masks on S6, S8 and S11 (`PlayForgeGoldenTests.cs:55, 61, 64`).
  They already passed at the baseline, so there was no red. S6 now pins today's answer to Decision 4
  (GAS-15). The runner's move to the Unity Test Framework is now SP-18 (Phase 2).
- [x] Gate: the golden tests are green with none masked. Record the result here. Passed 2026-10-29; see the
  Log. The Phase 0 IDs are `verified`.

## Bug tracker

Severity: **C** critical, **H** high, **M** medium or low. The phase is the roadmap phase the fix belongs to.

### ProcessControl

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| PC-1 | C | 0 | A throw in `WhenUpdate` aborts the step loop; every later process misses its step (PF-011) | `ProcessControlBlock.cs:225-247`, `ProcessControl.cs:337-344` | verified |
| PC-2 | C | 1 | A throw in async `RunProcess` skips the queued state; the process stays Running forever and abilities hang (B10) | `ProcessControlBlock.cs:285-302`, `AbilityProxy.cs:171, 282` | verified |
| PC-3 | C | 1 | The hierarchy is keyed by prefab, not clone, so one GAS death cascades to every instance of the prefab | `ProcessControl.cs:500, 506`, `ProcessHierarchy.cs:112-118`, `GameplayAbilitySystem.EndOfFrame.cs:109` | verified |
| PC-4 | C | 1 | A forced terminate runs `WhenTerminate` twice (two Destroys, unregisters and events); forced Waiting doubles `WhenWait` | `ProcessControlBlock.cs:131-138, 207-219, 281-302` | verified |
| PC-5 | C | 1 | A queued Terminate on a running Lazy process never completes (`ActionProcess`, runtime GAS, `TaskSequenceProcess`) | `LazyMonoProcess.cs:20-24`, `LazyRuntimeProcess.cs:24-28` | verified |
| PC-6 | C | 1 | The runtime `Register` overloads that take null data throw a NullReferenceException after using up a cache index; `RunProcessTask` hits this on every activation | `ProcessControl.cs:516, 524, 549`, `RunProcessTask.cs:25` | verified |
| PC-7 | C | 1 | A control-state change queues a default transition for every process: it parks Synchronous processes, starts RequiresControl ones and re-runs RunThenWait ones | `ProcessControl.cs:1009, 1186-1195, 1278` | verified |
| PC-8 | C | 1 | A throw in `WhenTerminate` leaves an unkillable zombie that is still stepped | `ProcessControlBlock.cs:211-212` | verified |
| PC-9 | H | 1 | An out-of-scene object is cloned as if it were a prefab; the original stays uninitialized and leaks (C19) | `MonoWrapperProcess.cs:43-53` | verified |
| PC-10 | H | 1 | A GameObject with several processes has no single teardown; an external Destroy or scene unload never unregisters (C13) | `MonoWrapperProcess.cs:93`, `AbstractMonoProcess.cs:146-149` | verified |
| PC-11 | H | 1 | Relay Pause waits for `RunProcess` to end, replaces a pending Terminate, and Unpause re-runs from the start | `ProcessControl.cs:796-811`, `ProcessControlBlock.cs:121-124, 186-194` | verified |
| PC-12 | H | 1 | Pause and resume in one frame let the old run's cancellation kill the new run (shared `_cts`) | `ProcessControlBlock.cs:268, 291-297` | verified |
| PC-13 | H | 1 | Callbacks report old state equal to new; watchers never see Terminated; Unregistered fires after removal | `ProcessControl.cs:1128, 1141-1148, 738-742` | verified |
| PC-14 | H | 1 | The first watcher never gets `onStartWatching`; `AddActions` edits a struct copy; unwatching inside a callback throws | `ProcessControl.cs:177-226` | verified |
| PC-15 | H | 1 | A throw in `WhenInitialize` leaves a half-registered process | `ProcessControl.cs:947-948, 970-974` | verified |
| PC-16 | H | 1 | Data packets are never released on unregister, so long-lived packets leak relays | `ProcessControl.cs:504, 549, 719` | verified |
| PC-17 | H | 1 | `GroupProcess` never terminates itself; `DetachMember` removes every watcher | `GroupProcess.cs:88, 151, 180` | verified |
| PC-18 | H | 1 | `LazyRuntimeProcess` never sets `IsInitialized`, so one instance can be registered twice | `LazyRuntimeProcess.cs:16-19` | verified |
| PC-19 | H | 1 | `ActionProcess(Action)` never runs; `ActionProcess<T>`'s second constructor never sets its source | `ActionProcess.cs:9-12, 39-45` | verified |
| PC-20 | H | 1 | Scene processes are reparented under GameRoot at startup | `ProcessControl.cs:391`, `AbstractMonoProcess.cs:71-74` | verified |
| PC-21 | H | 1 | GameRoot is not DontDestroyOnLoad; after a scene load, every default overload uses a destroyed handler | `GameRoot.cs`, `Bootstrapper.cs:76`, `ProcessDataPacket.cs:73` | verified |
| PC-22 | H | 1 | Step priority is computed but never used | `ProcessControl.cs:91, 106-129` | verified |
| PC-23 | M | 1 | Bootstrap: a duplicate GameRoot still claims Instance; a failed register throws before the fallback; `[SerializeReference]` on ScriptableObjects (C03); an un-bootstrapped ProcessControl throws every frame | `GameRoot.cs:25-30`, `Bootstrapper.cs:18-19, 78-82`, `ProcessControl.cs:37, 327` | verified |
| PC-24 | M | 3 | `PooledMonoProcessInstantiator` is a stub that returns null and leaks GameObjects. Its `CleanProcess` did nothing, and a process's wrapper hands an instantiator its object to clean, so a process made through it kept its GameObject. Nothing used it. Removed, rather than built into a pool (Decision 23); a pool extends `AbstractMonoProcessInstantiator` | `PooledMonoProcessInstantiator.cs:7-22`, `MonoWrapperProcess.cs:90` | verified |
| PC-25 | M | 1 | `ProcessTaskUtil.DoWhile` loops forever; `RemainingRuntime` truncates before multiplying | `ProcessTaskUtil.cs:111-117`, `ProcessRelay.cs:42-43` | verified |
| PC-26 | M | 1 | Lifecycle edges: no domain-reload reset; `is null` misses destroyed singletons; the hierarchy throws after a root is destroyed; changing `ProcessTiming` at runtime; `ResetProcessControl` keeps its watchers, which is harmless, as it only runs on a fresh instance | `ProcessHierarchy.cs:313`, `ProcessControl.cs:424-446, 1021, 1064` | verified |
| PC-27 | H | 1 | `Unpause` runs a process while ProcessControl itself is Waiting or ClosedWaiting, against 8 cells of the transition table: `ValidateStateTransition` never reads the control state. See Decisions 5 | `ProcessControl.cs:776-791, 1361-1390` | verified |
| PC-28 | M | 1 | The hierarchy reads a process's parent when it registers, before `WhenInitialize` applies `PARENT_TRANSFORM`, and never hears of a later re-parent, so a process parented through its data is no one's child for a cascade. Decide this with PC-20, because every `SceneRoot` process would become GameRoot's child. See Decision 11 | `ProcessControl.cs` `PrepareCreatedProcess`, `AbstractMonoProcess.cs:71-74`, `ProcessHierarchy.cs:121` | verified |
| PC-29 | M | 1 | A `StartState` of Closed or ClosedWaiting refuses GameRoot as well, so neither works as a start state. Since PC-23 the Bootstrapper reports the refusal. Registering GameRoot under Ready and entering `StartState` afterwards would fix it | `ProcessControl.cs:247`, `Bootstrapper.cs` | verified |
| PC-30 | M | 1 | A process registered on an object that was never active outlives it. Unity sends no OnDestroy to such an object, so the process's block keeps stepping and its hierarchy node stays. This is a gap in PC-10's fix | `AbstractMonoProcess.cs` `OnDestroy`, `ProcessControl.cs` `EndDestroyedProcess` | verified |
| PC-31 | M | 1 | A Mono process class that declares its own `OnDestroy` hides the base's, which Unity then never calls, so the process outlives its object. PC-30's watch covers only objects that never woke. A warning once per such class at registration, and watching its processes as PC-30 does, would close it | `AbstractMonoProcess.cs:151`, `ProcessControl.cs` `SweepDormant` | verified |
| PC-32 | M | 2 | A Mono process terminated outside play mode keeps its object: `MonoWrapperProcess.WhenTerminate` calls `Object.Destroy`, which Unity refuses in edit mode, logging an error. Editor tools and edit-mode tests reach it; the Test Runner's golden S22 did (SP-18), as it fails a test on any logged error | `MonoWrapperProcess.cs:104` | verified |
| PC-33 | H | 5 | A pool's object was destroyed as its process ended. The Mono wrapper handed a finished object back through the object's own `Instantiator`, which a clone never has, as the field isn't serialized, so an instantiator that made it, through a template's `Instantiator` or the handler's, never saw it again and pooling couldn't work (Decision 23 leaves pools to games). Found by the Bullet heaven lab. The wrapper keeps the instantiator that made the object and hands it back to that one | `MonoWrapperProcess.cs` (`ConfigureWrapper`, `WhenTerminate`) | verified |
| PC-34 | M | 5 | Ending a process searched its step group's list for it from the front and shifted what came after, so ending one cost as much as the processes stepped with it: ending the newest of 10,000 took about 4.4 µs against 0.7 among 1,000, and ending every one of them cost the square of how many there were. The review's O(n) unregister, which Bullet heaven's 10,000 bolts didn't show, as they end oldest first. Found by the stress suite's churn. Fixed: a step group knows each process's position, so an ending leaves a hole there at once, which stepping passes over, and the holes are swept out in place once they outnumber the processes, the order kept | `Management/GameplayProcess/Core/ProcessWorld.cs` (`StepGroup`) | fixed |

### TaskSequence

TS-1 to TS-10 are Arcade Fighter's findings. TS-A to TS-X are new findings from the review. TS-Y and TS-Z
had no letter in the review. TS-AA onward came from the test project.

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| TS-1 | C | 0 | Resume sets `Time.timeScale = 1` (PF-001) | `TaskSequenceProcess.cs:234` | verified |
| TS-2 | M | 0 | The README names types that do not exist (PF-010) | `Core/TaskSequence/README.md` | verified |
| TS-3 | H | 0 | The proxy and sequences are recompiled on every activation; `IsCriticalSection` builds the inner sequence 4–5 times (PF-012) | `AbilityProxy.cs:62`, `AbilitySequenceCompiler.cs:30-80`, `RunSequenceTask.cs:18-21` | verified |
| TS-4 | H | 0 | A bare `catch { }` swallows ability errors (PF-008) | `AbilitySpecContainer.cs:113-116` | verified |
| TS-5 | M | 3 | LINQ on a hot path; `AppendPath` concatenates strings and grows without bound on repeat. PF-007 (Phase 0) took the LINQ out of `HasAnyStageConditions` and made the path's parts join only when read, which left them to grow: a sequence that repeated added its stages' parts every time round, and its twentieth run had a 272-character path to its first's 25. Now each part a run or a stage adds comes off as it ends, each repeat starts where its iteration began, and a failed run keeps the path to where it failed for its log. The parts are formatted once, and the LINQ left on an ability's activation, in `AbilityProxy`, and in a chain's `HasConditions` is loops | `TaskSequenceRuntime.cs` (`Run`, `RunStage`, `RestartStage`), `ProcessDataPacket.cs` (`TrimPath`), `AbilityProxy.cs`, `AbilityTaskAdapter.cs`, `AbilitySequenceCompiler.cs` | verified |
| TS-6 | M | 5 | Global time only; `RunProcessTask` defaults to unscaled time (C02) → FEAT-7, in Phase 5's foundations (Decision 30). Fixed with FEAT-7's clocks: `RunProcessTask` and `ProcessRelay.RemainingRuntime` default to scaled time, and an entity's sequences and waits run by its own clock | `RunProcessTask.cs:14`, `ProcessRelay.cs` | verified |
| TS-7 | M | 5 | Critical sections are static; the compiler never emits a `BranchStage` (C12). Fixed: an ability's stage has a Condition (a tag in the activation's data, on the caster or the target, there or missing), which the compiler emits as a branch; Apply Effects' Record Landed As tags the data as an effect lands; and a critical section ends as the run starts a stage past its last critical stage, so one a branch skipped lets the claim go | `AbilitySequenceCompiler.cs`, `AbilityBehaviour.cs`, `ApplyEffectsAbilityTask.cs`, `TaskSequenceRuntime.cs` | verified |
| TS-8 | M | 3 | `TryActivate` runs synchronously deep into the first stages (C16). Measured: it runs an instant ability to its end, and a slower one to its first wait, before it returns. An ability asked for again from its own end or claim-released callback nested inside the call: 40 activations, 40 deep, in one frame before a cap, so an uncapped loop overflowed the stack. Activation stays synchronous (Decision 25); an ability asked for again while its own activation call still runs starts next frame | `AbilitySystemComponent.cs` (`TryActivateAbility`), `AbilitySpecContainer.cs` (`ActivateAbility`) | verified |
| TS-9 | H | 1 | `OnAbilityEnd` means claim released; task callbacks fire only on the legacy path (C07) | `AbilityProxy.cs` | verified |
| TS-10 | M | 3 | About 14 CancellationTokenSources per three-stage ability (C08, B11). Every stage made a second source for its policy, linked to its own, though only WhenAny and WhenN cancel one. Measured: a frame that runs one stage of one instant task allocated 1,912 bytes under WhenAll, the same as under a policy the runtime doesn't know. Now a stage under WhenAll or RepeatUntilSkipped hands its policy its own source: 1,464 bytes, 448 less a stage, and about 10 sources to a three-stage ability. The rest are one each for an activation phase, a run of a process, a sequence run and a stage | `TaskSequenceRuntime.cs` (`ExecuteStageInternal`) | verified |
| TS-11 | L | 3 | A task asks whether its sequence is paused by looking its token up in a dictionary whose default comparer boxed the token on every lookup under Unity's Mono, where a token isn't IEquatable: 24 bytes a frame for an ability waiting in a `DelayTask` or a `ChannelAbilityTask`, measured for Phase 3's exit gate. The dictionary now compares tokens as tokens | `TaskSequenceRuntime.cs` (`RunsByToken`) | verified |
| TS-A | C | 1 | A WhenAny or WhenN stage hangs on outside skip, timeout or max duration | `SequenceCore.cs:291-294, 351-354` | verified |
| TS-B | C | 1 | Any-policy ability stages end at once (`StageCallbackTask` satisfies "any") | `AbilitySequenceCompiler.cs:206, 296-300` | verified |
| TS-C | C | 1 | Interrupted or failed targeting still executes; `TargetingFailed` is never read | `AbilityProxy.cs:174-177, 197-200`, `AbilitySpecContainer.cs:63-88, 138`, `AbstractTargetingAbilityTask.cs:66-69` | verified |
| TS-D | H | 1 | `ApplyUsageAtStageStart` fires `AbilityStageEnded` at stage start | `AbilitySequenceCompiler.cs:211-232` | verified |
| TS-E | C | 1 | Unguarded callbacks in `finally` blocks; a throwing listener skips claim release | `TaskSequenceRuntime.cs:212, 389, 544` | verified |
| TS-F | H | 1 | `RunSequenceTask` shares the outer data packet | `TaskSequenceRuntime.cs:124, 231-232` | verified |
| TS-G | C | 1 | Cancelling the outer ability can orphan the nested sequence | `RunSequenceTask.cs:75`, `TaskSequence.cs:106` | verified |
| TS-H | H | 1 | FixedUpdate and LateUpdate sequences never step | `TaskSequenceProcess.cs:137`, `RuntimeWrapperProcess.cs:32-40` | verified |
| TS-I | C | 1 | Definitions are mutated at runtime and have a single `Runtime` slot, so concurrent runs clobber each other | `TaskSequence.cs:57-67`, `TaskSequenceRuntime.cs:801` | verified |
| TS-J | H | 1 | Skip-and-maintain calls `Clean` on tasks that are still running | `TaskSequenceRuntime.cs:302-306` | verified |
| TS-K | H | 1 | Maintained-stage bug (first of two) | `TaskSequenceRuntime.cs:777-791` | verified |
| TS-L | H | 1 | Maintained-stage bug (second of two) | `TaskSequenceRuntime.cs:962, 969-971` | verified |
| TS-M | C | 1 | Async repeat never re-runs `Prepare`; the synced `Delay` spins forever on repeat | `SwarmDefenderAbilitySequences.cs:70-72` (workaround) | verified |
| TS-N | H | 1 | Sub-stage settings are ignored | `TaskSequenceRuntime.cs:460-505` | verified |
| TS-O | H | 1 | `IsCriticalSection` on a chain throws; repeat recursion | `TaskSequence.cs:445, 462` | verified |
| TS-P | H | 1 | Sync-only `TaskSequenceProcess` paths are broken | `TaskSequenceProcess.cs:95-107, 141, 199-217` | verified |
| TS-Q | H | 1 | `BuildSyncRunner` drops features | `SequenceStage.cs:467-486`, `TaskSequenceBuilder.cs:554` | verified |
| TS-R | M | 1 | Rethrows lose the stack trace. Doesn't reproduce: every rethrown error has passed through an await, which keeps its trace, and a test holds it there | — | verified |
| TS-S | M | 1 | The legacy `Inject` path throws a NullReferenceException. Doesn't reproduce since TS-AC took the legacy fallback away, and a test holds it there | — | verified |
| TS-T | H | 1 | `ActiveCache` drifts from the real state | `AbilitySystemComponent.cs:22, 385, 419` | verified |
| TS-U | H | 1 | `RunProcessTask` keeps state on a shared `[SerializeReference]` instance | `RunProcessTask.cs:11, 18` | verified |
| TS-V | H | 1 | A non-cancellation targeting exception leaks the activation handle | `AbilityProxy.cs` | verified |
| TS-W | M | 1 | `ElapsedTime` is unreliable | — | verified |
| TS-X | M | 1 | Payload lists are aliased between packets | `AbilityDataPacket.cs:50` | verified |
| TS-Y | M | 1 | Fake pause: `TryHandlePause` returns `processActive`, so a paused sequence keeps running | `TaskSequenceProcess.cs:225-228` | verified |
| TS-Z | M | 3 | Frame drift: `Task(Action)` costs a frame, and relay polling adds a frame per nesting level. Measured: four `Task(Action)` stages took 5 frames to four `Do` stages' 1; a run waited a frame at its end for maintained stages it didn't have; an instant ability ended 2 frames after it started, and a sequence nested by `RunSequenceTask` added 2 more. Now `Task(Action)` runs its action at once, a run waits only for maintained stages it has, and `ProcessRelay.WhenTerminated` completes in the frame its process ends, which the proxy and `RunSequenceTask` wait on instead of polling (Decision 24). An instant ability ends in the frame it starts. A repeating sequence that takes no frame still takes one per repeat | `AbilityProxy.cs:171, 282`, `RunSequenceTask.cs`, `TaskSequenceRuntime.cs` (`Run`), `SequenceCore.cs` (`DelegateSequenceTask`), `ProcessControlBlock.cs` (`TryTerminate`) | verified |
| TS-AA | H | 1 | A cancelled sequence cleans its tasks while they still run: `AttachExternalCancellation` throws the moment the token cancels, so `CleanStage` runs before `Execute` has unwound | `TaskSequenceRuntime.cs:286, 305` | verified |
| TS-AC | H | 1 | An ability activated while ProcessControl refuses new processes (Closed, ClosedWaiting) falls back to legacy execution, outside ProcessControl, so it runs anyway and no pause can hold it | `AbilityProxy.cs` `ActivateViaSequence`, `ActivateTargetingViaSequence` | verified |
| TS-AD | H | 1 | A stage's or sequence's timer outlives what it timed: its source is disposed and never cancelled, so the timer fires into the next round of a repeating sequence, or into a stage run again | `TaskSequenceRuntime.cs` stage end, `Cleanup` | verified |
| TS-AB | H | 1 | `ChannelAbilityTask` yields in its body and `DoWhileAsync` yields again, so a channel counts one frame in two and lasts twice its `ChannelDuration` | `ChannelAbilityTask.cs:27-36`, `ProcessTaskUtil.cs:94-102` | verified |

### GAS core

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| GAS-1 | C | 0 | The runtime GAS steps in Update only, so its end of frame (workers, death) never runs | `RuntimeGameplayAbilitySystem.cs:61` | verified |
| GAS-2 | C | 0 | Pre-change workers have no effect (PF-004, now worse) | `AttributeSystemComponent.cs:275-341`, `EffectSystemComponent.cs:115, 157, 186`, `ForgeHelper.cs:229-235` | verified |
| GAS-3 | C | 0 | `ItemShelf` is never created, so `GiveItem` throws, and every read of the empty inventory allocates a new list. The rest of this finding moved to GAS-32 | `ItemSystemComponent.cs:11, 279` | verified |
| GAS-4 | C | 1 | The independent-durations container throws on construction; its struct copies never count down | `AbstractEffectContainer.cs:32, 54`, `IndependentDurationsContainer.cs:18-110` | verified |
| GAS-5 | C | 1 | Re-application always adds one stack; Max is never enforced; the last config wins | `SingleDurationEffectContainer.cs:58`, `StackQueueEffectContainer.cs:62`, `AbstractEffectConfig.cs:261-264`, `GameplayEffectDuration.cs:281-291` | verified |
| GAS-6 | C | 1 | Removal requirements are inverted and AND-ed; application requires the removal group to pass | `EffectTagRequirements.cs:60-63`, `GameplayEffect.cs:169-179` | verified |
| GAS-7 | C | 1 | Cost validation compares against the signed magnitude, so it always passes | `CommonValidations.cs:19-21` | verified |
| GAS-8 | C | 1 | OnRemove contained effects land on the source | `AbstractEffectContainer.cs:77-80` | verified |
| GAS-9 | C | 1 | The runtime identity copy drops starting abilities, starting items and the duplicate-equip flag | `RuntimeGameplayAbilitySystem.cs:459-479` | verified |
| GAS-10 | C | 0 | Ongoing requirements are evaluated before tags exist and never re-checked (PF-002) | `EffectSystemComponent.cs:127, 330-334, 372` | verified |
| GAS-11 | H | 0 | Source tag requirements are never enforced (PF-003) | `AbilitySpec.cs:27-31`, `AbilityTags.cs:30-33` | verified |
| GAS-12 | H | 0 | Current is applied before Base, so the clamp loses the delta (PF-005) | `IAttributeImpactCarrier.cs:138-139`, `CachedAttributeValue.cs:242-281` | verified |
| GAS-13 | H | 0 | `OnSystemInitialized` cannot be observed (PF-013) | `GameplayAbilitySystem.EndOfFrame.cs:34`, `RuntimeGameplayAbilitySystem.cs:137, 149`, `AttributeSystemComponent.cs:49` | verified |
| GAS-14 | H | 1 | Shelved effects grant the source's asset tag to their target | `AbstractEffectContainer.cs:108` | verified |
| GAS-15 | H | 1 | Commits are stored before modifiers but read through them (−30 under ×1.5 lands as −45). See Decisions 4 | `CachedAttributeValue.cs:241-327` | verified |
| GAS-16 | H | 1 | Tag queries count distinct tags, not weights; `SameRoot` differs between overloads | `GameplayAbilitySystem.cs:246-249`, `TagQuery.cs:83-128` | verified |
| GAS-17 | H | 1 | Tag hierarchy helpers and the implicit `string → Tag` conversion return hash-padded tags | `Tag.cs:73-91, 148-189, 295` | verified |
| GAS-18 | H | 1 | Attribute-set impact workers are registered and run twice | `GameplayAbilitySystem.cs:39`, `AttributeSet.cs:55`, `ForgeHelper.cs:922-926` | verified |
| GAS-19 | H | 1 | Tag workers tick twice per frame | `GameplayAbilitySystem.cs:93`, `GameplayAbilitySystem.EndOfFrame.cs:159` (Runtime: `:177, :186`) | verified |
| GAS-20 | H | 1 | Ability removal leaves stale tags and a stale active index; the next claim throws | `AbilitySystemComponent.cs:45, 225-227, 414` | verified |
| GAS-21 | H | 1 | `ClaimActive` picks the wrong slot for duplicates and mismatches policies | `AbilitySystemComponent.cs:381-385, 416-419` | verified |
| GAS-22 | H | 1 | Duration scalers are evaluated before they are initialized (attribute-backed duration returns 0) | `GameplayEffectDuration.cs:106-115` | verified |
| GAS-23 | H | 1 | Max-abilities and max-items have no initializer, so magnitude is likely 0. From reading only; verify first | `EntityIdentity.cs:31, 36` | verified |
| GAS-24 | H | 1 | Infinite periodic effects ignore the tick-interval scaler (C05) | `GameplayEffectDuration.cs:158-161` | verified |
| GAS-25 | H | 1 | Raw deltas are tagged `DisallowImpact`, so workers skip them (C10). See Decision 9 | `SourcedModifiedAttributeValue.cs:40`, `ForgeHelper.cs:205` | verified |
| GAS-26 | H | 1 | Projectile disjoint re-targets the same caller, and its payloads pile up (C14). Its covering homing shots only is GAS-36 | `DisjointProxyTaskBehaviour.cs:53`, `DisjointTarget.cs:95`, `GameplayAbilitySystem.cs:230-233` | verified |
| GAS-36 | H | 1 | Projectile disjoint covers homing shots only: a skill shot registers no intent, so it lands in full on an entity that disjointed. Dota leaves skill shots alone, and so does PlayForge, under Decision 10: the disjoint's docs say so | `DisjointProxyTaskBehaviour.cs`, `AbstractTargetedMonoProcess.cs` | verified |
| GAS-27 | M | 1 | Scalers and workers: `RandomizedScaler` re-rolls (Decision 12); `InlineAndDeferred` impact workers are dropped; `StandardWorkerGroup` NREs; set "Combine" did nothing, so the first element stood alone, and its loop took each element twice (Decision 13); derived refresh bypasses workers (Decision 14) | `RandomizedScaler.cs:29-46`, `ImpactWorkerCache.cs:88-98`, `StandardWorkerGroup.cs:30-89`, `AttributeSet.cs:401-430`, `AttributeBlueprint.cs` `Combine`, `AttributeSystemComponent.cs` `RefreshRegulatedAttributes` | verified |
| GAS-28 | M | 1 | Effects: removal workers see only the last impact; contained OnApply recursion has no cycle guard; `RecordEffectApplied` and `RecordEffectRemoved` are never called | `EffectSystemComponent.cs:82-89, 110-119`, `FrameSummary.cs:154-157` | verified |
| GAS-29 | M | 1 | Target validation and range checks fail for a runtime GAS | `AbstractTargetingAbilityTask.cs:73, 89-91`, `CommonValidations.cs:132-138` | verified |
| GAS-30 | M | 1 | `UnlinkAllChildren` re-links; Ability unlink checks Cost instead of Cooldown | `EntityIdentity.cs:202, 210`, `Ability.cs:151` | verified |
| GAS-31 | M | 1 | `AbstractTargetedMonoProcess` dereferences a missing target after warning about it | `AbstractTargetedMonoProcess.cs:22-26` | verified |
| GAS-32 | C | 1 | Split from GAS-3, reachable once items can be given: `ClearAllItems` skips every other item, so their equipped effects stay; unequip removes every source's effect; `ItemSpec.IsActive` is always false, which drops every action queued by an item's effect; an item with no `ActiveAbility` throws on equip with its effects already on; tags compile before `IsEquipped` clears, so they outlive the item | `ItemSystemComponent.cs:230-240`, `ItemSpecContainer.cs:133, 150`, `ItemSpec.cs:32-35` | verified |
| GAS-33 | H | 1 | At a fixed frame time, a durational periodic effect can lose its last tick for good. Its period and duration timers lose the same delta, so the last tick and the expiry share a frame and rounding decides which comes first: 5 ticks in 5 s lands 4 at 60, 120 and 144 fps. Jittered frame time hides it | `EffectSystemComponent.cs:361, 391-393`, `NonStackingEffectContainer.cs:31` | verified |
| GAS-34 | M | 1 | A stack expiring doesn't refresh Continuous entries, though gaining one does, so a scaler that reads the container keeps the old stack count until the next application | `EffectSystemComponent.cs:237-243, 391`, `StackQueueEffectContainer.cs:31-34`, `IndependentDurationsContainer.cs` | verified |
| GAS-35 | M | 1 | `EnforceScaling` tags its rescale `AllowImpact`, so an impact worker that accepts unworkable impacts sees the system's own rescaling. From reading only; verify first | `CachedAttributeValue.cs` `EnforceScaling` | verified |
| GAS-37 | M | 1 | `EnforceScaling` scales from Current after the clamp has moved it, so under ZeroToBase an 80/100 attribute that loses half its Base ends at 50/50, not 40/50. The diagnosis read 100/100 to 25/50, which the clamp happens to put right. `ATTR_SET_DefaultSet` pairs ZeroToBase with `AutoScaleWithBase` | `CachedAttributeValue.cs:558-562` | verified |
| GAS-38 | M | 1 | A runtime GAS's position is a `StaticTargetingPacket` that nothing updates, and the built-in targeting tasks read the caster's transform. For a runtime caster, `AutoTargetTask` ignores its range and takes every Mono GAS, unsorted, and `SelectDirectionTargetTask` always fails | `AutoTargetTask.cs:50`, `AutoTargetByAffinityTask.cs:50`, `SelectDirectionTargetTask.cs:49`, `SelectGasTargetInRangeTask.cs:47`, `RuntimeGameplayAbilitySystem.cs:228` | verified |
| GAS-39 | L | 1 | `FrameSummarySnapshot` copies neither the applied and removed effects nor the deaths and ability injections, so `FrameComplete` listeners never see them | `FrameSummary.cs:356-367` | verified |
| GAS-40 | L | 1 | A set's `UseAverage` takes the first element, and `Ignore` throws | `AttributeSet.cs:423-458` | verified |
| GAS-41 | L | 1 | On `RelativeOperationContextImpactWorker`, `InlineAndDeferred` does the work twice, once inline and once queued, so lifesteal heals double. Hide the option there, or document it | `RelativeOperationContextImpactWorker.cs:22` | verified |
| GAS-42 | M | 1 | A runtime GAS's death dereferences `Relay`, so one stepped by hand, outside ProcessControl, throws at its end of frame | `RuntimeGameplayAbilitySystem_EndOfFrame.cs:127` | verified |
| GAS-43 | M | 1 | `AutoTargetTask` sizes its collider buffer by `MaxTargets`, so 0, meaning every match, finds nothing, and at 1 the caster's own collider can fill the buffer and leave no target | `AutoTargetTask.cs:84-85` | verified |
| GAS-44 | M | 3 | An attribute set that includes itself, directly or through another set, recurses without end: `GetGrantedTags` and the attribute builder have no guard, and `GetUnique` skips only a set that names itself. Found writing ED-16's docs, which warn of it | `AttributeSet.cs:66-90, 381` | verified |
| GAS-45 | L | 3 | Two settings nothing reads: an Item's `AllowDuplicates` (the entity's Allow Duplicate Items decides) and an entity's `CapAtMaxLevel`, which only the inspector shows. Found writing ED-16's docs, which say so. The user's call: remove both, with their toggles and stored values | `ItemDefinition.cs:25`, `EntityIdentity.cs:25` | verified |
| GAS-46 | C | 2 | Apply Effects never found its target. Targeting files a targeting packet under `Target`, and what reads a target (Apply Effects, Apply/Remove Effects, Check Range, the after-targeting checks, a spawned process) asks `TargetReal` for the entity, which nothing wrote. A Runtime GAS's own packet didn't point at it either. The tests set `TargetReal` by hand. Found writing ED-16's docs | `AbilityDataPacket.cs:125-126`, `AbilitySpecContainer.cs:103`, `RuntimeGameplayAbilitySystem.cs:67, 97` | verified |
| GAS-47 | H | 3 | The Mono GAS writes affiliation from its registration data onto its shared Entity asset, so every entity that uses the asset changes side, and in the editor the asset itself is changed | `GameplayAbilitySystem.cs:64-67` | verified |
| GAS-48 | M | 3 | Unequipping removes the item's Active Ability even when equipping didn't grant it, so an entity that already had that ability loses its own | `ItemSpecContainer.cs:154-182` | verified |
| GAS-49 | M | 3 | Both hubs give their Starting Abilities, and try Activate Immediately ones, before they build their attributes, so the default Cost rule refuses such an ability if it has a cost | `GameplayAbilitySystem.cs:82-84`, `RuntimeGameplayAbilitySystem.cs:153-155`, `AbilitySystemComponent.cs:270-275` | verified |
| GAS-50 | M | 3 | A Durational or Infinite effect with Tick On Application but no periodic ticks derives OnApply for Add and Flat Bonus, and only the Instant path commits OnApply, so its change never lands. The Audit flags the shape | `GameplayEffectImpact.cs:92-104`, `EffectSystemComponent.cs:141-147, 214-225` | verified |
| GAS-51 | L | 3 | Item stacking is declared but not built: an Item's `MaxStackSize` is never read, and the inspector doesn't draw it; `ItemSpecContainer.ItemStacks` is never set. Remove both, or build stacking as a Phase 5 feature. Found removing GAS-45. The user's call: keep both and build stacking in Phase 5, as FEAT-14 | `ItemDefinition.cs:25`, `ItemSpecContainer.cs:25` | verified (FEAT-14) |
| GAS-52 | H | 3 | A runtime entity's abilities outlive it. A Mono process ends the processes it handles as it ends, and a runtime process didn't, so an ability running on a runtime entity ran on, however the entity ended: a 10-minute delay was still running 5 frames after. An entity of either kind also took activations once ended, and a destroyed Mono entity threw on one. Found measuring Phase 3's exit gate. A runtime process now ends what it handles, as a Mono process does, and an ending entity's ability system shuts first, refusing activation before it reads the entity | `AbstractRuntimeProcess.cs` (`WhenTerminate`), `GASCore.cs` (`WhenTerminate`), `AbilitySystemComponent.cs` (`ShutDown`, `TryActivateAbility`), both hubs' `WhenTerminate` | verified |
| GAS-53 | H | 3 | Interrupting every running ability under a policy, all at once or by turning the ability system off walked the live set of them. TS-Z made an interrupted ability end inside the interrupt, taking itself off that set, so the walk threw "Collection was modified" once the first had ended, and the rest ran on: 1 of 2 ended, on both hubs. It now walks a copy | `AbilitySystemComponent.cs` (`Inject`, `ClearAbilityCache`) | verified |
| GAS-54 | M | 4 | An attribute set element with no Attribute, as a list's "+" leaves one, throws as the entity starts: `AttributeSetMeta` files the set's elements in a dictionary by their attribute, and a null key throws, so the entity gets none of its attributes. Such an element now gives nothing, and the set's unique count leaves it out | `AttributeSet.cs:92, 369-373` | verified |
| GAS-55 | M | 4 | Use Minimum takes the first colliding element: `ForgeUtils.IndexMin` started its search from `float.MinValue`, so no value was ever below it. It starts from `float.MaxValue` | `ForgeUtils.cs:23-38` | verified |
| GAS-56 | M | 5 | An effect's While-on and Removal requirements are checked again only when an effect is applied or removed (`requirementsDirty`, set in `EffectSystemComponent.ApplyGameplayEffect` and where effects leave the shelf). `CompileGrantedTags` doesn't mark them, so a tag granted or taken back by an ability, an item or a loose tag (FEAT-16) doesn't end an effect until another effect comes or goes. FEAT-3's events should mark them. Found in the Phase 5 survey. Fixed with FEAT-3: a rebuild of the tags that changes one marks the tag cache, and the effect system's next tick runs its requirement pass when an effect is on the shelf. An effect that comes or goes runs the pass itself, and a tag added straight to the cache asks for nothing, as the next rebuild drops it | `EffectSystemComponent.cs`, `TagCache.cs`, `GASCore.cs` (`CompileGrantedTags`) | verified |
| GAS-57 | H | 5 | A relative-operation impact worker, the framework's lifesteal, read the impacted attribute's value, never its Relative Attribute, though it checked the Relative Attribute was there: so lifesteal by a Lifesteal share healed the attacker by its own health times the blow, a full heal. Found by the MOBA lab. It reads the Relative Attribute, and one with none set reads the impacted attribute still, as GAS-41's test does | `RelativeOperationContextImpactWorker.cs` (`GetWrtTarget`) | verified |
| GAS-58 | M | 5 | A lasting effect's magnitude was read as its modifier registered, and nothing read it again as its origin's level changed, so an item, an ability or an entity that levelled up kept its effects' first-level values, as GAS-34's stacks did. Found by the Roguelite lab. The GAS core hears its level system's changes, and the shelf re-reads each effect whose origin's level is the one that changed. Effects the entity's items put on other entities aren't on its shelf, and wait for their next read | `EffectSystemComponent.cs` (`RefreshEffectsLevelledBy`), `GASCore.cs` | verified |
| GAS-59 | M | 5 | `SystemLevelsComponent.TrySetLevel` and `TryModifyLevel` set the level before the max, so a level raised past the old max at once was held at it, and only the max moved. Found by the mirror clones scenario. Fixed: the max goes first | `LevelComponent/SystemLevelsComponent.cs` | verified |
| GAS-60 | M | 5 | A restore let go of every ability the entity had and gave each back as a new one, so another entity's effect that one of them applied kept hold of the old, and that entity's next save named no slot for it, and a load after that took the effect as its source's own. Two entities that bled each other could load in no order that kept both. Found by the rollback scenario. Fixed: a restore keeps each ability the entity holds in the slot its save gives it, its level from the save's levels, and lets go of and gives back the rest | `Save/ForgeSave.cs` (`Put`), `AbilitySystem/Core/AbilitySystemComponent.cs` (`ClearForRestore`) | verified |
| GAS-61 | M | 5 | A save kept an effect's time left and its time to its next tick as floats, which its timers, doubles since GAS-33, rounded, so a restored effect ran on from a time a hair off, and came to differ from one never restored in the last digit. Found by the rollback scenario. Fixed: the clock's capture and restore carry doubles (`ExactDurationRemaining` and `ExactTimeUntilPeriodTick` on the containers), and `EffectSave` keeps them; a save written with floats still loads | `GameplayEffect/Core/GameplayShelfContainer/*`, `Save/ForgeSnapshot.cs` | verified |
| GAS-62 | H | 5 | A targeting task filed each target it selected by setting the primary, so each one replaced the last: an Auto Target of every match kept only the farthest, and one of two kept the second nearest. Then Target Real took only the first packet's entity, and Apply Effects and Apply/Remove Effects only the first target. So an area ability, Auto Target and then Apply Effects, struck one entity, and Auto Target From Data with Primary Only off kept one of the targets it says it adds. Found by the stress suite's area run. Fixed: the auto-targeting tasks file every target they select, in order (`SetTargetingPackets`), a resolve files each one's entity under Target Real, and both apply tasks reach each target, through `TryGetAt`, which builds no list | `AbilitySystem/Tasks/Targeting/Authored/AutoTarget*.cs`, `Utils/ProcessTaskUtil.cs`, `AbilitySystem/Ability/AbilityProxy/Core/AbilityDataPacket.cs` (`ResolveTarget`), `AbilitySystem/Tasks/Gameplay/Apply*EffectsAbilityTask.cs` | fixed |
| GAS-63 | M | 5 | An ability's stage list had no initializer, so an ability whose behaviour was made in code had none until it was saved and loaded, and adding a stage to it threw. Found by the authoring round trip, filling an ability The Forge made. Fixed: the list starts empty, as a load leaves it; an ability with no stage still activates and ends | `AbilitySystem/Ability/AbilityProxy/Core/AbilityBehaviour.cs` | fixed |
| GAS-64 | M | 6 | A scaler spread over its provider's levels "up to a level" (`ELevelConfig.Clamped`) ended its range at the lower of its Max Level and the level being read, as its tooltip said, so a level always sat at the range's end and every level above the first read the last value. ED-62's Reads row showed it, and the user chose to change it. Fixed: the range ends at the lower of the Max Level and the provider's last level, and a level past it reads the last value; the Reads row reads it the same way | `AbstractScaler.cs` (`GetEffectiveMaxLevel`), `ScalerFields.cs` | verified |
| GAS-P1 | M | 0 | `ActionQueue` LINQ allocations; snapshot built every frame (PF-006) | `ActionQueue.cs:41, 46, 156`, `FrameSummary.cs:340-351` | verified |
| GAS-P2 | M | 0 | Tag and FrameSummary allocations (PF-007) | `TagCache.cs:60`, `TagQuery.cs:89-136`, `FrameSummary.cs:306-308` | verified |
| GAS-P3 | M | 3 | Each application revalidates every container and rebuilds all tags (area application is quadratic). Measured: an instant application on an entity carrying 1,000 effects took 0.49 ms, 135 times one on an entity carrying 2. Now the requirements wait for one pass at the next tick, a shelved container's tags are counted as it lands, and re-application and removal find containers in an index by asset tag | `EffectSystemComponent.cs:312-348`, `GameplayAbilitySystem.cs:250-281` | verified |
| GAS-P4 | M | 3 | Recompute allocations; impacter lookups allocate every tick; string tags with no interning. Measured: a tick allocated 528 bytes before its commit, a recompute 136, a modifier refresh 288 and a same-root tag query 60. Now each is 0: a spec builds its lookups once, contained effects are walked in place, a recompute keeps its list, modifier entries are reused, and roots are compared in place. The built-in `Tags` are made once, not with two locks on every read. Tags hashed and compared as strings, with no interned ID, moved to GAS-P7 | `CachedAttributeValue.cs:175, 203, 348`, `GameplayEffectSpec.cs:174, 188`, `Tag.cs:266, 286-288` | verified |
| GAS-P5 | M | 3 | Ending effects is quadratic. `FinishedEffects` is a list, so marking a container finished searches it, the shelf's tick and the requirement pass search it for every container, and clearing takes each off the shelf with a linear remove. A cleanse or a mass expiry of n containers costs n². Found fixing GAS-P3. Measured: ending 3,000 at once cost 0.0057 ms each, five to nine times as much as ending 50. Now a set mirrors the finished list, and a cleaning pass takes its containers off the shelf and out of the index together, in one walk of each | `EffectSystemComponent.cs` (`MarkFinished`, `TickEffectShelf`, `HandleGameplayEffects`, `ClearFinishedEffects`) | verified |
| GAS-P6 | M | 3 | A commit allocates. Measured: a frame in which an effect ticked and committed allocated 328 bytes. A commit made a `ChangeValue` (a class), and a list of frozen impacters with a constant impacter for each; `TrackedImpact` kept a node for every impact; and `ForgeHelper.SignPolicy` built a params array and summed it through LINQ. A worker that adjusted the commit, as armor does, added a carrier to wrap the new delta in, and a sign check of its own. Found fixing GAS-P4, whose figure of about 1.4 KB was taken within one frame and counted the frame summary's lists growing. Now a commit rents its change and its frozen impacters and gives them back, `TrackedImpact` keeps totals, and a sign check takes one or two values: 0 bytes a frame, with armor or without. What a raw delta and `EnforceScaling` allocate moved to GAS-P8 | `AttributeSystemComponent.cs` (`CommitWithCarrier`, `FreezeImpacters`, `WrapDelta`), `TrackedImpact.cs`, `ForgeHelper.cs` (`SignPolicy`) | verified |
| GAS-P7 | L | 3 | Tags are hashed and compared as strings on every lookup, with no interned ID. A built-in tag's name is 64 characters. Measured: 10,000 lookups in an entity's tag cache took 0.21 ms for a 3-character name and 7.1 ms for a 2,050-character one, and 58 ms when the long tag was loaded with an asset, whose name, a string of its own, was compared in full. Moved from GAS-P4. Now each tag has an ID, given at run time as it's made or loaded and never serialized (Decision 22), and equality and hashing compare IDs: about 0.27 ms either way | `Tag.cs` (`Equals`, `GetHashCode`, `TagIds`) | verified |
| GAS-P8 | L | 3 | A raw delta allocates. Measured: a frame in which a raw delta landed, its carrier kept, allocated 356 bytes. Its commit wrapped the delta in a new `SourceAttributeImpact` with a list and a constant impacter, and the carrier made a new origin each time it was asked for one, three times a commit. `GenerateSimple` makes a carrier on every call. Found fixing GAS-P6. Now the delta's Adds are frozen in the commit's own impacters and a carrier keeps the origin it makes: 0 bytes a frame. `GenerateSimple` still makes a carrier per call, so a raw delta made every frame keeps one. What `EnforceScaling` and the lifesteal worker allocate moved to GAS-P9 | `AttributeSystemComponent.cs` (`CommitWithCarrier`, `WrapDelta`), `IAttributeImpactCarrier.cs` (`SourceAttributeImpact.GetOrigin`), `SourcedModifiedAttributeValue.cs` (`GenerateSimple`) | verified |
| GAS-P9 | L | 3 | The changes the system makes on someone's behalf allocate, once per change. Measured: a frame in which an attribute rescaled with its Base allocated 500 bytes, and one in which lifesteal healed 164 inline and 532 deferred. `EnforceScaling`'s rescale and `RelativeOperationContextImpactWorker`'s heal each made a carrier for the source, and a `ModifyAttributeAction` when deferred; the rescale made its impact-type list, and the worker's deferred half an iterator. Taking any action off the queue allocated 104 bytes, as the queue walked a SortedDictionary to find it. Split from GAS-P8. Now each carrier is kept for its source and attribute, in a table keyed weakly by source; the system's `ModifyAttributeAction`s go back to it once the queue has run them (Decision 21); the queue walks its buckets by index, and `EnqueueRange` reads a list by index: 0 bytes each | `CachedAttributeValue.cs` (`EnforceScaling`), `RelativeOperationContextImpactWorker.cs` (`CreateDeferredActions`, `PerformWork`), `ActionQueue.cs` (`DequeueNext`, `EnqueueRange`) | verified |
| GAS-P10 | M | 3 | A frame summary's listener got a new snapshot each frame, which copied the frame's ten lists: 504 bytes a frame for an entity whose summary anyone listened to, 824 with an effect ticking, measured for Phase 3's exit gate. The listeners' snapshot is now the entity's own, refilled each frame, so a listener reads it during the call (Decision 26) | `FrameSummary.cs` (`TakeSnapshot`, `FrameSummarySnapshot`), `GASCore.cs` (`EndOfFrame`) | verified |
| GAS-S1 | M | 3 | The GAS twins duplicate about 400 lines and have drifted; `ToGASObject()` returns null on Runtime. Both now forward to one `GASCore`. The drift found: a runtime GAS applied no affiliation tags and lost its Entity asset's Local Data, and the GAS Debugger, asking `ToGASObject()`, named every runtime entity "unknown". `ToGASObject()` stays null on Runtime, which has no MonoBehaviour. What a hub keeps on purpose: the Mono hub builds at registration and a runtime GAS in its constructor, and on death the Mono hub ends at once while a runtime GAS queues its end | `GameplayAbilitySystem*.cs`, `RuntimeGameplayAbilitySystem*.cs`, `GASCore.cs` | verified |

### Editor and UI

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| ED-1 | M | 0 | Three compiler warnings: CS0162, CS8509, CS8321 (PF-009) | `GameplayEffectDrawer.cs:159-176`, `AbilityEditor.cs:138-143`, `TheForge.ViewTab.cs:1640` | verified |
| ED-2 | H | 4 | The effect inspector never shows delivery and still draws `ReverseImpactOnRemoval`. Now the impact says its delivery, which the duration gives it, and how often a periodic one commits, as in "7 over 6 s, level 1", and Reverse On Removal, which nothing reads, is gone from the inspector | `GameplayEffectImpactDrawer.cs` | verified |
| ED-3 | H | 4 | There is no `AttributeImpactEntry` drawer, so multi-entry effects cannot be authored. Now an effect's Additional Impacts are authored entries, each with its own attribute, operation, magnitude and delivery, which `AttributeImpactEntryDrawer` edits and the runtime appends after the derived one; the Cost rule checks each attribute a cost spends | `GameplayEffectImpact.cs` (`RebuildEntries`), `AttributeImpactEntryDrawer.cs`, `CommonValidations.cs` | verified |
| ED-4 | H | 4 | GAS Debugger: rebuilds every 0.25 s, `FindObjectsByType` every tick, Mono only, reflection on `TagCache`, raw cooldown magnitude, unlabelled badges. Fixed: one Debugger (Q4), built from the design canvas's boards (version 21), lists each hub ProcessControl registers, of either kind, following its join and leave callbacks (Q28); a sample changes the detail's values in place, at the rate the window menu sets; it reads no private field; an entity's header says what it is, and its attributes, effects, abilities and tags sit two by two under titled columns; an ability shows its state, as Stage 2/3, and its cooldown's time left; the Tags view shows every tag held, as a tree, and who holds the one selected; Follow Selection shows the entity selected in the scene; a status bar counts the processes and entities; it listens to the entity it shows alone; outside Play mode it offers Play Mode | `Editor/Debugger/RuntimeDebugger.cs`, `RuntimeDebugger.Entities.cs`, `RuntimeDebugger.Tags.cs` | verified |
| ED-5 | M | 4 | ProcessControlDebugger: usage is not CPU time; the Terminated filter is dead; Pause misleads; per-row style allocations. Fixed: the Debugger's Processes view shows each process's time a step over the last second, and its longest, which ProcessControl times while the debugger reads the game (`ProcessControlBlock.MeasureSteps`); its columns sort; its filters are Running and Waiting; Pause and Resume take the processes it handles alike, and Pause is offered for any running process; its Run says whether a paused run is held (`RunIsHeld`); it's UI Toolkit, with no style set in code | `RuntimeDebugger.Processes.cs`, `ProcessControlBlock.cs` | verified |
| ED-6 | M | 2 | Settings: an inverted range check; Reset All goes to the wrong tab and is partial; Auto Refresh is never read | `TheForge.SettingsTab.cs:233-236, 264, 346, 1770-1785` | verified |
| ED-7 | M | 2 | Numeric columns sort as strings | `TheForge.ViewTab.cs:656` | verified |
| ED-8 | H | 2 | Hard delete with no reference check | `TheForge.ViewTab.cs:2552`, `AssetBrowserWindow.cs:285` | verified |
| ED-9 | M | 4 | The Visualizer is blank for items and sets, and after a domain reload. Fixed: the Visualizer is gone; The Forge's detail pane summarizes every type, from its inspector's section summaries, and the window keeps its selection across a reload (Q6) | `TheForge.cs` (`FillSummary`) | verified |
| ED-10 | M | 2 | Analysis exposes "Not Implemented" groups and truncates silently | `TheForge.AnalysisTab.cs:362, 716, 1461, 1979-1991` | verified |
| ED-11 | M | 4 | The sticky-header workaround and drawer timers poll forever, among them two 250 ms timers in each impact drawer. Now nothing polls: the drawers follow the properties they show, the object picker's listener runs only while a picker is open, and the inspector keeps no scroll view of its own, so nothing measures the window; the header follows the Inspector's scroll events (ED-36) | `BasePlayForgeEditor.cs` (`OnAttachedToPanel`, `ConfigureScrollView`, `OnEditorUpdate`) | verified |
| ED-12 | H | 4 | No Undo, multi-edit or prefab overrides. The inspectors wrote 42 edits straight into their asset, and only the 5 renames recorded Undo; no inspector took a multiple selection; six drawers copy their SerializedObject for one target, and two write while drawing. Fixed: Phase 2's part (a rename stores the tag in the same undoable step, and showing never writes), the Attribute inspector (its fields bind, it takes a multiple selection, and every inspector's header follows Undo), the MonoProcess inspector's multiple selection, the Effect, Item and Attribute Set inspectors' Description and Link Mode, the Entity inspector's fields, the Ability inspector's Description, Activation Policy, Activate Immediately and Link Mode, Link, Unlink and template assignment in every inspector, the drawers that rebuilt on a one-target copy, the type drawers that wrote a default while drawing, and every asset inspector's multiple selection; the magnitude field and the scaler drawer change each selected asset's own values | `EachAsset.cs`, `MagnitudeField.cs`, `ScalerFields.cs`, `EffectEditor.cs`, `EntityIdentityEditor.cs`, `AttributeSetEditor.cs`, `BasePlayForgeEditor.cs` (`IsMultiple`) | verified |
| ED-13 | M | 4 | Template propagation on save loads everything, opens a modal mid-save and records no Undo; the validator is empty. Fixed: templates work as prefabs do (Q23): an explicit Push to Its Children, as one Undo step, which keeps each child's overrides; an edit to a child's field records the override, which the inspector marks, and one Undo takes back both; Revert to Template. The save hook and its empty validator are gone | `ForgeTemplatePush.cs`, `BasePlayForgeEditor.cs` (`ShowOverrides`) | verified |
| ED-14 | H | 1 | The `GameplayEffectImpact` copy constructor drops affiliation and throws on null packets | `GameplayEffectImpact.cs:155-176` | verified |
| ED-15 | M | 4 | "Validate All" is noisy and capped at 15; "Find All References" is a dialog. Fixed: the Used By sections replace Find All References, and one validator (`ForgeValidator`, with a `ForgeValidationRule` per check) feeds the Problems view and each inspector header's count, in place of Validate All and the Audit (Q8). The Forge's problem glyph and counts come with its window (ED-27) | `ForgeValidator.cs`, `ForgeRules.cs`, `ForgeProblemsWindow.cs`, `ForgeAssetHeader.cs` | verified |
| ED-16 | M | 2 | 49 placeholder `docs.playforge.dev` links in six editors, some pointing at the wrong page, with slugs that vary (`level-source`, `levelsource`). The Asset Store strongly recommends offline docs (rule 2.3.a) | `AbilityEditor.cs:77, 116`, `AttributeSetEditor.cs:818`, `AttributeEditor.cs:183`, `EntityIdentityEditor.cs:158` | verified |
| ED-17 | M | 2 | Stray `Debug.Log` calls | `GameplayEffectImpactDrawer.cs:33`, `AssetBrowserWindow.cs:348`, `AbstractRefDrawer.cs:81`, `TagDrawer.cs:745`, `TheForge.ViewTab.cs:2009` | verified |
| ED-18 | M | 4 | Collapse state lives in static dictionaries keyed by property path, so every asset shares it and a domain reload loses it. Each drawer now keeps it per asset for the editor session, in SessionState; a [SerializeReference] field's state follows the object it holds, and deleting a stage moves the later stages' states up with them | `AbilityTaskBehaviourStageDrawer.cs:31`, `AbilityBehaviourDrawer.cs:18-20` | verified |
| ED-19 | M | 2 | Naming drift: The Forge or PlayForge Manager, and no window's title matches its menu item. The top-level `PlayForge/` menu is not among the placements the Asset Store's rule 2.5.1.a describes: an existing menu such as `Window/`, or else `Tools/`. The docs follow whatever wins | `TheForge.cs:16-28, 88`, `GASDebugger.cs:134`, `GETTING_STARTED.md` | verified |
| ED-20 | H | 4 | No theming: inline styles, hard-coded dark backgrounds, emoji icons, dead UXML and USS. Now one style sheet, `Editor/UI/PlayForge.uss`, holds every value `Docs/EDITOR_STYLE.md` lists, in a Dark and a Light set, and a kit of controls reads it: the asset header, the section, the glyphs and the type icons. Every asset inspector's header and sections use the kit; The Forge and the GAS Debugger take the theme; no editor text shows an emoji; the dead style sheet and layouts are gone. Each surface's own inline styles go with its redesign, Phase 4's fifth step | `TheForge.cs:183`, `GASDebugger.cs:108`, `ForgeDrawerStyles.cs:111-131` | verified |
| ED-21 | M | 4 | Settings live in EditorPrefs and are machine-wide. Fixed: the shared settings (new assets' folders and file names, each type's defaults, the Cost and Cooldown effects) live in `ProjectSettings/PlayForgeSettings.asset`, a `ScriptableSingleton` on Project Settings → PlayForge, and the view toggles on Preferences → PlayForge (Q21). A project takes its EditorPrefs values on first use; The Forge's Settings tab and `TemplateRegistry` are gone | `PlayForgeSettings.cs`, `PlayForgePreferences.cs`, `PlayForgeSettingsPages.cs` | verified |
| ED-22 | H | 2 | A second project wipes the first project's templates: `TemplateRegistry` keeps one EditorPrefs JSON for every project on the machine, and `Save()` drops each template whose GUID doesn't resolve in the current project | `TemplateRegistry.cs:110, 164-177` | verified |
| ED-23 | M | 2 | An Item's Local Data can't be edited: Items inherit `LocalData` and the five other asset editors draw it, but `ItemEditor` builds no section for it | `ItemEditor.cs:71-80`, `BaseForgeAsset.cs:11` | verified |
| ED-24 | M | 2 | `ParticipateInSiblingCascade` can't be set: it is `[HideInInspector]` and `AbstractMonoProcessEditor` never draws it, yet ProcessControl reads it | `AbstractMonoProcess.cs:24-26`, `ProcessControl.cs:926, 986` | verified |
| ED-25 | M | 2 | The tag display settings barely work: "Display full tag path" changes nothing, since `GetDisplayText` returns before the code that reads it; "Group tags by root" shows off in Settings but defaults to on in `TagDrawer`; and the View tab reads neither | `TagDrawer.cs:178, 181-198`, `TheForge.SettingsTab.cs:201` | verified |
| ED-26 | M | 2 | The GAS Debugger never removes its three frame-summary hooks, so a window closed in play mode keeps sampling every entity, and reopening it adds another set | `GASDebugger.cs:149-152, 269-305` | verified |
| ED-27 | M | 4 | The View tab has two click models: one click selects and pings a row in the grouped list, but a typed table acts only on a double-click. Fixed: The Forge is one list and detail (Q5), where a click selects a row and shows its asset, and a double click shows it in the Project window | `TheForge.cs` | verified |
| ED-28 | L | 2 | Dead UI: `QuickFillWindow` is never opened, `m_AbilityIcon` is never assigned, and four editor `.cs.meta` files name UXML GUIDs that exist nowhere | `ScalerDrawerWindows.cs:356-372`, `AbilityEditor.cs:19-20, 70`, `AbilityEditor.cs.meta:7` | verified |
| ED-29 | L | 2 | The Entity inspector's Allow Duplicate Equipped Items toggle opens showing Allow Duplicate Items' value | `EntityIdentityEditor.cs:927` | verified |
| ED-30 | M | 2 | Local Data's object rows edit each other's value: a ScriptableObject row binds the MonoBehaviour field and the other way round, so neither keeps what's assigned | `DataWrapperDrawer.cs:127-128` | verified |
| ED-31 | L | 4 | The four unlink dialogs count with `IsLinkedTo(this)`, the editor rather than the asset, so they always say 0. Each now counts the children its unlink would change | `AbilityEditor.cs:840`, `EffectEditor.cs:279`, `EntityIdentityEditor.cs:301`, `ItemEditor.cs:311` | verified |
| ED-32 | H | 4 | Six import windows behave differently. Section import copies every field with no choice and Clear asks in a modal; the Effect's Duration and Impact sections couldn't import at all, since their Import button only logged a warning (they open the section window since ED-37); scaler import ignores the kinds a field allows and caches its list for 120 s. One Import window replaces them (Decision 28): choose a source, choose the fields, import as one Undo step. Fixed: `ForgeImportWindow` serves every section, a whole asset, worker groups and requirement groups, in place of five windows, and Clear asks nothing; a scaler's Import Values compares a Scaler asset's or another field's values with the field's before it copies them | `ForgeImportWindow.cs`, `ScalerWindows.cs` (`ScalerValuesWindow`) | verified |
| ED-33 | M | 4 | Scalers can't be shared. Every scaler is copied into its field, and the Scaler Template asset's link code has no callers, so reuse means copying and then propagating. Shared Scaler assets that fields reference replace it (Decision 28). Fixed: the Scaler Template is the Scaler asset, a field of the new Shared kind reads one (an attribute set's element through the cached Shared), its link code is gone, and levels map exactly unless a scaler stretches; a magnitude field reads as the canvas's scaler board draws it, with its field menu (Save as Scaler Asset, Use a Scaler Asset, Import Values, Quick Fill, Make Constant), its per-level table and curve, and the asset's own inspector lists the fields that share it | `MagnitudeField.cs`, `ScalerFields.cs`, `ScalerWindows.cs`, `ScalerAsset.cs` | verified |
| ED-34 | H | 4 | Opening a scaler writes to it while drawing. On an effect with no level link, Lock mode cuts its per-level table to one value; the other modes resize the table to Max Level, a Max Level of 0 or less becomes 10, and building the drawer clones a scaler that shares its reference id. Now nothing is written while drawing: a table keeps its length until the user changes it, and a shared scaler shows a warning with a button that makes a separate copy | `ScalerDrawer.cs:125-180, 285, 489-529` | verified |
| ED-36 | M | 4 | The asset header scrolls away with the content. It stays at the top of the inspector while the content scrolls under it (Decision 29), following the Inspector's scroll view by its events, so nothing polls (ED-11) | `BasePlayForgeEditor.cs` (`CreateInspectorGUI`), `ForgeAssetHeader.cs` (`StickTo`) | verified |
| ED-35 | M | 4 | Whole-asset import copies the template and level-link fields (`_template`, `_linkedSource`) and doesn't skip the nested `Definition.Name`. It copied each top-level field whole, so the asset tag came along in Tags too. The Import window's whole-asset import keeps the name, description, icons, asset tag, template and level link | `ForgeImportWindow.cs` (`KeepsOwn`) | verified |
| ED-37 | M | 4 | The asset inspectors don't follow `Docs/EDITOR_STYLE.md`: their sections stray from its order, Level Source sits apart from Leveling, a collapsed section shows nothing, sentences explain the fields, the link state sits in tinted boxes with colored borders, and many sections' Import and Clear only log a warning. Each inspector is redone in Phase 4's fifth step. Fixed: the Attribute (`ed6295a`, with ED-15), Item (`cf70e8f`), Entity (`a06a4fb`), Attribute Set (`86ead05`), Ability (`031c2d4`) and Effect inspectors: the style doc's order, Leveling with the level link and its provider in Unity's object field, the asset tag under the name, a summary on each section, Import and Clear on each, Used By, and no look set in code; the Entity mockup's attribute table, in the Entity and Attribute Set inspectors; the mockups' When Busy, in the Ability and Entity inspectors; and the Effect mockup's requirements table, in the Effect and Ability inspectors | `EffectEditor.cs`, `AbilityEditor.cs`, `ForgeRequirementsTable.cs` | verified |
| ED-38 | M | 4 | An asset reference isn't Unity's object field: `AbstractRefDrawer` draws a button that opens a dropdown list of its own, with hover colors and Clear and Open icon buttons set in C#, and setting an effect asks in a dialog whether to link it. Now every asset reference is Unity's object field with a plain button beside it: Open, which shows the asset in a window of its own, or New, which makes one where The Forge puts its type, named and tagged, and uses it (Q15). An effect field says on a line under it how the effect takes its levels, with Link; its template, a pick of unlinked effects and New Linked Effect are in its right-click menu (Q18) | `AbstractForgeObjectDrawer.cs`, `GameplayEffectDrawer.cs` | verified |
| ED-39 | M | 4 | Opening an attribute set element can change the set: 50 ms after it draws, and on every change to the set, `AttributeSetElementDrawer` clamps a Current or Base magnitude that sits outside the element's bounds and applies it to the asset, so looking at a set marks it modified. The runtime clamps the same values as the entity starts. Now the drawer writes nothing as it draws: a help box says how the runtime clamps such a magnitude, and only a value typed into a Magnitude field is kept within the bounds, as part of the same edit. The drawer follows the style sheet | `AttributeSetElementDrawer.cs` | verified |
| ED-40 | M | 4 | The debugger only watches: it can't change an attribute or a tag, apply an effect or grant an ability, so trying a change means writing code for it. Q27: editing attributes and tags first, then applying effects and granting abilities. Fixed: each section of the Debugger's Entities view ends in a foot row (Decision 31): the selected attribute's Base and Current fields, which the entity takes as they are; Remove for the selected effect, and an effect picked there lands from the entity itself; Activate and Interrupt for the selected ability, and one picked there is granted at a level; Remove for the selected loose tag, and a tag typed or picked from the project's is granted as a loose tag (FEAT-16). The status bar says what each did | `Editor/Debugger/RuntimeDebugger.Entities.cs` | verified |
| ED-41 | M | 5 | An ability's cost and cooldown apply only from a stage set to Apply Usage Effects, and nothing said so when no stage was: the Tactics lab's Slash had a cooldown that never came on. Fixed: the Ability inspector warns under Cooldown while the ability has a cost or a cooldown and no stage applies them, and the Problems view lists it (`UsageEffectsRule`) | `Editor/CustomPropertyDrawers/Forge/Editors/Assets/AbilityEditor.cs`, `Editor/Problems/ForgeRules.cs` | verified |
| ED-42 | M | 5 | A section's Clear set each field to its type's default, 0 or the first choice, not the field's own (`SectionClearUtility.ResetPropertyToDefault`). Clearing an effect's Impact set its affiliation to Use Affiliation List with none listed and its Chance to Apply to 0, so the effect never landed. Fixed: a cleared field takes a new asset's value, read from a new asset of the target's kind that no one sees, and a cleared list still empties. Impact's Import and Clear take the impact's own fields but its chance, which Requirements shows, imports and clears (FEAT-8) | `Editor/CustomPropertyDrawers/Forge/Editors/Windows/SectionClearUtility.cs` (`FreshOf`, `TakeFresh`), `Editor/CustomPropertyDrawers/Forge/Editors/Assets/EffectEditor.cs` | verified |
| ED-43 | M | 5 | The Debugger kept its wide layout at any width, so docked in a column its lists and details squeezed side by side and its tables clipped, against the user's rule that it read as clearly at 360 px as in a full window (EDITOR_STYLE's Debugger section, the canvas's three narrow boards). Found starting FEAT-12, whose Timeline view follows the rule too. Fixed: below 720 px each view's list sits above its detail, the toolbar's views take a row of their own with the search and the view's menus under them, the frame count moves to the status bar, whose counts drop their parts, the Processes list keeps ID, Process, State and Step, Held By drops Source, and Follow Selection keeps its icon | `Editor/Debugger/RuntimeDebugger.cs` (`Fit`), `Editor/Debugger/RuntimeDebugger.Tags.cs`, `Editor/UI/PlayForge.uss` | verified |
| ED-44 | M | 5 | A template's push found each child's own children by searching the project for assets of its type and loading every one, so a push searched the project once for each member of the family: a push to 2,000 children loaded 2,000 assets 2,000 times, 14 ms a child against 5 for 200, and took 28 s. `PropagateToAllChildren` searched twice for each. Found by the editor stress runs. Fixed: `ChildrenByTemplate` finds a type's assets by the template they name, in one search, which the push and the propagation walk, so a push costs about 2.9 ms a child however big the family | `Editor/Tools/ForgeTemplatePropogation/ForgeTemplatePush.cs`, `Editor/Tools/ForgeTemplatePropogation/ForgeTemplatePropagator.cs` (`ChildrenByTemplate`) | fixed |
| ED-45 | M | 6 | The Debugger's Entities view lost its list: back from another view, the list's column was collapsed against the window's left edge, and only dragging the splitter brought it back. A view hidden by display: none was laid out at 0 px, and its split squeezed the pane that keeps its width to nothing, and kept it so. In a narrow window, where each view's list keeps its height, each view did. And the Tags view's tree never showed: Unity's own style gives a tree view a flex basis of 0, which beat the width its split gave it. Fixed: the views, and the prompt outside Play mode, take turns in one place, each laid out at the place's size while another shows and only the one shown visible (`ForgeStack`). A view that shows again tells its splits their size, which a split ignores while it's hidden. The Debugger's lists take a flex basis of auto | `RuntimeDebugger.cs`, `Kit/ForgeStack.cs`, `PlayForge.uss` | verified |
| ED-46 | M | 6 | The Timeline added each new record at the bottom, so a long list hid the newest under the oldest. Fixed: the newest track is at the top, and a track's detail lists its latest run, or its latest application, first. A run keeps its number | `RuntimeDebugger.Timeline.cs` (`RefillTracks`, `ShowAbilityTrack`, `ShowEffectTrack`) | verified |
| ED-47 | M | 6 | Selecting an effect in the Timeline hid part of its Last 10 s bar, usually the span furthest left. The run or application the detail shows was drawn in the selection's color, on its track's row, which takes the selection's color too. An effect that stays on the shelf is one long span from its landing, which the strip clamps to its left edge, so that's the span that vanished. Fixed: that span stands taller than the others, in strong text, which reads on the selection and on the unfocused selection in both themes | `PlayForge.uss` (`pf-debugger__span--selected`) | verified |
| ED-48 | M | 6 | A column's width wasn't remembered. Switched away, a table was laid out at 0 px, which shrank its columns to their least, and they came back at their first widths; and a domain reload, as entering Play mode is, or the window's closing built them at their first widths again. Unity keeps a table's columns only for a table with a view data key, which brings back its selection and scroll too, behind the window's back. Fixed: the Debugger's views and The Forge's two lists keep their layout while hidden (ED-45), and each table keeps the widths its columns are dragged to, one preference a table (`ForgeColumnWidths`), which Use Defaults forgets: the Debugger's four lists, The Forge's two and the Problems view's | `Kit/ForgeColumnWidths.cs`, `RuntimeDebugger*.cs`, `TheForge.cs`, `ForgeProblemsWindow.cs`, `PlayForgePreferences.cs` | verified |
| ED-49 | M | 6 | The attribute set element drawer needed a rework, above all to show what each attribute starts at: from its magnitudes, its level and its constraints, with its set's sub-sets, and with the set it's a sub-set of. It showed a formula, as "10 + Level", Current and Base one at a time on a switch, and the attribute table read "Scaled" for any value a scaler sets; its Bounds and Constraints drew tinted boxes with colored borders and badges, in styles set in C#. Fixed: `AttributeStartValues` works the values out as the runtime does, a stand-in entity at each level whose attribute system is set up from the set. An element shows Starts At at each level, with a note when another element's value is used and a warning when a scaler reads an attribute the set doesn't give; Base and Current are magnitude fields, as an effect's are; Bounds, Constraints and a value's Current and Base are Unity's fields. Preview In works a set's values out in a set that holds it, and the table shows the same values. A reference field inside a list's element dropped its label, as the tag field did (ED-55), and resizing a spread scaler after an Undo crashed the editor through a property its field kept | `AttributeSetElementDrawer.cs`, `AttributeStartValues.cs`, `ForgeAttributeTable.cs`, `AttributeOverflowDataDrawer.cs`, `AttributeConstraintsDrawer.cs`, `AttributeValueDrawer.cs`, `MagnitudeField.cs`, `EachAsset.cs` | verified |
| ED-50 | M | 6 | An effect's Impact section felt broken. Much of it was the scaler drawer (ED-51), and the rest the snapshots showed: an additional impact's Retention Group drew as a bare bar with no label, as the tag drawer dropped its label for any field inside a list (ED-55); and an entry's row named no delivery, which EDITOR_STYLE puts on its right (Q19), nor said what it commits, as the first impact does. Fixed: an entry's row names its delivery, a periodic one says what it commits, and its Retention Group has its label | `AttributeImpactEntryDrawer.cs`, `TagDrawer.cs` | verified |
| ED-51 | H | 6 | The scaler drawer wasn't legible. Its fields ran on, unmarked, into the fields around it, and it drew what a kind's base class allowed, not what the kind reads: a Fixed Value, a Set By Caller, a Threshold and the stack kinds showed a table of values per level they never read, while a Randomized scaler, a Conditional with no True scaler and a Group with no members read a table the drawer hid. It drew a kind's own fields only down to the first abstract class, so a scaler of the user's own built on a base of its own lost that base's fields (the user's question), and a range below 0 read "-25—10". Fixed: a scaler's settings sit on one shaded, indented block under its field (Decision 36). Each kind says what it reads of every scaler's parts (`AbstractScaler.Parts`, `EScalerParts`), which the drawer draws and no others. A kind's own fields draw from every class below PlayForge's bases, as Unity draws their types, without those marked Hide in Inspector, and `[ForgeShowWhen]` shows one only for the settings it applies to. A range below 0 reads "-25 to -10" | `ScalerFields.cs`, `MagnitudeField.cs`, `ScalerDrawer.cs`, `AbstractScaler.cs` and each kind, `ForgeShowWhenAttribute.cs` | verified |
| ED-52 | M | 6 | The ⋮ menu beside a scaler field opened so narrow that none of its items could be read: anchored to its button, it took the button's 20 px. Fixed: it opens at the button, as wide as its widest item (`GenericDropdownMenu.DropDown`'s fit-content width) | `MagnitudeField.cs` | verified |
| ED-53 | M | 6 | A scaler's Levels count could be set to anything while its asset was linked to a level provider, whose levels the scaler must keep, and Quick Fill filled the values the scaler had. Fixed: Per Level, a scaler whose asset has a level provider, as an ability, or an effect linked to one, follows the provider's levels, as "Fireball's levels (1–4)", with a value for each. A level past its values shows the last, and typing there gives it its own. Own levels gives it a count again; an asset whose levels come from whatever uses it has the count alone. Quick Fill fills the levels the table shows | `ScalerFields.cs` (`LevelsOf`, `LevelsShown`), `QuickFillWizard.cs` | verified |
| ED-54 | M | 6 | What Stretch Over Levels did was hard to see: a box, with Level Range, Max Level and Between Levels under it while on, over a table that labelled each value with a level it might not sit at. Fixed: a scaler's Values are Per Level or Spread Over Levels, side by side (`ForgeSegmentedField`). Spread, each value says the level it sits at over the range, the range reads Over Levels with Points beside it, and the curve beside the table is the one the scaler reads, steps, straight or smooth | `ScalerFields.cs`, `ForgeSegmentedField.cs`, `ForgeCurvePreview.cs` | verified |
| ED-55 | M | 6 | The tag drawer didn't follow `Docs/EDITOR_STYLE.md`: a button with a colored left border by its context, styles set in code, text that changed as the pointer passed, a 120 px label it dropped for any field inside a list, and a picker drawn under the field in tinted boxes, with a set-as-parent button that showed only on hover. Fixed: a tag field is Unity's field (`ForgeTagField`), its label in the label column, the tag's parents in secondary text and its leaf in strong text, by its display name. It opens Unity's searchable dropdown of the tags its context offers (`ForgeTagPicker`, an `AdvancedDropdown`), with None and New Tag… (`ForgeNewTagWindow`); its right-click menu clears it, makes a new tag, makes its tag a child of another, or copies its path. A pick is one Undo step and keeps the tag registry's uses | `TagDrawer.cs`, `Kit/ForgeTagField.cs`, `Kit/ForgeTagPicker.cs`, `Kit/ForgeNewTagWindow.cs` | verified |
| ED-56 | M | 6 | The ability task drawer, and the base it shares with every field that holds an object of some kind (tasks, validation rules, targeting, effect behaviours, cues, workers, scaler behaviours, stage policies, instantiators), didn't follow the style: a button that opened a list drawn under it in tinted boxes, the kind's fields in a box with an orange left border, colored badges for a folded field, from styles set in C#. Every element of a list shares one drawer, which kept the last element's fields and kind in fields of its own. Fixed: a kind field is Unity's field (`ForgeTypeField`), which opens Unity's searchable dropdown of the kinds (`ForgeTypePicker`), with the kind's fields indented under it; the generic drawer folds as an attribute set element does, to a line of its values. Each field keeps its state in its own elements, and a pick makes one object for each selected asset, as one Undo step. `AbstractRefDrawer` is gone. The stage drawer is ED-64 | `AbstractTypeRefDrawer.cs`, `AbstractGenericDrawer.cs`, `AbilityTaskDrawer.cs` and the kinds' drawers | verified |
| ED-57 | M | 6 | The worker group, asset loader and other drawers older than Phase 4 didn't follow the style. A worker group was a box of its own, with a colored border, colored count badges, a type tag and an Import of its own, from 90 styles set in C#, inside a Workers section whose header already counted and imported it, and its lists' + copied the last worker, as Unity's does for objects held by reference. A sequence reference was an IMGUI popup that wrote "(None)" and searched every type of every assembly. Fixed: a worker group is its name and its four lists, Unity's reorderable lists, where a new worker starts empty (`ForgeLists.AddsEmpty`); a sequence reference is Unity's popup of the project's sequences, None first, by `TypeCache`, a method it names that's gone marked missing, and a pick one Undo step. The asset loader took ED-56's kind field. The other drawers older than Phase 4 went to ED-66 | `StandardWorkerGroupDrawer.cs`, `TaskSequenceReferenceDrawer.cs` | verified |
| ED-58 | M | 6 | The Forge's list showed each asset's file name beside its name, while Show File Names was on, its default. Fixed: the list shows the asset's name alone, and the selected asset's header shows its file name under Show in Project, its path on hover (`ForgeAssetHeader.AddLineUnder`). Show File Names is retired: the Preferences page has no switch for it, and Use Defaults clears it | `TheForge.cs`, `ForgeAssetHeader.cs`, `BasePlayForgeEditor.cs`, `PlayForgePreferences.cs`, `PlayForgeSettingsPages.cs` | verified |
| ED-59 | M | 6 | The Forge's tag view couldn't sort its columns; it listed its tags by their text alone. And the asset list's sort, which the window keeps through a domain reload, didn't show in its header once the window was built again. Fixed: the tag view's Tag, For and Uses columns sort, either way, each in natural order, Uses as a number (`TheForge.SortedTags`), and the window keeps the tag view's sort as it keeps the asset list's. Each list's header shows the window's sort as it's built | `TheForge.cs` | verified |
| ED-60 | M | 6 | Tags showed their internal names. A tag made unique, as the tag picker makes each new one, pads its name to 64 characters with a generated suffix, and its display name keeps the path as typed; The Forge, the Debugger and the inspectors' summaries showed the padded name, and the Debugger's Tags view split a unique tag from its parents. Fixed: each shows a tag by its display name (`TagDisplaySettings.PathOf`): The Forge's tag view, its search, a tag's detail (the padded name on hover) and the Asset Tag column; the Debugger's Tags view and an entity's Tags table; the asset header, the Tags summaries, the requirements table and the Import window. The Debugger's Add Tag finds the project's tag a display name names | `TheForge.cs`, `RuntimeDebugger.Tags.cs`, `RuntimeDebugger.Entities.cs`, `BasePlayForgeEditor.cs`, `PlayForgePreferences.cs` and others | verified |
| ED-61 | M | 6 | Many custom inspectors and windows older than Phase 4 don't follow the style, as the Mono Process inspector | `AbstractMonoProcessEditor.cs` and others | open |
| ED-62 | M | 6 | A spread scaler's table named each point by the level it sat at, as 1.8 or 2.5, which no entity can be, and never said what a real level reads (the user's report). Fixed: the points are numbered, and under them each real level shows what it reads, as the runtime reads it: the stored curve at the level's place between the starting level and the last. A stored curve that reads other than its points draw says so | `ScalerFields.cs` | verified |
| ED-63 | M | 6 | Quick Fill drew a look of its own, a purple bold title, a green Apply, 10 px hints and tinted boxes, from styles set in C#, with its fields outside the label column, and wrote through a property it kept from when it opened (the user's report: its UI needs a rework, and maybe more content). Fixed: it's a dialog in the style, with what it fills, a pattern and its values, rounding, and a preview as the scaler's table shows it, with its curve. It adds Ease In and Out, a Custom Curve, a strength for its two bending patterns, rounding to whole numbers or a step, and a count for a scaler with levels or points of its own. Apply writes the values and the curve as one Undo step | `QuickFillWizard.cs` | verified |
| ED-64 | M | 6 | Split from ED-56: the ability stage drawer didn't follow the style. It drew a colored badge for the stage's number, its policy, its usage effects, its task count and a critical task, ▶ and ▼ as text, a delete button, and a list of its own with ▲, ▼ and × buttons, from 163 styles set in C#, and grew that list as an array, which gave a new task the last one's object. A stage's condition was Unity's foldout, its subject and When Missing showing with no tag. Fixed: a stage folds as an attribute set element does, named by its place, Critical on its right, to a line of what it holds; its tasks are Unity's reorderable list, where a new task starts empty (`ForgeLists.AddsEmpty`); At Stage Start indents under Apply Usage Effects; and a condition's subject and When Missing show once it names a tag (`AbilityStageConditionDrawer`). The stages' list removes a stage, and as it removes or moves one, each stage keeps its fold (`ForgeLists.KeepsStates`, ED-18) | `AbilityTaskBehaviourStageDrawer.cs`, `AbilityStageConditionDrawer.cs`, `ForgeLists.cs`, `DrawerCollapseState.cs` | verified |
| ED-65 | M | 6 | In The Forge, the asset header's file name sat under Show in Project, so that button stood two lines tall in the header's row, and the problem count, Help and the menu sat level with its middle (the user's report on ED-58). Fixed: the actions share one row, and the file name has a row of its own under them, on the right | `ForgeAssetHeader.cs`, `BasePlayForgeEditor.cs` | verified |
| ED-66 | M | 6 | Split from ED-57: the drawers older than Phase 4 that still set their colors, borders or fonts in C#: the ability behaviour, level provider link, tag requirements, tag query, effect reference and effect impact drawers, and the scaler's threshold and group member drawers | `AbilityBehaviourDrawer.cs`, `LevelProviderDrawer.cs`, `TagRequirementDrawers.cs`, `TagQueryDrawer.cs`, `GameplayEffectDrawer.cs`, `GameplayEffectImpactDrawer.cs`, `ScalerSupportDrawers.cs` | open |

The design questions behind Phase 4's rows, each with its evidence, are in the UI interview doc of 2026-11-03,
https://claude.ai/artifact/BbnGybqgX3spJsT6HoTafp. Its answers (Decision 18) settle ED-8, ED-16, ED-19 and ED-12's
Phase 2 part, and set Phase 4's direction.

**Phase 4's order,** from Q35, Q36 and Q3:
1. ED-12, Undo and multi-editing, one inspector at a time; with its drawer part, ED-34, so opening a scaler writes
   nothing (done).
2. No polling: ED-11 and ED-18.
3. The smoke suite, the phase's gate: in each theme, every window opens without an exception and scripted edits undo.
   Done: `EditorSmokeTests` in Proving Grounds, which its README describes. It runs in the EditMode gate.
4. ED-20: one style sheet and a kit of controls, in the style `Docs/EDITOR_STYLE.md` records (Decision 20).
5. The redesign, one surface at a time: the asset inspectors, with ED-2, ED-3, and the Import window and shared
   scalers of Decision 28 (ED-32, ED-33); The Forge, with ED-13, ED-15,
   ED-21, ED-27 and ED-31 (done); one runtime debugger for the GAS, Process and Tag views (Q4), with ED-4 and ED-5,
   then its edits (Q27, ED-40) (done); and
   the Visualizer, with ED-9 (done: The Forge's detail pane summarizes each asset, Q6).
   The asset inspectors go one at a time, each following the style doc (ED-37): the Attribute, Item, Entity and
   Attribute Set inspectors first (done), then the reference fields (ED-38, done), the Ability inspector (done), then
   the Effect inspector with ED-2 and ED-3 (done), then the Import window and shared scalers (ED-32, ED-33, done),
   which freed ED-12's last part (done).

**Phase 6's order,** from the user's list (Decision 36):
1. The Debugger and The Forge: ED-45 to ED-48, and ED-58 to ED-60.
2. The scaler drawer, ED-51 to ED-54, then the Impact section around it, ED-50.
3. The attribute set element drawer, ED-49.
4. The drawers and inspectors older than Phase 4: ED-55, ED-56, ED-57 and ED-61, with ED-64 split from ED-56 and
   ED-66 from ED-57.

The user's reports along the way, ED-62, ED-63, ED-65, GAS-64 and FEAT-25, went in as they came. FEAT-24 waits for
the user's answer (Open decision 20). Phase 7, the demo refresh (Decision 37), follows; the smoke suite's edit sweep
reads the old demos' fullest assets, so it moves to the new demo's as the old go.

### Samples and packaging

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| SP-1 | C | 2 | 39 sample assets serialize framework types as `Assembly-CSharp`, with no `[MovedFrom]`; add a missing-type guard test | `Samples/Data/*`, `Samples/Demo/Data/*` | verified |
| SP-2 | C | 2 | 621 missing-script components in DemoScene; `TestSequences.cs` is two lines | `Samples/Demo/Scenes/DemoScene.unity` | verified |
| SP-3 | H | 2 | The Demo hard-depends on the Input System but its scene has a legacy input module | `DemoInputConduit.cs:5` | verified |
| SP-4 | H | 2 | Samples reference assets outside the package (materials, PanelSettings, `demo_uss.uss`) | `Assets/UI Toolkit/`, `Assets/demo_uss.uss` | verified |
| SP-5 | C | 2 | QuickOutline is "All rights reserved" and missing from the notices | `Samples/QuickOutline/Scripts/Outline.cs:1-7` | verified |
| SP-6 | H | 2 | Swarm Defender depends on the Demo; `TestIntEffect` leftovers | `SwarmDefenderGameManager.cs:5, 32-37` | verified |
| SP-7 | M | 2 | Lorem and "ASda" descriptions; an `EFECT_` typo; no sample README | `Samples/Data` | verified |
| SP-8 | H | 2 | UniTask not embedded and not pinned to a revision, so projects drift (PlayForge 2.5.10, Proving Grounds 2.5.11); the Input System dependency is undeclared | `Packages/manifest.json`, `PUBLISHING.md:14-29` | verified |
| SP-9 | M | 2 | Stray files: the stale `PlayForge_1.0.0.unitypackage`, `asda.unity`, `PlayForgeTesting.cs`, tracked `UIElementsSchema` and `.idea` | repo root and `Assets/` | verified |
| SP-10 | M | 2 | `.claude/worktrees` (1.1 GB) is not in `.gitignore` | `.gitignore` | verified |
| SP-11 | H | 2 | Examples and test processes compile into Runtime | `TaskSequenceExamples*.cs`, `SyncSequenceExamples.cs`, `GroupProcessExamples.cs`, `TestAOEMonoProcess.cs`, `TestMultiProcess.cs` | verified |
| SP-12 | M | 2 | BuildVerification builds an arbitrary scene | `Editor/Tools/BuildVerification.cs:27-33` | verified |
| SP-13 | C | 2 | Licence: MIT versus the Asset Store EULA (Decisions 6) | `LICENSE` | verified |
| SP-14 | M | 2 | No version or changelog; `productName: GAS` | `ProjectSettings` | verified |
| SP-15 | M | 2 | Stale docs: PUBLISHING claims 12/12 and names `Samples/InputHandling`; wrong menu paths | `PUBLISHING.md:11, 56, 68` | verified |
| SP-16 | H | 0 | S6, S8 and S11 are masked as target semantics. The runner move is now SP-18 | `PlayForgeGoldenTests.cs:55, 61, 64` | verified |
| SP-17 | M | 2 | 842 MB and 206 MB of material packs tracked without LFS (Decisions 7) | repo | verified |
| SP-18 | M | 2 | Split from SP-16: the custom golden runner should move to the Unity Test Framework (B04), so `unity test` and CI report pass/fail | `PlayForgeGoldenTests.cs` | verified |
| SP-19 | C | 2 | Unused `using log4net.Appender` and `using Codice.CM.SEIDInfo` compiled only through the DLLs in `com.unity.collab-proxy`, so the Editor assembly failed in any project without that package | `AbstractRefDrawer.cs:5`, `ForgeDrawerStyles.cs:2` | verified |
| SP-20 | H | 2 | `Samples/` is neither `Samples~` nor covered by an asmdef, so as a package its 15 scripts never compile, Unity warns about each one on every compile, and the samples can't run | `Samples/` | verified |
| SP-21 | M | 2 | Editor UXML and USS break on a clean import: 4 `TypeLoadException`s from stale `…, Assembly-CSharp` type names (`FESGameplayAbilitySystem.GameRoot` and `.ProcessControl`, the removed `FrameworkIndex`, and `EAbilityActivationPolicyExtended`, which moved assembly), 9 invalid paths to assets outside the package (`Editor/Icons/Creator/ability.png`, `Assets/TextMesh Pro/…`), and an unknown `:last-child` pseudo class. Nothing references `forge_abilityDrawer.uxml`; the other two are default references in script `.meta` files, so check them against ED-20's dead UXML and USS | `fesgas_bootstrapper.uxml`, `forge_abilityDrawer.uxml`, `AbilityInspector.uss:52` | verified |
| SP-22 | M | 2 | Untested with domain reloading off (Enter Play Mode Options). The Asset Store requires every package to work that way starting with Editor 6.6 (rule 2.5.h), so the static state in ProcessControl, the registries and the caches needs a check. Decision 17 keeps it in Phase 2 | — | verified |
| SP-23 | M | 2 | The Demos are Built-in RP only: all ten of their materials use the Standard shader, which renders magenta under URP. The Asset Store requires URP or HDRP support of new assets and updates submitted from Editor 6.5 or newer (rule 1.3.c). Decision 17 keeps it in Phase 2, though a 6.0 submission isn't bound by the rule | `Samples~/Demos/**/*.mat`, `PUBLISHING.md` | verified |
| SP-24 | M | — | No CI, so Phase 3 closed on its local gates, not the CI its exit gate asked for (Decision 27). CI needed tests it can reach and a Unity license stored as GitHub secrets. Done: Proving Grounds is public on GitHub, and its `Gates` workflow runs the full gates on Ubuntu, with the user's license in its secrets (Decision 35). Its first green run passed PlayMode in three orders, the planted failures and EditMode | Proving Grounds' `.github/workflows/gates.yml`; `.github/workflows/proving-grounds.yml` | verified |

### Missing features (Phase 5)

| ID | Feature | Notes |
|---|---|---|
| FEAT-1 | Gameplay cues | Hooks for effects, sounds and camera on apply, tick and remove. Done (Decision 30): `GameplayEffect.Cues`, a list of `AbstractCueHandler`, whose virtual hooks are bursts (On Applied, On Ticked, On Removed) and a lasting cue's Begin and End; the shelf plays them (`EffectSystemComponent.Cue`), and a restore ends and begins the lasting ones and plays no burst; `CameraShakeCue` goes through the `ICameraShaker` the game gives `ForgeCues.Shaker`, with `SoundCue` and `PrefabCue` beside it; the Effect inspector's Cues section, an effect's own after Impact (EDITOR_STYLE's Q9 order), edits them |
| FEAT-2 | SetByCaller magnitudes | Scalers read the data packet (C01). Done (Decision 30): `SetByCaller(tag, magnitude)` on a `GameplayEffectSpec` or a `ProcessDataPacket`; `SetByCallerScaler` reads it, or its Default; each spec an ability's data makes takes its magnitudes as it's made (Apply Effects, Apply Remove Effects, the task helpers, the cost and cooldown), a contained or overflow effect takes its container's, and a save keeps them. Found and driven by the ability crafting scenario |
| FEAT-3 | Tag-change events | Replace tag-worker polling. Done (Decision 30): `TagCache.Listen` hears a tag and the tags under it come and go, at once, or every weight change; `TagChanged` hears every change; a rebuild tells only what it changed; a tag worker is evaluated when the tags change, unless it asks for every frame (`EvaluateEveryFrame`) |
| FEAT-4 | Ability triggers | Activate on a gameplay event or when a tag is added. Done (Decision 30): `AbilityDefinition.Triggers`, each an `AbilityTrigger` of a source (Gameplay Event, Tag Added) and a tag; `SendGameplayEvent(tag, data)` on the ability system and the hubs activates each known ability an event under the trigger's tag sets off, with the event's data; the ability system hears a tag gained and fires its triggers at once, one at a time, so a triggered ability that grants tags doesn't nest; `OnGameplayEvent` hears each event; the Ability inspector edits them. Driven by the beyond combat scenario |
| FEAT-5 | Cancel and block abilities by tag | C11. Done (Decision 30): an ability's Cancelled By names the abilities that cancel it as they activate, and its Blocks those that can't activate while it runs, by asset or context tag; each stage can name its own, and one that names none takes the ability's; a critical stage can't be cancelled; the While active row of its tag requirements interrupts it as soon as its caster's tags fail it |
| FEAT-6 | Input buffer | Expiry and latest-wins for queued activations (C06). Done (Decision 30): the Queue policy is the input buffer, one slot per ability that a new press refreshes; a request waits out its ability's Buffer Window (0.3 s by default, or its own) in the caster's time, for the claim to go or a block to lift; the highest Buffer Priority fires first, then the newest |
| FEAT-7 | Time source | Per-entity time scale and deterministic fixed tick (C02). The clock per entity is done (Decision 30): `GetClock()`, the game's time by `ForgeTime` times the entity's `TimeScale`, which its effects, cooldowns, sequences, waits and projectiles run by. The fixed-rate tick is done too: with ProcessControl's Fixed Tick Rate set, each frame runs as many ticks as its time holds, each stepping every process and its end of frame by a fixed step, and each sequence's wait goes on by the tick (`ForgeTick.Next`) |
| FEAT-8 | Effect conveniences | Cleanse by tag query, immunity, overflow at max stacks, abilities granted by effects, chance to apply. Done. Cleanse and immunity (Removes Effects Tagged, Blocks Effects Tagged) match an effect's asset, context and granted tags (Decision 30); overflow at max stacks is on Stack Bounds (Overflow Effects, Deny Refresh On Overflow, Clear Stacks On Overflow); an effect's Granted Abilities are lent at its origin's level while it's on; an effect lands by its Chance to Apply, drawn from `ForgeRandom`. The Effect inspector edits Chance to Apply in Requirements, above its tables, as it gates the whole application, with the section's summary saying a chance short of 1, and the Tags summary counts the abilities granted, as in "Grants Status.Burning, 1 ability" |
| FEAT-9 | Save and load | Snapshots of attributes, effects, tags and cooldowns. Done (Decision 30): `ForgeSave.Capture` takes an entity at rest into a `ForgeSnapshot` of plain serializable types: each attribute's permanent commits by retention group, its levels, the abilities it knows of its own by slot, its items with their stacks, worn or not, its effects with their origin, stacks and clock, its loose tags and time scale. A `ForgeAssetCatalog` names the assets by IDs that last, by GUID from its editor command. `ForgeSave.Restore` interrupts running abilities, lets go of what the entity has with nothing told, puts back the save's, compiles the tags once and fires `OnRestored`. An `IForgeEntityIds` names the other entities whose effects are on it |
| FEAT-10 | Headless mode | Abilities run without a scene ProcessControl. Done (Decision 30), in six steps the Log lists. Step 1 done: a process reaches the control that holds it, `ProcessControlBlock.Control`, which its relay (`ProcessRelay.Control`), its state changes and unregistering, a watch on it, a runtime process's ending of what it handles, a group's watch and Run Process go through, not `ProcessControl.Instance`. Step 2 done: `ProcessWorld`, a plain class, holds the bookkeeping (the process tables, step groups, watches, groups, GAS registry, callbacks, the control state and its transitions, registration and stepping); ProcessControl wraps one (`ProcessControl.World`), steps it from Unity's loop and forwards its API; `new ProcessWorld()` makes one ready to take processes, `ProcessRegistration.In(world)` registers into it and `world.Step()` steps it; a block and a relay hold their world (`ProcessControlBlock.World`, `ProcessRelay.World`). Step 3 done: a world's own tick and clock: `ForgeTickSource`, a tick, of which the scene has one (`ForgeTick.Scene`, which ProcessControl's rate turns on) and each world of its own its own (`ProcessWorld.Tick`); `ForgeTick` reads the tick in force (`ForgeTick.Current`), a world of its own's while it steps or moves a process; `world.Step(deltaTime)` puts its clock in, ticks and steps, so a loop of steps runs its sequences' waits within one frame. Step 4 done: abilities in their caster's world: `ProcessWorld.Of(process)` finds the world that holds a process; an ability's targeting and its sequence, Run Process, Run Sequence and Create Mono Process register there, and activation asks that world whether it takes new processes; a fallen entity ends through its own relay, where it ended whatever the scene's control held under its cache index; the keep-alives wait on `ForgeTick.Next`, so a world of its own's processes end by its steps. Step 5 done: `ForgeTick.WaitWhile` and `WaitUntil` ask once a tick where the tick in force ticks, and are UniTask's own waits where it doesn't; the sequence runtime's pause hold and its maintained stages, a chain's pause, the builder's Delay Until, Wait Until and Wait While, and the legacy ability path's maintained stages wait through them. Step 6 done: worlds side by side and with no scene: every world draws its cache indices from one counter, so an index is a process's own among all worlds'; a MonoBehaviour process ends what it handles through each relay; a world of its own reaches for nothing of the scene's, so what's registered there with no handler has none, and with no packet takes the default, and it runs outside play mode with no scene at all. The headless simulation scenario runs duels of runtime entities, each pair in a world of its own, eight side by side, within one frame, for a balance's numbers |
| FEAT-11 | Input binding | Targeting without legacy `Input` and `Camera.main`. Done (Decision 30): the five click-targeting tasks read `ForgeInput.Current`, an `IForgeInput` of whether a pointer button went down this frame and the ray the pointer aims along, through `AbstractTargetingAbilityTask.WaitForPointerPress`; a game puts its own in with `ForgeInput.Use`; the default is the Input System's pointer (`InputSystemPointerInput`) where its package is in and on, through an optional reference and a version define, or else the Input Manager's mouse (`LegacyPointerInput`), each aimed through a camera given or `Camera.main`; with no camera a press aims nowhere, where it threw; a source-scanning test keeps Unity's input out of the rest of the runtime |
| FEAT-12 | Ability graph and timeline, effect recorder | Editor. Done (Decision 30): the Debugger's fourth view, Timeline (`Editor/Debugger/RuntimeDebugger.Timeline.cs`), and its recorder (`TimelineRecorder.cs`), which hears each entity the Debugger lists while it reads the game in Play mode and keeps, by the game's time, each ability's runs, stage by stage, its refusals, and each effect's time on the shelf, a hundred spans a track. Each track draws its spans over the last seconds, 5 to 60 from the toolbar. A track selected shows an ability's graph, read-only: its stages in order, each with its policy, its tasks and when the selected run reached it, beside its runs; or an effect's applications. Record pauses the recorder and Clear forgets what it kept. It lays out as ED-43 has the Debugger, in a narrow column too |
| FEAT-13 | Networking | Prediction and replication; not planned. Only its two cheap changes are in Phase 5 (Decision 30): one injectable clock and one seedable random source, and IDs scoped to an entity. Both done: `ForgeTime` and `ForgeRandom`, which a source-scanning test keeps the runtime reading; `HandleId` per ability system; `GameplayEffectSpec.ApplicationId` per target; `Tag.Id` was never serialized (Decision 22) |
| FEAT-14 | Item stacking | An inventory holds an item as a count, up to the Item's `MaxStackSize`. The field and `ItemSpecContainer.ItemStacks` exist, but nothing reads or sets them (GAS-51). Decide how giving and removing count, how a stack's effects and abilities scale, and how Allow Duplicate Items meets it. Done (Decision 30): a give fills the item's stack first, up to `ItemDefinition.MaxStackSize`; a full stack opens another only with Allow Duplicate Items; `GiveItem(item, count)` takes what fits and returns how many, and `RemoveItem(item, count)` takes stacks off; a stack applies its effects and ability once, and `ItemStacksScaler` reads its count, which an equipped item's effects read again as it changes |
| FEAT-15 | Register builder | One builder or options struct in place of `ProcessControl`'s 28 static `Register` overloads, from the review's API changes. The roadmap gave it to Phase 3, but no row tracked it, so it moved here when Phase 3 closed. Today the argument's static type picks the overload, and a failed call returns false with a null relay. The review paired it with a `Run` on the relay, which doesn't exist yet, and an awaitable completion, which `ProcessRelay.WhenTerminated` now is (TS-Z). Done (Decision 30): `ProcessControl.Register(process)` starts a registration that `HandledBy`, `With` and `In` shape and `Run` makes, returning the relay or why it was refused; the handler follows the packet's run-time type; `ProcessRelay.Run` starts a waiting process; the 28 overloads are `[Obsolete]` wrappers until the first Asset Store submission |
| FEAT-16 | Loose tags | Done with ED-40, ahead of the phase. A tag granted with no effect, ability or item behind it, as Unreal's loose gameplay tags are: `AddLooseTag`, `RemoveLooseTag` and `GetLooseTags` on each hub, each grant counted, and the tag compile includes them. A tag added straight to the tag cache went at its next compile |
| FEAT-17 | Activation by asset or tag, with a reason | From the review's API changes: activate by ability asset or tag, and say why a refused activation was refused, with a matching callback. `TryActivateAbility` returns only true or false (`AbilitySystemComponent.TryActivateAbility`). Done (Decision 30): `TryActivateAbility(ability)` activates by asset, in the first slot that holds it, and `TryActivateAbility(tag)` the first ability by slot whose asset or context tag is the tag or under it; each form, and the request's, can give an `out ActivationRefusal` (`EActivationRefusal`: Not Known, Disabled, Locked, No New Processes, Level Zero, Requirements, Cooldown, Cost, Dead, Rule with its name, Blocked, Busy, Critical), and `AbilitySystemCallbacks.OnAbilityRefused` tells each refusal; a request the buffer retries isn't told again |
| FEAT-18 | Performance tools | From the review's Profiling gap: Runtime had no `ProfilerMarker`, so the cost of a process step, an effect application or an ability activation couldn't be seen in Unity's Profiler. Decision 32 widens it: markers on process steps and the GAS's work, counters for ProcessControl, the GAS and TaskSequence, and two Profiler modules that chart them, one listing each frame's costliest processes. Adds the `com.unity.profiling.core` dependency. Editor and development builds only. Done (Decision 30): `ForgeProfiler`'s markers, in the Scripts category: `PlayForge.ProcessStep`, `PlayForge.GASStep`, `PlayForge.EndOfFrame`, `PlayForge.ApplyEffect` and `PlayForge.ActivateAbility`; the step loop's at each timing (`PlayForge.Update`, `PlayForge.LateUpdate`, `PlayForge.FixedUpdate`), `PlayForge.TagCompile`, `PlayForge.Recompute` and `PlayForge.Stage`; and a step marker per process type, `PlayForge.Step.` and the type's name, within ProcessStep, with the process's ID as its metadata (`ForgeProfiler.StepOf`, a `ProfilerMarker<int>` from `com.unity.profiling.core`, a dependency now); and `ForgeCounters`, the counters, in the PlayForge category: ProcessControl's (Processes Running, Waiting and Created, the scene's, and every world's Registrations, Terminations, Steps, State Changes and Step Time a frame), the GAS's (Mono and Runtime Entities, Effects Active, and Effects Applied, Effects Removed, Impacts, Activations, Refused Activations, Tag Compiles and Recomputes a frame) and TaskSequence's (Sequences Running, and Stages Started a frame); and the two Profiler modules (`Editor/Debugger/ForgeProfilerModules.cs`), PlayForge Processes and PlayForge GAS, ten counters each, the Processes module's details listing the selected frame's costliest processes from the frame's metadata, which `ForgeFrameCosts` puts into the capture while the Profiler records |
| FEAT-19 | Effect stacks scaler | Nothing built in read an effect's stack count, so a change by the stack, as a sunder's −5 armor a stack, took a scaler written for it: Proving Grounds wrote its own twice (`StackCountScaler` in `EffectStackingTests` and `GoldenImpactTests`). Found by the Combat lab. Done (Decision 30): `EffectStacksScaler` reads the stack count of the effect's own container, 1 for one that doesn't stack, as `ItemStacksScaler` reads an item's (FEAT-14) |
| FEAT-20 | Copy an entity | The review's mirror clones: copying a GAS's state into a new runtime GAS took FEAT-9's capture and restore, which need a catalog naming every asset and IDs for every entity, which assets made as the game runs never have; and the entity's own level stayed behind on a clone of another kind, saved under its kind's tag. Found by the mirror clones scenario. Done (Decision 30): `ForgeSave.Copy(from, onto)` captures and restores with names for assets and entities that last the session, and a save keeps the entity's own level under no key, which loads onto the target's own |
| FEAT-21 | Run a sequence made in code | Run Sequence ran only a sequence a static method made, by name (`TaskSequenceReference`), so a sequence made in code, as an ability built as the game runs makes one with `TaskSequenceBuilder`, couldn't run in an ability; Proving Grounds' nested-sequence tests handed theirs over in a static field. Found by the ability crafting scenario. Done (Decision 30): `RunSequenceTask.Definition`, a `TaskSequenceDefinition` made in code, of which each activation runs a fresh sequence |
| FEAT-22 | Time scale from an attribute | The review's time magic: an entity's time (FEAT-7) could be set only by its game's code, through its clock, so no effect could slow or haste one, and two that tried would each need code to undo the other. Found by the time magic scenario. Done (Decision 30): an identity's Time Scale (`EntityIdentity.TimeScaleAttribute`, and the runtime identity's) names the attribute its time runs at, read as each step begins, so an effect on it slows or hastes the entity, stacks with others as modifiers do, and ends with its effect. The Entity inspector's Attributes section edits it |
| FEAT-23 | Move an effect between entities | The review's possession: an effect could only be taken off one entity and applied to another, which started its time again, ran its application's workers, commits and contained effects a second time, and told both. Found by the possession scenario. Done (Decision 30): `EffectSystemComponent.MoveTo(effect, onto)` moves each of the effect's containers with its stacks, its time left and what applied it, as a restore puts one back (FEAT-9); its lasting modifiers, granted tags, lent abilities and lasting cues go with it, and nothing of a removal or an application runs |
| FEAT-24 | What a spawned thing knows | The user's question of 2026-11-06, for Phase 6: an arrow an ability fires, how does it know how fast to fly? Should its speed be an attribute, and should the arrow carry it, set as it spawns with respect to factors, as …? The note ends there. Today `ProjectileMonoProcess` reads its source's Projectile Speed attribute by tag on each frame of its flight, and fails if the source doesn't have it. Open: Open decision 20 |
| FEAT-25 | Quick fixes for problems | The user's request of 2026-11-06: the Problems window gets a second view, its problems grouped under the rule each breaks, and where a simple step fixes a problem, a Quick Fix for it and a Fix All for its rule's problems. Done: By Rule, beside the list in the window's toolbar. Each problem knows its rule (`ForgeProblem.Rule`), each rule names what it finds (`ForgeValidationRule.Name`), and a problem a step fixes carries the step (`ForgeProblem.Fix`, a `ForgeQuickFix`): Store the Tag, Use Own Levels, Make It Constant, Give It Its Own, Clamp to Bounds and Remove the Element. A fix and a Fix All are one Undo step each. A problem whose fix needs a choice, as which stage applies usage effects, has none | `ForgeProblemsWindow.cs`, `ForgeRules.cs`, `ForgeProblem.cs`, `ForgeValidator.cs` | verified |

**Phase 5's order,** from the review's plan for the test project (its labs, one at a time) and the table above. A lab
is a small game: assets the lab builds in code, and a bot that plays it in PlayMode tests under a live ProcessControl,
as the harness's World runs one. Each lab brings the features it finds missing, test-first. The foundations come
first (Decision 30).
0. **Foundations:** FEAT-15, the Register builder; FEAT-3, tag-change events, with GAS-56; FEAT-7 and TS-6, the time
   source and the fixed-rate tick; and FEAT-13's two changes, the injectable clock and random source and per-entity
   IDs.
1. **The Status-effect lab:** poison, burn and bleed under each stacking policy; cleanse, immunity, and shields that
   absorb damage before health. With FEAT-8: cleanse and immunity first, then overflow at max stacks, abilities
   granted by effects and the chance to apply.
2. **The MOBA hero kit:** four heroes of four abilities, with mana, cooldown reduction, armor and magic resist,
   lifesteal, silence, and channels a stun cancels. With FEAT-5, cancel and block abilities by tag.
3. **The Duel lab:** two fighters with frame data, hit-stop, an input buffer and cancel windows. With FEAT-6 (the
   input buffer), FEAT-7 and TS-6 (a time source per entity), and TS-7 (critical sections that branch).
4. **Bullet heaven:** 10,000 projectiles and 500 enemies, area auras, pooled spawns and death cascades.
5. **Roguelite builds:** relics and items, set bonuses, tomes under auras and level-up rescaling. With FEAT-3
   (tag-change events) and FEAT-14 (item stacking).
6. **Summoner:** summons as runtime hubs with lifetimes, auras through ongoing requirements, and despawn on the
   owner's death.
7. **Tactics:** turn-based abilities stepped from a manual time source, with save and load between turns. With
   FEAT-9.
8. **The Combat lab:** every delivery, stacking and interaction policy in one scene, with a health bar from
   `GetBreakdown`.

Then the stretch scenarios, which drive FEAT-1, FEAT-2, FEAT-4, FEAT-10, FEAT-11 and FEAT-12, all in Phase 5's
scope (Decision 30), with FEAT-17 and FEAT-18. FEAT-13, networking, gets only its two cheap changes. FEAT-15 is a
foundation, and FEAT-16 is done.

## Test project: PlayForge Proving Grounds

A separate Unity project that consumes PlayForge as a local UPM package (Decision 2). The project and its shared
harness exist, with the golden scenarios, the framework and planted suites, and the editor tests (360 EditMode
tests in `Tests/Editor`, among them the editor smoke suite, which its README explains). The gameplay labs exist, all eight
green (Decision 30), each with a scene that plays its bot (Decision 33), and a ninth, the Battle lab, at the user's
asking, and so does the stress suite (`Tests/Stress`). See
Environment for its location and commands. The full plan is in the
review doc's section "The test project: PlayForge Proving Grounds". In short:

- **The shared harness:**
  - a frame stepper;
  - invariants: no leaked control blocks, Prepare and Clean paired, slices sum to ActiveValue, dealt equals
    received;
  - a fault injector;
  - an allocation probe using `ProfilerRecorder`, because the GC API returns 0 under Mono;
  - a ProcessControl state oracle;
  - scene fixtures.
- **Four suites:**
  - framework suites;
  - fault and stress;
  - gameplay labs: Duel, MOBA, Roguelite, Status-effect, Bullet heaven, Summoner, Tactics, Combat lab;
  - editor and UI.
- **Plus stretch scenarios.**
- **Rule:** run under a live ProcessControl. Never call `WhenUpdate` or `WhenLateUpdate` by hand; that is how
  GAS-1 stayed hidden.
- **First build step:** done. The harness, the 25 golden scenarios ported, and red tests for the top 15.

**Top 15 to test first:** GAS-1, PC-1, PC-2 with TS-4, PC-3, TS-1, TS-A, TS-B, TS-C, GAS-2, GAS-3, GAS-6, GAS-7,
GAS-4 with GAS-5, PC-6, and TS-I with PC-5.

## Log

Record baselines, gate results and notable decisions here, newest first.

- 2026-11-06, **ED-57: the worker group and sequence reference drawers.**
  - A worker group is its Name, then Attribute, Impact, Tag and Analysis Workers, each Unity's reorderable list,
    whose + adds an empty worker through ED-64's `ForgeLists.AddsEmpty`. Its Workers section folds, counts and
    imports it, so the group's own box, badges and Import are gone.
  - A sequence reference is Unity's popup, None first, of the static `[TaskSequenceMethod]` methods that take
    nothing and return a sequence, found by `TypeCache`; a chain reads "(Chain)", two of one name add their
    class, and a method that's gone reads "(missing)" until another is picked.
  - A sweep for C# styles found seven more drawers older than Phase 4, now ED-66.
  - Each test failed first. EditMode: 516 of 516, with ED-64.

- 2026-11-06, **ED-64: the ability stage drawer.**
  - A stage folds as an attribute set element does, "Stage 1" with Critical on its right while a task commits the
    ability, to a line as "2 tasks · All · Applies usage effects". Its fields are Unity's, the policy the kind field
    ED-56 made, its tasks Unity's reorderable list. A condition draws its tag, then Looked For On and When Missing
    under it once it has one.
  - `ForgeLists` (Kit) hooks the list a PropertyField builds, as the field is laid out or pressed, so nothing polls
    (ED-11). `AddsEmpty` makes + add an empty element, so a new task never shares the last one's object.
    `KeepsStates` makes the stages' − and drag move each stage's fold with it (`DrawerCollapseState.MoveElement`):
    the stage's own × did this, and a drag never had.
  - Found on the way: the kind field and the condition tracked their property, and Unity threw "has disappeared" as
    it took up the tracking of a stage just removed. They follow the asset now and find the property again.
  - Testing Unity's list: query a list's own footer buttons, since its elements' lists come first in its tree, and
    use it only once it's laid out. Before its first layout Unity's binding reads an element a delete took.
  - Each test failed first. EditMode: 516 of 516.

- 2026-11-06, **Decision 37: the demo refresh, Phase 7.** The user asked for a new demo once Phase 6's UI fixes are
  in, to replace every demo but the labs, Swarm Defender among them. Their spec is Decision 37, and the questions it
  leaves for when the phase starts are Open decision 21.

- 2026-11-06, **FEAT-25: quick fixes in the Problems window (the user's request).**
  - By Rule groups the shown problems under the rule each breaks, a rule with errors first, each with its count
    and Fix All, then a row for each problem with its Fix. The Errors, Warnings and search filters apply to it.
  - The validator gives each problem the rule that found it; a rule names what it finds, and a problem a simple
    step fixes carries the step, written through a SerializedObject so Undo takes it back and the validator checks
    the asset again. Six fixes: a stale asset tag is stored, as Identity's Update does; a link with no provider
    takes the asset's own levels, which it already reads; a field that reads a scaler it hasn't got reads its
    number, as its message says; the second of two fields holding one scaler gets a copy; a magnitude outside its
    bounds takes the value the runtime clamps it to; an element with no attribute goes. Fix All runs a rule's fixes
    from the last, so a removed element leaves the places of those before it.
  - Left without a fix, as each needs a choice: data whose class is missing (a fix would throw it away), an
    asset with no name, an impact that doesn't land as set, a shared scaler with no Scaler asset, a set that gives
    nothing, and a cost or cooldown no stage applies.
  - The snapshot of By Rule over the test project showed 34 Demo assets whose stale tag message named one path
    twice: their tags were saved with no display name. The message says that now.
  - Each test failed first: they use `ForgeProblem.Rule` and `Fix`, which didn't exist.
  - EditMode: 510 of 510. A first full run had ED-44's timing check over its limit, 84.6 ms against 77.4, which passed alone and on the rerun; the validator's work isn't in the push it times.

- 2026-11-06, **ED-56: the kind field; ED-65: the header's file name (the user's report).**
  - ED-56: `AbstractTypeRefDrawer` sits under about seventeen drawers, so its rewrite moves them all into the
    style at once: a kind field as the tag field is (`ForgeTypeField`, sharing its box), Unity's searchable
    dropdown of the kinds (`ForgeTypePicker`), and the kind's fields indented under it. `AbstractGenericDrawer`
    folds as an attribute set element does, to a line of values, as "Strength 2 · Duration 0.5 · On Each Tick".
    Unity gives a list's elements one drawer, by their path without the index, so the drawers keep no state of
    their own: each field's state is in its elements, and its property is found again by its path (ED-49). The
    subclasses keep only how they name a kind and whether None is offered; the refresh each did after a pick is
    the base's. `AbstractRefDrawer`, the old picker, had no other subclass and is gone. The stage drawer, 670
    lines of its own, is ED-64.
  - ED-65, the user's report on ED-58: the header's actions share one row, and the file name a row of its own
    under them, on the right.
  - FEAT-25, the user's request for a Quick Fix view of the Problems window, is queued after these.
  - Each test failed first. ED-56's list test draws the two elements as fields of their own, as the list does:
    a list's view builds no rows in a panel with no screen to size it.
  - EditMode: 508 of 508.

- 2026-11-06, **GAS-64: "up to a level" spreads to its level (the user's call).**
  - ED-62's Reads row showed a scaler spread "up to a level" read its last value at every level above the first:
    `GetEffectiveMaxLevel` took the lower of its Max Level and the level being read as the range's end. The user
    chose the lower of the Max Level and the provider's last level, so the values spread to the Max Level, or to the
    provider's last level when it has fewer, and a level past the range reads the last value.
  - Red first: `ScalerTests.GAS_64_A_scaler_spread_up_to_a_level_spreads_to_it` (PlayMode) read 30 at level 2
    where 20 belongs, and `ScalerDrawerTests.GAS_64_The_reads_row_spreads_up_to_a_level` (EditMode) likewise.
    The tooltips say what the option does now.
  - EditMode: 502 of 502. PlayMode, its golden scenarios among them: 1306 of 1314, the 7 planted and the soak skipped, as before.

- 2026-11-06, **ED-62 and ED-63: a spread scaler's levels, and Quick Fill (the user's report, mid-phase).**
  - A spread scaler's points were named by levels such as 1.8, and nothing showed what levels 2 and 3 read. The
    points are numbered, and a Reads row under Between Points shows each real level's value: the stored curve at
    the place `AbstractScaler.GetEffectiveRelativeLevel` gives the level, the runtime's own reading. A curve that
    reads other than its points draw, as one made in code without `RegenerateCurve`, says so. The first cut
    compared the curves' keys and warned on a one-point scaler that reads the same either way, which the
    snapshot showed, so it compares what the levels read.
  - Quick Fill is a dialog in the style. Its patterns have plain names, as Slow Then Fast, and it adds Ease In and
    Out, a Custom Curve drawn in Unity's curve field, a strength for the two bending patterns, rounding to whole
    numbers or the nearest step, and a count for a scaler with levels or points of its own. Apply finds the scaler
    again by its path, as ED-49's fix does, and writes the values and, through `ScalerFields.KeepCurve`, the curve
    as one Undo step; it built its own curve, with other tangents.
  - Found, not changed: a spread scaler over the provider's levels "up to a level" (`ELevelConfig.Clamped`) takes
    the lower of its Max Level and the current level as the range's end, as its tooltip says, so every level above
    the starting one reads its last point. The Reads row shows it. Put to the user.
  - Each test failed first, against the committed drawer and wizard.
  - EditMode: 501 of 501.

- 2026-11-06, **ED-49: what an attribute starts at.**
  - The user asked whether an attribute's start value can be worked out, given sub-sets and the sets a set sits
    in. It can, and only the runtime's own path gives it faithfully: a scaler can read another attribute, which
    the set may not give, and collisions settle on magnitudes before scalers run. `AttributeStartValues` builds
    a stand-in runtime entity at each level and sets its attribute system up from the set, as the GAS does. The
    stand-in gets no set of its own, so the set's workers, which could act beyond it, aren't given to it.
  - An element shows Starts At at each of the set's levels, as "100/120/140/160", or Current and Base apart.
    A note names the set whose value is used, and a warning names an attribute a scaler reads that the set
    doesn't give, which reads 0. Preview In, shown while another set holds this one, works the values out as
    part of that set; the choice lasts the editor session. The table shows the same values, an entity's at its
    own levels.
  - Base and Current are the magnitude field the Effect inspector uses, both shown; copying one into the other
    moved to the field's menu. Bounds, Constraints and the value pair are Unity's fields, a floor, a ceiling and
    a snap interval indented under what they depend on.
  - The snapshot of an element showed a scaler's attribute field with no label: a reference field inside a
    list's element took itself for the list's row, as the tag field did (ED-55). It keeps its label.
  - The first full EditMode run crashed the editor. The smoke sweep of the Demos' Boss A set now reaches Base's
    scaler, which the old drawer hid behind its switch, and resized a spread scaler through a property its field
    kept from when it was built, which pointed into a managed reference an Undo had put back. The magnitude field
    finds its properties again each time it draws, and `EachAsset` finds a single asset's property again by its
    path. The Boss A sweep crashed every time before the fix and passes after it; a test of the resize alone,
    outside the inspector's list, doesn't reproduce the crash, so the sweep is its guard.
  - The tests were written first; against the old drawer they couldn't compile, with no `AttributeStartValues`.
  - The stand-in says nothing below an error as it starts, so a set whose runtime warns doesn't fill the Console
    on each redraw, and Preview In's choice is kept in SessionState, so a script reload keeps it.
  - EditMode: 499 of 499; after the last two changes, the attribute set, scaler and sweep tests again,
    54 of 54. No runtime change, so no PlayMode run.

- 2026-11-06, **ED-50 and ED-55: an impact entry, and the tag field.**
  - **ED-50:** with the scaler drawer redone, the snapshots of an effect with two additional impacts showed what
    was left: each entry's Retention Group a bare `<None>` bar, and its row naming no delivery. An entry's row names
    its delivery on the right, and a periodic entry says what it commits.
  - **ED-55:** the tag drawer is redone in the style. The picker is Unity's `AdvancedDropdown`, the native
    searchable hierarchy Add Component uses, over the field's context's tags by their display names; New Tag… opens
    a small window for the path. Making a tag a child of another, a button that showed on hover, is in the field's
    right-click menu. A dropdown needs Unity's IMGUI, which batch mode can't show, so the tests draw the field, call
    the pick, and read the picker's rows.
  - Each test failed first. ED-50's entry is a field in the impact's field, which builds a few frames after it, so its
    test waits for it. The smoke suite opens New Tag, as it opens every window.
  - EditMode: 488 of 488.

- 2026-11-06, **ED-51 to ED-54: the scaler drawer.** The scaler, which the user counts the most important field,
  is reworked from the user's list, with the style exception Decision 36 gives it.
  - **What a scaler shows:** its settings sit on one block under its field, indented, on a translucent shade with a
    hairline under it (`--pf-scaler-backing`, `--pf-scaler-shadow`), so a block in a block shades a step deeper.
    A kind shows what it reads: `AbstractScaler.Parts` says which of every scaler's parts, values per level, level
    range and behaviours, it reads, its default following `UseScalingOptions()` so a user's existing scaler shows
    as it did. A kind's own fields draw from each class below `AbstractScaler` and `AbstractCachedScaler`, as Unity
    draws them, so any field type and any property drawer of the user's own work (the user's question while it
    was built). `[ForgeShowWhen]` shows a field for some settings only.
  - **Its levels:** Values are Per Level or Spread Over Levels, side by side. Per Level follows the asset's level
    provider unless the scaler takes its own levels; spread, each value says the level it sits at.
  - **Seen, not only tested:** a diagnostic kept in the private copy alone renders an inspector into a runtime
    panel with a target texture, with Unity run in batch mode with graphics, and writes it as a PNG. The
    snapshots showed the block, the menu, the curve beside the table, and a Constant kind popup that read
    "Constan": Unity's popup keeps 40 px of its text's room beside the arrow, which PlayForge's style gives back.
    They also showed what ED-50 is: an impact entry's Retention Group drawn as a bare bar, and its row naming no
    delivery.
  - Each test failed first; the menu opened 20 px wide. A scaler of the user's own, with a base of its own and
    fields of eleven types, draws each field.
  - EditMode: 482 of 482. PlayMode, its golden scenarios among them: 1305 of 1313, the 7 planted and the soak skipped
    as ever. The runtime change adds `Parts` and an attribute that nothing at runtime reads.

- 2026-11-06, **ED-58, ED-59 and ED-60: The Forge's names, its tag view's sorting, and tags' display names.**
  - The Forge's list shows each asset's name alone. The selected asset's header shows its file name under Show in
    Project (a stack of the button and a line of secondary text, `ForgeAssetHeader.AddLineUnder`), and Show File
    Names is retired. The tag view's three columns sort, and each list's header shows the window's sort again as
    the window is built, as after a domain reload.
  - A unique tag (`Tag.GenerateAsUnique`, which the tag picker uses for each new tag) pads its name with a
    SHA-256 suffix to 64 characters. Every place that showed a tag's name shows its display name now, through one
    helper, `TagDisplaySettings.PathOf`. A tag's name stays its identity: The Forge keeps the selected tag by it, and
    the Debugger's rows are named by it.
  - The tests that failed first: the list's name cell held a file name; the header had no file line; the tag view
    didn't sort and its rebuilt header lost the sort; TagText gave a 64-character name; the Debugger's tree had no
    node at the display path. The tag registry reads the project's assets, rebuilding on an import, so the tests
    put their tags in saved assets rather than registering uses by hand.
  - EditMode: 477 of 477.

- 2026-11-06, **ED-46 and ED-47: the Timeline's order and its selected span.** The tracks list the newest first, and
  a track's runs and applications the latest first. The span the detail shows was the selection's color on a row
  the selection colors; it's taller and in strong text now. `ED_47` reads the selected row's and the span's colors in
  a runtime panel and wants 3:1 between them; the strong text measures 4.6:1 on Dark's selection, 5.6:1 on its
  unfocused selection, and 4.0:1 and 9:1 on Light's. The Debugger's suite: 22 of 22.

- 2026-11-06, **ED-45 and ED-48: the Debugger's splits, and the tables' column widths.**
  - **The cause:** a view hidden by display: none was laid out at 0 px. Unity's split skips its layout for a split
    hidden itself, not for one whose parent is (`TwoPaneSplitView.UpdateLayout`, 6000.0), so it shrank its fixed pane
    to its least and kept that, and the column header (`ColumnLayout`) shrank its columns the same way. The Tags
    view's tree had a flex basis of 0 from Unity's tree view style, so it never showed.
  - **The fix:** `ForgeStack` holds views that take turns in one place, each placed over the whole place and hidden
    by visibility, so it keeps its layout. Picking and Tab skip an invisible element, and a view that hides blurs
    what it had focused. `ForgeColumnWidths` keeps a table's dragged widths, one EditorPrefs entry a table
    (`PlayForge_Columns_<table>`). The tables carry no view data key: one would make Unity keep their columns, but
    it would also restore their selection and scroll by index.
  - **Tested** in a runtime panel the window's content moves into, as batch mode gives a window no size: each view's
    split through the other views at 1280 and 360 px, and through Play mode's end and start; a view's splitter after
    the window widened while it was hidden; a dragged column through the other views and through the window's
    reopening, in the Debugger and in The Forge. Each failed first. A runtime panel runs its scheduler by real time,
    and a column header whose layout was scheduled on the window's panel never lays out in the runtime one, so the
    tests lay it out.
  - EditMode: 471 of 471.

- 2026-11-06, **Phase 6: the user's UI list (Decision 36).** The user listed the editor UI that still needs work.
  It's in the tracker as ED-45 to ED-61, with a design question as FEAT-24, and the roadmap has a Phase 6 for it.

- 2026-11-05, **CI is green: SP-24.** With the user's Unity license in Proving Grounds' secrets, the `Gates` workflow
  ran the full gates on GitHub's Ubuntu runners, in GameCI's Unity 6000.0.33f1 container.
  - **The first run** ran nothing. GameCI's `unity-test-runner@v4` now names v4.4.0, which wraps a new CLI, and that
    CLI took the workflow's `coverageEnabled: false` as an unknown argument and stopped. The workflow pins v4.3.2,
    the classic runner, with coverage off by an empty `coverageOptions`; checkout, cache and upload-artifact move
    to their current majors, which run on Node 24.
  - **The second** passed PlayMode in all three orders, 1305 of 1313 with the 7 planted and the soak skipped, as on
    Windows, and the planted failures, 7 of 7. EditMode passed 467 of 468:
    `ED_4_The_status_bar_counts_the_processes_and_entities` read the Debugger's short counts. A batch editor on
    Linux opens its windows narrower than 720 px, so the Debugger laid itself out as a column (ED-43); on Windows a
    batch window has no width, and it stays wide. The test fits the window to a full width first, as ED-43's
    test fits it to each width it checks. Nothing of PlayForge changed.
  - **The third** is green: https://github.com/gabedvdsn/PlayForge-Proving-Grounds/actions/runs/42176163757. PlayMode 1305 of 1313 in each order, the planted failures 7 of 7, EditMode 468
    of 468. Each job takes about five to six minutes once the Library cache is there.
  - **Phase 3's exit gate,** zero steady-state allocation enforced in CI, is met now: the allocation tests run in
    every run. Decision 27 closed the phase without it. SP-24 is verified.
  - **Not set up:** `PROVING_GROUNDS_DISPATCH_TOKEN` in PlayForge's secrets, so a PlayForge push waits for the
    daily run or a Proving Grounds push. The check runs the test runner makes read neutral, not passed.

- 2026-11-05, **The Battle sample (Decision 34).** The Battle lab ships with the package as the sample Battle, first
  in Package Manager's list, beside Demos and Code Examples.
  - **What it is:** the lab's battle, out of Proving Grounds' test assembly. Its fighters, kits, minds, shots and
    tasks are in `Samples~/Battle/Scripts/Battle.cs`, its view in `BattleView.cs`, and in `BattleKit.cs` what the
    harness gave it: its assets built in code, and where its entities and shots live. `BattleSample`, the scene's
    one script, plays battle after battle, brings PlayForge up when the scene has none, and frames the camera.
  - **Two changes from the lab:** its materials take the render pipeline's own default shader, where the lab's took
    Standard, which shows pink under URP. And its entities register through the Register builder (FEAT-15), where
    the harness used an overload marked obsolete, which a clean import counts as a warning.
  - **Checked:** its scene, opened in play mode with nothing of the harness, brought PlayForge up and played a
    battle to its end: 28.4 s, Red won, 205 shots, 56 hits over an area, 26 knockbacks, 6 stuns, 10 summons, 14
    heals, 6 blessings and 21 deaths. The next battle began after the pause. The clean import with every sample
    (Proving Grounds' `Tools/clean-import.py --samples`): 0 compile errors or warnings, 0 uncompiled scripts, 0
    missing scripts, references or types, 0 exceptions.
  - **Not checked:** how it looks, as batch mode draws nothing. Its camera framing is new; the rest is the lab's.
  - **No runtime or editor code changed,** so the gates didn't run. GETTING_STARTED names the sample, and says the
    samples render in the Built-in pipeline or URP.

- 2026-11-05, **SP-24's CI, and the user's answers.** Decisions 34 and 35.
  - **The labs and the UI kit:** the Battle lab ships as a sample, Battle, beside Demos and Code Examples; the other
    labs stay in Proving Grounds. No runtime UI kit: a game makes its own UI on PlayForge's API.
  - **Proving Grounds on GitHub:** before it went public, its 236 commits were checked for what they'd publish:
    no credentials, and no Unity cloud or organization IDs in its project settings. The user's own files, which
    git ignores or which aren't committed, stayed on the machine.
  - **The `Gates` workflow:** its first run, on the push that added it, warned that the gates are skipped until
    the secrets hold a Unity license, and skipped them: https://github.com/gabedvdsn/PlayForge-Proving-Grounds/actions/runs/42162134114. The jobs run on Ubuntu 24.04, pinned, as GitHub
    moves `ubuntu-latest` to Ubuntu 26.
  - **What's the user's now:** the license secrets, and the optional dispatch token; then the first full run, which
    may find what a Linux editor does differently. SP-24 closes when it's green.
  - **Next:** the Battle sample.

- 2026-11-05, **The authoring round trip, and GAS-63.** The review's last editor check (Module 4), in Proving
  Grounds' `Tests/Editor/AuthoringRoundTripTests`, EditMode.
  - **The round trip:** an attribute, a set, an entity, an effect, an ability and an item, each made by The Forge's
    Create, saves as the same text when written again. Each of their simple fields, 120 of them, edited through a
    SerializedObject as an inspector edits, undoes to the byte and redoes. So do 132 more in assets filled in: an
    ability with a stage of two tasks and an effect with a worker, each held as a reference to its own type, and a
    set of two attributes. An edit across three abilities at once reaches each, and one undo takes it back from
    each. A push down a chain of templates, A → B → C, changes only the lines it pushes, and one undo takes it back.
  - **GAS-63,** which filling the ability found: an ability's stage list had no initializer, so one made in code
    had none until it was saved and loaded, and adding a stage threw. It starts empty now.
  - **Its tests,** seen failing first: `GAS_63_An_ability_made_in_code_has_a_stage_list`, for each hub, in
    `AbilityActivationTests`; and the seventeen in `AuthoringRoundTripTests`.
  - **Tests:** PlayMode 1313: 1305 pass, 0 fail, and 8 are skipped, the 7 planted and the soak, in
    three orders. Planted 7 of 7; EditMode 468 of 468.
  - **What's left** of the review's test project is the runtime UI kit, a design question. And the user's: CI
    (SP-24), and whether the labs ship in Samples (Decision 33).

- 2026-11-05, **The editor's stress runs, and ED-44.** The review's editor stress (Module 4), in Proving Grounds'
  `Tests/Editor/EditorStressTests`, EditMode, category Stress. Each cost is per asset or entity, against a tenth.
  - **The Forge** over 2,586 assets, 2,000 effects and 500 abilities with the project's own: it lists them at about
    0.9 ms an asset the first time, as the validator checks each for problems, and 46 µs again, once it knows them.
    It sorts by every column at 10 µs an asset and filters at 30, each about as fast as over 336. Open and idle over
    them, an asset's inspector showing, it listens to no editor update.
  - **A template pushed** to 2,000 children costs 2.9 ms a child, as to 200; saving the template, which changes no
    child (ED-13), takes 23 ms against 18.
  - **ED-44,** which the push found: it searched the project for each child's own children, so it searched once for
    each member of the family. A push to 2,000 children took 28 s, 14 ms a child. One search finds the family now.
  - **The Debugger** samples 200 entities at 8.5 µs an entity, as 20.
  - **Not measured:** the review's idle inspector CPU, as batch mode has no graphics device to repaint a window. The
    editor's update listeners stand for it, as ED-11's tests have them.
  - **Its tests,** seen failing first: `ED_44_A_push_searches_the_project_once_however_big_the_family`, in
    `TemplatePushTests`: down a chain of 20, among 100 and then 1,000 other abilities, 88 → 166 ms, and 52 → 60 ms
    once fixed. And the three in `EditorStressTests`.
  - **Tests:** PlayMode 1311: 1303 pass, 0 fail, and 8 are skipped, the 7 planted and the soak, in
    three orders. Planted 7 of 7; EditMode 451 of 451.
  - **What's left** of the review's test project is the runtime UI kit (Module 4: a health bar from `GetBreakdown`, a
    buff bar, a cooldown radial and a cast bar, combat text from `FrameSummary`, a debug overlay), which no phase
    holds, and how it should look is a design question. And the user's: CI (SP-24), and whether the labs ship in
    Samples (Decision 33).

- 2026-11-05, **The stress suite's other half: churn and soaks, with PC-34.** So the test project's four suites are
  all built.
  - **Churn** (`ChurnTests`): registering a process and ending it cost about as much among 10,000 as among 1,000,
    ended oldest first or newest first: 0.96 → 1.20 µs to register, 0.30 → 0.35 µs to end. A process's step costs
    87 ns among 1,000 and 90 among 10,000. They're the review's budgets, churn linear to 10,000 and the step cost per
    process, and each run measures them; a run's output has the numbers.
  - **PC-34,** which the churn found: ending the newest of 10,000 took 4.4 µs against 0.7 among 1,000, as a step
    group searched its list from the front. It keeps each process's position now, and leaves a hole it sweeps out
    later.
  - **Soaks** (`SoakTests`): 100 casters each run an ability 100 times, 10,000 runs of its sequence. Each run counts
    once and its last stage is cleaned up, ProcessControl ends with what it began with, and the last thousand runs
    take about as long as the second thousand: 57 ms against 48 to 52. And a level of 10 entities, each burning and
    channelling, loaded over 100 times: each load takes the level and every process of it, and each cast's task is
    cleaned up as its entity goes.
  - **Half an hour of bullet heaven** (`Soak_Half_an_hour_of_bullet_heaven`, Explicit, category Soak, so the gates
    skip it; run it with `-testCategory Soak`):
    2,304,000 bolts over 30 minutes of the lab's time took 3 minutes of the machine's. The pool made 46 bolts; the
    step loops took 825 µs a frame in the second minute and 820 in the last; and the heap, 26.1 MB at the start, was
    27.6 MB two minutes in, once warmed up, and 27.6 MB at the end, after 2.15 million more bolts. Its first run met
    the Test Framework's three-minute timeout, so it has a Timeout of its own.
  - **Play-mode re-entry with domain reloading off,** the review's last soak, is SP-22's `DomainReloadTests`.
  - **The soaks' own two mistakes,** for the record: casters that shared one ability shared its probe task, which the
    ledger pairs by name, so their runs looked like one task prepared twice; each has its own now. And the soak's
    bolt index, the count of bolts times a prime, overflowed an int some 270,000 bolts in.
  - **Its tests,** seen failing first: `PC_34_Ending_a_process_costs_as_much_among_10000_as_among_1000`, in
    `ProcessRegistrationTests`; and the churn and soaks.
  - **Tests:** PlayMode 1311: 1303 pass, 0 fail, and 8 are skipped, the 7 planted and the soak, in
    three orders. Planted 7 of 7; EditMode 447 of 447.
  - **Next:** the review's editor stress (Module 4): The Forge over 2,000 effects and 500 abilities, a template pushed
    to 2,000 children, and the Debugger over 200 entities. What's left after it is the user's: CI (SP-24), and whether
    the labs ship in Samples (Decision 33).

- 2026-11-05, **The stress suite begins: scale, and GAS-62.** The test project's one suite not yet built (the
  review's Module 3), in Proving Grounds' new `Tests/Stress`, category Stress.
  - **Scale** (`ScaleTests`, on each hub): 500 entities with 20 effects each, ticking every quarter second, tick on
    time and allocate nothing in a steady frame. An entity's frame costs about as much among 500 as among 50: about
    6.8 µs against 5.7 in PlayForge's step loops, within the three times the cost tests allow. And an area ability,
    every match within 10 m and then Apply Effects, strikes each of 200 entities once.
  - **GAS-62,** which the area run found: a targeting task set each target it selected as the primary, so each
    replaced the last, and an Auto Target of every match kept only the farthest. Target Real and both apply tasks
    took only the first target. So an area ability struck one entity. The auto-targeting tasks file every target
    they select, in order, and each one reaches Target Real and both apply tasks. ABILITIES.md says how an area
    ability is made.
  - **The allocation probe's bursts:** the 500-entity run failed its allocation check on the runtime hub, 4,310
    bytes in one frame in each attempt. The Profiler traced them to the editor's Build Report REST service, not
    PlayForge: where another editor holds its port, as the user's does beside a batch run, it tries to listen again
    about once a second, on a thread of its own, and each try allocates about 4 KB, which "GC Allocated In Frame"
    takes as the frame's. These were the bursts the probe's notes gave no source for. A frame among 500 entities
    fills most of a second, so nearly every measure took one. The probe cancels those tries where the service
    isn't listening, and leaves a listening one, as in the user's editor, alone.
  - **ED-41's severity** was S, which the scale doesn't have. It's M.
  - **Its tests,** seen failing first: `GAS_62_An_auto_target_of_every_match_strikes_each`,
    `GAS_62_Auto_targeting_two_strikes_the_two_nearest` and `GAS_62_Apply_and_remove_reaches_every_target`, each
    for each hub, in `AbilityActivationTests`; and the six in `ScaleTests`.
  - **Tests:** PlayMode 1303: 1296 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 447 of 447.
  - **Next:** the stress suite's other half: register and unregister churn up to 10,000 processes, and the soaks:
    10,000 repeats of a sequence, 100 scene loads, and a long run of bullet heaven.

- 2026-11-05, **Phases 4 and 5 closed.** Phase 4's exit gate, the editor smoke suite in both themes, has passed
  in every gates run since 2026-11-04 (`4fd6a53`). Phase 5's, every test-project lab green, holds: all eight labs
  pass in every order, with the Battle lab beside them. So Phase 4's 25 rows and Phase 5's 12 move from `fixed` to
  `verified`, and so does GAS-51, with FEAT-14.
  - **The last full runs:** PlayMode 1291 tests, 1284 pass and 7 planted are skipped, in three orders; planted 7 of
    7; EditMode 447 of 447, the editor smoke suite among them. The run was on the code as committed at `2c8dff3`,
    and later commits change only docs.
  - **ED-42's row** had a feature's three cells in the Editor table's six, so it showed its name as its severity
    and no status. It has the table's six now.
  - **Left open, the user's:** CI (SP-24), and whether the labs ship in Samples (Decision 33). FEAT-13, networking,
    isn't planned past its two cheap changes.
  - **Next:** the test project's one suite not yet built, the stress half of fault and stress (the review's
    Module 3): entities at scale, and soaks. The review's build order gave it to Phase 3, which closed on its
    allocation budgets alone, and the roadmap has no phase for it.

- 2026-11-05, **The rollback scenario, with GAS-60 and GAS-61.** The ninth and last stretch scenario (Decision 30),
  so all of the review's stretch scenarios are in.
  - **Rollback** (Proving Grounds' `Tests/Stretch`): two duelists in a world of their own slash each other by a log
    of presses, at a fixed step, each tick's draws seeded, each slash's critical a chance and its bleed a lasting
    effect from the other, both saved after every tick. Rolled back eight ticks and run again on the same presses,
    the duel ends in the same state, to the byte of its save; with a press of Right's that came five ticks late, it
    ends elsewhere, and there each time.
  - **GAS-60:** the first run failed at a bleed's origin: a restore gave each ability back as a new one, so the
    other's bleed kept the old one, which no slot held. A restore keeps each ability in the slot its save gives it.
  - **GAS-61:** then at a bleed's time left, a float's last bit apart: a save kept the double timers as floats. The
    capture and restore carry doubles now.
  - **And JsonUtility:** then at a time to the next tick, a double's last bit apart, which was the save's own text:
    loaded from its JSON and saved again, a double came back one bit off. JsonUtility doesn't round-trip a double
    exactly, which PlayForge can't fix. A rollback keeps its saves in memory, as the test now does, where a load
    saved again is the same save. SAVING.md says so, with how to roll back, and that a load ends running abilities,
    so one that runs over several steps isn't replayed from its middle.
  - **Its tests,** seen failing first: `GAS_60_A_restore_keeps_the_ability_another_entitys_effect_came_from`, for
    each hub, and `GAS_61_A_restored_effect_keeps_time_with_its_twin_to_the_last_digit`, in PlayMode's
    `SaveLoadTests`; and `Stretch_Rollback_restores_eight_ticks_back_and_lands_where_it_did`.
  - **Tests:** PlayMode 1291: 1284 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 447 of 447.
  - **Phase 5:** every FEAT row but networking is done, all eight labs are green, and so is every stretch scenario.
    The roadmap has no Phase 6; whether the labs ship in Samples (Decision 33) and CI (SP-24) are the user's.

- 2026-11-05, **FEAT-23, and the possession scenario.** The eighth stretch scenario (Decision 30).
  - **FEAT-23:** `EffectSystemComponent.MoveTo(effect, onto)`. Each of the effect's containers on the shelf takes its
    clock as a save does, leaves quietly (its lasting modifiers unregistered, its lasting cues ended, its lent
    abilities taken back), and is put back on the other entity as FEAT-9's restore puts one back, by what applied
    it, with its Set By Caller magnitudes. Both entities' tags are compiled again. EFFECTS.md says how.
  - **Possession** (Proving Grounds' `Tests/Stretch`): a ghost possesses a knight as the knight prays. The prayer,
    cut off mid-cast by `RemoveAbility`, ends and goes to the ghost; the ghost's Haunt, its Ethereal and its Curse
    go into the knight, the Curse with the time it had left; the possessed knight haunts; and as the ghost leaves,
    each goes back, and the knight prays again. Nothing past FEAT-23 was missing: removing a running ability
    interrupts it cleanly, and its new owner casts it anew.
  - **Its tests,** seen failing first (no `MoveTo`, so they didn't compile):
    `FEAT_23_An_effect_moves_to_another_entity_with_its_time_left`, for each hub, and
    `Stretch_Possession_moves_abilities_and_effects_mid_cast`.
  - **Tests:** PlayMode 1287: 1280 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 447 of 447.
  - **Next:** the rollback scenario: a deterministic fixed tick, a snapshot, a restore eight ticks back and a
    re-simulation.

- 2026-11-05, **FEAT-22, and the time magic scenario.** The seventh stretch scenario (Decision 30), the first of
  the three left: time magic, possession and rollback.
  - **FEAT-22:** an identity's Time Scale names the attribute its time runs at. `GASCore.FollowTimeScale` reads its
    current value as each step begins, and as the entity initializes, so an effect that lowers it slows the entity,
    its effects, cooldowns, sequences and waits with it, one that raises it hastes it, and two add as modifiers do.
    Only the game's code could set an entity's time. The Entity inspector's Attributes section edits it, under the
    Attribute Set, and ENTITIES.md says so.
  - **Time magic** (Proving Grounds' `Tests/Stretch`): a slow field takes the entities within 5 m to half time for
    3 seconds of their own time, so 6 of the game's, while one outside runs on: a 2 s mark lasts 4 game seconds
    inside and 2 outside. Haste runs an entity at half again as fast, so its 3 s mark lasts 2. And Rewind, an
    ability whose task reads the caster's history, undoes what the last 3 seconds of its time did to its
    attributes, rebuilt from its frames' summaries (`OnFrameComplete`), so of four hits only the first stays.
  - **A trap it found:** the first rewind derived each undo from the hit's own sourced modifier, and the attribute
    system committed the hit again, as an effect's carrier commits its impacters as authored, the values only
    travelling with them. Not a bug, but nothing said so: the derivation constructor's comment does now, and points
    to `GenerateSimple` and `GenerateUnworkable`, whose carriers have none. The rewind's undo is unworkable, the
    entity's own change, which no worker sees.
  - **Its tests:** `FEAT_22_An_effect_on_the_time_scale_attribute_slows_or_hastes_its_entity`, seen failing first
    (no identity had a time-scale attribute, so it didn't compile), and `Stretch_Time_magic_slows_hastes_and_rewinds`.
  - **Tests:** PlayMode 1284: 1277 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 447 of 447.
  - **Next:** the possession scenario: abilities and effects moved between entities mid-cast.

- 2026-11-05, **FEAT-12: the ability graph and timeline, and the effect recorder** (Decision 30).
  - **The recorder** (`TimelineRecorder`, editor-only): while the Debugger reads the game in Play mode, it hears each
    entity the Debugger lists, whatever the view, so the Timeline has their past. It keeps, by the game's time
    (`ForgeTime.Time`), each ability's runs, keyed by the activation's own data packet, with each stage's span from
    the stage callbacks, which the compiled path fires too; each refusal, from FEAT-17's callback, with its reason;
    and each effect's time on the shelf, from its first application until no container of it is left there, an
    instant effect a tick. A hundred spans a track; entering Play mode starts it afresh.
  - **The view:** the Debugger's fourth, Timeline. Its tracks, one for each entity's ability or effect, draw their
    spans over the last seconds, a run or a time on the shelf as a bar in the secondary text's grey, the selected
    run in the selection's blue, and an instant as a tick, a refusal's in the destructive color. A track selected
    shows an ability's graph, read-only (Stages: each stage with its policy, its tasks, and the selected run's start
    and time in it), beside its runs (start, length, stages reached, outcome); or an effect's applications. The
    toolbar's Record, Clear and Last 5 to 60 s show with the view. The canvas has no board for it, so it follows the
    Debugger's boards and EDITOR_STYLE, whose Debugger section says four views now.
  - **Its test,** seen failing first: `FEAT_12_The_Timeline_records_abilities_and_effects_as_they_run` in EditMode's
    `RuntimeDebuggerTests` (the toolbar had no Timeline), on a stopped clock: a two-stage ability that ends as it
    activates, a channel that goes on and is then refused on its cooldown, and an effect held two seconds, then a
    360 px fit. A first draft expected a second activation of the running channel refused under Refuse; it isn't,
    as a delay doesn't make the ability system busy, so the test refuses it on its cooldown.
  - **Docs:** GETTING_STARTED's Debugger bullet names the four views and the narrow layout.
  - **Tests:** EditMode 447 of 447. An editor change, so PlayMode didn't run: it stands at 1282 from FEAT-18.
  - **Next:** what's left of Phase 5: the open decisions and the remaining stretch scenarios.

- 2026-11-05, **ED-43: the Debugger fits a narrow column.** Found as FEAT-12 began: EDITOR_STYLE's Debugger
  section and the canvas's three narrow boards say how the Debugger reads at 360 px, and nothing did it.
  - **The fix:** `RuntimeDebugger.Fit(width)`, which the window's geometry calls, lays it out by its width. Below
    720 px each view's split turns its list above its detail, 240 px tall, the toolbar wraps its views onto a row
    of their own across it, the frame count moves to the status bar, whose counts drop their parts, the Processes
    list hides Handler, Lifecycle, Timing and Lifetime, which its detail shows, Held By hides Source, and Follow
    Selection keeps its icon. At 720 px and up, it's as it was.
  - **Its test,** seen failing first: `ED_43_The_Debugger_fits_a_narrow_column` in EditMode's
    `RuntimeDebuggerTests` (there was no fit), which fits the window to 360 px and then to 1280 px.
  - **Tests:** EditMode 446 of 446. An editor change, so PlayMode didn't run: it stands at 1282 from FEAT-18.
  - **Next:** FEAT-12, the Debugger's Timeline view.

- 2026-11-05, **FEAT-18's two Profiler modules** (Decision 32), the last of three parts. FEAT-18 is done.
  - **The modules:** PlayForge Processes charts ProcessControl's and TaskSequence's ten counters, and PlayForge GAS
    the GAS's ten (`Editor/Debugger/ForgeProfilerModules.cs`). The Profiler finds them as it finds its own.
  - **The costliest processes:** while the Profiler records, a process's steps in a frame are timed and added up,
    and as the scene's ProcessControl ends the frame, its ten costliest go into the capture as the frame's metadata
    (`ForgeFrameCosts`, `ForgeProcessCost`): their IDs and times under one tag, the costliest first, and each
    one's type's name under the tag after, so a saved capture still names them. The Processes module's details
    list the selected frame's, in a table the Debugger's style sheet draws, its Process column the one that gives
    way in a narrow window. Off the Profiler, nothing is timed for it. Nothing is sorted or allocated: the ten are
    kept in order as each process comes, each block keeps its frame's time, and each type's name is encoded once.
  - **Docs:** GETTING_STARTED's Profiling section lists the markers, the counters and the modules.
  - **Its tests,** each seen failing first: EditMode's `FEAT_18_The_Profiler_modules_chart_PlayForges_counters` (the
    Profiler found no PlayForge module), and PlayMode's
    `FEAT_18_A_frames_costliest_processes_go_into_the_Profilers_capture`, which turns the Profiler on and finds a
    process that takes 2 ms a step first (it didn't compile, as nothing kept a frame's costs).
  - **Tests:** PlayMode 1282: 1275 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 445 of 445.
  - **Next:** FEAT-12, the ability graph and timeline.

- 2026-11-05, **FEAT-18's counters** (Decision 32), the second of three parts.
  - **What they count:** `ForgeCounters`, in a PlayForge category of its own, each a `ProfilerCounterValue`.
    ProcessControl's: Processes Running, Waiting and Created, and a frame's Registrations, Terminations, Steps, State
    Changes and Step Time, the step loops' time in nanoseconds. The GAS's: Mono Entities (GameRoot aside) and
    Runtime Entities, Effects Active, and a frame's Effects Applied, Effects Removed, Impacts, Activations, Refused
    Activations, Tag Compiles and Recomputes. TaskSequence's: Sequences Running, and a frame's Stages Started.
  - **Two kinds:** a frame's counts start from 0 each frame and count every world's work, a headless run's within
    the frame among them. A level holds until it changes. The process levels are the scene's, set as its
    ProcessControl's LateUpdate loop ends, so they can't drift. The others follow what starts and ends: an entity
    counts from its start to its end, a GAS brings Effects Active up to date at each step and takes its own out as
    it ends, and a sequence's run counts while it runs. A world dropped with its processes still in it leaves
    them counted. They start from 0 each session.
  - **Its test,** seen failing first: `FEAT_18_PlayForges_counters_count_its_work`, for each hub, in PlayMode's new
    `ProfilerCounterTests`, which reads each counter back through a `ProfilerRecorder`. The allocation suite holds:
    a count allocates nothing.
  - **Tests:** PlayMode 1281: 1274 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 444 of 444.
  - **Next:** the two Profiler modules, PlayForge Processes, with the frame's costliest processes, and PlayForge GAS.

- 2026-11-05, **FEAT-18's open markers** (Decision 32), the first of three parts: markers, counters, modules.
  - **The markers:** the step loop at each timing (`PlayForge.Update`, `PlayForge.LateUpdate` and
    `PlayForge.FixedUpdate`), a tag compile (`PlayForge.TagCompile`), an attribute's recompute
    (`PlayForge.Recompute`) and a stage's start (`PlayForge.Stage`), all in the Scripts category. A stage's marker
    holds only its start, its tasks up to the first that waits, since a marker ends where it began.
  - **A step marker per process type:** within ProcessStep, each process steps under `PlayForge.Step.` and its
    type's name, with its process ID as the marker's metadata, so the Profiler tells which process cost what. Each
    type's marker is made the first time a process of it steps, and a block keeps its own, so a step allocates
    nothing.
  - **The dependency:** the per-type marker is a `ProfilerMarker<int>`, and the counters to come are
    `ProfilerCounterValue`s, both from `com.unity.profiling.core`, which Unity 6's engine doesn't have. The package
    depends on its 1.0.2 now, which the editor ships, so it resolves offline. The PlayForge project's own manifest
    and lock take it too. Proving Grounds, which takes the package by path, resolves it as it opens.
  - **Its test,** seen failing first: `FEAT_18_The_step_loop_tags_recomputes_stages_and_each_type_show_in_the_Profiler`,
    for each hub, in PlayMode's `ProfilerMarkerTests`.
  - **Tests:** PlayMode 1279: 1272 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 444 of 444.
  - **Next:** FEAT-18's counters, then its two Profiler modules.

- 2026-11-05, **FEAT-10, step 6 of 6: worlds side by side, and the headless simulation.** FEAT-10 is done.
  - **Worlds side by side:** each world numbered its processes from 0, and a handler keeps what it handles by that
    number. GameRoot took what was registered with no handler, so a second world's first process was refused
    under the first world's (an `ArgumentException`: the key was already there). Every world draws its cache
    indices from one counter now, and `Created` still counts a world's own.
  - **A Mono handler:** a MonoBehaviour process ended what it handled through the scene's ProcessControl, by cache
    index. So one in a world of its own ran on, and the scene's process under that index ended in its place. It
    ends each through its relay, as a runtime process has since step 4.
  - **No scene:** a world of its own reaches for nothing of the scene's. What's registered there with no handler
    has none, where GameRoot took it and kept it for as long as the scene lasted, its world gone or not. With no
    packet it takes the default, parented nowhere, where a sequence's took the scene root's, which needed a
    GameRoot, so with none it threw. So a world of its own runs a sequence outside play mode with no scene at all.
  - **The headless simulation** (Proving Grounds' `Tests/Stretch`, the review's scenario): a brute, a duelist and a
    caster duel each pairing and mirror, 100 duels a pairing. Each duel is two runtime entities in a world of its
    own, and eight worlds step side by side, at 20 steps a second, all within one frame. A second blow lands as a
    critical hit by FEAT-8's chance to apply, and each side first strikes at a point within its cooldown, both
    drawn from a seeded `ForgeRandom`. The 600 duels take 171,590 world steps and about 3 s of wall time, and their
    numbers are the same in each of the gates' three orders.
  - **Its numbers,** a side's wins first: Brute v Brute 54-46, Brute v Duelist 82-17, Brute v Caster 98-2, Duelist
    v Duelist 45-53, Duelist v Caster 80-19 and Caster v Caster 51-49, with 4 drawn in all. The brute is too strong,
    and the duelist beats the caster: numbers like these are what the scenario is for.
  - **Is the order fair?** A first run, of 40 duels a pairing, had one mirror at 11-29. So 1,000 duels of each mirror
    ran in the private copy, twice, once with each side striking first. Every mirror came out between 48% and 52%,
    so the order the sides act in gives neither an edge worth seeing, and the scenario runs 100 duels a pairing.
  - **Its tests,** each seen failing first: `FEAT_10_Worlds_side_by_side_keep_their_own_time` and the scenario,
    `Stretch_Duels_with_no_scene_give_a_balances_numbers` (the index already there);
    `FEAT_10_What_a_Mono_process_handles_ends_with_it_in_its_own_world` (the handled process ran on);
    `FEAT_10_A_world_of_its_own_reaches_for_nothing_of_the_scenes` (GameRoot handled it); and EditMode's
    `FEAT_10_A_world_of_its_own_runs_a_sequence_with_no_scene_at_all` (it threw).
  - **Tests:** PlayMode 1277: 1270 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 444 of 444.
  - **Next:** FEAT-18's open markers, its counters and its Profiler modules (Decision 32), then FEAT-12.

- 2026-11-05, **FEAT-10, step 5 of 6: waits that ask once a tick.**
  - **The problem:** a sequence's conditional waits asked Unity's loop once a frame (`UniTask.WaitUntil` and
    `WaitWhile`), so a world of its own stepped in a loop never saw what they waited for change.
  - **The change:** `ForgeTick.WaitWhile` and `WaitUntil` (and `ForgeTickSource`'s) ask once a tick, on the tick in
    force as the wait begins, where it ticks; where it doesn't, they are UniTask's own waits, so a scene without
    ticks waits as it did. The physics step's timings stay Unity's. The sequence runtime's pause hold and its
    maintained stages, a chain's pause, the builder's Delay Until, Wait Until and Wait While, and the legacy ability
    path's maintained stages wait through them. The animator wait stays on Unity's loop, as an animator steps with
    it.
  - **Its tests:** `FEAT_10_A_wait_in_a_world_of_its_own_asks_at_each_step`, seen failing first (the wait never saw
    its condition change within the frame), and `FEAT_10_A_paused_sequence_in_a_world_of_its_own_goes_on_by_its_steps`,
    which held already: a paused process's run starts again on resume, inside the world's tick.
  - **Tests:** PlayMode 1273: 1266 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 443 of 443.
  - **Next:** step 6, several worlds side by side, and the headless simulation scenario.

- 2026-11-05, **FEAT-10, step 4 of 6: abilities in their caster's world.**
  - **The problem:** an ability's sequences registered with the scene's ProcessControl wherever its caster was, so
    in a world of its own they waited on Unity's frames and piled up in the scene's control. A runtime entity that
    fell ended whatever the scene's control held under its cache index, and ran on itself. The keep-alives waited
    on Unity's loop, so a process in a world of its own couldn't end between frames.
  - **The change:** `ProcessWorld.Of(process)` finds the world that holds a process by its relay, or the scene's.
    An ability's targeting and its sequence register in their caster's world, as Run Process, Run Sequence and
    Create Mono Process do, and activation asks that world whether it takes new processes. A fallen entity, of
    either hub, ends through its own relay. The keep-alives of the lazy runtime and Mono processes, the group and a
    synchronous sequence wait on `ForgeTick.Next`, which is Unity's next frame where nothing ticks, as before.
  - **Its test,** seen failing first: `FEAT_10_A_duel_in_a_world_of_its_own_ends_within_one_frame` (the scene's
    control held the 19 strikes' sequences): two runtime entities strike once a second, an ability with a
    cooldown, until the one half a second behind falls, nine seconds of the world's time within one frame, and the
    fallen one's process ends in its world. Its first draft expected the second to fall alone with both striking
    together, but a blow lands at its target's end of frame, so both fell in one step: a fact of the timing, which
    the half second's start takes into account.
  - **Tests:** PlayMode 1271: 1264 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 443 of 443.
  - **Next:** step 5, the polls still on Unity's loop: pause holds and maintained stages.

- 2026-11-05, **FEAT-10, step 3 of 6: a world's own tick and clock.**
  - **The problem:** a world of its own stepped in a loop got nowhere: its sequences waited on `ForgeTick`, one
    global tick that only the scene's ProcessControl advanced, or Unity's next frame with ticks off.
  - **The change:** `ForgeTickSource` is a tick: its waits, how many it has run, whether it ticks. The scene has one,
    `ForgeTick.Scene`, which ProcessControl's tick rate turns on as before, and each world of its own has its own,
    always ticking, `ProcessWorld.Tick`. `ForgeTick`'s statics read the tick in force, `ForgeTick.Current`: the
    scene's, or a world of its own's while it steps or moves a process, since a run can start as a process moves,
    between steps. `world.Step(deltaTime)` puts the world's clock in, which reads that time, its steps and their
    sum (`ProcessWorld.Elapsed`), ticks the world's tick, so what waits on it goes on, and steps Update and
    LateUpdate. `world.Step()` steps a world of its own by Unity's frame time.
  - **Its test,** seen failing first: `FEAT_10_A_world_of_its_own_keeps_its_own_time` (no `Step(float)`): a sequence's
    1 s delay in a world of its own runs out after about 60 steps of 1/60 s, all within one frame.
  - **Tests:** PlayMode 1270: 1263 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 443 of 443.
  - **Next:** step 4, abilities and effects in a world of their own.

- 2026-11-05, **FEAT-10, step 2 of 6: the plain world.**
  - **The change:** `ProcessWorld`, a plain class, holds what ProcessControl's bookkeeping held: the process tables
    and step groups, the watches, the groups, which processes each GAS owns, the callbacks, the control state and its
    transitions, registration of runtime, Mono and sequence processes, and stepping, the ticks among it. Its code moved
    from ProcessControl.cs nearly as it was, the Mono branches with it, since they run only for a Mono process.
  - **ProcessControl** stays the public MonoBehaviour. It owns a world (`ProcessControl.World`) and steps it from
    Update, LateUpdate and FixedUpdate. It keeps what needs a GameObject or a scene: the inspector fields, which the
    world reads, the bootstrap's Instance, DontDestroyOnLoad and Destroy, scene collection, and the static API. It
    forwards its instance API, and a private `StepProcesses` and `DefaultTransitions` stay for the golden tests and the
    harness's oracle, which reach them by name.
  - **A world of its own:** `new ProcessWorld()` is ready to take processes, `ProcessRegistration.In(world)` registers
    into it, and `world.Step()` steps it once, as a frame: Update, then LateUpdate. A block and a relay hold their world
    (`ProcessControlBlock.World`, `ProcessRelay.World`; `Control` is the world's ProcessControl, or null). A
    ProcessControl's handler check compares by reference, where a cast threw for any other handler.
  - **Its test,** seen failing first: `FEAT_10_A_world_of_its_own_steps_what_it_holds_as_its_owner_steps_it` in
    PlayMode's new `ProcessWorldTests` (no world class): a world made with new takes a runtime probe, which the scene's
    frames don't step and each of its owner's steps does, once.
  - **Tests:** PlayMode 1269: 1262 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 443 of 443.
  - **Next:** step 3, a world's own tick and clock.

- 2026-11-05, **The Battle lab,** at the user's asking: a full battle to watch, two sides of many fighters with
  projectiles, spells, area effects, summons, knockback and stuns, lasting well past 15 seconds, with a health bar over
  each fighter and boxes over it for its good and bad effects.
  - **Proving Grounds 8d66006:** `Tests/Labs/BattleLab.cs` sets Blue and Red against each other, eight a side: two
    knights (Cleave, whose knockback is a cue, and a stunning Shield Bash), two archers (arrows and poison darts), two
    mages (a fireball that bursts over an area and burns, and a frost nova that chills), a cleric (Mend, and a Blessing
    that regenerates and fortifies) and a summoner (imps with a lifetime, and shadow bolts). It's built from
    PlayForge's parts: abilities with mana costs and cooldowns that a stun refuses, armor as a worker, the projectile
    process from a pool, deaths through the death watcher. Each fighter takes the nearest foe, closes to its range and
    casts what's ready, and the sides take turns to act first, frame by frame. `BattleView.cs` draws each health bar,
    the effect boxes (good from the bar's left, bad from its right), a scoreboard, a legend, and rings for bursts,
    novas, heals, summons and falls. Its scene, `Battle Lab.unity`, plays it at 60 frames a second through the lab
    runner, which gains Show Bodies, Show Cards and Framing for a lab that draws its own. Watched there, each battle
    takes a new seed, so either side can win.
  - **Its round,** `BattleLabTests.Lab_A_battle`, plays a battle to its end and checks it lasts at least 15 s, one side
    wins, and every kind of blow lands. Its seed's battle: Blue won after 32 s with 3 of 8 standing, after about 216
    shots, 50 area hits, 23 knockbacks, 5 stuns, 12 imps, 13 heals and 6 blessings. Blue, enlisted first, had acted
    first every frame, an edge the alternating order took away.
  - **Tests:** PlayMode 1268: 1261 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 443 of 443.
  - **Next:** FEAT-10's step 2, the plain world.

- 2026-11-05, **FEAT-10, step 1 of 6: a process reaches the control that holds it.** The headless split (Decision
  30) goes in six steps, each with its red test and the gates:
  1. A process reaches the control that holds it.
  2. A plain `ProcessWorld` class holds the bookkeeping, which ProcessControl wraps, with a hook for what needs
     Unity's objects.
  3. A world's own tick and clock, with each sequence waiting on its own world's tick.
  4. Abilities and effects in a world of their own, stepped in a loop inside one frame.
  5. The waits on Unity's loop (pause holds, maintained stages, the keep-alives) made tick-aware.
  6. Several worlds side by side, and the headless simulation scenario.
  - **Step 1:** `ProcessControlBlock.Control`, the control that registered the process. The relay
    (`ProcessRelay.Control`), the block's state changes and unregistering, a watch, a runtime process's ending of what
    it handles (GAS-52's path), a group's watch on its members and Run Process go through it. They went to
    `ProcessControl.Instance`, whichever control held the process, and `WatchProcess` kept a watch in Instance's
    table.
  - **Its tests,** seen failing first: the three `FEAT_10_*` tests in PlayMode's new `ProcessOwnerTests`: a relay, a
    watch and a runtime entity's ending, each while another control is Instance, as a new scene's is while the old
    one goes.
  - **Tests:** PlayMode 1266: 1259 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 443 of 443.
  - **Next:** step 2, the plain world.

- 2026-11-05, **The labs' scenes (Decision 33).** Each of the eight labs has a scene in Proving Grounds that plays
  its bot's rounds to watch.
  - **Proving Grounds fa650f8:** `Tests/Labs/LabScene.cs`, the runner: it finds the bot's `[UnityTest]` rounds,
    builds each one's world through the test's own setup, steps its body, and ends the world with the harness's new
    `EndWorldOutsideTests` (the test's teardown reads the runner's result). `Tests/Editor/LabSceneBuilder.cs` makes the
    scenes, by `-executeMethod`, with no menu item, as ED-19 keeps menu items under Window/PlayForge. A round that
    takes a hub plays on the Mono one, so its entities have a place in the scene.
  - **Its test:** `LabScenesTests.Lab_Each_scene_plays_its_bots_rounds`, for each lab, loads the scene, plays it as
    fast as it can, and checks every round passes there as in the lab's tests.
  - **Tests:** PlayMode 1263: 1256 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 443 of 443.
  - **Next:** FEAT-10's headless run.

- 2026-11-05, **ED-42 and FEAT-8's placement: Clear gives a new asset's values, and Chance to Apply moves to
  Requirements.** The work parked at the stop below, picked up at the user's word.
  - **ED-42:** a section's Clear set each field to its type's default, 0 or the first choice. Clearing an effect's
    Impact set its affiliation to Use Affiliation List with none listed and its Chance to Apply to 0, so the effect
    never landed. A cleared field now takes a new asset's value, read from a new asset of the target's kind that no
    one sees, and a cleared list still empties.
  - **FEAT-8's placement,** as the design asks: Chance to Apply sits in Requirements, above its tables, since it
    gates the whole application, and Requirements' summary says a chance short of 1. Impact's Import and Clear take
    the impact's own fields but the chance. The Tags summary counts the abilities granted, as in "Grants
    Status.Burning, 1 ability".
  - **Their tests,** seen failing first: EditMode's `ED_42_Clearing_a_section_gives_its_fields_a_new_assets_values`
    (Use Affiliation List for Always Allow) and the updated
    `FEAT_8_The_Effect_inspector_edits_the_chance_to_apply_and_granted_abilities` (no chance field in Requirements).
  - **Tests:** EditMode 443 of 443. An editor change, so PlayMode didn't run: it stands at 1255 from FEAT-11.
  - **Next:** FEAT-10's headless run, from the shared notes' map; then FEAT-18's counters and modules, and FEAT-12.

- 2026-11-05, **Stopped here, at the user's word.** FEAT-11 is in (ed60c62, tour f09d9f9), and the docs commit
  664a9ab adds EDITOR_STYLE's Debugger section (its narrow column) and Decision 32 with FEAT-18's widened row.
  - **Parked:** ED-42, found while moving FEAT-8's Chance to Apply to Requirements as the design asks. Its two
    EditMode tests, `ED_42_Clearing_a_section_gives_its_fields_a_new_assets_values` and the updated
    `FEAT_8_The_Effect_inspector_edits_the_chance_to_apply_and_granted_abilities`, were seen failing (Use Affiliation
    List for Always Allow; no chance field in Requirements). Proving Grounds is back as committed; the tests and the
    fix wait in the shared notes as a patch and a script.
  - **Next:** apply the two, run them green and the EditMode suite, then FEAT-10 (a map of ProcessControl's
    couplings is in the shared notes' `feat10-processcontrol-map.md`), FEAT-18's counters and modules, and FEAT-12.

- 2026-11-05, **FEAT-11: targeting input.** Decision 30's default: targeting reads an input interface, with legacy
  and Input System defaults.
  - **The problem:** the five click-targeting tasks (Select Position, Direction, GAS Target, GAS Or Position, GAS
    Target In Range) read the Input Manager's mouse and `Camera.main` themselves. A game on the Input System alone,
    with a camera, a reticle or a controller's ray of its own, or a test couldn't stand in, and a scene with no main
    camera threw at the press.
  - **The change:** they read `ForgeInput.Current`, an `IForgeInput` of whether a pointer button went down this
    frame and the ray the pointer aims along, through `AbstractTargetingAbilityTask.WaitForPointerPress`. A game puts
    its own in with `ForgeInput.Use`. The default is the Input System's pointer (`InputSystemPointerInput`, a mouse,
    a pen or a touch) where its package is in and its input handling on, or else the Input Manager's mouse
    (`LegacyPointerInput`), each aimed through the camera it's given or `Camera.main`. With no camera a press aims
    nowhere, and a warning says so.
  - **The Input System, optionally:** the runtime's asmdef references `Unity.InputSystem` by name, with a version
    define, `PLAYFORGE_INPUT_SYSTEM`, as the Demos sample does. Proving Grounds, which has no Input System, compiles
    without it, and the Input System default was compile-checked against the PlayForge repo's own copy of the
    package. No test runs it: Proving Grounds would need the package.
  - **Its tests,** seen failing first: EditMode's `FEAT_11_The_runtime_reads_input_only_through_ForgeInput`, a
    source scan beside FEAT-13's that found the tasks' 15 reads, and PlayMode's
    `FEAT_11_Click_targeting_reads_the_games_input` in the new `TargetingInputTests` (10 of 10: each task, from
    either hub, waits for the game's press and targets along its aim). A third test,
    `FEAT_11_The_Input_Managers_mouse_aims_through_a_camera`, covers the default.
  - **Next:** FEAT-10's headless run, then the headless simulation.
  - **Tests:** PlayMode 1255: 1248 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7;
    EditMode 442 of 442.

- 2026-11-05, **FEAT-1: gameplay cues, and the sequence-driven boss.** The fifth stretch scenario (Decision 30).
  - **FEAT-1:** `GameplayEffect.Cues`, a list of `AbstractCueHandler`, as an effect lists its workers. Its virtual
    hooks are bursts (On Applied, On Ticked, On Removed) and a lasting cue's Begin and End, each given a `CueContext`
    of the target, the spec, the ticks and where the target is. The shelf plays them (`EffectSystemComponent.Cue`): a
    burst as an effect lands, ticks or comes off, and Begin and End as a durational or infinite one goes on and off.
    A restore ends and begins the lasting ones and plays no burst (FEAT-9). `CameraShakeCue` goes through the
    `ICameraShaker` the game gives `ForgeCues.Shaker`, and `SoundCue` and `PrefabCue` sit beside it. The Effect
    inspector's new Cues section, an effect's own after Impact, edits them, so ED-37's order and EDITOR_STYLE's Q9
    sentence name it.
  - **The sequence-driven boss** runs one TaskSequence whose stages are the fight's phases: it slams until it's worn
    (an injection breaks the opening's repeat), branches to a meteor rain while the arena's pillars stand or stands
    exposed once they're down, and ends in a fury, which the arena's alarm can call early by jumping to its stage.
    Each slam, and the rain as it lands and on each tick, shakes the camera through the game's shaker. It held at its
    first run: nothing past FEAT-1 was missing, and only its count of shakes was wrong, as the rain shakes as it
    lands as well.
  - **Their tests,** seen failing first: `FEAT_1_A_shake_cue_goes_through_the_games_shaker` and
    `FEAT_1_A_lasting_cue_lasts_as_its_effect_does` in PlayMode's new `GameplayCueTests` (4 of 4), and EditMode's
    `FEAT_1_The_Effect_inspector_edits_its_cues`.
  - **Next:** FEAT-11's input binding, then FEAT-10's headless run.
  - **Tests:** PlayMode 1244: 1237 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    441 of 441.

- 2026-11-05, **FEAT-4: ability triggers, and the beyond combat scenario.** The fourth stretch scenario (Decision 30).
  - **FEAT-4:** `AbilityDefinition.Triggers`, each an `AbilityTrigger` of a source (Gameplay Event, Tag Added) and a
    tag. `SendGameplayEvent(tag, data)`, on the ability system and on the hubs through the interface, activates each
    known ability an event under its trigger's tag sets off, with the event's data, and `OnGameplayEvent` hears it. The
    ability system hears a tag gained (FEAT-3) and fires its triggers at once, one at a time, so a triggered ability
    that grants tags doesn't nest. The Ability inspector's Activation section edits them.
  - **Beyond combat** grows crops through their stages, faster when watered (FEAT-7), blooming as they ripen through a
    Tag Added trigger; a smelter makes an ingot each tick of its timer and takes another order by extending it; and a
    dialogue gate opens as a greeting event sets off the keeper's reply and the player reads the letter, refused for its
    requirements until then (FEAT-17). It held at its first run: nothing past FEAT-4 was missing.
  - **Their tests,** seen failing first: the two `FEAT_4_*` tests in PlayMode's new `AbilityTriggerTests` (4 of 4) and
    EditMode's `FEAT_4_The_Ability_inspector_edits_its_triggers`.
  - **Next:** the sequence-driven boss, with FEAT-1's cues.
  - **Tests:** PlayMode 1232: 1225 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    440 of 440.

- 2026-11-05, **FEAT-2 and FEAT-21: SetByCaller magnitudes, and the ability crafting scenario.** The third stretch
  scenario (Decision 30).
  - **Ability crafting** builds spells as the game runs from runes, their casts made with `TaskSequenceBuilder`, caches
    each recipe whatever its runes' order, and casts them through one spell effect whose damage the caster sets.
  - **FEAT-2:** `SetByCaller(tag, magnitude)` on a `GameplayEffectSpec` or a `ProcessDataPacket`, which the new
    `SetByCallerScaler` reads, or its Default. Each spec an ability's data makes takes its magnitudes as it's made:
    Apply Effects, Apply Remove Effects, the task helpers, and the cost and cooldown (`ApplyUsageEffects(data)`). A
    contained or overflow effect takes its container's, and a save keeps them (`EffectSave.SetByCaller`).
  - **FEAT-21,** which the scenario found: Run Sequence ran only a sequence a static method made, by name, so a cast
    built in code couldn't run in an ability. `RunSequenceTask.Definition`, a definition made in code, runs a fresh
    sequence each activation, with the activation's own data.
  - **Their tests,** seen failing first: the three `FEAT_2_*` tests in PlayMode's new `SetByCallerTests` (6 of 6), and
    `FEAT_21_Run_Sequence_runs_a_sequence_made_in_code` (2 of 2).
  - **Next:** beyond combat, with FEAT-4's triggers.
  - **Tests:** PlayMode 1222: 1215 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    439 of 439.

- 2026-11-05, **FEAT-20 and GAS-59: mirror clones and chain reactions.** The first two of the review's stretch
  scenarios (Decision 30), in Proving Grounds' new `Tests/Stretch`, category Stretch.
  - **Mirror clones** copy a mage's state onto a new runtime GAS of another kind: its hurt, its haste with its time
    left, a witch's curse, Fireball at level 2, a staff, a loose tag and its level, after which the two go their own
    ways. It found FEAT-20 and GAS-59.
  - **FEAT-20:** a copy took FEAT-9's capture and restore, which need a catalog naming every asset and IDs for every
    entity. `ForgeSave.Copy(from, onto)` names assets and entities for the session (`SessionNames`, `SessionIds`,
    through an internal `IAssetNames` that the catalog implements too), and a save keeps the entity's own level under
    no key, which loads onto the target's own, where it stayed behind under its kind's tag.
  - **GAS-59:** `TrySetLevel` and `TryModifyLevel` set the level before the max, so a level raised past the old max at
    once was held at it. The max goes first.
  - **Chain reactions** run a five-deep chain of contained effects in order, cut a loop of two where it comes back,
    and send an arc round a ring of four entities once: each entity's guard refuses the shock coming back while its
    own still applies (GAS-28). They held at their first run.
  - **Their tests,** seen failing first: `Stretch_A_mirror_clone_takes_its_originals_state` and
    `GAS_59_A_level_raised_past_its_old_max_at_once_takes_it` (4 of 4).
  - **Next:** ability crafting, with FEAT-2.
  - **Tests:** PlayMode 1210: 1203 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    439 of 439.

- 2026-11-05, **FEAT-18 and FEAT-17: profiler markers, and activation by asset or tag with a reason.** The first of the
  stretch work (Decision 30).
  - **FEAT-18:** `ForgeProfiler`'s five markers, in the Scripts category: `PlayForge.ProcessStep` around a process's
    step in `ProcessControlBlock.Step`, `PlayForge.GASStep` and `PlayForge.EndOfFrame` around `GASCore.Tick` and
    `EndOfFrame`, `PlayForge.ApplyEffect` around `EffectSystemComponent.ApplyGameplayEffect`, and
    `PlayForge.ActivateAbility` around the ability system's activation. GETTING_STARTED.md lists them.
  - **FEAT-17:** `TryActivateAbility(ability)` and `TryActivateAbility(tag)`, the first ability by slot whose asset or
    context tag is the tag or under it, and each form, the request's among them, can give an `out ActivationRefusal`:
    its `EActivationRefusal` reason, and a caster check's name for `Rule`. `AbilitySpec.SourceRefusal` names a source
    requirement, a cooldown, a cost or a death; the activation names the rest. `OnAbilityRefused` tells each refusal; a
    request the buffer retries isn't told again. ABILITIES.md says so.
  - **Their tests,** seen failing first: `FEAT_18_PlayForge_shows_in_the_Profiler` (2 of 2), and
    `FEAT_17_An_ability_activates_by_its_asset_or_its_tag` and `FEAT_17_A_refused_activation_says_why` (4 of 4).
  - **Next:** the stretch scenarios, starting with mirror clones.
  - **Tests:** PlayMode 1200: 1193 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    439 of 439.

- 2026-11-05, **The Combat lab, and FEAT-19.** The eighth and last lab, so Phase 5's labs are all green (Decision 30).
  - **The lab** (`Tests/Labs/CombatLab.cs`, category Lab): a training dummy, and every way an effect lands on it, from
    a trainer, on a clock of a quarter second a frame. A health bar reads the dummy's Health breakdown by retention
    group: what it has of its own, what effects add while they last, and the damage it took. Five rounds: each
    delivery, the ten re-application cases (each policy with what follows it), each stacking policy, a stack count
    feeding a change, and a bout on whose every frame the bar adds up to the Health and its Base. Every claim the
    rounds test, from EFFECTS.md, held at the first run.
  - **FEAT-19,** which the lab found missing: nothing built in read an effect's stack count, so a change by the stack
    took a scaler written for it, as Proving Grounds wrote twice. `EffectStacksScaler` reads the count of the effect's
    own container, 1 for one that doesn't stack. Seen failing first:
    `FEAT_19_The_Effect_Stacks_scaler_reads_the_effects_stack_count`.
  - **Next:** the stretch scenarios, with FEAT-18 and FEAT-17 first. The labs' scenes, which Decision 30 brings in once
    their bots are green, and whether they ship in Samples, are the user's call.
  - **Tests:** PlayMode 1194: 1187 pass, 0 fail and 7 planted are skipped, in three orders (the Combat lab's 10 among
    them). Planted 7 of 7; EditMode 439 of 439.

- 2026-11-05, **The Tactics lab, and ED-41.** The seventh lab (Decision 30).
  - **The lab** (`Tests/Labs/TacticsLab.cs`, category Lab): a knight and an archer against two goblins, a turn at a
    time, on a clock that stands still until a turn ends and then moves a whole turn in one frame. Slash has a
    two-turn cooldown, Poison Arrow bites as each of three turns ends, Guard turns a slash aside for the turn (FEAT-8's
    immunity), and Mend heals. Between turns a squad is saved, each unit to JSON through `ForgeSave` (FEAT-9); a
    `Squad` is the game's IDs for its units, so a goblin's poison finds its archer again. Six rounds: a poison's
    turns, a cooldown's, a guard's, a load that takes the battle back a turn and replays it alike, a save carried on
    by a fresh squad as a new session, and a battle saved and loaded every turn, which goes as one played straight
    through.
  - **ED-41,** which the lab found: an ability's cost and cooldown apply only from a stage set to Apply Usage Effects,
    and nothing said so when none was, so the lab's Slash had a cooldown that never came on. The Ability inspector
    warns under Cooldown while no stage applies them, and the Problems view lists it (`UsageEffectsRule`). Seen
    failing first: `ED_41_The_Ability_inspector_warns_of_usage_no_stage_applies` and
    `ED_41_A_cost_or_cooldown_no_stage_applies_is_a_problem`.
  - **Next:** the Combat lab.
  - **Tests:** PlayMode 1182: 1175 pass, 0 fail and 7 planted are skipped, in three orders (the Tactics lab's 12
    among them). Planted 7 of 7; EditMode 439 of 439.

- 2026-11-04, **FEAT-9, save and load.** For the Tactics lab (Decision 30).
  - **The API:** `ForgeSave.Capture(gas, catalog, ids)` takes an entity at rest into a `ForgeSnapshot` of plain
    serializable types: each attribute's permanent commits by retention group (`CachedAttributeValue.GetCommits`), its
    levels, the abilities it knows of its own by slot, its items with their stacks, worn or not, its effects with their
    origin, stacks and clock (`AbstractEffectContainer.CaptureClock`, each stack's own for independent durations), and
    its loose tags and time scale. `ForgeSave.Restore(gas, snapshot, catalog, ids)` interrupts running abilities, lets
    go of what the entity has with nothing told, and puts back the save's: abilities in their slots, items, effects,
    levels and commits. A worn item's effects come from the save, so wearing it applies none, and an ability an effect
    or item lends comes back with it. Then every attribute recomputes, the tags compile once, and the new `OnRestored`
    fires. A load inside the entity's step is refused (`GASCore.Stepping`).
  - **The catalog:** a `ForgeAssetCatalog` names a save's assets by IDs that last; its Add Every PlayForge Asset names
    each by its GUID. An `IForgeEntityIds` names the other entities whose effects are on the entity; without one, such
    an effect comes back as the entity's own.
  - **Not saved:** Local Data, sides, and what an effect captured as it landed (`Documentation/SAVING.md`).
  - **Their tests,** seen failing first: the five `FEAT_9_*` tests in PlayMode's new `SaveLoadTests` (10 of 10). Four
    more there, and EditMode's `FEAT_9_Add_Every_PlayForge_Asset_names_each_by_its_GUID`, cover stacks with their own
    times, lent abilities with loose tags and time scale, an effect from another entity, and a load inside a step.
  - **Next:** the Tactics lab.
  - **Tests:** PlayMode 1170: 1163 pass, 0 fail and 7 planted are skipped, in three orders (SaveLoadTests' 18 among
    them). Planted 7 of 7; EditMode 437 of 437.

- 2026-11-04, **The Summoner lab.** The sixth lab (Decision 30).
  - **The lab** (`Tests/Labs/SummonerLab.cs`, category Lab): a summoner calls imps, each a runtime entity it handles
    (`Register(imp).HandledBy(summoner)`), so each ends as the summoner does. An imp lasts its lifetime, an effect
    whose removal marks the imp dead; the summoner's bond heals each imp a tick a second, with an ongoing requirement
    on its source, the summoner's Standing tag, which each tick asks as it comes due. Four rounds, a ritual among
    them. Its bot passed at its first run: nothing in the framework was missing.
  - **Next:** FEAT-9, save and load, for the Tactics lab.
  - **Tests:** PlayMode 1152: 1145 pass, 0 fail and 7 planted are skipped, in three orders (the Summoner lab's 8 among
    them). Planted 7 of 7; EditMode 436 of 436.

- 2026-11-04, **FEAT-14, GAS-58 and the Roguelite lab.** The fifth lab (Decision 30).
  - **FEAT-14, item stacking:** a give fills the item's stack first, up to `MaxStackSize` (`ItemDefinition.StackSize`
    is it or 1), and a full stack opens another only with Allow Duplicate Items. `GiveItem(item, count)` gives one at
    a time through the old path, which now stacks first, and returns how many it took; `RemoveItem(item, count)` takes
    stacks off from the last slot. `ItemSpec.Stacks` holds the count, which the container's `ItemStacks` reads and
    `ItemStacksScaler` reads for an effect the item applied; as the count changes, the equipped item's effects
    re-register (`EffectSystemComponent.RefreshEffectsFrom`), applied once. The Item inspector draws Max Stack Size.
    GAS-51 is fixed by it.
  - **GAS-58,** which the lab found: a lasting effect's magnitude was read as its modifier registered, so the Crown of
    Ages kept its first level's bonus as it levelled. The GAS core hears its level system (`OnLevelChanged`,
    `OnMaxLevelChanged`) and re-reads the effects whose origin's level changed.
  - **The lab** (`Tests/Labs/RogueliteLab.cs`, category Lab): a whetstone that stacks, a three-piece Ember set whose
    bonuses follow its tag through `TagCache.Listen` (FEAT-3), tomes that stack and teach twice under the Library aura
    (a `ConditionalScaler`), and the Crown of Ages, which levels. Five rounds, a run among them.
  - **Their tests,** seen failing first: five `FEAT_14_*` tests in PlayMode's new `ItemStackingTests` (9 of 9),
    `FEAT_14_The_Item_inspector_edits_its_stack_size`, and `GAS_58_An_items_effect_rescales_as_the_item_levels`.
  - **Next:** the Summoner lab.
  - **Tests:** PlayMode 1144: 1137 pass, 0 fail and 7 planted are skipped, in three orders (the Roguelite lab's 10
    among them). Planted 7 of 7; EditMode 436 of 436.

- 2026-11-04, **The Bullet heaven lab, and PC-33.** The fourth lab (Decision 30).
  - **The lab** (`Tests/Labs/BulletHeavenLab.cs`, category Lab): a wizard and a horde of runtime enemies on a grid, on
    a clock of 1/64 s a frame. Its bolts are the framework's `ProjectileMonoProcess`, drawn from a pool the lab writes
    as a game would (Decision 23); its aura burns what's in reach; an enemy dies through the framework's death
    watcher and blasts its neighbours, which cascades. Four rounds: 10,000 bolts at 500 enemies, the aura over 500,
    a cascade through one cluster of 200 that stops at a gap, and a storm of all three.
  - **PC-33,** which the lab found: the Mono wrapper handed a finished object back through the object's own
    `Instantiator`, which a clone never has, so a pool's object was destroyed and pooling couldn't work. The wrapper
    keeps the instantiator that made the object. Seen failing first: `PC_33_A_pooled_object_goes_back_to_its_pool`,
    on a template's instantiator and on the handler's.
  - **What the review expected it to find:** PC-3 was fixed in Phase 1, and the pooling stub went in PC-24. Churn: the
    10,000-bolt round takes about half a second of batch time, so `StepGroup.Remove`'s linear search (the review's
    O(n) unregister) doesn't show at this scale, and no row is opened for it. The lab's own area loops are linear.
  - **The lab's own two mistakes,** for the record: its wizard was observed while its horde wasn't, so the invariants
    saw damage dealt with none received (both are unobserved now); and its aura used Replace Existing alone, which
    leaves a container's time alone as EFFECTS.md says, so it needed Refresh Duration.
  - **Next:** the Roguelite lab, with FEAT-14 (item stacking).
  - **Tests:** PlayMode 1123: 1116 pass, 0 fail and 7 planted are skipped, in three orders (Bullet heaven's 4 among
    them). Planted 7 of 7; EditMode 435 of 435.

- 2026-11-04, **TS-7, stages that branch, and the Duel lab.** The third lab (Decision 30).
  - **TS-7's shape is my call, not the user's:** Decision 30 doesn't cover it, and the review says only that there's
    no on-hit or on-miss stage. A stage's `Condition` (`AbilityStageCondition`: On, Tag, When Missing) looks in the
    activation's data or the caster's or target's tags; the compiler emits a stage with one as a `BranchStage` of one
    case, so it runs or is skipped with its own settings. Two stages that name one tag branch. `ApplyEffectsAbilityTask.
    RecordLandedAs` tags the data as an effect lands (the review's packet as a blackboard). Flat, as nested stages would
    be a recursive serialized type.
  - **The claim follows the branch:** the runtime counted a branch as critical if any case was (`HoldsCriticalStage`),
    but its critical section ended only as a critical stage ended, so a skipped critical stage held the claim to the
    end. It ends too as the run starts a stage past its last critical one. At the skip itself the run still stood on
    the skipped stage, and the Duel lab found that a buffered special fired then didn't see the jab's hit recovery to
    cancel. So a committed whiff goes before the hit's recovery.
  - **The lab** (`Tests/Labs/DuelLab.cs`, category Lab): two fighters on a clock of 1/64 s a frame, exact in floats. A
    jab and a fireball, each a critical startup and active frames, a hit-stop on a hit that stops both fighters'
    clocks (FEAT-7), a committed whiff on a miss, and a hit recovery that a special cancels and that blocks normals
    (FEAT-5). A guard is FEAT-8's immunity. A press while busy waits 12 frames in the buffer (FEAT-6), and the window
    waits out a hit-stop. Its bot plays seven rounds, a hit-confirm among them.
  - **The MOBA lab** casts under Refuse now: under its heroes' Queue, FEAT-6 buffered a spell its channel blocked.
  - **Its tests,** seen failing first (10 of 10 PlayMode, 1 of 1 EditMode, in FEAT-6's gate run): three `TS_7_*` tests
    in PlayMode's new `BranchingStageTests`, two over hit and miss, and `TS_7_A_stage_edits_its_condition`.
  - **Next:** Bullet heaven, the fourth lab.
  - **Tests:** PlayMode 1117: 1110 pass, 0 fail and 7 planted are skipped, in three orders (the Duel lab's 14
    among them). Planted 7 of 7; EditMode 435 of 435.

- 2026-11-04, **FEAT-6: the input buffer.** The Duel lab's first feature (Decision 30).
  - **The buffer** (`AbilitySystemComponent`): the Queue policy's plain queue becomes a list of one slot per ability.
    A press that can't start, as while another Queue ability holds the claim or a running stage blocks it (FEAT-5),
    takes its ability's slot, replacing an earlier press, with its window. As a claim is released, and each step
    after the effects tick, the buffer sorts best first (`BufferPriority`, then the newest) and fires the first that
    can start; each step then counts the windows down by the entity's clock, so a paused entity or a hit-stop holds
    them. `AbilityDefinition.BufferWindow` (0.3 s by default, Arcade Fighter's attack buffer) and `BufferPriority`;
    `CreateActivationRequest`'s `bufferWindow` names a request's own. A window of 0 refuses.
  - **What changed:** a Queue request waited for good and fired in the order asked; a press again queued again.
    `FEAT_5_A_stage_blocks_the_abilities_it_names` asks under Refuse now, as under Queue a blocked request waits.
  - **The Ability inspector:** Buffer Window and Buffer Priority in Activation, after When Busy.
  - **Its tests,** seen failing first (10 of 10 PlayMode, 1 of 1 EditMode): five `FEAT_6_*` tests on each hub in
    PlayMode's new `InputBufferTests`, and `FEAT_6_The_Ability_inspector_edits_the_buffer`.
  - **Next:** TS-7, then the Duel lab.
  - **Tests:** PlayMode 1117: 1110 pass, 0 fail and 7 planted are skipped, in three orders (the Duel lab's 14
    among them). Planted 7 of 7; EditMode 435 of 435.

- 2026-11-04, **The MOBA hero kit lab, GAS-57, and While active heard at once.** The second lab (Decision 30).
  - **The lab** (`Tests/Labs/MobaLab.cs` in Proving Grounds, category Lab): four heroes of four abilities, on a clock
    of half a second a frame. Mana pays for each cast; a hero's Cooldown Reduction shortens its cooldowns (a game's
    scaler on the cooldown's duration); armor and magic resist each let through 100 / (100 + resist) of a blow of
    their kind (a game's inline worker, by impact type); lifesteal is the framework's relative-operation worker. A
    silence, a stun, a cleanse (FEAT-8), and a channel that a stun ends (While active), a blink cancels (Cancelled
    By) and that blocks the caster's other spells (Blocks). Its bot plays nine rounds, the last two on two for eight
    seconds.
  - **GAS-57,** which the lab found: the relative-operation worker read the impacted attribute's value, never its
    Relative Attribute, so lifesteal healed the attacker by its own health times the blow. Seen failing first:
    `GAS_57_A_relative_worker_reads_its_relative_attribute`.
  - **While active, heard at once** (FEAT-5), which the lab found too: checked at the stunned entity's next step, a
    stun let a channel's tick that came due earlier in the frame land. The ability system checks as it hears a tag
    change, as tag events are heard, guarded so a change its own interrupt sets off waits for the check under way,
    and at each step as before. Seen failing first: `FEAT_5_A_stun_ends_a_channel_before_its_next_tick`.
  - **Next:** the Duel lab, with FEAT-6 (the input buffer) and TS-7 (critical sections that branch).
  - **Tests:** PlayMode 1083: 1076 pass, 0 fail and 7 planted are skipped, in three orders (the MOBA lab's 18 among
    them). Planted 7 of 7; EditMode 433 of 433.

- 2026-11-04, **FEAT-5: cancel and block abilities by tag, and While active.** The MOBA hero kit's feature, ahead of
  the lab (Decision 30).
  - **Cancelled By and Blocks** (`AbilityBehaviour` for the ability, `AbilityTaskBehaviourStage` for a stage): tags
    that name abilities by asset or context tag, or a child of one (`AbilityTags.Names`). A stage that names none
    takes the ability's, and so does targeting, before the first stage. As an ability activates, each running one in
    a stage it cancels is interrupted first; a critical stage is committed, so it's skipped. While a running stage
    blocks an ability, `CanActivateAbility` says no and the activation is refused under every policy: retrying a
    blocked request waits for FEAT-6's input buffer.
  - **While active** (`AbilityTagRequirements.WhileActiveRequirements`): the caster's tags while the ability runs.
    The ability system hears every tag change (`TagCache.TagChanged`) and, each step after the effects tick, checks
    the running abilities again, interrupting one that fails; it's checked as the ability is asked to activate too.
    An interrupt this way ends a critical stage as well, as an interrupt by slot does.
  - **The Ability inspector:** Cancelled By and Blocks in Stages, before the stage list, and each stage's own in its
    drawer; a While active row in the tag requirements table, between Caster and Target.
  - **Its tests,** seen failing first (10 of 10 PlayMode, 2 of 2 EditMode): five `FEAT_5_*` tests on each hub in
    PlayMode's new `CancelAndBlockTests`, `FEAT_5_The_Ability_inspector_edits_cancel_block_and_While_active`, and
    `ED_37_The_requirements_table_opens_a_cell_to_edit_it`, which now expects the While active row.
  - **Next:** the MOBA hero kit lab.
  - **Tests:** PlayMode 1061: 1054 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    433 of 433.

- 2026-11-04, **FEAT-8 done: abilities granted by effects, and the chance to apply.**
  - **Granted Abilities** (`GameplayEffect.GrantedAbilities`): a lasting effect gives its target each ability as it
    lands, at its origin's level, as an item gives its ability at the item's; the shelf notes the slot each took and
    takes it back as the effect comes off, interrupting it if it runs (`RemoveAbilityAt`). An ability the target can't
    take isn't lent, so it isn't taken back either (GAS-48). A replace by another effect under the same tag swaps the
    lent abilities too. An instant effect lends none. The Effect inspector draws them in Tags, after Granted Tags,
    and the Tags summary says "Grants Smite".
  - **Chance to Apply** (`GameplayEffectImpact.ChanceToApply`, a scaler magnitude, 1 by default): each application
    draws once from `ForgeRandom` after the requirements and immunities pass, and a miss is refused. A chance of 1
    or of 0 draws nothing, so effects without one leave the random numbers as they were. A contained effect draws its
    own. The inspector draws it in Impact, after Affiliations (who the effect may change, then how likely it lands),
    and the Impact summary says "30% chance".
  - **The lab:** two rounds more. A jagged strike lacerates on a roll from a seeded source, the bot predicting each
    draw, and a blessing lends the caster Smite until it runs out.
  - **Its tests,** seen failing first (10 of 10 PlayMode, 1 of 1 EditMode): five `FEAT_8_*` tests on each hub in
    PlayMode's new `GrantedAbilityAndChanceTests`, and
    `FEAT_8_The_Effect_inspector_edits_the_chance_to_apply_and_granted_abilities` in `InspectorUndoTests`.
  - **Next:** the next lab in Decision 30's order.
  - **Tests:** PlayMode 1051: 1044 pass, 0 fail and 7 planted are skipped, in three orders (the lab's 20 among
    them). Planted 7 of 7; EditMode 432 of 432.

- 2026-11-04, **The Status-effect lab, and FEAT-8's overflow at max stacks.** The first lab (Decision 30's order, now
  that the foundations are in).
  - **The lab** (`Tests/Labs` in Proving Grounds, category Lab): a training dummy and a caster; poison, burn and bleed
    under each stacking policy; a cleanse, an antidote that makes the dummy immune to poison, a barrier whose shield
    takes damage before health (an inline attribute worker), a strike and a potion; and a bot that plays eight rounds,
    the last a fight of forty seconds. Its bot passed every round on both hubs at its first run, the foundations and
    FEAT-8's cleanse and immunity having what it needed. Its shield worker reads `ForgeTime.FrameCount`, as a game on
    PlayForge should.
  - **Overflow at max stacks** (Decision 30): a stack bound's `OverflowEffects` land from the application's origin as
    a stack would take the effect past its max; `DenyRefreshOnOverflow` keeps that application from refreshing or
    extending the effect; `ClearStacksOnOverflow` takes its stacks off. A stack past the max did nothing.
  - **Its tests,** seen failing first: `FEAT_8_An_effect_at_its_max_stacks_overflows` and
    `FEAT_8_Deny_Refresh_keeps_an_overflowing_application_from_refreshing` on each hub in `CleanseAndImmunityTests`.
  - **Next:** FEAT-8's abilities granted by effects and the chance to apply.
  - **Tests:** PlayMode 1037: 1030 pass, 0 fail and 7 planted are skipped, in three orders (the lab's 16 among
    them). Planted 7 of 7; EditMode 431 of 431.

- 2026-11-04, **FEAT-7's fixed-rate tick.** The time source's second part, which closes Phase 5's foundations
  (Decision 30). FEAT-7 is done.
  - **The tick:** ProcessControl's Fixed Tick Rate, 0 by default, which keeps one step a frame. Set, as 60, each frame
    adds its time to an accumulator and runs as many ticks as it holds, up to Max Ticks Per Frame, dropping the rest
    of a frame too long for them. A tick reads a clock of its own, the fixed step as its time step and its number as
    its frame; in it, what waits for a tick goes on, every process steps, and every process steps its end of frame, so
    an entity's actions land within the tick that queued them.
  - **Sequences:** the runtime's 28 frame waits, in the sequence runtime and library, the sync cores, the ability
    tasks and `ProcessTaskUtil`, await `ForgeTick.Next` (or `NextFrame`), which is Unity's next frame with ticks off
    and the next tick with them on, through pooled completion sources. A wait for the physics step stays on Unity's.
  - **Its tests,** which didn't compile against `48c2c8e`, `FixedTickRate` being new: the four `FEAT_7_*` tests in
    the new PlayMode `FixedTickTests`, among them `FEAT_7_A_fixed_tick_runs_the_same_at_any_frame_rate` on each hub.
  - **Next:** the labs, the Status-effect lab first.
  - **Tests:** PlayMode 1017: 1010 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    431 of 431.

- 2026-11-04, **FEAT-7's clocks, with TS-6.** The first part of the time source, the last foundation (Decision 30).
  - **A clock for each entity:** `EntityClock`, from `IGameplayAbilitySystem.GetClock()`, both hubs forwarding to
    `GASCore.Clock`: the game's time, as `ForgeTime` reads it, times the entity's `TimeScale`. Its `Time` adds up the
    entity's steps, once a frame as the entity ticks. A scale of 0 holds the entity still; it isn't a pause.
  - **What runs by it:** an effect by its target's clock (`GameplayEffectDuration.GetDeltaTime`), so a cooldown, which
    is on its caster, by the caster's; a sequence by its packet's owner, an ability's source
    (`ForgeTime.DeltaTimeFor`), its delays, clock and timeouts among them, and a task's waits through
    `SequenceTaskLibrary.DeltaTime`, which finds its run's packet by the token (`TaskSequenceRuntime.DataOfRun`); a
    projectile by its handler's. Process bookkeeping stays on the game's clock, and a physics wait on the fixed step.
  - **TS-6:** `RunProcessTask.UseUnscaledDeltaTime` is off for a new task, and `ProcessRelay.RemainingRuntime` reads
    scaled time unless asked. A task saved with it on keeps it.
  - **Docs:** `GETTING_STARTED.md`, and the CHANGELOG.
  - **Its tests,** which didn't compile against FEAT-13's commit, `GetClock` being new: the three `FEAT_7_*` and two
    `TS_6_*` tests in the new PlayMode `EntityClockTests`.
  - **Next:** FEAT-7's fixed-rate tick.
  - **Tests:** PlayMode 1012: 1005 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    431 of 431.

- 2026-11-04, **FEAT-13's two cheap changes: one clock, one random source, and IDs per entity.** The third of Phase
  5's foundations (Decision 30), which keeps networking possible without building it.
  - **The clock:** `ForgeTime` reads an `IForgeClock`, Unity's (`UnityClock`) unless `ForgeTime.Use` puts another in.
    The runtime read Unity's `Time` in a dozen files, process bookkeeping among them; every read goes through
    `ForgeTime` now, comments aside.
  - **The random source:** `ForgeRandom` draws from an `IForgeRandom`, Unity's (`UnityRandom`) unless `Use` puts
    another in, as `SeededRandom`, whose rolls repeat from their seed whatever else draws from Unity's. Unity's stays
    the default, so a test that seeds it with `Random.InitState` still repeats its roll.
  - **The rule:** `FEAT_13_The_runtime_reads_time_and_random_numbers_only_through_ForgeTime`, an EditMode test in the
    new `RuntimeSourceTests`, scans the runtime's source, comments and strings aside, for Unity's `Time` and `Random`.
  - **IDs per entity:** an activation's `HandleId` counts from 0 per ability system, where one static count numbered
    every entity's activations in turn; `GameplayEffectSpec.ApplicationId` numbers each application on its target,
    from 1. `Tag.Id` is never serialized already (Decision 22).
  - **Docs:** `GETTING_STARTED.md`'s "Time and random numbers", and the CHANGELOG.
  - **Its tests,** seen failing first: the scan, which found every read, and the four `FEAT_13_*` tests in the new
    PlayMode `ForgeTimeTests`, which didn't compile against `3dc3129`.
  - **Next:** FEAT-7 with TS-6, a clock for each entity and the fixed-rate tick.
  - **Tests:** PlayMode 1004: 997 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    431 of 431.

- 2026-11-04, **FEAT-3 and GAS-56: tag-change events.** The second of Phase 5's foundations (Decision 30).
  - **Listeners:** `TagCache.Listen(tag, onChange, kind)` hears the tag and every tag under it, as a listener on
    `Status` hears `Status.Burning`: as each comes or goes, or, with `ETagChangeKind.AnyCount`, at every weight change.
    A `TagChange` says the tag and its weight before and after. Changes are told as they happen, inside the call that
    makes them, as Unreal's are. `TagChanged` hears every change to any tag. A listener's `Dispose` stops it, even
    while it hears a change; one that throws stops neither the change nor the other listeners.
  - **A rebuild tells only what it changed:** `CompileGrantedTags` clears the tags and adds them back from their
    sources, so telling each step would report every tag going and coming. It's a rebuild now
    (`BeginRebuild`/`EndRebuild`): the old weights are kept aside, by a swap that copies nothing, and the end tells the
    difference. A listener that rebuilds the tags while it hears a change has its own rebuild told in turn.
  - **Tag workers** are evaluated at the end of a frame in which the tags changed, a worker came or went, or the last
    evaluation queued an action; they were evaluated every frame, a poll for the tags they name. A worker whose
    validity reads more than the tags overrides `EvaluateEveryFrame`.
  - **GAS-56:** a rebuild that changes a tag marks the cache, and the effect system's next tick runs its requirement
    pass if an effect is on the shelf, so a loose tag, an ability's or an item's tag meets or breaks an effect's
    requirements a frame later. A tag added straight to the cache doesn't ask: the pass's own compile would drop it,
    as `GAS_P7_A_tag_lookup_costs_the_same_however_long_its_name` showed the first version doing.
  - **Docs:** `ENTITIES.md`'s Tags section, and the CHANGELOG.
  - **Its tests,** seen failing first: `GAS_56_A_loose_tag_ends_an_effect_that_names_it` and
    `GAS_56_A_tag_an_ability_grants_ends_an_effect_that_names_it` on each hub in `EffectRequirementTests` (4 of 4
    failed), and the seven `FEAT_3_*` tests in the new `TagEventTests`, which didn't compile against `6586536`.
  - **Next:** FEAT-7 with TS-6, the time source and the fixed-rate tick.
  - **Tests:** PlayMode 996: 989 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    430 of 430.

- 2026-11-04, **FEAT-15: the Register builder.** The first of Phase 5's foundations (Decision 30).
  - **The builder:** `ProcessControl.Register(process)`, for a MonoBehaviour or runtime process, a task sequence or a
    chain of them, starts a `ProcessRegistration`; `HandledBy`, `With` and `In` shape it, and `Run()` registers the
    process and starts it, as registering always has. It returns a `ProcessRegistrationResult`, which reads as true
    when the process was taken: the relay, or why ProcessControl refused it (`EProcessRegistrationRefusal`: no
    process, no ProcessControl, registered already, ProcessControl closed, a sequence's packet that isn't a
    sequence's, or a throw in WhenInitialize). `Run(out relay)` reads as the overloads did.
  - **The handler follows the packet's run-time type:** an ability's packet hands its process to its source however
    it's typed. Through the overloads, an ability's packet passed as a `SequenceDataPacket` or a `ProcessDataPacket`
    put its process under GameRoot, so it didn't end with its entity, as GAS-52's processes didn't;
    `TaskSequence.RegisterAndRun` did that with every ability's packet.
  - **The 28 overloads** are `[Obsolete]` wrappers of the builder until the first Asset Store submission. Runtime's
    nine calls, the golden tests' four and the samples' use the builder; Proving Grounds' tests still call the
    overloads, so the gates cover the wrappers too. Nothing compiles `Samples~`, so the samples were built against the
    package's source in a throwaway project, with no error and no obsolete call.
  - **`ProcessRelay.Run`** starts a process that waits, as one that requires control does after it registers.
  - **With no ProcessControl,** a registration is refused, where every overload threw (the Phase 5 survey's FEAT-10
    note).
  - **Docs:** `GETTING_STARTED.md` has "Running a process or a sequence", and the CHANGELOG has the builder and the
    overloads' change.
  - **Its tests,** which didn't compile against `520c433`, the API being new: the six `FEAT_15_*` tests in the new
    `RegisterBuilderTests`, among them `FEAT_15_The_handler_follows_the_packets_run_time_type` on each hub, whose
    sequences end with their entity.
  - **Next:** FEAT-3, tag-change events, with GAS-56.
  - **Tests:** PlayMode 978: 971 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    430 of 430.

- 2026-11-04, **ED-40 and FEAT-8: the Debugger's foot rows, and cleanse by granted tags.** The user answered Phase
  5's design interview, which is Decision 30, and the Debugger's edits have their place on the canvas's Entities
  board (version 22), which is Decision 31. Open decisions 18 and 19 are settled.
  - **Decision 30** sets Phase 5's direction: every FEAT row but networking; the foundations first (FEAT-15, FEAT-3
    with GAS-56, FEAT-7 with TS-6, and FEAT-13's two cheap changes), then the labs. New rows: GAS-56 (a tag an
    ability, an item or a loose tag grants doesn't end an effect until another effect comes or goes), FEAT-17
    (activation by asset or tag, with a reason) and FEAT-18 (profiler markers). The Status-effect lab waits for the
    foundations.
  - **ED-40's foot rows** (Decision 31): each section ends in one, under a thin rule. Attributes: the selected
    attribute's name, then Base and Current fields, delayed, that set it on Enter; a double-click on a value goes to
    its field. Effects: Remove, a picker and Apply. Abilities: Activate and Interrupt, then a picker, Level and
    Grant; the ability's Remove is gone, as the board has it. Tags: Remove, for a loose tag only, a tag field with
    the project's tags under its arrow, and Add Tag. An action that doesn't apply stays in place, disabled, and the
    buttons say plain Remove, Activate and Interrupt.
  - **Selection:** a section's selected row takes Unity's unfocused selection grey (`--pf-selection-unfocused`,
    Dark #4D4D4D, Light #AEAEAE), now in `EDITOR_STYLE.md`'s colors, so the entity list keeps the blue.
    `--pf-selection-text` is gone.
  - **What an action did** (Applied Burning, Burning was refused, Activated Channel) is on the status bar's right, so
    the foot rows never shift to say it. The boards don't draw it; the result labels beside the buttons are gone.
  - **FEAT-8:** a cleanse and an immunity name an effect by a tag it grants too, as Decision 30's default asks, so
    `State.Burning` takes off whatever grants it (`GameplayEffectTags.Names`). `EFFECTS.md` says so.
  - **Its tests,** seen failing first: the four `ED_40_*` tests in `RuntimeDebuggerTests`, against `c934980`'s
    window (4 of 16 failed, finding no foot row), which now select rows, fill the foot rows' fields and interrupt a
    running ability (`ED_40_An_ability_granted_in_the_debugger_activates_and_is_interrupted`); and
    `FEAT_8_A_cleanse_and_an_immunity_name_an_effect_by_a_tag_it_grants` on each hub in `CleanseAndImmunityTests`,
    which failed on both.
  - **Next:** Phase 5's foundations, starting with FEAT-15, the Register builder.
  - **Tests:** PlayMode 970: 963 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    430 of 430.

- 2026-11-04, **ED-4 and ED-5: the Debugger, as the boards draw it.** The user approved the design canvas's debugger
  boards (version 21, second row) and asked for the window built from them: the Entities, Processes and Tags boards
  and the board for outside Play mode. The runtime notes stand: the step time is measured, and `RunIsHeld` is
  public for the Run field.
  - **The window** is the Debugger (`Window → PlayForge → Debugger`), as the boards title it. Its toolbar has the
    three views' toggles, the search, the view's filter (Both, Mono or Runtime hubs; All states, Running or
    Waiting), Follow Selection or ProcessControl's state, and the frame. The rate is in the window menu (Samples a
    Second). A status bar counts ProcessControl's state, the processes by state and the entities by hub.
  - **Entities:** the list's Name, Hub, Lvl and Effects, and a header with the entity's icon, its name, what it is
    ("Entity · Goblin Raider · Mono hub · Level 3"), Select in Scene and Open Asset. Then four sections two by two,
    each with its count and titled columns: Attributes (Base, Current, and Changed by: each source of the last
    second's impacts with what it took a second, then the effects holding a modifier on it), Effects (Left, Stacks,
    and From, as "Player · Fireball" or Self), Abilities (Lvl, State, as Stage 2/3 from the new
    `AbilitySpecContainer.ActiveStageIndex`, and Cooldown) and Tags (the path, parents in secondary text, Count, and
    From: the effect, ability or item that grants it, Affiliation, Asset tag or Loose tag).
  - **ED-40's edits,** which the boards don't draw, follow open decision 19's working assumption: a double-click
    sets Base or Current in place; a click selects a row, and the buttons under its table act on it; the pickers
    and the tag field sit under their tables. The question went to the parent session for the user.
  - **Processes:** the list's ID, Process, Handler, Lifecycle, Timing ("Update + Late"), State ("Running →
    Terminated" once asked), Lifetime and Step, the time a step over the last second, from a sample of each
    process's steps at each read. Its column titles sort it. The detail's header has Select in Scene for a Mono
    process; then Pause, Resume, Terminate and Terminate Now; then Process (lifecycle, step timing, step priority,
    and the handler, with Show for its process), State (state, queued, and Run: Going, Held, Cancelled or Ended,
    with a note that a cancelled run's Resume starts a new one) and Time (lifetime, time running, step time and
    the longest step, `ProcessControlBlock.PeakStepSeconds`).
  - **Tags:** every tag the shown hubs hold, as a tree by its path, where a parent counts the entities holding any
    tag under it and adds up their weights; a parent opens the first time it shows. The selected tag's detail names
    it and how widely it's held ("Tag · 2 entities · weight 3"), then Held By: each entity, its weight, what
    grants the tag and where that came from, walked as the tag compile walks its sources.
  - **Style:** the boards' measures, in the style sheet: 20 px rows that alternate and take Unity's selection color,
    titled columns that clip their own text, a 120 px fact column, and the type colors only in the icons. USS has
    no `:first-child`, so a table's first cell carries a class. `--pf-selection-text` is new in both themes.
  - **Its tests,** seen failing first against the committed window (14 of 21 failed, and the other 7 passed):
    the updated `ED_4_*`, `ED_5_*` and `ED_40_*` tests in `RuntimeDebuggerTests`, now reading the boards' labels
    and pressing rows and cells, and the new `ED_4_The_Tags_view_shows_each_tag_and_who_holds_it`,
    `ED_4_The_status_bar_counts_the_processes_and_entities`, `ED_4_Follow_Selection_shows_the_selected_hub` and
    `ED_5_The_Run_field_says_whether_a_paused_run_is_held`. The menu test checks the new title.
  - **Docs:** the CHANGELOG, `GETTING_STARTED.md`, `PUBLISHING.md` and `IMPACT_MODEL_REFACTOR.md` call it the
    Debugger.
  - **Next:** Phase 5 goes on: the Status-effect lab, with FEAT-8's overflow at max stacks.
  - **Tests:** PlayMode 968: 961 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    430 of 430.

- 2026-11-04, **Phase 5 begins; FEAT-8's cleanse and immunity.** Phase 4's exit gate holds: the editor smoke suite
  passes in both themes in every gates run. Phase 5's order is above the test project's section: the labs one at a
  time, each bringing the features it finds missing. Open decision 18 asks how the labs' scenes ship.
  - **Cleanse:** an effect's `Tags.RemovesEffectsTagged` takes off, as it lands, every other effect on the target
    tagged with one of those tags or a child of one, by its asset tag or a context tag. They leave at the next tick,
    as a removed effect does, before the cleanse's own contained effects land.
  - **Immunity:** `Tags.BlocksEffectsTagged` refuses such an effect while the immunity is on the target and its
    ongoing requirements hold; applying it returns false. It leaves effects already on; a cleanse clears them.
  - **Found by the gates:** the first immunity check walked the whole shelf on every application, so
    `GAS_P3_An_application_costs_the_same_however_much_the_entity_carries` failed on both hubs in every order (about
    0.013 ms against 0.0035). The shelf now keeps its immunities in a list of their own, and an application checks
    those alone.
  - `GameplayEffectTags.Names` decides what a list names, for both. The Effect inspector's Tags section edits both
    lists, Import and Clear cover them, and its summary says "Removes Status.Debuff". `EFFECTS.md` describes them.
  - **Its tests,** seen failing first: `FEAT_8_A_cleanse_takes_off_the_effects_it_names` and
    `FEAT_8_An_immunity_refuses_the_effects_it_names` on each hub in the new PlayMode `CleanseAndImmunityTests`, and
    `FEAT_8_The_Effect_inspector_edits_what_an_effect_removes_and_blocks` in `InspectorUndoTests`.
  - **Next:** the Status-effect lab, with FEAT-8's overflow at max stacks.
  - **Tests:** PlayMode 968: 961 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    426 of 426.

- 2026-11-04, **ED-40 and FEAT-16: the debugger acts.** From Q27's answer, editing attributes and tags first, then
  applying effects and granting abilities. ED-40 is fixed, and the debugger's part of step 5 is done.
  - **Attributes:** the Entities view's current and base fields take a value. The entity takes it as it is: the change
    goes in directly, as a delta no worker sees (`GenerateUnworkable`), and the next sample shows what the entity holds.
    A field being typed in keeps what's typed while samples come.
  - **Tags:** a tag's name, typed (Enter or Add Tag) or picked from the project's tags under their roots, is granted
    as a loose tag, and a loose tag's row has Remove. A tag added straight to the tag cache went at its next compile,
    when an ability starts or ends, an item changes or an effect lands.
  - **Loose tags, in the runtime (FEAT-16):** `AddLooseTag`, `RemoveLooseTag` and `GetLooseTags` on
    `IGameplayAbilitySystem`, forwarded by both hubs to `GASCore`, which counts each grant and has
    `CompileGrantedTags` include them, as Unreal's loose gameplay tags. Done ahead of Phase 5, as the debugger needs
    it. Both hubs are the interface's only implementations.
  - **Effects:** an effect picked in the Effects section lands from the entity itself, and the section says Applied
    or Refused; each effect's row has Remove, which takes it off where its source applied it.
  - **Abilities:** an ability picked in the Abilities section is granted at the level given (Granted or Refused);
    each ability's row has Activate, which says Activated or Refused, and Remove, which takes that copy away.
  - **Its tests,** seen failing first: `ED_40_An_attribute_takes_the_value_typed_into_the_debugger`,
    `ED_40_A_tag_added_in_the_debugger_lasts_until_its_removed`,
    `ED_40_An_effect_applied_in_the_debugger_lands_and_its_row_removes_it` and
    `ED_40_An_ability_granted_in_the_debugger_activates_and_goes` in `RuntimeDebuggerTests`, and
    `FEAT_16_A_loose_tag_lasts_until_its_removed` on each hub in PlayMode's `TagTests`.
  - **Next:** step 5 is done. Phase 5 (FEAT-n) begins.
  - **Tests:** PlayMode 964: 957 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    425 of 425.

- 2026-11-04, **ED-4 and ED-5: one Runtime Debugger.** From Q4, Q28 and Q30: one window in place of the GAS Debugger,
  Process Control and the Tag Debugger. ED-4 and ED-5 are fixed; the debugger's edits (Q27) are the new ED-40.
  - **The window,** `RuntimeDebugger` in `Editor/Debugger/`: a toolbar with the Entities and Processes views, the
    processes' filter, the search, the rate and ProcessControl's state; a list beside a detail; and a status bar.
  - **Both kinds of hub, from the registry** (Q28). The hubs are ProcessControl's processes whose wrapper holds an
    `IGameplayAbilitySystem`. The window follows the ProcessControl it finds, and a process that joins or leaves it has
    the lists read again at the next sample. Nothing scans the scene, so a runtime hub shows too.
  - **In place, at a rate** (Q30). While the game plays, the window reads it on the editor's update at 2, 4, 10 or 30
    samples a second; a sample changes the values the lists and the detail show, and rebuilds a section only when what
    it lists changes. The GAS Debugger cleared and rebuilt its details every 0.25 s, replacing a field while it was
    typed in. `.Every(` stays banned (ED-11): nothing runs while the editor is in edit mode.
  - **The Entities view:** each hub's name, level and affiliation; the selected one's attributes (current and base,
    with the breakdown by retention group from `GetBreakdown`), tags with their weights, effects with the time each has
    left of its duration, its next tick and its stacks, abilities with their level and the time a cooldown has left,
    and the last impact it dealt and took. It listens to that entity's frame summaries alone, and stops as it closes
    or another is shown (ED-26). It reads no private field.
  - **The Processes view:** each process's name, ID, handler, state and time a step; the selected one's lifecycle,
    timing, next state, lifetime and how many processes it handles; and Pause, Resume, Terminate and Terminate Now.
    Pause and Resume both take the processes it handles; Process Control's Resume didn't, and it greyed out Pause for
    a running process that waits or needs control, as it read the state the process would take next.
  - **Step time, in the runtime.** `ProcessControlBlock.MeasureSteps`, on while a debugger reads the game, has each
    block count its steps and time them with a `Stopwatch` (`Steps`, `StepSeconds`). Off, a step costs one check of
    it. Process Control's usage was the wall-clock time between a block's steps, much the same for every process.
  - **In edit mode** it offers Play Mode (Q28).
  - **Gone:** `GASDebugger`, `ProcessControlDebugger` with its four public enums, and `RuntimeTagViewer`.
  - **Docs:** `GETTING_STARTED.md`'s Runtime Debugger entry, `PUBLISHING.md`'s checklist, and
    `IMPACT_MODEL_REFACTOR.md`'s follow-up, which the debugger's move to `GetBreakdown` closes.
  - **Its tests,** seen failing against the three windows first: `ED_4_The_debugger_lists_each_hub_from_the_registry`,
    `ED_4_A_sample_changes_the_detail_in_place`, `ED_4_A_cooldown_shows_the_time_it_has_left`,
    `ED_4_In_edit_mode_the_debugger_offers_Play_Mode`, `ED_5_A_process_shows_how_long_its_steps_take`,
    `ED_5_A_running_process_pauses_and_resumes`, `ED_26_Closing_the_debugger_removes_its_hooks` and
    `GAS_S1_The_debugger_names_a_runtime_entity` in the new `RuntimeDebuggerTests`, which replace `GASDebuggerTests`;
    and `ED_5_A_process_times_its_steps_while_asked` in PlayMode's `ProcessUtilityTests`. The smoke suite opens the
    new window, and the menu test checks its title.
  - **Next:** the debugger's edits, attributes and tags first (Q27, ED-40).
  - **Tests:** PlayMode 962: 955 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    421 of 421.

- 2026-11-04, **ED-13: templates work as prefabs do.** The last of The Forge's redesign, from Q23's answer: an explicit
  push to children, with a mark on each overridden field. ED-13 is fixed, and The Forge's part of step 5 is done.
  - **The push,** `ForgeTemplatePush.PushToChildren`, from a template's asset menu (Push to Its N Children): each child,
    and each of theirs, takes the template's fields but those it overrides, through the propagator, under one
    `RegisterCompleteObjectUndo` per child in one Undo group. It flushes pending undo records first, so an edit made just
    before it, as by a script, has marked its override. A loop of templates stops where it comes back.
  - **Overrides** are recorded as they're made, as a prefab instance's are: `Undo.postprocessModifications` hands over
    each edit to a child's field, an inspector's, an Import's or a script's through a SerializedObject, and the field
    becomes the child's own, at the granularity the push keeps (a plain field's path, or the outermost
    `[SerializeReference]` field it's in; an edit inside a list, which the push merges, or to an excluded field, such as
    the name, overrides nothing). They were worked out on each save against the template as it was then, so a template
    changed and not yet pushed would have marked its own changes as the children's.
  - **One Undo takes back an edit and its mark,** and Redo makes both again. A probe of each way showed that Unity's
    Undo takes back what a `postprocessModifications` hook adds to an edit but never makes it again on Redo, drops a
    record the hook starts, and puts a whole-object record ahead of the edit's own, where Undo would make the edit
    again. So each mark is remembered with the Undo step it was made in, in `SessionState`, as Undo outlives a domain
    reload: `OnUndoRedo`, on `Undo.undoRedoEvent`, takes it back with that step, or any step collapsed into it, and
    makes it again on its Redo.
  - **Found by the gates:**
    - The inspector sweep, `Every_edit_in_an_inspector_undoes`, failed for the five Demo assets that are template
      children, as one Undo left each edit's mark. The sweep now says where the asset's JSON differs.
    - `ED_27_The_list_follows_the_project` failed in the full suite: The Forge's reload waited on a `delayCall`, which
      batch mode ran late or not at all. The Forge now reloads on the editor's next update, queued from the import
      itself (`TheForgeImports`), from an asset made or destroyed, and from `projectChanged`.
  - **The marks:** an inspector marks each field its asset overrides with Unity's prefab override bar and a bold
    label (`ShowOverrides`, classes only), following every change. A marked field's right-click menu has Revert to
    Template, which copies the template's value back and makes the field the template's again, as one Undo step
    (`RevertToTemplate`, which writes without a modification of its own, so it marks nothing again).
  - **Gone:** `ForgeTemplateProcessor`, the save hook, with its modal, its load of every asset of the type and its
    unwired switches, and `ForgeTemplateValidator`, which did nothing.
  - **Docs:** `GETTING_STARTED.md` has a Templates entry.
  - **Its tests,** seen failing against the save hook first (the third showed its dialog cancelled mid-save):
    `ED_13_Saving_a_template_changes_no_child`, `ED_13_Push_to_Children_keeps_each_childs_overrides` and
    `ED_13_A_childs_inspector_marks_its_overrides` in the new `TemplatePushTests`. With them,
    `ED_13_One_Undo_takes_back_an_edit_and_its_override`, seen failing against a mark made outside Undo.
  - **Next:** one runtime debugger for the GAS, Process and Tag views (ED-4, ED-5).
  - **Tests:** PlayMode 961: 954 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    421 of 421.

- 2026-11-04, **ED-27 and ED-9: The Forge is a list and a detail.** The third of The Forge's redesign, from Q5, Q6,
  Q7, Q24, Q29 and Q30. ED-27 and ED-9 are fixed.
  - **The window,** `TheForge`, one file in place of six: a toolbar with Create, a filter for each of the eight types
    (its type icon, or its name for Scaler and Requirement, which have none), Tags, the search and the problem count; a
    `MultiColumnListView` of the project's PlayForge assets beside a detail pane, in a `TwoPaneSplitView`; and a status
    bar with the counts. The list virtualizes, for thousands of assets (Q24). Its columns, the name with its type icon
    and the file name when it differs, the type, the asset tag, the levels and the problem glyph, sort in natural order
    (ED-7). A click selects a row and shows the asset (ED-27); a double click shows it in the Project window; the
    right-click menu has Show in Project, Open in a Window, Duplicate and Move to Trash.
  - **The detail pane** holds the asset's own inspector, through an `InspectorElement` over its editor, under a
    summary (Q6): the description, then each section's title and one-line summary, which `BasePlayForgeEditor` now
    lists (`Sections`), then its problems. It covers every type, where the Visualizer was blank for items and sets
    (ED-9). The window serializes its filter, search, sort and selection, so a domain reload keeps them. Inside it, the
    inspector's Open in The Forge reads Show in Project.
  - **Tags** lists the project's tags, from the tag registry, by full path or leaf as the tag preferences say (ED-25),
    with what each is for and how many assets use it; the detail lists each asset that uses it, as a row that shows it.
  - **It follows the project,** with no Refresh and no timer (Q30): `projectChanged` reads it again once the change
    settles, an edit redraws the rows, and the validator's `Changed` redraws the glyphs and counts.
  - **Create** opens a small New Asset window, a name and a folder, which makes the asset through
    `ForgeAssetCreation.CreateNew`: a copy of the type's New Asset default (its values, not a link, Q22), named, with the
    tag its name gives, where Project Settings puts its type (Q20). Duplicate names the copy as a copy, with its own
    tag. The Project window's own Create menu still makes a blank asset; routing it through the factory is left.
  - **The shortcut** `PlayForge/The Forge`, Ctrl+Alt+Shift+F by default and rebindable, opens it with the search
    focused (Q29).
  - **Gone:** the Create, View and Analysis tabs and the Visualizer window. The power scores stay hidden until items
    and entities score too (Q7); the code is in git history before this commit, for the tunable presets that come later.
    The Create tab's recently created list and statistics, the View tab's embedded-scaler and requirement browsers,
    the Visualize menu item, and the five preferences they used, which Use Defaults now clears.
  - **Its tests,** seen failing against the old window first: `ED_27_One_click_selects_a_row_and_shows_its_asset`,
    `ED_27_The_list_follows_the_project`, `ED_27_The_type_filter_and_the_search_narrow_the_list`,
    `ED_27_A_shortcut_opens_The_Forge`, `ED_15_The_Forge_counts_the_projects_problems`,
    `ED_9_The_summary_shows_each_type_and_the_selection_is_kept`, `ED_7_A_numeric_column_sorts_by_number` and
    `ED_25_The_tag_list_follows_the_tag_preferences` in the new `TheForgeTests`, which replace the View, Analysis and
    Settings tabs' tests. The smoke suite opens The Forge and each type's New Asset window.
  - **Next:** template propagation as an explicit push (ED-13).
  - **Tests:** PlayMode 961: 954 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    417 of 417.

- 2026-11-04, **ED-15: one validator, the Problems view and the header's count.** The second of The Forge's redesign,
  from Q8's answer: a Problems view and badges, both fed by one validator. ED-15 is fixed.
  - **The validator,** `ForgeValidator`, runs each `ForgeValidationRule` it finds (PlayForge's, and any a project adds)
    on every PlayForge asset, Scaler asset and requirement template, and keeps what they find. It follows the project
    with no polling: an edit, an Inspector's or an Undo's, checks its asset again through `ObjectChangeEvents`; an
    import checks what it brought; a delete or a move has every asset checked again the next time someone asks.
    `LogAll` writes each problem to the Console, for batch mode.
  - **Its rules,** each in the inspector's words where it has some, so the three agree: a link with no provider
    (Leveling's warning), the first impact's delivery note (the Audit's flag) and an additional impact its duration can't
    make (the entry's), a magnitude field that reads a scaler it hasn't got and a Shared scaler with no asset, two fields
    that hold one scaler (the scaler drawer's; an element's Current and Base, which Copy shares, are left), a set that
    gives nothing, an element with no attribute (GAS-54) or a magnitude outside its bounds (the element's help box,
    whose sentences `AttributeSetElementDrawer.BoundsMessages` now gives both), a stale asset tag, a missing name, and
    data whose class Unity can't find, the one Error. Validate All's other checks are gone: an effect with no workers,
    or an ability with no cost or cooldown, is fine.
  - **The Problems view,** `ForgeProblemsWindow` (`Window/PlayForge/Problems`): a toolbar with the Errors and Warnings
    filters, each with its count, and a search; a list of severity, asset and problem, sortable by each; a click
    selects the asset and a double click opens it; a status line with the counts.
  - **The header's count:** an inspector's header shows "2 problems" while the selection has any, in the warning color
    (an error's in the destructive one), with each message in its tooltip, and a click opens the view on the asset.
  - **Gone:** the Audit Effect Impact Deliveries menu item (`ImpactModelMigrationTool`), whose flag is a rule now;
    Validate All went with the Settings tab (ED-21). The docs name the Problems view where they named the Audit.
  - **Its tests,** seen failing against stubs first: `ED_15_The_validator_finds_what_the_inspectors_warn_about`,
    `ED_15_An_edit_checks_its_asset_again`, `ED_15_The_Problems_view_lists_each_problem_and_selects_its_asset`,
    `ED_15_An_inspectors_header_counts_its_problems` and `ED_15_The_validator_writes_each_problem_to_the_Console` in
    the new `ProblemsTests`. The smoke suite opens the view.
  - **Next:** The Forge's list and detail (ED-27, ED-9), with the problem glyph and counts.
  - **Tests:** PlayMode 961: 954 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    418 of 418.

- 2026-11-04, **ED-21: settings belong to the project.** The first of The Forge's redesign (step 5), from Q21's answer.
  ED-21 is fixed.
  - **The project's settings,** `PlayForgeSettings`, a `ScriptableSingleton` kept in
    `ProjectSettings/PlayForgeSettings.asset`, so they travel with the project under version control: each type's
    folder, prefix and suffix for new assets; each type's defaults, the asset a new one starts as a copy of and the
    assets Import lists first (Q22's "defaults", which the Import window's first group is now called); and the Cost and
    Cooldown effects. **Project Settings → PlayForge** edits them, bound, so an edit undoes, and each edit and Undo is
    saved. A project takes the values The Forge kept in EditorPrefs on its first use: the paths and names, the templates
    its own key registered (or the shared key's, ED-22), and the usage templates. It writes nothing back, so the other
    projects on the machine still find theirs.
  - **Each user's preferences,** `PlayForgePreferences`, stay in EditorPrefs, as Unity's own do: the tag display, The
    Forge's view toggles and the log level, on **Preferences → PlayForge**, with **Use Defaults**.
  - **Gone:** The Forge's Settings tab, which the window menu replaces with the two pages, `TemplateRegistry`,
    template tags (they only labelled templates on that tab), the per-type template tags nothing read, the
    Additional Settings (all but the Requirement's category named fields the types don't have), and Validate All, which
    the Problems view replaces next (ED-15). The four view toggles that go with the window's redesign keep their
    values until then.
  - **Callers:** `ForgeAssetCreation`, the Create dialog, the effect field's New Linked Effect (which read an "Effect_"
    prefix of its own), the Import window and `EffectTemplateRegistry` read the settings.
  - **Docs:** `GETTING_STARTED.md` has a Settings entry; it, `ABILITIES.md` and `PUBLISHING.md` name the pages.
  - **Its tests** were written with the change, not before it, since the old storage had no API to test against:
    `ED_21_A_shared_setting_belongs_to_the_project`, `ED_21_A_project_takes_the_settings_it_kept_in_EditorPrefs`,
    `ED_21_The_Project_Settings_page_edits_the_shared_settings`, `ED_21_The_Preferences_page_edits_each_users_toggles`
    and `ED_6_Use_Default_Folders_and_Names_keeps_the_defaults` in the new `PlayForgeSettingsTests`, which replace the
    Settings tab's ED-6 and ED-25 tests and `TemplateRegistryTests`. The smoke suite builds both pages in each theme.
  - **Next:** one validator, with the Problems view (ED-15).
  - **Tests:** PlayMode 961: 954 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    411 of 411.

- 2026-11-04, **ED-12, the last part: the Effect, Entity and Attribute Set inspectors edit a multiple selection.** ED-12
  is fixed with it.
  - **The three inspectors** take a multiple selection, as the Item and Ability inspectors do: a field the assets
    disagree on shows as mixed, and an edit reaches each. Name, the asset tag, linking and Used By wait for one asset.
    So do the lists each asset keeps of its own, whose elements at one index are different things and which a list of
    another length would cut: an effect's additional impacts, workers and stack behaviours, and a set's attributes. The
    attribute tables wait too. Leveling sets Link Mode, and the range while every asset keeps its own. The summaries
    are blank, and Equipped Too waits until every entity allows duplicates.
  - **Each asset's own values:** a property of a multiple selection reads the first asset, and a write through it gives
    every asset the same value. The new `EachAsset` reads and writes each selected asset through a SerializedObject of
    its own, so the magnitude field and the scaler drawer work per asset. A kind change gives each field the kind from
    its own first value, and a field of that kind keeps its scaler. Levels keeps each table's own values. Make
    Constant, Use a Scaler Asset, Import Values and the curve a stretched scaler reads work per asset too. A kind,
    count, range, Combine, operation or delivery the assets disagree on shows as mixed, and the duration and impact
    drawers show the parts any selected effect uses. Save as Scaler Asset waits for one asset.
  - **In passing:** the magnitude field's Combine follows an Undo now; it kept the value it was drawn with. No test
    covers it.
  - **Docs:** `SCALERS.md` says how the field edits a multiple selection.
  - **Its tests,** seen failing first: `ED_12_The_Effect_inspector_edits_every_selected_effect`,
    `ED_12_The_Entity_inspector_edits_every_selected_entity` and `ED_12_The_Attribute_Set_inspector_edits_every_selected_set`
    in `InspectorUndoTests`, and `ED_12_A_magnitude_field_edits_every_selected_asset` and
    `ED_12_A_scaler_drawn_on_its_own_edits_every_selected_asset` in `ScalerDrawerTests`.
  - **Next:** The Forge (ED-13, ED-15's Validate All, ED-21, ED-27).
  - **Tests:** PlayMode 961: 954 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    412 of 412.

- 2026-11-04, **ED-33, part 2, and the last of ED-32: the scaler field.** The canvas's scaler board, in the editor.
  ED-32 and ED-33 are fixed with it.
  - **The magnitude field,** `MagnitudeField`, draws both magnitude operations: its name, its kind, its number or its
    range, and its field menu. A Constant shows its number and unit, from `ScalerOperationKeyword`'s new `Unit` ("s" for
    durations and tick intervals), and warns when it reads a scaler it hasn't got. A scaler's kind adds Combine (Real
    Magnitude by what it does: Scaler, Scaler + Number, Scaler × Number, Number Only), Rounding for a whole-number
    field, then the scaler's content. A kind change is one Undo step: a constant made By Level keeps its number as level
    1's value, and a Shared field reads its asset times 1.
  - **The scaler's content,** in `ScalerFields`: the values per level, ten to a row, with Levels; a read-only curve, the
    kit's new `ForgeCurvePreview`, drawn in the style sheet's colors; Stretch Over Levels, with Level Range and Between
    Levels while it's on (their choices are named Provider's Levels, Own Max Level and Clamped to Provider; Steps,
    Straight Line and Smooth Curve); the kind's own fields; and its behaviours. A typed value writes the curve a
    stretched scaler reads in the same Undo step, through `AbstractScaler`'s new `CurveFor`; binding's own change
    events write nothing, as ED-39 found.
  - **The field menu:** Save as Scaler Asset (a small window: a name, a folder, Save and Use), Use a Scaler Asset (the
    project's Scaler assets that fit the field), Import Values, Quick Fill (the old wizard) and Make Constant, through the
    new public `ScalerActions`.
  - **Import Values,** `ScalerValuesWindow`: the Scaler assets and the other assets' fields with values per level; the
    values level by level, with both curves, and which levels a shorter table's last value holds for; Copy Values as
    one Undo step. With the field menu, it replaces `ScalerImportWindow` and the curve editor.
  - **The Scaler asset's inspector,** `ScalerAssetEditor`: its name, kind and uses, its Description, its Values, and
    Used By, a row for each field that shares it. A shared field says how many fields share its asset.
  - **Attribute sets share too:** the new `SharedCachedScaler` reads a Scaler asset whose scaler is a cached kind.
  - **The scaler drawer,** for a scaler drawn on its own, as an element's Scaling, takes the same kind and content. It
    rebuilt itself on a copy of the first selected asset, one of ED-12's two left copies; it writes through the field's
    own SerializedObject now, and a kind change gives each selected asset its own scaler. It keeps ED-34's note for a
    scaler two list elements share. Its partial files, the import cache, the curve editor and the Quick Fill tabs are
    gone.
  - **Next:** ED-12's multi-edit in the Effect, Entity and Attribute Set inspectors, which this frees.
  - **Docs:** `SCALERS.md` describes the field, its menu, the values and Scaler assets; `EFFECTS.md` and `ENTITIES.md`
    point at it.
  - **Its tests** were written with the redesign: `ED_33_A_magnitude_field_reads_as_the_scaler_board`,
    `ED_33_A_fields_kind_changes_as_one_undo_step`, `ED_33_Save_as_Scaler_Asset_shares_the_fields_values` and
    `ED_33_Import_Values_copies_a_scalers_values` in `ScalerDrawerTests`, and
    `ED_33_An_attribute_set_element_shares_a_scaler` in `ScalerTests`. The ED-34 test finds the drawn tables, since a
    scaler no longer folds, and the smoke suite opens Import Values and Save as Scaler Asset in place of the two old
    windows.
  - **Tests:** PlayMode 961: 954 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    407 of 407.

- 2026-11-04, **ED-33, part 1: Scaler assets, and exact levels.** Decision 28's scaler model, in the runtime first.
  - **Scaler assets:** `ScalerPipeline`, the Scaler Template, is `ScalerAsset` now (the file and its meta moved, so
    saved assets keep their script). Its link code had no callers and is gone: `AbstractScaler`'s template field, its
    sync switch and the three methods that used them. The new `SharedScaler` kind holds an asset and reads its scaler,
    so an edit to the asset reaches every field that uses it; the field's own magnitude adjusts it. A chain of shared
    scalers that comes back to its asset reads 0, past eight deep.
  - **Levels map exactly:** level 4 reads the 4th value, and a level past the end the last. Every scaler spread its
    values over a level range, so three values on an ability of five levels read 15 at level 2. The new
    `StretchOverLevels` keeps that, and it is on for the scalers saved before it existed, since a missing field takes
    its initializer, so saved assets and scalers built in code read as they did. The drawer makes new scalers with it
    off and shows it as Stretch Over Levels. This keeps every saved project's numbers. The user chose to keep it on
    for old scalers (Decision 28).
  - **The cached kinds,** an attribute set's, can't share yet: `SharedScaler` is a plain scaler.
  - **Docs:** the new `SCALERS.md`.
  - **Its tests,** `ED_33_A_shared_scaler_reads_its_asset` and `ED_33_Levels_map_exactly_unless_the_scaler_stretches`,
    in the new `ScalerTests`, were seen failing against stubs; `ED_33_A_new_scaler_maps_levels_exactly` was written
    after the drawer's one-line change and seen failing with that line taken out.
  - **Tests:** PlayMode 959: 952 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    399 of 399.

- 2026-11-04, **ED-32 and ED-35: one Import window.** Decision 28's window, from the canvas's Import board.
  - **The window,** `ForgeImportWindow`: Into, then 1 Choose a Source (a search, then the templates, then the assets of
    the type, each with its icon and tag), 2 Choose What to Import (a table of the fields that differ, each checked,
    beside its value now and the source's; Show Matching; and a line listing the unchanged ones), then 3 the count and
    Import N Changes, one Undo step. A field is a value, a list or a reference, down to two levels of a class; a
    magnitude reads as its field, as "Duration" and "Duration scaler". A list is replaced unless its row's Add is on.
  - **It serves** every section's Import, the asset menu's Import from Another Asset, and the worker group's and
    requirement group's import buttons, which list every asset with such a group at that path. It replaces five
    windows: `SectionImportWindow`, `WorkerGroupImportWindow`, and the effect's, ability's and group's requirement
    windows. The scaler's waits for ED-33's Import Values. The old worker import shared each worker object with its
    source; the window copies them.
  - **ED-35:** the whole-asset import copied each top-level field whole, so `Definition` brought its Name, `Tags` the
    asset tag, and the private template and level-link fields came along. It keeps the name, description, icons,
    asset tag, template and link now. Its dialog is gone, and so is the error it showed when no template existed,
    though other assets did.
  - **Clear** resets at once, where it asked in a modal dialog (Decision 28). `SectionImportWindow.cs` is
    `SectionClearUtility.cs` now.
  - **Docs:** `GETTING_STARTED.md` describes Import and Clear.
  - **Its tests,** `ED_32_The_import_window_lists_the_fields_that_differ`,
    `ED_32_A_list_can_take_the_sources_items_after_its_own` and
    `ED_35_Importing_a_whole_asset_leaves_its_identity_and_links`, in the new `ImportWindowTests`, were written with
    the window rather than before it, since they open it; ED-35's fault was read in the old code. The smoke suite opens
    the one window in place of the five.
  - **Tests:** PlayMode 957: 950 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    398 of 398, four windows fewer in each theme.

- 2026-11-04, **Step 5: the Effect inspector (ED-37).** The redesign's sixth and last asset inspector, after the
  others' pattern and the Effect mockup. ED-37 is fixed with it.
  - **Its sections:** Identity, Leveling, Tags, Duration, Impact, Requirements, Workers, Local Data and Used By. Identity
    was Definition and holds the asset tag; Retention Group moved to Impact, which it's about. Level Source merged into
    Leveling: a standalone effect takes the level of what applies it, as a note says, and Link and Unlink Contained
    Effects show while it's linked, since with no provider Link would unlink them. Impact came before Duration, and
    Workers before Requirements.
  - **Duration**, `GameplayEffectDurationDrawer`, shows the fields its Policy uses, by the mockup's names: Policy,
    Duration, Periodic Ticks with Ticks or Tick Every, Tick On Application, Additional Executions, the clock,
    Re-application and Then, and Stacking for Stack Existing. Re-application's and Then's choices carry
    `InspectorName`s: Add Another, Replace Existing, Stack Existing and Do Nothing; Refresh Duration and Extend
    Duration. It drew tinted boxes with colored headings under explaining sentences, and showed or hid them 50 ms after
    it drew; it follows the policy now. Then hides for Add Another, which never takes an application in.
  - **Requirements** is the mockup's table, in the kit's new `ForgeRequirementsTable`: a row for each group, Applying,
    While On and Removed By, with a Requires and a Must Not Have cell that list their tags. A cell opens its list under
    the table, with the row's template. A two-button switch shows the target's table, then the source's. The Ability
    inspector's tag requirements use the same table, with a Caster and a Target row. The old drawer drew a tinted box
    with colored subsections, a type badge, a Name and import buttons, and rebuilt itself as it opened; its import
    windows stay until ED-32's one window.
  - **The base editor's** Leveling helpers take what an asset with no levels of its own needs: `AddLinkedLevels` a
    message for no provider, and `CreateChildLinks` the asset the children link to, so Unlink counts the contained
    effects linked to the effect's provider.
  - **Left for later:** the mockup's problems in place, such as "Nothing grants Status.Wet, so this removal never
    happens", and the header's problem count, with the Problems view (ED-15's Validate All); and the units and "scaled
    by" beside Duration, Tick Every and Magnitude, with the scaler workflow (ED-33).
  - **Docs:** `EFFECTS.md` follows the inspector, and `ABILITIES.md` the table. The help topics name the new headings;
    `effects/leveling` is new and `effects/level-source` is gone.
  - **Its tests,** `ED_37_The_Effect_inspector_keeps_the_style_docs_sections`,
    `ED_37_The_Effect_inspector_sets_no_look_in_code`, `ED_37_The_Effect_inspector_lists_what_uses_it`,
    `ED_37_The_requirements_table_opens_a_cell_to_edit_it` and `ED_37_The_duration_shows_what_its_policy_uses`, were
    seen failing first. The Effect's two ED-12 tests find the provider in Leveling and unlink through its field;
    `ED_12_A_rebuilt_drawer_edits_every_selected_asset` edits a table row's Sync With Template on two effects; and
    `ED_18_Opening_a_drawer_on_one_asset_leaves_another_as_it_was` folds an additional impact, since the table doesn't
    fold.
  - **Tests:** PlayMode 957: 950 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    403 of 403.

- 2026-11-04, **ED-2 and ED-3: an effect's impacts.** The Effect step begins with its impact.
  - **ED-3, additional impacts.** An effect made one change, so a drain that took health and mana took two effects.
    `GameplayEffectImpact.AdditionalImpacts` holds authored entries, each with its own attribute, operation, magnitude
    and delivery. `RebuildEntries` appends them, live, after the entry it derives from the impact's fields; one with
    no attribute is skipped, and so is one whose delivery the duration can't make. The runtime already applied entries
    by delivery, so nothing else changed there. This is the flip the impact model's design waited for, a multi-entry
    drawer, and `CLAUDE.md`'s gotcha says so now.
  - **The new drawer,** `AttributeImpactEntryDrawer`: a foldout row with the attribute and Remove, and while folded
    one line such as "Mana −10 · Continuous"; open, Operation, Multiply Behaviour, Magnitude, Delivery, Order (Override
    only), Retention Group and Impact Types. Delivery offers what the duration can make, and a help box says when the
    one it names can't happen. **Add Impact** adds one the duration can make: Continuous on a lasting effect, On Apply
    on an Instant one. Add and Remove are one Undo step each.
  - **The Cost rule** checked only the first impact, so 0 stamina paid a cost of 10 mana and 10 stamina. It checks
    each attribute a cost's impacts spend.
  - **ED-2, the impact drawer,** `GameplayEffectImpactDrawer`, follows the style sheet: Attribute; Operation, the
    mockup's one choice for the operation and the value it changes, such as Add to Current; Multiply Behaviour
    (Multiply only); Magnitude; then Delivery and, for a periodic impact, Commits, read from the runtime's own
    `AbilityInspector.BuildEffectSeries`. A note says when Tick On Application alone keeps a multiply or override as a
    modifier. Reverse On Removal is gone, since nothing reads it. Comparison shows for every affiliation but Always
    Allow, and the list for Use Affiliation List. It drew tinted boxes with colored headings, and a contained effect
    drew a tinted box with a stripe in its policy's color, set 50 ms after it drew; that's one row now.
  - **Docs:** `EFFECTS.md` (Impact Specification), `EFFECT_PIPELINE.md` §1 and §7, an amendment to
    `IMPACT_MODEL_REFACTOR.md`, `ABILITIES.md` (the Cost check) and `CLAUDE.md`.
  - **Their tests,** `ED_3_An_effect_makes_each_of_its_impacts`, `ED_3_Each_impact_keeps_its_own_delivery` and
    `ED_3_A_cost_is_checked_for_each_attribute_it_spends`, on both hubs, and `ED_2_The_impact_says_how_it_is_delivered`,
    `ED_3_An_additional_impact_draws_as_an_entry` and `ED_3_An_impact_is_added_and_removed_in_place`, were seen
    failing first.
  - **The control** `The_impact_drawer_follows_its_operation` finds Multiply Behaviour as the enum field it is now.
  - **Tests:** PlayMode 957: 950 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    398 of 398.

- 2026-11-04, **Step 5: the Ability inspector (ED-37).** The redesign's fifth surface, after the other asset
  inspectors' pattern and the Ability mockup.
  - **Its sections:** Identity, Leveling, Tags, Activation, Stages, Requirements, Workers, Local Data and Used By.
    Identity was Definition, and holds the asset tag under the name. Level Source merged into Leveling. Runtime
    Behaviour, which came before Tags, split into Activation and Stages.
  - **Activation** holds When Busy, Activate At Start, Cost, Cooldown and the targeting, as the mockup's does. Cost and
    Cooldown sat in a tinted box at the end of Definition. Implicit Tag shows while Use Implicit Targeting is on; it
    showed as the inspector opened, whatever the toggle said.
  - **When Busy** is the mockups' name for Activation Policy, in the Ability and Entity inspectors. Its choices show by
    what they do: Entity's Policy, Activate Anyway, Refuse and Queue. They showed the enums' names, such as Queue
    Activation If Busy. The two enums carry `InspectorName`s, so every field of theirs shows the same names, and the
    base editor's new `EnumName` reads them for the summaries.
  - **Activate At Start** is the mockup's Activate When Granted, renamed to say what the runtime does: it tries the
    ability only as the entity's GAS starts, for its Starting Abilities. An ability an item or code grants later
    doesn't try it. Making such a grant try it would be a runtime change, so it waits for the user.
  - **Requirements** holds the tag requirements, which sat in Tags, and the checks, which sat in Validation Rules:
    Caster Checks and Target Checks, the mockup's names for Source and Target Rules. Clear empties the tag
    requirements and leaves the checks, since an ability without its Cost and Cooldown checks would skip them.
  - **Summaries,** such as "Queue · Fireball Cost · Fireball Cooldown", "3 stages" and "4 checks · 2 tag
    requirements".
  - **Multiple selection** works as before (ED-12). The targeting, the stages and the checks, which each ability keeps
    of its own, ask for one ability.
  - **Docs:** `ABILITIES.md` follows the inspector, and `ENTITIES.md` names When Busy. The help topics name the new
    headings: `abilities/activation` and `abilities/stages` are new, and `abilities/runtime` and
    `abilities/level-source` are gone.
  - **Its tests,** `ED_37_The_Ability_inspector_keeps_the_style_docs_sections`,
    `ED_37_The_Ability_inspector_sets_no_look_in_code` and `ED_37_The_Ability_inspector_lists_what_uses_it`, were seen
    failing first. `ED_12_An_edit_in_the_Ability_inspector_undoes` finds the provider in Leveling now.
  - **Tests:** PlayMode 949: 942 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    395 of 395.

- 2026-11-04, **ED-39: the attribute set element drawer.** It wrote to the set as it drew, and it drew in its own look.
  - **No write while drawing.** 50 ms after an element drew, and on every change to the set, it clamped a magnitude
    outside the bounds into the asset. It writes nothing now: a help box says the magnitude is outside its bounds and
    what the runtime clamps it to. Only a value typed into a Magnitude field is kept within the bounds, as part of the
    same edit, so one Undo takes it back; a typed Base keeps Current within the bounds Base sets.
  - **Only a typed value.** Binding a field sends it change events of its own as it draws, so the drawer clamps only
    while the field has the focus, as it does while a value is typed. The first version clamped on those events and
    wrote as it drew; the test caught it.
  - **Its look,** from the style sheet: a foldout row with the attribute (the new reference field), and while folded,
    one line such as "Current 150 · Base 100 · 0–100". Current and Base sit on a two-button switch, the copy buttons say
    what they copy ("Copy Base's Settings"), and Link Current to Base sits among the fields. It drew a tinted, rounded
    box with a border in its collision policy's color, tab buttons, arrows in its button text and 9 px colored text.
  - **Docs:** `ATTRIBUTE_SETS.md` describes the element as it draws now.
  - **Its tests,** `ED_39_Opening_an_attribute_set_writes_nothing` and `ED_39_A_typed_magnitude_stays_in_bounds`,
    were seen failing first. The first failed on the old drawer's write only once it changed the set elsewhere: the
    50 ms clamp doesn't land in batch mode.
  - **Tests:** PlayMode 949: 942 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    392 of 392.

- 2026-11-04, **ED-38: the reference fields.** Every reference to a PlayForge asset, in every inspector and drawer.
  - **Unity's object field,** from the reworked `AbstractForgeObjectDrawer`, with a plain button beside it: Open,
    which shows the asset in a window of its own (`EditorUtility.OpenPropertyEditor`), so the asset that holds the field
    stays in the Inspector; or New, while the field is empty. It was a button that opened a dropdown list of its own,
    with Clear and Open icon buttons and hover colors set in C#. `AbstractRefDrawer` stays for the type pickers.
  - **New** makes the asset in the folder The Forge's settings name for its type, with their file prefix and suffix,
    named for the asset that holds the field and the field, as in "Goblin Attribute Set", with the tag its name gives
    (the new `ForgeAssetCreation`, whose tag rule the inspectors now share). One Undo takes back the field's value; the
    asset stays. An effect New makes takes its levels from the holder, as the old + button's did.
  - **An effect field** asks nothing when it's set. It asked in a dialog whether to link the effect, and again when the
    effect was linked elsewhere. A line under it says how the effect takes its levels, with Link or Link Here, one Undo
    step each. Its right-click menu holds the field's template (copy it or use it, which asked in a three-way dialog),
    a searchable pick of the effects nothing links (the old All and Unlinked button), and New Linked Effect.
  - **Docs:** `ABILITIES.md`, `ITEMS.md` and `GETTING_STARTED.md` describe the fields.
  - **Its tests,** `ED_38_An_asset_reference_is_Unity_s_object_field`, `ED_38_New_makes_an_asset_and_uses_it` and
    `ED_38_An_effect_reference_offers_its_link_in_place`, were seen failing first.
  - **Tests:** PlayMode 949: 942 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    390 of 390.

- 2026-11-04, **GAS-54 and GAS-55: two attribute set bugs.** Found while building the attribute table; the user asked
  for bugs found along the way to be fixed as they appear.
  - **GAS-54:** an element with no Attribute threw as the entity started, since `AttributeSetMeta` keys a dictionary
    by the attribute, so the entity got none of its attributes. It is passed by, and `GetUnique` no longer counts it.
  - **GAS-55:** `ForgeUtils.IndexMin` started from `float.MinValue`, so Use Minimum always took the first element. It
    starts from `float.MaxValue`. The attribute table settles Use Minimum through the same helper, so it follows.
  - **Their tests,** `GAS_54_An_element_with_no_attribute_gives_nothing` and `GAS_55_Use_Minimum_takes_the_lowest`,
    on both hubs, were seen failing first: the first threw `ArgumentNullException`, the second gave 100, not 60.
  - **Tests:** PlayMode 949: 942 pass, 0 fail and 7 planted are skipped, in three orders, the golden scenarios
    among them. Planted 7 of 7; EditMode 387 of 387.

- 2026-11-04, **Step 5: the Attribute Set inspector (ED-37).** The redesign's fourth surface, after the Item and Entity
  inspectors' pattern.
  - **Its sections:** Identity, Leveling, Attributes, Sub-Sets, Collision Settings, All Attributes, Workers, Local Data
    and Used By. Identity was Definition, and Import no longer copies the name (ED-35's rule). Level Source merged into
    Leveling.
  - **All Attributes** replaces the Summary section, which counted the unique attributes and opened a dialog that
    listed their names. It is the Entity mockup's attribute table, in the kit's new `ForgeAttributeTable`: each
    attribute the set gives, its sub-sets' included, the value its Current starts at, and its range. Where elements
    overlap, it settles the value by the runtime's rules (`AttributeSetMeta`), through the runtime's own helpers; a
    value a scaler sets reads Scaled. The Entity inspector shows the same table under its Attribute Set, which it owed
    from its own step, and both follow the set as it changes.
  - **Summaries** replace the counts beside the Attributes and Sub-Sets titles: "2 attributes", "1 sub-set", "Use
    Maximum".
  - **New row ED-39:** the element drawer clamps magnitudes into the asset when it opens. Its redesign is a step of its
    own.
  - **Docs:** `ATTRIBUTE_SETS.md` follows the inspector, and `ENTITIES.md` names the table.
  - **Its tests,** `ED_37_The_Attribute_Set_inspector_keeps_the_style_docs_sections`,
    `ED_37_The_attribute_table_shows_what_an_entity_starts_with`, `ED_37_The_Attribute_Set_inspector_sets_no_look_in_code`
    and `ED_37_The_Attribute_Set_inspector_lists_what_uses_it`, were seen failing first.
    `ED_12_An_edit_in_the_Attribute_Set_inspector_undoes` finds the provider in Leveling now.
  - **Noticed in the runtime,** and fixed next as GAS-54 and GAS-55: an attribute set element with no Attribute threw
    as an entity started, and Use Minimum always took the first element. The table follows the runtime, so it settles
    Use Minimum correctly from then on.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    387 of 387.

- 2026-11-04, **Step 5: the Entity inspector (ED-37).** The redesign's third surface, after the Item inspector's
  pattern and the Entity mockup.
  - **Its sections:** Identity, Leveling, Tags, Attributes, Abilities, Items, Workers, Local Data and Used By. The
    Attribute Set moved out of Definition into Attributes, a section the inspector had left out. Leveling was Level,
    after Tags, with Level Source apart from it. Tags shows Affiliation first, as the mockup does.
  - **Items:** Allow Duplicates and Equipped Too, the mockup's names. Equipped Too indents under Allow Duplicates and
    waits for it, with a note beside it: an inventory that holds one of each item has no second copy to equip. A
    starting item takes one row, the item and an Equip checkbox, in the new `StartingItemDrawer`; it drew as a foldout.
  - **Shared:** the Item inspector's Leveling pieces moved into the base editor (`AddLinkedLevels`, `CreateChildLinks`,
    `LevelingSummary`, `LevelingState`, `TagsSummary`, `WorkerCount`), so the two inspectors share one copy. The
    entity's child links say Link Items & Abilities; they said Ability & Effects.
  - **Import and Clear** work on every section. They leave the name, the asset tag, and the Max limits, whose scalers
    the Import window doesn't copy yet (ED-32, ED-33).
  - **Left for later:** the mockup's attribute table, with the Attribute Set inspector, which decides how an element's
    value shows; the mockup's "When Busy" for Activation Policy, with the Ability inspector, whose mockup uses it too;
    and the reference fields, which the new row ED-38 tracks.
  - **Docs:** `ENTITIES.md` follows the inspector.
  - **Its tests,** `ED_37_The_Entity_inspector_keeps_the_style_docs_sections`,
    `ED_37_The_Entity_inspector_sets_no_look_in_code`, `ED_37_The_Entity_inspector_lists_what_uses_it`,
    `ED_37_Equipped_Too_needs_Allow_Duplicates` and `ED_37_A_starting_item_takes_one_row`, were seen failing first.
    `ED_12_An_edit_in_the_Entity_inspector_undoes` finds the provider in Leveling now.
  - **Noticed, not fixed:** run right after the inspector tests that make and delete assets, as a filtered run
    puts it, `ED_12_An_edit_in_the_Entity_inspector_undoes` logs Unity's `IsInSyncWithParentSerializedObject()`
    assertion: a binding request whose SerializedProperty went stale. The old Entity inspector does the same in
    that order, and the test passes alone and in the full suite. A drawer's deferred rebuild is the likely cause,
    such as ScalerDrawer's, which binds to a SerializedObject of its own (ED-12's left part).
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    383 of 383.

- 2026-11-04, **Step 5: the Item inspector (ED-37).** The redesign's second surface. The new row ED-37 tracks how the
  asset inspectors stray from the style doc, one inspector at a time, the Attribute inspector first.
  - **Its sections:** Identity, Leveling, Tags, Effects, Abilities, Workers, Local Data and Used By, in the style doc's
    order (Q9). Identity was Definition, and holds the asset tag under the name, in the kit's new
    `ForgeAssetTagField`: a read-only text field in secondary text, whose Update shows while the stored tag differs.
    The tag sat at the top of Tags.
  - **Leveling** holds what Level Source did: Link Mode, then the item's own range, or its provider and the range the
    provider gives. The provider is Unity's object field, whose right-click menu picks one type (the style doc's "link
    filter"), and a help box says when none is set. Link and Unlink Ability & Effects follow as plain buttons. Level
    Source drew the link in tinted boxes with colored borders, under sentences and a small bold heading.
  - **Summaries:** each collapsed section shows one line, such as "Levels 1–20 · from Hero", "1 effect" or "None".
  - **Import and Clear** work on every section. Seven of the eight only logged a warning. They leave the name and the
    asset tag, which identify the item (ED-35's rule).
  - **Used By** lists the assets that use the item, such as an entity that starts with it or an effect linked to it.
  - **No look in code:** the inspector sets no color, border or font in C#. The style sheet has new rules for value
    fields, rows of buttons, help boxes and notes. The Attribute inspector's multiple-selection note uses them too.
  - **Docs:** `ITEMS.md` and `ATTRIBUTES.md` follow the two inspectors, and their help topics name the new headings.
    `ATTRIBUTES.md` still described Find All References.
  - **Its tests,** `ED_37_The_Item_inspector_keeps_the_style_docs_sections`, `ED_37_The_Item_inspector_sets_no_look_in_code`,
    `ED_37_The_Item_inspector_lists_what_uses_it` and `ED_37_The_Level_Provider_field_links_the_item`, were seen failing
    first. `ED_12_An_edit_in_the_Item_inspector_undoes` finds the provider in Leveling now.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    378 of 378.

- 2026-11-04, **Step 5 begins: the Attribute inspector.** The first surface of Phase 4's redesign, in the style doc's
  rules.
  - **Its sections:** Identity, Local Data and Used By. Identity was Definition: the style doc's section order starts
    with it (Q9).
  - **Used By** lists the PlayForge assets whose data refers to the attribute, each with its type icon, and a row
    selects its asset. It follows the project as it changes (Q30). It replaces a section of sentences about where
    attributes can go and a Find All References button that opened a dialog telling the user how to search, which is
    ED-15's first part.
  - **Summaries:** a collapsed section shows one line: the abbreviation, how many Local Data entries, how many assets
    use the attribute.
  - **One label column:** a bound text field's label lines up with the PropertyFields' in every inspector.
  - **Its test,** `ED_15_The_Attribute_inspector_lists_what_uses_it`, was seen failing first.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. In the first run of order
    90210 a timing test, `GAS_P3_An_application_costs_the_same_however_much_the_entity_carries`, measured 0.0073 ms
    a re-application against its 0.0024 ms limit; it passed three runs alone, and the rerun of that order passed.
    Planted 7 of 7; EditMode 374 of 374.

- 2026-11-04, **ED-36: the asset header stays at the top.** The user looked at ED-20 in the editor and asked for the
  header to stay at the top of the inspector while the content scrolls (Decision 29), which reverses Q17's answer.
  The header now comes after the content, so it draws over it, and a space of its height holds its place at the top.
  It follows the Inspector's scroll view by its events and its own layout, so nothing polls, and it stops where its
  inspector ends. Before ED-11 it stayed put by hiding the Inspector's scroll view and measuring the window on timers.
  Its test, `ED_36_The_asset_header_stays_at_the_top_as_the_content_scrolls`, was seen failing first;
  `ED_11_The_asset_header_scrolls_with_the_content` is now `ED_11_The_inspector_keeps_no_scroll_view_of_its_own`.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    373 of 373.

- 2026-11-04, **ED-20, part 2: no emoji.** The editor showed 102 emoji, as icons and inside its text: pictographs
  such as a link, a clipboard or a mage, and symbols with an emoji form, such as the warning sign, the lightning bolt
  and the gear, which an emoji font draws in color. Each is now one of three things:
  - **A kit glyph:** Link (new), Warning, Search and Import, and the type icons where The Forge showed an asset's type.
  - **A word,** on a button that was an icon alone: Copy, Preview, Visualize, Ping, Trash, Duplicate, All and Unlinked.
  - **Nothing,** where it only decorated a text, as in "Quick Fill Wizard".
  - **Where a glyph shows:** each drawer that shows one takes the theme on its own root, so the glyph is sized and
    colored in Unity's own inspector too. The windows that show one do the same.
  - **Left for the redesign:** text symbols with no emoji form, such as a check mark or an arrow, go with each
    surface's redesign. ED-20 is fixed.
  - **Its test,** `ED_20_No_editor_text_shows_an_emoji`, which scans the editor code, was seen failing first.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    372 of 372.

- 2026-11-04, **ED-20, part 1: one style sheet and a kit of controls.** Phase 4's fourth step, in the style
  `Docs/EDITOR_STYLE.md` records (Decision 20).
  - **The style sheet:** `Editor/UI/PlayForge.uss` holds every color the style doc lists as a variable, in a Dark and a
    Light set, and the kit's rules. `ForgeTheme.Apply` gives a root the sheet and the editor's theme, `.pf-theme-dark`
    or `.pf-theme-light`, chosen by `EditorGUIUtility.isProSkin`. The sheet loads by its GUID, so it's found whether
    the package is in `Packages` or in `Assets`.
  - **The kit,** in `Editor/UI/Kit`: `ForgeGlyph` draws the mockups' vector glyphs with `Painter2D`, in the color
    the sheet gives it; `ForgeTypeIcon` is a type's glyph in its type's color; `ForgeIconButton`, `ForgeSection` and
    `ForgeAssetHeader` build on them. None sets a style in code.
  - **What uses it:** every asset inspector's header and sections, through `BasePlayForgeEditor` and
    `CreateCollapsibleSection`, whose signature and result stay. The header shows the type icon, the name, the type
    and asset tag, then Open in The Forge, Help and a menu that holds import, the template and Visualize. It no longer
    shows the description or the asset's own icon. Sections keep Import, Clear and Help in view; they appeared only on
    hover (Q18). The Forge and the GAS Debugger take the theme instead of painting their windows dark gray.
  - **Removed:** a style sheet nothing loaded and two UXML layouts nothing used, with the three scripts' default
    references to them.
  - **Left in ED-20:** the emoji. Each surface's own inline styles go with its redesign, Phase 4's fifth step.
  - **Its tests,** in the smoke suite, so each runs in Dark and in Light: every color resolves to the style doc's value,
    the header's type icon takes its type's color, the header and section headers set no style in code, the section
    actions always show, and The Forge and the GAS Debugger take the theme. `ED_20_The_editor_has_one_style_sheet_and_no_unused_layouts`
    scans the editor folder. All were seen failing first. A probe confirmed the glyphs draw in batch mode, each at its
    size and in its theme's color.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    371 of 371.

- 2026-11-04, **Phase 4's gate: the editor smoke suite.** Phase 4's third step, from the UI interview's Q36:
  `EditorSmokeTests` in Proving Grounds runs in each editor theme, Dark and Light, and checks two things.
  - **Every inspector and window opens without an error.** The inspectors open on a new asset of each type, on two
    selected together, and on each of the 82 assets in the Demos sample's Data folders, copied in while the suite
    runs. The 19 windows open the way the editor opens them, in 30 cases: The Forge on each tab, the debuggers, every
    import window and every dialog. An IMGUI window's `OnGUI` runs too, handed a layout event, since batch mode
    repaints no view.
  - **Every edit in an inspector undoes.** A sweep changes each value an inspector shows, one at a time, and one Undo
    must put the asset back exactly as it was. It covers a new asset of each type, a MonoProcess, and the fullest
    Demo asset in each Data folder. One sweep reaches up to 63 controls and makes up to 42 edits.
  - **Coverage:** before it checks an inspector, the suite opens every collapsed part: the sections, each drawer's
    collapsed parts and every foldout. It shows a section without its header's click, which saves the state
    machine-wide, as the user's. Two checks fail when a new inspector or window isn't in the suite, and three check
    the suite itself: OnGUI runs, every list draws a row for each element, and an edit that records no Undo is
    reported.
  - **Found:** nothing. Every inspector and window opens without an error in both themes, and every edit undoes.
    ED-12's work shows here: before it, the inspectors wrote 42 edits straight into their asset.
  - **How the theme is set:** through Unity's own skin setting, which saves it machine-wide as the user's choice in
    the `UserSkin` EditorPrefs key. Each fixture puts that key back as it found it.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    360 of 360, the suite's 296 among them.

- 2026-11-04, **ED-18: a drawer keeps its collapse state per asset.** Eight drawers kept whether a part is collapsed
  in static dictionaries keyed by property path alone. So opening a part on one asset opened it on every asset, a
  domain reload forgot it, and a moved worker took the state of the worker that had been in its place. Deleting a
  stage cleared every stage's state, so they all opened. One helper, `DrawerCollapseState`, keeps it per asset in
  SessionState, for the editor session and through domain reloads, with each drawer's own default. A
  [SerializeReference] field keeps it with the object it holds, so it follows the object through its list, and
  deleting a stage moves the later stages' states up. The scaler's Quick Fill state, which nothing wrote, and the base
  inspector's unused section dictionary are gone. Its tests, the three in `DrawerCollapseTests` and
  `ED_18_No_drawer_keeps_collapse_state_in_a_static_dictionary`, were seen failing first. With ED-11, Phase 4's second
  step, no polling, is done.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    64 of 64.

- 2026-11-04, **ED-11, the sticky header: the header scrolls with the content.** The asset header stayed put while
  the content scrolled under it. For that, each inspector hid the Inspector's own scroll view and gave its content one
  of its own, sized by measuring the window. Two loops measured it again every 200 and 300 ms for as long as the
  inspector stayed open. Q17 lets the header scroll with the content, so the inspector keeps no scroll view and
  measures nothing. The Forge's Inspector settings, the two delays those loops read, are gone, and with them ED-6's
  test of the delay row. Its test, `ED_11_The_asset_header_scrolls_with_the_content`, was seen failing first. ED-11
  is fixed.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    60 of 60.

- 2026-11-04, **ED-11, the object picker: an inspector listens only while a picker is open.** Every asset inspector
  added itself to `EditorApplication.update` as it opened and ran every frame of its life, to catch an object
  picker's choice. Now the two pickers, import and level provider, add it as they open, and it takes itself off when
  the picker closes. Its test, `ED_11_An_idle_inspector_listens_to_no_update`, was seen failing first.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    60 of 60.

- 2026-11-04, **ED-11, the drawer timers: a view follows the properties it shows.** Phase 4's second step, no
  polling, starts with the drawers. The Impact drawer re-checked its operation and its effect's duration every
  250 ms, and the Effect drawer rebuilt its link info every 500 ms, for as long as an inspector stayed open. Each now
  follows the property it shows, through `TrackPropertyValue`; the Effect drawer also follows the effect its field
  names, so a link made elsewhere shows. Its tests: `ED_11_No_editor_code_repeats_a_timer`, which scans the editor
  code, and `The_impact_drawer_follows_its_operation`, written as a control, were both seen failing first. The second
  failed for a reason of its own: the timer re-read a stale copy of the operation, so an Undo never hid Multiply
  Behaviour again.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    59 of 59.
  - **Left in ED-11:** the sticky header, which Q17 makes scroll with the content, and the object picker's per-frame
    listener.

- 2026-11-04, **ED-12: the Ability inspector edits a multiple selection.** As in the Item inspector, a field the
  abilities disagree on shows as mixed and an edit reaches every ability. Name, the asset tag, linking and a linked
  ability's levels step aside the same way. Targeting, the asset loaders, the stages and the activation rules are
  objects and lists each ability keeps of its own, and the stage drawers resize lists directly, so a multiple
  selection could cut a longer list. A note stands in for them until one ability is selected. Its test,
  `ED_12_The_Ability_inspector_edits_every_selected_ability`, was seen failing first.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    57 of 57.
  - **Left in ED-12:** the Effect, Entity and Attribute Set inspectors' multiple selection, which waits for
    ScalerDrawer's one-target copies, reworked with Decision 28 (ED-32, ED-33).

- 2026-11-04, **ED-12: the Item inspector edits a multiple selection.** It refused one. Now a field the items
  disagree on shows as mixed and an edit reaches every item. What acts on one item steps aside:
  - Name stays read-only, since each item's tag comes from its own name, and the asset tag row hides.
  - Level Source shows Link Mode alone, without the provider or the child links.
  - Leveling edits the range when every item is standalone, since a linked item's range comes from its provider.
  - The base's `IsMultiple` is now visible to inspectors. Its test, `ED_12_The_Item_inspector_edits_every_selected_item`,
    was seen failing first.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    56 of 56.

- 2026-11-04, **ED-12: a type field's drawer writes nothing while drawing.** Five type drawers wrote their default
  type into an empty `[SerializeReference]` field as they drew. So opening an asset changed it, and a field whose
  class Unity couldn't find lost the data Unity kept for it. The write is gone, and so is `GetDefault`, which nothing
  else called. How the runtime treats each field empty decided the rest:
  - An empty set-assets loader and an empty stage policy were already handled: the loader is skipped, and the stage
    runs as When All. The stage policy drawer's default, `WhenAllStagePolicy`, was the wrong type, so its write had
    stored nothing anyway.
  - An empty activation rule and an empty scaler behaviour threw, the first whenever the ability was checked and the
    second on every evaluation. Both are now skipped, as the loaders are. A list's "+" leaves such a slot.
  - An Inject task's injection now starts as Interrupt from its field initializer, which the type picker runs when it
    makes the task.
  - Its tests, `ED_12_Drawing_an_empty_type_field_leaves_it_empty` and `ED_12_A_new_inject_task_interrupts` in
    `TypeRefDrawerTests`, and `ED_12_An_empty_activation_rule_asks_nothing` and
    `ED_12_An_empty_scaler_behaviour_changes_nothing` on both hubs, were seen failing first.
  - **Tests:** PlayMode 945: 938 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    55 of 55; the custom golden runner in PlayForge 25 of 25.

- 2026-11-04, **ED-12: a rebuilt drawer edits every selected asset.** Five drawers rebuilt themselves on a new
  SerializedObject over the first selected asset: the ability behaviour and stage drawers, the worker group drawer,
  and the two tag requirement drawers. After a rebuild, such as opening one, an edit reached that asset alone. The tag
  drawer did the same for every read and every pick. Each now rebuilds or reads through its own SerializedObject,
  refreshed first, and a rebuild that comes after the drawer has left its panel is skipped. Its test,
  `ED_12_A_rebuilt_drawer_edits_every_selected_asset`, opens the Effect requirements drawer on two effects and edits
  the group's Name; it was seen failing first, with the second effect untouched. ScalerDrawer's two copies wait for
  Decision 28's scaler work (ED-32, ED-33).
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    53 of 53.

- 2026-11-04, **ED-31: each unlink question counts the children it would change.** The Effect, Ability, Item and
  Entity inspectors' Unlink All questions counted children linked to `this`, the editor, which no asset links to, so
  they always said 0. Each now counts as its asset's `UnlinkAllChildren` decides: an effect's contained effects linked
  to its provider, and an ability's, item's or entity's children linked to the asset itself. Its test,
  `ED_31_The_unlink_question_counts_the_linked_children`, which links one child of each and reads the question, was
  seen failing first.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    52 of 52.

- 2026-11-04, **ED-12: Link, Unlink and template assignment undo.** Each asset inspector's Link and Unlink buttons,
  for the asset's own provider and for its children (contained effects, cost and cooldown, granted effects and the
  active ability, starting items and abilities), the provider picker's link, and the template picker's Assign and
  Unassign all wrote into the assets with no Undo. Each now records every asset it changes first, through the base's
  `RecordLinkChange`, so one Undo takes it back. Their 13 confirmations ask through a hook, `ConfirmLinkChange`, that a
  test can answer, and the two pickers' steps are the methods `LinkTo` and `AssignTemplate`. Its test,
  `ED_12_Linking_in_the_Effect_inspector_undoes`, was seen failing first, after the hook and the two methods were in
  and before the recording was.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    51 of 51.
  - **Still open:** ED-31, the unlink dialogs that count with the editor rather than the asset, so they say 0.

- 2026-11-04, **ED-34: opening a scaler writes nothing.** The user chose to fix it now, ahead of the scaler workflow
  of Decision 28. Opening a scaler wrote to it while drawing. On an effect with no level link, Lock mode cut its
  per-level table to one value, since the unlinked effect counts as its own level provider, with a Max Level of 1. The
  other modes resized the table to Max Level, and a Max Level of 0 or less became 10. Building the drawer also cloned
  a scaler that another list element shared. Now drawing writes nothing: a table keeps its length until the user
  changes it, and a level past its end reads its last value, as `GetValueAtLevel` already did. A shared scaler shows
  a warning under its header, with a button that makes a separate copy as one undoable step. Its tests,
  `ED_34_Opening_a_scaler_leaves_its_table_alone` and `ED_34_Opening_a_shared_scaler_leaves_it_shared`, were seen
  failing first: the first lost four of five values, and the second found its scaler cloned.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    50 of 50.
  - **Found on the way:** inside the Attribute Set inspector, in the test's panel, the elements' scaler drawers never
    ran, so the second test draws the element's scaler with a PropertyField of its own.

- 2026-11-04, **ED-12, the Ability inspector: its fields undo.** Its Description, Activation Policy, Activate
  Immediately and Link Mode wrote straight into the asset, with no Undo, and Level Source and Leveling were drawn from
  the link mode once. All now bind, and `OnAssetChanged` redraws the two sections when the mode changes, an Undo among
  them. Its test, `ED_12_An_edit_in_the_Ability_inspector_undoes`, was seen failing first. Every asset inspector's own
  fields now undo; their Link and Unlink buttons are next.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    48 of 48.

- 2026-11-04, **ED-12, the Attribute Set inspector: Description and Link Mode undo.** As in the Effect and Item
  inspectors, both wrote straight into the asset with no Undo, and Level Source and Leveling were drawn from the mode
  once. Both now bind, and `OnAssetChanged` redraws the two sections when the mode changes, an Undo among them. Its
  test, `ED_12_An_edit_in_the_Attribute_Set_inspector_undoes`, was seen failing first.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    47 of 47.

- 2026-11-04, **ED-12, the Entity inspector: every field undoes.** Its Description, Starting and Max Level,
  Activation Policy, Allow Duplicate Abilities, Items and Equipped Items, and Link Mode all wrote straight into the
  asset, with no Undo, and Level Source and Leveling were drawn from the link mode once. All now bind, and
  `OnAssetChanged` redraws the two sections when the mode changes, an Undo among them. Its test,
  `ED_12_An_edit_in_the_Entity_inspector_undoes`, which edits and undoes each in turn, was seen failing first. ED-29's
  test still finds each toggle showing its own value.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    46 of 46.

- 2026-11-04, **ED-12, the Item inspector: Description and Link Mode undo.** As in the Effect inspector, both wrote
  straight into the asset with no Undo, and Level Source and Leveling were drawn from the mode once. Both now bind,
  and `OnAssetChanged` redraws the two sections when the mode changes, an Undo among them. Its test,
  `ED_12_An_edit_in_the_Item_inspector_undoes`, was seen failing first.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    45 of 45.

- 2026-11-04, **ED-12, the Effect inspector: Description and Link Mode undo.** The Effect inspector wrote its
  Description and Link Mode straight into the asset, with no Undo, and drew its Level Source section from the mode
  once, so after an Undo it showed a mode the effect no longer had. Both now bind to their properties. The base's new
  `OnAssetChanged` hook, called after every change to the asset along with the header's update, lets the inspector
  redraw Level Source when the mode changes, an Undo among them. Its rename already undid in one step (Phase 2). Its
  test, `ED_12_An_edit_in_the_Effect_inspector_undoes`, was seen failing first.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    44 of 44.
  - **Left in this inspector:** Link and Unlink, which every inspector shares and which come as one part, and a
    multiple selection, which waits for the drawers and the scaler workflow.

- 2026-11-04, **ED-12, the MonoProcess inspector edits a multiple selection.** It binds every field already, but it
  lacked `[CanEditMultipleObjects]`, so the Inspector never gave it a multiple selection and showed "Multi-object
  editing not supported" instead. Its test, `ED_12_The_MonoProcess_inspector_edits_every_selected_process`, was seen
  failing first. It edits Lifecycle on two processes through the inspector's own field, and one Undo takes it back.
  - **Found on the way:** a SerializedObject over several objects flagged DontSave writes only to the first of them,
    so a multiple-selection test uses plain scene objects. PropertyField draws an enum as a popup of its names.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    43 of 43.
  - **Next:** the Effect, Entity and Attribute Set assets hold scalers, and ScalerDrawer edits a one-target copy of
    its SerializedObject; it will change with the scaler workflow the user is deciding. So those three inspectors get
    Undo for every edit first, and a multiple selection after that.

- 2026-11-04, **ED-12, the Attribute inspector: every edit undoes, and it edits a multiple selection.** Phase 4 starts
  with Undo and multi-editing, as Q35 asks, one inspector at a time. The Attribute inspector wrote its Name,
  Abbreviation and Description straight into the asset when a field lost focus, with no Undo. They now bind to their
  properties and write when an edit is committed, so each edit goes through the SerializedObject. An inspector that
  writes that way turns on the base's new `SupportsMultipleObjects` hook and takes a multiple selection: its header
  counts the selection and leaves out Template, Visualize and Open in The Forge, which act on one asset. Every
  inspector's header now follows the asset's changes, an Undo or a Redo among them. Its tests,
  `ED_12_An_edit_in_the_Attribute_inspector_undoes` and `ED_12_The_Attribute_inspector_edits_every_selected_attribute`,
  were seen failing first. They build the whole inspector in a runtime panel, where its bindings run in batch mode.
  - **Surveyed:** the inspectors wrote 42 edits straight into their asset, and only the 5 renames recorded Undo. No
    inspector took a multiple selection. Most drawers already write through SerializedProperty; six copy their
    SerializedObject for one target, and two write while drawing. ED-12's row lists what is left.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    42 of 42.
  - **Order:** the Editor and UI section now lists Phase 4's order, from the interview's Q35, Q36 and Q3.

- 2026-11-04, **Phase 3 closed.** Its exit gate asks for zero steady-state allocation, enforced in CI. The first
  half is met: every steady-state frame the gate's measuring covered allocates nothing. The repo has no CI, so the
  user chose to close the phase on the local gates (Decision 27). All 23 Phase 3 rows move from `fixed` to
  `verified`, and GAS-51 stays `moved`, as FEAT-14.
  - **The last full runs:** PlayMode 941 tests, 934 pass and 7 planted are skipped, in three orders; planted 7 of 7;
    EditMode 40 of 40. The run was on the code as committed at `ec008f7`, and later commits change only docs. Once
    the user had closed the editor, the custom golden runner in PlayForge passed 25/25 in batch mode.
  - **Filed:** SP-24, CI, which waits on two things only the user can provide (Decision 27).
  - **Scope not built:** the roadmap gave Phase 3 pooling and a Register builder. Decision 23 settled pooling:
    PC-24 removed the stub instead of building a pool. No row asked for a Register builder, so none was built; it
    is now FEAT-15, in Phase 5.
  - **Next:** Phase 4 on `main`, the editor redesign, in the style `Docs/EDITOR_STYLE.md` records (Decision 20).

- 2026-11-04, **TS-11: a task waiting in a delay allocates nothing a frame.** A task asks whether its sequence is
  paused by looking its token up, and the dictionary's default comparer boxed the token on every lookup under Unity's
  Mono, where a token isn't IEquatable. The dictionary now compares tokens as tokens. Its test,
  `TS_11_A_frame_in_which_an_ability_waits_in_a_delay_allocates_nothing`, on both hubs, in a `DelayTask` and a
  `ChannelAbilityTask`, was seen failing first.
  - **Measured,** in bytes a frame: 24 before, on both hubs and in both tasks; 0 now.
  - **Tests:** PlayMode 941: 934 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    40 of 40. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Phase 3's exit gate:** with GAS-P10 and this, every steady-state frame the gate's measuring covered allocates
    nothing. Its "enforced in CI" is still to settle: the repo has no CI, and the allocation tests live in Proving
    Grounds, which has no remote.

- 2026-11-04, **GAS-P10: a frame whose summary has a listener allocates nothing.** Each frame made a new snapshot for
  the `OnFrameComplete` listeners, copying the frame's ten lists. The listeners' snapshot is now the entity's own,
  refilled each frame, so a listener reads it during the call. Its test,
  `GAS_P10_A_frame_whose_summary_has_a_listener_allocates_nothing`, on both hubs, idle and with an effect ticking, was
  seen failing first. Its control, `GAS_39_The_frame_snapshot_carries_effects_injections_and_deaths`, which reads the
  snapshot in several frames, passed before and after.
  - **Measured,** in bytes a frame: 504 idle and 824 with an effect ticking before, on both hubs; 0 now.
  - **Tests:** PlayMode 937: 930 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    40 of 40. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Calls made:** Decision 26. `FrameSummary.CreateSnapshot` still makes a copy to keep. Every listener in the
    package and in Proving Grounds already read the snapshot during the call.

- 2026-11-04, **GAS-53: interrupting every running ability ends each.** `Inject` with a policy, `InjectAll` and
  turning `Enabled` off walked the live set of the abilities running under a policy. TS-Z made an interrupted ability
  end inside the interrupt, where it takes itself off that set, so the walk threw "Collection was modified" once the
  first had ended, and the rest ran on. It now walks a copy, from a pooled array. Its test,
  `GAS_53_Interrupting_every_running_ability_ends_each`, on both hubs and each way of interrupting, was seen failing
  first: each threw "Collection was modified".
  - **Tests:** PlayMode 933: 926 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    40 of 40. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock. The first run in the default order failed `GAS_P4_Refreshing_a_modifier_allocates_nothing` on
    the runtime hub by 2.7 bytes a run, about 1,350 bytes over its 500 runs: one burst, not a cost per run, in a path
    GAS-53 doesn't touch. The other two orders passed it, and a rerun of the default order passed everything.
  - **Calls made:** the private `ClearAbilityCache`, which nothing calls, walked the set the same way, and now goes
    through the same `Inject`. A single ability's `Inject` already walked a copy of its activations.

- 2026-11-04, **GAS-52: a runtime entity's abilities end when it does.** Measuring Phase 3's exit gate turned this
  up. A Mono process ends the processes it handles as it ends, and a runtime process didn't, so an ability running on
  a runtime entity ran on, however the entity was ended, for as long as its stages lasted. An entity of either kind
  also took activations once it had ended, and a destroyed Mono entity threw on one. A runtime process now ends what
  it handles, as a Mono process does, and an ending entity's ability system shuts first. Its test,
  `GAS_52_Ending_an_entity_ends_its_abilities`, on both hubs, ended queued, at once and without cascading, in
  targeting, a stage and a critical stage, was seen failing first: the runtime entity's ability never ended, and the
  Mono entity threw `MissingReferenceException` when asked for an activation after it ended.
  - **Measured:** a 10-minute delay on a runtime entity was still running 5 frames after the entity ended, whichever
    way; now it ends with the entity.
  - **Tests:** PlayMode 927: 920 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    40 of 40. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Calls made:** the fix is in the runtime process base, so any runtime process that handles others now ends them
    as it ends, as a Mono process does, not a GAS alone. Shutting the ability system sends its abilities no interrupt,
    unlike turning `Enabled` off: their processes end with the entity's.

- 2026-11-04, **Phase 3's exit gate, measured.** In frames where nothing starts or ends, over 300 frames on both
  hubs, these allocate nothing: 20 class or 20 Mono processes stepping in all three phases, one entity carrying 10
  durational buffs and 3 effects that tick every frame, 50 entities each with one, and an ability waiting in a task
  that yields each frame. Two allocate. An ability waiting in `DelayTask` or `ChannelAbilityTask` takes 24 bytes a
  frame: the dictionary that finds a task's run by its token boxes the token on every lookup under Unity's Mono. An
  entity whose frame summary has a listener takes 504 bytes a frame, or 824 with an effect ticking: each snapshot
  copies ten lists.
  - **Per event, for scale:** an instant ability's activation about 7 KB (6.8 KB on the Mono hub), an instant
    effect's application 2.7 KB, an instant sequence's run 4 KB, and a repeating sequence about 1.3 KB per repeat.
  - **Not met yet:** the two above, and the gate's "enforced in CI". The repo has no CI, and the allocation tests live
    in Proving Grounds, which has no remote. The measuring also turned up GAS-52.

- 2026-11-03, **TS-8: activation stays synchronous, and a loop of activations goes round once a frame.**
  `TryActivateAbility` runs an ability until one of its tasks waits: an instant one to its end, a slower one to its
  first wait, and targeting before the stages. That stays, as Unreal's does. An ability asked for again from inside
  its own activation nested in the call, so a loop of instant activations nested until the stack overflowed; from an
  end callback, TS-Z made this so, as the end had come 2 frames later. Such a request now starts next frame. Its test,
  `TS_8_An_ability_asked_again_inside_its_own_activation_starts_next_frame`, from its end, its claim release and
  through another ability, on both hubs, was seen failing first. Its controls,
  `An_ability_runs_inside_the_call_that_activates_it` and
  `An_ability_asked_for_outside_its_own_activation_starts_at_once`, passed before and after.
  - **Measured:** an instant ability that asked for itself again from its end or its claim release ran 40
    activations, nested 40 deep, in one frame, where a diagnostic capped it; uncapped, it recursed without end. Each
    round now starts the next frame.
  - **Tests:** PlayMode 909: 902 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    40 of 40. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Calls made:** Decision 25. `TryActivateAbility` returns true for a request it puts off, which is tried in full
    next frame while the ability's slot still holds it. Every Phase 3 row is now fixed, or moved as GAS-51 was. The
    phase's exit gate, zero steady-state allocation enforced in CI, isn't checked yet, and the repo has no CI.

- 2026-11-03, **TS-Z: sequences and abilities end in the frame their last task does.** A `Task(Action)` yielded a
  frame before its action, a run waited a frame at its end for maintained stages it didn't have, and waiting on a
  process polled its relay once a frame. Now the action runs at once, as `Do`'s does; a run waits only for
  maintained stages it has; and `ProcessRelay.WhenTerminated` completes as the process ends, after it's unregistered,
  so the ability proxy and `RunSequenceTask` wait on that instead of polling. Its tests,
  `TS_Z_A_task_made_from_an_action_runs_it_in_the_frame_its_stage_starts`,
  `TS_Z_An_instant_sequence_ends_in_the_frame_it_starts` and `TS_Z_An_instant_ability_ends_in_the_frame_it_starts`
  (both hubs, flat and nested), were seen failing first. Their control, `An_instant_sequence_repeats_once_a_frame`,
  passed before and after.
  - **Measured:** four `Task(Action)` stages ran their actions in frames 1 to 4 after their sequence started; now all
    four run in the frame it starts. An instant ability ended 2 frames after it started, and 4 with a sequence nested
    by `RunSequenceTask`; now both end in the frame they start. An instant sequence was still running when
    `ProcessControl.Register` returned; now it has ended.
  - **Tests:** PlayMode 895: 888 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    40 of 40. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Calls made:** Decision 24. An instant sequence can end inside `ProcessControl.Register`, and an instant ability
    inside `TryActivateAbility`, so their end callbacks can fire before those calls return. A repeating sequence that
    takes no frame still takes one per repeat. TS-8, how far `TryActivate` runs before it returns, is still the
    user's call; with this, it runs an instant ability to its end.

- 2026-11-03, **PC-24: the pooled instantiator stub is gone.** `PooledMonoProcessInstantiator` gave back no process
  and kept a process's object when it ended, so the GameObject leaked. Nothing used it, and it's removed rather than
  built into a pool. Its test, `PC_24_The_pooled_instantiator_stub_is_gone`, an EditMode check that the type no
  longer exists, was seen failing first.
  - **Tests:** PlayMode 889: 882 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    40 of 40. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Calls made:** Decision 23. Code that named the stub no longer compiles; a pool extends
    `AbstractMonoProcessInstantiator`.

- 2026-11-03, **TS-10: a stage that waits for all its tasks makes no second token source.** A stage under WhenAll
  or RepeatUntilSkipped hands its policy the stage's own source; WhenAny, WhenN and a policy the runtime doesn't
  know still get one of their own to cancel. A three-stage ability makes about 10 token sources, not 14. Its test,
  `TS_10_A_stage_that_waits_for_all_its_tasks_makes_no_second_token_source`, was seen failing first.
  - **Measured,** in bytes a frame for one stage of one instant task: 1,912 under WhenAll and 1,912 under a policy
    the runtime doesn't know before; 1,464 and 1,912 now, 448 less a stage.
  - **Tests:** PlayMode 889: 882 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    39 of 39. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Left as it is:** one source each for an activation phase, a run of a process, a sequence run and a stage. The
    handle's two are public fields, and the others are what interrupts, pauses, skips and timers cancel.

- 2026-11-03, **TS-5: a repeating sequence keeps its path the same.** Each part a run or a stage adds to its
  packet's path comes off as it ends, each repeat starts where its iteration began, and a failed run keeps the path
  to where it failed for its log. The parts are formatted once, and the LINQ left on an ability's activation is
  loops. PF-007 had already taken out the LINQ and the joins the review cited. Its test,
  `TS_5_A_repeating_sequence_keeps_its_path_the_same`, was seen failing first; its control, that a failed run logs
  the path where it failed, passed before and after.
  - **Measured:** the twentieth run's path was 272 characters to the first's 25; now both are 25.
  - **Tests:** PlayMode 888: 881 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    39 of 39. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Changed on purpose:** the path is where a packet is now. A run or a stage that has ended is off it.

- 2026-11-03, **GAS-P7: a tag lookup costs the same however long its name.** Each tag has an ID, given at run time
  as it's made or as an asset holding it loads, and never serialized; equality and hashing compare IDs. Its test,
  for a tag made in code and one loaded with an asset, was seen failing first; its control, that a tag equals another
  of its name, passed before and after. The loaded case shows Unity calls `OnAfterDeserialize` on a struct.
  - **Measured,** in ms per 10,000 lookups, a 3-character name against a 2,050-character one: made in code 0.21 and
    7.1 before, 0.27 and 0.28 now; loaded with an asset 0.26 and 58 before, 0.28 and 0.27 now.
  - **Tests:** PlayMode 886: 879 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    39 of 39. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Calls made:** Decision 22. `Tag.GetHashCode` is no longer the name's hash and can differ between runs.

- 2026-11-03, **GAS-P9: rescales, lifesteal and the action queue allocate nothing.** The carriers for the changes
  the system makes on someone's behalf are kept per source and attribute, in a table keyed weakly by source. The
  system's `ModifyAttributeAction`s go back to it once the queue has run them. The queue walks its priority buckets
  by index, and `EnqueueRange` reads a list or an array by index. Its four tests were seen failing first; the
  queue's ordering has a control test, which passed before and after.
  - **Measured,** in bytes: a frame in which an attribute rescales with its Base 500 → 0, one in which lifesteal
    heals 164 → 0 inline and 532 → 0 deferred, and an action queued and run 104 → 0.
  - **Tests:** PlayMode 883: 876 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    39 of 39. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Calls made:** Decision 21. A listener of `OnActionQueued` or `OnActionExecuted` reads an action during the
    call, since one the system made is used again. The lifesteal worker's `CreateDeferredActions` returns a list it
    reuses.

- 2026-11-03, **GAS-P8: a raw delta allocates nothing on its way through.** A carrier with no impacters has its
  delta's Adds frozen in the commit's own impacters, not wrapped in a new carrier, and a `SourceAttributeImpact` keeps
  the origin it makes. `GenerateSimple` still makes a carrier on every call; its summary says to keep one for a raw
  delta made every frame. Its test, `GAS_P8_A_frame_in_which_a_raw_delta_lands_allocates_nothing`, was seen failing
  first on both hubs.
  - **Measured,** in bytes a frame, for a raw delta with its carrier kept: 356 → 0.
  - **Tests:** PlayMode 875: 868 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    39 of 39. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Changed on purpose:** a `SourceAttributeImpact` with no root carrier gives the same origin each time it's asked
    for one.
  - **Split out:** GAS-P9, what the changes the system makes on someone's behalf allocate: `EnforceScaling`'s rescale
    and the lifesteal worker's. Making them free needs a call.

- 2026-11-03, **GAS-P6: a commit allocates nothing.** A commit rents its change and its frozen impacters and gives
  them back, one set for each commit in progress, and an adjusted delta's Adds are frozen in the same impacters, not
  wrapped in a new carrier. `TrackedImpact` keeps a total, the last impact and a count. `ForgeHelper.SignPolicy` and
  `SignPolicyExtended` take one or two values without a params array or a LINQ sum. Its test for a frame in which an
  effect commits was seen failing first, at 328 bytes a frame. Its test with armor adjusting the commit came part way
  through, after a throwaway probe of the same scenario read 140.
  - **Measured,** in bytes a frame: an effect's commit 328 → 0. Reusing the change and the impacters and keeping
    totals brought it to 72, and the sign check took the rest. Armor's own sign check was 68 of the armored 140.
  - **Tests:** PlayMode 873: 866 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    39 of 39. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Changed on purpose:** a `ChangeValue` is reused once its commit is done. A callback or an inline worker reads
    it during the call, or copies its `Value`. A deferred worker's actions may keep it, and its commit then takes a
    new one.
  - **Split out:** GAS-P8, what a raw delta and `EnforceScaling` allocate.

- 2026-11-03, **GAS-P4: a tick, a recompute and a tag query allocate nothing.** A spec builds its impacted attributes
  and their impacters once per delivery, contained effects are walked in place, a recompute keeps its list, modifier
  entries are reused, and a same-root query compares roots in place. The 57 built-in `Tags` are made once. Its seven
  test cases were seen failing first.
  - **Measured,** in bytes: a tick up to its commit 528 → 0, a recompute 136 → 0, a modifier refresh 288 → 0, a
    same-root query 60 → 0. A tick that commits fell from about 2.0 KB to 1.4 KB; the rest is the commit, filed as
    GAS-P6.
  - **Tests:** PlayMode 869: 862 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    39 of 39. The 25 golden scenarios pass inside PlayMode; the custom runner can't run while the user's editor holds
    PlayForge's lock.
  - **Split out:** GAS-P7, tags hashed and compared as strings with no interned ID, which needs a design call first.

- 2026-11-03, **GAS-P5: ending an effect costs the same however many end with it.** A set mirrors the finished list,
  so marking a container and skipping a finished one no longer walk it, and a cleaning pass takes its containers off
  the shelf and out of the index together, in one walk of each, before any `OnRemove`. Its test,
  `GAS_P5_Ending_an_effect_costs_the_same_however_many_end_with_it`, was seen failing first on both hubs.
  - **Measured,** per ended effect, ending 50 at once and 3,000 at once: 0.0006 or 0.0012 ms and 0.0057 ms before,
    and 0.0005 ms for either now. GAS-P3's application cost still holds at about 0.001 ms.
  - **Tests:** PlayMode 862: 855 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    39 of 39. The 25 golden scenarios pass inside PlayMode; the custom runner still can't run while the user's editor
    holds PlayForge's lock.
  - **Changed on purpose:** effects ending in the same update leave the shelf together, so one's removal workers no
    longer find another that is ending with it.

- 2026-11-03, **GAS-P3: an application costs the same however much the entity carries.** Applying no longer walks the
  shelf. The shelf's requirements wait for one pass at the next tick, a shelved container's tags are counted as it
  lands, re-application and removal find containers in an index by asset tag, and `HandleGameplayEffects` compiles
  the tags once, not twice. Its test, `GAS_P3_An_application_costs_the_same_however_much_the_entity_carries`, was
  seen failing first on both hubs.
  - **Measured,** per application, on an entity carrying 2 effects and one carrying 1,000: an instant effect took
    0.0036 ms and 0.49 ms before, 135 times as much, and about 0.001 ms on either now. A re-application takes about
    0.001 ms on either.
  - **Tests:** PlayMode 860: 853 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    39 of 39. The 25 golden scenarios pass inside PlayMode. The custom runner didn't run, because the user's editor
    holds PlayForge's project lock.
  - **Next:** GAS-P5, which this fix found: ending effects is still quadratic.

- 2026-11-03, **GAS-S1: one core for both hubs.** `GameplayAbilitySystem` and `RuntimeGameplayAbilitySystem` forward
  to one internal `GASCore`, which builds the entity, compiles its tags and runs its end of frame and death. Its three
  tests were seen failing first: a runtime GAS applied no affiliation tags and lost its Entity asset's Local Data,
  and the GAS Debugger named a runtime entity "unknown".
  - **Tests:** PlayMode 858: 851 pass, 0 fail and 7 planted are skipped, in three orders. Planted 7 of 7; EditMode
    39 of 39. Clean import, plain and with samples, is 0 on every count.
  - **Each hub keeps on purpose:** when it builds (the Mono hub at registration, a runtime GAS in its constructor)
    and how it ends (the Mono hub at once, a runtime GAS by a queued Terminate).
  - **Next:** GAS-P3 and GAS-P4, the performance rows that touched both hubs, now change one place.

- 2026-11-03, **GAS-51 moved to Phase 5.** The user chose to keep item stacking's two fields and build stacking in
  Phase 5, as FEAT-14. Comments at `ItemDefinition.MaxStackSize` and `ItemSpecContainer.ItemStacks` say they do
  nothing yet. The code change is those two comments, and it compiles.

- 2026-11-03, **GAS-45 removed.** The user chose to remove both settings nothing read: an entity's `CapAtMaxLevel` and
  an Item's `AllowDuplicates`, with their inspector toggles and the 11 values the package's entity assets stored. Its
  test, `GAS_45_Settings_nothing_reads_are_gone` in EditMode, failed on all five parts first.
  - **Tests:** EditMode 38 of 38; PlayMode 847 of 854 with the 7 planted skipped.
  - **Found:** GAS-51, item stacking that is declared but not built. It waits on the same kind of call: remove it, or
    build it in Phase 5.

- 2026-11-03, **Phase 3, the first five rows.** On `main`, GAS-47 (`fdc1886`), GAS-48 (`27c2325`), GAS-49
  (`999e779`), GAS-50 (`e40b3b6`) and GAS-44 (`57ea4ff`) are `fixed`, one commit each, each pushed once its checks
  passed. They are the runtime bugs the docs survey found.
  - **Tests:** Proving Grounds' PlayMode suite runs 854 tests: 847 pass, 0 fail and 7 planted are skipped, in three
    orders. Planted 7 of 7; EditMode 37 of 37. Each new test was seen failing first; GAS-44's loop case crashed the
    run with a stack overflow, so the crash was its red.
  - **Golden:** the 25 ported scenarios pass inside the PlayMode suite. The custom runner did not run, because the
    user's editor holds PlayForge's project lock.
  - **GAS-P2 held:** GAS-44's first fix allocated on every tag compile, and S18 caught it. The compiler now walks the
    sets into reused buffers.
  - **Waiting on the user:** GAS-45, implement or remove the two settings nothing reads.
  - **Next:** GAS-S1, one GAS core for both hubs, so the performance rows that touch both (GAS-P3, GAS-P4) change one
    place; then TS-5, TS-10, TS-8, TS-Z and PC-24.

- 2026-11-03, **Phase 2 closed.** Its exit gate, a clean import into a fresh Unity 6 project with zero warnings and
  zero missing types, is met on `main` at `3cff9d4`, plain and with `--samples`. So all 39 Phase 2 rows move from
  `fixed` to `verified`.
  - **The last full runs:** PlayMode 842 tests, 835 pass and 7 planted are skipped, in three orders; planted 7 of
    7; EditMode 37 of 37; golden 25/25 in the custom runner.
  - **Next:** Phase 3 on `main`, starting with the runtime bugs the docs survey found, GAS-47 to GAS-50.

- 2026-11-03, **Phase 2, the last eight rows.** On `refactor/phase-2-release`, ED-6, ED-8, ED-10, ED-16, ED-19, ED-25,
  SP-22 and SP-23 are `fixed`, one commit each, and ED-12's Phase 2 part is done; the row stays open for Phase 4.
  Every Phase 2 row is now `fixed`.
  - **Calls made:** the UI interview's answers (Decision 18) and Decision 17, the submission Editor.
  - **Tests:** Proving Grounds' EditMode suite passes 37 of 37. Its 29 new tests were each seen failing first. The
    PlayMode suite runs 842 tests, GAS-46's 4 among them: 835 pass, 0 fail and 7 planted are skipped, in three
    orders. Planted 7 of 7, and the golden scenarios pass 25/25 in the custom runner.
  - **The gate:** a clean import into a fresh Unity 6 project shows 0 compile errors or warnings, 0 uncompiled
    scripts, 0 missing types, 0 exceptions and 0 asset warnings, plain and with `--samples`, which also finds 0
    missing scripts or references and no shader message.
  - **How the editor tests reach the UI:** a field sends its events only through a panel, and batch mode under
    `-nographics` has no graphics device to give an editor window one. So the tests that need events (ED-6's delay
    field, ED-12's rename and Update) add the element to a UIDocument's runtime panel. ED-19's title test opens each
    window through its menu item and ignores the error batch mode logs about drawing it. ED-8's test answers the
    delete question through `ForgeAssetDeletion.Confirm`, since a dialog would block the run.
  - **SP-22:** the test plays two sessions in one editor with domain reloading off. `TagRegistry` is kept on purpose,
    since static tag fields register as their types initialise, once per domain.
  - **SP-23:** a render check draws each Demo material on a sphere, under URP and then the Built-in pipeline. It runs
    in a throwaway project made from Unity 6's Universal 3D template (`AppData\Local\Temp\claude\urp`, in neither
    repo). Before the fix, every Standard material drew magenta under URP. After it, each draws its own color under
    both pipelines, and Built-in draws exactly what Standard drew. A first try drew black: Standard's borrowed passes
    blend by `[_SrcBlend] [_DstBlend]`, which an undeclared property reads as 0. So the shader declares all of
    Standard's properties, and the check fails a sphere left background black as well as a magenta one.
  - **ED-16:** a subagent wrote the six reference pages from the inspector and runtime code, and they were checked.
  - **The compile check** now builds `FarEmerald.PlayForge.Editor.csproj`. `Assembly-CSharp.csproj` has had no
    sources since SP-20.
  - **New rows:** writing ED-16's pages turned up GAS-44 to GAS-50 and ED-29 to ED-31. GAS-46, critical, is
    fixed, and so are ED-29 and ED-30. The rest are filed under Phase 3, and ED-31 under Phase 4.
  - **Not merged:** nothing is merged into `main` or pushed. Closing Phase 2 is the user's call.

- 2026-11-03, **Phase 2, the first 28 rows.** On `refactor/phase-2-release`, SP-1 to SP-15, SP-17 to SP-21, PC-32,
  ED-7, ED-17, ED-22 to ED-24, ED-26 and ED-28 are `fixed`, one commit each. Eight Phase 2 rows are open.
  - **The gate:** a clean import into a fresh Unity 6 project shows 0 compile errors or warnings, 0 uncompiled scripts,
    0 missing types, 0 exceptions and 0 asset warnings. With `--samples`, it also finds 0 missing scripts, references
    or types (Proving Grounds' `Tools/clean-import.py`).
  - **Tests:** the PlayMode suite runs 838 tests, 831 pass, 0 fail and 7 planted are skipped, in three orders.
    Planted 7 of 7. Proving Grounds' new EditMode tests in `Tests/Editor` pass 8 of 8, each red first. The golden
    scenarios pass 25/25 in the Test Runner (SP-18) and in the custom runner.
  - **Calls made:** Decisions 6, 7, 15 and 16. UniTask is pinned to 2.5.11; embedding it waits for the packaging branch
    (`PUBLISHING.md` section 2). SP-7 is content only, so it has no test. ED-28 leaves the editor's UXML and USS that
    still resolve to ED-20.
  - **A slip:** `git add` on the removed QuickOutline folder failed, so SP-6's commit `592a978` also took QuickOutline's
    removal and SP-5's status. SP-5's own commit `ed32de9` holds the rest. Neither was amended.
  - **New rows:** the editor survey behind the UI interview doc turned up ED-22 to ED-28, plus two more timers for
    ED-11. The Asset Store's submission guidelines (updated 2026-10-23) add SP-22 and SP-23, and open decision 17.
    Running the golden scenarios in the Test Runner turned up PC-32.
  - **Open:** ED-16 and ED-19 wait on the UI interview's questions 31 to 33, and SP-22 and SP-23 on decision 17. ED-8,
    the hard delete, ties into question 16, so it waits too. ED-6, ED-10 and ED-25 are next. ED-12's Phase 2 part
    waits on a call: `UpdateAssetTagDisplay` rewrites an asset's tag on every refresh, without Undo or a dirty mark,
    and the fix depends on whether viewing an asset should correct a stale tag (recorded, undoable) or never write.
  - **Not merged:** nothing is merged into `main` or pushed.

- 2026-11-03, **Phase 1 closed.** Its exit gate, the fault-injection and framework suites passing, is met, so all 96
  Phase 1 rows move from `fixed` to `verified`.
  - **The gate's run:** 838 tests with no category filter: 831 pass, 0 fail, 7 planted skipped, in three orders.
    Planted 7 of 7, so the harness still catches what it should. The custom golden runner in PlayForge passes 25/25.
    The run was on the code as committed at `51a630f`.
  - **Decisions:** 12 of the 14 are made. Decisions 6 and 7, the licence (SP-13) and the material packs (SP-17), stay
    open for Phase 2.
  - **Not merged:** `refactor/phase-1-correctness` holds all of Phase 1. It isn't merged into `main` or pushed;
    that's the user's call.
  - **Next:** Phase 2, release readiness, on a branch of its own: 26 open rows, ED and SP. Its gate is a clean import
    into a fresh Unity 6 project, with zero warnings and zero missing types.

- 2026-11-03, **PC-31 and GAS-43.** Both are `fixed`, so no Phase 1 row is open: all 96 are `fixed`.
  - **Result:** 838 tests with no category filter: 831 pass, 0 fail, 7 planted skipped, in three orders. Planted
    7 of 7. The custom golden runner in PlayForge passes 25/25.
  - **The fixes,** one commit each:
    - PC-31: ProcessControl looks each process class up once. One that declares its own `OnDestroy` hides
      `AbstractMonoProcess`'s, so it's warned of, and its processes are watched as dormant ones are (PC-30), each
      ended when its object goes. No class in PlayForge or Arcade Fighter does this today.
    - GAS-43: `AutoTargetTask` takes every collider in range, as the affinity task does.
  - **Proof:** 4 red tests failed on the old code first. A control holds that a class that leaves `OnDestroy` alone
    isn't warned about. 4 mutants, each undoing one branch of a fix, are all caught.
  - **Next:** Phase 1's gate. Its exit criterion, the fault-injection and framework suites passing, is met by the
    result above, so the 96 rows can move to `verified`.

- 2026-11-03, **Phase 1, the rows the medium ones turned up.** PC-29, PC-30 and GAS-37 to GAS-42 are `fixed`. Two
  more turned up on the way, both open: PC-31 and GAS-43.
  - **Result:** 833 tests with no category filter: 826 pass, 0 fail, 7 planted skipped, in three orders. Planted
    7 of 7. The custom golden runner in PlayForge passes 25/25.
  - **The fixes,** one commit each:
    - PC-29: the Bootstrapper bootstraps the ProcessControl it makes Ready, registers GameRoot, then enters
      StartState. `Bootstrap()` called any other way enters StartState at once, as before.
    - PC-30: ProcessControl watches processes registered on an object that isn't active, and ends the block of one
      whose object is destroyed before it wakes.
    - GAS-37: a rescale is a share of Current before the bounds, where it lands, so the bounds clamp once.
    - GAS-38: the four built-in targeting tasks measure from the caster's targeting packet.
    - GAS-39: the frame snapshot carries the applied and removed effects, the deaths and the ability injections.
    - GAS-40: `UseAverage` averages colliding elements, and `Ignore` keeps the first.
    - GAS-41: on the relative-operation worker, `InlineAndDeferred` runs deferred.
    - GAS-42: a runtime GAS stepped by hand can die. The Mono hub has the same guard, though nothing reaches it.
  - **Calls made here, with no decision:**
    - PC-29: StartState comes in after GameRoot registers and before `BootstrapOverrides` and the scene's processes,
      so those still meet it, as before.
    - GAS-37 leaves a change to Current and Base together as it was: it adds, then scales. A test holds it.
    - GAS-38: a runtime GAS still can't be found by a physics query, so these tasks find Mono targets only. A
      runtime GAS's position is its targeting packet's, which whoever drives it keeps current, as its summary now
      says.
    - GAS-40: `Ignore` passes the overriding values by, as a scaler group does, so the first element stands, as with
      `UseFirst`. An average is of resolved values, with bounds and rounding from the first.
    - GAS-41 maps `InlineAndDeferred` to Deferred, the safe default, rather than hiding the option.
  - **Corrections:**
    - GAS-37's diagnosis read 100/100 going to 25/50. The clamp puts that case right; 80/100 is the one that went
      wrong, ending at 50/50 rather than 40/50.
    - The red tests' commit says fifteen failed alongside nine controls. It was fourteen and eight.
  - **Proof:** 14 red tests failed on the old code first, alongside 8 controls. GAS-39's test came with its fix, as
    its properties are new. 12 mutants, each undoing one branch of a fix, are all caught. The first run let one
    through: PC-30's watch had kept PC-26's test from reaching the guard it tests, so that test now registers in the
    frame its root goes.
  - **Found on the way:** PC-31, a class that hides `OnDestroy`, which PC-30's watch doesn't cover. GAS-43,
    `AutoTargetTask`'s collider buffer.
  - **Next:** PC-31 and GAS-43, then Phase 1's gate.

- 2026-11-03, **Phase 1, the medium rows.** All 15 are `fixed`, so every Phase 1 row from the review is. The
  diagnoses turned up 8 more, all open: PC-29, PC-30 and GAS-37 to GAS-42.
  - **Result:** 809 tests with no category filter: 802 pass, 0 fail, 7 planted skipped, in three orders. Planted
    7 of 7. The custom golden runner in PlayForge passes 25/25.
  - **The fixes,** one commit each:
    - PC-25, TS-W, TS-X, GAS-30 and GAS-31 were plain.
    - PC-23: bootstrapping survives a duplicate GameRoot, a refused one and a stray ProcessControl.
    - PC-26: singletons, roots and timings at their edges.
    - PC-28: the hierarchy follows transforms.
    - GAS-27: scalers, worker modes, Combine and derived refreshes.
    - GAS-28: effects guard, record and report.
    - GAS-29: targeting and range checks work for a runtime GAS.
    - GAS-34: a lost stack refreshes.
    - GAS-35: a rescale is typed as the system's.
    - TS-R and TS-S didn't reproduce, and a test now holds each where it is.
  - **Decisions 11 to 14** are the user's: the hierarchy follows transforms, GameRoot included; a random scaler
    rolls once per spec, with a switch back; Combine adds up; a derived refresh is the system's own change.
  - **Calls made here, with no decision:**
    - GAS-28: an effect asked for again on an entity before its application there is over is refused, with one
      warning per entity and effect. That rules out an effect that applies itself until a requirement stops it.
      Nothing in PlayForge or Arcade Fighter uses contained effects.
    - GAS-28: with several impacts, `ImpactData` is the last and the new `Impacts` holds them all.
    - GAS-28: applications and removals are recorded where their callbacks fire. An instant effect counts as
      applied but never removed, and an absorbed re-application counts as applied.
    - GAS-34 takes in its sibling: the stack `Extend` adds to independent durations refreshes too.
    - GAS-35 types the rescale `DisallowImpact` and fixes the `AcceptUnworkableImpact` tooltip. The cache doesn't
      refuse `DisallowImpact` for every worker, which would also hide carriers that have no type list.
    - PC-23: a refused GameRoot is reported, with no fallback to a bare clone, which would never have initialized
      or stepped. Why a closed StartState refuses it is PC-29.
    - PC-23: `[SerializeReference]` came off all 11 fields of Unity objects, not only the Bootstrapper's. Unity
      stores those as plain references either way, so no data moves.
    - PC-26's watcher part is harmless: the reset only runs on a fresh instance.
  - **Proof:**
    - 38 red tests failed on the old code first, each for its row's reason, alongside 8 controls.
    - 13 mutants, each undoing one branch of a fix, are all caught. The first run let one through: the filing
      after initialization (PC-28), which `OnTransformParentChanged` already covers. A test with a class that hides
      that message now needs it.
    - Coverage tests were added for a hit on an attribute the entity lacks and for a combined part's scaler.
  - **Docs:** `EFFECT_PIPELINE.md` §4 to §6 describe the cycle guard, the records, the stack refresh, the derived
    refresh and the rescale's type.
  - **Behaviour to expect,** for Arcade Fighter's switch (Decision 3):
    - Pausing GameRoot pauses every process spawned at SceneRoot.
    - A random buff keeps its roll, and every tick of one periodic application deals the same. Turn
      `RollOncePerSpec` off for a roll per tick.
    - Combined elements add up.
    - A derived refresh reaches post-change workers, `OnImpactReceived` and the frame summary.
    - An effect can't apply itself inside its own application.
  - **Next:** the 8 new rows, then Phase 1's gate.

- 2026-11-03, **GAS-15, after Decision 4.** GAS-15 is `fixed`, so all 40 high rows are. Phase 1 has 15 rows still
  open, all medium.
  - **Result:** 745 tests with no category filter: 738 pass, 0 fail, 7 planted skipped, in three orders. Planted
    7 of 7. The custom golden runner in PlayForge passes 25/25.
  - **The fix:** committed history moved from the baseline to after FlatBonus, before Override. A commit's delta was
    always measured against the live value, after the modifiers, and now it lands where it was measured. Override
    still comes last, so the case the impact model fixed, an override leaking committed history, stays fixed, and a
    test holds it there.
  - **Docs:** `EFFECT_PIPELINE.md` §2.1 has the new order. `IMPACT_MODEL_REFACTOR.md`, the design that chose the
    baseline, has a dated amendment, and its decision 7.1 is struck through.
  - **Golden S6** is renamed `S6_CommittedHistoryLandsAfterModifiers` and expects 85, not 90, in both runners. No other
    test assumed the old order.
  - **Proof:** the new test failed on the old code first. Two mutants, putting committed history back in the baseline
    or past the override, are both caught.
  - **Behaviour to expect:** a % modifier no longer scales permanent history. Under a ×1.5 aura a 30 hit takes 30, not
    45, and a +10 tome is worth 10, not 15. Once the modifier is gone, the value is the same either way. For Arcade
    Fighter's switch (Decision 3): a hit landing under a multiplier on the attribute it hits takes what it says.
  - **Next:** the 15 medium rows, then Phase 1's gate.

- 2026-11-03, **GAS-36, after Decision 10.** A disjoint stops homing shots only, as in Dota, so GAS-36 is closed by
  saying so: `DisjointProxyTaskBehaviour`, `DisjointTarget` and `DisjointAbilityTask` now describe what a disjoint
  reaches. One of their summaries was empty and another stopped mid-sentence. 16 Phase 1 rows are still open: GAS-15
  (Decision 4) and 15 medium.
  - **Result:** 741 tests with no category filter: 734 pass, 0 fail, 7 planted skipped. The custom golden runner
    passes 25/25. The change is to comments only, so one order was run.

- 2026-11-03, **GAS-25, after Decision 9.** GAS-25 is `fixed`, so 39 of the 40 high rows are. 17 Phase 1 rows are
  still open: GAS-15 (Decision 4), GAS-36 (Decision 10) and 15 medium.
  - **Result:** 741 tests with no category filter: 734 pass, 0 fail, 7 planted skipped, in three orders. Planted
    7 of 7. The custom golden runner in PlayForge passes 25/25.
  - **The fix:** a raw delta has no impact type and is workable, so a worker that takes any impact type acts on it and
    one that wants a type doesn't. That holds for both ways a caller makes one, `GenerateSimple` and the two-argument
    `GenerateSourceDerivation`. The pulse each attribute starts with goes through a new `GenerateUnworkable`, so no
    worker sees the changes the system makes to itself.
  - **Golden S15** expects 42.5 instead of 35, in both runners: the pre-change callback halves the raw -30 to -15, and
    armor then halves that. The value was predicted from reading, and holds when run.
  - **Proof:** the new tests failed on the old code first. The control, armor that wants one impact type, passed
    before and after. 4 mutants of the new branches are all caught.
  - **For Arcade Fighter's switch (Decision 3):** once it uses the package, armor and any other worker that takes every
    impact type act on its raw deltas, its fall chip among them.

- 2026-11-03, **Phase 1, the high rows.** 38 of the 40 open high rows are `fixed`: the 12 ProcessControl rows, the
  14 TaskSequence rows and 12 GAS rows, ED-14 among them. TS-AD, found on the way, is fixed as well. Two high rows wait on you: GAS-15
  on Decision 4 and GAS-25 on the new Decision 9. GAS-26's third part, covering homing shots only, is now GAS-36, on
  the new Decision 10. 18 Phase 1 rows are still open: those 3 high and 15 medium, GAS-35 among them, found on the way.
  - **Result:** 731 tests with no category filter: 724 pass, 0 fail, 7 planted skipped, in three orders. Planted
    7 of 7. The custom golden runner in PlayForge passes 25/25.
  - **ProcessControl:** processes step in priority order (PC-22). One instance can't be registered twice at once, and an
    ended one can be again (PC-18). Action processes run their action (PC-19). A throw in `WhenInitialize` refuses the
    process (PC-15), and a packet lets go of a process that ends (PC-16). A state change reports the old state and the
    new one while the watchers are still there (PC-13), and watching works (PC-14). A group ends with its last member
    (PC-17). Scene processes stay where the scene put them (PC-20), GameRoot survives a scene change (PC-21), only a
    prefab is cloned (PC-9), and a destroyed object ends its process (PC-10).
  - **What the sequence rows turned out to be:**
    - TS-J went wider than `Clean`. Maintained stages are tracked by index, and each cleans itself as it ends. A run
      that ends early waits for them before its cleanup, which cleaned them at once, under tasks still unwinding. The
      active stages are a list now: a maintained stage ends under the stages after it, and the stack let a stage go only
      from the top, so a maintained critical stage held the critical section for good. Past the last stage, maintaining
      counted a stage that nothing would end, and the run waited on it for ever.
    - TS-N and TS-K went together. Every stage, top-level, sub-stage or chosen branch, runs through one core, so a
      sub-stage has its callbacks, timers, repeat, error handler, conditions and critical section. A stage's own timer
      or condition acts on that stage. It went to the current top-level stage, which skipped a sub-stage's parent, or
      the stage after a maintained one.
    - TS-AD is new. Stage and sequence sources were disposed and never cancelled, so their timers kept counting and
      fired into the next round of a repeating sequence.
    - TS-Q: `BuildSyncRunner` existed twice. A sync stage now runs its callbacks, WhenN, RepeatUntilSkipped and skip
      timeouts, and the sequence its OnComplete and whole-sequence critical flag. What a runner still can't do
      (sub-stages, branches, conditions, sequence timers, async-only tasks) is named in a warning as it's built.
    - TS-H: OnStep callbacks were stored by timing and never called. They fire at their timing now, and a composite
      timing moves a sync sequence on at one of its timings only.
    - TS-V went wider: closing ProcessControl doesn't end what's running, it only refuses new processes. The ability
      goes on only if its targeting task got to its end, whatever cut it short.
    - TS-T, GAS-20 and GAS-21 went in one commit, since they share `ClaimActive` and `EndActivation`. Removing an
      ability also kept whatever was queued behind it waiting.
  - **What the GAS rows turned out to be:**
    - GAS-16: both hubs asked tag queries through the tag list, which has no weights, so even an exact query counted
      each tag once.
    - GAS-17: the hierarchy helpers and `string → Tag` made deterministic tags, padded with a hash, so a `SameRoot`
      query with a root tag never found its own children.
    - GAS-23 is confirmed: a runtime identity's maximums were null and threw, and a new asset's were 0. They default to
      99, as max level does.
    - GAS-26: a disjoint re-aimed each shot at the entity it was already aimed at, and nothing took an intent back, so
      three shots and two disjoints left 12 entries on their target.
  - **Proof:** each row's tests failed on the old code first. 23 mutants of the new branches were then run
    against the suites, and all 23 are caught. Getting there took six more tests. Choosing the mutants showed
    three branches no test reached: a stop by index aimed at the current stage, a sync task that ends its own
    sequence, and a sync Any stage with no tasks. Three mutants survived at first: a repeat broken from OnRepeat that
    ran once more, a run that ended before its slow-to-stop maintained stage, and a landed shot living on after its
    run that kept its intent.
  - **Behaviour to expect:**
    - `OnAbilityEnd` fires when the activation ends. The claim's release has its own event, `OnAbilityClaimReleased`,
      and compiled abilities fire the task callbacks.
    - An ability can't activate while ProcessControl takes no new processes, and a refusal later on ends it rather than
      running it outside ProcessControl.
    - Tag queries count weights. `Tag.GetParent`, `GetRoot`, `GetAncestors`, `Child` and `string → Tag` give plain
      tags, so code that matched the implicit conversion against `GenerateAsUnique` of the same name no longer matches.
    - Effects don't grant their source's asset tag. Set workers run once, and tag workers tick once a frame.
    - Sequences step at FixedUpdate and LateUpdate, and a sequence's timers end with it.
    - For Arcade Fighter's switch (Decision 3): tag workers tick at half their old rate, set workers run half as often,
      and `OnAbilityEnd` moves from the claim's release to the activation's end.
  - **Next:** Decisions 4, 9 and 10, then the 15 medium rows.

- 2026-10-30, **Phase 1, the last critical rows.** PC-8, TS-G, TS-M, GAS-8, GAS-9 and GAS-32 are `fixed`, so Phase 1
  has no critical row open. 54 rows are still open, 40 high and 14 medium.
  - **Result:** 580 tests with no category filter: 573 pass, 0 fail, 7 planted skipped, in three orders. Planted 7 of
    7. The custom golden runner in PlayForge passes 25/25.
  - **What the rows turned out to be:**
    - TS-M is a freeze, not only a missed `Prepare`. `SyncDelay` resets in `Prepare`, so from a repeat's second
      iteration it finished at once. Nothing in the repeat loop yielded, so a stage that finishes without yielding
      repeated for ever inside one frame. It took a fuse to test: on the old code it reached 1,000 iterations in a
      frame. Each iteration now gets its own `Clean` and `Prepare`, and one that took no frame takes one. The synced
      runner cleans before it prepares again, and a repeat broken from `OnRepeat` prepares nothing more. The
      whole-sequence repeat never had this problem, because it waits a frame on its maintained stages. The sample's
      "mandatory yield" is no longer needed, and was left alone.
    - GAS-32 grew. An item with no `ActiveAbility`, which is any passive item, threw on equip with its effects already
      on. `IsActive` being false dropped every action queued by an item's effect. Tags compiled before `IsEquipped`
      cleared, so they outlived the item. Unequipping now removes only what that item applied, through a new
      `ITarget.RemoveGameplayEffect(effect, origin)`, a default method so other implementers still compile.
    - PC-8 covers a watcher's `OnUnregister` too, which kept the block the same way, and a Mono process's object now
      goes whatever `WhenTerminate` threw.
    - TS-G ends the nested sequence through its relay. An interrupt can be refused by a critical section and can't
      reach a run that hasn't started.
    - GAS-8's contained effects now go through `GetContainedCarriers`, and a test walks every field `EntityIdentity`
      and `RuntimeEntityIdentity` share, so GAS-9's kind of gap can't come back unnoticed.
  - **Proof:** each row's tests failed on the old code first. 11 mutants of the new branches were then run against the
    suites, and all 11 are caught. Two survived at first, and each showed a test was missing: a repeat broken from
    `OnRepeat`, and a passive item reporting a failed ability grant.
  - **Behaviour to expect:** tasks in a repeating stage now get `Prepare` and `Clean` on every iteration, not once. A
    task that kept state across iterations in `Prepare` now starts each one fresh. `CleanStage` cleans only what was
    prepared, so a stage that fails while preparing no longer cleans tasks it never reached.
  - **Next:** the 40 open high rows.

- 2026-10-30, **Phase 1, pause**, after Decision 5. PC-7, PC-27, PC-11, PC-12 and TS-Y are `fixed`, plus TS-AB, found
  on the way. No test is red any more, for the first time. 60 Phase 1 rows are still open (6 critical, 40 high).
  - **Result:** 542 tests with no category filter: 535 pass, 0 fail, 7 planted skipped, in three orders (default,
    seeds 1729 and 90210). Planted 7 of 7. The custom golden runner in PlayForge passes 25/25.
  - **Processes:**
    - `Wait` forces Waiting at once, so a pause no longer waits for the run. `TryHandlePause` true keeps the run, and
      false cancels it. It's virtual on Mono processes now, as it was on runtime ones.
    - ProcessControl marks what it stops (`HeldByControl`) and sends back exactly that, so the stateless guess in
      `GetDefaultStateWhenControlChanged` is gone. A process registered during a pause is marked too, unless it's
      RequiresControl.
    - A resume follows the transition table, so `Unpause` is refused while ProcessControl is paused (PC-27), and a
      RequiresControl process doesn't start under `Closed`.
    - Each run owns its token source. An old run still unwinding can't cancel the new one or end the process
      (PC-12), which restart-on-resume would otherwise hit every time.
    - `TerminateRequested` outranks a pause and survives ProcessControl's. A run that ends during a pause moves on at
      resume, and terminating a paused process cancels a run it held.
  - **PC-7's closing cells had one cause.** `GameRoot` is a Synchronous process, so closing queued it Terminated, and
    its `WhenTerminate` ended everything it handled. Under `Closed`, a Synchronous process now just keeps running.
    The table's `Closed Synchronous Running` says Terminated, but nothing triggers it, so that cell is unreachable,
    as the same cell is under `Ready`.
  - **Sequences:** `TaskSequenceRuntime.Pause`, `Resume`, `IsPaused` and `Delay` hold the run. A stage's end waits, the
    next stage waits, and timers stop. A static map from each stage's token to its run means a token is enough to
    ask, so `SequenceTaskLibrary`'s helpers hold with no change of signature, through the new `IsPaused`, `DeltaTime`
    and `Delay`. Builder delays, `DelayTask`, `ChannelAbilityTask`, `ApplyEffectsAbilityTask` and `RunProcessTask` use
    them. `SequenceDataPacket.IsPaused` serves a task's own code, and `TaskSequenceChain` holds between sequences.
  - **TS-AB,** measured by TS-Y's test: a channel yielded twice a loop, so it lasted twice its duration.
  - **Proof:** 13 mutants, each removing one new branch of the pause logic, were run against the suites, and all 13
    are caught. Two survived at first, which showed no test had something end during a pause. Those tests were added:
    a stage skipped during a pause, and a sequence skipped during a chain's pause.
  - **Limits of hold in place:**
    - A task's own code holds only if it keeps time through the library, with the token it was handed. Otherwise it
      finishes when it finishes, and its stage waits.
    - `ArcForce` can't hold, because physics moves the body.
    - `relay.Pause()` on an ability's sequence doesn't reach a nested `RunSequenceTask` sequence, which is a process
      of its own. ProcessControl's pause does. This belongs with TS-F and TS-G.
    - An ability activated under `Closed` or `ClosedWaiting` isn't held at all. That's TS-AC, now tracked.
  - **Found on the way:** a synchronous sequence terminated mid-task never cleans that task, which belongs with TS-P.
  - **Next:** the 6 open critical rows, PC-8, TS-G, TS-M, GAS-8, GAS-9 and GAS-32, then the high ones.

- 2026-10-30, **Phase 1, first batch**, on `refactor/phase-1-correctness`. Every ID with a red test that needs no
  decision is `fixed`: PC-2, PC-3, PC-5, PC-6, TS-A, TS-AA, TS-B, TS-C, TS-I, GAS-4, GAS-5, GAS-6, GAS-7 and GAS-33,
  plus PC-4, TS-D and TS-E, which share their code (below). They stay `fixed` until the phase's gate.
  - **Result:** 488 tests: 464 pass, 17 known red (PC-7 has 9 cases, PC-27 has 8, and both need Decision 5), 7 planted
    skipped. The green gate passes 464 of 464 in three orders (default, seeds 1729 and 90210). Planted 7 of 7. The
    custom golden runner in PlayForge passes 25/25, which also compiles the sources there.
  - **Added to the batch, each with its own red test:**
    - PC-4 sits on PC-2's lines. PC-2's fix sent a throwing run down the path that doubled `WhenTerminate`.
    - TS-D is the same wiring as TS-B. `ApplyUsageAtStageStart` defaults to true, so every default stage reported
      `AbilityStageEnded` as it began and never at its end.
    - TS-E became urgent with TS-AA. A throwing stage callback skips the stage's signal, and with TS-AA a cancel
      no longer escapes a stage that never signals.
  - **Choices in the fixes, where the sketch wouldn't do:**
    - PC-5: not the sketch's blanket interrupt, which cut every graceful `Terminate` short and missed cascades.
      `Terminate` and cascades go through `QueueTerminate`, which calls a new virtual `WhenTerminateQueued`. Its
      default clears `processActive`, so a run with nothing left to do ends and one with work finishes
      (`A_queued_terminate_lets_a_run_with_work_finish`). `TaskSequenceProcess` interrupts its sequence. Default
      transitions don't call it, so `Closed` still lets runs finish. Only Terminate is covered: a queued Wait on a
      Lazy process is PC-11, under Decision 5.
    - GAS-5: an amount left at 0 still means one stack, and new effects default to 1. The inspector's default is 0,
      and the containers always added one, so a literal fix would have silently stopped stacking. No asset in this
      repo or Arcade Fighter uses `StackExistingContainers` today. Configs run by `ConfigPriority`, so a bound has
      the last word on both the amount and the container.
    - GAS-7: the check is what the change leaves, not the size of the magnitude, so an effect that adds to the
      attribute isn't refused.
    - GAS-33: not the sketch's epsilon. Both parts of the fix are measured: with float timers and a tolerance, a
      five-minute effect at 144 fps still lost ticks; with double timers and no tolerance, 11 of 92 cases lost one.
      Timers are doubles behind the float API, with a tolerance of 0.1% of a period on the duration's last frame.
    - TS-I: not the sketch's `TerminateImmediate`. Each run has a runtime, a `TaskSequenceProcess` reaches only its
      own, and sequence-level calls (`Inject`, `Interrupt`, `SkipCurrentStage`, `CheckConditions`) reach every run.
      `SequenceStage.RepeatBroken`, `RepeatCount` and `ResetConditions` are gone, replaced by
      `TaskSequenceRuntime.IsRepeatBroken` and `GetRepeatCount`. Chains keep their own state: a chain is a single run.
  - **Behaviour to expect:**
    - A task that ignores its cancellation token now holds its sequence until it ends (TS-AA). Before, the sequence
      left it behind and cleaned it mid-run. `TerminateImmediate` still ends the process at once.
    - For Arcade Fighter's switch (Decision 3): `AbilityStageEnded` moves to the stage's end, and costs now block
      activation. Among this repo's and Arcade Fighter's assets, only `ABILITY_Meteor_Blast` has a cost and none has
      a removal requirement, so GAS-6 and GAS-7 change nothing else that ships.
  - **Open tracker rows found on the way:** PC-28 (a process parented through its data joins the hierarchy before
    `WhenInitialize` moves it; decide with PC-20) and GAS-34.
  - **Flake to watch:** S17 failed once in about 20 full runs, on a single 942-byte frame, with the editor open on
    another project. It passed the next 4 runs alone and every full run after. It looks like the known burst
    outlasting `MeasureFrames`'s 3 attempts. If it comes back, raise the attempts or quarantine the check.
  - **Next:** Decision 5 unblocks PC-7, PC-27, PC-11 and TS-Y. Without it, the other open Phase 1 rows go on.

- 2026-10-30, **Red tests for the top 15**, which finishes the first build step. PlayForge's source is unchanged.
  - **Coverage:** six of the top 15 were already fixed and pinned by goldens: GAS-1 (S25), PC-1 (S20), TS-4 (S19),
    TS-1 (S23), GAS-2 (S15) and GAS-3 (S24). The other twelve IDs are now `red`: PC-2, PC-3, PC-5, PC-6, TS-A, TS-B,
    TS-C, TS-I, GAS-4, GAS-5, GAS-6 and GAS-7. PC-7 and PC-27 joined them through the state oracle, whose other
    82 cells are a green test. With GAS-33 and TS-AA that's 16 IDs and 63 failing cases.
  - **Each red is proven both ways.** Every failure message was read against its tracker row. Then, against a
    throwaway copy of the package with a minimal sketch of a fix for each of the 16 bugs, all 63 went green and
    nothing else broke: 356 of 356. The sketches prove the tests; they are not the fixes. Several are too blunt to
    ship, and two (PC-7, PC-27) assume an answer to Decision 5.
  - **What that checking caught:** all six GAS-6 cases first failed on a null list in the test itself. Fixed, one
    passed: with a requirement on the target alone, the AND-ing hides the inversion. It's now a green control, and a
    new test with a requirement on both sides shows the inversion.
  - **Controls:** PC-3, GAS-5, GAS-6, GAS-7, TS-A and TS-C each have a green test beside the red one, so a fix can't
    pass by refusing everything.
  - **Keeping the bugs apart:** TS-A, TS-B and TS-C avoid ledger-tracked tasks where they'd cancel one, or TS-AA would
    keep them red after their own fixes.
  - **The allocation probe gained retries.** Two idle tests failed intermittently once the reds landed, then not in
    the next 41 clean runs. See Environment for what's known. A one-off burst now passes and a leak still fails,
    which `A_one_off_burst_doesnt_fail_a_measure` and the leak tests pin.

- 2026-10-30, **Golden scenarios ported to Proving Grounds**, the second part of the first build step. PlayForge's
  source is unchanged. Same scenarios and expected values, now stepped by a live ProcessControl.
  - **Result:** all 25 pass. That's 46 cases, because a scenario runs on both GAS hubs wherever it applies, and the
    hubs agree on every one. With the harness's own tests, a `!Red` run passes 199 of 199.
  - **They can fail.** Against pre-Phase-0 `455dd44`, with only SP-19 and S21's counter added to compile, S13 to S25
    all go red on every hub they run on, 22 cases, each with the symptom this Log recorded for it (S13 10 vs 5, S15
    70 vs 85, S16 0 vs 2). S1 to S12 stay green, as they pin behaviour Phase 0 didn't change, and so does S25 on the
    Mono hub, since GAS-1 was the runtime hub's bug. There, S21 stopped at TS-4's swallowed exception, so its own
    assertion got a separate check: breaking only proxy reuse on current code gives "expected 1, was 5" on both hubs.
  - **Cost of the old code, measured live:** S17 reads 960 bytes a frame on a runtime GAS and 1,920 on a Mono GAS.
  - **What had to change:** S20, S22 and S25 lost their reflection on `StepProcesses`, since live frames do that.
    S23 now pauses and resumes ProcessControl with a sequence mid-run, and that real path still reaches TS-1 (1.0 vs
    0.25). S19 and S21 assert through the fault injector, which found the framework reports them through `LogError`.
  - **Two new bugs, both `red`:**
    - GAS-33, found by running S4 on frame time, which the original's whole-second delta couldn't show. Across 9 fixed
      rates and 8 tick counts, 17 of 72 combinations lost a tick. With frame time jittered between 45 and 120 fps, 0 of
      200 runs did, so it bites fixed-step simulation: captured time, deterministic ticks, frame-data games.
    - TS-AA, found by the ledger invariant when S23 first left a sequence running at teardown.
  - **The custom runner stays** in the package until SP-18 (Phase 2), so `PlayForgeGoldenTests.RunAll` still works.

- 2026-10-30, **Proving Grounds harness built**, the first part of the first build step. PlayForge's source is
  unchanged. All of it runs in PlayMode under a live ProcessControl, brought up by the real Bootstrapper.
  - **Result:** 153 harness tests pass, in three runs and a run with `-randomOrderSeed`; golden tests 25/25 from
    the project. The 7 planted failures each fail for their own reason, which proves a broken invariant, a swallowed
    fault, an unexpected error log and a timed-out wait really fail a test.
  - **Measured under live stepping:** a runtime GAS and a Mono GAS together allocate exactly 0 bytes across 1,200
    idle frames (GAS-P1 holds with real frames, not just hand-stepped ones). The probe still catches an `object`
    allocated in a single Update every frame, well below the runner's 82-byte noise.
  - **The state oracle against live processes, all 96 cells:** 41 agree, 41 can't be reached, 14 disagree.
    - 6 `Closed` cells: closing ProcessControl terminates processes the table keeps going, by the default transition
      PC-7 describes.
    - 8 `Waiting` and `ClosedWaiting` cells: `Unpause` runs a process while ProcessControl is paused. New row PC-27.
    - A cell can't be reached when ProcessControl refuses the registration, a terminated block is already gone, or
      a synchronous process has no run to end.
    - The cells aren't asserted yet: that test is PC-7's, with the red tests.

- 2026-10-30, **Proving Grounds set up** on `refactor/proving-grounds` at `555b2e9` (Decision 2). The new project's
  own repo is at `2c49575`. Commands are under Environment.
  - **Red:** the first compile exited 1. Runtime, Tests and UniTask built, but the Editor assembly failed with
    CS0246 on `log4net` and `Codice` (SP-19). Both were unused usings that only compiled through
    `com.unity.collab-proxy`, which a clean consumer doesn't have.
  - **Green:** with SP-19 fixed, a clean import from an empty `Library` takes 31 s (680 imports) and exits 0, with
    0 C# errors and 0 C# warnings.
  - **Golden tests from Proving Grounds:** batch mode, run twice, exit code 0 both times. **25/25 pass, 0 fail,
    0 expected-red**, in about 6 s. That includes S17 and S18, the heap-growth checks.
  - **Canonical after SP-19:** batch mode 25/25, exit code 0. Both MSBuild rebuilds report 0 C# diagnostics.
  - **Dot-folder:** Unity ignores `Assets/PlayForge/.claude/`. No `.claude.meta` appeared, neither the compile
    response files nor the import log mention it, and the asset count (415 scripts, 265 others) matches a copy of
    the package without the folder.
  - **Clean-import findings,** none blocking. Only the first line shows on a warm `Library`; the other warnings
    appear once, on import.

    | Finding | Tracker |
    |---|---|
    | 15 sample scripts "will not be compiled" (60 log lines) | SP-20, new |
    | 32 assets log "Missing types referenced", 16 framework types under `Assembly-CSharp`; 39 sample assets hold the old assembly name | SP-1, confirmed |
    | `DemoMenu.uxml:2` references `Assets/demo_uss.uss`, outside the package | SP-4, confirmed |
    | 4 `TypeLoadException`s, 9 invalid asset paths and 1 `:last-child` warning in editor UXML and USS | SP-21, new |
    | Consumers must add UniTask by hand; PlayForge resolved 2.5.10 and Proving Grounds 2.5.11, as nothing pins it | SP-8, confirmed |

  - **Phase 2's gate is not met:** the package compiles and passes its tests from a clean project, but SP-1, SP-4,
    SP-20 and SP-21 still break "zero warnings, zero missing types".

- 2026-10-29, **Phase 0 gate passed** on `refactor/phase-0-upstream` at `6a82037`:
  - **Build:** both MSBuild commands with `-t:Rebuild` report 0 C# diagnostics (the baseline had ED-1's three).
  - **Golden tests:** batch mode, run twice, exit code 0 both times. **25/25 pass, 0 fail, 0 expected-red**; no
    scenario uses `targetSemantics`. That is the 12 originals, Arcade Fighter's 10 (S13–S22) and canonical's
    S23–S25.
  - **Status:** TS-1, TS-2, TS-3, TS-4, PC-1, GAS-1, GAS-2, GAS-3, GAS-10, GAS-11, GAS-12, GAS-13, GAS-P1, GAS-P2,
    ED-1 and SP-16 are `verified`. New rows: GAS-32 (rest of GAS-3) and SP-18 (rest of SP-16).
  - **Carried into Phase 1:** GAS-19 now affects the runtime GAS too (see GAS-1 below). Arcade Fighter's
    `GameRoot` `DontDestroyOnLoad` was not ported; it belongs with PC-21.

- 2026-10-29, **Phase 0 port**, one commit per fix. Each row gives the red result without the fix and the
  golden total with it. Arcade Fighter's scenarios keep their S-numbers and gain the tracker ID
  (`S13_GAS_10_…`); scenarios new to canonical start at S23.
  - PF-001 / TS-1: S23 red without the fix ("time scale after the resume: expected 0.25, got 1"); 13/13 with
    it. S23 clears the TimeManager's dirty flag, or the editor rewrites `ProjectSettings/TimeManager.asset`
    in the Unity 6 format on exit.
  - PF-002 / GAS-10: S13 red without the fix ("regen paused while the avoided tag is granted: expected 5,
    got 10"); 14/14 with it.
  - PF-003 / GAS-11: S14 red without the fix ("blocked while the avoided tag is on the caster: expected
    true"); 15/15 with it.
  - PF-004 / GAS-2: S15 red without the fix (five checks, e.g. "armor halves a 30 hit: expected 85, got
    70"; "pre-change callback halves a raw −30: expected 35, got 20"); 16/16 with it.
  - PF-005 / GAS-12: S16 red without the fix ("raising an empty pool by 2/2: expected 2, got 0", and two
    more); 17/17 with it.
  - PF-006 / GAS-P1: S17 red without the fix (every attempt collected, so the per-frame measure never
    settled). With only the four files Arcade Fighter lists, S17 still measured 49.2 bytes per frame; the
    rest was `TagCache.EvaluateTagWorkers` copying its active workers each frame. Arcade Fighter logged that
    under PF-007 (one commit held both), so that hunk lands here. 18/18 with it.
  - GAS-3 (ahead of PF-007): with PF-007 applied, S18 still measured 47.1 bytes per round in batch mode,
    though Arcade Fighter reported it green (in-editor runs; not re-checked there). The allocation was
    `ItemSystemComponent.GetItemContainers` returning a new list for the never-created shelf; initializing
    the shelf took S18 to green. So GAS-3 landed first: S24 red without it (`ArgumentNullException` from
    `HasItem`); 19/19 with it.
  - PF-007 / GAS-P2: S18 red without the fix (every attempt collected); 20/20 with it, on top of GAS-3.
    Adds `Tag/GrantedTagCompiler.cs` with Arcade Fighter's `.meta` GUID. Every PF-007 file matches Arcade
    Fighter at `7ba578d`.
  - PF-008 / TS-4: S19 red without the fix ("logged: 0 errors"); 21/21 with it.
  - PF-009 / ED-1: the test is the compiler. Red: CS0162, CS8509 and CS8321 in the baseline rebuild and in
    Unity's batch compiles. Green: an MSBuild rebuild and Unity's recompile of the Editor assembly report no
    C# diagnostics; 21/21.
  - PF-010 / TS-2: docs only. Arcade Fighter's fix covered the process types; the sync section still
    named `SyncSequenceBuilder`, `SyncSequenceRunner`, `ISyncSequenceTask` and `RootDefault`, so it now uses
    `TaskSequenceBuilder` … `BuildSyncRunner()`, `SyncedTaskSequence`, `ISequenceTask` and `SceneRoot()`. A
    scan of the README's identifiers against the sources finds nothing missing beyond Unity/.NET types and
    the example's own placeholders.
  - PF-011 / PC-1: S20 red without the fix, all four checks (the exception escapes, the next process
    steps 0 times, the broken one stays Running, nothing logged); 22/22 with it.
  - PF-012 / TS-3: S21 needs the fix's `CompiledProxies` counter to compile, so the red run added only
    that counter at today's compile site (not committed): "compiled 5 times". 23/23 with the fix. The
    tracker's other TS-3 cost (`IsCriticalSection` building the inner sequence 4–5 times per compile) is
    not touched by PF-012; it now runs once per proxy instead of once per activation.
  - PF-013 / GAS-13: S22 red without the fix ("the GAS has its callbacks before it's initialized"); 24/24
    with it. As Arcade Fighter noted, a runtime GAS still initializes in its constructor, so its
    `OnSystemInitialized` has fired before anyone can subscribe.
  - GAS-1: S25 steps a runtime GAS through a test ProcessControl's own `StepProcesses`, as S20 does. Red
    without the fix ("ran 0" ends of frame); 25/25 with it. Both constructors hard-coded `Update`. Side
    effect: the runtime GAS's `WhenUpdate` and its `EndOfFrame` both call `TagCache.TickTagWorkers`, so its
    tag workers now tick twice a frame, as the Mono GAS's already did. That is GAS-19 (Phase 1).
  - SP-16: masks removed from S6, S8 and S11, plus the stale "expected to fail" notes and the header's
    Assembly-CSharp claim. No red (they passed at the baseline); 25/25, 0 expected-red.

- 2026-10-29, **Phase 0 baseline** at `455dd44`, no code changed:
  - **Build:** both MSBuild commands under Environment succeed. The only C# diagnostics are the three ED-1
    warnings: CS0162 `GameplayEffectDrawer.cs:161`, CS8509 `AbilityEditor.cs:138`, CS8321
    `TheForge.ViewTab.cs:1640`.
  - **Golden tests:** batch mode, editor closed, exit code 0. **12/12 pass, 0 fail, 0 expected-red.** S6, S8
    and S11 are still marked `targetSemantics`, but they pass, so the masks currently hide nothing (SP-16).
- 2026-10-29: plan created from the review; branch `refactor/handoff`.
