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
    /// Both scroll together in the Inspector's own scroll view (Q17).
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
        }

        protected virtual void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _waitingForPicker = false;
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
        
        /// <summary>Called when Visualize button is clicked. Opens PlayForgeVisualizer by default.</summary>
        protected void OnVisualize()
        {
            var window = EditorWindow.GetWindow<PlayForgeVisualizer>("Visualizer");
            window.SetAsset(target as ScriptableObject);
            window.Show();
        }
        
        /// <summary>Called when Open button is clicked. Opens PlayForge Manager to this asset.</summary>
        protected virtual void OnOpenInManager()
        {
            TheForge.OpenToAsset(target);
        }
        
        /// <summary>Whether to show the Visualize button</summary>
        protected virtual bool ShowVisualizeButton => false;
        
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
            return field;
        }

        /// <summary>For programmatic editors, this can be empty</summary>
        protected virtual void SetupCollapsibleSections() { }

        // ═══════════════════════════════════════════════════════════════════════════
        // Import Functionality - Generic implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Opens an object picker to import properties from another asset of the same type.
        /// </summary>
        /// <summary>
        /// Opens a dropdown menu to import from templates or pick any asset.
        /// </summary>
        protected void OpenImportPicker()
        {
            var assetType = target.GetType();
            var templates = TemplateRegistry.GetTemplates(assetType).Where(t => t.IsValid() && t.IsNotTarget(target)).ToList();
            
            // Section: Templates (if any exist)
            if (templates.Count > 0)
            {
                var menu = new GenericMenu();
                menu.AddDisabledItem(new GUIContent("Templates"));
                
                foreach (var template in templates)
                {
                    var asset = template.GetAsset();
                    if (asset == null) continue;
                    
                    var displayName = GetTemplateDisplayName(template, asset);
                    menu.AddItem(new GUIContent(displayName), false, () =>
                    {
                        ImportFromAsset(asset);
                        Refresh();
                    });
                }
                
                menu.AddSeparator("");
                
                // Section: Browse for any asset
                menu.AddItem(new GUIContent("Browse All Assets..."), false, () =>
                {
                    OpenAssetPicker(assetType);
                });
                
                menu.ShowAsContext();
            }
            else Debug.LogError($"[ PlayForge ] No importable assets found.");
        }

        protected void ShowTemplatePicker()
        {
            var assetType = target.GetType();
            var windowConfig = AssetBrowserConfig.Create("Choose Template")
                .ForTypes(assetType)
                .WithSubtitle("Set asset template.")
                .Excluding(target)
                .WithDisplayName(so =>
                {
                    var bfo = so as BaseForgeAsset;
                    return bfo?.GetName() ?? assetType.Name;
                })
                .WithAction("Assign", so =>
                {
                    var sfo = so as BaseForgeAsset;
                    AssignTemplate(sfo);
                    Debug.Log($"[ PlayForge ] Template \"{sfo.GetName()}\" assigned.");
                }, closeOnClick: true)
                .WithTypeBadges();
            if (target is BaseForgeAsset _bfo && _bfo.HasTemplate)
            {
                string template = _bfo.Template.GetName();
                windowConfig.WithFooterAction($"Unnasign Template \"{template}\"", () => AssignTemplate(null));
            }

            AssetBrowserWindow.Show(windowConfig);
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

        private string GetTemplateDisplayName(AssetTemplate template, ScriptableObject asset)
        {
            var name = GetAssetDisplayName(asset);
            if (string.IsNullOrEmpty(name) || name == asset.GetType().Name)
                name = asset.name;
            
            // Add tag indicator if present
            if (!string.IsNullOrEmpty(template.TemplateTagName))
            {
                var tag = TemplateRegistry.GetTag(template.TemplateTagName);
                if (tag != null)
                    name = $"[{tag.Name}] {name}";
            }
            
            // Add default indicator
            if (template.IsDefault)
                name = $"★ {name}";
            
            return name;
        }

        /// <summary>
        /// Opens the standard Unity object picker for any asset of the given type.
        /// </summary>
        private void OpenAssetPicker(Type assetType)
        {
            _pickerControlId = GUIUtility.GetControlID(FocusType.Passive) + 10000 + Random.Range(1, 1000);
            WaitForPicker();
            _lastPickedObject = null;
            _pickerCallback = sourceAsset =>
            {
                if (sourceAsset != null && sourceAsset != target && sourceAsset.GetType() == assetType)
                {
                    ImportFromAsset(sourceAsset);
                    Refresh();
                }
            };
            
            EditorGUIUtility.ShowObjectPicker<ScriptableObject>(null, false, $"t:{assetType.Name}", _pickerControlId);
        }

        /// <summary>
        /// Helper to get display name from an asset (override in subclasses if needed).
        /// </summary>
        protected virtual string GetAssetDisplayName(ScriptableObject asset)
        {
            // Try common patterns
            if (asset is Ability ability)
                return ability.GetName() ?? ability.name;
            if (asset is GameplayEffect effect)
                return effect.GetName() ?? effect.name;
            if (asset is Item item)
                return item.GetName() ?? item.name;
            if (asset is EntityIdentity entity)
                return entity.GetName() ?? entity.name;
            if (asset is Attribute attr)
                return attr.GetName() ?? attr.name;
            if (asset is AttributeSet set)
                return set.GetName();
            
            return asset.name;
        }
        
        /// <summary>
        /// Imports properties from another asset using SerializedObject.
        /// Override this to customize import behavior.
        /// </summary>
        protected virtual void ImportFromAsset(Object sourceAsset)
        {
            if (sourceAsset == null || Array.IndexOf(targets, sourceAsset) >= 0) return;

            // The import writes through the SerializedObject, so it reaches every selected asset (ED-12)
            var into = IsMultiple ? $"{targets.Length} assets" : $"'{target.name}'";
            var confirmed = EditorUtility.DisplayDialog(
                "Import Properties",
                $"Import properties from '{sourceAsset.name}' to {into}?\n\nThis will overwrite current values (except for Name and Description).",
                "Import",
                "Cancel"
            );

            if (!confirmed) return;

            Undo.RecordObjects(targets, $"Import from {sourceAsset.name}");
            
            var sourceSO = new SerializedObject(sourceAsset);
            var targetSO = serializedObject;
            
            // Copy all serialized properties except m_Script, Name, and Description
            var sourceIterator = sourceSO.GetIterator();
            sourceIterator.NextVisible(true); // Enter the first property
            
            do
            {
                var path = sourceIterator.propertyPath;
                
                // Skip the script reference
                if (path == "m_Script") continue;
                
                // Skip Name and Description fields - check both the property name and path
                // This handles fields at root level AND nested in Definition
                if (ShouldSkipForImport(path)) continue;
                
                var targetProp = targetSO.FindProperty(path);
                if (targetProp != null)
                {
                    targetSO.CopyFromSerializedProperty(sourceIterator);
                }
            }
            while (sourceIterator.NextVisible(false));
            
            targetSO.ApplyModifiedProperties();
            
            MarkDirty(target);
            Refresh();
            
            Debug.Log($"[PlayForge] Imported properties from '{sourceAsset.name}' to '{target.name}'");
        }
        
        /// <summary>
        /// Determines if a property should be skipped during import.
        /// Override to customize which properties are excluded.
        /// </summary>
        protected virtual bool ShouldSkipForImport(string propertyPath)
        {
            // Skip Name fields (handles Definition.Name, Tags.Name, etc.)
            if (propertyPath == "Name" || propertyPath.EndsWith(".Name"))
                return true;
            
            // Skip Description fields
            if (propertyPath == "Description" || propertyPath.EndsWith(".Description"))
                return true;
            
            // Skip Textures/icons (part of visual identity)
            if (propertyPath == "Textures" || propertyPath.EndsWith(".Textures"))
                return true;
            if (propertyPath.StartsWith("Definition.Textures"))
                return true;
            
            return false;
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
            // asset, an Undo or a Redo among them (ED-12)
            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                UpdateHeader();
                OnAssetChanged();
            });

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
            _headerNameLabel = header.NameLabel;
            _headerSubtitleLabel = header.Subtitle;
            UpdateHeader();

            // Open in The Forge and the template act on one asset, so a multiple selection leaves them out (ED-12)
            if (!IsMultiple) header.AddButton("Open in The Forge", "Show this asset in The Forge", OnOpenInManager);

            var topic = GetDocumentationTopic();
            if (!string.IsNullOrEmpty(topic)) header.AddIconButton(ForgeGlyphShape.Help, "Open the documentation", () => ForgeDocs.Open(topic));

            Button menuButton = null;
            menuButton = header.AddIconButton(ForgeGlyphShape.Menu, "Asset menu", () => ShowAssetMenu(menuButton));
            return header;
        }

        // The asset's other actions: import, its template, and the Visualizer
        private void ShowAssetMenu(VisualElement under)
        {
            var menu = new GenericMenu();
            if (ShowImportButton) menu.AddItem(new GUIContent("Import from Another Asset..."), false, OpenImportPicker);
            if (ShowTemplateButton && !IsMultiple)
            {
                menu.AddItem(new GUIContent("Set Template..."), false, ShowTemplatePicker);
                if (target is BaseForgeAsset asset && asset.HasTemplate)
                    menu.AddItem(new GUIContent($"Unassign Template \"{asset.Template.GetName()}\""), false, () => AssignTemplate(null));
            }
            if (ShowVisualizeButton && !IsMultiple) menu.AddItem(new GUIContent("Visualize"), false, OnVisualize);
            if (menu.GetItemCount() == 0) menu.AddDisabledItem(new GUIContent("No Actions"));
            menu.DropDown(under.worldBound);
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
                    if (!string.IsNullOrEmpty(tag.Name) && !tag.Equals(Tags.NONE)) parts.Add(tag.Name);
                }
                if (target is BaseForgeAsset asset && asset.HasTemplate) parts.Add($"Template {asset.Template.GetName()}");
            }
            _headerSubtitleLabel.text = string.Join(" · ", parts);
        }

        /// <summary>
        /// The header shows the asset's type icon, which doesn't change, so there's nothing to update. It showed the
        /// asset's own icon texture.
        /// </summary>
        protected void UpdateHeaderIcon()
        {
        }

        /// <summary>
        /// Updates all header elements. Call from Refresh() or when multiple fields change.
        /// </summary>
        protected void UpdateHeader()
        {
            UpdateHeaderName();
            UpdateHeaderDescription();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Tag Generation
        // ═══════════════════════════════════════════════════════════════════════════

        protected const string UnnamedAssetTag = "Unnamed";
        protected const string UnknownAssetName = "Error";
        
        protected (string result, string display, bool isUnknown) GenerateAssetTag(string assetName, string typeName)
        {
            if (string.IsNullOrEmpty(assetName))
                return ($"{UnnamedAssetTag} {typeName}!", UnnamedAssetTag, true);
            
            // Remove special characters (keep only alphanumeric and spaces)
            string cleaned = Regex.Replace(assetName, @"[^a-zA-Z0-9\s]", "");
            
            // Split by spaces and capitalize each word (PascalCase)
            string[] words = cleaned.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + 
                               (words[i].Length > 1 ? words[i].Substring(1) : "");
                }
            }
            
            string result = string.Join("", words);
            result = $"{typeName}.{result}";
            
            // Ensure it starts with a letter
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = $"{typeName}." + result;
            }

            return string.IsNullOrEmpty(result) ? ($"{UnnamedAssetTag} {typeName}!", UnnamedAssetTag, true) : (result, string.Join("", words), false);
        }

        private const string AssetTagUpdateName = "AssetTagUpdate";

        /// <summary>
        /// Shows the asset tag a name gives. When the stored tag differs, the label turns amber and an Update button
        /// appears, which calls sync. Showing never writes: each asset editor rewrote the tag every time it built or
        /// refreshed, with no Undo and no modified mark (ED-12).
        /// </summary>
        protected void ShowAssetTag(Label valueLabel, string assetName, string typeName, Tag stored, Action sync)
        {
            if (valueLabel == null) return;

            var (result, display, isUnknown) = GenerateAssetTag(assetName, typeName);
            var stale = !isUnknown && (stored.Name != result || stored.DisplayName != display);
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
        /// Stores the tag a name gives, recorded for Undo and marked modified. A rename calls it in the same step as the
        /// name's own change, so one Undo takes back both (ED-12).
        /// </summary>
        protected void StoreAssetTag(Object asset, string assetName, string typeName, Tag stored, Action<Tag> store)
        {
            var (result, display, isUnknown) = GenerateAssetTag(assetName, typeName);
            if (isUnknown || (stored.Name == result && stored.DisplayName == display)) return;

            Undo.RecordObject(asset, "Update Asset Tag");
            stored.Name = result;
            stored.DisplayName = display;
            store(stored);
            EditorUtility.SetDirty(asset);
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
            var linkAssetButton = new Button(() =>
            {
                _pickerControlId = GUIUtility.GetControlID(FocusType.Passive);
                _waitingForPicker = true;
                _pickerType = typeof(T);
                _pickerCallback = obj =>
                {
                    if (obj is T asset) LinkTo(src, asset);
                };
                ShowProviderPicker<T>(src);
            });
            linkAssetButton.text = GetProviderTypeName(typeof(T));
            linkAssetButton.tooltip = $"Link to an {GetProviderTypeName(typeof(T))} to use its level range";
            linkAssetButton.style.marginRight = 4;
            ApplyButtonHoverStyle(linkAssetButton);
            
            parent.Add(linkAssetButton);
        }

        /// <summary>Links an asset to the level provider the picker chose, unless the link would make a loop.</summary>
        internal void LinkTo(BaseForgeLevelProvider src, BaseForgeLevelProvider provider)
        {
            if (src.HasCircularDependency(provider, out var issue, out var chain))
            {
                Debug.LogError($"[PlayForge] Cannot link {src.GetProviderName()} to {provider.GetProviderName()} due to a circular dependency. " +
                               $"\nRoot Issue: {issue.GetProviderName()} is linked to {src.GetProviderName()}." +
                               $"\nLink Chain: {chain}." +
                               $"\n");
                return;
            }

            RecordLinkChange($"Link to {provider.GetProviderName()}", src);
            src.LinkToProvider(provider);
            MarkDirty(src);
            RebuildLevelSourceContent();
            RebuildLevelingContent();
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