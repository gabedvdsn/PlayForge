using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Saves each effect, ability, item and entity that moved an older save's fields into its components as it loaded
    /// (FN-14, Decision 76). An asset moves them each time it loads until it's saved, so this keeps the move.
    /// </summary>
    public static class ForgeComponentPass
    {
        [MenuItem("Window/PlayForge/Move to Components", false, 100)]
        private static void Run()
        {
            int moved = MoveAll();
            EditorUtility.DisplayDialog("Move to Components",
                moved == 0
                    ? "No asset had older fields to move into its components."
                    : $"{moved} asset{(moved == 1 ? "" : "s")} saved, older fields moved into components.",
                "OK");
        }

        /// <summary>Saves every asset that moved older fields into its components as it loaded; how many it saved.</summary>
        public static int MoveAll()
        {
            int moved = 0;
            foreach (var type in new[] { nameof(GameplayEffect), nameof(Ability), nameof(Item), nameof(EntityIdentity) })
            {
                foreach (var guid in AssetDatabase.FindAssets($"t:{type}"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset is not IMovesToComponents { MovedToComponents: true } movable) continue;
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                    movable.MovedToComponents = false;
                    moved++;
                }
            }
            return moved;
        }
    }
}
