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

### Fixed
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

### Removed
- QuickOutline, replaced by the Demo's own outline.
- The test processes `TestAOEMonoProcess` and `TestMultiProcess`, and the unused `BootstrapperEditor`.
- Two settings nothing read: an entity's **Cap At Max**, since its level always stays between Starting Level and Max
  Level, and an item's **Allow Duplicates**, since the entity's **Allow Duplicate Items** decides. Code that set
  either field no longer compiles; delete the assignment.
- From the GAS hubs, what nothing used: `GameplayAbilitySystem`'s `Relays` field and `HandleDestructionInternally`
  hook, and each hub's protected `localData` field. A subclass reads Local Data through `GetLocalDataStructure()`.

## [0.1.0] - 2026-10-30

- First version as a UPM package.
