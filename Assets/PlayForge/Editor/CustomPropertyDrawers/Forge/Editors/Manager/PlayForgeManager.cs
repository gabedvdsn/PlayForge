using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// PlayForge Manager - Central hub for managing PlayForge assets.
    /// Split into partial classes for maintainability:
    /// - PlayForgeManager.cs (this file) - Core window, header, tab navigation
    /// - PlayForgeManager.CreateTab.cs - Create tab with statistics
    /// - PlayForgeManager.ViewTab.cs - View tab with spreadsheet/tags
    /// - PlayForgeManager.SettingsTab.cs - Settings with General/Assets tabs
    /// - PlayForgeManager.Utilities.cs - Shared helper methods
    /// </summary>
    public partial class PlayForgeManager : EditorWindow
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Constants & Static Data
        // ═══════════════════════════════════════════════════════════════════════════
        
        private const string WINDOW_TITLE = "PlayForge Manager";
        internal const string PREFS_PREFIX = "PlayForge_";
        internal const int COLOR_INDICATOR_WIDTH = 3;
        internal const string TAGS_VIEW_MARKER = "__TAGS__";
        
        // Default paths (configurable in settings)
        internal static readonly Dictionary<Type, string> DefaultPaths = new Dictionary<Type, string>
        {
            { typeof(Attribute), "Assets/Data/Attributes" },
            { typeof(AttributeSet), "Assets/Data/AttributeSets" },
            { typeof(GameplayEffect), "Assets/Data/Effects" },
            { typeof(EntityIdentity), "Assets/Data/Entities" },
            { typeof(Ability), "Assets/Data/Abilities" },
            { typeof(Item), "Assets/Data/Items" },
            { typeof(ScalerTemplate), "Assets/Data/Templates/Scalers" },
            { typeof(RequirementTemplate), "Assets/Data/Templates/Requirements" },
        };
        
        // Asset type metadata for UI
        public static readonly List<AssetTypeInfo> AssetTypes = new List<AssetTypeInfo>
        {
            new AssetTypeInfo(typeof(Ability), "Ability", "⚡", Colors.AccentOrange, "Ability definition", true, true),
            new AssetTypeInfo(typeof(GameplayEffect), "Effect", "✦", Colors.AccentRed, "Gameplay effect definition", true, true),
            new AssetTypeInfo(typeof(Item), "Item", "🎁", new Color(1f, 0.8f, 0.3f), "Item with effects & abilities", true, true),
            new AssetTypeInfo(typeof(Attribute), "Attribute", "📈", Colors.AccentBlue, "Base stat definition", true, false),
            new AssetTypeInfo(typeof(AttributeSet), "Attribute Set", "📊", Colors.AccentGreen, "Collection of attributes", true, true),
            new AssetTypeInfo(typeof(EntityIdentity), "Entity", "👤", Colors.AccentPurple, "Entity configuration", true, true),
            new AssetTypeInfo(typeof(ScalerTemplate), "Scaler", "📋", Colors.AccentCyan, "Reusable scaler configuration", true, false),
            new AssetTypeInfo(typeof(RequirementTemplate), "Requirement", "📋", new Color(0.95f, 0.6f, 0.2f), "Reusable tag requirement configuration", true, false),
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // State
        // ═══════════════════════════════════════════════════════════════════════════
        
        private int currentTab = 0;
        private readonly string[] tabNames = { "Create", "View", "Settings" };
        
        // View tab state
        internal string searchFilter = "";
        internal Type selectedTypeFilter = null;
        internal bool showTagsView = false;
        internal string selectedTagContextFilter = null;
        internal List<ScriptableObject> cachedAssets = new List<ScriptableObject>();
        
        // UI References
        private VisualElement root;
        internal VisualElement contentContainer;
        private List<Button> tabButtons = new List<Button>();
        
        // Settings tabs state
        internal int currentSettingsTab = 0;
        internal readonly string[] settingsTabNames = { "General", "Assets" };
        internal List<Button> settingsTabButtons = new List<Button>();
        internal VisualElement settingsContentContainer;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Window Management
        // ═══════════════════════════════════════════════════════════════════════════
        
        [MenuItem("Tools/PlayForge/Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<PlayForgeManager>();
            window.titleContent = new GUIContent(WINDOW_TITLE, EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(700, 450);
        }
        
        public static void OpenVisualizer(ScriptableObject asset)
        {
            if (asset == null) return;
            
            var window = GetWindow<PlayForgeVisualizer>();
            window.SetAsset(asset);
            window.Show();
        }
        
        public static string GetAssetDisplayName(ScriptableObject asset)
        {
            return asset switch
            {
                Ability a => !string.IsNullOrEmpty(a.GetName()) ? a.GetName() : a.name,
                GameplayEffect e => !string.IsNullOrEmpty(e.GetName()) ? e.GetName() : e.name,
                Attribute attr => !string.IsNullOrEmpty(attr.Name) ? attr.Name : attr.name,
                EntityIdentity ent => !string.IsNullOrEmpty(ent.GetName()) ? ent.GetName() : ent.name,
                AttributeSet attrSet => !string.IsNullOrEmpty(attrSet.GetName()) ? attrSet.GetName() : attrSet.Name,
                Item item => !string.IsNullOrEmpty(item.GetName()) ? item.GetName() : item.name,
                _ => asset.name
            };
        }
        
        /// <summary>
        /// Opens the PlayForge Manager and navigates to a specific asset.
        /// Called from asset editor headers via the "Open" button.
        /// </summary>
        public static void OpenToAsset(UnityEngine.Object asset)
        {
            if (asset == null) return;
            
            var window = GetWindow<PlayForgeManager>();
            window.titleContent = new GUIContent(WINDOW_TITLE, EditorGUIUtility.IconContent("d_ScriptableObject Icon").image);
            window.minSize = new Vector2(700, 450);
            window.Show();
            window.Focus();
            
            // Navigate to View tab
            window.currentTab = 1; // View tab
            window.ShowTab(1);
            
            // Set type filter to match asset type
            var assetType = asset.GetType();
            var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == assetType);
            if (typeInfo != null)
            {
                window.selectedTypeFilter = assetType;
                window.showTagsView = false;
            }
            
            // Set search filter to asset name to highlight it
            if (asset is ScriptableObject so)
            {
                var displayName = GetAssetDisplayName(so);
                window.searchFilter = displayName;
            }
            
            // Refresh the view
            window.RefreshAssetCache();
            
            // Select the asset in Unity's selection
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
        
        private void OnEnable()
        {
            RefreshAssetCache();
            
            if (EditorPrefs.GetBool(PREFS_PREFIX + "RememberTypeFilter", true))
            {
                var lastType = EditorPrefs.GetString(PREFS_PREFIX + "LastTypeFilter", "");
                if (lastType == TAGS_VIEW_MARKER)
                {
                    showTagsView = true;
                    selectedTypeFilter = null;
                }
                else if (!string.IsNullOrEmpty(lastType))
                {
                    selectedTypeFilter = AssetTypes.FirstOrDefault(t => t.Type.Name == lastType)?.Type;
                }
            }
        }
        
        private void CreateGUI()
        {
            root = rootVisualElement;
            root.style.flexGrow = 1;
            root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            BuildHeader();
            BuildMainTabBar();
            BuildContentArea();
            
            ShowTab(currentTab);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Header
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildHeader()
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 8;
            header.style.paddingBottom = 8;
            header.style.backgroundColor = Colors.HeaderBackground;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Colors.BorderDark;
            root.Add(header);
            
            var titleLabel = new Label("⚒ PLAYFORGE");
            titleLabel.style.fontSize = 16;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Colors.HeaderText;
            titleLabel.style.letterSpacing = 2;
            header.Add(titleLabel);
            
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            header.Add(spacer);
            
            var statsContainer = new VisualElement();
            statsContainer.style.flexDirection = FlexDirection.Row;
            statsContainer.style.alignItems = Align.Center;
            header.Add(statsContainer);
            
            foreach (var typeInfo in AssetTypes.Take(5))
            {
                var count = CountAssetsOfType(typeInfo.Type);
                var stat = CreateCompactStat(typeInfo.Icon, count.ToString(), typeInfo.Color);
                stat.tooltip = $"{count} {typeInfo.DisplayName}(s)";
                statsContainer.Add(stat);
            }
            
            var tagCount = TagRegistry.GetAllTags().Count();
            var tagStat = CreateCompactStat("🏷", tagCount.ToString(), Colors.AccentCyan);
            tagStat.tooltip = $"{tagCount} unique tag(s)";
            statsContainer.Add(tagStat);
            
            var refreshBtn = CreateButton("↻", RefreshAll, "Refresh asset cache & tag registry");
            refreshBtn.style.marginLeft = 12;
            refreshBtn.style.width = 28;
            refreshBtn.style.height = 28;
            ApplyButtonStyle(refreshBtn);
            header.Add(refreshBtn);
        }
        
        private VisualElement CreateCompactStat(string icon, string value, Color color)
        {
            var stat = new VisualElement();
            stat.style.flexDirection = FlexDirection.Row;
            stat.style.alignItems = Align.Center;
            stat.style.marginLeft = 8;
            stat.style.paddingLeft = 6;
            stat.style.paddingRight = 6;
            stat.style.paddingTop = 2;
            stat.style.paddingBottom = 2;
            stat.style.backgroundColor = new Color(color.r, color.g, color.b, 0.2f);
            stat.style.borderTopLeftRadius = 4;
            stat.style.borderTopRightRadius = 4;
            stat.style.borderBottomLeftRadius = 4;
            stat.style.borderBottomRightRadius = 4;
            
            var iconLabel = new Label(icon);
            iconLabel.style.fontSize = 10;
            iconLabel.style.marginRight = 4;
            stat.Add(iconLabel);
            
            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 11;
            valueLabel.style.color = color;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            stat.Add(valueLabel);
            
            return stat;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Main Tab Bar
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildMainTabBar()
        {
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
            tabBar.style.paddingLeft = 8;
            tabBar.style.paddingTop = 4;
            tabBar.style.paddingBottom = 0;
            root.Add(tabBar);
            
            tabButtons.Clear();
            
            for (int i = 0; i < tabNames.Length; i++)
            {
                int tabIndex = i;
                var btn = CreateButton(tabNames[i], () => ShowTab(tabIndex));
                btn.style.paddingLeft = 16;
                btn.style.paddingRight = 16;
                btn.style.paddingTop = 8;
                btn.style.paddingBottom = 8;
                btn.style.marginRight = 2;
                btn.style.borderTopLeftRadius = 4;
                btn.style.borderTopRightRadius = 4;
                btn.style.borderBottomLeftRadius = 0;
                btn.style.borderBottomRightRadius = 0;
                btn.style.borderBottomWidth = 0;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                btn.style.fontSize = 12;
                
                tabButtons.Add(btn);
                tabBar.Add(btn);
            }
        }
        
        internal void ShowTab(int index)
        {
            currentTab = index;
            
            for (int i = 0; i < tabButtons.Count; i++)
            {
                bool isActive = i == currentTab;
                tabButtons[i].style.backgroundColor = isActive 
                    ? new Color(0.25f, 0.25f, 0.25f, 1f) 
                    : new Color(0.15f, 0.15f, 0.15f, 1f);
                tabButtons[i].style.color = isActive 
                    ? Colors.HeaderText 
                    : Colors.HintText;
            }
            
            contentContainer.Clear();
            
            switch (currentTab)
            {
                case 0: BuildCreateTab(); break;
                case 1: BuildViewTab(); break;
                case 2: BuildSettingsTab(); break;
            }
        }
        
        private void BuildContentArea()
        {
            contentContainer = new VisualElement();
            contentContainer.style.flexGrow = 1;
            contentContainer.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f, 1f);
            contentContainer.style.paddingLeft = 12;
            contentContainer.style.paddingRight = 12;
            contentContainer.style.paddingTop = 12;
            contentContainer.style.paddingBottom = 12;
            root.Add(contentContainer);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Type Info
        // ═══════════════════════════════════════════════════════════════════════════
        
        public class AssetTypeInfo
        {
            public Type Type { get; }
            public string DisplayName { get; }
            public string Icon { get; }
            public Color Color { get; }
            public string Description { get; }
            public bool CanCreate { get; }
            public bool CanVisualize { get; }
            
            public AssetTypeInfo(Type type, string displayName, string icon, Color color, string description, bool canCreate = true, bool canVisualize = true)
            {
                Type = type;
                DisplayName = displayName;
                Icon = icon;
                Color = color;
                Description = description;
                CanCreate = canCreate;
                CanVisualize = canVisualize;
            }
        }
    }
}