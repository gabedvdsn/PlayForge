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

            var snapshot = new ForgeSnapshot { TimeScale = core.Clock.TimeScale };
            var names = new Names(catalog);

            CaptureCommits(core, snapshot, names);
            foreach (var pair in core.LevelSystem.GetAllLevels())
            {
                if (!pair.Key.IsValid) continue;
                snapshot.Levels.Add(new LevelSave
                {
                    Key = pair.Key.Name, Level = pair.Value.CurrentValue, Min = pair.Value.MinValue, Max = pair.Value.MaxValue,
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

            var finds = new Finds(catalog);

            // What's running ends and what the entity has goes, with nothing told. So do the frame's queued actions,
            // which were for the entity as it was
            core.AbilitySystem.ClearForRestore();
            core.EffectSystem.ClearForRestore();
            core.ItemSystem.ClearForRestore();
            core.ActionQueue?.Clear();

            // An ability's or an item's spec registers its level as it's made, and the save's levels set them after
            core.LevelSystem.Quiet = true;
            try
            {
                foreach (var save in Each(snapshot.Abilities))
                {
                    if (finds.TryFind(save.Ability, out Ability ability)) core.AbilitySystem.GiveQuietly(ability, save.Level, out _, save.Slot);
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
                    core.EffectSystem.Restore(effect.Generate(origin, gas), save);
                }

                foreach (var save in Each(snapshot.Levels))
                {
                    if (string.IsNullOrEmpty(save.Key)) continue;
                    core.LevelSystem.Restore(Tag.Generate(save.Key), new IntValuePairClamped(save.Level, save.Min, save.Max));
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

        // Names assets by their IDs in the catalog, and warns once of each it doesn't name, which the save leaves out
        private sealed class Names
        {
            private readonly ForgeAssetCatalog catalog;
            private readonly HashSet<ScriptableObject> warned = new();

            public Names(ForgeAssetCatalog catalog) => this.catalog = catalog;

            public bool TryName(ScriptableObject asset, out string id)
            {
                if (catalog.TryGetId(asset, out id)) return true;
                if (asset != null && warned.Add(asset))
                {
                    ForgeLog.Warning($"{asset.name} isn't in the asset catalog {catalog.name}, so the save leaves it out. " +
                                     "Add Every PlayForge Asset, in the catalog's menu, names it.", asset);
                }
                return false;
            }
        }

        // Finds what a save names in the catalog, and warns once of each ID it can't find, which the load leaves out
        private sealed class Finds
        {
            private readonly ForgeAssetCatalog catalog;
            private readonly HashSet<string> warned = new();

            public Finds(ForgeAssetCatalog catalog) => this.catalog = catalog;

            public bool Names(ScriptableObject asset) => catalog.TryGetId(asset, out _);

            public bool TryFind<T>(string id, out T asset) where T : ScriptableObject
            {
                if (catalog.TryFind(id, out asset)) return true;
                if (!string.IsNullOrEmpty(id) && warned.Add(id))
                {
                    ForgeLog.Warning($"The save names {id}, which the asset catalog {catalog.name} has no {typeof(T).Name} for, " +
                                     "so the load leaves it out.", catalog);
                }
                return false;
            }
        }
    }
}
