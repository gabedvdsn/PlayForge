using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks.Triggers;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class PlayForgeSettingsWindow : EditorWindow
    {
        public VisualTreeAsset m_InspectorUXML;
        
        // Provided by caller (your GasifyEditorWindow)
        private static System.Action _onChanged;  // callback: reload settings in the main window

        // Cached copies (so user can edit then Save)
        private ForgeJsonUtility.SettingsWrapper Master = new();
        private ForgeJsonUtility.SettingsWrapper Local  = new();

        private static string loadedFramework;
        private static FrameworkProject Project;
        
        private static SettingsPage lastActivePage = SettingsPage.Home;
        private static SettingsPage activePage = SettingsPage.Home;

        private Dictionary<SettingsPage, (Action, Action)> pageSetActions = new Dictionary<SettingsPage, (Action, Action)>();

        private static Color BackgroundDarker => new Color(.25f, .25f, .25f);
        private static Color BackgroundDark => new Color(.35f, .35f, .35f);
        private static Color BackgroundSub => new Color(.67f, .67f, .67f);
        private static Color BackgroundLight => new Color(.46f, .47f, .47f);

        private static string ChevronDown => "▼";
        private static string ChevronUp => "▲";
        private static string ChevronRight => "\u25b6";
        
        public enum SettingsPage
        {
            // Main
            Home,
            MasterSettings,
            Framework,
        }

        enum OptionRowType
        {
            Header,
            Framework
        }
        
        private class OptionRow
        {
            public string Text;
            public FrameworkProject Project;
            public SettingsPage Page;
            public OptionRowType Type;

            public OptionRow(string text, FrameworkProject project, SettingsPage page, OptionRowType type)
            {
                Text = text;
                Project = project;
                Page = page;
                Type = type;
            }
        }

        private List<OptionRow> options = new()
        {
            new OptionRow("Home", null, SettingsPage.Home, OptionRowType.Header),
            new OptionRow("Master Settings", null, SettingsPage.MasterSettings, OptionRowType.Header),
            new OptionRow("Frameworks", null, SettingsPage.Framework, OptionRowType.Header)
        };

        bool OptionIsActivePage(OptionRow data)
        {
            return activePage switch
            {
                SettingsPage.Home => data.Text == "Home",
                SettingsPage.MasterSettings => data.Text == "Master Settings",
                SettingsPage.Framework => false,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        [MenuItem("Tools/PlayForge/Settings", false, 2)]
        private static void ShowWindow()
        {
            Open("");
        }

        public static void SetFocus()
        {
            // set settings focus
        }

        public static PlayForgeSettingsWindow Open(string projectFilePath, System.Action onSettingsChanged = null)
        {
            loadedFramework      = projectFilePath; // simple + stable
            _onChanged       = onSettingsChanged;

            lastActivePage = SettingsPage.Home;
            activePage = SettingsPage.Home;

            var w = GetWindow<PlayForgeSettingsWindow>("Gasify Settings");
            w.minSize = new Vector2(560, 220);
            w.Show();

            return w;
        }

        public static void Open(string path, Action onChange, SettingsPage page, FrameworkProject proj)
        {
            var w = Open(path, onChange);
            
            w.LoadProject(proj);
            w.SetPage(page);
        }

        void SetPage(SettingsPage page)
        {
            lastActivePage = activePage;
            activePage = page;

            pageSetActions[lastActivePage].Item2?.Invoke();
            pageSetActions[activePage].Item1?.Invoke();
            
            RefreshOptionsList();
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;

            minSize = new Vector2(600, 400);
            maxSize = new Vector2(1000, 600);
            
            // Load the UXML file and clone its tree into the inspector.
            if (m_InspectorUXML != null)
            {
                VisualElement uxmlContent = m_InspectorUXML.CloneTree();
                root.Add(uxmlContent);
            }

            homePage = GetContent(SettingsPage.Home);
            masterSettingsPage = GetContent(SettingsPage.MasterSettings);
            
            frameworksPage = GetContent(SettingsPage.Framework);
            BuildFrameworksPage();

            homePage.style.display = DisplayStyle.None;
            masterSettingsPage.style.display = DisplayStyle.None;
            frameworksPage.style.display = DisplayStyle.None;

            Master = ForgeStores.LoadMasterSettings();
            
            SetupPageSetActions();
            SetupOptionsList();
            
            SetPage(activePage);
        }

        void SetupPageSetActions()
        {
            pageSetActions = new Dictionary<SettingsPage, (Action, Action)>()
            {
                { SettingsPage.Home, (SetHomePage, LeaveHomePage) },
                { SettingsPage.MasterSettings, (SetMasterSettingsPage, LeaveMasterSettingsPage) },
                { SettingsPage.Framework, (SetFrameworksPage, LeaveFrameworksPage) },
            };
        }

        #region Options List
        
        void SetupOptionsList()
        {
            var root = rootVisualElement.Q<VisualElement>("Root");
            var optCon = root.Q<VisualElement>("OptionsCon");
            var optList = optCon.Q<ListView>("OptionsList");

            optList.itemsSource = options;

            optList.makeItem = () =>
            {
                var ve = new VisualElement()
                {
                    style =
                    {
                        flexGrow = 1,
                        maxHeight = 22,
                        flexDirection = FlexDirection.Row,
                        paddingLeft = 5,
                        paddingRight = 5,
                        backgroundColor = BackgroundDarker,
                        alignContent = Align.Center,
                        justifyContent = Justify.FlexStart
                    }
                };
                var label = new Label()
                {
                    name = "name",
                    style =
                    {
                        alignSelf = Align.FlexStart,
                        flexWrap = Wrap.NoWrap,
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis,
                        unityTextOverflowPosition = TextOverflowPosition.End,
                        alignContent = Align.Center,
                        justifyContent = Justify.FlexStart,
                        paddingTop = 3
                    }
                };
                
                ve.Add(label);
                return ve;
            };

            optList.bindItem = (ve, idx) =>
            {
                var list = (IList)optList.itemsSource;
                if (list == null || idx < 0 || idx >= list.Count) return;

                var data = (OptionRow)list[idx];

                var label = ve.Q<Label>("name");
                label.text = data.Type == OptionRowType.Header ? data.Text : $"   {data.Text}";

                var bColor = BackgroundDarker;
                
                switch (data.Type)
                {
                    case OptionRowType.Header:
                    {
                        label.style.unityFontStyleAndWeight = FontStyle.Bold;
                        if (OptionIsActivePage(data))
                        {
                            ve.style.backgroundColor = BackgroundDark;
                            bColor = BackgroundDark;
                        }

                        break;
                    }
                    case OptionRowType.Framework:
                    {
                        if (!string.IsNullOrEmpty(loadedFramework) && data.Project.MetaName == loadedFramework)
                        {
                            label.style.unityFontStyleAndWeight = FontStyle.Italic;
                            ve.style.backgroundColor = BackgroundDark;
                            bColor = BackgroundDark;
                        }
                        else
                        {
                            label.style.unityFontStyleAndWeight = FontStyle.Normal;
                            ve.style.backgroundColor = BackgroundDarker;
                            bColor = BackgroundDarker;
                        }
                        break;
                    }
                }
                
                ve.RegisterCallback<PointerEnterEvent>(_ => ve.style.backgroundColor = BackgroundDark);
                ve.RegisterCallback<PointerLeaveEvent>(_ => ve.style.backgroundColor = bColor);
                ve.RegisterCallback<ClickEvent>(_ =>
                {
                    LoadOption(data);
                    // ve.style.backgroundColor = BackgroundSub;
                });
            };

            optList.selectionType = SelectionType.Single;
            optList.fixedItemHeight = 22;
        }

        void RefreshOptionsList()
        {
            var root   = rootVisualElement.Q<VisualElement>("Root");
            var optCon = root?.Q<VisualElement>("OptionsCon");
            var optList = optCon?.Q<ListView>("OptionsList");
            if (optList == null) return;

            var list = new List<OptionRow>(options); // start with the static headers

            foreach (var fw in ForgeStores.IterateFrameworkKeys())
            {
                string niceName = ObjectNames.NicifyVariableName(fw);
                list.Add(new OptionRow($"  {niceName}", ForgeStores.LoadFramework(fw), SettingsPage.Framework, OptionRowType.Framework));
            }

            optList.itemsSource = list;
            optList.Rebuild();
        }

        void LoadOption(OptionRow opt)
        {
            if (opt.Type == OptionRowType.Framework)
            {
                LoadProject(opt.Project);
                SetPage(SettingsPage.Framework);
            }
            else
            {
                if (opt.Page == SettingsPage.Framework)
                {
                    LoadProject(ForgeStores.LoadActiveFramework());
                }
                SetPage(opt.Page);
            }
        }

        void LoadProject(FrameworkProject proj)
        {
            Debug.Log($"load proj {proj.MetaName}");
            
            loadedFramework = proj.MetaName;
            Project = proj;

            Local = ForgeStores.LoadLocalSettings(loadedFramework);
        }
        
        #endregion
        
        #region Home
        
        private VisualElement homePage;

        void SetHomePage()
        {
            homePage.style.display = DisplayStyle.Flex;
            
            RefreshHomePage();
        }

        void LeaveHomePage()
        {
            homePage.style.display = DisplayStyle.None;
        }

        void RefreshHomePage()
        {
            var activeFramework = GetContent(SettingsPage.Home).Q<VisualElement>("ActiveFramework").Q<DropdownField>("Dropdown");
            var frameworks = ForgeStores.IterateFrameworkKeys().ToList();

            if (frameworks.Count == 0)
            {
                activeFramework.choices = new List<string>() { };
                activeFramework.value = "None";
            }
            else
            {
                activeFramework.choices = frameworks;
                activeFramework.value = ForgeStores.ActiveFrameworkKey;
            }

            activeFramework.RegisterValueChangedCallback(evt =>
            {
                if (ForgeStores.ActiveFrameworkKey != evt.newValue && evt.newValue != "None")
                {
                    bool accept = EditorUtility.DisplayDialog(
                        "[ Warning ] Set Active Framework",
                        $"This action will overwrite *Ref fields in your project.\n\nInvalid assignments will be nullified. Re-instating the previous active framework ({ForgeStores.ActiveFrameworkKey}) will re-validate those assignments.",
                        "Accept", "Cancel"
                    );

                    if (accept)
                    {
                        ForgeStores.SetActiveFramework(evt.newValue);

                        var scene = SceneManager.GetActiveScene();
                        if (!scene.isLoaded)
                        {
                            return;
                        }

                        Bootstrapper bootstrapper = null;
                        foreach (var root in scene.GetRootGameObjects())
                        {
                            bootstrapper = root.GetComponentInChildren<Bootstrapper>(true);
                            if (bootstrapper is not null) break;
                        }
                
                        if (bootstrapper is null) return;
                
                        bool setIndex = EditorUtility.DisplayDialog(
                            "[ Option ] Set Bootstrapper Index?",
                            $"Bootstrapper has been found in the active scene. Would you like to overwrite the Bootstrapper Framework Index to reflect the active framework? (Recommended)",
                            "Accept", "Cancel"
                        );

                        if (!setIndex) return;
                
                        bootstrapper.Framework = ForgeStores.LoadIndex(evt.newValue);
                    }
                    else
                    {
                        activeFramework.SetValueWithoutNotify(evt.previousValue);

                        int i = evt.previousValue != null ? activeFramework.choices.IndexOf(evt.previousValue) : -1;
                        activeFramework.index = i;
                    }
                }
            });
        }
        
        #endregion
        
        #region Master Settings

        private VisualElement masterSettingsPage;

        void SetMasterSettingsPage()
        {
            masterSettingsPage.style.display = DisplayStyle.Flex;
        }

        void LeaveMasterSettingsPage()
        {
            masterSettingsPage.style.display = DisplayStyle.None;
        }

        void RefreshMasterSettingsPage()
        {
            
        }

        #endregion
        
        #region Frameworks

        private VisualElement frameworksPage;
        
        private FrameworkSubPage subPage;
        private VisualElement fw_settingsPage;
        private VisualElement fw_contextsPage;
        private VisualElement fw_templatesPage;
        
        
        enum FrameworkSubPage
        {
            Settings,
            Contexts,
            Templates
        }

        void BuildFrameworksPage()
        {
            var root = GetContent(SettingsPage.Framework);
            
            fw_settingsPage = root.Q("SettingsContent");
            fw_contextsPage = root.Q("ContextsContent");
            fw_templatesPage = root.Q("TemplatesContent");
            
            var buttons = root.Q("Buttons");
            
            var settings = buttons.Q<Button>("Settings");
            var contexts = buttons.Q<Button>("Contexts");
            var templates = buttons.Q<Button>("Templates");

            settings.focusable = false;
            contexts.focusable = false;
            templates.focusable = false;
            
            settings.style.backgroundColor = BackgroundDark;
            contexts.style.backgroundColor = BackgroundDarker;
            templates.style.backgroundColor = BackgroundDarker;

            settings.clicked += () =>
            {
                ChangeButtonColoring(settings);
                SetFrameworkSubPage(FrameworkSubPage.Settings);
            };
            
            contexts.clicked += () =>
            {
                ChangeButtonColoring(contexts);
                SetFrameworkSubPage(FrameworkSubPage.Contexts);
            };
            
            templates.clicked += () =>
            {
                ChangeButtonColoring(templates);
                SetFrameworkSubPage(FrameworkSubPage.Templates);
            };

            subPage = FrameworkSubPage.Settings;

            return;

            void ChangeButtonColoring(Button btn)
            {
                if (btn == settings)
                {
                    settings.style.backgroundColor = BackgroundDark;
                    contexts.style.backgroundColor = BackgroundDarker;
                    templates.style.backgroundColor = BackgroundDarker;
                }
                else if (btn == contexts)
                {
                    settings.style.backgroundColor = BackgroundDarker;
                    contexts.style.backgroundColor = BackgroundDark;
                    templates.style.backgroundColor = BackgroundDarker;
                }
                else if (btn == templates)
                {
                    settings.style.backgroundColor = BackgroundDarker;
                    contexts.style.backgroundColor = BackgroundDarker;
                    templates.style.backgroundColor = BackgroundDark;
                }
            }
        }
        
        void SetFrameworksPage()
        {
            frameworksPage.style.display = DisplayStyle.Flex;
            
            if (string.IsNullOrEmpty(loadedFramework)) SetEmptyFrameworksPage();
            else SetLoadedFrameworkPage();
        }

        void SetEmptyFrameworksPage()
        {
            loadedFramework = ForgeStores.ActiveFrameworkKey;
            if (!string.IsNullOrEmpty(loadedFramework))
            {
                SetFrameworksPage();
            }
            else
            {
                fw_settingsPage.style.display = DisplayStyle.None;
                fw_contextsPage.style.display = DisplayStyle.None;
                fw_templatesPage.style.display = DisplayStyle.None;
            }
        }

        void SetLoadedFrameworkPage()
        {
            var root = GetContent(SettingsPage.Framework);
            var header = root.Q<VisualElement>("Header");
            var _title = header.Q<Label>("Title");
            var desc = header.Q("Desc");
            var created = desc.Q<Label>("Created");
            var numData = desc.Q<Label>("NumData");

            _title.text = ObjectNames.NicifyVariableName(loadedFramework);
            if (ForgeStores.LoadLocalSettings(loadedFramework).StatusCheck(ForgeTags.Settings.DATE_CREATED, out object time) && time is string sTime && DateTime.TryParse(sTime, null, DateTimeStyles.RoundtripKind, out var parsedTime))
            {
                created.text = $"Created: {parsedTime.ToString(CultureInfo.InvariantCulture)}";
            }
            else
            {
                // fall back to file metadata, then persist for next time
                var lpath = ForgePaths.LocalSettingsPath(loadedFramework);
                if (File.Exists(lpath)) time = File.GetCreationTimeUtc(lpath);
                else
                {
                    var fpath = ForgePaths.FrameworkPath(loadedFramework);
                    if (File.Exists(fpath)) time = File.GetCreationTimeUtc(fpath);
                    else time = DateTime.UtcNow;
                }
                if (time is string _sTime && DateTime.TryParse(_sTime, null, DateTimeStyles.RoundtripKind, out var pTime))
                {
                    ForgeStores.LoadLocalSettings(loadedFramework).Set(ForgeTags.Settings.DATE_CREATED, pTime.ToString(CultureInfo.InvariantCulture));
                    created.text = $"Created: {time.ToString()}";
                }
            }

            numData.text = $"Num. Data: {Project.DataCount}";
            
            SetFrameworkSubPage(subPage);
        }

        void SetFrameworkSubPage(FrameworkSubPage page)
        {
            var root = GetContent(SettingsPage.Framework);
            var settings = root.Q("SettingsContent");
            var contexts = root.Q("ContextsContent");
            var templates = root.Q("TemplatesContent");
            
            switch (page)
            {
                case FrameworkSubPage.Settings:
                    fw_settingsPage.style.display = DisplayStyle.Flex;
                    fw_contextsPage.style.display = DisplayStyle.None;
                    fw_templatesPage.style.display = DisplayStyle.None;
                    SetLocalSettingsSubPage();
                    break;
                case FrameworkSubPage.Contexts:
                    fw_settingsPage.style.display = DisplayStyle.None;
                    fw_contextsPage.style.display = DisplayStyle.Flex;
                    fw_templatesPage.style.display = DisplayStyle.None;
                    SetContextsSubPage();
                    break;
                case FrameworkSubPage.Templates:
                    fw_settingsPage.style.display = DisplayStyle.None;
                    fw_contextsPage.style.display = DisplayStyle.None;
                    fw_templatesPage.style.display = DisplayStyle.Flex;
                    SetTemplatesSubPage();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(page), page, null);
            }
        }
        
        void SetLocalSettingsSubPage()
        {
            var root = fw_settingsPage.Q("RootTemplates");
            var ability = root.Q("Ability");
            var attributeSet = root.Q("AttributeSet");
            var effect = root.Q("Effect");
            var entity = root.Q("Entity");
            var attribute = root.Q("Attribute");
            var tag = root.Q("Tag");
            
            var abilityButton = ability.Q("Body").Q<Button>("SetButton");
            var attributeSetButton = attributeSet.Q("Body").Q<Button>("SetButton");
            var effectButton = effect.Q("Body").Q<Button>("SetButton");
            var entityButton = entity.Q("Body").Q<Button>("SetButton");
            var attributeButton = attribute.Q("Body").Q<Button>("SetButton");
            var tagButton = tag.Q("Body").Q<Button>("SetButton");
            
            var abilityLabel = ability.Q<Label>("Name");
            var attributeSetLabel = attributeSet.Q<Label>("Name");
            var effectLabel = effect.Q<Label>("Name");
            var entityLabel = entity.Q<Label>("Name");
            var attributeLabel = attribute.Q<Label>("Name");
            var tagLabel = tag.Q<Label>("Name");
            
            Refresh();

            return;

            void Refresh()
            {
                Dictionary<EDataType, int> assignments = Local.Status<Dictionary<EDataType, int>>(ForgeTags.Settings.ROOT_TEMPLATE_ASSIGNMENTS);
                
                SetupTemplateAssignments(EDataType.Ability, abilityLabel, abilityButton);
                SetupTemplateAssignments(EDataType.Attribute, attributeLabel, attributeButton);
                SetupTemplateAssignments(EDataType.AttributeSet, attributeSetLabel, attributeSetButton);
                SetupTemplateAssignments(EDataType.Effect, effectLabel, effectButton);
                SetupTemplateAssignments(EDataType.Entity, entityLabel, entityButton);
                SetupTemplateAssignments(EDataType.Tag, tagLabel, tagButton);

                return;

                void SetupTemplateAssignments(EDataType kind, Label label, Button button)
                {
                    string _name;
                    
                    if (assignments.TryGetValue(kind, out int idx)) _name = Project.TryGet(idx, kind, out var node) ? node.Name : idx == -1 ? "No Available Templates" : idx == 0 ? "Not Assigned" : "Error Finding Template";
                    else _name = "No available Templates";
                    
                    label.text = _name;
                    button.RegisterCallback<PointerDownEvent>(_ => button.style.backgroundColor = BackgroundSub);
                    button.RegisterCallback<ClickEvent>(evt =>
                    {
                        CreateSearchPopup(kind, evt);
                        button.style.backgroundColor = BackgroundDark;
                    });
                }
                
                void CreateSearchPopup(EDataType kind, ClickEvent evt)
                {
                    // var nodes = Project.GetAllOf(kind) as List<GasifyDataNode>;
                    var nodes = new List<ForgeDataNode>();
                    foreach (var item in Project.GetAllOf(kind))
                    {
                        nodes.Add(item as ForgeDataNode);
                    }
                    
                    var filtered = nodes.Where(n => n.TagStatus(ForgeTags.IS_TEMPLATE)).ToList();

                    var popup = new TemplateValuePopup(
                        $"Select {PlayForgeEditor.Quotify(PlayForgeEditor.DataTypeText(kind))} Root Template",
                        kind,
                        filtered,
                        SetRootAssignment
                    );

                    var local = evt.position;
                    var screen = new Vector2(local.x, local.y);
                    var anchor = new Rect(screen, new Vector2(1, 1));
                    UnityEditor.PopupWindow.Show(anchor, popup);
                }

                void SetRootAssignment(EDataType kind, ForgeDataNode _node, int returnId)
                {
                    if (_node is null) assignments[kind] = returnId;
                    else assignments[kind] = _node.Id;
                    
                    Local.Set(ForgeTags.Settings.ROOT_TEMPLATE_ASSIGNMENTS, assignments);
                    ForgeStores.SaveLocalSettings(loadedFramework, Local);
                    
                    Refresh();
                }
            }
        }

        class TemplateValuePopup : PopupWindowContent
        {
            private readonly string _title;
            private readonly List<ForgeDataNode> nodes;
            private readonly Action<EDataType, ForgeDataNode, int> onClick;
            private readonly EDataType Kind;

            private string _query = "";
            private Vector2 scroll;

            public TemplateValuePopup(string title, EDataType kind, List<ForgeDataNode> nodes, Action<EDataType, ForgeDataNode, int> onClick)
            {
                _title = title;
                this.nodes = nodes;
                this.onClick = onClick;
                Kind = kind;
            }
            
            public override Vector2 GetWindowSize() => new Vector2(360, 360);

            public override void OnGUI(Rect rect)
            {
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
                IEnumerable<ForgeDataNode> filtered = nodes;
                if (q.Length > 0)
                    filtered = nodes.Where(n =>
                        (!string.IsNullOrEmpty(n.Name) && n.Name.ToLowerInvariant().Contains(q)) ||
                        n.Id.ToString().Contains(q));

                scroll = EditorGUILayout.BeginScrollView(scroll);
                int count = 0;
                foreach (var n in filtered)
                {
                    using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18)))
                    {
                        GUILayout.Space(7);
                        if (GUILayout.Button(n.Name ?? $"<{n.Id}>", GUILayout.ExpandWidth(true)))
                        {
                            onClick?.Invoke(Kind, n, 0);
                            editorWindow.Close();
                            GUIUtility.ExitGUI();
                        }

                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"Id: {n.Id:X8}");
                        GUILayout.Space(7);
                    }
                }

                using (new EditorGUILayout.HorizontalScope(GUILayout.Height(18)))
                {
                    GUILayout.Space(7);
                    if (GUILayout.Button("None", GUILayout.ExpandWidth(true)))
                    {
                        onClick?.Invoke(Kind, null, nodes.Count > 0 ? 0 : -1);
                        editorWindow.Close();
                        GUIUtility.ExitGUI();
                    }
                    GUILayout.Space(7);
                }

                EditorGUILayout.EndScrollView();

                if (!nodes.Any())
                    EditorGUILayout.HelpBox($"No templates found for {PlayForgeEditor.DataTypeText(Kind)}.", MessageType.Info);
            }
        }

        void SetContextsSubPage()
        {
            var root = GetContent(SettingsPage.Framework);
            var content = root.Q("Contexts");
        }

        void SetTemplatesSubPage()
        {
            var root = GetContent(SettingsPage.Framework);
            var content = root.Q("Templates");
        }

        void LeaveFrameworksPage()
        {
            frameworksPage.style.display = DisplayStyle.None;
            loadedFramework = string.Empty;
            Project = null;
        }

        VisualElement GetContent(SettingsPage page)
        {
            return GetContent<VisualElement>(page);
        }

        T GetContent<T>(SettingsPage page) where T : VisualElement
        {
            var root = rootVisualElement.Q<VisualElement>("Root");
            var content = root.Q<VisualElement>("Content");
            return page switch
            {

                SettingsPage.Home => content.Q<T>($"HomeContent"),
                SettingsPage.MasterSettings => content.Q<T>($"MasterSettingsContent"),
                SettingsPage.Framework => content.Q<T>($"FrameworkContent"),
                _ => throw new ArgumentOutOfRangeException(nameof(page), page, null)
            };
        }
        
        #endregion
        
        private void OnEnable()
        {
            ReloadCaches();
        }

        private void Reset()
        {
            ReloadCaches();
        }

        private void ReloadCaches()
        {
            Master = ForgeStores.LoadMasterSettings();

            if (string.IsNullOrEmpty(loadedFramework))
            {
                Project = null;
                Local = null;
                return;
            }
            
            Project = ForgeStores.LoadFramework(loadedFramework);
            Local = ForgeStores.LoadLocalSettings(loadedFramework);
        }
    }
}
