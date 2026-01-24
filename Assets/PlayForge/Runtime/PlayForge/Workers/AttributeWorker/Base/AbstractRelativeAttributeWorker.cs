using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Attribute worker that operates relative to another attribute.
    /// Useful for workers that need to compare or compute based on multiple attributes.
    /// </summary>
    [Serializable]
    public abstract class AbstractRelativeAttributeWorker : AbstractFocusedAttributeWorker
    {
        [Header("Relative Attribute")]
        [Tooltip("Where to get the relative attribute from")]
        public EFromSelfSource From;
        
        [Tooltip("The attribute to use as the relative value")]
        public Attribute RelativeTo;
        
        [Tooltip("Multiplier applied to the relative attribute value")]
        public float RelativeMultiplier = 1f;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // VALIDATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override bool PreValidateWorkFor(ChangeValue change)
        {
            // Check that the relative attribute exists on the appropriate system
            bool hasRelative = From switch
            {
                EFromSelfSource.FromSource => change.Value.BaseDerivation.GetSource()
                    .FindAttributeSystem(out var attrT) && attrT.DefinesAttribute(RelativeTo),
                EFromSelfSource.FromSelf => change.Value.BaseDerivation.GetTarget()
                    .FindAttributeSystem(out var attrS) && attrS.DefinesAttribute(RelativeTo),
                _ => false
            };
            
            return hasRelative && base.PreValidateWorkFor(change);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Get the relative attribute value.
        /// </summary>
        protected AttributeValue GetRelative(WorkerContext ctx)
        {
            return From switch
            {
                EFromSelfSource.FromSource => ctx.Change.Value.BaseDerivation.GetSource()
                    .FindAttributeSystem(out var attr) && 
                    attr.TryGetAttributeValue(RelativeTo, out AttributeValue value)
                        ? value * RelativeMultiplier
                        : default,
                EFromSelfSource.FromSelf => ctx.AttributeCache.TryGetValue(RelativeTo, out var cached)
                    ? cached.Value * RelativeMultiplier
                    : default,
                _ => default
            };
        }
    }

    public enum EFromSelfSource
    {
        FromSelf,
        FromSource
    }
}