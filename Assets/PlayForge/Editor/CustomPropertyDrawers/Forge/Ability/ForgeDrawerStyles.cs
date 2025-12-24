using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Shared styling utilities for PlayForge property drawers.
    /// Provides consistent theming across all custom drawers.
    /// </summary>
    public static class ForgeDrawerStyles
    {
        // ═══════════════════════════════════════════════════════════════════════
        // Color Palette
        // ═══════════════════════════════════════════════════════════════════════
        
        public static class Colors
        {
            // Accent Colors (for left borders)
            public static readonly Color AccentBlue = new Color(0.5f, 0.7f, 0.9f, 0.6f);
            public static readonly Color AccentGreen = new Color(0.5f, 0.7f, 0.5f, 0.6f);
            public static readonly Color AccentRed = new Color(0.9f, 0.5f, 0.5f, 0.6f);
            public static readonly Color AccentOrange = new Color(0.9f, 0.6f, 0.4f, 0.6f);
            public static readonly Color AccentPurple = new Color(0.6f, 0.5f, 0.7f, 0.6f);
            public static readonly Color AccentCyan = new Color(0.5f, 0.7f, 0.8f, 0.6f);
            public static readonly Color AccentYellow = new Color(0.8f, 0.75f, 0.4f, 0.6f);
            public static readonly Color AccentGray = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            
            // Section Background Tints
            public static readonly Color SectionBlue = new Color(0.4f, 0.55f, 0.7f, 0.3f);
            public static readonly Color SectionGreen = new Color(0.5f, 0.7f, 0.5f, 0.3f);
            public static readonly Color SectionRed = new Color(0.7f, 0.5f, 0.5f, 0.3f);
            public static readonly Color SectionOrange = new Color(0.7f, 0.55f, 0.4f, 0.3f);
            public static readonly Color SectionPurple = new Color(0.6f, 0.5f, 0.7f, 0.3f);
            public static readonly Color SectionCyan = new Color(0.5f, 0.65f, 0.7f, 0.3f);
            public static readonly Color SectionYellow = new Color(0.7f, 0.65f, 0.4f, 0.3f);
            public static readonly Color SectionGray = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            
            // Background Colors
            public static readonly Color MainBackground = new Color(0.35f, 0.35f, 0.35f, 0.0f);
            //public static readonly Color MainBackground = new Color(0.18f, 0.18f, 0.18f, 0.9f);
            
            public static readonly Color SectionBackground = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            //public static readonly Color SectionBackground = new Color(0.2f, 0.2f, 0.2f, 0.6f);
            
            public static readonly Color ItemBackground = new Color(0.2f, 0.2f, 0.2f, 0.4f);
            //public static readonly Color ItemBackground = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            
            // Text Colors
            public static readonly Color HeaderText = new Color(0.8f, 0.8f, 0.8f);
            public static readonly Color SectionTitle = new Color(0.6f, 0.6f, 0.6f);
            public static readonly Color HintText = new Color(0.5f, 0.5f, 0.5f);
            
            // Policy Colors (for ContainedEffectPacket)
            public static readonly Color PolicyApply = new Color(0.4f, 0.7f, 0.4f);
            public static readonly Color PolicyTick = new Color(0.7f, 0.6f, 0.3f);
            public static readonly Color PolicyRemove = new Color(0.7f, 0.4f, 0.4f);
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // Container Builders
        // ═══════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates a main container box with themed styling.
        /// </summary>
        public static VisualElement CreateMainContainer(Color accentColor)
        {
            var container = new VisualElement();
            container.style.backgroundColor = Colors.MainBackground;
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.borderLeftWidth = 2;
            container.style.borderLeftColor = accentColor;
            container.style.paddingTop = 6;
            container.style.paddingBottom = 6;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 6;
            container.style.marginTop = 4;
            container.style.marginBottom = 4;
            return container;
        }
        
        /// <summary>
        /// Creates a header with icon and title.
        /// </summary>
        public static VisualElement CreateHeader(string icon, string title, Color iconColor)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 6;
            
            var headerIcon = new Label(icon);
            headerIcon.style.fontSize = 14;
            headerIcon.style.marginRight = 6;
            headerIcon.style.color = iconColor;
            header.Add(headerIcon);
            
            var headerLabel = new Label(title);
            headerLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLabel.style.fontSize = 12;
            headerLabel.style.color = Colors.HeaderText;
            header.Add(headerLabel);
            
            return header;
        }
        
        /// <summary>
        /// Creates a collapsible section with title and accent color.
        /// </summary>
        public static VisualElement CreateSection(string title, Color accentColor)
        {
            var section = new VisualElement();
            section.style.marginTop = 6;
            section.style.marginBottom = 4;
            section.style.paddingLeft = 8;
            section.style.paddingTop = 4;
            section.style.paddingBottom = 6;
            section.style.paddingRight = 4;
            section.style.backgroundColor = Colors.SectionBackground;
            section.style.borderTopLeftRadius = 3;
            section.style.borderTopRightRadius = 3;
            section.style.borderBottomLeftRadius = 3;
            section.style.borderBottomRightRadius = 3;
            section.style.borderLeftWidth = 2;
            section.style.borderLeftColor = accentColor;
            
            var sectionHeader = new Label(title);
            sectionHeader.style.fontSize = 10;
            sectionHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            sectionHeader.style.color = Colors.SectionTitle;
            sectionHeader.style.marginBottom = 4;
            sectionHeader.style.letterSpacing = 1;
            section.Add(sectionHeader);
            
            return section;
        }
        
        /// <summary>
        /// Creates a horizontal row container.
        /// </summary>
        public static VisualElement CreateRow(int marginBottom = 2)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = marginBottom;
            return row;
        }
        
        /// <summary>
        /// Creates a hint/description label.
        /// </summary>
        public static Label CreateHintLabel(string text)
        {
            var hint = new Label(text);
            hint.style.fontSize = 10;
            hint.style.color = Colors.HintText;
            hint.style.marginBottom = 4;
            return hint;
        }
        
        /// <summary>
        /// Creates a divider line.
        /// </summary>
        public static VisualElement CreateDivider(int marginVertical = 6)
        {
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            divider.style.marginTop = marginVertical;
            divider.style.marginBottom = marginVertical;
            return divider;
        }
        
        /// <summary>
        /// Creates a badge/pill label for counts or status.
        /// </summary>
        public static Label CreateBadge(string text, Color? backgroundColor = null)
        {
            var badge = new Label(text);
            badge.style.fontSize = 10;
            badge.style.color = new Color(0.7f, 0.7f, 0.7f);
            badge.style.backgroundColor = backgroundColor ?? new Color(0.3f, 0.3f, 0.3f, 0.5f);
            badge.style.paddingLeft = 6;
            badge.style.paddingRight = 6;
            badge.style.paddingTop = 2;
            badge.style.paddingBottom = 2;
            badge.style.borderTopLeftRadius = 8;
            badge.style.borderTopRightRadius = 8;
            badge.style.borderBottomLeftRadius = 8;
            badge.style.borderBottomRightRadius = 8;
            return badge;
        }
        
        /// <summary>
        /// Creates a color indicator strip (for visual categorization).
        /// </summary>
        public static VisualElement CreateColorIndicator(Color color, int width = 4, int height = 16)
        {
            var indicator = new VisualElement();
            indicator.style.width = width;
            indicator.style.height = height;
            indicator.style.backgroundColor = color;
            indicator.style.borderTopLeftRadius = 2;
            indicator.style.borderTopRightRadius = 2;
            indicator.style.borderBottomLeftRadius = 2;
            indicator.style.borderBottomRightRadius = 2;
            indicator.style.marginRight = 6;
            return indicator;
        }
        
        // ═══════════════════════════════════════════════════════════════════════
        // Icon Characters
        // ═══════════════════════════════════════════════════════════════════════
        
        public static class Icons
        {
            public const string Duration = "⏱";
            public const string Impact = "⚡";
            public const string Target = "◎";
            public const string Magnitude = "📊";
            public const string Behavior = "⚙";
            public const string Effect = "✦";
            public const string Tag = "🏷";
            public const string Attribute = "📈";
            public const string Policy = "📋";
            public const string Tick = "🔄";
            public const string Stack = "📚";
            public const string Arrow = "→";
            public const string Check = "✓";
            public const string Cross = "✗";
            public const string Warning = "⚠";
            public const string Info = "ℹ";
        }
    }
}