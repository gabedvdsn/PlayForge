# PlayForge — Gameplay Effect & Attribute Pipeline

How a `GameplayEffect` becomes an attribute change, stays correct while it lives, and reverses
when it dies — under the **two-kind impact model** (design rationale: `IMPACT_MODEL_REFACTOR.md`).
This is the authoritative description of the runtime pipeline; if code and doc disagree, one of
them has a bug — file it either way.

All paths relative to `Assets/PlayForge/Runtime/PlayForge/`.

---

## 1. The model in one page

Every authored impact is an **`AttributeImpactEntry`** on `GameplayEffectImpact.Entries`:
`{ AttributeTarget, Delivery, Operation, TargetSlot, MultiplyBehaviour, SortOrder, Magnitude,
RetentionGroupOverride, ImpactTypes }`. One effect may carry many entries across many
attributes ("+10% speed while active AND 5 HP per tick" is one asset).

**Delivery decides everything** — there is deliberately no separate "retained?" or
"reverse on removal?" switch:

| Delivery | Kind | When | Reversal |
|---|---|---|---|
| `Continuous` | **Modifier** — transient, derived | registered on apply, while the carrier lives | automatic & exact: unregister + recompute. The ONLY reversal mechanism. |
| `OnApply` | **Commit** — permanent | once at application | never |
| `Periodic` | **Commit** — permanent | once per execute tick | never |

- *Modifier* = "while this carrier is present" — item stat bonuses, auras, durational %-buffs.
  Its magnitude is re-evaluated on every recompute; removing the carrier recomputes without it.
  A `×1.2` buff can never leave residue because its value is never stored, always derived.
- *Commit* = an event that permanently changed the attribute — damage, heals, DoT/HoT ticks.
  The post-constraint delta is recorded into per-group committed history and survives everything.

