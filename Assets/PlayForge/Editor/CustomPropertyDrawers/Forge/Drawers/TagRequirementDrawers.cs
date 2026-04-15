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
    // Abstract Base Drawer for Tag Requirements
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public abstract class AbstractTagRequirementsDrawer<T> : PropertyDrawer where T : AbstractTagRequirements
    {
        private static readonly Dictionary<string, bool> CollapsedStates = new();
        private static readonly HashSet<string> RebuildingProperties = new();
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Configuration - Override in concrete drawers
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Main accent color for the container border</summary>
        protected abstract Color AccentColor { get; }
        
        /// <summary>Text shown in the type indicator badge</summary>
        protected abstract string TypeIndicatorText { get; }
        
        /// <summary>Returns the subsection configurations: (fieldName, displayName, shortName, tooltip, color)</summary>
        protected abstract IEnumerable<(string fieldName, string displayName, string shortName, string tooltip, Color color)> GetSubsectionConfigs();
        
        /// <summary>Shows the import window and calls onImport when user selects a source</summary>
        protected abstract void ShowImportWindow(SerializedProperty property, VisualElement root, Action<T> onImport);
        
        /// <summary>Copies data from source to property</summary>
        protected abstract void CopyFromSource(SerializedProperty property, T source, VisualElement root);
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Collapse State Management
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected static bool IsCollapsed(string propertyPath) => 
            !CollapsedStates.TryGetValue(propertyPath, out bool c) || c;
        
        protected static void SetCollapsed(string propertyPath, bool collapsed) => 
            CollapsedStates[propertyPath] = collapsed;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Entry Point
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "TagRequirementsRoot" };
            root.style.marginBottom = 4;
            BuildUI(root, property);
            return root;
        }
        
        protected void ScheduleRebuild(VisualElement root, SerializedProperty property)
        {
            var propPath = property.propertyPath;
            var targetObject = property.serializedObject.targetObject;
            if (RebuildingProperties.Contains(propPath)) return;
            RebuildingProperties.Add(propPath);
            root.schedule.Execute(() =>
            {
                RebuildingProperties.Remove(propPath);
                if (targetObject == null) return;
                var so = new SerializedObject(targetObject);
                var freshProp = so.FindProperty(propPath);
                if (freshProp != null) BuildUI(root, freshProp);
            });
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Main UI Builder
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildUI(VisualElement root, SerializedProperty property)
        {
            root.Clear();
            if (property?.serializedObject?.targetObject == null) return;
            
            bool isCollapsed = IsCollapsed(property.propertyPath);
            var nameProp = property.FindPropertyRelative("Name");
            string customName = nameProp?.stringValue ?? "";
            
            // Gather subsection data
            var subsections = GetSubsectionConfigs()
                .Select(cfg => (cfg, GetRequirementCounts(property.FindPropertyRelative(cfg.fieldName))))
                .ToList();
            
            // Main container
            var container = CreateMainContainer(isCollapsed);
            root.Add(container);
            
            // Header
            container.Add(CreateHeader(property, root, isCollapsed, customName, subsections));
            
            // Content
            if (isCollapsed)
                container.Add(CreateCollapsedSummary(subsections));
            else
                container.Add(CreateExpandedContent(property, root, customName, nameProp, subsections));
        }
        
        private VisualElement CreateMainContainer(bool isCollapsed)
        {
            var container = new VisualElement { name = "RequirementsContainer" };
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = AccentColor;
            container.style.borderTopWidth = 1;
            container.style.borderTopColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.5f);
            container.style.borderRightWidth = 1;
            container.style.borderRightColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.5f);
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.5f);
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
            return container;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Header
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateHeader(
            SerializedProperty property, 
            VisualElement root, 
            bool isCollapsed, 
            string customName,
            List<((string fieldName, string displayName, string shortName, string tooltip, Color color) cfg, (int req, int avoid) counts)> subsections)
        {
            var header = new VisualElement { name = "RequirementsHeader" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = isCollapsed ? 0 : 4;
            
            // Collapse button
            var collapseBtn = new Button { text = isCollapsed ? Icons.ChevronRight : Icons.ChevronDown };
            collapseBtn.tooltip = isCollapsed ? "Expand" : "Collapse";
            collapseBtn.style.width = 18;
            collapseBtn.style.height = 18;
            collapseBtn.style.marginRight = 4;
            collapseBtn.style.fontSize = 8;
            collapseBtn.style.paddingLeft = 0;
            collapseBtn.style.paddingRight = 0;
            collapseBtn.style.paddingTop = 0;
            collapseBtn.style.paddingBottom = 0;
            collapseBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            ApplyButtonStyle(collapseBtn);
            collapseBtn.clicked += () => { SetCollapsed(property.propertyPath, !isCollapsed); ScheduleRebuild(root, property); };
            header.Add(collapseBtn);
            
            // Label
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
            
            // Collapsed summary badges in header
            if (isCollapsed && false)  // todo fix if needed
            {
                bool hasAny = false;
                foreach (var (cfg, counts) in subsections)
                {
                    if (counts.req + counts.avoid > 0)
                    {
                        var badge = CreateSectionBadge(cfg.shortName, counts.req, counts.avoid, cfg.color);
                        if (hasAny) badge.style.marginLeft = 4;
                        header.Add(badge);
                        hasAny = true;
                    }
                }
                if (!hasAny)
                    header.Add(new Label("Empty") { style = { fontSize = 9, color = Colors.HintText, unityFontStyleAndWeight = FontStyle.Italic } });
            }
            
            // Import button
            var importBtn = new Button { text = "⬇", tooltip = "Import requirements from another asset" };
            importBtn.style.width = 22;
            importBtn.style.height = 20;
            importBtn.style.marginLeft = 8;
            importBtn.style.fontSize = 10;
            ApplyButtonStyle(importBtn);
            importBtn.clicked += () => ShowImportWindow(property, root, req => CopyFromSource(property, req, root));
            header.Add(importBtn);
            
            // Type indicator
            header.Add(CreateTypeIndicator());
            
            return header;
        }
        
        private VisualElement CreateTypeIndicator()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 8;
            container.style.paddingTop = 2;
            container.style.paddingBottom = 2;
            container.style.marginLeft = 4;
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.borderTopWidth = 1;
            container.style.borderBottomWidth = 1;
            container.style.borderLeftWidth = 1;
            container.style.borderRightWidth = 1;
            container.style.borderTopColor = AccentColor;
            container.style.borderBottomColor = AccentColor;
            container.style.borderLeftColor = AccentColor;
            container.style.borderRightColor = AccentColor;
            container.style.backgroundColor = new Color(0.18f, 0.20f, 0.22f, 0.8f);
            
            container.Add(new Label("◆") { style = { color = AccentColor, fontSize = 10, marginRight = 4 } });
            container.Add(new Label(TypeIndicatorText) { style = { color = Colors.LabelText, fontSize = 11 } });
            
            return container;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Collapsed Summary
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateCollapsedSummary(
            List<((string fieldName, string displayName, string shortName, string tooltip, Color color) cfg, (int req, int avoid) counts)> subsections)
        {
            var summary = new VisualElement { name = "CollapsedSummary" };
            summary.style.flexDirection = FlexDirection.Row;
            summary.style.alignItems = Align.Center;
            summary.style.marginTop = 4;
            summary.style.paddingTop = 4;
            summary.style.paddingBottom = 2;
            summary.style.borderTopWidth = 1;
            summary.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            
            bool hasAny = false;
            foreach (var (cfg, counts) in subsections)
            {
                if (counts.req + counts.avoid > 0)
                {
                    var badge = CreateSectionBadge(cfg.displayName, counts.req, counts.avoid, cfg.color);
                    if (hasAny) badge.style.marginLeft = 6;
                    summary.Add(badge);
                    hasAny = true;
                }
            }
            
            if (!hasAny)
            {
                summary.Add(new Label("No requirements defined") { style = { fontSize = 10, color = Colors.HintText, unityFontStyleAndWeight = FontStyle.Italic } });
            }
            
            return summary;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Expanded Content
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateExpandedContent(
            SerializedProperty property, 
            VisualElement root, 
            string customName, 
            SerializedProperty nameProp,
            List<((string fieldName, string displayName, string shortName, string tooltip, Color color) cfg, (int req, int avoid) counts)> subsections)
        {
            var content = new VisualElement { name = "RequirementsContent" };
            content.style.paddingTop = 4;
            
            // Name field
            var nameRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 } };
            nameRow.Add(new Label("Name") { style = { width = 50, fontSize = 10, color = Colors.HintText } });
            var nameField = new TextField { value = customName, style = { flexGrow = 1 } };
            nameField.tooltip = "Optional name for this requirement group (helps identify when importing)";
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
            
            // Subsections
            bool first = true;
            foreach (var (cfg, counts) in subsections)
            {
                var prop = property.FindPropertyRelative(cfg.fieldName);
                var section = CreateRequirementSubsection(cfg.displayName, cfg.tooltip, cfg.color, prop, counts);
                if (!first) section.style.marginTop = 4;
                content.Add(section);
                first = false;
            }
            
            content.Bind(property.serializedObject);
            return content;
        }
        
        private VisualElement CreateRequirementSubsection(string title, string tooltip, Color color, SerializedProperty prop, (int req, int avoid) counts)
        {
            var section = new VisualElement();
            section.style.marginTop = 4;
            section.style.marginBottom = 2;
            section.style.paddingLeft = 6;
            section.style.paddingTop = 4;
            section.style.paddingBottom = 4;
            section.style.paddingRight = 4;
            section.style.borderLeftWidth = 2;
            section.style.borderLeftColor = color;
            section.style.backgroundColor = Colors.SubsectionBackground;
            section.style.borderTopLeftRadius = 2;
            section.style.borderTopRightRadius = 2;
            section.style.borderBottomLeftRadius = 2;
            section.style.borderBottomRightRadius = 2;
            
            var headerRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            headerRow.Add(new Label(title) { tooltip = tooltip, style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, color = Colors.LabelText } });
            headerRow.Add(new VisualElement { style = { flexGrow = 1 } });
            
            if (counts.req > 0) headerRow.Add(CreateCountBadge($"+{counts.req}", Colors.AccentGreen));
            if (counts.avoid > 0)
            {
                var avoidBadge = CreateCountBadge($"-{counts.avoid}", Colors.AccentRed);
                avoidBadge.style.marginLeft = 4;
                headerRow.Add(avoidBadge);
            }
            if (counts.req == 0 && counts.avoid == 0)
                headerRow.Add(new Label("—") { style = { fontSize = 9, color = Colors.HintText } });
            
            section.Add(headerRow);
            
            var field = new PropertyField(prop, "");
            field.style.marginTop = 2;
            section.Add(field);
            section.Bind(prop.serializedObject);
            
            return section;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Helper Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        protected (int req, int avoid) GetRequirementCounts(SerializedProperty groupProp)
        {
            if (groupProp == null) return (0, 0);
            return (
                groupProp.FindPropertyRelative("RequireTags")?.arraySize ?? 0,
                groupProp.FindPropertyRelative("AvoidTags")?.arraySize ?? 0
            );
        }
        
        protected Label CreateBadge(string text, Color color)
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
        
        protected Label CreateCountBadge(string text, Color color)
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
        
        protected VisualElement CreateSectionBadge(string label, int reqCount, int avoidCount, Color accentColor)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.paddingLeft = 4;
            container.style.paddingRight = 4;
            container.style.paddingTop = 2;
            container.style.paddingBottom = 2;
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.backgroundColor = new Color(accentColor.r, accentColor.g, accentColor.b, 0.15f);
            
            container.Add(new Label(label) { style = { fontSize = 9, color = accentColor, marginRight = 2 } });
            if (reqCount > 0) container.Add(new Label($"+{reqCount}") { style = { fontSize = 9, color = Colors.AccentGreen } });
            if (avoidCount > 0) container.Add(new Label($"-{avoidCount}") { style = { fontSize = 9, color = Colors.AccentRed, marginLeft = reqCount > 0 ? 2 : 0 } });
            
            return container;
        }
        
        protected static void ApplyButtonStyle(Button btn)
        {
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.backgroundColor = Colors.ButtonBackground;
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // EffectTagRequirements Drawer
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(EffectTagRequirements))]
    public class EffectTagRequirementsDrawer : AbstractTagRequirementsDrawer<EffectTagRequirements>
    {
        protected override Color AccentColor => Colors.AssetRequirements;
        protected override string TypeIndicatorText => "Effect Requirements";
        
        protected override IEnumerable<(string fieldName, string displayName, string shortName, string tooltip, Color color)> GetSubsectionConfigs()
        {
            yield return ("ApplicationRequirements", "Apply", "Apply", "Required to apply effect", Colors.BorderLight);
            yield return ("OngoingRequirements", "Ongoing", "Ongoing", "Must remain true while active", Colors.BorderLight);
            yield return ("RemovalRequirements", "Remove", "Remove", "Triggers removal when met", Colors.BorderLight);
        }
        
        protected override void ShowImportWindow(SerializedProperty property, VisualElement root, Action<EffectTagRequirements> onImport)
        {
            EffectTagRequirementsImportWindow.Show(property, root, onImport);
        }
        
        protected override void CopyFromSource(SerializedProperty property, EffectTagRequirements source, VisualElement root)
        {
            AvoidRequireTagGroupHelper.CopyAvoidRequireGroup(property.FindPropertyRelative("ApplicationRequirements"), source.ApplicationRequirements);
            AvoidRequireTagGroupHelper.CopyAvoidRequireGroup(property.FindPropertyRelative("OngoingRequirements"), source.OngoingRequirements);
            AvoidRequireTagGroupHelper.CopyAvoidRequireGroup(property.FindPropertyRelative("RemovalRequirements"), source.RemovalRequirements);
            property.serializedObject.ApplyModifiedProperties();
            ScheduleRebuild(root, property);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // AbilityTagRequirements Drawer
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AbilityTagRequirements))]
    public class AbilityTagRequirementsDrawer : AbstractTagRequirementsDrawer<AbilityTagRequirements>
    {
        private static readonly Color AbilityReqColor = new Color(0.4f, 0.7f, 1f);
        
        protected override Color AccentColor => AbilityReqColor;
        protected override string TypeIndicatorText => "Ability Requirements";
        
        protected override IEnumerable<(string fieldName, string displayName, string shortName, string tooltip, Color color)> GetSubsectionConfigs()
        {
            yield return ("SourceRequirements", "Source", "Src", "Source requirements", Colors.BorderLight);
            yield return ("TargetRequirements", "Target", "Tgt", "Target requirements", Colors.BorderLight);
        }
        
        protected override void ShowImportWindow(SerializedProperty property, VisualElement root, Action<AbilityTagRequirements> onImport)
        {
            AbilityTagRequirementsImportWindow.Show(property, root, onImport);
        }
        
        protected override void CopyFromSource(SerializedProperty property, AbilityTagRequirements source, VisualElement root)
        {
            var nameProp = property.FindPropertyRelative("Name");
            if (nameProp != null && !string.IsNullOrEmpty(source.Name))
                nameProp.stringValue = source.Name;
            AvoidRequireTagGroupHelper.CopyAvoidRequireGroup(property.FindPropertyRelative("SourceRequirements"), source.SourceRequirements);
            AvoidRequireTagGroupHelper.CopyAvoidRequireGroup(property.FindPropertyRelative("TargetRequirements"), source.TargetRequirements);
            property.serializedObject.ApplyModifiedProperties();
            ScheduleRebuild(root, property);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // Helper class for copying AvoidRequireTagGroup
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public static class AvoidRequireTagGroupHelper
    {
        public static void CopyAvoidRequireGroup(SerializedProperty prop, AvoidRequireTagGroup source)
        {
            if (prop == null || source == null) return;
            
            var nameProp = prop.FindPropertyRelative("Name");
            if (nameProp != null && !string.IsNullOrEmpty(source.Name))
                nameProp.stringValue = source.Name;
            
            CopyTagList(prop.FindPropertyRelative("RequireTags"), source.RequireTags);
            CopyTagList(prop.FindPropertyRelative("AvoidTags"), source.AvoidTags);
        }
        
        private static void CopyTagList(SerializedProperty listProp, List<TagQuery> source)
        {
            if (listProp == null || source == null) return;
            listProp.arraySize = source.Count;
            for (int i = 0; i < source.Count; i++)
            {
                var elemProp = listProp.GetArrayElementAtIndex(i);
                var srcElem = source[i];
                var tagProp = elemProp.FindPropertyRelative("Tag");
                var opProp = elemProp.FindPropertyRelative("Operator");
                var magProp = elemProp.FindPropertyRelative("Magnitude");
                if (tagProp != null) tagProp.boxedValue = srcElem.Tag;
                if (opProp != null) opProp.enumValueIndex = (int)srcElem.Operator;
                if (magProp != null) magProp.intValue = srcElem.Magnitude;
            }
        }
        
        public static (int req, int avoid) GetCounts(AvoidRequireTagGroup group)
        {
            if (group == null) return (0, 0);
            return (group.RequireTags?.Count ?? 0, group.AvoidTags?.Count ?? 0);
        }
        
        public static bool HasAnyTags(AvoidRequireTagGroup group)
        {
            if (group == null) return false;
            var (req, avoid) = GetCounts(group);
            return req > 0 || avoid > 0;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // Import Windows
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class EffectTagRequirementsImportWindow : EditorWindow
    {
        private Action<EffectTagRequirements> _onImport;
        private List<(GameplayEffect effect, EffectTagRequirements reqs, string label, bool hasName)> _sources = new();
        private string _searchFilter = "";
        private bool _showNamedFirst = true;
        
        public static void Show(SerializedProperty targetProp, VisualElement root, Action<EffectTagRequirements> onImport)
        {
            var window = GetWindow<EffectTagRequirementsImportWindow>(true, "Import Effect Tag Requirements");
            window._onImport = onImport;
            window.minSize = new Vector2(450, 400);
            window.maxSize = new Vector2(650, 550);
            window.RefreshSources();
            window.ShowUtility();
        }
        
        private void RefreshSources()
        {
            _sources.Clear();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(GameplayEffect)}"))
            {
                var effect = AssetDatabase.LoadAssetAtPath<GameplayEffect>(AssetDatabase.GUIDToAssetPath(guid));
                if (effect != null)
                {
                    AddReqs(effect, effect.SourceRequirements, "Source");
                    AddReqs(effect, effect.TargetRequirements, "Target");
                }
            }
        }
        
        private void AddReqs(GameplayEffect effect, EffectTagRequirements reqs, string ctx)
        {
            if (reqs == null) return;
            bool hasAny = AvoidRequireTagGroupHelper.HasAnyTags(reqs.ApplicationRequirements) ||
                          AvoidRequireTagGroupHelper.HasAnyTags(reqs.OngoingRequirements) ||
                          AvoidRequireTagGroupHelper.HasAnyTags(reqs.RemovalRequirements);
            if (!hasAny) return;
            
            bool hasName = (reqs.ApplicationRequirements?.HasName ?? false) || 
                           (reqs.OngoingRequirements?.HasName ?? false) || 
                           (reqs.RemovalRequirements?.HasName ?? false);
            string name = reqs.ApplicationRequirements?.Name ?? reqs.OngoingRequirements?.Name ?? reqs.RemovalRequirements?.Name ?? "";
            string label = !string.IsNullOrEmpty(name) ? $"{name} — {effect.name} ({ctx})" : $"{effect.name} ({ctx})";
            _sources.Add((effect, reqs, label, hasName));
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            
            root.Add(new Label("Import Effect Tag Requirements") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, color = Colors.AccentCyan, marginBottom = 8 } });
            
            var optRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            var toggle = new Toggle("Show named first") { value = _showNamedFirst };
            toggle.RegisterValueChangedCallback(evt => { _showNamedFirst = evt.newValue; RebuildList(); });
            optRow.Add(toggle);
            root.Add(optRow);
            
            var searchRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            var searchField = new TextField { value = _searchFilter, style = { flexGrow = 1 } };
            searchField.RegisterValueChangedCallback(evt => { _searchFilter = evt.newValue; RebuildList(); });
            searchRow.Add(searchField);
            searchRow.Add(new Button(() => { RefreshSources(); RebuildList(); }) { text = "↻", style = { width = 24, marginLeft = 4 } });
            root.Add(searchRow);
            
            root.Add(new ScrollView { name = "ResultsList", style = { flexGrow = 1, backgroundColor = Colors.SubsectionBackground, borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4 } });
            root.Add(new Label { name = "Footer", style = { fontSize = 10, color = Colors.HintText, marginTop = 8 } });
            
            root.schedule.Execute(() => { if (_sources.Count == 0) RefreshSources(); RebuildList(); UpdateFooter(); });
        }
        
        private void UpdateFooter()
        {
            var f = rootVisualElement?.Q<Label>("Footer");
            if (f != null) f.text = $"{_sources.Count} effects ({_sources.Count(s => s.hasName)} named)";
        }
        
        private void RebuildList()
        {
            var sv = rootVisualElement?.Q<ScrollView>("ResultsList");
            if (sv == null) return;
            sv.Clear();
            
            var filtered = string.IsNullOrEmpty(_searchFilter) 
                ? _sources 
                : _sources.Where(s => s.label.ToLower().Contains(_searchFilter.ToLower())).ToList();
            filtered = _showNamedFirst 
                ? filtered.OrderByDescending(s => s.hasName).ThenBy(s => s.label).ToList() 
                : filtered.OrderBy(s => s.label).ToList();
            
            foreach (var (effect, reqs, label, hasName) in filtered)
            {
                var item = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 8, paddingRight = 8, paddingTop = 4, paddingBottom = 4, marginLeft = 4, marginRight = 4, marginBottom = 2, backgroundColor = Colors.ItemBackground, borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3 } };
                if (hasName) { item.style.borderLeftWidth = 2; item.style.borderLeftColor = Colors.AccentGreen; }
                item.RegisterCallback<MouseEnterEvent>(_ => item.style.backgroundColor = Colors.ButtonHover);
                item.RegisterCallback<MouseLeaveEvent>(_ => item.style.backgroundColor = Colors.ItemBackground);
                item.Add(new Label(label) { style = { flexGrow = 1, color = hasName ? Colors.LabelText : Colors.HintText, fontSize = 11 } });
                item.Add(new Button(() => { _onImport?.Invoke(reqs); Close(); }) { text = "Import", style = { paddingLeft = 8, paddingRight = 8, height = 18, fontSize = 10 } });
                sv.Add(item);
            }
            
            if (filtered.Count == 0)
                sv.Add(new Label("No effects found") { style = { color = Colors.HintText, unityTextAlign = TextAnchor.MiddleCenter, paddingTop = 20 } });
        }
    }
    
    public class AbilityTagRequirementsImportWindow : EditorWindow
    {
        private Action<AbilityTagRequirements> _onImport;
        private List<(Ability ability, string label, bool hasName)> _sources = new();
        private string _searchFilter = "";
        private bool _showNamedFirst = true;
        private static readonly Color AbilityReqColor = new Color(0.4f, 0.7f, 1f);
        
        public static void Show(SerializedProperty targetProp, VisualElement root, Action<AbilityTagRequirements> onImport)
        {
            var window = GetWindow<AbilityTagRequirementsImportWindow>(true, "Import Ability Requirements");
            window._onImport = onImport;
            window.minSize = new Vector2(450, 400);
            window.maxSize = new Vector2(650, 550);
            window.RefreshSources();
            window.ShowUtility();
        }
        
        private void RefreshSources()
        {
            _sources.Clear();
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(Ability)}"))
            {
                var ability = AssetDatabase.LoadAssetAtPath<Ability>(AssetDatabase.GUIDToAssetPath(guid));
                if (ability?.Tags?.TagRequirements != null)
                {
                    var reqs = ability.Tags.TagRequirements;
                    if (AvoidRequireTagGroupHelper.HasAnyTags(reqs.SourceRequirements) || 
                        AvoidRequireTagGroupHelper.HasAnyTags(reqs.TargetRequirements))
                    {
                        bool hasName = reqs.HasName;
                        _sources.Add((ability, hasName ? $"{reqs.Name} — {ability.name}" : ability.name, hasName));
                    }
                }
            }
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            
            root.Add(new Label("Import Ability Requirements") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, color = AbilityReqColor, marginBottom = 8 } });
            
            var optRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            var toggle = new Toggle("Show named first") { value = _showNamedFirst };
            toggle.RegisterValueChangedCallback(evt => { _showNamedFirst = evt.newValue; RebuildList(); });
            optRow.Add(toggle);
            root.Add(optRow);
            
            var searchRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            var searchField = new TextField { value = _searchFilter, style = { flexGrow = 1 } };
            searchField.RegisterValueChangedCallback(evt => { _searchFilter = evt.newValue; RebuildList(); });
            searchRow.Add(searchField);
            searchRow.Add(new Button(() => { RefreshSources(); RebuildList(); }) { text = "↻", style = { width = 24, marginLeft = 4 } });
            root.Add(searchRow);
            
            root.Add(new ScrollView { name = "ResultsList", style = { flexGrow = 1, backgroundColor = Colors.SubsectionBackground, borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4 } });
            root.Add(new Label { name = "Footer", style = { fontSize = 10, color = Colors.HintText, marginTop = 8 } });
            
            root.schedule.Execute(() => { if (_sources.Count == 0) RefreshSources(); RebuildList(); UpdateFooter(); });
        }
        
        private void UpdateFooter()
        {
            var f = rootVisualElement?.Q<Label>("Footer");
            if (f != null) f.text = $"{_sources.Count} abilities ({_sources.Count(s => s.hasName)} named)";
        }
        
        private void RebuildList()
        {
            var sv = rootVisualElement?.Q<ScrollView>("ResultsList");
            if (sv == null) return;
            sv.Clear();
            
            var filtered = string.IsNullOrEmpty(_searchFilter) 
                ? _sources 
                : _sources.Where(s => s.label.ToLower().Contains(_searchFilter.ToLower())).ToList();
            filtered = _showNamedFirst 
                ? filtered.OrderByDescending(s => s.hasName).ThenBy(s => s.label).ToList() 
                : filtered.OrderBy(s => s.label).ToList();
            
            foreach (var (ability, label, hasName) in filtered)
            {
                var item = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 8, paddingRight = 8, paddingTop = 4, paddingBottom = 4, marginLeft = 4, marginRight = 4, marginBottom = 2, backgroundColor = Colors.ItemBackground, borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3 } };
                if (hasName) { item.style.borderLeftWidth = 2; item.style.borderLeftColor = Colors.AccentGreen; }
                item.RegisterCallback<MouseEnterEvent>(_ => item.style.backgroundColor = Colors.ButtonHover);
                item.RegisterCallback<MouseLeaveEvent>(_ => item.style.backgroundColor = Colors.ItemBackground);
                item.Add(new Label("⚡") { style = { color = AbilityReqColor, fontSize = 12, marginRight = 6 } });
                item.Add(new Label(label) { style = { flexGrow = 1, color = hasName ? Colors.LabelText : Colors.HintText, fontSize = 11 } });
                item.Add(new Button(() => { _onImport?.Invoke(ability.Tags.TagRequirements); Close(); }) { text = "Import", style = { paddingLeft = 8, paddingRight = 8, height = 18, fontSize = 10 } });
                sv.Add(item);
            }
            
            if (filtered.Count == 0)
                sv.Add(new Label("No abilities found") { style = { color = Colors.HintText, unityTextAlign = TextAnchor.MiddleCenter, paddingTop = 20 } });
        }
    }
    
    public class AvoidRequireTagGroupImportWindow : EditorWindow
    {
        private Action<AvoidRequireTagGroup> _onImport;
        private List<(UnityEngine.Object asset, AvoidRequireTagGroup group, string label, string type, bool hasName, bool isTemplate)> _sources = new();
        private string _searchFilter = "";
        private bool _showNamedFirst = true;
        
        public static void Show(SerializedProperty targetProp, VisualElement root, Action<AvoidRequireTagGroup> onImport)
        {
            var window = GetWindow<AvoidRequireTagGroupImportWindow>(true, "Import Requirements");
            window._onImport = onImport;
            window.minSize = new Vector2(450, 400);
            window.maxSize = new Vector2(650, 550);
            window.RefreshSources();
            window.ShowUtility();
        }
        
        private void RefreshSources()
        {
            _sources.Clear();
            
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(RequirementTemplate)}"))
            {
                var template = AssetDatabase.LoadAssetAtPath<RequirementTemplate>(AssetDatabase.GUIDToAssetPath(guid));
                if (template?.Requirements != null && AvoidRequireTagGroupHelper.HasAnyTags(template.Requirements))
                {
                    bool hasName = template.Requirements.HasName;
                    string label = hasName ? $"{template.Requirements.Name} — {template.name}" : template.name;
                    _sources.Add((template, template.Requirements, label, "Template", hasName, true));
                }
            }
            
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(Ability)}"))
            {
                var ability = AssetDatabase.LoadAssetAtPath<Ability>(AssetDatabase.GUIDToAssetPath(guid));
                if (ability?.Tags?.TagRequirements != null)
                {
                    var reqs = ability.Tags.TagRequirements;
                    AddGroup(ability, reqs.SourceRequirements, $"{ability.name} → Source", "Ability");
                    AddGroup(ability, reqs.TargetRequirements, $"{ability.name} → Target", "Ability");
                }
            }
            
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(GameplayEffect)}"))
            {
                var effect = AssetDatabase.LoadAssetAtPath<GameplayEffect>(AssetDatabase.GUIDToAssetPath(guid));
                if (effect != null)
                {
                    AddEffectGroups(effect, effect.SourceRequirements, "Source");
                    AddEffectGroups(effect, effect.TargetRequirements, "Target");
                }
            }
        }
        
        private void AddGroup(UnityEngine.Object asset, AvoidRequireTagGroup g, string suffix, string type)
        {
            if (!AvoidRequireTagGroupHelper.HasAnyTags(g)) return;
            bool hasName = g?.HasName ?? false;
            string label = hasName ? $"{g.Name} — {suffix}" : suffix;
            _sources.Add((asset, g, label, type, hasName, false));
        }
        
        private void AddEffectGroups(GameplayEffect effect, EffectTagRequirements reqs, string ctx)
        {
            if (reqs == null) return;
            AddGroup(effect, reqs.ApplicationRequirements, $"{effect.name} → {ctx} → Application", "Effect");
            AddGroup(effect, reqs.OngoingRequirements, $"{effect.name} → {ctx} → Ongoing", "Effect");
            AddGroup(effect, reqs.RemovalRequirements, $"{effect.name} → {ctx} → Removal", "Effect");
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            
            root.Add(new Label("Import Requirements") { style = { fontSize = 14, unityFontStyleAndWeight = FontStyle.Bold, color = new Color(0.95f, 0.6f, 0.2f), marginBottom = 8 } });
            
            var optRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            var toggle = new Toggle("Show named first") { value = _showNamedFirst };
            toggle.RegisterValueChangedCallback(evt => { _showNamedFirst = evt.newValue; RebuildList(); });
            optRow.Add(toggle);
            root.Add(optRow);
            
            var searchRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            var searchField = new TextField { value = _searchFilter, style = { flexGrow = 1 } };
            searchField.RegisterValueChangedCallback(evt => { _searchFilter = evt.newValue; RebuildList(); });
            searchRow.Add(searchField);
            searchRow.Add(new Button(() => { RefreshSources(); RebuildList(); }) { text = "↻", style = { width = 24, marginLeft = 4 } });
            root.Add(searchRow);
            
            root.Add(new ScrollView { name = "ResultsList", style = { flexGrow = 1, backgroundColor = Colors.SubsectionBackground, borderTopLeftRadius = 4, borderTopRightRadius = 4, borderBottomLeftRadius = 4, borderBottomRightRadius = 4 } });
            root.Add(new Label { name = "Footer", style = { fontSize = 10, color = Colors.HintText, marginTop = 8 } });
            
            root.schedule.Execute(() => { if (_sources.Count == 0) RefreshSources(); RebuildList(); UpdateFooter(); });
        }
        
        private void UpdateFooter()
        {
            var f = rootVisualElement?.Q<Label>("Footer");
            if (f != null)
            {
                int templateCount = _sources.Count(s => s.isTemplate);
                f.text = templateCount > 0
                    ? $"{_sources.Count} groups ({templateCount} templates, {_sources.Count(s => s.hasName)} named)"
                    : $"{_sources.Count} groups ({_sources.Count(s => s.hasName)} named)";
            }
        }
        
        private void RebuildList()
        {
            var sv = rootVisualElement?.Q<ScrollView>("ResultsList");
            if (sv == null) return;
            sv.Clear();
            
            var filtered = string.IsNullOrEmpty(_searchFilter)
                ? _sources
                : _sources.Where(s => s.label.ToLower().Contains(_searchFilter.ToLower())).ToList();
            
            filtered = filtered
                .OrderByDescending(s => s.isTemplate)
                .ThenByDescending(s => _showNamedFirst && s.hasName)
                .ThenBy(s => s.type)
                .ThenBy(s => s.label)
                .ToList();
            
            foreach (var (asset, group, label, type, hasName, isTemplate) in filtered)
            {
                var item = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 8, paddingRight = 8, paddingTop = 4, paddingBottom = 4, marginLeft = 4, marginRight = 4, marginBottom = 2, backgroundColor = Colors.ItemBackground, borderTopLeftRadius = 3, borderTopRightRadius = 3, borderBottomLeftRadius = 3, borderBottomRightRadius = 3 } };
                
                if (isTemplate)
                {
                    item.style.borderLeftWidth = 3;
                    item.style.borderLeftColor = Colors.AccentCyan;
                    item.style.backgroundColor = new Color(Colors.AccentCyan.r, Colors.AccentCyan.g, Colors.AccentCyan.b, 0.1f);
                }
                else if (hasName)
                {
                    item.style.borderLeftWidth = 2;
                    item.style.borderLeftColor = Colors.AccentGreen;
                }
                
                item.RegisterCallback<MouseEnterEvent>(_ => item.style.backgroundColor = Colors.ButtonHover);
                item.RegisterCallback<MouseLeaveEvent>(_ => item.style.backgroundColor = isTemplate 
                    ? new Color(Colors.AccentCyan.r, Colors.AccentCyan.g, Colors.AccentCyan.b, 0.1f) 
                    : Colors.ItemBackground);
                
                string icon = isTemplate ? "📋" : (type == "Ability" ? "⚡" : "✦");
                Color iconColor = isTemplate ? Colors.AccentCyan : (type == "Ability" ? new Color(0.4f, 0.7f, 1f) : new Color(0.6f, 0.4f, 0.9f));
                item.Add(new Label(icon) { style = { color = iconColor, fontSize = 12, marginRight = 6 } });
                
                item.Add(new Label(label) { style = { flexGrow = 1, color = (hasName || isTemplate) ? Colors.LabelText : Colors.HintText, fontSize = 10 } });
                
                if (isTemplate)
                    item.Add(new Label("Template") { style = { fontSize = 9, color = Colors.AccentCyan, marginRight = 6 } });
                
                item.Add(new Button(() => { _onImport?.Invoke(group); Close(); }) { text = "Import", style = { paddingLeft = 8, paddingRight = 8, height = 18, fontSize = 10 } });
                sv.Add(item);
            }
            
            if (filtered.Count == 0)
                sv.Add(new Label("No groups found") { style = { color = Colors.HintText, unityTextAlign = TextAnchor.MiddleCenter, paddingTop = 20 } });
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // AvoidRequireContainer and AvoidRequireTagGroup Drawers
    // ═══════════════════════════════════════════════════════════════════════════════

    [CustomPropertyDrawer(typeof(AvoidRequireTagGroup))]
    public class AvoidRequireTagGroupDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, bool> _collapsedStates = new();
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
            root.style.marginBottom = 4;
            BuildUI(root, property);
            return root;
        }
        
        private void ScheduleRebuild(VisualElement root, SerializedProperty property)
        {
            var propPath = property.propertyPath;
            var targetObject = property.serializedObject.targetObject;
            if (_rebuildingProperties.Contains(propPath)) return;
            _rebuildingProperties.Add(propPath);
            root.schedule.Execute(() =>
            {
                _rebuildingProperties.Remove(propPath);
                if (targetObject == null) return;
                var so = new SerializedObject(targetObject);
                var freshProp = so.FindProperty(propPath);
                if (freshProp != null) BuildUI(root, freshProp);
            });
        }
        
        private void BuildUI(VisualElement root, SerializedProperty property)
        {
            root.Clear();
            if (property?.serializedObject?.targetObject == null) return;
            
            bool isCollapsed = !_collapsedStates.TryGetValue(property.propertyPath, out bool c) || c;
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
            collapseBtn.clicked += () => { _collapsedStates[property.propertyPath] = !isCollapsed; ScheduleRebuild(root, property); };
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
            
            var importBtn = new Button { text = "⬇" };
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
            importBtn.clicked += () => AvoidRequireTagGroupImportWindow.Show(property, root, g =>
            {
                AvoidRequireTagGroupHelper.CopyAvoidRequireGroup(property, g);
                property.serializedObject.ApplyModifiedProperties();
                ScheduleRebuild(root, property);
            });
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