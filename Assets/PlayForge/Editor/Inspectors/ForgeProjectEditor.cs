using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A Forge Project's inspector: the shared header, then its settings; its catalog a line saying how many assets
    /// it names for saves and how it keeps them, where it drew each entry, a list of GUIDs as long as the project.
    /// </summary>
    [CustomEditor(typeof(ForgeProject))]
    public class ForgeProjectEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = ForgeAssetHeaders.Inspector(this);
            var entries = root.Q<PropertyField>("PropertyField:entries");
            if (entries?.parent == null) return root;

            var line = new Label { name = "CatalogLine" };
            line.AddToClassList("pf-note");
            void Show()
            {
                if (target is ForgeProject project) line.text = CatalogLine(project);
            }
            Show();
            line.TrackSerializedObjectValue(serializedObject, _ => Show());
            entries.parent.Insert(entries.parent.IndexOf(entries), line);
            entries.RemoveFromHierarchy();
            return root;
        }

        /// <summary>What a project's catalog names, as its inspector says it, as "Names 166 assets for saves, kept current".</summary>
        internal static string CatalogLine(ForgeProject project)
        {
            int count = project.Count;
            var names = $"Names {count} {(count == 1 ? "asset" : "assets")} for saves";
            return project.KeepCatalogCurrent ? names + ", kept current as they're made, moved in and deleted"
                : names + "; Add Its Assets names them";
        }
    }
}
