using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class AvoidRequireTagGroup
    {
        public List<AvoidRequireContainer> AvoidTags;
        public List<AvoidRequireContainer> RequireTags;
        
        public bool Validate(List<Tag> appliedTags)
        {
            if (AvoidTags.Count == 0 && RequireTags.Count == 0) return true;
            return !AvoidTags.Any(arc => arc.Validate(appliedTags, false)) &&
                   RequireTags.All(arc => arc.Validate(appliedTags, true));
            // return !AvoidTags.Any(appliedTags.Contains) && RequireTags.All(appliedTags.Contains);
        }
    }

    [Serializable]
    public class AvoidRequireContainer
    {
        [ForgeTagContext(ForgeContext.Required, ForgeContext.Granted, ForgeContext.AssetIdentifier)]
        public Tag Tag;
        public EComparisonOperator Operator = EComparisonOperator.GreaterThan;
        public int Magnitude = 0;

        public bool Validate(List<Tag> appliedTags, bool sign)
        {
            var count = 0;
            foreach (var _ in appliedTags.Where(t => t == Tag))
            {
                count += 1;

                switch (Operator)
                {
                    case EComparisonOperator.GreaterThan:
                        if (count > Magnitude) return sign;
                        break;
                    case EComparisonOperator.LessThan:
                        if (count > Magnitude) return !sign;
                        break;
                    case EComparisonOperator.GreaterOrEqualTo:
                        if (count >= Magnitude) return sign;
                        break;
                    case EComparisonOperator.LessOrEqualTo:
                        if (count >= Magnitude) return !sign;
                        break;
                    case EComparisonOperator.Equal:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            if (Operator == EComparisonOperator.Equal && count == Magnitude) return sign;
            return !sign;
        }
    }
}
