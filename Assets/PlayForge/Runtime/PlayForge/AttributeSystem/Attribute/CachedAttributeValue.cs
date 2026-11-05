using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Per-attribute runtime state under the two-kind impact model
    /// (IMPACT_MODEL_REFACTOR.md §1–§2):
    ///
    /// <list type="bullet">
    ///   <item><b>Modifiers</b> (transient, derived) — registered via
    ///   <see cref="RegisterModifier"/> while their carrier lives, re-evaluated on every
    ///   <see cref="Recompute"/>, reversed exactly by <see cref="UnregisterModifier"/>.</item>
    ///
    ///   <item><b>Commits</b> (permanent) — applied via <see cref="CommitImpact"/>; the
    ///   post-bounds delta is recorded per retention group in <see cref="committedByGroup"/>
    ///   and NEVER reversed.</item>
    /// </list>
    ///
    /// <para>The formula (EFFECT_PIPELINE.md §2.1):
    /// <c>ActiveValue = ConstrainValue( Override( FlatBonus( Multiply( Add( root ) ) ) + Σ committedByGroup ) )</c>.
    /// Committed history lands where it was measured, after the % modifiers, so they don't scale it:
    /// 50 base × 1.5 aura + 10 tome = 85, and a 30 hit under the aura takes 30 (Decision 4).</para>
    ///
    /// <para>Reproducibility invariant: <see cref="ActiveValue"/> is always exactly what
    /// <see cref="Recompute"/> produces from current state — every mutation path ends in a
    /// recompute. There is no second bookkeeping system to drift.</para>
    /// </summary>
    public class CachedAttributeValue
    {
        public AttributeValue ActiveValue { get; private set; }

        // The last recompute's value before bounds and rounding, where commits land, so where a rescale aims from (GAS-37)
        private AttributeValue unconstrained;

        /// <summary>
        /// Intermediate aggregation snapshot — the value AFTER the Add pass but BEFORE
        /// Multiply / FlatBonus / Override. Diagnostics only. Updated by <see cref="Recompute"/>.
        /// </summary>
        public AttributeValue DirtyValue { get; private set; }

        /// <summary>The basest attribute value derived from the entity's attribute set.</summary>
        public AttributeValue RootValue => Blueprint.RootValue;

        public AttributeBlueprint Blueprint { get; }

        private IAttributeImpactCarrier root;
        public IAttributeImpactCarrier Root => root;

        // ─── Modifiers (transient, derived) ──────────────────────────────────────
        // Registration order preserved; one entry per (carrier, impacter). A carrier
        // re-registering REPLACES its previous entries (idempotent refresh — used by
        // stacking, which re-registers after Stack(n) so stack-aware magnitudes take).
        private sealed class ModifierEntry
        {
            public IAttributeImpactCarrier Carrier;
            public IAttributeImpacter Impacter;
            public Tag Group;
        }

        private readonly List<ModifierEntry> modifiers = new();

        // Entries a carrier's removal let go, for the next registration to reuse; a refresh built new ones (GAS-P4)
        private readonly Stack<ModifierEntry> spareEntries = new();

        // Recompute's multipliers that pool their bonus, kept between recomputes: a new list each time allocated on
        // every recompute, and every tick that commits recomputes (GAS-P4)
        private readonly List<(ModifierEntry entry, float curBonus, float baseBonus)> pooled = new();

        // ─── Committed history (permanent), attributed per retention group ───────
        private readonly Dictionary<Tag, AttributeValue> committedByGroup = new();
        private readonly List<Tag> committedOrder = new();

        // ─── Per-group attribution of the CURRENT ActiveValue (§2.3) ─────────────
        // Rebuilt on every Recompute: slices always sum exactly to ActiveValue.
        private readonly Dictionary<Tag, AttributeValue> attribution = new();
        private readonly List<Tag> attributionOrder = new();

        /// <summary>Aggregate committed history across all groups (diagnostics / legacy).</summary>
        public AttributeValue CommittedOneShotDelta
        {
            get
            {
                float c = 0f, b = 0f;
                foreach (var v in committedByGroup.Values) { c += v.CurrentValue; b += v.BaseValue; }
                return new AttributeValue(c, b);
            }
        }

        /// <summary>
        /// Legacy diagnostic projection of the modifier collection in the old
        /// retention-group shape. Allocates — do not use in hot paths. The Debugger
        /// reads <see cref="GetBreakdown"/>, as the GAS Debugger read this (ED-4).
        /// </summary>
        public IReadOnlyDictionary<Tag, RetainedGroup> RetainedGroups
        {
            get
            {
                var view = new Dictionary<Tag, RetainedGroup>();
                foreach (var m in modifiers)
                {
                    if (!view.TryGetValue(m.Group, out var g))
                    {
                        g = new RetainedGroup(m.Group);
                        view[m.Group] = g;
                    }
                    if (!g.Carriers.Contains(m.Carrier)) g.Carriers.Add(m.Carrier);
                }
                return view;
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRUCTION / INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════════════

        public static CachedAttributeValue GenerateNull() => new(null);

        public static CachedAttributeValue GenerateGeneric(IAttribute attribute, IGameplayAbilitySystem source, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache, Tag retentionGroup,
            AttributeValue value)
        {
            var cav = GenerateNull();
            cav.Initialize(attribute, source, cache, retentionGroup, value);
            return cav;
        }

        public static CachedAttributeValue Generate(AttributeBlueprint blueprint) => new(blueprint);

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
        private AttributeValue genericRootValue;

        // Group the root value is attributed to. Blueprinted attributes use the element's
        // RetentionGroup ("base" slice of the pool); generic ones use the group passed in.
        private Tag rootGroup;

        private bool Initialize(IAttribute attribute, IGameplayAbilitySystem source, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache, Tag retentionGroup,
            AttributeValue value)
        {
            // Root is identity-only — it tags the "from this source/attribute" lineage of
            // synthesised modifications generated downstream (auto-scale, regulation
            // refresh, etc.). The starting point of every recompute is
            // Blueprint.GetActiveValue (or the stored generic seed) so the blueprint-
            // resolved base value is always re-derived fresh.
            root = IAttributeImpactCarrier.GenerateSourceDerivation(
                source, attribute,
                retentionGroup,
                new List<Tag>() { Tags.DisallowImpact });

            rootGroup = retentionGroup.IsValid ? retentionGroup : Tags.DEFAULT;
            if (Blueprint is null) genericRootValue = value;

            Recompute(cache);
            return true;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // MODIFIERS — register / unregister (the ONLY reversal mechanism)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Register (or refresh) a carrier's modifier impacters. Re-registering the same
        /// carrier replaces its previous entries — an idempotent refresh, not a stack.
        /// Distinct carrier instances coexist (AppendNewContainer stacking-by-coexistence).
        /// </summary>
        public void RegisterModifier(IAttributeImpactCarrier carrier, IReadOnlyList<IAttributeImpacter> impacters, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            if (carrier is null) return;

            RemoveCarrier(carrier);

            if (impacters != null)
            {
                // By index: a foreach through the interface boxed the list's enumerator
                for (int i = 0; i < impacters.Count; i++)
                {
                    var imp = impacters[i];
                    if (imp == null) continue;

                    var entry = spareEntries.Count > 0 ? spareEntries.Pop() : new ModifierEntry();
                    entry.Carrier = carrier;
                    entry.Impacter = imp;
                    entry.Group = imp.Group.IsValid ? imp.Group : Tags.IgnoreRetention;
                    modifiers.Add(entry);
                }
            }

            Recompute(cache);
        }

        /// <summary>
        /// Takes a carrier's entries off, in one pass that keeps the rest in order, and keeps them spare. A RemoveAll
        /// lambda captured the carrier, allocating on every call (GAS-P4).
        /// </summary>
        private int RemoveCarrier(IAttributeImpactCarrier carrier)
        {
            int kept = 0;
            for (int i = 0; i < modifiers.Count; i++)
            {
                var m = modifiers[i];
                if (ReferenceEquals(m.Carrier, carrier))
                {
                    m.Carrier = null;
                    m.Impacter = null;
                    spareEntries.Push(m);
                    continue;
                }
                modifiers[kept++] = m;
            }

            int removed = modifiers.Count - kept;
            if (removed > 0) modifiers.RemoveRange(kept, removed);
            return removed;
        }

        /// <summary>
        /// Remove every modifier entry owned by <paramref name="carrier"/> and recompute.
        /// The recompute simply omits its impacters — that IS the reversal, exact by
        /// construction (no delta arithmetic, no rollback bookkeeping).
        /// </summary>
        public bool UnregisterModifier(IAttributeImpactCarrier carrier, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            if (carrier is null) return false;

            int removed = RemoveCarrier(carrier);
            Recompute(cache);
            return removed > 0;
        }

        public bool HasModifier(IAttributeImpactCarrier carrier)
        {
            if (carrier is null) return false;
            foreach (var m in modifiers)
            {
                if (ReferenceEquals(m.Carrier, carrier)) return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // COMMITS — permanent, per-group history
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Apply the given impacters as a PERMANENT commit. Each impacter runs as a
        /// sequential mini-step against the live value (op → bounds → rounding), and the
        /// post-constraint delta it produced is recorded into its retention group's
        /// committed history. A final <see cref="Recompute"/> then rebuilds ActiveValue
        /// canonically (committed history lands after the modifiers, where it was measured).
        ///
        /// <para>Commits are never reversed. There is deliberately no rollback path.</para>
        /// </summary>
        public void CommitImpact(IAttributeImpactCarrier carrier, IReadOnlyList<IAttributeImpacter> impacters, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            if (carrier is null || impacters == null || impacters.Count == 0)
            {
                // Zero-impact pulse (attribute-set init, empty synthetic delta): still
                // recompute so callers get a canonical post-call state.
                Recompute(cache);
                return;
            }

            var working = ActiveValue;
            for (int i = 0; i < impacters.Count; i++)
            {
                var imp = impacters[i];
                if (imp == null) continue;

                // Record the POST-CONSTRAINT delta this step actually produced — a +50
                // heal clamped to +30 commits +30, keeping history reproducible.
                var stepped = ConstrainValue(ApplyCommitStep(working, imp, imp.Evaluate(carrier)));
                var delta = stepped - working;
                working = stepped;

                if (delta.CurrentValue != 0f || delta.BaseValue != 0f)
                {
                    var group = imp.Group.IsValid ? imp.Group : Tags.IgnoreRetention;
                    AccumulateCommitted(group, delta);
                }
            }

            Recompute(cache);
        }

        /// <summary>
        /// The INCOMING delta a commit of these impacters proposes: each op runs in order
        /// against the live value with no bounds or rounding, and nothing is written.
        /// Pre-change workers adjust this delta; <see cref="CommitImpact"/> constrains
        /// whatever is finally committed (armor acts on incoming damage, the clamp after).
        /// </summary>
        public AttributeValue PreviewCommit(IAttributeImpactCarrier carrier, IReadOnlyList<IAttributeImpacter> impacters)
        {
            if (carrier is null || impacters == null) return default;

            var working = ActiveValue;
            for (int i = 0; i < impacters.Count; i++)
            {
                var imp = impacters[i];
                if (imp == null) continue;
                working = ApplyCommitStep(working, imp, imp.Evaluate(carrier));
            }
            return working - ActiveValue;
        }

        /// <summary>One unconstrained commit step: the impacter's op, magnitude <paramref name="v"/>, on its slot(s).</summary>
        private static AttributeValue ApplyCommitStep(AttributeValue value, IAttributeImpacter imp, float v)
        {
            float curr = value.CurrentValue;
            float baseV = value.BaseValue;
            bool coversCurr = imp.TargetSlot is EAttributeTarget.Current or EAttributeTarget.CurrentAndBase;
            bool coversBase = imp.TargetSlot is EAttributeTarget.Base or EAttributeTarget.CurrentAndBase;

            switch (imp.Operation)
            {
                case ECalculationOperation.Add:
                case ECalculationOperation.FlatBonus:
                    if (coversCurr) curr += v;
                    if (coversBase) baseV += v;
                    break;
                case ECalculationOperation.Multiply:
                    if (coversCurr) curr *= v;
                    if (coversBase) baseV *= v;
                    break;
                case ECalculationOperation.Override:
                    if (coversCurr) curr = v;
                    if (coversBase) baseV = v;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return new AttributeValue(curr, baseV);
        }

        private void AccumulateCommitted(Tag group, AttributeValue delta)
        {
            if (!committedByGroup.TryGetValue(group, out var existing))
            {
                committedOrder.Add(group);
                existing = default;
            }
            committedByGroup[group] = new AttributeValue(
                existing.CurrentValue + delta.CurrentValue,
                existing.BaseValue + delta.BaseValue);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // RECOMPUTE — the single source of truth for ActiveValue
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Rebuild <see cref="DirtyValue"/>, <see cref="ActiveValue"/> and the per-group
        /// attribution from scratch:
        /// root → Add → Multiply → FlatBonus → committed history → Override → bounds
        /// → rounding.
        /// </summary>
        public AttributeValue Recompute(IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            attribution.Clear();
            attributionOrder.Clear();

            // ─── Baseline: root ──────────────────────────────────────────────────
            var rootValue = Blueprint is not null ? Blueprint.GetActiveValue(cache) : genericRootValue;
            Attribute_Accumulate(rootGroup.IsValid ? rootGroup : Tags.DEFAULT, rootValue);

            float curr = rootValue.CurrentValue;
            float baseV = rootValue.BaseValue;

            // ─── Add ─────────────────────────────────────────────────────────────
            foreach (var m in modifiers)
            {
                var imp = m.Impacter;
                if (imp.Operation != ECalculationOperation.Add) continue;
                float v = imp.Evaluate(m.Carrier);
                float dc = 0f, db = 0f;
                if (imp.TargetSlot is EAttributeTarget.Current or EAttributeTarget.CurrentAndBase) { curr += v; dc = v; }
                if (imp.TargetSlot is EAttributeTarget.Base or EAttributeTarget.CurrentAndBase) { baseV += v; db = v; }
                Attribute_Accumulate(m.Group, new AttributeValue(dc, db));
            }

            DirtyValue = new AttributeValue(curr, baseV);

            // ─── Multiply ────────────────────────────────────────────────────────
            // Multiplicative impacters compound sequentially (each credited with the
            // value swing its own factor produced); AdditiveBonus impacters pool their
            // (eval − 1) bonuses into ONE final ×(1 + Σ), credited proportionally.
            float curBonusSum = 0f, baseBonusSum = 0f;
            pooled.Clear();

            foreach (var m in modifiers)
            {
                var imp = m.Impacter;
                if (imp.Operation != ECalculationOperation.Multiply) continue;
                float v = imp.Evaluate(m.Carrier);
                bool coversCurr = imp.TargetSlot is EAttributeTarget.Current or EAttributeTarget.CurrentAndBase;
                bool coversBase = imp.TargetSlot is EAttributeTarget.Base or EAttributeTarget.CurrentAndBase;

                if (imp.MultiplyBehaviour == EMultiplyBehaviour.AdditiveBonus)
                {
                    float cb = coversCurr ? v - 1f : 0f;
                    float bb = coversBase ? v - 1f : 0f;
                    curBonusSum += cb;
                    baseBonusSum += bb;
                    pooled.Add((m, cb, bb));
                }
                else
                {
                    float dc = 0f, db = 0f;
                    if (coversCurr) { dc = curr * (v - 1f); curr *= v; }
                    if (coversBase) { db = baseV * (v - 1f); baseV *= v; }
                    Attribute_Accumulate(m.Group, new AttributeValue(dc, db));
                }
            }

            if (curBonusSum != 0f || baseBonusSum != 0f)
            {
                float poolDeltaCurr = curBonusSum != 0f ? curr * curBonusSum : 0f;
                float poolDeltaBase = baseBonusSum != 0f ? baseV * baseBonusSum : 0f;
                curr += poolDeltaCurr;
                baseV += poolDeltaBase;

                foreach (var (entry, cb, bb) in pooled)
                {
                    float shareC = curBonusSum != 0f ? poolDeltaCurr * (cb / curBonusSum) : 0f;
                    float shareB = baseBonusSum != 0f ? poolDeltaBase * (bb / baseBonusSum) : 0f;
                    Attribute_Accumulate(entry.Group, new AttributeValue(shareC, shareB));
                }
            }

            // ─── FlatBonus ───────────────────────────────────────────────────────
            foreach (var m in modifiers)
            {
                var imp = m.Impacter;
                if (imp.Operation != ECalculationOperation.FlatBonus) continue;
                float v = imp.Evaluate(m.Carrier);
                float dc = 0f, db = 0f;
                if (imp.TargetSlot is EAttributeTarget.Current or EAttributeTarget.CurrentAndBase) { curr += v; dc = v; }
                if (imp.TargetSlot is EAttributeTarget.Base or EAttributeTarget.CurrentAndBase) { baseV += v; db = v; }
                Attribute_Accumulate(m.Group, new AttributeValue(dc, db));
            }

            // ─── Committed history ───────────────────────────────────────────────
            // Where a commit was measured, after the modifiers, so it lands as it was taken: a 30 hit under a ×1.5
            // aura takes 30. Joining the baseline, it went through the multipliers and took 45 (GAS-15, Decision 4).
            // Before the override, so an override still pins the value
            foreach (var group in committedOrder)
            {
                var committed = committedByGroup[group];
                curr += committed.CurrentValue;
                baseV += committed.BaseValue;
                Attribute_Accumulate(group, committed);
            }

            // ─── Override (per-slot, lowest sort wins) ───────────────────────────
            ModifierEntry bestCurr = null, bestBase = null;
            foreach (var m in modifiers)
            {
                var imp = m.Impacter;
                if (imp.Operation != ECalculationOperation.Override) continue;
                bool coversCurr = imp.TargetSlot is EAttributeTarget.Current or EAttributeTarget.CurrentAndBase;
                bool coversBase = imp.TargetSlot is EAttributeTarget.Base or EAttributeTarget.CurrentAndBase;
                if (coversCurr && (bestCurr == null || imp.SortOrder < bestCurr.Impacter.SortOrder)) bestCurr = m;
                if (coversBase && (bestBase == null || imp.SortOrder < bestBase.Impacter.SortOrder)) bestBase = m;
            }
            if (bestCurr != null)
            {
                float v = bestCurr.Impacter.Evaluate(bestCurr.Carrier);
                Attribute_Accumulate(bestCurr.Group, new AttributeValue(v - curr, 0f));
                curr = v;
            }
            if (bestBase != null)
            {
                float v = bestBase.Impacter.Evaluate(bestBase.Carrier);
                Attribute_Accumulate(bestBase.Group, new AttributeValue(0f, v - baseV));
                baseV = v;
            }

            // ─── Constraints ─────────────────────────────────────────────────────
            var preConstraint = new AttributeValue(curr, baseV);
            unconstrained = preConstraint;
            ActiveValue = ConstrainValue(preConstraint);

            // Bounds/rounding shift is attributed to the unattributed slice so the
            // breakdown always sums exactly to ActiveValue.
            var constraintShift = ActiveValue - preConstraint;
            if (constraintShift.CurrentValue != 0f || constraintShift.BaseValue != 0f)
            {
                Attribute_Accumulate(Tags.IgnoreRetention, constraintShift);
            }

            return ActiveValue;
        }

        private void Attribute_Accumulate(Tag group, AttributeValue delta)
        {
            if (!attribution.TryGetValue(group, out var existing))
            {
                attributionOrder.Add(group);
                existing = default;
            }
            attribution[group] = new AttributeValue(
                existing.CurrentValue + delta.CurrentValue,
                existing.BaseValue + delta.BaseValue);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // PER-GROUP QUERY SURFACE (§2.3) — attribution only, never changes the math
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// This group's slice of <see cref="ActiveValue"/> as of the last recompute:
        /// its committed history plus the attributed effect of its modifiers.
        /// Slices across <see cref="GetBreakdown"/> sum exactly to ActiveValue.
        /// </summary>
        public AttributeValue GetGroupValue(Tag group)
            => attribution.TryGetValue(group, out var v) ? v : default;

        public IReadOnlyList<GroupContribution> GetBreakdown()
        {
            var list = new List<GroupContribution>(attributionOrder.Count);
            foreach (var group in attributionOrder)
            {
                list.Add(new GroupContribution(group, attribution[group]));
            }
            return list;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // CONSTRAINTS
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Apply the element's overflow clamp + rounding policy to a value.</summary>
        private AttributeValue ConstrainValue(AttributeValue value)
        {
            if (Blueprint is null) return value;

            if (Blueprint.SetElement.Constraints.AutoClamp)
            {
                value = Blueprint.SetElement.Overflow.Clamp(value);
            }

            value = Blueprint.SetElement.Constraints.RoundingMode switch
            {
                EAttributeRoundingPolicy.None => value,
                EAttributeRoundingPolicy.ToFloor => new AttributeValue(
                    Mathf.Floor(value.CurrentValue),
                    Mathf.Floor(value.BaseValue)),
                EAttributeRoundingPolicy.ToCeil => new AttributeValue(
                    Mathf.Ceil(value.CurrentValue),
                    Mathf.Ceil(value.BaseValue)),
                EAttributeRoundingPolicy.Round => new AttributeValue(
                    Mathf.Round(value.CurrentValue),
                    Mathf.Round(value.BaseValue)),
                EAttributeRoundingPolicy.SnapTo => Mathf.Approximately(Blueprint.SetElement.Constraints.SnapInterval, 0f)
                    ? value
                    : new AttributeValue(
                        Mathf.Round(value.CurrentValue / Blueprint.SetElement.Constraints.SnapInterval) * Blueprint.SetElement.Constraints.SnapInterval,
                        Mathf.Round(value.BaseValue / Blueprint.SetElement.Constraints.SnapInterval) * Blueprint.SetElement.Constraints.SnapInterval),
                _ => throw new ArgumentOutOfRangeException()
            };

            return value;
        }

        /// <summary>Clamp <see cref="ActiveValue"/> to the element's overflow bounds.</summary>
        public void ApplyBounds()
        {
            if (!(Blueprint?.SetElement.Constraints.AutoClamp ?? false)) return;
            ActiveValue = Blueprint.SetElement.Overflow.Clamp(ActiveValue);
        }

        public void ApplyRounding()
        {
            if (Blueprint is null) return;
            ActiveValue = ConstrainValue(ActiveValue);
        }

        public void EnforceScaling(WorkerContext ctx)
        {
            if (Blueprint is null) return;

            if (!Blueprint.SetElement.Constraints.AutoScaleWithBase || ctx.Change.Value.BaseValue == 0) return;
            float oldBase = ActiveValue.BaseValue - ctx.Change.Value.BaseValue;
            if (Mathf.Approximately(oldBase, 0f)) return;
            float proportion = ctx.Change.Value.BaseValue / oldBase;  // change / oldBase

            // A share of Current before the bounds, where the commit lands, so the bounds clamp the result once. Taken
            // from the clamped Current, under ZeroToBase an 80/100 attribute losing half its Base was clamped to 50 and
            // lost only 25 under the clamp, which held it at 50, not 40 (GAS-37). With nothing clamped, the two are one
            float delta = proportion * unconstrained.CurrentValue;

            // The system's own change, so typed DisallowImpact, as the pulse each attribute starts with is. Typed
            // AllowImpact, it passed every type filter, and a worker taking unworkable impacts acted on it (GAS-35, Decision 9)
            //
            // Carried for whoever changed Base, by a carrier kept for them, and queued in an action this attribute keeps.
            // Each rescale made a new carrier, its list and a new action (GAS-P9)
            var changed = ctx.Change.Value.Carrier;
            var derivation = RescaleCarriers.For(changed.GetOrigin().GetSource(), changed.GetAttribute(),
                Tags.IgnoreRetention, RescaleImpactTypes);
            var scaleAmount = new SourcedModifiedAttributeValue(derivation, delta, 0f, false);
            ctx.ActionQueue.Enqueue(ModifyAttributeAction.Rent(spareRescales ??= new Stack<ModifyAttributeAction>(),
                ctx.System, changed.GetAttribute(), scaleAmount, false));
        }

        // The carriers rescales are carried by, one table for every attribute, and the impact types each is made with
        private static readonly SourceCarriers RescaleCarriers = new();
        private static readonly List<Tag> RescaleImpactTypes = new() { Tags.DisallowImpact };

        // The actions this attribute's rescales are queued in, back here once the queue has run them
        private Stack<ModifyAttributeAction> spareRescales;
    }

    /// <summary>One group's slice of an attribute's ActiveValue (see <see cref="CachedAttributeValue.GetBreakdown"/>).</summary>
    public readonly struct GroupContribution
    {
        public readonly Tag Group;
        public readonly AttributeValue Value;

        public GroupContribution(Tag group, AttributeValue value)
        {
            Group = group;
            Value = value;
        }

        public override string ToString() => $"{Group}: {Value.CurrentValue}/{Value.BaseValue}";
    }

    /// <summary>
    /// Legacy diagnostic shape — a retention-group bucket of carriers. Kept for editor
    /// tooling that renders the old view; the runtime store is the modifier collection
    /// inside <see cref="CachedAttributeValue"/>.
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
}
