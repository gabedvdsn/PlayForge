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
  refresh — the stacking path relies on this). Removed entries are kept spare and reused, so a
  refresh allocates nothing (GAS-P4).
- `committedByGroup` — permanent history per retention group. Written only by `CommitImpact`.
- Attribution — rebuilt on every `Recompute`; per-group slices that sum exactly to `ActiveValue`.

### 2.1 Recompute pipeline

1. Baseline = the root value.
2. **Add** — additive modifier impacters, per slot (Current / Base / CurrentAndBase) → `DirtyValue`.
3. **Multiply** — `Multiplicative` impacters compound (×1.5, ×1.5 → ×2.25);
   `AdditiveBonus` impacters pool `(eval − 1)` into one `×(1 + Σ)` (+50%, +50% → ×2.0).
4. **FlatBonus** — post-multiplication additive.
5. **Committed history** — all of it, where it was measured: after the modifiers, so a transient %
   modifier doesn't scale it. 50 base × 1.5 aura + 10 tome = 85, and a 30 hit under the aura takes 30.
   Until Decision 4 (2026-11-03) it joined the baseline, and the same hit took 45.
6. **Override** — per-slot, lowest `SortOrder` wins. It comes after the committed history, so an
   override ("stun: speed = 0") still pins the value.
7. Constrain: element overflow clamp (`AttributeOverflowData.Clamp`) + rounding policy.

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
| `ModifyAttribute(attr, smv)` | CommitImpact; a delta-only carrier's delta commits as constant Adds in its retention group | init pulse, `EnforceScaling` propagation, external systems (`TryModifyAttribute`) |

Registration/unregistration are **identity-based** on the carrier instance — they are never
wrapped in synthetic carriers.

**Pre-change phase (commits).** Before the pre-change callback and workers run, a commit
evaluates its impacters once (frozen as constants, so a randomized scaler can't roll
differently twice) and previews the **incoming** delta: ops in order, no bounds or rounding.
That delta is what `ChangeValue` holds, since effect specs arrive as `(0,0)` transport, and an
Inline worker's edit is what commits:
- untouched → the frozen impacters commit as authored (op semantics, per-impacter groups);
- adjusted → the new delta commits as one constant Add in the impacters' shared group (mixed
  groups fall back to the carrier's retention group).

Either way §2.2 applies: post-constraint delta recorded, then `Recompute`. Armor therefore acts
on incoming damage and the clamp comes after: 20 HP, −30 hit, 50% armor → −15 → 5 HP. Raw
deltas (`ModifyAttribute`) preview as themselves. Register/Unregister are observe-only here,
because modifiers are re-derived every recompute, so a one-off adjustment has nowhere to live.

**A commit allocates nothing** (GAS-P6), adjusted or not. It rents its `ChangeValue` and its
frozen impacters and gives them back when it's done, one set for each commit in progress, since
a worker or a listener can commit inside another. An adjusted delta's Adds are frozen in the same
impacters. So:
- a `ChangeValue` belongs to its commit. A callback or an Inline worker reads it during the call,
  or copies its `Value`, and doesn't keep it;
- a Deferred worker's actions may keep it. A change handed to one is left to them, and its commit
  takes a new one for the next;
- the source-side `TrackImpact` keeps a total, the last impact and a count, not every impact.

A raw delta allocates nothing on its way through either: its delta's Adds are frozen in the same
impacters, and a `SourceAttributeImpact` keeps the origin it makes (GAS-P8). `GenerateSimple` makes a
new carrier on every call, so a raw delta made every frame keeps its carrier, made once with
`IAttributeImpactCarrier.GenerateSourceDerivation(source, attribute)`.

The changes the system makes on someone's behalf, `EnforceScaling`'s rescale and the lifesteal
worker's heal, allocate nothing either (GAS-P9, Decision 21):
- each is carried by a carrier kept for its source and attribute, in a table keyed weakly by
  source, so a source that's gone isn't held;
