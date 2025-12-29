using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // AvoidRequireContainer Drawer
    // Compact inline display: [Tag] [Operator] [Magnitude]
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
            
            // Tag field (primary - takes most space)
            var tagProp = property.FindPropertyRelative("Tag");
            var tagField = new PropertyField(tagProp, "");
            tagField.style.flexGrow = 1;
            tagField.style.minWidth = 80;
            tagField.BindProperty(tagProp);
            container.Add(tagField);
            
            // Operator dropdown
            var operatorProp = property.FindPropertyRelative("Operator");
            var operatorField = new PropertyField(operatorProp, "");
            operatorField.style.width = 110;
            operatorField.style.marginLeft = 4;
            operatorField.style.marginRight = 4;
            operatorField.BindProperty(operatorProp);
            container.Add(operatorField);
            
            // Magnitude field
            var magnitudeProp = property.FindPropertyRelative("Magnitude");
            var magnitudeField = new IntegerField();
            magnitudeField.style.width = 40;
            magnitudeField.bindingPath = magnitudeProp.propertyPath;
            container.Add(magnitudeField);
            
            return container;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // AvoidRequireTagGroup Drawer
    // Simplified: Just two labeled lists, no heavy containers
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AvoidRequireTagGroup))]
    public class AvoidRequireTagGroupDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.marginTop = 2;
            container.style.marginBottom = 2;
            
            // REQUIRE TAGS
            var requireProp = property.FindPropertyRelative("RequireTags");
            var requireField = new PropertyField(requireProp, "Require");
            requireField.BindProperty(requireProp);
            container.Add(requireField);
            
            // AVOID TAGS
            var avoidProp = property.FindPropertyRelative("AvoidTags");
            var avoidField = new PropertyField(avoidProp, "Avoid");
            avoidField.style.marginTop = 2;
            avoidField.BindProperty(avoidProp);
            container.Add(avoidField);
            
            return container;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // TagRequirements Drawer
    // Clean sections with inline badges in headers
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(TagRequirements))]
    public class TagRequirementsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.marginTop = 2;
            
            // APPLICATION REQUIREMENTS
            var appProp = property.FindPropertyRelative("ApplicationRequirements");
            var appSection = CreateRequirementSection("Application", "Tags required to apply", Colors.AccentGreen, appProp);
            container.Add(appSection);
            
            // ONGOING REQUIREMENTS
            var ongoingProp = property.FindPropertyRelative("OngoingRequirements");
            var ongoingSection = CreateRequirementSection("Ongoing", "Must remain true while active", Colors.AccentYellow, ongoingProp);
            container.Add(ongoingSection);
            
            // REMOVAL REQUIREMENTS
            var removalProp = property.FindPropertyRelative("RemovalRequirements");
            var removalSection = CreateRequirementSection("Removal", "Triggers removal when met", Colors.AccentRed, removalProp);
            container.Add(removalSection);
            
            return container;
        }
        
        private VisualElement CreateRequirementSection(string title, string tooltip, Color accentColor, SerializedProperty prop)
        {
            var section = new VisualElement();
            section.style.marginTop = 4;
            section.style.marginBottom = 2;
            section.style.paddingLeft = 8;
            section.style.paddingTop = 4;
            section.style.paddingBottom = 4;
            section.style.paddingRight = 4;
            section.style.borderLeftWidth = 2;
            section.style.borderLeftColor = accentColor;
            section.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.3f);
            section.style.borderTopLeftRadius = 2;
            section.style.borderTopRightRadius = 2;
            section.style.borderBottomLeftRadius = 2;
            section.style.borderBottomRightRadius = 2;
            
            // Header row with title and badges
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;
            section.Add(headerRow);
            
            // Title
            var titleLabel = new Label(title.ToUpper());
            titleLabel.style.fontSize = 10;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = accentColor;
            titleLabel.style.letterSpacing = 1;
            titleLabel.tooltip = tooltip;
            headerRow.Add(titleLabel);
            
            // Flexible spacer
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            headerRow.Add(spacer);
            
            // Badges (will be updated)
            var requireProp = prop.FindPropertyRelative("RequireTags");
            var avoidProp = prop.FindPropertyRelative("AvoidTags");
            
            var requireCount = requireProp?.arraySize ?? 0;
            var avoidCount = avoidProp?.arraySize ?? 0;
            
            var reqBadge = CreateCompactBadge($"{Icons.Check}{requireCount}", Colors.PolicyGreen);
            reqBadge.tooltip = $"{requireCount} required";
            headerRow.Add(reqBadge);
            
            var avoidBadge = CreateCompactBadge($"{Icons.Cross}{avoidCount}", Colors.PolicyRed);
            avoidBadge.tooltip = $"{avoidCount} avoided";
            avoidBadge.style.marginLeft = 4;
            headerRow.Add(avoidBadge);

            var hint = CreateHintLabel(tooltip);
            section.Add(hint);
            
            // Property field for AvoidRequireTagGroup
            var field = new PropertyField(prop, "");
            field.BindProperty(prop);
            section.Add(field);
            
            return section;
        }
        
        private Label CreateCompactBadge(string text, Color color)
        {
            var badge = new Label(text);
            badge.style.fontSize = 9;
            badge.style.color = color;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            badge.style.paddingLeft = 4;
            badge.style.paddingRight = 4;
            badge.style.paddingTop = 1;
            badge.style.paddingBottom = 1;
            badge.style.borderTopLeftRadius = 6;
            badge.style.borderTopRightRadius = 6;
            badge.style.borderBottomLeftRadius = 6;
            badge.style.borderBottomRightRadius = 6;
            return badge;
        }
    }
}