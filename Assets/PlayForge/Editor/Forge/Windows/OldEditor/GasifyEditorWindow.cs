/*
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using CodiceApp;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;
using Image = UnityEngine.UIElements.Image;
using FarEmerald;
using JetBrains.Annotations;

/*
 * Attributions:
 * - Home Icon -> <a href="https://www.flaticon.com/free-icons/home-button" title="home button icons">Home button icons created by Freepik - Flaticon</a>
 * - Fireball -> <a href="https://www.flaticon.com/free-icons/fireball" title="fireball icons">Fireball icons created by Vectorslab - Flaticon</a>
 * 
 #1#

namespace FarEmerald.PlayForge.Extended.Editor
{
    #if UNITY_EDITOR
    public class GasifyEditorWindow : EditorWindow
    {
        
        #region Wrappers
        
        class SearchItem
        {
            public string Display;
            public int Id;
            public EDataType Kind;
            public bool IsPlaceholder;
            public int KindOrder => (int)Kind;
            public override string ToString() => Display;

            public SearchItem(string display, int id, EDataType kind, bool isPlaceholder = false)
            {
                Display = display;
                Id = id;
                Kind = kind;
                IsPlaceholder = isPlaceholder;
            }
        }

        // Menu entry model for multi-column dropdowns
        class MenuEntry
        {
            public Texture2D Icon;
            public string Text;
            public Action OnClick;
            public MenuEntry(Texture2D icon, string text, Action onClick) { Icon = icon; Text = text; OnClick = onClick; }
        }
        
        public enum GasifyPage { Landing, Home, Creator, Developer }
        enum PageSetTiming { Set, Leave }
        
        public static string DataTypeText(EDataType kind)
        {
            var header = kind switch
            {
                EDataType.Ability   => "Ability",
                EDataType.Effect    => "Effect",
                EDataType.Entity    => "Entity",
                EDataType.Attribute => "Attribute",
                EDataType.Tag       => "Tag",
                EDataType.AttributeSet       => "Attribute Set",
                _                  => kind.ToString()
            };
            return header;
        }
        
        static string DataTypeTextPlural(EDataType kind)
        {
            var header = kind switch
            {
                EDataType.Ability   => "Abilities",
                EDataType.Effect    => "Effects",
                EDataType.Entity    => "Entities",
                EDataType.Attribute => "Attributes",
                EDataType.Tag       => "Tags",
                EDataType.AttributeSet       => "Attribute Sets",
                _                  => kind.ToString()
            };
            return header;
        }
        
        #endregion
        
        #region Variables
        
        private static GasifyPage lastPage = GasifyPage.Landing;
        private static GasifyPage activePage = GasifyPage.Landing;
        
        private const string RootTitle = "FESGAS";
        
        #region Colors
        
        private Color PrimaryButtonColor => new Color(0.38f, 0.36f, 0.7f, 1f);
        private Color PrimaryButtonColorHover => new Color(.46f, .42f, .8f);
        private Color SecondaryButtonColor => new Color(.46f, .44f, .55f);
        private Color SecondaryButtonColorHover => new Color(.56f, .54f, .65f);
        private Color TertiaryButtonColor => new Color(.77f, .75f, .85f);
        private Color TertiaryButtonColorHover => new Color(.87f, .85f, .95f);
        
        private Color BackgroundColorDark => new Color(0.17f, 0.17f, 0.2f, 1f);
        private Color BackgroundColorDeepDark => new Color(0.12f, 0.12f, 0.14f, 1f);
        private Color BackgroundColorLight => new Color(0.27f, 0.27f, 0.3f, 1f);
        private Color BackgroundColorDeepLight => new Color(0.42f, 0.42f, 0.45f, 1f);
        private Color BackgroundColorDropdown => new Color(0.22f, 0.22f, 0.26f, 1f);
        private Color BackgroundColorSub => new Color(0.4f, 0.4f, 0.5f, 1f);
        private Color EdgeColorDark => new Color(0, 0, 0, .5f);

        private Color TextColorLight => new Color(0.85f, 0.85f, 0.9f, 1f);
        private Color TextColorSub => new Color(0.75f, 0.75f, 0.75f, 0.95f);
        
        #endregion
        
        #region Sizing
        
        private const float DropdownColumnWidthSmall = 160f;
        private const float DropdownColumnWidth = 200f;
        private const float DropdownColumnWidthLarge = 260f;
        private const float ListviewMinHeight = 220f;
        private const float ListviewItemMinHeight = 22f;

        #endregion
        
        #endregion

        #region Internal
        
        // Project / data management
        private FrameworkProject Project = new FrameworkProject();
        private Dictionary<EDataType, List<(int, string)>> projectData;
        
        private static ForgeJsonUtility.SettingsWrapper MasterSettings;
        private ForgeJsonUtility.SettingsWrapper LocalSettings;
        
        // Absolute path of the currently loaded FrameworkProject JSON:
        string _projectFilePath;                 // set this when you load a project
        string ProjectKey => _projectFilePath;   // simple and stable

        private IList GetProjectItems(EDataType data)
        {
            return data switch
            {
                EDataType.Ability => Project.Abilities,
                EDataType.Effect => Project.Effects,
                EDataType.Entity => Project.Entities,
                EDataType.Attribute => Project.Attributes,
                EDataType.Tag => Project.Tags,
                EDataType.AttributeSet => Project.AttributeSets,
                EDataType.None => new List<byte>(),
                _ => throw new ArgumentOutOfRangeException(nameof(data), data, null)
            };
        }
        
        #region Page Elements
        
        private VisualElement content;
        private VisualElement overlay;
        
        #region Navigation Bar
        
        // Navigation bar
        private VisualElement navBar;
        
        // Home
        private VisualElement nav_homeButton;
        
        // Create dropdown
        private VisualElement nav_createWrap;
        private Button nav_createButton;
        private Button nav_createShortcut;
        private bool _nav_overCreateButton, _nav_overCreateDD;
        private bool nav_overCreateButton, nav_overCreateDD;
        private VisualElement nav_createDropdown;
        private int nav_createHoverCount;

        enum ProxyRowType { Header, RecentItem, Separator, TaskItem, Placeholder }
        private TextField nav_proxySearchField;
        private ListView nav_proxyListView;
        private List<ProxyRow> nav_recentProxyTasks = new();
        private const int nav_maxRecentProxy = 6;

        class ProxyRow
        {
            public ProxyRowType Type;
            public string Text;
            private ProxyRow(ProxyRowType type, string text)
            {
                Type = type;
                Text = text;
            }

            public static ProxyRow TaskItem(string text) => new(ProxyRowType.TaskItem, text);
            public static ProxyRow Header(string text) => new(ProxyRowType.Header, text);
            public static ProxyRow Separator() => new(ProxyRowType.Separator, "");
            public static ProxyRow Placeholder(string text) => new(ProxyRowType.Placeholder, text);
        }
        
        // Develop dropdown
        private VisualElement nav_developButton;
        private bool nav_overDevelopButton, nav_overDevelopDD;
        private VisualElement nav_developDropdown;
        private int nav_developHoverCount;
        
        // Setup
        private VisualElement nav_setupButton;
        
        // Search
        private VisualElement nav_searchDropdown;
        private TextField nav_searchField;
        private ListView nav_searchList;
        private List<SearchItem> nav_searchItems = new();
        private bool _nav_overSearchField, _nav_overSearchDD;
        
        // Options
        private VisualElement nav_optionsButton;
        private bool nav_overOptionsButton, nav_overOptionsDD;
        private VisualElement nav_optionsDropdown;
        
        #endregion
        
        #region Landing Page
        
        private VisualElement landingPage;
        private VisualElement landing_disclaimer;
        private VisualElement landing_card;
        private VisualElement landing_openPopup;

        private Button landing_loadButton;
        private Button landing_quickLoadButton;
        
        // ===== Landing: Create Popup state =====
        private VisualElement landing_createCard;
        private VisualElement landing_createPopup;
        private TextField     landing_create_nameField;

        private enum CreateFrameworkMode { CreateNew, FromExisting }
        private enum MergePolicy { PreferEarlier, PreferLater }

        private CreateFrameworkMode landing_createFrameworkMode = CreateFrameworkMode.CreateNew;
        private PopupField<string> landing_settingsPolicyPopup;
        private MergePolicy  landing_settingsPolicy = MergePolicy.PreferLater;
        private MergePolicy  landing_templatesPolicy = MergePolicy.PreferLater;
        
        private VisualElement landing_fwGrid;
        private List<string> landing_fwKeys = new(); // display order (alpha)
        private readonly Dictionary<string, bool> landing_fwUseTemplate = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> landing_fwUseSettings = new(StringComparer.OrdinalIgnoreCase);

// Selection state
        private readonly HashSet<string> landing_selectedFrameworksForSettings = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> landing_selectedTemplates = new(StringComparer.OrdinalIgnoreCase);

// UI lists
        private ScrollView landing_settingsList;
        private ScrollView landing_templatesList;
        
        #region Open
        
        VisualElement landing_openCard;
        TextField     landing_open_searchField;
        PopupField<string> landing_open_sortField; // Name, Date, Count
        ScrollView    landing_open_list;
        Label     landing_open_selectedField;
        Label         landing_open_issueSummary;

        readonly string[] landing_open_sortModes = { "Name", "Date Created", "# Datas" };
        string landing_open_search = "";
        string landing_open_sort   = "Name";
        string landing_open_selectedKey = null;
        
        #endregion
        
        #endregion
        
        #region Home Page
        
        private VisualElement homePage;
        
        // Project view
        private VisualElement home_projectView;
        private TextField home_projectSearchField;
        private Button home_projectFilterButton;
        private ListView home_projectList;
        private Dictionary<EDataType, List<(int, string)>> filteredProjectData;
        
        enum PVRowType { Header, Item, Placeholder }

        class PVRow
        {
            public PVRowType Type;
            public EDataType DataType;
            public int Id;
            public string Name;
            public bool Collapsed;

            public PVRow()
            {
            }

            public PVRow(PVRowType type, EDataType dataType, int id, string name, bool collapsed = false)
            {
                Type = type;
                DataType = dataType;
                Id = id;
                Name = name;
                Collapsed = collapsed;
            }
        }

        private readonly Dictionary<EDataType, bool> home_pvSectionCollapsed = new();
        private readonly HashSet<EDataType> home_pvTypeFilter = new();
        private bool home_pvOnlyInUse = false;
        private bool home_pvOnlyTemplates = false;
        private bool home_pvOnlyPopulated = true;
        private bool home_pvOnlyEditable = true;
        private List<PVRow> home_pvRows = new();
        
        // Usage map
        private VisualElement home_usageMap;
        
        #endregion
        
        #region Creator Page
        
        private VisualElement creatorPage;
        
        private VisualElement creator_actions;
        private Button creator_saveBtn;
        private Button creator_optionsBtn;
        private Button creator_createButton;
        
        private VisualElement creator_usage;

        private bool creator_autoSave;
        
        // Primary stack (top-wide)
        private struct StackToken { public string Text; public Action OnClick; public StackToken(string t, Action a = null){ Text=t; OnClick=a; } }
        private readonly List<StackToken> creator_primaryStack = new();

        // Buried stack (right pane) already exists:
        private readonly List<BuriedFrame> creator_buriedStack = new();

        // Track which top-level field owns the buried stack
        private FieldInfo creator_buriedRootField;  // null = none
        
        // Creator editor split panes
        private VisualElement creator_editorLeft;      // “Stack” (default fields)
        private VisualElement creator_editorRight;     // “Buried Stack” (drill-ins)
        private VisualElement creator_identifier;      // Identifier card (top-left)
        private VisualElement creator_leftList;        // Scrollable container for default fields
        private VisualElement creator_buriedHeader;    // Stack / breadcrumbs header (right)
        private VisualElement creator_buriedContent;   // Content area (right)
        
        // Identifier live parts
        private Label creator_identifierNameLbl;
        private Label creator_identifierTypeChipLbl;
        private Label creator_identifierIdLbl;

        // Buried frame model
        private class BuriedFrame
        {
            public string Title;
            public Func<VisualElement> BuildUI; // build the right-pane UI when active
            public string PathToken;            // short token for breadcrumb
            public BuriedFrame(string title, string token, Func<VisualElement> build)
            {
                Title = title; PathToken = token; BuildUI = build;
            }
        }
        
        private VisualElement creator_editor;

        private const float Creator_LeftColWidth = 260f;
        private const float Creator_BarHeight    = 28f;

        private CreatorItem creator_focus;
        private CreatorItem creator_focusCopy;
        private CreatorItem creator_lastFocus;

        public struct CreatorItem
        {
            public string Name => Node.Name;
            public int Id => Node.Id;
            
            public EDataType Kind;
            public ForgeDataNode Node;

            public CreatorItem(EDataType kind, ForgeDataNode node)
            {
                Kind = kind;
                Node = node;
            }

            public static CreatorItem None()
            {
                return new CreatorItem(EDataType.None, null);
            }
        } 
        
        #endregion
        
        #region Developer Page
        
        private VisualElement developerPage;
        
        #endregion
        
        #region Popups

        private VisualElement alertStack;
        private const float alert_minLifetime = 7f;
        private const float alert_maxLifetime = 60f;
        private const float alert_lifetimePerSpace = .6f;
        private static float GetAlertLifetime(AlertStyle style, string body)
        {
            int count = body.Split(' ').Length;
            float time = alert_lifetimePerSpace * count;
            time = style switch
            {

                AlertStyle.Info => time,
                AlertStyle.Warn => time * 2.5f,
                AlertStyle.Error => time * 5f,
                _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
            };
            float clampedTime = Mathf.Clamp(time, alert_minLifetime, alert_maxLifetime);
            return clampedTime;
        }

        private static string GetAlertHeader(AlertStyle style)
        {
            return style switch
            {

                AlertStyle.Info => "Info",
                AlertStyle.Warn => "Warning",
                AlertStyle.Error => "Error",
                _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
            };
        }

        private Color AlertColor_Body => new Color(0.16f, 0.18f, 0.22f, 1f);
        private Color AlertColor_Info => new Color(0.25f, 0.35f, 0.55f, 1f);
        private Color AlertColor_Warn => new Color(.859f, .48f, .05f, 1f);
        private Color AlertColor_Error => new Color(.8f, .207f, .007f, 1f);
        
        class AlertButtonSpec
        {
            public string Text;
            public Action OnClick;
            public bool Primary;

            public AlertButtonSpec(string text, Action onClick, bool primary = false)
            {
                Text = text;
                OnClick = onClick;
                Primary = primary;
            }
        }
        
        #endregion
        
        #endregion
        
        #region Icons

        // CREATOR
        private Texture2D icon_ABILITY;
        private Texture2D icon_EFFECT;
        private Texture2D icon_ENTITY;
        private Texture2D icon_ATTRIBUTE;
        private Texture2D icon_TAG;
        private Texture2D icon_ATTRIBUTE_SET;
        private Texture2D icon_MODIFIER;
        private Texture2D icon_IMPACT;
        private Texture2D icon_PROCESS;

        private Texture2D icon_TEMPLATE;
        private Texture2D icon_NO_EDIT;
        
        // ALERTS
        private Texture2D icon_INFO;
        private Texture2D icon_CAUTION;
        private Texture2D icon_WARN;
        private Texture2D icon_ERROR;
        
        // NAVIGATION
        private Texture2D icon_HOME;
        private Texture2D icon_RECENT;
        private Texture2D icon_FILTER;
        private Texture2D icon_BACK;
        private Texture2D icon_CLOSE;
        private Texture2D icon_IMPORT;
        private Texture2D icon_SETTINGS;
        
        #endregion
        
        #endregion
        
        #region Editor
        
        //[MenuItem("Tools/PlayForge/Old Editor", false, 1)]
        private static void ShowWindow()
        {
            var window = GetWindow<GasifyEditorWindow>(RootTitle);
            window.minSize = new Vector2(640, 420);
            window.Show();
        }

        private void CreateGUI()
        {
            activePage = GasifyPage.Landing;
            
            HardReset();
            
            nav_searchItems.Clear();
            nav_recentProxyTasks.Clear();
            
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.flexGrow = 1;
            
            rootVisualElement.style.paddingLeft = 0;
            rootVisualElement.style.paddingRight = 0;
            rootVisualElement.style.paddingTop = 0;
            rootVisualElement.style.paddingBottom = 0;

            LoadIcons();
            
            navBar = BuildNavigationBar();
            content = BuildContent();
            overlay = BuildOverlay();
            
            // === Build dropdown panels (Create / Develop / Search) ===
            nav_createDropdown  = BuildCreateDropdown_Nav();
            nav_developDropdown = BuildDevelopDropdown_Nav();
            nav_searchDropdown  = BuildSearchDropdown_Nav();

            // dropdowns live at the same level as the bar so they can overlap content
            overlay.Add(nav_createDropdown);
            overlay.Add(nav_developDropdown);
            overlay.Add(nav_searchDropdown);

            alertStack = BuildAlertStack();
            overlay.Add(alertStack);
            
            landingPage = BuildLandingPage();
            homePage = BuildHomePage();
            creatorPage = BuildCreatorPage();
            developerPage = BuildDeveloperPage();
            
            rootVisualElement.Add(navBar);
            
            content.Add(landingPage);
            content.Add(homePage);
            content.Add(creatorPage);
            content.Add(developerPage);

            rootVisualElement.Add(content);
            rootVisualElement.Add(overlay);
            
            rootVisualElement.RegisterCallback<MouseDownEvent>(_ => CloseAllDropdowns());
            
            SetPage(activePage);
            TryAutoLoadIntoSession();
        }
        
        private void LoadIcons()
        {
            icon_HOME = LoadFESGASIcon("Navigation", "home.png");
            icon_RECENT = LoadFESGASIcon("Navigation", "recent.png");
            icon_FILTER = LoadFESGASIcon("Navigation", "filter.png");
            icon_BACK = LoadFESGASIcon("Navigation", "back.png");
            icon_CLOSE = LoadFESGASIcon("Navigation", "close.png");
            icon_SETTINGS = LoadFESGASIcon("Navigation", "settings.png");
            
            icon_INFO = LoadFESGASIcon("Alert", "information.png");
            icon_CAUTION = LoadFESGASIcon("Alert", "caution.png");
            icon_WARN = LoadFESGASIcon("Alert", "warning.png");
            icon_ERROR = LoadFESGASIcon("Alert", "error.png");
            
            icon_ABILITY = LoadFESGASIcon("Creator", "ability.png");
            icon_EFFECT = LoadFESGASIcon("Creator", "effect.png");
            icon_ENTITY = LoadFESGASIcon("Creator", "person.png");
            icon_ATTRIBUTE = LoadFESGASIcon("Creator", "attribute.png");
            icon_TAG = LoadFESGASIcon("Creator", "tag.png");
            icon_ATTRIBUTE_SET = LoadFESGASIcon("Creator", "attribute_set.png");
            icon_MODIFIER = LoadFESGASIcon("Creator", "modifier.png");
            icon_IMPACT = LoadFESGASIcon("Creator", "impact.png");
            icon_PROCESS = LoadFESGASIcon("Creator", "process.png");
            
            icon_TEMPLATE = EditorGUIUtility.IconContent("d_Text Icon").image as Texture2D;
            icon_NO_EDIT = LoadFESGASIcon("Navigation", "no-edit.png");
            icon_IMPORT = LoadFESGASIcon("Navigation", "import.png");
        }

        void SoftReset()
        {
            
        }

        public static GasifyEditorWindow OpenTo(string key)
        {
            var window = GetWindow<GasifyEditorWindow>(RootTitle);
            window.minSize = new Vector2(640, 420);
            window.Show();
            
            window.TryQuickLoad(key);
            
            return window;
        }
        
        private bool hasHardReset;
        private void HardReset()
        {
            // 
            
            LoadMasterSettings();
            
            ResetPageSetActions();
            
            nav_searchItems.Clear();
            nav_recentProxyTasks.Clear();

            creator_focus = new CreatorItem(EDataType.None, null);
            creator_lastFocus = new CreatorItem(EDataType.None, null);

            return;
            
            void ResetPageSetActions()
            {
                PageSetActions = new();
                
                PageSetActions[GasifyPage.Creator] = new Dictionary<PageSetTiming, PageSetDelegate>()
                {
                    { PageSetTiming.Set, WhenSetCreatorPage },
                    { PageSetTiming.Leave, WhenLeaveCreatorPage }
                };
                
                PageSetActions[GasifyPage.Home] = new Dictionary<PageSetTiming, PageSetDelegate>()
                {
                    { PageSetTiming.Set, WhenSetHomePage },
                    { PageSetTiming.Leave, WhenLeaveHomePage }
                };
                
                PageSetActions[GasifyPage.Developer] = new Dictionary<PageSetTiming, PageSetDelegate>()
                {
                    { PageSetTiming.Set, WhenSetDeveloperPage },
                    { PageSetTiming.Leave, WhenLeaveDeveloperPage }
                };
                
                PageSetActions[GasifyPage.Landing] = new Dictionary<PageSetTiming, PageSetDelegate>()
                {
                    { PageSetTiming.Set, WhenSetLandingPage },
                    { PageSetTiming.Leave, WhenLeaveLandingPage }
                };
            }
        }

        /// <summary>
        /// Load framework into project, load local settings and template data
        /// </summary>
        /// <param name="fp"></param>
        private void LoadFramework(FrameworkProject fp)
        {
            DataIdRegistry.Reset();
            
            Project = fp;
            
            DataIdRegistry.RebuildFrom(Project);
            
            LoadLocalSettings();
            SetMasterSetting(ForgeTags.Settings.ACTIVE_FRAMEWORK, Project.MetaName);

            GetWindow<GasifyEditorWindow>().titleContent.text = $"{RootTitle}/{Project.MetaName}";
        }

        private void LoadProjectData()
        {
            projectData = Project.GetCompleteDescriptions();
            var keys = projectData.Keys;
            foreach (var k in keys)
            {
                projectData[k].Sort((a, b) => String.Compare(a.Item2, b.Item2, StringComparison.OrdinalIgnoreCase));
            }
        }

        private bool Setting(Tag target, bool fallback = false) => Setting<bool>(target, fallback);

        private T Setting<T>(Tag target, T fallback = default)
        {
            if (LocalSettings is null) return fallback;
            if (LocalSettings.StatusCheck<T>(target, out var result) && result != null) return result;

            if (typeof(T) == typeof(int) && result is long l) return (T)(object)unchecked((int)l);
            if (typeof(T) == typeof(string) && result != null) return (T)(object)result.ToString();

            return fallback;
        }
        
        private bool MasterSetting(Tag target, bool fallback = false) => MasterSetting<bool>(target, fallback);

        private T MasterSetting<T>(Tag target, T fallback = default)
        {
              if (MasterSettings.StatusCheck<T>(target, out var obj) && obj != null) return obj;

            if (typeof(T) == typeof(int) && obj is long l) return (T)(object)unchecked((int)l);
            if (typeof(T) == typeof(string) && obj != null) return (T)(object)obj.ToString();

            return fallback;
        }

        private void SetSetting(Tag key, object value, bool saveNow = true)
        {
            LocalSettings.Set(key, value);
            if (saveNow) ForgeStores.SaveLocalSettings(Project.MetaName, LocalSettings);
        }

        private void SetMasterSetting(Tag key, object value, bool saveNow = true)
        {
            MasterSettings.Set(key, value);
            if (saveNow) ForgeStores.SaveMasterSettings(MasterSettings);
        }
        
        private void LoadMasterSettings()
        {
            MasterSettings = ForgeStores.LoadMasterSettings();

            // Make sure last opened exists
            if (MasterSettings.StatusCheck(ForgeTags.Settings.ACTIVE_FRAMEWORK, out string lastOpened))
            {
                if (!ForgeStores.IterateFrameworkKeys().Contains(lastOpened)) SetMasterSetting(ForgeTags.Settings.ACTIVE_FRAMEWORK, "");
            }
        }

        /// <summary>
        /// Loads master settings a
        /// </summary>
        private void LoadLocalSettings()
        {
            LocalSettings = ForgeStores.LoadLocalSettings(Project.MetaName);
        }
        
        #endregion
        
        #region Helpers
        
        void SetElement(VisualElement ve, bool flag)
        {
            ve.style.display = flag ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private delegate void PageSetDelegate();
        private Dictionary<GasifyPage, Dictionary<PageSetTiming, PageSetDelegate>> PageSetActions; 
        
        private void SetPage(GasifyPage page)
        {
            if (page == GasifyPage.Landing) SoftReset();
            
            CloseAllDropdowns();
            lastPage = activePage;
            activePage = page;

            // Show nav only on Home/Creator/Developer
            bool showNav = page is GasifyPage.Home or GasifyPage.Creator or GasifyPage.Developer;
            if (navBar != null)
                navBar.style.display = showNav ? DisplayStyle.Flex : DisplayStyle.None;

            landingPage.style.display   = page == GasifyPage.Landing   ? DisplayStyle.Flex : DisplayStyle.None;
            homePage.style.display      = page == GasifyPage.Home      ? DisplayStyle.Flex : DisplayStyle.None;
            creatorPage.style.display   = page == GasifyPage.Creator   ? DisplayStyle.Flex : DisplayStyle.None;
            developerPage.style.display = page == GasifyPage.Developer ? DisplayStyle.Flex : DisplayStyle.None;

            PageSetActions[lastPage][PageSetTiming.Leave]?.Invoke();
            PageSetActions[activePage][PageSetTiming.Set]?.Invoke();
            
            RefreshNavigationBar();
        }

        Texture2D LoadFESGASIcon(string ext, string file, string path = "Assets/FESGAS/Editor/Icons")
        {
            #if UNITY_EDITOR
            string _path = $"{path}/{ext}/{file}";
            var t2d = AssetDatabase.LoadAssetAtPath<Texture2D>(_path);
            return t2d;
#else
            return null;
#endif
        }
        
        #region Buttons
        private const int defaultRadius = 8;
        private const float defaultHoverMultiplier = .8f;
        
        private const int primaryButtonHeight = 28;
        private const int secondaryButtonHeight = 24;
        private const int tertiaryButtonHeight = 24;

        private const int defaultIconSize = 16;
        
        Button PrimaryButton(string text, System.Action onClick, int height = primaryButtonHeight, int radius = defaultRadius, string tooltip = null)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            b.style.height = height;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.style.backgroundColor = PrimaryButtonColor; // purple-ish
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.color = TextColorLight;
            b.style.borderTopLeftRadius = radius;
            b.style.borderTopRightRadius = radius;
            b.style.borderBottomLeftRadius = radius;
            b.style.borderBottomRightRadius = radius;

            return b.AttachHoverCallbacks(PrimaryButtonColor).AttachTooltip(tooltip) as Button;
        }

        VisualElement ButtonRow(params Button[] buttons)
        {
            if (buttons.Length == 0) return null;

            VisualElement row = new VisualElement()
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = buttons[0].style.minHeight
                }
            };
            foreach (var btn in buttons) row.Add(btn);
            return row;
        }
        
        Button SecondaryButton(string text, System.Action onClick, int height = secondaryButtonHeight, int radius = defaultRadius, string tooltip = null)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            b.style.height = height;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.style.backgroundColor = SecondaryButtonColor;
            b.style.color = TextColorLight;
            b.style.borderTopLeftRadius = radius;
            b.style.borderTopRightRadius = radius;
            b.style.borderBottomLeftRadius = radius;
            b.style.borderBottomRightRadius = radius;
            
            return b.AttachHoverCallbacks(SecondaryButtonColor).AttachTooltip(tooltip) as Button;
        }
        
        Button TertiaryButton(string text, System.Action onClick, int height = tertiaryButtonHeight, int radius = defaultRadius, string tooltip = null)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            b.style.height = height;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            b.style.backgroundColor = TertiaryButtonColor;
            b.style.color = TextColorLight;
            b.style.borderTopLeftRadius = radius;
            b.style.borderTopRightRadius = radius;
            b.style.borderBottomLeftRadius = radius;
            b.style.borderBottomRightRadius = radius;
            
            return b.AttachHoverCallbacks(SecondaryButtonColor).AttachTooltip(tooltip) as Button;
        }
        
        Button FillButton(string text, System.Action onClick, int radius = defaultRadius, string tooltip = null)
        {
            var b = new Button(() => onClick?.Invoke()) { text = text };
            // Fill the parent's height (row cross-axis)
            b.style.alignSelf = Align.Stretch;
            b.style.flexGrow = 1;                 // share space with siblings
            b.style.height = StyleKeyword.Auto;   // no fixed height

            // Visuals to match your theme
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.backgroundColor = BackgroundColorLight;
            b.style.color = TextColorLight;
            b.style.borderTopLeftRadius = radius; b.style.borderTopRightRadius = radius;
            b.style.borderBottomLeftRadius = radius; b.style.borderBottomRightRadius = radius;
            
            return b.AttachHoverCallbacks(BackgroundColorLight).AttachTooltip(tooltip) as Button;
        }
        
        #endregion
        
        #region Events

        void TrackHover(VisualElement ve, Action<bool> onHover)
        {
            ve.RegisterCallback<PointerEnterEvent>(_ => onHover?.Invoke(true));
            ve.RegisterCallback<PointerLeaveEvent>(_ => onHover?.Invoke(false));
        }
        
        void ShowUnityMessage(string _title, string message, string ok = "OK")
        {
            EditorUtility.DisplayDialog(_title, message, ok);
        }
        
        #endregion
        
        #region Dropdown
        
        void CloseAllDropdowns()
        {
            CloseAllDropdowns_Navigation();

            // SetElement(overlay, false);
        }
        
        void CloseAllDropdowns_Navigation()
        {
            nav_createDropdown.style.display = DisplayStyle.None;
            nav_developDropdown.style.display = DisplayStyle.None;
            nav_searchDropdown.style.display = DisplayStyle.None;
        }

        VisualElement NewDropdownBase(float minWidth)
        {
            var ve = new VisualElement
            {
                style =
                {
                    position = Position.Absolute,
                    display  = DisplayStyle.None,
                    backgroundColor = BackgroundColorDropdown,
                    borderTopLeftRadius = 6, borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
                    borderBottomWidth = 1, borderTopWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderBottomColor = EdgeColorDark, borderTopColor = EdgeColorDark,
                    borderLeftColor   = EdgeColorDark, borderRightColor = EdgeColorDark,
                    paddingTop = 4, paddingBottom = 6, paddingLeft = 6, paddingRight = 6,
                    minWidth = minWidth
                }
            };
            return ve;
        }
        
        VisualElement NewColumn(float flexGrow = 1)
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column, 
                    flexGrow = flexGrow, 
                    marginRight = 8
                }
            };
        }
        
        VisualElement NewColumn(float width, float minWidth, float maxWidth, float flexGrow = 1)
        {
            return new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column, 
                    flexGrow = flexGrow, 
                    marginRight = 8,
                    width = width,
                    minWidth = minWidth,
                    maxWidth = maxWidth
                }
            };
        }

        Label BuildHeader(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                    marginTop = 4, marginBottom = 4,
                    paddingLeft = 4
                }
            };
        }

        VisualElement BuildSeparator(float thickness = 2f)
        {
            return new VisualElement
            {
                style = {
                    height = thickness,
                    backgroundColor = new Color(0f,0f,0f,0.35f),
                    marginTop = 4, marginBottom = 4
                }
            };
        }
        
        VisualElement BuildIconTextItem(MenuEntry entry)
        {
            var item = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 26,
                    paddingLeft = 6, paddingRight = 6,
                    marginBottom = 2
                }
            };

            var icon = new Image
            {
                name = "ICON",
                image = entry.Icon,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = 16, height = 16, marginRight = 6 }
            };
            item.Add(icon);

            var label = new Label(entry.Text)
            {
                name = "TEXT",
                style = {
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = new Color(0.95f,0.95f,1f,1f),
                    flexGrow = 1
                }
            };
            item.Add(label);

            // Hover highlight
            TrackHover(item, over => { item.style.backgroundColor = over ? new Color(0.20f,0.20f,0.24f,1f) : new StyleColor(); });

            item.RegisterCallback<MouseDownEvent>(e => e.StopPropagation());
            
            // Click
            item.RegisterCallback<MouseUpEvent>(_ =>
            {
                entry.OnClick?.Invoke();
                // Close menus after click
                nav_createDropdown.style.display  = DisplayStyle.None;
                nav_developDropdown.style.display = DisplayStyle.None;
            });

            return item;
        }
        
        #endregion
        
        #endregion
        
        #region Navigation Bar
        
        #region Helpers
        
        // A consistently styled nav button matching your palette
        Button MakeNavButton(string text, Action onClick, Color color, Color hoverColor, string tooltip = null)
        {
            var btn = new Button(() => onClick?.Invoke()) { text = text };
            btn.tooltip = string.IsNullOrEmpty(tooltip) ? string.Empty : tooltip;
            btn.style.height = 26;
            btn.style.marginRight = 6;
            btn.style.paddingLeft = 10; btn.style.paddingRight = 10;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.backgroundColor = color;
            btn.style.color = TextColorLight;
            btn.style.borderTopLeftRadius = 8; btn.style.borderTopRightRadius = 8;
            btn.style.borderBottomLeftRadius = 8; btn.style.borderBottomRightRadius = 8;
            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverColor);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = color);
            return btn;
        }

        Button MakeNavButton(Texture2D icon, Action onClick, Color color, Color hoverColor, string tooltip = null)
        {
            var btn = new Button(() => onClick?.Invoke())
            {
                style =
                {
                    height = 26,
                    marginRight = 6,
                    paddingLeft = 8, paddingRight = 8, // remove padding so centering is exact
                    backgroundColor = color,
                    color = TextColorLight,
                    borderTopLeftRadius = 6, borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
                    
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center
                },
                tooltip = string.IsNullOrEmpty(tooltip) ? string.Empty : tooltip
            };

            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverColor);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = color);

            var _icon = new Image
            {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                style =
                {
                    width = 16,
                    height = 16,
                    marginRight = 0, // remove margin so it’s dead-center
                    marginLeft = 0
                }
            };

            btn.Add(_icon);
            return btn;
        }

        VisualElement MakeNavButton(Texture2D icon, string text, Action onClick, Color color, Color hoverColor, string tooltip = null)
        {
            var btn = new Button(() => onClick?.Invoke())
            {
                tooltip = string.IsNullOrEmpty(tooltip) ? (text ?? string.Empty) : tooltip
            };

            // Base styling (reuse your palette)
            btn.style.height = 26;
            btn.style.marginRight = 6;
            btn.style.backgroundColor = color;
            btn.style.color = TextColorLight;
            btn.style.borderTopLeftRadius = 8;  btn.style.borderTopRightRadius = 8;
            btn.style.borderBottomLeftRadius = 8; btn.style.borderBottomRightRadius = 8;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            // Make the button act like a flex container
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;

            // Center if icon-only; otherwise left-align content
            bool iconOnly = (icon != null) && string.IsNullOrEmpty(text);
            btn.style.justifyContent = iconOnly ? Justify.Center : Justify.FlexStart;

            // Padding: tighter for icon-only, roomier for text/buttons
            btn.style.paddingLeft  = iconOnly ? 6 : 10;
            btn.style.paddingRight = iconOnly ? 6 : 10;

            // Hover feedback
            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverColor);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = color);

            // ---- Content ----
            if (icon != null)
            {
                var img = new Image
                {
                    image = icon,
                    scaleMode = ScaleMode.ScaleToFit
                };
                img.style.width = 16;
                img.style.height = 16;
                img.style.marginRight = string.IsNullOrEmpty(text) ? 0 : 6; // gap only when text exists
                btn.Add(img);
            }

            if (!string.IsNullOrEmpty(text))
            {
                var lbl = new Label(text)
                {
                    style =
                    {
                        unityTextAlign = TextAnchor.MiddleLeft,
                        color = TextColorLight
                    }
                };
                btn.Add(lbl);
            }

            return btn;
        }

        // Positions a dropdown flush under a target (no gap) and shows it
        void OpenDropdownFrom(VisualElement button, VisualElement dd)
        {
            if (dd == null) return;

            CloseAllDropdowns();
            // SetElement(overlay, true);
            
            /#1#/ Hide others
            if (dd != nav_createDropdown)  nav_createDropdown.style.display  = DisplayStyle.None;
            if (dd != nav_developDropdown) nav_developDropdown.style.display = DisplayStyle.None;
            if (dd != nav_searchDropdown)  nav_searchDropdown.style.display  = DisplayStyle.None;#1#

            if (dd.style.display == DisplayStyle.Flex)
            {
                dd.style.display = DisplayStyle.None;
                // SetElement(overlay, false);
                return;
            }

            var targetWB   = button.worldBound;
            var wrapOrigin = overlay.worldBound.position;
            dd.style.left = targetWB.xMin - wrapOrigin.x;
            dd.style.top  = targetWB.yMax - wrapOrigin.y; // flush (prevents tiny dead zone)
            dd.style.display = DisplayStyle.Flex;
        }
        
        #endregion
        
        #region Building

        VisualElement BuildNavigationBar()
        {
            // === Wrapper that holds the bar and absolute-positioned dropdowns ===
            var _navWrap = new VisualElement
            {
                name = "navWrap",
                style =
                {
                    position = Position.Relative,
                    flexDirection = FlexDirection.Column,
                    display = DisplayStyle.None, // shown only on Home/Creator/Developer
                    // paddingTop = 6, paddingBottom = 6, paddingLeft = 8, paddingRight = 8
                }
            };

            // === Custom styled bar (card-like, matches landing/dropdowns) ===
            var bar = new VisualElement
            {
                name = "navBarCard",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = BackgroundColorDeepDark,
                    // square edges so it looks like a single rectangle across the top
                    borderTopLeftRadius = 0, borderTopRightRadius = 0,
                    borderBottomLeftRadius = 0, borderBottomRightRadius = 0,

                    borderTopWidth = 0, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = EdgeColorDark,
                    borderBottomColor = EdgeColorDark,
                    borderLeftColor = EdgeColorDark,
                    borderRightColor = EdgeColorDark,

                    // remove any outer margins and padding that create a gap
                    marginLeft = 0, marginRight = 0, marginTop = 0, marginBottom = 0,
                    paddingLeft = 6, paddingRight = 6, paddingTop = 6, paddingBottom = 6,

                    // force it to span the full window width
                    width = Length.Percent(100)
                }
            };
            _navWrap.Add(bar);

            // ---------- Left group (Home / Create / Develop) ----------
            var left = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            bar.Add(left);

            // var icon = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;
            // var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/FESGASEditor/Icons/home.png");

            nav_createWrap = new VisualElement()
            {
                style =
                {
                    display = DisplayStyle.Flex,
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.Center
                }
            };

            nav_homeButton = MakeNavButton(icon_HOME, OnHomeClicked_Nav, PrimaryButtonColor, PrimaryButtonColorHover, "Return to home");
            //nav_homeButton = PrimaryButton("", OnHomeClicked_Nav).InsertIcon(icon_HOME) as Button;
            nav_createButton = MakeNavButton("Create", OnCreateClicked_Nav, SecondaryButtonColor, SecondaryButtonColorHover);
            nav_createShortcut = MakeNavButton(">", () => { }, SecondaryButtonColor, SecondaryButtonColorHover);
            nav_developButton = MakeNavButton("Develop", OnDevelopClicked_Nav, SecondaryButtonColor, SecondaryButtonColorHover);
            nav_setupButton = MakeNavButton(icon_SETTINGS, OnDevelopClicked_Nav, SecondaryButtonColor, SecondaryButtonColorHover);
            
            nav_createWrap.Add(nav_createButton);
            nav_createWrap.Add(nav_createShortcut);
            
            left.Add(nav_homeButton);
            left.Add(nav_createWrap);
            left.Add(nav_developButton);
            left.Add(nav_setupButton);

            // ---------- Spacer ----------
            var spacer = new VisualElement { style = { flexGrow = 1 } };
            bar.Add(spacer);

            // ---------- Search ----------
            // A small container so we can pad/round the TextField like our cards
            var searchWrap = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = BackgroundColorDark,
                    borderTopLeftRadius = 8, borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = EdgeColorDark,
                    borderBottomColor = EdgeColorDark,
                    borderLeftColor = EdgeColorDark,
                    borderRightColor = EdgeColorDark,
                    paddingLeft = 6, paddingRight = 6,
                    marginLeft = 0, marginRight = 0, marginTop = 0, marginBottom = 0
                }
            };
            bar.Add(searchWrap);

            nav_searchField = new TextField { name = "FES-SearchField" };
            nav_searchField.style.width = 320;
            nav_searchField.style.marginTop = 2;
            nav_searchField.style.marginBottom = 2;
            nav_searchField.label = "";
            nav_searchField.style.color = BackgroundColorSub;
            searchWrap.Add(nav_searchField);

            // Open results on focus/click
            nav_searchField.RegisterCallback<FocusInEvent>(_ =>
            {
                OpenSearchDropdown_Nav();
                if (string.IsNullOrEmpty(nav_searchField.value)) ShowNoSearchResults_Nav();
            });
            // Value change → (re)query
            nav_searchField.RegisterValueChangedCallback(OnSearchChanged_Nav);

            // ---------- Options menu (custom-styled button + Unity menu) ----------
            var optionsBtn = BuildOptionsMenu_Nav();
            bar.Add(optionsBtn);

            return _navWrap;

            // ---------- Local builders for the three dropdowns with matching style ----------
        }

        VisualElement BuildCreateDropdown_Nav()
        {
            float width = 3 * DropdownColumnWidth;
            var dd = NewDropdownBase(width); // card style consistent with landing
            /*dd.style.width = width;
            dd.style.minWidth = width;
            dd.style.maxWidth = width;#1#
            
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            dd.Add(row);

            var left  = NewColumn(DropdownColumnWidth, DropdownColumnWidthSmall, DropdownColumnWidthLarge, 0f);
            var right = NewColumn(DropdownColumnWidth, DropdownColumnWidth, DropdownColumnWidth * 2);
            
            row.Add(left); row.Add(right);
            
            // LEFT: Data + Keys + Special
            left.Add(BuildHeader("Data"));
            left.Add(BuildIconTextItem(new MenuEntry(icon_ABILITY, "Ability",  () => LoadIntoCreator(EDataType.Ability))));
            left.Add(BuildIconTextItem(new MenuEntry(icon_EFFECT, "Effect",   () => LoadIntoCreator(EDataType.Effect))));
            left.Add(BuildIconTextItem(new MenuEntry(icon_ENTITY, "Entity",   () => LoadIntoCreator(EDataType.Entity))));
            left.Add(BuildSeparator());
            left.Add(BuildHeader("Keys"));
            left.Add(BuildIconTextItem(new MenuEntry(icon_ATTRIBUTE, "Attribute",() => LoadIntoCreator(EDataType.Attribute))));
            left.Add(BuildIconTextItem(new MenuEntry(icon_TAG, "Tag",      () => LoadIntoCreator(EDataType.Tag))));
            left.Add(BuildSeparator());
            left.Add(BuildHeader("Special"));
            left.Add(BuildIconTextItem(new MenuEntry(icon_ATTRIBUTE_SET, "Attribute Set", () => LoadIntoCreator(EDataType.AttributeSet))));

            // RIGHT: Proxy Task (search + recent)
            right.Add(BuildHeader("Proxy Task"));

            nav_proxySearchField = new TextField { tooltip = "Search Proxy Tasks" };
            nav_proxySearchField.style.marginTop = 4;
            nav_proxySearchField.style.width = Length.Percent(100);
            nav_proxySearchField.style.flexGrow = 0;
            // nav_proxySearchField.RegisterCallback<PointerUpEvent>(e => e.StopImmediatePropagation());
            nav_proxySearchField.RegisterValueChangedCallback(evt => RefreshProxyTaskList(evt.newValue));
            right.Add(nav_proxySearchField);

            nav_proxyListView = new ListView
            {
                selectionType = SelectionType.Single,
                // Use fixed-height rows so each item actually takes vertical space
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                //fixedItemHeight = -1,

                style =
                {
                    height = ListviewMinHeight,      // you already have this constant
                    marginTop = 4,
                    width = Length.Percent(100),
                    // Keep list content clipped so it doesn’t draw over the column header
                    overflow = Overflow.Hidden
                }
            };
            // nav_proxyListView.makeItem = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 8, paddingRight = 8 } };
            nav_proxyListView.makeItem = () =>
            {
                // row container
                var r = new VisualElement
                {
                    name = "ProxyRow",
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        height = ListviewItemMinHeight,                    // critical: gives the row vertical space
                        paddingLeft = 6, paddingRight = 6
                    }
                };

                // optional icon (hidden for normal items; shown for Recent)
                var _icon = new Image
                {
                    name = "Icon",
                    scaleMode = ScaleMode.ScaleToFit,
                    style = { width = 14, height = 14, marginRight = 6, display = DisplayStyle.None }
                };
                r.Add(_icon);

                // main label
                var label = new Label
                {
                    name = "Text",
                    style =
                    {
                        flexGrow = 1,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        whiteSpace = WhiteSpace.NoWrap,
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis
                    }
                };
                r.Add(label);

                // small clear button (only visible for the “Recent” header row)
                var clearBtn = new Button { name = "ClearRecent", text = "×" };
                clearBtn.style.width = 18; clearBtn.style.height = 18;
                clearBtn.style.marginLeft = 6;
                clearBtn.style.display = DisplayStyle.None; // hidden by default
                clearBtn.clicked += OnClearRecentClicked;
                r.Add(clearBtn);
                
                // dedicated separator element (hidden by default)
                var sep = new VisualElement
                {
                    name = "Sep",
                    style = { height = 1, flexGrow = 1, backgroundColor = new Color(0,0,0,0.35f), display = DisplayStyle.None }
                };
                r.Add(sep);

                return r;
            };
            
            nav_proxyListView.bindItem = (r, idx) =>
            {
                var rows = nav_proxyListView.itemsSource as List<ProxyRow>;
                if (rows == null || idx < 0 || idx >= rows.Count) return;
                var data = rows[idx];

                // Ensure expected children exist (in case Unity recycled one from older code)
                var icon    = r.Q<Image>("Icon") ?? new Image { name = "Icon" };
                var label   = r.Q<Label>("Text") ?? new Label   { name = "Text" };
                var clearBt = r.Q<Button>("ClearRecent") ?? new Button { name = "ClearRecent", text = "×" };
                var sep     = r.Q<VisualElement>("Sep") ?? new VisualElement { name = "Sep" };

                if (icon.parent == null)    r.Insert(0, icon);
                if (label.parent == null)   r.Add(label);
                if (clearBt.parent == null) r.Add(clearBt);
                if (sep.parent == null)     r.Add(sep);
                
                label.style.display  = DisplayStyle.Flex;   // default for non-separator rows
                icon.style.display   = DisplayStyle.None;
                clearBt.style.display= DisplayStyle.None;
                sep.style.display    = DisplayStyle.None;

                // reset visuals every bind
                r.style.height = ListviewItemMinHeight;
                r.style.paddingLeft = 6; r.style.paddingRight = 6;
                r.pickingMode = PickingMode.Position;
                r.focusable = false;
                r.SetEnabled(true);

                icon.style.display = DisplayStyle.None;
                clearBt.style.display = DisplayStyle.None;
                sep.style.display = DisplayStyle.None;

                label.style.color = Color.white;
                label.style.unityFontStyleAndWeight = FontStyle.Normal;

                // avoid stacking multiple handlers
                clearBt.clicked -= OnClearRecentClicked;
                r.UnregisterCallback<PointerDownEvent>(StopSelect);

                switch (data.Type)
                {
                    case ProxyRowType.Header:
                        label.text = "Recent";
                        label.style.unityFontStyleAndWeight = FontStyle.Bold;
                        clearBt.style.display = DisplayStyle.Flex;
                        clearBt.clicked += OnClearRecentClicked;

                        // non-interactive
                        r.RegisterCallback<PointerDownEvent>(StopSelect);
                        break;

                    case ProxyRowType.RecentItem:
                        label.text = data.Text;
                        if (icon_RECENT != null)
                        {
                            icon.image = icon_RECENT;
                            icon.style.display = DisplayStyle.Flex;
                            icon.style.unityBackgroundImageTintColor = Color.white;
                        }
                        break;

                    case ProxyRowType.Separator:
                        // make the row a 1px line, full width
                        r.style.height = 2;
                        r.style.paddingLeft = 0;
                        r.style.paddingRight = 0;

                        // HIDE other children so they don't consume width
                        label.style.display = DisplayStyle.None;
                        icon.style.display  = DisplayStyle.None;
                        clearBt.style.display = DisplayStyle.None;

                        // show the line
                        sep.style.display = DisplayStyle.Flex;
                        sep.style.height = 2;
                        sep.style.flexGrow = 1;
                        // ensure no margins clip it
                        sep.style.marginLeft = 0;
                        sep.style.marginRight = 0;

                        // non-interactive
                        r.RegisterCallback<PointerDownEvent>(e => e.StopImmediatePropagation());
                        break;

                    case ProxyRowType.TaskItem:
                        label.text = data.Text;
                        break;

                    case ProxyRowType.Placeholder:
                        label.text = data.Text;
                        label.style.unityFontStyleAndWeight = FontStyle.Italic;
                        label.style.color = new Color(0.7f,0.7f,0.8f,1f);
                        r.SetEnabled(false);
                        r.RegisterCallback<PointerDownEvent>(StopSelect);
                        break;
                }

                void StopSelect(PointerDownEvent e) => e.StopImmediatePropagation();
            };

            
            nav_proxyListView.selectionChanged += objs =>
            {
                var r = objs.FirstOrDefault() as ProxyRow;
                if (r == null)
                {
                    nav_proxyListView.ClearSelection();
                    return;
                }

                if (r.Type is ProxyRowType.Header or ProxyRowType.Separator or ProxyRowType.Placeholder)
                {
                    nav_proxyListView.ClearSelection();
                    return;
                }

                // valid selection (RecentItem or TaskItem)
                var sel = r;
                if (string.IsNullOrEmpty(sel.Text)) { nav_proxyListView.ClearSelection(); return; }

                // MRU update
                // nav_recentProxyTasks.RemoveAll(t => t.Id == sel.Id);
                nav_recentProxyTasks.Insert(0, sel);
                if (nav_recentProxyTasks.Count > nav_maxRecentProxy) nav_recentProxyTasks.RemoveAt(nav_recentProxyTasks.Count - 1);
                
                CloseAllDropdowns();
                RefreshProxyTaskList("");
                
                // SetCreatorPage(sel.Id, sel.Text, sel.DataType);
            };

            right.Add(nav_proxyListView);

            // First populate (recent if empty)
            RefreshProxyTaskList("");

            return dd;
            
            void OnClearRecentClicked()
            {
                nav_recentProxyTasks.Clear();
                RefreshProxyTaskList(nav_proxySearchField?.value ?? "");
            }

            void RefreshProxyTaskList(string query)
            {
                // Normalize / prepare source
                var q = (query ?? "").Trim();
                var qLower = q.ToLowerInvariant();

                // Base task catalog in strict alphabetical order
                // (Replace 'allProxyTasks' with your actual source if it’s named differently)
                var allAlpha = TypePickerCache.GetConcreteTypesAssignableTo<AbstractProxyTask>()
                    .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // If there is a query, filter the alpha list
                if (!string.IsNullOrEmpty(qLower)) allAlpha = allAlpha.Where(t => t != null && t.Name.ToLowerInvariant().Contains(qLower)).ToList();

                // Build rows
                var rows = new List<ProxyRow>();

                // RECENTS (only if there are any, and only when query is empty)
                // If you want recents to still show while typing (filtered too), remove the 'qLower.Length == 0' condition.
                if (nav_recentProxyTasks.Count > 0)
                {
                    var keepRecent = new List<ProxyRow>();
                    foreach (var r in nav_recentProxyTasks)
                        keepRecent.Add(r);

                    if (keepRecent.Count > 0)
                    {
                        rows.Add(ProxyRow.Header("Recently Used"));
                        rows.AddRange(keepRecent);
                        rows.Add(ProxyRow.Separator());
                    }
                }

                // TASKS
                if (allAlpha.Count > 0)
                {
                    foreach (var t in allAlpha)
                        rows.Add(ProxyRow.TaskItem(ObjectNames.NicifyVariableName(t.Name)));
                }
                else
                {
                    // No matches → placeholder (non-interactive)
                    rows.Add(ProxyRow.Placeholder("No items found"));
                }

                nav_proxyListView.itemsSource = rows;
                nav_proxyListView.Rebuild();
                nav_proxyListView.ClearSelection(); // ensure nothing is highlighted
            }
        }

        VisualElement BuildDevelopDropdown_Nav()
        {
            var dd = NewDropdownBase(minWidth: 3 * DropdownColumnWidth);
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            dd.Add(row);

            var a = NewColumn(); var b = NewColumn(); var c = NewColumn();
            row.Add(a); row.Add(b); row.Add(c);

            var icon = EditorGUIUtility.IconContent("d_Folder Icon").image as Texture2D;

            a.Add(BuildHeader("Build"));
            a.Add(BuildIconTextItem(new MenuEntry(icon, "Content Build", () => Debug.Log("Content Build"))));
            a.Add(BuildIconTextItem(new MenuEntry(icon, "Rebuild All",   () => Debug.Log("Rebuild All"))));
            a.Add(BuildIconTextItem(new MenuEntry(icon, "Clean Build",   () => Debug.Log("Clean Build"))));

            b.Add(BuildHeader("Validate"));
            b.Add(BuildIconTextItem(new MenuEntry(icon, "Validate Project", () => Debug.Log("Validate Project"))));
            b.Add(BuildIconTextItem(new MenuEntry(icon, "Lint Data",        () => Debug.Log("Lint Data"))));
            b.Add(BuildIconTextItem(new MenuEntry(icon, "Find Duplicates",  () => Debug.Log("Find Duplicates"))));

            c.Add(BuildHeader("Utilities"));
            c.Add(BuildIconTextItem(new MenuEntry(icon, "Run Tests",     () => Debug.Log("Run Tests"))));
            c.Add(BuildIconTextItem(new MenuEntry(icon, "Open Logs",     () => Debug.Log("Open Logs"))));
            c.Add(BuildIconTextItem(new MenuEntry(icon, "Export Report", () => Debug.Log("Export Report"))));

            return dd;
        }

        VisualElement BuildSearchDropdown_Nav()
        {
            var dd = NewDropdownBase(minWidth: DropdownColumnWidthLarge);

            nav_searchList = new ListView
            {
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.Single,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
                style = { width = DropdownColumnWidthLarge, height = ListviewMinHeight }
            };
            nav_searchList.makeItem = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft, paddingLeft = 8, paddingRight = 8 } };
            nav_searchList.bindItem = (e, i) =>
            {
                if (i < 0 || i >= nav_searchItems.Count) return;
                var item = nav_searchItems[i];
                ((Label)e).text = item.Display;
                e.SetEnabled(!item.IsPlaceholder);
            };
            nav_searchList.selectionChanged += OnSearchItemChosen_Nav;
            
            var scroll = nav_searchList.Q<ScrollView>("unity-vertical-scrollbar");
            if (scroll != null)
            {
                scroll.style.backgroundColor = BackgroundColorDark;
            }

            dd.Add(nav_searchList);
            return dd;
        }

        VisualElement BuildOptionsMenu_Nav()
        {
            var optionsBtn = MakeNavButton("Options", null, SecondaryButtonColor, SecondaryButtonColorHover);
            
            // Build a Unity menu and open it on click
            var optionsMenu = new GenericMenu();
            optionsMenu.AddItem(new GUIContent("Open Framework…"), false, () =>
            {
                var path = ForgePaths.FrameworkFolder(Project.MetaName);
                if (Directory.Exists(path)) EditorUtility.RevealInFinder(path);
            });
            optionsMenu.AddItem(new GUIContent("Save Framework"), false, () => SaveFramework());
            optionsMenu.AddSeparator("");
            optionsMenu.AddItem(new GUIContent("Settings"), false, OpenSettings);
            optionsMenu.AddItem(new GUIContent("Documentation"), false, () => Application.OpenURL("https://example.com/docs"));
            optionsMenu.AddItem(new GUIContent("About"), false, OnClick_AboutFESGAS);

            optionsBtn.RegisterCallback<MouseUpEvent>(evt =>
            {
                var r = new Rect(evt.mousePosition, Vector2.zero);
                optionsMenu.DropDown(r);
            });

            return optionsBtn;

            void OpenSettings()
            {
                PlayForgeSettingsWindow.Open(_projectFilePath, LoadLocalSettings, PlayForgeSettingsWindow.SettingsPage.Framework, Project);
            }
        }

        void LoadIntoCreator(EDataType kind)
        {
            CloseAllDropdowns();
            SetCreatorPage(new CreatorItem
                (
                    kind,
                    Project.BuildNode(DataIdRegistry.Generate(), $"Unnamed {DataTypeText(kind)}", kind)
                )
            );
        }
        
        #endregion
        
        #region Functionality
        
        #region Buttons
        
        void OnHomeClicked_Nav()
        {
            SetPage(activePage == GasifyPage.Home ? GasifyPage.Landing : GasifyPage.Home);
        }

        void OnCreateClicked_Nav()
        {
            
            OpenDropdownFrom(nav_createButton, nav_createDropdown);
        }

        void OnCreateShortcutClicked_Nav()
        {
            
        }

        void RefreshNavigationBar()
        {
            SetCreateShortcut();
        }
        
        void SetCreateShortcut()
        {
            if (activePage == GasifyPage.Home && creator_lastFocus.Kind != EDataType.None)
            {
                nav_createButton.style.borderTopRightRadius = 0;
                nav_createButton.style.borderBottomRightRadius = 0;
                nav_createButton.style.paddingRight = 8;
                nav_createButton.style.marginRight = 0;
                nav_createButton.style.alignItems = Align.Center;
                
                nav_createShortcut.style.display = DisplayStyle.Flex;
                nav_createShortcut.style.borderBottomLeftRadius = 0;
                nav_createShortcut.style.borderTopLeftRadius = 0;
                nav_createShortcut.style.paddingLeft = 8;
                nav_createShortcut.style.marginLeft = 0;
                nav_createShortcut.style.minWidth = 25;
                nav_createShortcut.style.alignItems = Align.Center;

                nav_createShortcut.text = ">";

                nav_createShortcut.AttachTooltip($"Edit {DataTypeText(creator_lastFocus.Kind)} {Quotify(creator_lastFocus.Name)}");

                nav_createShortcut.clicked += SetCreatorPageLastFocus;
                nav_createShortcut.clicked -= SetCreatorPageAgain;

            }
            else if (activePage == GasifyPage.Creator && creator_focus.Kind != EDataType.None)
            {
                nav_createButton.style.borderTopRightRadius = 0;
                nav_createButton.style.borderBottomRightRadius = 0;
                nav_createButton.style.paddingRight = 8;
                nav_createButton.style.marginRight = 0;
                nav_createButton.style.alignItems = Align.Center;
                
                nav_createShortcut.style.display = DisplayStyle.Flex;
                nav_createShortcut.style.borderBottomLeftRadius = 0;
                nav_createShortcut.style.borderTopLeftRadius = 0;
                nav_createShortcut.style.paddingLeft = 8;
                nav_createShortcut.style.marginLeft = 0;
                nav_createShortcut.style.minWidth = 25;
                nav_createShortcut.style.alignItems = Align.Center;

                nav_createShortcut.text = "+";
                nav_createShortcut.AttachTooltip($"Create another {DataTypeText(creator_focus.Kind)}");
                
                nav_createShortcut.clicked -= SetCreatorPageLastFocus;
                nav_createShortcut.clicked += SetCreatorPageAgain;
            }
            else
            {
                nav_createButton.style.borderTopRightRadius = 8;
                nav_createButton.style.borderBottomRightRadius = 8;
                nav_createButton.style.paddingRight = 8;
                nav_createButton.style.marginRight = 6;
                nav_createButton.style.alignItems = Align.Center;

                nav_createShortcut.style.display = DisplayStyle.None;

                nav_createShortcut.AttachTooltip("");
            }
        }

        void OnDevelopClicked_Nav()
        {
            OpenDropdownFrom(nav_developButton, nav_developDropdown);
        }

        void OnSetupClicked_Nav()
        {
            
        }
        
        #endregion

        #region Search
        
        void OpenSearchDropdown_Nav()
        {
            OpenDropdownFrom(nav_searchField, nav_searchDropdown);
            SetElement(overlay, true);
            
            // Size the list
            nav_searchList.style.width = nav_searchField.worldBound.width;
            nav_searchList.style.height = ListviewMinHeight;

            // Populate with current query
            RefreshSearchResults_Nav(nav_searchField.value);
        }

        void ShowNoSearchResults_Nav()
        {
            
        }

        void CloseSearchDropdown_Nav()
        {
            nav_searchDropdown.style.display = DisplayStyle.None;
            nav_searchList.ClearSelection();
            
            CloseAllDropdowns();
        }

        void OnSearchChanged_Nav(ChangeEvent<string> evt)
        {
            if (nav_searchDropdown.style.display == DisplayStyle.None) OpenSearchDropdown_Nav();
            RefreshSearchResults_Nav(evt.newValue ?? "");
        }

        void RefreshSearchResults_Nav(string query)
        {
            var q = (query ?? "").Trim().ToLowerInvariant();

            // Collect across your domains; weight/display as you like
            var items = new List<SearchItem>();

            foreach (var a in Project.Abilities)
                if (Matches(a.Name, a.Id, q))
                    items.Add(new SearchItem($"{a.Name} • Ability", a.Id, EDataType.Ability));

            foreach (var at in Project.Attributes)
                if (Matches(at.Name, at.Id, q))
                    items.Add(new SearchItem($"{at.Name} • Attribute", at.Id, EDataType.Attribute));
            
            foreach (var t in Project.AttributeSets)
                if (Matches(t.Name, t.Id, q))
                    items.Add(new SearchItem($"{t.Name} • Tag", t.Id, EDataType.AttributeSet));
            
            foreach (var t in Project.Effects)
                if (Matches(t.Name, t.Id, q))
                    items.Add(new SearchItem($"{t.Name} • Tag", t.Id, EDataType.Effect));
            
            foreach (var t in Project.Entities)
                if (Matches(t.Name, t.Id, q))
                    items.Add(new SearchItem($"{t.Name} • Tag", t.Id, EDataType.Entity));
            
            foreach (var t in Project.Tags)
                if (Matches(t.Name, t.Id, q))
                    items.Add(new SearchItem($"{t.Name} • Tag", t.Id, EDataType.Tag));

            // Sort (Abilities first, then Tags, then Attributes, then by name)
            nav_searchItems = items
                .OrderBy(it => it.KindOrder)
                .ThenBy(it => it.Display, StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            if (nav_searchItems.Count == 0) nav_searchItems.Add(new SearchItem("No search results", -1, EDataType.None, true));

            nav_searchList.itemsSource = nav_searchItems;
            nav_searchList.Rebuild();
        }

        static bool Matches(string name, int id, string q)
        {
            if (string.IsNullOrEmpty(q)) return true;
            return (!string.IsNullOrEmpty(name) && name.ToLowerInvariant().Contains(q)) ||
                   (id > 0 && id.ToString().Contains(q));
        }

        void OnSearchItemChosen_Nav(IEnumerable<object> selection)
        {
            var item = selection?.FirstOrDefault() as SearchItem;
            if (item == null) return;

            if (item.IsPlaceholder) return;
            
            // Navigate depending on item.Kind (here we just go to Home and pretend to open it)
            SetPage(GasifyPage.Home);
            // TODO: tell your Home/Creator page to focus the selected object by ID (item.Id)
            CloseSearchDropdown_Nav();
        }
        
        #endregion
        
        #endregion
        
        #endregion
        
        #region Landing Page

        VisualElement BuildLandingPage()
        {
            var root = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };

            // Center wrapper
            var center = new VisualElement
            {
                style = { flexGrow = 1, justifyContent = Justify.Center, alignItems = Align.Center, paddingLeft = 10, paddingRight = 10 }
            };
            root.Add(center);

            // Card
            landing_card = new VisualElement
            {
                style =
                {
                    width = 420, maxWidth = 520,
                    paddingTop = 16, paddingBottom = 16, paddingLeft = 16, paddingRight = 16,
                    backgroundColor = BackgroundColorDark,
                    borderTopLeftRadius = 12, borderTopRightRadius = 12,
                    borderBottomLeftRadius = 12, borderBottomRightRadius = 12,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            center.Add(landing_card);

            var _title = new Label("Gasify Editor Suite")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 18, marginBottom = 8, color = Color.white }
            };
            landing_card.Add(_title);

            var subtitle = new Label("Create, manage and build FESGAS framework data")
            {
                style = { fontSize = 12, marginBottom = 10, color = TextColorLight }
            };
            landing_card.Add(subtitle);

            var col = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            landing_card.Add(col);

            col.Add(PrimaryButton("Create", OnCreateClicked_Landing));

            landing_loadButton = PrimaryButton("Load", OnLoadClicked_Landing);
            landing_quickLoadButton = PrimaryButton("\u2192", OnQuickLoadClicked_Landing);

            landing_loadButton.style.marginRight = 0;
            landing_quickLoadButton.style.marginLeft = 0;
            
            landing_loadButton.style.borderTopRightRadius    = 0;
            landing_loadButton.style.borderBottomRightRadius = 0;
            landing_quickLoadButton.style.borderTopLeftRadius    = 0;
            landing_quickLoadButton.style.borderBottomLeftRadius = 0;

            // Decide visibility / width split
            var lastOpened = MasterSetting(ForgeTags.Settings.ACTIVE_FRAMEWORK, fallback: "");
            if (string.IsNullOrEmpty(lastOpened))
            {
                // No remembered project → hide quick-load and let Load take the full row
                landing_quickLoadButton.style.display = DisplayStyle.None;
                landing_loadButton.style.flexGrow = 1f;
                
                landing_loadButton.style.borderTopRightRadius    = defaultRadius;
                landing_loadButton.style.borderBottomRightRadius = defaultRadius;
                
                landing_loadButton.style.paddingLeft = 0;
            }
            else
            {
                landing_quickLoadButton.style.display = DisplayStyle.Flex;
                landing_quickLoadButton.tooltip = $"Quick load \"{lastOpened}\"";
                // 80 / 20 split via flex-grow (4:1)
                landing_loadButton.style.flexGrow      = 6f;
                landing_quickLoadButton.style.flexGrow = 1f;
                
                landing_loadButton.style.borderTopRightRadius    = 0;
                landing_loadButton.style.borderBottomRightRadius = 0;

                landing_loadButton.style.paddingLeft = 67;
            }

            var loadRow = ButtonRow(landing_loadButton, landing_quickLoadButton);
            col.Add(loadRow);
            
            //col.Add(PrimaryButton("Load", OnLoadClicked_Landing));
            col.Add(SecondaryButton("Documentation", OnDocumentationClicked_Landing));
            col.Add(SecondaryButton("About", OnClick_AboutFESGAS));
            col.Add(SecondaryButton("Settings", OnSettingsClicked_Landing));

            // Disclaimer pinned bottom
            landing_disclaimer = new Label("Disclaimer: This tool is pre-release. Data formats may change. Back up your project before use. Thank you for using FESGAS.")
            {
                style = {
                    color = TextColorSub, fontSize = 11,
                    marginTop = 8, marginBottom = 8, marginLeft = 8, marginRight = 8,
                    whiteSpace = WhiteSpace.Normal,
                    alignSelf = Align.FlexStart,
                    justifyContent = Justify.FlexStart
                }
            };
            root.Add(landing_disclaimer);

            landing_createCard = BuildCreatePopup_Landing();
            landing_openPopup = BuildOpenPopup_Landing();

            root.Add(landing_createCard);
            root.Add(landing_openPopup);
            
            return root;
        }
        
        #region Create
        
        // Simple helper to build a “subheader” row
        private Label SubHeader(string text) => new Label(text)
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 12,
                marginTop = 8, marginBottom = 6,
                color = Color.white
            }
        };

// Multi-select list made of Toggles
        private ScrollView BuildToggleList(List<string> items, HashSet<string> selected, Action onChanged = null)
        {
            var sv = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { height = 160, backgroundColor = BackgroundColorDark * .65f, borderTopLeftRadius = 6, borderTopRightRadius = 6, borderBottomLeftRadius = 6, borderBottomRightRadius = 6, paddingTop = 6, paddingBottom = 6, paddingLeft = 6, paddingRight = 6 }
            };

            foreach (var _name in items)
            {
                var t = new Toggle(_name)
                {
                    value = selected.Contains(_name),
                    style = { marginBottom = 4 }
                };
                t.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) selected.Add(_name);
                    else selected.Remove(_name);
                    onChanged?.Invoke();
                });
                sv.Add(t);
            }
            return sv;
        }

        VisualElement BuildCreatePopup_Landing()
        {
            // Overlay wrapper that fills the window and centers the card
            var _overlay = new VisualElement
            {
                name = "CreateOverlay",
                pickingMode = PickingMode.Position,
                style =
                {
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    display = DisplayStyle.None, // hidden until "Create"
                    justifyContent = Justify.Center,
                    alignItems = Align.Center,
                    paddingLeft = 10, paddingRight = 10
                }
            };

            // Card
            var card = new VisualElement
            {
                name = "CreateCard",
                style =
                {
                    width = 560, maxWidth = 600,
                    paddingTop = 16, paddingBottom = 16, paddingLeft = 16, paddingRight = 16,
                    backgroundColor = BackgroundColorDark,
                    borderTopLeftRadius = 12, borderTopRightRadius = 12,
                    borderBottomLeftRadius = 12, borderBottomRightRadius = 12
                }
            };
            _overlay.Add(card);

            landing_createCard = _overlay; // overlay is what we toggle

            // Header
            card.Add(BuildHeader("Create New Framework"));

            // Body
            var body = new VisualElement { style = { flexDirection = FlexDirection.Column } };
            card.Add(body);

            // Name
            body.Add(SubHeader("Name"));
            landing_create_nameField = new TextField { value = "", style = { marginBottom = 6 } };
            landing_create_nameField.RegisterCallback<FocusOutEvent>(_ =>
            {
                landing_create_nameField.value = (landing_create_nameField.value ?? "").Trim();
            });
            body.Add(landing_create_nameField);

            // Mode
            body.Add(SubHeader("Settings & Templates"));
            var modePopup = new PopupField<string>(
                "Mode",
                new List<string> { "Use Default", "Combine From Existing Frameworks" },
                landing_createFrameworkMode == CreateFrameworkMode.CreateNew ? 0 : 1
            );
            modePopup.RegisterValueChangedCallback(evt =>
            {
                bool useDefault = evt.newValue.StartsWith("Use Default");
                landing_createFrameworkMode = useDefault ? CreateFrameworkMode.CreateNew : CreateFrameworkMode.FromExisting;
                landing_fwGrid.style.display = useDefault ? DisplayStyle.None : DisplayStyle.Flex;
                landing_settingsPolicyPopup.style.display = useDefault ? DisplayStyle.None : DisplayStyle.Flex;
            });
            body.Add(modePopup);

            // Conflict policy (applies to both settings + template merges)
            landing_settingsPolicyPopup = new PopupField<string>(
                "On Conflict",
                new List<string> { "Prefer Earlier (top of list)", "Prefer Later (bottom of list)" },
                landing_settingsPolicy == MergePolicy.PreferEarlier ? 0 : 1
            );
            landing_settingsPolicyPopup.style.display = DisplayStyle.None;
            landing_settingsPolicyPopup.RegisterValueChangedCallback(evt =>
            {
                landing_settingsPolicy = evt.newValue.StartsWith("Prefer Earlier")
                    ? MergePolicy.PreferEarlier : MergePolicy.PreferLater;
                // purely semantic; ordering is from landing_fwKeys
            });
            body.Add(landing_settingsPolicyPopup);

            // Framework grid (Name | Template | Settings)
            landing_fwGrid = BuildFrameworkPickerGrid();
            landing_fwGrid.style.display = DisplayStyle.None; // off until Combine mode
            body.Add(landing_fwGrid);

            // Footer
            var footer = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd, marginTop = 10, paddingTop = 5, borderTopColor = EdgeColorDark, borderTopWidth = 2, marginRight = 0, marginLeft = 0} };
            var cancel = SecondaryButton("Cancel", () =>
            {
                landing_createCard.style.display = DisplayStyle.None;
                RefreshLandingPage();
            });
            var create = PrimaryButton("Create", OnConfirmCreate_Landing);
            create.style.maxHeight = secondaryButtonHeight;
            footer.Add(cancel);
            footer.Add(create);
            card.Add(footer);

            return _overlay;
        }

        void RefreshOpenPage_Landing()
        {
            
        }
        
        VisualElement BuildFrameworkPickerGrid()
        {
            landing_fwGrid?.Clear();
            
            // Reset selection state
            landing_fwKeys = SafeListFrameworkKeys();
            landing_fwUseTemplate.Clear();
            landing_fwUseSettings.Clear();
            foreach (var k in landing_fwKeys)
            {
                landing_fwUseTemplate[k] = false;
                landing_fwUseSettings[k] = false;
            }

            const int ColWidth = 120; // width for Template/Settings columns

            // Container
            var wrap = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    backgroundColor = BackgroundColorDark * .65f,
                    borderTopLeftRadius = 6, borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
                    paddingTop = 6, paddingBottom = 6, paddingLeft = 6, paddingRight = 6
                }
            };

            // ---------- Header row ----------
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };

            Label H(string t) => new Label(t)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = Color.white,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            // Name column (flex)
            var colName = new VisualElement { style = { flexGrow = 1 } };
            colName.Add(H("Name"));
            header.Add(colName);

            // Template column (label + tiny toggle centered as a unit)
            var colTpl = new VisualElement
            {
                style =
                {
                    width = ColWidth,
                    alignItems = Align.Center,      // center the pair horizontally
                    justifyContent = Justify.Center
                }
            };
            var tplHeader = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            tplHeader.Add(new Label("Templates")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, color = Color.white, marginRight = 0 }
            });
            var allTpl = new Toggle()
            {
                tooltip = "Select all Templates",
                style = { width = 14, height = 14, marginLeft = 2, marginTop = 0, alignSelf = Align.Center }
            };
            tplHeader.Add(allTpl);
            colTpl.Add(tplHeader);
            header.Add(colTpl);

            // Settings column (label + tiny toggle centered)
            var colSet = new VisualElement
            {
                style =
                {
                    width = ColWidth,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            var setHeader = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            setHeader.Add(new Label("Settings")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, color = Color.white, marginRight = 0 }
            });
            var allSet = new Toggle()
            {
                tooltip = "Select all Settings",
                style = { width = 14, height = 14, marginLeft = 2, marginTop = 0, alignSelf = Align.Center }
            };
            setHeader.Add(allSet);
            colSet.Add(setHeader);
            header.Add(colSet);

            wrap.Add(header);
            wrap.Add(new VisualElement { style = { height = 1, backgroundColor = new Color(0,0,0,.35f), marginBottom = 4 } });

            // ---------- Rows ----------
            var rows = new ListView
            {
                itemsSource = landing_fwKeys,
                virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight,
                selectionType = SelectionType.None,
                style = { height = Math.Max(160, 22 * landing_fwKeys.Count) }
            };

            rows.makeItem = () =>
            {
                var r = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, height = 22, paddingLeft = 4, paddingRight = 4 } };

                var nameLbl = new Label
                {
                    name = "N",
                    style =
                    {
                        color = TextColorLight,
                        unityTextAlign = TextAnchor.MiddleLeft,
                        whiteSpace = WhiteSpace.NoWrap,
                        flexGrow = 1, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis
                    }
                };

                var tplTog = new Toggle
                {
                    name = "T",
                    style = { width = ColWidth, alignItems = Align.Center, justifyContent = Justify.Center }
                };
                var setTog = new Toggle
                {
                    name = "S",
                    style = { width = ColWidth, alignItems = Align.Center, justifyContent = Justify.Center }
                };

                // Register once; we use toggle.userData to know which key this row is bound to.
                tplTog.RegisterValueChangedCallback(evt =>
                {
                    if (evt?.target is Toggle t && t.userData is string k) landing_fwUseTemplate[k] = evt.newValue;
                });
                setTog.RegisterValueChangedCallback(evt =>
                {
                    if (evt?.target is Toggle t && t.userData is string k) landing_fwUseSettings[k] = evt.newValue;
                });

                r.Add(nameLbl); r.Add(tplTog); r.Add(setTog);
                return r;
            };

            rows.bindItem = (r, i) =>
            {
                if (i < 0 || i >= landing_fwKeys.Count) return;
                var key  = landing_fwKeys[i];
                r.userData = key;

                var name = r.Q<Label>("N");
                var tplT = r.Q<Toggle>("T");
                var setT = r.Q<Toggle>("S");

                name.text = key;

                // Stamp the key onto each toggle so the single registered handler knows what to update
                tplT.userData = key;
                setT.userData = key;

                tplT.SetValueWithoutNotify(landing_fwUseTemplate[key]);
                setT.SetValueWithoutNotify(landing_fwUseSettings[key]);
            };

            // Header "select all" behaviour
            allTpl.RegisterValueChangedCallback(e =>
            {
                foreach (var k in landing_fwKeys) landing_fwUseTemplate[k] = e.newValue;
                rows.Rebuild();
            });
            allSet.RegisterValueChangedCallback(e =>
            {
                foreach (var k in landing_fwKeys) landing_fwUseSettings[k] = e.newValue;
                rows.Rebuild();
            });

            // Initialize header toggles to current state
            bool AllOn(Func<string, bool> pred) => landing_fwKeys.Count > 0 && landing_fwKeys.TrueForAll(k => pred(k));
            allTpl.SetValueWithoutNotify(AllOn(k => landing_fwUseTemplate[k]));
            allSet.SetValueWithoutNotify(AllOn(k => landing_fwUseSettings[k]));

            wrap.Add(rows);
            return wrap;
        }

        private void RefreshLandingPage()
        {
            var templateKeys = SafeListTemplates();
            landing_templatesList = BuildToggleList(templateKeys, landing_selectedTemplates);
            
            // Decide visibility / width split
            var lastOpened = MasterSetting(ForgeTags.Settings.ACTIVE_FRAMEWORK, fallback: "");
            if (string.IsNullOrEmpty(lastOpened) || !ForgeStores.IterateFrameworkKeys().Contains(lastOpened))
            {
                // No remembered project → hide quick-load and let Load take the full row
                landing_quickLoadButton.style.display = DisplayStyle.None;
                landing_loadButton.style.flexGrow = 1f;
                
                landing_loadButton.style.borderTopRightRadius    = defaultRadius;
                landing_loadButton.style.borderBottomRightRadius = defaultRadius;
                
                landing_loadButton.style.paddingLeft = 0;
            }
            else
            {
                landing_quickLoadButton.style.display = DisplayStyle.Flex;
                landing_quickLoadButton.tooltip = $"Quick load \"{lastOpened}\"";
                // 80 / 20 split via flex-grow (4:1)
                landing_loadButton.style.flexGrow      = 6f;
                landing_quickLoadButton.style.flexGrow = 1f;
                
                landing_loadButton.style.borderTopRightRadius    = 0;
                landing_loadButton.style.borderBottomRightRadius = 0;
                
                landing_loadButton.style.paddingLeft = 67;
            }

            landing_card.style.display = DisplayStyle.Flex;

            // landing_createCard = BuildCreatePopup_Landing();
            // landing_openPopup = BuildOpenPopup_Landing();
        }
        
        private void OnConfirmCreate_Landing()
        {
            var _name = (landing_create_nameField?.value ?? "").Trim();
            if (string.IsNullOrEmpty(_name))
            {
                BuildWarnAlert("Please enter a name for your framework.");
                return;
            }

            var slug = ForgeJsonUtility.Slugify(_name);
            if (ForgeStores.IterateFrameworkKeys().Contains(slug))
            {
                BuildWarnAlert("A framework with this name already exists.");
                return;
            }

            // If default: create empty shell
            var newProject = new FrameworkProject { MetaName = _name, MetaAuthor = Environment.MachineName, Version = AboutPlayForge.PlayForgeVersion };

            if (landing_createFrameworkMode == CreateFrameworkMode.FromExisting)
            {
                // Determine merge order from the visible order (landing_fwKeys)
                // "Earlier" means keys earlier in the list (top), "Later" is bottom.
                var keysInOrder = landing_fwKeys.ToList();

                // SETTINGS
                var selectedForSettings = keysInOrder.Where(k => landing_fwUseSettings.TryGetValue(k, out var on) && on).ToList();
                var settingsList = new List<ForgeJsonUtility.SettingsWrapper>();
                foreach (var fk in selectedForSettings)
                    settingsList.Add(ForgeStores.LoadLocalSettings(fk) ?? new ForgeJsonUtility.SettingsWrapper());
                if (settingsList.Count == 0)
                {
                    // Use default settings
                    settingsList.Add(ForgeStores.CreateDefaultLocalSettings());
                }

                var mergedSettings = MergeSettings(
                    policy: landing_settingsPolicy,
                    sets: settingsList // same input; MergeSettings respects policy internally
                );

                // TEMPLATES (project content)
                var selectedForTemplate = keysInOrder.Where(k => landing_fwUseTemplate.TryGetValue(k, out var on) && on).ToList();
                var sources = new List<FrameworkProject>();
                foreach (var tk in selectedForTemplate)
                {
                    var src = ForgeStores.LoadFramework(tk);
                    if (src != null) sources.Add(src);
                }
                var mergedProject = MergeProjects(sources, landing_settingsPolicy); // reuse same policy concept
                if (mergedProject != null) newProject = mergedProject;

                // Always set the final name after merge
                newProject.MetaName = _name;

                // Save settings now (per-framework local settings file)
                ForgeStores.SaveLocalSettings(slug, new ForgeJsonUtility.SettingsWrapper(mergedSettings));
            }
            else
            {
                // Default mode: write an empty local settings file for this new framework
                ForgeStores.SaveLocalSettings(slug, ForgeStores.CreateDefaultLocalSettings());
            }

            // Save framework file
            ForgeStores.CreateNewFramework(newProject);

            // Load into editor
            LoadFramework(newProject);
            SetPage(GasifyPage.Home);

            // Close popup
            landing_createCard.style.display = DisplayStyle.None;
        }
        
        private FrameworkProject MergeProjects(IEnumerable<FrameworkProject> sources, MergePolicy policy)
        {
            var list = sources?.ToList() ?? new List<FrameworkProject>();
            if (list.Count == 0) return new FrameworkProject();

            bool preferLater = policy == MergePolicy.PreferLater;
            var outProj = new FrameworkProject();

            // Helper for lists of nodes, uniqueness by Name (case-insensitive)
            void MergeList<T>(List<T> dest, IEnumerable<T> add) where T : ForgeDataNode, new()
            {
                var index = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < dest.Count; i++) index[dest[i].Name ?? string.Empty] = i;

                foreach (var node in add ?? Enumerable.Empty<T>())
                {
                    var key = node.Name ?? string.Empty;
                    if (index.TryGetValue(key, out var idx))
                    {
                        if (preferLater)
                        {
                            var clone = ForgeDataNode.Clone(node, out _) ?? node;
                            dest[idx] = clone;
                        }
                    }
                    else
                    {
                        var clone = ForgeDataNode.Clone(node, out _) ?? node;
                        dest.Add(clone);
                        index[key] = dest.Count - 1;
                    }
                }
            }

            foreach (var s in list)
            {
                MergeList(outProj.Abilities,        s.Abilities);
                MergeList(outProj.Effects,          s.Effects);
                MergeList(outProj.Entities,         s.Entities);
                MergeList(outProj.Attributes,       s.Attributes);
                MergeList(outProj.Tags,             s.Tags);
                MergeList(outProj.AttributeSets,    s.AttributeSets);
            }

            return outProj;
        }
        
        // Merge settings dictionaries by Tag.Name
        private sealed class TagNameComparer : IEqualityComparer<Tag>
        {
            public bool Equals(Tag x, Tag y) => string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
            public int GetHashCode(Tag obj) => (obj.Name ?? string.Empty).ToLowerInvariant().GetHashCode();
        }

        private Dictionary<Tag, object> MergeSettings(IEnumerable<ForgeJsonUtility.SettingsWrapper> sets, MergePolicy policy)
        {
            var result = new Dictionary<Tag, object>(new TagNameComparer());
            bool preferLater = policy == MergePolicy.PreferLater;

            // If we prefer earlier, iterate forward and only set if missing.
            // If we prefer later, iterate forward and always overwrite.
            foreach (var wrap in sets)
            {
                foreach (var kv in wrap.Data)
                {
                    if (!result.ContainsKey(kv.Key)) result[kv.Key] = kv.Value;
                    else if (preferLater) result[kv.Key] = kv.Value;
                }
            }
            return result;
        }
        
        private List<string> SafeListFrameworkKeys()
        {
            try { return ForgeStores.IterateFrameworkKeys().OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(); }
            catch { return new List<string>(); }
        }

        private List<string> SafeListTemplates()
        {
            try { return ForgeStores.IterateFrameworkKeys().OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToList(); }
            catch { return new List<string>(); }
        }
        
        void OnCreateClicked_Landing()
        {
            // Hide the main landing card; disclaimer remains in the background.
            if (landing_card != null)
                landing_card.style.display = DisplayStyle.None;

            // Show the overlay we built in BuildCreatePopup_Landing (landing_createCard is the overlay).
            if (landing_createCard != null)
                landing_createCard.style.display = DisplayStyle.Flex;

            // Put the caret in the name box.
            if (landing_create_nameField != null)
            {
                // Immediate try
                landing_create_nameField.Focus();
                // ...and ensure focus after layout just in case
                rootVisualElement.schedule.Execute(() =>
                {
                    landing_create_nameField.Focus();
                    landing_create_nameField.SelectAll();
                }).ExecuteLater(0);
            }
        }

        void OnQuickLoadClicked_Landing()
        {
            // Read the remembered key from master settings
            var key = MasterSetting(ForgeTags.Settings.ACTIVE_FRAMEWORK, fallback: "");
            if (string.IsNullOrEmpty(key)) return;
            
            // Load framework by key, then push it into the editor
            var pproj = ForgeStores.LoadFramework(key);
            if (pproj == null)
            {
                BuildErrorAlert($"Could not quick load \"{key}\" (no project found).");
                return;
            }

            LoadFramework(pproj);   // sets Project and pulls local settings
            SetPage(GasifyPage.Home);

            return;

            try
            {
                // Load framework by key, then push it into the editor
                var proj = ForgeStores.LoadFramework(key);
                if (proj == null)
                {
                    BuildErrorAlert($"Could not quick load \"{key}\" (no project found).");
                    return;
                }

                LoadFramework(proj);   // sets Project and pulls local settings
                SetPage(GasifyPage.Home);

                // (Optional) re-store the key so it stays fresh in case you move this logic
                // MasterSettings.data[EditorTagService.Settings.LAST_OPENED_FRAMEWORK] = key;
                // GasifyStores.SaveMasterSettings(MasterSettings);
            }
            catch (Exception ex)
            {
                BuildErrorAlert($"Failed to quick load \"{key}\": {ex.Message}");
                Debug.Log(ex);
            }
        }

        void OnDocumentationClicked_Landing()
        {
            // TODO: point to your docs URL or local file
            Application.OpenURL("https://example.com/your-framework-docs");
        }
        
        void OnSettingsClicked_Landing()
        {
            PlayForgeSettingsWindow.SetFocus();
            PlayForgeSettingsWindow.Open(_projectFilePath, LoadLocalSettings);
        }

        void OnDeleteFrameworkClicked_Landing(string key)
        {
            if (ForgeStores.DeleteFramework(key, out var msg))
            {
                Debug.Log(msg);
                Rebuild_OpenList();
            }
            else Debug.Log(msg);
        }

        void OnClick_AboutFESGAS()
        {
            EditorUtility.DisplayDialog("About FESGAS",
                "FESGAS & Gasify Editor Suite\n\nCreate precise, meaningful gameplay behaviour at the intersection of logic and complexity.\n\nA Gameplay Ability System for Unity.\n\n© Far Emerald Studio",
                "OK");
        }

        void WhenSetLandingPage()
        {
            RefreshLandingPage();
        }

        void TryAutoLoadIntoSession()
        {
            var sessionId = MasterSetting(ForgeTags.Settings.SESSION_ID, fallback: "");
            
            if (string.IsNullOrEmpty(sessionId))
            {
                SetMasterSetting(ForgeTags.Settings.SESSION_ID, ForgeStores.SessionId);
                return;
            }

            if (sessionId == ForgeStores.SessionId)
            {
                TryQuickLoad(MasterSetting(ForgeTags.Settings.ACTIVE_FRAMEWORK, ""));
            }
            else
            {
                SetMasterSetting(ForgeTags.Settings.SESSION_ID, ForgeStores.SessionId);
            }
        }

        void WhenLeaveLandingPage()
        {
            
        }
        
        #endregion
        
        #region Open

        VisualElement BuildOpenPopup_Landing()
        {
            // Overlay
            var _overlay = new VisualElement
            {
                name = "LandingOpenOverlay",
                style =
                {
                    position = Position.Absolute, left = 0, right = 0, top = 0, bottom = 0,
                    backgroundColor = new Color(0,0,0,0.32f),
                    display = DisplayStyle.None,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center
                }
            };

            // Card
            var card = new VisualElement
            {
                name = "OpenCard",
                style =
                {
                    width = 700,
                    maxWidth = 760,
                    maxHeight = 560,
                    backgroundColor = BackgroundColorDark,
                    paddingLeft = 14, paddingRight = 14, paddingTop = 14, paddingBottom = 18,
                    borderTopLeftRadius = 10, borderTopRightRadius = 10, borderBottomLeftRadius = 10, borderBottomRightRadius = 10,
                    flexDirection = FlexDirection.Column
                }
            };
            _overlay.Add(card);

            // Title
            card.Add(new Label("Open Framework")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, fontSize = 16 }
            });

            // Search + Sort row
            var filters = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            landing_open_searchField = new TextField { label = "Search", value = "" };
            landing_open_searchField.style.flexGrow = 1;
            landing_open_searchField.style.marginRight = 8;  // leave room for dropdown
            landing_open_searchField.RegisterValueChangedCallback(evt =>
            {
                landing_open_search = evt.newValue ?? "";
                Rebuild_OpenList();
            });
            filters.Add(landing_open_searchField);

            landing_open_sortField = new PopupField<string>("", new List<string> { "Name", "Date Created", "# Datas" }, 0);
            landing_open_sortField.tooltip = "Sort by…";
            landing_open_sortField.style.width = 160;
            landing_open_sortField.style.flexShrink = 0;     // keep that width
            landing_open_sortField.RegisterValueChangedCallback(evt => { landing_open_sort = evt.newValue; Rebuild_OpenList(); });
            filters.Add(landing_open_sortField);

            card.Add(filters);

            // Table header: Name (left) | Actions (right).  (Issues header removed)
            var head = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    backgroundColor = BackgroundColorDark,
                    paddingLeft = 8, paddingRight = 8, paddingTop = 6, paddingBottom = 6,
                    borderTopLeftRadius = 6, borderTopRightRadius = 6
                }
            };
            head.Add(new Label("Name")    { style = { flexGrow = 1, unityFontStyleAndWeight = FontStyle.Bold } });
            var actionsHead = new VisualElement { style = { width = 96, alignItems = Align.FlexEnd } };
            actionsHead.Add(new Label { style = { unityFontStyleAndWeight = FontStyle.Bold } });
            head.Add(actionsHead);
            card.Add(head);

            // List
            landing_open_list = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1,
                    minWidth = 560,                       // <-- critical: don’t collapse
                    backgroundColor = BackgroundColorDark,
                    borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
                    paddingTop = 2, paddingBottom = 4,
                    borderBottomWidth = 2, borderBottomColor = EdgeColorDark,
                    borderTopWidth = 2, borderTopColor = EdgeColorDark,
                    borderLeftWidth = 2, borderLeftColor = EdgeColorDark,
                    borderRightWidth = 2, borderRightColor = EdgeColorDark,
                    maxHeight = 125,
                    minHeight = 125
                }
            };
            landing_open_list.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            landing_open_list.mode = ScrollViewMode.Vertical;
            card.Add(landing_open_list);

            // Selected (read-only, not focusable) as a faux text field
            var selectedWrap = new VisualElement
            {
                style =
                {
                    marginTop = 8,
                    backgroundColor = BackgroundColorLight,
                    borderTopLeftRadius = 2, borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2, borderBottomRightRadius = 2,
                    minHeight = 12,
                    paddingLeft = 8, paddingRight = 8, paddingTop = 9, paddingBottom = 9,
                    maxHeight = 12,
                    alignItems = Align.FlexStart,
                    justifyContent = Justify.Center
                }
            };
            landing_open_selectedField = new Label("") {
                style = { color = TextColorLight }
            };
            selectedWrap.Add(landing_open_selectedField);
            card.Add(selectedWrap);

            // Bottom control row: Left = options + Close; Right = issues + Load
            var bottom = new VisualElement
            {
                style = {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginTop = 8,
                    width = Length.Percent(100)
                }
            };

            // Left actions
            var leftActions = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            leftActions.Add(TertiaryButton("Settings", () => { if (landing_open_selectedKey != null) OnSettingsClicked_Landing(); }, radius: 4));
            leftActions.Add(TertiaryButton("Classifications", () => { /* TODO #1# }, radius: 4));
            leftActions.Add(TertiaryButton("Statistics", () => { /* TODO #1# }, radius: 4));
            bottom.Add(leftActions);

            // Spacer
            bottom.Add(new VisualElement { style = { flexGrow = 1 } });

            // Right: issues summary + Load
            var rightActions = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            landing_open_issueSummary = new Label("") { style = { minWidth = 200, unityTextAlign = TextAnchor.MiddleRight, opacity = 0.9f, marginRight = 8 } };
            rightActions.Add(landing_open_issueSummary);
            var closeBtn = SecondaryButton("Close", () =>
            {
                _overlay.style.display = DisplayStyle.None;
                RefreshLandingPage();
            });
            rightActions.Add(closeBtn);
            var loadBtn = PrimaryButton("Load", () =>
            {
                if (!string.IsNullOrEmpty(landing_open_selectedKey)) TryQuickLoad(landing_open_selectedKey);
            });
            loadBtn.style.width = 96;
            rightActions.Add(loadBtn);

            bottom.Add(rightActions);
            card.Add(bottom);

            landing_openCard = _overlay;
            Rebuild_OpenList();
            return landing_openCard;
        }
        
        // Call this when "Load" is clicked on the landing page
        void OnLoadClicked_Landing()
        {
            if (landing_card != null) landing_card.style.display = DisplayStyle.None;
            landing_openCard.style.display = DisplayStyle.Flex;
            landing_open_searchField?.Focus();
            
            Rebuild_OpenList();
        }

        // ===== helpers for Open popup =====

        class FrameworkRow
        {
            public string Key;
            public string Name;
            public int    DataCount;
            public DateTime CreatedUtc;
            public (int caution, int warning, int error) Issues;
        }

        void Rebuild_OpenList()
        {
            landing_open_list.Clear();

            List<FrameworkRow> rows = SafeListFrameworkKeys()
                .Select(BuildFrameworkRowSafe)
                .Where(r => r != null)
                .ToList();
            

            // Search filter
            if (!string.IsNullOrWhiteSpace(landing_open_search))
            {
                var q = landing_open_search.Trim();
                rows = rows.Where(r => r.Name?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                                    || r.Key?.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }

            // Sort
            rows = landing_open_sort switch
            {
                "Date Created" => rows.OrderByDescending(r => r.CreatedUtc).ToList(),
                "# Datas"      => rows.OrderByDescending(r => r.DataCount).ToList(),
                _              => rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList()
            };

            int count = 0;
            foreach (var r in rows)
            {
                var row = BuildFrameworkRowElement(r);
                var color = count++ % 2 == 0 ? BackgroundColorSub * .75f : BackgroundColorSub * .6f;
                row.style.backgroundColor = color;
                TrackHover(row, hovered => row.style.backgroundColor = hovered ? BackgroundColorSub * 1.2f : color);
                landing_open_list.Add(row);
            }

            // keep bottom summary up-to-date
            UpdateSelectedSummary();
        }

        FrameworkRow BuildFrameworkRowSafe(string key)
        {
            try
            {
                // Local settings (also ensure DateCreated exists)
                var local = ForgeStores.LoadLocalSettings(key);
                DateTime created;
                if (local.Data.TryGetValue(ForgeTags.Settings.DATE_CREATED, out var dcObj) && dcObj is string sDate && DateTime.TryParse(sDate, null, DateTimeStyles.RoundtripKind, out var parsed))
                {
                    created = parsed.ToUniversalTime();
                }
                else
                {
                    // fall back to file metadata, then persist for next time
                    var lpath = ForgePaths.LocalSettingsPath(key);
                    if (File.Exists(lpath)) created = File.GetCreationTimeUtc(lpath);
                    else
                    {
                        var fpath = ForgePaths.FrameworkPath(key);
                        if (File.Exists(fpath)) created = File.GetCreationTimeUtc(fpath);
                        else created = DateTime.UtcNow;
                    }
                    
                    // Update local settings if the time created didn't exist
                    local.Data[ForgeTags.Settings.DATE_CREATED] = created.ToString(CultureInfo.InvariantCulture);
                    ForgeStores.SaveLocalSettings(key, local);
                }

                // Load framework to get display name, counts and issues
                var proj = ForgeStores.LoadFramework(key);
                if (proj == null) return null;

                var count = CountDatas(proj);
                var issues = SummarizeIssues(proj);

                return new FrameworkRow
                {
                    Key = key,
                    Name = string.IsNullOrEmpty(proj.MetaName) ? key : proj.MetaName,
                    CreatedUtc = created,
                    DataCount = count,
                    Issues = issues
                };
            }
            catch (Exception ex)
            {
                BuildWarnAlert($"Open list skipped {Quotify(key)}: {ex.Message}");
                return null;
            }
        }

        int CountDatas(FrameworkProject p)
        {
            // Sum up all node lists you maintain in FrameworkProject
            int n = 0;
            n += p.Abilities?.Count ?? 0;
            n += p.Effects?.Count ?? 0;
            n += p.Entities?.Count ?? 0;
            n += p.Attributes?.Count ?? 0;
            n += p.Tags?.Count ?? 0;
            n += p.AttributeSets?.Count ?? 0;
            return n;
        }

        // Very lightweight “issues” pass; you can expand later.
        // Error = any node with MISSING_REFS true; Warning/Caution are placeholders for now.
        (int caution, int warning, int error) SummarizeIssues(FrameworkProject p)
        {
            int errors = 0, warns = 0, cautions = 0;

            void scan<T>(IEnumerable<T> list) where T : ForgeDataNode
            {
                if (list == null) return;
                foreach (var n in list)
                {
                    // if (n.TagStatus<int>(ForgeService.ERROR_ALERTS) > 0) errors += 1;
                    
                    // TODO add other checks here
                }
            }

            scan(p.Abilities); scan(p.Effects); scan(p.Entities); scan(p.Attributes); scan(p.Tags); scan(p.AttributeSets);

            return (cautions, warns, errors);
        }

        VisualElement BuildFrameworkRowElement(FrameworkRow r)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8, paddingRight = 8, paddingTop = 4, paddingBottom = 4,
                    width = Length.Percent(100),
                    maxHeight = 18,
                    // minWidth = 300,
                    justifyContent = Justify.Center
                }
            };

            // Name + small status icon tucked right after it
            var nameWrap = new VisualElement { style = { flexDirection = FlexDirection.Row, alignContent = Align.Center, flexGrow = 1 } };
            var nameLbl  = new Label(r.Name) { style = { color = TextColorLight } };
            nameWrap.Add(nameLbl);

            var iconTex = r.Issues.error > 0 ? icon_ERROR : (r.Issues.warning > 0 || r.Issues.caution > 0 ? icon_WARN : null);
            if (iconTex != null)
            {
                var issuesIcon = new Image
                {
                    image = iconTex, scaleMode = ScaleMode.ScaleToFit,
                    tooltip = BuildIssueTooltip(r.Issues),
                    style = { width = 16, height = 16, marginLeft = 6 }
                };
                nameWrap.Add(issuesIcon);
            }
            row.Add(nameWrap);

            // Actions (right)
            var actions = new VisualElement { style = { width = 96, flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };

            Button IconBtn(string glyph, Action onClick)
            {
                var b = new Button(onClick) { text = glyph };
                b.style.width = 35; b.style.height = 15;
                b.style.paddingLeft = 0; b.style.paddingRight = 0;
                return b;
            }

            var btnOptions = IconBtn("⋯", () =>
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Reveal in Explorer"), false, () =>
                {
                    var path = ForgePaths.FrameworkFolder(r.Key);
                    if (Directory.Exists(path)) EditorUtility.RevealInFinder(path);
                });
                menu.AddItem(new GUIContent("Settings"), false, OnSettingsClicked_Landing);
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Delete"), false, () => OnDeleteFrameworkClicked_Landing(r.Key));
                menu.ShowAsContext();
            });
            btnOptions.RegisterCallback<ClickEvent>(e => e.StopImmediatePropagation());
            btnOptions.style.display = DisplayStyle.None;

            var btnQuick = IconBtn("→", () => TryQuickLoad(r.Key));
            actions.Add(btnOptions);
            actions.Add(btnQuick);
            row.Add(actions);
            btnQuick.style.display = DisplayStyle.None;
            
            TrackHover(row, flag =>
            {
                if (flag)
                {
                    btnOptions.style.display = DisplayStyle.Flex;
                    btnQuick.style.display = DisplayStyle.Flex;
                }
                else
                {
                    btnOptions.style.display = DisplayStyle.None;
                    btnQuick.style.display = DisplayStyle.None;
                }
            });

            // Selection + double click
            row.RegisterCallback<ClickEvent>(evt =>
            {
                landing_open_selectedKey = r.Key;
                landing_open_selectedField.text = r.Name;    // not focusable
                UpdateSelectedSummary(r);
                if (evt.clickCount == 2) TryQuickLoad(r.Key);
            });

            return row;
        }

        string BuildIssueTooltip((int caution, int warning, int error) s)
        {
            if (s.caution == 0 && s.warning == 0 && s.error == 0) return "No issues detected.";
            // One compact line per type, only if present.
            var parts = new List<string>();
            if (s.caution > 0) parts.Add($"Caution: {s.caution}");
            if (s.warning > 0) parts.Add($"Warning: {s.warning}");
            if (s.error > 0)   parts.Add($"Error: {s.error}");
            return string.Join("\n", parts);
        }

        void UpdateSelectedSummary(FrameworkRow row = null)
        {
            if (row == null && !string.IsNullOrEmpty(landing_open_selectedKey))
                row = SafeListFrameworkKeys().Select(BuildFrameworkRowSafe).FirstOrDefault(r => r != null && r.Key == landing_open_selectedKey);

            if (row == null)
            {
                landing_open_issueSummary.text = "";
                landing_open_selectedField.text = "";
                return;
            }

            landing_open_selectedField.text = row.Name;

            if (row.Issues.caution == 0 && row.Issues.warning == 0 && row.Issues.error == 0)
                landing_open_issueSummary.text = "";
            else
                landing_open_issueSummary.text = $"Caution: {row.Issues.caution}\nWarning: {row.Issues.warning}\nError: {row.Issues.error}";
        }

        void TryQuickLoad(string key)
        {
            try
            {
                var proj = ForgeStores.LoadFramework(key);
                if (proj == null)
                {
                    BuildErrorAlert($"Could not open \"{key}\" (no project found).");
                    return;
                }
                LoadFramework(proj);   // sets Project and pulls local settings
                SetPage(GasifyPage.Home);
                if (landing_openCard != null) landing_openCard.style.display = DisplayStyle.None;
            }
            catch (Exception ex)
            {
                BuildErrorAlert($"Failed to open \"{key}\": {ex.Message}");
                Debug.LogException(ex);
            }
        }
        
        #endregion
        
        #endregion
        
        #region Content

        VisualElement BuildContent()
        {
            var v = new VisualElement()
            {
                style =
                {
                    flexGrow = 1,
                    marginLeft = 0, marginRight = 0, marginTop = 0, marginBottom = 0,
                    paddingLeft = 0, paddingRight = 0, paddingTop = 0, paddingBottom = 0,
                    display = DisplayStyle.Flex
                }
            };
            return v;
        }

        VisualElement BuildOverlay()
        {
            var v = new VisualElement()
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    position = Position.Absolute,
                    left = 0, right = 0, top = 0, bottom = 0,
                    display = DisplayStyle.Flex
                }
            };
            return v;
        }

        
        #endregion
        
        #region Alerts

        VisualElement BuildAlertStack()
        {
            return new VisualElement
            {
                name = "AlertStack",
                pickingMode = PickingMode.Position,
                style =
                {
                    position = Position.Absolute,
                    right = 12, bottom = 12,
                    flexDirection = FlexDirection.ColumnReverse, // newest on bottom, stack upward
                    alignItems = Align.FlexEnd
                }
            };   
        }

        enum AlertStyle
        {
            Info, Warn, Error
        }

        VisualElement BuildInformationAlert(string body, params AlertButtonSpec[] buttons)
        {
            // Simple info popup
            return BuildPopupAlert(
                AlertStyle.Info,
                icon: icon_INFO,
                bodyText: body,
                buttons: buttons
            );
        }

        VisualElement BuildWarnAlert(string body, params AlertButtonSpec[] buttons)
        {
            // Simple info popup
            return BuildPopupAlert(
                AlertStyle.Warn,
                icon: icon_WARN,
                bodyText: body,
                buttons: buttons
            );
        }

        VisualElement BuildErrorAlert(string body, params AlertButtonSpec[] buttons)
        {
            // Simple info popup
            return BuildPopupAlert(
                AlertStyle.Error,
                icon: icon_ERROR,
                bodyText: body,
                buttons: buttons
            );
        }

        VisualElement BuildPopupAlert(
            AlertStyle style,
            Texture2D icon,
            string bodyText,
            params AlertButtonSpec[] buttons
        )
        {
            Color headerColor = style switch
            {
                AlertStyle.Info => AlertColor_Info,
                AlertStyle.Warn => AlertColor_Warn,
                AlertStyle.Error => AlertColor_Error,
                _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
            };

            Color bodyColor = AlertColor_Body;
            return BuildPopupAlert(icon, GetAlertHeader(style), bodyText, headerColor, bodyColor, GetAlertLifetime(style, bodyText), buttons);
        }
        
        VisualElement BuildPopupAlert(
            Texture2D icon,
            string headerText,
            string bodyText,
            Color headerColor,
            Color bodyColor,
            float secondsToLive,
            params AlertButtonSpec[] buttons
        )
        {
            var card = new VisualElement
            {
                name = "PopupAlert",
                style =
                {
                    minWidth = 350,
                    maxWidth = 350,
                    backgroundColor = bodyColor,
                    borderTopLeftRadius = 8, borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                    borderTopWidth = 1, borderRightWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1,
                    borderTopColor = EdgeColorDark,
                    borderRightColor = EdgeColorDark,
                    borderBottomColor = EdgeColorDark,
                    borderLeftColor = EdgeColorDark,
                    overflow = Overflow.Hidden
                }
            };
            
            // ===== Header =====
            var header = new VisualElement
            {
                name = "Header",
                style =
                {
                    backgroundColor = headerColor,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8, paddingRight = 8, paddingTop = 6, paddingBottom = 6
                }
            };
            
            if (icon != null)
            {
                var img = new Image
                {
                    image = icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    style = { width = 16, height = 16, marginRight = 2 }
                };
                header.Add(img);
            }

            var _title = new Label(headerText)
            {
                style =
                {
                    color = TextColorLight,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexGrow = 1,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            };
            header.Add(_title);
            
            // Countdown timer label
            var timerLabel = new Label("")
            {
                style =
                {
                    color = new Color(1,1,1,0.8f),
                    unityTextAlign = TextAnchor.MiddleRight,
                    minWidth = 34
                }
            };
            header.Add(timerLabel);
            
            IVisualElementScheduledItem sch = null;

            // Close button
            var closeBtn = new Button(() => Dismiss(card)) { text = "×" };
            closeBtn.style.width = 22;
            closeBtn.style.height = 18;
            closeBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(closeBtn);

            card.Add(header);
            
            // ===== Body =====
            var body = new Label(bodyText)
            {
                style =
                {
                    color = TextColorLight,
                    paddingLeft = 10, paddingRight = 10, paddingTop = 8, paddingBottom = 8,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            card.Add(body);
            
            // ===== Footer (context buttons) =====
            if (buttons != null && buttons.Length > 0)
            {
                var footer = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        justifyContent = Justify.FlexEnd,
                        paddingLeft = 8, paddingRight = 8, paddingTop = 6, paddingBottom = 8
                    }
                };

                foreach (var spec in buttons)
                {
                    var b = new Button(() => { spec.OnClick?.Invoke(); Dismiss(card); })
                    {
                        text = spec.Text
                    };

                    // simple theme
                    b.style.paddingLeft = 10; b.style.paddingRight = 10;
                    b.style.height = 22;
                    b.style.borderTopLeftRadius = 6; b.style.borderTopRightRadius = 6;
                    b.style.borderBottomLeftRadius = 6; b.style.borderBottomRightRadius = 6;

                    if (spec.Primary)
                    {
                        b.style.backgroundColor = PrimaryButtonColor;
                        b.style.color = Color.white;
                    }
                    else
                    {
                        b.style.backgroundColor = BackgroundColorLight;
                        b.style.color = TextColorLight;
                    }

                    footer.Add(b);
                }

                card.Add(footer);
            }
            
            // Add to stack (bottom-right)
            alertStack.Add(card);

            // ===== Auto-dismiss timer =====
            float ttl = Mathf.Max(0.5f, secondsToLive);
            double started = EditorApplication.timeSinceStartup;
            
            void Tick()
            {
                double elapsed = EditorApplication.timeSinceStartup - started;
                double remain = Math.Max(0.0, ttl - elapsed);
                timerLabel.text = $"{remain:0.0}s";

                if (remain <= 0.0)
                    Dismiss(card);
            }

            // Local dismiss helper
            void Dismiss(VisualElement ve)
            {
                sch?.Pause();
                if (ve.parent != null)
                    ve.RemoveFromHierarchy();
            }
            
            // schedule: every ~0.1s
            sch = card.schedule.Execute(Tick).Every(100);
            // immediate first update
            Tick();

            return card;
        }
        
        #endregion
        
        #region Home Page
        
        VisualElement BuildHomePage()
        {
            var root = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Row,
                    paddingLeft = 0, paddingRight = 0, paddingTop = 0, paddingBottom = 0
                }
            };
            
                // LEFT: Project View
            home_projectView = BuildProjectView();
            // Give it a card-like look
            home_projectView.style.flexBasis = 0;
            home_projectView.style.flexGrow  = 1f;
            home_projectView.style.minWidth = 300;
            home_projectView.style.maxWidth = 300;
            home_projectView.style.backgroundColor = BackgroundColorDark;
            home_projectView.style.borderTopLeftRadius = 0;
            home_projectView.style.borderTopRightRadius = 0;
            home_projectView.style.borderBottomLeftRadius = 0;
            home_projectView.style.borderBottomRightRadius = 0;
            home_projectView.style.paddingLeft = 0; home_projectView.style.paddingRight = 0; home_projectView.style.paddingTop = 0; home_projectView.style.paddingBottom = 0;

            
            home_usageMap = BuildUsageMap();
            
            root.Add(home_projectView);
            root.Add(home_usageMap);
            
            return root;

            VisualElement BuildProjectView()
            {
                var wrap = new VisualElement { style = { flexGrow = 1, flexDirection = FlexDirection.Column } };
                wrap.style.backgroundColor = BackgroundColorDark;
                
                // --- Top bar: Search + Filter
                var top = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        marginBottom = 0,
                        paddingTop = 8,
                        paddingRight = 8,
                        backgroundColor = BackgroundColorDark
                    }
                };
                wrap.Add(top);

                home_projectSearchField = new TextField { name = "PV-Search", tooltip = "Search by name or id…" };
                home_projectSearchField.style.flexGrow = 1;
                home_projectSearchField.style.height = 22;
                home_projectSearchField.style.paddingLeft = 8;
                home_projectSearchField.style.paddingTop = 0;
                home_projectSearchField.style.paddingRight = 8;
                // Style inner input background/text
                var input = home_projectSearchField.Q("unity-text-input");
                if (input != null)
                {
                    input.style.backgroundColor = BackgroundColorLight;
                    input.style.color = TextColorLight;
                }
                home_projectSearchField.RegisterValueChangedCallback(_ => RefreshProjectView());
                top.Add(home_projectSearchField);

                //home_projectFilterButton = new Button(ShowProjectFilterMenu) { text = "Filter" };
                home_projectFilterButton = TertiaryButton("", ShowProjectFilterMenu, 22);

                home_projectFilterButton = MakeNavButton(icon_FILTER, ShowProjectFilterMenu, TertiaryButtonColor, TertiaryButtonColorHover);
                
                home_projectFilterButton.style.height = 22;
                top.Add(home_projectFilterButton);

                // --- List
                home_projectList = new ListView
                {
                    selectionType = SelectionType.Single,
                    virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                    fixedItemHeight = 22,
                    style =
                    {
                        flexGrow = 1,
                        backgroundColor = BackgroundColorDark,
                        paddingLeft = 8,
                        paddingTop = 8
                    }
                };

                // Row visual template
                home_projectList.makeItem = () =>
                {
                    var row = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            paddingLeft = 6, paddingRight = 6,
                            height = 22
                        }
                    };

                    // Chevron / icon (for headers)
                    var chevron = new Label { name = "Chevron", text = "", style = { width = 14, unityTextAlign = TextAnchor.MiddleCenter, marginRight = 4, color = TextColorLight } };
                    row.Add(chevron);

                    // Name
                    var _name = new Label { name = "Name", text = "", style = { flexGrow = 1, color = TextColorLight, unityTextAlign = TextAnchor.MiddleLeft, whiteSpace = WhiteSpace.NoWrap, overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis } };
                    row.Add(_name);
                    
                    // template chip
                    var chipTemplate = BuildIconChip(icon_TEMPLATE, 3, 16, 1, "This item is a template.");
                    chipTemplate.style.display = DisplayStyle.None;
                    chipTemplate.name = "ChipTemplate";
                    row.Add(chipTemplate);
                    
                    // template chip
                    var chipMissingRefs = BuildIconChip(icon_WARN, 3, 16, 1, "This item has missing references.");
                    chipMissingRefs.style.display = DisplayStyle.None;
                    chipMissingRefs.name = "ChipMissingRefs";
                    row.Add(chipMissingRefs);
                    
                    // editable chip
                    var chipEditable = BuildIconChip(icon_WARN, 3, 16, 1, "This item has missing references.");
                    chipEditable.style.display = DisplayStyle.None;
                    chipEditable.name = "ChipEditable";
                    row.Add(chipEditable);
                    
                    // Hover-only arrow (for items)
                    var optionsBtn = new Button { name = "Options", text = "···" };
                    optionsBtn.style.height = 18; optionsBtn.style.width = 24; optionsBtn.style.display = DisplayStyle.None;
                    optionsBtn.style.alignItems = Align.Center;
                    row.Add(optionsBtn);

                    // Hover-only arrow (for items)
                    var goBtn = new Button { name = "Go", text = "→" };
                    goBtn.style.height = 18; goBtn.style.width = 32; goBtn.style.display = DisplayStyle.None;
                    goBtn.style.paddingLeft = 0;
                    goBtn.style.alignItems = Align.Center;
                    row.Add(goBtn);

                    // Hover behavior to show/hide the go arrow on items
                    row.RegisterCallback<PointerEnterEvent>(_ =>
                    {
                        var data = row.userData as PVRow;
                        if (data != null && data.Type == PVRowType.Item)
                        {
                            goBtn.style.display = DisplayStyle.Flex;
                            optionsBtn.style.display = DisplayStyle.Flex;
                        }
                    });
                    row.RegisterCallback<PointerLeaveEvent>(_ =>
                    {
                        goBtn.style.display = DisplayStyle.None;
                        optionsBtn.style.display = DisplayStyle.None;
                    });

                    return row;
                };
                
                // Bind per row
                home_projectList.bindItem = (row, index) =>
                {
                    if (index < 0 || index >= home_pvRows.Count) return;
                    var data = home_pvRows[index];
                    row.userData = data;

                    var chevron = row.Q<Label>("Chevron");
                    var _name    = row.Q<Label>("Name");
                    var goBtn   = row.Q<Button>("Go");
                    var editBtn = row.Q<Button>("Options");
                    var chipTemplate = row.Q<VisualElement>("ChipTemplate");
                    var chipMissingRefs = row.Q<VisualElement>("ChipMissingRefs");
                    var chipEditable = row.Q<VisualElement>("ChipEditable");
                    goBtn.tooltip = "Edit";
                    editBtn.tooltip = "Options";

                    // Reset
                    goBtn.style.display = DisplayStyle.None;
                    editBtn.style.display = DisplayStyle.None;
                    row.style.unityBackgroundImageTintColor = new StyleColor();

                    if (data.Type == PVRowType.Header)
                    {
                        // Header row
                        chevron.text = data.Collapsed ? "▲" : "▼";
                        chevron.style.display = DisplayStyle.Flex;
                        _name.text = DataTypeTextPlural(data.DataType) + $" ({filteredProjectData[data.DataType].Count})";
                        _name.style.unityFontStyleAndWeight = FontStyle.Bold;

                        // Click anywhere in header row to toggle
                        row.RegisterCallback<MouseDownEvent>(OnHeaderClicked, TrickleDown.NoTrickleDown);
                        row.style.backgroundColor = BackgroundColorLight;
                        goBtn.style.display = DisplayStyle.None;
                        editBtn.style.display = DisplayStyle.None;
                    }
                    else if (data.Type == PVRowType.Placeholder)
                    {
                        chevron.text = "";
                        _name.text = "No items to display";
                        _name.style.unityFontStyleAndWeight = FontStyle.Bold;
                        row.style.backgroundColor = BackgroundColorLight;
                        goBtn.style.display = DisplayStyle.None;
                        editBtn.style.display = DisplayStyle.None;
                    }
                    else
                    {
                        // Item row
                        chevron.text = ""; // no chevron for items
                        chevron.style.display = DisplayStyle.None;
                        _name.text = $"{data.Name}";
                        _name.style.unityFontStyleAndWeight = FontStyle.Normal;

                        if (Project.TryGet(data.Id, data.DataType, out var node))
                        {
                            if (node.TagStatus(ForgeTags.IS_TEMPLATE)) chipTemplate.style.display = DisplayStyle.Flex;
                            // if (node.TagStatus<int>(ForgeService.ERROR_ALERTS) > 0) chipMissingRefs.style.display = DisplayStyle.Flex;
                            if (node.TagStatus(ForgeTags.EDITABLE)) chipEditable.style.display = DisplayStyle.Flex;
                        }

                        row.style.paddingLeft = 24;

                        // Primary click loads into Usage (stub)
                        row.RegisterCallback<MouseDownEvent>(OnItemClicked, TrickleDown.NoTrickleDown);

                        // Arrow button → go to Create page & load item (stub)
                        goBtn.clicked -= OnGoClicked; // avoid stacking
                        goBtn.clicked += OnGoClicked;

                        editBtn.clicked -= OnOptionsClicked;
                        editBtn.clicked += OnOptionsClicked;
                        
                        void OnOptionsClicked()
                        {
                            Debug.Log($"Options clicked");
                        }
                        
                        void OnGoClicked()
                        {
                            SetCreatorPage(data.Id, data.Name, data.DataType);
                        }
                    }

                    return;

                    // Local handlers capture current row via row.userData
                    void OnHeaderClicked(MouseDownEvent e)
                    {
                        e.StopImmediatePropagation();
                        ToggleSection(data.DataType);
                    }

                    void OnItemClicked(MouseDownEvent e)
                    {
                        if (data.Type != PVRowType.Item) return;
                        
                        Debug.Log($"[ProjectView] Load into Usage: {data.DataType} • {data.Name} ({data.Id})");
                        // TODO load into usage map
                    }
                };

                // Avoid “selection highlight” sticking on headers
                home_projectList.selectionChanged += objs =>
                {
                    var row = objs?.FirstOrDefault() as PVRow;
                    if (row == null || row.Type == PVRowType.Header)
                    {
                        home_projectList.ClearSelection();
                        return;
                    }
                    // You could react to item selection here if desired.
                };

                wrap.Add(home_projectList);

                // Init collapsed flags (default: expanded)
                foreach (EDataType dt in Enum.GetValues(typeof(EDataType)))
                    home_pvSectionCollapsed[dt] = false;

                // First population
                RefreshProjectView();
                return wrap;
                
                void ShowProjectFilterMenu()
                {
                    CloseAllDropdowns();
                    
                    var m = new GenericMenu();
                    var sub = new GenericMenu();

                    // DataType filters (toggle)
                    void ToggleKind(EDataType k)
                    {
                        if (home_pvTypeFilter.Contains(k)) home_pvTypeFilter.Remove(k);
                        else home_pvTypeFilter.Add(k);
                        RefreshProjectView();
                    }

                    // Build entries for the core types you’re using. Add more as you add types.
                    AddKind(EDataType.Ability, "Ability");
                    AddKind(EDataType.Effect, "Effect");
                    AddKind(EDataType.Entity, "System");
                    AddKind(EDataType.Attribute, "Attribute");
                    AddKind(EDataType.Tag, "Tag");
                    AddKind(EDataType.AttributeSet, "Attribute Set");

                    // m.AddSeparator("");
                    
                    // In-Use only (stubbed—flag stored, not applied yet)
                    m.AddItem(new GUIContent("▲ Reset Data Type Filter"), false, () =>
                    {
                        home_pvTypeFilter.Clear();
                        RefreshProjectView();
                    });
                    
                    m.AddSeparator("");

                    // In-Use only (stubbed—flag stored, not applied yet)
                    m.AddItem(new GUIContent("In Use Only"), home_pvOnlyInUse, () =>
                    {
                        home_pvOnlyInUse = !home_pvOnlyInUse;
                        RefreshProjectView();
                    });
                    
                    // In-Use only (stubbed—flag stored, not applied yet)
                    m.AddItem(new GUIContent("Populated Types Only"), home_pvOnlyPopulated, () =>
                    {
                        home_pvOnlyPopulated = !home_pvOnlyPopulated;
                        RefreshProjectView();
                    });
                    
                    m.AddItem(new GUIContent("Editable Only"), home_pvOnlyEditable, () =>
                    {
                        home_pvOnlyEditable = !home_pvOnlyEditable;
                        RefreshProjectView();
                    });
                    
                    m.AddSeparator("");
                    
                    // In-Use only (stubbed—flag stored, not applied yet)
                    m.AddItem(new GUIContent("Show Templates"), home_pvOnlyTemplates, () =>
                    {
                        home_pvOnlyTemplates = !home_pvOnlyTemplates;
                        
                        RefreshProjectView();
                    });
                    
                    m.AddSeparator("");
                    
                    // In-Use only (stubbed—flag stored, not applied yet)
                    m.AddItem(new GUIContent("▲ Reset All"), false, () =>
                    {
                        home_pvTypeFilter.Clear();
                        home_pvOnlyInUse = false;
                        home_pvOnlyPopulated = true;
                        home_pvOnlyEditable = true;
                        home_pvOnlyTemplates = false;
                        RefreshProjectView();
                    });
                    
                    m.ShowAsContext();
                    return;

                    void AddKind(EDataType kind, string label)
                    {
                        bool on = home_pvTypeFilter.Contains(kind);
                        m.AddItem(new GUIContent($"Type Filter/{label}"), on, () => ToggleKind(kind));
                    }
                }

                void ToggleSection(EDataType kind)
                {
                    home_pvSectionCollapsed.TryAdd(kind, false);
                    home_pvSectionCollapsed[kind] = !home_pvSectionCollapsed[kind];
                    RefreshProjectView();
                }
            }

            VisualElement BuildUsageMap()
            {
                // RIGHT: Usage Map (placeholder for now)
                var rightUsage = new VisualElement
                {
                    style =
                    {
                        flexBasis = 0,
                        flexGrow = 1f,
                        backgroundColor = BackgroundColorDeepLight,
                        justifyContent = Justify.Center, alignItems = Align.Center
                    }
                };
                rightUsage.Add(new Label("Usage Map (coming soon)") { style = { color = TextColorLight, unityFontStyleAndWeight = FontStyle.Italic } });
                return rightUsage;
            }
        }

        void WhenSetHomePage()
        {
            SaveFramework();
            RefreshHomePage();
        }

        void SaveFramework(bool buildAlert = true)
        {
            if (Project is null) return;
            
            if (Setting(ForgeTags.Settings.FW_HAS_UNSAVED_WORK))
            {
                ForgeStores.SaveFrameworkAndSettings(Project, LocalSettings);
                ForgeStores.SaveFramework(Project, false);  // Update streaming assets version

                ForgeIndexBuilder.BuildOrUpdateIndex(Project);
                
                if (buildAlert) BuildInformationAlert($"Framework {Quotify(Project.MetaName)} has been saved.");
                SetSetting(ForgeTags.Settings.FW_HAS_UNSAVED_WORK, false);
            }
        }

        void WhenLeaveHomePage()
        {
            
        }

        void RefreshHomePage()
        {
            LoadProjectData();
            
            RefreshProjectView();
            RefreshUsageView();
            RefreshNavigationBar();
        }
        
        void RefreshProjectView()
        {
            LoadProjectData();

            var q = (home_projectSearchField?.value ?? string.Empty).Trim().ToLowerInvariant();
            bool hasQuery = q.Length > 0;

            var rows = new List<PVRow>();
            IEnumerable<EDataType> kindsOrder = new[]
            {
                EDataType.Ability, EDataType.Effect, EDataType.Entity, EDataType.Attribute, EDataType.Tag, EDataType.AttributeSet
            };

            // search pass
            var searched = new Dictionary<EDataType, List<(int id, string name)>>();
            foreach (var k in projectData.Keys)
            {
                var items = projectData[k];
                if (hasQuery)
                {
                    items = items.Where(p =>
                                (!string.IsNullOrEmpty(p.Item2) && p.Item2.ToLowerInvariant().Contains(q)) ||
                                (p.Item1 > 0 && p.Item1.ToString().Contains(q)))
                                 .ToList();
                }
                searched[k] = items;
            }

            filteredProjectData = new Dictionary<EDataType, List<(int, string)>>();
            
            foreach (var kind in kindsOrder)
            {
                filteredProjectData[kind] = new List<(int, string)>();
                if (searched[kind].Count == 0) continue;
                
                // type filter
                if (home_pvTypeFilter.Count > 0 && !home_pvTypeFilter.Contains(kind))
                    continue;

                if (!searched.TryGetValue(kind, out var itemsForKind))
                    continue;

                // ---- apply per-item filters to build the list we'll display ----
                var filtered = itemsForKind;
                if (home_pvOnlyTemplates)
                {
                    filtered = filtered
                        .Where(p => Project.TryGet(p.Item1, p.Item2, kind, out var node) &&
                                    node.TagStatus(ForgeTags.IS_TEMPLATE))
                        .ToList();
                }
                else
                {
                    filtered = filtered
                        .Where(p => Project.TryGet(p.Item1, p.Item2, kind, out var node) &&
                                    !node.TagStatus(ForgeTags.IS_TEMPLATE))
                        .ToList();
                }
                
                if (home_pvOnlyEditable && !home_pvOnlyTemplates)
                {
                    filtered = filtered
                        .Where(p => Project.TryGet(p.Item1, p.Item2, kind, out var node) &&
                                    node.TagStatus(ForgeTags.EDITABLE))
                        .ToList();
                }
                
                filteredProjectData[kind] = filtered;

                // populated types only => skip headers with zero filtered items
                if (home_pvOnlyPopulated && filtered.Count == 0)
                    continue;

                bool collapsed = home_pvSectionCollapsed.TryGetValue(kind, out var c) && c;

                // header shows the COUNT OF FILTERED ITEMS
                rows.Add(new PVRow
                {
                    Type = PVRowType.Header,
                    DataType = kind,
                    Name = $"{DataTypeTextPlural(kind)} ({filtered.Count})",
                    Collapsed = collapsed
                });

                if (!collapsed)
                {
                    foreach (var (id, _name) in filtered)
                    {
                        rows.Add(new PVRow
                        {
                            Type = PVRowType.Item,
                            DataType = kind,
                            Id = id,
                            Name = _name
                        });
                    }
                }
            }

            if (rows.Count == 0)
                rows.Add(new PVRow { Type = PVRowType.Placeholder, DataType = EDataType.None, Name = "No items to display", Collapsed = false });

            home_pvRows = rows;
            home_projectList.itemsSource = home_pvRows;
            home_projectList.Rebuild();
        }

        void RefreshUsageView()
        {
            
        }
        
        #endregion
        
        #region Creator Page
        
        VisualElement BuildCreatorPage()
        {
            // Root column
            var root = new VisualElement
            {
                name = "creatorRoot",
                style =
                {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Column
                }
            };

            // ===== Second row: two columns =====
            var secondRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexGrow = 1 }
            };

            // LEFT column: [ ACTIONS bar | USAGE pane ]
            var leftCol = new VisualElement
            {
                style = { flexDirection = FlexDirection.Column, width = Creator_LeftColWidth, flexShrink = 0 }
            };

            BuildActionsInPlace();
            void BuildActionsInPlace()
            {
                // ===== Actions Bar (Reset/Discard stacked, Save/Create wide) =====
                creator_actions = Card("ACTIONS");
                creator_actions.style.borderTopWidth = 0;
                creator_actions.style.flexDirection = FlexDirection.Row;
                creator_actions.style.alignItems = Align.Stretch;     // children may stretch vertically

// Make the bar tall enough for two rows and leave breathing room
                creator_actions.style.minHeight    = Creator_BarHeight * 2 + 12;
                creator_actions.style.paddingLeft  = 8;
                creator_actions.style.paddingRight = 8;
                creator_actions.style.paddingTop   = 6;
                creator_actions.style.paddingBottom= 6;
                
                creator_saveBtn   = FillButton("Save", SaveItem_Creator);
                creator_actions.Add(creator_saveBtn);
                
                creator_createButton = FillButton("Create", CreateItem_Creator);
                creator_actions.Add(creator_createButton);
                
                creator_optionsBtn = FillButton("▼", null);
                creator_optionsBtn.name = "ACTION_OPTIONS";
                creator_optionsBtn.style.minWidth = 25;
                creator_optionsBtn.style.width = 25;
                creator_optionsBtn.style.maxWidth = 25;
                creator_actions.Add(creator_optionsBtn);
                creator_optionsBtn.RegisterCallback<MouseUpEvent>(OpenOptionsMenu_Creator);
            }

            BuildUsageInPlace();
            void BuildUsageInPlace()
            {
                creator_usage = Card("USAGE");
                creator_usage.style.flexGrow = 1;
                creator_usage.style.justifyContent = Justify.Center;
                creator_usage.style.alignItems = Align.Center;
                creator_usage.Add(new Label("Usage")
                {
                    style = { color = TextColorLight, unityFontStyleAndWeight = FontStyle.Normal }
                });
            }

            leftCol.Add(creator_actions);
            leftCol.Add(creator_usage);

            // RIGHT column: EDITOR pane
            var rightCol = new VisualElement
            {
                style = { flexDirection = FlexDirection.Column, flexGrow = 1 }
            };

            BuildEditorInPlace();
            
            rightCol.Add(creator_editor);

            secondRow.Add(leftCol);
            secondRow.Add(rightCol);
            root.Add(secondRow);

            return root;

            // Local helper for consistent “card” look
            VisualElement Card(string _name)
            {
                return new VisualElement
                {
                    name = _name,
                    style =
                    {
                        backgroundColor = BackgroundColorDark,
                        borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                        borderTopColor = EdgeColorDark, borderBottomColor = EdgeColorDark,
                        borderLeftColor = EdgeColorDark, borderRightColor = EdgeColorDark,
                    }
                };
            }
            
            void BuildEditorInPlace()
            {
                // master pane
                creator_editor = Card("EDITOR");
                creator_editor.style.flexGrow = 1;
                creator_editor.style.flexDirection = FlexDirection.Row;
                creator_editor.style.backgroundColor = BackgroundColorDeepLight;

                // LEFT: identifier + fields (scroll)
                creator_editorLeft = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        width = Length.Percent(50),                     // left column width (tweak to taste)
                        minWidth = 300,
                        maxWidth = Length.Percent(50)
                    }
                };
                creator_editor.Add(creator_editorLeft);
                
                // Identifier card
                creator_identifier = new VisualElement
                {
                    style =
                    {
                        backgroundColor = BackgroundColorDark
                    }
                };
                creator_editorLeft.Add(creator_identifier);
                
                // Scroll for default fields
                var leftScroll = new ScrollView(ScrollViewMode.Vertical)
                {
                    style = { flexGrow = 1 }
                };
                creator_leftList = leftScroll.contentContainer;
                creator_editorLeft.Add(leftScroll);
                
                // RIGHT: buried stack (header + content)
                creator_editorRight = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Column,
                        flexGrow = 1
                    }
                };
                creator_editor.Add(creator_editorRight);
                
                // Stack header (Back, Clear, depth badge, path tokens)
                creator_buriedHeader = new VisualElement
                {
                    style =
                    {
                        backgroundColor = BackgroundColorDark,
                        flexDirection = FlexDirection.Row,
                        alignItems = Align.Center,
                        minHeight = 32,
                    }
                };
                creator_editorRight.Add(creator_buriedHeader);

                // Content surface
                var rightCard = new VisualElement
                {
                    style =
                    {
                        backgroundColor = BackgroundColorDeepDark,
                        paddingLeft = 8, paddingRight = 8, paddingTop = 8, paddingBottom = 8,
                        flexGrow = 1,
                        overflow = Overflow.Hidden
                    }
                };
                creator_buriedContent = new ScrollView { style = { flexGrow = 1 } }.contentContainer;
                rightCard.Add((VisualElement)creator_buriedContent.parent); // keep scroll view
                creator_editorRight.Add(rightCard);
            }
        }
        
        void OpenOptionsMenu_Creator(MouseUpEvent evt)
        {
            // Build a Unity menu and open it on click
            var optionsMenu = new GenericMenu();
            optionsMenu.AddItem(new GUIContent("Create as Template"), false, CreateItemAsTemplate_Creator);
            optionsMenu.AddSeparator("");
            optionsMenu.AddItem(new GUIContent("Discard Changes"), false, DiscardItemChanges_Creator);
            optionsMenu.AddItem(new GUIContent("Reset to Default"), false, ResetItemToDefault_Creator);
            optionsMenu.AddSeparator("");
            optionsMenu.AddItem(new GUIContent("Auto Save"), creator_autoSave, () => creator_autoSave = !creator_autoSave);
                
            var r = new Rect(evt.mousePosition, Vector2.zero);
            optionsMenu.DropDown(r);
            
        }
        
        void RefreshCreatorPage()
        {
            if (creator_focus.Kind == EDataType.None)
            {
                BuildWarnAlert("No creator focus given. Returning to Home.");
                SetPage(GasifyPage.Home);
                return;
            }
            
            RefreshCreatorEditorPane();
            
            /*if (creator_focus.Node.TagStatus(ForgeService.IS_DRAFT))
            {
                creator_saveBtn.style.display = DisplayStyle.None;
                
                creator_createButton.style.display = DisplayStyle.Flex;
                creator_createButton.text = "CREATE*";
                
                creator_optionsBtn.style.display = DisplayStyle.Flex;
                
            }
            else
            {
                creator_saveBtn.style.display = DisplayStyle.Flex;
                
                creator_createButton.style.display = DisplayStyle.None;
                creator_saveBtn.text = "Save" + SaveStatus(creator_focus.Node);
                
                creator_optionsBtn.style.display = DisplayStyle.Flex;
            }#1#
            
            RefreshNavigationBar();
        }
        
        void WhenSetCreatorPage()
        {
            /*if (creator_focus.Kind == EDataType.None) return;
            creator_lastFocus = new CreatorItem(creator_focus.Kind, creator_focus.Node);
            
            if (lastPage == GasifyPage.Creator)
            {
                if (creator_lastFocus.Node.TagStatus(ForgeService.IS_DRAFT))
                {
                    BuildInformationAlert($"{DataTypeText(creator_lastFocus.Kind)} draft has been discarded.");
                    creator_lastFocus.Kind = EDataType.None;
                }
                else if (creator_lastFocus.Node.TagStatus(ForgeService.HAS_UNSAVED_WORK))
                {
                    BuildWarnAlert($"{DataTypeText(creator_lastFocus.Kind)} has unsaved work.");
                    // TODO add save/discard changes buttons
                }
                else
                {
                    BuildInformationAlert($"{DataTypeText(creator_lastFocus.Kind)} has been saved.");
                }
            }#1#
            
            RefreshCreatorPage();
        }

        void WhenLeaveCreatorPage()
        {
            if (creator_focus.Kind == EDataType.None)
            {
                creator_lastFocus.Kind = EDataType.None;
                return;
            }

            /*if (creator_focus.Node.TagStatus(ForgeService.IS_DRAFT))
            {
                DiscardDraft_Creator();
                
                creator_lastFocus = creator_focus;
                creator_focus.Kind = EDataType.None;
            }
            else if (creator_focus.Node.TagStatus(ForgeService.IS_CREATED))
            {
                SaveItem_Creator();

                creator_lastFocus = creator_focus;
                creator_focus.Kind = EDataType.None;
            }#1#
        }
        
        #region Editor
        
        void RefreshCreatorEditorPane()
        {
            if (creator_focus.Kind == EDataType.None) return;
            
            // Identifier
            BuildIdentifierCard(creator_focus);

            // Left: default fields
            creator_leftList.Clear();
            
            var fields = GetEditableFields(creator_focus.Node.GetType());

            var registry = GasifyRegistry.DefaultRegistry();

            int count = 0;
            foreach (var f in fields)
            {
                var ft = f.FieldType;

                if (f.Name == "Id") continue; // shown in Identifier card only
                if (f.Name == "editorTags") continue;

                count += 1;

                if (IsSimpleType(ft))
                {
                    var row = registry.Find(ft).Draw(creator_focus.Node, f, MarkDirtyOnChange_Creator, MarkDirtyFocusIn_Creator, MarkDirtyOnFocusOut_Creator);
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.paddingLeft = 8;
                    row.style.paddingRight = 8;
                    row.style.paddingBottom = 5;
                    row.style.paddingTop = 5;
                    row.style.backgroundColor = BackgroundColorDark * .55f;
                    row.style.alignItems = Align.Center;
                    row.style.marginLeft = 0;
                    row.style.marginTop = 0;
                    row.style.marginBottom = 0;
                    row.style.marginRight = 0;
                    
                    if (count == 1)
                    {
                        row.style.borderTopWidth = 2;
                        row.style.borderTopColor = EdgeColorDark;
                    }

                    if (count == 2)
                    {
                        row.style.borderBottomWidth = 4;
                        row.style.borderBottomColor = EdgeColorDark;
                    }
                    else
                    {
                        row.style.borderBottomWidth = 2;
                        row.style.borderBottomColor = EdgeColorDark;
                    }
                    
                    creator_leftList.Add(row);

                    // If this is the Name field, keep the Identifier in sync
                    if (f.Name == "Name")
                    {
                        var tf = row as TextField ?? row.Q<TextField>();
                        if (tf != null)
                        {
                            tf.RegisterCallback<FocusOutEvent>(_ =>
                            {
                                creator_identifierNameLbl.text = string.IsNullOrEmpty(tf.text) ? $"Unnamed {DataTypeText(creator_focus.Kind)}" : tf.text;
                                /*if (creator_focus.Node.TagStatus(ForgeService.IS_DRAFT) || creator_focus.Node.TagStatus(ForgeService.HAS_UNSAVED_WORK))
                                    creator_identifierNameLbl.text += "*";#1#
                            });
                        }
                    }
                }
                else
                {
                    var row = MakeReferenceRow(creator_focus, f);
                    if (count == 1)
                    {
                        row.style.borderTopWidth = 2;
                        row.style.borderTopColor = EdgeColorDark;
                    }
                    row.style.borderBottomWidth = 2;
                    row.style.borderBottomColor = EdgeColorDark;
                    creator_leftList.Add(row);
                }
            }
            
            // Right: (re)render the current buried frame
            RefreshCreatorBuriedPane();
        }
        
        void MarkDirtyOnChange_Creator()
        {
            // creator_focus.Node.SetEditorTag(EditorTagService.HAS_UNSAVED_WORK, true);
        }

        void MarkDirtyFocusIn_Creator()
        {
            
        }
        
        void MarkDirtyOnFocusOut_Creator()
        {
            
        }

        VisualElement Spacer => new VisualElement() { style = { flexGrow = 1 } };
        
        private void RefreshCreatorBuriedPane()
        {
            // Header
            
            
            creator_buriedHeader.Clear();

            creator_buriedHeader.style.backgroundColor = BackgroundColorDark;

            var back = MakeNavButton(icon_BACK, PopBuried, SecondaryButtonColor, SecondaryButtonColorHover, "Return to last element");
            back.style.height = 22;
            back.style.marginLeft = 12;
            back.SetEnabled(creator_buriedStack.Count > 0);
            creator_buriedHeader.Add(back);

            // Breadcrumb tokens
            for (int i = 0; i < creator_buriedStack.Count; i++)
            {
                int idx = i;
                var chip = new Button(() =>
                {
                    // jump to index (truncate tail)
                    if (idx >= 0 && idx < creator_buriedStack.Count)
                    {
                        creator_buriedStack.RemoveRange(idx + 1, creator_buriedStack.Count - 1 - idx);
                        RefreshCreatorBuriedPane();
                    }
                })
                {
                    text = creator_buriedStack[i].PathToken
                };
                chip.style.height = 22;
                chip.style.backgroundColor = BackgroundColorLight;
                chip.style.color = TextColorLight;
                chip.style.borderTopLeftRadius = 6; chip.style.borderTopRightRadius = 6;
                chip.style.borderBottomLeftRadius = 6; chip.style.borderBottomRightRadius = 6;
                creator_buriedHeader.Add(chip);

                if (i == creator_buriedStack.Count - 1) continue;
                
                var sep = new Label("/")
                {
                    style =
                    {
                        marginLeft = 0,
                        marginRight = 0
                    }
                };
                creator_buriedHeader.Add(sep);

            }

            /*var spacer = new VisualElement { style = { flexGrow = 1 } };
            creator_buriedHeader.Add(spacer);

            // Title of current frame
            var _title = new Label(creator_buriedStack.LastOrDefault()?.Title ?? "")
            {
                style = { color = TextColorSub }
            };
            creator_buriedHeader.Add(_title);#1#

            // Content
            creator_buriedContent.Clear();
            if (creator_buriedStack.Count > 0)
                creator_buriedContent.Add(creator_buriedStack[^1].BuildUI?.Invoke() ?? new Label("(empty)"));
            else
                creator_buriedContent.Add(new Label($"Nothing selected. Click {Quotify("Edit →")} on a complex field to open it here.")
                { style = { color = TextColorSub, flexWrap = Wrap.Wrap} });
        }
        
        private void PushBuried(CreatorItem root, FieldInfo fi, int pushDepth)
        {
            // Create frame on demand
            string token = ObjectNames.NicifyVariableName(fi.Name);
            string _title = $"{DataTypeText(root.Kind)} › {token}";
            
            if (pushDepth == 0) ClearBuried();

            creator_buriedStack.Add(new BuriedFrame(_title, token, () =>
            {
                var panel = new VisualElement { style = { flexDirection = FlexDirection.Column } };

                var ft = fi.FieldType;
                var registry = GasifyRegistry.DefaultRegistry();

                if (IsList(ft))
                {
                    // Reuse the List drawer but mount it here
                    var ve = registry.Find(ft).Draw(root.Node, fi, MarkDirtyOnChange_Creator, MarkDirtyFocusIn_Creator, MarkDirtyOnFocusOut_Creator);
                    panel.Add(ve);
                }
                else if (IsDataNode(ft))
                {
                    // Draw the referenced node inline (identifier at top + its default fields)
                    var refNode = fi.GetValue(root.Node) as ForgeDataNode;
                    if (refNode == null)
                    {
                        panel.Add(new Label("No reference set.") { style = { color = TextColorSub } });
                    }
                    else
                    {
                        // mini-identifier
                        var mini = new VisualElement
                        {
                            style =
                            {
                                backgroundColor = BackgroundColorDark,
                                borderTopLeftRadius = 6, borderTopRightRadius = 6,
                                borderBottomLeftRadius = 6, borderBottomRightRadius = 6,
                                paddingLeft = 8, paddingRight = 8, paddingTop = 6, paddingBottom = 6
                            }
                        };
                        mini.Add(new Label($"{refNode.Name}  (ID: {refNode.Id})") { style = { unityFontStyleAndWeight = FontStyle.Bold, color = TextColorLight } });
                        panel.Add(mini);

                        // default fields of referenced node (simple only; complex become more buried frames)
                        var fields = GetEditableFields(refNode.GetType()).ToList();
                        foreach (var f2 in fields)
                        {
                            var ft2 = f2.FieldType;
                            if (f2.Name == "Id") continue;

                            if (IsSimpleType(ft2))
                                panel.Add(registry.Find(ft2).Draw(refNode, f2, MarkDirtyOnChange_Creator, MarkDirtyFocusIn_Creator, MarkDirtyOnFocusOut_Creator));
                            else
                                panel.Add(MakeReferenceRow(new CreatorItem(root.Kind, refNode), f2, creator_buriedStack.Count + 1));
                        }
                    }
                }
                else
                {
                    // Struct or other complex type: try default drawer or a fallback message
                    var drawer = GasifyRegistry.DefaultRegistry().Find(ft);
                    panel.Add(drawer.Draw(root.Node, fi, MarkDirtyOnChange_Creator, MarkDirtyFocusIn_Creator, MarkDirtyOnFocusOut_Creator));
                }

                return panel;
            }));

            RefreshCreatorBuriedPane();
        }

        private void PopBuried()
        {
            if (creator_buriedStack.Count > 0)
                creator_buriedStack.RemoveAt(creator_buriedStack.Count - 1);

            RefreshCreatorBuriedPane();
        }

        private void ClearBuried()
        {
            creator_buriedStack.Clear();
            RefreshCreatorBuriedPane();
        }
        
        #endregion

        #region Helpers
        
        List<FieldInfo> GetEditableFields(Type type)
        {
            // 1) Resolve source type (if mirrored), build its declaration order map
            var mirror = type.GetCustomAttribute<MirrorFromAttribute>();
            Type sourceType = mirror?.SourceType;

            // name -> index in source declaration order (base-first)
            Dictionary<string,int> sourceDeclIndex = null;
            if (sourceType != null)
            {
                sourceDeclIndex = GetDeclChain(sourceType)
                    .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                    .Select((f, i) => (f, i))
                    .ToDictionary(x => x.f.Name, x => x.i, StringComparer.Ordinal);
            }

            // 2) Walk the target (data) inheritance chain, gather public instance fields
            var targetFields = GetDeclChain(type)
                .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                .ToList();

            // 3) Filter out base/infra fields
            static bool IsInfra(FieldInfo f)
                => f.Name is "Id" or "editorTags";

            // Check GasifyHidden on target or (if missing) on source
            bool IsHidden(FieldInfo tf)
            {
                if (tf.IsDefined(typeof(ForgeHiddenAttribute), true)) return true;
                if (sourceType == null) return false;
                var sf = sourceType.GetField(tf.Name, BindingFlags.Public | BindingFlags.Instance);
                return sf != null && sf.IsDefined(typeof(ForgeHiddenAttribute), true);
            }

            // Read GasifyOrder from target or (if missing) from source
            int? GetOrder(FieldInfo tf)
            {
                if (tf.Name == "Name") return -2;
                if (tf.Name == "Description") return -1;
                
                var a = tf.GetCustomAttribute<ForgeOrderAttribute>(true);
                if (a != null) return a.Order;
                if (sourceType != null)
                {
                    var sf = sourceType.GetField(tf.Name, BindingFlags.Public | BindingFlags.Instance);
                    var a2 = sf?.GetCustomAttribute<ForgeOrderAttribute>(true);
                    if (a2 != null) return a2.Order;
                }
                return null;
            }

            var editable = targetFields
                .Where(f => !IsInfra(f))
                .Where(f => !IsHidden(f))
                .Select(f => new {
                    Field = f,
                    Order = GetOrder(f) ?? int.MaxValue,
                    SrcIdx = sourceDeclIndex != null && sourceDeclIndex.TryGetValue(f.Name, out var idx) ? idx : int.MaxValue,
                    // stable fallback: metadata token
                    Fallback = f.MetadataToken
                })
                .OrderBy(x => x.Order)
                .ThenBy(x => x.SrcIdx)
                .ThenBy(x => x.Fallback)
                .Select(x => x.Field)
                .ToList();
            
            

            return editable;

            // --- helpers ---
            static IEnumerable<Type> GetDeclChain(Type t)
            {
                var chain = new List<Type>();
                for (var cur = t; cur != null && cur != typeof(object); cur = cur.BaseType)
                    chain.Add(cur);
                chain.Reverse(); // base-first
                return chain;
            }
        }
        
        private VisualElement MakeReferenceRow(CreatorItem item, FieldInfo fi, int pushDepth = 0)
        {
            var ft = fi.FieldType;
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = BackgroundColorDark * .75f,
                    paddingLeft = 8, paddingRight = 8, paddingTop = 4, paddingBottom = 4,
                }
            };

            var label = new Label(GasifyRegistry.GetDrawerLabel(fi))
            {
                style = { flexGrow = 1, color = TextColorLight }
            };
            row.Add(label);
            
            AttachImportChip(row, creator_focus.Node, fi, () => { });

            var openBtn = SecondaryButton("→", () =>
            {
                PushBuried(item, fi, pushDepth);
            }, height: 22, radius: 6);
            row.Add(openBtn);

            return row;

            // Attach this to each field row you build in Creator/Inspector
            void AttachImportChip(
                VisualElement fieldRow,
                ForgeDataNode targetNode,
                System.Reflection.FieldInfo fieldInfo,
                System.Action onChanged)
            {
                // wrapper row to host field + chip
                /*var wrap = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                fieldRow.style.flexGrow = 1;
                wrap.Add(fieldRow);#1#
                
                var chip = BuildIconChip(icon_IMPORT, 3, 22, 9, tooltip: $"Import from other {DataTypeText(creator_focus.Kind)}...");
                chip.style.backgroundColor = BackgroundColorDeepLight;

                chip.RegisterCallback<PointerUpEvent>(evt =>
                {
                    // Gather candidates of the same node type (excluding self)
                    var candidates = EnumerateSameTypeNodes(targetNode)
                        .Where(n => !ReferenceEquals(n, targetNode) && !n.TagStatus(ForgeTags.IS_SAVED_COPY))
                        .OrderBy(n => n.Name ?? "")
                        .ToList();

                    // Build popup & show near the chip
                    var popup = new ImportValuePopup(
                        title: $"{fieldInfo.Name} : Import from other {DataTypeText(creator_focus.Kind)}…",
                        candidates: candidates,
                        onPick: picked =>
                        {
                            CopyFieldValue(fieldInfo, fromNode: picked, toNode: targetNode);
                            onChanged?.Invoke();
                        });

                    // Convert UI Toolkit position to screen rect for the popup anchor
                    var local = evt.position;                                // panel coords
                    var screen = new Vector2(local.x, local.y);
                    var anchor = new Rect(screen, new Vector2(1, 1));
                    UnityEditor.PopupWindow.Show(anchor, popup);
                });

                chip.AttachHoverCallbacks(BackgroundColorDeepLight);

                fieldRow.Add(chip);
            }
            
            static void CopyFieldValue(System.Reflection.FieldInfo field, ForgeDataNode fromNode, ForgeDataNode toNode)
            {
                if (field == null || fromNode == null || toNode == null) return;

                var src = field.GetValue(fromNode);
                var cloned = CloneValue(src, field.FieldType);
                field.SetValue(toNode, cloned);
            }

            static object CloneValue(object src, System.Type fieldType)
            {
                if (src == null) return null;

                // value types & strings: just copy
                if (fieldType.IsValueType || fieldType == typeof(string)) return src;

                // IList<T>
                if (typeof(System.Collections.IList).IsAssignableFrom(fieldType) && fieldType.IsGenericType)
                {
                    var elemT = fieldType.GetGenericArguments()[0];
                    var list = (System.Collections.IList)System.Activator.CreateInstance(fieldType);
                    foreach (var item in (System.Collections.IEnumerable)src)
                        list.Add(CloneValue(item, elemT));
                    return list;
                }

                // IDictionary<TKey,TValue>
                if (typeof(System.Collections.IDictionary).IsAssignableFrom(fieldType) && fieldType.IsGenericType)
                {
                    var keyT = fieldType.GetGenericArguments()[0];
                    var valT = fieldType.GetGenericArguments()[1];
                    var dict = (System.Collections.IDictionary)System.Activator.CreateInstance(fieldType);
                    foreach (System.Collections.DictionaryEntry kv in (System.Collections.IDictionary)src)
                        dict.Add(CloneValue(kv.Key, keyT), CloneValue(kv.Value, valT));
                    return dict;
                }

                // Fallback: JSON round-trip (editor-safe; uses your settings/converters)
                try
                {
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(src, ForgeJsonSettings.Settings);
                    return Newtonsoft.Json.JsonConvert.DeserializeObject(json, fieldType, ForgeJsonSettings.Settings);
                }
                catch
                {
                    // As a last resort, return the original reference
                    return src;
                }
            }
            
            IEnumerable<ForgeDataNode> EnumerateSameTypeNodes(ForgeDataNode self)
            {
                var proj = Project;
                if (proj == null || self == null) yield break;

                var t = self.GetType();

                // Fast path: find public List<T> fields whose T == self.GetType()
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
                foreach (var f in proj.GetType().GetFields(flags))
                {
                    if (!f.FieldType.IsGenericType) continue;
                    var gen = f.FieldType.GetGenericTypeDefinition();
                    if (gen != typeof(System.Collections.Generic.List<>)) continue;

                    var elemT = f.FieldType.GetGenericArguments()[0];
                    if (elemT != t) continue;

                    var list = f.GetValue(proj) as System.Collections.IEnumerable;
                    if (list == null) continue;
                    foreach (var n in list) yield return (ForgeDataNode)n;
                }
            }
        }

        class ImportValuePopup : PopupWindowContent
        {
            private readonly string _title;
            readonly List<ForgeDataNode> _nodes;
            readonly System.Action<ForgeDataNode> _onPick;

            string _query = "";
            Vector2 _scroll;

            public ImportValuePopup(string title, List<ForgeDataNode> candidates, System.Action<ForgeDataNode> onPick)
            {
                _title = title;
                _nodes = candidates ?? new List<ForgeDataNode>();
                _onPick = onPick;
            }

            public override Vector2 GetWindowSize() => new Vector2(360, 360);

            public override void OnGUI(Rect rect)
            {
                var line = EditorGUIUtility.singleLineHeight;

                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18)))
                {
                    GUILayout.Space(7);
                    EditorGUILayout.LabelField(_title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("x"))
                    {
                        editorWindow.Close();
                        GUIUtility.ExitGUI();
                    }
                    GUILayout.Space(5);
                }
#if UNITY_2021_2_OR_NEWER
                _query = EditorGUILayout.TextField(GUIContent.none, _query, EditorStyles.toolbarSearchField);
#else
        _query = EditorGUILayout.TextField(_query);
#endif
                EditorGUILayout.Space(2);

                var q = (_query ?? "").Trim().ToLowerInvariant();
                IEnumerable<ForgeDataNode> filtered = _nodes;
                if (q.Length > 0)
                    filtered = _nodes.Where(n =>
                        (!string.IsNullOrEmpty(n.Name) && n.Name.ToLowerInvariant().Contains(q)) ||
                        n.Id.ToString().Contains(q));

                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                int count = 0;
                Color old = GUI.backgroundColor;
                foreach (var n in filtered)
                {
                    string _name = n.TagStatus(ForgeTags.IS_TEMPLATE) ? $"{n.Name} (Template)" : n.Name ?? $"<{n.Id}>";
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18)))
                    {
                        GUI.backgroundColor = count++ % 2 == 0 ? new Color(.35f, .35f, .35f) : new Color(.55f, .55f, .55f);
                        GUILayout.Space(7);
                        if (GUILayout.Button(_name, GUILayout.ExpandWidth(true)))
                        {
                            _onPick?.Invoke(n);
                            editorWindow.Close();
                            GUIUtility.ExitGUI();
                        }

                        GUI.backgroundColor = old;

                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"Id: {IntToHex(n.Id)}");
                        GUILayout.Space(7);
                    }
                }

                EditorGUILayout.EndScrollView();

                if (!_nodes.Any())
                    EditorGUILayout.HelpBox("No other nodes of this type found.", MessageType.Info);
            }
        }
        
        

        private void BuildIdentifierCard(CreatorItem item)
        {
            creator_identifier.Clear();
            
            // Row (icon + name + type chip ... spacer ... ID)
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    minHeight = 32
                }
            };
            creator_identifier.Add(row);

            // --- Circular icon badge (extra inset from the left) ---
            var iconBadge = new VisualElement
            {
                style =
                {
                    width = 22, height = 22,
                    marginLeft = 4,                 // extra spacing from the left edge
                    marginRight = 4,
                    maxHeight = 28,
                    maxWidth = 28,
                    backgroundColor = BackgroundColorDeepLight,   // light gray circle
                    borderTopLeftRadius = 18, borderTopRightRadius = 18,
                    borderBottomLeftRadius = 18, borderBottomRightRadius = 18,
                    alignItems = Align.Center, justifyContent = Justify.Center,
                    paddingLeft = 4,
                    flexShrink = 0
                }
            };
            row.Add(iconBadge);

            Texture2D icon = item.Kind switch
            {
                EDataType.Ability   => icon_ABILITY,
                EDataType.Effect    => icon_EFFECT,
                EDataType.Entity    => icon_ENTITY,
                EDataType.Attribute => icon_ATTRIBUTE,
                EDataType.Tag       => icon_TAG,
                EDataType.AttributeSet => icon_ATTRIBUTE_SET,
                _ => icon_HOME
            };

            var img = new Image
            {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                style = { width = 16, height = 16, unityBackgroundImageTintColor = Color.white, flexShrink = 0 }
            };
            iconBadge.Add(img);

            string lbl = (item.Node?.Name ?? $"Unnamed {DataTypeText(creator_focus.Kind)}") + SaveStatus(item.Node);
            // --- Live Name (from the data node) ---
            creator_identifierNameLbl = new Label(lbl)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = TextColorLight,
                    fontSize = 14
                }
            };
            row.Add(creator_identifierNameLbl);

            // Type chip
            creator_identifierTypeChipLbl = new Label(DataTypeText(item.Kind))
            {
                style =
                {
                    backgroundColor = BackgroundColorLight,
                    color = TextColorLight,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                    paddingLeft = 6, paddingRight = 6, paddingTop = 2, paddingBottom = 2,
                    marginLeft = 4,
                    fontSize = 9
                }
            };
            row.Add(creator_identifierTypeChipLbl);

            foreach (var chip in GetChipLabels(creator_focus.Node))
            {
                row.Add(chip);
            }

            // spacer
            row.Add(new VisualElement { style = { flexGrow = 1 } });

            // ID (read-only display)
            creator_identifierIdLbl = new Label($"ID: {IdDisplayText(item)}")
            {
                style = { color = TextColorSub, paddingRight = 6}
            };
            row.Add(creator_identifierIdLbl);
        }

        List<VisualElement> GetChipLabels(ForgeDataNode node, int radius = 3, int size = 14, int inset = 3)
        {
            var chips = new List<VisualElement>();
            
            if (node.TagStatus(ForgeTags.IS_TEMPLATE)) chips.Add(BuildIconChip(icon_TEMPLATE, radius, size, inset, "This item is a template"));
            //if (node.TagStatus<int>(ForgeService.ERROR_ALERTS) > 0) chips.Add(BuildIconChip(icon_WARN, radius, size, inset, "This item has missing references"));
            if (!node.TagStatus(ForgeTags.EDITABLE)) chips.Add(BuildIconChip(icon_NO_EDIT, radius, size, inset, "This item is not editable"));

            return chips;
        }

        VisualElement BuildIconChip(Texture2D icon, int radius = 3, int size = 14, int icon_offset = 3, string tooltip = null)
        {
            // --- Circular icon badge (extra inset from the left) ---
            var iconBadge = new VisualElement
            {
                style =
                {
                    width = size, height = size,
                    marginLeft = 3,                 // extra spacing from the left edge
                    //marginRight = 3,
                    // maxHeight = size + icon_offset,
                    // maxWidth = size + icon_offset,
                    backgroundColor = BackgroundColorLight,   // light gray circle
                    borderTopLeftRadius = radius, borderTopRightRadius = radius,
                    borderBottomLeftRadius = radius, borderBottomRightRadius = radius,
                    alignItems = Align.Center, justifyContent = Justify.Center,
                    flexShrink = 0,
                    overflow = Overflow.Hidden,
                }
            };

            var img = new Image
            {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                style =
                {
                    width = size - icon_offset, 
                    height = size - icon_offset, 
                    unityBackgroundImageTintColor = Color.white, 
                    flexShrink = 0,
                    alignSelf = Align.Center
                },
                pickingMode = PickingMode.Ignore
            };

            if (tooltip is not null)
            {
                iconBadge.tooltip = tooltip;
            }
            
            iconBadge.Add(img);

            return iconBadge;
        }
        
        string IdDisplayText(CreatorItem item)
        {
            return item.Node is not null ? (item.Id >= 0 ? IntToHex(item.Id) : "None") : "--";
        }
        
        public static string IntToHex(int id)
        {
            string hex = id.ToString("X8");
            return hex;
        }

        public static string Quotify(string body) => $"“{body}”";
        
        public static bool IsSimpleType(Type t)
        {
            return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(float) ||
                   t == typeof(double) || t == typeof(int) || t == typeof(bool);
        }
        public static bool IsList(Type t) => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
        public static bool IsDataNode(Type t) => typeof(ForgeDataNode).IsAssignableFrom(t) && t != typeof(ForgeDataNode);

        string SaveStatus(ForgeDataNode node)
        {
            return node.TagStatus(ForgeTags.HAS_UNSAVED_WORK) ? "*" : "";
        }
        
        #endregion
        
        #region Import Popup

        private VisualElement importPopup;

        void BuildImportPopup()
        {
            var card = new VisualElement
            {
                name = "PopupAlert",
                style =
                {
                    minWidth = 350,
                    maxWidth = 350,
                    backgroundColor = AlertColor_Body,
                    borderTopLeftRadius = 8, borderTopRightRadius = 8,
                    borderBottomLeftRadius = 8, borderBottomRightRadius = 8,
                    borderTopWidth = 1, borderRightWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1,
                    borderTopColor = EdgeColorDark,
                    borderRightColor = EdgeColorDark,
                    borderBottomColor = EdgeColorDark,
                    borderLeftColor = EdgeColorDark,
                    overflow = Overflow.Hidden
                }
            };
            
            // ===== Header =====
            var header = new VisualElement
            {
                name = "Header",
                style =
                {
                    backgroundColor = AlertColor_Info,
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8, paddingRight = 8, paddingTop = 6, paddingBottom = 6
                }
            };

            var _title = new Label("Header")
            {
                name = "TitleCard",
                style =
                {
                    color = TextColorLight,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexGrow = 1,
                    whiteSpace = WhiteSpace.NoWrap,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            };
            header.Add(_title);
            
            /#1#/ Close button
            var closeBtn = new Button(() => Dismiss(card)) { text = "×" };
            closeBtn.style.width = 22;
            closeBtn.style.height = 18;
            closeBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(closeBtn);#1#

            card.Add(header);
            
            // ===== Body =====
            var body = new VisualElement
            {
                style =
                {
                    color = TextColorLight,
                    paddingLeft = 10, paddingRight = 10, paddingTop = 8, paddingBottom = 8,
                    whiteSpace = WhiteSpace.Normal,
                    minHeight = 350
                }
            };
            card.Add(body);
        }

        void RefreshImportPopup<T>(EDataType kind, string fieldName, ref T arg)
        {
            
        }

        void OpenImportPopup<T>(EDataType kind, string fieldName, ref T arg)
        {
            importPopup.style.display = DisplayStyle.Flex;
            
        }

        void OnSelectImportItem<T>([NotNull] ref T arg, T data)
        {
            if (arg == null) throw new ArgumentNullException(nameof(arg));
            arg = data;
        }
        
        #endregion

        #region Actions

        /// <summary>
        /// Set creator focus and open creator page. Called specifically from Home/ProjectView.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="_name"></param>
        /// <param name="kind"></param>
        void SetCreatorPage(int id, string _name, EDataType kind)
        {
            var status = Project.TryGet(id, _name, kind, out var node);
            if (!status)
            {
                ShowUnityMessage("Error", $"Could not find {DataTypeText(kind)}-{_name} with Id={id}");
                return;
            }

            var item = new CreatorItem(kind, node);

            SetCreatorPage(item);
        }

        void SetCreatorPageLastFocus()
        {
            if (creator_lastFocus.Kind == EDataType.None) return;
            SetCreatorPage(creator_lastFocus);
        }

        void SetCreatorPageAgain()
        {
            if (creator_focus.Kind == EDataType.None) return;

            /*if (creator_focus.Node.TagStatus(EditorTagService.IS_DRAFT))
            {
                BuildInformationAlert($"{DataTypeText(creator_focus.Kind)} draft has been discarded.");
            }
            else
            {
                SaveItem_Creator();
            }#1#
            
            LoadIntoCreator(creator_focus.Kind);
        }
        
        void SetCreatorPage(CreatorItem item)
        {
            creator_lastFocus = creator_focus;
            creator_focus = item;
            creator_focusCopy = Project.TryGet(item.Node.TagStatus<int>(ForgeTags.COPY_ID), item.Kind, out var copy) ? new CreatorItem(item.Kind, copy) : CreatorItem.None();
            
            SetPage(GasifyPage.Creator);
        }

        void DiscardDraft_Creator()
        {
            if (creator_focus.Kind == EDataType.None) return;
            BuildInformationAlert($"{DataTypeText(creator_focus.Kind)} draft has been discarded.");
            
            creator_focus.Kind = EDataType.None;
            creator_lastFocus.Kind = EDataType.None;
        }
        
        /// <summary>
        /// Save a previously created data
        /// </summary>
        void SaveItem_Creator()
        {
            if (creator_focus.Kind == EDataType.None) return;
 
            /*if (creator_focus.Node.TagStatus(ForgeService.IS_DRAFT))
            {
                BuildErrorAlert("Cannot save a draft item. Draft items must be created before being saved. How did you even manage to get here?!");
                return;
            }#1#

            // if (!creator_focus.Node.TagStatus(EditorTagService.HAS_UNSAVED_WORK)) return;
            
            /*if (creator_focus.Node.TagStatus<int>(ForgeService.ERROR_ALERTS) > 0)
            {
                BuildWarnAlert($"{DataTypeText(creator_focus.Kind)} {Quotify(creator_focus.Name)} has been saved, but contains missing references.\nMissing references must be resolved before this item is buildable.");
            }
            else
            {
                BuildInformationAlert($"{DataTypeText(creator_focus.Kind)} {Quotify(creator_focus.Name)} has been saved.");
            }#1#
            
            Project.Save(creator_focus.Node, creator_focus.Kind);
            LoadProjectData();
            
            RefreshCreatorPage();
        }
        
        /// <summary>
        /// Creates data from draft.
        /// Adds to
        /// </summary>
        void CreateItem_Creator()
        {
            if (creator_focus.Kind == EDataType.None) return;

            if (Project.DataWithNameExists(creator_focus.Name, creator_focus.Kind))
            {
                BuildErrorAlert($"Cannot create item. {DataTypeText(creator_focus.Kind)} {Quotify(creator_focus.Name)} already exists");
                return;
            }
            
            /*if (creator_focus.Node.TagStatus<int>(ForgeService.ERROR_ALERTS) > 0)
            {
                BuildWarnAlert($"{DataTypeText(creator_focus.Kind)} {Quotify(creator_focus.Name)} has been created, but contains missing references.\nMissing references must be resolved before this item is buildable.");
            }
            else
            {
                BuildInformationAlert($"{DataTypeText(creator_focus.Kind)} {Quotify(creator_focus.Name)} has been created.");
            }#1#

            Project.Create(creator_focus.Node, creator_focus.Kind);
            LoadProjectData();
            
            SetSetting(ForgeTags.Settings.FW_HAS_UNSAVED_WORK, true);
            SaveFramework(false);
            
            RefreshCreatorPage();
        }

        void CreateItemAsTemplate_Creator()
        {
            if (creator_focus.Kind == EDataType.None) return;
            
            /*if (creator_focus.Node.TagStatus<int>(ForgeService.ERROR_ALERTS) > 0)
            {
                BuildWarnAlert($"{DataTypeText(creator_focus.Kind)} {Quotify(creator_focus.Name)} cannot be saved as template because it contains missing references.");
            }
            else
            {
                BuildInformationAlert($"{DataTypeText(creator_focus.Kind)} {Quotify(creator_focus.Name)} has been saved as template.");
            }#1#
            
            /*if (creator_focus.Node.TagStatus(ForgeService.IS_DRAFT)) Project.CreateTemplate(creator_focus.Node, creator_focus.Kind);
            else
            {
                var copy = Project.BuildTemplateClone(creator_focus.Kind, creator_focus.Node, out _);
                Project.CreateTemplate(copy, creator_focus.Kind);
            }#1#
            
            LoadProjectData();
            
            SetSetting(ForgeTags.Settings.FW_HAS_UNSAVED_WORK, true);
            SaveFramework(false);
            
            RefreshCreatorPage();
        }

        void DiscardItemChanges_Creator()
        {
            if (creator_focus.Kind == EDataType.None) return;

            Debug.Log("Discarding item changes");

            if (creator_focus.Node.TagStatus<int>(ForgeTags.COPY_ID) <= 0)
            {
                BuildErrorAlert("Could not find a valid saved copy to revert to");
                return;
            }
        }

        void ResetItemToTemplate_Creator()
        {
            if (creator_focus.Kind == EDataType.None) return;

            Debug.Log("Resetting to template values");
        }
        
        void ResetItemToDefault_Creator()
        {
            if (creator_focus.Kind == EDataType.None) return;

            Debug.Log("Resetting to default values");
        }
        
        #endregion
        
        #endregion
        
        #region Developer Page
        
        VisualElement BuildDeveloperPage()
        {
            var v = new VisualElement { style = { flexGrow = 1 } };
            v.Add(new Label("Developer page placeholder — Actions / Usage / Editor") { style = { unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 } });
            return v;
        }

        void WhenSetDeveloperPage()
        {
            
        }

        void WhenLeaveDeveloperPage()
        {
            
        }
        
        #endregion
        
        #region Stub Page

        VisualElement BuildStubPage()
        {
            var v = new VisualElement { style = { flexGrow = 1 } };
            v.Add(new Label("This page has no content.") { style = { unityTextAlign = TextAnchor.MiddleCenter, flexGrow = 1 } });
            return v;
        }
        
        #endregion

        private void OnDestroy()
        {
            SaveFramework(false);
        }
    }
    #endif
}
*/
