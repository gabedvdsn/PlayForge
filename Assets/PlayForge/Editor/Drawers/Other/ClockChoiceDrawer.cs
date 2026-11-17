using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A clock picked in an inspector, as an ability's Run Sequence task's Clock: its kind, Caster's, Game's, Unscaled or
    /// Scaled, or one of your own, then the kind's own fields, as Scaled's Scale and the clock it scales. None leaves the
    /// run the clock it would have had.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractClockChoice), true)]
    public class ClockChoiceDrawer : AbstractTypeRefDrawer<AbstractClockChoice>
    {
        private const string Suffix = "ClockChoice";

        protected override string CleanTypeName(string typeName) =>
            typeName.EndsWith(Suffix) && typeName.Length > Suffix.Length ? typeName.Substring(0, typeName.Length - Suffix.Length) : typeName;
    }
}
