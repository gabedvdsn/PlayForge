using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A tag's weight on an entity changing: the tag, and its weight before and after, 0 when it isn't held. An event
    /// carries one as it happens; a compile, which rebuilds the tags from their sources, carries only what it changed
    /// (FEAT-3).
    /// </summary>
    public readonly struct TagChange
    {
        public readonly Tag Tag;
        public readonly int Before;
        public readonly int After;

        public TagChange(Tag tag, int before, int after)
        {
            Tag = tag;
            Before = Math.Max(0, before);
            After = Math.Max(0, after);
        }

        /// <summary>Whether the entity has the tag now and didn't before.</summary>
        public bool Added => Before == 0 && After > 0;

        /// <summary>Whether the entity had the tag and doesn't now.</summary>
        public bool Removed => Before > 0 && After == 0;

        public override string ToString() => $"{Tag}: {Before} -> {After}";
    }

    /// <summary>What a tag listener hears (FEAT-3).</summary>
    public enum ETagChangeKind
    {
        /// <summary>A tag coming or going: its weight leaving 0, or reaching it.</summary>
        AddedOrRemoved,

        /// <summary>Every change to a tag's weight.</summary>
        AnyCount,
    }

    /// <summary>
    /// A listener on one tag of an entity and the tags under it, as <see cref="TagCache.Listen"/> makes: it hears
    /// <c>Status.Burning</c> when it listens to <c>Status</c>. Dispose stops it (FEAT-3).
    /// </summary>
    public sealed class TagListener : IDisposable
    {
        private TagCache cache;

        internal TagListener(TagCache cache, Tag tag, Action<TagChange> onChange, ETagChangeKind kind)
        {
            this.cache = cache;
            Tag = tag;
            OnChange = onChange;
            Kind = kind;
        }

        /// <summary>The tag it listens to, and the tags under it.</summary>
        public Tag Tag { get; }

        /// <summary>What it hears.</summary>
        public ETagChangeKind Kind { get; }

        /// <summary>Whether it still listens.</summary>
        public bool Listening => cache != null;

        internal Action<TagChange> OnChange { get; }

        internal bool Hears(TagChange change) => Kind == ETagChangeKind.AnyCount || change.Added || change.Removed;

        /// <summary>Stops the listener. Stopped during a change, it hears no more of it.</summary>
        public void Dispose()
        {
            var owner = cache;
            cache = null;
            owner?.StopListening(this);
        }
    }
}
