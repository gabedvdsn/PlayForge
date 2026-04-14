using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Tag requirements for effect application, ongoing, and removal phases.
    /// Each phase can have its own set of required and avoided tags.
    /// </summary>
    [Serializable]
    public class EffectTagRequirements : AbstractTagRequirements
    {
        [Tooltip("Tags required/avoided to apply the effect")]
        [AvoidRequireTagGroupColor(0.5f, 0.7f, 0.5f, 0.6f)]
        public AvoidRequireTagGroup ApplicationRequirements;
        
        [Tooltip("Tags required/avoided to keep the effect active")]
        [AvoidRequireTagGroupColor(0.8f, 0.75f, 0.4f, 0.6f)]
        public AvoidRequireTagGroup OngoingRequirements;
        
        [Tooltip("Tags required/avoided to remove the effect")]
        [AvoidRequireTagGroupColor(0.9f, 0.5f, 0.5f, 0.6f)]
        public AvoidRequireTagGroup RemovalRequirements;

        // ═══════════════════════════════════════════════════════════════════════════
        // AbstractTagRequirements Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override bool HasAnyRequirements =>
            GroupHasRequirements(ApplicationRequirements) ||
            GroupHasRequirements(OngoingRequirements) ||
            GroupHasRequirements(RemovalRequirements);
        
        public override int TotalRequirementCount =>
            GetGroupCount(ApplicationRequirements) +
            GetGroupCount(OngoingRequirements) +
            GetGroupCount(RemovalRequirements);
        
        public override bool HasLinkedTemplates =>
            GroupIsLinked(ApplicationRequirements) ||
            GroupIsLinked(OngoingRequirements) ||
            GroupIsLinked(RemovalRequirements);

        // ═══════════════════════════════════════════════════════════════════════════
        // Validation Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        public bool CheckApplicationRequirements(List<Tag> tags)
        {
            return ApplicationRequirements?.Validate(tags) ?? true;
        }

        public bool CheckOngoingRequirements(List<Tag> tags)
        {
            return OngoingRequirements?.Validate(tags) ?? true;
        }
        
        public bool CheckRemovalRequirements(List<Tag> tags)
        {
            return !(RemovalRequirements?.Validate(tags) ?? false);
        }
    }
}