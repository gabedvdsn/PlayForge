using System;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The asset tag an asset's name gives, as the Identity section shows it: Unity's text field, read-only, with its
    /// label in the Inspector's one label column and the tag in secondary text, so it can be selected and copied. When the
    /// stored tag differs from the one the name gives, the tag shows in warning text and Update stores the new one. It
    /// takes its look from the style sheet (Decision 20). The tag sat in a tinted, bordered box, in bold italics, beside
    /// "(auto)" in 9 px text (ED-37).
    /// </summary>
    internal sealed class ForgeAssetTagField : TextField
    {
        public const string ValueFieldClass = "pf-value-field";
        public const string WarningClass = "pf-value-field--warning";

        public ForgeAssetTagField() : base("Asset Tag")
        {
            name = "AssetTag";
            isReadOnly = true;
            AddToClassList(alignedFieldUssClassName);
            AddToClassList(ValueFieldClass);

            UpdateButton = new Button { name = "AssetTagUpdate", text = "Update", tooltip = "Store the tag this name gives" };
            UpdateButton.AddToClassList("pf-value-field__button");
            Add(UpdateButton);
            Show("", null, null);
        }

        /// <summary>Stores the tag the name gives. It shows only while the stored tag differs.</summary>
        public Button UpdateButton { get; }

        /// <summary>Shows the tag the name gives. A stored tag that differs, or null, and what Update does then.</summary>
        public void Show(string tag, string staleStored, Action update)
        {
            SetValueWithoutNotify(tag);
            var stale = staleStored != null;
            EnableInClassList(WarningClass, stale);
            tooltip = stale ? $"Stored as \"{staleStored}\". Update stores the tag this name gives." : null;
            UpdateButton.clickable = update == null ? null : new Clickable(update);
            UpdateButton.style.display = stale ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
