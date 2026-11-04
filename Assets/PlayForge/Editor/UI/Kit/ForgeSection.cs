using System;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A section of an inspector, as Unity's foldout headers draw one: the arrow, the bold title, a one-line summary
    /// while it's collapsed, and the section's actions, which always show (Q18). The content under it indents past the
    /// arrow. It takes its look from the style sheet alone (Decision 20). Its actions appeared only while the pointer
    /// was over the header, and its colors held in the Dark theme only (ED-20).
    /// </summary>
    internal class ForgeSection : VisualElement
    {
        public const string SectionClass = "pf-section";
        public const string CollapsedClass = "pf-section--collapsed";

        private bool expanded;

        public ForgeSection(string name, string title, bool expanded)
        {
            this.name = $"{name}_CONTAINER";
            AddToClassList(SectionClass);

            Header = new VisualElement { name = name };
            Header.AddToClassList("pf-section__header");
            Arrow = new ForgeIconButton(ForgeGlyphShape.ArrowDown, null, Toggle);
            Arrow.AddToClassList("pf-section__arrow");
            Header.Add(Arrow);
            Title = new Label(title);
            Title.AddToClassList("pf-section__title");
            Header.Add(Title);
            Summary = new Label();
            Summary.AddToClassList("pf-section__summary");
            Header.Add(Summary);
            Add(Header);

            Content = new VisualElement { name = name };
            Content.AddToClassList("pf-section__content");
            Add(Content);

            // A click on the header, but not on one of its buttons, opens or closes the section, as a foldout's does
            Header.RegisterCallback<ClickEvent>(click =>
            {
                if (click.target is Button) return;
                Toggle();
                click.StopPropagation();
            });

            SetExpanded(expanded, false);
        }

        public VisualElement Header { get; }
        public ForgeIconButton Arrow { get; }
        public Label Title { get; }

        /// <summary>The one line the header shows while the section is collapsed, such as "Levels 1–5".</summary>
        public Label Summary { get; }

        public VisualElement Content { get; }

        /// <summary>Raised when the user opens or closes the section, with whether it's now open.</summary>
        public event Action<bool> Toggled;

        public bool Expanded
        {
            get => expanded;
            set => SetExpanded(value, false);
        }

        /// <summary>Adds an action to the header, after the summary: an icon button that always shows.</summary>
        public Button AddAction(string name, ForgeGlyphShape glyph, string tooltip, Action onClick)
        {
            var action = new ForgeIconButton(glyph, tooltip, onClick) { name = name };
            action.AddToClassList("pf-section__action");
            Header.Add(action);
            return action;
        }

        private void Toggle() => SetExpanded(!expanded, true);

        private void SetExpanded(bool open, bool byUser)
        {
            expanded = open;
            EnableInClassList(CollapsedClass, !open);
            Arrow.Glyph.Shape = open ? ForgeGlyphShape.ArrowDown : ForgeGlyphShape.ArrowRight;
            if (byUser) Toggled?.Invoke(open);
        }
    }
}
