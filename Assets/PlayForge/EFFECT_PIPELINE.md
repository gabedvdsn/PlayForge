# PlayForge — Gameplay Effect & Attribute Pipeline

How a `GameplayEffect` becomes an attribute change, stays correct while it lives, and reverses
when it dies. This is the authoritative description of the runtime pipeline; if code and doc
disagree, one of them has a bug — file it either way.

All paths are relative to `Assets/PlayForge/Runtime/PlayForge/`.

---

## 1. Cast of characters

| Concept | Type | Role |
|---|---|---|
| Carrier | `IAttributeImpactCarrier` | Anything that impacts an attribute. Identity (source, origin, retention group, impact types) + an impacter list. |
| Applicable carrier | `ICarrierApplication : IAttributeImpactCarrier` | A carrier with a *lifecycle*: duration policy, container generation, worker phases, contained carriers, `IsSustainedModifier()`. |
| Effect spec | `GameplayEffectSpec` | THE applicable carrier. Runtime instance of a `GameplayEffect` asset: origin/source/target, `TrackedImpact`, captured attributes, cached impacter list, back-reference to its shelf container (`RuntimeContainer`). |
| Synthetic carrier | `SourceAttributeImpact` | Carrier for hand-rolled deltas (init pulses, auto-scale propagation, third-party writes). Built via `FromAdditiveDelta` with `ConstantImpacter`s. |
| Impacter | `IAttributeImpacter` | One magnitude-evaluation unit: `{ Operation, TargetSlot, SortOrder, MultiplyBehaviour, Evaluate(carrier) }`. A spec exposes exactly one `ScalerImpacter` built from its `ImpactSpecification`. |
| Cached value | `CachedAttributeValue` | Per-attribute state: `ActiveValue`, retained carriers (`retainedGroups`), one-shot history (`committedOneShotDelta`), blueprint root. |
| Container | `AbstractEffectContainer` | Shelf bookkeeping for one durational/infinite spec: duration, tick timers, stacks, removal reversal. |
| Shelf | `List<AbstractEffectContainer> EffectShelf` | Per-GAS list of live durational effects. Ticked every update. |

Key files:

- `AttributeSystem/Attribute/CachedAttributeValue.cs`
- `AttributeSystem/Core/AttributeSystemComponent.cs`
- `GAS/Core/Runtime/RuntimeGameplayAbilitySystem.cs` ⟷ `GAS/Core/MonoBehaviour/GameplayAbilitySystem.cs` (**twins — keep in sync, always edit both**)
- `GameplayEffect/Core/GameplayEffectSpec.cs`, `ICarrierApplication.cs`, `IAttributeImpactCarrier.cs`
- `GameplayEffect/Core/ScalerImpacter.cs`, `ConstantImpacter.cs`, `IAttributeImpacter.cs`
- `GameplayEffect/Core/Definition/GameplayEffectDuration.cs`, `GameplayEffectImpact.cs`
- `GameplayEffect/Core/GameplayShelfContainer/AbstractEffectContainer.cs` + stacking subclasses

---

## 2. The two-mode model (the crux)

Every applicable carrier interacts with an attribute in **exactly one** of two modes, decided by
one predicate:

```
IsSustainedModifier() == DurationPolicy is Durational/Infinite
                         AND NOT (TickOnApplication || EnablePeriodicTicks)
```

### Mode A — sustained modifier (`IsSustainedModifier() == true`)

*"+50% damage while equipped", "+100 max HP buff".*

- Registered into `CachedAttributeValue.retainedGroups` on apply.
- Its impacters are **re-evaluated on every `Recompute()`** — the value is *derived*, never stored.
- Reversal is automatic: remove the carrier, recompute, contribution is gone.
- A durational effect with no tick events has no other way to deliver impact, so retention is
  exactly what it means to be a sustained modifier.

### Mode B — discrete impact (`IsSustainedModifier() == false`)

*Instant damage, every tick of a DoT/HoT.*

- Instant effects commit on apply; tick-delivered effects commit **once per execute tick** — the
  apply event itself carries nothing.
- Each commit goes through `ApplyOneShotImpacters`, which mutates `ActiveValue` and records the
  **post-bounds** delta into `committedOneShotDelta` (permanent history).
- Reversal is opt-in: `ReverseImpactOnRemoval` + `TrackedImpact` rollback on container removal
  (see §7). Without it, Mode B history is permanent by design.

