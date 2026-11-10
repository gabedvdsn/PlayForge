using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Several numbers joined as one (FEAT-29, NUM-14): each member is a scaler or a kind of number, and says how it joins
    /// the others. Members that add are added up; that sum is multiplied by each member that multiplies; then each flat
    /// bonus is added on. So <c>(5 + Caster's Strength) × 2 + 3</c> is one number, and a curse's <c>5 + what its worker
    /// answers</c> is one magnitude.
    /// <para>
    /// A member that overrides gives the group's value alone. Where several override,
    /// <see cref="OverrideMemberCollisionPolicy"/> picks among them: the highest, the lowest, their average, the first or
    /// the last; Ignore sets them aside, and the other members join as they would without them.
    /// </para>
    /// <para>
    /// Each member reads in the number's own context, so a member that is a kind reads all the field's context gives, and
    /// a member that is a scaler evaluates against the effect, or against the carrier the context fills where there's no
    /// effect. With no members, it reads its own values per level, as a scaler does (ED-51). Joining its members allocates
    /// nothing.
    /// </para>
    /// </summary>
    [Serializable]
    [NumberKind("Group")]
    public class ScalerGroup : AbstractScaler
    {
        [Tooltip("The group's members, each with how it joins the others: added up, then multiplied, then flat bonuses added on")]
        public MagnitudeModifierGroupMember[] Calculations = Array.Empty<MagnitudeModifierGroupMember>();

        [Tooltip("A member that overrides gives the group's value alone. Where several do, which one: the highest, the " +
                 "lowest, their average, the first or the last. Ignore sets them aside, and the other members join as they would")]
        public EValueCollisionPolicy OverrideMemberCollisionPolicy = EValueCollisionPolicy.UseMaximum;

        /// <summary>Whether it has members to join. Without any, it reads its own values per level (ED-51).</summary>
        public bool HasMembers => Calculations != null && Calculations.Length > 0;

        public override void Initialize(IAttributeImpactCarrier deriv)
        {
            if (Calculations == null) return;

            foreach (var member in Calculations)
            {
                member.Calculation?.Initialize(deriv);
            }
        }

        public override bool UseScalingOptions()
        {
            return false;
        }

        // With no members, it reads its own values per level (ED-51)
        public override EScalerParts Parts => HasMembers ? EScalerParts.None : EScalerParts.All;

        public override float Evaluate(IAttributeImpactCarrier deriv) =>
            HasMembers ? Read(NumberContext.Of(deriv)) : EvaluateFromSpec(deriv);

        /// <summary>
        /// Joins its members in a number's context: a member that overrides alone, by the policy; else the members that
        /// add, times each that multiplies, plus each flat bonus. By index and with no lists, so it allocates nothing.
        /// </summary>
        internal float Read(in NumberContext context)
        {
            if (!HasMembers) return 0f;

            if (OverrideMemberCollisionPolicy != EValueCollisionPolicy.Ignore)
            {
                int overriding = 0;
                float chosen = 0f;
                for (int i = 0; i < Calculations.Length; i++)
                {
                    var member = Calculations[i];
                    if (member.RelativeOperation != ECalculationOperation.Override || member.Calculation == null) continue;
                    float read = NumberContext.ReadScaler(member.Calculation, in context);
                    switch (OverrideMemberCollisionPolicy)
                    {
                        case EValueCollisionPolicy.UseFirst:
                            return read;
                        case EValueCollisionPolicy.UseMaximum:
                            if (overriding == 0 || read > chosen) chosen = read;
                            break;
                        case EValueCollisionPolicy.UseMinimum:
                            if (overriding == 0 || read < chosen) chosen = read;
                            break;
                        case EValueCollisionPolicy.UseAverage:
                            chosen += read;
                            break;
                        default:
                            chosen = read;
                            break;
                    }
                    overriding++;
                }
                if (overriding > 0)
                    return OverrideMemberCollisionPolicy == EValueCollisionPolicy.UseAverage ? chosen / overriding : chosen;
            }

            float sum = 0f;
            float times = 1f;
            float bonus = 0f;
            for (int i = 0; i < Calculations.Length; i++)
            {
                var member = Calculations[i];
                if (member.Calculation == null) continue;
                switch (member.RelativeOperation)
                {
                    case ECalculationOperation.Add:
                        sum += NumberContext.ReadScaler(member.Calculation, in context);
                        break;
                    case ECalculationOperation.Multiply:
                        times *= NumberContext.ReadScaler(member.Calculation, in context);
                        break;
                    case ECalculationOperation.FlatBonus:
                        bonus += NumberContext.ReadScaler(member.Calculation, in context);
                        break;
                }
            }
            return sum * times + bonus;
        }

        /// <summary>
        /// Adds a new calculation member to the group.
        /// </summary>
        public void AddMember(AbstractScaler scaler, ECalculationOperation operation)
        {
            Calculations ??= Array.Empty<MagnitudeModifierGroupMember>();
            var newArray = new MagnitudeModifierGroupMember[Calculations.Length + 1];
            Array.Copy(Calculations, newArray, Calculations.Length);
            newArray[Calculations.Length] = new MagnitudeModifierGroupMember
            {
                Calculation = scaler,
                RelativeOperation = operation
            };
            Calculations = newArray;
        }
    }

    [Serializable]
    public struct MagnitudeModifierGroupMember
    {
        [SerializeReference]
        [Tooltip("The member: a scaler, or a kind of number")]
        public AbstractScaler Calculation;

        [Tooltip("How it joins the others: added up, multiplied into the sum, added on after as a flat bonus, or " +
                 "overriding them all")]
        public ECalculationOperation RelativeOperation;
    }

    public enum EValueCollisionPolicy
    {
        [Tooltip("Use the highest value among conflicting members")]
        UseMaximum,

        [Tooltip("Use the lowest value among conflicting members")]
        UseMinimum,

        [Tooltip("Average all conflicting member values")]
        UseAverage,

        [Tooltip("Use the first matching member")]
        UseFirst,

        [Tooltip("Use the last matching member")]
        UseLast,

        [Tooltip("Ignore all overriding member values")]
        Ignore
    }
}
