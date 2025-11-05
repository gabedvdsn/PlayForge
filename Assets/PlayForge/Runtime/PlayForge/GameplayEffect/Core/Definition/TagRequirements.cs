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

        public bool CheckApplicationRequirements(Tag[] tags)
        {
            return !ApplicationRequirements.AvoidTags.Any(tags.Contains) 
                   && ApplicationRequirements.RequireTags.All(tags.Contains) 
                   && NestedRequirements.All(req => req.CheckApplicationRequirements(tags));
        }

        public bool CheckOngoingRequirements(Tag[] tags)
        {
            if (OngoingRequirements.AvoidTags.Length == 0)
            {
                return (OngoingRequirements.RequireTags.Length == 0
                        || OngoingRequirements.RequireTags.All(tags.Contains))
                       && NestedRequirements.All(req => req.CheckOngoingRequirements(tags));
            }

            return !OngoingRequirements.AvoidTags.Any(tags.Contains) 
                   && (OngoingRequirements.RequireTags.Length == 0 || OngoingRequirements.RequireTags.All(tags.Contains))
                   && NestedRequirements.All(req => req.CheckOngoingRequirements(tags));;
        }
        
        public bool CheckRemovalRequirements(Tag[] tags)
        {
            return RemovalRequirements.AvoidTags.Any(tags.Contains)
                   || RemovalRequirements.RequireTags.Length != 0 && RemovalRequirements.RequireTags.All(tags.Contains)
                   || NestedRequirements.Any(req => req.CheckRemovalRequirements(tags));
        }
    }
}
