using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A tag field in Docs/EDITOR_STYLE.md's terms (ED-55): Unity's field, its label in the label column, the tag's parents in
    /// secondary text and its leaf in strong text, by its display name (<see cref="ForgeTagField"/>). Pressing it opens the
    /// tag picker, Unity's searchable dropdown of the tags its context offers (<see cref="ForgeTagContext"/>), with None and,
    /// where the field may make one, New Tag… (<see cref="ForgeTagPicker"/>). Its right-click menu clears it, makes a new
    /// tag, makes its tag a child of another, or copies its path. It was a button with a colored left border, a 120 px label
    /// it dropped for any field inside a list, text that changed as the pointer passed, and a picker drawn under it in
    /// tinted boxes in code, with a button that showed only on hover.
    /// </summary>
    [CustomPropertyDrawer(typeof(Tag))]
    public class TagDrawer : PropertyDrawer
    {
        // What a field's attributes say of it: the tags it offers, whether it makes new ones, and whether it edits
        private sealed class Settings
        {
            public string[] Contexts = Array.Empty<string>();
            public bool AllowCreate = true;
            public bool IncludeUniversal = true;
            public bool ReadOnly;
            public string Tooltip;
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "TagDrawerRoot" };
            // The style sheet lays out the field, wherever it's drawn (ED-20)
            ForgeTheme.Apply(root);
            var settings = SettingsOf(property);
            var field = new ForgeTagField(LabelOf(property)) { name = "TagField", tooltip = settings.Tooltip ?? "" };
            field.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            root.Add(field);

            // The tag each selected asset holds, mixed where they differ (ED-12)
            void Show()
            {
                if (!ScalerFields.Alive(property)) return;
                var each = EachAsset.Of(property).Select(TagOf).ToList();
                field.Show(each.Count > 0 ? each[0] : default, TagDisplaySettings.ShowFullPath);
                field.showMixedValue = each.Select(tag => tag.Name ?? "").Distinct().Count() > 1;
            }
            root.TrackSerializedObjectValue(property.serializedObject, _ => Show());
            Show();

            if (settings.ReadOnly)
            {
                field.SetEnabled(false);
                return root;
            }

            field.Opened += opened => OpenPicker(property, settings, opened.Box, "Tags", tag => Pick(property, tag, settings.Contexts));
            field.AddManipulator(new ContextualMenuManipulator(menu => Menu(menu, property, settings, field)));
            return root;
        }

        // The field's label: the one the inspector gives it, or its name; none for a list's element, whose list names it
        private string LabelOf(SerializedProperty property) =>
            property.propertyPath.EndsWith("]", StringComparison.Ordinal) ? null : preferredLabel ?? property.displayName;

        private Settings SettingsOf(SerializedProperty property)
        {
            var context = GetFieldAttribute<ForgeTagContext>(property);
            var settings = new Settings
            {
                Contexts = context?.Context ?? Array.Empty<string>(),
                AllowCreate = context?.AllowCreate ?? true,
                IncludeUniversal = context?.IncludeUniversal ?? true,
                ReadOnly = GetFieldAttribute<ForgeTagReadOnly>(property) != null,
                Tooltip = GetFieldAttribute<ForgeTagTooltip>(property)?.Tooltip ?? GetFieldAttribute<TooltipAttribute>(property)?.tooltip,
            };
            if (GetFieldAttribute<ForgeTagNoCreate>(property) != null) settings.AllowCreate = false;
            if (GetFieldAttribute<ForgeTagExcludeUniversal>(property) != null) settings.IncludeUniversal = false;
            return settings;
        }

        private bool GroupByRoot() => TagDisplaySettings.GroupByRoot;

        // Opens the picker under the field's box, over the tags the field's context offers
        private void OpenPicker(SerializedProperty property, Settings settings, VisualElement under, string title, Action<Tag> picked)
        {
            // The tags the active Forge Project uses alone, where another project uses them alone (FEAT-34)
            var offers = ForgeProjects.OffersTag();
            var tags = ForgeTagRegistry.GetTagsForContext(settings.Contexts, settings.IncludeUniversal).Where(offers).ToList();
            var start = StartOf(TagOf(property));
            new ForgeTagPicker(tags, settings.AllowCreate, GroupByRoot(), title, picked,
                () => ForgeNewTagWindow.ShowFor(start, settings.Contexts, made => Pick(property, made, settings.Contexts))).Show(under.worldBound);
        }

        // A new tag's path starts with the field's tag's parents
        private static string StartOf(Tag tag)
        {
            var path = TagDisplaySettings.PathOf(tag);
            int dot = path.LastIndexOf('.');
            return dot >= 0 ? path.Substring(0, dot + 1) : "";
        }

        // The field's right-click menu: None, New Tag…, Make It a Child Of…, Copy Path
        private void Menu(ContextualMenuPopulateEvent menu, SerializedProperty property, Settings settings, ForgeTagField field)
        {
            var tag = TagOf(property);
            bool held = tag.IsValid;
            menu.menu.AppendAction("None", _ => Pick(property, default, settings.Contexts),
                held ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            if (settings.AllowCreate)
            {
                menu.menu.AppendAction("New Tag…", _ => ForgeNewTagWindow.ShowFor(StartOf(tag), settings.Contexts, made => Pick(property, made, settings.Contexts)));
                // Its leaf under a tag the picker chooses, as Burn under Status.Debuff makes Status.Debuff.Burn
                menu.menu.AppendAction("Make It a Child Of…", _ => OpenPicker(property, settings, field.Box, "Make It a Child Of", parent =>
                {
                    if (!parent.IsValid) return;
                    var leaf = TagDisplaySettings.PathOf(tag);
                    leaf = leaf.Substring(leaf.LastIndexOf('.') + 1);
                    Pick(property, Tag.GenerateAsUnique($"{TagDisplaySettings.PathOf(parent)}.{leaf}"), settings.Contexts);
                }), held ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
            menu.menu.AppendAction("Copy Path", _ => EditorGUIUtility.systemCopyBuffer = TagDisplaySettings.PathOf(tag),
                held ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Reading and writing a tag
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gives a field a tag, its name and display name, in each selected asset, as one Undo step, and tells the tag
        /// registry which assets use it now, and which no longer use the tag they had. The default tag clears the field.
        /// </summary>
        internal static void Pick(SerializedProperty property, Tag tag, string[] contexts)
        {
            if (property == null) return;
            var serialized = property.serializedObject;
            serialized.Update();
            var own = serialized.FindProperty(property.propertyPath);
            if (own == null) return;
            var had = serialized.targetObjects
                .Select(target => (Target: target, Tag: TagOf(new SerializedObject(target).FindProperty(property.propertyPath))))
                .ToList();
            Write(own, tag);
            serialized.ApplyModifiedProperties();
            foreach (var (target, old) in had)
            {
                if (target is not ScriptableObject asset) continue;
                if (old.IsValid && old.Name != tag.Name) ForgeTagRegistry.UnregisterTagUsage(old, contexts ?? Array.Empty<string>(), asset);
                if (tag.IsValid) ForgeTagRegistry.RegisterTagUsage(tag, contexts ?? Array.Empty<string>(), asset);
            }
        }

        /// <summary>
        /// The tag a field holds. The registry's tag of its name, with its display name; after a domain reload the registry
        /// may not know a unique tag, so the field's own display name fills it in.
        /// </summary>
        internal static Tag TagOf(SerializedProperty property)
        {
            var nameProp = property?.FindPropertyRelative(nameof(Tag.Name));
            if (nameProp == null || string.IsNullOrEmpty(nameProp.stringValue)) return default;
            var resolved = TagRegistry.Resolve(nameProp.stringValue);
            if (resolved.DisplayName != resolved.Name) return resolved;
            var display = property.FindPropertyRelative(nameof(Tag.DisplayName))?.stringValue;
            return !string.IsNullOrEmpty(display) && display != resolved.Name ? Tag.Generate(resolved.Name, display) : resolved;
        }

        private static void Write(SerializedProperty property, Tag tag)
        {
            var nameProp = property.FindPropertyRelative(nameof(Tag.Name));
            if (nameProp != null) nameProp.stringValue = tag.IsValid ? tag.Name : "";
            var displayProp = property.FindPropertyRelative(nameof(Tag.DisplayName));
            if (displayProp != null) displayProp.stringValue = tag.IsValid ? tag.DisplayName : "";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The field's attributes
        // ═══════════════════════════════════════════════════════════════════════════

        private T GetFieldAttribute<T>(SerializedProperty property) where T : System.Attribute
        {
            if (fieldInfo != null) return fieldInfo.GetCustomAttribute<T>();
            var targets = property.serializedObject.targetObjects;
            if (targets == null || targets.Length == 0) return null;
            return ResolveFieldInfo(targets[0].GetType(), property.propertyPath)?.GetCustomAttribute<T>();
        }

        private static FieldInfo ResolveFieldInfo(Type host, string propertyPath)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var type = host;
            FieldInfo last = null;
            foreach (var part in propertyPath.Split('.'))
            {
                if (part == "Array" || part.StartsWith("data[")) continue;
                var field = type.GetField(part, flags);
                if (field == null) return last;
                last = field;
                type = field.FieldType;
                if (type.IsArray) type = type.GetElementType();
                else if (type.IsGenericType && typeof(System.Collections.IList).IsAssignableFrom(type)) type = type.GetGenericArguments()[0];
            }
            return last;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;
    }
}
