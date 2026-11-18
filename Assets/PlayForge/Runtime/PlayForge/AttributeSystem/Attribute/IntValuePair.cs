namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A whole value and a maximum, unclamped, as a level and its max level: <c>SetLevel</c> sets a level an entity
    /// keeps under a tag to one, and <c>ModifyLevel</c> adds one to it, the max first in each, so a level raised past
    /// the old max takes it. <c>ToClamped</c> gives it as an <c>IntValuePairClamped</c>.
    /// </summary>
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
