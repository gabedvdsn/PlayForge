using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public interface ITagHandler
    {
        public List<Tag> GetAppliedTags();
        public int GetTagWeight(Tag _tag);
        public bool QueryTags(TagQuery query);
        public void CompileGrantedTags();
    }

    public interface ITagSource
    {
        public IEnumerable<Tag> GetGrantedTags();
    }
}