- each deferred one is queued in a `ModifyAttributeAction` the system takes back once the queue
  has run it, so a listener of `OnActionQueued` or `OnActionExecuted` reads an action during the
  call. One the queue invalidates isn't taken back;
- the queue walks its priority buckets by index, and `EnqueueRange` reads a list or an array by
  index, so taking an action off allocates nothing.

Null / undefined attribute targets early-out safely (pure tag-grant effects are legal).

---

## 4. Effect lifecycle (`EffectSystemComponent`)

One shared component owns the shelf. Both `RuntimeGameplayAbilitySystem` and the MonoBehaviour
`GameplayAbilitySystem` forward to a `GASCore`, which holds it (public API unchanged). **Edit
`EffectSystemComponent` for effect logic and `GASCore` for the rest of the GAS, not the hub
classes.**

```
ApplyGameplayEffect(spec)
│  validate (affiliation + source/target tag requirements)
├─ Instant  → grant tags → app workers → CommitImpact per OnApply entry
│             → removal workers (every impact made; none when nothing committed) → remove tags
└─ Durational/Infinite
      → TryHandleExistingDurationalGameplayEffect (absorbed? → done)
      → GenerateContainer → shelf (container ctor sets spec.RuntimeContainer + duration math);
        its tags count from here
      → RegisterModifier per Continuous entry
      → app workers
      → if TickOnApplication: tick workers once + CommitImpact per Periodic entry × executeTicks
then: requirements left for the next tick → RecordEffectApplied + EffectApplied callback
      → contained OnApply effects (recursive; see §4.2 for the cycle guard)

Tick (per update):
   if anything was applied since the last: HandleGameplayEffects (compile tags once → a met removal
   requirement finishes its container, ongoing requirements set Ongoing)
   EnablePeriodicTicks: timers advance → executeTicks → tick workers (every frame, even 0)
   → per execute tick: CommitImpact per Periodic entry + contained OnTick effects

Remove (ClearFinishedEffects):
   the pass's finished containers off the shelf and the index together
   → each: OnRemove: ReverseImpact() = UnregisterModifier per Continuous entry,
   then contained OnRemove effects → removal workers → RecordEffectRemoved + EffectRemoved callback
```

`SourcedModifiedAttributeValue(spec, 0, 0)` is transport only — magnitudes are always evaluated
freshly by the impacter pipeline.

### 4.1 Re-application (`ReApplicationPolicy`)

| Policy | Behavior |
|---|---|
| `DoNothing` | absorbed; only the interaction runs |
| `ReplaceOldContainer` | `ReverseImpact()` old spec → container adopts new spec (+durations; stack count preserved) → register new spec's Continuous entries. Old commits stay — commits never reverse. |
| `AppendNewContainer` | fresh container + distinct spec instance → modifiers coexist (stacking by coexistence; `MultiplyBehaviour` controls composition) |
| `StackExistingContainers` | `Stack(n)` on the found container → re-register Continuous entries so stack-aware magnitudes recompute now (`ScalerImpacter` evaluates container-aware via `spec.RuntimeContainer`). A stack lost when its time runs out re-registers the same way, as does the stack `Extend` adds to independent durations (GAS-34) |

`ReApplicationInteraction` (Refresh/Extend + re-fire TickOnApplication) runs **only when
absorbed** — the append path would double-tick otherwise.

### 4.2 Loop-safety invariants (do not undo)

- All finish paths go through `MarkFinished` (exactly-once; duplicate OnRemove = double reversal). A
  set mirrors the finished list, so marking a container and skipping a finished one never walk it.
- `TickEffectShelf` iterates by index over the frame-start count (contained effects add to the
  shelf mid-loop); already-finished containers are skipped.
- `ClearFinishedEffects` snapshots-and-clears, then takes the whole pass off the shelf and out of the
  index, in one walk of each, **before** any `OnRemove`. Taken off one at a time, each a linear
  remove, ending n containers cost n² (GAS-P5).
