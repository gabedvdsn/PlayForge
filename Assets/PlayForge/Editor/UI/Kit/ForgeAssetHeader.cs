using System;
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
    }
}
