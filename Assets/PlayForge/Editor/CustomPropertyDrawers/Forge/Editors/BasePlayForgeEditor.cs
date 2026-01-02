using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Base class for all PlayForge custom editors.
    /// Features a custom IMGUI header in Unity's inspector header area
    /// and UIElements-based content below.
    /// </summary>
    public abstract class BasePlayForgeEditor : UnityEditor.Editor
    {
        protected VisualElement root;
        
        // Header configuration
        private const float HeaderHeight = 72f;
        private const float IconSize = 48f;
        private const float ButtonSize = 22f;
        private const float ButtonSpacing = 4f;
        private const float Padding = 8f;

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Methods - Header Data (Must be implemented)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Display name shown in header (e.g., ability name)</summary>
        protected abstract string GetDisplayName();
        
        /// <summary>Short description shown below name</summary>
        protected abstract string GetDisplayDescription();
        
        /// <summary>Icon texture for the header (can be null)</summary>
        protected abstract Texture2D GetHeaderIcon();
        
        /// <summary>Asset type label (e.g., "ABILITY", "EFFECT")</summary>
        protected abstract string GetAssetTypeLabel();
        
        /// <summary>Accent color for the asset type</summary>
        protected abstract Color GetAssetTypeColor();
        
        /// <summary>Documentation URL for this asset type</summary>
        protected abstract string GetDocumentationUrl();

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Methods - Actions (Must be implemented)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Called when the Refresh button is clicked</summary>
        protected abstract void Refresh();

        // ═══════════════════════════════════════════════════════════════════════════
        // Virtual Methods - Optional Overrides
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Called when Visualize button is clicked. Override to enable.</summary>
        protected virtual void OnVisualize() { }
        
        /// <summary>Called when Import button is clicked. Override to enable.</summary>
        protected virtual void OnImport() { }
        
        /// <summary>Called when Open button is clicked. Opens PlayForge Manager to this asset.</summary>
        protected virtual void OnOpenInManager()
        {
            PlayForgeManager.OpenToAsset(target);
        }
        
        /// <summary>Whether to show the Visualize button</summary>
        protected virtual bool ShowVisualizeButton => false;
        
        /// <summary>Whether to show the Import button</summary>
        protected virtual bool ShowImportButton => false;
        
        /// <summary>For programmatic editors, this can be empty</summary>
        protected virtual void SetupCollapsibleSections() { }

        // ═══════════════════════════════════════════════════════════════════════════
        // IMGUI Header Drawing - Overrides Unity's default inspector header
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected override void OnHeaderGUI()
        {
            base.OnHeaderGUI();
            DrawCustomHeader();
        }
        
        private void DrawCustomHeader()
        {
            var headerRect = GUILayoutUtility.GetRect(0, HeaderHeight, GUILayout.ExpandWidth(true));
            
            // Background
            EditorGUI.DrawRect(headerRect, new Color(0.16f, 0.16f, 0.16f));
            
            // Bottom border with accent color
            var borderRect = new Rect(headerRect.x, headerRect.yMax - 2, headerRect.width, 2);
            EditorGUI.DrawRect(borderRect, GetAssetTypeColor());
            
            // === Left Side: Icon ===
            var iconRect = new Rect(
                headerRect.x + Padding,
                headerRect.y + (HeaderHeight - IconSize) / 2 - 1,
                IconSize,
                IconSize
            );
            
            // Icon background
            var iconBgRect = new Rect(iconRect.x - 2, iconRect.y - 2, iconRect.width + 4, iconRect.height + 4);
            EditorGUI.DrawRect(iconBgRect, new Color(0.12f, 0.12f, 0.12f));
            
            var icon = GetHeaderIcon();
            if (icon != null)
            {
                GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            }
            else
            {
                // Draw placeholder
                EditorGUI.DrawRect(iconRect, new Color(0.2f, 0.2f, 0.2f));
                var placeholderStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
                {
                    fontSize = 20
                };
                GUI.Label(iconRect, "?", placeholderStyle);
            }
            
            // === Right Side: Buttons ===
            float btnX = headerRect.xMax - Padding - ButtonSize;
            float btnY = headerRect.y + Padding;
            
            // Documentation button (small, top-right corner)
            var docBtnRect = new Rect(btnX, btnY, ButtonSize, ButtonSize);
            if (DrawHeaderButton(docBtnRect, "?", "Open Documentation", new Color(0.4f, 0.4f, 0.4f)))
            {
                var url = GetDocumentationUrl();
                if (!string.IsNullOrEmpty(url))
                    Application.OpenURL(url);
            }
            
            // Main action buttons row (below doc button)
            btnY = headerRect.y + HeaderHeight - ButtonSize - Padding - 4;
            btnX = headerRect.xMax - Padding;
            
            // Open in Manager button
            btnX -= ButtonSize;
            var openBtnRect = new Rect(btnX, btnY, ButtonSize, ButtonSize);
            if (DrawHeaderButton(openBtnRect, "⊞", "Open in Manager", new Color(0.3f, 0.5f, 0.7f)))
            {
                OnOpenInManager();
            }
            
            // Visualize button (if enabled)
            if (ShowVisualizeButton)
            {
                btnX -= ButtonSize + ButtonSpacing;
                var vizBtnRect = new Rect(btnX, btnY, ButtonSize, ButtonSize);
                if (DrawHeaderButton(vizBtnRect, "◉", "Visualize", new Color(0.6f, 0.4f, 0.7f)))
                {
                    OnVisualize();
                }
            }
            
            // Import button (if enabled)
            if (ShowImportButton)
            {
                btnX -= ButtonSize + ButtonSpacing;
                var importBtnRect = new Rect(btnX, btnY, ButtonSize, ButtonSize);
                if (DrawHeaderButton(importBtnRect, "↓", "Import from another asset", new Color(0.4f, 0.6f, 0.4f)))
                {
                    OnImport();
                }
            }
            
            // Refresh button
            btnX -= ButtonSize + ButtonSpacing;
            var refreshBtnRect = new Rect(btnX, btnY, ButtonSize, ButtonSize);
            if (DrawHeaderButton(refreshBtnRect, "↻", "Refresh", new Color(0.5f, 0.5f, 0.5f)))
            {
                Refresh();
            }
            
            // === Center: Text Content ===
            float textX = iconRect.xMax + 12;
            float textWidth = btnX - textX - 12;
            
            // Asset Type Badge
            var typeLabel = GetAssetTypeLabel();
            var typeColor = GetAssetTypeColor();
            var typeLabelStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                normal = { textColor = typeColor },
                padding = new RectOffset(4, 4, 2, 2)
            };
            var typeLabelContent = new GUIContent(typeLabel);
            var typeLabelSize = typeLabelStyle.CalcSize(typeLabelContent);
            var typeLabelRect = new Rect(textX, headerRect.y + Padding, typeLabelSize.x + 4, typeLabelSize.y);
            
            // Badge background
            var badgeBgRect = new Rect(typeLabelRect.x - 2, typeLabelRect.y, typeLabelRect.width + 4, typeLabelRect.height);
            EditorGUI.DrawRect(badgeBgRect, new Color(typeColor.r * 0.2f, typeColor.g * 0.2f, typeColor.b * 0.2f, 0.8f));
            GUI.Label(typeLabelRect, typeLabel, typeLabelStyle);
            
            // Name
            var displayName = GetDisplayName();
            var nameStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
                clipping = TextClipping.Ellipsis
            };
            var nameRect = new Rect(textX, typeLabelRect.yMax + 2, textWidth, 20);
            GUI.Label(nameRect, displayName, nameStyle);
            
            // Description
            var description = GetDisplayDescription();
            if (!string.IsNullOrEmpty(description))
            {
                var descStyle = new GUIStyle(EditorStyles.miniLabel)
                {
                    fontSize = 10,
                    normal = { textColor = new Color(0.55f, 0.55f, 0.55f) },
                    clipping = TextClipping.Ellipsis,
                    wordWrap = true
                };
                var descRect = new Rect(textX, nameRect.yMax + 1, textWidth, 28);
                GUI.Label(descRect, description, descStyle);
            }
        }
        
        private bool DrawHeaderButton(Rect rect, string icon, string tooltip, Color baseColor)
        {
            var content = new GUIContent(icon, tooltip);
            
            // Check hover
            bool isHovered = rect.Contains(Event.current.mousePosition);
            var bgColor = isHovered ? new Color(baseColor.r + 0.15f, baseColor.g + 0.15f, baseColor.b + 0.15f) : baseColor;
            
            // Draw background
            EditorGUI.DrawRect(rect, bgColor);
            
            // Draw border on hover
            if (isHovered)
            {
                var borderColor = new Color(baseColor.r + 0.3f, baseColor.g + 0.3f, baseColor.b + 0.3f);
                DrawRectBorder(rect, borderColor, 1);
            }
            
            // Draw icon
            var btnStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = isHovered ? Color.white : new Color(0.8f, 0.8f, 0.8f) }
            };
            GUI.Label(rect, content, btnStyle);
            
            // Handle click
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                return true;
            }
            
            // Request repaint for hover effects
            if (isHovered)
            {
                Repaint();
            }
            
            return false;
        }
        
        private void DrawRectBorder(Rect rect, Color color, float thickness)
        {
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), color); // Top
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color); // Bottom
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), color); // Left
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color); // Right
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Tag Generation
        // ═══════════════════════════════════════════════════════════════════════════

        protected const string UnnamedAssetTag = "Unnamed";
        protected const string UnknownAssetName = "Error";
        
        protected (string result, bool isUnknown) GenerateAssetTag(string assetName, string fallback)
        {
            if (string.IsNullOrEmpty(assetName))
                return ($"{UnnamedAssetTag} {fallback}!", true);
            
            // Remove special characters (keep only alphanumeric and spaces)
            string cleaned = Regex.Replace(assetName, @"[^a-zA-Z0-9\s]", "");
            
            // Split by spaces and capitalize each word (PascalCase)
            string[] words = cleaned.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < words.Length; i++)
            {
                if (words[i].Length > 0)
                {
                    words[i] = char.ToUpper(words[i][0]) + 
                               (words[i].Length > 1 ? words[i].Substring(1) : "");
                }
            }
            
            string result = string.Join("", words);
            
            // Ensure it starts with a letter
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = $"{fallback}_" + result;
            }

            return string.IsNullOrEmpty(result) ? ($"{UnnamedAssetTag} {fallback}!", true) : (result, false);
        }
        
        /// <summary>
        /// Updates the Name field of a Tag based on the asset's display name.
        /// </summary>
        protected string UpdateTagName(ref Tag tag, string assetName, string fallback)
        {
            var (generatedName, isUnknown) = GenerateAssetTag(assetName, fallback);
            
            if (!isUnknown)
            {
                tag.Name = generatedName;
            }
            
            return generatedName;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Utility Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected void MarkDirty(Object obj)
        {
            EditorUtility.SetDirty(obj);
        }
        
        protected void PingAsset(Object obj)
        {
            EditorGUIUtility.PingObject(obj);
        }
        
        protected string GetAssetGuid(Object obj)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            return AssetDatabase.AssetPathToGUID(path);
        }
        
        /// <summary>
        /// Truncates text to max length with ellipsis
        /// </summary>
        protected string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            return text.Substring(0, maxLength - 3) + "...";
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Legacy UXML Support
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected void SetupCollapsibleSection(string sectionName, bool startExpanded = true)
        {
            var header = root.Q($"{sectionName}Header");
            var content = root.Q(sectionName);
            var arrow = root.Q<Label>($"{sectionName}Arrow");
            
            if (header == null || content == null) return;
            
            bool isExpanded = startExpanded;
            
            content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            if (arrow != null)
            {
                arrow.text = isExpanded ? Icons.ChevronDown : Icons.ChevronRight;
            }
            
            header.RegisterCallback<MouseEnterEvent>(_ =>
            {
                header.style.backgroundColor = Colors.SectionHeaderHover;
            });
            
            header.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                header.style.backgroundColor = Colors.SectionHeaderBackground;
            });
            
            header.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target is Button) return;
                
                isExpanded = !isExpanded;
                content.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                
                if (arrow != null)
                {
                    arrow.text = isExpanded ? Icons.ChevronDown : Icons.ChevronRight;
                }
                
                evt.StopPropagation();
            });
        }
        
        protected PropertyField BindPropertyField(VisualElement container, string fieldName, string propertyPath, string fieldLabel = "")
        {
            var field = container.Q<PropertyField>(fieldName);
            if (field != null && !string.IsNullOrEmpty(fieldLabel))
            {
                field.label = fieldLabel;
            }
            
            var prop = serializedObject.FindProperty(propertyPath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
            return field;
        }
        
        protected PropertyField BindPropertyField(VisualElement container, string fieldName, SerializedProperty parent, string relativePath, string fieldLabel = "")
        {
            var field = container.Q<PropertyField>(fieldName);
            if (field != null && !string.IsNullOrEmpty(fieldLabel))
            {
                field.label = fieldLabel;
            }
            
            var prop = parent.FindPropertyRelative(relativePath);
            if (field != null && prop != null)
            {
                field.BindProperty(prop);
            }
            return field;
        }
        
        protected void SetupHelpButton(string buttonName, string url)
        {
            var btn = root.Q<Button>(buttonName);
            if (btn != null)
            {
                btn.clicked += () => Application.OpenURL(url);
            }
        }
        
        protected void ConfigureScrollView()
        {
            var scrollView = root.Q<ScrollView>("ContentScrollView");
            if (scrollView != null)
            {
                scrollView.style.minHeight = 200;
                scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
                scrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;
            }
        }
        
        protected void ApplyButtonHoverStyle(Button btn)
        {
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
        }
    }
}