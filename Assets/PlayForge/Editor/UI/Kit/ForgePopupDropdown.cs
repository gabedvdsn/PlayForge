using System;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Has a kind popup open PlayForge's dropdown in place of Unity's menu (EP-1), as a number field's Kind does: its
    /// choices, each in the submenus its menu path names, as "Scaler/Clamped" files Clamped under Scaler, the value checked,
    /// and each one's context where it has one. The popup stays Unity's, its value and its choices, so what binds it and what
    /// reads it are as they were; only its menu changes. That menu was the operating system's, which ran the screen's height.
    /// </summary>
    internal static class ForgePopupDropdown
    {
        /// <param name="popup">The popup, whose <c>formatListItemCallback</c> gives each choice's menu path.</param>
        /// <param name="title">The dropdown's heading, as "Kind".</param>
        /// <param name="contextOf">What the dropdown says of a choice, or null for nothing.</param>
        public static void Use(PopupField<string> popup, string title, Func<string, string> contextOf = null)
        {
            if (popup == null) return;
            var input = popup.Q(className: BasePopupField<string, string>.inputUssClassName);

            void Open()
            {
                if (!popup.enabledInHierarchy || popup.panel == null) return;
                var dropdown = new ForgeDropdown(title) { MenusFirst = false };
                foreach (var choice in popup.choices)
                {
                    var each = choice;
                    var full = popup.formatListItemCallback?.Invoke(choice) ?? choice;
                    int slash = full.LastIndexOf('/');
                    var path = slash < 0 ? "" : full.Substring(0, slash);
                    var name = slash < 0 ? full : full.Substring(slash + 1);
                    dropdown.Add(path, name, () => popup.value = each, contextOf?.Invoke(choice),
                        isChecked: !popup.showMixedValue && popup.value == choice);
                }
                dropdown.Show(input ?? popup);
            }

            // Before the popup's own, which would open Unity's menu
            void Pressed(EventBase down, int button)
            {
                if (button != 0 || down.target is not VisualElement target || input == null || (target != input && !input.Contains(target))) return;
                down.StopImmediatePropagation();
                Open();
            }
            popup.RegisterCallback<PointerDownEvent>(down => Pressed(down, down.button), TrickleDown.TrickleDown);
            popup.RegisterCallback<MouseDownEvent>(down => Pressed(down, down.button), TrickleDown.TrickleDown);
            popup.RegisterCallback<NavigationSubmitEvent>(submit =>
            {
                submit.StopImmediatePropagation();
                Open();
            }, TrickleDown.TrickleDown);
        }
    }
}
