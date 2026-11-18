using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An entity's levels, each under a tag: its own, under its identity's asset tag, and each of its abilities' and
    /// items', under theirs, registered as they're given. <c>TrySetLevel</c> and <c>TryModifyLevel</c> set or change
    /// one, as a hub's <c>SetLevel</c> and <c>ModifyLevel</c> do, and its <c>Callbacks</c> hear each change; an effect
    /// that reads a level reads it again as it changes. A hub's <c>LevelSystem</c> gives it.
    /// </summary>
    public class SystemLevelsComponent
    {
        private Dictionary<Tag, IntValuePairClamped> Levels = new();
        public readonly LevelSystemCallbacks Callbacks = new();
        
        public int LevelCount => Levels.Count;

        // A restore registers levels as it gives abilities and items back, and tells nothing as it goes
        internal bool Quiet;

        public LevelCallbackStatus Register(Tag assetTag, IntValuePairClamped levelBounds)
        {
            Levels[assetTag] = levelBounds;

            var status = LevelCallbackStatus.GenerateForRegistration(levelBounds, assetTag, true);
            if (!Quiet) Callbacks.LevelRegistered(status);

            return status;
        }

        /// <summary>Sets a level as a save left it, with nothing told: a restore tells once as it ends.</summary>
        internal void Restore(Tag key, IntValuePairClamped level) => Levels[key] = level;

        public bool Unregister(Tag key)
        {
            if (!Levels.TryGetValue(key, out var tracker))
            {
                Callbacks.LevelUnregistered(LevelCallbackStatus.GenerateForRegistration(default, key, false));
                return false;
            }
            
            bool removed = Levels.Remove(key);
            Callbacks.LevelUnregistered(LevelCallbackStatus.GenerateForRegistration(tracker, key, removed));
            
            return removed;
        }

        public IReadOnlyDictionary<Tag, IntValuePairClamped> GetAllLevels() => Levels;

        public LevelCallbackStatus TrySetLevel(Tag key, IntValuePair value)
        {
            if (!Levels.TryGetValue(key, out var level)) return LevelCallbackStatus.GenerateForInvalid(key);

            int prevLevel = level.CurrentValue;
            int prevMax = level.MaxValue;
            
            // The max first, so a level raised past the old max at once takes it. Set first, the level was held at the
            // old max, and only the max moved
            level.MaxValue = value.MaxValue;
            level.CurrentValue = value.CurrentValue;

            Internal_SetLevel(key, level);

            if (prevLevel == level.CurrentValue && prevMax == level.MaxValue) return LevelCallbackStatus.GenerateForNoOp(level, key);
            
            var status = LevelCallbackStatus.GenerateForLevelChanged(level, key, prevLevel, prevMax, true);
            Callbacks.LevelChanged(status);
            // Declared and never raised
            if (prevMax != level.MaxValue) Callbacks.MaxLevelChanged(status);
                
            if (status.IsLevelUp) Callbacks.LevelUp(status);
            else if (status.IsLevelDown) Callbacks.LevelDown(status);

            return status;

        }

        public LevelCallbackStatus TryModifyLevel(Tag key, IntValuePair value)
        {
            if (!Levels.TryGetValue(key, out var level)) return LevelCallbackStatus.GenerateForInvalid(key);

            int prevLevel = level.CurrentValue;
            int prevMax = level.MaxValue;
            
            // The max first, as a set's is
            level.MaxValue += value.MaxValue;
            level.CurrentValue += value.CurrentValue;
            
            Internal_SetLevel(key, level);

            if (prevLevel == level.CurrentValue && prevMax == level.MaxValue) return LevelCallbackStatus.GenerateForNoOp(level, key);
            
            var status = LevelCallbackStatus.GenerateForLevelChanged(level, key, prevLevel, prevMax, true);
            Callbacks.LevelChanged(status);
            // Declared and never raised
            if (prevMax != level.MaxValue) Callbacks.MaxLevelChanged(status);
                
            if (status.IsLevelUp) Callbacks.LevelUp(status);
            else if (status.IsLevelDown) Callbacks.LevelDown(status);

            return status;

        }
        
        public IntValuePairClamped GetLevel(Tag key, int fallback = 0)
        {
            return Levels.TryGetValue(key, out var level) 
                ? level 
                : new IntValuePairClamped(fallback);
        }
        
        public bool TryGetLevel(Tag levelAttribute, out IntValuePairClamped tracker)
        {
            return Levels.TryGetValue(levelAttribute, out tracker);
        }
        
        public bool HasLevelFor(Tag levelAttribute)
        {
            return Levels.ContainsKey(levelAttribute);
        }

        private void Internal_SetLevel(Tag key, IntValuePairClamped value)
        {
            Levels[key] = value;
        }
    }
}