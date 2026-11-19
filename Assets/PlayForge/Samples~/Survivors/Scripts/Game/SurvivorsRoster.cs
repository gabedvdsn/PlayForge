using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Lab;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// A kind of enemy the waves send: its entity, how it looks, from which wave it comes and how often, what its crystal
    /// is worth, and what it does past walking at the hero and attacking, its abilities its entity's own.
    /// </summary>
    [Serializable]
    public sealed class SurvivorsEnemyKind
    {
        [Tooltip("The entity, its attribute set, its attack first among its starting abilities and its others after")]
        public EntityIdentity Identity;

        [Tooltip("Its body's color")]
        public Color Color = Color.gray;

        [Tooltip("Its body's size, 1 a person's")]
        [Min(0.2f)] public float Size = 1f;

        [Tooltip("The first wave it comes in")]
        [Min(1)] public int FirstWave = 1;

        [Tooltip("How often it comes, against the others the wave may send")]
        [Min(0f)] public float Weight = 1f;

        [Tooltip("How many come at once, around one point")]
        [Min(1)] public int Group = 1;

        [Tooltip("What its crystal is worth in experience")]
        [Min(1)] public int Experience = 1;

        [Tooltip("The kind it breaks into as it dies, as a slime splits; none for none")]
        public EntityIdentity SplitsInto;

        [Tooltip("How many of that kind it breaks into")]
        [Min(0)] public int Splits;
    }

    /// <summary>A boss wave: the hard ones it sends at once, as it starts, beside the wave's own.</summary>
    [Serializable]
    public sealed class SurvivorsBossWave
    {
        [Tooltip("What the wave's banner calls it")]
        public string Name = "Boss";

        [Tooltip("The hard ones it sends, each an enemy kind and how many")]
        public List<SurvivorsBossEntry> Units = new();
    }

    /// <summary>One kind of a boss wave's units, and how many.</summary>
    [Serializable]
    public sealed class SurvivorsBossEntry
    {
        public SurvivorsEnemyKind Kind = new();

        [Min(1)] public int Count = 1;

        [Tooltip("Levels over the wave's own, which its attributes are read at")]
        [Min(0)] public int ExtraLevels;
    }

    /// <summary>
    /// The Survivors demo's rules, as one asset: the hero and its pools of abilities and passives, the experience a level
    /// takes, the waves and what they send, the boss waves, and the attributes and tags the game reads. It's the Lab's
    /// rules too, so the hero and the enemies are the Creator Lab's units: they walk, attack and cast as the lab's do.
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Survivors/Roster", fileName = "Survivors Roster", order = 81)]
    public class SurvivorsRoster : ScriptableObject, ILabRules
    {
        [Header("The hero")]
        [Tooltip("The hero's entity; its first starting ability is the one it starts with")]
        public EntityIdentity Hero;

        public Color HeroColor = new(0.35f, 0.75f, 1f);

        [Tooltip("The abilities a level-up's cards offer: a new one, or a level more of one the hero holds")]
        public List<Ability> AbilityPool = new();

        [Tooltip("The hero's dash, which its key casts: a starting ability of the hero's, never a card, never cast on its own")]
        public Ability Dash;

        [Tooltip("The passives a boss wave's cards offer, each an item whose effects grow with its level")]
        public List<Item> PassivePool = new();

        [Tooltip("How many abilities the hero holds at most")]
        [Min(1)] public int MaxAbilities = 5;

        [Tooltip("How many passives the hero holds at most")]
        [Min(1)] public int MaxPassives = 5;

        [Tooltip("How many cards a level-up offers")]
        [Min(1)] public int AbilityCards = 3;

        [Tooltip("How many cards a boss wave's end offers")]
        [Min(1)] public int PassiveCards = 5;

        [Header("Experience")]
        [Tooltip("The experience the first level-up takes")]
        [Min(1f)] public float FirstLevel = 5f;

        [Tooltip("What each level-up takes more than the one before")]
        [Min(0f)] public float LevelGrowth = 5f;

        [Tooltip("What each level-up takes more again, by the square of the level, so later levels come slower")]
        [Min(0f)] public float LevelCurve = 0.5f;

        [Header("Waves")]
        [Tooltip("Seconds a wave lasts; the next starts as it ends")]
        [Min(1f)] public float WaveSeconds = 25f;

        [Tooltip("Every how many waves a boss wave comes")]
        [Min(1)] public int BossEvery = 2;

        [Tooltip("How many enemies the first wave sends over its time")]
        [Min(1)] public int FirstWaveCount = 23;

        [Tooltip("How many more each wave sends than the one before")]
        [Min(0)] public int WaveGrowth = 17;

        [Tooltip("How many more again, by the square of the wave, so the horde builds")]
        [Min(0f)] public float WaveCurve = 2.5f;

        [Tooltip("Every how many waves an enemy is a level higher")]
        [Min(1)] public int WavesALevel = 2;

        [Tooltip("The most enemies standing at once; a wave sends no more while there are")]
        [Min(10)] public int MaxEnemies = 600;

        [Tooltip("How far from the hero enemies come in, past what the camera sees")]
        [Min(5f)] public float SpawnDistance = 22f;

        public List<SurvivorsEnemyKind> Enemies = new();

        [Tooltip("The boss waves, in turn: the first comes at the first boss wave, and they go round")]
        public List<SurvivorsBossWave> BossWaves = new();

        [Header("Attributes")]
        public Attribute HealthAttribute;
        public Attribute MovementSpeedAttribute;

        [Tooltip("How far the hero draws crystals in from")]
        public Attribute PickupRadius;

        [Tooltip("What the hero heals each second")]
        public Attribute HealthRegeneration;

        [Tooltip("What abilities' projectiles add to their count")]
        public Attribute ProjectileAmount;

        [Tooltip("The enemies a projectile passes through, past its own")]
        public Attribute ProjectilePierce;

        [Tooltip("What an area's radius is multiplied by")]
        public Attribute Area;

        [Tooltip("What a lasting process's time is multiplied by")]
        public Attribute Duration;

        [Header("Tags")]
        public Tag HeroSide;
        public Tag EnemySide;

        [Tooltip("An enemy with any of these can't move, as while it's chilled solid or stunned")]
        public List<Tag> StopTags = new();

        [Tooltip("An enemy with any of these can't be targeted, as a Wraith while it's phased")]
        public List<Tag> ShieldTags = new();

        // How far an ability reaches, and an enemy attacks from, is each ability's own Cast Range component, which the hero,
        // the horde and the targeting read alike

        /// <summary>The experience the level after <paramref name="level"/> takes.</summary>
        public float ExperienceFor(int level)
        {
            int past = Mathf.Max(0, level - 1);
            return FirstLevel + LevelGrowth * past + LevelCurve * past * past;
        }

        /// <summary>How many enemies a wave sends over its time.</summary>
        public int CountFor(int wave)
        {
            int past = Mathf.Max(0, wave - 1);
            return Mathf.RoundToInt(FirstWaveCount + WaveGrowth * past + WaveCurve * past * past);
        }

        /// <summary>Whether a wave is a boss wave.</summary>
        public bool IsBossWave(int wave) => BossEvery > 0 && wave % BossEvery == 0 && BossWaves.Count > 0;

        /// <summary>The boss wave a wave sends, in turn, or null for none.</summary>
        public SurvivorsBossWave BossWaveFor(int wave) =>
            IsBossWave(wave) ? BossWaves[(wave / BossEvery - 1) % BossWaves.Count] : null;

        /// <summary>The level a wave's enemies are, as it goes on.</summary>
        public int EnemyLevelFor(int wave) => 1 + Mathf.Max(0, wave - 1) / Mathf.Max(1, WavesALevel);

        // ═══════════════════════════════════════════════════════════════════════════
        // The Lab's rules
        // ═══════════════════════════════════════════════════════════════════════════

        Attribute ILabRules.Health => HealthAttribute;
        Attribute ILabRules.MovementSpeed => MovementSpeedAttribute;
        float ILabRules.WalkSpeed => 3f;
        IReadOnlyList<Tag> ILabRules.Stops => StopTags;
        IReadOnlyList<Tag> ILabRules.Hides => Array.Empty<Tag>();
        IReadOnlyList<Tag> ILabRules.Shields => ShieldTags;
        // No Local Data range: a unit attacks from its attack's Cast Range, and an ability reaches by its own
        Tag ILabRules.AttackRangeKey => default;
        float ILabRules.AttackRange => 1.4f;
        int ILabRules.AttackSlot => 0;
        Tag ILabRules.CastOrderKey => default;
        Tag ILabRules.CastWhenKey => default;
        Tag ILabRules.ReachKey => default;
    }
}
