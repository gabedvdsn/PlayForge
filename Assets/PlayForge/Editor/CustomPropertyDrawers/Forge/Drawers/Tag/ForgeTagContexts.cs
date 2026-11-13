using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The contexts a tag is declared for, which the project's Tag Library keeps (ED-83). A tag field's picker offers the
    /// tags used in its context and the tags declared for it, so declaring a context puts a tag in that context's pickers,
    /// whatever uses it. A tag's contexts came only from the fields that used it, so they couldn't be changed, in The Forge
    /// or as a tag was made (the user's report).
    /// </summary>
    internal static class ForgeTagContexts
    {
        /// <summary>
        /// The contexts there are: PlayForge's, any a field of the project names, and the project's own, which its Tag
        /// Libraries keep (FN-9); Universal first.
        /// </summary>
        public static List<string> All()
        {
            var own = typeof(ForgeContext).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue());
            var used = ForgeTagRegistry.GetAllContexts().SelectMany(context => context.ContextStrings);
            var made = Libraries().SelectMany(library => library.Contexts ?? new List<string>());
            return own.Concat(used).Concat(made)
                .Where(context => !string.IsNullOrWhiteSpace(context))
                .Distinct()
                .OrderBy(context => context == ForgeContext.Universal ? 0 : 1)
                .ThenBy(context => context, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Makes a context of the project's own, in its first Tag Library, made where Project Settings puts new assets while
        /// there's none, as one Undo step (FN-9, Decision 70). A context there already changes nothing.
        /// </summary>
        public static void MakeContext(string name)
        {
            name = name?.Trim();
            if (WhyNotContext(name) != null) return;
            var library = Libraries().FirstOrDefault() ?? MakeLibrary();
            Undo.RecordObject(library, "Make a Tag Context");
            library.Contexts ??= new List<string>();
            library.Contexts.Add(name);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            ForgeTagRegistry.MarkDirty();
        }

        /// <summary>Why a name makes no new context, or null where it makes one (FN-9).</summary>
        public static string WhyNotContext(string name) =>
            string.IsNullOrWhiteSpace(name) ? "A context needs a name."
            : All().Contains(name.Trim(), StringComparer.OrdinalIgnoreCase) ? $"There is a context {name.Trim()} already." : null;

        /// <summary>The contexts a tag is declared for, in the Tag Library that names it; none where none does.</summary>
        public static List<string> DeclaredOf(Tag tag)
        {
            var entry = EntryOf(tag, out _);
            return entry?.Contexts?.Where(context => !string.IsNullOrWhiteSpace(context)).Distinct().ToList() ?? new List<string>();
        }

        /// <summary>
        /// Declares a tag for contexts, as one Undo step: its entry in the Tag Library that names it, or a new entry in the
        /// project's first library, made where Project Settings puts new assets while there's none. No contexts takes its
        /// declaration away, so it's for the contexts its uses are in alone.
        /// </summary>
        public static void Declare(Tag tag, IEnumerable<string> contexts)
        {
            if (!tag.IsValid) return;
            var chosen = (contexts ?? Enumerable.Empty<string>()).Where(context => !string.IsNullOrWhiteSpace(context)).Distinct().ToList();
            var entry = EntryOf(tag, out var library);
            if (library == null)
            {
                if (chosen.Count == 0) return;
                library = Libraries().FirstOrDefault() ?? MakeLibrary();
            }

            Undo.RecordObject(library, "Change a Tag's Contexts");
            library.Entries ??= new List<ForgeTagLibrary.Entry>();
            if (chosen.Count == 0) library.Entries.Remove(entry);
            else
            {
                if (entry == null) library.Entries.Add(entry = new ForgeTagLibrary.Entry { Name = tag.Name });
                // A unique tag's name carries a suffix; it reads by its display name, as it does where it's used
                entry.DisplayName = tag.DisplayName;
                entry.Contexts = chosen;
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssetIfDirty(library);
            ForgeTagRegistry.MarkDirty();
        }

        // The entry that names a tag, and its library; null for both where no library names it
        private static ForgeTagLibrary.Entry EntryOf(Tag tag, out ForgeTagLibrary library)
        {
            library = null;
            if (!tag.IsValid) return null;
            foreach (var each in Libraries())
            {
                var entry = each.Entries?.FirstOrDefault(found => found != null && found.Name?.Trim() == tag.Name);
                if (entry == null) continue;
                library = each;
                return entry;
            }
            return null;
        }

        private static IEnumerable<ForgeTagLibrary> Libraries() => AssetDatabase.FindAssets($"t:{nameof(ForgeTagLibrary)}")
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<ForgeTagLibrary>)
            .Where(library => library != null);

        // The project's Tag Library, where Project Settings puts new assets
        private static ForgeTagLibrary MakeLibrary()
        {
            var folder = ForgeAssetCreation.FolderFor(typeof(ForgeTagLibrary));
            ForgeAssetCreation.EnsureFolder(folder);
            var library = ScriptableObject.CreateInstance<ForgeTagLibrary>();
            AssetDatabase.CreateAsset(library, AssetDatabase.GenerateUniqueAssetPath($"{folder}/Tag Library.asset"));
            return library;
        }
    }
}
