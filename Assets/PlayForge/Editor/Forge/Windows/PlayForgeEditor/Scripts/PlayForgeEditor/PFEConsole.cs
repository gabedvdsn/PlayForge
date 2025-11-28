using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeEditor
    {
        public VisualTreeAsset ConsoleEntryRow;

        abstract class LinkingConsoleSource : IConsoleMessenger
        {

            public abstract void Trace(ConsoleEntry ce, PlayForgeEditor editor, bool inOut);
            public abstract bool HasTrace(ConsoleEntry ce, PlayForgeEditor editor);
            public virtual void Link(ConsoleEntry ce, PlayForgeEditor editor)
            {
                editor.DoContextAction(FromConsoleContextToExpanded(ce.context), ce, true);
            }
            public virtual bool HasLink(ConsoleEntry ce, PlayForgeEditor editor) => true;
            public abstract bool CanResolve(ConsoleEntry ce, PlayForgeEditor editor);
        }
        
        /// <summary>
        /// For sys and informational alerts
        /// </summary>
        class ResolvableConsoleSource : LinkingConsoleSource
        {
            public override void Trace(ConsoleEntry ce, PlayForgeEditor editor, bool inOut)
            {
                
            }
            public override bool HasTrace(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return false;
            }

            public override bool CanResolve(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return true;
            }
        }

        class ProgressConsoleSource : IConsoleMessenger
        {

            public void Trace(ConsoleEntry ce, PlayForgeEditor editor, bool inOut)
            {
                
            }
            public bool HasTrace(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return false;
            }
            public void Link(ConsoleEntry ce, PlayForgeEditor editor)
            {
                
            }
            public bool HasLink(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return false;
            }
            public bool CanResolve(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return !editor.ProgressLocked;
            }
        }

        class ErrorServiceConsoleSource : LinkingConsoleSource
        {
            public override void Trace(ConsoleEntry ce, PlayForgeEditor editor, bool inOut)
            {
                
            }
            public override bool HasTrace(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return false;
            }
            public override bool CanResolve(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return true;
            }
        }
        
        /// <summary>
        /// For alerts sourced from data items while not in Creator mode
        /// E.g. in creator or analytics mode
        /// </summary>
        class UnfocusedDataSource : LinkingConsoleSource
        {
            private int id;
            public UnfocusedDataSource(int id)
            {
                this.id = id;
            }
            
            public override void Trace(ConsoleEntry ce, PlayForgeEditor editor, bool inOut)
            {
                
            }
            public override bool HasTrace(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return true;
            }
            public override void Link(ConsoleEntry ce, PlayForgeEditor editor)
            {
                DataContainer dc = ce.userData as DataContainer;
                if (dc is null) return;
                
                editor.LoadIntoCreator(dc);
            }

            public override bool HasLink(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return ce.userData is DataContainer;
            }

            public override bool CanResolve(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return ActiveContext == EForgeContext.Creator;
            }
        }

        private interface IConsoleMessenger
        {
            /// <summary>
            /// Shows where the console entry is sourced from
            /// </summary>
            /// <param name="ce"></param>
            /// <param name="editor"></param>
            /// <param name="inOut"></param>
            void Trace(ConsoleEntry ce, PlayForgeEditor editor, bool inOut);

            bool HasTrace(ConsoleEntry ce, PlayForgeEditor editor);

            /// <summary>
            /// Traces and applies focus, typically
            /// </summary>
            /// <param name="ce"></param>
            /// <param name="editor"></param>
            void Link(ConsoleEntry ce, PlayForgeEditor editor);

            bool HasLink(ConsoleEntry ce, PlayForgeEditor editor);
            
            bool CanResolve(ConsoleEntry ce, PlayForgeEditor editor);
        }
        
        private class ConsoleEntry
        {
            public readonly int id;
            public int idx;
            public readonly EConsoleContext context;
            public readonly EValidationCode code;
            public readonly EConsolePriority priority;

            public object userData;
            
            public DateTime time;
            public string focus;
            public string message;
            public string description;

            public readonly IConsoleMessenger source;
            public Action<bool> trace;
            public Action<ConsoleEntry> link;
            public string tooltip;

            public bool IsTheSameAs(ConsoleEntry ce)
            {
                if (ce is null) return false;
                return context == ce.context
                       && code == ce.code
                       && focus == ce.focus
                       && message == ce.message
                       && description == ce.description
                       && source == ce.source
                       && tooltip == ce.tooltip;
            }

            public bool TryResolve(PlayForgeEditor editor)
            {
                if (source is null) return true;
                if (!source.CanResolve(this, editor)) return false;
                
                editor.ForceResolveConsoleEntry(this);
                return true;

            }

            public ConsoleEntry(int id, EConsoleContext context, IConsoleMessenger source, DateTime time, EValidationCode code, string focus, string message, string description, Action<ConsoleEntry> link, Action<bool> trace, string tooltip, EConsolePriority priority = EConsolePriority.Default)
            {
                this.id = id;
                this.context = context;
                this.source = source;
                this.priority = priority;
                
                this.time = time;
                
                this.code = code;
                this.focus = focus;
                this.message = message;
                this.description = description;
                
                this.link = link;
                this.trace = trace;
                this.tooltip = tooltip;
            }
        }

        public enum EConsoleContext
        {
            Home,
            Creator,
            Analytics,
            Develop,
            Validate,
            Settings,
            All
        }

        enum EConsolePriority
        {
            Default,
            System
        }

        static EConsoleContext FromForgeContext(EForgeContext ctx)
        {
            return ctx switch
            {

                EForgeContext.Home => EConsoleContext.Home,
                EForgeContext.Creator => EConsoleContext.Creator,
                EForgeContext.Analytics => EConsoleContext.Analytics,
                EForgeContext.Develop => EConsoleContext.Develop,
                EForgeContext.Validate => EConsoleContext.Validate,
                EForgeContext.Settings => EConsoleContext.Settings,
                _ => throw new ArgumentOutOfRangeException(nameof(ctx), ctx, null)
            };
        }

        static EForgeContextExpanded FromForgeContextToExpanded(EForgeContext ctx)
        {
            return ctx switch
            {

                EForgeContext.Home => EForgeContextExpanded.Home,
                EForgeContext.Creator => EForgeContextExpanded.Creator,
                EForgeContext.Analytics => EForgeContextExpanded.Analytics,
                EForgeContext.Develop => EForgeContextExpanded.Develop,
                EForgeContext.Validate => EForgeContextExpanded.Validate,
                EForgeContext.Settings => EForgeContextExpanded.Settings,
                _ => throw new ArgumentOutOfRangeException(nameof(ctx), ctx, null)
            };
        }

        static EForgeContext FromConsoleContext(EConsoleContext ctx)
        {
            return ctx switch
            {

                EConsoleContext.Home => EForgeContext.Home,
                EConsoleContext.Creator => EForgeContext.Creator,
                EConsoleContext.Analytics => EForgeContext.Analytics,
                EConsoleContext.Develop => EForgeContext.Develop,
                EConsoleContext.Validate => EForgeContext.Validate,
                EConsoleContext.Settings => EForgeContext.Settings,
                EConsoleContext.All => ActiveContext,
                _ => throw new ArgumentOutOfRangeException(nameof(ctx), ctx, null)
            };
        }

        static EForgeContextExpanded FromConsoleContextToExpanded(EConsoleContext ctx)
        {
            return ctx switch
            {

                EConsoleContext.Home => EForgeContextExpanded.Home,
                EConsoleContext.Creator => EForgeContextExpanded.Creator,
                EConsoleContext.Analytics => EForgeContextExpanded.Analytics,
                EConsoleContext.Develop => EForgeContextExpanded.Develop,
                EConsoleContext.Validate => EForgeContextExpanded.Validate,
                EConsoleContext.Settings => EForgeContextExpanded.Settings,
                EConsoleContext.All => EForgeContextExpanded.All,
                _ => throw new ArgumentOutOfRangeException(nameof(ctx), ctx, null)
            };
        }
        
        private VisualElement consoleRoot;

        private VisualElement console_activeContextsBar;

        private ListView console_list;
        private Button console_refreshButton;
        private Button console_resolveButton;
        private Label console_countText;
        private Button console_toggleDescriptions;
        private Button console_showTimeToggle;
        private Button console_logInfoHelp;

        // context: {sourceHash: ConsoleEntry[]}
        private Dictionary<EConsoleContext, Dictionary<int, ConsoleEntry[]>> console_entries;
        private List<ConsoleEntry> console_flattenedEntries;

        private ConsoleEntry console_selected;
        private bool console_hasSelection => console_selected is not null;
        
        private const int console_defaultMaxEntries = 100;
        private int console_maxEntries = console_defaultMaxEntries;
        private bool console_logTime;

        private VisualElement console_descriptions;
        private VisualElement console_descrIcon;
        private Label console_descrTitle;
        private Label console_descrMsg;
        private Button console_descrLink;
        private Label console_descrTime;
        private Label console_descrBody;

        private bool console_descriptionsOpen = true;

        private EConsoleContext console_context;
        private Dictionary<EConsoleContext, Button> console_activeContexts;

        private static ResolvableConsoleSource console_resolvable = new();
        private static ProgressConsoleSource console_progressSource = new();
        private static ErrorServiceConsoleSource console_errorSource = new();
        
        void SetupConsole()
        {
            console_resolvable = new ResolvableConsoleSource();
            console_progressSource = new ProgressConsoleSource();
        }
        
        void BindConsole()
        {
            consoleRoot = storeRoot.Q("Console");

            console_activeContextsBar = consoleRoot.Q("Header").Q("OpenContexts");
            
            var body = consoleRoot.Q("Body");

            console_list = body.Q<ListView>("ConsoleList");
            console_refreshButton = consoleRoot.Q("Header").Q<Button>("Refresh");
            console_resolveButton = consoleRoot.Q("Header").Q<Button>("Resolve");
            console_countText = consoleRoot.Q("Header").Q<Label>("ConsoleCount");
            console_toggleDescriptions = consoleRoot.Q("Header").Q<Button>("ToggleDescriptions");
            console_showTimeToggle = consoleRoot.Q("Body").Q<ToolbarButton>("ShowTime");
            console_logInfoHelp = consoleRoot.Q("Body").Q<ToolbarButton>("Info");
            
            console_descriptions = body.Q("Descriptions");
            console_descrIcon = console_descriptions.Q("Icon");
            console_descrTitle = console_descriptions.Q<Label>("Title");
            console_descrMsg = console_descriptions.Q<Label>("Message");
            console_descrLink = console_descriptions.Q<Button>("Link");
            console_descrBody = console_descriptions.Q<Label>("DescriptionBody");
            console_descrTime = console_descriptions.Q<Label>("Time");
        }

        void BuildConsole()
        {

            console_activeContexts = new Dictionary<EConsoleContext, Button>();
            console_flattenedEntries = new List<ConsoleEntry>();
            
            console_list.itemsSource = console_flattenedEntries;

            console_entries = new Dictionary<EConsoleContext, Dictionary<int, ConsoleEntry[]>>()
            {
                { EConsoleContext.Home, new Dictionary<int, ConsoleEntry[]>() },
                { EConsoleContext.Creator, new Dictionary<int, ConsoleEntry[]>() },
                { EConsoleContext.Analytics, new Dictionary<int, ConsoleEntry[]>() },
                { EConsoleContext.Develop, new Dictionary<int, ConsoleEntry[]>() },
                { EConsoleContext.Validate, new Dictionary<int, ConsoleEntry[]>() },
                { EConsoleContext.Settings, new Dictionary<int, ConsoleEntry[]>() },
                { EConsoleContext.All, new Dictionary<int, ConsoleEntry[]>() },
            };
            
            console_maxEntries = Setting(ForgeTags.Settings.MAX_CONSOLE_ENTRIES, fallback: console_defaultMaxEntries);

            console_refreshButton.style.backgroundImage = icon_REFRESH;
            
            console_resolveButton.clicked += TryResolveConsole;

            console_descrLink.style.backgroundImage = icon_LINK;

            console_toggleDescriptions.clicked += () =>
            {
                console_descriptionsOpen = !console_descriptionsOpen;
                RefreshConsole();
            };
            
            console_logInfoHelp.clicked += () =>
            {
                LogConsoleEntry(Console.Sys.WelcomeToPlayForge(), refresh: false);
                LogConsoleEntry(Console.Sys.GettingStarted(), refresh: false);
                LogConsoleEntry(Console.Sys.NavigatingTheForge(), refresh: false);
                LogConsoleEntry(Console.Sys.PlayForgeDocumentation(), refresh: false);
                LogConsoleEntry(Console.Sys.PlayForgeAbout(), refresh: false);
                
                RefreshConsole();
            };

            console_list.makeNoneElement = () => new VisualElement() { style = { flexGrow = 0 } };
            
            console_list.makeItem = () => ConsoleEntryRow.CloneTree();

            console_list.bindItem = (ve, idx) =>
            {
                if (idx < 0 || idx >= console_flattenedEntries.Count) return;
                if (console_list.itemsSource[idx] is not ConsoleEntry ce)
                {
                    Debug.Log($"\t\tBinding: CE is null");
                    return;
                }
                
                var root = ve;
                var icon = root.Q("Icon");
                var time = root.Q<Label>("Time");
                var focus = root.Q<Label>("Focus");
                var msg = root.Q<Label>("Message");
                var link = root.Q<Button>("GoTo");

                if (ce.priority == EConsolePriority.System) root.style.backgroundColor = ColorSystem;
                else root.style.backgroundColor = Color.clear;

                time.style.display = console_logTime ? DisplayStyle.Flex : DisplayStyle.None;
                
                icon.style.backgroundImage = GetConsoleAlertIcon(ce.code);

                time.text = $"[{ce.time.Hour:D2}:{ce.time.Minute}:{ce.time.Second}]";
                focus.text = $"{ce.focus} \u2192";
                msg.text = ce.message;
                
                root.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    if (ce.source.HasLink(ce, this))
                    {
                        link.style.display = DisplayStyle.Flex;
                        link.style.backgroundImage = icon_LINK;
                    }
                });
                
                root.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    if (console_hasSelection && console_selected.id == ce.id) return;
                    link.style.display = DisplayStyle.None;
                });
                
                root.RegisterCallback<ClickEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (console_hasSelection && console_selected.id == ce.id) ResetConsoleSelected();
                    else SetConsoleSelected(ce);
                });

                if (console_hasSelection && ce.id == console_selected.id)
                {
                    root.style.backgroundColor = ColorSelected;
                    if (ce.source.HasLink(ce, this)) link.style.display = DisplayStyle.Flex;
                }
                else link.style.display = DisplayStyle.None;
                
                link.style.backgroundImage = icon_LINK;

                link.RegisterCallback<ClickEvent>(evt =>
                {
                    DoContextAction(FromConsoleContextToExpanded(ce.context), ce, true);
                    ce.link?.Invoke(ce);
                    evt.StopPropagation();
                });

                link.tooltip = ce.tooltip ?? "Locate";
            };

            console_list.RegisterCallback<ClickEvent>(evt =>
            {
                ResetConsoleSelected();
            });

            console_showTimeToggle.clicked += () =>
            {
                console_logTime = !console_logTime;
                RefreshConsole();
            };

            console_descrLink.clicked += () =>
            {
                if (!console_hasSelection) return;
                console_selected.link?.Invoke(console_selected);
            };
            
            AddConsoleContext(EConsoleContext.Home);
        }
        
        void RefreshConsole()
        {
            //Debug.Log($"------ CONSOLE REFRESH -------");
            
            console_flattenedEntries.Clear();
            foreach (var ce in console_entries[console_context].Values.SelectMany(ces => ces).Where(ce => ce is not null))
            {
                console_flattenedEntries.Add(ce);
            }

            console_list.Clear();
            console_list.Rebuild();
            
            console_countText.text = ConsoleCount(console_context).ToString();

            if (console_descriptionsOpen) console_descriptions.style.display = DisplayStyle.Flex;
            else console_descriptions.style.display = DisplayStyle.None;
            
            console_toggleDescriptions.text = console_descriptionsOpen ? ChevronDown : ChevronRight;

            if (console_hasSelection)
            {
                console_descrIcon.style.display = DisplayStyle.Flex;
                console_descrLink.style.display = DisplayStyle.Flex;
                
                console_descrLink.style.backgroundImage = console_selected.link is not null ? icon_LINK : icon_UNLINKED;
                console_descrLink.tooltip = console_selected.link is null ? "No link available..." : console_selected.tooltip;
                
                console_descrIcon.style.backgroundImage = GetConsoleAlertIcon(console_selected.code);
                console_descrTitle.text = console_selected.focus;
                console_descrMsg.text = console_selected.message;
                console_descrBody.text = console_selected.description;
                console_descrTime.text = $"[{console_selected.time.Hour:00}:{console_selected.time.Minute:00}:{console_selected.time.Second:00}]";
            }
            else
            {
                console_descrIcon.style.display = DisplayStyle.None;
                console_descrLink.style.display = DisplayStyle.None;
                
                console_descrTitle.text = "No log selected...";
                console_descrBody.text = "";
                console_descrTime.text = "";
                console_descrMsg.text = "Select a console entry for more information.";
            }
            
            //ApplyFancyButtonBarBorders(console_activeContexts[console_context], 0, console_activeContexts.Values.ToArray());
            
            if (console_activeContexts.Count > 1)
            {
                console_activeContextsBar.style.display = DisplayStyle.Flex;
                ApplyFancyButtonBarBorders(console_activeContexts[console_context], 0, 2, console_activeContexts.Values.ToArray());
            }
            else
            {
                console_activeContextsBar.style.display = DisplayStyle.None;
            }
            
            console_list.schedule.Execute(() =>
            {
                var scrollView = console_list.Q<ScrollView>();
                scrollView.scrollOffset = new Vector2(0, float.MaxValue);
            });
        }

        void AddConsoleContext(EConsoleContext ctx)
        {
            if (!console_activeContexts.ContainsKey(ctx))
            {
                console_activeContexts[ctx] = CreateConsoleContextButton();
                console_activeContextsBar.Add(console_activeContexts[ctx]);
            }
            
            return;
            
            Button CreateConsoleContextButton()
            {
                int width = Math.Max(10 * ctx.ToString().Length, 55);
                var btn = new Button()
                {
                    name = ctx.ToString(),
                    text = ctx.ToString(),
                    style =
                    {
                        display = DisplayStyle.Flex,
                        alignSelf = Align.FlexEnd,
                        minHeight = 24, maxHeight = 24,
                        minWidth = width, maxWidth = width,
                        marginRight = 0, marginBottom = 0, marginLeft = 0,
                        paddingBottom = 0, paddingTop = 0, paddingLeft = 0, paddingRight = 0,
                        borderBottomWidth = 0,
                        borderBottomColor = Color.black, borderLeftColor = Color.black, borderRightColor = Color.black, borderTopColor = Color.black,
                        borderBottomLeftRadius = -1, borderTopLeftRadius = 4, borderBottomRightRadius = -1, borderTopRightRadius = 4
                        
                    },
                    focusable = false
                };

                btn.clicked += () =>
                {
                    SetConsoleContext(ctx, true);
                    console_context = ctx;
                    SetConsoleContextIndicator(ctx, DisplayStyle.None, Color.clear);
                    RefreshConsole();
                };

                int indSize = 10;
                var indicator = new VisualElement()
                {
                    name = "indicator",
                    style =
                    {
                        display = DisplayStyle.None,
                        flexGrow = 0,
                        minWidth = indSize, maxWidth = indSize,
                        minHeight = indSize, maxHeight = indSize,
                        alignSelf = Align.FlexStart,
                        marginTop = 1,
                        marginLeft = 1,
                        borderBottomColor = ColorDark, borderLeftColor = ColorDark, borderRightColor = ColorDark, borderTopColor = ColorDark,
                        borderBottomLeftRadius = 5, borderTopLeftRadius = 5, borderBottomRightRadius = 5, borderTopRightRadius = 5,
                        borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1, borderTopWidth = 1
                    }
                };
                
                btn.Add(indicator);

                return btn;
            }
        }

        void RemoveConsoleContext(EConsoleContext ctx, EConsoleContext fallback = EConsoleContext.Home)
        {
            if (ctx == EConsoleContext.Home) return;
            
            console_activeContextsBar.Remove(console_activeContexts[ctx]);
            console_activeContexts.Remove(ctx);

            console_context = fallback;
            
            RefreshConsole();
        }

        private void AddConsoleEntry(ConsoleEntry ce)
        {
            var ctx = ce.context;
            int sourceHash = ce.source.GetHashCode();
            
            ce.idx = GetNextAvailableSourceIdx(ctx, sourceHash);
            
            console_entries[ctx][sourceHash][ce.idx] = ce;
            
            if (console_context != ctx) SetConsoleContextIndicator(ctx, DisplayStyle.Flex, GetConsoleAlertColor(ce.code, ce.priority));
        }

        void SetConsoleContextIndicator(EConsoleContext ctx, DisplayStyle style, Color c)
        {
            var btn = console_activeContexts[ctx];
            var indicator = btn.Q("indicator");
            indicator.style.display = style;
            indicator.style.backgroundColor = c;
        }

        private void RemoveConsoleEntry(ConsoleEntry ce)
        {
            var sourceHash = ce.source.GetHashCode();
            var arr = console_entries[ce.context][sourceHash];
            if (arr.Length == 1)
            {
                console_entries[ce.context][sourceHash] = new ConsoleEntry[1];
                return;
            }
            
            int indexOf = -1;
            for (int i = 0; i < arr.Length; i++)
            {
                if (console_entries[ce.context][sourceHash][i].IsTheSameAs(ce))
                {
                    indexOf = i;
                    break;
                }
            }

            if (indexOf < 0) return;

            console_entries[ce.context][sourceHash][indexOf] = null;
        }

        private bool ConsoleEntryExists(ConsoleEntry ce, bool updateTime = false)
        {
            if (!console_entries[ce.context].ContainsKey(ce.source.GetHashCode())) return false;
            
            foreach (var _ce in console_entries[ce.context][ce.source.GetHashCode()])
            {
                if (!ce.IsTheSameAs(_ce)) continue;
                if (updateTime) _ce.time = ce.time;
                
                return true;
            }

            return false;
        }
        
        private int LogConsoleEntry(ConsoleEntry ce, bool noDuplicates = true, bool updateDuplicates = true, bool refresh = true)
        {
            if (noDuplicates)
            {
                if (!ConsoleEntryExists(ce, updateDuplicates))
                {
                    AddConsoleEntry(ce);
                }
                else
                {
                    DataIdRegistry.Release(ce.id);
                    return -1;
                }
            }
            else
            {
                AddConsoleEntry(ce);
            }
            
            if (refresh) RefreshConsole();

            return ce.id;
        }

        private int ConsoleCount()
        {
            return console_entries.Keys.Sum(ConsoleCount);
        }
        
        private int ConsoleCount(EConsoleContext context) => console_entries[context].Sum(entries => entries.Value.Count(e => e != null));
        
        private void TryResolveConsole()
        {
            var contexts = console_entries.Keys.ToArray();
            foreach (var context in contexts) TryResolveConsole(context);
            console_list.Clear();

            ResetConsoleSelected(false);
            RefreshConsole();
        }

        private void TryResolveConsole(EConsoleContext context)
        {
            var keys = console_entries[context].Keys;
            foreach (var k in keys)
            {
                int length = console_entries[context][k].Length;
                for (int i = 0; i < length; i++)
                {
                    var ce = console_entries[context][k][i];
                    if (ce is null) continue;
                    ce.TryResolve(this);
                }
            }
        }

        private void ForceResolveConsole(EConsoleContext ctx)
        {
            var sourceHashes = console_entries[ctx].Keys.ToList();
            foreach (var sourceHash in sourceHashes) ForceResolveConsoleHash(ctx, sourceHash);
        }

        private void ForceResolveConsoleSource(EConsoleContext ctx, IConsoleMessenger source)
        {
            int sourceHash = source.GetHashCode();
            ForceResolveConsoleHash(ctx, sourceHash);
        }

        private void ForceResolveConsoleHash(EConsoleContext ctx, int sourceHash)
        {
            if (!console_entries[ctx].ContainsKey(sourceHash)) return;
            
            for (int i = 0; i < console_entries[ctx][sourceHash].Length; i++)
            {
                if (console_entries[ctx][sourceHash][i] is null) continue;
                console_entries[ctx][sourceHash][i] = null;
            }

            console_entries[ctx].Remove(sourceHash);
        }
        
        private void ForceResolveConsoleEntry(ConsoleEntry ce)
        {
            try
            {
                console_entries[ce.context][ce.source.GetHashCode()][ce.idx] = null;
            }
            catch (Exception)
            {
                // 
            }
        }

        private void SetConsoleContext(EConsoleContext ctx, bool switchToNew = true, bool refresh = true)
        {
            AddConsoleContext(ctx);

            SetConsoleContextIndicator(ctx, DisplayStyle.None, Color.clear);
            
            if (console_context != ctx)
            {
                ResetConsoleSelected(false);
            }
            if (switchToNew) console_context = ctx;
            
            if (refresh) RefreshConsole();
        }

        void ResetConsoleSelected(bool refresh = true)
        {
            if (console_selected is not null) console_selected.trace?.Invoke(false);
            console_selected = null;
            if (refresh) RefreshConsole();
        }
        
        void SetConsoleSelected(ConsoleEntry ce)
        {
            console_selected = ce;
            ce.trace?.Invoke(true);
            RefreshConsole();
        }

        int GetNextAvailableSourceIdx(EConsoleContext ctx, int sourceHash)
        {
            if (!console_entries[ctx].ContainsKey(sourceHash))
            {
                console_entries[ctx][sourceHash] = new ConsoleEntry[1];
                return 0;
            }

            var entries = console_entries[ctx][sourceHash];
            int length = entries.Length;

            for (int i = 0; i < length; i++)
            {
                if (entries[i] is null) return i;
            }
            
            Array.Resize(ref entries, length * 2);
            console_entries[ctx][sourceHash] = entries;
            
            return length;
        }
        
        public static Texture2D GetConsoleAlertIcon(EValidationCode code)
        {
            return code switch
            {
                EValidationCode.Ok => icon_INFO,
                EValidationCode.Warn => icon_WARNING,
                EValidationCode.Error => icon_ERROR,
                EValidationCode.None => icon_SYSTEM,
                _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
            };
        }

        private static Color GetConsoleAlertColor(EValidationCode code, EConsolePriority prio, float alpha = 1f)
        {
            if (prio == EConsolePriority.System) return ColorSystem;
            
            var color = code switch
            {
                EValidationCode.Ok => ColorValidationOk,
                EValidationCode.Warn => ColorValidationWarn,
                EValidationCode.Error => ColorValidationError,
                EValidationCode.None => ColorSystem,
                _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
            };
            color.a = alpha;
            return color;
        }

        private static class Console
        {
            public static string ForgeName => AboutPlayForge.PlayForgeTitle;
            public static string ForgeVersion => AboutPlayForge.PlayForgeVersion;

            static ConsoleEntry BaseSysConsole(EValidationCode code = EValidationCode.Ok, IConsoleMessenger source = null)
            {
                var ce = new ConsoleEntry(
                    DataIdRegistry.Generate(), EConsoleContext.Home, source ?? console_resolvable,
                    DateTime.Now, code,
                    ForgeName, "", "", null, null, null, EConsolePriority.System);

                ce.link = _ => ce.source.Link(ce, Instance);
                ce.trace = flag => ce.source.Trace(ce, Instance, flag);
                
                return ce;
            }

            static ConsoleEntry BaseConsole(EConsoleContext ctx, EValidationCode code, IConsoleMessenger source = null)
            {
                var ce = new ConsoleEntry(
                    DataIdRegistry.Generate(), ctx, source ?? console_resolvable,
                    DateTime.Now, code,
                    "", "", "", null, null, null);

                ce.link = _ => ce.source.Link(ce, Instance);
                ce.trace = flag => ce.source.Trace(ce, Instance, flag);

                return ce;
            }
            
            #region Creator
            
            public static class Creator
            {
                public static class CreatorSys
                {
                    private const string focus = "Error Service";

                    public static ConsoleEntry CompositeInitFailure(FieldInfo fi)
                    {
                        var ce = BaseSysConsole(EValidationCode.Error, console_errorSource);

                        ce.focus = focus;
                        ce.message = $"Critical Error — Field {fi.Name} could not be initialized.";
                        ce.description = $"Field {fi.Name} ({fi.FieldType}) could not be assigned at initialization. Restart editor.";

                        return ce;
                    }
                }
                
                public static class Validation
                {
                    private const string focus = "Creator";
                    
                    public static ConsoleEntry GenericValidation(EValidationCode code, (IConsoleMessenger source, string focus, string msg, string descr) details)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, code, details.source);

                        ce.focus = focus;
                        ce.message = details.msg;
                        ce.description = details.descr;

                        return ce;
                    }
                    
                    public static ConsoleEntry Ok(DataContainer dc, FieldData source)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok, source);

                        ce.focus = focus;
                        ce.message = $"{source.Fi.Name} is valid.";
                        ce.description = $"";

                        return ce;
                    }
                    
                    public static ConsoleEntry WarnNullOrEmpty(DataContainer dc, FieldData source)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Warn, source);

                        ce.focus = focus;
                        ce.message = $"Field {Quotify(source.Fi.Name)} is null or empty.";
                        ce.description = $"It is recommended to assign a value to {dc.Kind}.{source.Fi.Name}.";

                        return ce;
                    }
                    
                    public static ConsoleEntry MissingValidation(DataContainer dc, FieldData source)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error, source);

                        ce.focus = focus;
                        ce.message = $"Validation Missing";
                        ce.description = $"Could not produce a validation for {dc.Kind}.{source.Fi.Name}";

                        return ce;
                    }
                    
                    public static ConsoleEntry NullValue(DataContainer dc, FieldData efd)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error, efd);

                        ce.focus = focus;
                        ce.message = $"Field {Quotify(efd.Fi.Name)} is invalid — cannot be null.";
                        ce.description = $"Field {dc.Kind}.{efd.Fi.Name} cannot be null.";

                        return ce;
                    }
                    
                    public static ConsoleEntry NameExists(DataContainer dc, FieldData efd)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error, efd);

                        ce.focus = focus;
                        ce.message = $"Field {Quotify(efd.Fi.Name)} is invalid — Name already exists.";
                        ce.description = $"A(n) {DataTypeText(dc.Kind)} with name {Quotify(efd.ValueTo<string>())} already exists.";

                        return ce;
                    }

                    public static ConsoleEntry ValueMissingWarn(DataContainer dc, FieldData efd)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Warn, efd);

                        ce.focus = focus;
                        ce.message = $"Field {Quotify(efd.Fi.Name)} is missing.";
                        ce.description = $"Field {dc.Kind}.{efd.Fi.Name} is missing, but allowable.";

                        return ce;
                    }
                }
                
                public static class Create
                {
                    private const string focus = "Create Data";
                    
                    
                    public static ConsoleEntry Success(DataContainer dc)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok);

                        ce.focus = focus;
                        ce.message = $"{DataTypeText(dc.Kind)} {Quotify(dc.Node.Name)} created successfully.";
                        ce.description = $"{DataTypeText(dc.Kind)} {Quotify(dc.Node.Name)} has been created without errors or warnings.";

                        return ce;
                    }
                    
                    public static ConsoleEntry UnresolvedDataType(EDataType kind)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error);

                        ce.focus = focus;
                        ce.message = $"Cannot create {Quotify(DataTypeText(kind))} type — Unresolved DataType.";
                        ce.description = "Could not resolve DataType associated with this item. Please re-create the item.";

                        return ce;
                    }
                
                    public static ConsoleEntry InvalidField(EDataType kind, string fieldName, string descr)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error);

                        ce.focus = focus;
                        ce.message = $"Cannot create {DataTypeText(kind)} — Field {Quotify(fieldName)} is invalid.";
                        ce.description = descr;

                        return ce;
                    }

                    public static ConsoleEntry DataWithNameExists(EDataType kind, string _name)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error);

                        ce.focus = focus;
                        ce.message = $"Cannot create {DataTypeText(kind)} — Name already exists.";
                        ce.description = $"{DataTypeText(kind)} {Quotify(_name)} already exists in the framework.";

                        return ce;
                    }

                    public static ConsoleEntry HasErrors(DataContainer dc)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Warn);

                        ce.focus = focus;
                        ce.message = $"{DataTypeText(dc.Kind)} {Quotify(dc.Node.Name)} created with errors.";
                        ce.description = $"{DataTypeText(dc.Kind)} {Quotify(dc.Node.Name)} has been created but contains errors. This item will not be recognized by the index until all errors are resolved.";

                        return ce;
                    }
                    
                    public static ConsoleEntry HasWarnings(DataContainer dc)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Warn);

                        ce.focus = focus;
                        ce.message = $"{DataTypeText(dc.Kind)} {Quotify(dc.Node.Name)} created with warnings.";
                        ce.description = $"{DataTypeText(dc.Kind)} {Quotify(dc.Node.Name)} has been created but contains warnings. This item will be recognized by the index, despite having warnings. It is recommended to resolve warnings.";

                        return ce;
                    }
                }

                public static class CreateTemplate
                {
                    private const string focus = "Create Template";
                    
                    public static ConsoleEntry Success(DataContainer dc)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok);

                        ce.focus = focus;
                        ce.message = $"{DataTypeText(dc.Kind)} template {Quotify(dc.Node.Name)} created successfully.";
                        ce.description = $"{DataTypeText(dc.Kind)} template {Quotify(dc.Node.Name)} has been created without errors or warnings.";

                        return ce;
                    }
                    
                    public static ConsoleEntry UnresolvedDataType(EDataType kind)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error);

                        ce.focus = focus;
                        ce.message = $"Cannot create {Quotify(DataTypeText(kind))} type template — Unresolved DataType.";
                        ce.description = "Could not resolve DataType associated with this item. Please re-create the item.";

                        return ce;
                    }
                
                    public static ConsoleEntry InvalidField(EDataType kind, string fieldName, string descr)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error);

                        ce.focus = focus;
                        ce.message = $"Cannot create {DataTypeText(kind)} template — Field {Quotify(fieldName)} is invalid.";
                        ce.description = descr;

                        return ce;
                    }

                    public static ConsoleEntry DataWithNameExists(EDataType kind, string _name)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error);

                        ce.focus = focus;
                        ce.message = $"Cannot create {DataTypeText(kind)} — Name already exists.";
                        ce.description = $"{DataTypeText(kind)} {Quotify(_name)} already exists in the framework.";

                        return ce;
                    }

                    public static ConsoleEntry HasErrors(DataContainer dc)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error);

                        ce.focus = focus;
                        ce.message = $"Cannot create {DataTypeText(dc.Kind)} template {Quotify(dc.Node.Name)} — Contains errors.";
                        ce.description = $"Templates cannot be created with errors.";

                        return ce;
                    }
                    
                    public static ConsoleEntry HasWarnings(DataContainer dc)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Warn);

                        ce.focus = focus;
                        ce.message = $"Create {DataTypeText(dc.Kind)} template {Quotify(dc.Node.Name)} created with warnings.";
                        ce.description = $"{DataTypeText(dc.Kind)} template {Quotify(dc.Node.Name)} has been created but contains warnings. This template is still usable.";

                        return ce;
                    }
                }

                public static class Save
                {
                    private const string focus = "Save Data";
                    
                    public static ConsoleEntry Success(DataContainer dc)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok);

                        ce.focus = focus;
                        ce.message = $"{DataTypeText(dc.Kind)} {Quotify(dc.Node.Name)} saved successfully.";
                        ce.description = "";

                        return ce;
                    }
                    
                    public static ConsoleEntry UnresolvedDataType(EDataType kind)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error);

                        ce.focus = focus;
                        ce.message = $"Cannot save {Quotify(DataTypeText(kind))} type — Unresolved DataType.";
                        ce.description = "Could not resolve DataType associated with this item. Please re-create the item.";

                        return ce;
                    }
                
                    public static ConsoleEntry InvalidField(EDataType kind, string fieldName, string descr)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error);

                        ce.focus = focus;
                        ce.message = $"Cannot save {DataTypeText(kind)} — Field {Quotify(fieldName)} is invalid.";
                        ce.description = descr;

                        return ce;
                    }

                    public static ConsoleEntry DataWithNameExists(EDataType kind, string _name)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Error);

                        ce.focus = focus;
                        ce.message = $"Cannot save {DataTypeText(kind)} — Name already exists.";
                        ce.description = $"{DataTypeText(kind)} {Quotify(_name)} already exists in the framework.";

                        return ce;
                    }

                    public static ConsoleEntry HasErrors(DataContainer dc)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Warn);

                        ce.focus = focus;
                        ce.message = $"{DataTypeText(dc.Kind)} {Quotify(dc.Node.Name)} saved with errors.";
                        ce.description = $"{DataTypeText(dc.Kind)} {Quotify(dc.Node.Name)} has been saved but contains errors. This item will not be recognized by the index until all errors are resolved.";

                        return ce;
                    }
                    
                    public static ConsoleEntry HasWarnings(DataContainer dc)
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Warn);

                        ce.focus = focus;
                        ce.message = $"{DataTypeText(dc.Kind)} saved {Quotify(dc.Node.Name)} created with warnings.";
                        ce.description = $"{DataTypeText(dc.Kind)} {Quotify(dc.Node.Name)} has been saved but contains warnings. This item will be recognized by the index, despite having warnings. It is recommended to resolve warnings.";

                        return ce;
                    }
                }

                public static class Import
                {
                    public static class Field
                    {
                        private const string focus = "Import Field";
                    
                        public static ConsoleEntry Success(DataContainer dc, string fieldName)
                        {
                            var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok);

                            ce.focus = focus;
                            ce.message = $"Field {dc.Kind}.{fieldName} was imported.";
                            ce.description = $"Field {dc.Kind}.{fieldName} was imported from {dc.Node.Name} successfully.";

                            return ce;
                        }
                    }

                    public static class Node
                    {
                        private const string focus = "Import Template";
                    
                        public static ConsoleEntry Success(DataContainer dc)
                        {
                            var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok);

                            ce.focus = focus;
                            ce.message = $"{DataTypeText(dc.Kind)} template {Quotify(dc.Node.Name)} was imported.";
                            ce.description = $"All field values were replaced with values imported from {Quotify(dc.Node.Name)} successfully.";

                            return ce;
                        }
                    }
                }

                public static class Help
                {
                    private const string focus = "Help";
                    
                    public static ConsoleEntry AboutTagWorkers()
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok, console_resolvable);

                        ce.focus = focus;
                        ce.message = $"About Tag Workers.";
                        ce.description = $"Tag Workers activate with respect to granted Tags and run on application, tick, and removal.\n\nTag Workers instantiate runtime instances to track running values.\n\nAs opposed to other workers, Tag Worker activations depend on a set of requirements.";

                        return ce;
                    }
                    
                    public static ConsoleEntry AboutEffectWorkers()
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok, console_resolvable);

                        ce.focus = focus;
                        ce.message = $"About Effect Workers.";
                        ce.description = $"Effect Workers co-depend on Gameplay Effects and run on application, tick, removal, and effect impact.\n\nEffect workers instantiate runtime instances to track running values.";

                        return ce;
                    }
                    
                    public static ConsoleEntry AboutAttributeWorkers()
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok, console_resolvable);

                        ce.focus = focus;
                        ce.message = $"About Attribute Workers.";
                        ce.description = $"Attribute Workers activate with respect to certain attributes. Activations must be validated before occurring.\n\nActivations can occur before, after, or before and after attribute modifications take place.";

                        return ce;
                    }
                    
                    public static ConsoleEntry AboutAnalysisWorkers()
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok, console_resolvable);

                        ce.focus = focus;
                        ce.message = $"About Analysis Workers.";
                        ce.description = $"Analysis Workers activate with respect to some event, such as end-of-frame, worker activation, or ability/attribute system callback.\n\nTypically reserved for outer-scope \"check-ins\", such as death or milestones.";

                        return ce;
                    }
                    
                    public static ConsoleEntry AboutImpactWorkers()
                    {
                        var ce = BaseConsole(EConsoleContext.Creator, EValidationCode.Ok, console_resolvable);

                        ce.focus = focus;
                        ce.message = $"About Impact Workers.";
                        ce.description = $"Impact Workers activate in response to impact transactions occurring between effects and GAS components. Before activating, the impact transaction must be validated with respect to the worker.";

                        return ce;
                    }
                }
            }
            
            #endregion
            
            #region Framework

            public static class Framework
            {
                public static ConsoleEntry OnLoadFramework(string _name)
                {
                    var ce = BaseSysConsole();

                    ce.message = $"Framework {Quotify(_name)} was loaded successfully.";
                    ce.description = "To change which framework is loaded, click on the framework title in the upper left.";

                    return ce;
                }
                
                public static ConsoleEntry FailedToLoad()
                {
                    var ce = BaseSysConsole();

                    ce.message = $"Failed to auto-load framework";
                    ce.description = "An exception occurred while auto-loading the framework. To change which framework is loaded, click on the framework title in the upper left.";

                    return ce;
                }
                
                public static ConsoleEntry FailedToLoadMasterSettings()
                {
                    var ce = BaseSysConsole();

                    ce.message = $"Failed to load master settings";
                    ce.description = "An exception occurred while loading master settings. Master settings will be regenerated.";

                    return ce;
                }

                public static ConsoleEntry FrameworkSaved(FrameworkProject project)
                {
                    var ce = BaseSysConsole();

                    ce.message = "Framework Saved";
                    ce.description = $"Framework {Quotify(project.MetaName)} has been saved and it's index rebuilt.";

                    return ce;
                }
            }
            
            #endregion
        
            #region Sys Info

            public static class Sys
            {
                public static ConsoleEntry WelcomeToPlayForge()
            {
                var ce = BaseSysConsole();

                ce.message = "Welcome to PlayForge!";
                ce.description = "PlayForge is an editor-suite/runtime framework that gives developers the power to create complex and reliable gameplay behaviour.";

                return ce;
            }

                public static ConsoleEntry GettingStarted()
                {
                    var ce = BaseSysConsole();

                    ce.message = "Getting Started";
                    ce.description = "To get started step-by-step, a Welcome Guide is available online. Click the Link button below to open in your browser.";
                    ce.link = _ => Application.OpenURL("https://example.com/your-framework-docs");
                    ce.tooltip = "Open 'https://playforge.com/getting-started'";

                    return ce;
                }
                
                public static ConsoleEntry NavigatingTheForge()
                {
                    var ce = BaseSysConsole();

                    ce.message = "Navigating The Forge";
                    ce.description = "Learn to navigate The Forge and utilize the tools it offers! Click the Link button below to begin the tour.";
                    ce.link = _ => Debug.Log($"Playing tutorial!");;
                    ce.tooltip = "Start PlayForge Tour";

                    return ce;
                }
                
                public static ConsoleEntry PlayForgeDocumentation()
                {
                    var ce = BaseSysConsole();

                    ce.message = "Documentation";
                    ce.description = "Documentation is available online. Click the Link button below to open in your browser.";
                    ce.link = _ => Application.OpenURL("https://example.com/your-framework-docs");
                    ce.tooltip = "Open 'https://playforge.com/getting-started'";

                    return ce;
                }
                
                public static ConsoleEntry PlayForgeAbout()
                {
                    var ce = BaseSysConsole();

                    ce.message = "About";
                    ce.description = "PlayForge is a Gameplay Ability System Framework and editor tool suite for Unity6+.";
                    ce.link = _ =>
                    {
                        EditorUtility.DisplayDialog("About PlayForge",
                            "PlayForge Framework & Editor Suite\n\nCreate memorable gameplay experiences at the intersection of logic and complexity.\n\nA Gameplay Ability System for Unity.\n\n© Far Emerald Studio",
                            "Ok");
                    };
                    ce.tooltip = "About PlayForge";

                    return ce;
                }
                
                /// <summary>
                /// 
                /// </summary>
                /// <param name="ctx">The context being requested</param>
                /// <param name="permCtx"></param>
                /// <param name="descr"></param>
                /// <returns></returns>
                public static ConsoleEntry PermitIssue(EForgeContext ctx, string descr)
                {
                    var ce = BaseSysConsole(EValidationCode.Error, console_progressSource);

                    ce.message = $"Locking Process Active — {ctx} access restricted.";
                    ce.description = descr;

                    return ce;
                }
            }

            public static class Process
            {
                private const string focus = "Locking Process";
                
                public static ConsoleEntry ProcessBegin(EProgressLock @lock)
                {
                    var ce = BaseConsole(EConsoleContext.Home, EValidationCode.Ok, console_progressSource);

                    ce.focus = focus;
                    ce.message = $"Locking Process Started — {ProgressLockMessage(@lock)} access restricted.";
                    ce.description = $"Certain activity is restricted while the process completes.";

                    return ce;
                }
                
                public static ConsoleEntry ProcessEnd(EProgressLock @lock)
                {
                    var ce = BaseConsole(EConsoleContext.Home, EValidationCode.Ok, console_progressSource);

                    ce.focus = focus;
                    ce.message = $"Locking Process Complete — {ProgressLockMessage(@lock)} access restored.";
                    ce.description = $"Activity permissions are restored.";

                    return ce;
                }
            }
            
            #endregion
        }

        
    }
}
