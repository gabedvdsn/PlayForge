using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The tag side of an entity, as a GAS hub's: the tags it holds (<c>GetAppliedTags</c>), how many times a tag is
    /// granted (<c>GetTagWeight</c>, which a hub gives as −1 for a tag it doesn't hold), whether a tag query holds of
    /// it (<c>QueryTags</c>), and a rebuild of its tags from what grants them (<c>CompileGrantedTags</c>). Every target
    /// is one, and a tag worker checks its requirements against it.
    /// </summary>
    public interface ITagHandler
    {
        public List<Tag> GetAppliedTags();
        public int GetTagWeight(Tag _tag);
        public bool QueryTags(TagQuery query);
        public void CompileGrantedTags();
    }

    /// <summary>
    /// What grants tags: <c>GetGrantedTags</c> gives the tags it grants the entity that holds it, as an ability held
    /// grants its passive tags and, while it runs, its active ones. Every PlayForge asset is one, as are the containers
    /// of an entity's effects and abilities; an entity's tags are built from what its sources grant.
    /// </summary>
    public interface ITagSource
    {
        public IEnumerable<Tag> GetGrantedTags();
    }
}
