using System;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What an ability spends of an attribute as it's used (TW-29): the attribute it names, or the one playing the role it
    /// names in the project, and how much, a number that can scale by level. It's paid beside the ability's Cost effect.
    /// </summary>
    [Serializable]
    public class AttributeCost
    {
        [Tooltip("The attribute it spends; or none, for the one playing its Role in the project")]
        public Attribute Attribute;

        [Tooltip("The role whose attribute it spends, where it names no attribute, as one of your own for a resource")]
        [ForgeTagContext(ForgeContext.AttributeRole)]
        public Tag Role;

        [Tooltip("How much it spends, a number that can scale by level")]
        public ScalableFloat Amount = 0f;

        /// <summary>The attribute it spends: its own, or else the one playing its role in the current project; or null.</summary>
        public Attribute Spends => Attribute ? Attribute : ForgeRoles.AttributeFor(Role);
    }
}
