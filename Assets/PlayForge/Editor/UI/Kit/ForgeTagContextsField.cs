using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A tag's contexts, as a choice: the contexts in its box, as "Granted, Universal", or None, which opens a menu of every
    /// context with those chosen checked; choosing one adds it, or takes it away (ED-83). It looks as Unity's popup does,
    /// as a choice does in the style (Docs/EDITOR_STYLE.md), and says each change through <see cref="Changed"/>.
    /// </summary>
    internal sealed class ForgeTagContextsField : BaseField<string>
    {
        private readonly List<string> chosen = new();
        private readonly TextElement shown;

        /// <summary>Raised with the contexts chosen, each time one is added or taken away.</summary>
        public event Action<IReadOnlyList<string>> Changed;

        /// <summary>The contexts chosen, in the menu's order.</summary>
        public IReadOnlyList<string> Contexts => chosen;

        public ForgeTagContextsField(string label, IEnumerable<string> contexts) : base(label, new VisualElement())
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

            chosen.AddRange((contexts ?? Enumerable.Empty<string>()).Where(context => !string.IsNullOrWhiteSpace(context)).Distinct());
            Show();
            input.RegisterCallback<PointerDownEvent>(down =>
            {
                if (down.button != 0) return;
                Open();
                down.StopPropagation();
            });
            RegisterCallback<NavigationSubmitEvent>(_ => Open());
        }

        /// <summary>Adds a context the field hasn't chosen, or takes away one it has, as its menu does.</summary>
        internal void Toggle(string context)
        {
            if (string.IsNullOrWhiteSpace(context)) return;
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

        private void Show()
        {
            var text = chosen.Count == 0 ? "None" : string.Join(", ", chosen);
            SetValueWithoutNotify(text);
            shown.text = text;
            shown.tooltip = text;
        }

        // Every context, those chosen checked
        private void Open()
        {
            if (!enabledInHierarchy) return;
            var menu = new ForgeFieldMenu();
            foreach (var context in ForgeTagContexts.All().Concat(chosen).Distinct())
            {
                var each = context;
                menu.AddItem(each, chosen.Contains(each), () => Toggle(each));
            }
            menu.DropDown(this.Q(className: inputUssClassName));
        }
    }
}
