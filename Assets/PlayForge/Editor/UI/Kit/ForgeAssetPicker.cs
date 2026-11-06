using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A pick of assets (ED-61): Unity's searchable dropdown, as Add Component's, of the assets offered, by their names.
    /// An asset's template is picked this way, from the asset menu's Set Template... It was the Asset Browser, a window of
    /// its own with an accent-colored header, type badges and an Assign button on every row.
    /// </summary>
    internal sealed class ForgeAssetPicker : AdvancedDropdown
    {
        private readonly IReadOnlyList<(Object Asset, string Name)> assets;
        private readonly string title;
        private readonly string none;
        private readonly Action<Object> picked;

        /// <param name="assets">The assets to offer, in the order to list them, each with the name it shows.</param>
        /// <param name="title">The dropdown's heading, as "Template".</param>
        /// <param name="none">What the dropdown says when it offers nothing.</param>
        /// <param name="picked">What a pick does.</param>
        public ForgeAssetPicker(IReadOnlyList<(Object Asset, string Name)> assets, string title, string none, Action<Object> picked)
            : base(new AdvancedDropdownState())
        {
            this.assets = assets ?? Array.Empty<(Object, string)>();
            this.title = title;
            this.none = none;
            this.picked = picked;
            minimumSize = new Vector2(240f, 280f);
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem(title);
            for (int i = 0; i < assets.Count; i++) root.AddChild(new AdvancedDropdownItem(assets[i].Name) { id = i });
            if (assets.Count == 0) root.AddChild(new AdvancedDropdownItem(none) { enabled = false });
            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item.id >= 0 && item.id < assets.Count) picked?.Invoke(assets[item.id].Asset);
        }
    }
}
