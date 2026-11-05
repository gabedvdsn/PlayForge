using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// PlayForge's one validator (Q8, ED-15). It runs every <see cref="ForgeValidationRule"/> on each PlayForge asset in
    /// the project and keeps what they find, which the Problems view lists and an inspector's header counts. It follows
    /// the project, so nothing polls (Q30): an edit checks its asset again, an import checks what it brought, and a deleted
    /// asset has every asset checked again, since another may have read it. In batch mode, <see cref="LogAll"/> writes
    /// each problem to the Console. Validate All checked four things, flagged every effect with no workers, and showed 15
    /// problems in a dialog.
    /// </summary>
    [InitializeOnLoad]
    public static class ForgeValidator
    {
        // The rules, PlayForge's and any a project adds, each made once, in a fixed order
        private static readonly List<ForgeValidationRule> Rules = TypeCache.GetTypesDerivedFrom<ForgeValidationRule>()
            .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => (ForgeValidationRule)Activator.CreateInstance(type))
            .ToList();

        // What each of the project's PlayForge assets has, by its instance ID, once the project has been checked
        private static readonly Dictionary<int, List<ForgeProblem>> Known = new();
        private static bool scanned;

        /// <summary>Raised when what the validator knows changes, as after an edit, an import or a delete.</summary>
        public static event Action Changed;

        static ForgeValidator()
        {
            ObjectChangeEvents.changesPublished += OnChanges;
        }

        /// <summary>Runs every rule on an asset, saved or not, and returns what they find.</summary>
        public static IReadOnlyList<ForgeProblem> Check(Object asset)
        {
            var problems = new List<ForgeProblem>();
            if (asset == null) return problems;
            foreach (var rule in Rules)
            {
                try
                {
                    rule.Check(asset, problems);
                }
                catch (Exception exception)
                {
                    // One rule that throws doesn't keep the others from checking
                    Debug.LogException(exception);
                }
            }
            return problems;
        }

        /// <summary>The problems an asset has: a saved one's as the validator last found them, another's as it is now.</summary>
        public static IReadOnlyList<ForgeProblem> ProblemsOf(Object asset)
        {
            if (asset == null) return new List<ForgeProblem>();
            if (!EditorUtility.IsPersistent(asset)) return Check(asset);
            EnsureScanned();
            return Known.TryGetValue(asset.GetInstanceID(), out var found) ? found : Remember(asset);
        }

        /// <summary>Every problem the project's PlayForge assets have.</summary>
        public static IReadOnlyList<ForgeProblem> All
        {
            get
            {
                EnsureScanned();
                return Known.Values.SelectMany(each => each).Where(problem => problem.Asset != null).ToList();
            }
        }

        /// <summary>
        /// Checks every PlayForge asset in the project afresh and writes each problem to the Console, an Error's as an
        /// error, then how many there are. Run it in batch mode, as on a build server, with -executeMethod
        /// FarEmerald.PlayForge.Extended.Editor.ForgeValidator.LogAll.
        /// </summary>
        public static void LogAll()
        {
            Scan();
            var all = All;
            foreach (var problem in all)
            {
                var line = $"[PlayForge problems] {AssetDatabase.GetAssetPath(problem.Asset)}: {problem.Message}";
                if (problem.Severity == ForgeProblemSeverity.Error) Debug.LogError(line, problem.Asset);
                else Debug.LogWarning(line, problem.Asset);
            }
            int assets = all.Select(problem => problem.Asset).Distinct().Count();
            Debug.Log($"[PlayForge problems] {all.Count} {(all.Count == 1 ? "problem" : "problems")} in {assets} {(assets == 1 ? "asset" : "assets")}.");
            Changed?.Invoke();
        }

        /// <summary>Whether the validator checks an asset: every PlayForge asset, Scaler asset and requirement template.</summary>
        public static bool Checks(Object asset) => asset is BaseForgeAsset or ScalerAsset or RequirementTemplate;

        /// <summary>The project's PlayForge assets.</summary>
        internal static IEnumerable<ScriptableObject> ProjectAssets() =>
            new[] { nameof(BaseForgeAsset), nameof(ScalerAsset), nameof(RequirementTemplate) }
                .SelectMany(type => AssetDatabase.FindAssets("t:" + type))
                .Distinct()
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableObject>)
                .Where(Checks);

        private static void EnsureScanned()
        {
            if (!scanned) Scan();
        }

        private static void Scan()
        {
            Known.Clear();
            foreach (var asset in ProjectAssets()) Remember(asset);
            scanned = true;
        }

        private static List<ForgeProblem> Remember(Object asset)
        {
            var problems = Check(asset).ToList();
            Known[asset.GetInstanceID()] = problems;
            return problems;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Following the project
        // ═══════════════════════════════════════════════════════════════════════════

        // An edit, an Inspector's or an Undo's, checks its asset again; a new asset is checked; a deleted one has every
        // asset checked again, the next time someone asks
        private static void OnChanges(ref ObjectChangeEventStream stream)
        {
            if (!scanned) return;
            bool changed = false;
            for (int i = 0; i < stream.length; i++)
            {
                switch (stream.GetEventType(i))
                {
                    case ObjectChangeKind.ChangeAssetObjectProperties:
                        stream.GetChangeAssetObjectPropertiesEvent(i, out var edited);
                        changed |= Recheck(EditorUtility.InstanceIDToObject(edited.instanceId));
                        break;
                    case ObjectChangeKind.CreateAssetObject:
                        stream.GetCreateAssetObjectEvent(i, out var created);
                        changed |= Recheck(EditorUtility.InstanceIDToObject(created.instanceId));
                        break;
                    case ObjectChangeKind.DestroyAssetObject:
                        scanned = false;
                        changed = true;
                        break;
                }
            }
            if (changed) Changed?.Invoke();
        }

        // What an import brought: an asset changed on disk, as by a merge, is checked again, and a deleted or moved one
        // has every asset checked again
        internal static void OnImported(string[] imported, string[] deleted, string[] moved)
        {
            if (!scanned) return;
            bool changed = false;
            if (deleted.Length > 0 || moved.Length > 0)
            {
                scanned = false;
                changed = true;
            }
            else
            {
                foreach (var path in imported)
                {
                    if (!path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) continue;
                    changed |= Recheck(AssetDatabase.LoadMainAssetAtPath(path));
                }
            }
            if (changed) Changed?.Invoke();
        }

        // Checks an asset again, and says whether what it has changed
        private static bool Recheck(Object asset)
        {
            if (!Checks(asset) || !EditorUtility.IsPersistent(asset)) return false;
            Known.TryGetValue(asset.GetInstanceID(), out var before);
            var now = Remember(asset);
            return before == null ? now.Count > 0 : !Same(before, now);
        }

        private static bool Same(List<ForgeProblem> a, List<ForgeProblem> b) =>
            a.Count == b.Count && a.Zip(b, (x, y) => x.Severity == y.Severity && x.Message == y.Message).All(same => same);
    }

    // Tells the validator what an import brought
    internal sealed class ForgeValidatorImports : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom) =>
            ForgeValidator.OnImported(imported, deleted, moved);
    }
}
