namespace FarEmerald.PlayForge
{
    public struct LevelCallbackStatus
    {
        public readonly LevelTracker Tracker;
        public readonly RuntimeAttribute LevelAttribute;
        public readonly int PreviousLevel;
        public readonly int PreviousMaxLevel;
        public readonly bool Success;

        public Tag AssetTag => Tracker?.AssetTag ?? Tags.NONE;
        public int CurrentLevel => Tracker?.Level.CurrentValue ?? 0;
        public int MaxLevel => Tracker?.Level.MaxValue ?? 0;
        public int LevelDelta => CurrentLevel - PreviousLevel;
        public int MaxLevelDelta => MaxLevel - PreviousMaxLevel;
        public bool IsLevelUp => CurrentLevel > PreviousLevel;
        public bool IsLevelDown => CurrentLevel < PreviousLevel;

        private LevelCallbackStatus(LevelTracker tracker, RuntimeAttribute levelAttribute, int previousLevel, int previousMaxLevel, bool success)
        {
            Tracker = tracker;
            LevelAttribute = levelAttribute;
            PreviousLevel = previousLevel;
            PreviousMaxLevel = previousMaxLevel;
            Success = success;
        }

        /// <summary>
        /// Level tracker is registered
        /// </summary>
        public static LevelCallbackStatus GenerateForRegistration(LevelTracker tracker, RuntimeAttribute levelAttribute, bool success)
        {
            return new LevelCallbackStatus(
                tracker, 
                levelAttribute, 
                tracker.Level.CurrentValue, 
                tracker.Level.MaxValue, 
                success);
        }

        /// <summary>
        /// Current level changed
        /// </summary>
        public static LevelCallbackStatus GenerateForLevelChanged(LevelTracker tracker, RuntimeAttribute levelAttribute, int previousLevel, int previousMaxLevel, bool success)
        {
            return new LevelCallbackStatus(tracker, levelAttribute, previousLevel, previousMaxLevel, success);
        }
    }
}
