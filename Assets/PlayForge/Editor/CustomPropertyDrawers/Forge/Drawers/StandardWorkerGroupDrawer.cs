using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Custom drawer for StandardWorkerGroup.
    /// Styled to match Requirements and Scaler drawers.
    /// </summary>
    [CustomPropertyDrawer(typeof(StandardWorkerGroup))]
    public class StandardWorkerGroupDrawer : PropertyDrawer
    {
        private static Dictionary<string, bool> _collapsedStates = new Dictionary<string, bool>();
        private static HashSet<string> _rebuildingProperties = new HashSet<string>();
        
        private static readonly Color WorkerGroupAccent = Colors.AccentGreen;
        
        private static bool IsCollapsed(string propertyPath) => !_collapsedStates.TryGetValue(propertyPath, out bool c) || c;
        private static void SetCollapsed(string propertyPath, bool collapsed) => _collapsedStates[propertyPath] = collapsed;
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "WorkerGroupRoot" };
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
            
            bool isCollapsed = IsCollapsed(property.propertyPath);
            
            var nameProp = property.FindPropertyRelative("Name");
            string customName = nameProp?.stringValue ?? "";
            var counts = GetTotalWorkerCount(property);
            
            // Main container with border styling
            var container = new VisualElement { name = "WorkerGroupContainer" };
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = WorkerGroupAccent;
            container.style.borderTopWidth = 1;
            container.style.borderTopColor = new Color(WorkerGroupAccent.r, WorkerGroupAccent.g, WorkerGroupAccent.b, 0.5f);
            container.style.borderRightWidth = 1;
            container.style.borderRightColor = new Color(WorkerGroupAccent.r, WorkerGroupAccent.g, WorkerGroupAccent.b, 0.5f);
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = new Color(WorkerGroupAccent.r, WorkerGroupAccent.g, WorkerGroupAccent.b, 0.5f);
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
            var header = new VisualElement { name = "WorkerGroupHeader" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = isCollapsed ? 0 : 4;
            container.Add(header);
            
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
            
            // Title - show custom name if set, otherwise "Worker Group"
            if (!string.IsNullOrEmpty(customName) && customName != "Workers")
            {
                header.Add(new Label(customName) { style = { color = Colors.LabelText, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11, marginRight = 6 } });
                header.Add(CreateBadge("Worker Group", Colors.HintText));
            }
            else
            {
                header.Add(new Label("Worker Group") { style = { color = Colors.LabelText, unityFontStyleAndWeight = FontStyle.Bold, fontSize = 11 } });
            }
            
            // Spacer
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            
            // Count badge
            if (counts.total > 0) 
            {
                header.Add(CreateBadge(counts.attr.ToString(), GetWorkerTypeColor("Attribute"), "Attribute Workers"));
                header.Add(CreateBadge(counts.impact.ToString(), GetWorkerTypeColor("Impact"), "Impact Workers"));
                header.Add(CreateBadge(counts.tags.ToString(), GetWorkerTypeColor("Tag"), "Tag Workers"));
                header.Add(CreateBadge(counts.analysis.ToString(), GetWorkerTypeColor("Analysis"), "Analysis Workers"));
            }
            else if (isCollapsed)
            {
                header.Add(new Label("Empty") { style = { fontSize = 9, color = Colors.HintText, unityFontStyleAndWeight = FontStyle.Italic } });
            }
            
            // Import button
            var importBtn = new Button { text = "⬇", tooltip = "Import workers from another asset" };
            importBtn.style.width = 22;
            importBtn.style.height = 20;
            importBtn.style.marginLeft = 8;
            importBtn.style.fontSize = 10;
            ApplyButtonStyle(importBtn);
            importBtn.clicked += () => WorkerGroupImportWindow.Show(property, root, group => CopyWorkerGroup(property, group, root));
            header.Add(importBtn);
            
            // Type indicator
            header.Add(CreateTypeIndicator("Workers", WorkerGroupAccent));
            
            // Content (when expanded)
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
                
                // Worker type sections
                content.Add(CreateWorkerTypeSection(property, "AttributeWorkers", "Attribute", GetWorkerTypeColor("Attribute")));
                
                var impactSection = CreateWorkerTypeSection(property, "ImpactWorkers", "Impact", GetWorkerTypeColor("Impact"));
                impactSection.style.marginTop = 6;
                content.Add(impactSection);
                
                var tagSection = CreateWorkerTypeSection(property, "TagWorkers", "Tag", GetWorkerTypeColor("Tag"));
                tagSection.style.marginTop = 6;
                content.Add(tagSection);
                
                var analysisSection = CreateWorkerTypeSection(property, "AnalysisWorkers", "Analysis", GetWorkerTypeColor("Analysis"));
                analysisSection.style.marginTop = 6;
                content.Add(analysisSection);
                
                container.Bind(property.serializedObject);
            }
        }

        private Color GetWorkerTypeColor(string workerType)
        {
            return workerType switch
            {
                "Attribute" => Colors.AccentOrange,
                "Impact" => Colors.AccentRed,
                "Tag" => Colors.AccentBlue,
                "Analysis" => Colors.AccentGreen,
                _ => Colors.AccentGray
            };
        }
        
        private void CopyWorkerGroup(SerializedProperty property, StandardWorkerGroup source, VisualElement root)
        {
            if (source == null) return;
            
            // Copy Name
            var nameProp = property.FindPropertyRelative("Name");
            if (nameProp != null) nameProp.stringValue = source.Name;
            
            // Copy worker arrays
            CopyWorkerArray(property.FindPropertyRelative("AttributeWorkers"), source.AttributeWorkers);
            CopyWorkerArray(property.FindPropertyRelative("ImpactWorkers"), source.ImpactWorkers);
            CopyWorkerArray(property.FindPropertyRelative("TagWorkers"), source.TagWorkers);
            CopyWorkerArray(property.FindPropertyRelative("AnalysisWorkers"), source.AnalysisWorkers);
            
            property.serializedObject.ApplyModifiedProperties();
            ScheduleRebuild(root, property);
        }
        
        private void CopyWorkerArray<T>(SerializedProperty arrayProp, List<T> sourceList) where T : class
        {
            if (arrayProp == null || sourceList == null) return;
            
            arrayProp.ClearArray();
            for (int i = 0; i < sourceList.Count; i++)
            {
                arrayProp.InsertArrayElementAtIndex(i);
                var element = arrayProp.GetArrayElementAtIndex(i);
                element.managedReferenceValue = sourceList[i];
            }
        }
        
        private static void ApplyButtonStyle(Button btn)
        {
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.backgroundColor = Colors.ButtonBackground;
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
        }
        
        private (int attr, int impact, int tags, int analysis, int total) GetTotalWorkerCount(SerializedProperty property)
        {
            var value = (GetArrayCount(property, "AttributeWorkers"),
                GetArrayCount(property, "ImpactWorkers"),
                GetArrayCount(property, "TagWorkers"),
                GetArrayCount(property, "AnalysisWorkers"),
                0);
            value.Item5 = value.Item1 + value.Item2 + value.Item3 + value.Item4;
            return value;
        }
        
        private int GetArrayCount(SerializedProperty property, string arrayName)
        {
            var array = property.FindPropertyRelative(arrayName);
            return array?.arraySize ?? 0;
        }
        
        private VisualElement CreateTypeIndicator(string text, Color color)
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
            container.style.borderTopColor = color;
            container.style.borderBottomColor = color;
            container.style.borderLeftColor = color;
            container.style.borderRightColor = color;
            container.style.backgroundColor = new Color(0.18f, 0.20f, 0.22f, 0.8f);
            
            var icon = new Label("⚙");
            icon.style.fontSize = 10;
            icon.style.marginRight = 4;
            icon.style.color = color;
            container.Add(icon);
            
            var label = new Label(text);
            label.style.fontSize = 10;
            label.style.color = Colors.LabelText;
            container.Add(label);
            
            return container;
        }
        
        private Label CreateBadge(string text, Color color, string tooltip = null)
        {
            var badge = new Label(text);
            badge.style.fontSize = 9;
            badge.style.color = color;
            badge.style.backgroundColor = new Color(color.r, color.g, color.b, 0.15f);
            badge.style.paddingLeft = 4;
            badge.style.paddingRight = 4;
            badge.style.paddingTop = 1;
            badge.style.paddingBottom = 1;
            badge.style.marginLeft = 3;
            badge.style.borderTopLeftRadius = 4;
            badge.style.borderTopRightRadius = 4;
            badge.style.borderBottomLeftRadius = 4;
            badge.style.borderBottomRightRadius = 4;
            badge.tooltip = tooltip;
            return badge;
        }
        
        private VisualElement CreateWorkerTypeSection(
            SerializedProperty property, 
            string propertyName, 
            string displayName,
            Color accentColor)
        {
            var arrayProp = property.FindPropertyRelative(propertyName);
            if (arrayProp == null) return new VisualElement();
            
            var section = new VisualElement();
            section.style.paddingLeft = 4;
            section.style.paddingTop = 2;
            section.style.paddingBottom = 2;
            section.style.borderLeftWidth = 2;
            section.style.borderLeftColor = accentColor;
            section.style.backgroundColor = Colors.SubsectionBackground;
            section.style.borderTopLeftRadius = 2;
            section.style.borderTopRightRadius = 2;
            section.style.borderBottomLeftRadius = 2;
            section.style.borderBottomRightRadius = 2;
            
            // Section header
            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 } };
            header.Add(new Label(displayName) { style = { fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, color = Colors.LabelText } });
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            
            int count = arrayProp.arraySize;
            if (count > 0)
            {
                //header.Add(new Label($"({count})") { style = { fontSize = 9, color = accentColor } });
            }
            section.Add(header);
            
            // Property field for the list
            var field = new PropertyField(arrayProp, "");
            field.style.marginTop = 2;
            field.BindProperty(arrayProp);
            section.Add(field);
            
            return section;
        }
    }
    
    /// <summary>
    /// Import window for StandardWorkerGroup.
    /// Searches for assets with worker groups and allows importing.
    /// </summary>
    public class WorkerGroupImportWindow : EditorWindow
    {
        private Action<StandardWorkerGroup> _onImport;
        private List<(UnityEngine.Object asset, StandardWorkerGroup group, string label, string type, bool hasName)> _sources = new();
        private string _searchFilter = "";
        private bool _showNamedFirst = true;
        
        public static void Show(SerializedProperty targetProp, VisualElement root, Action<StandardWorkerGroup> onImport)
        {
            var window = GetWindow<WorkerGroupImportWindow>(true, "Import Worker Group");
            window._onImport = onImport;
            window.minSize = new Vector2(450, 400);
            window.maxSize = new Vector2(650, 550);
            window.RefreshSources();
            window.ShowUtility();
        }
        
        private void RefreshSources()
        {
            _sources.Clear();
            
            // Search EntityIdentity assets
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(EntityIdentity)}"))
            {
                var entity = AssetDatabase.LoadAssetAtPath<EntityIdentity>(AssetDatabase.GUIDToAssetPath(guid));
                if (entity?.WorkerGroup != null && HasAnyWorkers(entity.WorkerGroup))
                {
                    AddSource(entity, entity.WorkerGroup, entity.name, "Entity");
                }
            }
            
            // Search Ability assets
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(Ability)}"))
            {
                var ability = AssetDatabase.LoadAssetAtPath<Ability>(AssetDatabase.GUIDToAssetPath(guid));
                if (ability?.WorkerGroup != null && HasAnyWorkers(ability.WorkerGroup))
                {
                    AddSource(ability, ability.WorkerGroup, ability.name, "Ability");
                }
            }
            
            // Search Item assets
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(Item)}"))
            {
                var item = AssetDatabase.LoadAssetAtPath<Item>(AssetDatabase.GUIDToAssetPath(guid));
                if (item?.WorkerGroup != null && HasAnyWorkers(item.WorkerGroup))
                {
                    AddSource(item, item.WorkerGroup, item.name, "Item");
                }
            }
            
            // Search AttributeSet assets
            foreach (var guid in AssetDatabase.FindAssets($"t:{nameof(AttributeSet)}"))
            {
                var attrSet = AssetDatabase.LoadAssetAtPath<AttributeSet>(AssetDatabase.GUIDToAssetPath(guid));
                if (attrSet?.WorkerGroup != null && HasAnyWorkers(attrSet.WorkerGroup))
                {
                    AddSource(attrSet, attrSet.WorkerGroup, attrSet.name, "AttributeSet");
                }
            }
        }
        
        private void AddSource(UnityEngine.Object asset, StandardWorkerGroup group, string assetName, string type)
        {
            bool hasName = !string.IsNullOrEmpty(group.Name) && group.Name != "Workers";
            string label = hasName ? $"{group.Name} — {assetName}" : assetName;
            _sources.Add((asset, group, label, type, hasName));
        }
        
        private bool HasAnyWorkers(StandardWorkerGroup group)
        {
            if (group == null) return false;
            return (group.AttributeWorkers?.Count ?? 0) > 0 ||
                   (group.ImpactWorkers?.Count ?? 0) > 0 ||
                   (group.TagWorkers?.Count ?? 0) > 0 ||
                   (group.AnalysisWorkers?.Count ?? 0) > 0;
        }
        
        private int GetWorkerCount(StandardWorkerGroup group)
        {
            if (group == null) return 0;
            return (group.AttributeWorkers?.Count ?? 0) +
                   (group.ImpactWorkers?.Count ?? 0) +
                   (group.TagWorkers?.Count ?? 0) +
                   (group.AnalysisWorkers?.Count ?? 0);
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 10;
            root.style.paddingRight = 10;
            root.style.paddingTop = 10;
            root.style.paddingBottom = 10;
            
            // Title
            root.Add(new Label("Import Worker Group") 
            { 
                style = 
                { 
                    fontSize = 14, 
                    unityFontStyleAndWeight = FontStyle.Bold, 
                    color = Colors.AccentCyan, 
                    marginBottom = 8 
                } 
            });
            
            // Options row
            var optRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            var toggle = new Toggle("Show named first") { value = _showNamedFirst };
            toggle.RegisterValueChangedCallback(evt => { _showNamedFirst = evt.newValue; RebuildList(); });
            optRow.Add(toggle);
            root.Add(optRow);
            
            // Search row
            var searchRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 8 } };
            var searchField = new TextField { value = _searchFilter, style = { flexGrow = 1 } };
            searchField.RegisterValueChangedCallback(evt => { _searchFilter = evt.newValue; RebuildList(); });
            searchRow.Add(searchField);
            searchRow.Add(new Button(() => { RefreshSources(); RebuildList(); }) { text = "↻", style = { width = 24, marginLeft = 4 } });
            root.Add(searchRow);
            
            // Results list
            root.Add(new ScrollView 
            { 
                name = "ResultsList", 
                style = 
                { 
                    flexGrow = 1, 
                    backgroundColor = Colors.SubsectionBackground, 
                    borderTopLeftRadius = 4, 
                    borderTopRightRadius = 4, 
                    borderBottomLeftRadius = 4, 
                    borderBottomRightRadius = 4 
                } 
            });
            
            // Footer
            root.Add(new Label { name = "Footer", style = { fontSize = 10, color = Colors.HintText, marginTop = 8 } });
            
            root.schedule.Execute(() => 
            { 
                if (_sources.Count == 0) RefreshSources(); 
                RebuildList(); 
                UpdateFooter(); 
            });
        }
        
        private void UpdateFooter()
        {
            var footer = rootVisualElement?.Q<Label>("Footer");
            if (footer != null)
            {
                int namedCount = _sources.Count(s => s.hasName);
                footer.text = $"{_sources.Count} worker groups ({namedCount} named)";
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
            
            filtered = _showNamedFirst 
                ? filtered.OrderByDescending(s => s.hasName).ThenBy(s => s.type).ThenBy(s => s.label).ToList() 
                : filtered.OrderBy(s => s.type).ThenBy(s => s.label).ToList();
            
            foreach (var (asset, group, label, type, hasName) in filtered)
            {
                var item = new VisualElement 
                { 
                    style = 
                    { 
                        flexDirection = FlexDirection.Row, 
                        alignItems = Align.Center, 
                        paddingLeft = 8, 
                        paddingRight = 8, 
                        paddingTop = 4, 
                        paddingBottom = 4, 
                        marginLeft = 4, 
                        marginRight = 4, 
                        marginBottom = 2, 
                        backgroundColor = Colors.ItemBackground, 
                        borderTopLeftRadius = 3, 
                        borderTopRightRadius = 3, 
                        borderBottomLeftRadius = 3, 
                        borderBottomRightRadius = 3 
                    } 
                };
                
                if (hasName) 
                { 
                    item.style.borderLeftWidth = 2; 
                    item.style.borderLeftColor = Colors.AccentGreen; 
                }
                
                item.RegisterCallback<MouseEnterEvent>(_ => item.style.backgroundColor = Colors.ButtonHover);
                item.RegisterCallback<MouseLeaveEvent>(_ => item.style.backgroundColor = Colors.ItemBackground);
                
                // Type icon
                string icon = type switch
                {
                    "Entity" => "◆",
                    "Ability" => "⚡",
                    "Item" => "◈",
                    "AttributeSet" => "◉",
                    _ => "●"
                };
                Color iconColor = type switch
                {
                    "Entity" => new Color(0.3f, 0.8f, 0.6f),
                    "Ability" => new Color(0.4f, 0.7f, 1f),
                    "Item" => new Color(1f, 0.8f, 0.3f),
                    "AttributeSet" => new Color(0.9f, 0.7f, 0.3f),
                    _ => Colors.HintText
                };
                item.Add(new Label(icon) { style = { color = iconColor, fontSize = 12, marginRight = 6 } });
                
                // Label
                item.Add(new Label(label) { style = { flexGrow = 1, color = hasName ? Colors.LabelText : Colors.HintText, fontSize = 10 } });
                
                // Type badge
                item.Add(new Label(type) { style = { fontSize = 9, color = Colors.HintText, marginRight = 6 } });
                
                // Worker count
                int count = GetWorkerCount(group);
                item.Add(new Label($"({count})") { style = { fontSize = 9, color = Colors.AccentCyan, marginRight = 8 } });
                
                // Import button
                item.Add(new Button(() => { _onImport?.Invoke(group); Close(); }) 
                { 
                    text = "Import", 
                    style = { paddingLeft = 8, paddingRight = 8, height = 18, fontSize = 10 } 
                });
                
                sv.Add(item);
            }
            
            if (filtered.Count == 0) 
            {
                sv.Add(new Label("No worker groups found") 
                { 
                    style = { color = Colors.HintText, unityTextAlign = TextAnchor.MiddleCenter, paddingTop = 20 } 
                });
            }
        }
    }
}