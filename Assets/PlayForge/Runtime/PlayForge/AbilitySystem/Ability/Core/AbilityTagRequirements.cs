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
        
        /// <summary>
        /// The caster's tags while the ability runs. When they fail, the ability is interrupted, so a stun ends a
        /// channel, and while they fail, it can't activate. Requirements weren't checked once an ability ran (FEAT-5).
        /// </summary>
        [Tooltip("Caster requirements while the ability runs: when they fail, the ability is interrupted")]
        public AvoidRequireTagGroup WhileActiveRequirements;

        [Tooltip("Target requirements to use this ability (n/a for non-targeted abilities, e.g. ground cast)")]
        [AvoidRequireTagGroupColor(0.392f, 0.392f, 0.392f, 1f)]
        public AvoidRequireTagGroup TargetRequirements;

        // ═══════════════════════════════════════════════════════════════════════════
        // AbstractTagRequirements Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override bool HasAnyRequirements =>
            GroupHasRequirements(SourceRequirements) ||
            GroupHasRequirements(WhileActiveRequirements) ||
            GroupHasRequirements(TargetRequirements);
        
        public override int TotalRequirementCount =>
            GetGroupCount(SourceRequirements) +
            GetGroupCount(WhileActiveRequirements) +
            GetGroupCount(TargetRequirements);
        
        public override bool HasLinkedTemplates =>
            GroupIsLinked(SourceRequirements) ||
            GroupIsLinked(WhileActiveRequirements) ||
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

        /// <summary>Whether the caster's tags pass the While active group, which holds when it asks for nothing (FEAT-5).</summary>
        public bool CheckWhileActiveRequirements(TagCache tags)
        {
            return tags == null || WhileActiveRequirements == null || !WhileActiveRequirements.HasAnyRequirements
                   || WhileActiveRequirements.Validate(tags);
        }
    }
}
