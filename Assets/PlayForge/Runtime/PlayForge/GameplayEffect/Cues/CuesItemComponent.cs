using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Cues: what an item shows and sounds on its holder: as it's equipped, while it's equipped, and as it's used, as
    /// an ability it lends is. A load that puts an equipped item back begins its lasting cues and plays no burst. An
    /// item holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Shows and sounds the item's moments on its holder: as it's equipped, while it's equipped, and as it's used.")]
    public sealed class CuesItemComponent : AbstractItemComponent
    {
        [Tooltip("Plays as the item is equipped")]
        [SerializeReference]
        public List<AbstractCueHandler> OnEquip = new();

        [Tooltip("Lasts while the item is equipped")]
        [SerializeReference]
        public List<AbstractCueHandler> WhileEquipped = new();

        [Tooltip("Plays as the item is used, as an ability it lends is")]
        [SerializeReference]
        public List<AbstractCueHandler> OnUse = new();

        public override bool AllowsSeveral => false;

        public override string Describe()
        {
            var parts = new List<string>();
            Count(OnEquip, "on equip");
            Count(WhileEquipped, "while equipped");
            Count(OnUse, "on use");
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

        public override void Equipped(ItemComponentContext ctx)
        {
            // A load puts an equipped item back: its lasting cues begin, and no burst plays
            if (!ctx.Restoring) ForgeCues.Burst(OnEquip, CueOf(ctx, ECueMoment.Equipped), From(ctx));
            ForgeCues.Begin(WhileEquipped, CueOf(ctx, ECueMoment.Equipped, lasting: true), From(ctx));
        }

        public override void Unequipped(ItemComponentContext ctx) => ForgeCues.End(WhileEquipped, CueOf(ctx, ECueMoment.Equipped, lasting: true), From(ctx));

        public override void Used(ItemComponentContext ctx) => ForgeCues.Burst(OnUse, CueOf(ctx, ECueMoment.Used), From(ctx));

        // On its holder, from the item as its holder has it, which keys its lasting cues
        private static CueContext CueOf(in ItemComponentContext ctx, ECueMoment moment, bool lasting = false)
        {
            var holder = ctx.Source;
            return new CueContext(holder as IGameplayAbilitySystem, moment, source: holder, origin: ctx.Container?.Spec,
                key: ctx.Container, lasting: lasting);
        }

        // The item whose cues they are
        private static CueSource From(in ItemComponentContext ctx) => CueSource.Of(ctx.Container?.Spec?.Base);
    }
}
