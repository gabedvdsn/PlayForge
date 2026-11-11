using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A project-authored tag catalogue: tags that exist by declaration rather than by
    /// being referenced somewhere in an asset. The Forge's Create tab writes entries
    /// here, and the editor tag registry (ForgeTagRegistry) scans every library asset
    /// so declared tags appear in tag pickers under their chosen contexts — even before
    /// any asset uses them.
    ///
    /// <para>Multiple libraries may exist (e.g. one per feature); all are scanned.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "PlayForge/Tag Library", fileName = "TagLibrary_")]
    public class ForgeTagLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Full hierarchical tag name, dot-delimited (e.g. Status.Debuff.Burn)")]
            public string Name;

            [Tooltip("The name the tag reads by, where it differs from its Name, as a unique tag's, whose Name has a suffix")]
            public string DisplayName;

            [Tooltip("Contexts this tag is intended for (ForgeContext constants). Empty = Universal.")]
            public List<string> Contexts = new();

            [Tooltip("Author note — what this tag is for")]
            public string Note;
        }

        public List<Entry> Entries = new();
    }
}
