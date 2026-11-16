using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A Scaler asset's inspector: its name, kind and how many fields use it, its
    /// Description, its values, and the fields that use it, each a row that selects its asset. Its Name names it,
    /// as menus and The Forge list it; it could only be read, so a Scaler asset couldn't be renamed in The Forge.
    /// </summary>
    [CustomEditor(typeof(ScalerAsset))]
    public class ScalerAssetEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var asset = (ScalerAsset)target;
            var root = new VisualElement { name = "ScalerAssetRoot" };
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-scaler-asset");

            // The shared header, as every PlayForge asset's inspector opens with, and under it what the asset is and how many
            // fields use it
            var header = ForgeAssetHeaders.For(this);
            var name = header.NameLabel;
            var about = header.AddLine("AssetAbout");
            root.Add(header);

            // Its name, as lists and menus show it: its scaler's, or its file's while it has no scaler to hold one
            var rename = new TextField("Name") { name = "ScalerName", value = asset.DisplayName, isDelayed = true, tooltip = "The Scaler asset's name, as menus and The Forge list it" };
            rename.AddToClassList(TextField.alignedFieldUssClassName);
            rename.RegisterValueChangedCallback(change => Rename(asset, change.newValue));
            root.Add(rename);

            var description = new TextField("Description") { name = "Description", multiline = true, bindingPath = nameof(ScalerAsset.Description) };
            description.AddToClassList(TextField.alignedFieldUssClassName);
            root.Add(description);

            var values = ForgeDrawerStyles.CreateCollapsibleSection(new ForgeDrawerStyles.SectionConfig
            {
                Name = "Values",
                Title = "Values",
                IncludeButtons = new[] { false, false, false },
            });
            values.Content.Add(new PropertyField(serializedObject.FindProperty(nameof(ScalerAsset.Scaler)), "Kind") { name = "Scaler" });
            root.Add(values.Section);

            // What it gives at each level, against a caster, a target and a caller the author gives it, typed or filled from
            // an entity; it only reads the asset, so it has no Import or Clear
            var tryIt = ForgeDrawerStyles.CreateCollapsibleSection(new ForgeDrawerStyles.SectionConfig
            {
                Name = "Try",
                Title = "Try",
                IncludeButtons = new[] { false, false, false },
            });
            var tried = new ScalerTryView(asset);
            tryIt.Content.Add(tried);
            tryIt.Summary.text = tried.Summary;
            root.Add(tryIt.Section);

            var usedBy = ForgeDrawerStyles.CreateCollapsibleSection(new ForgeDrawerStyles.SectionConfig
            {
                Name = "UsedBy",
                Title = "Used By",
                IncludeButtons = new[] { false, false, false },
            });
            root.Add(usedBy.Section);

            void Show()
            {
                if (target == null) return;
                var uses = ScalerFields.UsesOf(asset);
                // A Table, or By Level, as the kind menu names a number by level
                var kind = asset.Scaler != null ? NumberText.KindName(asset.Scaler) : ScalerFields.KindName(null);
                ForgeAssetHeader.ShowLine(about, $"Scaler · {kind} · {(uses.Count == 1 ? "1 use" : $"{uses.Count} uses")}");
                usedBy.Summary.text = uses.Count == 0 ? "None" : uses.Count == 1 ? "1 field" : $"{uses.Count} fields";
                usedBy.Content.Clear();
                if (uses.Count == 0)
                {
                    var none = new Label("No field shares this scaler.");
                    none.AddToClassList("pf-note");
                    usedBy.Content.Add(none);
                    return;
                }
                foreach (var use in uses.OrderBy(each => ForgeImportWindow.NameOf(each.Asset), StringComparer.OrdinalIgnoreCase))
                {
                    var each = use;
                    // A row opens its asset in The Forge, which it selected in the Project window
                    var row = new Button(() => TheForge.OpenToAsset(each.Asset))
                    {
                        name = ForgeImportWindow.NameOf(each.Asset), tooltip = $"Open {ForgeImportWindow.NameOf(each.Asset)} in The Forge",
                    };
                    row.AddToClassList("pf-used-by__row");
                    row.Add(new ForgeGlyph(ForgeAssetTypes.GlyphOf(each.Asset.GetType())));
                    var assetName = new Label(ForgeImportWindow.NameOf(each.Asset));
                    assetName.AddToClassList("pf-used-by__name");
                    row.Add(assetName);
                    var field = new Label(each.Label);
                    field.AddToClassList("pf-used-by__field");
                    row.Add(field);
                    usedBy.Content.Add(row);
                }
            }

            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.projectChanged += Show);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.projectChanged -= Show);
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                if (target == null) return;
                name.text = asset.DisplayName;
                if (rename.focusController?.focusedElement != rename) rename.SetValueWithoutNotify(asset.DisplayName);
                // An edit to its kind or its values reads again
                tried.Refresh();
                tryIt.Summary.text = tried.Summary;
                Show();
            });
            Show();
            return root;
        }

        /// <summary>
        /// Renames a Scaler asset: its scaler's name, as one Undo step; or, while it has no scaler to hold one, its file.
        /// An empty name leaves it as it was.
        /// </summary>
        internal static void Rename(ScalerAsset asset, string name)
        {
            name = name?.Trim();
            if (asset == null || string.IsNullOrEmpty(name) || name == asset.DisplayName) return;
            if (asset.Scaler != null)
            {
                Undo.RecordObject(asset, "Rename Scaler Asset");
                asset.Scaler.Name = name;
                EditorUtility.SetDirty(asset);
                ScalerFields.ForgetUses();
                return;
            }
            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path)) AssetDatabase.RenameAsset(path, ForgeAssetCreation.FileNameOf(name));
        }
    }
}
