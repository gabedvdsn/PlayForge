using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An entity at rest, as <see cref="ForgeSave.Capture"/> takes it and <see cref="ForgeSave.Restore"/> puts it back: its
    /// attributes' permanent commits, its levels, abilities, items and effects, its loose tags and its time scale. Plain
    /// serializable types, so a game writes a save where it likes, through <c>JsonUtility</c> or its own serializer. Assets
    /// are named by their IDs in a <see cref="ForgeAssetCatalog"/> and tags by name, which last across sessions as a
    /// reference doesn't (FEAT-9).
    /// </summary>
    [Serializable]
    public class ForgeSnapshot
    {
        /// <summary>The save's format, for a game to read an older one by.</summary>
        public int Version = ForgeSave.Version;

        /// <summary>The attributes with permanent commits. One the save doesn't list had none.</summary>
        public List<AttributeSave> Attributes = new();

        public List<LevelSave> Levels = new();

        /// <summary>The abilities the entity knows of its own. One an effect or a worn item lends comes back with that.</summary>
        public List<AbilitySave> Abilities = new();

        /// <summary>The items, in their slots' order.</summary>
        public List<ItemSave> Items = new();

        /// <summary>The effects on the entity's shelf, in its order.</summary>
        public List<EffectSave> Effects = new();

        /// <summary>The tags granted to the entity directly, by name (FEAT-16).</summary>
        public List<string> LooseTags = new();

        /// <summary>How fast the entity's time runs against the game's (FEAT-7).</summary>
        public float TimeScale = 1f;
    }

    /// <summary>An attribute's permanent commits: each retention group's total, in the order the groups first committed.</summary>
    [Serializable]
    public class AttributeSave
    {
        /// <summary>The attribute's ID in the catalog.</summary>
        public string Attribute;

        public List<CommitSave> Commits = new();
    }

    /// <summary>One retention group's committed total on an attribute.</summary>
    [Serializable]
    public class CommitSave
    {
        /// <summary>The group's tag, by name.</summary>
        public string Group;

        public float Current;
        public float Base;
    }

    /// <summary>A level the entity keeps: its own, or an ability's or an item's, under that one's asset tag.</summary>
    [Serializable]
    public class LevelSave
    {
        /// <summary>The level's tag, by name.</summary>
        public string Key;

        public int Level;
        public int Min;
        public int Max;
    }

    /// <summary>An ability the entity knows, in its slot.</summary>
    [Serializable]
    public class AbilitySave
    {
        /// <summary>The ability's ID in the catalog.</summary>
        public string Ability;

        /// <summary>The slot it's in, which an input bound by index names.</summary>
        public int Slot;

        public int Level;
    }

    /// <summary>An item the entity holds.</summary>
    [Serializable]
    public class ItemSave
    {
        /// <summary>The item's ID in the catalog.</summary>
        public string Item;

        public int Level;
        public int Stacks = 1;
        public bool Equipped;
    }

    /// <summary>
    /// An effect on the entity's shelf, with what applied it and its clock: its stacks, its duration and period, and each
    /// stack's time left and to its next tick. A clock has one of each, unless its stacks have their own durations.
    /// </summary>
    [Serializable]
    public class EffectSave
    {
        /// <summary>The effect's ID in the catalog.</summary>
        public string Effect;

        public OriginSave Origin = new();

        /// <summary>Whether its ongoing requirements held, which the next tick asks again.</summary>
        public bool Ongoing = true;

        public int Stacks;
        public float TotalDuration;
        public float PeriodDuration;
        public List<float> Remaining = new();
        public List<float> UntilTick = new();
    }

    /// <summary>
    /// What applied an effect: an entity, or one of its abilities or items. <see cref="Entity"/> is empty for the entity the
    /// save is of, and otherwise the ID an <see cref="IForgeEntityIds"/> gives the other.
    /// </summary>
    [Serializable]
    public class OriginSave
    {
        public ESavedOrigin Kind;
        public string Entity = "";

        /// <summary>The ability's or item's ID in the catalog.</summary>
        public string Asset = "";

        /// <summary>Where the ability or item is, which tells two of one apart: an ability's slot, or an item's place in the save.</summary>
        public int Slot = -1;

        /// <summary>The level a leveler origin carries.</summary>
        public int Level, MinLevel, MaxLevel;
    }

    /// <summary>The kinds of origin a save tells apart.</summary>
    public enum ESavedOrigin
    {
        /// <summary>The entity's hub itself.</summary>
        Entity,

        /// <summary>An origin that stands for an entity, as <see cref="IEffectOrigin.GenerateSourceDerivation"/> makes.</summary>
        Source,

        /// <summary>One that stands for an entity at a level of its own, as <see cref="IEffectOrigin.GenerateLevelerDerivation"/> makes.</summary>
        Leveler,

        Ability,
        Item,
    }
}
