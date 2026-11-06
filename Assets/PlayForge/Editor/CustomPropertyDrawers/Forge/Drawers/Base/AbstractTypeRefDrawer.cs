using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A [SerializeReference] field of an abstract type, in Docs/EDITOR_STYLE.md's terms (ED-56): Unity's field, its label
    /// in the label column, the kind it holds named in its box, which opens Unity's searchable dropdown of the kinds it can
    /// hold; then the kind's own fields, each Unity's, indented under it. A pick makes a new object of the kind for each
    /// selected asset, by the kind's constructor so its initializers run, as one Undo step; drawing writes nothing (ED-12).
    /// <para>
    /// Every element of a list shares one drawer, so each field keeps its state in its own elements. The drawer kept the
    /// last element's fields and kind in fields of its own, so an edit to one element could fill another with its fields.
    /// It drew a button that opened a list in tinted boxes, and the kind's fields in a box with an orange left border.
    /// </para>
    /// </summary>
    public abstract class AbstractTypeRefDrawer<T> : PropertyDrawer where T : class
    {
        public const string RootClass = "pf-type-ref";
        protected const string HiddenClass = "pf-hidden";

        // ═══════════════════════════════════════════════════════════════════════════
        // What a drawer can change
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Which kinds the picker offers.</summary>
        protected virtual bool FilterType(Type type) => true;

        /// <summary>A kind's class name as its name starts, before it's spaced out: by default without "AbilityTask" or "Task".</summary>
        protected virtual string CleanTypeName(string typeName)
        {
            foreach (var suffix in new[] { "AbilityTask", "Task" })
                if (typeName.EndsWith(suffix) && typeName.Length > suffix.Length) return typeName.Substring(0, typeName.Length - suffix.Length);
            return typeName;
        }

        /// <summary>The kinds the field can hold: the concrete classes of its type.</summary>
        protected virtual Type[] GetEntries() =>
            TypePickerCache.GetConcreteTypesAssignableTo<T>().Where(FilterType).OrderBy(type => type.Name).ToArray();

        /// <summary>A kind's name as the field and the picker show it, or None.</summary>
        protected virtual string GetStringValue(SerializedProperty prop, Type value) =>
            value == null ? "None" : ObjectNames.NicifyVariableName(CleanTypeName(value.Name));

        /// <summary>Whether the field may be empty, so the picker offers None.</summary>
        protected virtual bool AcceptClear(SerializedProperty prop) => true;

        /// <summary>Called after a kind is picked, with the property found again.</summary>
        protected virtual void OnTypeChanged(SerializedProperty prop, Type newType) { }

        /// <summary>
        /// Gives the field a new object of a kind, or none: one for each selected asset, made by the kind's constructor, as
        /// one Undo step.
        /// </summary>
        protected virtual void SetValue(SerializedProperty prop, Type value)
        {
            if (prop == null) return;
            EachAsset.Write(prop, own => own.managedReferenceValue = value != null ? Activator.CreateInstance(value) : null);
            OnTypeChanged(prop, value);
        }

        /// <summary>The kind the field holds: its object's class, or the class its data names when Unity can't find it.</summary>
        protected Type GetCurrentValue(SerializedProperty prop)
        {
            if (prop == null || prop.propertyType != SerializedPropertyType.ManagedReference) return null;
            if (prop.managedReferenceValue != null) return prop.managedReferenceValue.GetType();
            return TypeNamed(prop.managedReferenceFullTypename);
        }

        /// <summary>
        /// Lays out the field's parts: its kind field, then the kind's fields. A drawer that folds puts an arrow before the
        /// kind field and a line of values under it.
        /// </summary>
        protected virtual void Arrange(VisualElement root, SerializedProperty property, VisualElement field, VisualElement fields)
        {
            root.Add(field);
            root.Add(fields);
        }

        /// <summary>Called each time the field shows its kind, with the property found again.</summary>
        protected virtual void Shown(VisualElement root, SerializedProperty property) { }

        // ═══════════════════════════════════════════════════════════════════════════
        // The field
        // ═══════════════════════════════════════════════════════════════════════════

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "TypeRef" };
            // The style sheet lays out the field, wherever it's drawn (ED-20)
            ForgeTheme.Apply(root);
            root.AddToClassList(RootClass);

            var field = new ForgeTypeField(LabelOf(property));
            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            var fields = new VisualElement { name = "TypeFields" };
            fields.AddToClassList("pf-type-ref__fields");
            Arrange(root, property, field, fields);

            // The property found again by its path each time, as a list's elements move and an Undo puts objects back (ED-49)
            var serialized = property.serializedObject;
            var path = property.propertyPath;
            SerializedProperty Fresh()
            {
                try
                {
                    return serialized.targetObject != null ? serialized.FindProperty(path) : null;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            // The kind the fields were built for, so they're built again only when it changes, not as their values do
            string built = null;
            void Show()
            {
                var prop = Fresh();
                if (prop == null || prop.propertyType != SerializedPropertyType.ManagedReference) return;
                bool mixed = EachAsset.Differ(prop, each => each.managedReferenceFullTypename);
                var type = GetCurrentValue(prop);
                field.showMixedValue = mixed;
                var data = prop.managedReferenceFullTypename;
                // A class Unity can't find keeps its data, and the field names it as missing
                var name = type != null || string.IsNullOrEmpty(data) ? GetStringValue(prop, type) : $"{data.Substring(data.LastIndexOf('.') + 1)} (missing)";
                if (!mixed) field.Show(name, string.IsNullOrEmpty(data), TipOf(prop, type));
                var kind = mixed ? "\u0001mixed" : prop.managedReferenceFullTypename ?? "";
                if (kind != built)
                {
                    built = kind;
                    Fill(fields, prop, mixed);
                }
                Shown(root, prop);
            }

            field.Opened += opened =>
            {
                var prop = Fresh();
                if (prop == null) return;
                var title = string.IsNullOrEmpty(field.label) ? KindTitle() : field.label;
                new ForgeTypePicker(KindsFor(prop), AcceptClear(prop), title, type =>
                {
                    var now = Fresh();
                    if (now != null) SetValue(now, type);
                }).Show(opened.Box.worldBound);
            };
            root.TrackPropertyValue(property, _ => Show());
            Show();
            return root;
        }

        // The picker's heading for a field with no label: the type's name, as "Ability Task" for AbstractAbilityTask
        private static string KindTitle()
        {
            var name = typeof(T).Name;
            if (name.StartsWith("Abstract")) name = name.Substring("Abstract".Length);
            else if (typeof(T).IsInterface && name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1])) name = name.Substring(1);
            return ObjectNames.NicifyVariableName(name);
        }

        /// <summary>The kinds the picker offers, each with its name, in name order.</summary>
        internal IReadOnlyList<(Type Type, string Name)> KindsFor(SerializedProperty prop) =>
            GetEntries().Where(type => type != null && !type.IsAbstract)
                .Select(type => (type, GetStringValue(prop, type)))
                .OrderBy(kind => kind.Item2, StringComparer.OrdinalIgnoreCase)
                .ToList();

        // The kind's own fields, each Unity's, under the kind field; or why there are none to show
        private static void Fill(VisualElement fields, SerializedProperty prop, bool mixed)
        {
            fields.Clear();
            if (mixed)
            {
                var note = new Label("The selected fields hold different kinds.") { name = "MixedKinds" };
                note.AddToClassList("pf-note");
                fields.Add(note);
                fields.RemoveFromClassList(HiddenClass);
                return;
            }
            int count = 0;
            if (prop.managedReferenceValue != null)
            {
                var child = prop.Copy();
                var end = prop.GetEndProperty();
                if (child.NextVisible(true))
                {
                    do
                    {
                        if (SerializedProperty.EqualContents(child, end)) break;
                        fields.Add(new PropertyField(child.Copy()) { name = child.name });
                        count++;
                    } while (child.NextVisible(false));
                }
            }
            fields.EnableInClassList(HiddenClass, count == 0);
            if (count > 0) fields.Bind(prop.serializedObject);
        }

        /// <summary>Whether the field's kind has fields of its own to show.</summary>
        protected static bool HasFields(SerializedProperty prop)
        {
            if (prop == null || prop.managedReferenceValue == null) return false;
            var child = prop.Copy();
            var end = prop.GetEndProperty();
            return child.NextVisible(true) && !SerializedProperty.EqualContents(child, end);
        }

        // The label a field shows: none as a list's row, whose rows need none, else the property's
        private string LabelOf(SerializedProperty property) =>
            property.propertyPath.EndsWith("]") ? null : preferredLabel ?? property.displayName;

        private static string TipOf(SerializedProperty prop, Type type)
        {
            if (type != null) return type.FullName;
            var data = prop.managedReferenceFullTypename;
            return string.IsNullOrEmpty(data) ? "Empty: pick a kind" : $"Its class can't be found: {data}";
        }

        // A class by the name Unity keeps for it, as "Assembly Namespace.Class"
        private static Type TypeNamed(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return null;
            var parts = fullTypeName.Split(' ');
            if (parts.Length != 2) return null;
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => assembly.GetName().Name == parts[0])
                .Select(assembly => assembly.GetType(parts[1]))
                .FirstOrDefault(found => found != null);
        }
    }
}
