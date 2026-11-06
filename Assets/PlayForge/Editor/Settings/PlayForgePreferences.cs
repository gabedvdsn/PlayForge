using System.Collections.Generic;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// PlayForge's view toggles, which belong to each user rather than to the project, so they stay in EditorPrefs, as
    /// Unity's own preferences do, and the Preferences page edits them (Q21). The settings the project shares are
    /// <see cref="PlayForgeSettings"/>.
    /// </summary>
    public static class PlayForgePreferences
    {
        internal const string Prefix = "PlayForge_";

        /// <summary>One on-or-off preference: its key, its label and tooltip on the page, and its value until set.</summary>
        internal sealed class Switch
        {
            public readonly string Key;
            public readonly string Label;
            public readonly string Tooltip;
            public readonly bool Default;

            public Switch(string key, string label, string tooltip, bool defaultValue)
            {
                Key = key;
                Label = label;
                Tooltip = tooltip;
                Default = defaultValue;
            }

            public bool Value
            {
                get => EditorPrefs.GetBool(Prefix + Key, Default);
                set => EditorPrefs.SetBool(Prefix + Key, value);
            }
        }

        internal static readonly Switch ShowFullTagPath = new("ShowFullTagPath", "Show Full Tag Paths",
            "Show a tag's whole path, rather than its leaf after an ellipsis", false);
        internal static readonly Switch GroupTagsByRoot = new("GroupTagsByRoot", "Group Tags by Root",
            "Group the tag picker's tags under their root", true);
        internal static readonly Switch ShowFileNames = new("ShowFileNames", "Show File Names",
            "The Forge's list shows each asset's file name beside its name, when they differ", true);

        /// <summary>The Forge's switches, in the order the page shows them.</summary>
        internal static readonly Switch[] ForgeSwitches = { ShowFileNames };

        // The preferences The Forge's redesign left behind: its window keeps its filter, follows the project and selects
        // what it makes, and its Create tab and Visualizer are gone (ED-27). Use Defaults clears them too
        private static readonly string[] Retired =
        {
            "RememberTypeFilter", "DoubleClickVisualize", "AutoRefresh", "ShowNotifications", "RecentlyCreatedRows", "LastTypeFilter",
        };

        /// <summary>The tag fields' switches, in the order the page shows them.</summary>
        internal static readonly Switch[] TagSwitches = { ShowFullTagPath, GroupTagsByRoot };

        /// <summary>The keys every preference here is kept under, the tables' column widths among them, and the retired ones'.</summary>
        internal static IEnumerable<string> Keys()
        {
            foreach (var each in ForgeSwitches) yield return Prefix + each.Key;
            foreach (var each in TagSwitches) yield return Prefix + each.Key;
            foreach (var table in ForgeColumnWidths.Tables) yield return ForgeColumnWidths.KeyOf(table);
            foreach (var each in Retired) yield return Prefix + each;
            yield return ForgeLogSettingsBridge.PrefsKey;
        }

        /// <summary>Puts every preference back to its default, the log level among them.</summary>
        public static void UseDefaults()
        {
            foreach (var key in Keys()) EditorPrefs.DeleteKey(key);
            ForgeLogSettingsBridge.ResetLevel();
        }
    }

    /// <summary>
    /// The tag display preferences, which the tag field, The Forge's tag list and the Preferences page share. Each used to
    /// keep a default, or a switch, of its own (ED-25).
    /// </summary>
    internal static class TagDisplaySettings
    {
        /// <summary>A tag's whole path, rather than its leaf after an ellipsis. Off unless set.</summary>
        public static bool ShowFullPath
        {
            get => PlayForgePreferences.ShowFullTagPath.Value;
            set => PlayForgePreferences.ShowFullTagPath.Value = value;
        }

        /// <summary>Tags grouped under their root. On unless set.</summary>
        public static bool GroupByRoot
        {
            get => PlayForgePreferences.GroupTagsByRoot.Value;
            set => PlayForgePreferences.GroupTagsByRoot.Value = value;
        }
    }

    /// <summary>
    /// Keeps the ForgeLog level chosen in Preferences → PlayForge and applies it to <see cref="ForgeLog.MinimumLevel"/> on
    /// every domain reload, since static fields reset on reload and EditorPrefs lasts.
    /// </summary>
    [InitializeOnLoad]
    public static class ForgeLogSettingsBridge
    {
        internal const string PrefsKey = "PlayForge_ForgeLogMinLevel";

        static ForgeLogSettingsBridge()
        {
            ForgeLog.MinimumLevel = CurrentLevel;
        }

        public static ForgeLog.ELevel CurrentLevel =>
            (ForgeLog.ELevel)EditorPrefs.GetInt(PrefsKey, (int)ForgeLog.ELevel.Info);

        public static void SetLevel(ForgeLog.ELevel level)
        {
            EditorPrefs.SetInt(PrefsKey, (int)level);
            ForgeLog.MinimumLevel = level;
        }

        public static void ResetLevel()
        {
            EditorPrefs.DeleteKey(PrefsKey);
            ForgeLog.MinimumLevel = CurrentLevel;
        }
    }
}
