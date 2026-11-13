using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// What ProjectSettings/PlayForgeSettings.asset holds: the project's authoring settings from before TW-30, which the
    /// first Forge Project to need them takes, once. <see cref="NewAssetSettings"/> and <see cref="TypeDefaults"/> moved
    /// to the runtime, where a Forge Project holds them.
    /// </summary>
    [Serializable]
    public sealed class PlayForgeSettingsData
    {
        public List<NewAssetSettings> NewAssets = new();
        public List<TypeDefaults> Defaults = new();
        [Tooltip("The effect a new Cost effect starts as a copy of")]
        public GameplayEffect CostEffect;
        [Tooltip("The effect a new Cooldown effect starts as a copy of")]
        public GameplayEffect CooldownEffect;
    }

    /// <summary>
    /// PlayForge's shared settings, which belong to the project rather than to one machine (Q21): where each type's new
    /// assets go and how their files are named, the defaults a new asset copies and Import lists first, and the effects a
    /// new Cost or Cooldown copies. They live in ProjectSettings/PlayForgeSettings.asset, so they travel with the project
    /// under version control, and the Project Settings page edits them. They lived in EditorPrefs, which every project on
    /// the machine shares (ED-21). The view toggles stay in Preferences (<see cref="PlayForgePreferences"/>).
    /// </summary>
    [FilePath("ProjectSettings/PlayForgeSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class PlayForgeSettings : ScriptableSingleton<PlayForgeSettings>
    {
        // The EditorPrefs keys the settings lived under, which a project reads once, to take its settings
        internal const string PrefsPrefix = "PlayForge_";
        internal const string SharedTemplatesKey = "PlayForge_TemplateRegistry";
        internal static string ProjectTemplatesKey => SharedTemplatesKey + "_" + PlayerSettings.productGUID.ToString("N");
        internal const string UsageEffectPrefix = "PlayForge_EffectTemplate_";

        /// <summary>The asset types with settings, in The Forge's order, with the folder each one's new assets go in.</summary>
        internal static readonly (Type type, string folder)[] Kinds =
        {
            (typeof(Ability), "Assets/Data/Abilities"),
            (typeof(GameplayEffect), "Assets/Data/Effects"),
            (typeof(Item), "Assets/Data/Items"),
            (typeof(Attribute), "Assets/Data/Attributes"),
            (typeof(AttributeSet), "Assets/Data/AttributeSets"),
            (typeof(EntityIdentity), "Assets/Data/Entities"),
            (typeof(ScalerAsset), "Assets/Data/Templates/Scalers"),
            (typeof(RequirementTemplate), "Assets/Data/Templates/Requirements"),
            // The rules assets, which The Forge lists too (FN-12, Decision 72)
            (typeof(ValuePipeline), "Assets/Data/Pipelines"),
            (typeof(EffectModifier), "Assets/Data/Modifiers"),
            (typeof(CrowdControlRules), "Assets/Data/Rules"),
        };

        [SerializeField] internal PlayForgeSettingsData Data = new();

        // Whether the first Forge Project to need the settings has taken them, which it does once (TW-30)
        [SerializeField] private bool movedToProject;

        // The project a test uses in place of the active one
        private static ForgeProject usedProject;

        /// <summary>
        /// Uses <paramref name="project"/>'s authoring settings in place of the active project's, as a test does, or the
        /// active project's again for null. Gives back the one used before, to put back (TW-30).
        /// </summary>
        public static ForgeProject UseProject(ForgeProject project)
        {
            var was = usedProject;
            usedProject = project;
            return was;
        }
        // Whether this project has taken the settings it kept in EditorPrefs, which it does on its first use
        [SerializeField] private bool tookMachineSettings;

        // What was last written, so an Undo that changes the settings saves them again
        [NonSerialized] private string savedJson;
        private static bool loaded;

        private void OnEnable() => loaded = true;

        [InitializeOnLoadMethod]
        private static void FollowUndo() => Undo.undoRedoPerformed += () =>
        {
            if (loaded) instance.SaveIfChanged();
        };

        /// <summary>
        /// The settings, once this project has taken what it kept in EditorPrefs. It does on its first use, and leaves
        /// those values in EditorPrefs for the other projects on the machine.
        /// </summary>
        internal static PlayForgeSettings Ready
        {
            get
            {
                var settings = instance;
                if (settings.tookMachineSettings) return settings;
                settings.tookMachineSettings = true;
                TakeMachineSettings(settings.Data);
                settings.SaveSettings();
                return settings;
            }
        }

        /// <summary>Writes the settings to ProjectSettings/PlayForgeSettings.asset.</summary>
        public void SaveSettings()
        {
            Save(true);
            savedJson = EditorJsonUtility.ToJson(this);
        }

        private void SaveIfChanged()
        {
            if (EditorJsonUtility.ToJson(this) != savedJson) SaveSettings();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // What the settings say
        // ═══════════════════════════════════════════════════════════════════════════

        // ─── The project whose settings these are (TW-30) ───

        /// <summary>
        /// The project whose authoring settings The Forge reads and writes: the one a test uses, or else the active Forge
        /// Project; or null, where there's none (TW-30). They were the whole Unity project's, in this file.
        /// </summary>
        internal static ForgeProject Project => usedProject != null ? usedProject : ForgeProjects.Active;

        // A project's authoring settings, ready to read, or null for no project: the old file's taken where none has taken
        // them yet
        private static ForgeAuthoringSettings AuthoringOf(ForgeProject project)
        {
            if (project == null) return null;
            project.Settings ??= new ForgeProjectSettings();
            project.Settings.Authoring ??= new ForgeAuthoringSettings();
            TakeOldSettings(project);
            return project.Settings.Authoring;
        }

        // The project an edit writes to: the one in use, or else the default project, made where there's none
        private static ForgeProject ProjectToWrite()
        {
            var project = Project;
            if (project == null) project = ForgeProjects.EnsureActive();
            if (project != null) AuthoringOf(project);
            return project;
        }

        private static void Save(ForgeProject project)
        {
            EditorUtility.SetDirty(project);
            AssetDatabase.SaveAssetIfDirty(project);
        }

        /// <summary>
        /// Hands what this file held to the first project that needs authoring settings, once, where that project holds
        /// none of its own yet; EditorPrefs' are taken into the file first, as before (TW-30).
        /// </summary>
        private static void TakeOldSettings(ForgeProject project)
        {
            if (instance.movedToProject) return;
            var settings = Ready;
            settings.movedToProject = true;
            settings.SaveSettings();

            var authoring = project.Settings.Authoring;
            if (authoring.NewAssets.Count > 0 || authoring.Defaults.Count > 0 || authoring.CostEffect != null || authoring.CooldownEffect != null) return;
            var old = settings.Data;
            authoring.NewAssets = old.NewAssets.Where(entry => entry != null)
                .Select(entry => new NewAssetSettings { Type = entry.Type, Folder = entry.Folder, Prefix = entry.Prefix, Suffix = entry.Suffix }).ToList();
            authoring.Defaults = old.Defaults.Where(entry => entry != null)
                .Select(entry => new TypeDefaults { Type = entry.Type, NewAsset = entry.NewAsset, Import = new List<ScriptableObject>(entry.Import ?? new List<ScriptableObject>()) })
                .ToList();
            authoring.CostEffect = old.CostEffect;
            authoring.CooldownEffect = old.CooldownEffect;
            Save(project);
        }

        // ─── What the settings say ───

        /// <summary>The folder a new asset of the type goes in, in the project in use, as "Assets/Data/Abilities".</summary>
        public static string FolderFor(Type type) => FolderFor(type, Project);

        /// <summary>The folder a new asset of the type goes in, by a project's settings, as "Assets/Data/Abilities" (TW-30).</summary>
        public static string FolderFor(Type type, ForgeProject project)
        {
            var folder = NewAssetsFor(AuthoringOf(project)?.NewAssets, type)?.Folder;
            return (string.IsNullOrWhiteSpace(folder) ? DefaultFolder(type) : folder.Trim()).TrimEnd('/');
        }

        /// <summary>The text a new asset's file name starts with.</summary>
        public static string PrefixFor(Type type) => NewAssetsFor(AuthoringOf(Project)?.NewAssets, type)?.Prefix ?? "";

        /// <summary>The text a new asset's file name ends with.</summary>
        public static string SuffixFor(Type type) => NewAssetsFor(AuthoringOf(Project)?.NewAssets, type)?.Suffix ?? "";

        /// <summary>The asset a new asset of the type starts as a copy of, or null.</summary>
        public static ScriptableObject NewAssetDefault(Type type)
        {
            var asset = DefaultsFor(AuthoringOf(Project)?.Defaults, type)?.NewAsset;
            return asset != null && type.IsInstanceOfType(asset) ? asset : null;
        }

        /// <summary>The assets Import lists first for the type.</summary>
        public static IEnumerable<ScriptableObject> ImportDefaults(Type type) =>
            (DefaultsFor(AuthoringOf(Project)?.Defaults, type)?.Import ?? new List<ScriptableObject>()).Where(asset => asset != null && type.IsInstanceOfType(asset));

        /// <summary>The effect a new Cost or Cooldown effect starts as a copy of, by "Cost" or "Cooldown".</summary>
        public static GameplayEffect UsageEffect(string key)
        {
            var authoring = AuthoringOf(Project);
            return key switch
            {
                "Cost" => authoring?.CostEffect,
                "Cooldown" => authoring?.CooldownEffect,
                _ => null,
            };
        }

        /// <summary>Sets the effect a new Cost or Cooldown effect starts as a copy of, as one Undo step.</summary>
        public static void SetUsageEffect(string key, GameplayEffect effect)
        {
            var project = ProjectToWrite();
            if (project == null) return;
            var authoring = project.Settings.Authoring;
            Undo.RecordObject(project, $"Set the {key} Effect");
            switch (key)
            {
                case "Cost": authoring.CostEffect = effect; break;
                case "Cooldown": authoring.CooldownEffect = effect; break;
                default: return;
            }
            Save(project);
        }

        /// <summary>Sets where a type's new assets go and how their files are named, as one Undo step.</summary>
        public static void SetNewAssets(Type type, string folder, string prefix, string suffix)
        {
            var project = ProjectToWrite();
            if (project == null) return;
            var authoring = project.Settings.Authoring;
            Undo.RecordObject(project, $"Set New {ForgeAssetTypes.NameOf(type)} Assets");
            EnsureKinds(authoring.NewAssets, authoring.Defaults);
            var entry = authoring.NewAssets.FirstOrDefault(found => found.Type == type.Name);
            if (entry == null) authoring.NewAssets.Add(entry = new NewAssetSettings { Type = type.Name });
            entry.Folder = folder;
            entry.Prefix = prefix ?? "";
            entry.Suffix = suffix ?? "";
            Save(project);
        }

        /// <summary>Sets a type's defaults, the asset a new one copies and the assets Import lists first, as one Undo step.</summary>
        public static void SetDefaults(Type type, ScriptableObject newAsset, IEnumerable<ScriptableObject> import)
        {
            var project = ProjectToWrite();
            if (project == null) return;
            var authoring = project.Settings.Authoring;
            Undo.RecordObject(project, $"Set {ForgeAssetTypes.NameOf(type)} Defaults");
            EnsureKinds(authoring.NewAssets, authoring.Defaults);
            var entry = authoring.Defaults.FirstOrDefault(found => found.Type == type.Name);
            if (entry == null) authoring.Defaults.Add(entry = new TypeDefaults { Type = type.Name });
            entry.NewAsset = newAsset;
            entry.Import = (import ?? Enumerable.Empty<ScriptableObject>()).Where(asset => asset != null).Distinct().ToList();
            Save(project);
        }

        /// <summary>
        /// Puts every type's folder and file names back to their defaults, as one Undo step. The defaults and the Cost and
        /// Cooldown effects are content you chose, so they stay.
        /// </summary>
        public static void UseDefaultFoldersAndNames()
        {
            var project = ProjectToWrite();
            if (project == null) return;
            var authoring = project.Settings.Authoring;
            Undo.RecordObject(project, "Use Default Folders and Names");
            authoring.NewAssets.Clear();
            EnsureKinds(authoring.NewAssets, authoring.Defaults);
            Save(project);
        }

        /// <summary>The folder a type's new assets go in until a setting says otherwise.</summary>
        public static string DefaultFolder(Type type)
        {
            for (var each = type; each != null; each = each.BaseType)
            {
                foreach (var (kind, folder) in Kinds)
                {
                    if (kind == each) return folder;
                }
            }
            return "Assets/Data";
        }

        // The entry for a type, or for the nearest of its base types with one
        internal static NewAssetSettings NewAssetsFor(List<NewAssetSettings> entries, Type type)
        {
            if (entries == null) return null;
            for (var each = type; each != null; each = each.BaseType)
            {
                var entry = entries.FirstOrDefault(found => found != null && found.Type == each.Name);
                if (entry != null) return entry;
            }
            return null;
        }

        internal static TypeDefaults DefaultsFor(List<TypeDefaults> entries, Type type)
        {
            if (entries == null) return null;
            for (var each = type; each != null; each = each.BaseType)
            {
                var entry = entries.FirstOrDefault(found => found != null && found.Type == each.Name);
                if (entry != null) return entry;
            }
            return null;
        }

        /// <summary>Gives every type an entry, in The Forge's order, so the Project Settings page shows each.</summary>
        internal static void EnsureKinds(List<NewAssetSettings> newAssets, List<TypeDefaults> defaults)
        {
            foreach (var (type, folder) in Kinds)
            {
                if (newAssets.All(entry => entry?.Type != type.Name))
                    newAssets.Add(new NewAssetSettings { Type = type.Name, Folder = folder, Prefix = "", Suffix = "" });
                if (defaults.All(entry => entry?.Type != type.Name))
                    defaults.Add(new TypeDefaults { Type = type.Name });
            }
            newAssets.RemoveAll(entry => entry == null);
            defaults.RemoveAll(entry => entry == null);
            int Order(string name)
            {
                int at = Array.FindIndex(Kinds, kind => kind.type.Name == name);
                return at >= 0 ? at : Kinds.Length;
            }
            newAssets.Sort((a, b) => Order(a.Type).CompareTo(Order(b.Type)));
            defaults.Sort((a, b) => Order(a.Type).CompareTo(Order(b.Type)));
        }

        /// <summary>Gives every type an entry in this file's settings, from before TW-30.</summary>
        internal static void EnsureKinds(PlayForgeSettingsData data) => EnsureKinds(data.NewAssets, data.Defaults);

        // ═══════════════════════════════════════════════════════════════════════════
        // The settings a project kept in EditorPrefs
        // ═══════════════════════════════════════════════════════════════════════════

        [Serializable] internal sealed class LegacyTemplates { public List<LegacyCollection> Collections = new(); }
        [Serializable] internal sealed class LegacyCollection { public string TypeName; public List<LegacyTemplate> Templates = new(); }
        [Serializable] internal sealed class LegacyTemplate { public string AssetGUID; public bool IsDefault; }

        /// <summary>
        /// Takes the settings The Forge kept in EditorPrefs: each type's folder, prefix and suffix, the templates this
        /// project registered (by its own key, or else by the one every project shared, ED-22), which become its defaults,
        /// and the Cost and Cooldown templates. A template whose asset isn't in this project is left out.
        /// </summary>
        internal static void TakeMachineSettings(PlayForgeSettingsData data)
        {
            EnsureKinds(data);
            foreach (var (type, folder) in Kinds)
            {
                var entry = NewAssetsFor(data.NewAssets, type);
                entry.Folder = EditorPrefs.GetString(PrefsPrefix + "Path_" + type.Name, folder);
                entry.Prefix = EditorPrefs.GetString(PrefsPrefix + "Prefix_" + type.Name, "");
                entry.Suffix = EditorPrefs.GetString(PrefsPrefix + "Postfix_" + type.Name, "");
            }

            var json = EditorPrefs.GetString(ProjectTemplatesKey, "");
            if (string.IsNullOrEmpty(json)) json = EditorPrefs.GetString(SharedTemplatesKey, "");
            LegacyTemplates templates = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    templates = JsonUtility.FromJson<LegacyTemplates>(json);
                }
                catch (ArgumentException)
                {
                    templates = null;
                }
            }
            foreach (var collection in templates?.Collections ?? new List<LegacyCollection>())
            {
                var type = Kinds.Select(kind => kind.type).FirstOrDefault(kind => kind.Name == collection?.TypeName);
                if (type == null) continue;
                var defaults = DefaultsFor(data.Defaults, type);
                foreach (var template in collection.Templates ?? new List<LegacyTemplate>())
                {
                    var asset = Load<ScriptableObject>(template?.AssetGUID);
                    if (asset == null || !type.IsInstanceOfType(asset)) continue;
                    if (!defaults.Import.Contains(asset)) defaults.Import.Add(asset);
                    if (template.IsDefault && defaults.NewAsset == null) defaults.NewAsset = asset;
                }
            }

            data.CostEffect = Load<GameplayEffect>(EditorPrefs.GetString(UsageEffectPrefix + "Cost", ""));
            data.CooldownEffect = Load<GameplayEffect>(EditorPrefs.GetString(UsageEffectPrefix + "Cooldown", ""));
        }

        private static T Load<T>(string guid) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(guid)) return null;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        /// <summary>
        /// A SerializedObject over a project, for the Project Settings page, with an entry for every type in its authoring
        /// settings, saved where the entries are new (TW-30).
        /// </summary>
        internal static SerializedObject Serialized(ForgeProject project)
        {
            var authoring = AuthoringOf(project);
            int before = authoring.NewAssets.Count + authoring.Defaults.Count;
            EnsureKinds(authoring.NewAssets, authoring.Defaults);
            if (authoring.NewAssets.Count + authoring.Defaults.Count != before) Save(project);
            return new SerializedObject(project);
        }
    }
}
