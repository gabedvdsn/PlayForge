using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A Randomized scaler's variance: its kind, Percentage, Flat or Multiplier, or one of your own, named without
    /// "Variance", then the kind's own numbers, as Multiplier's Min and Max.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractVariance), true)]
    public class VarianceDrawer : AbstractTypeRefDrawer<AbstractVariance>
    {
        private const string Suffix = "Variance";

        protected override string CleanTypeName(string typeName) =>
            typeName.EndsWith(Suffix) && typeName.Length > Suffix.Length ? typeName.Substring(0, typeName.Length - Suffix.Length) : typeName;
    }
}
