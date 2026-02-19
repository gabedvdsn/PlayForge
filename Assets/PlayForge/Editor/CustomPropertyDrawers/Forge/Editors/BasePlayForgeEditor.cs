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
    /// Base class for all PlayForge custom editors.
    /// Features a sticky UIElements header that stays at the top of the inspector
    /// and scrollable content below.
    /// </summary>
    public abstract class BasePlayForgeEditor : UnityEditor.Editor
    {
        protected VisualElement root;
        protected VisualElement headerContainer;
        protected ScrollView contentScrollView;
        
        // Header element references for live updates
        private Label _headerNameLabel;
        private Label _headerDescLabel;
        private VisualElement _headerIconContainer;
        
        protected int _pickerControlId;
        protected bool _waitingForPicker;
        protected Object _lastPickedObject;
        protected System.Type _pickerType;
        protected System.Action<Object> _pickerCallback;
        
        // Header configuration
        private const float HeaderHeight = 72f;
        private const float IconSize = 48f;
        private const float ButtonSize = 22f;
        private const float ButtonSpacing = 4f;
        private const float Padding = 8f;

        private static Dictionary<BaseForgeObject, Dictionary<string, bool>> _collapsedStates = new();

        protected static bool IsCollapsed(BaseForgeObject obj, string section)
        {
            return !(_collapsedStates.TryGetValue(obj, out var sections) && sections.TryGetValue(section, out bool collapsed)) || collapsed;
        }

        protected static void SetCollapsed(BaseForgeObject obj, string section, bool collapsed)
        {
            _collapsedStates.SafeAdd(obj, new Dictionary<string, bool>(), false);
            _collapsedStates[obj].SafeAdd(section, collapsed, true);
        }

        protected virtual void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
        }
        
        protected virtual void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            _waitingForPicker = false;
        }
        
        private void OnEditorUpdate()
        {
            if (!_waitingForPicker) return;
            
            var currentControlId = EditorGUIUtility.GetObjectPickerControlID();

            if (currentControlId == 0)
            {
                _waitingForPicker = false;
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

        protected abstract BaseForgeLinkProvider GetAsset();
        
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
        protected abstract string GetDocumentationUrl();

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
            PlayForgeManager.OpenToAsset(target);
        }
        
        /// <summary>Whether to show the Visualize button</summary>
        protected virtual bool ShowVisualizeButton => false;
        
        /// <summary>Whether to show the Import button</summary>
        protected virtual bool ShowImportButton => false;
        
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
            var templates = TemplateRegistry.GetTemplates(assetType).Where(t => t.IsValid()).ToList();
            
            var menu = new GenericMenu();
            
            // Section: Templates (if any exist)
            if (templates.Count > 0)
            {
                //menu.AddDisabledItem(new GUIContent("Templates"));
                
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
            }
            
            // Section: Browse for any asset
            menu.AddItem(new GUIContent("Browse All Assets..."), false, () =>
            {
                OpenAssetPicker(assetType);
            });
            
            menu.ShowAsContext();
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
            _waitingForPicker = true;
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
            if (sourceAsset == null || sourceAsset == target) return;
            
            var confirmed = EditorUtility.DisplayDialog(
                "Import Properties",
                $"Import properties from '{sourceAsset.name}' to '{target.name}'?\n\nThis will overwrite current values (except for Name and Description).",
                "Import",
                "Cancel"
            );
            
            if (!confirmed) return;
            
            Undo.RecordObject(target, $"Import from {sourceAsset.name}");
            
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
        // UIElements Header - Creates a sticky header that doesn't scroll
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Override Unity's default header to hide it - we'll use UIElements instead.
        /// </summary>
        protected override void OnHeaderGUI()
        {
            // Empty - we use UIElements header instead
        }
        
        /// <summary>
        /// Creates the root inspector GUI with sticky header.
        /// Derived classes should override BuildInspectorContent instead of this.
        /// </summary>
        public override VisualElement CreateInspectorGUI()
        {
            root = CreateRoot();
            
            if (serializedObject.isEditingMultipleObjects) 
                return CreateIneligibleGUI("Cannot edit multiple assets at once.");
            if (!AssignLocalAsset()) 
                return CreateIneligibleGUI("Critical error: Could not assign local asset.");
            
            // Create sticky header container (outside ScrollView)
            headerContainer = CreateEditorHeader();
            root.Add(headerContainer);
            
            // Create scrollable content area
            contentScrollView = CreateScrollView();
            root.Add(contentScrollView);

            //contentScrollView.style.backgroundColor = new StyleColor(new Color(.3f, .5f, .7f, .6f));
            
            // Let derived classes build their content
            BuildInspectorContent(contentScrollView);
            
            contentScrollView.Add(CreateBottomPadding());
            
            root.Bind(serializedObject);
            
            // Hook into panel attachment to disable Inspector's native ScrollView
            root.RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            root.RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);

            // Debug.Log($"[ Created ] {GetType().Name}");
            
            configureDelayMs = EditorPrefs.GetInt(PlayForgeManager.PREFS_PREFIX + "EditorRefreshDelayMs", DefaultConfigureDelayMsMin);
            forceConfigureDelayMs = EditorPrefs.GetInt(PlayForgeManager.PREFS_PREFIX + "EditorForceRefreshDelayMs", DefaultForceConfigureDelayMsMin);
            
            return root;
        }
        
        /// <summary>
        /// Called when our root is attached to the Inspector panel.
        /// We disable the native Inspector ScrollView to make our header sticky.
        /// </summary>
        private void OnAttachedToPanel(AttachToPanelEvent evt)
        {
            // Debug.Log($"\t[ Attached ] {GetType().Name}");
            // Only run once after layout is ready
            root.schedule.Execute(() =>
            {
                DisableInspectorScrollView();

                configure = true;
                ConfigureScrollView();
                ForceConfigureScrollView();
                
                root.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            }).ExecuteLater(50); // Slight delay to ensure layout is complete
        }
        
        private void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            // Debug.Log($"\t[ Detached ] {GetType().Name}");
            root.UnregisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
            root.UnregisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
            root.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        // Remove the GeometryChangedEvent entirely - it causes the recursive loop

        private bool configure = false;
        private int configureDelayMs = 251;
        private int forceConfigureDelayMs = 297;
        
        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            // Debug.Log($"[ {Mathf.Max(0f, evt.newRect.size.y - evt.oldRect.size.y) > 0f} ] Geom change: {evt.newRect.size.y} - {evt.oldRect.size.y} = {Mathf.Max(0f, evt.newRect.size.y - evt.oldRect.size.y)} ({evt.newRect.size.y - Mathf.Max(0f, evt.newRect.size.y - evt.oldRect.size.y)})");
            // Only reconfigure if the size actually changed
            if (Mathf.Max(0f, evt.newRect.size.y - evt.oldRect.size.y) > 0f)
            {
                configure = true;
            }
        }

        private void ForceConfigureScrollView()
        {
            root.schedule.Execute(() =>
            {
                configure = true;
                ConfigureScrollView(false);
                
                ForceConfigureScrollView();
            }).ExecuteLater(forceConfigureDelayMs); // Slight delay to ensure layout is complete
        }
        
        private void ConfigureScrollView(bool configureLater = true)
        {
           // Debug.Log($"\t\t[ Configure ] {configure}");
            
            if (root.panel == null || !configure)
            {
                if (configureLater)
                {
                    root.schedule.Execute(() =>
                    {
                        ConfigureScrollView();
                    }).ExecuteLater(configureDelayMs);
                }
                configure = false;
                return;
            } 
            
            configure = false;
    
            // Find the inspector's root container to get available height
            VisualElement inspectorRoot = root.parent;
            ScrollView scrollView = null;

            
            while (inspectorRoot?.parent != null)
            {
                if (!(inspectorRoot.parent is ScrollView sc))
                {
                    inspectorRoot = inspectorRoot.parent;
                }
                else
                {
                    scrollView = sc;
                    break;
                }
            }
           // Debug.Log($"[ Unity Scroll ] {(scrollView is null ? "Null" : $"Found: {scrollView.name}")}");
            inspectorRoot = inspectorRoot?.parent;
    
            // Use resolvedStyle to get actual rendered dimensions
            float availableHeight = inspectorRoot?.resolvedStyle.height ?? 0;

           // Debug.Log($"\t\tA) Available height: {availableHeight}");
    
            // If we can't get the inspector height, try getting it from panel
            if (availableHeight <= 0)
            {
                availableHeight = root.panel.visualTree.resolvedStyle.height;
            }
            
           // Debug.Log($"\t\tB) Available height: {availableHeight}");
    
            // Subtract header height and some padding
            float scrollHeight = availableHeight - HeaderHeight - 20f; // 20f for margins/padding
            
           // Debug.Log($"\t\tA) Scroll height: {scrollHeight}");

            scrollHeight = scrollHeight - 318 + 154 - 20;

            scrollHeight = scrollView?.resolvedStyle.height - HeaderHeight - 20 ?? scrollHeight;
            
           // Debug.Log($"\t\tB) Scroll height: {scrollHeight}");
            
            if (scrollHeight > 100) // Sanity check
            {
                contentScrollView.style.height = scrollHeight;
                contentScrollView.style.minHeight = scrollHeight;
                contentScrollView.style.maxHeight = scrollHeight;
            }

            if (configureLater)
            {
                root.schedule.Execute(() =>
                {
                    ConfigureScrollView();
                }).ExecuteLater(configureDelayMs);
            }
        }

        private void DisableInspectorScrollView()
        {
            if (root?.panel == null) return;
            
            VisualElement current = root.parent;
            while (current != null)
            {
                if (current is ScrollView inspectorScrollView)
                {
                    // Completely disable the inspector's scroll
                    inspectorScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                    inspectorScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                    
                    // Critical: make it not affect layout
                    inspectorScrollView.style.overflow = Overflow.Visible;
                    
                    // Let content determine its own size
                    var contentContainer = inspectorScrollView.contentContainer;
                    if (contentContainer != null)
                    {
                        contentContainer.style.flexGrow = 1;
                        contentContainer.style.overflow = Overflow.Visible;
                    }
                    
                    break;
                }
                current = current.parent;
            }
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
        /// Creates the UIElements-based sticky header.
        /// </summary>
        private VisualElement CreateEditorHeader()
        {
            var header = new VisualElement();
            header.name = "StickyHeader";
            header.style.height = HeaderHeight;
            header.style.flexShrink = 0; // Don't shrink - critical for sticky behavior
            header.style.flexGrow = 0;   // Don't grow
            header.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f);
            header.style.borderBottomWidth = 3.5f;
            header.style.borderBottomColor = GetAssetTypeColor().Fade(.8f).Amplify(1f);
            header.style.paddingLeft = Padding;
            header.style.paddingRight = Padding;
            header.style.paddingTop = Padding;
            header.style.paddingBottom = Padding;
            header.style.flexDirection = FlexDirection.Row;
            
            // Extend header to compensate
            header.style.marginLeft = -InspectorMarginInset - InspectorMarginInsetFix;
            header.style.marginRight = -InspectorMarginInset - InspectorMarginInsetFix;
            header.style.paddingLeft = Padding; // Your internal padding
            header.style.paddingRight = Padding;
            
            // Left: Icon
            _headerIconContainer = new VisualElement();
            _headerIconContainer.name = "HeaderIconContainer";
            _headerIconContainer.style.width = IconSize;
            _headerIconContainer.style.height = IconSize;
            _headerIconContainer.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            _headerIconContainer.style.alignSelf = Align.Center;
            _headerIconContainer.style.marginRight = 12;
            _headerIconContainer.style.justifyContent = Justify.Center;
            _headerIconContainer.style.alignItems = Align.Center;
            
            RebuildHeaderIcon();
            header.Add(_headerIconContainer);
            
            // Center: Text content
            var textContainer = new VisualElement();
            textContainer.name = "HeaderTextContainer";
            textContainer.style.flexGrow = 1;
            textContainer.style.justifyContent = Justify.Center;
            
            // Asset type badge
            var typeBadge = new Label(GetAssetTypeLabel());
            typeBadge.style.fontSize = 9;
            typeBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            typeBadge.style.color = GetAssetTypeColor();
            typeBadge.style.backgroundColor = GetAssetTypeColor().Fade(.8f).Amplify(.2f);

            typeBadge.style.paddingLeft = 4;
            typeBadge.style.paddingRight = 4;
            typeBadge.style.paddingTop = 2;
            typeBadge.style.paddingBottom = 2;
            typeBadge.style.alignSelf = Align.FlexStart;
            typeBadge.style.marginBottom = 2;
            textContainer.Add(typeBadge);
            
            // Name label (store reference for updates)
            _headerNameLabel = new Label(GetDisplayName());
            _headerNameLabel.name = "HeaderName";
            _headerNameLabel.style.fontSize = 14;
            _headerNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _headerNameLabel.style.color = new Color(0.9f, 0.9f, 0.9f);
            _headerNameLabel.style.overflow = Overflow.Hidden;
            _headerNameLabel.style.textOverflow = TextOverflow.Ellipsis;
            textContainer.Add(_headerNameLabel);
            
            // Description label (store reference for updates)
            // Always create it, just hide when empty
            _headerDescLabel = new Label();
            _headerDescLabel.name = "HeaderDescription";
            _headerDescLabel.style.fontSize = 10;
            _headerDescLabel.style.color = new Color(0.55f, 0.55f, 0.55f);
            _headerDescLabel.style.overflow = Overflow.Hidden;
            _headerDescLabel.style.textOverflow = TextOverflow.Ellipsis;
            _headerDescLabel.style.whiteSpace = WhiteSpace.Normal;
            _headerDescLabel.style.maxHeight = 28;
            textContainer.Add(_headerDescLabel);
            
            // Set initial description
            UpdateHeaderDescription();
            
            header.Add(textContainer);
            
            // Right: Buttons
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.alignSelf = Align.FlexStart;
            buttonsContainer.style.flexDirection = FlexDirection.Column;
            buttonsContainer.style.alignItems = Align.FlexEnd;
            buttonsContainer.style.marginRight = 12;
            
            // Top row: Doc button
            var topButtonRow = new VisualElement();
            topButtonRow.style.flexDirection = FlexDirection.Row;
            topButtonRow.style.marginBottom = 4;
            
            var docBtn = CreateHeaderButton("?", "Open Documentation", new Color(0.4f, 0.4f, 0.4f), () =>
            {
                var url = GetDocumentationUrl();
                if (!string.IsNullOrEmpty(url))
                    Application.OpenURL(url);
            });
            topButtonRow.Add(docBtn);
            buttonsContainer.Add(topButtonRow);
            
            // Bottom row: Action buttons
            var bottomButtonRow = new VisualElement();
            bottomButtonRow.style.flexDirection = FlexDirection.Row;
            
            // Refresh button
            var refreshBtn = CreateHeaderButton("↻", "Refresh", new Color(0.5f, 0.5f, 0.5f), () => Refresh());
            bottomButtonRow.Add(refreshBtn);
            
            // Import button (if enabled)
            if (ShowImportButton)
            {
                var importBtn = CreateHeaderButton("↓", "Import from another asset", new Color(0.4f, 0.6f, 0.4f), () => OpenImportPicker());
                importBtn.style.marginLeft = ButtonSpacing;
                bottomButtonRow.Add(importBtn);
            }
            
            // Visualize button (if enabled)
            if (ShowVisualizeButton)
            {
                var vizBtn = CreateHeaderButton("◉", "Visualize", new Color(0.6f, 0.4f, 0.7f), () => OnVisualize());
                vizBtn.style.marginLeft = ButtonSpacing;
                bottomButtonRow.Add(vizBtn);
            }
            
            // Open in Manager button
            var openBtn = CreateHeaderButton("⊞", "Open in Manager", new Color(0.3f, 0.5f, 0.7f), () => OnOpenInManager());
            openBtn.style.marginLeft = ButtonSpacing;
            bottomButtonRow.Add(openBtn);
            
            buttonsContainer.Add(bottomButtonRow);
            header.Add(buttonsContainer);
            
            return header;
        }
        
        private Button CreateHeaderButton(string icon, string tooltip, Color baseColor, Action onClick)
        {
            var btn = new Button(onClick);
            btn.text = icon;
            btn.tooltip = tooltip;
            btn.focusable = false;
            btn.style.width = ButtonSize;
            btn.style.height = ButtonSize;
            btn.style.fontSize = 12;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.backgroundColor = baseColor;
            btn.style.color = new Color(0.8f, 0.8f, 0.8f);
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.paddingLeft = 0;
            btn.style.paddingRight = 0;
            btn.style.paddingTop = 0;
            btn.style.paddingBottom = 0;
            btn.style.marginLeft = 0;
            btn.style.marginRight = 0;
            
            var hoverColor = new Color(baseColor.r + 0.15f, baseColor.g + 0.15f, baseColor.b + 0.15f);
            btn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                btn.style.backgroundColor = hoverColor;
                btn.style.color = Color.white;
            });
            btn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                btn.style.backgroundColor = baseColor;
                btn.style.color = new Color(0.8f, 0.8f, 0.8f);
            });
            
            return btn;
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
                _headerNameLabel.text = GetDisplayName();
            }
        }
        
        /// <summary>
        /// Updates the header description label. Call when Description field changes.
        /// </summary>
        protected void UpdateHeaderDescription()
        {
            if (_headerDescLabel == null) return;
            
            var desc = GetDisplayDescription();
            _headerDescLabel.text = desc;
            _headerDescLabel.style.display = string.IsNullOrEmpty(desc) ? DisplayStyle.None : DisplayStyle.Flex;
        }
        
        /// <summary>
        /// Rebuilds the header icon. Call when Textures field changes.
        /// </summary>
        protected void UpdateHeaderIcon()
        {
            RebuildHeaderIcon();
        }
        
        private void RebuildHeaderIcon()
        {
            if (_headerIconContainer == null) return;
            
            _headerIconContainer.Clear();
            
            var icon = GetHeaderIcon();
            if (icon != null)
            {
                var iconImage = new Image();
                iconImage.image = icon;
                iconImage.style.width = IconSize - 4;
                iconImage.style.height = IconSize - 4;
                iconImage.scaleMode = ScaleMode.ScaleToFit;
                _headerIconContainer.Add(iconImage);
            }
            else
            {
                var placeholder = new Label("?");
                placeholder.style.fontSize = 20;
                placeholder.style.color = Colors.HintText;
                placeholder.style.unityTextAlign = TextAnchor.MiddleCenter;
                _headerIconContainer.Add(placeholder);
            }
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
        
        protected (string result, bool isUnknown) GenerateAssetTag(string assetName, string fallback)
        {
            if (string.IsNullOrEmpty(assetName))
                return ($"{UnnamedAssetTag} {fallback}!", true);
            
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
            
            // Ensure it starts with a letter
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = $"{fallback}_" + result;
            }

            return string.IsNullOrEmpty(result) ? ($"{UnnamedAssetTag} {fallback}!", true) : (result, false);
        }
        
        /// <summary>
        /// Updates the Name field of a Tag based on the asset's display name.
        /// </summary>
        protected string UpdateTagName(ref Tag tag, string assetName, string fallback)
        {
            var (generatedName, isUnknown) = GenerateAssetTag(assetName, fallback);
            
            if (!isUnknown)
            {
                tag.Name = generatedName;
            }
            
            return generatedName;
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
        
        protected void BuildLinkToButton<T>(VisualElement parent, BaseForgeLinkProvider src) where T : BaseForgeLinkProvider
        {
            // Link to Entity button
            var linkAssetButton = new Button(() =>
            {
                _pickerControlId = GUIUtility.GetControlID(FocusType.Passive);
                _waitingForPicker = true;
                _pickerType = typeof(T);
                _pickerCallback = obj =>
                {
                    if (obj is T asset)
                    {
                        if (src.HasCircularDependency(asset, out var issue, out var chain))
                        {
                            Debug.LogError($"[PlayForge] Cannot link {src.GetProviderName()} to {asset.GetProviderName()} due to a circular dependency. " +
                                           $"\nRoot Issue: {issue.GetProviderName()} is linked to {src.GetProviderName()}." +
                                           $"\nLink Chain: {chain}." +
                                           $"\n");
                            return;
                        }

                        src.LinkToProvider(asset);
                        MarkDirty(src);
                        RebuildLevelSourceContent();
                        RebuildLevelingContent();
                    }
                };
                ShowProviderPicker<T>(src);
            });
            linkAssetButton.text = GetProviderTypeName(typeof(T));
            linkAssetButton.tooltip = $"Link to an {GetProviderTypeName(typeof(T))} to use its level range";
            linkAssetButton.style.marginRight = 4;
            ApplyButtonHoverStyle(linkAssetButton);
            
            parent.Add(linkAssetButton);
        }
        
        protected void ShowProviderPicker<T>(BaseForgeLinkProvider src) where T : BaseForgeLinkProvider
        {
            // Generate a unique control ID
            _pickerControlId = GUIUtility.GetControlID(FocusType.Passive) + 10000 + Random.Range(1, 1000);
            _waitingForPicker = true;
            _lastPickedObject = src.LinkedProvider; // Start with current value
            EditorGUIUtility.ShowObjectPicker<T>(src.LinkedProvider as T, false, "", _pickerControlId);
        }

        protected abstract void RebuildLevelSourceContent();
        
        protected void BuildProviderSelector(VisualElement levelSourceContent, BaseForgeLinkProvider src)
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
        }

        protected abstract void RebuildLevelingContent();
        
    }
}