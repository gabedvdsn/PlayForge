using System;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeEditor
    {
        #region Unity Core
        
        public VisualTreeAsset m_InspectorUXML;

        private static PlayForgeEditor Instance;

        [MenuItem("Tools/PlayForge/The Forge", false, 0)]
        private static void ShowWindow()
        {
            var w = GetWindow<PlayForgeEditor>("PlayForge Editor");
            Instance = w;
            w.minSize = new Vector2(700, 600);
            w.Show();
        }

        [MenuItem("Tools/PlayForge/Documentation", false, 3)]
        private static void FollowDocumentationLink()
        {
            Application.OpenURL("https://example.com/your-framework-docs");
        }

        public static void OpenTo(string frameworkKey)
        {
            
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;
            root.style.flexShrink = 0;
            
            root.style.paddingLeft = 0;
            root.style.paddingRight = 0;
            root.style.paddingTop = 0;
            root.style.paddingBottom = 0;

            // Load the UXML file and clone its tree into the inspector.
            if (m_InspectorUXML != null)
            {
                VisualElement uxmlContent = m_InspectorUXML.CloneTree();
                root.Add(uxmlContent);
            }
            
            Setup();

            Bind();
            Build();

            AutoLoad();
            
            RefreshConsole();
            RefreshProjectView(true);
            RefreshHome();
            
            Initialize();
        }

        private void Reset()
        {
            Instance = this;
        }

        private void Setup()
        {
            undoStack = new UndoStack(capacity: undoStackSize);
            
            LoadIcons();
            
            SetupContent();
            SetupProjectView();
            SetupConsole();
        }

        private void Bind()
        {
            BindProjectView();
            
            BindContent();
            
            BindHeader();
            BindNavBar();
            
        }

        private void Build()
        {
            BuildProjectView();
            
            BuildContent();
            
            BuildHeader();
            BuildNavBar();
        }

        private void Refresh()
        {
            if (Project is not null) projectData = Project.GetCompleteNodes();
            
            RefreshProjectView();
            
            RefreshContent();
            RefreshHeader();
            
            RefreshNavBar();
            
            RefreshHome();
            RefreshCreator();
            RefreshAnalytics();
            RefreshDevelop();
            RefreshValidate();
            RefreshSettings();
            
            RefreshStore();
        }

        private void OnGUI()
        {
            var e = Event.current;

            if (EditorGUIUtility.editingTextField) return;

            if (e.type == EventType.KeyDown)
            {
                if (e.keyCode == KeyCode.P)
                {
                    TestProgressBar();
                }
            }
        }
        #endregion

        #region Core
        public FrameworkProject Project;
        private Dictionary<EDataType, List<ForgeDataNode>> projectData;

        private static ForgeJsonUtility.SettingsWrapper MasterSettings;
        private ForgeJsonUtility.SettingsWrapper LocalSettings;

        private const int undoStackSize = 100;
        private UndoStack undoStack;

        #region Loading
        private void LoadFramework(FrameworkProject fp)
        {
            DataIdRegistry.Reset();

            Project = fp;

            DataIdRegistry.RebuildFrom(Project);

            LocalSettings = ForgeStores.LoadLocalSettings(Project.MetaName);

            foreach (var kvp in Project.GetCompleteNodes())
            {
                foreach (var node in kvp.Value) ForgeTags.ValidateEditorTags(node);
            }

            LoadProjectData();
            
            LogConsoleEntry(Console.Framework.OnLoadFramework(Project.MetaName));
        }

        void LoadProjectData()
        {
            projectData = Project.GetCompleteNodes();
            var keys = projectData.Keys;
            foreach (var k in keys)
            {
                projectData[k].Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void AutoLoad()
        {
            try
            {
                // Load master settings
                MasterSettings = ForgeStores.LoadMasterSettings();
                // Make sure last opened exists
                if (MasterSettings.StatusCheck(ForgeTags.Settings.LAST_OPENED_FRAMEWORK, out string active))
                {
                    if (!ForgeStores.IterateFrameworkKeys().Contains(active)) SetMasterSetting(ForgeTags.Settings.LAST_OPENED_FRAMEWORK, "");
                }

                string sessionId = MasterSetting(ForgeTags.Settings.SESSION_ID, "");

                if (string.IsNullOrEmpty(sessionId))
                {
                    SetMasterSetting(ForgeTags.Settings.SESSION_ID, ForgeStores.SessionId);
                    return;
                }

                if (sessionId == ForgeStores.SessionId)
                {
                    QuickLoad(MasterSetting(ForgeTags.Settings.ACTIVE_FRAMEWORK, ""));
                }
                else
                {
                    SetMasterSetting(ForgeTags.Settings.SESSION_ID, ForgeStores.SessionId);
                }
            }
            catch (Exception ex)
            {
                LogConsoleEntry(Console.Framework.FailedToLoad());
            }
        }

        private void QuickLoad(string key)
        {
            var proj = ForgeStores.LoadFramework(key);
            if (proj is null) return;

            LoadFramework(proj);
        }

        private void Initialize()
        {
            DoContextAction(EForgeContextExpanded.Home);
        }
        
        #endregion

        #region Settings
        private bool Setting(Tag target, bool fallback = false)
        {
            return Setting<bool>(target, fallback);
        }

        private T Setting<T>(Tag target, T fallback = default)
        {
            if (LocalSettings is null) return fallback;
            if (LocalSettings.StatusCheck<T>(target, out var result) && result != null) return result;
            return fallback;
        }

        private bool MasterSetting(Tag target, bool fallback = false)
        {
            return MasterSetting<bool>(target, fallback);
        }

        private T MasterSetting<T>(Tag target, T fallback = default)
        {
            if (MasterSettings is null) return fallback;
            if (MasterSettings.StatusCheck<T>(target, out var obj) && obj != null) return obj;
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
        #endregion

        #region Icons
        private Texture2D icon_ABILITY;
        private Texture2D icon_EFFECT;
        private Texture2D icon_ENTITY;
        private Texture2D icon_ATTRIBUTE;
        private Texture2D icon_TAG;
        private Texture2D icon_ATTRIBUTE_SET;
        private Texture2D icon_UNKNOWN;

        private static Texture2D icon_SYSTEM;
        private static Texture2D icon_ERROR;
        private static Texture2D icon_WARNING;
        private static Texture2D icon_INFO;

        private static Texture2D icon_LINK;
        private static Texture2D icon_UNLINKED;

        private static Texture2D icon_REFRESH;

        private Texture2D GetDataIcon(EDataType kind)
        {
            return kind switch
            {

                EDataType.Ability => icon_ABILITY,
                EDataType.Effect => icon_EFFECT,
                EDataType.Entity => icon_ENTITY,
                EDataType.Attribute => icon_ATTRIBUTE,
                EDataType.Tag => icon_TAG,
                EDataType.AttributeSet => icon_ATTRIBUTE_SET,
                EDataType.None => icon_UNKNOWN,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
            };
        }

        private void LoadIcons()
        {
            icon_ABILITY = LoadForgeIcon("Creator", "ability.png");
            icon_EFFECT = LoadForgeIcon("Creator", "effect.png");
            icon_ENTITY = LoadForgeIcon("Creator", "person.png");
            icon_ATTRIBUTE = LoadForgeIcon("Creator", "attribute.png");
            icon_TAG = LoadForgeIcon("Creator", "tag.png");
            icon_ATTRIBUTE_SET = LoadForgeIcon("Creator", "attribute_set.png");
            icon_UNKNOWN = LoadForgeIcon("Creator", "unknown.png");

            icon_SYSTEM = EditorGUIUtility.IconContent("Prefab On Icon").image as Texture2D;
            icon_ERROR = EditorGUIUtility.IconContent("console.erroricon").image as Texture2D;
            icon_WARNING = EditorGUIUtility.IconContent("console.warnicon").image as Texture2D;
            icon_INFO = EditorGUIUtility.IconContent("console.infoicon").image as Texture2D;
            
            icon_LINK = EditorGUIUtility.IconContent("Linked@2x").image as Texture2D;
            icon_UNLINKED = EditorGUIUtility.IconContent("UnLinked@2x").image as Texture2D;
            
            icon_REFRESH = EditorGUIUtility.IconContent("d_Refresh@2x").image as Texture2D;
        }

        private Texture2D LoadForgeIcon(string ext, string file, string path = "Assets/PlayForge/Editor/Icons")
        {
#if UNITY_EDITOR
            string _path = $"{path}/{ext}/{file}";
            var t2d = AssetDatabase.LoadAssetAtPath<Texture2D>(_path);
            return t2d;
#else
            return null;
#endif
        }
        
        #endregion
        
        #endregion

        #region Helpers
        
        #region Colors
        
        public static Color ColorDark => new(0.2352941f, 0.2352941f, 0.2352941f, 1f);
        public static Color ColorLight => new(0.345098f, 0.345098f, 0.345098f, 1f);
        public static Color ColorSelected => new(0.2745098f, 0.3764706f, 0.4862745f, 1f);
        public static Color ColorHighlighted => new(242/(float)255, 219/(float)255, 44/(float)255, .75f);
        public static Color ColorSystem => new(85/(float)255, 11/(float)255, 94/(float)255, .5f);

        private static Color ColorValidationOk => new Color(32 / (float)255, 181 / (float)255, 27 / (float)255, 1f);
        private static Color ColorValidationWarn => new Color(201 / (float)255, 173 / (float)255, 30 / (float)255, 1f);
        private static Color ColorValidationError => new Color(201 / (float)255, 59 / (float)255, 30 / (float)255, 1f);
        private static Color ColorValidationNone => Color.gray;
        
        #endregion

        #region Data
        
        public class DataContainer
        {
            public EDataType Kind;
            public ForgeDataNode Node;

            public DataContainer(ForgeDataNode node, EDataType kind)
            {
                Node = node;
                Kind = kind;
            }
        }

        private class FocusContainer : DataContainer
        {
            public bool IsFocused => Kind != EDataType.None && Node is not null;

            public EForgeContext Context;
            
            public FocusContainer(ForgeDataNode node, EDataType kind) : base(node, kind)
            {
                
            }
            
            public void Reset()
            {
                Kind = EDataType.None;
                Node = null;
            }

            public void Set(DataContainer dc, EForgeContext ctx) => Set(dc.Node, dc.Kind, ctx);
            
            public void Set(ForgeDataNode node, EDataType kind, EForgeContext ctx)
            {
                Kind = kind;
                Node = node;
                Context = ctx;
            }
        }
        #endregion

        #region Other
        public static string DataTypeText(EDataType kind, bool withQuotes = false)
        {
            string header = kind switch
            {
                EDataType.Ability => "Ability",
                EDataType.Effect => "Effect",
                EDataType.Entity => "Entity",
                EDataType.Attribute => "Attribute",
                EDataType.Tag => "Tag",
                EDataType.AttributeSet => "Attribute Set",
                _ => kind.ToString()
            };
            return withQuotes ? Quotify(header) : header;
        }

        private static string DataTypeTextPlural(EDataType kind)
        {
            string header = kind switch
            {
                EDataType.Ability => "Abilities",
                EDataType.Effect => "Effects",
                EDataType.Entity => "Entities",
                EDataType.Attribute => "Attributes",
                EDataType.Tag => "Tags",
                EDataType.AttributeSet => "Attribute Sets",
                _ => kind.ToString()
            };
            return header;
        }

        public static string Quotify(string text)
        {
            return $"“{text}”";
        }
        
        #endregion
        
        #endregion
    }
}
