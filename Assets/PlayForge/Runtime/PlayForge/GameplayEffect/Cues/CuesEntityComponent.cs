using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Cues: what an entity shows and sounds at its moments: as it comes into the game, dies and comes back, and, While
    /// Tagged, for as long as it holds a tag, as stars over its head while it's Stunned. An entity holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Shows and sounds the entity's moments: as it spawns, dies and revives, and while it holds a tag.")]
    public sealed class CuesEntityComponent : AbstractEntityComponent
    {
        [Tooltip("Plays as the entity comes into the game")]
        [SerializeReference]
        public List<AbstractCueHandler> OnSpawn = new();

        [Tooltip("Plays as the entity dies, its killer behind it")]
        [SerializeReference]
        public List<AbstractCueHandler> OnDeath = new();

        [Tooltip("Plays as the entity comes back to life")]
        [SerializeReference]
        public List<AbstractCueHandler> OnRevive = new();

        [Tooltip("Lasting cues the entity plays while it holds a tag, as stars while it's Stunned")]
        public List<TaggedCues> WhileTagged = new();

        public override bool AllowsSeveral => false;

        public override string Describe()
        {
            var parts = new List<string>();
            Count(OnSpawn, "on spawn");
            Count(OnDeath, "on death");
            Count(OnRevive, "on revive");
            int tagged = 0;
            if (WhileTagged != null)
                foreach (var each in WhileTagged)
                    if (each != null && each.Tag.IsValid) tagged++;
            if (tagged > 0) parts.Add(tagged == 1 ? "1 while tagged" : $"{tagged} while tagged");
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

        public override void Register(EntityComponentContext ctx)
        {
            ForgeCues.Burst(OnSpawn, CueOf(ctx, ECueMoment.Spawned));
            if (WhileTagged != null && WhileTagged.Count > 0) ctx.System?.GetService<TaggedCueWatch>()?.Watch(WhileTagged);
        }

        public override void Death(EntityComponentContext ctx) => ForgeCues.Burst(OnDeath, CueOf(ctx, ECueMoment.Died, ctx.Killer));

        public override void Revive(EntityComponentContext ctx) => ForgeCues.Burst(OnRevive, CueOf(ctx, ECueMoment.Revived));

        // On the entity, which is behind it, but for its death, whose killer is
        private static CueContext CueOf(in EntityComponentContext ctx, ECueMoment moment, ISource source = null) =>
            new(ctx.System, moment, source: source ?? ctx.System, origin: ctx.System as IEffectOrigin, key: ctx.System);
    }

    /// <summary>The lasting cues an entity plays while it holds a tag.</summary>
    [Serializable]
    public class TaggedCues
    {
        [Tooltip("The tag the entity holds while they play")]
        [ForgeTagContext(ForgeContext.Granted)]
        public Tag Tag;

        [Tooltip("What plays while it holds the tag, begun as it takes the tag on and ended as it lets it go")]
        [SerializeReference]
        public List<AbstractCueHandler> Cues = new();
    }

    /// <summary>
    /// An entity's While Tagged cues: each step, a tag the entity has taken on begins its cues, and one it has let go
    /// of ends them; as the entity ends, every one still playing ends.
    /// </summary>
    public sealed class TaggedCueWatch : AbstractEntityService
    {
        // An entry it watches, whether its cues are playing, and the key their beginning and their end share
        private sealed class Watched
        {
            public TaggedCues Entry;
            public bool Playing;
            public readonly object Key = new();
        }

        private readonly List<Watched> watched = new();

        /// <summary>
        /// Watches the entity's tags for each entry's cues, one that holds its tag already beginning at once.
        /// </summary>
        internal void Watch(List<TaggedCues> entries)
        {
            if (entries == null) return;
            foreach (var entry in entries)
                if (entry != null && entry.Tag.IsValid && !Watching(entry)) watched.Add(new Watched { Entry = entry });
            Check();
        }

        public override void Step(float deltaTime) => Check();

        // Its entity ends: what plays ends with it
        public override void End()
        {
            foreach (var each in watched)
            {
                if (!each.Playing) continue;
                each.Playing = false;
                ForgeCues.End(each.Entry.Cues, CueOf(each));
            }
            watched.Clear();
        }

        private void Check()
        {
            var tags = System?.GetTagCache();
            if (tags == null) return;
            foreach (var each in watched)
            {
                bool holds = tags.GetWeight(each.Entry.Tag) > 0;
                if (holds == each.Playing) continue;
                each.Playing = holds;
                if (holds) ForgeCues.Begin(each.Entry.Cues, CueOf(each));
                else ForgeCues.End(each.Entry.Cues, CueOf(each));
            }
        }

        private bool Watching(TaggedCues entry)
        {
            foreach (var each in watched)
                if (ReferenceEquals(each.Entry, entry)) return true;
            return false;
        }

        private CueContext CueOf(Watched each) =>
            new(System, ECueMoment.Tagged, tag: each.Entry.Tag, source: System, origin: System as IEffectOrigin, key: each.Key, lasting: true);
    }
}
