using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>Where a type's new assets go, and how their files are named.</summary>
    [Serializable]
    public sealed class NewAssetSettings
    {
        [Tooltip("The asset type, by its class name")]
        public string Type;
        [Tooltip("The folder a new asset of this type goes in")]
        public string Folder;
        [Tooltip("Text a new asset's file name starts with")]
        public string Prefix;
        [Tooltip("Text a new asset's file name ends with")]
        public string Suffix;
    }

    /// <summary>A type's defaults: the asset a new one starts as a copy of, and the assets Import lists first.</summary>
    [Serializable]
    public sealed class TypeDefaults
    {
        [Tooltip("The asset type, by its class name")]
        public string Type;
        [Tooltip("The asset a new asset of this type starts as a copy of")]
        public ScriptableObject NewAsset;
        [Tooltip("The assets Import lists first for this type")]
        public List<ScriptableObject> Import = new();
    }

    /// <summary>What the project's shared settings hold.</summary>
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
        };

        [SerializeField] internal PlayForgeSettingsData Data = new();
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

        /// <summary>The folder a new asset of the type goes in, as "Assets/Data/Abilities".</summary>
        public static string FolderFor(Type type)
        {
            var folder = NewAssetsFor(Ready.Data, type)?.Folder;
            return (string.IsNullOrWhiteSpace(folder) ? DefaultFolder(type) : folder.Trim()).TrimEnd('/');
        }

        /// <summary>The text a new asset's file name starts with.</summary>
        public static string PrefixFor(Type type) => NewAssetsFor(Ready.Data, type)?.Prefix ?? "";

        /// <summary>The text a new asset's file name ends with.</summary>
        public static string SuffixFor(Type type) => NewAssetsFor(Ready.Data, type)?.Suffix ?? "";

        /// <summary>The asset a new asset of the type starts as a copy of, or null.</summary>
        public static ScriptableObject NewAssetDefault(Type type)
        {
            var asset = DefaultsFor(Ready.Data, type)?.NewAsset;
            return asset != null && type.IsInstanceOfType(asset) ? asset : null;
        }

        /// <summary>The assets Import lists first for the type.</summary>
        public static IEnumerable<ScriptableObject> ImportDefaults(Type type) =>
            (DefaultsFor(Ready.Data, type)?.Import ?? new List<ScriptableObject>()).Where(asset => asset != null && type.IsInstanceOfType(asset));

        /// <summary>The effect a new Cost or Cooldown effect starts as a copy of, by "Cost" or "Cooldown".</summary>
        public static GameplayEffect UsageEffect(string key) => key switch
        {
            "Cost" => Ready.Data.CostEffect,
            "Cooldown" => Ready.Data.CooldownEffect,
            _ => null,
        };

        /// <summary>Sets the effect a new Cost or Cooldown effect starts as a copy of, as one Undo step.</summary>
        public static void SetUsageEffect(string key, GameplayEffect effect)
        {
            var settings = Ready;
            Undo.RecordObject(settings, $"Set the {key} Effect");
            switch (key)
            {
                case "Cost": settings.Data.CostEffect = effect; break;
                case "Cooldown": settings.Data.CooldownEffect = effect; break;
                default: return;
            }
            settings.SaveSettings();
        }

        /// <summary>Sets where a type's new assets go and how their files are named, as one Undo step.</summary>
        public static void SetNewAssets(Type type, string folder, string prefix, string suffix)
        {
            var settings = Ready;
            Undo.RecordObject(settings, $"Set New {ForgeAssetTypes.NameOf(type)} Assets");
            EnsureKinds(settings.Data);
            var entry = settings.Data.NewAssets.FirstOrDefault(found => found.Type == type.Name);
            if (entry == null) settings.Data.NewAssets.Add(entry = new NewAssetSettings { Type = type.Name });
            entry.Folder = folder;
            entry.Prefix = prefix ?? "";
            entry.Suffix = suffix ?? "";
            settings.SaveSettings();
        }

        /// <summary>Sets a type's defaults, the asset a new one copies and the assets Import lists first, as one Undo step.</summary>
        public static void SetDefaults(Type type, ScriptableObject newAsset, IEnumerable<ScriptableObject> import)
        {
            var settings = Ready;
            Undo.RecordObject(settings, $"Set {ForgeAssetTypes.NameOf(type)} Defaults");
            EnsureKinds(settings.Data);
            var entry = settings.Data.Defaults.FirstOrDefault(found => found.Type == type.Name);
            if (entry == null) settings.Data.Defaults.Add(entry = new TypeDefaults { Type = type.Name });
            entry.NewAsset = newAsset;
            entry.Import = (import ?? Enumerable.Empty<ScriptableObject>()).Where(asset => asset != null).Distinct().ToList();
            settings.SaveSettings();
        }

        /// <summary>
        /// Puts every type's folder and file names back to their defaults, as one Undo step. The defaults and the Cost and
        /// Cooldown effects are content you chose, so they stay.
        /// </summary>
        public static void UseDefaultFoldersAndNames()
        {
            var settings = Ready;
            Undo.RecordObject(settings, "Use Default Folders and Names");
            settings.Data.NewAssets.Clear();
            EnsureKinds(settings.Data);
            settings.SaveSettings();
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
        internal static NewAssetSettings NewAssetsFor(PlayForgeSettingsData data, Type type)
        {
            for (var each = type; each != null; each = each.BaseType)
            {
                var entry = data.NewAssets.FirstOrDefault(found => found != null && found.Type == each.Name);
                if (entry != null) return entry;
            }
            return null;
        }

        internal static TypeDefaults DefaultsFor(PlayForgeSettingsData data, Type type)
        {
            for (var each = type; each != null; each = each.BaseType)
            {
                var entry = data.Defaults.FirstOrDefault(found => found != null && found.Type == each.Name);
                if (entry != null) return entry;
            }
            return null;
        }

        /// <summary>Gives every type an entry, in The Forge's order, so the Project Settings page shows each.</summary>
        internal static void EnsureKinds(PlayForgeSettingsData data)
        {
            foreach (var (type, folder) in Kinds)
            {
                if (data.NewAssets.All(entry => entry?.Type != type.Name))
                    data.NewAssets.Add(new NewAssetSettings { Type = type.Name, Folder = folder, Prefix = "", Suffix = "" });
                if (data.Defaults.All(entry => entry?.Type != type.Name))
                    data.Defaults.Add(new TypeDefaults { Type = type.Name });
            }
            data.NewAssets.RemoveAll(entry => entry == null);
            data.Defaults.RemoveAll(entry => entry == null);
            int Order(string name)
            {
                int at = Array.FindIndex(Kinds, kind => kind.type.Name == name);
                return at >= 0 ? at : Kinds.Length;
            }
            data.NewAssets.Sort((a, b) => Order(a.Type).CompareTo(Order(b.Type)));
            data.Defaults.Sort((a, b) => Order(a.Type).CompareTo(Order(b.Type)));
        }

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
                var entry = NewAssetsFor(data, type);
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
                var defaults = DefaultsFor(data, type);
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
        /// A SerializedObject over the settings, for the Project Settings page, with every type's entry. A
        /// ScriptableSingleton isn't editable until it's told so.
        /// </summary>
        internal static SerializedObject Serialized()
        {
            var settings = Ready;
            EnsureKinds(settings.Data);
            settings.hideFlags &= ~HideFlags.NotEditable;
            return new SerializedObject(settings);
        }
    }
}
