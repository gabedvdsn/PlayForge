using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The carrier a number's scaler evaluates against where its context has none, as a task's or a process's number
    /// (FEAT-29). Scalers read an impact carrier, and most sites have none: a context fills one of these, which answers as
    /// a carrier would from what the context holds. They're kept and reused, one for each depth a read nests to, so a read
    /// allocates nothing. The leveler derivation process attributes used allocated on every read.
    /// </summary>
    internal sealed class NumberBridge : IAttributeImpactCarrier
    {
        // How deep reads nest, a scaler's number reading a scaler, before the next is refused as a loop
        internal const int MostDepth = 16;

        private static readonly List<NumberBridge> Kept = new();
        private static int depth;
        private static bool saidTooDeep;

        private static readonly List<Tag> NoImpactTypes = new();
        private static readonly List<Tag> NoTags = new();

        /// <summary>The context the carrier answers from.</summary>
        public NumberContext Context { get; private set; }

        private readonly BridgeOrigin origin = new();
        private readonly Dictionary<IScaler, AttributeValue?> captured = new();
        private readonly TrackedImpact tracked = new();

        /// <summary>A carrier for the context, from those kept, or null past <see cref="MostDepth"/>.</summary>
        internal static NumberBridge Rent(in NumberContext context)
        {
            if (depth >= MostDepth)
            {
                if (!saidTooDeep)
                {
                    saidTooDeep = true;
                    ForgeLog.Warning($"A number read numbers {MostDepth} deep, as a loop of numbers would, so the deepest reads 0 (FEAT-29).");
                }
                return null;
            }
            if (Kept.Count <= depth) Kept.Add(new NumberBridge());
            var bridge = Kept[depth++];
            bridge.Fill(in context);
            return bridge;
        }

        /// <summary>Gives a carrier back, once its read is done.</summary>
        internal static void Return(NumberBridge bridge)
        {
            if (bridge == null || depth == 0) return;
            depth--;
            bridge.Context = default;
            bridge.origin.Fill(null, default);
            bridge.captured.Clear();
        }

        private void Fill(in NumberContext context)
        {
            Context = context;
            origin.Fill(context.Caster, context.Level);
            captured.Clear();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // A carrier's answers, from the context
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The context's origin, or one that gives its caster and its level.</summary>
        public IEffectOrigin GetOrigin() => Context.Origin ?? origin;

        /// <summary>The context's target, or, where it has none, its caster, as the leveler derivation answered.</summary>
        public ITarget GetTarget() => Context.Target ?? Context.Caster;

        public IHasReadableDefinition GetReadableDefinition() => Context.Origin?.GetReadableDefinition() ?? Context.Caster;
        public bool DerivationAlive() => false;
        public IAttribute GetAttribute() => null;
        public List<Tag> GetImpactTypes() => NoImpactTypes;
        public Tag GetRetentionGroup() => Tags.IgnoreRetention;
        public void TrackImpact(ImpactData impactData) { }
        public TrackedImpact GetTrackedImpact() => tracked;
        public ImpactDerivationContext GetContextTags() => new(GetOrigin().GetContextTags() ?? NoTags, null);
        public Dictionary<IScaler, AttributeValue?> GetSourcedCapturedAttributes() => captured;
        public bool RetainImpact() => false;
        public IEnumerable<Tag> GetGrantedTags() => Array.Empty<Tag>();

        /// <summary>The origin a context with none gives: its caster, at its level.</summary>
        private sealed class BridgeOrigin : IEffectOrigin
        {
            private ISource caster;
            private IntValuePairClamped level;

            public void Fill(ISource caster, IntValuePairClamped level)
            {
                this.caster = caster;
                this.level = level;
            }

            public ISource GetSource() => caster;
            public IHasReadableDefinition GetReadableDefinition() => caster;
            public List<Tag> GetContextTags() => caster?.GetContextTags() ?? NoTags;
            public Tag GetAssetTag() => caster?.GetAssetTag() ?? default;
            public IntValuePairClamped GetLevel() => level;
            public bool IsActive() => true;
            public List<Tag> GetAffiliation() => caster?.GetAffiliation() ?? NoTags;
        }
    }
}
