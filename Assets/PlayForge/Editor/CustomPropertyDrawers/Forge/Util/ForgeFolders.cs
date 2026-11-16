using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Where PlayForge keeps a Unity project's own files (EP-10, Decision 89): its Forge Projects, each in a folder of its
    /// own, as Assets/PlayForge/Arena, its assets in a folder for each type and its Game Root entity in GameRoot; and the
    /// Bootstrap folder, the prefabs a scene brings PlayForge up with (EP-11). The PlayForge Folder is a project setting,
    /// Assets/PlayForge to start, kept in ProjectSettings/PlayForge.asset so it travels with the project. Where it's the
    /// folder PlayForge's own code is in, as in a project that holds the code at Assets/PlayForge, the files go beside it,
    /// in "PlayForge Projects", so the code's folder never holds a project's files.
    /// </summary>
    [FilePath("ProjectSettings/PlayForge.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ForgeFolders : ScriptableSingleton<ForgeFolders>
    {
        /// <summary>The PlayForge Folder until it's set.</summary>
        public const string DefaultFolder = "Assets/PlayForge";

        /// <summary>The Bootstrap folder's name, under the PlayForge Folder (Decision 89).</summary>
        public const string BootstrapName = "Bootstrap";

        [SerializeField] private string folder = DefaultFolder;

        /// <summary>The PlayForge Folder as it's set, as "Assets/PlayForge".</summary>
        public static string Setting
        {
            get => Clean(instance.folder) is { Length: > 0 } set ? set : DefaultFolder;
            set
            {
                instance.folder = Clean(value);
                instance.Save(true);
            }
        }

        /// <summary>
        /// The folder Forge Projects and Bootstrap go in: the PlayForge Folder, or one beside it where that's the folder
        /// PlayForge's code is in.
        /// </summary>
        public static string Root => RootFor(Setting, CodeFolder);

        /// <summary>The Bootstrap folder, which holds the prefabs a scene brings PlayForge up with (EP-11).</summary>
        public static string Bootstrap => $"{Root}/{BootstrapName}";

        /// <summary>
        /// The folder files go in for a PlayForge Folder, where PlayForge's code is in <paramref name="code"/>: the folder
        /// itself, or "… Projects" beside the code's where the folder is the code's or inside it.
        /// </summary>
        internal static string RootFor(string setting, string code)
        {
            setting = Clean(setting) is { Length: > 0 } set ? set : DefaultFolder;
            if (string.IsNullOrEmpty(code)) return setting;
            return setting == code || setting.StartsWith(code + "/", System.StringComparison.Ordinal) ? $"{code} Projects" : setting;
        }

        /// <summary>
        /// The folder PlayForge's code is in, where it's under Assets, as "Assets/Plugins/PlayForge"; null where it's a package
        /// under Packages.
        /// </summary>
        public static string CodeFolder
        {
            get
            {
                var asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName("FarEmerald.PlayForge");
                if (string.IsNullOrEmpty(asmdef) || !asmdef.StartsWith("Assets/", System.StringComparison.Ordinal)) return null;
                // Its Runtime folder's, under the code's
                var runtime = Path.GetDirectoryName(asmdef)?.Replace('\\', '/');
                return string.IsNullOrEmpty(runtime) ? null : Path.GetDirectoryName(runtime)?.Replace('\\', '/');
            }
        }

        private static string Clean(string path) => (path ?? "").Trim().Replace('\\', '/').TrimEnd('/');
    }
}
