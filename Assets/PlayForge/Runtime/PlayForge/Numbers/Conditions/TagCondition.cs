using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Whether the caster or the target holds a tag (FEAT-29): counted by weight, so a tag granted twice counts twice,
    /// on either side. It reads the side it looks at, so a target's tag doesn't hold where there's no target.
    /// </summary>
    [Serializable]
    [GameplayCondition("Tag")]
    public class TagCondition : GameplayCondition
    {
        [Tooltip("Whose tags it looks at")]
        public NumberSide On = NumberSide.Caster;

        [Tooltip("The tag, and how many of it: more than 0 means it's held")]
        public TagQuery Query = new();

        public override NumberReads Reads => On == NumberSide.Caster ? NumberReads.Caster : NumberReads.Target;

        protected override bool Evaluate(in NumberContext context) =>
            Query != null && (context.EntityOn(On)?.QueryTags(Query) ?? false);

        public override string Describe()
        {
            string side = On == NumberSide.Caster ? "Caster" : "Target";
            if (Query == null) return $"{side} has a tag";
            string tag = (Query.Tag.IsValid ? Query.Tag.ToString() : "a tag") + Query.MatchMode switch
            {
                ETagMatchMode.IncludeChildren => " (or below)",
                ETagMatchMode.IncludeParents => " (or above)",
                ETagMatchMode.SameRoot => " (or its family)",
                _ => "",
            };
            return Query.Operator == EComparisonOperator.GreaterThan && Query.Magnitude == 0
                ? $"{side} has {tag}"
                : $"{side}'s {tag} {NumberComparison.Symbol(Query.Operator)} {Query.Magnitude}";
        }
    }
}
