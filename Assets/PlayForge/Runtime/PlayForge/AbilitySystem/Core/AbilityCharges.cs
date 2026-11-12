using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An ability's charges on its caster (TW-17, Decision 55): several uses, each spending a charge that comes back as the
    /// ability's Recharge says, by default on a cooldown of its own. An ability of 1 charge, with a recharge of PlayForge's
    /// own, is an ordinary cooldown.
    /// </summary>
    public sealed class AbilityCharges
    {
        /// <summary>The ability, as its caster knows it.</summary>
        public AbilitySpec Spec { get; }

        internal AbilityCharges(AbilitySpec spec) => Spec = spec;

        /// <summary>The most it holds, at its level: its Charges, and never fewer than 1.</summary>
        public int Max
        {
            get
            {
                var charges = Spec.Base.Charges;
                if (charges == null) return 1;
                int max = charges.IsConstant ? charges.Constant
                    : charges.Evaluate(new NumberContext(Spec.Source, null, Spec.GetLevel(), true, Spec));
                return Mathf.Max(1, max);
            }
        }

        /// <summary>
        /// Charges spent and not yet back, as a recharge that counts them keeps them: All At Once does, and a recharge of
        /// an author's own may. Each Charge counts its running cooldowns instead.
        /// </summary>
        public int Spent { get; set; }

        /// <summary>How many of its own cooldowns run on its caster, a container each, not counting one that's ending.</summary>
        public int Cooldowns =>
            Spec.Base.Cooldown is { } cooldown && Spec.Source.ToGAS()?.GetEffectSystem() is { } effects ? effects.CountFrom(cooldown, Spec) : 0;

        // Its recharge, or the default where none is set
        private AbstractChargeRecharge Recharge => Spec.Base.Recharge ?? EachChargeRecharge.Default;

        /// <summary>Whether its charges are counted, or it's an ordinary cooldown: 1 charge, with a built-in recharge.</summary>
        public bool Counted => Recharge.CountsASingleCharge || Max > 1;

        /// <summary>The charges in hand: as its recharge says, and for an ordinary cooldown, 1 or 0 as it's on cooldown.</summary>
        public int InHand => Counted ? Mathf.Clamp(Recharge.InHand(this), 0, Max) : Spec.OnCooldown ? 0 : 1;

        /// <summary>
        /// Applies the ability's Cooldown to its caster as a container of its own, whatever the effect does when it's applied
        /// again, so each charge's cooldown runs alone. Nothing, where it has no Cooldown.
        /// </summary>
        public void StartCooldown(ProcessDataPacket data = null) => Spec.ApplyCooldown(data, ownContainer: true);

        // A use spent, as its recharge says
        internal void Spend(ProcessDataPacket data) => Recharge.Spend(this, data);
    }

    /// <summary>
    /// How an ability's spent charges come back (TW-17, Decision 55): what's in hand, and what a use spends. Extend it for a
    /// recharge of your own; each hook has a default, so a recharge overrides only what it changes. By default, each spent
    /// charge's cooldown runs alone and holds that charge back.
    /// </summary>
    [Serializable]
    public abstract class AbstractChargeRecharge
    {
        /// <summary>The charges in hand. By default, those whose cooldown isn't running: each spent charge's holds it back.</summary>
        public virtual int InHand(AbilityCharges charges) => charges.Max - charges.Cooldowns;

        /// <summary>A charge spent, as the ability pays its usage. By default, its own cooldown starts.</summary>
        public virtual void Spend(AbilityCharges charges, ProcessDataPacket data) => charges.StartCooldown(data);

        /// <summary>
        /// Whether a single charge is still counted, rather than the ability being an ordinary cooldown: a recharge of
        /// your own decides for itself, as one use and no more might. The built-in recharges aren't.
        /// </summary>
        public virtual bool CountsASingleCharge => true;
    }

    /// <summary>Each spent charge comes back on a cooldown of its own, run alongside the others: the default (TW-17).</summary>
    [Serializable]
    public class EachChargeRecharge : AbstractChargeRecharge
    {
        /// <summary>The one an ability with no Recharge set follows.</summary>
        internal static readonly EachChargeRecharge Default = new();

        public override bool CountsASingleCharge => false;
    }

    /// <summary>No cooldown until the last charge is spent; its end brings every charge back at once (TW-17).</summary>
    [Serializable]
    public class AllAtOnceRecharge : AbstractChargeRecharge
    {
        // All spent, and the cooldown they started gone: all are back
        public override int InHand(AbilityCharges charges)
        {
            if (charges.Spent < charges.Max) return charges.Max - charges.Spent;
            if (charges.Cooldowns > 0) return 0;
            charges.Spent = 0;
            return charges.Max;
        }

        // The last one starts the cooldown
        public override void Spend(AbilityCharges charges, ProcessDataPacket data)
        {
            charges.Spent++;
            if (charges.Spent >= charges.Max) charges.StartCooldown(data);
        }

        public override bool CountsASingleCharge => false;
    }
}
