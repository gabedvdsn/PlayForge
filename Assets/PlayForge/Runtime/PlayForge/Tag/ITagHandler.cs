using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public interface ITagHandler
    {
        public List<Tag> GetAppliedTags();
        public int GetWeight(Tag _tag);
    }
}
