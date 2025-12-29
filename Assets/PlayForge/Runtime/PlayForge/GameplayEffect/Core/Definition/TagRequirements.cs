using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class TagRequirements
    {
        public AvoidRequireTagGroup ApplicationRequirements;  // These tags are required to apply the effect
        public AvoidRequireTagGroup OngoingRequirements;  // These tags are required to keep the effect ongoing
        public AvoidRequireTagGroup RemovalRequirements;  // These tags are required to remove the effect

        public bool CheckApplicationRequirements(List<Tag> tags)
        {
            return ApplicationRequirements.Validate(tags);
        }

        public bool CheckOngoingRequirements(List<Tag> tags)
        {
            return OngoingRequirements.Validate(tags);
        }
        
        public bool CheckRemovalRequirements(List<Tag> tags)
        {
            return RemovalRequirements.Validate(tags);
        }
    }
}
