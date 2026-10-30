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

**Open. Ask the user; do not assume:**
1. ~~**Uncommitted changes**~~: decided, see above.
2. **The test project, "PlayForge Proving Grounds":** a separate Unity project referencing PlayForge as a local
   UPM package, which needs a `package.json` in `Assets/PlayForge`? Or a test assembly inside this repo for now?
3. **Arcade Fighter after Phase 0:** does it switch to consuming upstream PlayForge, and how (copy, or a local
   package)?
4. **Commit space (GAS-15):** should commits be stored in baseline space and read through modifiers (current
   behaviour, which amplifies damage), or applied to the current value?
5. **Pause (TS-Y, PC-7, PC-11):** which single meaning of pause is canonical?
6. **Licence (SP-13):** MIT on public GitHub, or the Asset Store EULA?
7. **Material packs (SP-17):** move the 842 MB and 206 MB of tracked third-party material packs to LFS, or out
   of the repo?

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
  | PF-001 Resume set `Time.timeScale = 1` | `TaskSequenceProcess.cs:234` | TS-1 | fixed |
  | PF-002 Ongoing requirements checked before tags compiled | `EffectSystemComponent.cs:127, 330-334, 372` | GAS-10 | fixed |
  | PF-003 Source requirements never read | `AbilitySpec.cs:27-31`, `AbilityTags.cs:30-33` | GAS-11 | fixed |
  | PF-004 Pre-change workers got a copy (frozen `ConstantImpacter.Snapshot` + `PreviewCommit`) | `AttributeSystemComponent.cs:275-341` | GAS-2 | fixed |
  | PF-005 Current applied before Base | `IAttributeImpactCarrier.cs:138-139` | GAS-12 | fixed |
  | PF-006 `HasActions` allocated; snapshot built every frame | `ActionQueue.cs:41, 46, 156`; both `EndOfFrame` files | GAS-P1 | fixed |
  | PF-007 Tag and FrameSummary allocations (`GrantedTagCompiler`, pooling) | `TagCache.cs:60`, `TagQuery.cs:89-136`, `FrameSummary.cs:306-308` | GAS-P2 | fixed |
  | PF-008 Bare `catch { }` in `AwaitAbility` | `AbilitySpecContainer.cs:113-116` | TS-4 | fixed |
  | PF-009 Three editor warnings | `GameplayEffectDrawer.cs:159-176`, `AbilityEditor.cs:138-143`, `TheForge.ViewTab.cs:1640` | ED-1 | fixed |
  | PF-010 README names types that do not exist | `Runtime/PlayForge/Core/TaskSequence/README.md` | TS-2 | fixed |
  | PF-011 Step exceptions stalled the frame | `ProcessControlBlock.cs:225-247`, `ProcessControl.cs:337-344` | PC-1 | fixed |
  | PF-012 Proxies recompiled every activation (`AbilityProxy.IsIdle`, `CompiledProxies`) | `AbilityActivationHandle.cs:26`, `AbilityProxy.cs:62-74` | TS-3 | fixed |
  | PF-013 `OnSystemInitialized` could not be observed | `GameplayAbilitySystem.EndOfFrame.cs:34`, `RuntimeGameplayAbilitySystem.cs:137, 149` | GAS-13 | fixed |

- [x] Port Arcade Fighter's extra 10 golden scenarios, taking the suite from 12 to 22. S13–S22 carry their
  tracker IDs in their names; S23–S25 are canonical's own (TS-1, GAS-3, GAS-1).
- [x] GAS-1: give `RuntimeGameplayAbilitySystem` the timing `EProcessStepTiming.UpdateAndLate`
  (`RuntimeGameplayAbilitySystem.cs:61`, and the second constructor at `:92`).
- [x] GAS-3: initialize `ItemShelf` (`ItemSystemComponent.cs:11`). Done ahead of PF-007, whose S18 needs it.
  The finding's other three bugs are now GAS-32 (Phase 1).
