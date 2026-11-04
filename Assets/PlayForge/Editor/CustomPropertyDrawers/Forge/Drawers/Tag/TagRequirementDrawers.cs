using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // The tag requirements, as the Effect mockup's table (ED-37)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// An effect's tag requirements on one side, the target's or the source's, as the Effect mockup's table: Applying,
    /// While On and Removed By, each with what it requires and what it must not have (ED-37). The drawer drew a tinted box
    /// with colored subsections, a type badge, a Name and import buttons of its own, and rebuilt itself as it opened.
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectTagRequirements))]
    public class EffectTagRequirementsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var table = new ForgeRequirementsTable(property, "When",
                new ForgeRequirementsTable.Group("Applying", nameof(EffectTagRequirements.ApplicationRequirements),
                    "Must pass for the effect to be applied"),
                new ForgeRequirementsTable.Group("While on", nameof(EffectTagRequirements.OngoingRequirements),
                    "While it fails, the effect stays, but its ticks don't execute"),
                new ForgeRequirementsTable.Group("Removed by", nameof(EffectTagRequirements.RemovalRequirements),
                    "When it's met, the effect is removed"));
            // The style sheet lays out the table, wherever the drawer is drawn (ED-20)
            ForgeTheme.Apply(table);
            return table;
        }
    }

    /// <summary>
    /// An ability's tag requirements, as the same table: the caster's and the target's, each with what it requires and
    /// what it must not have (ED-37).
    /// </summary>
    [CustomPropertyDrawer(typeof(AbilityTagRequirements))]
    public class AbilityTagRequirementsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var table = new ForgeRequirementsTable(property, "Who",
                new ForgeRequirementsTable.Group("Caster", nameof(AbilityTagRequirements.SourceRequirements),
                    "Checked as the ability is asked to activate, and again after targeting"),
                new ForgeRequirementsTable.Group("Target", nameof(AbilityTagRequirements.TargetRequirements),
                    "Checked after targeting, when there's a target"));
            ForgeTheme.Apply(table);
            return table;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // AvoidRequireContainer and AvoidRequireTagGroup Drawers
    // ═══════════════════════════════════════════════════════════════════════════════

    [CustomPropertyDrawer(typeof(AvoidRequireTagGroup))]
    public class AvoidRequireTagGroupDrawer : PropertyDrawer
    {
        private static readonly HashSet<string> _rebuildingProperties = new();

        private Color RequirementOrange
        {
            get
            {
                AvoidRequireTagGroupColor attr = null;
                //var attr = fieldInfo.GetCustomAttribute<AvoidRequireTagGroupColor>();
                return attr?.Color ?? Colors.BorderLight.Fade(.75f);
            }
        }
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "AvoidRequireTagGroupRoot" };
            // The style sheet sizes and colors the kit's glyphs, wherever the drawer is drawn: in Unity's own inspector too (ED-20)
            ForgeTheme.Apply(root);
            root.style.marginBottom = 4;
            BuildUI(root, property);
            return root;
        }
        
        private void ScheduleRebuild(VisualElement root, SerializedProperty property)
        {
            var propPath = property.propertyPath;
            var targetObject = property.serializedObject.targetObject;
            var serializedObject = property.serializedObject;
            if (_rebuildingProperties.Contains(propPath)) return;
            _rebuildingProperties.Add(propPath);
            root.schedule.Execute(() =>
            {
                _rebuildingProperties.Remove(propPath);
                if (targetObject == null) return;
                // The drawer's own SerializedObject, so the rebuilt UI edits every selected asset. A copy over the
                // first target edited that asset alone (ED-12)
                if (root.panel == null) return;
                var so = serializedObject;
                so.Update();
                var freshProp = so.FindProperty(propPath);
                if (freshProp != null) BuildUI(root, freshProp);
            });
        }
        
        private void BuildUI(VisualElement root, SerializedProperty property)
        {
            root.Clear();
            if (property?.serializedObject?.targetObject == null) return;
            
            // Kept per asset for the editor session, starting collapsed (ED-18)
            bool isCollapsed = DrawerCollapseState.IsCollapsed(property, collapsedByDefault: true);
            var nameProp = property.FindPropertyRelative("Name");
            var requireProp = property.FindPropertyRelative("RequireTags");
            var avoidProp = property.FindPropertyRelative("AvoidTags");
            
            string customName = nameProp?.stringValue ?? "";
            int reqCount = requireProp?.arraySize ?? 0;
            int avoidCount = avoidProp?.arraySize ?? 0;
            
            var container = new VisualElement { name = "AvoidRequireContainer" };
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = RequirementOrange;
            container.style.borderTopWidth = 1;
            container.style.borderTopColor = new Color(RequirementOrange.r, RequirementOrange.g, RequirementOrange.b, 0.5f);
            container.style.borderRightWidth = 1;
            container.style.borderRightColor = new Color(RequirementOrange.r, RequirementOrange.g, RequirementOrange.b, 0.5f);
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = new Color(RequirementOrange.r, RequirementOrange.g, RequirementOrange.b, 0.5f);
            container.style.borderTopLeftRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.backgroundColor = Colors.SectionBackground;
            container.style.paddingTop = 4;
            container.style.paddingBottom = isCollapsed ? 4 : 6;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            container.style.marginTop = 2;
            root.Add(container);
            
            // Header
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = isCollapsed ? 0 : 4 } };
            container.Add(header);
            
            var collapseBtn = new Button { text = isCollapsed ? Icons.ChevronRight : Icons.ChevronDown };
            collapseBtn.style.width = 18;
            collapseBtn.style.height = 18;
            collapseBtn.style.marginRight = 4;
            collapseBtn.style.fontSize = 8;
            collapseBtn.style.paddingLeft = 0;
            collapseBtn.style.paddingRight = 0;
            collapseBtn.style.paddingTop = 0;
            collapseBtn.style.paddingBottom = 0;
            collapseBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            collapseBtn.style.borderTopLeftRadius = 3;
            collapseBtn.style.borderTopRightRadius = 3;
            collapseBtn.style.borderBottomLeftRadius = 3;
            collapseBtn.style.borderBottomRightRadius = 3;
            collapseBtn.style.backgroundColor = Colors.ButtonBackground;
            collapseBtn.RegisterCallback<MouseEnterEvent>(_ => collapseBtn.style.backgroundColor = Colors.ButtonHover);
            collapseBtn.RegisterCallback<MouseLeaveEvent>(_ => collapseBtn.style.backgroundColor = Colors.ButtonBackground);
            collapseBtn.clicked += () => { DrawerCollapseState.SetCollapsed(property, !isCollapsed); ScheduleRebuild(root, property); };
            header.Add(collapseBtn);
            
            if (!string.IsNullOrEmpty(customName))
            {
                header.Add(new Label(customName) { style = { color = Colors.LabelText, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11, marginRight = 6 } });
                header.Add(CreateBadge(property.displayName, Colors.HintText));
            }
            else
            {
                header.Add(new Label(property.displayName) { style = { color = Colors.LabelText, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11 } });
            }
            
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            
            if (reqCount > 0) header.Add(CreateBadge($"+{reqCount}", Colors.AccentGreen));
            if (avoidCount > 0) { var b = CreateBadge($"-{avoidCount}", Colors.AccentRed); b.style.marginLeft = 4; header.Add(b); }
            if (reqCount == 0 && avoidCount == 0 && isCollapsed)
                header.Add(new Label("Empty") { style = { fontSize = 9, color = Colors.HintText, unityFontStyleAndWeight = FontStyle.Italic } });
            
            var importBtn = new Button();
            importBtn.Add(new ForgeGlyph(ForgeGlyphShape.Import));
            importBtn.style.width = 22;
            importBtn.style.height = 20;
            importBtn.style.marginLeft = 8;
            importBtn.style.fontSize = 10;
            importBtn.style.borderTopLeftRadius = 3;
            importBtn.style.borderTopRightRadius = 3;
            importBtn.style.borderBottomLeftRadius = 3;
            importBtn.style.borderBottomRightRadius = 3;
            importBtn.style.backgroundColor = Colors.ButtonBackground;
            importBtn.RegisterCallback<MouseEnterEvent>(_ => importBtn.style.backgroundColor = Colors.ButtonHover);
            importBtn.RegisterCallback<MouseLeaveEvent>(_ => importBtn.style.backgroundColor = Colors.ButtonBackground);
            // The one Import window, which lists every asset with a requirement group at this path (ED-32)
            importBtn.clicked += () => ForgeImportWindow.ShowFor(property.serializedObject, "Requirements",
                new[] { property.propertyPath }, () => ScheduleRebuild(root, property), acrossTypes: true);
            header.Add(importBtn);
            
            if (!isCollapsed)
            {
                var content = new VisualElement { style = { paddingTop = 4 } };
                container.Add(content);
                
                // Name field
                var nameRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
                nameRow.Add(new Label("Name") { style = { width = 50, fontSize = 10, color = Colors.HintText } });
                var nameField = new TextField { value = customName, style = { flexGrow = 1 } };
                nameField.RegisterValueChangedCallback(evt =>
                {
                    if (nameProp != null)
                    {
                        nameProp.stringValue = evt.newValue;
                        property.serializedObject.ApplyModifiedProperties();
                    }
                });
                nameRow.Add(nameField);
                content.Add(nameRow);
                
                content.Add(CreateTagSection("Require", Colors.AccentGreen, requireProp));
                var avoidSection = CreateTagSection("Avoid", Colors.AccentRed, avoidProp);
                avoidSection.style.marginTop = 6;
                content.Add(avoidSection);
                
                container.Bind(property.serializedObject);
            }
        }
        
        private VisualElement CreateTagSection(string title, Color color, SerializedProperty listProp)
        {
            var section = new VisualElement();
            section.style.paddingLeft = 4;
            section.style.paddingTop = 2;
            section.style.paddingBottom = 2;
            section.style.borderLeftWidth = 2;
            section.style.borderLeftColor = color;
            section.style.backgroundColor = Colors.SubsectionBackground;
            section.style.borderTopLeftRadius = 2;
            section.style.borderTopRightRadius = 2;
            section.style.borderBottomLeftRadius = 2;
            section.style.borderBottomRightRadius = 2;
            
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            header.Add(new Label(title) { style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, color = Colors.LabelText } });
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            int count = listProp?.arraySize ?? 0;
            if (count > 0) header.Add(new Label($"({count})") { style = { fontSize = 9, color = color } });
            section.Add(header);
            
            var field = new PropertyField(listProp, "");
            field.style.marginTop = 2;
            field.BindProperty(listProp);
            section.Add(field);
            return section;
        }
        
        private Label CreateBadge(string text, Color color)
        {
            var badge = new Label(text);
            badge.style.fontSize = 9;
            badge.style.color = color;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
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