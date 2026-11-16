using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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

        /// <summary>One whole-number preference, kept within its least and its most: its key, label, tooltip and default.</summary>
        internal sealed class Number
        {
            public readonly string Key;
            public readonly string Label;
            public readonly string Tooltip;
            public readonly int Default;
            public readonly int Min;
            public readonly int Max;

            public Number(string key, string label, string tooltip, int defaultValue, int min, int max)
            {
                Key = key;
                Label = label;
                Tooltip = tooltip;
                Default = defaultValue;
                Min = min;
                Max = max;
            }

            public int Value
            {
                get => Mathf.Clamp(EditorPrefs.GetInt(Prefix + Key, Default), Min, Max);
                set => EditorPrefs.SetInt(Prefix + Key, Mathf.Clamp(value, Min, Max));
            }
        }

        /// <summary>The widest a PlayForge dropdown grows, a picker's or a field's menu (EP-1).</summary>
        internal static readonly Number MenuWidth = new("MenuWidth", "Menu Width",
            "The widest a PlayForge dropdown grows, a picker's or a field's menu, in points", 360, 220, 1200);

        /// <summary>The tallest a PlayForge dropdown grows; a longer list scrolls (EP-1).</summary>
        internal static readonly Number MenuHeight = new("MenuHeight", "Menu Height",
            "The tallest a PlayForge dropdown grows, in points; a longer list scrolls", 420, 160, 1600);

        /// <summary>The menus' numbers, in the order the page shows them.</summary>
        internal static readonly Number[] MenuNumbers = { MenuWidth, MenuHeight };

        internal static readonly Switch ShowFullTagPath = new("ShowFullTagPath", "Show Full Tag Paths",
            "Show a tag's whole path in strong text, rather than its parents in secondary text before its leaf", false);
        internal static readonly Switch GroupTagsByRoot = new("GroupTagsByRoot", "Group Tags by Root",
            "Group the tag picker's tags under their root", true);

        /// <summary>The Forge's switches, in the order the page shows them. It has none since Show File Names (ED-58).</summary>
        internal static readonly Switch[] ForgeSwitches = { };

        /// <summary>The tag fields' switches, in the order the page shows them.</summary>
        internal static readonly Switch[] TagSwitches = { ShowFullTagPath, GroupTagsByRoot };

        /// <summary>
        /// The keys every preference here is kept under, the tables' column widths and hidden columns among them (FN-7).
        /// </summary>
        internal static IEnumerable<string> Keys()
        {
            foreach (var each in ForgeSwitches) yield return Prefix + each.Key;
            foreach (var each in TagSwitches) yield return Prefix + each.Key;
            foreach (var each in MenuNumbers) yield return Prefix + each.Key;
            foreach (var table in ForgeColumnWidths.Tables)
            {
                yield return ForgeColumnWidths.KeyOf(table);
                yield return ForgeColumnWidths.HiddenKeyOf(table);
            }
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
        /// <summary>A tag's whole path in strong text, rather than its parents in secondary text before its leaf. Off unless set.</summary>
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

        /// <summary>
        /// A tag's path as it reads: its display name. A tag made unique, as the tag picker makes one, pads its name to a
        /// fixed length with a generated suffix, and only its display name leaves that out (ED-60).
        /// </summary>
        public static string PathOf(Tag tag) => tag.IsValid ? tag.ToString() : "";

        /// <summary>A serialized tag's path as it reads: its display name, or its name when it has none.</summary>
        public static string PathOf(SerializedProperty tag)
        {
            var display = tag?.FindPropertyRelative(nameof(Tag.DisplayName))?.stringValue;
            return !string.IsNullOrEmpty(display) ? display : tag?.FindPropertyRelative(nameof(Tag.Name))?.stringValue ?? "";
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
