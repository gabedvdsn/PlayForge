using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Threading;
using Codice.Client.Common.Connection;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Unity.VisualScripting;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeEditor
    {
        #region Creator

        private static EditorRegistry Registry;

        public VisualTreeAsset EnumerableDrawerTree;
        public VisualTreeAsset DictDrawerTree;
        
        public VisualTreeAsset ColorDrawerTree;
        public VisualTreeAsset GradientDrawerTree;
        
        public VisualTreeAsset IntDrawerTree;
        public VisualTreeAsset FloatDrawerTree;
        public VisualTreeAsset StringDrawerTree;
        public VisualTreeAsset BoolDrawerTree;

        public VisualTreeAsset QuickRefDrawerTree;
        public VisualTreeAsset RefDrawerTree;
        public VisualTreeAsset ObjectDrawerTree;
        public VisualTreeAsset CompositeDrawerTree;

        public VisualTreeAsset EnumDrawerTree;
        public VisualTreeAsset Vector2DrawerTree;
        public VisualTreeAsset Vector3DrawerTree;
        public VisualTreeAsset Vector2IntDrawerTree;
        public VisualTreeAsset Vector3IntDrawerTree;
        
        private VisualElement creatorRoot;
        private VisualElement creator_editor;
        private VisualElement creator_home;

        // Editor
        private ListView creator_sourceList;
        private ListView creator_buriedList;

        private List<EditorFieldData> creator_fieldData = new();
        private List<EditorFieldData> creator_buriedData = new();

        private Dictionary<EditorFieldData, HashSet<int>> creator_alerts;
        private (ConsoleEntry ce, EditorFieldData efd) creator_lastTraced = (null, null);
        
        private object creator_buriedObject;

        private EditorFieldData creator_lastEdited;
        private Dictionary<EValidationCode, List<(IConsoleMessenger source, string focus, string msg, string descr)>> creator_lastEditedAlerts;
        
        // Actions
        private ToolbarButton c_save;
        private ToolbarButton c_saveOptions;
        private ToolbarButton c_template;
        private ToolbarButton c_templateQuick;
        private ToolbarButton c_help;
        private ToolbarButton c_options;


        delegate List<ValidationPacket> FieldValidationDelegate(object o, EditorFieldData efd);
        private class EditorFieldData : IConsoleMessenger
        {
            public ForgeDataNode Source;
            
            public object Target;
            public FieldInfo Fi;
            public object Value;
            
            public VisualElement Root;
            public Func<VisualElement> GetValueElement;
            
            // A method that takes in the DC and field data and returns a list of validation packets
            public FieldValidationDelegate Validation = (_, _) => new List<ValidationPacket>();
            public List<ValidationPacket> Validate()
            {
                var results = new List<ValidationPacket>();
                foreach (var @delegate in Validation.GetInvocationList())
                {
                    var handler = (FieldValidationDelegate)@delegate;
                    var list = handler(Value, this);
                    if (list is not null) results.AddRange(list);
                }

                return results;
            }
            
            public EditorFieldData(ForgeDataNode source, object target, FieldInfo fi)
            {
                Source = source;
                
                Target = target;
                Fi = fi;
                Value = null;
                
                Root = null;
                GetValueElement = () => default;
            }

            public VisualElement GetIndicator() => Root.Q("Indicator");

            public T GetFiAttribute<T>() where T : System.Attribute
            {
                return Fi.GetCustomAttribute<T>();
            }

            public EDataType GetDataType()
            {
                var dt = GetFiAttribute<ForgeDataType>();
                return dt?.Kind ?? EDataType.None;
            }

            public T ValueTo<T>() => Value is T tV ? tV : default;
            
            public void Trace(ConsoleEntry ce, PlayForgeEditor editor, bool inOut)
            {
                editor.TraceCreatorFieldSource(ce, this, inOut);
            }
            
            public void Link(ConsoleEntry ce, PlayForgeEditor editor)
            {
                editor.LinkCreatorFieldSource(ce, this);
            }

            public bool CanResolve(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return ce.code switch
                {
                    EValidationCode.Ok => true,
                    EValidationCode.Warn => true,
                    EValidationCode.Error => false,
                    EValidationCode.None => true,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
        }
        
        private void BindCreator()
        {
            creatorRoot = contentRoot.Q("CreatorPage");

            creator_editor = creatorRoot.Q("Editor");
            creator_sourceList = creator_editor.Q("Source").Q<ListView>("SourceList");
            creator_buriedList = creator_editor.Q("Buried").Q<ListView>("BuriedList");

            creator_home = contentRoot.Q("CreatorHome");
            
            var actions = creator_editor.Q("Actions");
            c_save = actions.Q<ToolbarButton>("Save");
            c_saveOptions = actions.Q<ToolbarButton>("SaveOptions");
            c_template = actions.Q<ToolbarButton>("Template");
            c_templateQuick = actions.Q<ToolbarButton>("TemplateQuick");
            c_help = actions.Q<ToolbarButton>("Help");
            c_options = actions.Q<ToolbarButton>("Options");
        }

        void BuildCreator()
        {
            Registry = EditorRegistry.DefaultRegistry(
                IntDrawerTree, FloatDrawerTree, StringDrawerTree, BoolDrawerTree,
                EnumDrawerTree,
                ObjectDrawerTree,
                ColorDrawerTree, GradientDrawerTree,
                Vector2DrawerTree, Vector2IntDrawerTree,
                Vector3DrawerTree, Vector3IntDrawerTree,
                EnumerableDrawerTree, DictDrawerTree,
                QuickRefDrawerTree, RefDrawerTree, CompositeDrawerTree
            );

            creator_alerts = new Dictionary<EditorFieldData, HashSet<int>>();

            BuildFieldList(creator_sourceList, creator_fieldData);
            BuildFieldList(creator_buriedList, creator_buriedData);

            c_save.clicked += () => TrySaveOrCreate(ReservedFocus);
            c_template.clicked += () => OpenNodeImportMenu("", ReservedFocus.Kind, ReservedFocus);
            c_help.clicked += () => Debug.Log("Creator Help Clicked");
            c_options.clicked += () => Debug.Log("Creator Options Clicked");
            
            return;

            void BuildFieldList(ListView lv, List<EditorFieldData> source)
            {
                lv.itemsSource = source;
                
                lv.makeItem = () => new VisualElement()
                {
                    style =
                    {
                        flexGrow = 1
                    }
                };

                lv.bindItem = (row, idx) =>
                {
                    var efd = lv.itemsSource[idx] as EditorFieldData;

                    row.Add(efd.Root);
                    
                    var template = row.Q<Button>("Template");
                    var push = row.Q<Button>("Push");

                    if (template is not null)
                    {
                        template.clicked += () =>
                        {
                            OpenFieldImportMenu($"{ReservedFocus.Kind}", efd.Fi, ReservedFocus);
                        };
                    }

                    if (push is not null)
                    {
                        push.clicked += () =>
                        {
                            PushBuried(efd);
                        };
                    }
                };

                lv.makeNoneElement = () => new VisualElement();
            }
        }
        
        private void RefreshCreator()
        {
            if (!ReservedFocus.IsFocused)
            {
                creator_home.style.display = DisplayStyle.Flex;
                creator_editor.style.display = DisplayStyle.None;
                RefreshCreatorHome();
            }
            else
            {
                creator_home.style.display = DisplayStyle.None;
                creator_editor.style.display = DisplayStyle.Flex;
                RefreshCreatorEditor();
            }
        }

        void RefreshCreatorEditor()
        {
            RefreshSourcePane();
            RefreshBuriedPane();
            
            ForceResolveConsole(EConsoleContext.Creator);
            RefreshHeader();
            
            PostNodeValidateStep();
        }

        void RefreshCreatorHome()
        {
            
        }

        // For existing data
        private void LoadIntoCreator(ForgeDataNode node, EDataType kind)
        {
            SetFocus(ReservedFocus, node, kind, EForgeContext.Creator);
            LoadIntoCreator(ReservedFocus);
        }
        
        // For existing data
        private void LoadIntoCreator(DataContainer focus)
        {
            SetFocus(ReservedFocus, focus, EForgeContext.Creator);
            if (!Focus.IsFocused) SetFocus(Focus, ReservedFocus, EForgeContext.Creator);
            
            SetHeaderReservation(true);
            
            DoContextAction(EForgeContextExpanded.Creator, focus, true);
            RefreshCreator();
        }

        // For fresh data
        private void LoadIntoCreator(EDataType kind)
        {
            var node = Project.BuildNode(DataIdRegistry.Generate(), "", kind);
            LoadIntoCreator(new DataContainer(node, kind));
        }

        private bool DataHasChanges(DataContainer dc)
        {
            return dc.Node.TagStatus(ForgeTags.IS_CREATED) && Project.HasNewChanges(dc.Node, dc.Kind);
        }

        void PushBuried(EditorFieldData efd)
        {
            
        }
        
        void RefreshSourcePane()
        {
            ClearSourceFields();

            c_save.text = ReservedFocus.Node.TagStatus(ForgeTags.IS_CREATED) ? "Save" : "Create";
            
            creator_fieldData = BuildFields(ReservedFocus.Node.GetType(), ReservedFocus.Node);
            
            creator_sourceList.itemsSource = creator_fieldData;
            creator_sourceList.Rebuild();
        }
        
        void ClearSourceFields()
        {
            creator_sourceList.Clear();
            creator_fieldData.Clear();
        }

        void RefreshBuriedPane()
        {
            ClearBuriedFields();
            
            
        }

        void ClearBuriedFields()
        {
            foreach (var d in creator_buriedData) creator_buriedList.Remove(d.Root);
            creator_buriedData.Clear();
        }
        
        void PostNodeValidateStep()
        {
            foreach (var field in creator_fieldData)
            {
                ValidateFieldFromCreator(field, false);
            }

            foreach (var field in creator_buriedData)
            {
                ValidateFieldFromCreator(field, false);
            }
            
            RefreshConsole();
        }
        
        void UpdateFieldIndicator(EditorFieldData efd, EValidationCode severe, string msg = null)
        {
            var res = DefaultIndicatorState(severe);
            var ind = efd.GetIndicator();
            
            if (ind is null) return;
            
            ind.style.backgroundColor = res.color;
            ind.tooltip = msg ?? res.msg;
        }
        
        List<EditorFieldData> BuildFields(
            Type target, object o, 
            Action<EditorFieldData, object> onChange = null, 
            Action<EditorFieldData> onFocusIn = null, 
            Action<EditorFieldData> onFocusOut = null
            )
        {
            var fields = GetEditableFields(target);
            var fieldData = new List<EditorFieldData>();

            foreach (var f in fields)
            {
                if (f.Name is "Id" or "editorTags" or "alerts") continue;

                var efd = new EditorFieldData(ReservedFocus.Node, o, f);

                if (f.Name == "Name")
                {
                    Action<EditorFieldData> _out = _ =>
                    {
                        string val = efd.ValueTo<string>();
                        header_title.text = string.IsNullOrEmpty(val) ? $"Unnamed {DataTypeText(ReservedFocus.Kind)}" : val;
                    };
                    
                    efd.Validation += (_, _) =>
                    {
                        string val = efd.ValueTo<string>();
                        if (string.IsNullOrEmpty(val)) return new List<ValidationPacket>() { new (Console.Creator.Validation.NullValue) };
                        if (Project.DataWithNameExists(val, ReservedFocus.Kind, ReservedFocus.Node.Id)) return new List<ValidationPacket>() { new (Console.Creator.Validation.NameExists) };
                        return new List<ValidationPacket>();
                    };
                    
                    FindAndDraw(efd, _onFocusOut: _out);
                    fieldData.Insert(0, efd);
                }
                else if (f.Name == "Description")
                {
                    FindAndDraw(efd);
                    fieldData.Insert(1, efd);
                }
                else if (f.Name == "Icon")
                {
                    Action<EditorFieldData, object> _change = (_, _) =>
                    {
                        var icon = efd.ValueTo<Sprite>();
                        if (icon is null)
                        {
                            var _icon = GetDataIcon(ReservedFocus.Kind);
                            efd.Fi.SetValue(o, _icon);
                            header_icon.style.backgroundImage = _icon;
                        }
                        else header_icon.style.backgroundImage = new StyleBackground(icon);
                    };
                
                    FindAndDraw(efd, _onChange: _change);
                    fieldData.Insert(2, efd);
                }
                else
                {
                    FindAndDraw(efd);
                    fieldData.Add(efd);
                }
            }

            return fieldData;

            void FindAndDraw(
                EditorFieldData efd,
                Action<EditorFieldData, object> _onChange = null,
                Action<EditorFieldData> _onFocusIn = null,
                Action<EditorFieldData> _onFocusOut = null
            )
            {
                var drawer = Registry.Find(efd.Fi.FieldType);
                drawer.Draw(
                    efd,
                    CreateOnChangeEvent(_onChange), CreateFocusInEvent(_onFocusIn), CreateFocusOutEvent(_onFocusOut),
                    editor: this
                );
                drawer.AttachValidations(efd);
            }

            Action<EditorFieldData, object> CreateOnChangeEvent(Action<EditorFieldData, object> added)
            {
                return (efd, value) =>
                {
                    onChange?.Invoke(efd, value);
                    added?.Invoke(efd, value);
                };
            }

            Action<EditorFieldData> CreateFocusInEvent(Action<EditorFieldData> added)
            {
                return efd =>
                {
                    onFocusIn?.Invoke(efd);
                    added?.Invoke(efd);
                };
            }
            
            Action<EditorFieldData> CreateFocusOutEvent(Action<EditorFieldData> added)
            {
                return efd =>
                {
                    onFocusOut?.Invoke(efd);
                    added?.Invoke(efd);

                    efd.Fi.SetValue(efd.Target, efd.Value);
                    // undoStack.SetMemberWithUndo(efd.Target, efd.Fi, efd.Fi.GetValue(efd.Target), $"Change {o.GetType().Name}.{efd.Fi.Name}");

                    creator_lastEdited = efd;
                    
                    ValidateFieldFromCreator(efd);
                };
            }
        }

        private static List<FieldInfo> GetEditableFields(Type type) {
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
        
        (Color color, string msg) DefaultIndicatorState(EValidationCode code)
        {
            return code switch
            {
                EValidationCode.Ok => (ColorValidationOk, "This field is valid"),
                EValidationCode.Warn => (ColorValidationWarn, "This field is valid but displays a warning"),
                EValidationCode.Error => (ColorValidationError, "This field is invalid"),
                EValidationCode.None => (ColorValidationNone, "Cannot validate this field"),
                _ => throw new ArgumentOutOfRangeException(nameof(code), code, null)
            };
        }

        static bool TryCreateInstance(Type t, out object instance)
        {
            instance = null;

            if (t.IsValueType)
            {
                instance = Activator.CreateInstance(t);
                return true;
            }

            if (t.IsAbstract || t.IsInterface) return false;

            var ctor = t.GetConstructor(Type.EmptyTypes);
            if (ctor is not null)
            {
                instance = ctor.Invoke(null);
                return true;
            }
            
#if UNITY_EDITOR
            try
            {
                instance = FormatterServices.GetUninitializedObject(t);
                return true;
            }
            catch
            {
                //
            }
#endif
            
            return false;
        }

        List<Type> GetConcreteTypesAssignable(Type abstractType)
        {
            return TypePickerCache.GetConcreteTypesAssignableTo(abstractType)
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool TryQuickCreateItem(EditorFieldData efd, string _name, EDataType kind, out int _id)
        {
            _id = -1;
            if (kind == EDataType.None) return false;
            
            _id = DataIdRegistry.Generate();
            
            var node = Project.BuildNode(_id, _name, kind);
            var dc = new DataContainer(node, kind);
            
            bool status = TryCreateItem(dc);

            if (!status) DataIdRegistry.Release(_id);

            return status;
        }

        private bool TrySaveOrCreate(DataContainer dc, bool log = true)
        {
            if (!ReservedFocus.IsFocused) return false;

            if (ReservedFocus.Node.TagStatus(ForgeTags.IS_CREATED)) return TrySaveItem(dc, log);
            return TryCreateItem(dc, log);
        }
        
        private bool TryCreateItem(DataContainer dc, bool log = true)
        {
            
            if (dc.Kind == EDataType.None)
            {
                LogConsoleEntry(Console.Creator.Create.UnresolvedDataType(dc.Kind));
                return false;
            }
            
            (int errors, int warnings) alertCounts = (0, 0);
            foreach (var alerts in creator_fieldData.Select(GetAlertCounts))
            {
                alertCounts.errors += alerts.errors;
                alertCounts.warnings += alerts.warnings;
            }

            if (alertCounts.errors > 0)
            {
                if (log) LogConsoleEntry(Console.Creator.Create.HasErrors(ReservedFocus));
                return false;
            }

            Project.Create(dc.Node, dc.Kind);
            
            if (log)
            {
                LogConsoleEntry(alertCounts.warnings > 0 ? Console.Creator.Create.HasWarnings(ReservedFocus) : Console.Creator.Create.Success(ReservedFocus));
            }
            
            return TrySaveItem(dc, false);
        }

        public bool TryCreateTemplateItem(DataContainer dc, bool log = true)
        {
            if (dc.Kind == EDataType.None)
            {
                LogConsoleEntry(Console.Creator.CreateTemplate.UnresolvedDataType(dc.Kind));
                return false;
            }
            
            (int errors, int warnings) alertCounts = (0, 0);
            foreach (var alerts in creator_fieldData.Select(GetAlertCounts))
            {
                alertCounts.errors += alerts.errors;
                alertCounts.warnings += alerts.warnings;
            }

            if (alertCounts.errors > 0)
            {
                if (log) LogConsoleEntry(Console.Creator.CreateTemplate.HasErrors(ReservedFocus));
                return false;
            }

            if (!dc.Node.TagStatus(ForgeTags.IS_CREATED)) Project.CreateTemplate(dc.Node, dc.Kind);
            else
            {
                var copy = Project.BuildTemplateClone(dc.Kind, dc.Node, out _);
                Project.CreateTemplate(copy, dc.Kind);
            }
            
            if (log)
            {
                LogConsoleEntry(alertCounts.warnings > 0 
                    ? Console.Creator.CreateTemplate.HasWarnings(ReservedFocus) 
                    : Console.Creator.CreateTemplate.Success(ReservedFocus));
            }
            
            return TrySaveItem(dc, false);
        }
        
        private bool TrySaveItem(DataContainer dc, bool log = true)
        {
            if (dc.Kind == EDataType.None)
            {
                if (log) LogConsoleEntry(Console.Creator.Save.UnresolvedDataType(ReservedFocus.Kind));
                return false;
            }

            (int errors, int warnings) alertCounts = (0, 0);
            foreach (var alerts in creator_fieldData.Select(GetAlertCounts))
            {
                alertCounts.errors += alerts.errors;
                alertCounts.warnings += alerts.warnings;
            }

            Project.Save(dc.Node, dc.Kind);

            if (log)
            {
                if (alertCounts.errors > 0) LogConsoleEntry(Console.Creator.Save.HasErrors(ReservedFocus));
                else LogConsoleEntry(alertCounts.warnings > 0 ? Console.Creator.Save.HasWarnings(ReservedFocus) : Console.Creator.Save.Success(ReservedFocus));
            }

            SaveFramework(log);
            RefreshHeader();

            return true;
        }

        void SaveFramework(bool withAlert = false)
        {
            if (Project is null) return;
            
            ForgeStores.SaveFrameworkAndSettings(Project, LocalSettings);
            ForgeStores.SaveFramework(Project, false);

            ForgeIndexBuilder.BuildOrUpdateIndex(Project);

            if (withAlert) LogConsoleEntry(Console.Framework.FrameworkSaved(Project));
            
            RefreshProjectView(true);
        }

        (int errors, int warnings) GetAlertCounts(EditorFieldData efd)
        {
            int errors = 0, warnings = 0;
            var sourceHash = efd.GetHashCode();
            
            if (!console_entries[EConsoleContext.Creator].ContainsKey(sourceHash)) return (0, 0);
                    
            foreach (var ce in console_entries[EConsoleContext.Creator][sourceHash])
            {
                if (ce is null) continue;
                if (ce.code == EValidationCode.Error) errors += 1;
                if (ce.code == EValidationCode.Warn) warnings += 1;
            }

            return (errors, warnings);
        }
        
        private void TraceCreatorFieldSource(ConsoleEntry ce, EditorFieldData efd, bool inOut)
        {
            if (!inOut)
            {
                efd.Root.style.backgroundColor = Color.clear;
                creator_lastTraced = (null, null);
                return;
            }
            
            if (creator_lastTraced.efd is not null)
            {
                creator_lastTraced.efd.Root.style.backgroundColor = Color.clear;
            }

            creator_lastTraced.ce = ce;
            creator_lastTraced.efd = efd;

            efd.Root.style.borderBottomLeftRadius = 5;
            efd.Root.style.borderTopRightRadius = 5;
            efd.Root.style.borderTopLeftRadius = 5;
            efd.Root.style.borderBottomRightRadius = 5;
            
            efd.Root.style.backgroundColor = GetConsoleAlertColor(ce.code, ce.priority, .3f);
            
            EventCallback<PointerEnterEvent> handler = null;
            handler = _ =>
            {
                efd.Root.style.backgroundColor = Color.clear;
                efd.Root.UnregisterCallback(handler);
            };

            efd.Root.RegisterCallback(handler);
        }

        private void LinkCreatorFieldSource(ConsoleEntry ce, EditorFieldData efd)
        {
            TraceCreatorFieldSource(ce, efd, true);

            var valueElem = efd.GetValueElement?.Invoke();
            if (valueElem is null) return;

            valueElem.Focus();
        }

        #endregion
    }
}
