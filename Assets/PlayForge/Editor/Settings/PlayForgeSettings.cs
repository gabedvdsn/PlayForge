using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// PlayForge's authoring settings, a Forge Project's, so they travel with the project under version control (TW-30):
    /// where each type's new assets go, the defaults a new asset copies and Import lists first, and the effects a new Cost
    /// or Cooldown copies. A new asset's file is its name alone (EP-13). The Project Settings page edits them; the view toggles stay in
    /// Preferences (<see cref="PlayForgePreferences"/>). The file and the EditorPrefs a project took them from once are
    /// gone with that taking (FN-21).
    /// </summary>
    public static class PlayForgeSettings
    {
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
        // ═══════════════════════════════════════════════════════════════════════════
        // What the settings say
        // ═══════════════════════════════════════════════════════════════════════════

        // ─── The project whose settings these are (TW-30) ───

        /// <summary>
        /// The project whose authoring settings The Forge reads and writes: the one a test uses, or else the active Forge
        /// Project; or null, where there's none (TW-30). They were the whole Unity project's, in this file.
        /// </summary>
        internal static ForgeProject Project => usedProject != null ? usedProject : ForgeProjects.Active;

        // A project's authoring settings, ready to read, or null for no project
        private static ForgeAuthoringSettings AuthoringOf(ForgeProject project)
        {
            if (project == null) return null;
            project.Settings ??= new ForgeProjectSettings();
            project.Settings.Authoring ??= new ForgeAuthoringSettings();
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

        // ─── What the settings say ───

        /// <summary>The folder a new asset of the type goes in, in the project in use, as "Assets/Data/Abilities".</summary>
        public static string FolderFor(Type type) => FolderFor(type, Project);

        /// <summary>The folder a new asset of the type goes in, by a project's settings, as "Assets/Data/Abilities" (TW-30).</summary>
        public static string FolderFor(Type type, ForgeProject project)
        {
            var folder = NewAssetsFor(AuthoringOf(project)?.NewAssets, type)?.Folder;
            return (string.IsNullOrWhiteSpace(folder) ? DefaultFolder(type) : folder.Trim()).TrimEnd('/');
        }

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

        /// <summary>Sets where a type's new assets go, as one Undo step.</summary>
        public static void SetNewAssets(Type type, string folder)
        {
            var project = ProjectToWrite();
            if (project == null) return;
            var authoring = project.Settings.Authoring;
            Undo.RecordObject(project, $"Set New {ForgeAssetTypes.NameOf(type)} Assets");
            EnsureKinds(authoring.NewAssets, authoring.Defaults);
            var entry = authoring.NewAssets.FirstOrDefault(found => found.Type == type.Name);
            if (entry == null) authoring.NewAssets.Add(entry = new NewAssetSettings { Type = type.Name });
            entry.Folder = folder;
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
        /// Puts every type's folder back to its default, as one Undo step. The defaults and the Cost and Cooldown effects are
        /// content you chose, so they stay.
        /// </summary>
        public static void UseDefaultFolders()
        {
            var project = ProjectToWrite();
            if (project == null) return;
            var authoring = project.Settings.Authoring;
            Undo.RecordObject(project, "Use Default Folders");
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
                    newAssets.Add(new NewAssetSettings { Type = type.Name, Folder = folder });
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
