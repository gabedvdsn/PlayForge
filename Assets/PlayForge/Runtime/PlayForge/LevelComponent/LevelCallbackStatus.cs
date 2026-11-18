using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What a change to one of an entity's levels did, as its level system's callbacks and a hub's <c>SetLevel</c> and
    /// <c>ModifyLevel</c> report it: the level under its key, the level and max level before, and whether it took.
    /// <c>IsLevelUp</c>, <c>IsLevelDown</c> and the deltas compare before with after; <c>Success</c> is false where no
    /// level is under the key or nothing changed.
    /// </summary>
    public struct LevelCallbackStatus
    {
        public readonly IntValuePairClamped Level;
        public readonly Tag LevelKey;
        public readonly int PreviousLevel;
        public readonly int PreviousMaxLevel;
        public readonly bool Success;

        public Tag AssetTag => LevelKey;
        public int CurrentLevel => Level.CurrentValue;
        public int MaxLevel => Level.MaxValue;
        public int LevelDelta => CurrentLevel - PreviousLevel;
        public int MaxLevelDelta => MaxLevel - PreviousMaxLevel;
        public bool IsLevelUp => CurrentLevel > PreviousLevel;
        public bool IsLevelDown => CurrentLevel < PreviousLevel;

        private LevelCallbackStatus(IntValuePairClamped level, Tag levelKey, int previousLevel, int previousMaxLevel, bool success)
        {
            Level = level;
            LevelKey = levelKey;
            PreviousLevel = previousLevel;
            PreviousMaxLevel = previousMaxLevel;
            Success = success;
        }

        /// <summary>
        /// Level tracker is registered
        /// </summary>
        public static LevelCallbackStatus GenerateForRegistration(IntValuePairClamped level, Tag key, bool success)
        {
            return new LevelCallbackStatus(
                level, 
                key, 
                level.CurrentValue, 
                level.MaxValue, 
                success);
        }

        /// <summary>
        /// Current level changed
        /// </summary>
        public static LevelCallbackStatus GenerateForLevelChanged(IntValuePairClamped level, Tag levelKey, int previousLevel, int previousMaxLevel, bool success)
        {
            return new LevelCallbackStatus(level, levelKey, previousLevel, previousMaxLevel, success);
        }

        public static LevelCallbackStatus GenerateForInvalid(Tag levelKey)
        {
            return new LevelCallbackStatus(new IntValuePairClamped(0), levelKey, 0, 0, false);
        }

        public static LevelCallbackStatus GenerateForNoOp(IntValuePairClamped level, Tag levelKey)
        {
            return new LevelCallbackStatus(level, levelKey, level.CurrentValue, level.MaxValue, false);
        }

        public override string ToString()
        {
            return $"[LevelCallbackStatus-{Success}] {LevelKey}: {PreviousLevel}/{PreviousMaxLevel} => {Level}";
        }
    }
}
