using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Files a kind in the pickers that offer it, an ability task's, a worker's, a component's, a cue's, a number's and the
    /// rest (EP-2): under the submenus its path names, a slash between each, as "Effects" or "Effects/Damage"; with a name
    /// to show in place of its class's; and with a description the picker's Context says while it's highlighted.
    /// <para>
    /// A path holds for the classes made from the one that names it, unless one names its own, "" for the picker's top; a
    /// kind that names only a name or a description keeps its base's path. A name and a description are a kind's own. A
    /// picker lists a menu's submenus in name order, then its kinds; its search finds a kind whatever its submenu.
    /// </para>
    /// <para>
    /// It replaces <c>ForgeCategory</c>, the ability tasks' one level of groups, and the Category of a number's kind and of
    /// a condition, each of which filed one family alone.
    /// </para>
    /// <example><code>
    /// [Serializable]
    /// [ForgeMenu("Effects/Damage", Description = "Deals its damage to each target, once")]
    /// public class StrikeTask : AbstractAbilityTask { ... }
    /// </code></example>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ForgeMenuAttribute : System.Attribute
    {
        /// <summary>
        /// The submenus the kind sits under, a slash between each, as "Effects/Damage", "" for the picker's top; or null
        /// where it names none, and its base's holds.
        /// </summary>
        public string Path { get; }

        /// <summary>The name the kind shows in place of its class's, or null for its class's.</summary>
        public string Name { get; set; }

        /// <summary>What the kind does, as its picker's Context says it, or null.</summary>
        public string Description { get; set; }

        /// <summary>A name or a description alone: the kind sits where the class it's made from does.</summary>
        public ForgeMenuAttribute()
        {
        }

        /// <param name="path">The submenus the kind sits under, a slash between each; "" for the picker's top.</param>
        public ForgeMenuAttribute(string path)
        {
            Path = Clean(path);
        }

        /// <summary>
        /// The submenus a kind sits under: the path its own attribute names, or else the nearest class it's made from's;
        /// false, and "", where none names one.
        /// </summary>
        public static bool TryPathOf(Type type, out string path)
        {
            for (var each = type; each != null; each = each.BaseType)
            {
                if (Own(each)?.Path is not { } named) continue;
                path = named;
                return true;
            }
            path = "";
            return false;
        }

        /// <summary>The submenus a kind sits under, as <see cref="TryPathOf"/> finds them; "" for the picker's top.</summary>
        public static string PathOf(Type type) => TryPathOf(type, out var path) ? path : "";

        /// <summary>The name a kind's own attribute gives it, or null for its class's.</summary>
        public static string NameOf(Type type) => Own(type)?.Name is { Length: > 0 } name ? name : null;

        /// <summary>The description a kind's own attribute gives it, or null.</summary>
        public static string DescriptionOf(Type type) => Own(type)?.Description is { Length: > 0 } text ? text : null;

        // The kind's own attribute, not a class it's made from's
        private static ForgeMenuAttribute Own(Type type) =>
            type == null ? null : GetCustomAttribute(type, typeof(ForgeMenuAttribute), false) as ForgeMenuAttribute;

        // A path's parts, each trimmed, empty ones left out, as " Effects / Damage/" reads "Effects/Damage"
        private static string Clean(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            var kept = new List<string>();
            foreach (var part in path.Split('/'))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) kept.Add(trimmed);
            }
            return string.Join("/", kept);
        }
    }
}
