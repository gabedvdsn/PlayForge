using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Comprehensive UI building utilities for PlayForge editors.
    /// Provides all necessary methods to create custom editors programmatically.
    /// </summary>
    public static class ForgeDrawerStyles
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 1: Color Palette
        // ═══════════════════════════════════════════════════════════════════════════
        
        public static class Colors
        {
            // Accent Colors (for left borders and highlights)
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
            public static readonly Color HeaderBackground = new Color(0.157f, 0.157f, 0.157f, 0.95f);
            public static readonly Color SectionBackground = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            public static readonly Color SectionHeaderBackground = new Color(0.196f, 0.196f, 0.196f, 0.5f);
            public static readonly Color SectionHeaderHover = new Color(0.35f, 0.35f, 0.35f, 0.6f);
            public static readonly Color SubsectionBackground = new Color(0.157f, 0.157f, 0.157f, 0.4f);
            public static readonly Color ItemBackground = new Color(0.2f, 0.2f, 0.2f, 0.4f);
            public static readonly Color ButtonBackground = new Color(0.314f, 0.314f, 0.314f, 1f);
            public static readonly Color ButtonHover = new Color(0.4f, 0.4f, 0.4f, 1f);
            public static readonly Color GridBackground = new Color(.4f, .4f, .4f, .8f);
            public static readonly Color GridAltBackground = new Color(.25f, .25f, .25f, .8f);
            
            // Text Colors
            public static readonly Color HeaderText = new Color(0.863f, 0.863f, 0.863f, 1f);
            public static readonly Color SectionTitle = new Color(0.784f, 0.784f, 0.784f, 1f);
            public static readonly Color LabelText = new Color(0.706f, 0.706f, 0.706f, 1f);
            public static readonly Color HintText = new Color(0.5f, 0.5f, 0.5f, 1f);
            public static readonly Color ArrowText = new Color(0.588f, 0.588f, 0.588f, 1f);
            public static readonly Color AssetTagText = new Color(0.549f, 0.706f, 0.549f, 1f);
            
            // Border Colors
            public static readonly Color BorderDark = new Color(0.137f, 0.137f, 0.137f, 1f);
            public static readonly Color BorderLight = new Color(0.392f, 0.392f, 0.392f, 1f);
            public static readonly Color DividerColor = new Color(0.392f, 0.392f, 0.392f, 1f);
            
            // Policy/Status Colors
            public static readonly Color PolicyPurple = new Color(0.514f, 0.357f, 0.604f, 1f);
            public static readonly Color PolicyGreen = new Color(0.4f, 0.7f, 0.4f, 1f);
            public static readonly Color PolicyYellow = new Color(0.7f, 0.6f, 0.3f, 1f);
            public static readonly Color PolicyRed = new Color(0.7f, 0.4f, 0.4f, 1f);
            public static readonly Color PolicyBlue = new Color(0.4f, 0.4f, 0.7f, 1f);
            public static readonly Color PolicyCyan = new Color(0.5f, 0.7f, 0.8f, 1f);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 2: Icon Characters
        // ═══════════════════════════════════════════════════════════════════════════
        
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
            public const string Refresh = "\u27f3";
            public const string Search = "\u2315";
            public const string Help = "?";
            public const string Clear = "X";
            public const string ChevronDown = "▼";
            public const string ChevronRight = "►";
            public const string ArrowDown = "\u2193";
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 3: Root & ScrollView
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates the root container for an inspector.
        /// </summary>
        public static VisualElement CreateRoot()
        {
            return new VisualElement
            {
                name = "Root",
                style = { flexGrow = 1, flexShrink = 1 }
            };
        }
        
        /// <summary>
        /// Creates a configured ScrollView for inspector content.
        /// Fills all available space in the inspector body.
        /// </summary>
        public static ScrollView CreateScrollView(int minHeight = 100)
        {
            var scrollView = new ScrollView
            {
                name = "ContentScrollView",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden,
                verticalScrollerVisibility = ScrollerVisibility.Hidden
            };
            scrollView.style.flexGrow = 1;
            scrollView.style.flexShrink = 1;
            scrollView.style.minHeight = minHeight;
            //scrollView.style.maxHeight = 
            // No maxHeight - let it fill available space
            
            // Ensure content container also grows
            scrollView.contentContainer.style.flexGrow = 1;
            
            return scrollView;
        }
        
        /// <summary>
        /// Creates bottom padding element for scroll content.
        /// </summary>
        public static VisualElement CreateBottomPadding(int height = 20)
        {
            return new VisualElement { style = { height = height, flexShrink = 0 } };
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 4: Main Header
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Configuration for main header creation.
        /// </summary>
        public class HeaderConfig
        {
            public Texture2D Icon;
            public Color IconTint = Color.clear;
            public string DefaultTitle = "Unnamed";
            public string DefaultDescription = "No description provided.";
            public bool ShowRefresh = true;
            public bool ShowLookup = true;
            public bool ShowVisualize = true;
            public bool ShowImport = true;
            public Action OnRefresh;
            public Action OnLookup;
            public Action OnVisualize;
            public Action OnImport;
        }
        
        /// <summary>
        /// Result from CreateMainHeader containing references to update.
        /// </summary>
        public class HeaderResult
        {
            public VisualElement Header;
            public VisualElement IconElement;
            public Label NameLabel;
            public Label DescriptionLabel;
            public Button RefreshButton;
            public Button LookupButton;
            public Button VisualizeButton;
            public Button ImportButton;
        }
        
        /// <summary>
        /// Creates the main sticky header for an inspector.
        /// </summary>
        public static HeaderResult CreateMainHeader(HeaderConfig config)
        {
            var result = new HeaderResult();
            
            var header = new VisualElement { name = "Header" };
            header.style.flexShrink = 0;
            header.style.flexDirection = FlexDirection.Row;
            header.style.minHeight = 64;
            header.style.maxHeight = 64;
            header.style.marginTop = 4;
            header.style.marginBottom = 4;
            header.style.marginLeft = 2;
            header.style.marginRight = 2;
            header.style.paddingTop = 4;
            header.style.paddingBottom = 4;
            header.style.paddingLeft = 4;
            header.style.paddingRight = 4;
            header.style.backgroundColor = Colors.HeaderBackground;
            header.style.borderTopLeftRadius = 4;
            header.style.borderTopRightRadius = 4;
            header.style.borderBottomLeftRadius = 4;
            header.style.borderBottomRightRadius = 4;
            header.style.borderTopWidth = 1;
            header.style.borderBottomWidth = 1;
            header.style.borderLeftWidth = 1;
            header.style.borderRightWidth = 1;
            header.style.borderTopColor = Colors.BorderDark;
            header.style.borderBottomColor = Colors.BorderDark;
            header.style.borderLeftColor = Colors.BorderDark;
            header.style.borderRightColor = Colors.BorderDark;
            result.Header = header;

            // Icon
            var headerIcon = new VisualElement { name = "HeaderIcon" };
            headerIcon.style.flexShrink = 0;
            headerIcon.style.width = 56;
            headerIcon.style.height = 56;
            headerIcon.style.minWidth = 56;
            headerIcon.style.minHeight = 56;
            headerIcon.style.marginRight = 8;
            headerIcon.style.borderTopLeftRadius = 3;
            headerIcon.style.borderTopRightRadius = 3;
            headerIcon.style.borderBottomLeftRadius = 3;
            headerIcon.style.borderBottomRightRadius = 3;
            headerIcon.style.borderTopWidth = 1;
            headerIcon.style.borderBottomWidth = 1;
            headerIcon.style.borderLeftWidth = 1;
            headerIcon.style.borderRightWidth = 1;
            headerIcon.style.borderTopColor = Colors.BorderDark;
            headerIcon.style.borderBottomColor = Colors.BorderDark;
            headerIcon.style.borderLeftColor = Colors.BorderDark;
            headerIcon.style.borderRightColor = Colors.BorderDark;
            headerIcon.style.alignSelf = Align.Center;
            
            if (config.Icon != null)
            {
                headerIcon.style.backgroundImage = config.Icon;
                headerIcon.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            if (config.IconTint != Color.clear)
            {
                headerIcon.style.backgroundColor = config.IconTint;
            }
            result.IconElement = headerIcon;
            header.Add(headerIcon);

            // Details container
            var details = new VisualElement();
            details.style.flexGrow = 1;
            details.style.justifyContent = Justify.Center;

            // Top row (title + buttons)
            var topRow = new VisualElement();
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.alignItems = Align.Center;

            var nameLabel = new Label { name = "Name", text = config.DefaultTitle };
            nameLabel.style.flexGrow = 1;
            nameLabel.style.paddingLeft = 4;
            nameLabel.style.paddingRight = 8;
            nameLabel.style.fontSize = 16;
            nameLabel.style.color = Colors.HeaderText;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            result.NameLabel = nameLabel;
            topRow.Add(nameLabel);

            // Buttons container
            var buttonsContainer = new VisualElement();
            buttonsContainer.style.flexShrink = 0;
            buttonsContainer.style.flexDirection = FlexDirection.Row;

            if (config.ShowImport)
            {
                result.ImportButton = CreateHeaderButton("Import", Icons.ArrowDown, "Import", config.OnImport);
                buttonsContainer.Add(result.ImportButton);
            }
            if (config.ShowVisualize)
            {
                result.VisualizeButton = CreateHeaderButton("Visualize", Icons.ChevronRight, "Visualize", config.OnVisualize);
                buttonsContainer.Add(result.VisualizeButton);
            }
            if (config.ShowRefresh)
            {
                result.RefreshButton = CreateHeaderButton("Refresh", Icons.Cross, "Refresh", config.OnRefresh);
                buttonsContainer.Add(result.RefreshButton);
            }
            if (config.ShowLookup)
            {
                result.LookupButton = CreateHeaderButton("Lookup", Icons.Arrow, "Find References", config.OnLookup);
                buttonsContainer.Add(result.LookupButton);
            }

            topRow.Add(buttonsContainer);
            details.Add(topRow);

            // Description
            var descLabel = new Label { name = "Description", text = config.DefaultDescription };
            descLabel.style.paddingLeft = 4;
            descLabel.style.paddingTop = 2;
            descLabel.style.fontSize = 11;
            descLabel.style.color = Colors.HintText;
            descLabel.style.unityTextAlign = TextAnchor.LowerLeft;
            descLabel.style.textOverflow = TextOverflow.Ellipsis;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.overflow = Overflow.Hidden;
            descLabel.style.maxHeight = 32;
            result.DescriptionLabel = descLabel;
            details.Add(descLabel);

            header.Add(details);

            return result;
        }

        /// <summary>
        /// Creates a header action button.
        /// </summary>
        public static Button CreateHeaderButton(string name, string text, string tooltip, Action onClick, int size = 24)
        {
            var btn = new Button { name = name, text = text, tooltip = tooltip, focusable = false };
            btn.style.width = size;
            btn.style.height = size;
            btn.style.minWidth = size;
            btn.style.minHeight = size;
            btn.style.maxWidth = size;
            btn.style.maxHeight = size;
            btn.style.marginLeft = 2;
            btn.style.marginRight = 2;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.backgroundColor = Colors.ButtonBackground;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.fontSize = 12;
            
            if (onClick != null) btn.clicked += onClick;
            
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
            
            return btn;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 5: Collapsible Sections
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Configuration for collapsible section creation.
        /// </summary>
        public class SectionConfig
        {
            public string Name;
            public string Title;
            public Color AccentColor = Colors.AccentGray;
            public bool StartExpanded = true;
            public string HelpUrl;
            public bool[] IncludeButtons;
        }
        
        /// <summary>
        /// Result from CreateCollapsibleSection containing references.
        /// </summary>
        public class SectionResult
        {
            public VisualElement Section;
            public VisualElement Header;
            public Label Arrow;
            public VisualElement Content;
            public Button HelpButton;
            public Button ImportButton;
            public Button ClearButton;
        }
        
        /// <summary>
        /// Creates a complete collapsible section with header and content.
        /// </summary>
        public static SectionResult CreateCollapsibleSection(SectionConfig config)
        {
            var result = new SectionResult();
            
            var section = new VisualElement { name = $"{config.Name}Section" };
            section.style.marginBottom = 4;
            section.AddToClassList("forge-section");
            result.Section = section;

            // Section Header
            var header = new VisualElement { name = $"{config.Name}Header" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginTop = 8;
            header.style.marginBottom = 4;
            header.style.paddingLeft = 2;
            header.style.paddingRight = 2;
            header.style.paddingTop = 4;
            header.style.paddingBottom = 4;
            header.style.backgroundColor = Colors.SectionHeaderBackground;
            header.style.borderTopLeftRadius = 3;
            header.style.borderTopRightRadius = 3;
            header.style.borderBottomLeftRadius = 3;
            header.style.borderBottomRightRadius = 3;
            header.AddToClassList("forge-section-header");
            result.Header = header;
            section.Add(header);

            // Arrow
            var arrow = new Label(config.StartExpanded ? Icons.ChevronDown : Icons.ChevronRight);
            arrow.name = $"{config.Name}Arrow";
            arrow.style.width = 16;
            arrow.style.fontSize = 10;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            arrow.style.color = Colors.ArrowText;
            arrow.style.marginRight = 2;
            arrow.AddToClassList("forge-section-arrow");
            result.Arrow = arrow;
            header.Add(arrow);

            // Title
            var title = new Label(config.Title);
            title.style.fontSize = 14;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.style.color = Colors.SectionTitle;
            title.style.paddingRight = 8;
            title.style.flexShrink = 0;
            title.AddToClassList("forge-section-title");
            header.Add(title);

            // Divider line
            var divider = new VisualElement();
            divider.style.flexGrow = 1;
            divider.style.height = 1;
            divider.style.backgroundColor = Colors.DividerColor;
            divider.style.marginLeft = 4;
            divider.style.marginRight = 8;
            divider.style.alignSelf = Align.Center;
            divider.style.borderTopLeftRadius = 1;
            divider.style.borderTopRightRadius = 1;
            divider.style.borderBottomLeftRadius = 1;
            divider.style.borderBottomRightRadius = 1;
            header.Add(divider);

            if (config.IncludeButtons is null)
            {
                result.ImportButton = CreateCircularButton($"{config.Name}Import", Icons.ArrowDown, Colors.PolicyGreen, 
                    () => Application.OpenURL(config.HelpUrl));
                header.Add(result.ImportButton);
                
                result.ClearButton = CreateCircularButton($"{config.Name}Clear", Icons.Clear, Colors.PolicyRed, 
                    () => Application.OpenURL(config.HelpUrl));
                header.Add(result.ClearButton);
                
                if (!string.IsNullOrEmpty(config.HelpUrl))
                {
                    result.HelpButton = CreateCircularButton($"{config.Name}Help", Icons.Help, Colors.PolicyPurple, 
                        () => Application.OpenURL(config.HelpUrl));
                    header.Add(result.HelpButton);
                }
            }
            else
            {
                if (config.IncludeButtons[0])
                {
                    result.ImportButton = CreateCircularButton($"{config.Name}Import", Icons.ArrowDown, Colors.PolicyGreen, 
                        () => Application.OpenURL(config.HelpUrl));
                    header.Add(result.ImportButton);
                }

                if (config.IncludeButtons[1])
                {
                    result.ClearButton = CreateCircularButton($"{config.Name}Clear", Icons.Clear, Colors.PolicyRed, 
                        () => Application.OpenURL(config.HelpUrl));
                    header.Add(result.ClearButton);
                }
                
                if (config.IncludeButtons[2] && !string.IsNullOrEmpty(config.HelpUrl))
                {
                    result.HelpButton = CreateCircularButton($"{config.Name}Help", Icons.Help, Colors.PolicyPurple, 
                        () => Application.OpenURL(config.HelpUrl));
                    header.Add(result.HelpButton);
                }
            }
            
            // Help button (optional)
            if (!string.IsNullOrEmpty(config.HelpUrl))
            {
                
            }

            // Content container
            var content = new VisualElement { name = config.Name };
            content.style.paddingLeft = 8;
            content.style.paddingRight = 4;
            content.style.paddingTop = 4;
            content.style.paddingBottom = 8;
            content.style.marginLeft = 2;
            content.style.borderLeftWidth = 2;
            content.style.borderLeftColor = config.AccentColor;
            content.style.display = config.StartExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            content.AddToClassList("forge-section-content");
            result.Content = content;
            section.Add(content);

            // Setup collapse/expand behavior
            SetupCollapseBehavior(header, content, arrow);

            return result;
        }
        
        private static void SetupCollapseBehavior(VisualElement header, VisualElement content, Label arrow)
        {
            bool isExpanded = content.style.display == DisplayStyle.Flex;
            
            header.RegisterCallback<MouseEnterEvent>(_ => header.style.backgroundColor = Colors.SectionHeaderHover);
            header.RegisterCallback<MouseLeaveEvent>(_ => header.style.backgroundColor = Colors.SectionHeaderBackground);
            
            header.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                
                isExpanded = !isExpanded;
                content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                arrow.text = isExpanded ? Icons.ChevronDown : Icons.ChevronRight;
                
                evt.StopPropagation();
            });
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 6: Subsections
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates a styled subsection container with title.
        /// </summary>
        public static VisualElement CreateSubsection(string name, string title, Color accentColor)
        {
            var subsection = new VisualElement { name = name };
            subsection.style.marginTop = 8;
            subsection.style.paddingLeft = 8;
            subsection.style.paddingRight = 8;
            subsection.style.paddingTop = 6;
            subsection.style.paddingBottom = 6;
            subsection.style.backgroundColor = Colors.SubsectionBackground;
            subsection.style.borderTopLeftRadius = 3;
            subsection.style.borderTopRightRadius = 3;
            subsection.style.borderBottomLeftRadius = 3;
            subsection.style.borderBottomRightRadius = 3;
            subsection.style.borderLeftWidth = 2;
            subsection.style.borderLeftColor = accentColor;

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 10;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Colors.HintText;
            titleLabel.style.marginBottom = 6;
            titleLabel.style.letterSpacing = 1;
            subsection.Add(titleLabel);

            return subsection;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 7: Field Creation Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates a styled TextField.
        /// </summary>
        public static TextField CreateTextField(string name, string label, bool multiline = false, int minHeight = 0)
        {
            var field = new TextField(label) { name = name, multiline = multiline };
            field.style.marginBottom = 4;
            field.style.marginTop = 2;
            if (minHeight > 0) field.style.minHeight = minHeight;
            return field;
        }
        
        /// <summary>
        /// Creates a styled Toggle.
        /// </summary>
        public static Toggle CreateToggle(string name, string label)
        {
            var toggle = new Toggle(label) { name = name };
            toggle.style.marginBottom = 4;
            toggle.style.marginTop = 2;
            return toggle;
        }
        
        /// <summary>
        /// Creates a styled EnumField.
        /// </summary>
        public static EnumField CreateEnumField<T>(string name, string label, T defaultValue) where T : Enum
        {
            var field = new EnumField(label, defaultValue) { name = name };
            field.style.marginBottom = 4;
            field.style.marginTop = 2;
            return field;
        }
        
        /// <summary>
        /// Creates a styled IntegerField.
        /// </summary>
        public static IntegerField CreateIntegerField(string name, string label, int defaultValue = 0)
        {
            var field = new IntegerField(label) { name = name, value = defaultValue };
            field.style.marginBottom = 4;
            field.style.marginTop = 2;
            return field;
        }
        
        /// <summary>
        /// Creates a styled FloatField.
        /// </summary>
        public static FloatField CreateFloatField(string name, string label, float defaultValue = 0f)
        {
            var field = new FloatField(label) { name = name, value = defaultValue };
            field.style.marginBottom = 4;
            field.style.marginTop = 2;
            return field;
        }
        
        /// <summary>
        /// Creates a PropertyField bound to a SerializedProperty.
        /// </summary>
        public static PropertyField CreatePropertyField(SerializedProperty property, string name = null, string label = null)
        {
            //var field = new PropertyField(property, label ?? property.displayName) { name = name ?? property.name };
            var field = new PropertyField(property, label ?? string.Empty) { name = name ?? property.name };
            field.style.marginBottom = 4;
            field.style.marginTop = 2;
            return field;
        }
        
        /// <summary>
        /// Creates a PropertyField with percentage width (for grid layouts).
        /// </summary>
        public static PropertyField CreatePropertyFieldPercent(SerializedProperty property, string name, string label, float widthPercent, float marginRightPercent = 0)
        {
            var field = CreatePropertyField(property, name, label);
            field.style.width = Length.Percent(widthPercent);
            if (marginRightPercent > 0) field.style.marginRight = Length.Percent(marginRightPercent);
            return field;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 8: Layout Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates a horizontal row container.
        /// </summary>
        public static VisualElement CreateRow(int marginBottom = 2, bool wrap = false)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginBottom = marginBottom;
            row.style.flexWrap = wrap ? Wrap.Wrap : Wrap.NoWrap;
            return row;
        }
        
        /// <summary>
        /// Creates a vertical column container.
        /// </summary>
        public static VisualElement CreateColumn(int marginRight = 0)
        {
            var column = new VisualElement();
            column.style.flexDirection = FlexDirection.Column;
            column.style.marginRight = marginRight;
            return column;
        }
        
        /// <summary>
        /// Creates a flexible spacer.
        /// </summary>
        public static VisualElement CreateFlexSpacer()
        {
            return new VisualElement { style = { flexGrow = 1 } };
        }
        
        /// <summary>
        /// Creates a fixed-width spacer.
        /// </summary>
        public static VisualElement CreateSpacer(int width = 8)
        {
            return new VisualElement { style = { width = width } };
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
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 9: Special Components
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates an Asset Tag display (read-only, auto-generated).
        /// Returns a tuple of (container, valueLabel) for updating.
        /// </summary>
        public static (VisualElement container, Label valueLabel) CreateAssetTagDisplay()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 6;
            container.style.marginTop = 2;

            var label = new Label("Asset Tag");
            label.style.minWidth = 120;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.color = Colors.LabelText;
            container.Add(label);

            var displayBox = new VisualElement { name = "AssetTagDisplay" };
            displayBox.style.flexGrow = 1;
            displayBox.style.flexDirection = FlexDirection.Row;
            displayBox.style.alignItems = Align.Center;
            displayBox.style.backgroundColor = Colors.ItemBackground;
            displayBox.style.borderTopLeftRadius = 3;
            displayBox.style.borderTopRightRadius = 3;
            displayBox.style.borderBottomLeftRadius = 3;
            displayBox.style.borderBottomRightRadius = 3;
            displayBox.style.paddingLeft = 8;
            displayBox.style.paddingRight = 8;
            displayBox.style.paddingTop = 4;
            displayBox.style.paddingBottom = 4;
            displayBox.style.borderTopWidth = 1;
            displayBox.style.borderBottomWidth = 1;
            displayBox.style.borderLeftWidth = 1;
            displayBox.style.borderRightWidth = 1;
            displayBox.style.borderTopColor = new Color(0.314f, 0.314f, 0.314f, 0.5f);
            displayBox.style.borderBottomColor = new Color(0.314f, 0.314f, 0.314f, 0.5f);
            displayBox.style.borderLeftColor = new Color(0.314f, 0.314f, 0.314f, 0.5f);
            displayBox.style.borderRightColor = new Color(0.314f, 0.314f, 0.314f, 0.5f);
            container.Add(displayBox);

            var valueLabel = new Label("AssetName") { name = "AssetTagValue" };
            valueLabel.style.flexGrow = 1;
            valueLabel.style.color = Colors.AssetTagText;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.BoldAndItalic;
            valueLabel.style.fontSize = 12;
            displayBox.Add(valueLabel);

            var autoLabel = new Label("(auto)");
            autoLabel.style.fontSize = 9;
            autoLabel.style.color = Colors.HintText;
            autoLabel.style.marginLeft = 8;
            displayBox.Add(autoLabel);

            return (container, valueLabel);
        }
        
        /// <summary>
        /// Creates a circular button (for help buttons, etc.).
        /// </summary>
        public static Button CreateCircularButton(string name, string text, Color backgroundColor, Action onClick = null, int size = 20)
        {
            var btn = new Button { name = name, text = text, tooltip = name.Replace("Help", " Help") };
            btn.style.width = size;
            btn.style.height = size;
            btn.style.minWidth = size;
            btn.style.minHeight = size;
            btn.style.maxWidth = size;
            btn.style.maxHeight = size;
            btn.style.borderTopLeftRadius = size / 2;
            btn.style.borderTopRightRadius = size / 2;
            btn.style.borderBottomLeftRadius = size / 2;
            btn.style.borderBottomRightRadius = size / 2;
            btn.style.marginLeft = 4;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.fontSize = 11;
            btn.style.paddingLeft = 0;
            btn.style.paddingRight = 0;
            btn.style.paddingTop = 0;
            btn.style.paddingBottom = 0;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.backgroundColor = backgroundColor;
            
            if (onClick != null) btn.clicked += onClick;
            
            return btn;
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
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SECTION 10: Property Drawer Containers
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Creates a main container box with themed styling (for property drawers).
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
        /// Creates a section container with title (for property drawers).
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
        /// Creates a header with icon and title (for property drawers).
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
    }
}