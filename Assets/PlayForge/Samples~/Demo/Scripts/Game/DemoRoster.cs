using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// What the demo offers and what its scene reads: its heroes, its training dummy, its items, the attributes its HUD
    /// shows, and the tags and keys its scripts use. One asset, so no script names an asset of its own.
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Demo/Roster", fileName = "Demo Roster")]
    public class DemoRoster : ScriptableObject
    {
        [Header("Who fights")]
        public List<DemoHero> Heroes = new();
        [Tooltip("The opponent that stands and takes damage")]
        public EntityIdentity TrainingDummy;
        public Color DummyColor = new(0.62f, 0.56f, 0.45f);
        [Tooltip("Mends the dummy to full, as it rests")]
        public GameplayEffect DummyMend;

        [Header("What can be given")]
        public List<Item> Items = new();

        [Header("What the scene reads")]
        public Attribute Health;
        public Attribute Mana;
        public Attribute MovementSpeed;
        [Tooltip("The attributes the HUD lists, in order")]
        public List<Attribute> Shown = new();

        [Header("Sides")]
        public Tag PlayerSide;
        public Tag OpponentSide;

        [Header("States the scene reads")]
        [Tooltip("A unit with it can't move")]
        public Tag Stunned;
        [Tooltip("A unit with it can't be seen or targeted by the other side")]
        public Tag Invisible;
        [Tooltip("A unit with it can't be targeted")]
        public Tag Untargetable;
        [Tooltip("A unit with it stands still, as while it channels")]
        public Tag Channeling;

        [Header("Keys")]
        [Tooltip("The Local Data key a hero's attack range is under")]
        public Tag AttackRange;
        [Tooltip("The Local Data key a hero's AI casting order is under, as \"W Q E\"")]
        public Tag CastOrder;
        [Tooltip("The Local Data key an ability's reach for the AI is under; 0 is anywhere")]
        public Tag Reach;
        [Tooltip("The Local Data key for when the AI casts an ability: \"InRange\", \"Hurt\", \"Low\" (away from its foe), \"Stunned\" or \"FoeLow\"")]
        public Tag CastWhen;
        [Tooltip("The Set By Caller key the AI's difficulty is under, which every damage effect multiplies by")]
        public Tag Difficulty;

        [Header("Damage types, as impact types, by which damage numbers are colored")]
        public Tag Physical;
        public Tag Magic;
        public Tag Pure;
    }

    /// <summary>A hero the demo offers: its entity, its color, and a line about it.</summary>
    [Serializable]
    public class DemoHero
    {
        public EntityIdentity Identity;
        public Color Color = Color.white;
        [TextArea] public string Summary;
    }
}
