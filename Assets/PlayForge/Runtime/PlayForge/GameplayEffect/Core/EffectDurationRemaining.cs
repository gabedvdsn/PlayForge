using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
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
