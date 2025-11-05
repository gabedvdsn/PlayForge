using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public interface ITagHandler
    {
        public Tag[] GetAppliedTags();
        public int GetWeight(Tag _tag);
    }
}
