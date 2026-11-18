using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectSettingsData = FarEmerald.PlayForge.ForgeProjectSettings;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// PlayForge's two settings pages: Project Settings → PlayForge, for the settings the project shares, and
    /// Preferences → PlayForge, for each user's view toggles. The Forge's Settings tab held both, all in EditorPrefs.
    /// Each page has Unity's own controls, and its look from the style sheet.
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
            keywords = new HashSet<string>
            {
                "PlayForge", "Folder", "Defaults", "New Asset", "Import", "Cost", "Cooldown",
                "Attributes", "Health", "Mana", "Luck", "Movement Speed",
            },
        };

        [SettingsProvider]
        internal static SettingsProvider CreatePreferencesPage() => new(PreferencesPath, SettingsScope.User)
        {
            label = "PlayForge",
            activateHandler = (_, root) => BuildPreferencesPage(root),
            keywords = new HashSet<string> { "PlayForge", "The Forge", "Tag", "Path", "Root", "Log", "Level", "Menu", "Width", "Height" },
        };

        /// <summary>Opens Project Settings → PlayForge.</summary>
        internal static void OpenProjectSettings() => SettingsService.OpenProjectSettings(ProjectPath);

        /// <summary>Opens Preferences → PlayForge.</summary>
        internal static void OpenPreferences() => SettingsService.OpenUserPreferences(PreferencesPath);

        // ═══════════════════════════════════════════════════════════════════════════
        // Project Settings → PlayForge
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the project's page in a root: which PlayForge this is and links to its documentation, then the active
        /// Forge Project's settings, where each type's new assets go, each type's defaults, the Cost and Cooldown effects,
        /// its roles and its crowd control. Its fields bind to the project, so an edit undoes, and each edit is saved in
        /// its asset. With no Forge Project, a button makes one.
        /// </summary>
        internal static VisualElement BuildProjectPage(VisualElement root)
        {
            var page = Page(root, "PlayForge");

            // What PlayForge is and what a Forge Project holds, in two sentences, where a dense note of roles and rules was;
            // each field's tooltip says the rest
            var description = Note("PlayForge is a gameplay ability system: attributes, effects, abilities, tags and items. A " +
                                   "Forge Project holds one game's assets, in a folder of its own, and the settings on this page.");
            description.name = "PlayForgeDescription";
            description.AddToClassList("pf-settings__description");
            page.Add(description);
            page.Add(PackageLine());
            page.Add(DocumentationLinks());
            page.Add(FolderSection());
            page.Add(ActiveProjectSection());
            return page;
        }

        // Which PlayForge this is, as its package.json says: its version and the Unity it needs
        private static Label PackageLine()
        {
            var line = Note(ForgePackageInfo.Line());
            line.name = "PackageVersion";
            line.tooltip = ForgePackageInfo.Folder == null ? "Its package.json wasn't found" : $"From {ForgePackageInfo.Folder}";
            return line;
        }

        // The documentation site's guides, the editor tools guide and the API reference, each button's tooltip its address;
        // and the changelog the package ships
        private static VisualElement DocumentationLinks()
        {
            var links = new VisualElement { name = "Documentation" };
            links.AddToClassList("pf-settings__buttons");
            void Link(string name, string text, string url) =>
                links.Add(new Button(() => ForgeDocs.OpenUrl(url)) { name = name, text = text, tooltip = url });
            Link("Guides", "Guides", ForgeDocs.Site);
            Link("EditorTools", "Editor Tools", ForgeDocs.UrlOf("editor"));
            Link("ApiReference", "API Reference", ForgeDocs.ApiReference);
            var changelog = ForgePackageInfo.Changelog;
            var button = new Button(() => EditorUtility.OpenWithDefaultApp(changelog))
            {
                name = "Changelog", text = "Changelog", tooltip = changelog ?? "No CHANGELOG.md beside the package's package.json",
            };
            button.SetEnabled(changelog != null);
            links.Add(button);
            return links;
        }

        // The PlayForge Folder, where a new Forge Project goes, each in a folder of its own, and the Bootstrap prefabs: the
        // Unity project's own setting, kept in ProjectSettings/PlayForge.asset
        private static VisualElement FolderSection()
        {
            var row = new VisualElement { name = "PlayForgeFolder" };
            row.AddToClassList("pf-settings__folder-field");
            var field = new TextField("PlayForge Folder")
            {
                name = "PlayForgeFolderField",
                value = ForgeFolders.Setting,
                isDelayed = true,
                tooltip = "Where a new Forge Project goes, in a folder of its own, and the Bootstrap prefabs a scene starts PlayForge with",
            };
            field.AddToClassList(TextField.alignedFieldUssClassName);
            field.AddToClassList("pf-settings__text");
            field.RegisterValueChangedCallback(change =>
            {
                ForgeFolders.Setting = change.newValue;
                field.SetValueWithoutNotify(ForgeFolders.Setting);
            });
            row.Add(field);
            row.Add(new Button(() =>
            {
                var chosen = PickFolder("PlayForge Folder", ForgeFolders.Setting, "Assets");
                if (chosen != null) field.value = chosen;
            })
            {
                name = "ChoosePlayForgeFolder",
                text = "…",
                tooltip = "Choose the PlayForge Folder",
            });
            return row;
        }

        // Everything the page shows of a project, bound to it, each edit saved in its asset
        private static VisualElement ProjectSettingsPage(ForgeProject project)
        {
            var page = new VisualElement { name = "ProjectSettings" };

            // Its roles and crowd control first, with the project it is, bound on their own
            page.Add(ForgeProjectSettings(project));

            var serialized = PlayForgeSettings.Serialized(project);
            var authoring = serialized.FindProperty($"{nameof(ForgeProject.Settings)}.{nameof(ProjectSettingsData.Authoring)}");
            var section = new VisualElement { name = "Authoring" };

            Heading(section, "New Assets");
            section.Add(Note("Where The Forge and a field's New put each type's new assets, within the project's folder."));
            section.Add(NewAssetsTable(authoring.FindPropertyRelative(nameof(ForgeAuthoringSettings.NewAssets)), project.Folder));
            var buttons = new VisualElement();
            buttons.AddToClassList("pf-settings__buttons");
            buttons.Add(new Button(() =>
            {
                PlayForgeSettings.UseDefaultFolders();
                serialized.Update();
            })
            {
                name = "UseDefaultFolders",
                text = "Use Default Folders",
                tooltip = "Put every type's folder back to its default. The defaults below stay",
            });
            section.Add(buttons);

            Heading(section, "Defaults");
            section.Add(Note("A new asset starts as a copy of its type's New Asset default, when it has one. Import lists a " +
                             "type's Import defaults first."));
            var defaults = authoring.FindPropertyRelative(nameof(ForgeAuthoringSettings.Defaults));
            for (int i = 0; i < defaults.arraySize; i++)
            {
                var entry = defaults.GetArrayElementAtIndex(i);
                var type = KindOf(entry.FindPropertyRelative(nameof(TypeDefaults.Type)).stringValue);
                if (type != null) section.Add(DefaultsOf(type, entry));
            }

            Heading(section, "Cost and Cooldown");
            section.Add(Note("The effects an ability's Cost and Cooldown fields offer to copy or use, in their right-click menu."));
            section.Add(EffectField(authoring, nameof(ForgeAuthoringSettings.CostEffect), "Cost Effect"));
            section.Add(EffectField(authoring, nameof(ForgeAuthoringSettings.CooldownEffect), "Cooldown Effect"));

            // The project's own template assets, which The Forge's Create lists beside PlayForge's presets
            Heading(section, "Templates");
            section.Add(Note("Assets of the project's own that a new asset can start as a copy of, listed in The Forge's Create " +
                             "menu under their type, after PlayForge's presets."));
            section.Add(new PropertyField(authoring.FindPropertyRelative(nameof(ForgeAuthoringSettings.Templates)), "Templates") { name = "Templates" });

            section.Bind(serialized);
            // Each edit, and an Undo of one, is written to the project's asset
            section.TrackSerializedObjectValue(serialized, _ =>
            {
                EditorUtility.SetDirty(project);
                AssetDatabase.SaveAssetIfDirty(project);
            });
            page.Add(section);
            return page;
        }

        // A row for each type: its name, then its folder within the project's, which it shows first, with a button
        // that picks one. A new asset's file is its name alone: the prefix and suffix columns are gone
        private static VisualElement NewAssetsTable(SerializedProperty newAssets, string projectFolder)
        {
            var table = new VisualElement { name = "NewAssets" };
            table.AddToClassList("pf-table");
            var header = new VisualElement();
            header.AddToClassList("pf-table__header");
            header.Add(Cell(new Label("Type"), "pf-settings__type"));
            header.Add(Cell(new Label("Folder"), "pf-settings__folder"));
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
                var within = new Label($"{projectFolder}/") { name = "FolderRoot", tooltip = $"The project's folder, {projectFolder}" };
                within.AddToClassList("pf-settings__folder-root");
                folder.Add(within);
                var folderField = Text(folderProp, "Folder", $"The folder a new {ForgeAssetTypes.NameOf(type)} goes in, within {projectFolder}");
                folder.Add(folderField);
                folder.Add(new Button(() => ChooseFolder(folderProp, type))
                {
                    name = "ChooseFolder",
                    text = "…",
                    tooltip = $"Choose the folder a new {ForgeAssetTypes.NameOf(type)} goes in",
                });
                row.Add(Cell(folder, "pf-settings__folder"));
                table.Add(row);
            }
            return table;
        }

        // A folder picked within the active project's folder, kept as a path from it, as "Abilities"; the project's
        // folder itself, or one outside it, is refused, as a project's assets stay within its folder
        private static void ChooseFolder(SerializedProperty folder, Type type)
        {
            var project = PlayForgeSettings.Project;
            var root = project != null ? project.Folder : ForgeFolders.Root;
            var chosen = PickFolder($"Folder for New {ForgeAssetTypes.NameOf(type)} Assets", ForgeProjects.FolderIn(project, type), root);
            if (chosen == null || chosen.Length <= root.Length) return;
            folder.stringValue = chosen.Substring(root.Length + 1);
            folder.serializedObject.ApplyModifiedProperties();
        }

        // A folder picked in the project, as a path from Assets, as "Assets/PlayForge/Arena/Abilities", where it's within
        // <within> or is it; else null
        private static string PickFolder(string title, string start, string within)
        {
            var from = Path.GetFullPath(!string.IsNullOrEmpty(start) && AssetDatabase.IsValidFolder(start) ? start
                : AssetDatabase.IsValidFolder(within) ? within : "Assets");
            var chosen = EditorUtility.OpenFolderPanel(title, from, "");
            if (string.IsNullOrEmpty(chosen)) return null;
            var project = Path.GetFullPath(".").Replace('\\', '/').TrimEnd('/') + "/";
            chosen = Path.GetFullPath(chosen).Replace('\\', '/').TrimEnd('/');
            if (!chosen.StartsWith(project, StringComparison.OrdinalIgnoreCase)) return null;
            chosen = chosen.Substring(project.Length);
            return chosen == within || chosen.StartsWith(within + "/", StringComparison.Ordinal) ? chosen : null;
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

        // The active project's settings, or, while there's none, a button that makes the default one; again as another
        // becomes the active one
        private static VisualElement ActiveProjectSection()
        {
            var holder = new VisualElement { name = "ActiveProject" };
            void Fill()
            {
                holder.Clear();
                var project = PlayForgeSettings.Project;
                if (project != null)
                {
                    holder.Add(ProjectSettingsPage(project));
                    return;
                }
                holder.Add(Note("No Forge Project is active, so none holds the project's settings: how The Forge makes its assets, " +
                                "and what its game reads as it runs. Choose one in The Forge, or make one, in a folder of its own " +
                                $"under {ForgeFolders.Root}, with its Game Root and the Bootstrap prefabs."));
                var buttons = new VisualElement();
                buttons.AddToClassList("pf-settings__buttons");
                buttons.Add(new Button(() =>
                {
                    ForgeProjects.EnsureActive();
                    Fill();
                })
                {
                    name = "CreateForgeProject",
                    text = "Create Forge Project",
                    tooltip = $"Make a Forge Project named for the game, in a folder of its own under {ForgeFolders.Root}",
                });
                holder.Add(buttons);
            }
            Fill();
            ForgeProjects.Changed += Fill;
            holder.RegisterCallback<DetachFromPanelEvent>(_ => ForgeProjects.Changed -= Fill);
            return holder;
        }

        /// <summary>
        /// A Forge Project's settings, as the page shows the active project's: its attributes' roles, by tag, and its
        /// default crowd control, bound to the project, so an edit undoes; and Fill Roles by Name.
        /// </summary>
        internal static VisualElement ForgeProjectSettings(ForgeProject project)
        {
            var section = new VisualElement { name = "ForgeProject" };
            if (project == null) return section;
            // A heading and a line, each field's tooltip saying what it is, where one note said it all
            Heading(section, "Forge Project");
            section.Add(Note("What the project's game reads as it runs, through its GameRoot."));

            var held = new ObjectField("Project") { name = "ForgeProjectAsset", objectType = typeof(ForgeProject), tooltip = "The Forge Project whose settings these are" };
            held.SetValueWithoutNotify(project);
            held.SetEnabled(false);
            held.AddToClassList(ObjectField.alignedFieldUssClassName);
            section.Add(held);

            var serialized = new SerializedObject(project);
            var settings = serialized.FindProperty(nameof(ForgeProject.Settings));
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.Roles)), "Roles") { name = "Roles" });
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.CrowdControl)), "Crowd Control") { name = "CrowdControl" });
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.Cues)), "Cue Library") { name = "Cues" });

            // The order an area's effects land on those it takes, by the tags they carry
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.ResolveOrder)), "Resolve Order") { name = "ResolveOrder" });

            // What each entity's combat log writes, and how many entries it keeps
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.CombatLog)), "Combat Log") { name = "CombatLog" });

            // Its purge levels, the tiers a purge reaches and what a death keeps
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.PurgeLevels)), "Purge Levels") { name = "PurgeLevels" });

            // The workers every entity in the project has, the Lifesteal worker among them as a project starts
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.Workers)), "Workers") { name = "Workers" });

            var buttons = new VisualElement();
            buttons.AddToClassList("pf-settings__buttons");
            buttons.Add(new Button(() =>
            {
                if (FillRolesByName(project))
                {
                    EditorUtility.SetDirty(project);
                    AssetDatabase.SaveAssetIfDirty(project);
                }
                serialized.Update();
            })
            {
                name = "FillRolesByName",
                text = "Fill Roles by Name",
                tooltip = "Fill each of PlayForge's roles no attribute plays yet with the project's attribute of its name: Health, " +
                          "Luck, and Movement Speed or Move Speed",
            });
            section.Add(buttons);

            section.Bind(serialized);
            return section;
        }

        // PlayForge's roles, and the names an attribute that plays each goes by
        private static (Tag role, string[] names)[] BuiltInRoles => new[]
        {
            (Tags.ROLE_HEALTH, new[] { "health" }),
            (Tags.ROLE_LUCK, new[] { "luck" }),
            (Tags.ROLE_MOVEMENT_SPEED, new[] { "movementspeed", "movespeed" })
        };

        /// <summary>
        /// Fills each of PlayForge's roles no attribute plays yet with the project's own attribute of its name, by its Name
        /// or else its file's, as one Undo step: Health, Luck, and Movement Speed or Move Speed. True where it filled any.
        /// </summary>
        internal static bool FillRolesByName(ForgeProject project)
        {
            if (project == null) return false;
            var folders = ForgeProject.Folders();
            var attributes = AssetDatabase.FindAssets("t:" + nameof(Attribute))
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Where(path => ForgeProject.Owner(path, folders) == project)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => AssetDatabase.LoadAssetAtPath<Attribute>(path))
                .Where(found => found != null)
                .ToList();

            Attribute Named(string[] names) =>
                attributes.FirstOrDefault(found => names.Contains(Plain(found.Name))) ??
                attributes.FirstOrDefault(found => names.Contains(Plain(found.name)));

            bool filled = false;
            foreach (var (role, names) in BuiltInRoles)
            {
                if (project.Settings.AttributeFor(role) != null || Named(names) is not { } attribute) continue;
                if (!filled) Undo.RecordObject(project, "Fill Roles by Name");
                project.Settings.Roles.Add(new AttributeRole { Role = role, Attribute = attribute });
                filled = true;
            }
            return filled;
        }

        // A name in letters and digits alone, lower case, so "Movement Speed" and "MovementSpeed" are one
        private static string Plain(string name) => new string((name ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        // ═══════════════════════════════════════════════════════════════════════════
        // Preferences → PlayForge
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Builds the user's page in a root: the tag fields' display, The Forge's view toggles, the menus' size and the log
        /// level.
        /// </summary>
        internal static VisualElement BuildPreferencesPage(VisualElement root)
        {
            var page = Page(root, "PlayForge");
            var switches = new List<(PlayForgePreferences.Switch preference, Toggle toggle)>();

            Heading(page, "Tags");
            foreach (var each in PlayForgePreferences.TagSwitches) switches.Add((each, Switch(page, each)));

            if (PlayForgePreferences.ForgeSwitches.Length > 0)
            {
                Heading(page, "The Forge");
                foreach (var each in PlayForgePreferences.ForgeSwitches) switches.Add((each, Switch(page, each)));
            }

            // The most a PlayForge dropdown grows to, a picker's or a field's menu
            Heading(page, "Menus");
            var numbers = new List<(PlayForgePreferences.Number preference, IntegerField field)>();
            foreach (var each in PlayForgePreferences.MenuNumbers) numbers.Add((each, NumberField(page, each)));

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
                foreach (var (preference, field) in numbers) field.SetValueWithoutNotify(preference.Value);
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

        // A whole number, kept within its least and its most as it's typed
        private static IntegerField NumberField(VisualElement page, PlayForgePreferences.Number preference)
        {
            var field = new IntegerField(preference.Label) { name = preference.Key, tooltip = preference.Tooltip, isDelayed = true };
            field.AddToClassList(IntegerField.alignedFieldUssClassName);
            field.SetValueWithoutNotify(preference.Value);
            field.RegisterValueChangedCallback(change =>
            {
                preference.Value = change.newValue;
                field.SetValueWithoutNotify(preference.Value);
            });
            page.Add(field);
            return field;
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
