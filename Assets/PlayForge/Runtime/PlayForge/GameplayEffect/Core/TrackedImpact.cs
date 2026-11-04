namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What a carrier's impacts came to: their total, the last of them, and how many. It kept a node for every impact
    /// on a list nothing walked, so an effect that ticked kept an allocation for every tick for as long as it lasted
    /// (GAS-P6).
    /// </summary>
    public class TrackedImpact
    {
        public AttributeValue Total { get; private set; }
        public AttributeValue Last { get; private set; }
        public int Count { get; private set; }

        public void Add(AttributeValue value)
        {
            Total += value;
            Last = value;
            Count++;
        }
    }
}
