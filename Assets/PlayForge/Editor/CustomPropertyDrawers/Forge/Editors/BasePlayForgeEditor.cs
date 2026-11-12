using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Base class for all PlayForge asset inspectors: a header with the asset's name and type, and the content below it.
    /// The content scrolls in the Inspector's own scroll view, and the header stays at its top (Decision 29).
    /// </summary>
    public abstract class BasePlayForgeEditor : UnityEditor.Editor
    {
        protected VisualElement root;
        protected VisualElement headerContainer;

        // Header element references for live updates
        private Label _headerNameLabel;
        private Label _headerSubtitleLabel;
        
        protected int _pickerControlId;
        protected bool _waitingForPicker;
        protected Object _lastPickedObject;
        protected System.Type _pickerType;
        protected System.Action<Object> _pickerCallback;
        

        /// <summary>
        /// Creates a collapsible section that remembers expand/collapse state per editor type.
        /// All instances of the same editor (e.g., all AbilityEditors) share state, so collapsing "Tags" on one
        /// Ability keeps it collapsed when switching to another Ability.
        /// Use this instead of CreateCollapsibleSection for automatic state persistence.
        /// </summary>
        protected SectionResult CreateSection(SectionConfig config)
        {
            var editorType = GetType();

            var isExpanded = GetOrInitSectionState(editorType.FullName + '.' + config.Name, true);
            config.StartExpanded = isExpanded;

            // Wire the onClick to persist state
            var result = CreateCollapsibleSection(config, (sectionName, expanded) =>
            {
                SetSectionState(editorType.FullName + '.' + config.Name, expanded);
            });

            sections.Add(result);
            return result;

            bool GetOrInitSectionState(string key, bool initValue)
            {
                if (!EditorPrefs.HasKey(key))
                {
                    SetSectionState(key, initValue);
                    return initValue;
                }

                return EditorPrefs.GetBool(key, false);
            }

            void SetSectionState(string key, bool flag)
            {
                EditorPrefs.SetBool(key, flag);
            }
        }

        protected virtual void OnEnable()
        {
            // Nothing listens to the editor's update here: an inspector listens only while an object picker is open,
            // through WaitForPicker. It listened every frame of its life (ED-11)
            // An Undo of a rename brings the file back with the name (ED-70)
            Undo.undoRedoPerformed += FileFollowsName;
        }

        protected virtual void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= FileFollowsName;
            _waitingForPicker = false;
        }

        // The assets this inspector renamed, whose files follow their names through Undo and Redo. Only these: an asset
        // whose file never matched its name keeps its file as an unrelated Undo is made (ED-70)
        private readonly HashSet<Object> renamedHere = new();

        // Each renamed asset's file named for its name again, as an Undo or a Redo changes the name
        private void FileFollowsName()
        {
            foreach (var each in renamedHere.ToList())
            {
                if (each is BaseForgeAsset asset && !string.IsNullOrWhiteSpace(asset.GetName())) RenameFile(asset, asset.GetName());
            }
        }

        /// <summary>
        /// Renames an asset's file after its name, with its type's prefix and suffix from Project Settings, so the two
        /// don't drift apart (ED-70, the user's request): a character a file can't have is left out, as a new asset's file
        /// leaves it; a name another file in its folder has takes a number, as Unity's own duplicate does; and an Undo of
        /// the rename brings the file back with the name, for an asset this inspector renamed. An asset not saved yet has no
        /// file to rename.
        /// </summary>
        protected void RenameFile(Object asset, string name)
        {
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsMainAsset(asset) || string.IsNullOrWhiteSpace(name)) return;
            renamedHere.Add(asset);
            var type = asset.GetType();
            var wanted = $"{PlayForgeSettings.PrefixFor(type)}{ForgeAssetCreation.FileNameOf(name)}{PlayForgeSettings.SuffixFor(type)}";
            var current = System.IO.Path.GetFileNameWithoutExtension(path);
            if (current == wanted) return;
            int slash = path.LastIndexOf('/');
            var folder = slash < 0 ? "Assets" : path.Substring(0, slash);
            var extension = System.IO.Path.GetExtension(path);
            var free = System.IO.Path.GetFileNameWithoutExtension(AssetDatabase.GenerateUniqueAssetPath($"{folder}/{wanted}{extension}"));
            // The unique name of its own file, where it's the one that has the name with a number already
            if (free == current) return;
            var error = AssetDatabase.RenameAsset(path, free);
            if (!string.IsNullOrEmpty(error)) ForgeLog.Warning($"{asset.name} keeps its file's name: {error}");
        }

        // The object picker reports its choice only to code that asks, so the inspector asks each frame while one is open
        // and stops when it closes (ED-11)
        private void WaitForPicker()
        {
            _waitingForPicker = true;
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!_waitingForPicker)
            {
                EditorApplication.update -= OnEditorUpdate;
                return;
            }

            var currentControlId = EditorGUIUtility.GetObjectPickerControlID();

            if (currentControlId == 0)
            {
                _waitingForPicker = false;
                EditorApplication.update -= OnEditorUpdate;
                if (_lastPickedObject != null && _pickerCallback != null)
                {
                    _pickerCallback(_lastPickedObject);
                    serializedObject.Update();
                    MarkDirty(GetAsset());
                    Repaint();
                }
                _lastPickedObject = null;
                _pickerCallback = null;
            }
            else if (currentControlId == _pickerControlId)
            {
                _lastPickedObject = EditorGUIUtility.GetObjectPickerObject();
            }
        }

        protected abstract BaseForgeLevelProvider GetAsset();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Methods - Header Data (Must be implemented)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Display name shown in header (e.g., ability name)</summary>
        protected abstract string GetDisplayName();
        
        /// <summary>Short description shown below name</summary>
        protected abstract string GetDisplayDescription();
        
        /// <summary>Icon texture for the header (can be null)</summary>
        protected abstract Texture2D GetHeaderIcon();
        
        /// <summary>Asset type label (e.g., "ABILITY", "EFFECT")</summary>
        protected abstract string GetAssetTypeLabel();
        
        /// <summary>Accent color for the asset type</summary>
        protected abstract Color GetAssetTypeColor();
        
        /// <summary>Documentation URL for this asset type</summary>
        /// <summary>The header help button's topic in ForgeDocs, such as "abilities". None by default.</summary>
        protected virtual string GetDocumentationTopic() => null;

        protected string GetProviderTypeName(Type type)
        {
            if (type == typeof(Item)) return "Item";
            if (type == typeof(EntityIdentity)) return "Entity";
            if (type == typeof(Ability)) return "Ability";
            if (type == typeof(GameplayEffect)) return "Effect";
            if (type == typeof(Attribute)) return "Attribute";
            if (type == typeof(AttributeSet)) return "Attribute Set";

            return "Provider";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Methods - Actions (Must be implemented)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Called when the Refresh button is clicked</summary>
        protected abstract void Refresh();

        // ═══════════════════════════════════════════════════════════════════════════
        // Virtual Methods - Optional Overrides
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Called when Open button is clicked. Opens PlayForge Manager to this asset.</summary>
        protected virtual void OnOpenInManager()
        {
            TheForge.OpenToAsset(target);
        }

        // Marks each field the asset overrides of its template, as a prefab instance's are: a field bound to an overridden
        // path, or to one with an overridden field inside it, outside the drawers that draw it (Q23, ED-13)
        private void ShowOverrides()
        {
            if (root == null) return;
            var overridden = !IsMultiple && target is BaseForgeAsset { HasTemplate: true } child
                ? child.GetOverriddenPaths()
                : (IReadOnlyList<string>)Array.Empty<string>();
            root.Query<VisualElement>().ForEach(element =>
            {
                if (element is not IBindable bindable || string.IsNullOrEmpty(bindable.bindingPath)) return;
                if (element is not PropertyField && element.GetFirstAncestorOfType<PropertyField>() != null) return;
                var path = bindable.bindingPath;
                element.EnableInClassList("pf-override", overridden.Any(each => each == path || each.StartsWith(path + ".")));
            });
        }

        // Whether an element is in The Forge's detail pane, which holds an asset's inspector
        private static bool InForgeDetail(VisualElement element)
        {
            for (var each = element.parent; each != null; each = each.parent)
            {
                if (each.ClassListContains("pf-forge__detail")) return true;
            }
            return false;
        }

        // The inspector's sections, in order, whose titles and one-line summaries The Forge's detail pane reads (Q6)
        private readonly List<SectionResult> sections = new();

        /// <summary>The inspector's sections, in the order it shows them.</summary>
        internal IReadOnlyList<SectionResult> Sections => sections;
        
        /// <summary>Whether to show the Import button</summary>
        protected virtual bool ShowImportButton => false;

        protected virtual bool ShowTemplateButton => true;

        /// <summary>
        /// Whether this inspector edits several assets at once (ED-12). An inspector that writes every edit through its
        /// SerializedObject turns this on and carries [CanEditMultipleObjects]; the others still refuse a multiple
        /// selection.
        /// </summary>
        protected virtual bool SupportsMultipleObjects => false;

        /// <summary>
        /// Called after any change to the asset, an Undo or a Redo among them (ED-12). A section drawn from the asset's
        /// values rather than bound redraws here. Does nothing by default.
        /// </summary>
        protected virtual void OnAssetChanged() { }

        // The question a link or unlink asks before it changes assets, answered true to go ahead. Tests replace it, since
        // a dialog would block their run (ED-12)
        internal static Func<string, string, string, bool> ConfirmLinkChange = (title, message, ok) =>
            EditorUtility.DisplayDialog(title, message, ok, "Cancel");

        /// <summary>
        /// Records the assets a link, an unlink or a template change is about to write, so one Undo takes the change back.
        /// These wrote straight into the assets with no Undo (ED-12).
        /// </summary>
        protected static void RecordLinkChange(string undoName, IEnumerable<Object> touched)
        {
            var assets = touched.Where(asset => asset != null).Distinct().ToArray();
            if (assets.Length > 0) Undo.RecordObjects(assets, undoName);
        }

        /// <inheritdoc cref="RecordLinkChange(string, IEnumerable{Object})"/>
        protected static void RecordLinkChange(string undoName, params Object[] touched) =>
            RecordLinkChange(undoName, (IEnumerable<Object>)touched);

        // A multiple selection shows its count in the header, and only the actions that act on every selected asset.
        // An inspector checks it too, to set aside what acts on one asset (ED-12)
        protected bool IsMultiple => serializedObject.isEditingMultipleObjects;
        private string HeaderName() => IsMultiple ? $"{targets.Length} selected" : GetDisplayName();

        /// <summary>
        /// A text field bound to a property, which writes when an edit is committed rather than on each keystroke
        /// (ED-12). Binding sends the edit through the SerializedObject, so Undo takes it back, it reaches every selected
        /// asset, and a selection that disagrees shows as mixed.
        /// </summary>
        protected static TextField CreateBoundTextField(string propertyPath, string label, bool multiline = false, int minHeight = 0,
            string name = null)
        {
            var field = CreateTextField(name ?? propertyPath, label, multiline, minHeight);
            field.isDelayed = true;
            field.bindingPath = propertyPath;
            // Its label takes the Inspector's one label column, as a PropertyField's does (Docs/EDITOR_STYLE.md)
            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            return field;
        }

        /// <summary>For programmatic editors, this can be empty</summary>
        protected virtual void SetupCollapsibleSections() { }

        // ═══════════════════════════════════════════════════════════════════════════
        // Import: the one Import window, for the whole asset (ED-32, ED-35)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Opens the Import window for the whole asset: choose a source, then the fields that differ. The asset keeps its
        /// name, description, icons, asset tag, template and level link (ED-35). A menu of templates opened, and a dialog
        /// asked before an import that copied every field, the template and the link among them, and showed an error when
        /// no template existed though other assets did.
        /// </summary>
        protected void OpenImportPicker() => ForgeImportWindow.ShowForAsset(serializedObject, Refresh);

        // The asset's template, picked from Unity's searchable dropdown of the other assets of its type, under the asset
        // menu (ED-61). It was the Asset Browser, a window of its own with type badges and an Assign button on each row
        protected void ShowTemplatePicker(Rect under)
        {
            if (target is not BaseForgeAsset asset) return;
            var templates = TemplatesFor(asset).Select(each => ((Object)each.Asset, each.Name)).ToList();
            new ForgeAssetPicker(templates, "Template", "No other assets of this type", picked =>
            {
                if (picked is BaseForgeAsset template) AssignTemplate(template);
            }).Show(under);
        }

        /// <summary>
        /// The assets an asset can take as its template: the others of its type, but those that take it as theirs, or a
        /// template of a template that does, which would make a loop, by name. Two of one name add their file's.
        /// </summary>
        public static List<(BaseForgeAsset Asset, string Name)> TemplatesFor(BaseForgeAsset asset)
        {
            if (asset == null) return new List<(BaseForgeAsset, string)>();
            var type = asset.GetType();
            // The active Forge Project's alone (FEAT-34)
            var inProject = ForgeProjects.InActive();
            var candidates = AssetDatabase.FindAssets($"t:{type.Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BaseForgeAsset>)
                .Where(each => each != null && each.GetType() == type && each != asset && !TakesFrom(each, asset) && inProject(each))
                .Distinct()
                .ToList();
            string NameOf(BaseForgeAsset each)
            {
                var name = each.GetName();
                return string.IsNullOrEmpty(name) ? each.name : name;
            }
            var repeated = new HashSet<string>(candidates.GroupBy(NameOf).Where(group => group.Count() > 1).Select(group => group.Key));
            return candidates
                .Select(each => (each, repeated.Contains(NameOf(each)) ? $"{NameOf(each)} ({each.name})" : NameOf(each)))
                .OrderBy(each => each.Item2, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        // Whether an asset takes another as its template, through any templates between them
        private static bool TakesFrom(BaseForgeAsset asset, BaseForgeAsset ancestor)
        {
            var seen = new HashSet<BaseForgeAsset>();
            for (var each = asset.Template; each != null && seen.Add(each); each = each.Template)
                if (each == ancestor) return true;
            return false;
        }

        /// <summary>Gives the asset a template, or takes it away with null. The template picker's two actions call it.</summary>
        internal void AssignTemplate(BaseForgeAsset template)
        {
            if (target is not BaseForgeAsset asset) return;
            RecordLinkChange(template != null ? "Assign Template" : "Unassign Template", asset);
            asset.SetTemplate(template);
            MarkDirty(asset);
            Repaint();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // UIElements Header
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Override Unity's default header to hide it - we'll use UIElements instead.
        /// </summary>
        protected override void OnHeaderGUI()
        {
            // Empty - we use UIElements header instead
        }

        /// <summary>
        /// Creates the inspector: the header, then the content.
        /// Derived classes should override BuildInspectorContent instead of this.
        /// </summary>
        public override VisualElement CreateInspectorGUI()
        {
            sections.Clear();
            root = CreateRoot();
            // PlayForge's style sheet, in the editor's theme (ED-20)
            ForgeTheme.Apply(root);

            if (IsMultiple && !SupportsMultipleObjects)
                return CreateIneligibleGUI("Cannot edit multiple assets at once.");
            if (!AssignLocalAsset())
                return CreateIneligibleGUI("Critical error: Could not assign local asset.");

            // The header stays at the top of the Inspector while the content scrolls under it (Decision 29). It comes
            // after the content, so it draws over it, and a space holds its place at the top. It follows the Inspector's
            // own scroll view by its events (ED-36): it used to hide that scroll view and size one of its own by
            // measuring the window on timers that ran for as long as the inspector was open (ED-11)
            var header = CreateEditorHeader();
            headerContainer = header;
            var headerSpace = new VisualElement { name = "AssetHeaderSpace" };
            headerSpace.AddToClassList("pf-asset-header-space");
            root.Add(headerSpace);

            var content = new VisualElement { name = "Content" };
            content.style.marginRight = 4;
            root.Add(content);

            // Let derived classes build their content
            BuildInspectorContent(content);

            content.Add(CreateBottomPadding());

            root.Add(header);
            header.StickTo(headerSpace);

            root.Bind(serializedObject);

            // The header, and any section drawn from the asset's values rather than bound, follow every change to the
            // asset, an Undo or a Redo among them (ED-12), and so do the marks on the fields it overrides of its template
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                UpdateHeader();
                OnAssetChanged();
                ShowOverrides();
            });
            root.schedule.Execute(ShowOverrides);

            return root;
        }
        
        protected abstract bool AssignLocalAsset();
        
        protected abstract void BuildInspectorContent(VisualElement parent);

        private VisualElement CreateIneligibleGUI(string reason)
        {
            var message = new Label(reason)
            {
                style =
                {
                    alignSelf = Align.Center,
                }
            };

            root.Add(message);
            return root;
        }
        
        /// <summary>
        /// Creates the header from the kit: the type icon, the asset's name, its type and asset tag, then Open in The
        /// Forge, Help and the asset menu (Decision 20). The other actions are in the menu. The header took the type's
        /// color as a thick border and badge, showed the description, and set its own dark colors (ED-20).
        /// </summary>
        private ForgeAssetHeader CreateEditorHeader()
        {
            var header = new ForgeAssetHeader(target.GetType());
            _header = header;
            _headerNameLabel = header.NameLabel;
            _headerSubtitleLabel = header.Subtitle;
            UpdateHeader();
            // Its texture follows its Textures as they're edited (ED-69)
            header.TrackSerializedObjectValue(serializedObject, _ => UpdateHeaderIcon());

            // How many problems the validator finds in the selection, which follows it and opens the Problems view on the
            // asset (Q8, ED-15)
            var problems = header.AddProblemCount(() => ForgeProblemsWindow.ShowFor(target));
            void ShowProblems() => header.ShowProblems(targets.Where(each => each != null).SelectMany(each => ForgeValidator.ProblemsOf(each)).ToList());
            problems.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                ForgeValidator.Changed += ShowProblems;
                ShowProblems();
            });
            problems.RegisterCallback<DetachFromPanelEvent>(_ => ForgeValidator.Changed -= ShowProblems);

            // Open in The Forge and the template act on one asset, so a multiple selection leaves them out (ED-12). In The
            // Forge's own detail pane, the button shows the asset in the Project window instead (Docs/EDITOR_STYLE.md)
            if (!IsMultiple)
            {
                var open = header.AddButton("Open in The Forge", "Show this asset in The Forge", OnOpenInManager);
                open.name = "OpenInForge";
                // There, the asset's file name sits in a row of its own under the actions, as The Forge's list shows its
                // name alone (ED-58, ED-65)
                var file = header.AddLine("HeaderFile");
                open.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    if (!InForgeDetail(open)) return;
                    open.text = "Show in Project";
                    open.tooltip = "Show this asset in the Project window";
                    open.clickable = new Clickable(() =>
                    {
                        Selection.activeObject = target;
                        EditorGUIUtility.PingObject(target);
                    });
                    ForgeAssetHeader.ShowLine(file, target != null ? target.name : "", target != null ? AssetDatabase.GetAssetPath(target) : "");
                });
            }

            var topic = GetDocumentationTopic();
            if (!string.IsNullOrEmpty(topic)) header.AddIconButton(ForgeGlyphShape.Help, "Open the documentation", () => ForgeDocs.Open(topic)).name = "HeaderHelp";

            Button menuButton = null;
            menuButton = header.AddIconButton(ForgeGlyphShape.Menu, "Asset menu", () => ShowAssetMenu(menuButton));
            menuButton.name = "HeaderMenu";
            return header;
        }

        // The asset's other actions: import, and its template and the push to its children; then what a right-click on its
        // row in The Forge's list offers, one menu for both (ED-71)
        private void ShowAssetMenu(VisualElement under) => AssetMenu(under).DropDown(under.worldBound);

        /// <summary>The asset menu, at the header's three dots, as <see cref="ShowAssetMenu"/> opens it.</summary>
        internal GenericMenu AssetMenu(VisualElement under)
        {
            var menu = new GenericMenu();
            if (ShowImportButton) menu.AddItem(new GUIContent("Import from Another Asset..."), false, OpenImportPicker);
            if (ShowTemplateButton && !IsMultiple)
            {
                menu.AddItem(new GUIContent("Set Template..."), false, () => ShowTemplatePicker(under.worldBound));
                if (target is BaseForgeAsset asset && asset.HasTemplate)
                    menu.AddItem(new GUIContent($"Unassign Template \"{asset.Template.GetName()}\""), false, () => AssignTemplate(null));
                // A template's changes reach its children when it's pushed, as one Undo step. Saving it asked in a dialog,
                // mid-save, and changed them with no Undo (Q23, ED-13)
                if (target is BaseForgeAsset template)
                {
                    var children = ForgeTemplatePush.ChildrenOf(template);
                    if (children.Count > 0)
                        menu.AddItem(new GUIContent(children.Count == 1 ? "Push to Its Child" : $"Push to Its {children.Count} Children"), false,
                            () => ForgeTemplatePush.PushToChildren(template));
                    else menu.AddDisabledItem(new GUIContent("Push to Children"));
                }
            }
            // Show in Project, Open in a Window, Duplicate, Move or Copy to Project and Move to Trash, as The Forge's list
            // offers them on a row (ED-71)
            var assets = targets.OfType<ScriptableObject>().Where(AssetDatabase.Contains).ToList();
            if (menu.GetItemCount() > 0) menu.AddSeparator("");
            ForgeAssetActions.AddTo(menu, ForgeAssetActions.For(assets, ForgeAssetActions.ShowCopy));
            return menu;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Update Methods - Call these when header data changes
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Updates the header name label. Call when Name field changes.
        /// </summary>
        protected void UpdateHeaderName()
        {
            if (_headerNameLabel != null)
            {
                _headerNameLabel.text = HeaderName();
            }
        }

        /// <summary>
        /// Updates the line under the name: the asset's type, its asset tag, and its template, as in
        /// "Gameplay Effect · Effect.Burning". The header showed the description here; the Identity section has it.
        /// </summary>
        protected void UpdateHeaderDescription()
        {
            if (_headerSubtitleLabel == null) return;

            var parts = new List<string> { ForgeAssetTypes.NameOf(target.GetType()) };
            if (!IsMultiple)
            {
                if (target is BaseForgeLevelProvider provider)
                {
                    var tag = provider.GetAssetTag();
                    if (!string.IsNullOrEmpty(tag.Name) && !tag.Equals(Tags.NONE)) parts.Add(TagDisplaySettings.PathOf(tag));
                }
                if (target is BaseForgeAsset asset && asset.HasTemplate) parts.Add($"Template {asset.Template.GetName()}");
            }
            _headerSubtitleLabel.text = string.Join(" · ", parts);
        }

        // The header, whose icon shows the asset's texture where it has one (ED-69)
        private ForgeAssetHeader _header;

        /// <summary>
        /// Shows the asset's own texture in the header, in the type icon's place: the one under its Primary tag, else its
        /// first that has a texture (ED-69, the user's request); the type icon where it has none, or for a selection of
        /// several. The Forge's list rows keep the type's icon.
        /// </summary>
        protected void UpdateHeaderIcon()
        {
            if (_header == null) return;
            _header.ShowPicture(!IsMultiple && target is BaseForgeAsset asset ? asset.GetDefaultIcon() : null);
        }

        /// <summary>
        /// Updates all header elements. Call from Refresh() or when multiple fields change.
        /// </summary>
        protected void UpdateHeader()
        {
            UpdateHeaderName();
            UpdateHeaderDescription();
            UpdateHeaderIcon();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Tag Generation
        // ═══════════════════════════════════════════════════════════════════════════

        protected const string UnnamedAssetTag = "Unnamed";
        protected const string UnknownAssetName = "Error";
        
        // The tag a name gives, made by the one rule a reference field's New also uses (ED-38)
        protected (string result, string display, bool isUnknown) GenerateAssetTag(string assetName, string typeName) =>
            ForgeAssetCreation.AssetTagFor(assetName, typeName);

        private const string AssetTagUpdateName = "AssetTagUpdate";

        /// <summary>
        /// Shows the asset tag a name gives. When the stored tag differs, the label turns amber and an Update button
        /// appears, which calls sync. Showing never writes: each asset editor rewrote the tag every time it built or
        /// refreshed, with no Undo and no modified mark (ED-12).
        /// </summary>
        protected void ShowAssetTag(Label valueLabel, string assetName, string typeName, Tag stored, Action sync)
        {
            if (valueLabel == null) return;

            var (result, stale) = AssetTagState(assetName, typeName, stored);
            valueLabel.text = result;
            valueLabel.style.color = stale ? Colors.AccentYellow : Colors.AssetTagText;
            valueLabel.tooltip = stale ? $"Stored as \"{stored.Name}\". Update stores the tag this name gives." : null;

            var row = valueLabel.parent?.parent;
            if (row == null) return;
            var update = row.Q<Button>(AssetTagUpdateName);
            if (update == null)
            {
                update = new Button { name = AssetTagUpdateName, text = "Update", tooltip = "Store the tag this name gives" };
                update.style.marginLeft = 4;
                row.Add(update);
            }
            update.clickable = new Clickable(sync);
            update.style.display = stale ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Shows the asset tag a name gives in the kit's asset tag field. When the stored tag differs, the tag shows in
        /// warning text and Update calls sync. Showing never writes (ED-12).
        /// </summary>
        private protected void ShowAssetTag(ForgeAssetTagField field, string assetName, string typeName, Tag stored, Action sync)
        {
            if (field == null) return;
            var (result, stale) = AssetTagState(assetName, typeName, stored);
            field.Show(result, stale ? stored.Name : null, sync);
        }

        // The tag a name gives, and whether the stored one differs from it
        private (string tag, bool stale) AssetTagState(string assetName, string typeName, Tag stored)
        {
            var (result, display, isUnknown) = GenerateAssetTag(assetName, typeName);
            return (result, !isUnknown && (stored.Name != result || stored.DisplayName != display));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Summaries: the line a collapsed section shows
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A count in a summary, as in "None", "1 entry" or "3 entries".</summary>
        protected static string CountOf(int count, string one, string many) =>
            count == 0 ? "None" : count == 1 ? $"1 {one}" : $"{count} {many}";

        /// <summary>A level range in a summary, as in "Levels 1–4", or "Level 3" when it holds one level.</summary>
        protected static string LevelRange(int starting, int max) =>
            starting == max ? $"Level {starting}" : $"Levels {starting}–{max}";

        /// <summary>The name an enum field shows for a value: its InspectorName, or its own name spaced out.</summary>
        protected static string EnumName(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var shown = field != null ? (InspectorNameAttribute)System.Attribute.GetCustomAttribute(field, typeof(InspectorNameAttribute)) : null;
            return shown?.displayName ?? ObjectNames.NicifyVariableName(value.ToString());
        }

        /// <summary>An asset's PlayForge name, or its file name when it has none, as Used By names it.</summary>
        protected static string NameOf(BaseForgeAsset asset) =>
            asset == null ? "None" : string.IsNullOrEmpty(asset.GetName()) ? asset.name : asset.GetName();

        /// <summary>
        /// Tags' summary: up to two tags by name, or how many there are, then how many tags the asset grants, as in
        /// "Weapon.Sword · 1 granted".
        /// </summary>
        protected static string TagsSummary(IEnumerable<Tag> named, string kind, int granted)
        {
            var names = named?.Where(tag => tag.IsValid).Select(TagDisplaySettings.PathOf).ToList() ?? new List<string>();
            var parts = new List<string>();
            if (names.Count is 1 or 2) parts.Add(string.Join(", ", names));
            else if (names.Count > 2) parts.Add($"{names.Count} {kind}");
            if (granted > 0) parts.Add($"{granted} granted");
            return parts.Count == 0 ? "None" : string.Join(" · ", parts);
        }

        /// <summary>
        /// A Workers section's line: its On Hit entries, then its workers, as in "None", "1 on hit" or "1 on hit, 2
        /// workers" (TW-6).
        /// </summary>
        protected static string WorkersLine(List<OnHitEntry> onHit, int workers) => WorkersLine(onHit, null, workers);

        /// <summary>
        /// A Workers section's line with its modifiers among it: "1 on hit, 2 modifiers, 1 worker", or "None" (TW-7).
        /// </summary>
        protected static string WorkersLine(List<OnHitEntry> onHit, List<EffectModifierEntry> modifiers, int workers)
        {
            var parts = new List<string>();
            int hits = onHit?.Count(entry => entry != null) ?? 0;
            int changes = modifiers?.Count(entry => entry != null) ?? 0;
            if (hits > 0) parts.Add(CountOf(hits, "on hit", "on hits"));
            if (changes > 0) parts.Add(CountOf(changes, "modifier", "modifiers"));
            if (workers > 0 || parts.Count == 0) parts.Add(CountOf(workers, "worker", "workers"));
            return string.Join(", ", parts);
        }

        /// <summary>How many workers a group holds, across its four lists.</summary>
        protected static int WorkerCount(StandardWorkerGroup group) => group == null
            ? 0
            : (group.AttributeWorkers?.Count(worker => worker != null) ?? 0) + (group.ImpactWorkers?.Count(worker => worker != null) ?? 0) +
              (group.TagWorkers?.Count(worker => worker != null) ?? 0) + (group.AnalysisWorkers?.Count(worker => worker != null) ?? 0);

        // ═══════════════════════════════════════════════════════════════════════════
        // Leveling, in the redesigned inspectors: Level Source's link, in Leveling (ED-37)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Leveling while Link Mode is Linked To Provider: the provider in an object field, a warning when none is set,
        /// then the range in use, in disabled fields. Level Source drew this in tinted boxes with colored borders. An
        /// asset with no levels of its own, as an effect, says what it uses instead (<paramref name="whenUnset"/>).
        /// </summary>
        protected void AddLinkedLevels(VisualElement parent, BaseForgeLevelProvider src, string kind, int starting, int max,
            string whenUnset = null)
        {
            parent.Add(CreateLevelProviderField(src));
            if (!src.IsLinked)
                parent.Add(new HelpBox(whenUnset ?? $"No level provider is set, so the {kind} uses its own levels.", HelpBoxMessageType.Warning));
            parent.Add(ShownLevel("StartingLevel", "Starting Level", starting));
            parent.Add(ShownLevel("MaxLevel", "Max Level", max));
        }

        // A level the asset uses but doesn't set here, in a disabled field
        private static IntegerField ShownLevel(string name, string label, int value)
        {
            var field = new IntegerField(label) { name = name };
            field.SetValueWithoutNotify(value);
            field.AddToClassList(IntegerField.alignedFieldUssClassName);
            field.SetEnabled(false);
            return field;
        }

        /// <summary>Leveling's summary: the range in use, as in "Levels 1–4", and for a linked asset where it comes from.</summary>
        protected static string LevelingSummary(BaseForgeLevelProvider src, bool linkedMode, int starting, int max)
        {
            var range = LevelRange(starting, max);
            if (!linkedMode) return range;
            return src.IsLinked ? $"{range} · from {NameOf(src.LinkedProvider)}" : $"{range} · no provider";
        }

        /// <summary>
        /// What Leveling is drawn from: the link mode, and while linked, the provider and the range it gives. Leveling
        /// redraws when this changes, but not when the asset's own range does, which its bound fields follow.
        /// </summary>
        protected static string LevelingState(BaseForgeLevelProvider src, bool linkedMode, int starting, int max) =>
            !linkedMode ? "own" : $"linked {(src.LinkedProvider != null ? src.LinkedProvider.GetInstanceID() : 0)} {starting} {max}";

        /// <summary>
        /// Link and Unlink for the assets that can take this one's levels, such as an item's active ability and granted
        /// effects: plain buttons sharing a row. Each asks first, counting what it changes, and is one Undo step. They sat
        /// under a divider and a small bold heading, with a sentence about them, and Unlink was tinted red (ED-37). The
        /// children link to <paramref name="linkTarget"/> when it's given, as an effect's contained effects take its
        /// provider's levels, and to the asset itself otherwise.
        /// </summary>
        protected VisualElement CreateChildLinks(BaseForgeLevelProvider src, string kind, string linkFocus, string children,
            Func<IEnumerable<BaseForgeLevelProvider>> childAssets, Action linkAll, Action unlinkAll,
            BaseForgeLevelProvider linkTarget = null)
        {
            var row = new VisualElement { name = "ChildLinks" };
            row.AddToClassList("pf-button-row");

            row.Add(new Button(() =>
            {
                var count = childAssets().Count();
                if (!ConfirmLinkChange($"Confirm Link {linkFocus}",
                        $"Are you sure you want to link {children}?\n\nThis will affect {count} {(count == 1 ? "asset" : "assets")}.",
                        "Yes")) return;
                ChangeChildLinks($"Link {linkFocus}", src, childAssets, linkAll);
            })
            {
                text = $"Link {linkFocus}",
                tooltip = $"Link the {children} to this {kind}, so they take its levels",
            });

            row.Add(new Button(() =>
            {
                // Counts the children the unlink would change; it asked IsLinkedTo(this), the editor, so it always said 0 (ED-31)
                var linked = childAssets().Count(child => child.IsLinkedTo(linkTarget != null ? linkTarget : src));
                if (!ConfirmLinkChange($"Confirm Unlink {linkFocus}",
                        $"Are you sure you want to unlink {children}?\n\nThis will affect {linked} {(linked == 1 ? "asset" : "assets")}." +
                        $"\n\nThis change only applies to assets linked to this {kind}.",
                        "Yes")) return;
                ChangeChildLinks($"Unlink {linkFocus}", src, childAssets, unlinkAll);
            })
            {
                text = $"Unlink {linkFocus}",
                tooltip = $"Remove the links the {children} have to this {kind}",
            });

            return row;
        }

        // Records every asset a link change writes, so one Undo takes it back (ED-12), then makes the change
        private void ChangeChildLinks(string undoName, BaseForgeLevelProvider src, Func<IEnumerable<BaseForgeLevelProvider>> childAssets,
            Action change)
        {
            RecordLinkChange(undoName, childAssets().Cast<Object>().Append(src));
            change();
            foreach (var child in childAssets()) MarkDirty(child);
            MarkDirty(src);
            RebuildLevelingContent();
            Repaint();
        }

        /// <summary>
        /// Stores the tag a name gives, recorded for Undo and marked modified. A rename calls it in the same step as the
        /// name's own change, so one Undo takes back both (ED-12).
        /// </summary>
        protected void StoreAssetTag(Object asset, string assetName, string typeName, Tag stored, Action<Tag> store)
        {
            var (result, display, isUnknown) = GenerateAssetTag(assetName, typeName);
            if (isUnknown || (stored.Name == result && stored.DisplayName == display)) return;

            Undo.RecordObject(asset, "Update Asset Tag");
            var from = stored.Name;
            stored.Name = result;
            stored.DisplayName = display;
            store(stored);
            EditorUtility.SetDirty(asset);
            // What stored its old tag stores the new one, or it finds the asset no more (ED-84)
            ForgeTagRenames.Rename(from, result, display, asset);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Utility Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected void MarkDirty(Object obj)
        {
            if (obj is null) return;
            EditorUtility.SetDirty(obj);
        }
        
        protected void PingAsset(Object obj)
        {
            EditorGUIUtility.PingObject(obj);
        }
        
        protected string GetAssetGuid(Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            return AssetDatabase.AssetPathToGUID(path);
        }
        
        /// <summary>
        /// Truncates text to max length with ellipsis
        /// </summary>
        protected string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Legacy UXML Support
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected void SetupCollapsibleSection(string sectionName, bool startExpanded = true)
        {
            var header = root.Q($"{sectionName}Header");
            var content = root.Q(sectionName);
            var arrow = root.Q<Label>($"{sectionName}Arrow");
            
            if (header == null || content == null) return;
            
            bool isExpanded = startExpanded;
            
            content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (arrow != null)
            {
                arrow.text = isExpanded ? Icons.ChevronDown : Icons.ChevronRight;
            }
            
            header.RegisterCallback<MouseEnterEvent>(_ =>
            {
                header.style.backgroundColor = Colors.SectionHeaderHover;
            });
            
            header.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                header.style.backgroundColor = Colors.SectionHeaderBackground;
            });
            
            header.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                
                isExpanded = !isExpanded;
                content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                
                if (arrow != null)
                {
                    arrow.text = isExpanded ? Icons.ChevronDown : Icons.ChevronRight;
                }
                
                evt.StopPropagation();
            });
        }
        
        protected PropertyField BindPropertyField(VisualElement container, string fieldName, string propertyPath, string fieldLabel = "")
        {
            var field = container.Q<PropertyField>(fieldName);
            if (field != null && !string.IsNullOrEmpty(fieldLabel))
            {
                field.label = fieldLabel;
            }
            
            var prop = serializedObject.FindProperty(propertyPath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
            return field;
        }
        
        protected PropertyField BindPropertyField(VisualElement container, string fieldName, SerializedProperty parent, string relativePath, string fieldLabel = "")
        {
            var field = container.Q<PropertyField>(fieldName);
            if (field != null && !string.IsNullOrEmpty(fieldLabel))
            {
                field.label = fieldLabel;
            }
            
            var prop = parent.FindPropertyRelative(relativePath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
            return field;
        }
        
        protected void SetupHelpButton(string buttonName, string url)
        {
            var btn = root.Q<Button>(buttonName);
            if (btn != null)
            {
                btn.clicked += () => Application.OpenURL(url);
            }
        }
        
        protected void ApplyButtonHoverStyle(Button btn)
        {
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
        }
        
        protected void BuildLinkToButton<T>(VisualElement parent, BaseForgeLevelProvider src) where T : BaseForgeLevelProvider
        {
            // Link to Entity button
            var linkAssetButton = new Button(() => PickProvider<T>(src));
            linkAssetButton.text = GetProviderTypeName(typeof(T));
            linkAssetButton.tooltip = $"Link to an {GetProviderTypeName(typeof(T))} to use its level range";
            linkAssetButton.style.marginRight = 4;
            ApplyButtonHoverStyle(linkAssetButton);
            
            parent.Add(linkAssetButton);
        }

        // Opens the object picker on one type of level provider, and links the asset to the one chosen
        private void PickProvider<T>(BaseForgeLevelProvider src) where T : BaseForgeLevelProvider
        {
            _pickerControlId = GUIUtility.GetControlID(FocusType.Passive);
            _waitingForPicker = true;
            _pickerType = typeof(T);
            _pickerCallback = obj =>
            {
                if (obj is T asset) LinkTo(src, asset);
            };
            ShowProviderPicker<T>(src);
        }

        /// <summary>
        /// Links an asset to the level provider the picker chose, unless the link would make a loop. Returns whether it
        /// linked.
        /// </summary>
        internal bool LinkTo(BaseForgeLevelProvider src, BaseForgeLevelProvider provider)
        {
            if (src.HasCircularDependency(provider, out var issue, out var chain))
            {
                Debug.LogError($"[PlayForge] Cannot link {src.GetProviderName()} to {provider.GetProviderName()} due to a circular dependency. " +
                               $"\nRoot Issue: {issue.GetProviderName()} is linked to {src.GetProviderName()}." +
                               $"\nLink Chain: {chain}." +
                               $"\n");
                return false;
            }

            RecordLinkChange($"Link to {provider.GetProviderName()}", src);
            src.LinkToProvider(provider);
            MarkDirty(src);
            RebuildLevelSourceContent();
            RebuildLevelingContent();
            return true;
        }

        /// <summary>
        /// The level provider a linked asset takes its levels from, in Unity's object field, with its label in the
        /// Inspector's label column. Choosing a provider links the asset, as one Undo step, unless the link would make a
        /// loop; clearing the field leaves the asset linked to nothing. The field's right-click menu picks a provider of
        /// one type (Docs/EDITOR_STYLE.md: the link filter goes in its right-click menu). The provider showed as a name in
        /// a tinted box, under a row of Link to buttons, one per type (ED-37).
        /// </summary>
        protected ObjectField CreateLevelProviderField(BaseForgeLevelProvider src)
        {
            var field = new ObjectField("Level Provider")
            {
                name = "LevelProvider",
                objectType = typeof(BaseForgeLevelProvider),
                allowSceneObjects = false,
                tooltip = "The asset this one takes its levels from",
            };
            field.SetValueWithoutNotify(src.LinkedProvider);
            field.AddToClassList(ObjectField.alignedFieldUssClassName);
            field.RegisterValueChangedCallback(change =>
            {
                var provider = change.newValue as BaseForgeLevelProvider;
                if (provider == src.LinkedProvider) return;
                if (provider == null)
                {
                    RecordLinkChange("Clear Level Provider", src);
                    src.LinkedProvider = null;
                    MarkDirty(src);
                    RebuildLevelSourceContent();
                    RebuildLevelingContent();
                    return;
                }
                if (!LinkTo(src, provider)) field.SetValueWithoutNotify(src.LinkedProvider);
            });
            field.AddManipulator(new ContextualMenuManipulator(menu =>
            {
                menu.menu.AppendAction("Pick an Entity", _ => PickProvider<EntityIdentity>(src));
                menu.menu.AppendAction("Pick an Ability", _ => PickProvider<Ability>(src));
                menu.menu.AppendAction("Pick an Effect", _ => PickProvider<GameplayEffect>(src));
                menu.menu.AppendAction("Pick an Item", _ => PickProvider<Item>(src));
                menu.menu.AppendAction("Pick an Attribute Set", _ => PickProvider<AttributeSet>(src));
            }));
            return field;
        }
        
        protected void ShowProviderPicker<T>(BaseForgeLevelProvider src) where T : BaseForgeLevelProvider
        {
            // Generate a unique control ID
            _pickerControlId = GUIUtility.GetControlID(FocusType.Passive) + 10000 + Random.Range(1, 1000);
            WaitForPicker();
            _lastPickedObject = src.LinkedProvider; // Start with current value
            EditorGUIUtility.ShowObjectPicker<T>(src.LinkedProvider as T, false, "", _pickerControlId);
        }

        protected abstract void RebuildLevelSourceContent();
        
        protected void BuildProviderSelector(VisualElement levelSourceContent, BaseForgeLevelProvider src)
        {
            var selectorRow = new VisualElement();
            selectorRow.style.flexDirection = FlexDirection.Row;
            selectorRow.style.alignItems = Align.Center;
            selectorRow.style.marginBottom = 6;
            
            var label = new Label("Level Provider");
            label.style.width = 125;
            label.style.color = Colors.LabelText;
            selectorRow.Add(label);
            
            var (assetTagContainer, valueLabel) = CreateAssetTagDisplay();
            
            assetTagContainer.style.marginBottom = 2;
            assetTagContainer.Remove(assetTagContainer.Q<Label>("Label"));
            assetTagContainer.style.flexGrow = 1f;
            
            selectorRow.Add(assetTagContainer);
            
            valueLabel.text = src.IsLinked ? src.LinkedProvider.GetProviderName() : "No Provider Linked";
            if (src.IsLinked) valueLabel.text = src.LinkedProvider.GetProviderName();
            else
            {
                valueLabel.text = "No Provider Linked";
                valueLabel.style.color = Colors.AccentRed;
            }
            
            levelSourceContent.Add(selectorRow);
            
            var linkButtonsContainer = new VisualElement { name = "linking-buttons" };
            linkButtonsContainer.style.flexDirection = FlexDirection.Row;
            linkButtonsContainer.style.marginTop = 4;
            levelSourceContent.Add(linkButtonsContainer);
                
            var linkLabel = new Label("Link to:");
            linkLabel.style.width = 125;
            linkLabel.style.color = Colors.LabelText;
            linkLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            linkButtonsContainer.Add(linkLabel);

            BuildLinkToButton<EntityIdentity>(linkButtonsContainer, src);
            BuildLinkToButton<Ability>(linkButtonsContainer, src);
            BuildLinkToButton<GameplayEffect>(linkButtonsContainer, src);
            BuildLinkToButton<Item>(linkButtonsContainer, src);
            BuildLinkToButton<AttributeSet>(linkButtonsContainer, src);
        }

        protected abstract void RebuildLevelingContent();
        
    }
}