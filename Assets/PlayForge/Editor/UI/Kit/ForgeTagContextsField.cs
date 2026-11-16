using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A tag's contexts, as a choice: the contexts in its box, as "Granted, Universal", or None, which opens a menu of every
    /// context with those chosen checked; choosing one adds it, or takes it away (ED-83). Contexts the tag's code declares,
    /// by <c>[ForgeTagExposed]</c>, show among them, checked in the menu and locked, as they're written in code (EP-7). It
    /// looks as Unity's popup does, as a choice does in the style (Docs/EDITOR_STYLE.md), and says each change through
    /// <see cref="Changed"/>.
    /// </summary>
    internal sealed class ForgeTagContextsField : BaseField<string>
    {
        private readonly List<string> chosen = new();
        private readonly List<string> locked = new();
        private readonly TextElement shown;

        /// <summary>Raised with the contexts chosen, each time one is added or taken away.</summary>
        public event Action<IReadOnlyList<string>> Changed;

        /// <summary>The contexts chosen, in the menu's order; those the tag's code declares aren't among them.</summary>
        public IReadOnlyList<string> Contexts => chosen;

        /// <summary>The contexts the tag's code declares, which the field shows checked and locked (EP-7).</summary>
        public IReadOnlyList<string> Locked => locked;

        /// <param name="label">The field's label.</param>
        /// <param name="contexts">The contexts chosen, which the menu changes.</param>
        /// <param name="inCode">The contexts the tag's code declares, shown checked and locked; none by default.</param>
        public ForgeTagContextsField(string label, IEnumerable<string> contexts, IEnumerable<string> inCode = null) : base(label, new VisualElement())
        {
            AddToClassList(BasePopupField<string, string>.ussClassName);
            AddToClassList(PopupField<string>.ussClassName);
            AddToClassList(alignedFieldUssClassName);
            labelElement.AddToClassList(BasePopupField<string, string>.labelUssClassName);
            var input = this.Q(className: inputUssClassName);
            input.AddToClassList(BasePopupField<string, string>.inputUssClassName);
            shown = new TextElement { name = "ContextsText" };
            shown.AddToClassList(BasePopupField<string, string>.textUssClassName);
            input.Add(shown);
            var arrow = new VisualElement();
            arrow.AddToClassList(BasePopupField<string, string>.arrowUssClassName);
            input.Add(arrow);

            locked.AddRange((inCode ?? Enumerable.Empty<string>()).Where(context => !string.IsNullOrWhiteSpace(context)).Distinct());
            chosen.AddRange((contexts ?? Enumerable.Empty<string>()).Where(context => !string.IsNullOrWhiteSpace(context) && !locked.Contains(context)).Distinct());
            Show();
            input.RegisterCallback<PointerDownEvent>(down =>
            {
                if (down.button != 0) return;
                Open();
                down.StopPropagation();
            });
            RegisterCallback<NavigationSubmitEvent>(_ => Open());
        }

        /// <summary>
        /// Adds a context the field hasn't chosen, or takes away one it has, as its menu does; one the tag's code declares
        /// stays (EP-7).
        /// </summary>
        internal void Toggle(string context)
        {
            if (string.IsNullOrWhiteSpace(context) || locked.Contains(context)) return;
            if (!chosen.Remove(context)) chosen.Add(context);
            var order = ForgeTagContexts.All();
            chosen.Sort((a, b) => Rank(order, a).CompareTo(Rank(order, b)));
            Show();
            Changed?.Invoke(chosen.ToList());
        }

        private static int Rank(List<string> order, string context)
        {
            int at = order.IndexOf(context);
            return at < 0 ? int.MaxValue : at;
        }

        // Every context it's for, the code's among them, in the menu's order
        private void Show()
        {
            var order = ForgeTagContexts.All();
            var all = locked.Concat(chosen).Distinct().OrderBy(context => Rank(order, context)).ToList();
            var text = all.Count == 0 ? "None" : string.Join(", ", all);
            SetValueWithoutNotify(text);
            shown.text = text;
            shown.tooltip = locked.Count == 0 ? text : $"{text}. In code, by [ForgeTagExposed]: {string.Join(", ", locked)}";
        }

        private void Open()
        {
            if (!enabledInHierarchy) return;
            Menu().DropDown(this.Q(className: inputUssClassName));
        }

        /// <summary>
        /// The field's menu: every context, those chosen checked, and those the tag's code declares checked and locked,
        /// saying so (EP-7); then New Context….
        /// </summary>
        internal ForgeFieldMenu Menu()
        {
            var menu = new ForgeFieldMenu(string.IsNullOrEmpty(label) ? "Contexts" : label);
            foreach (var context in ForgeTagContexts.All().Concat(locked).Concat(chosen).Distinct())
            {
                var each = context;
                if (locked.Contains(each))
                    menu.AddDisabledItem(each, true, "Declared in code, by [ForgeTagExposed] where the tag is written, so it stays");
                else menu.AddItem(each, chosen.Contains(each), () => Toggle(each));
            }
            // A context of the project's own (FN-9, Decision 70)
            menu.AddSeparator("");
            menu.AddItem("New Context…", false, NewContext);
            return menu;
        }

        /// <summary>Asks for a new context's name, makes it, and chooses it here (FN-9).</summary>
        internal void NewContext() =>
            ForgeNameWindow.Show("New Tag Context", "Name", "", "Make", ForgeTagContexts.WhyNotContext, name =>
            {
                ForgeTagContexts.MakeContext(name);
                if (!chosen.Contains(name)) Toggle(name);
            });
    }
}
