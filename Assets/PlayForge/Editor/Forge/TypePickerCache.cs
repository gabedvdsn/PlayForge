using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public static class TypePickerCache
    {
        static readonly Dictionary<Type, List<Type>> _byBase = new();

        public static IReadOnlyList<Type> GetConcreteTypesAssignableTo(Type baseOrInterface)
        {
            if (baseOrInterface == null) return Array.Empty<Type>();
            if (_byBase.TryGetValue(baseOrInterface, out var cached)) return cached;

            IEnumerable<Type> src = TypeCache.GetTypesDerivedFrom(baseOrInterface);

            var list = src
                .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
                .OrderBy(t => t.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _byBase[baseOrInterface] = list;
            return list;
        }

        public static IReadOnlyList<Type> GetConcreteTypesAssignableTo<TBase>() => GetConcreteTypesAssignableTo(typeof(TBase));
    }
    
    /// Minimal searchable picker window.
    class TypePickerWindow : EditorWindow
    {
        Type _base;
        Action<Type> _onPicked;
        ListView _list;
        TextField _search;
        List<Type> _items = new();

        public static void Open(Rect anchor, Type baseOrInterface, Action<Type> onPicked, Type preselect = null)
        {
            var w = CreateInstance<TypePickerWindow>();
            w._base = baseOrInterface;
            w._onPicked = onPicked;
            w.titleContent = new GUIContent($"Select {baseOrInterface.Name}");
            w.ShowAsDropDown(anchor, new Vector2(Mathf.Max(280, anchor.width), 340));
        }

        void OnEnable()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 6; root.style.paddingRight = 6; root.style.paddingTop = 6; root.style.paddingBottom = 6;
            _search = new TextField { label = "Search", value = "" };
            _search.RegisterValueChangedCallback(_ => Rebuild());
            root.Add(_search);

            _list = new ListView { selectionType = SelectionType.Single };
            _list.makeItem = () => new Label { style = { unityTextAlign = TextAnchor.MiddleLeft } };
            _list.bindItem = (e, i) => ((Label)e).text = _items[i].FullName;
            _list.itemsChosen += objs =>
            {
                var t = objs.OfType<Type>().FirstOrDefault();
                if (t != null) { _onPicked?.Invoke(t); Close(); }
            };
            _list.selectionChanged += objs =>
            {
                // double-click support: UI Toolkit sends onItemsChosen with double-click already
            };
            _list.style.flexGrow = 1;
            root.Add(_list);

            Rebuild();
        }

        void Rebuild()
        {
            var all = TypePickerCache.GetConcreteTypesAssignableTo(_base);
            var q = _search.value?.Trim();
            _items = string.IsNullOrEmpty(q)
                ? all.ToList()
                : all.Where(t => t.FullName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                               || t.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                     .ToList();

            _list.itemsSource = _items;
            _list.Rebuild();
        }
    }
    
    /// Drop-in UI field for picking a type assignable to TBase.
    public class TypePickerField<TBase> : VisualElement
    {
        readonly Label _label;
        readonly Button _button;
        TypeRef _value;

        public TypeRef value
        {
            get => _value;
            set
            {
                _value = value;
                _label.text = _value.IsEmpty ? "<None>" : _value.SystemType?.FullName ?? "<Missing>";
            }
        }

        //public new class UxmlFactory : UxmlFactory<TypePickerField<TBase>, UxmlTraits> { }

        public TypePickerField(string label, TypeRef initial, Action<TypeRef> onChanged)
        {
            style.flexDirection = FlexDirection.Row;
            style.alignItems = Align.Center;

            Add(new Label(label) { style = { minWidth = 120 } });

            _label = new Label("<None>")
            {
                style = {
                    flexGrow = 1,
                    backgroundColor = new Color(.18f,.18f,.18f,1),
                    paddingLeft = 6, paddingRight = 6, paddingTop = 4, paddingBottom = 4,
                    borderTopLeftRadius = 4, borderBottomLeftRadius = 4,
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };
            Add(_label);

            _button = new Button(() =>
                {
                    var anchor = worldBound; // open near this control
                    TypePickerWindow.Open(anchor, typeof(TBase), t =>
                    {
                        value = TypeRef.From(t);
                        onChanged?.Invoke(_value);
                    }, value.SystemType);
                })
                { text = "Pick", tooltip = $"Pick a {typeof(TBase).Name}" };

            _button.style.width = 60;
            _button.style.borderTopRightRadius = 4; _button.style.borderBottomRightRadius = 4;
            Add(_button);

            value = initial;
        }
        public TypePickerField()
        {
            
        }
    }
    
    /// Stores a type by AssemblyQualifiedName so it survives reloads + JSON.
    [Serializable]
    public struct TypeRef
    {
        [SerializeField] private string _asmQualifiedName;

        public bool IsEmpty => string.IsNullOrEmpty(_asmQualifiedName);
        public string AssemblyQualifiedName => _asmQualifiedName;

        public Type SystemType
        {
            get
            {
                if (string.IsNullOrEmpty(_asmQualifiedName)) return null;
                // Type.GetType handles same-assembly; when null, search all loaded assemblies.
                var t = Type.GetType(_asmQualifiedName, throwOnError: false);
                if (t != null) return t;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    t = a.GetType(_asmQualifiedName.Split(',')[0], false);
                    if (t != null) return t;
                }
                return null;
            }
        }

        public override string ToString() =>
            (SystemType != null ? SystemType.FullName : "<None>") ?? "<None>";

        public static TypeRef From(Type t) =>
            new TypeRef { _asmQualifiedName = t?.AssemblyQualifiedName };

        public static implicit operator Type(TypeRef r) => r.SystemType;
    }
}
