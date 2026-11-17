using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Cues: what an effect shows and sounds at its moments, on the one it's on: as it lands, as an application it takes
    /// in lands, as each of its ticks executes, as an impact it deals lands, as it comes off, while it lasts, as an
    /// application of it is refused, and as its stacks change. An effect holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Shows and sounds the effect's moments: as it lands, ticks, stacks or comes off, while it lasts, and as it's refused.")]
    public sealed class CuesEffectComponent : AbstractEffectComponent
    {
        [Tooltip("Plays as the effect lands")]
        [SerializeReference]
        public List<AbstractCueHandler> OnApply = new();

        [Tooltip("Plays as an application the effect on already takes in lands: a stack, a refresh or a replacement")]
        [SerializeReference]
        public List<AbstractCueHandler> OnReapply = new();

        [Tooltip("Plays as each tick executes, told how many")]
        [SerializeReference]
        public List<AbstractCueHandler> OnTick = new();

        [Tooltip("Plays as an impact the effect deals lands, on the one it lands on, its magnitude the value it changed, " +
                 "as a hit's damage or the gold it gave")]
        [SerializeReference]
        public List<AbstractCueHandler> OnHit = new();

        [Tooltip("Plays as a lasting effect comes off")]
        [SerializeReference]
        public List<AbstractCueHandler> OnRemove = new();

        [Tooltip("Lasts while the effect is on: from as it lands, or a load puts it back, to as it comes off")]
        [SerializeReference]
        public List<AbstractCueHandler> WhileActive = new();

        [Tooltip("Plays as an application is refused, as by an immunity, on the one it was aimed at")]
        [SerializeReference]
        public List<AbstractCueHandler> OnRefused = new();

        [Tooltip("Plays as the effect's stacks change while it lasts, its magnitude the stacks it has")]
        [SerializeReference]
        public List<AbstractCueHandler> OnStackChange = new();

        public override bool AllowsSeveral => false;

        public override string Describe()
        {
            var parts = new List<string>();
            Count(OnApply, "on apply");
            Count(OnReapply, "on reapply");
            Count(OnTick, "on tick");
            Count(OnHit, "on hit");
            Count(OnRemove, "on remove");
            Count(WhileActive, "while active");
            Count(OnRefused, "on refused");
            Count(OnStackChange, "on stack change");
            return parts.Count == 0 ? "No cues" : "Cues " + string.Join(", ", parts);

            void Count(List<AbstractCueHandler> cues, string when)
            {
                int count = 0;
                if (cues != null)
                    foreach (var cue in cues)
                        if (cue != null) count++;
                if (count > 0) parts.Add($"{count} {when}");
            }
        }

        // A lasting cue begins as the effect starts to last and ends as it stops, on the spec it lent with, so a
        // replacement or a move ends what began
        public override void Lend(EffectComponentContext ctx) => ForgeCues.Begin(WhileActive, CueOf(ctx, ECueMoment.Applied, lasting: true));

        public override void TakeBack(EffectComponentContext ctx) => ForgeCues.End(WhileActive, CueOf(ctx, ECueMoment.Removed, lasting: true));

        public override void Land(EffectComponentContext ctx) => ForgeCues.Burst(OnApply, CueOf(ctx, ECueMoment.Applied));

        public override void Reapply(EffectComponentContext ctx) => ForgeCues.Burst(OnReapply, CueOf(ctx, ECueMoment.Reapplied));

        public override void Tick(EffectComponentContext ctx) =>
            ForgeCues.Burst(OnTick, CueOf(ctx, ECueMoment.Ticked, ticks: ctx.ExecuteTicks));

        // On the one the impact lands on, from its dealer, its magnitude the value it changed: the current value's, or
        // the base value's for an impact that changed that alone
        public override void Hit(EffectComponentContext ctx)
        {
            if (ctx.ImpactData is not { } impact) return;
            var on = impact.Target as IGameplayAbilitySystem ?? ctx.System;
            float changed = impact.RealImpact.CurrentValue != 0f ? impact.RealImpact.CurrentValue : impact.RealImpact.BaseValue;
            var spec = ctx.Spec;
            ForgeCues.Burst(OnHit, new CueContext(on, ECueMoment.Hit, source: ctx.System, origin: spec?.Origin, spec: spec,
                magnitude: changed, key: spec));
        }

        // An instant effect is over as it lands; only a lasting one comes off
        public override void End(EffectComponentContext ctx)
        {
            if (ctx.Spec == null || ctx.Spec.GetDurationPolicy() == EEffectDurationPolicy.Instant) return;
            ForgeCues.Burst(OnRemove, CueOf(ctx, ECueMoment.Removed));
        }

        public override void Refused(EffectComponentContext ctx, EEffectRefusal refusal) =>
            ForgeCues.Burst(OnRefused, CueOf(ctx, ECueMoment.Refused));

        public override void StacksChanged(EffectComponentContext ctx, int before, int after) =>
            ForgeCues.Burst(OnStackChange, CueOf(ctx, ECueMoment.Stacked, magnitude: after));

        // On the one it's on, from its source; its application the key a lasting cue's beginning and end share
        private static CueContext CueOf(in EffectComponentContext ctx, ECueMoment moment, bool lasting = false, int ticks = 0,
            float magnitude = 0f)
        {
            var spec = ctx.Spec;
            return new CueContext(ctx.System, moment, source: spec?.Origin?.GetSource(), origin: spec?.Origin, spec: spec,
                ticks: ticks, magnitude: magnitude, key: spec, lasting: lasting);
        }
    }
}
