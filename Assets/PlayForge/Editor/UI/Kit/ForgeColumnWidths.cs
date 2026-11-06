using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Remembers the widths a table's columns are dragged to, so they outlive the window's closing and a domain reload, as
    /// entering Play mode is (ED-48). Unity keeps a table's columns only for a table with a view data key, which brings
    /// back its selection and scroll too, behind the window's back. This keeps the widths alone, one preference a table,
    /// and Use Defaults forgets them.
    /// </summary>
    internal static class ForgeColumnWidths
    {
        /// <summary>The tables whose widths are kept, each named by its window and its list.</summary>
        internal static readonly string[] Tables =
        {
            "Debugger.Entities", "Debugger.Processes", "Debugger.Tags", "Debugger.Timeline", "Forge.Assets", "Forge.Tags", "Problems",
        };

        /// <summary>The preference a table's widths are kept under.</summary>
        internal static string KeyOf(string table) => PlayForgePreferences.Prefix + "Columns_" + table;

        /// <summary>
        /// Gives a table's columns the widths they were last dragged to, and keeps each width a drag gives one from now on.
        /// Only a drag, or the header's Resize to Fit, changes a column's width: as a table narrows or widens, its list lays
        /// the columns out around their widths.
        /// </summary>
        public static void Remember(Columns columns, string table)
        {
            if (columns == null) return;
            var key = KeyOf(table);
            var kept = Read(EditorPrefs.GetString(key, ""));
            var defaults = new Dictionary<string, float>();
            foreach (var column in columns)
            {
                if (string.IsNullOrEmpty(column.name) || !column.resizable) continue;
                defaults[column.name] = column.width.value;
                if (kept.TryGetValue(column.name, out float width)) column.width = width;
            }

            foreach (var column in columns)
            {
                if (!defaults.ContainsKey(column.name)) continue;
                column.propertyChanged += (_, changed) =>
                {
                    if (changed.propertyName.ToString() != "width") return;
                    float width = column.width.value;
                    if (System.Math.Abs(width - defaults[column.name]) < 0.5f) kept.Remove(column.name);
                    else kept[column.name] = width;
                    if (kept.Count == 0) EditorPrefs.DeleteKey(key);
                    else EditorPrefs.SetString(key, Write(kept));
                };
            }
        }

        // "Hub=92;Level=40": each kept column's name and width
        private static Dictionary<string, float> Read(string text)
        {
            var widths = new Dictionary<string, float>();
            foreach (var pair in (text ?? "").Split(';'))
            {
                int at = pair.LastIndexOf('=');
                if (at <= 0) continue;
                if (float.TryParse(pair.Substring(at + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out float width) && width > 0f)
                    widths[pair.Substring(0, at)] = width;
            }
            return widths;
        }

        private static string Write(Dictionary<string, float> widths) =>
            string.Join(";", widths.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value.ToString("0.#", CultureInfo.InvariantCulture)}"));
    }
}
