using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Tag requirements for ability activation.
    /// Specifies what tags the source (caster) and target must have or avoid.
    /// </summary>
    [Serializable]
    public class AbilityTagRequirements : AbstractTagRequirements
    {
        [Tooltip("Source (caster) requirements to use this ability")]
        [AvoidRequireTagGroupColor(0.5f, 0.7f, 0.5f, 0.6f)]
        public AvoidRequireTagGroup SourceRequirements;
        
        [Tooltip("Target requirements to use this ability (n/a for non-targeted abilities, e.g. ground cast)")]
        [AvoidRequireTagGroupColor(0.392f, 0.392f, 0.392f, 1f)]
        public AvoidRequireTagGroup TargetRequirements;

        // ═══════════════════════════════════════════════════════════════════════════
        // AbstractTagRequirements Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override bool HasAnyRequirements =>
            GroupHasRequirements(SourceRequirements) ||
            GroupHasRequirements(TargetRequirements);
        
        public override int TotalRequirementCount =>
            GetGroupCount(SourceRequirements) +
            GetGroupCount(TargetRequirements);
        
        public override bool HasLinkedTemplates =>
            GroupIsLinked(SourceRequirements) ||
            GroupIsLinked(TargetRequirements);
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Validation Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        public bool CheckSourceRequirements(List<Tag> tags)
        {
            return SourceRequirements?.Validate(tags) ?? true;
        }
        
        public bool CheckTargetRequirements(List<Tag> tags)
        {
            return TargetRequirements?.Validate(tags) ?? true;
        }
    }
}
