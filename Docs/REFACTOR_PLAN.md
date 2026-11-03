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
- **One branch per phase:** `refactor/phase-0-upstream`, `refactor/phase-1-correctness`, and so on.
- `file:line` references were verified on 2026-10-29 against commit `01258b8`. Re-check before editing.

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
  "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" Assembly-CSharp.csproj -t:Rebuild -nologo -v:minimal
  "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe" FarEmerald.PlayForge.Tests.csproj -t:Rebuild -nologo -v:minimal
  ```
  - `Assembly-CSharp.csproj` builds Runtime, Editor and the samples; the Tests project references Runtime only.
    Output goes to `Temp\Bin\Debug\`.
  - Two `MSB3277` reference-conflict warnings come from the code-coverage and collab-proxy package DLLs. They
    are not ours; count only `CS` diagnostics.
  - Do not build `Assembly-CSharp-Editor.csproj`. It is stale (July 2026, from before the Editor asmdef) and
    compiles the Editor sources itself.
  - Unity regenerates the csproj files only while its editor is open. New files may need a manual `<Compile>`
    entry for MSBuild until then. On 2026-10-29 the three `FarEmerald.PlayForge*` projects matched the sources
    on disk exactly.
- **Golden tests:** the custom runner in `Assets/PlayForge/Tests/Editor/PlayForgeGoldenTests.cs`. Three ways to
  run it:
  - **Menu:** `PlayForge → Run Golden Tests`.
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

**Open. Ask the user; do not assume:**
1. ~~**Uncommitted changes**~~: decided, see above.
2. ~~**The test project, "PlayForge Proving Grounds"**~~: decided, see above.
3. ~~**Arcade Fighter after Phase 0**~~: decided, see above.
4. ~~**Commit space (GAS-15)**~~: decided, see above.
5. ~~**Pause (TS-Y, PC-7, PC-11)**~~: decided, see above.
6. **Licence (SP-13):** MIT on public GitHub, or the Asset Store EULA?
7. **Material packs (SP-17):** move the 842 MB and 206 MB of tracked third-party material packs to LFS, or out
   of the repo?
8. ~~**Known-red tests**~~: decided, see above.
9. ~~**Raw deltas and workers (GAS-25)**~~: decided, see above.
10. ~~**Disjoint and skill shots (GAS-36)**~~: decided, see above.
11. ~~**GameRoot and the hierarchy (PC-28)**~~: decided, see above.
12. ~~**When a random scaler rolls (GAS-27)**~~: decided, see above.
13. ~~**What Combine does (GAS-27)**~~: decided, see above.
14. ~~**Derived refreshes (GAS-27)**~~: decided, see above.

## Roadmap

| Phase | Scope | Exit gate |
|---|---|---|
| 0 | Port Arcade Fighter's 13 fixes and 22 golden tests; make three one-line fixes | Golden tests green, none masked |
| 1 | Runtime correctness: exception isolation, queued state, every critical and high bug | Fault-injection and framework suites pass |
| 2 | Release readiness: samples, packaging, docs, Unity Test Framework, CI | Clean import into a fresh Unity 6 project: zero warnings, zero missing types |
| 3 | Performance and structure: caching, pooling, one GAS core, a Register builder | Zero steady-state allocation, enforced in CI |
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
| PC-2 | C | 1 | A throw in async `RunProcess` skips the queued state; the process stays Running forever and abilities hang (B10) | `ProcessControlBlock.cs:285-302`, `AbilityProxy.cs:171, 282` | fixed |
| PC-3 | C | 1 | The hierarchy is keyed by prefab, not clone, so one GAS death cascades to every instance of the prefab | `ProcessControl.cs:500, 506`, `ProcessHierarchy.cs:112-118`, `GameplayAbilitySystem.EndOfFrame.cs:109` | fixed |
| PC-4 | C | 1 | A forced terminate runs `WhenTerminate` twice (two Destroys, unregisters and events); forced Waiting doubles `WhenWait` | `ProcessControlBlock.cs:131-138, 207-219, 281-302` | fixed |
| PC-5 | C | 1 | A queued Terminate on a running Lazy process never completes (`ActionProcess`, runtime GAS, `TaskSequenceProcess`) | `LazyMonoProcess.cs:20-24`, `LazyRuntimeProcess.cs:24-28` | fixed |
| PC-6 | C | 1 | The runtime `Register` overloads that take null data throw a NullReferenceException after using up a cache index; `RunProcessTask` hits this on every activation | `ProcessControl.cs:516, 524, 549`, `RunProcessTask.cs:25` | fixed |
| PC-7 | C | 1 | A control-state change queues a default transition for every process: it parks Synchronous processes, starts RequiresControl ones and re-runs RunThenWait ones | `ProcessControl.cs:1009, 1186-1195, 1278` | fixed |
| PC-8 | C | 1 | A throw in `WhenTerminate` leaves an unkillable zombie that is still stepped | `ProcessControlBlock.cs:211-212` | fixed |
| PC-9 | H | 1 | An out-of-scene object is cloned as if it were a prefab; the original stays uninitialized and leaks (C19) | `MonoWrapperProcess.cs:43-53` | fixed |
| PC-10 | H | 1 | A GameObject with several processes has no single teardown; an external Destroy or scene unload never unregisters (C13) | `MonoWrapperProcess.cs:93`, `AbstractMonoProcess.cs:146-149` | fixed |
| PC-11 | H | 1 | Relay Pause waits for `RunProcess` to end, replaces a pending Terminate, and Unpause re-runs from the start | `ProcessControl.cs:796-811`, `ProcessControlBlock.cs:121-124, 186-194` | fixed |
| PC-12 | H | 1 | Pause and resume in one frame let the old run's cancellation kill the new run (shared `_cts`) | `ProcessControlBlock.cs:268, 291-297` | fixed |
| PC-13 | H | 1 | Callbacks report old state equal to new; watchers never see Terminated; Unregistered fires after removal | `ProcessControl.cs:1128, 1141-1148, 738-742` | fixed |
| PC-14 | H | 1 | The first watcher never gets `onStartWatching`; `AddActions` edits a struct copy; unwatching inside a callback throws | `ProcessControl.cs:177-226` | fixed |
| PC-15 | H | 1 | A throw in `WhenInitialize` leaves a half-registered process | `ProcessControl.cs:947-948, 970-974` | fixed |
| PC-16 | H | 1 | Data packets are never released on unregister, so long-lived packets leak relays | `ProcessControl.cs:504, 549, 719` | fixed |
| PC-17 | H | 1 | `GroupProcess` never terminates itself; `DetachMember` removes every watcher | `GroupProcess.cs:88, 151, 180` | fixed |
| PC-18 | H | 1 | `LazyRuntimeProcess` never sets `IsInitialized`, so one instance can be registered twice | `LazyRuntimeProcess.cs:16-19` | fixed |
| PC-19 | H | 1 | `ActionProcess(Action)` never runs; `ActionProcess<T>`'s second constructor never sets its source | `ActionProcess.cs:9-12, 39-45` | fixed |
| PC-20 | H | 1 | Scene processes are reparented under GameRoot at startup | `ProcessControl.cs:391`, `AbstractMonoProcess.cs:71-74` | fixed |
| PC-21 | H | 1 | GameRoot is not DontDestroyOnLoad; after a scene load, every default overload uses a destroyed handler | `GameRoot.cs`, `Bootstrapper.cs:76`, `ProcessDataPacket.cs:73` | fixed |
| PC-22 | H | 1 | Step priority is computed but never used | `ProcessControl.cs:91, 106-129` | fixed |
| PC-23 | M | 1 | Bootstrap: a duplicate GameRoot still claims Instance; a failed register throws before the fallback; `[SerializeReference]` on ScriptableObjects (C03); an un-bootstrapped ProcessControl throws every frame | `GameRoot.cs:25-30`, `Bootstrapper.cs:18-19, 78-82`, `ProcessControl.cs:37, 327` | fixed |
| PC-24 | M | 3 | `PooledMonoProcessInstantiator` is a stub that returns null and leaks GameObjects | `PooledMonoProcessInstantiator.cs:7-22`, `MonoWrapperProcess.cs:90` | open |
| PC-25 | M | 1 | `ProcessTaskUtil.DoWhile` loops forever; `RemainingRuntime` truncates before multiplying | `ProcessTaskUtil.cs:111-117`, `ProcessRelay.cs:42-43` | fixed |
| PC-26 | M | 1 | Lifecycle edges: no domain-reload reset; `is null` misses destroyed singletons; the hierarchy throws after a root is destroyed; changing `ProcessTiming` at runtime; `ResetProcessControl` keeps its watchers, which is harmless, as it only runs on a fresh instance | `ProcessHierarchy.cs:313`, `ProcessControl.cs:424-446, 1021, 1064` | fixed |
| PC-27 | H | 1 | `Unpause` runs a process while ProcessControl itself is Waiting or ClosedWaiting, against 8 cells of the transition table: `ValidateStateTransition` never reads the control state. See Decisions 5 | `ProcessControl.cs:776-791, 1361-1390` | fixed |
| PC-28 | M | 1 | The hierarchy reads a process's parent when it registers, before `WhenInitialize` applies `PARENT_TRANSFORM`, and never hears of a later re-parent, so a process parented through its data is no one's child for a cascade. Decide this with PC-20, because every `SceneRoot` process would become GameRoot's child. See Decision 11 | `ProcessControl.cs` `PrepareCreatedProcess`, `AbstractMonoProcess.cs:71-74`, `ProcessHierarchy.cs:121` | fixed |
| PC-29 | M | 1 | A `StartState` of Closed or ClosedWaiting refuses GameRoot as well, so neither works as a start state. Since PC-23 the Bootstrapper reports the refusal. Registering GameRoot under Ready and entering `StartState` afterwards would fix it | `ProcessControl.cs:247`, `Bootstrapper.cs` | fixed |
| PC-30 | M | 1 | A process registered on an object that was never active outlives it. Unity sends no OnDestroy to such an object, so the process's block keeps stepping and its hierarchy node stays. This is a gap in PC-10's fix | `AbstractMonoProcess.cs` `OnDestroy`, `ProcessControl.cs` `EndDestroyedProcess` | fixed |

### TaskSequence

TS-1 to TS-10 are Arcade Fighter's findings. TS-A to TS-X are new findings from the review. TS-Y and TS-Z
had no letter in the review. TS-AA onward came from the test project.

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| TS-1 | C | 0 | Resume sets `Time.timeScale = 1` (PF-001) | `TaskSequenceProcess.cs:234` | verified |
| TS-2 | M | 0 | The README names types that do not exist (PF-010) | `Core/TaskSequence/README.md` | verified |
| TS-3 | H | 0 | The proxy and sequences are recompiled on every activation; `IsCriticalSection` builds the inner sequence 4–5 times (PF-012) | `AbilityProxy.cs:62`, `AbilitySequenceCompiler.cs:30-80`, `RunSequenceTask.cs:18-21` | verified |
| TS-4 | H | 0 | A bare `catch { }` swallows ability errors (PF-008) | `AbilitySpecContainer.cs:113-116` | verified |
| TS-5 | M | 3 | LINQ on a hot path; `AppendPath` concatenates strings and grows without bound on repeat | `SequenceStage.cs:394`, `AbilityDataPacket.cs:76-77` | open |
| TS-6 | M | 5 | Global time only; `RunProcessTask` defaults to unscaled time (C02) → FEAT-7 | `RunProcessTask.cs:14` | open |
| TS-7 | M | 5 | Critical sections are static; the compiler never emits a `BranchStage` (C12) | `AbilitySequenceCompiler.cs` | open |
| TS-8 | M | 3 | `TryActivate` runs synchronously deep into the first stages (C16) | `AbilityProxy.cs` | open |
| TS-9 | H | 1 | `OnAbilityEnd` means claim released; task callbacks fire only on the legacy path (C07) | `AbilityProxy.cs` | fixed |
| TS-10 | M | 3 | About 14 CancellationTokenSources per three-stage ability (C08, B11) | `TaskSequenceRuntime.cs` | open |
| TS-A | C | 1 | A WhenAny or WhenN stage hangs on outside skip, timeout or max duration | `SequenceCore.cs:291-294, 351-354` | fixed |
| TS-B | C | 1 | Any-policy ability stages end at once (`StageCallbackTask` satisfies "any") | `AbilitySequenceCompiler.cs:206, 296-300` | fixed |
| TS-C | C | 1 | Interrupted or failed targeting still executes; `TargetingFailed` is never read | `AbilityProxy.cs:174-177, 197-200`, `AbilitySpecContainer.cs:63-88, 138`, `AbstractTargetingAbilityTask.cs:66-69` | fixed |
| TS-D | H | 1 | `ApplyUsageAtStageStart` fires `AbilityStageEnded` at stage start | `AbilitySequenceCompiler.cs:211-232` | fixed |
| TS-E | C | 1 | Unguarded callbacks in `finally` blocks; a throwing listener skips claim release | `TaskSequenceRuntime.cs:212, 389, 544` | fixed |
| TS-F | H | 1 | `RunSequenceTask` shares the outer data packet | `TaskSequenceRuntime.cs:124, 231-232` | fixed |
| TS-G | C | 1 | Cancelling the outer ability can orphan the nested sequence | `RunSequenceTask.cs:75`, `TaskSequence.cs:106` | fixed |
| TS-H | H | 1 | FixedUpdate and LateUpdate sequences never step | `TaskSequenceProcess.cs:137`, `RuntimeWrapperProcess.cs:32-40` | fixed |
| TS-I | C | 1 | Definitions are mutated at runtime and have a single `Runtime` slot, so concurrent runs clobber each other | `TaskSequence.cs:57-67`, `TaskSequenceRuntime.cs:801` | fixed |
| TS-J | H | 1 | Skip-and-maintain calls `Clean` on tasks that are still running | `TaskSequenceRuntime.cs:302-306` | fixed |
| TS-K | H | 1 | Maintained-stage bug (first of two) | `TaskSequenceRuntime.cs:777-791` | fixed |
| TS-L | H | 1 | Maintained-stage bug (second of two) | `TaskSequenceRuntime.cs:962, 969-971` | fixed |
| TS-M | C | 1 | Async repeat never re-runs `Prepare`; the synced `Delay` spins forever on repeat | `SwarmDefenderAbilitySequences.cs:70-72` (workaround) | fixed |
| TS-N | H | 1 | Sub-stage settings are ignored | `TaskSequenceRuntime.cs:460-505` | fixed |
| TS-O | H | 1 | `IsCriticalSection` on a chain throws; repeat recursion | `TaskSequence.cs:445, 462` | fixed |
| TS-P | H | 1 | Sync-only `TaskSequenceProcess` paths are broken | `TaskSequenceProcess.cs:95-107, 141, 199-217` | fixed |
| TS-Q | H | 1 | `BuildSyncRunner` drops features | `SequenceStage.cs:467-486`, `TaskSequenceBuilder.cs:554` | fixed |
| TS-R | M | 1 | Rethrows lose the stack trace. Doesn't reproduce: every rethrown error has passed through an await, which keeps its trace, and a test holds it there | — | fixed |
| TS-S | M | 1 | The legacy `Inject` path throws a NullReferenceException. Doesn't reproduce since TS-AC took the legacy fallback away, and a test holds it there | — | fixed |
| TS-T | H | 1 | `ActiveCache` drifts from the real state | `AbilitySystemComponent.cs:22, 385, 419` | fixed |
| TS-U | H | 1 | `RunProcessTask` keeps state on a shared `[SerializeReference]` instance | `RunProcessTask.cs:11, 18` | fixed |
| TS-V | H | 1 | A non-cancellation targeting exception leaks the activation handle | `AbilityProxy.cs` | fixed |
| TS-W | M | 1 | `ElapsedTime` is unreliable | — | fixed |
| TS-X | M | 1 | Payload lists are aliased between packets | `AbilityDataPacket.cs:50` | fixed |
| TS-Y | M | 1 | Fake pause: `TryHandlePause` returns `processActive`, so a paused sequence keeps running | `TaskSequenceProcess.cs:225-228` | fixed |
| TS-Z | M | 3 | Frame drift: `Task(Action)` costs a frame, and relay polling adds a frame per nesting level | `AbilityProxy.cs:171, 282` | open |
| TS-AA | H | 1 | A cancelled sequence cleans its tasks while they still run: `AttachExternalCancellation` throws the moment the token cancels, so `CleanStage` runs before `Execute` has unwound | `TaskSequenceRuntime.cs:286, 305` | fixed |
| TS-AC | H | 1 | An ability activated while ProcessControl refuses new processes (Closed, ClosedWaiting) falls back to legacy execution, outside ProcessControl, so it runs anyway and no pause can hold it | `AbilityProxy.cs` `ActivateViaSequence`, `ActivateTargetingViaSequence` | fixed |
| TS-AD | H | 1 | A stage's or sequence's timer outlives what it timed: its source is disposed and never cancelled, so the timer fires into the next round of a repeating sequence, or into a stage run again | `TaskSequenceRuntime.cs` stage end, `Cleanup` | fixed |
| TS-AB | H | 1 | `ChannelAbilityTask` yields in its body and `DoWhileAsync` yields again, so a channel counts one frame in two and lasts twice its `ChannelDuration` | `ChannelAbilityTask.cs:27-36`, `ProcessTaskUtil.cs:94-102` | fixed |

### GAS core

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| GAS-1 | C | 0 | The runtime GAS steps in Update only, so its end of frame (workers, death) never runs | `RuntimeGameplayAbilitySystem.cs:61` | verified |
| GAS-2 | C | 0 | Pre-change workers have no effect (PF-004, now worse) | `AttributeSystemComponent.cs:275-341`, `EffectSystemComponent.cs:115, 157, 186`, `ForgeHelper.cs:229-235` | verified |
| GAS-3 | C | 0 | `ItemShelf` is never created, so `GiveItem` throws, and every read of the empty inventory allocates a new list. The rest of this finding moved to GAS-32 | `ItemSystemComponent.cs:11, 279` | verified |
| GAS-4 | C | 1 | The independent-durations container throws on construction; its struct copies never count down | `AbstractEffectContainer.cs:32, 54`, `IndependentDurationsContainer.cs:18-110` | fixed |
| GAS-5 | C | 1 | Re-application always adds one stack; Max is never enforced; the last config wins | `SingleDurationEffectContainer.cs:58`, `StackQueueEffectContainer.cs:62`, `AbstractEffectConfig.cs:261-264`, `GameplayEffectDuration.cs:281-291` | fixed |
| GAS-6 | C | 1 | Removal requirements are inverted and AND-ed; application requires the removal group to pass | `EffectTagRequirements.cs:60-63`, `GameplayEffect.cs:169-179` | fixed |
| GAS-7 | C | 1 | Cost validation compares against the signed magnitude, so it always passes | `CommonValidations.cs:19-21` | fixed |
| GAS-8 | C | 1 | OnRemove contained effects land on the source | `AbstractEffectContainer.cs:77-80` | fixed |
| GAS-9 | C | 1 | The runtime identity copy drops starting abilities, starting items and the duplicate-equip flag | `RuntimeGameplayAbilitySystem.cs:459-479` | fixed |
| GAS-10 | C | 0 | Ongoing requirements are evaluated before tags exist and never re-checked (PF-002) | `EffectSystemComponent.cs:127, 330-334, 372` | verified |
| GAS-11 | H | 0 | Source tag requirements are never enforced (PF-003) | `AbilitySpec.cs:27-31`, `AbilityTags.cs:30-33` | verified |
| GAS-12 | H | 0 | Current is applied before Base, so the clamp loses the delta (PF-005) | `IAttributeImpactCarrier.cs:138-139`, `CachedAttributeValue.cs:242-281` | verified |
| GAS-13 | H | 0 | `OnSystemInitialized` cannot be observed (PF-013) | `GameplayAbilitySystem.EndOfFrame.cs:34`, `RuntimeGameplayAbilitySystem.cs:137, 149`, `AttributeSystemComponent.cs:49` | verified |
| GAS-14 | H | 1 | Shelved effects grant the source's asset tag to their target | `AbstractEffectContainer.cs:108` | fixed |
| GAS-15 | H | 1 | Commits are stored before modifiers but read through them (−30 under ×1.5 lands as −45). See Decisions 4 | `CachedAttributeValue.cs:241-327` | fixed |
| GAS-16 | H | 1 | Tag queries count distinct tags, not weights; `SameRoot` differs between overloads | `GameplayAbilitySystem.cs:246-249`, `TagQuery.cs:83-128` | fixed |
| GAS-17 | H | 1 | Tag hierarchy helpers and the implicit `string → Tag` conversion return hash-padded tags | `Tag.cs:73-91, 148-189, 295` | fixed |
| GAS-18 | H | 1 | Attribute-set impact workers are registered and run twice | `GameplayAbilitySystem.cs:39`, `AttributeSet.cs:55`, `ForgeHelper.cs:922-926` | fixed |
| GAS-19 | H | 1 | Tag workers tick twice per frame | `GameplayAbilitySystem.cs:93`, `GameplayAbilitySystem.EndOfFrame.cs:159` (Runtime: `:177, :186`) | fixed |
| GAS-20 | H | 1 | Ability removal leaves stale tags and a stale active index; the next claim throws | `AbilitySystemComponent.cs:45, 225-227, 414` | fixed |
| GAS-21 | H | 1 | `ClaimActive` picks the wrong slot for duplicates and mismatches policies | `AbilitySystemComponent.cs:381-385, 416-419` | fixed |
| GAS-22 | H | 1 | Duration scalers are evaluated before they are initialized (attribute-backed duration returns 0) | `GameplayEffectDuration.cs:106-115` | fixed |
| GAS-23 | H | 1 | Max-abilities and max-items have no initializer, so magnitude is likely 0. From reading only; verify first | `EntityIdentity.cs:31, 36` | fixed |
| GAS-24 | H | 1 | Infinite periodic effects ignore the tick-interval scaler (C05) | `GameplayEffectDuration.cs:158-161` | fixed |
| GAS-25 | H | 1 | Raw deltas are tagged `DisallowImpact`, so workers skip them (C10). See Decision 9 | `SourcedModifiedAttributeValue.cs:40`, `ForgeHelper.cs:205` | fixed |
| GAS-26 | H | 1 | Projectile disjoint re-targets the same caller, and its payloads pile up (C14). Its covering homing shots only is GAS-36 | `DisjointProxyTaskBehaviour.cs:53`, `DisjointTarget.cs:95`, `GameplayAbilitySystem.cs:230-233` | fixed |
| GAS-36 | H | 1 | Projectile disjoint covers homing shots only: a skill shot registers no intent, so it lands in full on an entity that disjointed. Dota leaves skill shots alone, and so does PlayForge, under Decision 10: the disjoint's docs say so | `DisjointProxyTaskBehaviour.cs`, `AbstractTargetedMonoProcess.cs` | fixed |
| GAS-27 | M | 1 | Scalers and workers: `RandomizedScaler` re-rolls (Decision 12); `InlineAndDeferred` impact workers are dropped; `StandardWorkerGroup` NREs; set "Combine" did nothing, so the first element stood alone, and its loop took each element twice (Decision 13); derived refresh bypasses workers (Decision 14) | `RandomizedScaler.cs:29-46`, `ImpactWorkerCache.cs:88-98`, `StandardWorkerGroup.cs:30-89`, `AttributeSet.cs:401-430`, `AttributeBlueprint.cs` `Combine`, `AttributeSystemComponent.cs` `RefreshRegulatedAttributes` | fixed |
| GAS-28 | M | 1 | Effects: removal workers see only the last impact; contained OnApply recursion has no cycle guard; `RecordEffectApplied` and `RecordEffectRemoved` are never called | `EffectSystemComponent.cs:82-89, 110-119`, `FrameSummary.cs:154-157` | fixed |
| GAS-29 | M | 1 | Target validation and range checks fail for a runtime GAS | `AbstractTargetingAbilityTask.cs:73, 89-91`, `CommonValidations.cs:132-138` | fixed |
| GAS-30 | M | 1 | `UnlinkAllChildren` re-links; Ability unlink checks Cost instead of Cooldown | `EntityIdentity.cs:202, 210`, `Ability.cs:151` | fixed |
| GAS-31 | M | 1 | `AbstractTargetedMonoProcess` dereferences a missing target after warning about it | `AbstractTargetedMonoProcess.cs:22-26` | fixed |
| GAS-32 | C | 1 | Split from GAS-3, reachable once items can be given: `ClearAllItems` skips every other item, so their equipped effects stay; unequip removes every source's effect; `ItemSpec.IsActive` is always false, which drops every action queued by an item's effect; an item with no `ActiveAbility` throws on equip with its effects already on; tags compile before `IsEquipped` clears, so they outlive the item | `ItemSystemComponent.cs:230-240`, `ItemSpecContainer.cs:133, 150`, `ItemSpec.cs:32-35` | fixed |
| GAS-33 | H | 1 | At a fixed frame time, a durational periodic effect can lose its last tick for good. Its period and duration timers lose the same delta, so the last tick and the expiry share a frame and rounding decides which comes first: 5 ticks in 5 s lands 4 at 60, 120 and 144 fps. Jittered frame time hides it | `EffectSystemComponent.cs:361, 391-393`, `NonStackingEffectContainer.cs:31` | fixed |
| GAS-34 | M | 1 | A stack expiring doesn't refresh Continuous entries, though gaining one does, so a scaler that reads the container keeps the old stack count until the next application | `EffectSystemComponent.cs:237-243, 391`, `StackQueueEffectContainer.cs:31-34`, `IndependentDurationsContainer.cs` | fixed |
| GAS-35 | M | 1 | `EnforceScaling` tags its rescale `AllowImpact`, so an impact worker that accepts unworkable impacts sees the system's own rescaling. From reading only; verify first | `CachedAttributeValue.cs` `EnforceScaling` | fixed |
| GAS-37 | M | 1 | `EnforceScaling` scales from Current after the clamp has moved it, so under ZeroToBase an 80/100 attribute that loses half its Base ends at 50/50, not 40/50. The diagnosis read 100/100 to 25/50, which the clamp happens to put right. `ATTR_SET_DefaultSet` pairs ZeroToBase with `AutoScaleWithBase` | `CachedAttributeValue.cs:558-562` | fixed |
| GAS-38 | M | 1 | A runtime GAS's position is a `StaticTargetingPacket` that nothing updates, and the built-in targeting tasks read the caster's transform. For a runtime caster, `AutoTargetTask` ignores its range and takes every Mono GAS, unsorted, and `SelectDirectionTargetTask` always fails | `AutoTargetTask.cs:50`, `AutoTargetByAffinityTask.cs:50`, `SelectDirectionTargetTask.cs:49`, `SelectGasTargetInRangeTask.cs:47`, `RuntimeGameplayAbilitySystem.cs:228` | fixed |
| GAS-39 | L | 1 | `FrameSummarySnapshot` copies neither the applied and removed effects nor the deaths and ability injections, so `FrameComplete` listeners never see them | `FrameSummary.cs:356-367` | fixed |
| GAS-40 | L | 1 | A set's `UseAverage` takes the first element, and `Ignore` throws | `AttributeSet.cs:423-458` | fixed |
| GAS-41 | L | 1 | On `RelativeOperationContextImpactWorker`, `InlineAndDeferred` does the work twice, once inline and once queued, so lifesteal heals double. Hide the option there, or document it | `RelativeOperationContextImpactWorker.cs:22` | red |
| GAS-42 | M | 1 | A runtime GAS's death dereferences `Relay`, per the review. Whether one can die without a relay isn't checked; verify first | `RuntimeGameplayAbilitySystem_EndOfFrame.cs:127` | red |
| GAS-P1 | M | 0 | `ActionQueue` LINQ allocations; snapshot built every frame (PF-006) | `ActionQueue.cs:41, 46, 156`, `FrameSummary.cs:340-351` | verified |
| GAS-P2 | M | 0 | Tag and FrameSummary allocations (PF-007) | `TagCache.cs:60`, `TagQuery.cs:89-136`, `FrameSummary.cs:306-308` | verified |
| GAS-P3 | M | 3 | Each application revalidates every container and rebuilds all tags (area application is quadratic) | `EffectSystemComponent.cs:312-348`, `GameplayAbilitySystem.cs:250-281` | open |
| GAS-P4 | M | 3 | Recompute allocations; impacter lookups allocate every tick; string tags with no interning | `CachedAttributeValue.cs:175, 203, 348`, `GameplayEffectSpec.cs:174, 188`, `Tag.cs:266, 286-288` | open |
| GAS-S1 | M | 3 | The GAS twins duplicate about 400 lines and have drifted; `ToGASObject()` returns null on Runtime | `GameplayAbilitySystem*.cs`, `RuntimeGameplayAbilitySystem*.cs` | open |

### Editor and UI

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| ED-1 | M | 0 | Three compiler warnings: CS0162, CS8509, CS8321 (PF-009) | `GameplayEffectDrawer.cs:159-176`, `AbilityEditor.cs:138-143`, `TheForge.ViewTab.cs:1640` | verified |
| ED-2 | H | 4 | The effect inspector never shows delivery and still draws `ReverseImpactOnRemoval` | `GameplayEffectImpactDrawer.cs:117-126`, `GameplayEffectImpact.cs:131-144` | open |
| ED-3 | H | 4 | There is no `AttributeImpactEntry` drawer, so multi-entry effects cannot be authored | — | open |
| ED-4 | H | 4 | GAS Debugger: rebuilds every 0.25 s, `FindObjectsByType` every tick, Mono only, reflection on `TagCache`, raw cooldown magnitude, unlabelled badges | `GASDebugger.cs:142, 239-258, 1462, 1581-1603, 1761` | open |
| ED-5 | M | 4 | ProcessControlDebugger: usage is not CPU time; the Terminated filter is dead; Pause misleads; per-row style allocations | `ProcessControlDebugger.cs:429, 458, 617-620, 727, 776-799` | open |
| ED-6 | M | 2 | Settings: an inverted range check; Reset All goes to the wrong tab and is partial; Auto Refresh is never read | `TheForge.SettingsTab.cs:233-236, 264, 346, 1770-1785` | open |
| ED-7 | M | 2 | Numeric columns sort as strings | `TheForge.ViewTab.cs:656` | open |
| ED-8 | H | 2 | Hard delete with no reference check | `TheForge.ViewTab.cs:2552`, `AssetBrowserWindow.cs:285` | open |
| ED-9 | M | 4 | The Visualizer is blank for items and sets, and after a domain reload | `TheForge.Utilities.cs:344, 407-412` | open |
| ED-10 | M | 2 | Analysis exposes "Not Implemented" groups and truncates silently | `TheForge.AnalysisTab.cs:362, 716, 1461, 1979-1991` | open |
| ED-11 | M | 4 | The sticky-header workaround and drawer timers poll forever | `BasePlayForgeEditor.cs:524-611`, `GameplayEffectDrawer.cs:147-152` | open |
| ED-12 | H | 4 | No Undo, multi-edit or prefab overrides; `UpdateAssetTagDisplay` writes the asset on refresh (that part belongs in Phase 2) | `AbilityEditor.cs:153-160, 1167-1176`, `BasePlayForgeEditor.cs:446-447` | open |
| ED-13 | M | 4 | Template propagation on save loads everything, opens a modal mid-save and records no Undo; the validator is empty | `ForgeTemplateProcessor.cs:45-52`, `ForgeTemplatePropagator.cs:201-229` | open |
| ED-14 | H | 1 | The `GameplayEffectImpact` copy constructor drops affiliation and throws on null packets | `GameplayEffectImpact.cs:155-176` | fixed |
| ED-15 | M | 4 | "Validate All" is noisy and capped at 15; "Find All References" is a dialog | `TheForge.SettingsTab.cs:1808-1832`, `AttributeEditor.cs:261-273` | open |
| ED-16 | M | 2 | About 60 placeholder `docs.playforge.dev` links, some pointing at the wrong page | `AbilityEditor.cs:77, 116`, `EntityIdentityEditor.cs:158` | open |
| ED-17 | M | 2 | Stray `Debug.Log` calls | `GameplayEffectImpactDrawer.cs:33`, `AssetBrowserWindow.cs:348`, `AbstractRefDrawer.cs:81`, `TagDrawer.cs:745`, `TheForge.ViewTab.cs:2009` | open |
| ED-18 | M | 4 | Collapse state lives in static dictionaries keyed by property path | `AbilityTaskBehaviourStageDrawer.cs:31`, `AbilityBehaviourDrawer.cs:18-20` | open |
| ED-19 | M | 2 | Naming drift: The Forge or PlayForge Manager; the docs say `Tools → PlayForge` | `TheForge.cs:16-28`, `GETTING_STARTED.md:35, 86, 89` | open |
| ED-20 | H | 4 | No theming: inline styles, hard-coded dark backgrounds, emoji icons, dead UXML and USS | `TheForge.cs:183`, `GASDebugger.cs:108`, `ForgeDrawerStyles.cs:111-131` | open |
| ED-21 | M | 4 | Settings live in EditorPrefs and are machine-wide | `EffectTemplateRegistry.cs:12, 46`, `TemplateRegistry.cs:175` | open |

### Samples and packaging

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| SP-1 | C | 2 | 39 sample assets serialize framework types as `Assembly-CSharp`, with no `[MovedFrom]`; add a missing-type guard test | `Samples/Data/*`, `Samples/Demo/Data/*` | open |
| SP-2 | C | 2 | 621 missing-script components in DemoScene; `TestSequences.cs` is two lines | `Samples/Demo/Scenes/DemoScene.unity` | open |
| SP-3 | H | 2 | The Demo hard-depends on the Input System but its scene has a legacy input module | `DemoInputConduit.cs:5` | open |
| SP-4 | H | 2 | Samples reference assets outside the package (materials, PanelSettings, `demo_uss.uss`) | `Assets/UI Toolkit/`, `Assets/demo_uss.uss` | open |
| SP-5 | C | 2 | QuickOutline is "All rights reserved" and missing from the notices | `Samples/QuickOutline/Scripts/Outline.cs:1-7` | open |
| SP-6 | H | 2 | Swarm Defender depends on the Demo; `TestIntEffect` leftovers | `SwarmDefenderGameManager.cs:5, 32-37` | open |
| SP-7 | M | 2 | Lorem and "ASda" descriptions; an `EFECT_` typo; no sample README | `Samples/Data` | open |
| SP-8 | H | 2 | UniTask not embedded and not pinned to a revision, so projects drift (PlayForge 2.5.10, Proving Grounds 2.5.11); the Input System dependency is undeclared | `Packages/manifest.json`, `PUBLISHING.md:14-29` | open |
| SP-9 | M | 2 | Stray files: the stale `PlayForge_1.0.0.unitypackage`, `asda.unity`, `PlayForgeTesting.cs`, tracked `UIElementsSchema` and `.idea` | repo root and `Assets/` | open |
| SP-10 | M | 2 | `.claude/worktrees` (1.1 GB) is not in `.gitignore` | `.gitignore` | open |
| SP-11 | H | 2 | Examples and test processes compile into Runtime | `TaskSequenceExamples*.cs`, `SyncSequenceExamples.cs`, `GroupProcessExamples.cs`, `TestAOEMonoProcess.cs`, `TestMultiProcess.cs` | open |
| SP-12 | M | 2 | BuildVerification builds an arbitrary scene | `Editor/Tools/BuildVerification.cs:27-33` | open |
| SP-13 | C | 2 | Licence: MIT versus the Asset Store EULA (Decisions 6) | `LICENSE` | open |
| SP-14 | M | 2 | No version or changelog; `productName: GAS` | `ProjectSettings` | open |
| SP-15 | M | 2 | Stale docs: PUBLISHING claims 12/12 and names `Samples/InputHandling`; wrong menu paths | `PUBLISHING.md:11, 56, 68` | open |
| SP-16 | H | 0 | S6, S8 and S11 are masked as target semantics. The runner move is now SP-18 | `PlayForgeGoldenTests.cs:55, 61, 64` | verified |
| SP-17 | M | 2 | 842 MB and 206 MB of material packs tracked without LFS (Decisions 7) | repo | open |
| SP-18 | M | 2 | Split from SP-16: the custom golden runner should move to the Unity Test Framework (B04), so `unity test` and CI report pass/fail | `PlayForgeGoldenTests.cs` | open |
| SP-19 | C | 2 | Unused `using log4net.Appender` and `using Codice.CM.SEIDInfo` compiled only through the DLLs in `com.unity.collab-proxy`, so the Editor assembly failed in any project without that package | `AbstractRefDrawer.cs:5`, `ForgeDrawerStyles.cs:2` | fixed |
| SP-20 | H | 2 | `Samples/` is neither `Samples~` nor covered by an asmdef, so as a package its 15 scripts never compile, Unity warns about each one on every compile, and the samples can't run | `Samples/` | open |
| SP-21 | M | 2 | Editor UXML and USS break on a clean import: 4 `TypeLoadException`s from stale `…, Assembly-CSharp` type names (`FESGameplayAbilitySystem.GameRoot` and `.ProcessControl`, the removed `FrameworkIndex`, and `EAbilityActivationPolicyExtended`, which moved assembly), 9 invalid paths to assets outside the package (`Editor/Icons/Creator/ability.png`, `Assets/TextMesh Pro/…`), and an unknown `:last-child` pseudo class. Nothing references `forge_abilityDrawer.uxml`; the other two are default references in script `.meta` files, so check them against ED-20's dead UXML and USS | `fesgas_bootstrapper.uxml`, `forge_abilityDrawer.uxml`, `AbilityInspector.uss:52` | open |

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
| FEAT-8 | Effect conveniences | Cleanse by tag query, immunity, overflow at max stacks, abilities granted by effects, chance to apply |
| FEAT-9 | Save and load | Snapshots of attributes, effects, tags and cooldowns |
| FEAT-10 | Headless mode | Abilities run without a scene ProcessControl |
| FEAT-11 | Input binding | Targeting without legacy `Input` and `Camera.main` |
| FEAT-12 | Ability graph and timeline, effect recorder | Editor |
| FEAT-13 | Networking | Prediction and replication; not planned yet |

## Test project: PlayForge Proving Grounds

A separate Unity project that consumes PlayForge as a local UPM package (Decision 2). The project and its shared
harness exist, with the golden scenarios and the first framework tests; the other suites below don't yet. See
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
