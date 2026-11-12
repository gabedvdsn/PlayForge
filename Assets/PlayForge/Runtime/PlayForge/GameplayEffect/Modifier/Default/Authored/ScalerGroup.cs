using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Several numbers joined as one (FEAT-29, NUM-14): each member is a scaler or a kind of number, and says how it joins
    /// the others. Members that add are added up; that sum is multiplied by each member that multiplies and divided by each
    /// that divides (TW-9); then each flat bonus is added on. So <c>(5 + Caster's Strength) × 2 + 3</c> is one number, a
    /// wind-up's <c>1.7 ÷ Caster's Attack Speed</c> another, and a curse's <c>5 + what its worker answers</c> is one
    /// magnitude.
    /// <para>
    /// A member that overrides gives the group's value alone. Where several override,
    /// <see cref="OverrideMemberCollisionPolicy"/> picks among them: the highest, the lowest, their average, the first or
    /// the last; Ignore sets them aside, and the other members join as they would without them.
    /// </para>
    /// <para>
    /// Each member reads in the number's own context, so a member that is a kind reads all the field's context gives, and
    /// a member that is a scaler evaluates against the effect, or against the carrier the context fills where there's no
    /// effect. Joining its members allocates nothing.
    /// </para>
    /// <para>
    /// It reads its members alone, so it has no values per level. With no members, or none that holds a kind, it is no
    /// kind at all (<see cref="AbstractScaler.IsEmpty"/>): its field reads its number, as a field with no kind does
    /// (ED-80, Decision 44). It read values per level of its own, which its inspector hid (ED-51).
    /// </para>
    /// </summary>
    [Serializable]
    [NumberKind("Group")]
    public class ScalerGroup : AbstractScaler
    {
        [Tooltip("The group's members, each with how it joins the others: added up, then multiplied and divided, then flat " +
                 "bonuses added on")]
        public MagnitudeModifierGroupMember[] Calculations = Array.Empty<MagnitudeModifierGroupMember>();

        [Tooltip("A member that overrides gives the group's value alone. Where several do, which one: the highest, the " +
                 "lowest, their average, the first or the last. Ignore sets them aside, and the other members join as they would")]
        public EValueCollisionPolicy OverrideMemberCollisionPolicy = EValueCollisionPolicy.UseMaximum;

        /// <summary>
        /// Whether it has members to join: a member that holds a kind. Without one, it is no kind at all, so its field reads
        /// its number (ED-80).
        /// </summary>
        public bool HasMembers
        {
            get
            {
                if (Calculations == null) return false;
                for (int i = 0; i < Calculations.Length; i++)
                {
                    if (!IsNone(Calculations[i].Calculation)) return true;
                }
                return false;
            }
        }

        /// <summary>With no members that hold a kind, it is no kind at all (ED-80, Decision 44).</summary>
        public override bool IsEmpty => !HasMembers;

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

        // It reads its members alone, so it shows no values per level, with members or without (ED-80)
        public override EScalerParts Parts => EScalerParts.None;

        // With no members it is no kind, which its field doesn't read; read on its own, it joins nothing, 0 (ED-80)
        public override float Evaluate(IAttributeImpactCarrier deriv) => HasMembers ? Read(NumberContext.Of(deriv)) : 0f;

        /// <summary>
        /// Joins its members in a number's context: a member that overrides alone, by the policy; else the members that
        /// add, times each that multiplies, divided by each that divides, plus each flat bonus, in whatever order they're
        /// listed. A member that holds no kind joins nothing, and one that divides by 0 divides nothing. By index and with
        /// no lists, so it allocates nothing.
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
                    if (member.RelativeOperation != EGroupOperation.Override || IsNone(member.Calculation)) continue;
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
                if (IsNone(member.Calculation)) continue;
                switch (member.RelativeOperation)
                {
                    case EGroupOperation.Add:
                        sum += NumberContext.ReadScaler(member.Calculation, in context);
                        break;
                    case EGroupOperation.Multiply:
                        times *= NumberContext.ReadScaler(member.Calculation, in context);
                        break;
                    case EGroupOperation.Divide:
                        // A member that reads 0 divides nothing (TW-9)
                        float divisor = NumberContext.ReadScaler(member.Calculation, in context);
                        if (divisor != 0f) times /= divisor;
                        break;
                    case EGroupOperation.FlatBonus:
                        bonus += NumberContext.ReadScaler(member.Calculation, in context);
                        break;
                }
            }
            return sum * times + bonus;
        }

        /// <summary>Adds a member that joins as an impact's operation would: added, multiplied, a flat bonus or an override.</summary>
        public void AddMember(AbstractScaler scaler, ECalculationOperation operation) => AddMember(scaler, (EGroupOperation)operation);

        /// <summary>Adds a member that joins as <paramref name="operation"/> says, Divide among them (TW-9).</summary>
        public void AddMember(AbstractScaler scaler, EGroupOperation operation)
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
        // The kinds a number takes, none of an attribute set's cached ones, which its kind menu offered beside them (ED-81)
        [SerializeReference]
        [ScalerRootAssignment(null, typeof(AbstractCachedScaler))]
        [Tooltip("The member: a scaler, or a kind of number")]
        public AbstractScaler Calculation;

        [Tooltip("How it joins the others: added up, multiplied into the sum, dividing it, added on after as a flat bonus, " +
                 "or overriding them all")]
        public EGroupOperation RelativeOperation;
    }

    /// <summary>
    /// How a Group's member joins the others (TW-9): an impact's four operations, in their order, so a member saved before
    /// keeps its meaning, and Divide.
    /// </summary>
    public enum EGroupOperation
    {
        /// <summary>Added up with the others that add.</summary>
        Add,

        /// <summary>The sum multiplied by it.</summary>
        Multiply,

        /// <summary>It alone gives the Group's value, by the Group's policy where several do.</summary>
        Override,

        /// <summary>Added on after the sum is multiplied and divided.</summary>
        FlatBonus,

        /// <summary>The sum divided by it, as an attack's wind-up by its attack speed; one that reads 0 divides nothing (TW-9).</summary>
        Divide,
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
