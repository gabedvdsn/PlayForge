# Changelog

All notable changes to PlayForge. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
versions follow [semantic versioning](https://semver.org/spec/v2.0.0.html). The repository's `Docs/REFACTOR_PLAN.md`
tracks each fix below by its ID.

## [Unreleased]

### Changed
- PlayForge is licensed under the Unity Asset Store EULA.
- The samples moved to `Samples~`, so Package Manager imports them on request: **Demos** and **Code Examples**. The
  TaskSequence and group process examples no longer compile into the runtime.
- UniTask is pinned to 2.5.11.
- Pausing a process holds it in place: it stops stepping at once, and on resume it picks up its run if it can hold
  one.
- The process hierarchy follows transforms: a process spawned at `SceneRoot` is GameRoot's child.
- A commit lands on the current value as measured: after Add, Multiply and FlatBonus, and before Override.
- A raw delta, one handed to the GAS without an effect, reaches attribute and impact workers.
- A disjoint stops homing shots only; skill shots land as usual.
- A random scaler keeps one roll per effect spec, unless set to roll on every evaluation.
- Attribute elements set to Combine add up, and a set's `UseAverage` averages.
- A derived attribute's refresh runs the post-change pipeline and reaches the frame summary.

### Fixed
- Runtime correctness: 96 fixes across ProcessControl, TaskSequence and the GAS core, among them exception isolation,
  queued state changes, pause, effect stacking and ticking, commit order, worker scheduling and targeting.
- A fresh project imports the package with no warning and no missing type, and the samples import with no missing
  script, reference or type.
- BuildVerification builds an empty scene of its own when the build settings enable none, instead of the first scene
  the project lists.

### Removed
- QuickOutline, replaced by the Demo's own outline.
- The test processes `TestAOEMonoProcess` and `TestMultiProcess`, and the unused `BootstrapperEditor`.

## [0.1.0] - 2026-10-30

- First version as a UPM package.
