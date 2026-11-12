using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// A side a unit stands on in the lab (LAB-2, LAB-4): an affiliation tag, as its registration's data gives it, a name
    /// and the color of its ring.
    /// </summary>
    public readonly struct LabSide
    {
        public readonly string Name;
        public readonly Tag Tag;
        public readonly Color Color;

        public LabSide(string name, Tag tag, Color color)
        {
            Name = name;
            Tag = tag;
            Color = color;
        }

        public bool IsValid => Tag.IsValid;

        public override string ToString() => Name;
    }

    /// <summary>
    /// The lab's sides: Blue and Red for a fight, Green and Gold for more teams, and Neutral, the side of no one, which the
    /// lab's own caster stands on. Each is an affiliation tag of the lab's own, made as it's first asked for.
    /// </summary>
    public static class LabSides
    {
        private static List<LabSide> sides;

        /// <summary>Every side, Neutral last.</summary>
        public static IReadOnlyList<LabSide> All => sides ??= Make();

        public static LabSide Blue => All[0];
        public static LabSide Red => All[1];
        public static LabSide Green => All[2];
        public static LabSide Gold => All[3];
        public static LabSide Neutral => All[All.Count - 1];

        /// <summary>The side named so, or Neutral.</summary>
        public static LabSide Named(string name)
        {
            foreach (var side in All)
                if (side.Name == name) return side;
            return Neutral;
        }

        /// <summary>The side whose tag an entity's affiliation holds, or Neutral.</summary>
        public static LabSide Of(IAffiliated affiliated)
        {
            var tags = affiliated?.GetAffiliation();
            if (tags != null)
                foreach (var side in All)
                    if (tags.Contains(side.Tag)) return side;
            return Neutral;
        }

        /// <summary>Whether two share a side: an affiliation tag in common. One on no side is no one's ally but its own.</summary>
        public static bool AreAllies(IAffiliated one, IAffiliated other)
        {
            if (one == null || other == null) return false;
            if (ReferenceEquals(one, other)) return true;
            var mine = one.GetAffiliation();
            var theirs = other.GetAffiliation();
            if (mine == null || theirs == null) return false;
            foreach (var tag in mine)
                if (theirs.Contains(tag)) return true;
            return false;
        }

        // The tags are the registry's, which a test's teardown clears, so they're made again when asked after it
        internal static void Forget() => sides = null;

        private static List<LabSide> Make() => new()
        {
            new LabSide("Blue", Tag.GenerateAsUnique("Lab Side Blue"), new Color(0.27f, 0.55f, 0.95f)),
            new LabSide("Red", Tag.GenerateAsUnique("Lab Side Red"), new Color(0.9f, 0.3f, 0.26f)),
            new LabSide("Green", Tag.GenerateAsUnique("Lab Side Green"), new Color(0.35f, 0.75f, 0.38f)),
            new LabSide("Gold", Tag.GenerateAsUnique("Lab Side Gold"), new Color(0.88f, 0.72f, 0.25f)),
            new LabSide("Neutral", Tag.GenerateAsUnique("Lab Side Neutral"), new Color(0.6f, 0.6f, 0.6f)),
        };
    }
}
