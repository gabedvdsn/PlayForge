# PlayForge — Process Control, Sequences & Tasks

## Process Control

ProcessControl is a singleton MonoBehaviour that manages the lifecycle of gameplay processes. It hooks into Unity's update methods (Update, FixedUpdate, LateUpdate) and provides structured state management for any gameplay operation that needs to run over time.

### Lifecycles

Every process has a lifecycle that determines how it starts, runs, and finishes:

| Lifecycle | Starts As | After Run Completes | Async? | Use Case |
|---|---|---|---|---|
| **SelfTerminating** | Running | Terminated | Yes | One-shot operations: spawn projectile, play effect, apply damage |
| **RunThenWait** | Running | Waiting | Yes | Repeatable operations: ability execution, dialogue sequences |
| **RequiresControl** | Waiting | Waiting | Yes | Externally driven: player-triggered abilities, event-gated processes |
| **Synchronous** | Running | Waiting | **No** | Persistent update-driven logic: health bars, AI steering, buff ticking |

### Process Types

- **MonoProcess** (`SynchronousMonoProcess`, `LazyMonoProcess`, etc.) — MonoBehaviour-based. Can be prefabs, exist in scenes, and use Unity's component model. Instantiated and destroyed automatically by ProcessControl.
- **RuntimeProcess** (`SynchronousRuntimeProcess`, `LazyRuntimeProcess`, etc.) — Plain C# classes. No GameObject overhead. Ideal for logic-only processes.

### Registration & Control

```csharp
// Register a process
ProcessControl.Instance.Register(myProcess, data, out ProcessRelay relay);

// Control via ProcessRelay
relay.Pause();       // Move to Waiting (removes from update loop)
relay.Unpause();     // Move to Running (re-adds to update loop)
relay.Terminate();   // Graceful termination and cleanup
```

### When to Use Synchronous vs Async

**Use Synchronous** when your process:
- Only needs per-frame update logic (no awaiting)
- Is long-lived (health regen, AI behavior, environmental effects)
- Should avoid async allocation overhead
- Doesn't need to "wait" for external events

**Use Async** (SelfTerminating, RunThenWait, RequiresControl) when your process:
- Has a natural async workflow (move to target, wait for animation, apply effect)
- Needs to compose multiple awaitable steps
- Benefits from UniTask's cancellation and flow control
- Is short-lived or has a clear completion condition

---

## Task Sequences

Task Sequences are composable, reusable async workflows built from smaller tasks. They provide a fluent builder API for choreographing complex gameplay — ability executions, cutscenes, tutorials, combo attacks, and more.

### Core Concepts

- **Sequence** — An ordered list of stages executed sequentially
- **Stage** — A group of tasks within a sequence. Tasks within a stage can run per a policy (WhenAll, WhenAny, WhenN)
- **Task** — A single unit of work (async or sync)
- **Chain** — Multiple sequences executed back-to-back with shared data
- **Injection** — Runtime flow control (skip, interrupt, jump) applied via conditions or manually
- **Condition** — A predicate checked each frame that auto-applies an injection when met

### Building a Sequence

```csharp
var seq = TaskSequenceBuilder.Create("Fire Projectile")
    .WithCriticalFlag(true)
    .WithMaxDuration(5f, InterruptSequenceInjection.Instance)

    .Stage(s => s
        .WithName("Startup")
        .Task(async (data, token) =>
        {
            var owner = data.GetPrimary<Transform>(Tags.OWNER);
            await Seq.PlayAnimationState(owner.GetComponent<Animator>(), "Cast", token);
        })
    )

    .Stage(s => s
        .WithName("Launch")
        .Task(async (data, token) =>
        {
            var projectile = data.GetPrimary<Transform>(Tags.PROJECTILE);
            var target = data.GetPrimary<Transform>(Tags.TARGET);
            await Seq.ArcTowards(projectile, target, 20f, 3f, token);
        })
    )

    .Stage(s => s
        .WithName("Impact")
        .Task(data => ApplyDamage(data))
    )

    .BuildSequence();

// Register with ProcessControl
TaskSequenceProcess.Register(seq, data);
```

