using System;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// One retention group's part of an attribute (FEAT-29, NUM-11), on the caster or the target, current or base: an
    /// attribute's value is the sum of its groups' parts, so <c>2 × Caster's Strength (Bonus)</c> deals more for the
    /// Strength items give and nothing for the hero's own. An entity with no such attribute, or no part in the group,
    /// reads 0.
    /// </summary>
    [Serializable]
    [NumberKind("Group Impact", Reads = NumberReads.Caster)]
    public class GroupImpactKind : NumberKind
    {
        [Tooltip("The attribute whose part it reads")]
        public Attribute Attribute;

        [Tooltip("Whose attribute: the caster's or the target's")]
        public NumberSide Of = NumberSide.Caster;

        [Tooltip("The retention group whose part it reads, as the group items add to")]
        [ForgeTagContext(ForgeContext.RetentionGroup)]
        public Tag Group;

        [Tooltip("The part's current value, or its base")]
        public EAttributeTargetBinary Part = EAttributeTargetBinary.Current;

        /// <summary>What it needs: the entity whose attribute it reads.</summary>
        public override NumberReads Reads => Of == NumberSide.Target ? NumberReads.Target : NumberReads.Caster;

        protected override float Evaluate(in NumberContext context)
        {
            if (!context.TryGetGroupValue(Of, Attribute, Group, out var value)) return 0f;
            return Part == EAttributeTargetBinary.Base ? value.BaseValue : value.CurrentValue;
        }

        // "Caster's Strength (Bonus)", the group by its display name
        public override string Describe()
        {
            var side = Of == NumberSide.Target ? "Target's" : "Caster's";
            var name = Attribute != null ? Attribute.GetName() : "attribute";
            var group = Group.IsValid ? Group.ToString() : "no group";
            return Part == EAttributeTargetBinary.Base ? $"{side} {name} ({group}, base)" : $"{side} {name} ({group})";
        }
    }
}
