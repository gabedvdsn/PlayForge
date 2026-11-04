# PlayForge refactor: code tour

This tour lists the places where the refactor changed code, so you can open each one in your IDE and read it. It
covers `01258b8..835b75c` on `main`: 196 commits. Phase 0 ported Arcade Fighter's fixes, Phase 1 made the runtime
correct, Phase 2 got the package ready to release, and Phase 3 has begun. Each entry says what behaves differently and
why, names its tracker IDs and Decisions, and points at the two or three places to read. `Docs/REFACTOR_PLAN.md` has
every tracker row, the numbered Decisions, and a Log of each batch's test results.

## How to use this

- Paths are relative to the repo root.
- Line numbers are as of commit `835b75c`, the head of `main` on 2026-11-03. A later commit may move them, so each
  entry also names the class and method to search for. `git show 835b75c:<path>` shows a file as it was then.
- In Rider, Go to File accepts `path:line`. In Visual Studio, open the file, then Go To Line (Ctrl+G).
  A bare `:123` after a path means another line in the same file.
- `git grep "<ID>"` finds the comment at each fix, and `git log --grep "<ID>"` finds its commit. Three exceptions:
  - Phase 0's ports cite Arcade Fighter's log ID (`PF-005` to `PF-013`) in their comments, not the tracker ID. The
    plan's Phase 0 checklist maps one to the other.
  - These fixes have no ID comment at the fix: PC-6, PC-10, PC-11, PC-14, PC-16, PC-18, PC-19, PC-21, TS-1, TS-D,
    TS-AB, GAS-2, GAS-4, GAS-5, GAS-6, GAS-9, GAS-10, GAS-11, ED-17, ED-28, and most SP rows. `git log --grep` still
    finds their commits.
  - Seven commit subjects pad the number (`PC-09`, `GAS-04` to `GAS-09`), and a bare `GAS-4` also matches GAS-40 and
    up. To match one ID exactly: `git grep -E "GAS-4([^0-9]|$)"` and `git log -E --grep "GAS-0?4([^0-9]|$)"`.
