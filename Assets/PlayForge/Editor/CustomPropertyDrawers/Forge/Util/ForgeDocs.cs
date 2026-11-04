using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Where each help (?) button leads: a page in the package's Documentation folder, and the section on it. This one
    /// table replaces 49 links to a hosted site on a domain PlayForge doesn't own, some of which named the wrong page
    /// (ED-16). A hosted site would change only <see cref="Open"/>.
    /// </summary>
    internal static class ForgeDocs
    {
        // Each topic's page, and its section's heading there; null for the whole page
        private static readonly Dictionary<string, (string page, string heading)> Topics = new()
        {
            ["abilities"]                  = ("ABILITIES.md", null),
            // The Identity and Requirements sections keep their first topic names, from Definition and Validation Rules
            ["abilities/definition"]       = ("ABILITIES.md", "Identity"),
            ["abilities/tags"]             = ("ABILITIES.md", "Tags"),
            ["abilities/activation"]       = ("ABILITIES.md", "Activation"),
            ["abilities/stages"]           = ("ABILITIES.md", "Stages"),
            ["abilities/leveling"]         = ("ABILITIES.md", "Leveling"),
            ["abilities/validation"]       = ("ABILITIES.md", "Requirements"),
            ["abilities/workers"]          = ("ABILITIES.md", "Workers"),
            ["abilities/localdata"]        = ("ABILITIES.md", "Local Data"),
            ["effects"]                    = ("EFFECTS.md", null),
            // The Identity section keeps its first topic name, from when it was Definition
            ["effects/definition"]         = ("EFFECTS.md", "Identity"),
            ["effects/leveling"]           = ("EFFECTS.md", "Leveling"),
            ["effects/tags"]               = ("EFFECTS.md", "Tags"),
            ["effects/impact"]             = ("EFFECTS.md", "Impact"),
            ["effects/duration"]           = ("EFFECTS.md", "Duration"),
            ["effects/workers"]            = ("EFFECTS.md", "Workers"),
            ["effects/requirements"]       = ("EFFECTS.md", "Requirements"),
            ["effects/localdata"]          = ("EFFECTS.md", "Local Data"),
            ["items"]                      = ("ITEMS.md", null),
            // The Identity section keeps its first topic name, from when it was Definition
            ["items/definition"]           = ("ITEMS.md", "Identity"),
            ["items/tags"]                 = ("ITEMS.md", "Tags"),
            ["items/leveling"]             = ("ITEMS.md", "Leveling"),
            ["items/effects"]              = ("ITEMS.md", "Effects"),
            ["items/abilities"]            = ("ITEMS.md", "Abilities"),
            ["items/workers"]              = ("ITEMS.md", "Workers"),
            ["items/localdata"]            = ("ITEMS.md", "Local Data"),
            ["entities"]                   = ("ENTITIES.md", null),
            // The Identity section keeps its first topic name, from when it was Definition
            ["entities/definition"]        = ("ENTITIES.md", "Identity"),
            ["entities/tags"]              = ("ENTITIES.md", "Tags"),
            ["entities/leveling"]          = ("ENTITIES.md", "Leveling"),
            ["entities/abilities"]         = ("ENTITIES.md", "Abilities"),
            ["entities/items"]             = ("ENTITIES.md", "Items"),
            ["entities/attributes"]        = ("ENTITIES.md", "Attributes"),
            ["entities/workers"]           = ("ENTITIES.md", "Workers"),
            ["entities/localdata"]         = ("ENTITIES.md", "Local Data"),
            ["attributes"]                 = ("ATTRIBUTES.md", null),
            // The Identity and Used By sections keep their first topic names, from Definition and Usage
            ["attributes/definition"]      = ("ATTRIBUTES.md", "Identity"),
            ["attributes/usage"]           = ("ATTRIBUTES.md", "Used By"),
            ["attributes/localdata"]       = ("ATTRIBUTES.md", "Local Data"),
            ["attributesets"]              = ("ATTRIBUTE_SETS.md", null),
            // The Identity section keeps its first topic name, from when it was Definition
            ["attributesets/definition"]   = ("ATTRIBUTE_SETS.md", "Identity"),
            ["attributesets/leveling"]     = ("ATTRIBUTE_SETS.md", "Leveling"),
            ["attributesets/attributes"]   = ("ATTRIBUTE_SETS.md", "Attributes"),
            ["attributesets/subsets"]      = ("ATTRIBUTE_SETS.md", "Sub-Sets"),
            ["attributesets/settings"]     = ("ATTRIBUTE_SETS.md", "Collision Settings"),
            ["attributesets/workers"]      = ("ATTRIBUTE_SETS.md", "Workers"),
            ["attributesets/localdata"]    = ("ATTRIBUTE_SETS.md", "Local Data"),
        };

        /// <summary>The page and heading a topic names. An unknown topic gives the getting-started page.</summary>
        internal static (string page, string heading) Resolve(string topic) =>
            topic != null && Topics.TryGetValue(topic, out var target) ? target : ("GETTING_STARTED.md", null);

        /// <summary>
        /// Opens a topic's page in the system's app for Markdown. Only a click calls this: the Asset Store bars editor
        /// code from sending users out of the Editor unasked (rule 2.5.1.d).
        /// </summary>
        public static void Open(string topic)
        {
            var folder = DocumentationFolder();
            var path = folder == null ? null : Path.Combine(folder, Resolve(topic).page);
            if (path != null && File.Exists(path)) EditorUtility.OpenWithDefaultApp(path);
            else ForgeLog.Warning($"No documentation page for '{topic}'.");
        }

        // The package's Documentation folder, whether PlayForge came as a package or sits in Assets
        private static string DocumentationFolder()
        {
            var package = PackageInfo.FindForAssembly(typeof(ForgeDocs).Assembly);
            if (package != null) return Path.Combine(package.resolvedPath, "Documentation");

            // Imported into Assets: walk up from this script to the folder that holds Documentation
            var guid = AssetDatabase.FindAssets("ForgeDocs t:Script").FirstOrDefault();
            var folder = guid == null ? null : Path.GetDirectoryName(Path.GetFullPath(AssetDatabase.GUIDToAssetPath(guid)));
            while (folder != null && !Directory.Exists(Path.Combine(folder, "Documentation")))
                folder = Path.GetDirectoryName(folder);
            return folder == null ? null : Path.Combine(folder, "Documentation");
        }
    }
}
