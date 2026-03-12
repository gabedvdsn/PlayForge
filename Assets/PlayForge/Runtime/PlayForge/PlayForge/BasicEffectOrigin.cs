
using System.Collections.Generic;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEngine;
using UnityEngine.Purchasing;

namespace FarEmerald.PlayForge
{
    public abstract class BasicEffectOrigin : IEffectOrigin, IValidationReady
    {
        public ISource Source { get; private set; }
        public readonly RuntimeAttribute LevelKey;

        protected BasicEffectOrigin(ISource source, AttributeValueClamped level)
        {
            Source = source;
            
            if (!Source.FindLevelSystem(out var slc)) return;
            LevelKey = slc.Register(Source.GetAssetTag(), level);
        }
        
        public LevelTracker GetLeveler(int fallback = 0)
        {
            return Source.FindLevelSystem(out var slc)
                ? slc.GetLeveler(LevelKey)
                : new LevelTracker(Source.GetAssetTag(), new AttributeValueClamped(fallback));
        }

        public bool SetLevel(int level)
        {
            return Source.FindLevelSystem(out var slc) && slc.TrySetLevel(LevelKey, level);
        }

        public bool ModifyLevel(int amount, bool ignoreOverextensions = false)
        {
            var level = GetLeveler();
            
            int overextension = level.Overextension(amount);
            if (overextension != 0 && ignoreOverextensions) return false;
            level.Modify(amount + overextension);
            
            return true;
        }
        
        public abstract ISource GetOwner();
        public abstract List<Tag> GetContextTags();
        public abstract Tag GetAssetTag();
        public abstract int GetLevel();
        public abstract float GetRelativeLevel();
        public abstract string GetName();
        public abstract List<Tag> GetAffiliation();
        public abstract bool IsActive();
    }
}
