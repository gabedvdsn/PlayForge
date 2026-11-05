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
  in its own local git repo, with no remote. It consumes `Assets/PlayForge` as the package
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
18. **The labs' scenes.** The review asks for each lab as a small playable scene that doubles as a sample and as store
    screenshot material. Working assumption until the user answers: a lab is test-only in Proving Grounds at first,
    its assets built in code and played by its bot; its scene, and whether it ships in the package's Samples, come
    once its bot is green.
19. **Where the Debugger's edits go (ED-40).** The boards draw the Entities view's four sections without the edits
    Q27 asked for. Working assumption until the user answers: Base and Current are set by a double-click on the
    value; a click selects a table's row, and the buttons under the table act on it (Remove, Activate, Interrupt);
    a picker under Effects applies one, a picker and a level under Abilities grant one, and a tag field with Add Tag
    and Project Tags under Tags adds a loose tag.

## Roadmap

| Phase | Scope | Exit gate |
|---|---|---|
| 0 | Port Arcade Fighter's 13 fixes and 22 golden tests; make three one-line fixes | Golden tests green, none masked |
| 1 | Runtime correctness: exception isolation, queued state, every critical and high bug | Fault-injection and framework suites pass |
| 2 | Release readiness: samples, packaging, docs, Unity Test Framework, CI | Clean import into a fresh Unity 6 project: zero warnings, zero missing types |
| 3 | Performance and structure: caching, pooling, one GAS core, a Register builder | Zero steady-state allocation, enforced in CI. Closed on the local gates instead (Decision 27) |
| 4 | Editor UX: Undo, theming, no polling, a debugger that can act | Editor smoke suite passes in both themes |
| 5 | Missing GAS features (FEAT-n) | Every test-project lab green |

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
| TS-6 | M | 5 | Global time only; `RunProcessTask` defaults to unscaled time (C02) → FEAT-7 | `RunProcessTask.cs:14` | open |
| TS-7 | M | 5 | Critical sections are static; the compiler never emits a `BranchStage` (C12) | `AbilitySequenceCompiler.cs` | open |
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
| GAS-51 | L | 3 | Item stacking is declared but not built: an Item's `MaxStackSize` is never read, and the inspector doesn't draw it; `ItemSpecContainer.ItemStacks` is never set. Remove both, or build stacking as a Phase 5 feature. Found removing GAS-45. The user's call: keep both and build stacking in Phase 5, as FEAT-14 | `ItemDefinition.cs:25`, `ItemSpecContainer.cs:25` | moved |
| GAS-52 | H | 3 | A runtime entity's abilities outlive it. A Mono process ends the processes it handles as it ends, and a runtime process didn't, so an ability running on a runtime entity ran on, however the entity ended: a 10-minute delay was still running 5 frames after. An entity of either kind also took activations once ended, and a destroyed Mono entity threw on one. Found measuring Phase 3's exit gate. A runtime process now ends what it handles, as a Mono process does, and an ending entity's ability system shuts first, refusing activation before it reads the entity | `AbstractRuntimeProcess.cs` (`WhenTerminate`), `GASCore.cs` (`WhenTerminate`), `AbilitySystemComponent.cs` (`ShutDown`, `TryActivateAbility`), both hubs' `WhenTerminate` | verified |
| GAS-53 | H | 3 | Interrupting every running ability under a policy, all at once or by turning the ability system off walked the live set of them. TS-Z made an interrupted ability end inside the interrupt, taking itself off that set, so the walk threw "Collection was modified" once the first had ended, and the rest ran on: 1 of 2 ended, on both hubs. It now walks a copy | `AbilitySystemComponent.cs` (`Inject`, `ClearAbilityCache`) | verified |
| GAS-54 | M | 4 | An attribute set element with no Attribute, as a list's "+" leaves one, throws as the entity starts: `AttributeSetMeta` files the set's elements in a dictionary by their attribute, and a null key throws, so the entity gets none of its attributes. Such an element now gives nothing, and the set's unique count leaves it out | `AttributeSet.cs:92, 369-373` | fixed |
| GAS-55 | M | 4 | Use Minimum takes the first colliding element: `ForgeUtils.IndexMin` started its search from `float.MinValue`, so no value was ever below it. It starts from `float.MaxValue` | `ForgeUtils.cs:23-38` | fixed |
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
| ED-2 | H | 4 | The effect inspector never shows delivery and still draws `ReverseImpactOnRemoval`. Now the impact says its delivery, which the duration gives it, and how often a periodic one commits, as in "7 over 6 s, level 1", and Reverse On Removal, which nothing reads, is gone from the inspector | `GameplayEffectImpactDrawer.cs` | fixed |
| ED-3 | H | 4 | There is no `AttributeImpactEntry` drawer, so multi-entry effects cannot be authored. Now an effect's Additional Impacts are authored entries, each with its own attribute, operation, magnitude and delivery, which `AttributeImpactEntryDrawer` edits and the runtime appends after the derived one; the Cost rule checks each attribute a cost spends | `GameplayEffectImpact.cs` (`RebuildEntries`), `AttributeImpactEntryDrawer.cs`, `CommonValidations.cs` | fixed |
| ED-4 | H | 4 | GAS Debugger: rebuilds every 0.25 s, `FindObjectsByType` every tick, Mono only, reflection on `TagCache`, raw cooldown magnitude, unlabelled badges. Fixed: one Debugger (Q4), built from the design canvas's boards (version 21), lists each hub ProcessControl registers, of either kind, following its join and leave callbacks (Q28); a sample changes the detail's values in place, at the rate the window menu sets; it reads no private field; an entity's header says what it is, and its attributes, effects, abilities and tags sit two by two under titled columns; an ability shows its state, as Stage 2/3, and its cooldown's time left; the Tags view shows every tag held, as a tree, and who holds the one selected; Follow Selection shows the entity selected in the scene; a status bar counts the processes and entities; it listens to the entity it shows alone; outside Play mode it offers Play Mode | `Editor/Debugger/RuntimeDebugger.cs`, `RuntimeDebugger.Entities.cs`, `RuntimeDebugger.Tags.cs` | fixed |
| ED-5 | M | 4 | ProcessControlDebugger: usage is not CPU time; the Terminated filter is dead; Pause misleads; per-row style allocations. Fixed: the Debugger's Processes view shows each process's time a step over the last second, and its longest, which ProcessControl times while the debugger reads the game (`ProcessControlBlock.MeasureSteps`); its columns sort; its filters are Running and Waiting; Pause and Resume take the processes it handles alike, and Pause is offered for any running process; its Run says whether a paused run is held (`RunIsHeld`); it's UI Toolkit, with no style set in code | `RuntimeDebugger.Processes.cs`, `ProcessControlBlock.cs` | fixed |
| ED-6 | M | 2 | Settings: an inverted range check; Reset All goes to the wrong tab and is partial; Auto Refresh is never read | `TheForge.SettingsTab.cs:233-236, 264, 346, 1770-1785` | verified |
| ED-7 | M | 2 | Numeric columns sort as strings | `TheForge.ViewTab.cs:656` | verified |
| ED-8 | H | 2 | Hard delete with no reference check | `TheForge.ViewTab.cs:2552`, `AssetBrowserWindow.cs:285` | verified |
| ED-9 | M | 4 | The Visualizer is blank for items and sets, and after a domain reload. Fixed: the Visualizer is gone; The Forge's detail pane summarizes every type, from its inspector's section summaries, and the window keeps its selection across a reload (Q6) | `TheForge.cs` (`FillSummary`) | fixed |
| ED-10 | M | 2 | Analysis exposes "Not Implemented" groups and truncates silently | `TheForge.AnalysisTab.cs:362, 716, 1461, 1979-1991` | verified |
| ED-11 | M | 4 | The sticky-header workaround and drawer timers poll forever, among them two 250 ms timers in each impact drawer. Now nothing polls: the drawers follow the properties they show, the object picker's listener runs only while a picker is open, and the inspector keeps no scroll view of its own, so nothing measures the window; the header follows the Inspector's scroll events (ED-36) | `BasePlayForgeEditor.cs` (`OnAttachedToPanel`, `ConfigureScrollView`, `OnEditorUpdate`) | fixed |
| ED-12 | H | 4 | No Undo, multi-edit or prefab overrides. The inspectors wrote 42 edits straight into their asset, and only the 5 renames recorded Undo; no inspector took a multiple selection; six drawers copy their SerializedObject for one target, and two write while drawing. Fixed: Phase 2's part (a rename stores the tag in the same undoable step, and showing never writes), the Attribute inspector (its fields bind, it takes a multiple selection, and every inspector's header follows Undo), the MonoProcess inspector's multiple selection, the Effect, Item and Attribute Set inspectors' Description and Link Mode, the Entity inspector's fields, the Ability inspector's Description, Activation Policy, Activate Immediately and Link Mode, Link, Unlink and template assignment in every inspector, the drawers that rebuilt on a one-target copy, the type drawers that wrote a default while drawing, and every asset inspector's multiple selection; the magnitude field and the scaler drawer change each selected asset's own values | `EachAsset.cs`, `MagnitudeField.cs`, `ScalerFields.cs`, `EffectEditor.cs`, `EntityIdentityEditor.cs`, `AttributeSetEditor.cs`, `BasePlayForgeEditor.cs` (`IsMultiple`) | fixed |
| ED-13 | M | 4 | Template propagation on save loads everything, opens a modal mid-save and records no Undo; the validator is empty. Fixed: templates work as prefabs do (Q23): an explicit Push to Its Children, as one Undo step, which keeps each child's overrides; an edit to a child's field records the override, which the inspector marks, and one Undo takes back both; Revert to Template. The save hook and its empty validator are gone | `ForgeTemplatePush.cs`, `BasePlayForgeEditor.cs` (`ShowOverrides`) | fixed |
| ED-14 | H | 1 | The `GameplayEffectImpact` copy constructor drops affiliation and throws on null packets | `GameplayEffectImpact.cs:155-176` | verified |
| ED-15 | M | 4 | "Validate All" is noisy and capped at 15; "Find All References" is a dialog. Fixed: the Used By sections replace Find All References, and one validator (`ForgeValidator`, with a `ForgeValidationRule` per check) feeds the Problems view and each inspector header's count, in place of Validate All and the Audit (Q8). The Forge's problem glyph and counts come with its window (ED-27) | `ForgeValidator.cs`, `ForgeRules.cs`, `ForgeProblemsWindow.cs`, `ForgeAssetHeader.cs` | fixed |
| ED-16 | M | 2 | 49 placeholder `docs.playforge.dev` links in six editors, some pointing at the wrong page, with slugs that vary (`level-source`, `levelsource`). The Asset Store strongly recommends offline docs (rule 2.3.a) | `AbilityEditor.cs:77, 116`, `AttributeSetEditor.cs:818`, `AttributeEditor.cs:183`, `EntityIdentityEditor.cs:158` | verified |
| ED-17 | M | 2 | Stray `Debug.Log` calls | `GameplayEffectImpactDrawer.cs:33`, `AssetBrowserWindow.cs:348`, `AbstractRefDrawer.cs:81`, `TagDrawer.cs:745`, `TheForge.ViewTab.cs:2009` | verified |
| ED-18 | M | 4 | Collapse state lives in static dictionaries keyed by property path, so every asset shares it and a domain reload loses it. Each drawer now keeps it per asset for the editor session, in SessionState; a [SerializeReference] field's state follows the object it holds, and deleting a stage moves the later stages' states up with them | `AbilityTaskBehaviourStageDrawer.cs:31`, `AbilityBehaviourDrawer.cs:18-20` | fixed |
| ED-19 | M | 2 | Naming drift: The Forge or PlayForge Manager, and no window's title matches its menu item. The top-level `PlayForge/` menu is not among the placements the Asset Store's rule 2.5.1.a describes: an existing menu such as `Window/`, or else `Tools/`. The docs follow whatever wins | `TheForge.cs:16-28, 88`, `GASDebugger.cs:134`, `GETTING_STARTED.md` | verified |
| ED-20 | H | 4 | No theming: inline styles, hard-coded dark backgrounds, emoji icons, dead UXML and USS. Now one style sheet, `Editor/UI/PlayForge.uss`, holds every value `Docs/EDITOR_STYLE.md` lists, in a Dark and a Light set, and a kit of controls reads it: the asset header, the section, the glyphs and the type icons. Every asset inspector's header and sections use the kit; The Forge and the GAS Debugger take the theme; no editor text shows an emoji; the dead style sheet and layouts are gone. Each surface's own inline styles go with its redesign, Phase 4's fifth step | `TheForge.cs:183`, `GASDebugger.cs:108`, `ForgeDrawerStyles.cs:111-131` | fixed |
| ED-21 | M | 4 | Settings live in EditorPrefs and are machine-wide. Fixed: the shared settings (new assets' folders and file names, each type's defaults, the Cost and Cooldown effects) live in `ProjectSettings/PlayForgeSettings.asset`, a `ScriptableSingleton` on Project Settings → PlayForge, and the view toggles on Preferences → PlayForge (Q21). A project takes its EditorPrefs values on first use; The Forge's Settings tab and `TemplateRegistry` are gone | `PlayForgeSettings.cs`, `PlayForgePreferences.cs`, `PlayForgeSettingsPages.cs` | fixed |
| ED-22 | H | 2 | A second project wipes the first project's templates: `TemplateRegistry` keeps one EditorPrefs JSON for every project on the machine, and `Save()` drops each template whose GUID doesn't resolve in the current project | `TemplateRegistry.cs:110, 164-177` | verified |
| ED-23 | M | 2 | An Item's Local Data can't be edited: Items inherit `LocalData` and the five other asset editors draw it, but `ItemEditor` builds no section for it | `ItemEditor.cs:71-80`, `BaseForgeAsset.cs:11` | verified |
| ED-24 | M | 2 | `ParticipateInSiblingCascade` can't be set: it is `[HideInInspector]` and `AbstractMonoProcessEditor` never draws it, yet ProcessControl reads it | `AbstractMonoProcess.cs:24-26`, `ProcessControl.cs:926, 986` | verified |
| ED-25 | M | 2 | The tag display settings barely work: "Display full tag path" changes nothing, since `GetDisplayText` returns before the code that reads it; "Group tags by root" shows off in Settings but defaults to on in `TagDrawer`; and the View tab reads neither | `TagDrawer.cs:178, 181-198`, `TheForge.SettingsTab.cs:201` | verified |
| ED-26 | M | 2 | The GAS Debugger never removes its three frame-summary hooks, so a window closed in play mode keeps sampling every entity, and reopening it adds another set | `GASDebugger.cs:149-152, 269-305` | verified |
| ED-27 | M | 4 | The View tab has two click models: one click selects and pings a row in the grouped list, but a typed table acts only on a double-click. Fixed: The Forge is one list and detail (Q5), where a click selects a row and shows its asset, and a double click shows it in the Project window | `TheForge.cs` | fixed |
| ED-28 | L | 2 | Dead UI: `QuickFillWindow` is never opened, `m_AbilityIcon` is never assigned, and four editor `.cs.meta` files name UXML GUIDs that exist nowhere | `ScalerDrawerWindows.cs:356-372`, `AbilityEditor.cs:19-20, 70`, `AbilityEditor.cs.meta:7` | verified |
| ED-29 | L | 2 | The Entity inspector's Allow Duplicate Equipped Items toggle opens showing Allow Duplicate Items' value | `EntityIdentityEditor.cs:927` | verified |
| ED-30 | M | 2 | Local Data's object rows edit each other's value: a ScriptableObject row binds the MonoBehaviour field and the other way round, so neither keeps what's assigned | `DataWrapperDrawer.cs:127-128` | verified |
| ED-31 | L | 4 | The four unlink dialogs count with `IsLinkedTo(this)`, the editor rather than the asset, so they always say 0. Each now counts the children its unlink would change | `AbilityEditor.cs:840`, `EffectEditor.cs:279`, `EntityIdentityEditor.cs:301`, `ItemEditor.cs:311` | fixed |
| ED-32 | H | 4 | Six import windows behave differently. Section import copies every field with no choice and Clear asks in a modal; the Effect's Duration and Impact sections couldn't import at all, since their Import button only logged a warning (they open the section window since ED-37); scaler import ignores the kinds a field allows and caches its list for 120 s. One Import window replaces them (Decision 28): choose a source, choose the fields, import as one Undo step. Fixed: `ForgeImportWindow` serves every section, a whole asset, worker groups and requirement groups, in place of five windows, and Clear asks nothing; a scaler's Import Values compares a Scaler asset's or another field's values with the field's before it copies them | `ForgeImportWindow.cs`, `ScalerWindows.cs` (`ScalerValuesWindow`) | fixed |
| ED-33 | M | 4 | Scalers can't be shared. Every scaler is copied into its field, and the Scaler Template asset's link code has no callers, so reuse means copying and then propagating. Shared Scaler assets that fields reference replace it (Decision 28). Fixed: the Scaler Template is the Scaler asset, a field of the new Shared kind reads one (an attribute set's element through the cached Shared), its link code is gone, and levels map exactly unless a scaler stretches; a magnitude field reads as the canvas's scaler board draws it, with its field menu (Save as Scaler Asset, Use a Scaler Asset, Import Values, Quick Fill, Make Constant), its per-level table and curve, and the asset's own inspector lists the fields that share it | `MagnitudeField.cs`, `ScalerFields.cs`, `ScalerWindows.cs`, `ScalerAsset.cs` | fixed |
| ED-34 | H | 4 | Opening a scaler writes to it while drawing. On an effect with no level link, Lock mode cuts its per-level table to one value; the other modes resize the table to Max Level, a Max Level of 0 or less becomes 10, and building the drawer clones a scaler that shares its reference id. Now nothing is written while drawing: a table keeps its length until the user changes it, and a shared scaler shows a warning with a button that makes a separate copy | `ScalerDrawer.cs:125-180, 285, 489-529` | fixed |
| ED-36 | M | 4 | The asset header scrolls away with the content. It stays at the top of the inspector while the content scrolls under it (Decision 29), following the Inspector's scroll view by its events, so nothing polls (ED-11) | `BasePlayForgeEditor.cs` (`CreateInspectorGUI`), `ForgeAssetHeader.cs` (`StickTo`) | fixed |
| ED-35 | M | 4 | Whole-asset import copies the template and level-link fields (`_template`, `_linkedSource`) and doesn't skip the nested `Definition.Name`. It copied each top-level field whole, so the asset tag came along in Tags too. The Import window's whole-asset import keeps the name, description, icons, asset tag, template and level link | `ForgeImportWindow.cs` (`KeepsOwn`) | fixed |
| ED-37 | M | 4 | The asset inspectors don't follow `Docs/EDITOR_STYLE.md`: their sections stray from its order, Level Source sits apart from Leveling, a collapsed section shows nothing, sentences explain the fields, the link state sits in tinted boxes with colored borders, and many sections' Import and Clear only log a warning. Each inspector is redone in Phase 4's fifth step. Fixed: the Attribute (`ed6295a`, with ED-15), Item (`cf70e8f`), Entity (`a06a4fb`), Attribute Set (`86ead05`), Ability (`031c2d4`) and Effect inspectors: the style doc's order, Leveling with the level link and its provider in Unity's object field, the asset tag under the name, a summary on each section, Import and Clear on each, Used By, and no look set in code; the Entity mockup's attribute table, in the Entity and Attribute Set inspectors; the mockups' When Busy, in the Ability and Entity inspectors; and the Effect mockup's requirements table, in the Effect and Ability inspectors | `EffectEditor.cs`, `AbilityEditor.cs`, `ForgeRequirementsTable.cs` | fixed |
| ED-38 | M | 4 | An asset reference isn't Unity's object field: `AbstractRefDrawer` draws a button that opens a dropdown list of its own, with hover colors and Clear and Open icon buttons set in C#, and setting an effect asks in a dialog whether to link it. Now every asset reference is Unity's object field with a plain button beside it: Open, which shows the asset in a window of its own, or New, which makes one where The Forge puts its type, named and tagged, and uses it (Q15). An effect field says on a line under it how the effect takes its levels, with Link; its template, a pick of unlinked effects and New Linked Effect are in its right-click menu (Q18) | `AbstractForgeObjectDrawer.cs`, `GameplayEffectDrawer.cs` | fixed |
| ED-39 | M | 4 | Opening an attribute set element can change the set: 50 ms after it draws, and on every change to the set, `AttributeSetElementDrawer` clamps a Current or Base magnitude that sits outside the element's bounds and applies it to the asset, so looking at a set marks it modified. The runtime clamps the same values as the entity starts. Now the drawer writes nothing as it draws: a help box says how the runtime clamps such a magnitude, and only a value typed into a Magnitude field is kept within the bounds, as part of the same edit. The drawer follows the style sheet | `AttributeSetElementDrawer.cs` | fixed |
| ED-40 | M | 4 | The debugger only watches: it can't change an attribute or a tag, apply an effect or grant an ability, so trying a change means writing code for it. Q27: editing attributes and tags first, then applying effects and granting abilities. Fixed: a double-click sets an attribute's base or current value, which the entity takes as it is; a tag typed or picked from the project's is granted as a loose tag (FEAT-16), which Remove takes back from its selected row; an effect picked under Effects lands from the entity itself, and Remove takes the selected one off; an ability picked under Abilities is granted at a level, and the selected one is activated, interrupted or removed. Where these go is open decision 19 | `Editor/Debugger/RuntimeDebugger.Entities.cs` | fixed |

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
| SP-24 | M | — | No CI, so Phase 3 closed on its local gates, not the CI its exit gate asked for (Decision 27). CI needs two things only the user can provide: tests it can reach, by pushing Proving Grounds to a GitHub remote or by moving the allocation tests and their harness into the package, and a Unity license stored as GitHub secrets | — | open |

### Missing features (Phase 5)

| ID | Feature | Notes |
|---|---|---|
| FEAT-1 | Gameplay cues | Hooks for effects, sounds and camera on apply, tick and remove |
| FEAT-2 | SetByCaller magnitudes | Scalers read the data packet (C01) |
| FEAT-3 | Tag-change events | Replace tag-worker polling |
| FEAT-4 | Ability triggers | Activate on a gameplay event or when a tag is added |
| FEAT-5 | Cancel and block abilities by tag | C11 |
| FEAT-6 | Input buffer | Expiry and latest-wins for queued activations (C06) |
| FEAT-7 | Time source | Per-entity time scale and deterministic fixed tick (C02) |
| FEAT-8 | Effect conveniences | Cleanse by tag query, immunity, overflow at max stacks, abilities granted by effects, chance to apply. Cleanse and immunity done (Removes Effects Tagged, Blocks Effects Tagged); overflow at max stacks, abilities granted by effects and the chance to apply are next |
| FEAT-9 | Save and load | Snapshots of attributes, effects, tags and cooldowns |
| FEAT-10 | Headless mode | Abilities run without a scene ProcessControl |
| FEAT-11 | Input binding | Targeting without legacy `Input` and `Camera.main` |
| FEAT-12 | Ability graph and timeline, effect recorder | Editor |
| FEAT-13 | Networking | Prediction and replication; not planned yet |
| FEAT-14 | Item stacking | An inventory holds an item as a count, up to the Item's `MaxStackSize`. The field and `ItemSpecContainer.ItemStacks` exist, but nothing reads or sets them (GAS-51). Decide how giving and removing count, how a stack's effects and abilities scale, and how Allow Duplicate Items meets it |
| FEAT-15 | Register builder | One builder or options struct in place of `ProcessControl`'s 28 static `Register` overloads, from the review's API changes. The roadmap gave it to Phase 3, but no row tracked it, so it moved here when Phase 3 closed. Today the argument's static type picks the overload, and a failed call returns false with a null relay. The review paired it with a `Run` on the relay, which doesn't exist yet, and an awaitable completion, which `ProcessRelay.WhenTerminated` now is (TS-Z) |
| FEAT-16 | Loose tags | Done with ED-40, ahead of the phase. A tag granted with no effect, ability or item behind it, as Unreal's loose gameplay tags are: `AddLooseTag`, `RemoveLooseTag` and `GetLooseTags` on each hub, each grant counted, and the tag compile includes them. A tag added straight to the tag cache went at its next compile |

**Phase 5's order,** from the review's plan for the test project (its labs, one at a time) and the table above. A lab
is a small game: assets the lab builds in code, and a bot that plays it in PlayMode tests under a live ProcessControl,
as the harness's World runs one. Each lab brings the features it finds missing, test-first.
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

Then the stretch scenarios, which drive FEAT-1, FEAT-2, FEAT-4, FEAT-10, FEAT-11, FEAT-12 and FEAT-15. FEAT-13,
networking, isn't planned. FEAT-16 is done.

## Test project: PlayForge Proving Grounds

A separate Unity project that consumes PlayForge as a local UPM package (Decision 2). The project and its shared
harness exist, with the golden scenarios, the framework and planted suites, and the editor tests (360 EditMode
tests in `Tests/Editor`, among them the editor smoke suite, which its README explains). The stress suite and the gameplay labs
don't exist yet. See
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
