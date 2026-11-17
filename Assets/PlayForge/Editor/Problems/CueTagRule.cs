using System.Collections.Generic;
using UnityEditor;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A Cue Tag cue whose tag plays nowhere: it names none, or its project has no cue library, or the library routes
    /// neither the tag nor a parent of it. A Note, as an entity's Cue Receiver may play the tag its own way.
    /// </summary>
    internal sealed class CueTagRule : ForgeValidationRule
    {
        public override string Name => "Cue tags that play nowhere";

        // A cue library's routes, and a project's choice of library, say which tags play nowhere
        public override bool Reads(Object asset) => asset is CueLibrary or ForgeProject;

        public override void Check(Object asset, List<ForgeProblem> problems)
        {
            CueLibrary library = null;
            bool looked = false;
            foreach (var (cue, path) in CueTags.In(asset))
            {
                if (!looked)
                {
                    library = CueTags.LibraryFor(asset);
                    looked = true;
                }
                var label = ScalerFields.FieldLabel(path);
                string message;
                if (!cue.Tag.IsValid) message = $"{label} raises no cue tag, so it plays nothing.";
                else if (library == null)
                    message = $"{label} raises {cue.Tag}, but the project names no Cue Library, so it plays only where an " +
                              "entity's Cue Receiver plays it.";
                else if (ForgeCueRoutes.RouteFor(library, cue.Tag) == null)
                    message = $"{label} raises {cue.Tag}, which the Cue Library routes nowhere, neither its own route nor a " +
                              "parent's, so it plays only where an entity's Cue Receiver plays it.";
                else continue;
                problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Note, message, path));
            }
        }
    }

    /// <summary>The cue tags an asset raises, by its Cue Tag cues, wherever they're listed in it.</summary>
    internal static class CueTags
    {
        /// <summary>
        /// Each Cue Tag cue in the asset, with where it is: an effect's, a Cues component's, a Play Cue task's.
        /// </summary>
        public static IEnumerable<(TagCue Cue, string Path)> In(Object asset)
        {
            if (asset == null) yield break;
            using var serialized = new SerializedObject(asset);
            var iterator = serialized.GetIterator();
            while (iterator.Next(true))
                if (iterator.propertyType == SerializedPropertyType.ManagedReference && iterator.managedReferenceValue is TagCue cue)
                    yield return (cue, iterator.propertyPath);
        }

        /// <summary>The cue library the asset's tags route through: its project's, or else the game's.</summary>
        public static CueLibrary LibraryFor(Object asset)
        {
            var project = ForgeProjects.Of(asset);
            return project != null && project.Settings?.Cues != null ? project.Settings.Cues : ForgeCueRoutes.Library;
        }
    }
}
