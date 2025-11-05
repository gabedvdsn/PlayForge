namespace FarEmerald.PlayForge
{
    public struct GameplayEffectDuration
    {
        public bool Valid;
        public float TotalDuration;
        public float DurationRemaining;

        public GameplayEffectDuration(float totalDuration, float durationRemaining, bool valid)
        {
            TotalDuration = totalDuration;
            DurationRemaining = durationRemaining;
            Valid = true;
        }

        public override string ToString()
        {
            return $"{DurationRemaining}/{TotalDuration}";
        }
    }
}
