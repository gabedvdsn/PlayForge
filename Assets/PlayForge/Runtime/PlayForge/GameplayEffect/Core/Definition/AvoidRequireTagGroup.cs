using System;
using System.Collections.Generic;
using System.Linq;

namespace FarEmerald.PlayForge
{
    public class AvoidRequireTagGroup
    {
        public Tag[] AvoidTags;
        public Tag[] RequireTags;
        
        public bool Validate(Tag[] appliedTags)
        {
            if (AvoidTags.Length == 0 && RequireTags.Length == 0) return true;
            return !AvoidTags.Any(appliedTags.Contains) && RequireTags.All(appliedTags.Contains);
        }
    }
}
