using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class SystemLevelsComponent
    {
        private Dictionary<RuntimeAttribute, LevelTracker> Levels = new();
        public readonly LevelSystemCallbacks Callbacks = new();
        
        public RuntimeAttribute Register(Tag assetTag, AttributeValueClamped levelBounds)
        {
            var levelAttribute = ForgeHelper.ConfigureLevelAttributeFor(assetTag);
            var tracker = new LevelTracker(assetTag, levelBounds);

            Levels[levelAttribute] = tracker;
            
            Callbacks.LevelRegistered(LevelCallbackStatus.GenerateForRegistration(tracker, levelAttribute, true));
            
            return levelAttribute;
        }

        public bool Unregister(RuntimeAttribute levelAttribute)
        {
            if (!Levels.TryGetValue(levelAttribute, out var tracker))
            {
                Callbacks.LevelUnregistered(LevelCallbackStatus.GenerateForRegistration(null, levelAttribute, false));
                return false;
            }
            
            bool removed = Levels.Remove(levelAttribute);
            Callbacks.LevelUnregistered(LevelCallbackStatus.GenerateForRegistration(tracker, levelAttribute, removed));
            
            return removed;
        }
        
        public bool Unregister(Tag assetTag)
        {
            var levelAttribute = ForgeHelper.ConfigureLevelAttributeFor(assetTag);
            return Unregister(levelAttribute);
        }

        public bool TrySetLevel(RuntimeAttribute levelAttribute, int value)
        {
            if (!Levels.TryGetValue(levelAttribute, out var tracker)) return false;

            int prevLevel = tracker.Level.CurrentValue;
            int prevMax = tracker.Level.MaxValue;
            
            tracker.Set(value);

            bool success = prevLevel != tracker.Level.CurrentValue;
            var status = LevelCallbackStatus.GenerateForLevelChanged(tracker, levelAttribute, prevLevel, prevMax, success);
            Callbacks.LevelChanged(status);
                
            if (status.IsLevelUp) Callbacks.LevelUp(status);
            else if (status.IsLevelDown) Callbacks.LevelDown(status);
            
            return true;
        }
        
        public bool TrySetMaxLevel(RuntimeAttribute levelAttribute, int value)
        {
            if (!Levels.TryGetValue(levelAttribute, out var tracker)) return false;

            int prevLevel = tracker.Level.CurrentValue;
            int prevMax = tracker.Level.MaxValue;
            
            tracker.SetMax(value);

            bool success = prevMax != tracker.Level.MaxValue;
            Callbacks.MaxLevelChanged(LevelCallbackStatus.GenerateForLevelChanged(tracker, levelAttribute, prevLevel, prevMax, success));
            
            return true;
        }

        public bool TryModifyLevel(RuntimeAttribute levelAttribute, int amount)
        {
            if (!Levels.TryGetValue(levelAttribute, out var tracker)) return false;

            int prevLevel = tracker.Level.CurrentValue;
            int prevMax = tracker.Level.MaxValue;
            
            tracker.Modify(amount);
            
            if (prevLevel != tracker.Level.CurrentValue)
            {
                var status = LevelCallbackStatus.GenerateForLevelChanged(tracker, levelAttribute, prevLevel, prevMax, true);
                Callbacks.LevelChanged(status);
                
                if (status.IsLevelUp) Callbacks.LevelUp(status);
                else if (status.IsLevelDown) Callbacks.LevelDown(status);
            }
            
            return true;
        }
        
        public bool TryModifyMaxLevel(RuntimeAttribute levelAttribute, int amount)
        {
            if (!Levels.TryGetValue(levelAttribute, out var tracker)) return false;

            int prevLevel = tracker.Level.CurrentValue;
            int prevMax = tracker.Level.MaxValue;
            
            tracker.ModifyMax(amount);

            bool success = prevMax != tracker.Level.MaxValue;
            Callbacks.MaxLevelChanged(LevelCallbackStatus.GenerateForLevelChanged(tracker, levelAttribute, prevLevel, prevMax, success));
            
            return true;
        }
        
        public LevelTracker GetLeveler(RuntimeAttribute levelAttribute, int fallback = 0)
        {
            return Levels.TryGetValue(levelAttribute, out var level) 
                ? level 
                : new LevelTracker(levelAttribute.DataTag.HasValue 
                    ? levelAttribute.DataTag.Value
                    : Tags.NONE, new AttributeValueClamped(fallback));
        }
        
        public bool TryGetLeveler(RuntimeAttribute levelAttribute, out LevelTracker tracker)
        {
            return Levels.TryGetValue(levelAttribute, out tracker);
        }
        
        public bool HasLevel(RuntimeAttribute levelAttribute)
        {
            return Levels.ContainsKey(levelAttribute);
        }
        
        public int LevelCount => Levels.Count;
    }

    public class LevelTracker
    {
        public Tag AssetTag;

        private AttributeValueClamped level; 
        public AttributeValueClamped Level => level;
        
        public LevelTracker(Tag assetTag, AttributeValueClamped level)
        {
            AssetTag = assetTag;
            this.level = level;
        }

        public void Set(int value)
        {
            level.CurrentValue = Mathf.Clamp(value, 0, level.MaxValue);
        }
        
        public void Set(int currValue, int maxValue)
        {
            level.MaxValue = Mathf.Max(maxValue, 0);
            level.CurrentValue = Mathf.Clamp(currValue, 0, level.MaxValue);
        }
        
        public void SetMax(int value)
        {
            level.MaxValue = Mathf.Max(value, 0);
        }
        
        public void Modify(int amount)
        {
            level.CurrentValue = Mathf.Clamp(level.CurrentValue + amount, 0, level.MaxValue);
        }
        
        public void Modify(int currAmount, int maxAmount)
        {
            level.MaxValue = Mathf.Max(level.MaxValue + maxAmount, 0);
            level.CurrentValue = Mathf.Clamp(level.CurrentValue + currAmount, 0, level.MaxValue);
        }

        public void ModifyMax(int amount)
        {
            level.MaxValue = Mathf.Max(level.MaxValue + amount, 0);
        }
        
        public int Overextension(int delta)
        {
            if (Level.CurrentValue + delta < Level.MinValue) return Level.CurrentValue + delta - Level.MinValue;
            if (Level.CurrentValue + delta > Level.MaxValue) return Level.CurrentValue + delta - Level.MaxValue;
            return 0;
        }
    }
}