- [x] SP-16: remove the `targetSemantics: true` masks on S6, S8 and S11 (`PlayForgeGoldenTests.cs:55, 61, 64`).
  They already passed at the baseline, so there was no red. S6 now pins today's answer to Decision 4
  (GAS-15). The runner's move to the Unity Test Framework is now SP-18 (Phase 2).
- [ ] Gate: the golden tests are green with none masked. Record the result here.

## Bug tracker

Severity: **C** critical, **H** high, **M** medium or low. The phase is the roadmap phase the fix belongs to.

### ProcessControl

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| PC-1 | C | 0 | A throw in `WhenUpdate` aborts the step loop; every later process misses its step (PF-011) | `ProcessControlBlock.cs:225-247`, `ProcessControl.cs:337-344` | fixed |
| PC-2 | C | 1 | A throw in async `RunProcess` skips the queued state; the process stays Running forever and abilities hang (B10) | `ProcessControlBlock.cs:285-302`, `AbilityProxy.cs:171, 282` | open |
| PC-3 | C | 1 | The hierarchy is keyed by prefab, not clone, so one GAS death cascades to every instance of the prefab | `ProcessControl.cs:500, 506`, `ProcessHierarchy.cs:112-118`, `GameplayAbilitySystem.EndOfFrame.cs:109` | open |
| PC-4 | C | 1 | A forced terminate runs `WhenTerminate` twice (two Destroys, unregisters and events); forced Waiting doubles `WhenWait` | `ProcessControlBlock.cs:131-138, 207-219, 281-302` | open |
| PC-5 | C | 1 | A queued Terminate on a running Lazy process never completes (`ActionProcess`, runtime GAS, `TaskSequenceProcess`) | `LazyMonoProcess.cs:20-24`, `LazyRuntimeProcess.cs:24-28` | open |
| PC-6 | C | 1 | The runtime `Register` overloads that take null data throw a NullReferenceException after using up a cache index; `RunProcessTask` hits this on every activation | `ProcessControl.cs:516, 524, 549`, `RunProcessTask.cs:25` | open |
| PC-7 | C | 1 | A control-state change queues a default transition for every process: it parks Synchronous processes, starts RequiresControl ones and re-runs RunThenWait ones | `ProcessControl.cs:1009, 1186-1195, 1278` | open |
| PC-8 | C | 1 | A throw in `WhenTerminate` leaves an unkillable zombie that is still stepped | `ProcessControlBlock.cs:211-212` | open |
| PC-9 | H | 1 | An out-of-scene object is cloned as if it were a prefab; the original stays uninitialized and leaks (C19) | `MonoWrapperProcess.cs:43-53` | open |
| PC-10 | H | 1 | A GameObject with several processes has no single teardown; an external Destroy or scene unload never unregisters (C13) | `MonoWrapperProcess.cs:93`, `AbstractMonoProcess.cs:146-149` | open |
| PC-11 | H | 1 | Relay Pause waits for `RunProcess` to end, replaces a pending Terminate, and Unpause re-runs from the start | `ProcessControl.cs:796-811`, `ProcessControlBlock.cs:121-124, 186-194` | open |
| PC-12 | H | 1 | Pause and resume in one frame let the old run's cancellation kill the new run (shared `_cts`) | `ProcessControlBlock.cs:268, 291-297` | open |
| PC-13 | H | 1 | Callbacks report old state equal to new; watchers never see Terminated; Unregistered fires after removal | `ProcessControl.cs:1128, 1141-1148, 738-742` | open |
| PC-14 | H | 1 | The first watcher never gets `onStartWatching`; `AddActions` edits a struct copy; unwatching inside a callback throws | `ProcessControl.cs:177-226` | open |
| PC-15 | H | 1 | A throw in `WhenInitialize` leaves a half-registered process | `ProcessControl.cs:947-948, 970-974` | open |
| PC-16 | H | 1 | Data packets are never released on unregister, so long-lived packets leak relays | `ProcessControl.cs:504, 549, 719` | open |
| PC-17 | H | 1 | `GroupProcess` never terminates itself; `DetachMember` removes every watcher | `GroupProcess.cs:88, 151, 180` | open |
| PC-18 | H | 1 | `LazyRuntimeProcess` never sets `IsInitialized`, so one instance can be registered twice | `LazyRuntimeProcess.cs:16-19` | open |
| PC-19 | H | 1 | `ActionProcess(Action)` never runs; `ActionProcess<T>`'s second constructor never sets its source | `ActionProcess.cs:9-12, 39-45` | open |
| PC-20 | H | 1 | Scene processes are reparented under GameRoot at startup | `ProcessControl.cs:391`, `AbstractMonoProcess.cs:71-74` | open |
| PC-21 | H | 1 | GameRoot is not DontDestroyOnLoad; after a scene load, every default overload uses a destroyed handler | `GameRoot.cs`, `Bootstrapper.cs:76`, `ProcessDataPacket.cs:73` | open |
| PC-22 | H | 1 | Step priority is computed but never used | `ProcessControl.cs:91, 106-129` | open |
| PC-23 | M | 1 | Bootstrap: a duplicate GameRoot still claims Instance; a failed register throws before the fallback; `[SerializeReference]` on ScriptableObjects (C03); an un-bootstrapped ProcessControl throws every frame | `GameRoot.cs:25-30`, `Bootstrapper.cs:18-19, 78-82`, `ProcessControl.cs:37, 327` | open |
| PC-24 | M | 3 | `PooledMonoProcessInstantiator` is a stub that returns null and leaks GameObjects | `PooledMonoProcessInstantiator.cs:7-22`, `MonoWrapperProcess.cs:90` | open |
| PC-25 | M | 1 | `ProcessTaskUtil.DoWhile` loops forever; `RemainingRuntime` truncates before multiplying | `ProcessTaskUtil.cs:111-117`, `ProcessRelay.cs:42-43` | open |
| PC-26 | M | 1 | Lifecycle edges: no domain-reload reset; `is null` misses destroyed singletons; the hierarchy throws after a root is destroyed; changing `ProcessTiming` at runtime; `ResetProcessControl` keeps its watchers | `ProcessHierarchy.cs:313`, `ProcessControl.cs:424-446, 1021, 1064` | open |

