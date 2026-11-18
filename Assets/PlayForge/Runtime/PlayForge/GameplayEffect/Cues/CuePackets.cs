using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The data a cue's process or sequence runs with, loaded for what raised it. An ability's cue starts from its
    /// activation's packet: its targets, its targeting data and the numbers its caller set. On it go the cue itself
    /// (<see cref="Tags.CUE"/>), its asset (<see cref="Tags.ASSET"/>) and its spec (<see cref="Tags.SPEC"/>), its source,
    /// the one it plays on as its target where the packet names none, where it plays, and its owner's clock; then the
    /// asset's Local Data, its numbers read for the cue, as at its level; then the cue's own Data list. Each step's keys
    /// take the place of an earlier step's, so the cue's own data has the last word.
    /// </summary>
    public static class CuePackets
    {
        // Where a cue plays where nothing says otherwise: on the one it's on, or at its point
        private static readonly CuePlacement Default = new();

        /// <summary>
        /// A packet of its own for a cue's process or sequence, loaded for the cue: placed at <paramref name="place"/>, or
        /// where the cue plays by default, its Position and Rotation in place of its activation's, and under what it
        /// follows, its Transform; with <paramref name="data"/>, the cue's own Data list, loaded last.
        /// </summary>
        public static SequenceDataPacket For(in CueContext cue, IReadOnlyList<DataWrapper> data = null, CuePlace? place = null)
        {
            // An ability's cue starts from its activation's packet, in lists of its own, with the numbers its caller set
            var packet = cue.Data != null ? new SequenceDataPacket(cue.Data) : SequenceDataPacket.Default();
            packet.Clock = cue.Data != null ? ForgeTime.ClockFor(cue.Data) : ForgeTime.ClockOf(cue.Target);

            Put(packet, Tags.CUE, cue);
            var asset = AssetOf(cue);
            if (asset != null) Put(packet, Tags.ASSET, asset);
            if (SpecOf(cue) is { } spec) Put(packet, Tags.SPEC, spec);
            if (cue.Source != null) Put(packet, Tags.SOURCE, cue.Source);
            if (!packet.ContainsKey(Tags.TARGET_REAL) && cue.Target is ITarget target) Put(packet, Tags.TARGET_REAL, target);

            var at = place;
            if (!at.HasValue && Default.Resolve(cue, cue.Lasting) is { Count: > 0 } places) at = places[0];
            if (at is { } where)
            {
                Put(packet, Tags.POSITION, where.Position);
                Put(packet, Tags.ROTATION, where.Rotation);
                // A process placed to follow what it's at goes under it; one placed at a point stays where it's put
                if (where.Parent != null) Put(packet, Tags.TRANSFORM, where.Parent);
                else packet.Remove(Tags.TRANSFORM);
            }

            var context = NumberContext.Of(in cue);
            Load(packet, LocalDataOf(cue, asset), context);
            Load(packet, data, context);
            return packet;
        }

        // A value under its key in place of whatever an earlier step put there
        private static void Put<T>(ProcessDataPacket packet, Tag key, T value)
        {
            packet.Remove(key);
            packet.AddPayload(key, value);
        }

        // Each entry under its key, in place of what an earlier step put there, its number read for the cue; an entry with
        // no key or no value puts nothing
        private static void Load(ProcessDataPacket packet, IReadOnlyList<DataWrapper> entries, in NumberContext context)
        {
            if (entries == null) return;
            HashSet<Tag> taken = null;
            foreach (var entry in entries)
            {
                if (entry == null || !entry.Key.IsValid || ValueOf(entry, context) is not { } value) continue;
                if ((taken ??= new HashSet<Tag>()).Add(entry.Key)) packet.Remove(entry.Key);
                packet.AddPayload(entry.Key, value);
            }
        }

        // An entry's value, as its kind reads it; a number read for the cue; a reference left empty, none
        private static object ValueOf(DataWrapper entry, in NumberContext context)
        {
            object value = entry.Value?.Read(in context);
            return value is Object reference && reference == null ? null : value;
        }

        // The asset behind the cue: its effect, its ability, its item, or the entity's identity
        private static BaseForgeAsset AssetOf(in CueContext cue)
        {
            if (cue.Spec != null) return cue.Spec.Base;
            return cue.Origin switch
            {
                AbilitySpec ability => ability.Base,
                ItemSpec item => item.Base,
                IGameplayAbilitySystem entity => GASCore.Of(entity)?.Identity?.Asset,
                _ => null,
            };
        }

        // The spec behind it: its effect's, its ability's or its item's; an entity's cue has none
        private static object SpecOf(in CueContext cue)
        {
            if (cue.Spec != null) return cue.Spec;
            return cue.Origin is AbilitySpec or ItemSpec ? cue.Origin : null;
        }

        // The asset's Local Data; an entity made at runtime with no asset keeps its own on its identity
        private static IReadOnlyList<DataWrapper> LocalDataOf(in CueContext cue, BaseForgeAsset asset)
        {
            if (asset != null) return asset.GetLocalData();
            return cue.Origin is IGameplayAbilitySystem entity ? GASCore.Of(entity)?.Identity?.GetLocalData() : null;
        }
    }
}
