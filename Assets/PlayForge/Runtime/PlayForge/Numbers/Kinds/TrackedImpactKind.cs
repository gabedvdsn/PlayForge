using System;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>What a Tracked Impact number reads of what effects have dealt (FEAT-29).</summary>
    public enum ETrackedImpactPart
    {
        [Tooltip("The last impact dealt")]
        Last,

        [Tooltip("Every impact dealt, added up")]
        Total,

        [Tooltip("How many impacts were dealt")]
        Count,
    }

    /// <summary>Whose impacts a Tracked Impact number reads (FEAT-29).</summary>
    public enum ETrackedImpactOf
    {
        [Tooltip("The effect whose number it is, as a periodic effect reading its own ticks")]
        [InspectorName("This Effect")] ThisEffect,

        [Tooltip("An effect on the caster, by its asset, its containers added up")]
        [InspectorName("An Effect on the Caster")] EffectOnCaster,

        [Tooltip("An effect on the target, by its asset, its containers added up")]
        [InspectorName("An Effect on the Target")] EffectOnTarget,

        [Tooltip("Every effect on the caster tagged with a tag, added up")]
        [InspectorName("Effects Tagged, on the Caster")] TaggedOnCaster,

        [Tooltip("Every effect on the target tagged with a tag, added up")]
        [InspectorName("Effects Tagged, on the Target")] TaggedOnTarget,
    }

    /// <summary>
    /// What an effect has dealt (FEAT-29, NUM-10): its last impact, its total, or how many impacts, as each effect spec
    /// tracks them once they land, after workers, so a blow reads as it landed, after the target's armor. Whose: this
    /// effect, the one whose number it is, as a burn that grows each tick by its own count; an effect on the caster or the
    /// target, by its asset, its containers summed, as a combustion consuming a burn's total; or every effect there tagged
    /// with a tag, summed. An impact is signed, so damage is negative; <see cref="BySize"/> reads it as a size, as a heal of
    /// half a blow wants. Where nothing is tracked, as an effect that isn't on the target, it reads 0.
    /// <para>
    /// At its first application an effect has dealt nothing: its last impact and total are 0, and so is its count. An
    /// effect's magnitude is worked out as it's applied and again at each tick, so a number on its own count reads the
    /// ticks so far. A save restores an effect's time and stacks but not what it has dealt, so after a load its tracked
    /// impact starts again at 0.
    /// </para>
    /// </summary>
    [Serializable]
    [NumberKind("Tracked Impact", Category = "Effect")]
    public class TrackedImpactKind : NumberKind
    {
        [Tooltip("What it reads of what was dealt: the last impact, the total, or how many impacts")]
        public ETrackedImpactPart What = ETrackedImpactPart.Last;

        [Tooltip("Whose impacts: this effect's own; an effect's on the caster or the target, by its asset; or every effect's " +
                 "there tagged with a tag, added up")]
        public ETrackedImpactOf Of = ETrackedImpactOf.ThisEffect;

        [Tooltip("The effect whose impacts it reads, its containers added up")]
        [ForgeShowWhen(nameof(Of), ETrackedImpactOf.EffectOnCaster, ETrackedImpactOf.EffectOnTarget)]
        public GameplayEffect Effect;

        [Tooltip("The tag the effects it reads carry: as their asset tag, a context tag or a tag they grant, or a tag under it")]
        [ForgeShowWhen(nameof(Of), ETrackedImpactOf.TaggedOnCaster, ETrackedImpactOf.TaggedOnTarget)]
        [ForgeTagContext(ForgeContext.ContextTag)]
        public Tag Tagged;

        [Tooltip("The impact's current part, or its base")]
        [ForgeShowWhen(nameof(What), ETrackedImpactPart.Last, ETrackedImpactPart.Total)]
        public EAttributeTargetBinary Part = EAttributeTargetBinary.Current;

        [Tooltip("On, the impact reads as a size, positive, as a heal of half a blow wants. Off, as dealt: damage is negative")]
        [ForgeShowWhen(nameof(What), ETrackedImpactPart.Last, ETrackedImpactPart.Total)]
        public bool BySize;

        /// <summary>What it needs: the carrier for this effect's own, else the entity whose effects it reads.</summary>
        public override NumberReads Reads => Of switch
        {
            ETrackedImpactOf.ThisEffect => NumberReads.Carrier,
            ETrackedImpactOf.EffectOnCaster or ETrackedImpactOf.TaggedOnCaster => NumberReads.Caster,
            _ => NumberReads.Target,
        };

        protected override float Evaluate(in NumberContext context)
        {
            float read = Of == ETrackedImpactOf.ThisEffect
                ? Read(context.TrackedImpact)
                : Sum(context.SystemOf(OnCaster ? NumberSide.Caster : NumberSide.Target)?.GetEffectSystem());
            return BySize && What != ETrackedImpactPart.Count ? Math.Abs(read) : read;
        }

        private bool OnCaster => Of is ETrackedImpactOf.EffectOnCaster or ETrackedImpactOf.TaggedOnCaster;
        private bool ByAsset => Of is ETrackedImpactOf.EffectOnCaster or ETrackedImpactOf.EffectOnTarget;

        // The effects on a shelf it reads, their tracked impacts added up; by index, which allocates nothing
        private float Sum(EffectSystemComponent effects)
        {
            if (effects == null) return 0f;
            var shelf = effects.Shelf;
            float sum = 0f;
            for (int i = 0; i < shelf.Count; i++)
            {
                var spec = shelf[i]?.Spec;
                if (spec == null) continue;
                bool reads = ByAsset ? Effect != null && spec.Base == Effect : GameplayEffectTags.Names(Tagged, spec.Base);
                if (reads) sum += Read(spec.TrackedImpact);
            }
            return sum;
        }

        private float Read(TrackedImpact tracked)
        {
            if (tracked == null) return 0f;
            return What switch
            {
                ETrackedImpactPart.Count => tracked.Count,
                ETrackedImpactPart.Total => Part == EAttributeTargetBinary.Base ? tracked.Total.BaseValue : tracked.Total.CurrentValue,
                _ => Part == EAttributeTargetBinary.Base ? tracked.Last.BaseValue : tracked.Last.CurrentValue,
            };
        }

        // "the size of Burning's total on the target", "this effect's impact count", "every Fire effect's last impact"
        public override string Describe()
        {
            var what = What switch
            {
                ETrackedImpactPart.Count => "impact count",
                ETrackedImpactPart.Total => "total",
                _ => "last impact",
            };
            if (What != ETrackedImpactPart.Count && Part == EAttributeTargetBinary.Base) what += " (base)";
            var whose = Of switch
            {
                ETrackedImpactOf.ThisEffect => "this effect's",
                ETrackedImpactOf.EffectOnCaster or ETrackedImpactOf.EffectOnTarget => Effect != null ? $"{Effect.GetName()}'s" : "an effect's",
                _ => Tagged.IsValid ? $"every {Tagged} effect's" : "every tagged effect's",
            };
            var where = Of switch
            {
                ETrackedImpactOf.ThisEffect => "",
                _ => OnCaster ? " on the caster" : " on the target",
            };
            var read = $"{whose} {what}{where}";
            return BySize && What != ETrackedImpactPart.Count ? $"the size of {read}" : read;
        }
    }
}
