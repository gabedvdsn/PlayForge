using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// Where each help (?) button leads: a page of the package's Documentation folder, and the section on it, opened on
    /// PlayForge's documentation site, which is built from those same pages. The pages ship in the package too, for
    /// reading offline.
    /// </summary>
    internal static class ForgeDocs
    {
        /// <summary>
        /// The documentation site: each Documentation page is a folder under guides/, its opening at index.html and each
        /// of its sections a page of its own.
        /// </summary>
        internal const string Site = "https://gabedvdsn.github.io/PlayForge-Docs/";

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
            ["abilities/components"]       = ("ABILITIES.md", "Components"),
            ["abilities/workers"]          = ("ABILITIES.md", "Workers"),
            ["abilities/localdata"]        = ("ABILITIES.md", "Local Data"),
            ["effects"]                    = ("EFFECTS.md", null),
            // The Identity section keeps its first topic name, from when it was Definition
            ["effects/definition"]         = ("EFFECTS.md", "Identity"),
            ["effects/leveling"]           = ("EFFECTS.md", "Leveling"),
            ["effects/tags"]               = ("EFFECTS.md", "Tags"),
            ["effects/impact"]             = ("EFFECTS.md", "Impact"),
            ["effects/levels"]             = ("EFFECTS.md", "Levels"),
            ["effects/duration"]           = ("EFFECTS.md", "Duration"),
            ["effects/components"]         = ("EFFECTS.md", "Components"),
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
            ["items/components"]           = ("ITEMS.md", "Components"),
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
            ["entities/components"]        = ("ENTITIES.md", "Components"),
            ["entities/workers"]           = ("ENTITIES.md", "Workers"),
            ["entities/death"]             = ("ENTITIES.md", "Death"),
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
            ["cues"]                       = ("CUES.md", null),
            ["cues/routes"]                = ("CUES.md", "Routes"),
            ["cues/raised"]                = ("CUES.md", "Raised"),
        };

        /// <summary>The page and heading a topic names. An unknown topic gives the getting-started page.</summary>
        internal static (string page, string heading) Resolve(string topic) =>
            topic != null && Topics.TryGetValue(topic, out var target) ? target : ("GETTING_STARTED.md", null);

        /// <summary>
        /// A topic's address on the site: its section's own page, named as the site names its heading, in its page's
        /// folder, the page's name in lowercase with a hyphen for each underscore; or the page's opening, for a topic of
        /// the whole page.
        /// </summary>
        internal static string UrlOf(string topic)
        {
            var (page, heading) = Resolve(topic);
            var folder = Path.GetFileNameWithoutExtension(page).ToLowerInvariant().Replace('_', '-');
            return Site + "guides/" + folder + "/" + (heading == null ? "index" : AnchorOf(heading)) + ".html";
        }

        // A heading's name as the site gives it, GitHub's way: lowercase, each space a hyphen, other punctuation dropped
        private static string AnchorOf(string heading)
        {
            var anchor = new StringBuilder(heading.Length);
            foreach (char c in heading.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_') anchor.Append(c);
                else if (c == ' ') anchor.Append('-');
            }
            return anchor.ToString();
        }

        /// <summary>
        /// Opens a topic's section on the documentation site, in the browser. Only a click calls this: the Asset Store
        /// bars editor code from sending users out of the Editor unasked (rule 2.5.1.d).
        /// </summary>
        public static void Open(string topic) => UnityEngine.Application.OpenURL(UrlOf(topic));
    }
}
