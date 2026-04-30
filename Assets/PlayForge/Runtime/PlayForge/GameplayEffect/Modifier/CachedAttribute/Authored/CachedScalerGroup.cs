using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Combines multiple cached scalers using the active <see cref="AttributeValue"/> interface.
    /// Members are evaluated component-wise (Current and Base independently) and combined per
    /// the member's <see cref="ECalculationOperation"/>. Override members short-circuit other
    /// operations and resolve via <see cref="OverrideMemberCollisionPolicy"/>.
    /// Regulation propagates to all members so any captured-attribute change triggers a refresh.
    /// </summary>
    public class CachedScalerGroup : AbstractCachedScaler
    {
        [Tooltip("Group members with their operations")]
        public CachedMagnitudeModifierGroupMember[] Calculations = Array.Empty<CachedMagnitudeModifierGroupMember>();

        [Tooltip("How to handle multiple Override members")]
        public EValueCollisionPolicy OverrideMemberCollisionPolicy = EValueCollisionPolicy.UseMaximum;

        public override void RegulateContactWith(IAttribute related, AttributeRegulationCache rules)
        {
            if (Calculations == null) return;
            foreach (var member in Calculations)
            {
                member.Calculation?.RegulateContactWith(related, rules);
            }
        }

        public override AttributeValue EvaluateInitialValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return Combine(blueprint, cache, initial: true);
        }

        public override AttributeValue EvaluateActiveValue(AttributeBlueprint blueprint, IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache)
        {
            return Combine(blueprint, cache, initial: false);
        }

        private AttributeValue Combine(
            AttributeBlueprint blueprint,
            IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache,
            bool initial)
        {
            if (Calculations == null || Calculations.Length == 0) return new AttributeValue(0f, 0f);

            AttributeValue Eval(AbstractCachedScaler s) =>
                initial ? s.EvaluateInitialValue(blueprint, cache) : s.EvaluateActiveValue(blueprint, cache);

            // Override members short-circuit everything else.
            var overrides = Calculations
                .Where(m => m.RelativeOperation == ECalculationOperation.Override && m.Calculation != null)
                .ToList();

            if (overrides.Count > 0)
            {
                return ResolveOverrideCollision(overrides, Eval);
            }

            // Additive sum.
            var value = new AttributeValue(0f, 0f);
            foreach (var member in Calculations.Where(m => m.RelativeOperation == ECalculationOperation.Add && m.Calculation != null))
            {
                value += Eval(member.Calculation);
            }

            // Multiplicative.
            foreach (var member in Calculations.Where(m => m.RelativeOperation == ECalculationOperation.Multiply && m.Calculation != null))
            {
                var factor = Eval(member.Calculation);
                value = new AttributeValue(value.CurrentValue * factor.CurrentValue, value.BaseValue * factor.BaseValue);
            }

            // Flat bonus added at the end.
            foreach (var member in Calculations.Where(m => m.RelativeOperation == ECalculationOperation.FlatBonus && m.Calculation != null))
            {
                value += Eval(member.Calculation);
            }

            return value;
        }

        private AttributeValue ResolveOverrideCollision(
            List<CachedMagnitudeModifierGroupMember> overrides,
            Func<AbstractCachedScaler, AttributeValue> eval)
        {
            switch (OverrideMemberCollisionPolicy)
            {
                case EValueCollisionPolicy.UseFirst:
                    return eval(overrides[0].Calculation);
                case EValueCollisionPolicy.UseLast:
                    return eval(overrides[overrides.Count - 1].Calculation);
                case EValueCollisionPolicy.UseMaximum:
                {
                    var best = eval(overrides[0].Calculation);
                    for (int i = 1; i < overrides.Count; i++)
                    {
                        var v = eval(overrides[i].Calculation);
                        best = new AttributeValue(Mathf.Max(best.CurrentValue, v.CurrentValue), Mathf.Max(best.BaseValue, v.BaseValue));
                    }
                    return best;
                }
                case EValueCollisionPolicy.UseMinimum:
                {
                    var best = eval(overrides[0].Calculation);
                    for (int i = 1; i < overrides.Count; i++)
                    {
                        var v = eval(overrides[i].Calculation);
                        best = new AttributeValue(Mathf.Min(best.CurrentValue, v.CurrentValue), Mathf.Min(best.BaseValue, v.BaseValue));
                    }
                    return best;
                }
                case EValueCollisionPolicy.UseAverage:
                {
                    float c = 0f, b = 0f;
                    foreach (var m in overrides)
                    {
                        var v = eval(m.Calculation);
                        c += v.CurrentValue;
                        b += v.BaseValue;
                    }
                    int n = overrides.Count;
                    return new AttributeValue(c / n, b / n);
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    [Serializable]
    public struct CachedMagnitudeModifierGroupMember
    {
        [SerializeReference]
        [Tooltip("The cached scaler calculation")]
        public AbstractCachedScaler Calculation;

        [Tooltip("How this member's result combines with others")]
        public ECalculationOperation RelativeOperation;
    }
}
