namespace FarEmerald.PlayForge
{
    public struct IntValuePair
    {
        public int CurrentValue;
        public int MaxValue;

        public IntValuePair(int currentValue, int maxValue)
        {
            CurrentValue = currentValue;
            MaxValue = maxValue;
        }

        public IntValuePairClamped ToClamped()
        {
            return new IntValuePairClamped(CurrentValue, MaxValue);
        }

        public override string ToString()
        {
            return $"{CurrentValue}/{MaxValue}";
        }
    }
}