### TaskSequence

TS-1 to TS-10 are Arcade Fighter's findings. TS-A to TS-X are new findings from the review. TS-Y and TS-Z
had no letter in the review.

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| TS-1 | C | 0 | Resume sets `Time.timeScale = 1` (PF-001) | `TaskSequenceProcess.cs:234` | fixed |
| TS-2 | M | 0 | The README names types that do not exist (PF-010) | `Core/TaskSequence/README.md` | fixed |
| TS-3 | H | 0 | The proxy and sequences are recompiled on every activation; `IsCriticalSection` builds the inner sequence 4–5 times (PF-012) | `AbilityProxy.cs:62`, `AbilitySequenceCompiler.cs:30-80`, `RunSequenceTask.cs:18-21` | fixed |
| TS-4 | H | 0 | A bare `catch { }` swallows ability errors (PF-008) | `AbilitySpecContainer.cs:113-116` | fixed |
| TS-5 | M | 3 | LINQ on a hot path; `AppendPath` concatenates strings and grows without bound on repeat | `SequenceStage.cs:394`, `AbilityDataPacket.cs:76-77` | open |
| TS-6 | M | 5 | Global time only; `RunProcessTask` defaults to unscaled time (C02) → FEAT-7 | `RunProcessTask.cs:14` | open |
| TS-7 | M | 5 | Critical sections are static; the compiler never emits a `BranchStage` (C12) | `AbilitySequenceCompiler.cs` | open |
| TS-8 | M | 3 | `TryActivate` runs synchronously deep into the first stages (C16) | `AbilityProxy.cs` | open |
| TS-9 | H | 1 | `OnAbilityEnd` means claim released; task callbacks fire only on the legacy path (C07) | `AbilityProxy.cs` | open |
| TS-10 | M | 3 | About 14 CancellationTokenSources per three-stage ability (C08, B11) | `TaskSequenceRuntime.cs` | open |
| TS-A | C | 1 | A WhenAny or WhenN stage hangs on outside skip, timeout or max duration | `SequenceCore.cs:291-294, 351-354` | open |
| TS-B | C | 1 | Any-policy ability stages end at once (`StageCallbackTask` satisfies "any") | `AbilitySequenceCompiler.cs:206, 296-300` | open |
| TS-C | C | 1 | Interrupted or failed targeting still executes; `TargetingFailed` is never read | `AbilityProxy.cs:174-177, 197-200`, `AbilitySpecContainer.cs:63-88, 138`, `AbstractTargetingAbilityTask.cs:66-69` | open |
| TS-D | H | 1 | `ApplyUsageAtStageStart` fires `AbilityStageEnded` at stage start | `AbilitySequenceCompiler.cs:211-232` | open |
| TS-E | C | 1 | Unguarded callbacks in `finally` blocks; a throwing listener skips claim release | `TaskSequenceRuntime.cs:212, 389, 544` | open |
| TS-F | H | 1 | `RunSequenceTask` shares the outer data packet | `TaskSequenceRuntime.cs:124, 231-232` | open |
| TS-G | C | 1 | Cancelling the outer ability can orphan the nested sequence | `RunSequenceTask.cs:75`, `TaskSequence.cs:106` | open |
| TS-H | H | 1 | FixedUpdate and LateUpdate sequences never step | `TaskSequenceProcess.cs:137`, `RuntimeWrapperProcess.cs:32-40` | open |
| TS-I | C | 1 | Definitions are mutated at runtime and have a single `Runtime` slot, so concurrent runs clobber each other | `TaskSequence.cs:57-67`, `TaskSequenceRuntime.cs:801` | open |
| TS-J | H | 1 | Skip-and-maintain calls `Clean` on tasks that are still running | `TaskSequenceRuntime.cs:302-306` | open |
| TS-K | H | 1 | Maintained-stage bug (first of two) | `TaskSequenceRuntime.cs:777-791` | open |
| TS-L | H | 1 | Maintained-stage bug (second of two) | `TaskSequenceRuntime.cs:962, 969-971` | open |
| TS-M | C | 1 | Async repeat never re-runs `Prepare`; the synced `Delay` spins forever on repeat | `SwarmDefenderAbilitySequences.cs:70-72` (workaround) | open |
| TS-N | H | 1 | Sub-stage settings are ignored | `TaskSequenceRuntime.cs:460-505` | open |
| TS-O | H | 1 | `IsCriticalSection` on a chain throws; repeat recursion | `TaskSequence.cs:445, 462` | open |
| TS-P | H | 1 | Sync-only `TaskSequenceProcess` paths are broken | `TaskSequenceProcess.cs:95-107, 141, 199-217` | open |
| TS-Q | H | 1 | `BuildSyncRunner` drops features | `SequenceStage.cs:467-486`, `TaskSequenceBuilder.cs:554` | open |
| TS-R | M | 1 | Rethrows lose the stack trace | — | open |
| TS-S | M | 1 | The legacy `Inject` path throws a NullReferenceException | — | open |
| TS-T | H | 1 | `ActiveCache` drifts from the real state | `AbilitySystemComponent.cs:22, 385, 419` | open |
| TS-U | H | 1 | `RunProcessTask` keeps state on a shared `[SerializeReference]` instance | `RunProcessTask.cs:11, 18` | open |
| TS-V | H | 1 | A non-cancellation targeting exception leaks the activation handle | `AbilityProxy.cs` | open |
| TS-W | M | 1 | `ElapsedTime` is unreliable | — | open |
| TS-X | M | 1 | Payload lists are aliased between packets | `AbilityDataPacket.cs:50` | open |
| TS-Y | M | 1 | Fake pause: `TryHandlePause` returns `processActive`, so a paused sequence keeps running | `TaskSequenceProcess.cs:225-228` | open |
| TS-Z | M | 3 | Frame drift: `Task(Action)` costs a frame, and relay polling adds a frame per nesting level | `AbilityProxy.cs:171, 282` | open |

