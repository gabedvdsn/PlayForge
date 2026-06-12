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

        public ConstantImpacter(
            float magnitude,
            ECalculationOperation operation,
            EAttributeTarget targetSlot,
            int sortOrder = 0,
            EMultiplyBehaviour multiplyBehaviour = EMultiplyBehaviour.Multiplicative)
        {
            this.magnitude    = magnitude;
            Operation         = operation;
            TargetSlot        = targetSlot;
            SortOrder         = sortOrder;
            MultiplyBehaviour = multiplyBehaviour;
        }

        public float Evaluate(IAttributeImpactCarrier carrier) => magnitude;
    }
}
