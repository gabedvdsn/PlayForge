using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Cues: what an ability shows and sounds at its moments, on its caster: as an activation starts, as it pays its
    /// cost and starts its cooldown, while it's active, as it ends, and as one is refused. A stage's exact moment plays
    /// through a Play Cue task. An ability holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Shows and sounds the ability's moments: as it activates, commits, ends or is refused, and while it's active.")]
    public sealed class CuesAbilityComponent : AbstractAbilityComponent
    {
        [Tooltip("Plays as an activation starts")]
        [SerializeReference]
        public List<AbstractCueHandler> OnActivate = new();

        [Tooltip("Plays as an activation pays its cost and starts its cooldown")]
        [SerializeReference]
        public List<AbstractCueHandler> OnCommit = new();

        [Tooltip("Lasts from an activation's start to its end")]
        [SerializeReference]
        public List<AbstractCueHandler> WhileActive = new();

        [Tooltip("Plays as an activation ends, however it ended")]
        [SerializeReference]
        public List<AbstractCueHandler> OnEnd = new();

        [Tooltip("Plays as an activation is refused")]
        [SerializeReference]
        public List<AbstractCueHandler> OnRefused = new();

        public override bool AllowsSeveral => false;

        public override string Describe()
        {
            var parts = new List<string>();
            Count(OnActivate, "on activate");
            Count(OnCommit, "on commit");
            Count(WhileActive, "while active");
            Count(OnEnd, "on end");
            Count(OnRefused, "on refused");
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

        public override void Start(AbilityComponentContext ctx)
        {
            ForgeCues.Burst(OnActivate, CueOf(ctx, ECueMoment.Activated), From(ctx));
            ForgeCues.Begin(WhileActive, CueOf(ctx, ECueMoment.Activated, lasting: true), From(ctx));
        }

        public override void Commit(AbilityComponentContext ctx) => ForgeCues.Burst(OnCommit, CueOf(ctx, ECueMoment.Committed), From(ctx));

        public override void End(AbilityComponentContext ctx, EAbilityEnd how)
        {
            ForgeCues.End(WhileActive, CueOf(ctx, ECueMoment.Ended, lasting: true), From(ctx));
            ForgeCues.Burst(OnEnd, CueOf(ctx, ECueMoment.Ended), From(ctx));
        }

        public override void Refused(AbilityComponentContext ctx, ActivationRefusal refusal) =>
            ForgeCues.Burst(OnRefused, CueOf(ctx, ECueMoment.Refused), From(ctx));

        // On its caster, from its ability; the activation's data the key a lasting cue's beginning and end share
        private static CueContext CueOf(in AbilityComponentContext ctx, ECueMoment moment, bool lasting = false)
        {
            var caster = ctx.Source;
            return new CueContext(caster as IGameplayAbilitySystem, moment, source: caster, origin: ctx.Spec, data: ctx.Data,
                key: ctx.Data ?? (object)ctx.Spec, lasting: lasting);
        }

        // The ability whose cues they are
        private static CueSource From(in AbilityComponentContext ctx) => CueSource.Of(ctx.Spec?.Base);
    }
}
