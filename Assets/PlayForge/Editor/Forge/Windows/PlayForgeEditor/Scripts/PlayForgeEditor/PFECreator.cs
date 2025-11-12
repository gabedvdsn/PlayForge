using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Runtime.Serialization;
using System.Threading;
using Codice.Client.Common.Connection;
using Codice.CM.Common;
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
        private ListView creator_buriedList;

        private (ConsoleEntry ce, FieldData efd) creator_lastTraced = (null, null);
        
        private object creator_buriedObject;
        
        
        // Actions
        private ToolbarButton c_save;
        private ToolbarButton c_saveOptions;
        private ToolbarButton c_template;
        private ToolbarButton c_templateQuick;
        private ToolbarButton c_help;
        private ToolbarButton c_options;

        private Dictionary<EDataType, Dictionary<string, FieldData>> creator_fields = new();
        private Dictionary<int, BuriedFieldData> creator_buriedFields = new();
        
        void BindCreator()
        {
            creatorRoot = focusRoot.Q("CreatorPage");
            creator_home = creatorRoot.Q("CreatorHome");
            creator_editor = creatorRoot.Q("Editor");
            
            var actions = creator_editor.Q("Actions");
            c_save = actions.Q<ToolbarButton>("Save");
            c_saveOptions = actions.Q<ToolbarButton>("SaveOptions");
            c_template = actions.Q<ToolbarButton>("Template");
            c_templateQuick = actions.Q<ToolbarButton>("TemplateQuick");
            c_help = actions.Q<ToolbarButton>("Help");
            c_options = actions.Q<ToolbarButton>("Options");
            
            BindAbilityCreator();
        }

        void BuildCreator()
        {
            BuildAbilityCreator();   
        }

        void RefreshCreator()
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

        class FieldData : IConsoleMessenger
        {
            public FieldInfo Fi;
            public VisualElement Root;
            public VisualElement Indicator;
            public Func<VisualElement> ValueField;
            private Func<FieldData, List<ValidationPacket>> Validation;
            public object Value;

            public FieldData(FieldInfo fi, VisualElement indicator, Func<FieldData, List<ValidationPacket>> validation, object value)
            {
                Fi = fi;
                Indicator = indicator;
                Validation = validation;
                Value = value;
            }

            public static FieldData Initial(FieldInfo fi, VisualElement root, VisualElement indicator, Func<VisualElement> valueField, Func<FieldData, List<ValidationPacket>> validation)
            {
                return new FieldData(fi, indicator, validation, null);
            }

            public List<ValidationPacket> GetValidation() => Validation?.Invoke(this) ?? new List<ValidationPacket>() { new(Console.Creator.Validation.MissingValidation ) };

            public T ValueTo<T>()
            {
                return Value is T tV ? tV : default;
            }

            public VisualElement GetValueField() => ValueField?.Invoke();
            public INotifyValueChanged<T> GetValueField<T>()
            {
                return ValueField?.Invoke() as INotifyValueChanged<T>;
            }

            public void Trace(ConsoleEntry ce, PlayForgeEditor editor, bool inOut)
            {
                
            }
            public void Link(ConsoleEntry ce, PlayForgeEditor editor)
            {
                
            }
            public bool CanResolve(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return ce.code <= 0;
            }
        }
        
        class BuriedFieldData : FieldData
        {
            public readonly int Key;
            public readonly Type MyType;
            
            public BuriedFieldData(int key, Type myType, FieldInfo fi, VisualElement indicator, Func<FieldData, List<ValidationPacket>> validation, object value) : base(fi, indicator, validation, value)
            {
                Key = key;
                MyType = myType;
            }
        }

        void ValidateFieldOnEdit(FieldData fd, bool refresh = true)
        {
            ForceResolveConsoleSource(EConsoleContext.Creator, fd);

            var v = fd.GetValidation();
            ConsoleEntry severe = null;
            
            foreach (var vp in v)
            {
                
                var ce = vp.ConsolePointer.Invoke(ReservedFocus, fd);
                if (severe is null || (int)ce.code > (int)severe.code) severe = ce;

                ce.link = _ => fd.Link(ce, this);
                ce.trace = flag => fd.Trace(ce, this, flag);

                LogConsoleEntry(ce, refresh: false);
            }
            
            UpdateFieldIndicator(fd, severe?.code ?? EValidationCode.Ok, severe?.message);
            
            if (refresh) RefreshConsole();
        }
        
        private List<Unbinder> creator_activeUnbinders = new();
        
        #region Ability
        
        void BindAbilityCreator()
        {
            var r = creator_editor.Q("Ability");
            
            // Definition
            var definition = r.Q("Definition");
            
            // NAME FIELD
            var nameField = definition.Q("Name");
            var nameFi = typeof(AbilityData).GetField(nameof(AbilityData.Name));
            var nameIndicator = nameField.Q("Indicator");
            var nameInput = nameField.Q<TextField>();
            Func<FieldData, List<ValidationPacket>> nameValidation = fd =>
            {
                string value = fd.ValueTo<string>();
                if (string.IsNullOrEmpty(value)) return new List<ValidationPacket>() { new(Console.Creator.Validation.NullValue) };
                if (Project.DataWithNameExists(value, ReservedFocus.Kind, ReservedFocus.Node.Id)) return new List<ValidationPacket>() { new(Console.Creator.Validation.NameExists) };
                return new List<ValidationPacket>();
            };
            var nameData = FieldData.Initial(nameFi, nameField, nameIndicator, () => nameInput, nameValidation);
            var nameBinder = BindCreatorField<string>(EDataType.Ability, nameData, refAssign: () =>
            {
                string value = nameData.ValueTo<string>();
                header_title.text = string.IsNullOrEmpty(value) ? $"Unnamed {DataTypeText(ReservedFocus.Kind)}" : value;
            });
            
            // DESCRIPTION FIELD
            var descrField = definition.Q("Description");
            var descrFi = typeof(AbilityData).GetField(nameof(AbilityData.Description));
            var descrIndicator = descrField.Q("Indicator");
            var descrInput = descrField.Q<TextField>();
            var descrData = FieldData.Initial(descrFi, descrField, descrIndicator, () => descrInput, QuickValidationWarnOnNullOrEmpty);
            var descrBinder = BindCreatorField<string>(EDataType.Ability, descrData);
            
            // ICON FIELD
            var iconField = definition.Q("Icon");
            var iconFi = typeof(AbilityData).GetField(nameof(AbilityData.Icon));
            var iconIndicator = iconField.Q("Indicator");
            var iconInput = iconField.Q<TextField>();
            var iconData = FieldData.Initial(iconFi, iconField, iconIndicator, () => iconInput, QuickValidationWarnOnNullOrEmpty);
            var iconBinder = BindCreatorField<Sprite>(EDataType.Ability, iconData, refAssign: () =>
            {
                Sprite value = iconData.ValueTo<Sprite>();
                header_icon.style.backgroundImage = value.texture;
            });
            
            creator_activeUnbinders.Add(nameBinder);
            creator_activeUnbinders.Add(descrBinder);
            creator_activeUnbinders.Add(iconBinder);

            var runtime = r.Q("Runtime");
            
            // COST FIELD
            var costField = runtime.Q("Cost");
            var costFi = typeof(AbilityData).GetField(nameof(AbilityData.Cost));
            var costIndicator = costField.Q("Indicator");
            var costInput = costField.Q<TextField>();
            var costData = FieldData.Initial(costFi, costField, costIndicator, () => costInput, QuickValidationWarnOnNullOrEmpty);
            var costBinder = BindCreatorField<GameplayEffect>(EDataType.Ability, costData, refAssign: () =>
            {
                // TODO update visualizer if open
            });
            // Setup search dropdown
            
            // COOLDOWN FIELD
            var cooldownField = runtime.Q("Cost");
            var cooldownFi = typeof(AbilityData).GetField(nameof(AbilityData.Cooldown));
            var cooldownIndicator = cooldownField.Q("Indicator");
            var cooldownInput = cooldownField.Q<TextField>();
            var cooldownData = FieldData.Initial(cooldownFi, cooldownField, cooldownIndicator, () => cooldownInput, QuickValidationWarnOnNullOrEmpty);
            var cooldownBinder = BindCreatorField<GameplayEffect>(EDataType.Ability, cooldownData, refAssign: () =>
            {
                // TODO update visualizer if open
            });
            // Setup search dropdown
            
            // TARGETING TASK FIELD
            var targetingField = runtime.Q("Targeting");
            var targetingFi = typeof(AbilityData).GetField(nameof(AbilityData.Proxy.TargetingProxy));
            var targetingIndicator = targetingField.Q("Indicator");
            var targetingInput = targetingField.Q<TextField>();
            var targetingData = FieldData.Initial(targetingFi, targetingField, targetingIndicator, () => targetingInput, QuickValidationWarnOnNullOrEmpty);
            var targetingBinder = BindCreatorField<string>(EDataType.Ability, targetingData, refAssign: () =>
            {
                // TODO update visualizer if open
            });
            // Setup search dropdown
            
            // USE IMPLICIT TARGETING FIELD
            var implicitTargetingField = runtime.Q("UseImplicitTargeting");
            var implicitTargetingFi = typeof(AbilityData).GetField(nameof(AbilityData.Proxy.UseImplicitTargeting));
            var implicitTargetingIndicator = implicitTargetingField.Q("Indicator");
            var implicitTargetingInput = implicitTargetingField.Q<TextField>();
            var implicitTargetingData = FieldData.Initial(implicitTargetingFi, implicitTargetingField, implicitTargetingIndicator, () => implicitTargetingInput, QuickValidationWarnOnNullOrEmpty);
            var implicitTargetingBinder = BindCreatorField<bool>(EDataType.Ability, implicitTargetingData, refAssign: () =>
            {
                // TODO update visualizer if open
            });
            // Setup search dropdown
            
            // PROXY TASKS FIELD
            var proxyTasksField = runtime.Q("ProxyTasks");
            var proxyTasksFi = typeof(AbilityData).GetField(nameof(AbilityData.Proxy.Stages));
            var proxyTasksIndicator = proxyTasksField.Q("Indicator");
            var proxyTasksData = FieldData.Initial(proxyTasksFi, proxyTasksField, proxyTasksIndicator, () => null, QuickValidationWarnOnNullOrEmpty);
            var proxyTasksNumStages = proxyTasksField.Q<Label>("NumStagesChip");
            var proxyTasksNumTasks = proxyTasksField.Q<Label>("NumTasksChip");
            var proxyTasksGoButton = proxyTasksField.Q<Button>("AddEB");
            var proxyTasksBinder = BindCreatorField<string>(EDataType.Ability, proxyTasksData, refAssign: () =>
            {
                // TODO update chips
            });
            proxyTasksGoButton.clicked += () =>
            {
                // TODO load buried list
            };
            
            creator_activeUnbinders.Add(costBinder);
            creator_activeUnbinders.Add(cooldownBinder);
            creator_activeUnbinders.Add(proxyTasksBinder);
        }
        
        void BuildAbilityCreator()
        {
            
        }

        void RefreshAbilityCreator()
        {
            
        }
        
        #endregion
        
        // Bind a control that implements INotifyValueChanged<T> to a field (two-way)
        private Unbinder BindCreatorField<T>(
            EDataType kind,
            FieldData fd,
            Action<FieldInfo> onFocusIn = null,
            Action refAssign = null,
            Func<T, T> sanitizeIncoming = null, // optional: massage UI -> data
            Func<object, T> toUi = null, // optional: data -> UI conversion
            Func<T, object> toData = null) // optional: UI -> data conversion
        {
            creator_fields.TryAdd(kind, new Dictionary<string, FieldData>());
            creator_fields[kind][fd.Fi.Name] = fd;
            
            var control = fd.GetValueField<T>();
            if (control is null) throw new Exception(nameof(fd.Root));

            var dataHost = ReservedFocus.Node;
            if (dataHost is null) throw new ArgumentNullException(nameof(dataHost));
            
            // Initial push data -> UI
            var vData = fd.Fi.GetValue(dataHost);
            var vUI = toUi != null ? toUi(vData) : (T)ConvertTo(typeof(T), vData);
            control.SetValueWithoutNotify(vUI);

            EventCallback<FocusInEvent> inEvent = evt =>
            {
                onFocusIn?.Invoke(fd.Fi);
            };
            fd.Root.RegisterCallback(inEvent);
            
            EventCallback<FocusOutEvent> outEvent = evt =>
            {
                var newUI = sanitizeIncoming != null ? sanitizeIncoming(control.value) : control.value;
                var newDataObj = toData != null ? toData(newUI) : ConvertTo(fd.Fi.FieldType, newUI);

                // Set on the data object
                fd.Fi.SetValue(dataHost, newDataObj);
                control.SetValueWithoutNotify(newUI);

                refAssign?.Invoke();

                ValidateFieldOnEdit(fd);
            };
            fd.Root.RegisterCallback(outEvent);

            // Return an unbinder so callers can cleanly detach
            return new Unbinder(fd.Root, inEvent, outEvent);
        }
        
        static object ConvertTo(Type target, object value)
        {
            if (value == null) return target.IsValueType ? Activator.CreateInstance(target) : null;
            if (target.IsInstanceOfType(value)) return value;

            // Handle Enum <-> string/int
            if (target.IsEnum)
            {
                if (value is string s) return Enum.Parse(target, s, ignoreCase: true);
                if (IsNumber(value))   return Enum.ToObject(target, value);
            }

            // UI Toolkit gives numeric types as their exact T; Convert.ChangeType handles most cases
            return System.Convert.ChangeType(value, target);
        }
        
        static bool IsNumber(object v) =>
            v is sbyte or byte or short or ushort or int or uint or long or ulong;

        sealed class Unbinder : IDisposable
        {
            readonly VisualElement _c;
            private readonly EventCallback<FocusInEvent> inEvent;
            private readonly EventCallback<FocusOutEvent> outEvent;
            public Unbinder(VisualElement c, EventCallback<FocusInEvent> inEvent, EventCallback<FocusOutEvent> outEvent)
            {
                _c = c;
                this.inEvent = inEvent;
                this.outEvent = outEvent;
            }
            public void Dispose()
            {
                if (_c is null) return;
                
                _c.UnregisterCallback(inEvent);
                _c.UnregisterCallback(outEvent);
            }
        }
        
        void SetupPresetDropdown(DropdownField df, string target, Dictionary<string, string> styling)
        {
            df.choices.Clear();
            df.choices.Add("No Preset");
            df.index = 0;

            var templates = projectData[ReservedFocus.Kind].Where(n => n.TagStatus(ForgeTags.IS_TEMPLATE) && !n.TagStatus(ForgeTags.IS_SAVED_COPY));
            
            foreach (var t in templates)
            {
                df.choices.Add(t.Name);
                if (styling.ContainsKey(target) && t.Name == styling[target]) df.index = df.choices.Count - 1;
            }
        }
        
        void RefreshCreatorEditor()
        {
            RefreshSourcePane();
            RefreshBuriedPane();
            
            ForceResolveConsole(EConsoleContext.Creator);
            RefreshHeader();
            
            PostNodeValidateStep(ReservedFocus.Kind);
        }

        void RefreshCreatorHome()
        {
            
        }

        void LoadIntoCreator(ForgeDataNode node, EDataType kind)
        {
            SetFocus(ReservedFocus, node, kind, EForgeContext.Creator);
            LoadIntoCreator(ReservedFocus);
        }

        void LoadIntoCreator(DataContainer focus)
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
        
        void RefreshSourcePane()
        {
            // TODO refresh field values and validations

            c_save.text = ReservedFocus.Node.TagStatus(ForgeTags.IS_CREATED) ? "Save" : "Create";
        }

        void RefreshBuriedPane()
        {
            // TODO refresh buried field values and validations
        }
        
        void PostNodeValidateStep(EDataType target)
        {
            foreach (var fd in creator_fields[target])
            {
                ValidateFieldFromCreator(fd.Value, false);
            }

            /*foreach (var field in creator_buriedData)
            {
                ValidateFieldFromCreator(field, false);
            }*/
            
            RefreshConsole();
        }
        
        void UpdateFieldIndicator(FieldData efd, EValidationCode severe, string msg = null)
        {
            var res = DefaultIndicatorState(severe);
            
            efd.Indicator.style.backgroundColor = res.color;
            efd.Indicator.tooltip = msg ?? res.msg;
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

        private bool TryQuickCreateItem(FieldData efd, string _name, EDataType kind, out int _id)
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
            var allAlerts = creator_fields[dc.Kind]
            foreach (var alerts in )
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

        (int errors, int warnings) GetAlertCounts(EConsoleContext ctx, IConsoleMessenger efd)
        {
            int errors = 0, warnings = 0;
            var sourceHash = efd.GetHashCode();
            
            if (!console_entries[ctx].ContainsKey(sourceHash)) return (0, 0);
                    
            foreach (var ce in console_entries[ctx][sourceHash])
            {
                if (ce is null) continue;
                if (ce.code == EValidationCode.Error) errors += 1;
                if (ce.code == EValidationCode.Warn) warnings += 1;
            }

            return (errors, warnings);
        }
        
        private void TraceCreatorFieldSource(ConsoleEntry ce, FieldData efd, bool inOut)
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

        private void LinkCreatorFieldSource(ConsoleEntry ce, FieldData efd)
        {
            TraceCreatorFieldSource(ce, efd, true);

            var valueElem = efd.GetValueField();
            if (valueElem is null) return;

            valueElem.Focus();
        }

        #endregion
    }
}