- "Decision n" is a numbered Decision in the plan. "New" means added since `01258b8`.
- "Still open" names a later tracker row on the same code, and "Fixed in Phase 3" points to
  [Phase 3 so far](#phase-3-so-far). The plan's tracker has each row's current status.
- Most fixed code carries a comment that says what it used to do, so the old behaviour is readable in place.

## Phase 3 so far

Phase 3 began on `main` with the rows that writing the reference docs turned up: five runtime bugs, and two settings
nothing read. Each has a test in Proving Grounds named after its ID, seen failing first; the runtime tests run on both
hubs. The plan's Log has the gate results.

### An entity's affiliation is its own (GAS-47)

The Mono GAS wrote the affiliation in its registration data onto its Entity asset. Every entity that shares the asset
changed side with it, and in the editor the asset itself changed. Each hub now keeps its affiliation in a field of its
own: the registration data's, or else a copy of the asset's.

- **IDs:** GAS-47, commit `fdc1886`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/MonoBehaviour/GameplayAbilitySystem.cs:68`, in
    `GameplayAbilitySystem.WhenInitialize`. The field is at `:33`, `GetAffiliation` at `:250` reads it, and the tag
    compile at `:274` adds it.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs:185`, in
    `RuntimeGameplayAbilitySystem.WhenInitialize`. It does the same for a runtime GAS, where two entities can share one
    runtime identity. The field is at `:42` and `GetAffiliation` at `:346`.
- **Test:** `GAS_47_A_registered_affiliation_stays_with_its_entity` in `EntityIdentityTests`.

### Unequipping takes back only the item's grant (GAS-48)

Unequipping removed the item's Active Ability wherever the entity held it. When the grant had failed, because the entity
already held that ability, the entity lost its own copy as the item came off. The item now notes the slot its grant
took, and takes back that slot only if the slot still holds its ability.

- **IDs:** GAS-48, commit `27c2325`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSpecContainer.cs:183`, in
    `ItemSpecContainer.RemoveActiveAbility`. `ApplyActiveAbility` notes the slot at `:167`, in `grantedAbilityIndex`
    (`:18`).
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:238`
    `AbilitySystemComponent.RemoveAbilityAt`, which removes one slot. `RemoveAbility` (`:228`) finds a slot and calls
    it.
- **New:** `AbilitySystemComponent.RemoveAbilityAt(int, Ability)`.
- **Test:** `GAS_48_Unequipping_removes_only_the_ability_the_item_granted` in `ItemTests`.

### Opening abilities wait for the entity's attributes (GAS-49)

Both hubs tried each Starting Ability set to Activate Immediately as they gave it, before any attribute existed, so the
Cost rule refused every one with a cost. The hubs now give the abilities and build the attributes, items and tags, and
only then try the immediate ones.

- **IDs:** GAS-49, commit `999e779`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:169`
    `AbilitySystemComponent.ActivateImmediateAbilities`, taken out of `Initialize`.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/MonoBehaviour/GameplayAbilitySystem.cs:91` and
    `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs:164`, each hub's call, after
    `CompileGrantedTags`.
- **New:** `AbilitySystemComponent.ActivateImmediateAbilities`.
- **Test:** `GAS_49_An_opening_ability_with_a_cost_activates` in `AbilityLifecycleTests`.

### Tick On Application alone lands its change (GAS-50)

A Durational or Infinite effect whose only tick is Tick On Application derives OnApply for Add and Flat Bonus. Only the
Instant path committed OnApply entries, so the change never landed. Applying such an effect now commits its OnApply
entries before its ticks run.

- **IDs:** GAS-50, commit `e40b3b6`.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:199`
  `EffectSystemComponent.ApplyTickOnApplication`. The Instant path, `ApplyInstantGameplayEffect`, is at `:127`.
- **Test:** `GAS_50_Tick_On_Application_alone_lands_its_change` in `EffectTickTests`.

### A set reached twice counts once (GAS-44)

An attribute set that included itself, directly or through another set, sent every walk over its sub-sets round
forever, and building the entity's attributes overflowed the stack. A set reached by two paths counted twice: its
combined elements were added twice, and its tag weighed 2. One walk now visits each set once, depth first, and
everything that walked the sub-sets uses it.

- **IDs:** GAS-44, commit `57ea4ff`.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeSet.cs:63` `AttributeSet.SelfAndSubSets`,
    the walk, and `CollectSelfAndSubSets` (`:75`), which fills a caller's buffers. `GetUnique`, `GetGrantedTags`,
    `InitWorkers` and `RemoveWorkers` use it.
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeSet.cs:363`, the `AttributeSetMeta`
    constructor, which builds the attributes from each set once.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/GrantedTagCompiler.cs:61` `GrantedTagCompiler.AddAttributeSet`. It walks
    into reused buffers (`:54`), so compiling tags still allocates nothing (GAS-P2).
- **New:** `AttributeSet.SelfAndSubSets`, `AttributeSet.CollectSelfAndSubSets`.
- **Tests:** `GAS_44_A_set_that_includes_itself_counts_once` and `GAS_44_A_set_reached_by_two_paths_counts_once` in
  `AttributeValueTests`.

### Two settings nothing read are gone (GAS-45)

An entity's Cap At Max and an item's Allow Duplicates were stored and shown, but nothing read them. The entity's level
always stays between Starting Level and Max Level, and the entity's Allow Duplicate Items decides whether an inventory
holds an item twice. Both fields are gone, with the runtime identity's copy, the two inspector toggles, and the values
the package's entity assets stored. Code that set either no longer compiles.

- **IDs:** GAS-45, commit `5ccaa88`. Removing them, not building them, was the user's call.
- **Read:** what decides instead.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/MonoBehaviour/GameplayAbilitySystem.cs:79` and
    `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs:155`, where each hub
    registers the level clamped to its range.
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSystemComponent.cs:58`, in
    `ItemSystemComponent.CanGiveItem`, where the entity's setting refuses a second copy.
- **Removed:** `EntityIdentity.CapAtMaxLevel`, `RuntimeEntityIdentity.CapAtMaxLevel`, `ItemDefinition.AllowDuplicates`.
- **Test:** `GAS_45_Settings_nothing_reads_are_gone` in `ForgeInspectorTests`, in EditMode.

### Still open in Phase 3

Performance and structure work: GAS-S1, GAS-P3, GAS-P4, PC-24, TS-5, TS-8, TS-10 and TS-Z. GAS-51, item stacking that
is declared but not built, moved to Phase 5 as FEAT-14, so an Item's `MaxStackSize` does nothing until then.

## ProcessControl

### Exception isolation

A throw from a process's own code no longer escapes into ProcessControl. It is logged with `Debug.LogException`, and
the process moves on as if the call had returned. A throwing step no longer stops the frame's other processes. A
throwing run still ends and applies its queued state, so nothing waiting on it hangs. A throwing `WhenTerminate` still
unregisters, and a throwing `WhenInitialize` refuses the process, so `Register` returns false.

- **IDs:** PC-1 (its comment says PF-011), PC-2, PC-8, PC-15.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:336`
    `ProcessControlBlock.Step`. A state queued during the step still applies after a throw.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:415`
    `ProcessControlBlock.RunProcessAsync`. The catch at `:437` ends a throwing run as a returning one ends.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:305`
    `ProcessControlBlock.TryTerminate`. `Unregister` runs whatever `WhenTerminate` did.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:1061`, in
    `ProcessControl.PrepareCreatedProcess`. A throw in `WhenInitialize` force-terminates the half-made block.
- Watcher callbacks and state-change callbacks are guarded the same way (`ProcessControl.ReportStateChange`,
  `ProcessControl.CheckWatcherOnUnregister`).

### Queued state and a requested Terminate

A requested Terminate now reaches a running process. A queued state applies only when the run ends, and a Lazy run
waits for `WhenTerminate`, so a queued Terminate never completed. `Terminate` and terminate cascades now go through
`QueueTerminate`, which tells the process through the new virtual `WhenTerminateQueued`. Its default clears
`processActive`, so a run with nothing left to do ends, and one with work still finishes. A forced Terminate or Wait
no longer runs `WhenTerminate` or `WhenWait` a second time when the cancelled run unwinds.

- **IDs:** PC-5, PC-4. PC-17: a Synchronous process with no step timing, such as `GroupProcess`, terminates at once,
  since no run or step would ever apply the queued state.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:148`
    `ProcessControlBlock.QueueTerminate`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:459`, the end of
    `RunProcessAsync`. It applies the queued state only while the process is still Running (PC-4).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceProcess.cs:248`
    `TaskSequenceProcess.WhenTerminateQueued`, which interrupts the sequence.
- **New:** `IGameplayProcess.WhenTerminateQueued`, with a default on `AbstractMonoProcess` and `AbstractRuntimeProcess`
  that clears `processActive`. `ProcessControlBlock.QueueTerminate`.

### Pause holds in place (Decision 5)

A pause takes effect at once: the process goes to Waiting and stops stepping. A process whose `TryHandlePause` returns
true keeps its run, and `TryHandleResume` picks it up. Otherwise the run is cancelled, and a new one starts on resume.
ProcessControl's own pause (Waiting, ClosedWaiting) holds what was Running and, when it ends, sends back exactly that,
so a process paused for its own reason stays paused.

Before, a control change gave every process its default transition. That parked Synchronous processes, started
RequiresControl ones, re-ran finished ones, and on closing ended GameRoot with everything it handled. A relay pause
waited for the run to end, replaced a pending Terminate, and restarted the run on resume.

- **IDs:** Decision 5. PC-7 (control changes), PC-11 (pause at once, held runs), PC-12 (a token source per run),
  PC-27 (no Unpause during ProcessControl's pause). TS-Y and TS-AB are the sequence side, under TaskSequence.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:1091`
    `ProcessControl.SetAllProcessesUponStateChange`. Only a pause, or its end, moves a process.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:180`
    `ProcessControlBlock.ForceIntoState` holds or cancels the run. `HoldForControl` (`:199`) marks what ProcessControl
    stops, and `ReleaseFromControl` (`:226`) sends it back.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:252`
    `ProcessControlBlock.TryRun`. On resume a held run carries on. A run that finished during the pause, or one a
    pending Terminate was waiting for, moves on instead of running again.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:1436`
    `ProcessControl.ValidateStateTransition`. A resume follows the transition table (PC-27), and a pending Terminate
    refuses a pause.
- **Rules worth knowing:**
  - A requested Terminate outranks a pause and survives ProcessControl's.
  - A process registered during ProcessControl's pause waits with the rest, unless it is RequiresControl:
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:1017`
    `ProcessControl.SetProcess`.
  - Pausing a process that ProcessControl already holds makes the pause its own:
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:867` `ProcessControl.Wait`.
  - A run's token source is its own, so an old run still unwinding can't cancel the new one:
    `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:419`, in
    `RunProcessAsync`.
- **New:** `ProcessControlBlock.HeldByControl`, `TerminateRequested`, `HoldForControl`, `WaitForControl`,
  `KeepPaused`, `ReleaseFromControl`; `ProcessControl.IsPaused`. `AbstractMonoProcess.TryHandlePause` and
  `TryHandleResume` are virtual now, as the runtime ones were. The contract is documented on
  `IGameplayProcess.TryHandlePause`:
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/IGameplayProcess.cs:48`.
- **Removed:** the private `GetDefaultStateWhenControlChanged`, a stateless guess at where each process should go.

### The hierarchy follows the clone and its transform (Decision 11)

The hierarchy files a Mono process under its live object, which for a prefab is the clone. It used the object handed
in, so every clone of a prefab shared one node, and one GAS's death cascaded to all of them. A process is also filed
again after `WhenInitialize` applies the parent its data asks for, and whenever its transform gets a new parent. So a
process spawned under another is its child for cascades, and one spawned at SceneRoot is GameRoot's child. Scene
processes now register with a default packet, not SceneRoot's, which reparented them, so the scene keeps its layout.

- **IDs:** PC-3, PC-28, PC-20, Decision 11.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:1030`
    `ProcessControl.PrepareCreatedProcess`. It files the clone at `:1036` and files it again after initializing at
    `:1076`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/AbstractMonoProcess.cs:161`
    `AbstractMonoProcess.OnTransformParentChanged`, which calls `ProcessControl.ProcessReparented`
    (`Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:1477`) and then
    `ProcessHierarchy.Reparent` (`Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/ProcessHierarchy.cs:147`).
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:408`
    `ProcessControl.CollectSceneProcesses`. The default packet is at `:442` (PC-20).
- **New:** `ProcessControl.ProcessReparented`, `ProcessHierarchy.Reparent`.
- **Watch for:** pausing or ending GameRoot now pauses or ends every process spawned at SceneRoot.

### A process ends with its object

A Mono process now ends however its object goes. `AbstractMonoProcess.OnDestroy` ends it, which also gives an object
with several processes one teardown; an external Destroy or a scene unload used to leave the block stepping nothing.
Unity sends no `OnDestroy` to an object that never woke, and none to the base class when a subclass declares its own,
so ProcessControl watches those blocks each Update and ends any whose object is gone. It warns once per class that
hides `OnDestroy`. Only a real prefab is cloned: a live object outside a scene, such as a `HideAndDontSave` one, is
used as it is. Outside play mode the object is destroyed with `DestroyImmediate`, since Unity refuses `Destroy` there.

- **IDs:** PC-10, PC-30, PC-31, PC-9, PC-32.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/AbstractMonoProcess.cs:151`
    `AbstractMonoProcess.OnDestroy`, which calls `ProcessControl.EndDestroyedProcess`
    (`Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:941`).
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:309`
    `ProcessControl.Sweep`, run each Update over `_dormant` (`:52`) and `_unheard` (`:56`). `HidesOnDestroy` (`:333`)
    picks the list.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/MonoWrapper/Core/MonoWrapperProcess.cs:51`
    `MonoWrapperProcess.IsPrefab` (PC-9), and `:93` `WhenTerminate` (PC-32; the object also goes whatever
    `WhenTerminate` threw, PC-8).
- **New:** `ProcessControl.EndDestroyedProcess`. To act on destruction, override `AbstractMonoProcess.WhenDestroy`
  rather than declaring `OnDestroy`.

### Bootstrapping and singletons

The Bootstrapper now makes ProcessControl in Ready, registers GameRoot, and only then enters the inspector's
StartState, so a StartState of Closed or ClosedWaiting no longer refuses GameRoot. A refused GameRoot is reported
instead of throwing halfway through the Bootstrapper. A duplicate GameRoot no longer takes `Instance` on its way out.
A ProcessControl that nothing bootstrapped steps nothing and says so once; it threw every frame. GameRoot survives
scene loads, behind an inspector toggle like ProcessControl's, since it is every default `Register` overload's handler.

- **IDs:** PC-29, PC-23, PC-26, PC-21.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Management/Bootstrapper/Bootstrapper.cs:47` `Bootstrapper.Bootstrap`. Ready
    at `:74`, the refusal at `:83`, StartState at `:96`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:265`
    `ProcessControl.Bootstrap(bool deferStartState)`, then `EnterStartState` (`:280`) and `Start` (`:289`).
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameRoot/GameRoot.cs:31` `GameRoot.Bootstrap`, and its
    `DontDestroyOnLoad` field at `:24`.
- **New:** `ProcessControl.Bootstrap(bool)`, `ProcessControl.EnterStartState`, the `GameRoot.DontDestroyOnLoad` field.
  Both singletons clear `Instance` at `SubsystemRegistration`, and the Bootstrapper tests them with Unity's null, so a
  destroyed one is replaced. See `ClearInstance` at
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:16` and
  `Assets/PlayForge/Runtime/PlayForge/Management/GameRoot/GameRoot.cs:21`.
- `[SerializeReference]` came off eleven fields that hold Unity objects, the Bootstrapper's `RegisterSets` among
  them. Unity stores those as plain references either way, so no data moves.

### Smaller fixes

- **PC-6.** The runtime `Register` overloads without a packet passed null, then threw after spending a cache index.
  An empty packet stands in, which also unblocked `RunProcessTask`.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:597`, in
  `Internal_RegisterRuntimeProcess`.
- **PC-13.** A state change is reported before ProcessControl acts on it, with the real old state, while watchers are
  still attached. `OnProcessUnregistered` fires before the removal.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:1230`
  `ProcessControl.ProcessWillChangeState`, and `:795` in `Unregister`.
- **PC-14.** The first watcher gets `onStartWatching`, a second subscription is written back into its struct, and a
  callback can stop watching. `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:186`
  `ProcessControl.WatchProcess`.
- **PC-16.** A data packet lets go of a process that ends.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:776`, in `Unregister`.
- **PC-17.** `GroupProcess.DetachMember` removes only the group's own watcher.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/GroupProcess.cs:176`.
- **PC-18.** One runtime process instance can't be registered twice at once, and one that has ended can be registered
  again. `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/AbstractRuntimeProcess.cs:29`
  `IsInitialized`, cleared by `MarkEnded` from
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/RuntimeWrapperProcess.cs:54`
  `WhenTerminate`.
- **PC-19.** `ActionProcess` steps at Update, so its action runs. `ActionProcess<T>`'s long constructor now takes a
  `T source`. `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Wrappers/ClassWrapper/ActionProcess.cs:38`.
- **PC-22.** Processes step in priority order, lowest first, then by arrival. The priority was stored and never used.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:94` `StepGroup`.
- **PC-25.** `ProcessTaskUtil.DoWhile` ends; it measured backwards and looped forever.
  `Assets/PlayForge/Runtime/PlayForge/Utils/ProcessTaskUtil.cs:104`. `ProcessRelay.RemainingRuntime` converts before
  truncating: `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessRelay.cs:43`.
- **PC-26.** A block leaves its step groups by the timing it joined under, so changing a running process's timing no
  longer strands it. The hierarchy skips a root whose object is gone.
  `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:1165` `RemoveFromStepping`.

## TaskSequence

### Each run has its own runtime

A `TaskSequence` held one runtime slot and kept run state (fired conditions, broken repeats, repeat counts) on the
shared definition. A second run left the first out of reach, and runs broke each other's repeats. Each run now has a
`TaskSequenceRuntime` of its own. A `TaskSequenceProcess` reaches only its own run, and the sequence-level calls
(`Inject`, `Interrupt`, `SkipCurrentStage`, `CheckConditions`) reach every run. Each run also keeps its own clock, so
`ElapsedTime` no longer reads 0 in a sequence without a timer.

- **IDs:** TS-I, TS-W.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequence.cs:25` `TaskSequence._runs`, then
    `CreateRuntime` (`:60`) and `Run(TaskSequenceRuntime, ProcessDataPacket, CancellationToken)` (`:76`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:79`, the per-run record of
    fired conditions, broken repeats and repeat counts, read through `IsRepeatBroken` (`:1037`) and `GetRepeatCount`
    (`:1040`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:892`
    `TaskSequenceRuntime.RunClock` (TS-W).
- **New:** `TaskSequence.CreateRuntime`, the `Run` overload that takes a runtime, `TaskSequenceRuntime.IsRepeatBroken`
  and `GetRepeatCount`.
- **Removed:** `SequenceStage.RepeatBroken`, `SequenceStage.RepeatCount`, `SequenceStage.ResetConditions`. A chain
  keeps its own condition state, as a chain is a single run.

### One core for every stage

Every stage, whether top-level, a sub-stage or the branch a `BranchStage` chose, now runs through `RunStage`. So a
sub-stage has its callbacks, timers, repeat, error handler, conditions and critical section; it used to run its tasks
and nothing else. A stage's own timer or condition acts on that stage, not on the current top-level one. Maintained
stages are tracked by index, and each cleans its tasks as it ends; moving on used to clean them while they still ran.
A run that ends early waits for them. The active stages are a list, not a stack, because a maintained stage ends under
the stages after it. The stop-maintained requests choose among maintained stages only.

- **IDs:** TS-N, TS-K, TS-J, TS-L.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:469`
    `TaskSequenceRuntime.RunStage`, and `ExecuteSubStage` (`:455`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:1211`
    `RequestSkipCurrentStage`, which acts on `_injectionSource` (`:113`), the stage applying the injection. Then
    `RequestSkipAndMaintain` (`:1217`) and the stop-maintained requests from `:1229`.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:346`
    `TaskSequenceRuntime.StopMaintainedStages`.

### Cancellation and cleanup

A cancelled stage now waits for its tasks to stop before `CleanStage` runs. Leaving the moment the token cancelled
cleaned tasks still unwinding. So a task that ignores its token holds its sequence until it ends, though
`TerminateImmediate` still ends the process at once. A WhenAny or WhenN stage that is stopped from outside ends instead
of hanging. A throwing `OnTerminate` or critical-exit listener can't skip the rest of cleanup, the claim release among
it. Stage and sequence token sources are cancelled as well as disposed, so their timers can't fire into the next round
of a repeating sequence.

- **IDs:** TS-AA, TS-A, TS-E, TS-AD.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:354`
    `TaskSequenceRuntime.ExecuteStagesSequentially`. The wait on the stage signal is at `:393` (TS-AA).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/SequenceCore.cs:259`
    `WhenAnyStagePolicy.AwaitCompletion` (the fix at `:294`), and `:323` `WhenNStagePolicy.AwaitCompletion` (`:370`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:545`, the `finally` of
    `RunStage` (TS-E, TS-AD), and `:1362` `Cleanup` (TS-AD).

### Repeats

Each iteration of a repeating stage is cleaned and prepared again, and an iteration that took no frame waits one. A
task that resets in `Prepare`, such as `SyncDelay`, finished at once from the second iteration, and a stage that
finished without yielding repeated forever inside one frame.

- **IDs:** TS-M.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:576`
  `TaskSequenceRuntime.ExecuteStageWithRepeat`, and `:622` `RestartStage`.
- **Watch for:** tasks in a repeating stage get `Prepare` and `Clean` on every iteration, not once.

### Pause inside a sequence (Decision 5)

`TaskSequenceProcess.TryHandlePause` returned `processActive`, so a paused sequence said it had paused and kept going.
It now holds its run. No stage starts or ends, and timeouts, max durations and built-in delays stop counting.
Conditions aren't checked either, since a Waiting process isn't stepped. A task's own code can't be stopped from
outside, so it holds only if it keeps time through `SequenceTaskLibrary` with the token it was handed, or watches
`data.IsPaused`. A static map from each stage's token to its run lets the library answer from the token alone, so its
helpers kept their signatures. `ChannelAbilityTask` yielded twice a loop, so a channel lasted twice its duration.

- **IDs:** TS-Y, TS-AB, Decision 5.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:133`
    `TaskSequenceRuntime.Pause`, then `IsPausedRun` (`:145`), `Delay` (`:154`) and `HoldWhilePaused` (`:165`). The
    token map is at `:55`; `ExecuteStageInternal` adds each stage's token at `:664` and holds a finished stage at
    `:685`.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceProcess.cs:263`
    `TaskSequenceProcess.TryHandlePause`, and `TryHandleResume` (`:273`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/SequenceTaskLibrary.cs:847`
    `SequenceTaskLibrary.IsPaused`, then `DeltaTime` (`:856`) and `Delay` (`:864`).
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Utility/ChannelAbilityTask.cs:23`
    `ChannelAbilityTask.Activate` (TS-AB).
- **New:** `TaskSequenceRuntime.Pause`, `Resume`, `IsPaused`, `IsPausedRun`, `Delay`; `SequenceTaskLibrary.IsPaused`,
  `DeltaTime`, `Delay`; `SequenceDataPacket.IsPaused`; `TaskSequenceChain.Pause` and `Resume`, which hold a chain
  between sequences.
- **Limit:** `SequenceTaskLibrary.ArcForce` can't hold, as physics moves the body whatever the sequence does.

### Nesting and chains

A run nested on the same packet gives the packet back as it found it; it used to clear the packet's runtime, which left
the outer run with none. A chain does the same with `Data.Chain`. Cancelling an ability ends a `RunSequenceTask`'s
inner sequence through its relay, since an interrupt can be refused by a critical section and can't reach a run that
hasn't started. A chain repeats in a loop, not by calling `Run` again from inside itself, and its `IsCriticalSection`
between two sequences is false rather than a throw.

- **IDs:** TS-F, TS-G, TS-O.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:193`
    `TaskSequenceRuntime.Run`. The outer runtime is kept at `:206` and given back at `:322`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Utility/RunSequenceTask.cs:23`
    `RunSequenceTask.Activate`. The relay ends the inner sequence at `:76`.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequence.cs:359` `TaskSequenceChain.Run`, and
    `IsCriticalSection` at `:510`.

### Sync sequences and step timings

A sync sequence keeps one packet, made in `WhenInitialize`. It made a new one every step, and swapped a plain packet
for an empty one, so what the packet carried never reached the tasks. It can be interrupted: its runner stops, and its
next step ends the process. Ended part way, it cleans the stage it's in. `BuildSyncRunner` has one implementation now.
A sync stage runs its callbacks, WhenN, RepeatUntilSkipped and skip timeouts, and what a runner can't do (sub-stages,
branches, conditions, sequence timers, async-only tasks) is named in a warning when it is built. Sequences step at
FixedUpdate and LateUpdate as well as Update. OnStep callbacks fire at their timing, and a composite timing moves a sync
sequence on at one timing only.

- **IDs:** TS-P, TS-Q, TS-H.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceProcess.cs:151`
    `TaskSequenceProcess.StepAt` (TS-H), with `AdvancingTiming` at `:344`. `WhenInitialize` (`:132`) and `Interrupt`
    (`:321`) are TS-P.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/SequenceStage.cs:445`
    `TaskSequenceDefinition.BuildSyncRunner` (TS-Q). `TaskSequenceBuilder.BuildSyncRunner` now calls it.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Synced/SyncSequenceCore.cs:240`
    `SyncedTaskSequence.Interrupt`, which also stops a runner that hasn't stepped yet.
- **New:** `SyncedTaskSequence.BreakStageRepeat`, which `SequenceDataPacket.BreakStageRepeat` falls back to.

### Abilities: activation and targeting outcome

An ability can't activate while ProcessControl takes no new processes. If ProcessControl refuses its targeting or
execution sequence later, the ability ends; it used to fall back to running outside ProcessControl, where no pause or
terminate could reach it. Failed or interrupted targeting stops the ability, unless the targeting task is set not to
break on an invalid target. So does targeting that didn't reach its end for any reason. A non-cancellation throw in
targeting is logged and cleans the activation; it used to leak the handle.

- **IDs:** TS-AC, TS-C, TS-V.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:312`
    `AbilitySystemComponent.CanActivateAbility` (TS-AC at `:317`).
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityProxy.cs:143`
    `AbilityProxy.ActivateTargetingTask` (TS-C at `:161`, TS-V at `:168`), then `ActivateTargetingViaSequence`
    (`:177`) and `ActivateViaSequence` (`:277`) for TS-AC.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpecContainer.cs:80`
    `AbilitySpecContainer.AwaitAbility`. The catch at `:92` is TS-V.
- **New:** `AbilityDataPacket.TargetingFinished`, set by the targeting adapter
  (`Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilitySequenceCompiler.cs:112`).
  `ProcessControl.AcceptsNewProcesses`.

### Abilities: stage hooks and ability events

A compiled ability stage fires `AbilityStageActivated` from the stage's `OnInit` and `AbilityStageEnded` from its
`OnTerminate`. It used to add a task that finished at once, which satisfied every Any stage, so such a stage ended as it
began. And `ApplyUsageAtStageStart`, on by default, fired `AbilityStageEnded` at the start and never at the end; now
only the usage effects follow that setting. `OnAbilityEnd` fires when the activation ends. It fired when the claim was
released, which for an ability with no critical stage is as it starts. The release has its own event,
`OnAbilityClaimReleased`. Compiled abilities fire the task callbacks too; only the legacy path did.

- **IDs:** TS-B, TS-D, TS-9.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilitySequenceCompiler.cs:204`
    `AbilitySequenceCompiler.WireStageCallbacks`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemCallbacks.cs:39` `OnAbilityEnd`, and `:50`
    `OnAbilityClaimReleased`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityTaskAdapter.cs:39`
    `AbilityTaskAdapter.Execute`.
- **New:** `AbilitySystemCallbacks.OnAbilityClaimReleased`. **Removed:** the compiler's private `StageCallbackTask`.
- **Watch for:** `AbilityStageEnded` moves to the stage's end, and `OnAbilityEnd` to the activation's end.

### Abilities: per-activation state and reuse

A task object is shared by every activation of its ability, so state for one activation now lives on the packet.
`RunProcessTask` kept its relays on itself, and the next activation replaced them, which left the first one's processes
running for good. A packet copies the request's payload lists instead of sharing them. From Phase 0: a proxy is
compiled once and reused while idle (it was compiled on every activation), a broken ability is logged rather than
swallowed by a bare `catch`, and resuming a sequence no longer sets `Time.timeScale` to 1.

- **IDs:** TS-U, TS-X, TS-3 (PF-012), TS-4 (PF-008), TS-1 (PF-001).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityDataPacket.cs:49`
    `AbilityDataPacket.GetTaskState<T>`, used by `RunProcessTask.Prepare`
    (`Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Process/Authored/RunProcessTask.cs:20`). TS-X is in the
    constructor, at `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityDataPacket.cs:72`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpecContainer.cs:195`
    `AbilitySpecContainer.TakeProxy`, with `AbilityProxy.IsIdle`
    (`Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityProxy.cs:90`).
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceProcess.cs:273`
    `TaskSequenceProcess.TryHandleResume` (TS-1).
- **New:** `AbilityDataPacket.GetTaskState<T>`, `AbilitySpecContainer.CompiledProxies` (a diagnostic count).

## GAS core

### Effects and the impact pipeline

#### Commit order inside the delivery model (Decision 4)

The OnApply, Continuous and Periodic delivery model predates the refactor; `01258b8` has it. Continuous entries are
modifiers, re-derived on every recompute and reversed by unregistering. OnApply and Periodic entries are commits, kept
in per-group history. What changed is where commits land. A commit was measured against the live value, after the
modifiers, but stored in the baseline, under them, so the modifiers scaled it again: under a ×1.5 aura a 30 hit took
45, and a +10 tome was worth 15. Committed history now comes after Add, Multiply and FlatBonus, and before Override, so
a hit takes what it says and an override still pins the value.

- **IDs:** GAS-15, Decision 4. Golden S6 now expects 85, not 90.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:336`
    `CachedAttributeValue.Recompute`. Committed history is at `:421`.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectImpact.cs:76`
    `GameplayEffectImpact.ResolveDelivery`, unchanged: how each authored shape maps to a delivery.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:127`
    `EffectSystemComponent.ApplyInstantGameplayEffect` (OnApply commits) and `:226`
    `ApplyDurationGameplayEffectAsInstant` (Periodic commits).
- `Assets/PlayForge/Documentation/EFFECT_PIPELINE.md` section 2.1 has the new order, and `IMPACT_MODEL_REFACTOR.md`
  has a dated amendment.
- **Fixed in Phase 3:** GAS-50. A Durational or Infinite effect with Tick On Application but no periodic ticks
  derives OnApply for Add and Flat Bonus, and only the Instant path committed OnApply. See
  [Phase 3 so far](#phase-3-so-far).

#### Pre-change workers and raw deltas (Decision 9)

A commit now evaluates its impacters once, freezes them as constants, and previews the incoming delta. Pre-change
workers and the pre-change callback see that delta and can change what is committed, so armor acts on incoming damage
and the clamp applies after. A raw delta grows Base before Current and shrinks it after, so the clamp no longer eats
the Current part. Under Decision 9, a raw delta (a change handed to the GAS without an effect) reaches attribute and
impact workers with no impact type: a worker that takes any type acts on it, and one that wants a type doesn't. The
system's changes to itself, such as each attribute's startup pulse, stay `DisallowImpact` and unworkable.

- **IDs:** GAS-2, GAS-12 (PF-005), GAS-25, Decision 9. Golden S15 expects 42.5 for its raw −30: the callback halves it,
  then armor halves that.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:274`
    `AttributeSystemComponent.CommitWithCarrier`. Phase 0 (freeze and preview) runs from `:284`, using
    `CachedAttributeValue.PreviewCommit`
    (`Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:271`) and
    `ConstantImpacter.Snapshot` (`Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/ConstantImpacter.cs:47`).
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/IAttributeImpactCarrier.cs:127`
    `SourceAttributeImpact.FromAdditiveDelta` (GAS-12).
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/SourcedModifiedAttributeValue.cs:43`
    `GenerateSimple`, and `:50` `GenerateUnworkable`, which the startup pulse uses
    (`Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:111`).
- **New:** `SourcedModifiedAttributeValue.GenerateUnworkable`.

#### Rescaling with AutoScaleWithBase

`EnforceScaling` takes its share from Current before the bounds, where commits land, so the bounds clamp once. Taken
from the clamped Current, an 80/100 attribute under ZeroToBase that lost half its Base ended at 50/50, not 40/50. The
rescale is typed `DisallowImpact`, as the startup pulse is, so a worker that checks impact types passes it by. Typed
`AllowImpact`, it passed every type filter, and a worker that takes unworkable impacts acted on it as if whoever
changed Base had dealt it.

- **IDs:** GAS-37, GAS-35, Decision 9.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:557`
  `CachedAttributeValue.EnforceScaling`, and the `unconstrained` value it reads (`:35`). The
  `AcceptUnworkableImpact` tooltip says what it covers:
  `Assets/PlayForge/Runtime/PlayForge/Workers/ImpactWorker/Core/AbstractImpactWorker.cs:19`.

#### Stacking

Stacks with independent durations work. The container threw on construction, and its stack packets were structs, so
ticks, expiry and refresh changed copies; they are classes now, and the effect lasts as long as its longest stack.
Re-application adds the configured stack amount, not always one, and an amount left at 0 still means one. A bound
returns how many stacks the container can still take, and configs run by `ConfigPriority`, lowest first, so a bound has
the last word. A stack that runs out, or one that `Extend` adds to independent durations, re-registers the effect's
Continuous entries, so a per-stack magnitude updates at once rather than at the next unrelated recompute.

- **IDs:** GAS-4, GAS-5, GAS-34.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/Stacking/IndependentDurationsContainer.cs:19`
    `Packets`, and the `StackableContainerPacket` class at `:54`.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectDuration.cs:267`
    `GameplayEffectDuration.GetStackAmount(AbstractStackingEffectContainer)`, and `ConfigsByPriority` (`:300`). The
    bound is `StackBoundsConfig.GetStacks`
    (`Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/Settings/AbstractEffectConfig.cs:265`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:405` `EffectSystemComponent.TickEffectShelf`
    (a lost stack at `:463`) and `:248` `TryHandleExistingDurationalGameplayEffect` (`Extend` at `:323`).

#### Durations and ticks

Duration, tick and stack-amount scalers are initialized before they are read, so an attribute-backed duration is no
longer 0. An infinite periodic effect reads its tick interval through its scaler. A durational periodic effect always
lands its last tick. Its period and duration timers counted down apart, so at some fixed frame rates the duration ended
first: 5 ticks in 5 s landed 4 at 60, 120 and 144 fps. Timers are doubles behind the float API, and a tick within 0.1%
of a period on the duration's last frame counts.

- **IDs:** GAS-22, GAS-24, GAS-33.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/GameplayEffectDuration.cs:106`
    `GameplayEffectDuration.ApplyDurationSpecifications` (GAS-22), and `:157` `ApplyInfiniteSpecs` (GAS-24).
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/AbstractEffectContainer.cs:79`
    `AbstractEffectContainer.TickIsDue`, shared by every container type.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/NonStackingEffectContainer.cs:33`
    `NonStackingEffectContainer.TickPeriodic`.

#### Tag requirements

A removal requirement now means "remove when met". An empty group asks for nothing, and either side, target or source,
is enough. The check was inverted and AND-ed, so an effect left while its requirement wasn't met, and application
demanded that the removal group pass. Ongoing requirements are checked after the new effect's tags compile, and again
when a tick is due, since tags change without any effect being applied.

- **IDs:** GAS-6, GAS-10 (PF-002).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/Definition/EffectTagRequirements.cs:63`
    `EffectTagRequirements.CheckRemovalRequirements`.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayEffect.cs:175`
    `GameplayEffect.ValidateRemovalRequirements`, and `ValidateApplicationRequirements` (`:164`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:382`
    `EffectSystemComponent.HandleGameplayEffects`, and the re-check in `TickEffectShelf` at `:436`.

#### Contained effects, records and the cycle guard

OnRemove contained effects land on the target, as OnApply and OnTick ones do; they went to the source. An effect
applied again inside its own application is refused, with one warning per entity and effect; one that contained itself
recursed until the stack overflowed. `RecordEffectApplied` and `RecordEffectRemoved` are called where the callbacks
fire; nothing called them. An instant effect's removal workers get every impact it made, not just the last. A shelved
effect grants its own asset tag; it granted its source's, so a target carried the tag of everything that had hit it.
The `GameplayEffectImpact` copy constructor keeps the affiliation and accepts an impact with no packets.

- **IDs:** GAS-8, GAS-28, GAS-14, ED-14.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:64`
    `EffectSystemComponent.ApplyGameplayEffect`. The guard is at `:75`, the record at `:104`.
  - `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Core/GameplayShelfContainer/AbstractEffectContainer.cs:87`
    `AbstractEffectContainer.OnRemove` (GAS-8), and `GetGrantedTags` (`:123`, GAS-14).
  - `Assets/PlayForge/Runtime/PlayForge/Workers/Core/WorkerContext.cs:108` `EffectWorkerContext.Impacts`, filled by
    `ApplyInstantGameplayEffect` (`Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:140`).
- **New:** `EffectWorkerContext.Impacts`.
- **Watch for:** an effect can no longer apply itself inside its own application.

### Attributes

#### Combine and UseAverage (Decision 13)

Elements set to Combine add up. Each resolves on its own, scalers included, and the blueprint's value is the sum;
bounds, rounding and retention group come from the first. `AttributeBlueprint.Combine` did nothing, so the first
element stood alone, and the loop that called it took the first in itself and each other element twice. Every combined
part's scalers are regulated, so a part scaled from another attribute refreshes with it. A set's `UseAverage` averages
the resolved values; it took the first. `Ignore` keeps the first; it threw.

- **IDs:** GAS-27 (part 4), Decision 13, GAS-40.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeBlueprint.cs:85`
    `AttributeBlueprint.Combine`, `Average` (`:95`), and `GetActiveValue` (`:59`), which sums the parts.
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeSet.cs:384`
    `AttributeSetMeta.InitializeAttributeSystem` (Combine at `:396`) and `InitializeAggregatePolicy` (`:418`, GAS-40).
  - `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:133`
    `AttributeSystemComponent.ProvideAttribute`, which regulates every part (`:143`).
- **New:** `AttributeBlueprint.Parts`, `AttributeBlueprint.Average`.
- **Fixed in Phase 3:** GAS-44, an attribute set that included itself recursed without end. See
  [Phase 3 so far](#phase-3-so-far).

#### Derived refreshes (Decision 14)

A derived attribute's refresh fired `OnAttributeChanged` alone: no post-change worker saw it, AutoScaleWithBase never
followed a derived Base, and the frame summary never heard of it. It now takes the post-change half of the pipeline
(`EnforceScaling`, post-change workers, `AttributePostChange`) and is recorded as received and dealt, self to self.
It's carried by the attribute's root, typed `DisallowImpact`, so a worker that checks impact types passes it by.

- **IDs:** GAS-27 (part 5), Decision 14.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:441`
  `AttributeSystemComponent.RefreshRegulatedAttributes`. The new half starts at `:462`.

#### Random scalers keep their roll (Decision 12)

`RandomizedScaler` rolls once per effect spec and keeps the roll with what the spec captures, so a buff holds its roll
and every tick of one application deals the same. It rolled on every evaluation, and a modifier is evaluated on every
recompute, so a random buff re-rolled whenever its attribute changed. `RollOncePerSpec`, on by default, switches back.

- **IDs:** GAS-27 (part 1), Decision 12.
- **Read:** `Assets/PlayForge/Runtime/PlayForge/GameplayEffect/Modifier/Default/Authored/RandomizedScaler.cs:28`
  `RandomizedScaler.Initialize`, and `Roll` (`:40`).
- **New:** `RandomizedScaler.RollOncePerSpec`.

### Abilities and targeting

#### Target resolution (GAS-46)

Targeting files a targeting packet under `Target`, or under the behaviour's implicit tag. Everything that reads a target
(Apply Effects, Apply/Remove Effects, Check Range, the after-targeting checks, a process spawned with the ability's
data) asks `Target Real` for the entity, and nothing wrote `Target Real`. So an ability authored the documented way
applied nothing; the tests set `Target Real` by hand, which hid it. When targeting ends, the activation now files the
entity that the packet points at under `Target Real`, unless something is there already. A Runtime GAS's own packet now
points back at it, as a Mono GAS's does.

- **IDs:** GAS-46, the newest runtime fix, found while writing ED-16's docs.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityDataPacket.cs:133`
    `AbilityDataPacket.ResolveTarget`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpecContainer.cs:104`, in
    `AbilitySpecContainer.AwaitAbility`, followed by `ValidateAllActivationRequirements` at `:106`.
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs:75`, in the first
    constructor, and `:108` in the second.
- **New:** `AbilityDataPacket.ResolveTarget(Tag)`.
- **Watch for:** target activation rules and target tag requirements now find a target, so they run and can refuse an
  activation.

#### Claims and the active list

A claim now goes to the container's own slot, under the policy the activation asked for. It went to the first slot
that held the same ability, and under the definition's policy, where `EndActivation` never looked, so the active list
drifted from what was running. Overlapping activations share the slot until the last one under that policy ends.
Removing an ability takes it off the active list before tags compile, and still lets whatever was queued behind it go.

- **IDs:** GAS-21, TS-T, GAS-20.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:411`
    `AbilitySystemComponent.ClaimActive`, then `ReleaseClaim` (`:429`) and `EndActivation` (`:446`).
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:228`
    `AbilitySystemComponent.RemoveAbility`.
- **New:** `AbilitySpecContainer.IsActiveUnder(EAbilityActivationPolicy)`.

#### Activation checks

The default Cost rule checks what the cost leaves, `Current + change >= 0`. It compared the value with the signed
magnitude, so every cost passed and an effect that adds to the attribute was refused. An ability's source tag
requirements ("can't use while stunned") are enforced; they were authored and never read.

- **IDs:** GAS-7, GAS-11 (PF-003).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Utils/Validation/CommonValidations.cs:11` `CostValidation.Validate`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySpec.cs:27`
    `AbilitySpec.ValidateSourceActivationRequirements`.
- **Watch for:** costs now block activation.
- **Fixed in Phase 3:** GAS-49. Both hubs gave Starting Abilities before building attributes, so the Cost rule
  refused an Activate Immediately ability that had a cost. See [Phase 3 so far](#phase-3-so-far).

#### Targeting for a runtime GAS

Targeting and range checks read targeting packets, which both hubs have, not GameObjects, which a runtime GAS lacks.
Validating a runtime target threw, and a range check with a runtime GAS at either end always failed. The four built-in
targeting tasks measure from the caster's targeting packet. A runtime GAS's position is that packet's, which whoever
drives the entity keeps current, and a physics query still finds Mono targets only. `AutoTargetTask` takes every
collider in range; its buffer was `MaxTargets` long, so 0 (every match) found nothing, and at 1 the caster's own
collider could fill it.

- **IDs:** GAS-29, GAS-38, GAS-43.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Targeting/AbstractTargetingAbilityTask.cs:70`
    `AbstractTargetingAbilityTask.TargetIsValid(ITarget)`.
  - `Assets/PlayForge/Runtime/PlayForge/Utils/Validation/CommonValidations.cs:126` `CheckRangeValidation.Validate`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Tasks/Targeting/Authored/AutoTargetTask.cs:47`
    `AutoTargetTask.Activate`, and `GatherCandidates` (`:78`). `AutoTargetByAffinityTask`,
    `SelectDirectionTargetTask` and `SelectGasTargetInRangeTask` have the same GAS-38 change.

#### Disjoint and homing shots (Decision 10)

A disjoint now re-aims each homing shot at a `DisjointTarget` placeholder where its target stood, which takes nothing.
It re-aimed the shot at the entity it was already aimed at, so the shot hit anyway. A shot withdraws its targeted intent
when it lands, ends, or is re-aimed, and a hub records an intent once, so a target's list no longer only grows. Under
Decision 10 a disjoint stops homing shots only, as in Dota; a skill shot registers no intent and lands as usual. That
part is documentation only. A targeted process with no `Target Real` warns and ends its run; it threw.

- **IDs:** GAS-26, GAS-36, Decision 10, GAS-31.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Authored/CompositeBehaviours/ProjectileDisjoint/Core/DisjointProxyTaskBehaviour.cs:30`
    `DisjointProxyTaskBehaviour.RunAsync`. The class summary at `:9` states Decision 10.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/Authored/AbstractTargetedMonoProcess.cs:34`
    `AbstractTargetedMonoProcess.RunProcess`, `SetTarget` (`:58`), and `WhenInitialize` (`:18`, GAS-31).
- **New:** `ITarget.WithdrawTargetedIntent`, a default no-op that both hubs implement
  (`Assets/PlayForge/Runtime/PlayForge/GAS/Interfaces/ITarget.cs:11`).

### Items

Items can be given and taken off cleanly. `ItemSystemComponent` never created its shelf, so `GiveItem` threw (Phase 0).
`ClearAllItems` counted up while removing, so it skipped every other item and dropped those with their effects still
on. Unequipping removed the effect by asset, from every source; it now removes only what this item applied.
`ItemSpec.IsActive` was always false, which dropped every action an item's effect queued. An item with no Active
Ability, which is any passive item, threw on equip with its effects already on. Tags compiled before `IsEquipped`
cleared, so they outlived the item.

- **IDs:** GAS-3, GAS-32.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSystemComponent.cs:228`
    `ItemSystemComponent.ClearAllItems`. The shelf is created at `:11` (GAS-3).
  - `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSpecContainer.cs:84` `ItemSpecContainer.Unequip`, then
    `RemoveGrantedEffects` (`:139`) and `ApplyActiveAbility` (`:158`).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:357`
    `EffectSystemComponent.RemoveGameplayEffect(GameplayEffect, IEffectOrigin)`.
- **New:** `ITarget.RemoveGameplayEffect(GameplayEffect, IEffectOrigin)`, a default method that falls back to the old
  overload so other implementers still compile (`Assets/PlayForge/Runtime/PlayForge/GAS/Interfaces/ITarget.cs:30`).
  `ItemSpec.IsEquipped`.
- **Fixed in Phase 3:** GAS-48, unequipping removed the Active Ability even when equipping hadn't granted it. See
  [Phase 3 so far](#phase-3-so-far).
- **Fixed in Phase 3:** GAS-45, an Item's `AllowDuplicates` was never read, and it is gone. See
  [Phase 3 so far](#phase-3-so-far).
- **Moved to Phase 5:** GAS-51, item stacking is declared but not built. FEAT-14 builds it.

### Tags

Tag queries count weights in every match mode, and both hubs ask the tag cache, which has the weights. They asked
through the tag list, which counted each tag once, and a hierarchical query counted distinct tags while an exact one
counted weight. The three `CountMatches` overloads share one match, so `SameRoot` is written once. `Tag.Child`,
`GetParent`, `GetRoot`, `GetAncestors` and the implicit `string` to `Tag` conversion return the plain tag of that name.
They returned hash-padded deterministic tags, so a `SameRoot` query with a root tag never found its children. Tag workers
tick once a frame, at the end of frame; both hubs also ticked them in Update. From Phase 0, compiling granted tags walks
the sources' lists instead of allocating iterators.

- **IDs:** GAS-16, GAS-17, GAS-19, GAS-P2 (PF-007).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Tag/TagQuery.cs:98` `TagQuery.CountMatches(TagCache)`, and `Matches`
    (`:135`). The hubs call it from `QueryTags`
    (`Assets/PlayForge/Runtime/PlayForge/GAS/Core/MonoBehaviour/GameplayAbilitySystem.cs:262`).
  - `Assets/PlayForge/Runtime/PlayForge/Tag/Tag.cs:84` `Tag.Child`, and the implicit conversion at `:299`.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/GrantedTagCompiler.cs:14` `GrantedTagCompiler`.
- **New:** `GrantedTagCompiler` (internal).
- **Watch for:** code that matched the implicit conversion against `Tag.GenerateAsUnique` of the same name no longer
  matches. Tag workers run at half their old rate.

### Workers

An impact worker set to `InlineAndDeferred` runs both halves; the cache had no case for it and ran neither. On
`RelativeOperationContextImpactWorker` that setting runs deferred, since both halves do the same work and lifesteal
healed double. A worker group hands workers only to systems the source has. A set's workers are given out once,
through `InitWorkers`; `AttributeSet.Initialize` gave them out too, so each ran twice. The frame snapshot carries the
applied and removed effects, deaths and ability injections, which `FrameComplete` listeners never saw. From Phase 0, an
idle frame allocates nothing: `ActionQueue` keeps a count, and the snapshot is built only for a listener.

- **IDs:** GAS-27 (parts 2 and 3), GAS-41, GAS-18, GAS-39, GAS-P1 (PF-006).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Workers/ImpactWorker/Core/ImpactWorkerCache.cs:65`
    `ImpactWorkerCache.RunImpactData`. The new case is at `:100`.
  - `Assets/PlayForge/Runtime/PlayForge/Workers/ImpactWorker/Authored/RelativeOperationContextImpactWorker.cs:31`
    `RelativeOperationContextImpactWorker.Execution` (GAS-41).
  - `Assets/PlayForge/Runtime/PlayForge/Workers/Core/FrameSummary.cs:360`, the `FrameSummarySnapshot` constructor
    (GAS-39). `StandardWorkerGroup.ProvideWorkersTo`
    (`Assets/PlayForge/Runtime/PlayForge/Workers/Core/StandardWorkerGroup.cs:26`) has the guard.
- How raw deltas and the system's own changes reach workers is under Effects (Decision 9) and Attributes (Decision 14).

### The two GAS hubs

Fixes land in both `GameplayAbilitySystem` and `RuntimeGameplayAbilitySystem`, which still duplicate about 400 lines;
the merge is GAS-S1, Phase 3. The runtime hub steps `UpdateAndLate`, so its end of frame (workers, death) runs; it
stepped in Update only. Both hubs create `Callbacks` with the object, so a caller can subscribe to
`OnSystemInitialized` before a Mono GAS initializes. A runtime GAS still initializes inside its constructor. A runtime
identity copied from an `EntityIdentity` keeps its starting abilities, starting items and
`AllowDuplicateEquippedItems`. Max abilities, items and equipped items default to 99; a new asset's were 0, and a
runtime identity's were null and threw. A hub stepped by hand, outside ProcessControl, can die.

- **IDs:** GAS-1, GAS-13 (PF-013), GAS-9, GAS-23, GAS-42. The runtime hub's targeting packet is GAS-46 and GAS-38,
  above.
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs:60`, the constructor
    (`UpdateAndLate` at `:68`), and `RuntimeEntityIdentity(EntityIdentity)` at `:463` (GAS-9).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Runtime/RuntimeGameplayAbilitySystem_EndOfFrame.cs:123` and
    `Assets/PlayForge/Runtime/PlayForge/GAS/Core/MonoBehaviour/GameplayAbilitySystem.EndOfFrame.cs:105`, each hub's
    `HandleDestruction` (GAS-42).
  - `Assets/PlayForge/Runtime/PlayForge/GAS/Core/Identity/EntityIdentity.cs:30`, the defaults (GAS-23).
- **Still open:** GAS-S1, one core for both hubs.
- **Fixed in Phase 3:** GAS-47, the Mono GAS wrote the affiliation from its registration data onto its shared Entity
  asset. See [Phase 3 so far](#phase-3-so-far).

## Editor

### Help buttons open the package's docs (Decision 18)

The 50 help buttons linked to `docs.playforge.dev`, a domain PlayForge doesn't own, and three named the wrong page.
They now open the package's own `Documentation/*.md` pages, through one table of topics, in the system's app for
Markdown, and only on a click (Asset Store rule 2.5.1.d). Six reference pages are new, one per asset type.

- **IDs:** ED-16, Decision 18 (Q33).
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeDocs.cs:79` `ForgeDocs.Open`, with the `Topics`
    table at `:17` and `DocumentationFolder` at `:88`, which finds the folder in a package or under `Assets`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:157`
    `BasePlayForgeEditor.GetDocumentationTopic`, read by `CreateEditorHeader` at `:792`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeDrawerStyles.cs:460` `SectionConfig.HelpTopic`.
- **New:** `ForgeDocs`; `Documentation/ABILITIES.md`, `EFFECTS.md`, `ITEMS.md`, `ENTITIES.md`, `ATTRIBUTES.md`,
  `ATTRIBUTE_SETS.md`.
- **Renamed:** the abstract `GetDocumentationUrl()` is now the virtual no-op `GetDocumentationTopic()`, and
  `SectionConfig.HelpUrl` is `HelpTopic`. Topics are uniform slugs such as `abilities/leveling`.

### Delete moves the asset to the trash (Decision 18)

The Forge's Delete and the asset browser's default delete both go through `ForgeAssetDeletion`. It lists the project's
assets, prefabs and scenes that refer to the asset, asks, and moves it to the trash, where it can be restored. Both
deleted for good, without looking.

- **IDs:** ED-8, Decision 18 (Q16).
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeAssetDeletion.cs:25`
    `ForgeAssetDeletion.MoveToTrash`, and `FindUsers` (`:43`). Tests answer the question through `Confirm` (`:21`).
  - Callers: `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.ViewTab.cs:2544` and
    `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Windows/AssetBrowserWindow.cs:280`
    `AssetAction.Delete`.
- **New:** `ForgeAssetDeletion`.

### Showing an asset never writes its tag (Decision 18)

The five asset editors rewrote the asset tag from the name every time they built or refreshed, with no Undo and no
dirty mark. Showing now only shows: a stored tag that differs from the name's turns amber and offers Update. A rename
records the name and the tag it gives in one undoable step. Item and Effect preview the tag while the name is typed.

- **IDs:** ED-12 (its Phase 2 part; the row stays open for Phase 4's Undo, multi-editing and prefab overrides).
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:996`
    `BasePlayForgeEditor.ShowAssetTag`, and `StoreAssetTag` (`:1023`).
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/AbilityEditor.cs:151`, the rename callback,
    and `SyncAssetTag` (`:1164`). The other four editors follow the same shape.
- **New:** `BasePlayForgeEditor.ShowAssetTag` and `StoreAssetTag`, and a `SyncAssetTag` in each asset editor.

### One pair of tag display settings

"Display full tag path" and "Group tags by root" live in `TagDisplaySettings`, which the tag field, the View tab's tag
list and the Settings tab all read and write. The tag field returned before reading the full-path setting, Settings
showed grouping off while the field defaulted it on, and the View tab had switches of its own.

- **IDs:** ED-25.
- **Read:**
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.SettingsTab.cs:1924`
    `TagDisplaySettings`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Tag/TagDrawer.cs:175` `TagDrawer.GetDisplayText`.
  - `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.ViewTab.cs:1626`, the View tab's
    chips.
- **New:** `TagDisplaySettings` (internal, in `TheForge.SettingsTab.cs`).

### Menu and window titles (Decision 18)

The menu moved from `PlayForge/` to `Window/PlayForge/`, where the Asset Store expects it (rule 2.5.1.a). Each window's
title matches its menu item: The Forge (it said "PlayForge Manager"), GAS Debugger, Process Control, Tag Debugger. Run
Golden Tests left the menu; the Test Runner runs the scenarios (SP-18).

- **IDs:** ED-19, Decision 18 (Q31, Q32).
- **Read:** `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.cs:91`
  `TheForge.ShowWindow`, with `WINDOW_TITLE` at `:30`. The other menu items are
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/GASDebugger.cs:134`,
  `Assets/PlayForge/Editor/Tools/ProcessControlDebugger.cs:52` and `Assets/PlayForge/Editor/Tools/RuntimeTagViewer.cs:20`.

### The Forge: settings, sorting, analysis and templates

- **ED-6.** The inspector delay fields save a value in range and clamp one outside it; the range check was inverted.
  Reset All resets every setting The Forge writes, keeps templates, and rebuilds the tab it was pressed on; it went to
  Analysis. "Auto-refresh on window focus" works: focus refreshes after the project has changed. Nothing read it.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.SettingsTab.cs:1725`
  `ResetAllSettings`, `:1745` `AddDelaySetting`, and
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.cs:194` `TheForge.OnFocus`.
- **ED-7.** Columns sort in natural order, so 9 comes before 10.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.ViewTab.cs:648` `SortAssets`.
- **ED-10.** Analysis offers only the groups that have an analyser (Effects and Abilities), and a capped list ends with
  "…and N more". `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TheForge.AnalysisTab.cs:355`
  `BuildAnalysisTab`, and `:735` `CreateMoreLabel`.
- **ED-22.** `TemplateRegistry` kept one EditorPrefs entry for every project on the machine, and `Save` drops the
  templates whose assets the current project lacks, so saving in one project wiped every other's. Each project now
  has its own key, from `PlayerSettings.productGUID`. The first load migrates from the shared key and leaves it alone.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Manager/TemplateRegistry.cs:114` `PrefsKey`, and
  `:139` `Load`.

### Inspector fixes

- **ED-23.** An Item's Local Data can be edited; `ItemEditor` drew no section for it.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/ItemEditor.cs:867` `BuildLocalDataSection`.
- **ED-24.** `ParticipateInSiblingCascade` is drawn; it was hidden and drawn nowhere, though ProcessControl reads it.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/AbstractMonoProcessEditor.cs:85`, in
  `CreateInspectorGUI`.
- **ED-26.** The GAS Debugger removes its frame-summary hooks when it closes. They kept sampling every entity and
  stacked when it reopened. `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/GASDebugger.cs:153`
  `OnDisable`, and `:320` `RemoveFrameSummaryHooks`.
- **ED-29.** Allow Duplicate Equipped Items opens with its own value.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/Assets/EntityIdentityEditor.cs:915`, in
  `BuildItemsSection`.
- **ED-30.** Local Data's ScriptableObject and MonoBehaviour rows each bind their own field.
  `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Drawers/Other/DataWrapperDrawer.cs:128`, in `UpdateValueField`.

### Removed from the editor

- `QuickFillWindow`, an IMGUI window nothing opened, and `AbilityEditor.m_AbilityIcon`, which nothing assigned. Four
  editor `.meta` files lost default references to UXML that exists nowhere (ED-28).
- `BootstrapperEditor`, which held `fesgas_bootstrapper.uxml` in a field it never used; that UXML named types that no
  longer exist. Also the unreferenced `forge_abilityDrawer.uxml`. The Bootstrapper keeps the same default inspector
  (SP-21).
- Stray `Debug.Log` calls (ED-17), Phase 0's three compiler warnings (ED-1), and two unused usings that compiled only
  with `com.unity.collab-proxy` (SP-19).

## Samples and packaging

### The samples move to `Samples~` (Decision 16)

`Samples/` was neither `Samples~` nor under an assembly definition, so in a UPM install its 15 scripts never compiled,
and Unity warned about each one on every compile. The samples are now `Samples~/Demos` and `Samples~/Code Examples`,
listed in `package.json`, and Package Manager imports one on request. Unity doesn't import a `~` folder, so to edit a
sample in this repo, import it first. The Asset Store export copies them in on the packaging branch.

- **IDs:** SP-20, Decision 16. SP-11 moved the TaskSequence and group process examples, about 3,000 lines, out of the
  runtime into Code Examples, and deleted `TestAOEMonoProcess` and `TestMultiProcess`.
- **Read:**
  - `Assets/PlayForge/package.json:14`, the `samples` list.
  - `Assets/PlayForge/Samples~/Demos/FarEmerald.PlayForge.Samples.asmdef`, which compiles only where the Input System
    is installed, through `versionDefines` (SP-3).
  - `Assets/PlayForge/Samples~/Code Examples/FarEmerald.PlayForge.Examples.asmdef` (SP-11).
- **New:** both sample assemblies, and a README in each sample (SP-7).

### The Demos render in URP (Decision 17)

The Demos' ten Standard materials drew magenta under URP. They now use `PlayForge/Samples/Demo Lit`, which keeps
Standard's property names, so each material keeps its values. In the Built-in pipeline it borrows Standard's own
passes and draws what Standard drew. Under URP its own pass lights the surface by the main light, with no shadows. It
declares the URP globals it reads instead of including URP's files, so it compiles in a project without URP. It
declares all of Standard's properties, since an undeclared `_SrcBlend` or `_DstBlend` reads 0 and draws black.

- **IDs:** SP-23, Decision 17.
- **Read:** `Assets/PlayForge/Samples~/Demos/Demo/Shaders/DemoLit.shader:42`, the URP SubShader, and `:113`, the
  Built-in one.
- **New:** `DemoLit.shader`.

### A play session starts clean with domain reloading off (Decision 17)

With domain reloading off, statics outlive play mode. What a session leaves behind is now cleared at
`SubsystemRegistration`, as a domain reload would clear it. The attribute registry is cleared: last session's attribute
kept its name, so this session's attribute of that name was turned away. So are the sequence runs-by-token map, the
activation id counter, the pipeline config registry, and ProcessControl's and GameRoot's `Instance`. `TagRegistry` is
kept on purpose: static tag fields register once per domain, as their types initialise, and wouldn't register again.

- **IDs:** SP-22, PC-26, Decision 17. The Asset Store requires this from Editor 6.6 (rule 2.5.h).
- **Read:**
  - `Assets/PlayForge/Runtime/PlayForge/Utils/AttributeRegistry.cs:14` `AttributeRegistry.Clear`.
  - `Assets/PlayForge/Runtime/PlayForge/Core/TaskSequence/Async/TaskSequenceRuntime.cs:60`
    `TaskSequenceRuntime.ClearRuns`.
  - `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilityActivationHandle.cs:22`
    `AbilityActivationHandle.ResetIds`.
  - `Assets/PlayForge/Runtime/PlayForge/Management/GameRoot/ConfigRegistry/PipelineConfigRegistry.cs:20`
    `PipelineConfigRegistry.Clear`.
  - `Assets/PlayForge/Runtime/PlayForge/Tag/TagRegistry.cs:23`, the note on why the tag registry stays.

### Sample content

- **SP-1.** 39 sample assets named the assembly of 16 framework types as `Assembly-CSharp`, from before the runtime
  had its own assembly, so their `[SerializeReference]` values didn't resolve. They name `FarEmerald.PlayForge`.
- **SP-2.** DemoScene lost 621 components of `TestSequences`, a script with no class.
- **SP-3.** The Demo's EventSystem uses `InputSystemUIInputModule`; the legacy module threw under the Input System.
- **SP-4.** The samples use no asset outside the package. The UI Toolkit panel settings and theme were copied in with
  their GUIDs, and the material packs' materials gave way to plain ones.
- **SP-5, Decision 15.** QuickOutline, whose header reserves all rights, is replaced by the Demo's own outline:
  `Assets/PlayForge/Samples~/Demos/Demo/Scripts/DemoOutline.cs:13` `DemoOutline`, and `DemoOutline.shader`.
- **SP-6.** Swarm Defender's debug hotkeys and `TestIntEffect` fields are gone.
- **SP-7.** Placeholder descriptions are filled in, and `EFECT_Speed_Override` is `EFFECT_Speed_Override`, with its
  GUID kept.

### Package and repo

- **The package (Decision 2).** `Assets/PlayForge/package.json` makes PlayForge the local UPM package
  `com.faremerald.playforge` 0.1.0, for Unity 6000.0. It depends on `com.unity.ugui`. A package can't declare a git
  dependency, so its description asks each project to add UniTask pinned to 2.5.11 (SP-8); this repo's
  `Packages/manifest.json:3` does.
- **SP-12.** `BuildVerification.BuildWindowsPlayer` builds an empty scene of its own when the build settings enable
  none; it built whichever scene the AssetDatabase listed first. The class itself is new in this range (`455dd44`).
  `Assets/PlayForge/Editor/Tools/BuildVerification.cs:21`.
- **SP-13, Decision 6.** `LICENSE` now points to the Unity Asset Store EULA; the MIT licence is gone.
- **SP-14.** `Assets/PlayForge/CHANGELOG.md` is new, and the project's product name and version are PlayForge 0.1.0.
- **SP-21.** The editor's UXML and USS import cleanly into a fresh project (see Removed from the editor).
- **SP-9, SP-10, SP-17 (Decision 7).** Stray files are gone. `.gitignore` covers `**/.claude/worktrees/`, `.idea/`,
  `/UIElementsSchema/` and the two material packs, which stay on disk untracked. History wasn't rewritten.

## Tests

- **The golden runner** is in `Assets/PlayForge/Tests`:
  - `Assets/PlayForge/Tests/Editor/PlayForgeGoldenTests.cs:47`, the `Scenarios` table: S1 to S12 are the originals,
    S13 to S22 came from Arcade Fighter, and S23 to S25 are new. From S13 each name carries the tracker ID it pins.
    `RunAll` (`:79`) runs them all and exits batch mode with 0 or 1; `Run` (`:91`) runs one by name.
  - `Assets/PlayForge/Tests/Golden/GoldenScenarioTests.cs:19` `GoldenScenarioTests.Scenario` runs each scenario as a
    Test Runner case in the `FarEmerald.PlayForge.Tests.Golden` assembly (SP-18). That assembly compiles only where
    tests are included: in a project's `Assets`, or for a package listed in `testables`.
  - The `PlayForge/Run Golden Tests` menu item is gone (ED-19). In batch mode,
    `-executeMethod FarEmerald.PlayForge.Tests.PlayForgeGoldenTests.RunAll` still works.
  - Changed expectations: S6 is now `S6_CommittedHistoryLandsAfterModifiers` and expects 85 (Decision 4), at
    `Assets/PlayForge/Tests/Editor/PlayForgeGoldenTests.cs:491`. S15 (`:825`) expects 42.5 for its raw delta
    (Decision 9). No scenario is masked as expected-red any more (SP-16).
- **PlayForge Proving Grounds** holds most of the refactor's tests. It is a separate Unity project at
  `C:\Users\17074\Documents\GitHub\PlayForge Proving Grounds`, in its own local git repo. It consumes this repo's
  `Assets/PlayForge` as the local package `com.faremerald.playforge`, so its tests run against the code here. Its tests
  are in `Assets/ProvingGrounds/Tests`:
  - `Framework/ProcessControl`, `Framework/TaskSequence` and `Framework/GAS` hold PlayMode tests by area, run under a
    live ProcessControl. A test that pins a bug is named after its tracker ID, for example
    `PauseTests.PC_11_A_pause_takes_effect_at_once` or
    `AbilityActivationTests.GAS_46_Apply_Effects_reaches_the_target_targeting_found`, so a search for `GAS_46` finds
    it.
  - `Editor` holds the EditMode tests for the editor code and the framework outside play mode, `DomainReloadTests`
    (SP-22) among them.
  - `Golden` runs the 25 golden scenarios on both hubs. `Harness` and `Planted` test the harness itself; every planted
    test must fail. The harness is in `Assets/ProvingGrounds/Harness`.
  - A test for a bug not fixed yet carries `[Category("Red")]` (Decision 8), and `-testCategory "!Red"` is the green
    gate. The project's `README.md` and the plan's Environment section have the commands.

## First read: the newest high-impact spots

Newest first. Each row is a good place to start reading.

| What | IDs | Where | Commit |
|---|---|---|---|
| An attribute set reached twice counts once | GAS-44 | `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeSet.cs:63` | `57ea4ff` |
| Tick On Application alone lands its change | GAS-50 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/EffectSystemComponent.cs:199` | `e40b3b6` |
| Opening abilities wait for the entity's attributes | GAS-49 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Core/AbilitySystemComponent.cs:169` | `999e779` |
| Unequipping takes back only the item's grant | GAS-48 | `Assets/PlayForge/Runtime/PlayForge/ItemSystem/Core/ItemSpecContainer.cs:183` | `27c2325` |
| Each entity keeps its own affiliation | GAS-47 | `Assets/PlayForge/Runtime/PlayForge/GAS/Core/MonoBehaviour/GameplayAbilitySystem.cs:68` | `fdc1886` |
| Targeting's packet resolves to the entity, so Apply Effects finds its target | GAS-46 | `Assets/PlayForge/Runtime/PlayForge/AbilitySystem/Ability/AbilityProxy/Core/AbilityDataPacket.cs:133` | `fe4271c` |
| Help buttons open the package's own docs through one table | ED-16, Decision 18 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeDocs.cs:79` | `67db441` |
| Demo Lit draws the Demos under URP and Built-in | SP-23, Decision 17 | `Assets/PlayForge/Samples~/Demos/Demo/Shaders/DemoLit.shader:42` | `954a11a` |
| Statics clear at the start of a play session | SP-22, PC-26 | `Assets/PlayForge/Runtime/PlayForge/Utils/AttributeRegistry.cs:14` | `86e9e11` |
| Showing an asset never writes its tag; a rename is one Undo | ED-12 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Editors/BasePlayForgeEditor.cs:996` | `4d71636` |
| Delete names what uses the asset and moves it to the trash | ED-8 | `Assets/PlayForge/Editor/CustomPropertyDrawers/Forge/Util/ForgeAssetDeletion.cs:25` | `e89a9f7` |
| The samples live in `Samples~` and import on request | SP-20, Decision 16 | `Assets/PlayForge/package.json:14` | `8d17bc0` |
| A derived refresh runs the post-change pipeline | GAS-27, Decision 14 | `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Core/AttributeSystemComponent.cs:441` | `550d07b` |
| Combined elements add up | GAS-27, Decision 13 | `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/AttributeBlueprint.cs:85` | `5244d99` |
| The hierarchy follows transforms; SceneRoot spawns are GameRoot's children | PC-28, Decision 11 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControl.cs:1477` | `5f6c0eb` |
| Commits land after the modifiers | GAS-15, Decision 4 | `Assets/PlayForge/Runtime/PlayForge/AttributeSystem/Attribute/CachedAttributeValue.cs:421` | `66b33ad` |
| Pause holds a process in place | PC-7, PC-11, PC-12, PC-27, Decision 5 | `Assets/PlayForge/Runtime/PlayForge/Management/GameplayProcess/Core/ProcessControlBlock.cs:180` | `23f52aa`, `666beda` |
