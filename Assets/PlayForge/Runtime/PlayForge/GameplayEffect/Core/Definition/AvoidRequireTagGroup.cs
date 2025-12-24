using System;
using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class AvoidRequireTagGroup
    {
        public List<Tag> AvoidTags;
        public List<Tag> RequireTags;
        
        public bool Validate(List<Tag> appliedTags)
        {
            if (AvoidTags.Count == 0 && RequireTags.Count == 0) return true;
            return !AvoidTags.Any(appliedTags.Contains) && RequireTags.All(appliedTags.Contains);
        }
    }
}
