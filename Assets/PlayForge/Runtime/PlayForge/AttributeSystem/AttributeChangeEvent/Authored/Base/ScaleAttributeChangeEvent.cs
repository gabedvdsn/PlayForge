using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Scales an attribute's current value based on modifications to its base value
    /// </summary>
    public class ScaleAttributeChangeEvent : AbstractFocusedAttributeChangeEvent
    {
        public override void AttributeChangeEvent(GASComponent system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change)
        {
            float proportion = change.Value.BaseValue / attributeCache[TargetAttribute].Value.BaseValue;
            float delta = proportion * attributeCache[TargetAttribute].Value.CurrentValue;
            
            IAttributeImpactDerivation scaleDerivation = IAttributeImpactDerivation.GenerateSourceDerivation(change.Value, Tags.RETENTION_IGNORE, Tags.GEN_NOT_APPLICABLE);
            SourcedModifiedAttributeValue scaleAmount = new SourcedModifiedAttributeValue(scaleDerivation, delta, 0f, false);
            if (system.FindAttributeSystem(out var attr)) attr.ModifyAttribute(TargetAttribute, scaleAmount, runEvents: false);
        }

        public override bool ValidateWorkFor(GASComponent system, Dictionary<Attribute, CachedAttributeValue> attributeCache, ChangeValue change)
        {
            return change.Value.BaseValue != 0 && base.ValidateWorkFor(system, attributeCache, change);
        }
    }
}
