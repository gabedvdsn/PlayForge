using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A kind field that folds, as an attribute set element does (ED-56): a row with the arrow and the kind field, then the
    /// kind's fields; folded, one line of their values in secondary text, as "Strength 2 · Duration 0.5 · On Each Tick".
    /// Whether it's folded is kept with the object the field holds, so it follows the object through its list (ED-18). It
    /// drew colored badges for a folded field's values, and its arrow and its look from styles set in C#.
    /// </summary>
    public abstract class AbstractGenericDrawer<T> : AbstractTypeRefDrawer<T> where T : class
    {
        public const string CollapsedClass = "pf-element--collapsed";

        // The arrow keeps its place while a kind has no fields to fold, so the field lines up with its neighbours
        private const string InvisibleClass = "pf-invisible";

        /// <summary>Whether the field folds.</summary>
        protected virtual bool EnableCollapse => true;

        /// <summary>Whether a folded field shows a line of its values.</summary>
        protected virtual bool EnableSummary => true;

        /// <summary>The words a kind's class name drops from its end before it's spaced out, as "Worker".</summary>
        protected virtual string[] TypeNameSuffixes => new[]
        {
            "Worker", "Scaler", "Provider", "Handler", "Processor", "Factory", "Behaviour", "Behavior", "Strategy",
            "Policy", "Rule", "Config", "Settings", "Task", "Cue",
        };

        protected override string CleanTypeName(string name)
        {
            foreach (var suffix in TypeNameSuffixes)
            {
                if (name.EndsWith(suffix) && name.Length > suffix.Length)
                    return name.Substring(0, name.Length - suffix.Length);
            }
            return name;
        }

        protected override void Arrange(VisualElement root, SerializedProperty property, VisualElement field, VisualElement fields)
        {
            root.AddToClassList("pf-element");
            var header = new VisualElement { name = "HeaderRow" };
            header.AddToClassList("pf-element__header");
            root.Add(header);
            var arrow = new ForgeIconButton(ForgeGlyphShape.ArrowDown, "Collapse", null) { name = "CollapseBtn" };
            arrow.AddToClassList("pf-element__arrow");
            arrow.EnableInClassList(HiddenClass, !EnableCollapse);
            header.Add(arrow);
            field.AddToClassList("pf-element__attribute");
            header.Add(field);

            var summary = new Label { name = "Summary" };
            summary.AddToClassList("pf-element__summary");
            root.Add(summary);

            fields.AddToClassList("pf-element__content");
            root.Add(fields);

            // The arrow folds the field it's in; the property is found again by its path, as the list's elements move
            var serialized = property.serializedObject;
            var path = property.propertyPath;
            arrow.clicked += () =>
            {
                var prop = serialized.targetObject != null ? serialized.FindProperty(path) : null;
                if (prop == null) return;
                bool folded = !DrawerCollapseState.IsCollapsed(prop, collapsedByDefault: false);
                DrawerCollapseState.SetCollapsed(prop, folded);
                Fold(root, folded);
            };
        }

        protected override void Shown(VisualElement root, SerializedProperty property)
        {
            bool fields = HasFields(property);
            root.Q(name: "CollapseBtn")?.EnableInClassList(InvisibleClass, !fields);
            var summary = root.Q<Label>("Summary");
            if (summary != null) summary.text = EnableSummary && fields ? SummaryOf(property) : "";
            Fold(root, EnableCollapse && fields && DrawerCollapseState.IsCollapsed(property, collapsedByDefault: false));
        }

        // A new kind opens, so its fields show
        protected override void OnTypeChanged(SerializedProperty prop, System.Type newType)
        {
            base.OnTypeChanged(prop, newType);
            DrawerCollapseState.SetCollapsed(prop, false);
        }

        private static void Fold(VisualElement root, bool folded)
        {
            root.EnableInClassList(CollapsedClass, folded);
            if (root.Q(name: "CollapseBtn") is ForgeIconButton arrow)
            {
                arrow.Glyph.Shape = folded ? ForgeGlyphShape.ArrowRight : ForgeGlyphShape.ArrowDown;
                arrow.tooltip = folded ? "Expand" : "Collapse";
            }
        }

        /// <summary>A folded field's line: each of its kind's values that says something, joined by " · ".</summary>
        protected string SummaryOf(SerializedProperty prop)
        {
            var parts = new List<string>();
            var child = prop.Copy();
            var end = prop.GetEndProperty();
            if (child.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(child, end)) break;
                    var part = GetSummaryText(child);
                    if (!string.IsNullOrEmpty(part)) parts.Add(part);
                } while (child.NextVisible(false));
            }
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// One value as a folded field's line shows it: a number or a choice after its name, as "Duration 0.5"; a name alone
        /// for a box that's on, and nothing for one that's off; an asset's name; or nothing.
        /// </summary>
        protected virtual string GetSummaryText(SerializedProperty prop)
        {
            var label = ObjectNames.NicifyVariableName(prop.name);
            return prop.propertyType switch
            {
                SerializedPropertyType.Integer => $"{label} {prop.intValue.ToString(CultureInfo.CurrentCulture)}",
                SerializedPropertyType.Float => $"{label} {prop.floatValue.ToString("0.##", CultureInfo.CurrentCulture)}",
                SerializedPropertyType.Boolean => prop.boolValue ? label : null,
                SerializedPropertyType.String when !string.IsNullOrEmpty(prop.stringValue) => $"{label} {prop.stringValue}",
                SerializedPropertyType.Enum when prop.enumValueIndex >= 0 && prop.enumValueIndex < prop.enumDisplayNames.Length =>
                    $"{label} {prop.enumDisplayNames[prop.enumValueIndex]}",
                SerializedPropertyType.ObjectReference when prop.objectReferenceValue != null => $"{label} {prop.objectReferenceValue.name}",
                _ => null,
            };
        }
    }
}