**Retention groups** (`Tag`) are pure attribution: every contribution — root value, modifiers,
commits — belongs to a group (entry override → else the effect's `Definition.RetentionGroup`;
attribute-set roots use their element's `RetentionGroup`). Groups partition one pool for
queries/UI (base vs bonus health, base vs bonus damage); they never change the math.

Validation: `Continuous` requires a durational/infinite effect; `Periodic` requires ticks.
(`AttributeImpactEntry.IsDeliveryValidFor`.)

---

## 2. Attribute state (`CachedAttributeValue`)

```
ActiveValue = ConstrainValue(                         ← element clamp + rounding
                 ModifierPipeline(                    ← Add → Multiply → FlatBonus → Override
                     root                             ← Blueprint.GetActiveValue(cache), re-derived
                   + Σ committedByGroup ) )           ←   fresh every recompute (or generic seed)
```

State:
- `modifiers` — ordered `(carrier, impacter, group)` entries. Presence == contribution; no
  alive-flags, no zombie state. Re-registering a carrier **replaces** its entries (idempotent
  refresh — the stacking path relies on this).
- `committedByGroup` — permanent history per retention group. Written only by `CommitImpact`.
- Attribution — rebuilt on every `Recompute`; per-group slices that sum exactly to `ActiveValue`.

### 2.1 Recompute pipeline

1. Baseline = root + all committed history. **Committed history joins the baseline**, so
   transient % modifiers scale permanent gains: (50 base + 10 tome) × 1.5 aura = 90.
2. **Add** — additive modifier impacters, per slot (Current / Base / CurrentAndBase) → `DirtyValue`.
3. **Multiply** — `Multiplicative` impacters compound (×1.5, ×1.5 → ×2.25);
   `AdditiveBonus` impacters pool `(eval − 1)` into one `×(1 + Σ)` (+50%, +50% → ×2.0).
4. **FlatBonus** — post-multiplication additive.
5. **Override** — per-slot, lowest `SortOrder` wins.
6. Constrain: element overflow clamp (`AttributeOverflowData.Clamp`) + rounding policy.

Attribution is captured during the walk (root → its group; commits → their groups; each
modifier → the value swing its op produced; pooled multiplies → proportional shares; the
constraint shift → the unattributed slice) so `GetBreakdown()` always sums to `ActiveValue`.

### 2.2 Commit math

`CommitImpact(carrier, impacters)` runs each impacter as a sequential mini-step against the
live value (op → clamp → round), records each step's **post-constraint delta** into its group
(a +50 heal clamped to +30 commits +30), then runs one canonical `Recompute`.

**Reproducibility invariant**: `ActiveValue` is always exactly `Recompute(state)` — every
mutation path ends in a recompute. There is no second bookkeeping system to drift.

### 2.3 Query surface

- `GetGroupValue(Tag)` / `GetBreakdown()` on `CachedAttributeValue`;
  `TryGetGroupValue(attr, group, out v)` / `GetBreakdown(attr)` on `AttributeSystemComponent`.
- Consumer pattern: UI splits a health bar by group slices; a damage formula reads
  `GetGroupValue(damage, Group.Base)` vs `GetGroupValue(damage, Group.Bonus)` and applies
  different outgoing calculations — group-specific math lives in consumers, not the pipeline.

---

## 3. The four-verb attribute API (`AttributeSystemComponent`)

Every attribute mutation funnels through one shared 4-phase pipeline
(`CommitWithCarrier`): pre-change workers → PHASE-2 dispatch → post-change workers →
impact relay (`ImpactData`, frame summaries, source-side `TrackImpact`, `AttributeChanged`).

| Verb | Dispatch | Used by |
|---|---|---|
| `RegisterModifier(attr, smv, impacters)` | register/refresh in `modifiers` + recompute | Continuous entries on apply / stack refresh / replace |
| `UnregisterModifier(attr, smv)` | remove carrier's entries + recompute | removal & replacement — **fires callbacks like any change**, so regulated attributes refresh (removal used to be silent → stale derived values) |
| `CommitImpact(attr, smv, impacters)` | permanent per-group commit | OnApply / Periodic entries |
| `ModifyAttribute(attr, smv)` | CommitImpact; delta-only carriers are wrapped in a synthetic `SourceAttributeImpact` with `ConstantImpacter`s | init pulse, `EnforceScaling` propagation, external systems (`TryModifyAttribute`) |

Registration/unregistration are **identity-based** on the carrier instance — they are never
wrapped in synthetic carriers.

Null / undefined attribute targets early-out safely (pure tag-grant effects are legal).

---

## 4. Effect lifecycle (`EffectSystemComponent`)

One shared component owns the shelf; both `RuntimeGameplayAbilitySystem` and the MonoBehaviour
`GameplayAbilitySystem` hold an instance and delegate (public API unchanged). **The twins no
longer duplicate effect logic — edit `EffectSystemComponent`, not the GAS classes.**

```
ApplyGameplayEffect(spec)
│  validate (affiliation + source/target tag requirements)
├─ Instant  → grant tags → app workers → CommitImpact per OnApply entry
│             → removal workers → remove tags
└─ Durational/Infinite
      → TryHandleExistingDurationalGameplayEffect (absorbed? → done)
      → GenerateContainer → shelf (container ctor sets spec.RuntimeContainer + duration math)
      → RegisterModifier per Continuous entry
      → app workers
      → if TickOnApplication: tick workers once + CommitImpact per Periodic entry × executeTicks
then: HandleGameplayEffects → EffectApplied callback → contained OnApply effects (recursive)

Tick (per update, EnablePeriodicTicks):
   timers advance → executeTicks → tick workers (every frame, even 0)
   → per execute tick: CommitImpact per Periodic entry + contained OnTick effects

Remove (ClearFinishedEffects):
   off shelf → OnRemove: ReverseImpact() = UnregisterModifier per Continuous entry,
   then contained OnRemove effects → removal workers → EffectRemoved callback
```

`SourcedModifiedAttributeValue(spec, 0, 0)` is transport only — magnitudes are always evaluated
freshly by the impacter pipeline.

### 4.1 Re-application (`ReApplicationPolicy`)

| Policy | Behavior |
|---|---|
| `DoNothing` | absorbed; only the interaction runs |
| `ReplaceOldContainer` | `ReverseImpact()` old spec → container adopts new spec (+durations; stack count preserved) → register new spec's Continuous entries. Old commits stay — commits never reverse. |
| `AppendNewContainer` | fresh container + distinct spec instance → modifiers coexist (stacking by coexistence; `MultiplyBehaviour` controls composition) |
| `StackExistingContainers` | `Stack(n)` on the found container → re-register Continuous entries so stack-aware magnitudes recompute now (`ScalerImpacter` evaluates container-aware via `spec.RuntimeContainer`) |

`ReApplicationInteraction` (Refresh/Extend + re-fire TickOnApplication) runs **only when
absorbed** — the append path would double-tick otherwise.

### 4.2 Loop-safety invariants (do not undo)

- All finish paths go through `MarkFinished` (exactly-once; duplicate OnRemove = double reversal).
- `TickEffectShelf` iterates by index over the frame-start count (contained effects add to the
  shelf mid-loop); already-finished containers are skipped.
- `ClearFinishedEffects` snapshots-and-clears, removes from the shelf **before** `OnRemove`.
- Scaler-driven execute-tick counts are evaluated once per batch, never per loop iteration.

---

## 5. Recalculation (regulated attributes)

Blueprint cached scalers register `contact → dependent` edges at attribute registration
(`ProvideAttribute` → `RegulateContactWith`). Any commit **or modifier registration or
unregistration** fires `AttributeChanged` → `RefreshRegulatedAttributes(contact)` → dependents
`Recompute` (re-deriving their root from the contact's fresh `ActiveValue` while their own
modifiers and committed history are preserved) → chained `AttributeChanged`, cycle-guarded.

Effect-magnitude scalers that read attributes capture snapshots at apply time by design — only
blueprint scalers create live dependencies.

---

## 6. Initialization & auto-scaling

- `ProvideAttributeSet`: register attributes → initialize each cached value (first recompute)
  → zero-delta `ModifyAttribute` pulse per attribute (workers/callbacks observe initial values;
  commits nothing).
- `EnforceScaling` (`AutoScaleWithBase`): Base changes enqueue a deferred proportional Current
  adjustment through a synthetic carrier — lands as ordinary committed history.

---

## 7. Entry derivation (no asset migration)

**Entries are derived, never serialized.** The serialized authoring surface is the effect's
single impact quad + duration flags; `GameplayEffectImpact.RebuildEntries` re-derives the
entry list on **every `Generate()`**, so inspector edits (magnitude, attribute target,
duration policy, tick flags) always take effect on the next application. `Magnitude` on the
derived entry references the live `ScalerMagnitudeOperation`, so even already-registered
modifiers re-evaluate new magnitudes on their next recompute.

> Serializing entries was tried and reverted: `OnValidate` froze half-authored state into
> assets (null targets, stale magnitudes, deliveries minted while the policy was still
> Instant) and never re-synced. Do not reintroduce serialized entries until a dedicated
> multi-entry drawer makes the entry list the *authored* surface.

Delivery derivation (`ResolveDelivery`):

| Authored shape | Derived delivery |
|---|---|
| Instant | `OnApply` |
| Durational/Infinite, no ticks | `Continuous` |
| + periodic ticks | `Periodic` (`ReverseImpactOnRemoval` flagged: rollback no longer exists) |
| TickOnApplication-only trap | `Continuous` for Multiply/Override (flagged), else `OnApply` |

**PlayForge → Audit Effect Impact Deliveries** reports every asset's derived delivery and
flags the shapes above — read-only, nothing is written.

Deleted outright: `IsSustainedModifier()`, the Mode A/B inference, `TickAttribute`,
`RollbackCommittedDelta`, tick-rollback via `TrackedImpact` (which is analytics-only now).

---

## 8. Verification

`Assets/PlayForge/Tests/Editor/PlayForgeGoldenTests.cs` — **PlayForge → Run Golden Tests**
(or batchmode `-executeMethod FarEmerald.PlayForge.Tests.PlayForgeGoldenTests.RunAll`).
Scenarios pin: multiplicative/pooled buff add/remove ordering (the 50→60→72→60→50 case),
item modifier reversal, HoT permanence through expiry, clamp + history reproducibility,
committed-joins-baseline, stack-aware magnitudes, regulation propagation on removal,
null-target effects, replace idempotency, group attribution sums.
