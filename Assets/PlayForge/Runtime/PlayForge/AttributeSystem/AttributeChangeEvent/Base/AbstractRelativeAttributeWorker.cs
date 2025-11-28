using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractRelativeAttributeWorker : AbstractFocusedAttributeWorker
    {
        [Header("Relative Attribute Event")] public ESourceTarget From;
        public Attribute RelativeTo;
        public float RelativeMultiplier = 1f;

        public override bool PreValidateWorkFor(ChangeValue change)
        {
            return From switch
            {
                ESourceTarget.Target => change.Value.BaseDerivation.GetTarget().FindAttributeSystem(out var attrT) &&
                                        attrT.DefinesAttribute(RelativeTo),
                ESourceTarget.Source => change.Value.BaseDerivation.GetSource().FindAttributeSystem(out var attrS) &&
                                        attrS.DefinesAttribute(RelativeTo),
                _ => throw new ArgumentOutOfRangeException()
            } && base.PreValidateWorkFor(change);
        }

        protected AttributeValue GetRelative(Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change)
        {
            return From switch
            {
                ESourceTarget.Source => change.Value.BaseDerivation.GetSource().FindAttributeSystem(out var attr) &&
                                        attr.TryGetAttributeValue(RelativeTo, out AttributeValue value)
                    ? value * RelativeMultiplier
                    : default,
                ESourceTarget.Target => attributeCache[RelativeTo].Value,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}