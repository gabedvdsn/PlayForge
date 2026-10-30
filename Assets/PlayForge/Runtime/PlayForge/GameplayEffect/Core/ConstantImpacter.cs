namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A precomputed-magnitude impacter. Used for synthetic deltas where there's no scaler
    /// pipeline to evaluate — e.g. auto-scale propagation, tracked-impact reversal on
    /// container removal, or external systems that just want to push a number into the
    /// attribute aggregator while still being a first-class carrier.
    ///
    /// <para>Construct one impacter per (operation, slot) pair you want to contribute.
    /// To express a (current=+10, base=+5) Add delta as a single carrier, hand that carrier
    /// two <see cref="ConstantImpacter"/>s — one per slot.</para>
    /// </summary>
    public class ConstantImpacter : IAttributeImpacter
    {
        private readonly float magnitude;

        public ECalculationOperation Operation { get; }
        public EAttributeTarget      TargetSlot { get; }
        public int                   SortOrder { get; }
        public EMultiplyBehaviour    MultiplyBehaviour { get; }
        public Tag                   Group { get; }

        public ConstantImpacter(
            float magnitude,
            ECalculationOperation operation,
            EAttributeTarget targetSlot,
            int sortOrder = 0,
            EMultiplyBehaviour multiplyBehaviour = EMultiplyBehaviour.Multiplicative,
            Tag group = default)
        {
            this.magnitude    = magnitude;
            Operation         = operation;
            TargetSlot        = targetSlot;
            SortOrder         = sortOrder;
            MultiplyBehaviour = multiplyBehaviour;
            Group             = group.IsValid ? group : Tags.IgnoreRetention;
        }

        public float Evaluate(IAttributeImpactCarrier carrier) => magnitude;

        /// <summary>
        /// Freeze <paramref name="impacter"/>'s magnitude for <paramref name="carrier"/> into a
        /// constant with the same op, slot, sort order, multiply behaviour and group. Commits
        /// evaluate through these once, so the delta pre-change workers saw is the delta
        /// that commits — even for a randomized scaler.
        /// </summary>
        public static ConstantImpacter Snapshot(IAttributeImpacter impacter, IAttributeImpactCarrier carrier)
            => new(impacter.Evaluate(carrier), impacter.Operation, impacter.TargetSlot,
                impacter.SortOrder, impacter.MultiplyBehaviour, impacter.Group);
    }
}
