using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The header an asset inspector opens with: the 32 px type icon, the asset's name, and its type and asset tag,
    /// then its actions. It scrolls with the content (Q17) and takes its look from the style sheet alone
    /// (Decision 20). It took the type's color as a thick border and badge, and set its own dark colors (ED-20).
    /// </summary>
    internal class ForgeAssetHeader : VisualElement
    {
        public const string HeaderClass = "pf-asset-header";

        public ForgeAssetHeader(Type assetType)
        {
            name = "AssetHeader";
            AddToClassList(HeaderClass);

            Icon = new ForgeTypeIcon(assetType);
            Icon.AddToClassList("pf-asset-header__icon");
            Add(Icon);

            var text = new VisualElement();
            text.AddToClassList("pf-asset-header__text");
            NameLabel = new Label { name = "HeaderName" };
            NameLabel.AddToClassList("pf-asset-header__name");
            text.Add(NameLabel);
            Subtitle = new Label { name = "HeaderSubtitle" };
            Subtitle.AddToClassList("pf-asset-header__subtitle");
            text.Add(Subtitle);
            Add(text);
        }

        public ForgeTypeIcon Icon { get; }
        public Label NameLabel { get; }

        /// <summary>The line under the name: the asset's type and asset tag, as in "Gameplay Effect · Effect.Burning".</summary>
        public Label Subtitle { get; }

        /// <summary>The asset's problem count, which shows while the validator finds any (Q8, ED-15).</summary>
        public Button ProblemCount { get; private set; }

        /// <summary>Adds the problem count, which opens the Problems view on the asset, hidden until it has problems.</summary>
        public Button AddProblemCount(Action onClick)
        {
            ProblemCount = new Button(onClick) { name = "ProblemCount" };
            ProblemCount.AddToClassList("pf-asset-header__problems");
            // Hidden by its class, as the header takes its whole look from the style sheet (ED-20)
            ProblemCount.AddToClassList("pf-asset-header__problems--none");
            Add(ProblemCount);
            return ProblemCount;
        }

        /// <summary>Shows how many problems the asset has, as "2 problems", each one's message in the tooltip.</summary>
        public void ShowProblems(IReadOnlyList<ForgeProblem> problems)
        {
            if (ProblemCount == null) return;
            int count = problems?.Count ?? 0;
            ProblemCount.EnableInClassList("pf-asset-header__problems--none", count == 0);
            ProblemCount.text = count == 1 ? "1 problem" : $"{count} problems";
            ProblemCount.EnableInClassList("pf-asset-header__problems--error",
                count > 0 && problems.Any(problem => problem.Severity == ForgeProblemSeverity.Error));
            ProblemCount.tooltip = count > 0 ? string.Join("\n", problems.Select(problem => problem.Message)) + "\n\nOpen them in the Problems view" : "";
        }

        /// <summary>Adds an action as a plain button with its name, as Open in The Forge is.</summary>
        public Button AddButton(string text, string tooltip, Action onClick)
        {
            var button = new Button(onClick) { text = text, tooltip = tooltip };
            button.AddToClassList("pf-asset-header__button");
            Add(button);
            return button;
        }

        /// <summary>
        /// Adds a line of secondary text under one of its buttons, as the asset's file name under Show in Project in The
        /// Forge, whose list shows the asset's name alone (ED-58). The line hides while it has no text.
        /// </summary>
        public Label AddLineUnder(Button button, string lineName)
        {
            var stack = new VisualElement();
            stack.AddToClassList("pf-asset-header__stack");
            Insert(IndexOf(button), stack);
            stack.Add(button);
            var line = new Label { name = lineName };
            line.AddToClassList(UnderClass);
            line.AddToClassList(UnderEmptyClass);
            stack.Add(line);
            return line;
        }

        public const string UnderClass = "pf-asset-header__under";
        public const string UnderEmptyClass = "pf-asset-header__under--empty";

        /// <summary>Shows a line added under a button, or hides it when the text is empty.</summary>
        public static void ShowLine(Label line, string text, string tooltip = "")
        {
            if (line == null) return;
            line.text = text ?? "";
            line.tooltip = tooltip ?? "";
            line.EnableInClassList(UnderEmptyClass, string.IsNullOrEmpty(text));
        }

        /// <summary>Adds an action as an icon button, as Help and the asset menu are.</summary>
        public Button AddIconButton(ForgeGlyphShape glyph, string tooltip, Action onClick)
        {
            var button = new ForgeIconButton(glyph, tooltip, onClick);
            Add(button);
            return button;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Staying at the top
        // ═══════════════════════════════════════════════════════════════════════════

        private VisualElement space;
        private ScrollView view;

        /// <summary>
        /// Keeps the header at the top of the scroll view it's in while the content scrolls under it, as far down as its
        /// own inspector reaches (Decision 29). The header comes after the content, so it draws over it, and
        /// <paramref name="space"/> holds its place at the top. It follows the scroll view's events and its own layout,
        /// so nothing polls (ED-11). It scrolled away with the content (ED-36).
        /// </summary>
        public void StickTo(VisualElement space)
        {
            this.space = space;
            AddToClassList("pf-asset-header--sticky");
            RegisterCallback<AttachToPanelEvent>(_ => Follow(GetFirstAncestorOfType<ScrollView>()));
            RegisterCallback<DetachFromPanelEvent>(_ => Follow(null));
            RegisterCallback<GeometryChangedEvent>(_ => Place());
            space.RegisterCallback<GeometryChangedEvent>(_ => Place());
            if (panel != null) Follow(GetFirstAncestorOfType<ScrollView>());
        }

        private void Follow(ScrollView scrollView)
        {
            if (view != null)
            {
                view.verticalScroller.valueChanged -= OnScrolled;
                view.contentViewport.UnregisterCallback<GeometryChangedEvent>(OnViewportChanged);
            }
            view = scrollView;
            if (view != null)
            {
                view.verticalScroller.valueChanged += OnScrolled;
                view.contentViewport.RegisterCallback<GeometryChangedEvent>(OnViewportChanged);
            }
            Place();
        }

        private void OnScrolled(float _) => Place();

        private void OnViewportChanged(GeometryChangedEvent _) => Place();

        // Puts the header where the view's top is, but no higher than the inspector's top and no lower than its end
        private void Place()
        {
            if (space == null) return;
            var height = layout.height;
            if (float.IsNaN(height)) return;
            if (!Mathf.Approximately(space.layout.height, height)) space.style.height = height;

            var inspector = hierarchy.parent;
            if (view == null || inspector == null)
            {
                style.top = 0f;
                return;
            }
            var inspectorTop = inspector.ChangeCoordinatesTo(view.contentContainer, Vector2.zero).y;
            var scrolledPast = view.scrollOffset.y - inspectorTop;
            style.top = Mathf.Clamp(scrolledPast, 0f, Mathf.Max(0f, inspector.layout.height - height));
        }
    }
}
