using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class SystemLevelsComponent
    {
        private Dictionary<Tag, IntValuePairClamped> Levels = new();
        public readonly LevelSystemCallbacks Callbacks = new();
        
        public LevelCallbackStatus Register(Tag assetTag, IntValuePairClamped levelBounds)
        {
            Levels[assetTag] = levelBounds;

            var status = LevelCallbackStatus.GenerateForRegistration(levelBounds, assetTag, true);
            Callbacks.LevelRegistered(status);
            
            return status;
        }

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

        public LevelCallbackStatus TrySetLevel(Tag key, IntValuePair value)
        {
            if (!Levels.TryGetValue(key, out var level)) return LevelCallbackStatus.GenerateForInvalid(key);

            int prevLevel = level.CurrentValue;
            int prevMax = level.MaxValue;
            
            level.CurrentValue = value.CurrentValue;
            level.MaxValue = value.MaxValue;

            if (prevLevel == level.CurrentValue && prevMax == level.MaxValue) return LevelCallbackStatus.GenerateForNoOp(level, key);
            
            var status = LevelCallbackStatus.GenerateForLevelChanged(level, key, prevLevel, prevMax, true);
            Callbacks.LevelChanged(status);
                
            if (status.IsLevelUp) Callbacks.LevelUp(status);
            else if (status.IsLevelDown) Callbacks.LevelDown(status);

            return status;

        }

        public LevelCallbackStatus TryModifyLevel(Tag key, IntValuePair value)
        {
            if (!Levels.TryGetValue(key, out var level)) return LevelCallbackStatus.GenerateForInvalid(key);

            int prevLevel = level.CurrentValue;
            int prevMax = level.MaxValue;
            
            level.CurrentValue += value.CurrentValue;
            level.MaxValue += value.MaxValue;

            if (prevLevel == level.CurrentValue && prevMax == level.MaxValue) return LevelCallbackStatus.GenerateForNoOp(level, key);
            
            var status = LevelCallbackStatus.GenerateForLevelChanged(level, key, prevLevel, prevMax, true);
            Callbacks.LevelChanged(status);
                
            if (status.IsLevelUp) Callbacks.LevelUp(status);
            else if (status.IsLevelDown) Callbacks.LevelDown(status);

            return status;

        }
        
        public IntValuePairClamped GetLeveler(Tag key, int fallback = 0)
        {
            return Levels.TryGetValue(key, out var level) 
                ? level 
                : new IntValuePairClamped(fallback);
        }
        
        public bool TryGetLeveler(Tag levelAttribute, out IntValuePairClamped tracker)
        {
            return Levels.TryGetValue(levelAttribute, out tracker);
        }
        
        public bool HasLevel(Tag levelAttribute)
        {
            return Levels.ContainsKey(levelAttribute);
        }
        
        public int LevelCount => Levels.Count;
    }
}