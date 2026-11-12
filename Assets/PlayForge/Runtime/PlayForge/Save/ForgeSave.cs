using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Names the entities a save refers to besides the one it's of: the source of an effect another entity applied. A game
    /// keeps IDs that last across sessions, as a reference doesn't (FEAT-9).
    /// </summary>
    public interface IForgeEntityIds
    {
        /// <summary>The entity's ID, or null where it has none.</summary>
        string IdOf(IGameplayAbilitySystem entity);

        /// <summary>The entity an ID names, or null where there's none, as once it's gone.</summary>
        IGameplayAbilitySystem Find(string id);
    }

    /// <summary>
    /// Names a save's assets and finds them again: an asset catalog, or, for a copy within one session, the assets
    /// themselves (FEAT-20).
    /// </summary>
    internal interface IAssetNames
    {
        /// <summary>What names the assets, as a warning says it.</summary>
        string Label { get; }

        /// <summary>The object a warning points at, or null.</summary>
        UnityEngine.Object Context { get; }

        bool TryGetId(ScriptableObject asset, out string id);

        bool TryFind<T>(string id, out T asset) where T : ScriptableObject;
    }

    /// <summary>
    /// Saves an entity at rest and loads it back (FEAT-9, Decision 30). Nothing captured an entity's state, so a game
    /// couldn't keep a battle between sessions.
    ///
    /// <para><see cref="Capture"/> takes what lasts on an entity into a <see cref="ForgeSnapshot"/>: its attributes'
    /// permanent commits, its levels, the abilities it knows, its items with their stacks, worn or not, its effects with
    /// their time left, its loose tags and its time scale. Modifiers aren't saved: they come back with the effects that
    /// carry them. <see cref="Restore"/> ends what's running, lets go of what the entity has, and puts back what the save
    /// had, on the entity it was taken from or a fresh one of its kind.</para>
    ///
    /// <para>A restore tells nothing as it goes. No effect is told as applied or removed, no attribute as changed, and
    /// nothing of an effect's application runs again: its workers, its commits on application, its contained effects. It
    /// tells <see cref="GameplayAbilitySystemCallbacks.OnRestored"/> once, as it ends, for a listener to read the entity
    /// afresh.</para>
    /// </summary>
    public static class ForgeSave
    {
        /// <summary>The save format <see cref="Capture"/> writes and <see cref="Restore"/> reads.</summary>
        public const int Version = 1;

        // ═══════════════════════════════════════════════════════════════════════════
        // Capture
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Takes what lasts on the entity, naming its assets by their IDs in <paramref name="catalog"/>. An asset the
        /// catalog doesn't name is left out, with a warning. Null for a hub not yet built from an entity.
        /// </summary>
        /// <param name="ids">
        /// Names the other entities whose effects are on this one, so a load finds them again. Without it, or where it
        /// can't name one, such an effect comes back as the entity's own.
        /// </param>
        public static ForgeSnapshot Capture(IGameplayAbilitySystem gas, ForgeAssetCatalog catalog, IForgeEntityIds ids = null)
        {
            if (gas == null) throw new ArgumentNullException(nameof(gas));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));

            var core = CoreOf(gas);
            if (core?.Identity == null)
            {
                ForgeLog.Warning("Only a GAS hub built from an entity can be saved.");
                return null;
            }

            return Take(core, catalog, ids);
        }

        // What Capture and Copy take: the entity at rest, its assets named by a catalog or by the session (FEAT-20)
        private static ForgeSnapshot Take(GASCore core, IAssetNames assets, IForgeEntityIds ids)
        {
            var gas = core.Owner;
            var snapshot = new ForgeSnapshot { TimeScale = core.Clock.TimeScale };
            var names = new Names(assets);

            CaptureCommits(core, snapshot, names);
            foreach (var pair in core.LevelSystem.GetAllLevels())
            {
                if (!pair.Key.IsValid) continue;

                // The entity's own under no key, so a save of one kind loads onto another as its own. Under its kind's
                // tag, it stayed behind on a clone of another kind (FEAT-20)
                snapshot.Levels.Add(new LevelSave
                {
                    Key = pair.Key == core.Identity.AssetTag ? "" : pair.Key.Name,
                    Level = pair.Value.CurrentValue, Min = pair.Value.MinValue, Max = pair.Value.MaxValue,
                });
            }
            CaptureAbilities(core, snapshot, names);
            var itemPlaces = CaptureItems(core, snapshot, names);
            CaptureEffects(gas, core, snapshot, names, ids, itemPlaces);
            foreach (var tag in core.LooseTags) snapshot.LooseTags.Add(tag.Name);

            return snapshot;
        }

        private static void CaptureCommits(GASCore core, ForgeSnapshot snapshot, Names names)
        {
            var cache = core.AttributeSystem.GetAttributeCache();
            if (cache == null) return;

            var commits = new List<GroupContribution>();
            foreach (var pair in cache)
            {
                commits.Clear();
                pair.Value.GetCommits(commits);
                if (commits.Count == 0 || !names.TryName(pair.Key as ScriptableObject, out string id)) continue;

                var save = new AttributeSave { Attribute = id };
                foreach (var commit in commits)
                {
                    save.Commits.Add(new CommitSave
                    {
                        Group = commit.Group.Name, Current = commit.Value.CurrentValue, Base = commit.Value.BaseValue,
                    });
                }
                snapshot.Attributes.Add(save);
            }
        }

        // The abilities the entity knows of its own, by slot. One an effect or a worn item lends comes back with that
        private static void CaptureAbilities(GASCore core, ForgeSnapshot snapshot, Names names)
        {
            foreach (var container in core.AbilitySystem.Containers)
            {
                if (core.EffectSystem.Lends(container.Index) || core.ItemSystem.Lends(container.Index)) continue;
                if (!names.TryName(container.Spec.Base, out string id)) continue;
                snapshot.Abilities.Add(new AbilitySave { Ability = id, Slot = container.Index, Level = container.Spec.GetLevel().CurrentValue });
            }
            snapshot.Abilities.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        }

        // The items in their slots' order, and each one's place in the save, which an effect it applied names
        private static Dictionary<ItemSpec, int> CaptureItems(GASCore core, ForgeSnapshot snapshot, Names names)
        {
            var places = new Dictionary<ItemSpec, int>();
            foreach (var container in core.ItemSystem.GetItemContainers())
            {
                if (!names.TryName(container.Spec.Base, out string id)) continue;
                places[container.Spec] = snapshot.Items.Count;
                snapshot.Items.Add(new ItemSave
                {
                    Item = id, Level = container.Spec.GetLevel().CurrentValue, Stacks = container.ItemStacks, Equipped = container.IsEquipped,
                });
            }
            return places;
        }

        private static void CaptureEffects(IGameplayAbilitySystem gas, GASCore core, ForgeSnapshot snapshot, Names names,
            IForgeEntityIds ids, Dictionary<ItemSpec, int> itemPlaces)
        {
            var effects = core.EffectSystem;
            foreach (var container in effects.Shelf)
            {
                // One leaving at the next tick is gone already
                if (effects.IsLeaving(container) || !names.TryName(container.Spec.Base, out string id)) continue;

                var save = new EffectSave
                {
                    Effect = id,
                    Origin = SaveOrigin(gas, container.Spec.Origin, names, ids, itemPlaces),
                    Ongoing = container.Ongoing,
                    Stacks = (container as AbstractStackingEffectContainer)?.Stacks ?? 0,
                    TotalDuration = container.TotalDuration,
                    PeriodDuration = container.PeriodDuration,
                };
                container.CaptureClock(save.Remaining, save.UntilTick);
                foreach (var pair in container.Spec.SetByCallerMagnitudes)
                    save.SetByCaller.Add(new MagnitudeSave { Key = pair.Key.Name, Magnitude = pair.Value });
                snapshot.Effects.Add(save);
            }
        }

        // What applied an effect, as a save names it. Another entity the IDs can't name stands as the entity's own
        private static OriginSave SaveOrigin(IGameplayAbilitySystem gas, IEffectOrigin origin, Names names, IForgeEntityIds ids,
            Dictionary<ItemSpec, int> itemPlaces)
        {
            var save = new OriginSave { Kind = ESavedOrigin.Entity };
            var owner = origin?.GetSource()?.ToGAS();
            if (owner == null) return save;
            if (!ReferenceEquals(owner, gas))
            {
                string entity = ids?.IdOf(owner);
                if (string.IsNullOrEmpty(entity)) return save;
                save.Entity = entity;
            }

            switch (origin)
            {
                case AbilitySpec ability when names.TryName(ability.Base, out string abilityId):
                    save.Kind = ESavedOrigin.Ability;
                    save.Asset = abilityId;
                    save.Slot = SlotOf(owner.GetAbilitySystem(), ability);
                    break;
                case ItemSpec item when names.TryName(item.Base, out string itemId):
                    save.Kind = ESavedOrigin.Item;
                    save.Asset = itemId;
                    save.Slot = ReferenceEquals(owner, gas)
                        ? itemPlaces.TryGetValue(item, out int place) ? place : -1
                        : SlotOf(owner.GetItemSystem(), item);
                    break;
                case LevelerEffectOrigin leveler:
                    save.Kind = ESavedOrigin.Leveler;
                    save.Level = leveler.Level.CurrentValue;
                    save.MinLevel = leveler.Level.MinValue;
                    save.MaxLevel = leveler.Level.MaxValue;
                    break;
                case IGameplayAbilitySystem:
                    save.Kind = ESavedOrigin.Entity;
                    break;
                default:
                    // An origin standing for the entity, or an ability or item the catalog doesn't name
                    save.Kind = ESavedOrigin.Source;
                    break;
            }
            return save;
        }

        private static int SlotOf(AbilitySystemComponent abilities, AbilitySpec spec)
        {
            if (abilities == null) return -1;
            foreach (var container in abilities.Containers)
                if (container.Spec == spec) return container.Index;
            return -1;
        }

        private static int SlotOf(ItemSystemComponent items, ItemSpec spec)
        {
            if (items == null) return -1;
            foreach (var container in items.GetItemContainers())
                if (container.Spec == spec) return container.Index;
            return -1;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Restore
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads a save onto the entity. What's running ends and the entity lets go of what it has, then what the save had
        /// comes back: its abilities, items and effects, its levels and commits, its loose tags and time scale. One
        /// compile of the tags tells what the load changed, and <see cref="GameplayAbilitySystemCallbacks.OnRestored"/>
        /// fires once. False where the save can't load, as onto a hub not yet built from an entity.
        /// </summary>
        /// <param name="ids">
        /// Finds the other entities the save names as effects' sources. An effect whose source it can't find, as one gone
        /// since, comes back as the entity's own. Restore those entities first, so their abilities and items are there.
        /// </param>
        public static bool Restore(IGameplayAbilitySystem gas, ForgeSnapshot snapshot, ForgeAssetCatalog catalog, IForgeEntityIds ids = null)
        {
            if (gas == null || snapshot == null || catalog == null) return false;

            var core = CoreOf(gas);
            return CanLoad(core, snapshot) && Put(core, snapshot, catalog, ids);
        }

        // Whether a save can load onto the entity now, and why not where it can't
        private static bool CanLoad(GASCore core, ForgeSnapshot snapshot)
        {
            if (core?.Identity == null)
            {
                ForgeLog.Warning("A save loads only onto a GAS hub built from an entity.");
                return false;
            }
            if (snapshot.Version > Version)
            {
                ForgeLog.Warning($"The save's format is version {snapshot.Version}, and this PlayForge reads up to {Version}.");
                return false;
            }

            // From inside the entity's step, as one of its callbacks, the load would empty the lists the step is walking
            if (core.Stepping)
            {
                ForgeLog.Warning("A save loads between the entity's steps, not inside one, so it isn't loaded. Load it " +
                                 "outside the entity's callbacks, or on the next frame.");
                return false;
            }
            return true;
        }

        // What Restore and Copy put back (FEAT-20)
        private static bool Put(GASCore core, ForgeSnapshot snapshot, IAssetNames assets, IForgeEntityIds ids)
        {
            var gas = core.Owner;
            var finds = new Finds(assets);

            // An ability the entity holds in the slot its save gives it stays as it is, so another entity's effect that
            // it applied still finds it there (GAS-60)
            var keep = new Dictionary<int, Ability>();
            foreach (var save in Each(snapshot.Abilities))
                if (save.Slot >= 0 && finds.TryFind(save.Ability, out Ability held)) keep[save.Slot] = held;

            // What's running ends and the rest of what the entity has goes, with nothing told. So do the frame's queued
            // actions, which were for the entity as it was
            core.AbilitySystem.ClearForRestore(keep);
            core.EffectSystem.ClearForRestore();
            core.ItemSystem.ClearForRestore();
            core.ActionQueue?.Clear();

            // An ability's or an item's spec registers its level as it's made, and the save's levels set them after
            core.LevelSystem.Quiet = true;
            try
            {
                foreach (var save in Each(snapshot.Abilities))
                {
                    if (!finds.TryFind(save.Ability, out Ability ability)) continue;

                    // One kept in its slot is there already, its level from the save's levels below
                    if (core.AbilitySystem.TryGetAbilityContainer(save.Slot, out var kept) && kept.Spec.Base == ability) continue;
                    core.AbilitySystem.GiveQuietly(ability, save.Level, out _, save.Slot);
                }

                // In the save's order, null where one isn't found, so an effect from an item finds it by its place
                var items = new List<ItemSpecContainer>();
                foreach (var save in Each(snapshot.Items))
                {
                    items.Add(finds.TryFind(save.Item, out Item item)
                        ? core.ItemSystem.Restore(item, save.Level, save.Stacks, save.Equipped)
                        : null);
                }

                foreach (var save in Each(snapshot.Effects))
                {
                    if (!finds.TryFind(save.Effect, out GameplayEffect effect)) continue;
                    var origin = OriginOf(gas, save.Origin, finds, ids, items);
                    var spec = effect.Generate(origin, gas);

                    // With the magnitudes its caller set, before its modifiers read them (FEAT-2)
                    foreach (var magnitude in Each(save.SetByCaller))
                        if (!string.IsNullOrEmpty(magnitude.Key)) spec.SetByCaller(Tag.Generate(magnitude.Key), magnitude.Magnitude);
                    core.EffectSystem.Restore(spec, save);
                }

                // The entity's own, under no key, onto this entity's own (FEAT-20)
                foreach (var save in Each(snapshot.Levels))
                {
                    var key = string.IsNullOrEmpty(save.Key) ? core.Identity.AssetTag : Tag.Generate(save.Key);
                    core.LevelSystem.Restore(key, new IntValuePairClamped(save.Level, save.Min, save.Max));
                }
            }
            finally
            {
                core.LevelSystem.Quiet = false;
            }

            RestoreCommits(core.AttributeSystem, snapshot, finds);

            // With everything back, each attribute reads it: one derived from another after that one, and each modifier
            // the level it reads
            core.AttributeSystem.RecomputeAll();

            // The effects' lasting transfers take from what the load put back, and the attributes read them (TW-20)
            if (core.EffectSystem.LendRestoredTransfers()) core.AttributeSystem.RecomputeAll();

            var loose = new List<Tag>();
            foreach (string name in Each(snapshot.LooseTags))
            {
                if (!string.IsNullOrEmpty(name)) loose.Add(Tag.Generate(name));
            }
            core.RestoreLooseTags(loose);
            core.Clock.TimeScale = snapshot.TimeScale;

            core.CompileGrantedTags();
            core.Callbacks?.Restored();
            return true;
        }

        // Each attribute the catalog names takes the save's commits, or none where the save lists none. One it doesn't name
        // the save couldn't have, so it keeps its own
        private static void RestoreCommits(AttributeSystemComponent attributes, ForgeSnapshot snapshot, Finds finds)
        {
            var cache = attributes.GetAttributeCache();
            if (cache == null) return;

            var saved = new Dictionary<IAttribute, AttributeSave>();
            foreach (var save in Each(snapshot.Attributes))
            {
                if (finds.TryFind(save.Attribute, out Attribute attribute)) saved[attribute] = save;
            }

            var commits = new List<GroupContribution>();
            foreach (var pair in cache)
            {
                if (pair.Key is not ScriptableObject asset || !finds.Names(asset)) continue;

                commits.Clear();
                if (saved.TryGetValue(pair.Key, out var save))
                {
                    foreach (var commit in Each(save.Commits))
                        commits.Add(new GroupContribution(Tag.Generate(commit.Group), new AttributeValue(commit.Current, commit.Base)));
                }
                pair.Value.RestoreCommits(commits, cache);
            }
        }

        // What applied a saved effect, found again. An ability or item that's gone leaves the entity it stood for, and an
        // entity that's gone leaves the effect as this one's own
        private static IEffectOrigin OriginOf(IGameplayAbilitySystem gas, OriginSave save, Finds finds, IForgeEntityIds ids,
            List<ItemSpecContainer> items)
        {
            if (save == null) return gas;
            var owner = string.IsNullOrEmpty(save.Entity) ? gas : ids?.Find(save.Entity);
            if (owner == null) return gas;

            switch (save.Kind)
            {
                case ESavedOrigin.Entity:
                    return owner;
                case ESavedOrigin.Leveler:
                    return IEffectOrigin.GenerateLevelerDerivation(owner, new IntValuePairClamped(save.Level, save.MinLevel, save.MaxLevel));
                case ESavedOrigin.Ability:
                    if (finds.TryFind(save.Asset, out Ability ability) && FindAbility(owner, ability, save.Slot) is { } abilitySpec) return abilitySpec;
                    break;
                case ESavedOrigin.Item:
                    if (finds.TryFind(save.Asset, out Item item) && FindItem(gas, owner, item, save.Slot, items) is { } itemSpec) return itemSpec;
                    break;
            }
            return IEffectOrigin.GenerateSourceDerivation(owner);
        }

        // In its slot, where that still holds it, or else the first copy
        private static AbilitySpec FindAbility(IGameplayAbilitySystem owner, Ability ability, int slot)
        {
            var abilities = owner.GetAbilitySystem();
            if (abilities == null) return null;
            if (abilities.TryGetAbilityContainer(slot, out var container) && container.Spec.Base == ability) return container.Spec;
            return abilities.TryGetAbilityContainer(ability, out container) ? container.Spec : null;
        }

        // This entity's, by its place in the save; another's, by its slot; else the first copy
        private static ItemSpec FindItem(IGameplayAbilitySystem gas, IGameplayAbilitySystem owner, Item item, int slot, List<ItemSpecContainer> restored)
        {
            if (ReferenceEquals(owner, gas))
            {
                if (slot >= 0 && slot < restored.Count && restored[slot] != null && restored[slot].Spec.Base == item) return restored[slot].Spec;
            }
            else if (owner.GetItemSystem() is { } other && other.TryGetItemContainer(slot, out var at) && at.Spec.Base == item)
            {
                return at.Spec;
            }

            var system = owner.GetItemSystem();
            return system != null && system.TryGetItemContainer(item, out var first) ? first.Spec : null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Copy
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Copies what lasts on one entity onto another, as a mirror clone takes its original's state: what
        /// <see cref="Capture"/> takes, put back as <see cref="Restore"/> puts it, with no catalog and no IDs, as both
        /// entities are in this session. An effect another entity applied stays that entity's, and the entity's own level
        /// goes onto the other's own. A copy took a catalog naming every asset, which an asset made as the game runs
        /// never has (FEAT-20).
        /// </summary>
        public static bool Copy(IGameplayAbilitySystem from, IGameplayAbilitySystem onto)
        {
            if (from == null || onto == null || ReferenceEquals(from, onto)) return false;

            var source = CoreOf(from);
            if (source?.Identity == null)
            {
                ForgeLog.Warning("Only a GAS hub built from an entity can be copied.");
                return false;
            }

            var assets = new SessionNames();
            var ids = new SessionIds();
            var snapshot = Take(source, assets, ids);
            var target = CoreOf(onto);
            return CanLoad(target, snapshot) && Put(target, snapshot, assets, ids);
        }

        // Names each asset as it's asked for, and finds it again, within one session: a copy's catalog
        private sealed class SessionNames : IAssetNames
        {
            private readonly Dictionary<ScriptableObject, string> idOf = new();
            private readonly Dictionary<string, ScriptableObject> byId = new();

            public string Label => "the copy";

            public UnityEngine.Object Context => null;

            public bool TryGetId(ScriptableObject asset, out string id)
            {
                id = null;
                if (asset == null) return false;
                if (idOf.TryGetValue(asset, out id)) return true;
                id = "#" + idOf.Count;
                idOf[asset] = id;
                byId[id] = asset;
                return true;
            }

            public bool TryFind<T>(string id, out T asset) where T : ScriptableObject
            {
                asset = null;
                if (id == null || !byId.TryGetValue(id, out var found)) return false;
                asset = found as T;
                return asset != null;
            }
        }

        // Names each entity as it's asked for, and finds it again, within one session: a copy's IDs
        private sealed class SessionIds : IForgeEntityIds
        {
            private readonly Dictionary<IGameplayAbilitySystem, string> idOf = new();
            private readonly Dictionary<string, IGameplayAbilitySystem> byId = new();

            public string IdOf(IGameplayAbilitySystem entity)
            {
                if (entity == null) return null;
                if (idOf.TryGetValue(entity, out string id)) return id;
                id = "#" + idOf.Count;
                idOf[entity] = id;
                byId[id] = entity;
                return id;
            }

            public IGameplayAbilitySystem Find(string id) => id != null && byId.TryGetValue(id, out var entity) ? entity : null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Helpers
        // ═══════════════════════════════════════════════════════════════════════════

        // The core a hub forwards to, which a save reads and writes past the hub's public face
        private static GASCore CoreOf(IGameplayAbilitySystem gas) => gas switch
        {
            GameplayAbilitySystem mono => mono.Core,
            RuntimeGameplayAbilitySystem runtime => runtime.Core,
            _ => null,
        };

        // A list a hand-made or older save left null reads as empty
        private static IEnumerable<T> Each<T>(List<T> list) => list ?? (IEnumerable<T>)Array.Empty<T>();

        // Names assets by their IDs, and warns once of each it doesn't name, which the save leaves out
        private sealed class Names
        {
            private readonly IAssetNames assets;
            private readonly HashSet<ScriptableObject> warned = new();

            public Names(IAssetNames assets) => this.assets = assets;

            public bool TryName(ScriptableObject asset, out string id)
            {
                if (assets.TryGetId(asset, out id)) return true;
                if (asset != null && warned.Add(asset))
                {
                    ForgeLog.Warning($"{asset.name} isn't in {assets.Label}, so the save leaves it out. " +
                                     "Add Every PlayForge Asset, in the catalog's menu, names it.", asset);
                }
                return false;
            }
        }

        // Finds what a save names, and warns once of each ID it can't find, which the load leaves out
        private sealed class Finds
        {
            private readonly IAssetNames assets;
            private readonly HashSet<string> warned = new();

            public Finds(IAssetNames assets) => this.assets = assets;

            public bool Names(ScriptableObject asset) => assets.TryGetId(asset, out _);

            public bool TryFind<T>(string id, out T asset) where T : ScriptableObject
            {
                if (assets.TryFind(id, out asset)) return true;
                if (!string.IsNullOrEmpty(id) && warned.Add(id))
                {
                    ForgeLog.Warning($"The save names {id}, which {assets.Label} has no {typeof(T).Name} for, " +
                                     "so the load leaves it out.", assets.Context);
                }
                return false;
            }
        }
    }
}
