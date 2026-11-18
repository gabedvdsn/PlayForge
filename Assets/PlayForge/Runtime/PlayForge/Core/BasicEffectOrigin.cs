using System.Collections.Generic;


namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of the effect origins an entity holds, an ability's spec and an item's: an origin with a source, the
    /// entity it belongs to, whose level system it registers its level with, under its asset tag, as it's made. A class
    /// made from it implements the rest of <c>IEffectOrigin</c>.
    /// </summary>
    public abstract class BasicEffectOrigin : IEffectOrigin
    {
        public ISource Source { get; private set; }
        
        protected BasicEffectOrigin(ISource source, IntValuePairClamped level, Tag assetTag)
        {
            Source = source;
            
            if (!Source.FindLevelSystem(out var slc)) return;
            slc.Register(assetTag, level);
        }
        
        public abstract ISource GetSource();
        public abstract IHasReadableDefinition GetReadableDefinition();
        public abstract List<Tag> GetContextTags();
        public abstract Tag GetAssetTag();
        public abstract IntValuePairClamped GetLevel();
        public abstract List<Tag> GetAffiliation();
        public abstract bool IsActive();
    }
}
