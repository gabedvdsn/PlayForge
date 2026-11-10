using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The one Import window, from Decision 28 and the canvas's Import board (ED-32): choose a source, choose the fields
    /// that differ, each pre-checked, and import them as one Undo step. A list is replaced, unless its row asks to add to
    /// it. It serves a section's Import, a whole asset's, and a single property's, such as a worker group. A whole asset
    /// keeps its name, description, icons, asset tag, template and level link (ED-35). Six windows did this before, each
    /// its own way: section import copied every field with no choice, and the Effect's Duration and Impact couldn't import.
    /// </summary>
    public sealed class ForgeImportWindow : EditorWindow
    {
        /// <summary>One field the window can import, by its property path.</summary>
        public sealed class Field
        {
            public string Path;
            public string Label;
            public bool IsList;
            public bool Include = true;
            public bool Append;
        }

        private SerializedObject into;
        private string what;
        private string[] paths;
        private bool acrossTypes;
        private Func<string, bool> skip;
        private Action done;

        private Object source;
        private readonly List<Field> fields = new();
        private readonly List<Field> differing = new();
        private readonly List<Field> matching = new();
        private bool showMatching;
        private string search = "";

        private VisualElement sourcesList;
        private VisualElement fieldsPane;
        private Label changesLabel;
        private Button importButton;

        // ═══════════════════════════════════════════════════════════════════════════
        // Opening
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Imports the fields under <paramref name="paths"/>, such as a section's, from another asset. With
        /// <paramref name="acrossTypes"/>, any PlayForge asset that has a property of the same type at the same path is a
        /// source, as an item's worker group is for an ability's.
        /// </summary>
        public static ForgeImportWindow ShowFor(SerializedObject into, string what, string[] paths, Action done = null, bool acrossTypes = false)
        {
            if (into?.targetObject == null || paths == null || paths.Length == 0) return null;
            var window = CreateInstance<ForgeImportWindow>();
            window.Setup(into, what, paths, done, acrossTypes, _ => false);
            return window;
        }

        /// <summary>
        /// Imports a whole asset's fields from another of its type. Its identity stays its own: the name, description and
        /// icons, the asset tag the name gives, its template and its level link. The import copied the template and the
        /// link, and the nested Definition.Name (ED-35).
        /// </summary>
        public static ForgeImportWindow ShowForAsset(SerializedObject into, Action done = null)
        {
            if (into?.targetObject == null) return null;
            var top = new List<string>();
            var iterator = into.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (!KeepsOwn(iterator.propertyPath)) top.Add(iterator.propertyPath);
                } while (iterator.NextVisible(false));
            }
            var window = CreateInstance<ForgeImportWindow>();
            window.Setup(into, into.targetObject.name, top.ToArray(), done, false, KeepsOwn);
            return window;
        }

        /// <summary>
        /// What a whole-asset import leaves: the script, the identity (Name, Description, Textures, the asset tag), the
        /// template and the level link with its mode.
        /// </summary>
        internal static bool KeepsOwn(string path)
        {
            if (path == "m_Script") return true;
            var leaf = path.Substring(path.LastIndexOf('.') + 1);
            return leaf is "Name" or "Description" or "Textures" or "AssetTag" or "_template" or "_linkedSource" or "LinkMode";
        }

        private void Setup(SerializedObject target, string title, string[] fieldPaths, Action onDone, bool anyType, Func<string, bool> leave)
        {
            into = target;
            what = title;
            paths = fieldPaths;
            done = onDone;
            acrossTypes = anyType;
            skip = leave;
            titleContent = new GUIContent($"Import {what}");
            minSize = new Vector2(640, 420);
            fields.Clear();
            fields.AddRange(FieldsOf(into, paths, skip));
            ShowUtility();
            Build();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The fields: each path's properties, down to values a row can show
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The rows the paths give: a value, a list or a reference is one row; a class or struct gives a row for each of
        /// its fields, two levels down. A magnitude reads as its field's name, as in "Duration" and "Duration scaler".
        /// </summary>
        internal static List<Field> FieldsOf(SerializedObject serialized, IEnumerable<string> paths, Func<string, bool> skip)
        {
            var found = new List<Field>();
            var pathList = paths.ToList();
            foreach (var path in pathList)
            {
                var property = serialized.FindProperty(path);
                if (property == null || skip(path)) continue;
                // One path's fields read without its name, as a section's do; several paths' are told apart by it
                Expand(property, pathList.Count == 1 && !IsLeaf(property, 0) ? "" : property.displayName, found, 0, skip);
            }
            return found;
        }

        private static void Expand(SerializedProperty property, string label, List<Field> found, int depth, Func<string, bool> skip)
        {
            if (IsLeaf(property, depth))
            {
                found.Add(new Field
                {
                    Path = property.propertyPath,
                    Label = string.IsNullOrEmpty(label) ? property.displayName : label,
                    IsList = property.isArray && property.propertyType != SerializedPropertyType.String,
                });
                return;
            }

            bool magnitude = NumberFields.IsNumber(property);
            var owner = string.IsNullOrEmpty(label) ? Trimmed(property.displayName) : Trimmed(label);
            var child = property.Copy();
            var end = property.GetEndProperty();
            if (!child.NextVisible(true)) return;
            do
            {
                if (SerializedProperty.EqualContents(child, end)) break;
                if (skip(child.propertyPath)) continue;
                string childLabel;
                if (magnitude)
                    childLabel = child.name switch
                    {
                        nameof(ScalableFloat.Magnitude) => owner,
                        nameof(ScalableFloat.Scaler) => $"{owner} scaler",
                        _ => $"{owner}: {child.displayName}",
                    };
                else childLabel = string.IsNullOrEmpty(label) ? child.displayName : $"{label} › {child.displayName}";
                Expand(child.Copy(), childLabel, found, depth + 1, skip);
            } while (child.NextVisible(false));
        }

        private static bool IsLeaf(SerializedProperty property, int depth) =>
            property.propertyType != SerializedPropertyType.Generic || property.isArray || !property.hasVisibleChildren ||
            property.type == nameof(Tag) || depth >= 3;

        // "Duration Operation" reads as "Duration", as the inspector names the field
        private static string Trimmed(string label) => label.EndsWith(" Operation") ? label.Substring(0, label.Length - " Operation".Length) : label;

        // ═══════════════════════════════════════════════════════════════════════════
        // The sources
        // ═══════════════════════════════════════════════════════════════════════════

        private IEnumerable<(string group, Object asset)> Sources()
        {
            var type = into.targetObject.GetType();
            var targets = new HashSet<Object>(into.targetObjects);
            // The type's Import defaults, from Project Settings → PlayForge, come first (Q22)
            var defaults = PlayForgeSettings.ImportDefaults(type).Cast<Object>()
                .Where(asset => !targets.Contains(asset)).Distinct().ToList();
            foreach (var each in defaults.OrderBy(NameOf, StringComparer.OrdinalIgnoreCase)) yield return ("Defaults", each);

            var types = acrossTypes ? new[] { typeof(BaseForgeAsset) } : new[] { type };
            var assets = types.SelectMany(each => AssetDatabase.FindAssets($"t:{each.Name}"))
                .Distinct()
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => AssetDatabase.LoadAssetAtPath<ScriptableObject>(path))
                .Where(asset => asset != null && !targets.Contains(asset) && !defaults.Contains(asset))
                .Where(asset => !acrossTypes || asset.GetType() == type || HasSameFields(asset))
                .OrderBy(asset => GroupOf(asset.GetType())).ThenBy(NameOf, StringComparer.OrdinalIgnoreCase);
            foreach (var asset in assets) yield return (GroupOf(asset.GetType()), asset);
        }

        // An asset of another type can be a source when it has each path, holding the same type of value
        private bool HasSameFields(Object asset)
        {
            var other = new SerializedObject(asset);
            return paths.All(path =>
            {
                var mine = into.FindProperty(path);
                var theirs = other.FindProperty(path);
                return mine != null && theirs != null && mine.type == theirs.type;
            });
        }

        private static string GroupOf(Type type)
        {
            var name = ForgeAssetTypes.NameOf(type);
            if (string.IsNullOrEmpty(name)) name = ObjectNames.NicifyVariableName(type.Name);
            return name.EndsWith("y") ? name.Substring(0, name.Length - 1) + "ies" : name + "s";
        }

        internal static string NameOf(Object asset) => asset is BaseForgeAsset forge && !string.IsNullOrEmpty(forge.GetName())
            ? forge.GetName()
            : asset != null ? asset.name : "None";

        private static string TagOf(Object asset) => asset is BaseForgeLevelProvider provider ? TagDisplaySettings.PathOf(provider.GetAssetTag()) : "";

        /// <summary>Chooses the asset to import from, as a click on its row does, and lists what it would change.</summary>
        public void Choose(Object asset)
        {
            source = asset;
            Compare();
            ShowSources();
            ShowFields();
        }

        // Which fields differ from the source's; the others are listed as unchanged
        private void Compare()
        {
            differing.Clear();
            matching.Clear();
            if (source == null) return;
            into.Update();
            var from = new SerializedObject(source);
            foreach (var field in fields)
            {
                var mine = into.FindProperty(field.Path);
                var theirs = from.FindProperty(field.Path);
                if (mine == null || theirs == null) continue;
                if (SerializedProperty.DataEquals(mine, theirs)) matching.Add(field);
                else differing.Add(field);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Importing
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The fields the import would change: those that differ and are checked.</summary>
        public IReadOnlyList<Field> Changes => differing.Where(field => field.Include).ToList();

        /// <summary>Imports the checked fields from the source, as one Undo step, and closes the window.</summary>
        public void Import()
        {
            var changes = Changes;
            if (source == null || changes.Count == 0) return;
            into.Update();
            var from = new SerializedObject(source);
            foreach (var field in changes)
            {
                var theirs = from.FindProperty(field.Path);
                var mine = into.FindProperty(field.Path);
                if (theirs == null || mine == null) continue;
                if (field.IsList && field.Append) Append(theirs, mine);
                else into.CopyFromSerializedProperty(theirs);
            }
            into.ApplyModifiedProperties();
            Undo.SetCurrentGroupName($"Import {what} from {NameOf(source)}");
            foreach (var target in into.targetObjects) EditorUtility.SetDirty(target);
            done?.Invoke();
            Close();
        }

        // Adds the source's items after the list's own
        private static void Append(SerializedProperty theirs, SerializedProperty mine)
        {
            int start = mine.arraySize;
            mine.arraySize = start + theirs.arraySize;
            for (int i = 0; i < theirs.arraySize; i++)
                SerializedCopy.Copy(theirs.GetArrayElementAtIndex(i), mine.GetArrayElementAtIndex(start + i));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The window
        // ═══════════════════════════════════════════════════════════════════════════

        private void Build()
        {
            var root = rootVisualElement;
            root.Clear();
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-import");

            // What the import goes into
            var header = new VisualElement { name = "Into" };
            header.AddToClassList("pf-import__into");
            header.Add(new Label("Into"));
            header.Add(new ForgeGlyph(ForgeAssetTypes.GlyphOf(into.targetObject.GetType())));
            var intoName = new Label(into.isEditingMultipleObjects ? $"{into.targetObjects.Length} assets" : NameOf(into.targetObject));
            intoName.AddToClassList("pf-import__name");
            header.Add(intoName);
            var kind = new Label($"{ObjectNames.NicifyVariableName(into.targetObject.GetType().Name)} · {what}");
            kind.AddToClassList("pf-import__kind");
            header.Add(kind);
            root.Add(header);

            var body = new VisualElement { name = "Body" };
            body.AddToClassList("pf-import__body");
            root.Add(body);

            // 1: the sources
            var left = new VisualElement { name = "Sources" };
            left.AddToClassList("pf-import__sources");
            left.Add(Step("1", "Choose a source"));
            var searchField = new ToolbarSearchField { name = "Search" };
            searchField.AddToClassList("pf-import__search");
            searchField.RegisterValueChangedCallback(change =>
            {
                search = change.newValue ?? "";
                ShowSources();
            });
            left.Add(searchField);
            var scroll = new ScrollView { name = "SourceList" };
            scroll.AddToClassList("pf-import__list");
            sourcesList = scroll.contentContainer;
            left.Add(scroll);
            body.Add(left);

            // 2: the fields
            var right = new VisualElement { name = "Fields" };
            right.AddToClassList("pf-import__fields");
            var stepRow = Step("2", "Choose what to import");
            var matchingToggle = new Toggle("Show matching") { name = "ShowMatching", tooltip = "Show the fields that are the same in both" };
            matchingToggle.AddToClassList("pf-import__matching");
            matchingToggle.RegisterValueChangedCallback(change =>
            {
                showMatching = change.newValue;
                ShowFields();
            });
            stepRow.Add(matchingToggle);
            right.Add(stepRow);
            var fieldsScroll = new ScrollView { name = "FieldList" };
            fieldsPane = fieldsScroll.contentContainer;
            right.Add(fieldsScroll);
            body.Add(right);

            // 3: the import
            var footer = new VisualElement { name = "Footer" };
            footer.AddToClassList("pf-import__footer");
            footer.Add(StepNumber("3"));
            changesLabel = new Label { name = "ChangesLabel" };
            changesLabel.AddToClassList("pf-import__changes");
            footer.Add(changesLabel);
            footer.Add(new Button(Close) { name = "Cancel", text = "Cancel" });
            importButton = new Button(Import) { name = "ImportButton" };
            footer.Add(importButton);
            root.Add(footer);

            ShowSources();
            ShowFields();
        }

        private static VisualElement Step(string number, string text)
        {
            var row = new VisualElement();
            row.AddToClassList("pf-import__step");
            row.Add(StepNumber(number));
            var label = new Label(text);
            label.AddToClassList("pf-import__step-text");
            row.Add(label);
            return row;
        }

        private static Label StepNumber(string number)
        {
            var label = new Label(number);
            label.AddToClassList("pf-import__step-number");
            return label;
        }

        private void ShowSources()
        {
            if (sourcesList == null) return;
            sourcesList.Clear();
            string shownGroup = null;
            int index = 0;
            foreach (var (group, asset) in Sources())
            {
                var name = NameOf(asset);
                var tag = TagOf(asset);
                if (search.Length > 0 && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    tag.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (group != shownGroup)
                {
                    var heading = new Label(group);
                    heading.AddToClassList("pf-import__group");
                    sourcesList.Add(heading);
                    shownGroup = group;
                    index = 0;
                }

                var row = new Button(() => Choose(asset)) { name = name, tooltip = AssetDatabase.GetAssetPath(asset) };
                row.AddToClassList("pf-import__source");
                if (index++ % 2 == 1) row.AddToClassList("pf-import__source--alternate");
                if (asset == source) row.AddToClassList("pf-import__source--selected");
                row.Add(new ForgeGlyph(ForgeAssetTypes.GlyphOf(asset.GetType())));
                var nameLabel = new Label(name);
                nameLabel.AddToClassList("pf-import__source-name");
                row.Add(nameLabel);
                var tagLabel = new Label(tag);
                tagLabel.AddToClassList("pf-import__source-tag");
                row.Add(tagLabel);
                sourcesList.Add(row);
            }
            if (sourcesList.childCount == 0) sourcesList.Add(Note("Nothing to import from."));
        }

        private void ShowFields()
        {
            if (fieldsPane == null) return;
            fieldsPane.Clear();
            var changes = Changes.Count;
            changesLabel.text = source == null ? "Choose a source" : $"{changes} {(changes == 1 ? "change" : "changes")} · one Undo step";
            importButton.text = changes == 1 ? "Import 1 Change" : $"Import {changes} Changes";
            importButton.SetEnabled(changes > 0);

            if (source == null)
            {
                fieldsPane.Add(Note("Choose a source to see what it would change."));
                return;
            }

            var table = new VisualElement { name = "FieldTable" };
            table.AddToClassList("pf-table");
            fieldsPane.Add(table);

            var header = new VisualElement();
            header.AddToClassList("pf-table__header");
            var all = new Toggle { name = "All", tooltip = "Import every changed field" };
            all.AddToClassList("pf-import__check");
            all.SetValueWithoutNotify(differing.Count > 0 && differing.All(field => field.Include));
            all.RegisterValueChangedCallback(change =>
            {
                foreach (var field in differing) field.Include = change.newValue;
                ShowFields();
            });
            header.Add(all);
            header.Add(Cell("Field", "pf-import__field"));
            header.Add(Cell(into.isEditingMultipleObjects ? "Now" : NameOf(into.targetObject), "pf-import__value"));
            header.Add(Cell(NameOf(source), "pf-import__value"));
            header.Add(Cell("", "pf-import__append"));
            table.Add(header);

            into.Update();
            var from = new SerializedObject(source);
            int index = 0;
            foreach (var field in showMatching ? fields.Where(each => differing.Contains(each) || matching.Contains(each)) : differing)
            {
                bool same = matching.Contains(field);
                var row = new VisualElement { name = field.Path };
                row.AddToClassList("pf-table__row");
                row.AddToClassList("pf-import__row");
                if (index++ % 2 == 1) row.AddToClassList("pf-table__row--alternate");
                if (same) row.AddToClassList("pf-import__row--same");

                var check = new Toggle { name = "Include", tooltip = same ? "The same in both" : $"Import {field.Label}" };
                check.AddToClassList("pf-import__check");
                check.SetValueWithoutNotify(!same && field.Include);
                check.SetEnabled(!same);
                check.RegisterValueChangedCallback(change =>
                {
                    field.Include = change.newValue;
                    ShowFields();
                });
                row.Add(check);
                row.Add(Cell(field.Label, "pf-import__field"));
                row.Add(Cell(Describe(into.FindProperty(field.Path)), "pf-import__value", "pf-import__now"));
                row.Add(Cell(Describe(from.FindProperty(field.Path)), "pf-import__value"));

                // A list is replaced, unless its row asks to add the source's items to it
                var append = new Toggle("Add") { name = "Append", tooltip = "Add the source's items to the list, instead of replacing it" };
                append.AddToClassList("pf-import__append");
                append.SetValueWithoutNotify(field.Append);
                append.RegisterValueChangedCallback(change => field.Append = change.newValue);
                append.style.visibility = field.IsList && !same ? Visibility.Visible : Visibility.Hidden;
                row.Add(append);
                table.Add(row);
            }
            if (differing.Count == 0 && !showMatching) fieldsPane.Add(Note("Every field is the same in both."));

            if (!showMatching && matching.Count > 0)
            {
                var unchanged = Note($"Unchanged: {string.Join(", ", matching.Select(field => field.Label))}");
                unchanged.name = "Unchanged";
                fieldsPane.Add(unchanged);
            }
        }

        private static Label Cell(string text, params string[] kinds)
        {
            var cell = new Label(text) { tooltip = text };
            cell.AddToClassList("pf-table__cell");
            foreach (var kind in kinds) cell.AddToClassList(kind);
            return cell;
        }

        private static Label Note(string text)
        {
            var note = new Label(text);
            note.AddToClassList("pf-note");
            note.AddToClassList("pf-import__note");
            return note;
        }

        /// <summary>A field's value as a row shows it, such as "6", "On", "Refresh Duration", "3 items" or a scaler's kind.</summary>
        internal static string Describe(SerializedProperty property)
        {
            if (property == null) return "—";
            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean: return property.boolValue ? "On" : "Off";
                case SerializedPropertyType.Integer: return property.intValue.ToString(CultureInfo.CurrentCulture);
                case SerializedPropertyType.Float: return property.floatValue.ToString("0.##", CultureInfo.CurrentCulture);
                case SerializedPropertyType.String:
                    var text = property.stringValue;
                    if (string.IsNullOrEmpty(text)) return "Empty";
                    var line = text.Split('\n')[0];
                    return line.Length < text.Length ? line + "…" : line;
                case SerializedPropertyType.Enum:
                    var names = property.enumDisplayNames;
                    return property.enumValueIndex >= 0 && property.enumValueIndex < names.Length ? names[property.enumValueIndex] : "—";
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue != null ? NameOf(property.objectReferenceValue) : "None";
                case SerializedPropertyType.ManagedReference:
                    var value = property.managedReferenceValue;
                    return value == null ? "None" : value is AbstractScaler scaler ? ImpactFields.ScalerName(scaler) : ObjectNames.NicifyVariableName(value.GetType().Name);
                case SerializedPropertyType.Generic:
                    if (property.isArray) return property.arraySize == 0 ? "Empty" : $"{property.arraySize} {(property.arraySize == 1 ? "item" : "items")}";
                    if (property.type == nameof(Tag))
                    {
                        var tag = TagDisplaySettings.PathOf(property);
                        return string.IsNullOrEmpty(tag) ? "None" : tag;
                    }
                    return "…";
                default:
                    return property.boxedValue?.ToString() ?? "—";
            }
        }
    }

    /// <summary>
    /// Copies one serialized value onto another of the same shape, field by field. A [SerializeReference] value is copied
    /// as a new object, so the two assets don't share it. The Import window's Add to the List copies items this way.
    /// </summary>
    internal static class SerializedCopy
    {
        public static void Copy(SerializedProperty source, SerializedProperty target)
        {
            if (source == null || target == null) return;
            switch (source.propertyType)
            {
                case SerializedPropertyType.ManagedReference:
                    target.managedReferenceValue = Clone(source.managedReferenceValue);
                    return;
                case SerializedPropertyType.Generic:
                    if (source.isArray)
                    {
                        target.arraySize = source.arraySize;
                        for (int i = 0; i < source.arraySize; i++) Copy(source.GetArrayElementAtIndex(i), target.GetArrayElementAtIndex(i));
                        return;
                    }
                    var child = source.Copy();
                    var end = source.GetEndProperty();
                    if (!child.NextVisible(true)) return;
                    do
                    {
                        if (SerializedProperty.EqualContents(child, end)) break;
                        Copy(child, target.FindPropertyRelative(child.name));
                    } while (child.NextVisible(false));
                    return;
                default:
                    target.boxedValue = source.boxedValue;
                    return;
            }
        }

        private static object Clone(object value)
        {
            if (value == null) return null;
            try
            {
                var clone = Activator.CreateInstance(value.GetType());
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(value), clone);
                return clone;
            }
            catch
            {
                return null;
            }
        }
    }
}
