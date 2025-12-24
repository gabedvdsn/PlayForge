using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractCachedMagnitudeModifier : AbstractMagnitudeModifier
    {
        public abstract void Regulate(Attribute attribute, AttributeModificationRule rules);
    }

    public class AttributeModificationRule
    {
        private Dictionary<Attribute, List<Attribute>> matrix = new();

        public void RegisterRelation(Attribute contact, Attribute related)
        {
            matrix.SafeAdd(contact, related);
        }

        /// <summary>
        /// When attr is changed, we want re-init values of related attributes via modifier(s)
        /// </summary>
        /// <param name="attr"></param>
        /// <param name="related"></param>
        /// <returns></returns>
        public bool RelatedAttributes(Attribute attr, out List<Attribute> related)
        {
            return matrix.TryGetValue(attr, out related);
        }
    }
}
