namespace FarEmerald.PlayForge
{
    /// <summary>What a raise's list of cues belongs to.</summary>
    public enum ECueSource
    {
        /// <summary>A game's own code raised it.</summary>
        Code,

        /// <summary>An effect's cues: its asset the effect.</summary>
        Effect,

        /// <summary>An ability's cues, its Cues component's or a Play Cue task's: its asset the ability.</summary>
        Ability,

        /// <summary>An entity's cues: its asset the entity's identity.</summary>
        Entity,

        /// <summary>An item's cues: its asset the item.</summary>
        Item,

        /// <summary>A cue tag raised through the project's routes: its tag the one raised.</summary>
        Tag,
    }

    /// <summary>
    /// What a raise's list of cues belongs to: an effect, an ability, an entity or an item, by its asset; a cue tag; or a
    /// game's own code. A network layer names it by the asset's ID, or the tag's, in place of the cues themselves.
    /// </summary>
    public readonly struct CueSource
    {
        public readonly ECueSource Kind;

        /// <summary>The asset whose cues they are, for an effect's, an ability's, an entity's or an item's.</summary>
        public readonly BaseForgeAsset Asset;

        /// <summary>The cue tag raised, for a tag's.</summary>
        public readonly Tag Tag;

        public CueSource(ECueSource kind, BaseForgeAsset asset = null, Tag tag = default)
        {
            Kind = kind;
            Asset = asset;
            Tag = tag;
        }

        /// <summary>A game's own code.</summary>
        public static CueSource Code => default;

        public static CueSource Of(GameplayEffect effect) => new(ECueSource.Effect, effect);
        public static CueSource Of(Ability ability) => new(ECueSource.Ability, ability);
        public static CueSource Of(EntityIdentity entity) => new(ECueSource.Entity, entity);
        public static CueSource Of(Item item) => new(ECueSource.Item, item);
        public static CueSource Of(Tag tag) => new(ECueSource.Tag, tag: tag);

        public override string ToString() => Kind switch
        {
            ECueSource.Code => "code",
            ECueSource.Tag => $"tag {Tag}",
            _ => $"{Kind} {(Asset != null ? Asset.GetName() : "none")}",
        };
    }

    /// <summary>One raise of a list of cues, as a sink is asked of it: the cue, and what the list belongs to.</summary>
    public readonly struct CueRaise
    {
        public readonly CueContext Cue;
        public readonly CueSource Source;

        public CueRaise(in CueContext cue, CueSource source)
        {
            Cue = cue;
            Source = source;
        }
    }

    /// <summary>
    /// What a world asks once for each raise of a list of cues, before any of them plays: whether it plays here. The
    /// default plays everything; a network layer's says what a server plays, what it sends on, and what a client has
    /// played ahead. Give a world one of its own with <see cref="ForgeCues.UseSink"/>.
    /// </summary>
    public class ForgeCueSink
    {
        /// <summary>
        /// Whether the raise plays here: every cue of the list, as it says, or none. Refusing an end leaves what its
        /// beginning placed playing.
        /// </summary>
        public virtual bool WillPlay(ECueEvent cueEvent, in CueRaise raise) => true;
    }
}