### GAS core

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| GAS-1 | C | 0 | The runtime GAS steps in Update only, so its end of frame (workers, death) never runs | `RuntimeGameplayAbilitySystem.cs:61` | fixed |
| GAS-2 | C | 0 | Pre-change workers have no effect (PF-004, now worse) | `AttributeSystemComponent.cs:275-341`, `EffectSystemComponent.cs:115, 157, 186`, `ForgeHelper.cs:229-235` | fixed |
| GAS-3 | C | 0 | `ItemShelf` is never created, so `GiveItem` throws, and every read of the empty inventory allocates a new list. The rest of this finding moved to GAS-32 | `ItemSystemComponent.cs:11, 279` | fixed |
| GAS-4 | C | 1 | The independent-durations container throws on construction; its struct copies never count down | `AbstractEffectContainer.cs:32, 54`, `IndependentDurationsContainer.cs:18-110` | open |
| GAS-5 | C | 1 | Re-application always adds one stack; Max is never enforced; the last config wins | `SingleDurationEffectContainer.cs:58`, `StackQueueEffectContainer.cs:62`, `AbstractEffectConfig.cs:261-264`, `GameplayEffectDuration.cs:281-291` | open |
| GAS-6 | C | 1 | Removal requirements are inverted and AND-ed; application requires the removal group to pass | `EffectTagRequirements.cs:60-63`, `GameplayEffect.cs:169-179` | open |
| GAS-7 | C | 1 | Cost validation compares against the signed magnitude, so it always passes | `CommonValidations.cs:19-21` | open |
| GAS-8 | C | 1 | OnRemove contained effects land on the source | `AbstractEffectContainer.cs:77-80` | open |
| GAS-9 | C | 1 | The runtime identity copy drops starting abilities, starting items and the duplicate-equip flag | `RuntimeGameplayAbilitySystem.cs:459-479` | open |
| GAS-10 | C | 0 | Ongoing requirements are evaluated before tags exist and never re-checked (PF-002) | `EffectSystemComponent.cs:127, 330-334, 372` | fixed |
| GAS-11 | H | 0 | Source tag requirements are never enforced (PF-003) | `AbilitySpec.cs:27-31`, `AbilityTags.cs:30-33` | fixed |
| GAS-12 | H | 0 | Current is applied before Base, so the clamp loses the delta (PF-005) | `IAttributeImpactCarrier.cs:138-139`, `CachedAttributeValue.cs:242-281` | fixed |
| GAS-13 | H | 0 | `OnSystemInitialized` cannot be observed (PF-013) | `GameplayAbilitySystem.EndOfFrame.cs:34`, `RuntimeGameplayAbilitySystem.cs:137, 149`, `AttributeSystemComponent.cs:49` | fixed |
| GAS-14 | H | 1 | Shelved effects grant the source's asset tag to their target | `AbstractEffectContainer.cs:108` | open |
| GAS-15 | H | 1 | Commits are stored before modifiers but read through them (−30 under ×1.5 lands as −45). See Decisions 4 | `CachedAttributeValue.cs:241-327` | open |
| GAS-16 | H | 1 | Tag queries count distinct tags, not weights; `SameRoot` differs between overloads | `GameplayAbilitySystem.cs:246-249`, `TagQuery.cs:83-128` | open |
| GAS-17 | H | 1 | Tag hierarchy helpers and the implicit `string → Tag` conversion return hash-padded tags | `Tag.cs:73-91, 148-189, 295` | open |
| GAS-18 | H | 1 | Attribute-set impact workers are registered and run twice | `GameplayAbilitySystem.cs:39`, `AttributeSet.cs:55`, `ForgeHelper.cs:922-926` | open |
| GAS-19 | H | 1 | Tag workers tick twice per frame | `GameplayAbilitySystem.cs:93`, `GameplayAbilitySystem.EndOfFrame.cs:159` (Runtime: `:177, :186`) | open |
| GAS-20 | H | 1 | Ability removal leaves stale tags and a stale active index; the next claim throws | `AbilitySystemComponent.cs:45, 225-227, 414` | open |
| GAS-21 | H | 1 | `ClaimActive` picks the wrong slot for duplicates and mismatches policies | `AbilitySystemComponent.cs:381-385, 416-419` | open |
| GAS-22 | H | 1 | Duration scalers are evaluated before they are initialized (attribute-backed duration returns 0) | `GameplayEffectDuration.cs:106-115` | open |
| GAS-23 | H | 1 | Max-abilities and max-items have no initializer, so magnitude is likely 0. From reading only; verify first | `EntityIdentity.cs:31, 36` | open |
| GAS-24 | H | 1 | Infinite periodic effects ignore the tick-interval scaler (C05) | `GameplayEffectDuration.cs:158-161` | open |
| GAS-25 | H | 1 | Raw deltas are tagged `DisallowImpact`, so workers skip them (C10) | `SourcedModifiedAttributeValue.cs:40`, `ForgeHelper.cs:205` | open |
| GAS-26 | H | 1 | Projectile disjoint covers homing only, re-targets the same caller, and its payloads pile up (C14) | `DisjointProxyTaskBehaviour.cs:53`, `DisjointTarget.cs:95`, `GameplayAbilitySystem.cs:230-233` | open |
| GAS-27 | M | 1 | Scalers and workers: `RandomizedScaler` re-rolls; `InlineAndDeferred` workers are dropped; `StandardWorkerGroup` NREs; set "Combine" double-counts; derived refresh bypasses workers | `RandomizedScaler.cs:29-46`, `ImpactWorkerCache.cs:119-129`, `StandardWorkerGroup.cs:30-89`, `AttributeSet.cs:401-430` | open |
| GAS-28 | M | 1 | Effects: removal workers see only the last impact; contained OnApply recursion has no cycle guard; `RecordEffectApplied` and `RecordEffectRemoved` are never called | `EffectSystemComponent.cs:82-89, 110-119`, `FrameSummary.cs:154-157` | open |
| GAS-29 | M | 1 | Target validation and range checks fail for a runtime GAS | `AbstractTargetingAbilityTask.cs:73, 89-91`, `CommonValidations.cs:132-138` | open |
| GAS-30 | M | 1 | `UnlinkAllChildren` re-links; Ability unlink checks Cost instead of Cooldown | `EntityIdentity.cs:202, 210`, `Ability.cs:151` | open |
| GAS-31 | M | 1 | `AbstractTargetedMonoProcess` dereferences a missing target after warning about it | `AbstractTargetedMonoProcess.cs:22-26` | open |
| GAS-32 | C | 1 | Split from GAS-3, reachable once items can be given: `ClearAllItems` skips every other item, so their equipped effects stay; unequip removes every source's effect; `ItemSpec.IsActive` is always false | `ItemSystemComponent.cs:230-240`, `ItemSpecContainer.cs:133, 150`, `ItemSpec.cs:32-35` | open |
| GAS-P1 | M | 0 | `ActionQueue` LINQ allocations; snapshot built every frame (PF-006) | `ActionQueue.cs:41, 46, 156`, `FrameSummary.cs:340-351` | fixed |
| GAS-P2 | M | 0 | Tag and FrameSummary allocations (PF-007) | `TagCache.cs:60`, `TagQuery.cs:89-136`, `FrameSummary.cs:306-308` | fixed |
| GAS-P3 | M | 3 | Each application revalidates every container and rebuilds all tags (area application is quadratic) | `EffectSystemComponent.cs:312-348`, `GameplayAbilitySystem.cs:250-281` | open |
| GAS-P4 | M | 3 | Recompute allocations; impacter lookups allocate every tick; string tags with no interning | `CachedAttributeValue.cs:175, 203, 348`, `GameplayEffectSpec.cs:174, 188`, `Tag.cs:266, 286-288` | open |
| GAS-S1 | M | 3 | The GAS twins duplicate about 400 lines and have drifted; `ToGASObject()` returns null on Runtime | `GameplayAbilitySystem*.cs`, `RuntimeGameplayAbilitySystem*.cs` | open |

