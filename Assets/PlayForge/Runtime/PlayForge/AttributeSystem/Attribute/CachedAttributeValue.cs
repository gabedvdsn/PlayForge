using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class CachedAttributeValue
    {
        public AttributeValue ActiveValue { get; private set; }

        /// <summary>
        /// Intermediate aggregation snapshot — the value AFTER all Add impacters but BEFORE
        /// Multiply / FlatBonus / Override. Exposed for diagnostics (and for cached scalers
        /// that key off the post-additive baseline). Updated only by <see cref="Recompute"/>.
        /// </summary>
        public AttributeValue DirtyValue { get; private set; }
        
        /// <summary>
        /// The basest attribute value derived from the entity's attribute set.
        /// </summary>
        public AttributeValue RootValue => Blueprint.RootValue;

        public AttributeBlueprint Blueprint { get; }

        private IAttributeImpactCarrier root;
        public IAttributeImpactCarrier Root => root;


        // ─── Retained carriers (Mode A — sustained modifiers) ────────────────────
        // Keyed by retention-group tag. Each group holds the list of carriers retained
        // under that tag, in apply order. The retention rule is a SINGLE predicate:
        //
        //   retain = carrier.IsSustainedModifier()
        //          = durational/infinite  AND  NOT tick-delivered
        //
        // A durational, non-tick-delivered effect can only have any effect by being
        // retained (it has no tick events to commit through) — that is precisely a
        // sustained modifier. Its impacters are folded into every Recompute and its
        // contribution reverses automatically when the carrier is removed.
        //
        // Everything else — instant effects, every tick of a tick-delivered effect,
        // synthetic SourceAttributeImpact deltas — is Mode B: committed one-shot into
        // committedOneShotDelta. Note that neither "touches Base" nor
        // "ReverseImpactOnRemoval" influence retention; Base one-shots survive Recompute
        // via committedOneShotDelta.Base, and reversal-on-removal is a removal-time
        // concern handled in AbstractEffectContainer.OnRemove.
        private readonly Dictionary<Tag, RetainedGroup> retainedGroups = new();
        public IReadOnlyDictionary<Tag, RetainedGroup> RetainedGroups => retainedGroups;

        // ─── One-shot committed delta ─────────────────────────────────────────────
        // Persistent record of every contribution that bypassed retention. Each
        // ApplyOneShotImpacters / TickCarrier call computes the post-bounds delta it
        // produced and accumulates it here, so subsequent Recompute passes can
        // reproduce ActiveValue exactly:
        //
        //   ActiveValue = root + retained-aggregate + committedOneShotDelta
        //
        // Without this, a Recompute triggered by anything else (regulated-attribute
        // refresh, retained-carrier add/remove, blueprint scaler change) would silently
        // wipe every instant-add tick's history because the retained list is the only
        // input it walks.
        private AttributeValue committedOneShotDelta;
        public AttributeValue CommittedOneShotDelta => committedOneShotDelta;

        public static CachedAttributeValue GenerateNull()
        {
            return new CachedAttributeValue(null);
        }

        public static CachedAttributeValue GenerateGeneric(IAttribute attribute, IGameplayAbilitySystem source, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache, Tag retentionGroup,
            AttributeValue value)
        {
            var cav = GenerateNull();
            cav.Initialize(attribute, source, cache, retentionGroup, value);

            return cav;
        }

        public static CachedAttributeValue Generate(AttributeBlueprint blueprint)
        {
            var cav = new CachedAttributeValue(blueprint);
            return cav;
        }
        
        private CachedAttributeValue(AttributeBlueprint blueprint)
        {
            Blueprint = blueprint;
        }

        public bool Initialize(IAttribute attribute, IGameplayAbilitySystem source, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return Blueprint is not null
                   && Initialize(
                       attribute, source, cache,
                       Blueprint.SetElement.RetentionGroup,
                       Blueprint.GetInitialValue(cache)
                   );
        }

        // Stored seed for blueprint-less (generic) attributes — for those, Recompute can't
        // call Blueprint.GetActiveValue, so we keep the construction-time seed here.
        // Blueprinted attributes ignore this entirely.
        private AttributeValue genericRootValue;

        private bool Initialize(IAttribute attribute, IGameplayAbilitySystem source, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache, Tag retentionGroup,
            AttributeValue value)
        {
            // Root is identity-only — it tags the "from this source/attribute" lineage of
            // synthesised modifications generated downstream (auto-scale, regulation refresh,
            // etc.) but does NOT participate in the retained list. The starting point of
            // every recompute is Blueprint.GetActiveValue (or the stored generic seed below)
            // so the blueprint-resolved base value is always re-derived fresh.
            root = IAttributeImpactCarrier.GenerateSourceDerivation(
                source, attribute,
                retentionGroup,
                new List<Tag>() { Tags.DisallowImpact });

            if (Blueprint is null) genericRootValue = value;

            Recompute(cache);
            return true;
        }

        /// <summary>
        /// Clamp <see cref="ActiveValue"/> to the element's overflow bounds (single source of
        /// truth lives on <see cref="AttributeOverflowData.Clamp"/> so the drawer's live input
        /// clamp and this runtime clamp can never disagree).
        /// </summary>
        public void ApplyBounds()
        {
            if (!(Blueprint?.SetElement.Constraints.AutoClamp ?? false)) return;

            ActiveValue = Blueprint.SetElement.Overflow.Clamp(ActiveValue);
        }

        public void EnforceScaling(WorkerContext ctx)
        {
            if (Blueprint is null) return;
            
            if (!Blueprint.SetElement.Constraints.AutoScaleWithBase || ctx.Change.Value.BaseValue == 0) return;
            float oldBase = ActiveValue.BaseValue - ctx.Change.Value.BaseValue;
            if (Mathf.Approximately(oldBase, 0f)) return;
            float proportion = ctx.Change.Value.BaseValue / oldBase;  // change / oldBase
            
            float delta = proportion * ActiveValue.CurrentValue;

            var derivation = IAttributeImpactCarrier.GenerateSourceDerivation(
                ctx.Change.Value, Tags.IgnoreRetention, new List<Tag>() { Tags.AllowImpact });
            var scaleAmount = new SourcedModifiedAttributeValue(derivation, delta, 0f, false);
            ctx.ActionQueue.Enqueue(new ModifyAttributeAction(
                ctx.System, ctx.Change.Value.Carrier.GetAttribute(), scaleAmount, false)
            );
        }

        // ════════════════════════════════════════════════════════════════════════
        // Recompute pipeline (refactor step 4 — additive)
        //
        // The "modify → recompute everything" model. ApplyCarrier appends to the
        // retention list per the retention rule, then Recompute walks Root + retained
        // carriers and rebuilds DirtyValue + ActiveValue from scratch in op-precedence
        // order: Add → Multiply → FlatBonus → Override.
        //
        // Reversal is automatic for retained carriers: removing one and recomputing
        // re-derives ActiveValue without its contribution. See ICarrierApplication +
        // GameplayEffectImpact.ReverseImpactOnRemoval for the periodic-tick edge case
        // that still needs manual TrackedImpact reversal.
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Apply path. The carrier's mode (see <see cref="ICarrierApplication.IsSustainedModifier"/>)
        /// decides what happens:
        ///
        /// <list type="bullet">
        ///   <item><b>Mode A — sustained modifier</b>: appended to <see cref="retainedGroups"/>
        ///   and folded into a fresh <see cref="Recompute"/>. Re-applying the same instance
        ///   is an idempotent refresh (no stacking).</item>
        ///
        ///   <item><b>Mode B — discrete impact</b>: split by lifecycle —
        ///   <list type="bullet">
        ///     <item>Durational tick-delivered carrier → <b>no-op on apply</b>. The apply
        ///     event itself carries nothing; every commit flows through <see cref="TickCarrier"/>
        ///     (TickOnApplication and/or periodic ticks).</item>
        ///     <item>Instant carrier / synthetic <see cref="SourceAttributeImpact"/> delta /
        ///     manual ConstantImpacter wrapper → committed one-shot right now via
        ///     <see cref="ApplyOneShotImpacters"/>; there is no tick path that will commit
        ///     for these, so the apply event IS the commit.</item>
        ///   </list></item>
        /// </list>
        /// </summary>
        public void ApplyCarrier(IAttributeImpactCarrier carrier, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            if (carrier is null) return;

            // Single retention predicate — no touchesBase, no reversal-on-removal coupling.
            // Only durational, non-tick-delivered carriers are sustained modifiers.
            bool retain = carrier is ICarrierApplication app && app.IsSustainedModifier();

            if (retain)
            {
                // Idempotency: re-applying the SAME sustained-modifier instance is a
                // refresh, not a stack. (A distinct re-application — AppendNewContainer —
                // produces a distinct spec instance, which is a genuine new entry.)
                foreach (var existingGroup in retainedGroups.Values)
                {
                    if (existingGroup.Carriers.Contains(carrier))
                    {
                        Recompute(cache);
                        return;
                    }
                }

                var key = carrier.GetRetentionGroup();
                if (!retainedGroups.TryGetValue(key, out var group))
                {
                    group = new RetainedGroup(key);
                    retainedGroups[key] = group;
                }
                group.Carriers.Add(carrier);

                Recompute(cache);
                return;
            }

            // ─── Mode B ──────────────────────────────────────────────────────
            // A durational tick-delivered carrier carries NO impact on its apply
            // event — TickCarrier owns every commit. Anything else (instant carrier,
            // synthetic SourceAttributeImpact, manual ConstantImpacter wrapper) has
            // no tick path, so the apply event is its one and only commit.
            bool isDurationalTickDelivered = carrier is ICarrierApplication app2
                                             && app2.GetDurationPolicy() != EEffectDurationPolicy.Instant;
            if (isDurationalTickDelivered) return;

            ApplyOneShotImpacters(carrier);
        }

        /// <summary>
        /// Tick path. Commits the carrier's impacters one-shot into
        /// <see cref="committedOneShotDelta"/>. This is the ONLY commit path for
        /// tick-delivered (Mode B) carriers — TickOnApplication and periodic ticks both
        /// route here.
        ///
        /// <para>The retained-carrier guard is purely defensive: a sustained modifier
        /// (Mode A) can never be tick-delivered, so it should never reach this path. If a
        /// caller ticks a retained carrier anyway, we skip rather than double-count its
        /// already-in-Recompute contribution.</para>
        /// </summary>
        public void TickCarrier(IAttributeImpactCarrier carrier, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            if (carrier is null) return;

            foreach (var existingGroup in retainedGroups.Values)
            {
                if (existingGroup.Carriers.Contains(carrier)) return;
            }

            ApplyOneShotImpacters(carrier);
        }

        /// <summary>
        /// Remove a carrier from the retained list and recompute. Removal-driven reversal
        /// of the carrier's Multiply / FlatBonus / Override contribution is automatic — no
        /// extra step needed because the recompute pass simply omits it. For periodic-tick
        /// contributions committed past the impacter list during the carrier's lifetime,
        /// the caller (typically <see cref="AbstractEffectContainer.OnRemove"/>) is
        /// responsible for enqueuing a synthetic reversal carrier built from
        /// <see cref="TrackedImpact"/>.
        /// </summary>
        public void RemoveCarrier(IAttributeImpactCarrier carrier, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            if (carrier is null) return;

            var key = carrier.GetRetentionGroup();
            if (retainedGroups.TryGetValue(key, out var group))
            {
                group.Carriers.Remove(carrier);
                if (group.Carriers.Count == 0) retainedGroups.Remove(key);
            }

            Recompute(cache);
        }

        /// <summary>
        /// Walk Root + every alive retained carrier's impacters and rebuild
        /// <see cref="DirtyValue"/> + <see cref="ActiveValue"/> from scratch in
        /// op-precedence order, then apply bounds + rounding.
        /// </summary>
        public AttributeValue Recompute(IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            // Starting point: blueprint's resolved active value for blueprinted attributes
            // (re-runs the cached scaler pipeline so dependent attributes reflect their
            // current sources), or the construction-time seed for generic attributes.
            var rootValue = Blueprint is not null ? Blueprint.GetActiveValue(cache) : genericRootValue;
            float curr  = rootValue.CurrentValue;
            float baseV = rootValue.BaseValue;

            // Materialize the live impacter set once. Dead carriers (e.g. nullified) are
            // skipped — they remain in the retained list for retention-group identity but
            // contribute zero to the recompute.
            var impacters = new List<(IAttributeImpactCarrier carrier, IAttributeImpacter impacter)>();
            foreach (var group in retainedGroups.Values)
            {
                foreach (var c in group.Carriers)
                {
                    // No DerivationAlive() filter — carriers in retainedGroups are by
                    // definition alive contributors. Lifecycle owners (e.g. AbstractEffectContainer.
                    // OnRemove → RemoveAttributeDerivation) are responsible for removing the
                    // carrier when its contribution should stop. This eliminates "zombie"
                    // entries that linger but contribute nothing AND fixes the AppendNewContainer
                    // bug where DerivationAlive on GameplayEffectSpec only matched the single
                    // container returned by TryGetEffectContainer (singular), so all but one
                    // appended spec were silently filtered out.
                    if (c is null) continue;
                    var list = c.GetImpacters();
                    if (list == null) continue;
                    foreach (var imp in list)
                    {
                        if (imp != null) impacters.Add((c, imp));
                    }
                }
            }

            // ─── Add ─────────────────────────────────────────────────────────────
            foreach (var (c, imp) in impacters)
            {
                if (imp.Operation != ECalculationOperation.Add) continue;
                float v = imp.Evaluate(c);
                if (imp.TargetSlot is EAttributeTarget.Current or EAttributeTarget.CurrentAndBase) curr += v;
                if (imp.TargetSlot is EAttributeTarget.Base    or EAttributeTarget.CurrentAndBase) baseV += v;
            }

            DirtyValue = new AttributeValue(curr, baseV);

            // ─── Multiply ────────────────────────────────────────────────────────
            // Two paths per slot:
            //   • Multiplicative impacters compound: value *= eval, value *= eval, …
            //   • AdditiveBonus impacters pool their (eval - 1) bonuses and apply ONE
            //     final multiplication of (1 + Σ bonus). Two ×1.5 (i.e. +50%) AdditiveBonus
            //     impacters → ×2.0, not ×2.25. The two pools coexist; multiplicative ones
            //     run first to keep the math associative.
            float curBonusSum  = 0f;
            float baseBonusSum = 0f;
            foreach (var (c, imp) in impacters)
            {
                if (imp.Operation != ECalculationOperation.Multiply) continue;
                float v = imp.Evaluate(c);
                bool coversCurr = imp.TargetSlot is EAttributeTarget.Current or EAttributeTarget.CurrentAndBase;
                bool coversBase = imp.TargetSlot is EAttributeTarget.Base    or EAttributeTarget.CurrentAndBase;

                if (imp.MultiplyBehaviour == EMultiplyBehaviour.AdditiveBonus)
                {
                    if (coversCurr) curBonusSum  += (v - 1f);
                    if (coversBase) baseBonusSum += (v - 1f);
                }
                else
                {
                    if (coversCurr) curr  *= v;
                    if (coversBase) baseV *= v;
                }
            }
            if (curBonusSum  != 0f) curr  *= (1f + curBonusSum);
            if (baseBonusSum != 0f) baseV *= (1f + baseBonusSum);

            // ─── FlatBonus ───────────────────────────────────────────────────────
            foreach (var (c, imp) in impacters)
            {
                if (imp.Operation != ECalculationOperation.FlatBonus) continue;
                float v = imp.Evaluate(c);
                if (imp.TargetSlot is EAttributeTarget.Current or EAttributeTarget.CurrentAndBase) curr += v;
                if (imp.TargetSlot is EAttributeTarget.Base    or EAttributeTarget.CurrentAndBase) baseV += v;
            }

            // ─── Override (per-slot, lowest sort wins) ───────────────────────────
            // Two passes — one per slot — so a CurrentAndBase override and a Current-only
            // override compete fairly within the Current slot, and likewise for Base.
            (IAttributeImpactCarrier carrier, IAttributeImpacter impacter, int sort)? bestCurr = null;
            (IAttributeImpactCarrier carrier, IAttributeImpacter impacter, int sort)? bestBase = null;
            foreach (var (c, imp) in impacters)
            {
                if (imp.Operation != ECalculationOperation.Override) continue;
                bool coversCurr = imp.TargetSlot is EAttributeTarget.Current or EAttributeTarget.CurrentAndBase;
                bool coversBase = imp.TargetSlot is EAttributeTarget.Base    or EAttributeTarget.CurrentAndBase;
                if (coversCurr && (!bestCurr.HasValue || imp.SortOrder < bestCurr.Value.sort))
                    bestCurr = (c, imp, imp.SortOrder);
                if (coversBase && (!bestBase.HasValue || imp.SortOrder < bestBase.Value.sort))
                    bestBase = (c, imp, imp.SortOrder);
            }
            if (bestCurr.HasValue) curr  = bestCurr.Value.impacter.Evaluate(bestCurr.Value.carrier);
            if (bestBase.HasValue) baseV = bestBase.Value.impacter.Evaluate(bestBase.Value.carrier);

            // ─── Late: committed one-shot delta ──────────────────────────────────
            // Applied AFTER all retained-impacter aggregation so one-shot history
            // (instant taps, periodic-tick contributions) survives intact even if
            // retained sustained modifiers come and go around it. Removing a sustained
            // ×1.5 buff doesn't retroactively scale historical damage taken.
            curr  += committedOneShotDelta.CurrentValue;
            baseV += committedOneShotDelta.BaseValue;

            ActiveValue = new AttributeValue(curr, baseV);
            ApplyBounds();
            ApplyRounding();

            return ActiveValue;
        }

        /// <summary>
        /// Commit a non-retained carrier's impacters directly into <see cref="ActiveValue"/>
        /// AND record the post-bounds delta in <see cref="committedOneShotDelta"/> so future
        /// Recompute passes preserve the contribution. Used for instant Current-only
        /// carriers and for periodic ticks (via <see cref="TickCarrier"/>).
        /// </summary>
        private void ApplyOneShotImpacters(IAttributeImpactCarrier carrier)
        {
            var beforeActive = ActiveValue;

            float curr  = ActiveValue.CurrentValue;
            float baseV = ActiveValue.BaseValue;

            foreach (var imp in carrier.GetImpacters())
            {
                if (imp == null) continue;
                float v = imp.Evaluate(carrier);
                bool coversCurr = imp.TargetSlot is EAttributeTarget.Current or EAttributeTarget.CurrentAndBase;
                bool coversBase = imp.TargetSlot is EAttributeTarget.Base    or EAttributeTarget.CurrentAndBase;

                switch (imp.Operation)
                {
                    case ECalculationOperation.Add:
                    case ECalculationOperation.FlatBonus:
                        if (coversCurr) curr  += v;
                        if (coversBase) baseV += v;
                        break;
                    case ECalculationOperation.Multiply:
                        if (coversCurr) curr  *= v;
                        if (coversBase) baseV *= v;
                        break;
                    case ECalculationOperation.Override:
                        if (coversCurr) curr  = v;
                        if (coversBase) baseV = v;
                        break;
                }
            }

            ActiveValue = new AttributeValue(curr, baseV);
            ApplyBounds();
            ApplyRounding();

            // Record the POST-BOUNDS delta — Recompute applies this same delta as a
            // late additive layer, so it must reflect what was actually committed
            // (clamped to the attribute's overflow policy).
            committedOneShotDelta = new AttributeValue(
                committedOneShotDelta.CurrentValue + (ActiveValue.CurrentValue - beforeActive.CurrentValue),
                committedOneShotDelta.BaseValue    + (ActiveValue.BaseValue    - beforeActive.BaseValue));
        }

        /// <summary>
        /// Subtract a previously-committed delta from <see cref="committedOneShotDelta"/>.
        /// Used by container removal when <see cref="GameplayEffectImpact.ReverseImpactOnRemoval"/>
        /// is set on a tick-bearing effect — the spec's TrackedImpact total is what was
        /// actually committed during ticks, and removing it from history equals "undo
        /// every tick this effect ever ran." Triggers a Recompute so the change shows up.
        /// </summary>
        public void RollbackCommittedDelta(AttributeValue delta, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            committedOneShotDelta = new AttributeValue(
                committedOneShotDelta.CurrentValue - delta.CurrentValue,
                committedOneShotDelta.BaseValue    - delta.BaseValue);
            Recompute(cache);
        }

        public void ApplyRounding()
        {
            if (Blueprint is null) return;
            
            var newValue = Blueprint.SetElement.Constraints.RoundingMode switch
            {
                EAttributeRoundingPolicy.None => ActiveValue,
                EAttributeRoundingPolicy.ToFloor => new AttributeValue(
                    Mathf.Floor(ActiveValue.CurrentValue),
                    Mathf.Floor(ActiveValue.BaseValue)),
                EAttributeRoundingPolicy.ToCeil => new AttributeValue(
                    Mathf.Ceil(ActiveValue.CurrentValue),
                    Mathf.Ceil(ActiveValue.BaseValue)),
                EAttributeRoundingPolicy.Round => new AttributeValue(
                    Mathf.Round(ActiveValue.CurrentValue),
                    Mathf.Round(ActiveValue.BaseValue)),
                EAttributeRoundingPolicy.SnapTo => Mathf.Approximately(Blueprint.SetElement.Constraints.SnapInterval, 0f)
                    ? ActiveValue
                    : new AttributeValue(
                        Mathf.Round(ActiveValue.CurrentValue / Blueprint.SetElement.Constraints.SnapInterval) * Blueprint.SetElement.Constraints.SnapInterval,
                        Mathf.Round(ActiveValue.BaseValue / Blueprint.SetElement.Constraints.SnapInterval) * Blueprint.SetElement.Constraints.SnapInterval),
                _ => throw new ArgumentOutOfRangeException()
            };

            ActiveValue = newValue;
        }
    }

    /// <summary>
    /// Retention bucket — a list of carriers retained under one retention-group tag.
    /// Carriers in this list are by definition active contributors; the recompute pass
    /// walks them unconditionally. Lifecycle owners (containers, ability specs, etc.)
    /// must call <see cref="CachedAttributeValue.RemoveCarrier"/> when a carrier should
    /// stop contributing — there is no "alive" flag and no zombie state.
    ///
    /// <para>Group identity = <see cref="IAttributeImpactCarrier.GetRetentionGroup"/>.
    /// Within a group, carriers are stored in apply order; stacking semantics (one-per-tag,
    /// keep-strongest, etc.) live on the consumer side, not here — this is plain storage.</para>
    /// </summary>
    public class RetainedGroup
    {
        public Tag RetentionGroup;
        public List<IAttributeImpactCarrier> Carriers = new();

        public RetainedGroup(Tag retentionGroup)
        {
            RetentionGroup = retentionGroup;
        }
    }

    // RetainedCachedValue + CachedValueInfo retired (refactor step 10).
    // Their per-source delta-and-set bookkeeping was the source of the "I barely
    // understand how this works" feeling — replaced wholesale by RetainedGroup +
    // Recompute, which derives ActiveValue from Blueprint.GetActiveValue + the
    // alive carriers' impacters every modification cycle.
}
