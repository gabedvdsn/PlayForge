
using System.Collections.Generic;
using Unity.VisualScripting.Dependencies.NCalc;
using UnityEngine;
using UnityEngine.Purchasing;

namespace FarEmerald.PlayForge
{
    public abstract class BasicEffectOrigin : IEffectOrigin, IValidationReady
    {
        public ISource Source { get; private set; }
        
        protected BasicEffectOrigin(ISource source, IntValuePairClamped level, Tag assetTag)
        {
            Source = source;
            
            if (!Source.FindLevelSystem(out var slc)) return;
            slc.Register(assetTag, level);
        }
        
        public abstract ISource GetOwner();
        public abstract IHasReadableDefinition GetReadableDefinition();
        public abstract List<Tag> GetContextTags();
        public abstract Tag GetAssetTag();
        public abstract IntValuePairClamped GetLevel();
        public abstract List<Tag> GetAffiliation();
        public abstract bool IsActive();
    }
}
