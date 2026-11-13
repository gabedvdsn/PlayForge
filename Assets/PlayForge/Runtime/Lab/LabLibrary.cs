using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>The kinds of asset the lab's library lists, in its order (LAB-5).</summary>
    public enum LabAssetKind
    {
        Abilities,
        Effects,
        Items,
        Entities,
        AttributeSets,
        Attributes,
        Scalers,
    }

    /// <summary>
    /// The assets the lab plays (LAB-5, LAB-18): in the editor every PlayForge asset of the project or of its active Forge
    /// Project, in a build those a project names. The library panel lists them by kind.
    /// </summary>
    public interface ILabLibrary
    {
        /// <summary>What the library is, as the panel heads it: a project's name, or "Every asset".</summary>
        string Name { get; }

        /// <summary>The assets of a kind, by name.</summary>
        IReadOnlyList<ScriptableObject> Assets(LabAssetKind kind);

        /// <summary>Lists the assets again, as after one is made or deleted.</summary>
        void Refresh();

        /// <summary>The Forge Projects it can play, by name, which the library's header switches between: none where it plays one set of assets (LAB-18).</summary>
        IReadOnlyList<string> Projects => Array.Empty<string>();

        /// <summary>Which of <see cref="Projects"/> it plays now, or -1.</summary>
        int Project => -1;

        /// <summary>Plays another of its projects, its assets listed again.</summary>
        void UseProject(int index) { }
    }

    /// <summary>A library of the assets given, as a test or a build gives them.</summary>
    public sealed class ListLibrary : ILabLibrary
    {
        /// <summary>
        /// The library of what a Forge Project names, the assets in its catalog, under its name, as a build has them
        /// (LAB-18); null for none.
        /// </summary>
        public static ListLibrary Of(ForgeProject project)
        {
            if (!project) return null;
            var named = new List<ScriptableObject>();
            foreach (var entry in project.Entries)
                if (entry.Asset) named.Add(entry.Asset);
            return new ListLibrary(project.name, named);
        }

        private readonly List<ScriptableObject> assets;
        private readonly Dictionary<LabAssetKind, List<ScriptableObject>> byKind = new();

        public string Name { get; }

        public ListLibrary(string name, IEnumerable<ScriptableObject> assets)
        {
            Name = string.IsNullOrEmpty(name) ? "Assets" : name;
            this.assets = new List<ScriptableObject>(assets ?? Array.Empty<ScriptableObject>());
            Refresh();
        }

        public IReadOnlyList<ScriptableObject> Assets(LabAssetKind kind) =>
            byKind.TryGetValue(kind, out var list) ? list : (IReadOnlyList<ScriptableObject>)Array.Empty<ScriptableObject>();

        public void Refresh()
        {
            byKind.Clear();
            foreach (var asset in assets)
            {
                if (!asset || !LabAssets.TryKindOf(asset, out var kind)) continue;
                if (!byKind.TryGetValue(kind, out var list)) byKind[kind] = list = new List<ScriptableObject>();
                list.Add(asset);
            }
            foreach (var list in byKind.Values) list.Sort(LabAssets.ByName);
        }
    }

    /// <summary>What the lab knows of an asset of each kind: its kind, its name, its color.</summary>
    public static class LabAssets
    {
        /// <summary>The kind an asset is listed under, or false for one the lab doesn't list.</summary>
        public static bool TryKindOf(ScriptableObject asset, out LabAssetKind kind)
        {
            switch (asset)
            {
                case Ability:
                    kind = LabAssetKind.Abilities;
                    return true;
                case GameplayEffect:
                    kind = LabAssetKind.Effects;
                    return true;
                case Item:
                    kind = LabAssetKind.Items;
                    return true;
                case EntityIdentity:
                    kind = LabAssetKind.Entities;
                    return true;
                case AttributeSet:
                    kind = LabAssetKind.AttributeSets;
                    return true;
                case Attribute:
                    kind = LabAssetKind.Attributes;
                    return true;
                case ScalerAsset:
                    kind = LabAssetKind.Scalers;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        /// <summary>The asset type a kind lists, as a search for it names.</summary>
        public static Type TypeOf(LabAssetKind kind) => kind switch
        {
            LabAssetKind.Abilities => typeof(Ability),
            LabAssetKind.Effects => typeof(GameplayEffect),
            LabAssetKind.Items => typeof(Item),
            LabAssetKind.Entities => typeof(EntityIdentity),
            LabAssetKind.AttributeSets => typeof(AttributeSet),
            LabAssetKind.Attributes => typeof(Attribute),
            _ => typeof(ScalerAsset),
        };

        /// <summary>A kind's heading in the library.</summary>
        public static string Heading(LabAssetKind kind) => kind switch
        {
            LabAssetKind.AttributeSets => "Attribute Sets",
            _ => kind.ToString(),
        };

        /// <summary>An asset's name as the lab shows it: its own, or its file's.</summary>
        public static string NameOf(UnityEngine.Object asset)
        {
            if (!asset) return "";
            string name = asset switch
            {
                Ability ability => ability.GetName(),
                GameplayEffect effect => effect.GetName(),
                Item item => item.GetName(),
                EntityIdentity entity => entity.GetName(),
                Attribute attribute => attribute.GetName(),
                _ => null,
            };
            return string.IsNullOrWhiteSpace(name) ? asset.name : name;
        }

        /// <summary>
        /// A kind's color, which shows in its icon alone (`Docs/EDITOR_STYLE.md`): the hues The Forge gives each type.
        /// </summary>
        public static Color ColorOf(LabAssetKind kind) => kind switch
        {
            LabAssetKind.Abilities => new Color(0.36f, 0.62f, 0.98f),
            LabAssetKind.Effects => new Color(0.93f, 0.55f, 0.26f),
            LabAssetKind.Items => new Color(0.84f, 0.72f, 0.3f),
            LabAssetKind.Entities => new Color(0.48f, 0.78f, 0.45f),
            LabAssetKind.AttributeSets => new Color(0.66f, 0.5f, 0.88f),
            LabAssetKind.Attributes => new Color(0.6f, 0.6f, 0.66f),
            _ => new Color(0.4f, 0.78f, 0.78f),
        };

        internal static int ByName(ScriptableObject a, ScriptableObject b) =>
            string.Compare(NameOf(a), NameOf(b), StringComparison.OrdinalIgnoreCase);
    }
}
