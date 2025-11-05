using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeEditor
    {
        private class EditorRegistry
        {
            private List<AbstractDrawer> drawers = new();

            public static EditorRegistry DefaultRegistry(
                VisualTreeAsset IntDrawer,
                VisualTreeAsset FloatDrawer,
                VisualTreeAsset StringDrawer,
                VisualTreeAsset BoolDrawer,
                VisualTreeAsset EnumDrawer,
                VisualTreeAsset ObjectDrawer,
                VisualTreeAsset ColorDrawer,
                VisualTreeAsset GradientDrawer,
                VisualTreeAsset Vector2Drawer,
                VisualTreeAsset Vector2IntDrawer,
                VisualTreeAsset Vector3Drawer,
                VisualTreeAsset Vector3IntDrawer,
                VisualTreeAsset EnumerableDrawer,
                VisualTreeAsset DictDrawer,
                VisualTreeAsset QuickRefDrawer,
                VisualTreeAsset RefDrawer,
                VisualTreeAsset CompositeDrawer
                )
            {
                return new EditorRegistry()
                    .Register(new IntDrawer(IntDrawer))
                    .Register(new FloatDrawer(FloatDrawer))
                    .Register(new StringDrawer(StringDrawer))
                    .Register(new BoolDrawer(BoolDrawer))
                    .Register(new EnumDrawer(EnumDrawer))
                    .Register(new ObjectDrawer(ObjectDrawer))
                    .Register(new QuickForgeDrawer(QuickRefDrawer))
                    .Register(new ForgeDrawer(RefDrawer))
                    .Register(new ObjectDrawer(ObjectDrawer))
                    .Register(new EnumerableDrawer(EnumerableDrawer))
                    .Register(new DictDrawer(DictDrawer))
                    .Register(new ColorDrawer(ColorDrawer))
                    .Register(new GradientDrawer(GradientDrawer))
                    .Register(new Vector2Drawer(Vector2Drawer))
                    .Register(new Vector2IntDrawer(Vector2IntDrawer))
                    .Register(new Vector3Drawer(Vector3Drawer))
                    .Register(new Vector3IntDrawer(Vector3IntDrawer))
                    .Register(new CompositeDrawer(CompositeDrawer));
            }

            private EditorRegistry Register(AbstractDrawer drawer)
            {
                drawers.Add(drawer);
                return this;
            }

            public AbstractDrawer Find(Type type)
            {
                return drawers.FirstOrDefault(drawer => drawer.CanDraw(type));
            }
        }

        struct ValuePacket
        {
            
        }

        private abstract class AbstractDrawer
        {
            protected VisualTreeAsset SourceAsset;

            protected AbstractDrawer(VisualTreeAsset sourceAsset)
            {
                SourceAsset = sourceAsset;
            }

            public void SetInitialValue(EditorFieldData efd, Type type)
            {
                var current = efd.Fi.GetValue(efd.Target);

                if (current is null)
                {
                    if (!TryCreateInstance(type, out current))
                    {
                        
                    }
                }
                efd.Value = current;
                
                var valueElem = efd.GetValueElement?.Invoke() as INotifyValueChanged<string>;
                if (valueElem is null) return;
                
                valueElem.SetValueWithoutNotify(ObjectNames.NicifyVariableName(type.Name));
            }
            
            public void SetInitialValue<T>(EditorFieldData efd)
            {
                var current = (T)efd.Fi.GetValue(efd.Target);
                efd.Value = current;
                
                var valueElem = efd.GetValueElement?.Invoke() as INotifyValueChanged<T>;
                if (valueElem is null) return;
                
                valueElem.SetValueWithoutNotify(current);
            }
            
            public abstract Type ValueType();

            public virtual bool CanDraw(Type t)
            {
                return t == ValueType();
            }

            public abstract VisualElement Draw(PlayForgeEditor.EditorFieldData efd,
                Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusOut = null,
                int height = 30, PlayForgeEditor editor = null);

            public abstract void AttachValidations(EditorFieldData efd);

            private VisualElement GetValueElement(VisualElement root, string target = "Value")
            {
                return GetValueElement<VisualElement>(root, target);
            }

            protected T GetValueElement<T>(VisualElement root, string target = "Value") where T : VisualElement
            {
                return root.Q<T>(target);
            }

            protected void Build(PlayForgeEditor.EditorFieldData efd,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn,
                Action<PlayForgeEditor.EditorFieldData> onFocusOut)
            {
                Build<VisualElement>(efd, onFocusIn, onFocusOut);
            }
            
            protected void Build<T>(PlayForgeEditor.EditorFieldData efd,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn,
                Action<PlayForgeEditor.EditorFieldData> onFocusOut) where T : VisualElement
            {
                
                if (efd.GetValueElement?.Invoke() is not T field) return;
                
                field.RegisterCallback<FocusInEvent>(_ =>
                {
                    onFocusIn?.Invoke(efd);
                });
                field.RegisterCallback<FocusOutEvent>(_ =>
                {
                    onFocusOut?.Invoke(efd);
                });
            }

            protected void RegisterValueChanged<T>(EditorFieldData efd, INotifyValueChanged<T> value, Action<PlayForgeEditor.EditorFieldData, object> onChange)
            {
                value.RegisterValueChangedCallback(evt =>
                {
                    onChange?.Invoke(efd, evt.newValue);
                    efd.Value = evt.newValue;
                });
            }

            protected VisualElement SetLabel(VisualElement root, FieldInfo fi)
            {
                var valueField = GetValueElement(root);
                if (valueField is null)
                {
                    var titleLabel = root.Q<Label>("Title");
                    var typeLabel = root.Q<Label>("TypeLabel");
                    typeLabel.text = ObjectNames.NicifyVariableName(fi.FieldType.Name);
                    
                    return SetLabel(root, titleLabel, fi);
                }
                
                var label = valueField.Q<Label>();
                return SetLabel(root, label, fi);
            }

            protected VisualElement SetLabel(VisualElement root, Label label, FieldInfo fi)
            {
                label.text = fi.Name;
                return root;
            }

            protected VisualElement Template(PlayForgeEditor.EditorFieldData efd)
            {
                return Template<VisualElement>(efd);
            }
            
            protected VisualElement Template<T>(PlayForgeEditor.EditorFieldData efd) where T : VisualElement
            {
                var root = SourceAsset.CloneTree();

                efd.Root = root;
                efd.GetValueElement = () => GetValueElement<T>(root);

                return root;
            }
        }

        private class StringDrawer : AbstractDrawer
        {
            public StringDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }

            public override Type ValueType()
            {
                return typeof(string);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd,
                Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusOut = null,
                int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template<TextField>(efd);
                Build<TextField>(efd, onFocusIn, onFocusOut);
                RegisterValueChanged(efd, efd.GetValueElement?.Invoke() as INotifyValueChanged<string>, onChange);
                SetInitialValue<string>(efd);
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }

        private class IntDrawer : AbstractDrawer
        {
            public IntDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(int);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template<IntegerField>(efd);
                Build<IntegerField>(efd, onFocusIn, onFocusOut);
                RegisterValueChanged(efd, efd.GetValueElement?.Invoke() as INotifyValueChanged<int>, onChange);
                SetInitialValue<int>(efd);
                return SetLabel(root, efd.Fi);
            }
            public Func<object, EditorFieldData, List<ValidationPacket>> aAttachValidations(EditorFieldData efd)
            {
                return (_, _) => new List<ValidationPacket>() { new ValidationPacket() };
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }
        
        private class FloatDrawer : AbstractDrawer
        {
            public FloatDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(float);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template<FloatField>(efd);
                Build<FloatField>(efd, onFocusIn, onFocusOut);
                RegisterValueChanged(efd, efd.GetValueElement?.Invoke() as INotifyValueChanged<float>, onChange);
                SetInitialValue<float>(efd);
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }
        
        private class BoolDrawer : AbstractDrawer
        {
            public BoolDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(bool);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template<Toggle>(efd);
                Build<Toggle>(efd, onFocusIn, onFocusOut);
                RegisterValueChanged(efd, efd.GetValueElement?.Invoke() as INotifyValueChanged<bool>, onChange);
                SetInitialValue<bool>(efd);
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }
        
        private class EnumDrawer : AbstractDrawer
        {
            public EnumDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(Enum);
            }
            public override bool CanDraw(Type t)
            {
                return t.IsEnum;
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template<EnumField>(efd);
                Build<EnumField>(efd, onFocusIn, onFocusOut);
                RegisterValueChanged(efd, efd.GetValueElement?.Invoke() as INotifyValueChanged<Enum>, onChange);
                SetInitialValue<Enum>(efd);
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }
        
        private class Vector2Drawer : AbstractDrawer
        {
            public Vector2Drawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(Vector2);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template(efd);
                Build(efd, onFocusIn, onFocusOut);

                foreach (var valField in root.Query<FloatField>().ToList())
                {
                    RegisterValueChanged(efd, valField, onChange);
                }
                
                SetInitialValue<Vector2>(efd);
                
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }
        
        private class Vector2IntDrawer : AbstractDrawer
        {
            public Vector2IntDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(Vector2Int);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template(efd);
                Build(efd, onFocusIn, onFocusOut);
                
                foreach (var valField in root.Query<IntegerField>().ToList())
                {
                    RegisterValueChanged(efd, valField, onChange);
                }
                
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }
        
        private class Vector3Drawer : AbstractDrawer
        {
            public Vector3Drawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(Vector3);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template(efd);
                Build(efd, onFocusIn, onFocusOut);
                foreach (var valField in root.Query<FloatField>().ToList())
                {
                    RegisterValueChanged(efd, valField, onChange);
                }
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }
        
        private class Vector3IntDrawer : AbstractDrawer
        {
            public Vector3IntDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(Vector3Int);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template(efd);
                Build(efd, onFocusIn, onFocusOut);
                foreach (var valField in root.Query<IntegerField>().ToList())
                {
                    RegisterValueChanged(efd, valField, onChange);
                }
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }
        
        private class ObjectDrawer : AbstractDrawer
        {
            public ObjectDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(Object);
            }
            public override bool CanDraw(Type t)
            {
                return ValueType().IsAssignableFrom(t);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template<ObjectField>(efd);
                Build<ObjectField>(efd, onFocusIn, onFocusOut);
                RegisterValueChanged(efd, efd.GetValueElement?.Invoke() as INotifyValueChanged<object>, onChange);
                SetInitialValue<object>(efd);
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                var fv = efd.GetFiAttribute<ForgeValidation>();
                if (fv is not null) efd.Validation += (o, _) => o is null ? new List<ValidationPacket>() { new(Console.Creator.Validation.NullValue) } : new List<ValidationPacket>();
                
                efd.Validation += (o, _) => o is null ? new List<ValidationPacket>() { new(Console.Creator.Validation.NullValue) } : new List<ValidationPacket>();
            }
        }
        
        /// <summary>
        /// For Tags & Attributes, which can quickly be made
        /// </summary>
        private class QuickForgeDrawer : AbstractDrawer
        {
            private const string placeholder = "Search/Create...";
            
            public QuickForgeDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return null;
            }
            public override bool CanDraw(Type t)
            {
                return t == typeof(Tag) || t == typeof(Attribute);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template<TextField>(efd);
                
                Build<TextField>(efd, onFocusIn, onFocusOut);
                SetInitialValue<ForgeDataNode>(efd);
                
                var quickCreate = root.Q<Button>("QuickCreate");
                var field = GetValueElement<TextField>(root);
                
                field.RegisterValueChangedCallback(evt =>
                {
                    quickCreate.SetEnabled(!string.IsNullOrEmpty(evt.newValue)); 
                    
                    // TODO open search dropdown and set value
                });

                quickCreate.clicked += () =>
                {
                    bool status = editor.TryQuickCreateItem(efd, field.value, efd.GetDataType(), out int _id);
                    if (status) efd.Value = editor.Project.TryGet(_id, efd.GetDataType(), out var node) ? node : null;
                    else efd.Value = null;
                };
                
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                efd.Validation += (o, _) => o is null ? new List<ValidationPacket>() { new(Console.Creator.Validation.NullValue) } : new List<ValidationPacket>();
            }
        }
        
        private class ForgeDrawer : AbstractDrawer
        {
            public ForgeDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(ForgeDataNode);
            }
            public override bool CanDraw(Type t)
            {
                return ValueType().IsAssignableFrom(t) && t != ValueType();
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template<TextField>(efd);
                Build<TextField>(efd, onFocusIn, onFocusOut);
                SetInitialValue<ForgeDataNode>(efd);
                
                var field = GetValueElement<TextField>(root);
                
                field.RegisterValueChangedCallback(evt =>
                {
                    // TODO open search dropdown and set value
                });
                
                RegisterValueChanged(efd, efd.GetValueElement?.Invoke() as INotifyValueChanged<object>, onChange);
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                efd.Validation += (o, _) => o is null ? new List<ValidationPacket>() { new(Console.Creator.Validation.NullValue) } : new List<ValidationPacket>();
            }
        }
        
        private class CompositeDrawer : AbstractDrawer
        {
            public CompositeDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return null;
            }
            public override bool CanDraw(Type t)
            {
                return t.IsClass;
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template(efd);
                Build(efd, onFocusIn, onFocusOut);
                SetInitialValue(efd, efd.Fi.FieldType);
                
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                // TODO deal with buried fields
                efd.Validation += (o, _) => o is null ? new List<ValidationPacket>() { new(Console.Creator.Validation.NullValue) } : new List<ValidationPacket>();
            }
        }

        private class ColorDrawer : AbstractDrawer
        {
            public ColorDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(Color);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template<GradientField>(efd);
                Build<ColorField>(efd, onFocusIn, onFocusOut);
                RegisterValueChanged(efd, efd.GetValueElement?.Invoke() as INotifyValueChanged<Color>, onChange);
                SetInitialValue<Color>(efd);
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }
        
        private class GradientDrawer : AbstractDrawer
        {
            public GradientDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(Gradient);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template<GradientField>(efd);
                Build<GradientField>(efd, onFocusIn, onFocusOut);
                RegisterValueChanged(efd, efd.GetValueElement?.Invoke() as INotifyValueChanged<Gradient>, onChange);
                SetInitialValue<Gradient>(efd);
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                
            }
        }
        
        private class EnumerableDrawer : AbstractDrawer
        {
            public EnumerableDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return null;
            }
            public override bool CanDraw(Type t)
            {
                return t.IsArray || t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template(efd);
                Build(efd, onFocusIn, onFocusOut);
                
                // TODO set default value on init
                
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                // TODO deal with buried values
                efd.Validation += (o, _) => o is null ? new List<ValidationPacket>() { new(Console.Creator.Validation.NullValue) } : new List<ValidationPacket>();
            }
        }
        
        private class DictDrawer : AbstractDrawer
        {
            private Type type => typeof(Dictionary<,>);
            public DictDrawer(VisualTreeAsset sourceAsset) : base(sourceAsset)
            {
            }
            public override Type ValueType()
            {
                return typeof(Dictionary<,>);
            }
            public override bool CanDraw(Type t)
            {
                return t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>);
            }
            public override VisualElement Draw(PlayForgeEditor.EditorFieldData efd, Action<PlayForgeEditor.EditorFieldData, object> onChange = null,
                Action<PlayForgeEditor.EditorFieldData> onFocusIn = null, Action<PlayForgeEditor.EditorFieldData> onFocusOut = null, int height = 30, PlayForgeEditor editor = null)
            {
                var root = Template(efd);
                Build(efd, onFocusIn, onFocusOut);
                
                // TODO set default value on init
                
                return SetLabel(root, efd.Fi);
            }
            public override void AttachValidations(EditorFieldData efd)
            {
                // TODO deal with buried values
                efd.Validation += (o, _) => o is null ? new List<ValidationPacket>() { new(Console.Creator.Validation.NullValue) } : new List<ValidationPacket>();
            }
        }
    }
}
