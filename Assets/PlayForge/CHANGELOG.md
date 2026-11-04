# Changelog

All notable changes to PlayForge. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
versions follow [semantic versioning](https://semver.org/spec/v2.0.0.html). The repository's `Docs/REFACTOR_PLAN.md`
tracks each fix below by its ID.

## [Unreleased]

### Added
- Reference pages for the six asset types in `Documentation`. Each inspector section's help button opens its type's
  page, through one table of links in the editor.

### Changed
- PlayForge is licensed under the Unity Asset Store EULA.
- The samples moved to `Samples~`, so Package Manager imports them on request: **Demos** and **Code Examples**. The
  TaskSequence and group process examples no longer compile into the runtime.
- UniTask is pinned to 2.5.11.
- The menu moved under **Window → PlayForge**, and each window's title matches its menu item. The main window is
  **The Forge** everywhere. Run Golden Tests left the menu: the Test Runner runs the golden scenarios.
- Deleting an asset from The Forge names the assets that use it, asks, and moves it to the trash.
- Showing an asset never changes it. A rename stores the name and its asset tag in one undoable step, and a stale
  tag offers Update.
- The Demos render in URP as well as the Built-in pipeline.
- Pausing a process holds it in place: it stops stepping at once, and on resume it picks up its run if it can hold
  one.
- The process hierarchy follows transforms: a process spawned at `SceneRoot` is GameRoot's child.
- A commit lands on the current value as measured: after Add, Multiply and FlatBonus, and before Override.
- A raw delta, one handed to the GAS without an effect, reaches attribute and impact workers.
- A disjoint stops homing shots only; skill shots land as usual.
- A random scaler keeps one roll per effect spec, unless set to roll on every evaluation.
- Attribute elements set to Combine add up, and a set's `UseAverage` averages.
- A derived attribute's refresh runs the post-change pipeline and reaches the frame summary.
- `GameplayAbilitySystem` and `RuntimeGameplayAbilitySystem` forward to one core, so the two hubs behave alike. A
  hub's `AttributeSystem`, `AbilitySystem`, `ItemSystem`, `EffectSystem` and `LevelSystem` are read-only properties,
  public on both. Each hub has what only the other had: `ApplyGameplayEffect(origin, effect)` and
  `GetRelativeLevel()`.
- Applying an effect costs the same however many effects the entity carries. It no longer re-checks every effect's
  requirements or rebuilds the entity's tags: the requirements are checked once, at the entity's next update, and a
  durational effect's tags count from the moment it lands, so its own application workers see them.
- Ending an effect costs the same however many end with it, so a cleanse or a mass expiry costs as many as it ends,
  not their square. Effects ending in the same update leave the entity together before any of their removals run, so
  a removal worker no longer finds another effect that is ending with its own.
- A tick allocates nothing on its way to its commit, and a recompute, a modifier refresh and a same-root tag query
  allocate nothing. The built-in `Tags` are made once, not on every read. `GameplayEffectSpec.GetImpactedAttributes`
  returns a `ReadOnlyListView` and `GameplayEffectImpact.GetContainedEffects` a `ContainedEffects`, which `foreach`
  walks without allocating and which still enumerate as before. `Tag.IsChildOf` compares ordinally.
- A commit allocates nothing, so a frame in which an effect ticks and commits allocates nothing, with or without a
  worker adjusting it as armor does. A `ChangeValue` handed to a callback or a worker is reused once its commit is
  done: read it during the call, or copy its `Value`. A deferred worker's actions may still keep it. `TrackedImpact`
  keeps a total, the last impact and a count, not every impact. `ForgeHelper.SignPolicy` and `SignPolicyExtended`
  take one or two values without allocating.
- A raw delta allocates nothing on its way through the GAS. `GenerateSimple` still makes a new carrier on every call,
  so a raw delta made every frame keeps one, made once with `IAttributeImpactCarrier.GenerateSourceDerivation`. A
  `SourceAttributeImpact` with no root carrier gives the same origin each time it's asked for one.
- A rescale with Base and a lifesteal's heal allocate nothing, and neither does taking an action off an entity's queue.
  The system uses its own `ModifyAttributeAction`s again once the queue has run them, so a listener of
  `OnActionQueued` or `OnActionExecuted` reads an action during the call. `ActionQueue.EnqueueRange` reads a list or
  an array by index, and `RelativeOperationContextImpactWorker.CreateDeferredActions` returns a list the worker
  reuses for its next action, so what it returns is queued at once.
- Tags compare and hash by an ID each is given at run time, as it's made or loaded, so a lookup costs the same however
  long a tag's name is. Nothing new is serialized. Two tags are still equal exactly when their names are, and a tag
  whose `Name` is changed in place takes its new name's ID. `Tag.GetHashCode` no longer returns the name's hash, and
  can differ from one run to the next.
- A stage under WhenAll or RepeatUntilSkipped hands its policy the stage's own token source and makes no second one,
  so a three-stage ability makes about 10 token sources, not 14. A stage under WhenAny, WhenN or a policy of your own
  still makes one, as its policy may cancel what it's handed.
- Sequences and abilities end in the frame their last task does. A task made with `Task(Action)` runs its action at
  once, as `Do` does, not a frame later. A run waits a frame for its maintained stages only when it has some, and
  waiting on a process no longer polls: `ProcessRelay.WhenTerminated` completes in the frame the process ends, and
  the ability proxy and `RunSequenceTask` wait on it. An instant ability ends in the frame it starts, and a nested
  sequence adds no frame; so an instant sequence can end inside `ProcessControl.Register`, and an instant ability
  inside `TryActivateAbility`. A repeating sequence that runs instantly still takes a frame per repeat.
- A frame whose summary has a listener allocates nothing. The `FrameSummarySnapshot` that `OnFrameComplete` hands a
  listener is its entity's own and is refilled each frame, so read it during the call, or copy what you need.
  `FrameSummary.CreateSnapshot` still makes one to keep.
- A task waiting out a time in its sequence, as `DelayTask` and `ChannelAbilityTask` do, allocates nothing a frame.
  Asking whether its sequence was paused took 24 bytes a frame.

### Fixed
- The Attribute inspector's Name, Abbreviation and Description undo, and the inspector edits several attributes at once.
  Every asset inspector's header follows Undo and Redo (ED-12).
- The MonoProcess inspector edits several selected processes at once (ED-12).
- The Effect inspector's Description and Link Mode undo, and its Level Source section follows an Undo (ED-12).
- The Item inspector's Description and Link Mode undo, and its Level Source and Leveling sections follow an Undo
  (ED-12).
- Every field of the Entity inspector undoes: Description, Starting and Max Level, Activation Policy, the three Allow
  Duplicate toggles and Link Mode (ED-12).
- The Attribute Set inspector's Description and Link Mode undo, and its Level Source and Leveling sections follow an
  Undo (ED-12).
- The Ability inspector's Description, Activation Policy, Activate Immediately and Link Mode undo (ED-12).
- Runtime correctness: 96 fixes across ProcessControl, TaskSequence and the GAS core, among them exception isolation,
  queued state changes, pause, effect stacking and ticking, commit order, worker scheduling and targeting.
- A fresh project imports the package with no warning and no missing type, and the samples import with no missing
  script, reference or type.
- BuildVerification builds an empty scene of its own when the build settings enable none, instead of the first scene
  the project lists.
- PlayForge works with domain reloading off (Enter Play Mode Options): each play session starts clean.
- The Forge: its settings save, reset and refresh as labelled; Analysis hides the groups it can't analyse yet and says
  how much a list leaves out; the tag display settings apply everywhere.
- An entity's affiliation is its own: the sides it registers with no longer change its Entity asset, and with it every
  entity that shares the asset.
- Unequipping an item takes back the ability that item granted, not another copy of it.
- Abilities that activate immediately wait until the entity's attributes, items and tags exist, so an opening ability
  with a cost activates.
- An effect that only ticks on application lands its change.
- An attribute set that includes itself, directly or through another set, no longer recurses forever, and a set reached
  by two paths counts once.
- A runtime GAS applies its entity's affiliation as tags, as a GameObject's does, and one made from an Entity asset
  keeps the asset's Local Data.
- The GAS Debugger names runtime entities. It showed each as "unknown".
- A sequence that repeats no longer grows its packet's path for as long as it runs. Each part a run or a stage adds to
  the path comes off as it ends, and a failed run still logs the path to where it failed. The parts are formatted
  once, and the LINQ left on an ability's activation is gone.
- An ability asked for again from inside its own activation, by its own callbacks or by another ability's that it
  started, starts in the next frame. It started inside the call, so a loop of instant activations nested until the
  stack overflowed. `TryActivateAbility` returns true for such a request. Activation is otherwise as before: the
  ability runs inside the call until one of its tasks waits.
- A runtime entity's abilities end when it does. A runtime process now ends the processes it handles as it ends, as a
  Mono process does, so ending a runtime entity, however it's ended, ends its running abilities, critical or not.
  They ran on for as long as their stages lasted. An entity that has ended takes no more activations:
  `TryActivateAbility` returns false at once, where a destroyed Mono entity threw.
- Interrupting every running ability at once ends each of them, whether through `InjectAll`, `Inject` with a policy,
  or turning `Enabled` off. Once the first had ended, it threw, and the rest ran on.

### Removed
- QuickOutline, replaced by the Demo's own outline.
- The test processes `TestAOEMonoProcess` and `TestMultiProcess`, and the unused `BootstrapperEditor`.
- Two settings nothing read: an entity's **Cap At Max**, since its level always stays between Starting Level and Max
  Level, and an item's **Allow Duplicates**, since the entity's **Allow Duplicate Items** decides. Code that set
  either field no longer compiles; delete the assignment.
- From the GAS hubs, what nothing used: `GameplayAbilitySystem`'s `Relays` field and `HandleDestructionInternally`
  hook, and each hub's protected `localData` field. A subclass reads Local Data through `GetLocalDataStructure()`.
- `PooledMonoProcessInstantiator`, a stub that gave back no process and kept a process's object when it ended, so it
  leaked. Nothing used it. To pool processes, extend `AbstractMonoProcessInstantiator`.

## [0.1.0] - 2026-10-30

- First version as a UPM package.