- A spec builds its impacters once, with each delivery's attributes and each attribute's
  impacters. `GetImpactersFor` returns the spec's own list: read it, don't change it. A tick's
  lookups, its contained effects and a recompute allocate nothing; walk the lists by index or
  with a `foreach` over the concrete type, since one through an interface boxes (GAS-P4).
- Scaler-driven execute-tick counts are evaluated once per batch, never per loop iteration.
- An effect asked for again on an entity before its own application there is over is refused, with one
  warning per entity and effect. An effect that contains itself, directly or through other contained
  effects or what applying it sets off, recursed until the stack overflowed (GAS-28).
- Any change to a container's stack count re-registers its Continuous entries, gained or lost (GAS-34).
- Applying never walks the shelf. Its requirements wait for one pass at the next tick, a container's tags
  count from the moment it is shelved, and re-application finds its containers in an index by asset tag,
  which `Shelve` and `Unshelve` keep. Each application re-checked every container and rebuilt all tags, so
  it cost as much as the entity carried (GAS-P3).

---

## 5. Recalculation (regulated attributes)

Blueprint cached scalers register `contact → dependent` edges at attribute registration
(`ProvideAttribute` → `RegulateContactWith`). Any commit **or modifier registration or
unregistration** fires `AttributeChanged` → `RefreshRegulatedAttributes(contact)` → dependents
`Recompute` (re-deriving their root from the contact's fresh `ActiveValue` while their own
modifiers and committed history are preserved) → chained `AttributeChanged`, cycle-guarded.

A dependent's refresh is the system's own change (Decision 14). It takes the post-change half of
the pipeline: `EnforceScaling`, post-change workers and `AttributePostChange`. It is recorded in
the frame summary as received and dealt, self to self. Its carrier is the attribute's root, typed
`DisallowImpact`, so a worker that checks impact types passes it by.

Effect-magnitude scalers that read attributes capture snapshots at apply time by design — only
blueprint scalers create live dependencies.

---

## 6. Initialization & auto-scaling

- `ProvideAttributeSet`: register attributes → initialize each cached value (first recompute)
  → zero-delta `ModifyAttribute` pulse per attribute (workers/callbacks observe initial values;
  commits nothing).
- `EnforceScaling` (`AutoScaleWithBase`): Base changes, a dependent's refresh included, enqueue a
  deferred proportional Current adjustment through a synthetic carrier, kept for whoever changed Base
  (GAS-P9) — lands as ordinary committed history. The carrier is typed `DisallowImpact` and unworkable, as the pulse is, so no worker that
  checks impact types sees it (GAS-35). The adjustment is a share of Current before the bounds, where
  it lands, so the bounds clamp the result once: under ZeroToBase an 80/100 attribute that loses half
  its Base ends at 40/50 (GAS-37).

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

**Window → PlayForge → Audit Effect Impact Deliveries** reports every asset's derived delivery and
flags the shapes above — read-only, nothing is written.

Deleted outright: `IsSustainedModifier()`, the Mode A/B inference, `TickAttribute`,
`RollbackCommittedDelta`, tick-rollback via `TrackedImpact` (which is analytics-only now).

---

## 8. Verification

`Assets/PlayForge/Tests/Editor/PlayForgeGoldenTests.cs` — the Test Runner's EditMode tab, `FarEmerald.PlayForge.Tests.Golden`
(or batchmode `-executeMethod FarEmerald.PlayForge.Tests.PlayForgeGoldenTests.RunAll`).
Scenarios pin: multiplicative/pooled buff add/remove ordering (the 50→60→72→60→50 case),
item modifier reversal, HoT permanence through expiry, clamp + history reproducibility,
committed history landing after the modifiers, stack-aware magnitudes, regulation propagation on removal,
null-target effects, replace idempotency, group attribution sums, pre-change workers adjusting
the committed delta (armor before the clamp).
