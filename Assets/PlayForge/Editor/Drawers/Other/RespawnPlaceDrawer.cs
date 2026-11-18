using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A Respawn's place: its kind, Home, Where It Died or Point, or one of your own, named without "Respawn Place", then
    /// the kind's own fields, as Point's point.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractRespawnPlace), true)]
    public class RespawnPlaceDrawer : AbstractTypeRefDrawer<AbstractRespawnPlace>
    {
        private const string Suffix = "RespawnPlace";

        protected override string CleanTypeName(string typeName) =>
            typeName.EndsWith(Suffix) && typeName.Length > Suffix.Length ? typeName.Substring(0, typeName.Length - Suffix.Length) : typeName;
    }
}
