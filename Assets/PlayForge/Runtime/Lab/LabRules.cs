using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// What the lab needs to know of a project to play its units (LAB-1): the attribute that is a unit's health, the one it
    /// walks at, the tags that stop, hide or shield it, where its attack range is kept, and which of its abilities is its
    /// attack. PlayForge has no movement or health of its own, so a project says which of its attributes are these. The
    /// Demo's roster says the same of the Demo.
    /// </summary>
    public interface ILabRules
    {
        /// <summary>A unit's health: its bar, what a dummy counts, and what a bot reads to know it's hurt.</summary>
        Attribute Health { get; }

        /// <summary>What a unit walks at, in units a second, or null to walk at <see cref="WalkSpeed"/>.</summary>
        Attribute MovementSpeed { get; }

        /// <summary>How fast a unit walks where it has no movement speed.</summary>
        float WalkSpeed { get; }

        /// <summary>A unit with any of these can't move, as while it's stunned or channeling.</summary>
        IReadOnlyList<Tag> Stops { get; }

        /// <summary>A unit with any of these can't be seen or targeted by the other side, as while it's invisible.</summary>
        IReadOnlyList<Tag> Hides { get; }

        /// <summary>A unit with any of these can't be targeted by anyone.</summary>
        IReadOnlyList<Tag> Shields { get; }

        /// <summary>The Local Data key a unit's attack range is under, or none to stand at <see cref="AttackRange"/>.</summary>
        Tag AttackRangeKey { get; }

        /// <summary>How near a unit stands to attack where its Local Data gives no range.</summary>
        float AttackRange { get; }

        /// <summary>Which of an entity's starting abilities is its attack, from 0, or -1 where none is.</summary>
        int AttackSlot { get; }

        /// <summary>The Local Data key an entity's bot casting order is under, as "W Q E", or none to cast each in turn (LAB-10).</summary>
        Tag CastOrderKey { get; }

        /// <summary>
        /// The Local Data key for when a bot casts an ability: InRange, Hurt, Low (away from its foe), Stunned or FoeLow;
        /// or none to cast each in range (LAB-10).
        /// </summary>
        Tag CastWhenKey { get; }

        /// <summary>The Local Data key an ability's reach for a bot is under, 0 for anywhere, or none for anywhere (LAB-10).</summary>
        Tag ReachKey { get; }
    }

    /// <summary>
    /// A project's rules for the Creator Lab, as an asset (LAB-1). An attribute left empty is found by its name among the
    /// project's: Health, and Movement Speed or Move Speed. With no asset, the lab makes one and finds both.
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Lab Rules", fileName = "Lab Rules")]
    public class LabRules : ScriptableObject, ILabRules
    {
        [Header("Attributes")]
        [Tooltip("A unit's health: its bar, and what a dummy counts. Left empty, the lab takes the project's attribute named Health")]
        public Attribute Health;

        [Tooltip("What a unit walks at, in units a second. Left empty, the lab takes one named Movement Speed or Move Speed")]
        public Attribute MovementSpeed;

        [Tooltip("How fast a unit walks where it has no movement speed")]
        public float WalkSpeed = 4f;

        [Header("States")]
        [Tooltip("A unit with any of these can't move, as while it's stunned or channeling")]
        public List<Tag> Stops = new();

        [Tooltip("A unit with any of these can't be seen or targeted by the other side, as while it's invisible")]
        public List<Tag> Hides = new();

        [Tooltip("A unit with any of these can't be targeted by anyone")]
        public List<Tag> Shields = new();

        [Header("Attacks")]
        [Tooltip("The Local Data key a unit's attack range is under; with none, every unit stands at the range below")]
        public Tag AttackRangeKey;

        [Tooltip("How near a unit stands to attack where its Local Data gives no range")]
        public float AttackRange = 2f;

        [Tooltip("Which of an entity's starting abilities is its attack, from 0, or -1 where none is")]
        public int AttackSlot;

        [Header("Bots")]
        [Tooltip("The Local Data key an entity's casting order is under, as \"W Q E\": the letters its abilities other than its attack, in its entity's order. With none, each in turn")]
        public Tag CastOrderKey;

        [Tooltip("The Local Data key for when a bot casts an ability: InRange, Hurt, Low (away from its foe), Stunned or FoeLow. With none, each in range")]
        public Tag CastWhenKey;

        [Tooltip("The Local Data key an ability's reach for a bot is under, 0 for anywhere. With none, anywhere")]
        public Tag ReachKey;

        [System.NonSerialized] private Attribute foundHealth;
        [System.NonSerialized] private Attribute foundSpeed;

        Attribute ILabRules.Health => Health ? Health : foundHealth;
        Attribute ILabRules.MovementSpeed => MovementSpeed ? MovementSpeed : foundSpeed;
        float ILabRules.WalkSpeed => WalkSpeed;
        IReadOnlyList<Tag> ILabRules.Stops => Stops;
        IReadOnlyList<Tag> ILabRules.Hides => Hides;
        IReadOnlyList<Tag> ILabRules.Shields => Shields;
        Tag ILabRules.AttackRangeKey => AttackRangeKey;
        float ILabRules.AttackRange => AttackRange;
        int ILabRules.AttackSlot => AttackSlot;
        Tag ILabRules.CastOrderKey => CastOrderKey;
        Tag ILabRules.CastWhenKey => CastWhenKey;
        Tag ILabRules.ReachKey => ReachKey;

        /// <summary>
        /// Finds the attributes left empty among a library's, by their names: Health, and Movement Speed or Move Speed.
        /// The asset keeps what was set; what's found lives as long as this object.
        /// </summary>
        public void FindIn(ILabLibrary library)
        {
            if (library == null) return;
            foundHealth = null;
            foundSpeed = null;
            foreach (var asset in library.Assets(LabAssetKind.Attributes))
            {
                if (asset is not Attribute attribute) continue;
                string name = Plain(attribute.GetName());
                if (!foundHealth && name == "health") foundHealth = attribute;
                if (!foundSpeed && (name == "movementspeed" || name == "movespeed")) foundSpeed = attribute;
            }
        }

        /// <summary>Rules made where a project has none: every attribute found by its name.</summary>
        public static LabRules MakeDefault(ILabLibrary library)
        {
            var rules = CreateInstance<LabRules>();
            rules.name = "Lab Rules";
            rules.hideFlags = HideFlags.DontSave;
            rules.FindIn(library);
            return rules;
        }

        // A name to match on: lower case, letters and digits alone
        private static string Plain(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var plain = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
                if (char.IsLetterOrDigit(c)) plain.Append(char.ToLowerInvariant(c));
            return plain.ToString();
        }
    }

    /// <summary>The checks a unit's rules answer, in one place for the lab, its bots and the Demo.</summary>
    public static class LabRulesExtensions
    {
        /// <summary>Whether a tag cache holds any of the tags given.</summary>
        public static bool HasAny(this TagCache tags, IReadOnlyList<Tag> any)
        {
            if (tags == null || any == null) return false;
            for (int i = 0; i < any.Count; i++)
                if (any[i].IsValid && tags.GetWeight(any[i]) > 0) return true;
            return false;
        }
    }
}