**Neither "touches Base" nor "wants reversal" influences the mode.** Base one-shots survive
recomputes via `committedOneShotDelta.BaseValue`; reversal is a removal-time concern.

> ⚠ `TickOnApplication` makes a durational effect Mode B. Its serialized default is `false`
> so that a plain durational effect is a sustained modifier out of the box. Enabling it on a
> buff-style effect makes the impact **permanent** unless `ReverseImpactOnRemoval` is also set.

---

## 3. Attribute state anatomy (`CachedAttributeValue`)

```
ActiveValue  = clamp+round(
                   Blueprint.GetActiveValue(cache)        ← root: re-derived fresh every time
                 + retained-impacter aggregation           ← Mode A, op-precedence order
                 + committedOneShotDelta )                 ← Mode B history, late additive layer
```

- **Root**: `AttributeBlueprint.GetActiveValue(cache)` re-runs the element's cached scalers
  (Current and Base sides independently, `LinkCurrentToBase` respected), so attributes derived
  from other attributes always read their sources' *current* values. Blueprint-less (generic)
  attributes use their construction-time seed instead.
- **`retainedGroups`**: `Dictionary<Tag, RetainedGroup>` keyed by retention-group tag
  (`GameplayEffect.Definition.RetentionGroup`; synthetic carriers use `Tags.IgnoreRetention`).
  Plain storage in apply order. No "alive" flags, no zombie state — presence == contribution.
  Lifecycle owners MUST call `RemoveCarrier` when contribution should end.
- **`committedOneShotDelta`**: the running sum of every post-bounds Mode B delta. Applied as a
  late additive layer so one-shot history (damage taken, heals received) is **not** retroactively
  scaled when sustained modifiers come and go around it.
- **`DirtyValue`**: diagnostic snapshot after the Add pass, before Multiply/FlatBonus/Override.

### 3.1 `Recompute(cache)` operation order

1. Root from blueprint (or generic seed).
2. **Add** — additive impacters, per target slot (Current / Base / CurrentAndBase).
3. **Multiply** — two coexisting pools:
   - `Multiplicative` impacters compound (`×1.5, ×1.5 → ×2.25`);
   - `AdditiveBonus` impacters pool `(eval − 1)` and apply one final `×(1 + Σ)` (`+50%, +50% → ×2.0`).
4. **FlatBonus** — post-multiplication additive.
5. **Override** — per-slot, lowest `SortOrder` wins; a `CurrentAndBase` override competes in both slots.
6. `+ committedOneShotDelta`.
7. `ApplyBounds()` (element's overflow clamp) → `ApplyRounding()` (element's rounding policy).

### 3.2 Entry points

| Method | Caller | Behavior |
|---|---|---|
| `ApplyCarrier` | `ModifyAttribute` (apply path) | Mode A → retain (same-instance re-apply is an idempotent refresh, not a stack) + `Recompute`. Mode B instant/synthetic → `ApplyOneShotImpacters` now. Mode B durational tick-delivered → **no-op** (ticks own every commit). |
| `TickCarrier` | `TickAttribute` (tick path) | Always `ApplyOneShotImpacters` (defensive skip if the carrier is somehow retained). |
| `RemoveCarrier` | `RemoveAttributeDerivation` | Drop from retained group + `Recompute`. Reversal of retained contribution is implicit. |
| `RollbackCommittedDelta` | container removal (§7) | Subtract a tracked total from `committedOneShotDelta` + `Recompute`. |

### 3.3 Idempotency invariant

`Recompute` must reproduce `ActiveValue` exactly from `root + retained + committedOneShotDelta`
at any time, unprompted. This holds because `ApplyOneShotImpacters` records **post-bounds**
deltas: if a +50 heal clamps to +30, history records +30. Any code that mutates `ActiveValue`
without going through `ApplyOneShotImpacters`/`Recompute` breaks this invariant.

---

## 4. Apply path

`ApplyGameplayEffect(spec)` — both GAS twins:

