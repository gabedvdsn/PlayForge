using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// Which PlayForge this is, as its package.json says: its name, its version and the Unity it needs. The file is read
    /// where the package is, whether Unity resolved it as a package or it was imported into Assets, as an Asset Store
    /// import puts it; and the changelog beside it.
    /// </summary>
    internal static class ForgePackageInfo
    {
        [Serializable]
        private sealed class Manifest
        {
            public string name;
            public string displayName;
            public string version;
            public string unity;
        }

        private static string folder;
        private static Manifest manifest;

        /// <summary>The package's folder, the one holding its package.json; or null where it can't be found.</summary>
        internal static string Folder
        {
            get
            {
                if (folder != null) return folder;
                var package = PackageInfo.FindForAssembly(typeof(ForgePackageInfo).Assembly);
                if (package != null) return folder = package.resolvedPath;
                // Imported into Assets: up from this script to the folder that holds package.json
                var guid = AssetDatabase.FindAssets("ForgePackageInfo t:MonoScript").FirstOrDefault();
                var at = guid == null ? null : Path.GetDirectoryName(Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid)));
                while (at != null && !File.Exists(Path.Combine(at, "package.json"))) at = Path.GetDirectoryName(at);
                return folder = at;
            }
        }

        private static Manifest Read()
        {
            if (manifest != null) return manifest;
            var path = Folder == null ? null : Path.Combine(Folder, "package.json");
            manifest = path != null && File.Exists(path) ? JsonUtility.FromJson<Manifest>(File.ReadAllText(path)) : new Manifest();
            return manifest;
        }

        /// <summary>Its name as the Package Manager lists it, as PlayForge.</summary>
        internal static string DisplayName => string.IsNullOrEmpty(Read().displayName) ? "PlayForge" : Read().displayName;

        /// <summary>Its version, as 1.0.0; or null where its package.json can't be read.</summary>
        internal static string Version => string.IsNullOrEmpty(Read().version) ? null : Read().version;

        /// <summary>Its package name, as com.faremerald.playforge; or null.</summary>
        internal static string Name => string.IsNullOrEmpty(Read().name) ? null : Read().name;

        /// <summary>The Unity version it needs, as 6000.0; or null.</summary>
        internal static string Unity => string.IsNullOrEmpty(Read().unity) ? null : Read().unity;

        /// <summary>Its CHANGELOG.md, or null where there's none beside its package.json.</summary>
        internal static string Changelog =>
            Folder != null && File.Exists(Path.Combine(Folder, "CHANGELOG.md")) ? Path.Combine(Folder, "CHANGELOG.md") : null;

        /// <summary>One line of what it is, as "PlayForge 1.0.0 · com.faremerald.playforge · Unity 6000.0 or later".</summary>
        internal static string Line()
        {
            var parts = new[]
            {
                Version == null ? DisplayName : $"{DisplayName} {Version}",
                Name,
                Unity == null ? null : $"Unity {Unity} or later",
            };
            return string.Join(" · ", parts.Where(part => !string.IsNullOrEmpty(part)));
        }
    }
}
