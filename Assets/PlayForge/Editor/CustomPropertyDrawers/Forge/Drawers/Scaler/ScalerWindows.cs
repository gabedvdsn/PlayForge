using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Save as Scaler Asset, from a magnitude field's menu, as the scaler board's second step: a name and a folder, then
    /// Save and Use, which saves the field's scaler as a Scaler asset and makes the field share it (ED-33).
    /// </summary>
    public sealed class SaveScalerAssetWindow : EditorWindow
    {
        private SerializedObject serialized;
        private string path;
        private string assetName;
        private string folder;

        public static SaveScalerAssetWindow Show(SerializedProperty operation, string defaultName)
        {
            var window = CreateInstance<SaveScalerAssetWindow>();
            window.serialized = operation.serializedObject;
            window.path = operation.propertyPath;
            window.assetName = defaultName;
            window.folder = ForgeAssetCreation.FolderFor(typeof(ScalerAsset));
            window.titleContent = new GUIContent("Save as Scaler Asset");
            window.minSize = new Vector2(380, 120);
            window.ShowUtility();
            window.Build();
            return window;
        }

        private void OnEnable()
        {
            // A domain reload leaves the window with nothing to save
            EditorApplication.delayCall += () =>
            {
                if (this != null && serialized == null) Close();
            };
        }

        /// <summary>Saves the field's scaler as named, in the folder, makes the field share it, and closes.</summary>
        public ScalerAsset SaveAndUse()
        {
            if (serialized?.targetObject == null) return null;
            serialized.Update();
            var operation = serialized.FindProperty(path);
            var saved = operation != null ? ScalerActions.SaveAsScalerAsset(operation, assetName, folder) : null;
            Close();
            return saved;
        }

        private void Build()
        {
            var root = rootVisualElement;
            root.Clear();
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-dialog");

            var nameField = new TextField("Name") { name = "AssetName", value = assetName, tooltip = "The Scaler asset's name" };
            nameField.AddToClassList(TextField.alignedFieldUssClassName);
            nameField.RegisterValueChangedCallback(change => assetName = change.newValue);
            root.Add(nameField);

            var folderRow = new VisualElement { name = "FolderRow" };
            folderRow.AddToClassList("pf-dialog__row");
            var folderField = new TextField("Folder") { name = "Folder", value = folder, isReadOnly = true, tooltip = "Where the asset is saved" };
            folderField.AddToClassList(TextField.alignedFieldUssClassName);
            folderField.AddToClassList(ForgeAssetTagField.ValueFieldClass);
            folderField.AddToClassList("pf-dialog__grow");
            folderRow.Add(folderField);
            folderRow.Add(new Button(() =>
            {
                var chosen = EditorUtility.OpenFolderPanel("Save the Scaler asset in", folder, "");
                if (string.IsNullOrEmpty(chosen)) return;
                var data = Application.dataPath.Replace('\\', '/');
                chosen = chosen.Replace('\\', '/');
                if (!chosen.StartsWith(data)) return;
                folder = "Assets" + chosen.Substring(data.Length);
                folderField.SetValueWithoutNotify(folder);
            }) { name = "Choose", text = "Choose…" });
            root.Add(folderRow);

            var footer = new VisualElement { name = "Footer" };
            footer.AddToClassList("pf-dialog__footer");
            footer.Add(new Button(Close) { name = "Cancel", text = "Cancel" });
            footer.Add(new Button(() => SaveAndUse()) { name = "SaveAndUse", text = "Save and Use" });
            root.Add(footer);
        }
    }

    /// <summary>
    /// Import Values, from a magnitude field's menu, as the scaler board's fourth step: choose a Scaler asset or another
    /// asset's field, compare their values level by level, with both curves, and Copy Values into the field as one Undo
    /// step. A shorter table's last value holds for the levels past its end. It replaces the scaler import window, which
    /// ignored the kinds a field allows and kept its list for 120 s (ED-32).
    /// </summary>
    public sealed class ScalerValuesWindow : EditorWindow
    {
        private sealed class Source
        {
            public string Group;
            public string Name;
            public string Detail;
            public float[] Values;
        }

        private SerializedObject serialized;
        private string path;
        private string what;
        private Source chosen;
        private string search = "";
        private List<Source> sources = new();

        private VisualElement list;
        private VisualElement compare;
        private Button copyButton;

        public static ScalerValuesWindow Show(SerializedProperty scaler, string what)
        {
            var window = CreateInstance<ScalerValuesWindow>();
            window.serialized = scaler.serializedObject;
            window.path = scaler.propertyPath;
            window.what = what;
            window.titleContent = new GUIContent("Import Values");
            window.minSize = new Vector2(640, 380);
            window.sources = window.FindSources();
            window.ShowUtility();
            window.Build();
            return window;
        }

        private void OnEnable()
        {
            EditorApplication.delayCall += () =>
            {
                if (this != null && serialized == null) Close();
            };
        }

        private float[] Mine()
        {
            serialized.Update();
            return ScalerFields.ValuesOf(serialized.FindProperty(path));
        }

        // The Scaler assets, then every other field in the project's PlayForge assets with values per level
        private List<Source> FindSources()
        {
            var found = new List<Source>();
            foreach (var asset in AssetDatabase.FindAssets($"t:{nameof(ScalerAsset)}").Select(AssetDatabase.GUIDToAssetPath)
                         .Select(AssetDatabase.LoadAssetAtPath<ScalerAsset>).Where(asset => asset != null))
            {
                var values = asset.Scaler?.LevelValues;
                if (values == null || values.Length == 0) continue;
                found.Add(new Source { Group = "Scaler Assets", Name = asset.DisplayName, Detail = "", Values = values.ToArray() });
            }

            var here = serialized.targetObject;
            foreach (var assetPath in AssetDatabase.FindAssets($"t:{nameof(BaseForgeAsset)}").Select(AssetDatabase.GUIDToAssetPath).Distinct())
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                if (asset == null) continue;
                var iterator = new SerializedObject(asset).GetIterator();
                while (iterator.Next(true))
                {
                    if (iterator.propertyType != SerializedPropertyType.ManagedReference) continue;
                    if (asset == here && iterator.propertyPath == path) continue;
                    if (!(iterator.managedReferenceValue is AbstractScaler scaler) || ScalerFields.IsShared(scaler.GetType()) || !scaler.UseScalingOptions()) continue;
                    var values = scaler.LevelValues;
                    if (values == null || values.Length == 0) continue;
                    found.Add(new Source
                    {
                        Group = "Fields in Other Assets",
                        Name = ForgeImportWindow.NameOf(asset),
                        Detail = ScalerFields.FieldLabel(iterator.propertyPath),
                        Values = values.ToArray(),
                    });
                }
            }
            return found;
        }

        /// <summary>Chooses a Scaler asset's values to import, as a click on its row does.</summary>
        public void Choose(ScalerAsset asset)
        {
            var values = asset != null ? asset.Scaler?.LevelValues : null;
            if (values == null) return;
            chosen = new Source { Group = "Scaler Assets", Name = asset.DisplayName, Detail = "", Values = values.ToArray() };
            ShowList();
            ShowCompare();
        }

        /// <summary>Copies the chosen values into the field, as one Undo step, and closes.</summary>
        public void CopyValues()
        {
            if (chosen == null || serialized?.targetObject == null) return;
            serialized.Update();
            var scaler = serialized.FindProperty(path);
            if (scaler == null) return;
            ScalerActions.CopyValues(scaler, chosen.Values);
            Undo.SetCurrentGroupName($"Import Values into {what}");
            Close();
        }

        private void Build()
        {
            var root = rootVisualElement;
            root.Clear();
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-import");

            var header = new VisualElement();
            header.AddToClassList("pf-import__into");
            var title = new Label("Import Values");
            title.AddToClassList("pf-import__name");
            header.Add(title);
            var into = new Label(what);
            into.AddToClassList("pf-import__kind");
            header.Add(into);
            root.Add(header);

            var body = new VisualElement();
            body.AddToClassList("pf-import__body");
            root.Add(body);

            var left = new VisualElement();
            left.AddToClassList("pf-import__sources");
            var searchField = new ToolbarSearchField { name = "Search" };
            searchField.AddToClassList("pf-import__search");
            searchField.RegisterValueChangedCallback(change =>
            {
                search = change.newValue ?? "";
                ShowList();
            });
            left.Add(searchField);
            var scroll = new ScrollView();
            scroll.AddToClassList("pf-import__list");
            list = scroll.contentContainer;
            left.Add(scroll);
            body.Add(left);

            var right = new ScrollView { name = "Compare" };
            right.AddToClassList("pf-import__fields");
            compare = right.contentContainer;
            body.Add(right);

            var footer = new VisualElement();
            footer.AddToClassList("pf-import__footer");
            var copies = new Label($"Copies the values into {what}");
            copies.AddToClassList("pf-import__changes");
            footer.Add(copies);
            footer.Add(new Button(Close) { name = "Cancel", text = "Cancel" });
            copyButton = new Button(CopyValues) { name = "CopyValues", text = "Copy Values" };
            footer.Add(copyButton);
            root.Add(footer);

            ShowList();
            ShowCompare();
        }

        private void ShowList()
        {
            if (list == null) return;
            list.Clear();
            string shownGroup = null;
            int index = 0;
            foreach (var source in sources)
            {
                if (search.Length > 0 && source.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    source.Detail.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (source.Group != shownGroup)
                {
                    var heading = new Label(source.Group);
                    heading.AddToClassList("pf-import__group");
                    list.Add(heading);
                    shownGroup = source.Group;
                    index = 0;
                }
                var each = source;
                var row = new Button(() =>
                {
                    chosen = each;
                    ShowList();
                    ShowCompare();
                }) { name = source.Name };
                row.AddToClassList("pf-import__source");
                if (index++ % 2 == 1) row.AddToClassList("pf-import__source--alternate");
                if (chosen != null && chosen.Name == source.Name && chosen.Detail == source.Detail) row.AddToClassList("pf-import__source--selected");
                var name = new Label(source.Name);
                name.AddToClassList("pf-import__source-name");
                row.Add(name);
                var detail = new Label(source.Detail);
                detail.AddToClassList("pf-import__source-tag");
                row.Add(detail);
                list.Add(row);
            }
            if (list.childCount == 0)
            {
                var none = new Label("No other values to import.");
                none.AddToClassList("pf-note");
                list.Add(none);
            }
        }

        // The values level by level, this field's beside the source's, then both curves and what a shorter table does
        private void ShowCompare()
        {
            if (compare == null) return;
            compare.Clear();
            copyButton?.SetEnabled(chosen != null);
            var mine = Mine();
            if (chosen == null)
            {
                var hint = new Label("Choose a Scaler asset or a field to compare its values.");
                hint.AddToClassList("pf-note");
                compare.Add(hint);
                return;
            }

            var table = new VisualElement { name = "ValueTable" };
            table.AddToClassList("pf-table");
            var head = new VisualElement();
            head.AddToClassList("pf-table__header");
            head.Add(Cell("Level", "pf-import__field"));
            head.Add(Cell(what, "pf-import__value"));
            head.Add(Cell(string.IsNullOrEmpty(chosen.Detail) ? chosen.Name : $"{chosen.Name} · {chosen.Detail}", "pf-import__value"));
            table.Add(head);
            int levels = Mathf.Max(mine.Length, chosen.Values.Length);
            for (int i = 0; i < levels; i++)
            {
                var row = new VisualElement { name = $"Level{i + 1}" };
                row.AddToClassList("pf-table__row");
                if (i % 2 == 1) row.AddToClassList("pf-table__row--alternate");
                row.Add(Cell((i + 1).ToString(), "pf-import__field"));
                row.Add(Cell(ValueAt(mine, i), "pf-import__value", "pf-import__now"));
                row.Add(Cell(ValueAt(chosen.Values, i), "pf-import__value"));
                table.Add(row);
            }
            compare.Add(table);

            var curves = new ForgeCurvePreview { name = "Curves" };
            curves.Show(chosen.Values, mine);
            compare.Add(curves);

            if (chosen.Values.Length < levels)
            {
                var past = chosen.Values.Length + 1;
                var note = new Label(past == levels
                    ? $"Level {levels} takes level {chosen.Values.Length}'s value."
                    : $"Levels {past}–{levels} take level {chosen.Values.Length}'s value.") { name = "Holds" };
                note.AddToClassList("pf-note");
                compare.Add(note);
            }
        }

        private static string ValueAt(float[] values, int index) => values.Length == 0
            ? "—"
            : ScalerFields.Number(values[Mathf.Min(index, values.Length - 1)]);

        private static Label Cell(string text, params string[] kinds)
        {
            var cell = new Label(text) { tooltip = text };
            cell.AddToClassList("pf-table__cell");
            foreach (var kind in kinds) cell.AddToClassList(kind);
            return cell;
        }
    }

    /// <summary>
    /// A Scaler asset's inspector, as the scaler board's third step: its name, kind and how many fields use it, its
    /// Description, its values, and the fields that use it, each a row that selects its asset (ED-33).
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

            var header = new VisualElement { name = "Header" };
            header.AddToClassList("pf-scaler-asset__header");
            var name = new Label(asset.DisplayName) { name = "AssetName" };
            name.AddToClassList("pf-scaler-asset__name");
            header.Add(name);
            var about = new Label { name = "AssetAbout" };
            about.AddToClassList("pf-scaler-asset__about");
            header.Add(about);
            root.Add(header);

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
                var kind = ScalerFields.KindName(asset.Scaler?.GetType());
                about.text = $"Scaler · {kind} · {(uses.Count == 1 ? "1 use" : $"{uses.Count} uses")}";
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
                    var row = new Button(() =>
                    {
                        Selection.activeObject = each.Asset;
                        EditorGUIUtility.PingObject(each.Asset);
                    }) { name = ForgeImportWindow.NameOf(each.Asset), tooltip = $"Select {ForgeImportWindow.NameOf(each.Asset)}" };
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
                Show();
            });
            Show();
            return root;
        }
    }
}
