# PlayForge — Impact Model Overhaul (Design Plan)

Status: **IMPLEMENTED** (2026-07-12) — Phases 0–4 landed; `EFFECT_PIPELINE.md` is the
authoritative description of the shipped pipeline.

Amendment (2026-07-14): entries are **derived on every `Generate()`, never serialized**
(§3.1 as-built differs from the plan here). The serialized-entries approach froze
half-authored inspector state into assets; the single quad remains the authored surface
until a multi-entry drawer ships, at which point an explicitly-authored entry list can take
precedence over derivation. The migration tool became the read-only
**PlayForge → Audit Effect Impact Deliveries**.

Remaining follow-ups: multi-entry property drawer (unlocks authored entry lists + per-entry
groups in the inspector), per-group payloads on `AttributeSystemCallbacks` (queries via
`GetBreakdown`/`TryGetGroupValue` cover the UX case), GASDebugger modernization (currently
renders through the compatibility `RetainedGroups` projection).
Verification: **PlayForge → Run Golden Tests**.
Paths relative to `Assets/PlayForge/Runtime/PlayForge/`.

---

## 0. Why (the motivating failure)

Test: attribute at 50, apply two durational "+20%" effects with `ReverseImpactOnRemoval = true`.

| Step | Expected | Actual | Why |
|---|---|---|---|
| Apply #1 | 60 | 60 | committed +10 as a flat delta (spec was tick-delivered → Mode B) |
| Apply #2 | 72 | 72 | committed +12 (×1.2 of the already-buffed 60) |
| Remove #1 | **60** | **62** | rollback subtracts the stored +10 — but the *live* state should be 50 × 1.2 |
| Remove #2 | 50 | 50 | coincidentally correct (last removal) |

The disease, not the symptom: **a percentage modifier was allowed to enter the commit/rollback
path.** A `×1.2` cannot be reversed as a stored delta; its contribution depends on everything
else that is live. It must be *derived* — recomputed from live carriers — so removal is simply
"recompute without it." The current model lets authors route any modifier through either path
(`Mode A` retained vs `Mode B` committed + `TrackedImpact` rollback), and the mode is *inferred*
from tick flags (`TickOnApplication`/`EnablePeriodicTicks`) rather than declared. One stale
serialized bool silently flips a buff from "derived" to "permanently committed with best-effort
rollback."

Secondary convolutions this plan removes:

- `IsSustainedModifier()` — mode inferred from delivery mechanics instead of author intent.
- `ReverseImpactOnRemoval` — a removal-time flag doing retention's job, only coherent for one
  narrow case (tick rollback) that produces the bug above for multiplicative ops.
- `TrackedImpact`-based rollback — a parallel bookkeeping system whose only invariant
  ("tracked total == committed total") exists to emulate what recompute gives for free.
- One `ScalerImpacter` per effect — forces contained-effect workarounds for compound effects.
- Twin GAS implementations (`RuntimeGameplayAbilitySystem` / `GameplayAbilitySystem`) that must
  be hand-synchronized.

---

## 1. The new model — three orthogonal axes

Today one effect = one impact whose behavior is inferred from duration + tick flags + a reversal
flag. The overhaul separates three concerns that are currently tangled:

### Axis 1 — Contribution kind (the ONLY behavioral fork)

Every impact entry is exactly one of:

| Kind | Semantics | Reversal | Storage |
|---|---|---|---|
| **Modifier** (transient, derived) | "While this carrier is present, the attribute is modified." | Automatic — unregister + recompute. There is no other way to end it. | Registered in the attribute's modifier collection; re-evaluated every recompute. |
| **Commit** (permanent) | "This event permanently changed the attribute." | **Never.** Permanent means permanent. | Folded into the attribute's committed history at commit time (post-bounds delta). |

This is the user-facing statement of the retention rule: **an impact is a Modifier iff its
carrier can be removed or manipulated and the impact should follow it** — item stat bonuses,
auras, durational %-buffs, stances. Everything event-shaped — damage, heals, DoT/HoT ticks,
XP — is a Commit.

`ReverseImpactOnRemoval` is **deleted**. "Grant +20% move speed for 5 s" = a Modifier (reverses
by definition). "Grant 5 HP per second for 10 s" = periodic Commits (never reverse). The flag
has no remaining meaning. The dropped capability — "committed ticks that un-commit on removal" —
is intentionally unsupported; if a design truly needs it, author it as a Modifier whose
magnitude grows per tick (stack-aware scaler), which reverses correctly by construction.

