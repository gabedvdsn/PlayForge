using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// How a chance rolls (TW-8, Decision 48): pseudo-random, so each miss raises the next roll's odds and a hit starts them
    /// again, each caster keeping its own streak for the chance; and its luck, the project's Luck attribute read on the
    /// caster, unless the chance gives its own.
    /// </summary>
    [Serializable]
    public class ChanceRoll
    {
        [Tooltip("Pseudo-random: each miss raises the next roll's odds and a hit starts them again, so streaks even out, the " +
                 "chance the same over many rolls. Each caster keeps its own streak for this chance")]
        public bool PseudoRandom;

        [Tooltip("Its own luck in place of the project's Luck attribute")]
        public bool OwnLuck;

        [Tooltip("Its luck, read for the roll: above 0 it rolls that many more times and keeps the best, below 0 the worst")]
        public ScalableFloat Luck = 0f;
    }

    /// <summary>
    /// Every chance PlayForge rolls goes through here (TW-8): a number of the chance, the caster's luck, and, where the
    /// chance is pseudo-random, the caster's streak for it, kept by the chance's key.
    /// </summary>
    public static class ForgeChance
    {
        /// <summary>
        /// The project's luck, read on the caster where a chance gives none of its own, or null for none: the project's
        /// Luck attribute, from Project Settings → PlayForge (TW-28).
        /// </summary>
        public static Attribute ProjectLuck => ForgeAttributes.Luck;

        // Each caster's misses in a row for each pseudo-random chance, by the chance's key; they go with the caster
        private static readonly ConditionalWeakTable<object, Dictionary<object, int>> streaks = new();
        private static readonly Dictionary<int, float> constants = new();

        /// <summary>
        /// Whether a roll of <paramref name="chance"/> comes up, from 0 to 1, for the caster the context gives, by
        /// <paramref name="roll"/>'s luck and streak. A chance of 1 or more always comes up and 0 or less never does, and
        /// neither draws, so a chance that can't miss leaves the numbers others draw as they were.
        /// </summary>
        public static bool Roll(float chance, in NumberContext context, ChanceRoll roll, object key)
        {
            if (chance >= 1f) return true;
            if (chance <= 0f) return false;

            int luck = LuckOf(context, roll);
            float odds = chance;
            Dictionary<object, int> misses = null;
            int streak = 0;
            if (roll != null && roll.PseudoRandom && key != null && context.Caster != null)
            {
                misses = streaks.GetOrCreateValue(context.Caster);
                misses.TryGetValue(key, out streak);
                odds = Mathf.Min(1f, Constant(chance) * (streak + 1));
            }

            bool hit = Draw(luck) < odds;
            if (misses != null) misses[key] = hit ? 0 : streak + 1;
            return hit;
        }

        /// <summary>A chance's luck for a roll: its own where it gives one, or the caster's Luck attribute, whole.</summary>
        public static int LuckOf(in NumberContext context, ChanceRoll roll)
        {
            if (roll != null && roll.OwnLuck) return Mathf.RoundToInt(roll.Luck?.Evaluate(in context, roll) ?? 0f);
            var luck = ProjectLuck;
            if (luck == null || context.Caster is not IGameplayAbilitySystem caster) return 0;
            return caster.TryGetAttributeValue(luck, out var value) ? Mathf.RoundToInt(value.CurrentValue) : 0;
        }

        // A draw from 0 to 1, the best (least) of more where the luck is good, the worst where it's bad
        private static float Draw(int luck)
        {
            float draw = ForgeRandom.Value;
            for (int i = 0; i < Mathf.Abs(luck); i++)
            {
                float again = ForgeRandom.Value;
                draw = luck > 0 ? Mathf.Min(draw, again) : Mathf.Max(draw, again);
            }
            return draw;
        }

        /// <summary>
        /// The pseudo-random constant for a chance: the odds the first roll has, each miss adding as much again, so the
        /// chance over many rolls is <paramref name="chance"/>, as Dota's distribution is.
        /// </summary>
        public static float Constant(float chance)
        {
            int at = Mathf.RoundToInt(Mathf.Clamp01(chance) * 10000f);
            if (constants.TryGetValue(at, out var constant)) return constant;
            constant = (float)FindConstant(at / 10000.0);
            constants[at] = constant;
            return constant;
        }

        /// <summary>Forgets every caster's streaks, as a test does to start clean.</summary>
        public static void ForgetStreaks(object caster)
        {
            if (caster != null) streaks.Remove(caster);
        }

        // The constant whose distribution's mean chance is p, by halving
        private static double FindConstant(double p)
        {
            if (p <= 0.0) return 0.0;
            if (p >= 1.0) return 1.0;
            double lower = 0.0, upper = p, previous = 1.0, middle = p;
            for (int i = 0; i < 64; i++)
            {
                middle = (lower + upper) / 2.0;
                double got = MeanChance(middle);
                if (Math.Abs(got - previous) <= 1e-12) break;
                if (got > p) upper = middle;
                else lower = middle;
                previous = got;
            }
            return middle;
        }

        // The chance over many rolls of a constant: one over the rolls a hit takes on average
        private static double MeanChance(double constant)
        {
            double byNow = 0.0, rollsToHit = 0.0;
            int most = (int)Math.Ceiling(1.0 / constant);
            for (int n = 1; n <= most; n++)
            {
                double onThis = Math.Min(1.0, n * constant) * (1.0 - byNow);
                byNow += onThis;
                rollsToHit += n * onThis;
            }
            return rollsToHit > 0.0 ? 1.0 / rollsToHit : 1.0;
        }
    }

    /// <summary>
    /// Holds on a roll of its chance (TW-8): a number, so it can be an attribute, as a critical strike chance, or an item's
    /// stacks through a curve. Read in a hit, it rolls once for the hit and keeps the roll, so a hit read twice, its preview
    /// and its commit, reads the same; read with no hit, it rolls each time. Pseudo-random and luck as its roll says.
    /// </summary>
    [Serializable]
    [GameplayCondition("Chance", Reads = NumberReads.Caster)]
    public class ChanceCondition : GameplayCondition
    {
        [Tooltip("The chance it holds, from 0 to 1, read for the roll")]
        public ScalableFloat Chance = 0.1f;

        [Tooltip("How it rolls: pseudo-random, and its luck")]
        public ChanceRoll Roll = new();

        protected override bool Evaluate(in NumberContext context)
        {
            if (context.Carrier is GameplayEffectSpec spec && spec.TryGetRoll(this, out bool kept)) return kept;
            bool rolled = ForgeChance.Roll(Chance?.Evaluate(in context, this) ?? 0f, in context, Roll, this);
            if (context.Carrier is GameplayEffectSpec hit) hit.KeepRoll(this, rolled);
            return rolled;
        }

        public override void Initialize(IAttributeImpactCarrier carrier) => Chance?.Initialize(carrier);

        public override void Explain(in NumberContext context, NumberTrace trace) => trace.Add(Chance, context, this, "Chance");

        public override string Describe() => $"a {NumberText.Of(Chance)} chance{(Roll != null && Roll.PseudoRandom ? ", pseudo-random" : "")}";
    }
}
