using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A purge's filter (FN-16): its kind, named without "Purge Filter", as Except Tagged, or one of your own, then the kind's
    /// own fields.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractPurgeFilter), true)]
    public class PurgeFilterDrawer : AbstractTypeRefDrawer<AbstractPurgeFilter>
    {
        protected override string CleanTypeName(string typeName)
        {
            foreach (var suffix in new[] { "PurgeFilter", "Filter" })
                if (typeName.EndsWith(suffix) && typeName.Length > suffix.Length) return typeName.Substring(0, typeName.Length - suffix.Length);
            return typeName;
        }
    }
}