### Axis 2 — Delivery (when the impact happens)

Declared **per impact entry**, validated against the carrier's duration policy:

| Delivery | Meaning | Valid on |
|---|---|---|
| `Continuous` | Modifier registered on apply, unregistered on remove | Durational / Infinite |
| `OnApply` | One Commit at application time | Any policy (this is what Instant effects are) |
| `Periodic` | One Commit per execute tick | Durational / Infinite with periodic ticks |

Kind is *implied* by delivery (`Continuous` → Modifier; `OnApply`/`Periodic` → Commit), so
authors pick one dropdown and cannot express the incoherent combinations that exist today.
`TickOnApplication` stops being a mode-flipping trap: it becomes tick *scheduling* only
("Periodic entries also fire once at application"), with zero influence on retention.

### Axis 3 — Retention group (who the contribution belongs to)

Every contribution — Modifier *and* Commit — is tagged with a retention group (`Tag`). Groups
are **pure attribution**: they never change the math of the shared pool; they partition it.

- The attribute-set element's innate value belongs to its configured group (e.g. `Group.Base`).
- An item's +50 HP Modifier belongs to e.g. `Group.Bonus`.
- A HoT's periodic Commits can land in `Group.Bonus` too — "bonus health from regen".
- Consumers query per-group aggregates: UI renders base vs bonus health; a damage formula reads
  `GetGroupValue(damageAttr, Group.BaseDamage)` and `GetGroupValue(damageAttr, Group.BonusDamage)`
  and applies different outgoing calculations to each.

One pool, N labeled slices, always summing exactly to `ActiveValue`.

---

## 2. Attribute-side redesign (`CachedAttributeValue` → leaner)

### 2.1 State

```
root                    ← unchanged: Blueprint.GetActiveValue(cache), re-derived every recompute
committedByGroup        ← Dictionary<Tag, AttributeValue>   (replaces single committedOneShotDelta)
modifiers               ← ModifierCollection: entries of (carrier, impacter, group), apply-ordered
                          (replaces retainedGroups — same storage idea, explicit registration)
```

Deleted: `TrackedImpact` rollback path, `RollbackCommittedDelta`, the retained-carrier
idempotency scan in the apply path (registration becomes an explicit, single-shot call from the
effect lifecycle — see §3), `IsSustainedModifier` checks.

### 2.2 The formula (one deliberate change)

```
baseline    = root + Σ committedByGroup                       ← permanent history joins the BASELINE
ActiveValue = ModifierPipeline(baseline, modifiers)            ← Add → Multiply → FlatBonus → Override
              then bounds + rounding
```

Today committed history is added *after* the modifier pipeline (after Override even). Moving it
into the baseline means **permanent gains are affected by transient % modifiers**, which matches
player expectations: base damage 50, permanent +10 from a tome, +20% aura → (50+10) × 1.2 = 72,
not 50 × 1.2 + 10 = 70. It also fixes the standing edge case where an Override modifier
("stun: speed = 0") leaked committed history on top of the override.

Consequence to accept: removing/adding modifiers rescales the *displayed* effect of past commits
(the +10 tome is worth +12 while the aura holds). That is the standard stat-system convention.

Commit math is unchanged in spirit: a Commit evaluates its impacters against the current
`ActiveValue`, clamps/rounds, and records the **post-bounds delta** into its group's committed
slot. Multiplicative Commits remain order-dependent — acceptable because they never reverse.

### 2.3 Per-group attribution (the UX query surface)

During recompute, contributions are attributed sequentially: each modifier's group is credited
with `(value after its op − value before its op)` in pipeline order; multiplicative pool
contributions are credited to the multiplier's group. Deterministic, cheap (computed during the
recompute walk we already do), and slices always sum to the total.

New public surface:

```csharp
AttributeValue GetGroupValue(Tag group);            // committed + attributed modifier share
IReadOnlyList<GroupContribution> GetBreakdown();    // ordered slices for UI
```

`AttributeSystemCallbacks` gains a per-group delta payload on change events so health bars can
animate base vs bonus independently.

### 2.4 New attribute-system API (replaces the Modify/Tick split)

