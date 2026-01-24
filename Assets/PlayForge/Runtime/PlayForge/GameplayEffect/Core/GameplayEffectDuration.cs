using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public struct GameplayEffectDuration
    {
        [FormerlySerializedAs("Valid")] public bool FoundDuration;
        public float TotalDuration;
        public float DurationRemaining;

        public float Ratio => TotalDuration > 0 ? DurationRemaining / TotalDuration : 0f;

        public GameplayEffectDuration(float totalDuration, float durationRemaining, bool foundDuration)
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
