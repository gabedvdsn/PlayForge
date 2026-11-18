
namespace FarEmerald.PlayForge
{
    /// <summary>
    /// How long an entity's effect under a tag has left: its total duration and the time remaining, and whether one was
    /// found, as <c>GetLongestDurationFor</c> gives them for the one of the longest duration that has the tag as its
    /// asset tag or grants it. An Infinite one gives float.MaxValue for both; <c>Ratio</c> is the share of it left.
    /// </summary>
    public struct EffectDurationRemaining
    {
        public bool FoundDuration;
        public float TotalDuration;
        public float DurationRemaining;

        public float Ratio => TotalDuration > 0 ? DurationRemaining / TotalDuration : 0f;

        public EffectDurationRemaining(float totalDuration, float durationRemaining, bool foundDuration)
        {
            TotalDuration = totalDuration;
            DurationRemaining = durationRemaining;
            FoundDuration = foundDuration;
        }

        public override string ToString()
        {
            return $"{DurationRemaining}/{TotalDuration}";
        }
    }
}