```csharp
RegisterModifier(carrier)      // Continuous entries; explicit, called once by the lifecycle owner
UnregisterModifier(carrier)    // removal = unregister + recompute; the ONLY reversal mechanism
CommitImpact(carrier)          // OnApply / Periodic entries; permanent, per-group history
ApplyDelta(...)                // synthetic writes (init pulse, EnforceScaling, external systems)
```

All four funnel through the existing 4-phase pipeline (pre-workers → mutate → post-workers →
impact relay), so workers/callbacks/regulation refresh behave exactly as today. `TickAttribute`
vs `ModifyAttribute` and the retention-vs-idempotency dance disappear — the caller states intent.

`TrackedImpact` survives **only** as analytics (damage meters, impact workers); nothing reads it
for reversal.

---

## 3. Effect-side redesign

### 3.1 Multi-entry impacts

`GameplayEffectImpact` becomes a **list of entries**:

```csharp
class AttributeImpactEntry {
    Attribute            AttributeTarget;
    EImpactDelivery      Delivery;         // Continuous | OnApply | Periodic
    EAttributeTarget     TargetSlot;       // Current | Base | CurrentAndBase
    ECalculationOperation Operation;       // Add | Multiply | FlatBonus | Override
    EMultiplyBehaviour   MultiplyBehaviour;
    int                  SortOrder;
    ScalerMagnitudeOperation Magnitude;    // container-aware evaluation kept (stacks)
    Tag                  RetentionGroupOverride;  // default: effect Definition.RetentionGroup
    List<Tag>            ImpactTypes;
}
```

One effect can now be "+10% speed while active (Continuous) AND 5 HP per tick (Periodic)" —
today that requires contained-effect contortions. `GetImpacters()` returns per-entry impacters
filtered by delivery, so the attribute system never re-derives intent.

Drawer validation makes illegal states unrepresentable: `Continuous` hidden on Instant effects;
`Periodic` hidden unless periodic ticks are enabled; a warning when an effect has zero entries.

### 3.2 Lifecycle becomes mechanical

```
Apply (durational):  shelve container
                     → RegisterModifier for each Continuous entry
                     → CommitImpact(OnApply entries)  [+ Periodic entries if TickOnApplication]
Tick:                CommitImpact(Periodic entries) per execute tick
Remove:              UnregisterModifier for each Continuous entry — done. No modes, no rollback.
Instant:             CommitImpact(OnApply entries) — no shelf, no removal phase.
```

Re-application maps cleanly:

- `AppendNewContainer` → second spec registers its own Continuous entries → natural stacking
  (the 60/72 scenario: `Multiplicative` behaviour → 72, `AdditiveBonus` → 70; **both** remove
  back to 60 then 50 correctly, because removal is recompute, not arithmetic).
- `StackExistingContainers` → `Stack(n)` + re-register refresh (container-aware magnitude
  already in place from the previous fix round).
- `ReplaceOldContainer` → unregister old spec's Continuous entries, adopt new spec, register
  new entries. (No committed reversal — commits are permanent regardless of replacement.)

### 3.3 Kill the twins

Extract the entire effect shelf (apply / tick / re-apply / remove / `MarkFinished` /
`ClearFinishedEffects` / `CompileGrantedTags` inputs) into a plain-class
**`EffectSystemComponent`**, owned by both `RuntimeGameplayAbilitySystem` and the MonoBehaviour
`GameplayAbilitySystem` — exactly the pattern `AttributeSystemComponent` already uses. The
MonoBehaviour keeps only Unity glue (transform, update forwarding). Twin drift stops being a
class of bug.

---

## 4. What is deleted (the simplification dividend)

| Deleted | Replaced by |
|---|---|
| `IsSustainedModifier()` + Mode A/B inference | per-entry `Delivery` declaration |
| `ReverseImpactOnRemoval` | Modifiers always reverse; Commits never do |
| `RollbackCommittedDelta` + tracked-total invariant | nothing — the case no longer exists |
| `TickAttribute` vs `ModifyAttribute` fork + retained-idempotency scan in `ApplyCarrier` | explicit `RegisterModifier` / `CommitImpact` |
| single `committedOneShotDelta` | `committedByGroup` (same idea, attributable) |
| `RetainImpact()` on carriers (already vestigial) | gone |
| duplicated shelf logic in two GAS classes | `EffectSystemComponent` |

