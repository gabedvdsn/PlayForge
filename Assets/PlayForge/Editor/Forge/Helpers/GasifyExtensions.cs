using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public static class GasifyExtensions
    {
        private const float defaultHoverMultiplier = .8f;
        private const int defaultIconSize = 16;
        
        public static VisualElement AttachHoverCallbacks(this VisualElement ve, Color defColor, float multiplier = defaultHoverMultiplier)
        {
            ve.RegisterCallback<PointerEnterEvent>(_ => ve.style.backgroundColor = defColor * multiplier);
            ve.RegisterCallback<PointerLeaveEvent>(_ => ve.style.backgroundColor = defColor);
            return ve;
        }

        public static VisualElement InsertIcon(this VisualElement ve, Texture2D icon, int id = 0, int size = defaultIconSize)
        {
            var _icon = new Image
            {
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                style =
                {
                    width = size,
                    height = size,
                    marginRight = 0, // remove margin so it’s dead-center
                    marginLeft = 0,
                    alignSelf = Align.Center,
                    justifyContent = Justify.Center
                }
            };
            
            ve.Insert(id, _icon);
            return ve;
        }

        public static VisualElement AttachTooltip(this VisualElement ve, string tooltip)
        {
            if (tooltip == null) return ve;

            ve.tooltip = tooltip;
            return ve;
        }
    }
}
