using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Area: as its effect lands on its target, it lands on each the area takes near that target too, as Storm Hammer stuns
    /// everyone near the one it hits. Without one, an effect lands on its target alone. An effect holds one
    /// at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Lands the effect on others near its target too.")]
    public sealed class AreaEffectComponent : AbstractEffectComponent
    {
        /// <summary>What it does, in one line.</summary>
        public override string Describe() => $"Lands on {Whom(Area?.Takes ?? EAffinityFilter.Enemies)} within {NumberText.Of(Area?.Radius)}" + (Area?.MaxTargets is { } most && (most.Magnitude > 0f || !AbstractScaler.IsNone(most.Scaler)) ? $", {NumberText.Of(most)} at most" : "");

        [Tooltip("Its area: as its effect lands on its target, it lands on each the area takes near that target too")]
        public EffectArea Area = new();

        public override bool AllowsSeveral => false;
    }

    /// <summary>
    /// Transfer: moves an amount from an attribute of its effect's caster or target to one of either, as the effect lands,
    /// each tick, or while it lasts, giving back as it ends; only what the giver has moves. An effect holds
    /// as many as it moves, each its own.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Moves an amount of an attribute from the caster or the target to either.")]
    public sealed class TransferEffectComponent : AbstractEffectComponent
    {
        /// <summary>What it does, in one line.</summary>
        public override string Describe() => $"Moves {NumberText.Of(Transfer?.Amount)} of the {Transfer?.From.ToString().ToLowerInvariant()}'s {NameOf(Transfer?.FromAttribute)} to the {Transfer?.To.ToString().ToLowerInvariant()}'s {NameOf(Transfer?.ToAttribute)}";

        [Tooltip("What it moves: when, who gives and of what, who gets and in what, which of the values, and how much")]
        public AttributeTransfer Transfer = new();
    }

    /// <summary>
    /// Side Change: while its effect lasts, its target's sides are its caster's, as they are as it lands, a mind control,
    /// or those it names, as a pacify's Neutral. Of several on one entity, the highest Priority holds, a
    /// tie going to the latest, and the entity's own come back as the last ends. One that interrupts breaks what its
    /// target runs as it changes the side, landing or ending. An Instant effect doesn't last, so it changes nothing. An
    /// effect holds one at most. It was a field of the effect's own, with a switch.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Changes the target's sides while the effect lasts, as a mind control.")]
    public sealed class SideChangeEffectComponent : AbstractEffectComponent
    {
        /// <summary>What it does, in one line.</summary>
        public override string Describe() => Source == ESideSource.Caster ? "Sides become its caster's" : $"Sides become {(Sides is { Count: > 0 } ? string.Join(", ", Sides) : "none")}";

        [Tooltip("Whose sides it gives: its caster's, as a mind control, or those it names")]
        public ESideSource Source = ESideSource.Caster;

        [Tooltip("The sides it gives, where it names them, as Neutral")]
        [ForgeTagContext(ForgeContext.Affiliation)]
        [ShownWhen(nameof(Source), (int)ESideSource.Named)]
        public List<Tag> Sides = new();

        [Tooltip("Of several on one entity, the highest holds, a tie going to the latest; read as it lands")]
        public ScalableInt Priority = 0;

        [Tooltip("Interrupt what its target runs as it changes its side, as it lands and as it ends")]
        public bool Interrupts;

        public override bool AllowsSeveral => false;
    }

    /// <summary>The moments a Spread component spreads at.</summary>
    [Flags]
    public enum ESpreadMoment
    {
        None = 0,

        /// <summary>As its effect lands on the one it's on.</summary>
        Land = 1,

        /// <summary>Each tick of its effect.</summary>
        Tick = 2,

        /// <summary>As its effect comes off the one it's on, however it ends.</summary>
        End = 4,

        /// <summary>As the one it's on dies, while it's on them.</summary>
        Death = 8,
    }

    /// <summary>
    /// Spread: spreads its effect, or another it names, from the one it's on to those near them, at the moments it names:
    /// as it lands, each tick, as it ends, or as the one it's on dies, as a plague jumps from its victim to the enemies
    /// near it. Each it takes gets the effect from the same origin, its caster's, so its modifiers and
    /// its chance to land are its own. A landing by spread spreads on only so far as its Hops allow.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Spreads the effect, or another, from its target to those near it.")]
    public sealed class SpreadEffectComponent : AbstractEffectComponent
    {
        /// <summary>What it does, in one line.</summary>
        public override string Describe() => $"Spreads {(Effect != null ? NameOf(Effect) : "itself")} to {Whom(Takes)}, {NumberText.Of(MaxTargets)} at most";

        [Tooltip("When it spreads: as its effect lands, each tick, as it comes off, or as the one it's on dies; any of them")]
        public ESpreadMoment When = ESpreadMoment.Death;

        [Tooltip("The effect it spreads; none, its own effect")]
        public GameplayEffect Effect;

        [Tooltip("How far from the one it's on it reaches, read at its effect's level")]
        [NumberMin(0f)] public ScalableFloat Radius = 4f;

        [Tooltip("Measures along the ground, leaving height out; off, a sphere")]
        public bool Flat = true;

        [Tooltip("Whom it takes, from its caster's sides: enemies, allies, or both. A caster with no sides takes anyone")]
        public EAffinityFilter Takes = EAffinityFilter.Enemies;

        [Tooltip("Its caster too, where it reaches them and takes their side")]
        public bool IncludeCaster;

        [Tooltip("At most this many each time it spreads, the nearest first; 0, every one it reaches")] 
        [NumberMin(0f)]
        public ScalableInt MaxTargets = 0;

        [Tooltip("How many more times a landing it made spreads on: 0, it spreads only from where its effect landed of its own")]
        [NumberMin(0)]
        public ScalableInt Hops = 0;

        [Tooltip("Passes over those who have the effect it spreads already")]
        public bool SkipHolders = true;

        [Tooltip("Lands on those it takes in the project's resolve order; off, the nearest first")]
        public bool UseResolveOrder = true;

        public override void Land(EffectComponentContext ctx) { if ((When & ESpreadMoment.Land) != 0) ForgeSpread.Spread(this, ctx); }
        public override void Tick(EffectComponentContext ctx) { if ((When & ESpreadMoment.Tick) != 0) ForgeSpread.Spread(this, ctx); }
        public override void End(EffectComponentContext ctx) { if ((When & ESpreadMoment.End) != 0) ForgeSpread.Spread(this, ctx); }
        public override void Death(EffectComponentContext ctx) { if ((When & ESpreadMoment.Death) != 0) ForgeSpread.Spread(this, ctx); }
    }

    /// <summary>What spreads an effect from the one it's on to those near them, for a Spread component.</summary>
    internal static class ForgeSpread
    {
        internal static void Spread(SpreadEffectComponent spread, EffectComponentContext ctx)
        {
            var spec = ctx.Spec;
            var holder = ctx.System;
            if (spec?.Base == null || holder == null || spec.SpreadHop > spread.Hops.Evaluate(NumberContext.Of(ctx.Spec))) return;
            var effect = spread.Effect != null ? spread.Effect : spec.Base;
            var here = holder.GetTargetingPacket();
            if (here == null) return;
            float radius = spread.Radius == null ? 0f : spread.Radius.Evaluate(NumberContext.Of(spec));
            if (radius <= 0f) return;

            // Each it reaches but the one it's on, the dead, and its caster unless it says, of the sides it takes
            var taken = new List<ITarget>();
            ForgeEntities.Within(here.position, radius, spread.Flat, taken);
            var centre = here.position;
            var caster = spec.Origin?.GetSource();
            var sides = spec.Origin?.GetAffiliation();
            for (int i = taken.Count - 1; i >= 0; i--)
            {
                var other = taken[i];
                if (ReferenceEquals(other, holder) || other.IsDead || ReferenceEquals(other, caster) && !spread.IncludeCaster
                    || !ForgeEntities.Takes(spread.Takes, sides, other) || spread.SkipHolders && Holds(other, effect))
                    taken.RemoveAt(i);
            }
            taken.Sort((a, b) => ForgeEntities.Distance(a, centre, spread.Flat).CompareTo(ForgeEntities.Distance(b, centre, spread.Flat)));
            int maxTargets = spread.MaxTargets.Evaluate(NumberContext.Of(ctx.Spec));
            if (maxTargets > 0 && taken.Count > maxTargets) taken.RemoveRange(maxTargets, taken.Count - maxTargets);
            if (spread.UseResolveOrder && ForgeResolveOrder.Orders) ForgeResolveOrder.Sort(taken);

            foreach (var other in taken)
            {
                var made = other.GenerateEffectSpec(spec.Origin, effect);
                if (made == null) continue;
                made.SpreadHop = spec.SpreadHop + 1;
                if (spec.SetByCallerMagnitudes != null) made.TakeSetByCaller(spec.SetByCallerMagnitudes);
                other.ApplyGameplayEffect(made);
            }
        }

        // Whether another has the effect on them already
        private static bool Holds(ITarget other, GameplayEffect effect)
        {
            if (other is not IGameplayAbilitySystem gas || gas.GetEffectSystem()?.Shelf is not { } shelf) return false;
            for (int i = 0; i < shelf.Count; i++)
                if (ReferenceEquals(shelf[i]?.Spec?.Base, effect)) return true;
            return false;
        }
    }
}
