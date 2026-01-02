using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Clamps an attributes current value with respect to its overflow policy (e.g. 0 to BaseValue)
    /// </summary>
    public class ClampAttributeWorker : AbstractFocusedAttributeWorker
    {
        public override void Activate(IGameplayAbilitySystem system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change)
        {
            var clampValue = attributeCache[TargetAttribute].Value;
            var baseAligned = clampValue.BaseAligned();

            Debug.Log($"Running clamp");

            // Clamp bounds logic is derived from the overflow policy associated with the target attribute
            switch (attributeCache[TargetAttribute].Overflow.Policy)
            {
                case EAttributeOverflowPolicy.ZeroToBase:
                    if (AttributeValue.WithinLimits(clampValue, default, baseAligned)) return;
                    attributeCache[TargetAttribute].Clamp(baseAligned);
                    break;
                case EAttributeOverflowPolicy.FloorToBase:
                    if (AttributeValue.WithinLimits(clampValue, attributeCache[TargetAttribute].Overflow.Floor, baseAligned)) return;
                    attributeCache[TargetAttribute].Clamp(attributeCache[TargetAttribute].Overflow.Floor, baseAligned);
                    break;
                case EAttributeOverflowPolicy.ZeroToCeil:
                    if (AttributeValue.WithinLimits(clampValue, default, attributeCache[TargetAttribute].Overflow.Ceil)) return;
                    attributeCache[TargetAttribute].Clamp(attributeCache[TargetAttribute].Overflow.Ceil);
                    break;
                case EAttributeOverflowPolicy.FloorToCeil:
                    if (AttributeValue.WithinLimits(clampValue, attributeCache[TargetAttribute].Overflow.Floor, attributeCache[TargetAttribute].Overflow.Ceil)) return;
                    attributeCache[TargetAttribute].Clamp(attributeCache[TargetAttribute].Overflow.Floor, attributeCache[TargetAttribute].Overflow.Ceil);
                    break;
                case EAttributeOverflowPolicy.Unlimited:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}
