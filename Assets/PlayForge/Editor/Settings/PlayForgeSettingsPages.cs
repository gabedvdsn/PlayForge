using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// PlayForge's two settings pages (Q21, ED-21): Project Settings → PlayForge, for the settings the project shares, and
    /// Preferences → PlayForge, for each user's view toggles. The Forge's Settings tab held both, all in EditorPrefs.
    /// Each page follows Docs/EDITOR_STYLE.md: Unity's own controls, and its look from the style sheet.
    /// </summary>
    internal static class PlayForgeSettingsPages
    {
        internal const string ProjectPath = "Project/PlayForge";
        internal const string PreferencesPath = "Preferences/PlayForge";

        [SettingsProvider]
        internal static SettingsProvider CreateProjectPage() => new(ProjectPath, SettingsScope.Project)
        {
            label = "PlayForge",
            activateHandler = (_, root) => BuildProjectPage(root),
            keywords = new HashSet<string> { "PlayForge", "Folder", "Prefix", "Suffix", "Defaults", "New Asset", "Import", "Cost", "Cooldown" },
        };

        [SettingsProvider]
        internal static SettingsProvider CreatePreferencesPage() => new(PreferencesPath, SettingsScope.User)
        {
            label = "PlayForge",
            activateHandler = (_, root) => BuildPreferencesPage(root),
            keywords = new HashSet<string> { "PlayForge", "The Forge", "Tag", "Path", "Root", "Log", "Level" },
        };

        /// <summary>Opens Project Settings → PlayForge.</summary>
        internal static void OpenProjectSettings() => SettingsService.OpenProjectSettings(ProjectPath);

        /// <summary>Opens Preferences → PlayForge.</summary>
        internal static void OpenPreferences() => SettingsService.OpenUserPreferences(PreferencesPath);

        // ═══════════════════════════════════════════════════════════════════════════
        // Project Settings → PlayForge
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the project's page in a root: where each type's new assets go and how they're named, each type's
        /// defaults, and the Cost and Cooldown effects. Its fields bind to the settings, so an edit undoes, and each edit
        /// is saved to ProjectSettings/PlayForgeSettings.asset.
        /// </summary>
        internal static VisualElement BuildProjectPage(VisualElement root)
        {
            var page = Page(root, "PlayForge");
            var serialized = PlayForgeSettings.Serialized();
            var data = serialized.FindProperty(nameof(PlayForgeSettings.Data));

            Heading(page, "New Assets");
            page.Add(Note("Where The Forge and a field's New put each type's new assets, and how their files are named."));
            page.Add(NewAssetsTable(data.FindPropertyRelative(nameof(PlayForgeSettingsData.NewAssets))));
            var buttons = new VisualElement();
            buttons.AddToClassList("pf-settings__buttons");
            buttons.Add(new Button(() =>
            {
                PlayForgeSettings.UseDefaultFoldersAndNames();
                serialized.Update();
            })
            {
                name = "UseDefaultFoldersAndNames",
                text = "Use Default Folders and Names",
                tooltip = "Put every type's folder, prefix and suffix back to their defaults. The defaults below stay",
            });
            page.Add(buttons);

            Heading(page, "Defaults");
            page.Add(Note("A new asset starts as a copy of its type's New Asset default, when it has one. Import lists a " +
                          "type's Import defaults first."));
            var defaults = data.FindPropertyRelative(nameof(PlayForgeSettingsData.Defaults));
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var entry = defaults.GetArrayElementAtIndex(i);
                var type = KindOf(entry.FindPropertyRelative(nameof(TypeDefaults.Type)).stringValue);
                if (type != null) page.Add(DefaultsOf(type, entry));
            }

            Heading(page, "Cost and Cooldown");
            page.Add(Note("The effects an ability's Cost and Cooldown fields offer to copy or use, in their right-click menu."));
            page.Add(EffectField(data, nameof(PlayForgeSettingsData.CostEffect), "Cost Effect"));
            page.Add(EffectField(data, nameof(PlayForgeSettingsData.CooldownEffect), "Cooldown Effect"));

            page.Bind(serialized);
            // Each edit, and an Undo of one, is written to the file
            page.TrackSerializedObjectValue(serialized, _ => PlayForgeSettings.instance.SaveSettings());
            return page;
        }

        // A row for each type: its name, then its folder, with a button that picks one, and its file name's prefix and
        // suffix
        private static VisualElement NewAssetsTable(SerializedProperty newAssets)
        {
            var table = new VisualElement { name = "NewAssets" };
            table.AddToClassList("pf-table");
            var header = new VisualElement();
            header.AddToClassList("pf-table__header");
            header.Add(Cell(new Label("Type"), "pf-settings__type"));
            header.Add(Cell(new Label("Folder"), "pf-settings__folder"));
            header.Add(Cell(new Label("Prefix"), "pf-settings__affix"));
            header.Add(Cell(new Label("Suffix"), "pf-settings__affix"));
            table.Add(header);

            for (int i = 0; i < newAssets.arraySize; i++)
            {
                var entry = newAssets.GetArrayElementAtIndex(i);
                var type = KindOf(entry.FindPropertyRelative(nameof(NewAssetSettings.Type)).stringValue);
                if (type == null) continue;
                var row = new VisualElement { name = type.Name };
                row.AddToClassList("pf-table__row");
                row.AddToClassList("pf-settings__row");
                row.EnableInClassList("pf-table__row--alternate", i % 2 == 1);

                var name = new VisualElement();
                name.Add(new ForgeTypeIcon(type));
                var label = new Label(ForgeAssetTypes.NameOf(type));
                label.AddToClassList("pf-table__name");
                name.Add(label);
                row.Add(Cell(name, "pf-table__cell--name", "pf-settings__type"));

                var folderProp = entry.FindPropertyRelative(nameof(NewAssetSettings.Folder));
                var folder = new VisualElement();
                folder.AddToClassList("pf-settings__folder-field");
                var folderField = Text(folderProp, "Folder", $"The folder a new {ForgeAssetTypes.NameOf(type)} goes in");
                folder.Add(folderField);
                folder.Add(new Button(() => ChooseFolder(folderProp, type))
                {
                    name = "ChooseFolder",
                    text = "…",
                    tooltip = $"Choose the folder a new {ForgeAssetTypes.NameOf(type)} goes in",
                });
                row.Add(Cell(folder, "pf-settings__folder"));
                row.Add(Cell(Text(entry.FindPropertyRelative(nameof(NewAssetSettings.Prefix)), "Prefix",
                    "Text a new asset's file name starts with"), "pf-settings__affix"));
                row.Add(Cell(Text(entry.FindPropertyRelative(nameof(NewAssetSettings.Suffix)), "Suffix",
                    "Text a new asset's file name ends with"), "pf-settings__affix"));
                table.Add(row);
            }
            return table;
        }

        // A folder picked in the project, kept as a path from Assets; one outside the project is refused
        private static void ChooseFolder(SerializedProperty folder, Type type)
        {
            var start = Path.GetFullPath(PlayForgeSettings.FolderFor(type).Length > 0 && AssetDatabase.IsValidFolder(PlayForgeSettings.FolderFor(type))
                ? PlayForgeSettings.FolderFor(type)
                : "Assets");
            var chosen = EditorUtility.OpenFolderPanel($"Folder for New {ForgeAssetTypes.NameOf(type)} Assets", start, "");
            if (string.IsNullOrEmpty(chosen)) return;
            var project = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            chosen = Path.GetFullPath(chosen).Replace('\\', '/');
            if (!chosen.StartsWith(project + "Assets", StringComparison.OrdinalIgnoreCase)) return;
            folder.stringValue = chosen.Substring(project.Length);
            folder.serializedObject.ApplyModifiedProperties();
        }

        // A type's defaults: the asset a new one copies, then the assets Import lists first
        private static VisualElement DefaultsOf(Type type, SerializedProperty entry)
        {
            var typeName = ForgeAssetTypes.NameOf(type);
            var fold = new Foldout { text = typeName, value = false, name = type.Name };
            fold.AddToClassList("pf-settings__defaults");
            var newAsset = new ObjectField("New Asset")
            {
                name = "NewAsset",
                objectType = type,
                allowSceneObjects = false,
                bindingPath = entry.FindPropertyRelative(nameof(TypeDefaults.NewAsset)).propertyPath,
                tooltip = $"The asset a new {typeName} starts as a copy of",
            };
            newAsset.AddToClassList(ObjectField.alignedFieldUssClassName);
            fold.Add(newAsset);
            var import = new ListView
            {
                name = "Import",
                bindingPath = entry.FindPropertyRelative(nameof(TypeDefaults.Import)).propertyPath,
                headerTitle = "Import",
                showFoldoutHeader = true,
                showBoundCollectionSize = true,
                showAddRemoveFooter = true,
                reorderable = true,
                reorderMode = ListViewReorderMode.Animated,
                showBorder = true,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                tooltip = $"The assets Import lists first for a {typeName}",
                makeItem = () => new ObjectField { objectType = type, allowSceneObjects = false },
            };
            fold.Add(import);
            return fold;
        }

        private static ObjectField EffectField(SerializedProperty data, string field, string label)
        {
            var property = data.FindPropertyRelative(field);
            var effect = new ObjectField(label)
            {
                name = field,
                objectType = typeof(GameplayEffect),
                allowSceneObjects = false,
                bindingPath = property.propertyPath,
                tooltip = property.tooltip,
            };
            effect.AddToClassList(ObjectField.alignedFieldUssClassName);
            return effect;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Preferences → PlayForge
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Builds the user's page in a root: the tag fields' display, The Forge's view toggles and the log level.</summary>
        internal static VisualElement BuildPreferencesPage(VisualElement root)
        {
            var page = Page(root, "PlayForge");
            var switches = new List<(PlayForgePreferences.Switch preference, Toggle toggle)>();

            Heading(page, "Tags");
            foreach (var each in PlayForgePreferences.TagSwitches) switches.Add((each, Switch(page, each)));

            Heading(page, "The Forge");
            foreach (var each in PlayForgePreferences.ForgeSwitches) switches.Add((each, Switch(page, each)));
            var rows = new IntegerField("Recently Created Rows")
            {
                name = PlayForgePreferences.RecentlyCreatedRowsKey,
                isDelayed = true,
                tooltip = "How many recently created assets the Create tab lists, from 0 to 50",
            };
            rows.AddToClassList(IntegerField.alignedFieldUssClassName);
            rows.SetValueWithoutNotify(PlayForgePreferences.RecentlyCreatedRows);
            rows.RegisterValueChangedCallback(change =>
            {
                PlayForgePreferences.RecentlyCreatedRows = change.newValue;
                rows.SetValueWithoutNotify(PlayForgePreferences.RecentlyCreatedRows);
            });
            page.Add(rows);

            Heading(page, "Logging");
            var level = new EnumField("Minimum Log Level", ForgeLogSettingsBridge.CurrentLevel)
            {
                name = "ForgeLogMinLevel",
                tooltip = "The quietest message PlayForge logs. Verbose logs every trace, Info notable events, Warning and " +
                          "Error only problems, and None nothing. A build logs Info unless your code sets ForgeLog.MinimumLevel",
            };
            level.AddToClassList(EnumField.alignedFieldUssClassName);
            level.RegisterValueChangedCallback(change => ForgeLogSettingsBridge.SetLevel((ForgeLog.ELevel)change.newValue));
            page.Add(level);

            var buttons = new VisualElement();
            buttons.AddToClassList("pf-settings__buttons");
            buttons.Add(new Button(() =>
            {
                PlayForgePreferences.UseDefaults();
                foreach (var (preference, toggle) in switches) toggle.SetValueWithoutNotify(preference.Value);
                rows.SetValueWithoutNotify(PlayForgePreferences.RecentlyCreatedRows);
                level.SetValueWithoutNotify(ForgeLogSettingsBridge.CurrentLevel);
            })
            {
                name = "UseDefaults",
                text = "Use Defaults",
                tooltip = "Put every preference on this page back to its default",
            });
            page.Add(buttons);
            return page;
        }

        private static Toggle Switch(VisualElement page, PlayForgePreferences.Switch preference)
        {
            var toggle = new Toggle(preference.Label) { name = preference.Key, tooltip = preference.Tooltip };
            toggle.AddToClassList(Toggle.alignedFieldUssClassName);
            toggle.SetValueWithoutNotify(preference.Value);
            toggle.RegisterValueChangedCallback(change => preference.Value = change.newValue);
            page.Add(toggle);
            return toggle;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Parts both pages share
        // ═══════════════════════════════════════════════════════════════════════════

        // The page, in the theme, with its title
        private static VisualElement Page(VisualElement root, string title)
        {
            ForgeTheme.Apply(root);
            var page = new ScrollView { name = "PlayForgeSettings" };
            page.AddToClassList("pf-settings");
            var heading = new Label(title);
            heading.AddToClassList("pf-settings__title");
            page.Add(heading);
            root.Add(page);
            return page;
        }

        private static void Heading(VisualElement page, string text)
        {
            var heading = new Label(text);
            heading.AddToClassList("pf-settings__heading");
            page.Add(heading);
        }

        private static Label Note(string text)
        {
            var note = new Label(text);
            note.AddToClassList("pf-note");
            return note;
        }

        private static VisualElement Cell(VisualElement content, params string[] classes)
        {
            content.AddToClassList("pf-table__cell");
            foreach (var each in classes) content.AddToClassList(each);
            return content;
        }

        private static TextField Text(SerializedProperty property, string name, string tooltip)
        {
            var field = new TextField { name = name, isDelayed = true, bindingPath = property.propertyPath, tooltip = tooltip };
            field.AddToClassList("pf-settings__text");
            return field;
        }

        private static Type KindOf(string typeName) =>
            PlayForgeSettings.Kinds.Select(kind => kind.type).FirstOrDefault(type => type.Name == typeName);
    }
}
