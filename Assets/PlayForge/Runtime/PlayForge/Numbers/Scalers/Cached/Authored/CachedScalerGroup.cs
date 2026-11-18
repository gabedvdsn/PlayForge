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
    /// <para>
    /// With no members, or none that holds a kind, it is no kind at all, so its element's side reads its number, as the
    /// Group does. Its Ignore policy threw; it sets overriding members aside.
    /// </para>
    /// </summary>
    [ForgeMenu(Description = "Several kept numbers joined as one.")]
    public class CachedScalerGroup : AbstractCachedScaler
    {
        [Tooltip("Group members with their operations")]
        public CachedMagnitudeModifierGroupMember[] Calculations = Array.Empty<CachedMagnitudeModifierGroupMember>();

        [Tooltip("How to handle multiple Override members")]
        public EValueCollisionPolicy OverrideMemberCollisionPolicy = EValueCollisionPolicy.UseMaximum;

        // It reads its members alone
        public override EScalerParts Parts => EScalerParts.None;

        /// <summary>With no members that hold a kind, it is no kind at all.</summary>
        public override bool IsEmpty
        {
            get
            {
                if (Calculations == null) return true;
                for (int i = 0; i < Calculations.Length; i++)
                {
                    if (!IsNone(Calculations[i].Calculation)) return false;
                }
                return true;
            }
        }

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
            if (IsEmpty) return new AttributeValue(0f, 0f);

            AttributeValue Eval(AbstractCachedScaler s) =>
                initial ? s.EvaluateInitialValue(blueprint, cache) : s.EvaluateActiveValue(blueprint, cache);

            // Override members short-circuit everything else.
            var overrides = Calculations
                .Where(m => m.RelativeOperation == EGroupOperation.Override && !IsNone(m.Calculation))
                .ToList();

            // Ignore sets them aside, and the other members join as they would without them
            if (overrides.Count > 0 && OverrideMemberCollisionPolicy != EValueCollisionPolicy.Ignore)
            {
                return ResolveOverrideCollision(overrides, Eval);
            }

            // Additive sum.
            var value = new AttributeValue(0f, 0f);
            foreach (var member in Calculations.Where(m => m.RelativeOperation == EGroupOperation.Add && !IsNone(m.Calculation)))
            {
                value += Eval(member.Calculation);
            }

            // Multiplicative.
            foreach (var member in Calculations.Where(m => m.RelativeOperation == EGroupOperation.Multiply && !IsNone(m.Calculation)))
            {
                var factor = Eval(member.Calculation);
                value = new AttributeValue(value.CurrentValue * factor.CurrentValue, value.BaseValue * factor.BaseValue);
            }

            // Divided by each member that divides; one that reads 0 divides nothing
            foreach (var member in Calculations.Where(m => m.RelativeOperation == EGroupOperation.Divide && !IsNone(m.Calculation)))
            {
                var divisor = Eval(member.Calculation);
                value = new AttributeValue(
                    divisor.CurrentValue != 0f ? value.CurrentValue / divisor.CurrentValue : value.CurrentValue,
                    divisor.BaseValue != 0f ? value.BaseValue / divisor.BaseValue : value.BaseValue);
            }

            // Flat bonus added at the end.
            foreach (var member in Calculations.Where(m => m.RelativeOperation == EGroupOperation.FlatBonus && !IsNone(m.Calculation)))
            {
                value += Eval(member.Calculation);
            }

            return value;
        }

        private AttributeValue ResolveOverrideCollision(
            List<CachedMagnitudeModifierGroupMember> overrides,
            Func<AbstractCachedScaler, AttributeValue> eval)
        {
            // The first or the last alone is read where it stands; the rest fold, a member at a time
            var policy = OverrideMemberCollisionPolicy;
            if (policy == EValueCollisionPolicy.UseFirst) return eval(overrides[0].Calculation);
            if (policy == EValueCollisionPolicy.UseLast) return eval(overrides[overrides.Count - 1].Calculation);
            var chosen = new AttributeValue(0f, 0f);
            for (int i = 0; i < overrides.Count; i++) chosen = ValueCollision.Fold(policy, chosen, eval(overrides[i].Calculation), i);
            return ValueCollision.Settle(policy, chosen, overrides.Count);
        }
    }

    [Serializable]
    public struct CachedMagnitudeModifierGroupMember
    {
        [SerializeReference]
        [Tooltip("The cached scaler calculation")]
        public AbstractCachedScaler Calculation;

        [Tooltip("How this member's result combines with others: added up, multiplied into the sum, dividing it, added on " +
                 "after as a flat bonus, or overriding them all")]
        public EGroupOperation RelativeOperation;
    }
}
