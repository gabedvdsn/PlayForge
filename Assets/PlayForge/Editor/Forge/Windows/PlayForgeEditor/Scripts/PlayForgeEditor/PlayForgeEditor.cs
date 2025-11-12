using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Codice.CM.SEIDInfo;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeEditor : EditorWindow
    {
        #region Content

        private new FocusContainer Focus = new FocusContainer(null, EDataType.None);
        private FocusContainer ReservedFocus = new FocusContainer(null, EDataType.None);
        private FocusContainer StoredFocus = new FocusContainer(null, EDataType.None);

        private FocusContainer NoneFocus = new FocusContainer(null, EDataType.None);

        private void SetFocus(FocusContainer fc, DataContainer dc, EForgeContext ctx)
        {
            fc.Set(dc.Node, dc.Kind, ctx);
        }
        
        private void SetFocus(FocusContainer fc, ForgeDataNode node, EDataType kind, EForgeContext ctx)
        {
            fc.Set(node, kind, ctx);
            RefreshHeader();
        }
        
        void ResetAllFocus()
        {
            Focus.Set(NoneFocus, EForgeContext.Home);
            ReservedFocus.Set(NoneFocus, EForgeContext.Home);
        }

        private bool headerReserved = false;

        void SetHeaderReservation(bool flag)
        {
            headerReserved = flag;
        }

        private static EForgeContext lastActiveContext = EForgeContext.Home;
        private static EForgeContext ActiveContext = EForgeContext.Home;
        private static object ActiveContextPayload;

        private VisualElement activePage;

        private Dictionary<EForgeContext, (Action set, Action exit)> contentActions = new();
        private Dictionary<EForgeContextExpanded, (bool canNavigate, string descr)> contentNavPermits = new();
        
        // Content
        private VisualElement contentRoot;
        private VisualElement windowRoot;
        private VisualElement focusRoot;
        
        enum EForgeContext
        {
            Home,
            Creator,
            Analytics,
            Develop,
            Validate,
            Settings
        }

        enum EForgeContextExpanded
        {
            Home,
            Creator,
            Analytics,
            Develop,
            Validate,
            Settings,
            All
        }

        EForgeContext FromForgeContextExpanded(EForgeContextExpanded ctx)
        {
            return ctx switch
            {

                EForgeContextExpanded.Home => EForgeContext.Home,
                EForgeContextExpanded.Creator => EForgeContext.Creator,
                EForgeContextExpanded.Analytics => EForgeContext.Analytics,
                EForgeContextExpanded.Develop => EForgeContext.Develop,
                EForgeContextExpanded.Validate => EForgeContext.Validate,
                EForgeContextExpanded.Settings => EForgeContext.Settings,
                EForgeContextExpanded.All => EForgeContext.Home,
                _ => throw new ArgumentOutOfRangeException(nameof(ctx), ctx, null)
            };
        }

        void SetupContent()
        {
            ActiveContext = EForgeContext.Home;
            lastActiveContext = EForgeContext.Home;
            ActiveContextPayload = null;
            
            contentActions = new Dictionary<EForgeContext, (Action set, Action exit)>()
            {
                {
                    EForgeContext.Home, (
                        () =>
                        {
                            
                        },
                        () =>
                        {
                            
                        })
                },
                {
                    EForgeContext.Creator, (
                        () =>
                        {
                            SetHeaderReservation(ReservedFocus.IsFocused);
                            _RefreshCreator();
                        },
                        () =>
                        {
                            SetHeaderReservation(false);
                        })
                },
                {
                    EForgeContext.Analytics, (
                        () =>
                        {
                            
                        },
                        () =>
                        {
                            
                        })
                },
                {
                    EForgeContext.Develop, (
                        () =>
                        {
                            
                        },
                        () =>
                        {
                            
                        })
                },
                {
                    EForgeContext.Validate, (
                        () =>
                        {
                            
                        },
                        () =>
                        {
                            
                        })
                },
                {
                    EForgeContext.Settings, (
                        () =>
                        {
                            
                        },
                        () =>
                        {
                            
                        })
                },
                
            };

            contentNavPermits = new Dictionary<EForgeContextExpanded, (bool canNavigate, string descr)>()
            {
                { EForgeContextExpanded.Home, (true, "") },
                { EForgeContextExpanded.Creator, (true, "") },
                { EForgeContextExpanded.Analytics, (true, "") },
                { EForgeContextExpanded.Develop, (true, "") },
                { EForgeContextExpanded.Validate, (true, "") },
                { EForgeContextExpanded.Settings, (true, "") },
                { EForgeContextExpanded.All, (true, "All activity is suspended at this time.") }
            };
        }

        private void BindContent()
        {
            contentRoot = rootVisualElement.Q("ContentView").Q("Content");
            windowRoot = contentRoot.Q("Window");
            focusRoot = windowRoot.Q("Focus");
            
            BindHome();
            BindCreator();
            BindAnalytics();
            BindDevelop();
            BindValidate();
            BindSettings();
            
            BindStore();
        }

        private void BuildContent()
        {
            BuildHome();
            BuildCreator();
            BuildAnalytics();
            BuildDevelop();
            BuildValidate();
            BuildSettings();
            
            BuildStore();
        }

        private void RefreshContent()
        {
            RefreshHome();
            RefreshCreator();
            RefreshAnalytics();
            RefreshDevelop();
            RefreshValidate();
            RefreshSettings();
            RefreshStore();
        }

        void DoContextAction(EForgeContextExpanded _ctx, object payload = null, bool ignoreSame = false)
        {
            var ctx = FromForgeContextExpanded(_ctx);

            if (ctx == ActiveContext && ignoreSame) return; 
            
            if (!contentNavPermits[EForgeContextExpanded.All].canNavigate)
            {
                LogConsoleEntry(Console.Sys.PermitIssue(ctx, contentNavPermits[EForgeContextExpanded.All].descr));
                return;
            }

            if (!contentNavPermits[_ctx].canNavigate)
            {
                LogConsoleEntry(Console.Sys.PermitIssue(ctx, contentNavPermits[_ctx].descr));
                return;
            }
            
            lastActiveContext = ActiveContext;
            
            ActiveContext = ctx;
            ActiveContextPayload = payload;
            
            SetConsoleContext(FromForgeContext(ActiveContext), refresh: false);
            if (!ContextIsResidualActive(lastActiveContext))
            {
                RemoveConsoleContext(FromForgeContext(lastActiveContext), FromForgeContext(ActiveContext));
            }
            else ForceResolveConsole(FromForgeContext(ActiveContext));
            
            contentActions[lastActiveContext].exit?.Invoke();
            
            SetActivePage();
            ManageContentPageDisplays();
            
            contentActions[ActiveContext].set?.Invoke();

            // Refresh();
            RefreshHeader();
            RefreshNavBar();
            RefreshConsole();

            return;
            
            void SetActivePage()
            {
                activePage = ActiveContext switch
                {
                    EForgeContext.Home => homeRoot,
                    EForgeContext.Creator => creatorRoot,
                    EForgeContext.Analytics => analyticsRoot,
                    EForgeContext.Develop => developRoot,
                    EForgeContext.Validate => validateRoot,
                    EForgeContext.Settings => settingsRoot,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            void ManageContentPageDisplays()
            {
                var pages = new VisualElement[]
                {
                    homeRoot,
                    creatorRoot,
                    analyticsRoot,
                    developRoot,
                    validateRoot,
                    settingsRoot
                };

                foreach (var page in pages)
                {
                    page.style.display = page == activePage ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }
        
        bool ContextIsResidualActive(EForgeContext ctx)
        {
            if (ctx == ActiveContext) return true;
            return ReservedFocus.Context == ctx;
        }

        void SetNavigationPermit(EForgeContextExpanded content, bool flag, string msg = null)
        {
            contentNavPermits[content] = (flag, msg);
        }
        
        #region Header
        
        private VisualElement header_icon;
        private Label header_title;
        private VisualElement header_sourceChipPlate;
        private Label header_id;
        private VisualElement header_contextChipPlate;
        private Button header_clearButton;
        private Button header_editButton;
        private Button header_analyzeButton;
        
        void BindHeader()
        {
            var header = contentRoot.Q("Header");
            
            header_icon = header.Q("IconHolder").Q("Icon");
            header_title = header.Q("TitleCard").Q<Label>("Title");
            header_sourceChipPlate = header.Q("TitleCard").Q("SourceChipPlate");
            header_id = header.Q<Label>("Id");
            header_contextChipPlate = header.Q("Bottom").Q("ContextChipPlate");

            header_clearButton = header.Q("Bottom").Q<Button>("ClearSelectionButton");
            header_editButton = header.Q("Bottom").Q<Button>("EditButton");
            header_analyzeButton = header.Q("Bottom").Q<Button>("AnalyzeButton");
        }

        void BuildHeader()
        {
            header_id.RegisterCallback<PointerEnterEvent>(_ => header_id.style.backgroundColor = ColorLight);
            header_id.RegisterCallback<PointerLeaveEvent>(_ => header_id.style.backgroundColor = Color.clear);
            header_id.RegisterCallback<PointerDownEvent>(_ => GUIUtility.systemCopyBuffer = Focus.Node.Id.ToString());

            header_clearButton.clicked += () =>
            {
                //ResetAllFocus();
                if (ReservedFocus.IsFocused) ReservedFocus.Reset();
                else Focus.Reset();
                DoContextAction(FromForgeContextToExpanded(ActiveContext));
            };

            header_editButton.clicked += () =>
            {
                if (!Focus.IsFocused) return;
                _LoadIntoCreator(Focus);
            };

            header_analyzeButton.clicked += () =>
            {
                if (!Focus.IsFocused) return;
                SetFocus(ReservedFocus, Focus, EForgeContext.Creator);
                DoContextAction(EForgeContextExpanded.Analytics);
            };
        }

        void RefreshHeader()
        {
            AssignHeader(headerReserved ? ReservedFocus : Focus);

            if (!Focus.IsFocused && !ReservedFocus.IsFocused)
            {
                header_clearButton.style.display = DisplayStyle.None;
                header_analyzeButton.style.display = DisplayStyle.None;
                header_editButton.style.display = DisplayStyle.None;
                return;
            }

            header_clearButton.style.display = DisplayStyle.Flex;
            header_analyzeButton.style.display = DisplayStyle.Flex;
            header_editButton.style.display = DisplayStyle.Flex;

            if (!headerReserved) return;
            if (!ReservedFocus.IsFocused) return;
                
            switch (ActiveContext)
            {
                case EForgeContext.Creator:
                    header_editButton.style.display = DisplayStyle.None;
                    break;
                case EForgeContext.Analytics:
                    header_analyzeButton.style.display = DisplayStyle.None;
                    break;
            }
        }
        
        void AssignHeader(FocusContainer fc)
        {
            if (!fc.IsFocused) AssignHeader(icon_UNKNOWN, "No item selected...", 0, withAddStar: false);
            else AssignHeader(GetDataIcon(fc.Kind), fc.Node.Name, fc.Node.Id, GetSourceChips(fc), GetContextChips(fc));
        }
        
        void AssignHeader(Texture2D icon, string _title, int id, List<(string text, Color color)> sourceChips = null, List<(string text, Color color)> contextChips = null, bool withAddStar = true)
        {
            header_icon.style.backgroundImage = icon;
            header_title.text = _title;
            header_id.text = $"ID: {id:X8}";

            header_sourceChipPlate.Clear();
            if (sourceChips is not null)
            {
                foreach (var chip in sourceChips) header_sourceChipPlate.Add(CreateChip(chip.text, chip.color));
            }
            
            header_contextChipPlate.Clear();
            if (contextChips is not null)
            {
                foreach (var chip in contextChips) header_contextChipPlate.Add(CreateChip(chip.text, chip.color));
            }

            if (!withAddStar) return;
            header_contextChipPlate.Add(CreateAddChip());
            header_contextChipPlate.Add(CreateFavoriteChip());
        }
        
        #region Helpers

        private List<(string text, Color color)> GetSourceChips(DataContainer dc)
        {
            var chips = new List<(string text, Color color)>();
            
            chips.Add((DataTypeText(dc.Kind), ChipDescrColor));
            if (dc.Node.TagStatus(ForgeTags.IS_TEMPLATE)) chips.Add(("Template", ChipDescrColor));
            if (!dc.Node.TagStatus(ForgeTags.VALID_FOR_GAMEPLAY)) chips.Add(("Invalid", ChipWarnColor));
            
            return chips;
        }
        
        private List<(string text, Color color)> GetContextChips(DataContainer dc)
        {
            var chips = new List<(string text, Color color)>();
            
            if (dc.Node.TagStatus(ForgeTags.SOURCES_TEMPLATE, out int tId) && Project.TryGet(tId, dc.Kind, out var tNode)) chips.Add(($"T: {tNode.Name}", ChipTemplateSourceColor));
            if (dc.Node.TagStatus(ForgeTags.EDITOR_CATEGORIES, out List<Tag> cats))
            {
                foreach (var c in cats) chips.Add((c.Name, ChipCategoryColor));
            }
            
            chips.Add(("T: Basic", ChipTemplateSourceColor));
            
            for (int i = 0; i < Random.Range(1, 4); i++)
            {
                chips.Add(($"Category {i}", ChipCategoryColor));
            }

            return chips;
        }
        
        #endregion
        
        #endregion

        #region Home
        
        // Root
        private VisualElement homeRoot;
        
        void BindHome()
        {
            homeRoot = contentRoot.Q("HomePage");
        }

        void BuildHome()
        {
            
        }

        void RefreshHome()
        {
            
        }
        
        #endregion
        
        #region Analytics
        
        // Root
        private VisualElement analyticsRoot;
        
        void BindAnalytics()
        {
            analyticsRoot = contentRoot.Q("AnalyticsPage");
        }

        void BuildAnalytics()
        {
            
        }

        void RefreshAnalytics()
        {
            
        }
        
        #endregion
        
        #region Develop
        
        // Root
        private VisualElement developRoot;
        
        void BindDevelop()
        {
            developRoot = contentRoot.Q("DevelopPage");
        }

        void BuildDevelop()
        {
            
        }

        void RefreshDevelop()
        {
            
        }
        
        #endregion
        
        #region Validate
        
        // Root
        private VisualElement validateRoot;
        
        void BindValidate()
        {
            validateRoot = contentRoot.Q("ValidatePage");
        }

        void BuildValidate()
        {
            
        }

        void RefreshValidate()
        {
            
        }
        
        #endregion
        
        #region Settings
        
        // Root
        private VisualElement settingsRoot;
        
        void BindSettings()
        {
            settingsRoot = contentRoot.Q("SettingsPage");
        }

        void BuildSettings()
        {
            
        }

        void RefreshSettings()
        {
            
        }
        
        #endregion

        #region Store

        private VisualElement storeRoot;
        
        private void BindStore()
        {
            storeRoot = contentRoot.Q("Store");
            
            BindConsole();
        }

        private void BuildStore()
        {
            BuildConsole();
        }

        private void RefreshStore()
        {
            RefreshConsole();
        }
        
        #endregion

        #region Helpers
        
        #region Other

        public VisualTreeAsset ImportDescrItem;
        
        /// <summary>
        /// For field values. Import field value from same-kind nodes.
        /// </summary>
        /// <param name="pos">Where to create the popup</param>
        /// <param name="_title">Popup title</param>
        /// <param name="fi">FieldInfo target</param>
        /// <param name="ignore">Calling node</param>
        private void OpenFieldImportMenu(string _title, FieldInfo fi, DataContainer ignore)
        {
            /*// Build packets
            var dMaker = new Func<ForgeDataNode, List<ImportPopupDescrPacket>>(node =>
            {
                // var value = fi.GetValue(node);
                List<ImportPopupDescrPacket> packets = new();
                
                //var alerts = ParseAlerts(node);
                
                foreach (var code in alerts.Keys)
                {
                    var messages = new List<string>();
                    foreach (var details in alerts[code])
                    {
                        if (details.field != fi.Name) continue;
                        messages.Add(details.msg);
                    }
                    
                    packets.Add(new ImportPopupDescrPacket(code, messages));
                }
                
                if (packets.Count == 0) packets.Add(new ImportPopupDescrPacket(EValidationCode.Ok, new List<string>() { "No alerts shown for this item; it can be imported without issue." }));

                return packets;
            });
            
            var subTitle = new Func<string>(() => fi.Name);

            var onPick = new Action<DataContainer>(dc =>
            {
                fi.SetValue(ignore.Node, fi.GetValue(dc.Node));
                LogConsoleEntry(Console.Creator.Import.Field.Success(dc, fi.Name));
            });
            
            OpenImportWindow(_title, ignore.Kind, ignore, subTitle, onPick, dMaker);*/
        }

        /// <summary>
        /// For Nodes. Import all node field values into same-kind node.
        /// </summary>
        /// <param name="pos"></param>
        /// <param name="_title"></param>
        /// <param name="kind"></param>
        /// <param name="ignore"></param>
        private void OpenNodeImportMenu(string _title, EDataType kind, DataContainer ignore)
        {
            /*// Creates popup packets
            var dMaker = new Func<ForgeDataNode, List<ImportPopupDescrPacket>>(node =>
            {
                // var value = fi.GetValue(node);
                List<ImportPopupDescrPacket> packets = new();
                
                var alerts = ParseAlerts(node);
                foreach (var code in alerts.Keys)
                {
                    var messages = new List<string>();
                    foreach (var details in alerts[code])
                    {
                        messages.Add(details.msg);
                    }
                    
                    packets.Add(new ImportPopupDescrPacket(code, messages));
                }
                
                if (packets.Count == 0) packets.Add(new ImportPopupDescrPacket(EValidationCode.Ok, new List<string>() { "No alerts shown for this item; it can be imported without issue." }));

                return packets;
            });

            var subTitle = new Func<string>(() => string.Empty);

            var onPick = new Action<DataContainer>(dc =>
            {
                foreach (var fi in GetEditableFields(dc.Node.GetType()))
                {
                    fi.SetValue(ignore.Node, fi.GetValue(dc.Node));
                }
                LogConsoleEntry(Console.Creator.Import.Node.Success(dc));
            });
            
            OpenImportWindow(_title, kind, ignore, subTitle, onPick, dMaker);*/
        }
        
        private void OpenImportWindow(string _title, EDataType kind, DataContainer ignore, Func<string> subTitle, Action<DataContainer> onPick, Func<ForgeDataNode, List<ImportPopupDescrPacket>> makeDescr)
        {
            var nodes = Project.GetCompleteNodes()[kind];
            var candidates = nodes.Where(n => n.Id != ignore.Node.Id).ToList();

            ImportPopupWindow.Open(_title, candidates, kind, subTitle, onPick, makeDescr);
        }
        
        #endregion
        
        #region Chips
        private static readonly Color ChipBorderColor = new(.17f, .17f, .21f, 1f);

        private static Color ChipDescrColor => new(.4f, .4f, .4f, 1f);
        private static Color ChipWarnColor => new(.53f, .38f, .38f, 1f);
        private static Color ChipTemplateSourceColor => new(.4f, .32f, .46f, 1f);
        private static Color ChipCategoryColor => new(.325f, .458f, .458f, 1f);

        private Button CreateChip(string text, Color color, int height = 18)
        {
            return new Button
            {
                text = text,
                style =
                {
                    flexGrow = 0, flexShrink = 0,
                    minHeight = height, maxHeight = height,
                    marginBottom = 2, marginTop = 4, marginLeft = 2, marginRight = 1,
                    paddingBottom = 4, paddingLeft = 4, paddingRight = 4, paddingTop = 4,
                    unityTextAlign = TextAnchor.MiddleCenter, fontSize = 9,
                    backgroundColor = color,
                    borderBottomColor = ChipBorderColor, borderLeftColor = ChipBorderColor, borderRightColor = ChipBorderColor, borderTopColor = ChipBorderColor,
                    borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1, borderTopWidth = 1,
                    borderBottomLeftRadius = 5, borderBottomRightRadius = 5, borderTopLeftRadius = 5, borderTopRightRadius = 5,
                    alignItems = Align.Stretch, justifyContent = Justify.FlexStart, alignSelf = Align.Center
                }
            };
        }

        private Button CreateAddChip()
        {
            return CreateChip("+", ChipDescrColor);
        }

        private Button CreateFavoriteChip()
        {
            return CreateChip("\u2606", ChipDescrColor);
        }
        #endregion
        
        #region Button Bar

        void ApplyFancyButtonBarBorders(Button target, int botWidth, int topWidth, params Button[] others)
        {
            foreach (var b in others)
            {
                b.style.backgroundColor = ColorLight;
                b.style.borderLeftWidth = 1;
                b.style.borderTopWidth = 1;
                b.style.borderRightWidth = 1;
                b.style.borderBottomWidth = botWidth;
            }

            target.style.backgroundColor = ColorDark;
            target.style.borderLeftWidth = 3;
            target.style.borderTopWidth = topWidth;
            target.style.borderRightWidth = 3;
            target.style.borderBottomWidth = 0;
        }
        
        #endregion
        
        #endregion
        
        #endregion
    }
}
