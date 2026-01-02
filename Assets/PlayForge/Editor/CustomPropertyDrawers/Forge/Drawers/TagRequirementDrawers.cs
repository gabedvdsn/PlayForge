using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // AvoidRequireContainer Drawer - Compact inline
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AvoidRequireContainer))]
    public class AvoidRequireContainerDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginBottom = 1;
            container.style.marginTop = 1;
            
            // Tag (primary)
            var tagProp = property.FindPropertyRelative("Tag");
            var tagField = new PropertyField(tagProp, "");
            tagField.style.flexGrow = 1;
            tagField.style.minWidth = 80;
            tagField.BindProperty(tagProp);
            container.Add(tagField);
            
            // Operator
            var opProp = property.FindPropertyRelative("Operator");
            var opField = new PropertyField(opProp, "");
            opField.style.width = 90;
            opField.style.marginLeft = 4;
            opField.BindProperty(opProp);
            container.Add(opField);
            
            // Magnitude
            var magProp = property.FindPropertyRelative("Magnitude");
            var magField = new IntegerField();
            magField.style.width = 36;
            magField.style.marginLeft = 4;
            magField.bindingPath = magProp.propertyPath;
            magField.tooltip = "Weight/Magnitude";
            container.Add(magField);
            
            return container;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // AvoidRequireTagGroup Drawer - Simple two-list layout
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AvoidRequireTagGroup))]
    public class AvoidRequireTagGroupDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            
            // Require
            var requireProp = property.FindPropertyRelative("RequireTags");
            var requireField = new PropertyField(requireProp, "Require");
            requireField.BindProperty(requireProp);
            container.Add(requireField);
            
            // Avoid
            var avoidProp = property.FindPropertyRelative("AvoidTags");
            var avoidField = new PropertyField(avoidProp, "Avoid");
            avoidField.style.marginTop = 2;
            avoidField.BindProperty(avoidProp);
            container.Add(avoidField);
            
            return container;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // TagRequirements Drawer - Clean minimal sections
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(TagRequirements))]
    public class TagRequirementsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            
            // Application
            var appProp = property.FindPropertyRelative("ApplicationRequirements");
            var appSection = CreateSection("Apply", "Required to apply effect", Colors.AccentGreen, appProp);
            container.Add(appSection);
            
            // Ongoing
            var ongoingProp = property.FindPropertyRelative("OngoingRequirements");
            var ongoingSection = CreateSection("Ongoing", "Must remain true while active", Colors.AccentYellow, ongoingProp);
            container.Add(ongoingSection);
            
            // Removal
            var removalProp = property.FindPropertyRelative("RemovalRequirements");
            var removalSection = CreateSection("Remove", "Triggers removal when met", Colors.AccentRed, removalProp);
            container.Add(removalSection);
            
            return container;
        }
        
        private VisualElement CreateSection(string title, string tooltip, Color accentColor, SerializedProperty prop)
        {
            var section = new VisualElement();
            section.style.marginTop = 4;
            section.style.marginBottom = 2;
            section.style.paddingLeft = 6;
            section.style.paddingTop = 4;
            section.style.paddingBottom = 4;
            section.style.paddingRight = 4;
            section.style.borderLeftWidth = 2;
            section.style.borderLeftColor = accentColor;
            section.style.backgroundColor = new Color(0.16f, 0.16f, 0.16f, 0.4f);
            section.style.borderTopLeftRadius = 2;
            section.style.borderTopRightRadius = 2;
            section.style.borderBottomLeftRadius = 2;
            section.style.borderBottomRightRadius = 2;
            
            // Header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;
            section.Add(headerRow);
            
            // Title
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 10;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = accentColor;
            titleLabel.tooltip = tooltip;
            headerRow.Add(titleLabel);
            
            // Spacer
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            headerRow.Add(spacer);
            
            // Count badges
            var requireProp = prop.FindPropertyRelative("RequireTags");
            var avoidProp = prop.FindPropertyRelative("AvoidTags");
            
            int reqCount = requireProp?.arraySize ?? 0;
            int avoidCount = avoidProp?.arraySize ?? 0;
            
            if (reqCount > 0 || avoidCount > 0)
            {
                if (reqCount > 0)
                {
                    var reqBadge = CreateCountBadge($"+{reqCount}", Colors.AccentGreen);
                    reqBadge.tooltip = $"{reqCount} required";
                    headerRow.Add(reqBadge);
                }
                
                if (avoidCount > 0)
                {
                    var avoidBadge = CreateCountBadge($"-{avoidCount}", Colors.AccentRed);
                    avoidBadge.tooltip = $"{avoidCount} avoided";
                    avoidBadge.style.marginLeft = 4;
                    headerRow.Add(avoidBadge);
                }
            }
            else
            {
                var emptyLabel = new Label("—");
                emptyLabel.style.fontSize = 9;
                emptyLabel.style.color = Colors.HintText;
                headerRow.Add(emptyLabel);
            }
            
            // Property field
            var field = new PropertyField(prop, "");
            field.BindProperty(prop);
            section.Add(field);
            
            return section;
        }
        
        private Label CreateCountBadge(string text, Color color)
        {
            var badge = new Label(text);
            badge.style.fontSize = 9;
            badge.style.color = color;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.12f);
            badge.style.paddingLeft = 4;
            badge.style.paddingRight = 4;
            badge.style.paddingTop = 1;
            badge.style.paddingBottom = 1;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            return badge;
        }
    }
}