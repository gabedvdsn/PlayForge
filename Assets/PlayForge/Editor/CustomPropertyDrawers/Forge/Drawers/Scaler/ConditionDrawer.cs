using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A condition field, as a Conditional scaler's When (FEAT-29, Decision 43), drawn as a scaler field is: its name and
    /// its kind, picked from the conditions there are, a project's own among them, its one line, as "Target's Health &lt;
    /// 0.3 × Target's Health (base)", then the condition's own fields on a block under it, each as Unity draws its type,
    /// so its numbers are number fields. It writes nothing while it draws.
    /// </summary>
    [CustomPropertyDrawer(typeof(GameplayCondition), true)]
    public class ConditionDrawer : PropertyDrawer
    {
        private const string NoKind = "None";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "ConditionRoot" };
            // The style sheet lays out the condition as it does a scaler, wherever it's drawn (ED-20)
            ForgeTheme.Apply(root);
            root.AddToClassList("pf-scaler-field");
            var label = preferredLabel ?? property.displayName;
            var kinds = ConditionKinds.All;

            Type shownType = null;
            long shownId = 0;
            bool mixedKinds = false;
            bool built = false;
            int sharedWith = -1;
            Label line = null;

            void Draw()
            {
                if (!ScalerFields.Alive(property)) return;
                root.Clear();
                line = null;
                // Selected conditions whose kinds differ show a mixed kind, and neither's fields as both's (ED-12)
                mixedKinds = EachAsset.Differ(property, ScalerFields.KindOf);
                shownType = ScalerFields.KindOf(property);
                shownId = property.managedReferenceId;
                built = true;

                var choices = new List<string> { NoKind };
                choices.AddRange(kinds.Select(NumberText.ConditionName));
                var shownName = shownType == null ? NoKind : NumberText.ConditionName(shownType);
                if (!choices.Contains(shownName)) choices.Add(shownName);

                var row = new VisualElement { name = "Row" };
                row.AddToClassList("pf-magnitude__row");
                var kind = new PopupField<string>(label, choices, shownName) { name = "Kind", tooltip = "The condition's kind, or none" };
                kind.AddToClassList(BaseField<string>.alignedFieldUssClassName);
                kind.AddToClassList("pf-magnitude__kind-labelled");
                // The menu files a condition under its category, as the kind menu does: "Logic/All Of"
                kind.formatListItemCallback = ConditionKinds.MenuPath;
                kind.showMixedValue = mixedKinds;
                row.Add(kind);
                root.Add(row);

                // A condition another list element shares, after Unity's array "+" copied its reference id, says so, as a
                // scaler does, with a button that gives this element its own copy
                sharedWith = ScalerDrawer.SharedWith(property);
                if (sharedWith >= 0)
                {
                    var note = new HelpBox($"Element {sharedWith} holds the same condition, so an edit here changes it too.",
                        HelpBoxMessageType.Warning) { name = "SharedSibling" };
                    note.Add(new Button(() => ScalerDrawer.GiveOwnCopy(property)) { name = "MakeSeparateCopy", text = "Make a Separate Copy" });
                    root.Add(note);
                }

                if (mixedKinds)
                {
                    var note = new Label("The selected conditions' kinds differ. Choose one to give it to each.") { name = "MixedKinds" };
                    note.AddToClassList("pf-note");
                    root.Add(note);
                }
                else if (shownType != null)
                {
                    line = new Label { name = "Summary" };
                    line.AddToClassList("pf-magnitude__summary");
                    root.Add(line);

                    // The condition's fields sit on one block under its kind, as a scaler's do (ED-51, Decision 36)
                    var content = new VisualElement { name = "ConditionContent" };
                    content.AddToClassList("pf-scaler");
                    content.AddToClassList("pf-scaler-field__content");
                    content.AddToClassList(ScalerFields.BlockClass);
                    foreach (var field in ScalerFields.KindFields(property)) content.Add(field);
                    if (content.childCount > 0) root.Add(content);
                }

                kind.RegisterValueChangedCallback(change =>
                {
                    // A mixed kind takes the one chosen, even the first condition's
                    if (!mixedKinds && change.newValue == change.previousValue) return;
                    var type = change.newValue == NoKind ? null : kinds.FirstOrDefault(each => NumberText.ConditionName(each) == change.newValue);
                    if (change.newValue != NoKind && type == null) return;
                    EachAsset.Write(property, own => own.managedReferenceValue = type == null ? null : Activator.CreateInstance(type));
                    Draw();
                });
                Show();
            }

            void Show()
            {
                if (line == null) return;
                bool differ = EachAsset.Differ(property, Line);
                line.text = differ ? MagnitudeField.MixedRange : Line(property);
                line.tooltip = differ ? "The selected conditions read different things" : line.text;
            }

            root.TrackSerializedObjectValue(property.serializedObject, _ =>
            {
                if (!ScalerFields.Alive(property)) return;
                bool mixed = EachAsset.Differ(property, ScalerFields.KindOf);
                // A new kind, or a copy of its own in place of a shared one, draws it again; and the element it shared with,
                // which no longer shares it (FN-2)
                if (!built || mixed != mixedKinds || ScalerFields.KindOf(property) != shownType || property.managedReferenceId != shownId
                    || ScalerDrawer.SharedWith(property) != sharedWith) Draw();
                else Show();
            });
            Draw();
            return root;
        }

        private static string Line(SerializedProperty property) => NumberText.Of(property.managedReferenceValue as GameplayCondition);
    }

    /// <summary>
    /// The conditions the condition menu offers (FEAT-29): every condition class that can be made and saved, a project's
    /// own among them. The framework's come first, then a project's by name, then each category's, as Logic's All Of,
    /// Any Of and Not.
    /// </summary>
    internal static class ConditionKinds
    {
        private static List<Type> all;

        public static List<Type> All => all ??= TypeCache.GetTypesDerivedFrom<GameplayCondition>()
            .Where(type => !type.IsAbstract && !type.IsGenericType && type.IsSerializable && type.GetConstructor(Type.EmptyTypes) != null)
            .OrderBy(type => string.IsNullOrEmpty(GameplayConditionAttribute.Of(type)?.Category) ? 0 : 1)
            .ThenBy(type => GameplayConditionAttribute.Of(type)?.Category ?? "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(Rank)
            .ThenBy(NumberText.ConditionName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        private static readonly Type[] Framework =
        {
            typeof(TagCondition), typeof(AttributeCondition), typeof(LevelCondition), typeof(CompareCondition),
            typeof(AlwaysCondition), typeof(NeverCondition), typeof(AllCondition), typeof(AnyCondition), typeof(NotCondition),
        };

        private static int Rank(Type type)
        {
            int index = Array.IndexOf(Framework, type);
            return index < 0 ? Framework.Length : index;
        }

        /// <summary>A condition's place in the menu by its name, as "Logic/All Of", or its name alone at the top.</summary>
        public static string MenuPath(string name)
        {
            var type = All.FirstOrDefault(each => NumberText.ConditionName(each) == name);
            var category = type == null ? null : GameplayConditionAttribute.Of(type)?.Category;
            return string.IsNullOrEmpty(category) ? name : $"{category}/{name}";
        }
    }
}
