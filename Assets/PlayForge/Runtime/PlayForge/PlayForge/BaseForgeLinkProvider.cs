using System.Collections.Generic;
using FarEmerald.PlayForge;
using Unity.VisualScripting;

namespace FarEmerald.PlayForge
{
    public class BaseForgeLinkProvider : BaseForgeObject, ILevelProvider
    {
        public virtual int GetMaxLevel() { return 0; }
        public virtual int GetStartingLevel() { return 0; }
        public virtual string GetProviderName() { return string.Empty; }
        public virtual Tag GetProviderTag() { return Tags.NONE; }
        public override IEnumerable<Tag> GetGrantedTags()
        {
            yield break;
        }
    }
}