### Editor and UI

| ID | Sev | Phase | Problem | Where | Status |
|---|---|---|---|---|---|
| ED-1 | M | 0 | Three compiler warnings: CS0162, CS8509, CS8321 (PF-009) | `GameplayEffectDrawer.cs:159-176`, `AbilityEditor.cs:138-143`, `TheForge.ViewTab.cs:1640` | fixed |
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
| ED-14 | H | 1 | The `GameplayEffectImpact` copy constructor drops affiliation and throws on null packets | `GameplayEffectImpact.cs:155-176` | open |
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
| SP-8 | H | 2 | UniTask not embedded; the Input System dependency is undeclared | `Packages/manifest.json`, `PUBLISHING.md:14-29` | open |
| SP-9 | M | 2 | Stray files: the stale `PlayForge_1.0.0.unitypackage`, `asda.unity`, `PlayForgeTesting.cs`, tracked `UIElementsSchema` and `.idea` | repo root and `Assets/` | open |
| SP-10 | M | 2 | `.claude/worktrees` (1.1 GB) is not in `.gitignore` | `.gitignore` | open |
| SP-11 | H | 2 | Examples and test processes compile into Runtime | `TaskSequenceExamples*.cs`, `SyncSequenceExamples.cs`, `GroupProcessExamples.cs`, `TestAOEMonoProcess.cs`, `TestMultiProcess.cs` | open |
| SP-12 | M | 2 | BuildVerification builds an arbitrary scene | `Editor/Tools/BuildVerification.cs:27-33` | open |
| SP-13 | C | 2 | Licence: MIT versus the Asset Store EULA (Decisions 6) | `LICENSE` | open |
| SP-14 | M | 2 | No version or changelog; `productName: GAS` | `ProjectSettings` | open |
| SP-15 | M | 2 | Stale docs: PUBLISHING claims 12/12 and names `Samples/InputHandling`; wrong menu paths | `PUBLISHING.md:11, 56, 68` | open |
| SP-16 | H | 0 | S6, S8 and S11 are masked as target semantics. The runner move is now SP-18 | `PlayForgeGoldenTests.cs:55, 61, 64` | fixed |
| SP-17 | M | 2 | 842 MB and 206 MB of material packs tracked without LFS (Decisions 7) | repo | open |
| SP-18 | M | 2 | Split from SP-16: the custom golden runner should move to the Unity Test Framework (B04), so `unity test` and CI report pass/fail | `PlayForgeGoldenTests.cs` | open |

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

The full plan is in the review doc's section "The test project: PlayForge Proving Grounds". In short:

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
- **First build step:** the harness, the 12 golden scenarios ported (22 after Phase 0), and red tests for the
  top 15.

**Top 15 to test first:** GAS-1, PC-1, PC-2 with TS-4, PC-3, TS-1, TS-A, TS-B, TS-C, GAS-2, GAS-3, GAS-6, GAS-7,
GAS-4 with GAS-5, PC-6, and TS-I with PC-5.

## Log

Record baselines, gate results and notable decisions here, newest first.

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
