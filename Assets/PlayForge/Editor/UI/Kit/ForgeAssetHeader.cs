using System;
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

        /// <summary>Adds an action as a plain button with its name, as Open in The Forge is.</summary>
        public Button AddButton(string text, string tooltip, Action onClick)
        {
            var button = new Button(onClick) { text = text, tooltip = tooltip };
            button.AddToClassList("pf-asset-header__button");
            Add(button);
            return button;
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
