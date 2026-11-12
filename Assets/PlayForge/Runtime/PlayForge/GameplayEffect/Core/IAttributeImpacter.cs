namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A single magnitude-evaluation unit owned by an <see cref="IAttributeImpactCarrier"/>.
    /// One carrier may expose many impacters; the cached attribute aggregator walks them
    /// and applies their contributions in op-precedence order:
    /// <list type="number">
    ///   <item><see cref="ECalculationOperation.Add"/>        — summed first → DirtyValue.</item>
    ///   <item><see cref="ECalculationOperation.Multiply"/>   — composed via <see cref="MultiplyBehaviour"/>.</item>
    ///   <item><see cref="ECalculationOperation.FlatBonus"/>  — added after multiplies.</item>
    ///   <item><see cref="ECalculationOperation.Override"/>   — wins (resolved by <see cref="SortOrder"/>).</item>
    /// </list>
    ///
    /// <para><b>Reversal under recompute model</b> — retained carriers reverse automatically
    /// when removed (Recompute simply walks the new shorter retention list). One-shot
    /// committed contributions persist via <c>CachedAttributeValue.committedOneShotDelta</c>
    /// and are reversed only by an explicit synthetic-carrier rollback (see
    /// <c>GameplayEffectImpact.ReverseImpactOnRemoval</c>).</para>
    /// </summary>
    public interface IAttributeImpacter
    {
        /// <summary>How this impacter's magnitude folds into the aggregate.</summary>
        ECalculationOperation Operation { get; }

        /// <summary>Which slot of the <see cref="AttributeValue"/> the impact lands on.</summary>
        EAttributeTarget TargetSlot { get; }

        /// <summary>
        /// Tiebreaker within an operation. Lower runs first. For Override, the lowest sort
        /// wins; for Add / Multiply / FlatBonus the order is theoretically irrelevant but the
        /// field is kept so authoring can pin determinism (e.g. across reorders of effects
        /// that produce floating-point drift).
        /// </summary>
        int SortOrder { get; }

        /// <summary>
        /// Only meaningful when <see cref="Operation"/> is <see cref="ECalculationOperation.Multiply"/>.
        /// Controls how this multiply impacter composes with other multiply impacters acting
        /// on the same attribute slot.
        /// </summary>
        EMultiplyBehaviour MultiplyBehaviour => EMultiplyBehaviour.Multiplicative;

        /// <summary>
        /// Only meaningful when <see cref="Operation"/> is <see cref="ECalculationOperation.Add"/>: how this add combines with
        /// the others on the attribute, as its role says, a plain sum, or diminishing (TW-1).
        /// </summary>
        EAddBehaviour AddBehaviour => EAddBehaviour.AttributeRole;

        /// <summary>
        /// The calculation its value goes through as it commits, once for each hit: an instant impact's, each tick of a
        /// periodic one's. None, it lands as it's evaluated (TW-2).
        /// </summary>
        ValuePipeline Pipeline => null;

        /// <summary>
        /// Retention group this impacter's contribution is attributed to (per-group value
        /// breakdown — base vs bonus health, etc.). Attribution only; never changes the
        /// math. Default: unattributed.
        /// </summary>
        Tag Group => Tags.IgnoreRetention;

        /// <summary>
        /// Evaluate the impacter's magnitude. Pure: must be safe to call repeatedly during a
        /// recompute pass. The carrier is supplied so the impacter can reach its origin's
        /// level, captured attributes, target context, etc.
        /// </summary>
        float Evaluate(IAttributeImpactCarrier carrier);
    }

    /// <summary>
    /// Cross-carrier composition strategies for <see cref="ECalculationOperation.Multiply"/>
    /// impacters. This is intentionally distinct from per-effect stacking policy:
    /// stacking decides how the SAME effect re-applies stack against itself, while
    /// MultiplyBehaviour decides how DIFFERENT carriers' multiply contributions to the
    /// SAME attribute slot compose.
    /// </summary>
    /// <summary>How an Add impacter combines with the others on its attribute (TW-1).</summary>
    public enum EAddBehaviour
    {
        /// <summary>As the attribute's role says: a Resistance's adds diminish, every other role's sum.</summary>
        AttributeRole,

        /// <summary>A plain sum, whatever the role.</summary>
        Sum,

        /// <summary>On what's left, 1 − (1 − x)(1 − v), whatever the role, after the sums.</summary>
        Diminishing,
    }

    public enum EMultiplyBehaviour
    {
        /// <summary>
        /// Default. Each multiply impacter compounds: ×1.5, ×1.5 → ×2.25.
        /// Fastest path, matches the legacy GAS behaviour.
        /// </summary>
        Multiplicative,

        /// <summary>
        /// Treat each impacter's value as `1 + bonus`; sum the bonuses across all
        /// AdditiveBonus impacters in the same slot, then apply ONE multiplication of
        /// `(1 + Σ bonus)`. Two ×1.5 (i.e. +50%) impacters → ×2.0 instead of ×2.25.
        /// Use this for "diminishing-returns-style" buffs that should not exponentially
        /// stack.
        /// </summary>
        AdditiveBonus
    }
}
