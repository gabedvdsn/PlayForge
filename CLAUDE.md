# PlayForge

PlayForge is a Unity 6 gameplay framework modelled on Unreal's Gameplay Ability System. It uses the namespace
`FarEmerald.PlayForge` and has three layers:

- **ProcessControl** (`Runtime/PlayForge/Management`) schedules every process from Unity's player loop, plus
  UniTask.
- **TaskSequence** (`Runtime/PlayForge/Core/TaskSequence`) runs every ability as a staged workflow.
- **The GAS core** (`GAS`, `AbilitySystem`, `AttributeSystem`, `GameplayEffect`, `Tag`, `ItemSystem`,
  `Workers`) owns attributes, effects, abilities, tags and items. There is one hub per entity, either
  `GameplayAbilitySystem` (MonoBehaviour) or `RuntimeGameplayAbilitySystem`.

The package lives in `Assets/PlayForge`, with `Runtime/`, `Editor/`, `Tests/`, `Samples/` and `Documentation/`.

## Start here

**A refactor is in progress.** Read `Docs/REFACTOR_PLAN.md` before changing anything. It holds:
- the roadmap and its phases;
- the bug tracker, with IDs such as `PC-3`, `TS-A`, `GAS-7`, `ED-4` and `SP-1`;
- the environment and command details;
- the open decisions that need the user's answer.

The full review behind it is https://claude.ai/artifact/1GsrCxW3Wff2KvXM6fnai9.

## Build and test

- **Unity:** 6000.0.33f1.
- **Compile check:** MSBuild from VS 2022 Build Tools; there is no dotnet SDK. Build
  `FarEmerald.PlayForge.Editor.csproj` (it references Runtime) and `FarEmerald.PlayForge.Tests.csproj` (exact
  commands in the plan). Unity regenerates the csproj files only
  while the editor is open, so new files may need a manual `<Compile>` entry until then.
- **Golden tests:** the Test Runner's EditMode tab (`FarEmerald.PlayForge.Tests.Golden`), or
  `-executeMethod FarEmerald.PlayForge.Tests.PlayForgeGoldenTests.RunAll` in batch mode with the editor closed.
  The exact commands are in `Docs/REFACTOR_PLAN.md`.
- After any runtime change, run the golden tests and record the result in the plan's Log.

## Working rules

- **Write a failing test first,** named after the tracker ID, then fix. Update the ID's status in
  `Docs/REFACTOR_PLAN.md` in the same commit.
- **Work on `main`** (the plan's Decision 19); open no new phase branches. Cite tracker IDs in commit messages.
  Push `main` to origin after each commit whose checks pass, fast-forward only. Never force-push; if a push is
  rejected, stop and report it.
- **Never read or edit `.claude/worktrees/`.** It holds stale copies, at the repo root and under
  `Assets/PlayForge/.claude/`.
- **Arcade Fighter** (`C:\Users\17074\Documents\GitHub\Arcade Fighter`) carries a patched copy in
  `Assets/Internal/PlayForge`, logged in `Docs/PlayForge-Log.md`. Phase 0 ports those fixes here.

## Code conventions

- Prefer inspector-configurable fields over hard-coded lifecycle or timing values.
- Prefer virtual no-op hooks over abstract methods.
- Extend base classes directly (for example `AbstractMonoProcess`) when an intermediate class's patterns
  don't apply.
- Log through `ForgeLog`, which is level-gated. ProcessControl and sequences have their own log gates.

## Gotchas

- **Impact entries are derived on every `Generate()` and never serialized.** Do not serialize them until a
  multi-entry drawer exists.
- **`TagCache.GetWeight` returns −1 for an absent tag,** not 0.
- **Both GAS hubs forward to one internal `GASCore`** (GAS-S1). It owns the subsystems (the effect shelf is its
  `EffectSystemComponent`), the tag compile, level, Local Data, affiliation, end of frame and death, so put GAS
  behaviour there. A hub keeps only how it is built (the Mono hub at registration, a runtime GAS in its constructor),
  where its entity is, and how it goes away (`TerminateImmediate` or `Destroy` for Mono, a queued `Terminate` for a
  runtime GAS).
- **A GAS must step in LateUpdate** for its end of frame to run (GAS-1).
- **Lifecycles** are SelfTerminating, RunThenWait, RequiresControl and Synchronous. There is no StepOnly.