```
ValidateEffectApplicationRequirements (affiliation + source/target tag requirements)
│
├── Instant ──► ApplyInstantGameplayEffect
│     1. add GrantedTags (transient, for the duration of the commit)
│     2. RunWorkerApplication
│     3. ModifyAttribute(spec, Δ0) ──► CommitWithCarrier (apply path)   [Mode B commit]
│     4. RunWorkerRemoval
│     5. remove GrantedTags
│     6. HandleGameplayEffects
│
└── Durational / Infinite ──► ApplyDurationalGameplayEffect
      1. DefinesAttribute gate (null attribute allowed — pure tag-grant effects are legal)
      2. TryHandleExistingDurationalGameplayEffect (§6) — returns true if absorbed
      3. GenerateContainer → EffectShelf.Add
         (container ctor sets spec.RuntimeContainer and applies duration/tick math)
      4. IF IsSustainedModifier(): ModifyAttribute(spec, Δ0)            [Mode A register]
      5. RunWorkerApplication
      6. IF ongoing && TickOnApplication: ApplyTickOnApplication        [Mode B first commits]
│
then (both branches):
  HandleGameplayEffects → Callbacks.EffectApplied → contained OnApply effects
  (each contained effect generates a fresh spec with THIS spec's origin → recursion)
```

The `Δ0` (`SourcedModifiedAttributeValue(spec, 0f, 0f)`) is transport only — magnitudes are
evaluated freshly by the impacter pipeline inside the attribute system, never precomputed.

### 4.1 The commit pipeline (`AttributeSystemComponent.CommitWithCarrier`)

Both `ModifyAttribute` (apply path) and `TickAttribute` (tick path) funnel here; they differ
*only* in the PHASE 2 dispatch (`ApplyCarrier` vs `TickCarrier`). Null attribute → early out.

```
PHASE 1  Callbacks.AttributePreChange → pre-change workers
PHASE 2  resolve carrier:
           • carrier has impacters → use as-is
           • no impacters (legacy delta callsite: init pulse, EnforceScaling, wrappers)
             → synthesize SourceAttributeImpact.FromAdditiveDelta(ConstantImpacters)
         dispatch: isTick ? TickCarrier : ApplyCarrier
         change ← ActiveValue − holdValue
         EnforceScaling (AutoScaleWithBase: enqueues a deferred proportional Current
         adjustment through a synthetic carrier — NOT synchronous)
PHASE 3  post-change workers
PHASE 4  change ← ActiveValue − holdValue (re-measured)
         Callbacks.AttributePostChange
         ImpactData.Generate → RecordFrameImpactReceived (target side)
         → source.AbilitySystem.ProvideFrameImpactDealt   ← this is where spec.TrackImpact
           accumulates TrackedImpact (source-side workers + callbacks run here too)
         Callbacks.AttributeChanged  ← triggers regulation refresh (§8)
```

**Why `TickAttribute` exists**: the apply path's retention rule treats a re-applied retained
instance as an idempotent refresh. Ticks of a spec must commit as one-shots regardless, so the
tick path bypasses retention/idempotency entirely. Without it, every tick after the first would
collapse into a no-op refresh.

---

## 5. Tick path

`WhenUpdate()` → `TickEffectShelf()` → (if `needsCleaning`) `ClearFinishedEffects()`.

`TickEffectShelf` — per container with `EnablePeriodicTicks`:

```
deltaTime  = DurationSpecification.GetDeltaTime(spec)      (optionally scaler-warped)
TickPeriodic(deltaTime, out executeTicks)                  (timer advance; stacking containers
                                                            may yield >1, e.g. per-stack ticks)
executeTicks = GetExecuteTicks(spec, executeTicks)          (additional-execute-ticks operation)
RunWorkerTick(ctx with executeTicks)                        (runs EVERY frame, even 0 ticks)
IF executeTicks > 0 && Ongoing:
    Callbacks.EffectTick
    executeTicks × ApplyDurationGameplayEffectAsInstant(container)
then: durational containers decrement DurationRemaining; expired → MarkFinished
```

`ApplyDurationGameplayEffectAsInstant(container)` — the single execute-tick:

```
TickAttribute(spec, Δ0) ──► CommitWithCarrier(isTick) ──► TickCarrier ──► one-shot commit
contained OnTick effects applied (fresh specs, recursive ApplyGameplayEffect)
```

No worker phase runs inside the execute-tick — tick workers run **once per batch** in the two
callers (`TickEffectShelf` for periodic ticks, `ApplyTickOnApplication` for apply-time ticks).

`ApplyTickOnApplication(container)`:

