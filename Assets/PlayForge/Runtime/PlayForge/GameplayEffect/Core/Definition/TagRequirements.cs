using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class TagRequirements
    {
        public AvoidRequireTagGroup ApplicationRequirements;  // These tags are required to apply the effect
        public AvoidRequireTagGroup OngoingRequirements;  // These tags are required to keep the effect ongoing
        public AvoidRequireTagGroup RemovalRequirements;  // These tags are required to remove the effect

        [Space]
        
        public TagRequirements[] NestedRequirements;

        public bool CheckApplicationRequirements(List<Tag> tags)
        {
            return !ApplicationRequirements.AvoidTags.Any(tags.Contains) 
                   && ApplicationRequirements.RequireTags.All(tags.Contains) 
                   && NestedRequirements.All(req => req.CheckApplicationRequirements(tags));
        }

        public bool CheckOngoingRequirements(List<Tag> tags)
        {
            if (OngoingRequirements.AvoidTags.Count == 0)
            {
                return (OngoingRequirements.RequireTags.Count == 0
                        || OngoingRequirements.RequireTags.All(tags.Contains))
                       && NestedRequirements.All(req => req.CheckOngoingRequirements(tags));
            }

            return !OngoingRequirements.AvoidTags.Any(tags.Contains) 
                   && (OngoingRequirements.RequireTags.Count == 0 || OngoingRequirements.RequireTags.All(tags.Contains))
                   && NestedRequirements.All(req => req.CheckOngoingRequirements(tags));;
        }
        
        public bool CheckRemovalRequirements(List<Tag> tags)
        {
            return RemovalRequirements.AvoidTags.Any(tags.Contains)
                   || RemovalRequirements.RequireTags.Count != 0 && RemovalRequirements.RequireTags.All(tags.Contains)
                   || NestedRequirements.Any(req => req.CheckRemovalRequirements(tags));
        }
    }
}