### Flow Control

```csharp
// From inside a task via the data packet
data.SkipStage();
data.JumpToStage("Impact");
data.Interrupt();
data.BreakStageRepeat();

// Auto-injection via conditions
.InterruptWhen(data => data.ElapsedTime > 10f)
.SkipStageWhen(data => targetIsDead)

// Stage-level conditions
.Stage(s => s
    .StopRepeatWhen(data => chargeLevel >= 1f)
    .WithTimeout(3f, SkipStageInjection.Instance)
)
```

### Async Task Library (`Seq` / `SequenceTaskLibrary`)

The async library provides awaitable building blocks for use inside `.Task()` lambdas:

**Position (timed)**
- `Seq.MoveTo`, `Seq.MoveBy`, `Seq.MoveLocalTo`, `Seq.MoveAlongPath`, `Seq.ArcTo`

**Position (tracking — dynamic targets)**
- `Seq.MoveTowards` — Follow a moving transform at a fixed speed
- `Seq.ArcTowards` — Arc toward a moving target with parabolic height
- `Seq.Orbit` — Orbit around a transform at a radius and angular speed
- `Seq.Dash` — Directional dash/knockback/lunge
- `Seq.MoveAlongPathToTarget` — Path with a tracking final waypoint

**Rotation**
- `Seq.RotateTo`, `Seq.RotateBy`, `Seq.LookAt`
- `Seq.LookAtTracking` — Continuously face a moving target

**Scale**
- `Seq.ScaleTo`, `Seq.PunchScale`

**Composite**
- `Seq.TransformTo` — Position + Rotation + Scale simultaneously

**Visual**
- `Seq.FadeAlpha` (CanvasGroup, SpriteRenderer), `Seq.ColorTo`, `Seq.MaterialColorTo`, `Seq.MaterialFloatTo`

**Audio**
- `Seq.PlayAndWait`, `Seq.FadeVolume`

**Animator**
- `Seq.PlayAnimationState`, `Seq.LerpAnimFloat`

**Physics**
- `Seq.ArcForce` — Rigidbody parabolic arc using physics forces

**UI Toolkit**
- `Seq.SlideElement`, `Seq.FadeElement`, `Seq.ScaleElement`, `Seq.PunchScaleElement`
- `Seq.ColorElement`, `Seq.TypeText`, `Seq.TypeTextSkippable`
- `Seq.WidthTo`, `Seq.HeightTo`

**Generic Lerp**
- `Seq.Lerp`, `Seq.LerpFloat`, `Seq.LerpVector3`, `Seq.LerpColor`

**Timing**
- `Seq.Yield`, `Seq.WaitFrames`

---

## Synchronous Sequences

Synchronous Sequences are the frame-driven counterpart to async Task Sequences. They use the same stage-based composition model but execute entirely within a process's `WhenUpdate` — no async, no UniTask, no allocation overhead.

### When to Use Sync vs Async Sequences

| | Async Sequences | Sync Sequences |
|---|---|---|
| **Driven by** | UniTask async/await | Per-frame `Step()` calls |
| **Process type** | SelfTerminating, RunThenWait | Synchronous |
| **Flow control** | Injection system, conditions | Manual skip/jump/interrupt |
| **Best for** | Finite workflows with clear start/end | Persistent or long-lived update logic |
| **Overhead** | Async state machine, CTS allocation | Zero — just method calls |
| **Cancellation** | CancellationToken | Runner.Interrupt() |
| **Composability** | Stages, chains, conditions, branches | Stages with WhenAll/WhenAny |

**Use async sequences** for operations that have a natural completion: ability execution, cutscenes, projectile flights, timed effects.

**Use sync sequences** for operations that run indefinitely or are purely update-driven: AI behavior loops, environmental effects, persistent UI animations, stat regeneration, buff ticking.

### Building a Sync Sequence

```csharp
var runner = SyncSequenceBuilder.Create("HealthRegen")
    .WithRepeat(true)
    .Stage(s => s
        .Task(SyncSeq.Delay(1f))
    )
    .Stage(s => s
        .Do(data =>
        {
            var health = data.GetPrimary<Attribute>(Tags.HEALTH);
            health.Modify(5f);
        })
    )
    .Build();
```

