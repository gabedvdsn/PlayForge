using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectSettingsData = FarEmerald.PlayForge.ForgeProjectSettings;

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
            keywords = new HashSet<string>
            {
                "PlayForge", "Folder", "Prefix", "Suffix", "Defaults", "New Asset", "Import", "Cost", "Cooldown",
                "Attributes", "Health", "Mana", "Luck", "Movement Speed",
            },
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
        /// Builds the project's page in a root: the active Forge Project's settings (TW-30), where each type's new assets go
        /// and how they're named, each type's defaults, the Cost and Cooldown effects, its roles and its crowd control. Its
        /// fields bind to the project, so an edit undoes, and each edit is saved in its asset. With no Forge Project, a
        /// button makes one. They were the whole Unity project's, in ProjectSettings/PlayForgeSettings.asset.
        /// </summary>
        internal static VisualElement BuildProjectPage(VisualElement root)
        {
            var page = Page(root, "PlayForge");
            page.Add(ActiveProjectSection());
            return page;
        }

        // Everything the page shows of a project, bound to it, each edit saved in its asset (TW-30)
        private static VisualElement ProjectSettingsPage(ForgeProject project)
        {
            var page = new VisualElement { name = "ProjectSettings" };

            // Its roles and crowd control first, with the project it is, bound on their own (TW-29)
            page.Add(ForgeProjectSettings(project));

            var serialized = PlayForgeSettings.Serialized(project);
            var authoring = serialized.FindProperty($"{nameof(ForgeProject.Settings)}.{nameof(ProjectSettingsData.Authoring)}");
            var section = new VisualElement { name = "Authoring" };

            Heading(section, "New Assets");
            section.Add(Note("Where The Forge and a field's New put each type's new assets, within the project's folder, and how " +
                             "their files are named."));
            section.Add(NewAssetsTable(authoring.FindPropertyRelative(nameof(ForgeAuthoringSettings.NewAssets))));
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

            // The project's own template assets, which The Forge's Create lists beside PlayForge's presets (TW-24)
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
                                "and what its game reads as it runs. Choose one in The Forge, or make the default one, at the " +
                                "project's root, which holds every asset no other project does."));
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
                    tooltip = "Make the default Forge Project, at the project's root, which holds every asset no other project does",
                });
                holder.Add(buttons);
            }
            Fill();
            ForgeProjects.Changed += Fill;
            holder.RegisterCallback<DetachFromPanelEvent>(_ => ForgeProjects.Changed -= Fill);
            return holder;
        }

        /// <summary>
        /// A Forge Project's settings, as the page shows the active project's (TW-29): its attributes' roles, by tag, and its
        /// default crowd control, bound to the project, so an edit undoes; and Fill Roles by Name.
        /// </summary>
        internal static VisualElement ForgeProjectSettings(ForgeProject project)
        {
            var section = new VisualElement { name = "ForgeProject" };
            if (project == null) return section;
            section.Add(Note("What the project's game reads as it runs, through its GameRoot: the part each attribute plays, by a " +
                             "role tag, the crowd control rules an entity follows where it names none, the order an area's " +
                             "effects land on those it takes, by the tags they carry, and what each entity's combat log " +
                             "writes and keeps, and the workers every entity has. PlayForge reads its " +
                             "own roles: Health, what an entity dies of at 0 and what On Hit counts where an entry names none; " +
                             "Luck, read on every chance's caster; Movement Speed, what the Creator Lab's units walk at; " +
                             "Lifesteal, the share of each loss an entity deals that the Lifesteal worker heals it. Your " +
                             "code reads its own by their tags."));

            var held = new ObjectField("Project") { name = "ForgeProjectAsset", objectType = typeof(ForgeProject), tooltip = "The Forge Project whose settings these are" };
            held.SetValueWithoutNotify(project);
            held.SetEnabled(false);
            held.AddToClassList(ObjectField.alignedFieldUssClassName);
            section.Add(held);

            var serialized = new SerializedObject(project);
            var settings = serialized.FindProperty(nameof(ForgeProject.Settings));
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.Roles)), "Roles") { name = "Roles" });
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.CrowdControl)), "Crowd Control") { name = "CrowdControl" });

            // The order an area's effects land on those it takes, by the tags they carry (TW-31)
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.ResolveOrder)), "Resolve Order") { name = "ResolveOrder" });

            // What each entity's combat log writes, and how many entries it keeps (TW-22)
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.CombatLog)), "Combat Log") { name = "CombatLog" });

            // Its purge levels, the tiers a purge reaches and what a death keeps (FN-16)
            section.Add(new PropertyField(settings.FindPropertyRelative(nameof(ProjectSettingsData.PurgeLevels)), "Purge Levels") { name = "PurgeLevels" });

            // The workers every entity in the project has, the Lifesteal worker among them as a project starts (FN-15)
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
                          "Luck, Movement Speed or Move Speed, and Lifesteal",
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
            (Tags.ROLE_MOVEMENT_SPEED, new[] { "movementspeed", "movespeed" }),
            (Tags.ROLE_LIFESTEAL, new[] { "lifesteal", "lifeleech" }),
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

        /// <summary>Builds the user's page in a root: the tag fields' display, The Forge's view toggles and the log level.</summary>
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
