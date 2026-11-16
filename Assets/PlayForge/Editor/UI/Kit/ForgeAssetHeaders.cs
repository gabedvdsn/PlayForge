using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The shared header for an inspector that isn't a PlayForge asset's own, as a Scaler asset's, a Value Pipeline's or a
    /// Tag Library's (FN-12, Decision 72): its type's icon, its name as The Forge lists it, its type, its one line where
    /// it has one, its problem count, Open in The Forge for a type The Forge lists, or Show in Project, and the asset menu,
    /// The Forge's row menu. A PlayForge asset's inspector builds its own (<see cref="BasePlayForgeEditor"/>).
    /// </summary>
    internal static class ForgeAssetHeaders
    {
        /// <summary>The header for an editor's asset, following its edits.</summary>
        public static ForgeAssetHeader For(UnityEditor.Editor editor)
        {
            var asset = editor.target as ScriptableObject;
            var header = new ForgeAssetHeader(editor.target.GetType());
            bool one = editor.targets.Length == 1;

            var line = one ? header.AddLine("HeaderLine") : null;
            void Show()
            {
                if (asset == null) return;
                header.NameLabel.text = one ? TheForge.NameOf(asset) : $"{editor.targets.Length} {ForgeAssetTypes.NameOf(asset.GetType())} assets";
                header.Subtitle.text = ForgeAssetTypes.NameOf(asset.GetType());
                if (line != null)
                {
                    var text = ForgeAssetLines.Of(asset);
                    ForgeAssetHeader.ShowLine(line, text, text);
                }
            }
            Show();
            header.TrackSerializedObjectValue(editor.serializedObject, _ => Show());

            // How many problems the validator finds, which opens the Problems view on the asset (Q8)
            var problems = header.AddProblemCount(() => ForgeProblemsWindow.ShowFor(asset));
            void ShowProblems() => header.ShowProblems(editor.targets.Where(each => each != null).SelectMany(each => ForgeValidator.ProblemsOf(each)).ToList());
            problems.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ForgeValidator.Changed += ShowProblems;
                ShowProblems();
            });
            problems.RegisterCallback<DetachFromPanelEvent>(_ => ForgeValidator.Changed -= ShowProblems);

            // Open in The Forge for a type it lists, else Show in Project; in The Forge's own detail, Show in Project
            if (one && asset != null)
            {
                bool listed = TheForge.Types.Any(type => type.IsInstanceOfType(asset));
                var open = header.AddButton(listed ? "Open in The Forge" : "Show in Project",
                    listed ? "Show this asset in The Forge" : "Show this asset in the Project window",
                    () => { if (listed) TheForge.OpenToAsset(asset); else ShowInProject(asset); });
                open.name = "OpenInForge";
                open.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    if (!InForgeDetail(open)) return;
                    open.text = "Show in Project";
                    open.tooltip = "Show this asset in the Project window";
                    open.clickable = new Clickable(() => ShowInProject(asset));
                });
            }

            Button menuButton = null;
            menuButton = header.AddIconButton(ForgeGlyphShape.Menu, "Asset menu", () =>
            {
                var menu = new GenericMenu();
                ForgeAssetActions.AddTo(menu, ForgeAssetActions.For(editor.targets.OfType<ScriptableObject>().ToList(), ForgeAssetActions.ShowCopy));
                menu.DropDown(menuButton.worldBound);
            });
            menuButton.name = "HeaderMenu";
            return header;
        }

        /// <summary>
        /// An inspector of the shared header and the asset's fields, each as Unity draws its type, for an asset with no
        /// inspector of its own (FN-12).
        /// </summary>
        public static VisualElement Inspector(UnityEditor.Editor editor)
        {
            var root = new VisualElement { name = "ForgeInspector" };
            ForgeTheme.Apply(root);
            root.Add(For(editor));
            var fields = new VisualElement { name = "Fields" };
            fields.AddToClassList("pf-inspector__fields");
            InspectorElement.FillDefaultInspector(fields, editor.serializedObject, editor);
            root.Add(fields);
            return root;
        }

        private static void ShowInProject(Object asset)
        {
            if (asset == null) return;
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        // Whether an element is in The Forge's detail pane
        private static bool InForgeDetail(VisualElement element)
        {
            for (var each = element.parent; each != null; each = each.parent)
                if (each.ClassListContains("pf-forge__detail")) return true;
            return false;
        }
    }

    /// <summary>A Requirement Template's inspector: the shared header, then its fields (FN-12).</summary>
    [CustomEditor(typeof(RequirementTemplate))]
    public class RequirementTemplateEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => ForgeAssetHeaders.Inspector(this);
    }

    /// <summary>A Crowd Control Rules asset's inspector: the shared header, then its rules (FN-12).</summary>
    [CustomEditor(typeof(CrowdControlRules))]
    public class CrowdControlRulesEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => ForgeAssetHeaders.Inspector(this);
    }

    /// <summary>A Tag Library's inspector: the shared header, then its entries and its contexts (FN-12).</summary>
    [CustomEditor(typeof(ForgeTagLibrary))]
    public class ForgeTagLibraryEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI() => ForgeAssetHeaders.Inspector(this);
    }

    /// <summary>
    /// A Forge Project's inspector: the shared header, then its settings (FN-12); its catalog a line saying how many assets
    /// it names for saves and how it keeps them (EP-15), where it drew each entry, a list of GUIDs as long as the project.
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