### Using in a Synchronous Process

```csharp
public class HealthRegenProcess : SynchronousMonoProcess
{
    private SyncSequenceRunner _runner;

    public override void WhenInitialize(ProcessRelay relay)
    {
        base.WhenInitialize(relay);
        _runner = SyncSequenceBuilder.Create("HealthRegen")
            .WithRepeat(true)
            .Stage(s => s.Task(SyncSeq.Delay(1f)))
            .Stage(s => s.Do(_ => HealPlayer(5)))
            .Build();
    }

    public override void WhenUpdate(ProcessRelay relay)
    {
        _runner.Step(regData as SequenceDataPacket ?? SequenceDataPacket.RootDefault(), Time.deltaTime);
    }
}
```

### Sync Task Library (`SyncSeq` / `SyncSequenceTaskLibrary`)

The sync library returns `ISyncSequenceTask` instances for use with `SyncSequenceBuilder`:

**Position (timed)**
- `SyncSeq.MoveTo`, `SyncSeq.MoveBy`, `SyncSeq.MoveLocalTo`, `SyncSeq.ArcTo`, `SyncSeq.Dash`

**Position (tracking)**
- `SyncSeq.MoveTowards` — Follow a moving transform at speed
- `SyncSeq.ArcTowards` — Arc toward a moving target
- `SyncSeq.Orbit` — Orbit around a center transform

**Rotation**
- `SyncSeq.RotateTo`, `SyncSeq.RotateBy`, `SyncSeq.LookAt`
- `SyncSeq.LookAtTracking` — Continuously face a moving target

**Scale**
- `SyncSeq.ScaleTo`, `SyncSeq.PunchScale`

**Composite**
- `SyncSeq.TransformTo`

**Visual**
- `SyncSeq.FadeAlpha` (CanvasGroup, SpriteRenderer), `SyncSeq.ColorTo`
- `SyncSeq.MaterialColorTo`, `SyncSeq.MaterialFloatTo`

**Audio**
- `SyncSeq.PlayAndWait`, `SyncSeq.FadeVolume`

**Animator**
- `SyncSeq.PlayAnimationState`, `SyncSeq.LerpAnimFloat`

**UI Toolkit**
- `SyncSeq.SlideElement`, `SyncSeq.FadeElement`, `SyncSeq.ScaleElement`, `SyncSeq.ColorElement`

**Generic Lerp**
- `SyncSeq.Lerp`, `SyncSeq.LerpFloat`, `SyncSeq.LerpVector3`, `SyncSeq.LerpColor`

**Timing**
- `SyncSeq.Delay`, `SyncSeq.WaitUntil`, `SyncSeq.WaitWhile`, `SyncSeq.WaitFrames`

---

## Architecture Summary

```
ProcessControl (Singleton MonoBehaviour)
├── Update / FixedUpdate / LateUpdate
│   └── Steps all Running processes per their StepTiming
├── Manages state transitions: Created → Running ↔ Waiting → Terminated
└── Lifecycle determines async behavior and default transitions

Async Sequences (TaskSequence + TaskSequenceProcess)
├── Fluent builder API (TaskSequenceBuilder)
├── Stage → Task composition with policies (WhenAll, WhenAny, WhenN)
├── Injection system for runtime flow control
├── Condition auto-triggers checked each Update
├── Chain support for multi-sequence workflows
└── SequenceTaskLibrary (Seq) — awaitable building blocks

Sync Sequences (SyncSequenceRunner)
├── Fluent builder API (SyncSequenceBuilder)
├── Stage → Task composition with policies (WhenAll, WhenAny)
├── Manual flow control (Skip, Jump, Interrupt, Reset)
├── Repeat support for looping behaviors
└── SyncSequenceTaskLibrary (SyncSeq) — per-frame building blocks
```

The two sequence systems are intentionally separate. Async sequences use UniTask and CancellationToken for complex flow control. Sync sequences use simple per-frame stepping for zero-overhead persistent logic. Choose the right tool for the job — they complement each other.