Kept unchanged: blueprint/root derivation, regulation graph + `RefreshRegulatedAttributes`,
bounds/rounding, worker phases and the 4-phase commit pipeline, tag compilation, duration/tick
scheduling math, containers/stacking configs, `EnforceScaling`, init pulse.

---

## 5. Migration

Serialized `GameplayEffect` assets upgrade via `ISerializationCallbackReceiver` (or a one-shot
editor tool — recommended so the upgrade is visible and committable):

| Old asset shape | New entry |
|---|---|
| Instant + impact quad | one `OnApply` entry |
| Durational/Infinite, no ticks (old Mode A) | one `Continuous` entry (behavior identical) |
| Durational/Infinite + ticks, `ReverseImpactOnRemoval = false` | one `Periodic` entry (behavior identical) |
| Durational/Infinite + ticks, `ReverseImpactOnRemoval = true` | one `Periodic` entry + **editor warning**: rollback semantics removed; if the effect was really a transient buff (the 62-bug shape), re-author as `Continuous` |
| Durational + `TickOnApplication` only (no periodic) — the accidental-Mode-B trap | **flagged for review**: almost always meant `Continuous`; migration defaults to `Continuous` when the op is Multiply/Override (a pulse-once multiply is near-certainly a buff), `OnApply` commit otherwise |
| `Definition.RetentionGroup` | entry group default (unchanged) |

`Tags.IgnoreRetention` remains the group for unattributed synthetic writes.

---

## 6. Implementation phases (each compiles + passes tests before the next)

**Phase 0 — Golden tests (before touching code).** Unity Test Framework asmdef +
EditMode tests around `RuntimeGameplayAbilitySystem` (plain class — no scenes needed):
the 50→60→72→60→50 scenario (both multiply behaviours), item-style infinite modifier
register/remove, HoT permanence after expiry, instant clamp + recompute reproducibility,
stack magnitude, replace policy, regulation chain (derived attribute recompute), null-target
effects. These encode the *target* semantics; the ones that fail today are the point.

**Phase 1 — Data model + migration.** `AttributeImpactEntry`, list on `GameplayEffectImpact`,
per-entry impacters from `GetImpacters(delivery)`, asset upgrader + drawer validation.
Runtime still consumes entry #0 through the old paths — pure additive step.

**Phase 2 — Attribute core.** `ModifierCollection` + `committedByGroup`, new formula
(baseline = root + committed → modifier pipeline), per-group attribution, new API
(`RegisterModifier` / `UnregisterModifier` / `CommitImpact` / `ApplyDelta`) implemented as
wrappers over the 4-phase pipeline. Old entry points delegate to the new ones (temporarily).

**Phase 3 — Effect lifecycle on the new API.** `EffectSystemComponent` extraction (both GAS
classes delegate); apply/tick/re-apply/remove rewritten per §3.2; delete
`IsSustainedModifier` / `ReverseImpactOnRemoval` / rollback machinery / `TickAttribute` fork.

**Phase 4 — Group surface.** `GetGroupValue` / `GetBreakdown`, per-group callback payloads,
sample: health bar split (base/bonus) + damage calc reading two damage groups.

**Phase 5 — Docs + cleanup.** Rewrite `EFFECT_PIPELINE.md` against the new model, delete this
plan's PROPOSED banner, remove dead types, final invariant checklist.

Verification per phase: `MSBuild Assembly-CSharp.csproj` (zero errors) + EditMode suite green.

---

## 7. Decisions taken (flag disagreement before Phase 1)

1. **Commits join the baseline** (§2.2) — permanent history is multiplied by live % modifiers.
2. **Tick rollback is dropped**, not ported (§1, Axis 1). Migration warns on assets using it.
3. **Delivery implies kind** — no independent "retained?" checkbox to get wrong.
4. **Groups are attribution-only** — group-specific *math* (e.g. "bonus damage ignores armor")
   lives in consumers (damage calc reads per-group values), not in the attribute pipeline.
5. **Multiplicative attribution rule**: sequential credit in pipeline order (§2.3) — simple and
   exactly summing, at the cost of slight order sensitivity in the *display* split only.
6. **Twin extraction happens inside this refactor** (Phase 3), not after — rewriting shelf
   logic twice and then extracting would be double work.
