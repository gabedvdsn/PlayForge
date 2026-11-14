using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Charges: how many uses an ability holds, each spending a charge that comes back as its Recharge says (TW-17,
    /// FN-14). Without one, an ability has one charge, an ordinary cooldown. An ability holds one at most.
    /// </summary>
    [Serializable]
    public sealed class ChargesAbilityComponent : AbstractAbilityComponent
    {
        [Tooltip("How many uses it holds, each spending a charge that comes back as its Recharge says; 1 is an ordinary " +
                 "cooldown (TW-17)")]
        public ScalableInt Charges = 2;

        [SerializeReference]
        [Tooltip("How its spent charges come back: each on a cooldown of its own, or all at once as one cooldown ends once " +
                 "they're all spent, or as a recharge of your own says (TW-17)")]
        public AbstractChargeRecharge Recharge = new EachChargeRecharge();

        public override bool AllowsSeveral => false;
    }

    /// <summary>
    /// Toggle: an effect an ability turns on its caster with one use, paying its cost, and off with the next, starting its
    /// cooldown (TW-20, FN-14). As its holder forgets the ability, the effect comes off, where it stayed on. An ability
    /// holds one at most.
    /// </summary>
    [Serializable]
    public sealed class ToggleAbilityComponent : AbstractAbilityComponent
    {
        [Tooltip("The effect it toggles on its caster: used, it puts the effect on, paying its cost; used again, it takes " +
                 "the effect off, paying and asking nothing, and starts its cooldown (TW-20)")]
        public GameplayEffect Effect;

        public override bool AllowsSeveral => false;

        public override void Removed(AbilityComponentContext ctx) => ctx.Spec?.ToggleOff();
    }
}