```
executeTicks = GetExecuteTicks(spec, container.InstantExecuteTicks)   ← evaluated ONCE
                (InstantExecuteTicks: 1 for non-stacking, Stacks for stacking containers)
RunWorkerTick(ctx with executeTicks)
executeTicks × ApplyDurationGameplayEffectAsInstant(container)
```

### 5.1 Loop-safety invariants (do not undo these)

- `TickEffectShelf` iterates **by index over the frame-start count**: executing a tick can apply
  contained effects that `EffectShelf.Add` mid-loop. Containers are never *removed* from the
  shelf during the pass (removal defers through `FinishedEffects`), so indices are stable; late
  additions start ticking next frame.
- Containers already in `FinishedEffects` are skipped — a container marked for removal earlier
  in the frame must not tick again.

---

## 6. Re-application (`TryHandleExistingDurationalGameplayEffect`)

Runs when a spec's effect asset (matched by `Tags.AssetTag`) already has container(s) on the
shelf. Returns `true` if the re-application was absorbed (caller stops), `false` if the caller
should proceed with a fresh application.

| `ReApplicationPolicy` | What happens |
|---|---|
| `DoNothing` | Absorbed; only the interaction (below) runs. |
| `ReplaceOldContainer` | For each old container: `ReverseImpact()` (mode-aware — ends the old spec's contribution exactly like removal, §7), `ReplaceValuesWith(newContainer)` (adopts the **new spec** + duration values; accumulated stack count preserved), then re-registers the new spec if it's a sustained modifier. |
| `AppendNewContainer` | Not absorbed → fresh container, distinct spec instance, genuine co-existing retained entry (stacking by coexistence). |
| `StackExistingContainers` | `FindStackingContainer` picks the target (stacking configs, priority-ordered); `Stack(n)` with `n = GetStackAmount(...)`; then a Δ0 `ModifyAttribute` refresh so a retained spec **recomputes with the new stack count**. If no container found → falls back to fresh application. |

Then, **only when absorbed** (`flag == true`), the `ReApplicationInteraction` runs:
`RefreshContainerDuration` / `ExtendContainerDuration` on each container, each re-firing
`ApplyTickOnApplication` if the (possibly new) spec has `TickOnApplication`. It deliberately does
NOT run on the append path — the fresh application will tick on its own; running both would
double-commit.

### 6.1 Stacks → magnitude

`ScalerImpacter.Evaluate(carrier)` is container-aware: when the carrier is a
`GameplayEffectSpec` whose `RuntimeContainer` is an `AbstractStackingEffectContainer`, magnitude
evaluation routes through `ScalerMagnitudeOperation.Evaluate(container)` →
`AbstractScaler.Evaluate(container)` (default falls back to spec-based evaluation; stack-aware
scalers override it). This applies to **both** the retained Recompute path and per-tick commits —
authors opt in by using a stack-reading scaler.

---

## 7. Removal path

Containers are *marked* (never removed inline) via `MarkFinished(container)`, which enforces the
**exactly-once** invariant: a duplicate `FinishedEffects` entry would run `OnRemove` twice and
double-reverse the effect. Marking happens from: duration expiry (`TickEffectShelf`), removal
requirements (`HandleGameplayEffects`), and explicit `RemoveGameplayEffect`.

`ClearFinishedEffects()` — snapshot-and-clear:

```
finished = FinishedEffects.ToArray(); FinishedEffects.Clear(); needsCleaning = false
per container:
    1. EffectShelf.Remove(container)      ← FIRST, so re-entrant HandleGameplayEffects
                                            passes can't re-mark a mid-removal container
    2. container.OnRemove()
         a. ContainerIsActive = false
         b. ReverseImpact()               ← mode-aware, see below
         c. contained OnRemove effects → Spec.Source.ApplyCarrier(fresh spec)
    3. RunWorkerRemoval
    4. RemoveAttributeDerivation(spec)    ← idempotent safety net (no-op if already removed)
    5. Callbacks.EffectRemoved
finally: HandleGameplayEffects()
```

Anything marked finished *during* the pass (worker/callback re-entry) lands in the now-empty
live list and is processed next update.

`AbstractEffectContainer.ReverseImpact()` — the mode-aware reversal, shared by removal and
ReplaceOldContainer:

- **Mode A**: `RemoveAttributeDerivation(spec)` → `RemoveCarrier` → `Recompute` re-derives
  `ActiveValue` without the spec's impacters. That IS the reversal.
- **Mode B** (only if `ReverseImpactOnRemoval`): `RollbackCommittedDelta(TrackedImpact.Total)` —
  subtracts the cumulative post-bounds tick history from `committedOneShotDelta`. "Undo every
  tick this spec ever ran."
- The two are mutually exclusive by the mode predicate — running both would double-undo.

**Rollback correctness invariant**: `TrackedImpact.Total` must equal exactly what the spec
committed into `committedOneShotDelta`. Both sides record post-bounds real impacts
(`TrackImpact` fires in commit PHASE 4 with the measured `ActiveValue − holdValue`), so clamped
ticks stay consistent. `EnforceScaling` propagation runs through a *separate* synthetic carrier
with its own `TrackedImpact` and does not pollute the spec's total.

---

## 8. Recalculation (regulated attributes)

Built at attribute registration (`ProvideAttribute`): each blueprint side's cached scaler calls
`RegulateContactWith(owningAttribute, RegulationCache)`, registering `contact → dependent` edges
(e.g. `CachedAttributeScaler` registers its `CaptureAttribute` as contact). The Base side is
always regulated; the Current side only when not `LinkCurrentToBase` (avoids double
registration of the same scaler).

At runtime:

```
any commit → Callbacks.AttributeChanged(impactData)
  → RefreshRegulatedAttributes(changedAttribute)
      for each dependent (cycle-guarded by _refreshingAttributes):
          Recompute(cache)      ← re-derives root via Blueprint.GetActiveValue, which reads
                                  the contact's fresh ActiveValue; retained carriers and
                                  one-shot history are preserved through the recompute
          if value changed → Callbacks.AttributeChanged(derived impact)   ← chains onward
```

`AttributeSystemComponent.Initialize()` performs one initial refresh sweep over all contact
attributes and then subscribes the refresh to `OnAttributeChanged`.

Note: only *blueprint* scalers participate in the regulation graph. An effect-magnitude scaler
that reads another attribute captures at apply time (spec's `SourceCapturedAttributes`) — it is
a snapshot by design and does not create a live dependency.

---

## 9. Initialization pulse & auto-scaling

- `ProvideAttributeSet`: phase 1 registers attributes, phase 2 initializes each
  `CachedAttributeValue` (root derivation + first `Recompute`), phase 3 fires a **zero-delta
  `ModifyAttribute` pulse** per attribute so workers/callbacks observe initial values. The pulse
  synthesizes an empty impacter list → commits nothing (bounds/rounding are already applied by
  init `Recompute`).
- `EnforceScaling` (`AutoScaleWithBase`): when a commit changes Base, a deferred
  `ModifyAttributeAction` is enqueued that adjusts Current by `(ΔBase / oldBase) × Current` —
  proportional scaling ("max HP +100% → current HP +100%"). It routes through a synthetic
  carrier (`Tags.AllowImpact`), so it lands as ordinary Mode B history.

---

## 10. Invariants checklist (regression tripwires)

1. **Twins**: `RuntimeGameplayAbilitySystem` and `GameplayAbilitySystem` implement identical
   effect logic. Every change to apply/tick/re-apply/remove goes in **both**.
2. **Exactly-once removal**: all finish paths go through `MarkFinished`; `ClearFinishedEffects`
   removes from the shelf before `OnRemove`.
3. **Recompute reproducibility**: `root + retained + committedOneShotDelta` (then bounds +
   rounding) must reproduce `ActiveValue` at any moment.
4. **Post-bounds bookkeeping**: both `committedOneShotDelta` and `TrackedImpact` record
   post-bounds deltas — never raw magnitudes.
5. **Mode exclusivity**: a spec is retained XOR tick-committing. The tick path never retains;
   the apply path never commits for durational tick-delivered specs.
6. **One reversal**: `ReverseImpact` runs Mode A OR Mode B reversal, never both, and runs at
   most once per spec lifetime (removal or replacement).
7. **Shelf iteration**: no `foreach` over `EffectShelf`/`FinishedEffects` in any code path that
   can re-enter effect application (contained effects, workers, callbacks).
8. **Batch-evaluated tick counts**: scaler-driven execute-tick counts are evaluated once per
   batch, not once per loop iteration.
9. **Null attribute targets are legal** (pure tag-grant effects): `CommitWithCarrier` and
   `RemoveAttributeDerivation` early-out; container reversal guards on the attribute.
