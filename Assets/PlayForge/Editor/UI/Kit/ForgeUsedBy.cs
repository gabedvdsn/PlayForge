using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The PlayForge assets whose data refers to an asset, each with its type icon, in name order: what
    /// Docs/EDITOR_STYLE.md calls "a list that shows what uses what". It follows the project as it changes (Q30), and a
    /// row selects its asset. The Attribute inspector's Find All References opened a dialog telling the user how to
    /// search (ED-15).
    /// </summary>
    internal class ForgeUsedBy : VisualElement
    {
        public const string UsedByClass = "pf-used-by";

        private readonly Object asset;

        public ForgeUsedBy(Object asset)
        {
            this.asset = asset;
            AddToClassList(UsedByClass);
            Rebuild();
            RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.projectChanged += Rebuild);
            RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.projectChanged -= Rebuild);
        }

        /// <summary>How many assets the list shows.</summary>
        public int Count { get; private set; }

        /// <summary>Raised each time the list is rebuilt, as the project changes.</summary>
        public event Action Changed;

        /// <summary>The PlayForge assets whose own data refers to the asset, in name order.</summary>
        public static List<BaseForgeAsset> UsersOf(Object asset)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return new List<BaseForgeAsset>();
            return AssetDatabase.FindAssets($"t:{nameof(BaseForgeAsset)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Distinct()
                .Where(candidate => candidate != path && AssetDatabase.GetDependencies(candidate, false).Contains(path))
                .Select(AssetDatabase.LoadAssetAtPath<BaseForgeAsset>)
                .Where(user => user != null)
                .OrderBy(NameOf, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // An asset's PlayForge name, or its file name when it has none
        private static string NameOf(BaseForgeAsset user) => string.IsNullOrEmpty(user.GetName()) ? user.name : user.GetName();

        private void Rebuild()
        {
            Clear();
            var users = UsersOf(asset);
            Count = users.Count;
            if (users.Count == 0)
            {
                var none = new Label("None");
                none.AddToClassList("pf-used-by__none");
                Add(none);
            }
            foreach (var user in users)
            {
                var name = NameOf(user);
                var row = new VisualElement { tooltip = $"Select {name}" };
                row.AddToClassList("pf-used-by__row");
                row.Add(new ForgeTypeIcon(user.GetType()));
                var label = new Label(name);
                label.AddToClassList("pf-used-by__name");
                row.Add(label);
                row.AddManipulator(new Clickable(() =>
                {
                    Selection.activeObject = user;
                    EditorGUIUtility.PingObject(user);
                }));
                Add(row);
            }
            Changed?.Invoke();
        }
    }
}
