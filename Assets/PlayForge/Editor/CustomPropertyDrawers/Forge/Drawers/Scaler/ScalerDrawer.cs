using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// UIElements property drawer for AbstractScaler and derived types.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractScaler), true)]
    public partial class ScalerDrawer : PropertyDrawer
    {
        // Cached scaler types
        private static Type[] _scalerTypes;
        private static string[] _scalerTypeNames;
        private static HashSet<string> _rebuildingProperties = new HashSet<string>();
        
        // Collapse state per property path (persists across rebuilds)
        private static Dictionary<string, bool> _collapsedStates = new Dictionary<string, bool>();
        
        // Quick fill expanded state
        private static Dictionary<string, bool> _quickFillExpanded = new Dictionary<string, bool>();
        private static Dictionary<string, EQuickFillMode> _quickFillMode = new Dictionary<string, EQuickFillMode>();
        
        private enum EQuickFillMode { Linear, Additive, Steps }
        
        // Quick fill parameters (per property)
        private static Dictionary<string, QuickFillParams> _quickFillParams = new Dictionary<string, QuickFillParams>();
        
        private class QuickFillParams
        {
            public float StartValue = 1f;
            public float EndValue = 10f;
            public float BaseValue = 1f;
            public float Increment = 1f;
            public int StepCount = 5;
        }
        
        private static bool IsCollapsed(string propertyPath)
        {
            return !_collapsedStates.TryGetValue(propertyPath, out bool collapsed) || collapsed;
        }
        
        private static void SetCollapsed(string propertyPath, bool collapsed)
        {
            _collapsedStates[propertyPath] = collapsed;
        }
        
        static ScalerDrawer()
        {
            CacheScalerTypes();
        }
        
        private static void CacheScalerTypes()
        {
            var types = new List<Type> { null };
            var names = new List<string> { "(None)" };
            
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var type in assembly.GetTypes()
                        .Where(t => typeof(AbstractScaler).IsAssignableFrom(t) && !t.IsAbstract)
                        .OrderBy(t => t.Name))
                    {
                        types.Add(type);
                        names.Add(FormatTypeName(type.Name));
                    }
                }
                catch { }
            }
            
            _scalerTypes = types.ToArray();
            _scalerTypeNames = names.ToArray();
        }
        
        private static string FormatTypeName(string n)
        {
            if (n.EndsWith("Scaler")) n = n.Substring(0, n.Length - 6);
            var sb = new System.Text.StringBuilder();
            foreach (char c in n) { if (char.IsUpper(c) && sb.Length > 0) sb.Append(' '); sb.Append(c); }
            return sb.ToString();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Linked Source Detection
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Attempts to find the parent GameplayEffect from the serialized property.
        /// </summary>
        private static GameplayEffect FindParentEffect(SerializedProperty property)
        {
            // Check if the target object is a GameplayEffect
            if (property.serializedObject.targetObject is GameplayEffect effect)
            {
                return effect;
            }
            return null;
        }
        
        /// <summary>
        /// Gets the linked level provider from the parent effect, if any.
        /// </summary>
        private static ILevelProvider GetLinkedProvider(SerializedProperty property)
        {
            var effect = FindParentEffect(property);
            if (effect != null && effect.IsLinked)
            {
                return effect.LinkedProvider;
            }
            return null;
        }
        
        /// <summary>
        /// Gets the max level to use for LockToLevelProvider mode.
        /// Returns the linked provider's max level if available, otherwise returns a default.
        /// </summary>
        private static int GetLinkedMaxLevel(SerializedProperty property, int defaultValue = 1)
        {
            var provider = GetLinkedProvider(property);
            return provider?.GetMaxLevel() ?? defaultValue;
        }
        
        /// <summary>
        /// Checks if the parent effect has a linked level provider.
        /// </summary>
        private static bool HasLinkedProvider(SerializedProperty property)
        {
            return GetLinkedProvider(property) != null;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Entry Point
        // ═══════════════════════════════════════════════════════════════════════════
        
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "ScalerRoot" };
            root.style.marginBottom = 4;
            BuildScalerUI(root, property);
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
                if (freshProp != null) BuildScalerUI(root, freshProp);
            });
        }
        
        private void BuildScalerUI(VisualElement root, SerializedProperty property)
        {
            root.Clear();
            if (property?.serializedObject?.targetObject == null) return;
            
            var container = new VisualElement { name = "ScalerContainer" };
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = Colors.AccentPurple;
            container.style.borderTopLeftRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.backgroundColor = Colors.SectionBackground;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 6;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            container.style.marginTop = 2;
            StyleSpecialContainer(container, Colors.AccentPurple);
            root.Add(container);
            
            bool isCollapsed = IsCollapsed(property.propertyPath);
            container.Add(CreateHeader(property, root, isCollapsed));
            
            if (property.managedReferenceValue != null)
            {
                if (isCollapsed)
                {
                    // Show condensed summary when collapsed
                    container.Add(CreateCollapsedSummary(property));
                }
                else
                {
                    // Show full content when expanded
                    container.Add(CreateContent(property, root));
                }
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Header
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateHeader(SerializedProperty property, VisualElement root, bool isCollapsed)
        {
            var header = new VisualElement { name = "ScalerHeader" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = isCollapsed ? 0 : 4;
            
            // Collapse toggle button
            var collapseBtn = new Button { text = isCollapsed ? Icons.ChevronRight : Icons.ChevronDown, tooltip = isCollapsed ? "Expand" : "Collapse" };
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
            collapseBtn.clicked += () =>
            {
                SetCollapsed(property.propertyPath, !isCollapsed);
                ScheduleRebuild(root, property);
            };
            header.Add(collapseBtn);
            
            var label = new Label(property.displayName);
            label.style.flexGrow = 1;
            label.style.color = Colors.LabelText;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.fontSize = 11;
            header.Add(label);
            
            var importBtn = new Button { text = "⬇", tooltip = "Import scaler from another asset" };
            importBtn.style.width = 22;
            importBtn.style.height = 20;
            importBtn.style.marginRight = 4;
            importBtn.style.fontSize = 10;
            ApplyButtonStyle(importBtn);
            importBtn.clicked += () => ScalerImportWindow.Show(property, root, s => CopyScaler(property, s, root));
            header.Add(importBtn);
            
            var dropdownContainer = CreateDropdownContainer();
            int currentIndex = 0;
            if (property.managedReferenceValue != null)
            {
                currentIndex = Array.IndexOf(_scalerTypes, property.managedReferenceValue.GetType());
                if (currentIndex < 0) currentIndex = 0;
            }
            
            var dropdown = new PopupField<string>(_scalerTypeNames.ToList(), currentIndex, s => s, s => s);
            dropdown.style.minWidth = 110;
            
            int lastIndex = currentIndex;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int newIndex = Array.IndexOf(_scalerTypeNames, evt.newValue);
                if (newIndex == lastIndex) return;
                lastIndex = newIndex;
                
                if (newIndex <= 0)
                {
                    property.managedReferenceValue = null;
                }
                else
                {
                    var newScaler = Activator.CreateInstance(_scalerTypes[newIndex]) as AbstractScaler;
                    property.managedReferenceValue = newScaler;
                    property.serializedObject.ApplyModifiedProperties();
                    
                    var lvp = property.FindPropertyRelative("LevelValues");
                    var maxLvl = property.FindPropertyRelative("MaxLevel");
                    if (lvp != null && lvp.arraySize == 0)
                    {
                        int size = (maxLvl != null && maxLvl.intValue > 0) ? maxLvl.intValue : 10;
                        if (maxLvl != null) maxLvl.intValue = size;
                        lvp.arraySize = size;
                        for (int i = 0; i < size; i++)
                            lvp.GetArrayElementAtIndex(i).floatValue = 1f;
                    }
                }
                
                property.serializedObject.ApplyModifiedProperties();
                ScheduleRebuild(root, property);
            });
            
            dropdownContainer.Add(dropdown);
            header.Add(dropdownContainer);
            return header;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Content
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateContent(SerializedProperty property, VisualElement root)
        {
            var content = new VisualElement { name = "ScalerContent" };
            content.style.paddingTop = 4;
            
            var scaler = property.managedReferenceValue as AbstractScaler;
            if (scaler == null) return content;
            
            var type = scaler.GetType();
            
            // Initialize MaxLevel
            var maxLevelProp = property.FindPropertyRelative("MaxLevel");
            if (maxLevelProp != null && maxLevelProp.intValue <= 0)
            {
                maxLevelProp.intValue = 10;
                property.serializedObject.ApplyModifiedProperties();
            }
            
            // Check config and linked provider
            var configProp = property.FindPropertyRelative("Configuration");
            var currentConfig = configProp != null ? (ELevelConfig)configProp.enumValueIndex : ELevelConfig.Unlocked;
            
            // For LockToLevelProvider, sync with linked source if available
            if (currentConfig == ELevelConfig.LockToLevelProvider)
            {
                int linkedMax = GetLinkedMaxLevel(property, 1);
                var lvpCheck = property.FindPropertyRelative("LevelValues");
                if (lvpCheck != null && lvpCheck.arraySize != linkedMax)
                {
                    ResizeLevelValues(property, scaler, linkedMax);
                    property.serializedObject.ApplyModifiedProperties();
                }
            }
            else
            {
                // Sync LevelValues array for other modes
                var lvpCheck = property.FindPropertyRelative("LevelValues");
                if (lvpCheck != null && maxLevelProp != null)
                {
                    int targetSize = maxLevelProp.intValue;
                    if (lvpCheck.arraySize != targetSize)
                    {
                        int oldSize = lvpCheck.arraySize;
                        float lastVal = oldSize > 0 ? lvpCheck.GetArrayElementAtIndex(oldSize - 1).floatValue : 1f;
                        lvpCheck.arraySize = targetSize;
                        for (int i = oldSize; i < targetSize; i++)
                            lvpCheck.GetArrayElementAtIndex(i).floatValue = lastVal;
                        property.serializedObject.ApplyModifiedProperties();
                    }
                }
            }
            
            // Type-specific properties (defined in partial class)
            AddTypeSpecificProperties(content, property, type, root);

            if (scaler.UseScalingOptions())
            {
                // Level Mode
                content.Add(CreateLevelModeRow(property, root, scaler));
            
                // Max Level (conditional - show for non-locked modes, or show linked info for locked mode)
                if (currentConfig == ELevelConfig.LockToLevelProvider)
                {
                    content.Add(CreateLinkedLevelInfoRow(property));
                }
                else
                {
                    content.Add(CreateMaxLevelRow(property, root, scaler));
                }
            
                // Interpolation
                content.Add(CreateInterpolationRow(property, root, scaler));
            
                // Quick Fill (revamped)
                content.Add(CreateQuickFillSection(property, root));
            
                // Level Values Grid
                content.Add(CreateLevelValuesGrid(property, scaler, root));
            
                // Curve Preview
                content.Add(CreateCurvePreview(property, scaler));
            }
            
            // Behaviours Section
            content.Add(CreateBehavioursSection(property, root));
            
            return content;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Collapsed Summary
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateCollapsedSummary(SerializedProperty property)
        {
            var summary = new VisualElement { name = "CollapsedSummary" };
            summary.style.flexDirection = FlexDirection.Row;
            summary.style.alignItems = Align.Center;
            summary.style.marginTop = 4;
            summary.style.paddingTop = 4;
            summary.style.paddingBottom = 2;
            summary.style.borderTopWidth = 1;
            summary.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            
            var scaler = property.managedReferenceValue as AbstractScaler;
            if (scaler == null) return summary;
            
            // Get level mode
            var configProp = property.FindPropertyRelative("Configuration");
            var config = configProp != null ? (ELevelConfig)configProp.enumValueIndex : ELevelConfig.Unlocked;
            string modeText = config switch
            {
                ELevelConfig.LockToLevelProvider => "Lock",
                ELevelConfig.Unlocked => "Unlk",
                ELevelConfig.Partitioned => "Part",
                _ => "?"
            };
            
            // Get max level - use linked provider for LockToLevelProvider
            int maxLevel;
            bool isLinked = false;
            if (config == ELevelConfig.LockToLevelProvider)
            {
                var provider = GetLinkedProvider(property);
                if (provider != null)
                {
                    maxLevel = provider.GetMaxLevel();
                    isLinked = true;
                }
                else
                {
                    maxLevel = 1;
                }
            }
            else
            {
                var maxLevelProp = property.FindPropertyRelative("MaxLevel");
                maxLevel = maxLevelProp?.intValue ?? 1;
            }
            
            // Get min/max values from LevelValues
            var lvp = property.FindPropertyRelative("LevelValues");
            float minVal = 0f, maxVal = 0f;
            if (lvp != null && lvp.arraySize > 0)
            {
                minVal = float.MaxValue;
                maxVal = float.MinValue;
                for (int i = 0; i < lvp.arraySize; i++)
                {
                    float v = lvp.GetArrayElementAtIndex(i).floatValue;
                    minVal = Mathf.Min(minVal, v);
                    maxVal = Mathf.Max(maxVal, v);
                }
            }
            
            // Level mode badge
            var modeBadge = new Label(modeText);
            modeBadge.style.fontSize = 9;
            modeBadge.style.color = isLinked ? Colors.AccentGreen : Colors.AccentPurple;
            modeBadge.style.backgroundColor = isLinked 
                ? new Color(0.3f, 0.5f, 0.3f, 0.3f) 
                : new Color(0.4f, 0.3f, 0.5f, 0.3f);
            modeBadge.style.paddingLeft = 4;
            modeBadge.style.paddingRight = 4;
            modeBadge.style.paddingTop = 1;
            modeBadge.style.paddingBottom = 1;
            modeBadge.style.borderTopLeftRadius = 3;
            modeBadge.style.borderTopRightRadius = 3;
            modeBadge.style.borderBottomLeftRadius = 3;
            modeBadge.style.borderBottomRightRadius = 3;
            modeBadge.style.marginRight = 8;
            modeBadge.tooltip = GetLevelModeTooltip(config) + (isLinked ? " (Linked to provider)" : "");
            summary.Add(modeBadge);
            
            // Linked indicator
            if (isLinked)
            {
                var linkIcon = new Label("🔗");
                linkIcon.style.fontSize = 9;
                linkIcon.style.marginRight = 4;
                linkIcon.tooltip = "Using linked level provider";
                summary.Add(linkIcon);
            }
            
            // Max level
            var levelLabel = new Label($"Lv{maxLevel.ToString()}");
            levelLabel.style.fontSize = 10;
            levelLabel.style.color = isLinked ? Colors.AccentGreen : Colors.HintText;
            levelLabel.style.marginRight = 8;
            levelLabel.tooltip = isLinked ? "Max Level (from linked provider)" : "Max Level";
            summary.Add(levelLabel);
            
            // Value range
            string rangeText = "";
            if (lvp != null && lvp.arraySize > 0)
            {
                int frontRange = Mathf.Min(2, lvp.arraySize);
                int endRange = Mathf.Min(2, lvp.arraySize - 2);
                int midRange = lvp.arraySize - frontRange - endRange;

                for (int i = 0; i < frontRange; i++)
                {
                    rangeText += $"{lvp.GetArrayElementAtIndex(i).floatValue:G4} → ";
                }

                if (midRange > 0)
                {
                    rangeText += "... → ";
                }

                for (int i = frontRange + midRange; i < lvp.arraySize; i++)
                {
                    rangeText += $"{lvp.GetArrayElementAtIndex(i).floatValue:G4} ";
                    if (i < lvp.arraySize - 1) rangeText += "→ ";
                }
            }
            
            var rangeLabel = new Label(rangeText.TrimEnd());
            rangeLabel.style.fontSize = 10;
            rangeLabel.style.color = Colors.HintText;
            rangeLabel.style.overflow = Overflow.Hidden;
            rangeLabel.style.textOverflow = TextOverflow.Ellipsis;
            rangeLabel.style.flexGrow = 1;
            rangeLabel.tooltip = $"Values from level 1 to {maxLevel}";
            summary.Add(rangeLabel);
            
            // Behaviours count badge
            var behavioursProp = property.FindPropertyRelative("Behaviours");
            if (behavioursProp != null && behavioursProp.arraySize > 0)
            {
                var behaviourBadge = new Label($"+{behavioursProp.arraySize}");
                behaviourBadge.style.fontSize = 9;
                behaviourBadge.style.color = Colors.AccentOrange;
                behaviourBadge.style.backgroundColor = new Color(0.5f, 0.4f, 0.3f, 0.3f);
                behaviourBadge.style.paddingLeft = 4;
                behaviourBadge.style.paddingRight = 4;
                behaviourBadge.style.paddingTop = 1;
                behaviourBadge.style.paddingBottom = 1;
                behaviourBadge.style.borderTopLeftRadius = 3;
                behaviourBadge.style.borderTopRightRadius = 3;
                behaviourBadge.style.borderBottomLeftRadius = 3;
                behaviourBadge.style.borderBottomRightRadius = 3;
                behaviourBadge.style.marginLeft = 4;
                behaviourBadge.tooltip = $"{behavioursProp.arraySize} behaviour(s) applied";
                summary.Add(behaviourBadge);
            }
            
            return summary;
        }
        
        private static string GetLevelModeTooltip(ELevelConfig config)
        {
            return config switch
            {
                ELevelConfig.LockToLevelProvider => "Lock to Level Provider: Uses the linked provider's level range",
                ELevelConfig.Unlocked => "Unlocked: Uses its own max level setting",
                ELevelConfig.Partitioned => "Partitioned: Uses minimum of max level and provider's current level",
                _ => ""
            };
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Level Mode Row
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateLevelModeRow(SerializedProperty property, VisualElement root, AbstractScaler scaler)
        {
            var row = CreateRow(4);
            var configProp = property.FindPropertyRelative("Configuration");
            
            var label = new Label("Level Mode");
            label.style.width = 80;
            label.style.color = Colors.LabelText;
            row.Add(label);
            
            var currentConfig = configProp != null ? (ELevelConfig)configProp.enumValueIndex : ELevelConfig.Unlocked;
            var field = new EnumField(currentConfig);
            field.style.flexGrow = 1;
            field.tooltip = GetLevelModeTooltip(currentConfig);
            
            field.RegisterValueChangedCallback(evt =>
            {
                if (configProp != null)
                    configProp.enumValueIndex = Convert.ToInt32(evt.newValue);
                
                var newConfig = (ELevelConfig)evt.newValue;
                field.tooltip = GetLevelModeTooltip(newConfig);
                
                // If switching to LockToLevelProvider, resize to linked max
                if (newConfig == ELevelConfig.LockToLevelProvider)
                {
                    int linkedMax = GetLinkedMaxLevel(property, 1);
                    ResizeLevelValues(property, scaler, linkedMax);
                }
                
                property.serializedObject.ApplyModifiedProperties();
                ScheduleRebuild(root, property);
            });
            
            row.Add(field);
            return row;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Linked Level Info Row (for LockToLevelProvider mode)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateLinkedLevelInfoRow(SerializedProperty property)
        {
            var row = CreateRow(4);
            
            var label = new Label("Max Level");
            label.style.width = 80;
            label.style.color = Colors.LabelText;
            row.Add(label);
            
            var provider = GetLinkedProvider(property);
            
            if (provider != null)
            {
                // Show linked provider info
                var infoBox = new VisualElement();
                infoBox.style.flexDirection = FlexDirection.Row;
                infoBox.style.alignItems = Align.Center;
                infoBox.style.flexGrow = 1;
                infoBox.style.paddingLeft = 6;
                infoBox.style.paddingRight = 6;
                infoBox.style.paddingTop = 3;
                infoBox.style.paddingBottom = 3;
                infoBox.style.backgroundColor = new Color(0.2f, 0.3f, 0.2f, 0.4f);
                infoBox.style.borderTopLeftRadius = 3;
                infoBox.style.borderTopRightRadius = 3;
                infoBox.style.borderBottomLeftRadius = 3;
                infoBox.style.borderBottomRightRadius = 3;
                infoBox.style.borderLeftWidth = 2;
                infoBox.style.borderLeftColor = Colors.AccentGreen;
                
                var linkIcon = new Label("🔗");
                linkIcon.style.fontSize = 10;
                linkIcon.style.marginRight = 4;
                infoBox.Add(linkIcon);
                
                var levelText = new Label($"{provider.GetMaxLevel()}");
                levelText.style.fontSize = 11;
                levelText.style.color = Colors.AccentGreen;
                levelText.style.unityFontStyleAndWeight = FontStyle.Bold;
                levelText.style.marginRight = 8;
                infoBox.Add(levelText);
                
                var providerName = new Label($"from {provider.GetProviderName()}");
                providerName.style.fontSize = 10;
                providerName.style.color = Colors.HintText;
                providerName.style.flexGrow = 1;
                infoBox.Add(providerName);
                
                // Navigate button
                var effect = FindParentEffect(property);
                if (effect != null && effect.LinkedProvider != null)
                {
                    var gotoBtn = new Button(() =>
                    {
                        Selection.activeObject = effect.LinkedProvider;
                        EditorGUIUtility.PingObject(effect.LinkedProvider);
                    });
                    gotoBtn.text = "→";
                    gotoBtn.tooltip = "Go to linked provider";
                    gotoBtn.style.width = 20;
                    gotoBtn.style.height = 16;
                    gotoBtn.style.fontSize = 10;
                    gotoBtn.style.paddingLeft = 0;
                    gotoBtn.style.paddingRight = 0;
                    ApplyButtonStyle(gotoBtn);
                    infoBox.Add(gotoBtn);
                }
                
                row.Add(infoBox);
            }
            else
            {
                // No linked provider - show warning
                var warningBox = new VisualElement();
                warningBox.style.flexDirection = FlexDirection.Row;
                warningBox.style.alignItems = Align.Center;
                warningBox.style.flexGrow = 1;
                warningBox.style.paddingLeft = 6;
                warningBox.style.paddingRight = 6;
                warningBox.style.paddingTop = 3;
                warningBox.style.paddingBottom = 3;
                warningBox.style.backgroundColor = new Color(0.3f, 0.25f, 0.2f, 0.4f);
                warningBox.style.borderTopLeftRadius = 3;
                warningBox.style.borderTopRightRadius = 3;
                warningBox.style.borderBottomLeftRadius = 3;
                warningBox.style.borderBottomRightRadius = 3;
                warningBox.style.borderLeftWidth = 2;
                warningBox.style.borderLeftColor = Colors.AccentYellow;
                
                var warnIcon = new Label("⚠");
                warnIcon.style.fontSize = 10;
                warnIcon.style.color = Colors.AccentYellow;
                warnIcon.style.marginRight = 4;
                warningBox.Add(warnIcon);
                
                var warnText = new Label("No linked provider - using 1 level");
                warnText.style.fontSize = 10;
                warnText.style.color = Colors.AccentYellow;
                warnText.style.flexGrow = 1;
                warningBox.Add(warnText);
                
                // Link button
                var effect = FindParentEffect(property);
                if (effect != null)
                {
                    var linkBtn = new Button(() =>
                    {
                        Selection.activeObject = effect;
                        EditorGUIUtility.PingObject(effect);
                    });
                    linkBtn.text = "Link...";
                    linkBtn.tooltip = "Open effect to configure level source";
                    linkBtn.style.height = 16;
                    linkBtn.style.fontSize = 9;
                    linkBtn.style.paddingLeft = 4;
                    linkBtn.style.paddingRight = 4;
                    ApplyButtonStyle(linkBtn);
                    warningBox.Add(linkBtn);
                }
                
                row.Add(warningBox);
            }
            
            return row;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Max Level Row (for non-locked modes)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateMaxLevelRow(SerializedProperty property, VisualElement root, AbstractScaler scaler)
        {
            var row = CreateRow(4);
            var maxLevelProp = property.FindPropertyRelative("MaxLevel");
            
            var label = new Label("Max Level");
            label.style.width = 80;
            label.style.color = Colors.LabelText;
            row.Add(label);
            
            int currentMax = maxLevelProp?.intValue ?? 10;
            
            var field = new IntegerField { value = currentMax };
            field.style.width = 50;
            field.tooltip = "Number of levels";
            
            var slider = new SliderInt(1, 100) { value = Mathf.Clamp(currentMax, 1, 100) };
            slider.style.flexGrow = 1;
            slider.style.marginLeft = 8;
            
            // Field: apply on blur or Enter
            field.RegisterCallback<FocusOutEvent>(_ => ApplyMaxLevel(property, scaler, field.value, slider, root));
            field.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    ApplyMaxLevel(property, scaler, field.value, slider, root);
            });
            row.Add(field);
            
            // Slider: update live, rebuild on pointer release
            slider.RegisterValueChangedCallback(evt =>
            {
                field.SetValueWithoutNotify(evt.newValue);
                if (maxLevelProp != null) maxLevelProp.intValue = evt.newValue;
                ResizeLevelValues(property, scaler, evt.newValue);
                property.serializedObject.ApplyModifiedProperties();
            });
            
            // Rebuild when slider drag ends
            slider.RegisterCallback<PointerCaptureOutEvent>(_ => ScheduleRebuild(root, property));
            row.Add(slider);
            
            return row;
        }
        
        private void ApplyMaxLevel(SerializedProperty property, AbstractScaler scaler, int value, SliderInt slider, VisualElement root)
        {
            int val = Mathf.Clamp(value, 1, 999);
            var maxLevelProp = property.FindPropertyRelative("MaxLevel");
            if (maxLevelProp != null) maxLevelProp.intValue = val;
            ResizeLevelValues(property, scaler, val);
            property.serializedObject.ApplyModifiedProperties();
            slider.SetValueWithoutNotify(Mathf.Clamp(val, 1, 100));
            ScheduleRebuild(root, property);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Interpolation Row
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateInterpolationRow(SerializedProperty property, VisualElement root, AbstractScaler scaler)
        {
            var row = CreateRow(4);
            var interpProp = property.FindPropertyRelative("Interpolation");
            
            var label = new Label("Interpolation");
            label.style.width = 80;
            label.style.color = Colors.LabelText;
            row.Add(label);
            
            var currentInterp = interpProp != null ? (EScalerInterpolation)interpProp.enumValueIndex : scaler.Interpolation;
            var field = new EnumField(currentInterp);
            field.style.flexGrow = 1;
            field.tooltip = GetInterpolationTooltip(currentInterp);
            
            field.RegisterValueChangedCallback(evt =>
            {
                var newInterp = (EScalerInterpolation)evt.newValue;
                if (interpProp != null)
                    interpProp.enumValueIndex = Convert.ToInt32(evt.newValue);
                
                field.tooltip = GetInterpolationTooltip(newInterp);
                
                // Pass new interpolation directly - key fix!
                RegenerateCurveWithInterpolation(property, scaler, newInterp);
                property.serializedObject.ApplyModifiedProperties();
                ScheduleRebuild(root, property);
            });
            
            row.Add(field);
            return row;
        }
        
        private static string GetInterpolationTooltip(EScalerInterpolation interp)
        {
            return interp switch
            {
                EScalerInterpolation.Constant => "Step function - no interpolation between levels",
                EScalerInterpolation.Linear => "Linear interpolation between level values",
                EScalerInterpolation.Smooth => "Smooth curve through level values",
                _ => "Interpolation mode"
            };
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Quick Fill Section (Revamped)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateQuickFillSection(SerializedProperty property, VisualElement root)
        {
            var section = new VisualElement { name = "QuickFillSection" };
            section.style.marginTop = 4;
            section.style.marginBottom = 4;
            
            var propPath = property.propertyPath;
            bool isExpanded = _quickFillExpanded.TryGetValue(propPath, out bool exp) && exp;
            
            // Ensure params exist
            if (!_quickFillParams.ContainsKey(propPath))
            {
                var lvp = property.FindPropertyRelative("LevelValues");
                _quickFillParams[propPath] = new QuickFillParams
                {
                    StartValue = lvp != null && lvp.arraySize > 0 ? lvp.GetArrayElementAtIndex(0).floatValue : 1f,
                    EndValue = lvp != null && lvp.arraySize > 0 ? lvp.GetArrayElementAtIndex(lvp.arraySize - 1).floatValue : 10f
                };
            }
            var qfParams = _quickFillParams[propPath];
            
            // Header row
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 4;
            
            var headerLabel = new Label("Quick Fill");
            headerLabel.style.fontSize = 10;
            headerLabel.style.color = Colors.HintText;
            headerLabel.style.marginRight = 8;
            header.Add(headerLabel);
            
            // Edit curve button
            var editCurveBtn = new Button(() => CurveEditorWindow.Show(property));
            editCurveBtn.text = "📈 Edit Curve";
            editCurveBtn.style.height = 18;
            editCurveBtn.style.fontSize = 9;
            editCurveBtn.style.paddingLeft = 4;
            editCurveBtn.style.paddingRight = 4;
            ApplyButtonStyle(editCurveBtn);
            header.Add(editCurveBtn);
            
            // Advanced wizard button
            var wizardBtn = new Button(() => QuickFillWizard.Show(property, () => ScheduleRebuild(root, property)));
            wizardBtn.text = "🧙 Advanced";
            wizardBtn.tooltip = "Open advanced wizard with more options";
            wizardBtn.style.height = 18;
            wizardBtn.style.fontSize = 9;
            wizardBtn.style.marginLeft = 4;
            wizardBtn.style.paddingLeft = 4;
            wizardBtn.style.paddingRight = 4;
            ApplyButtonStyle(wizardBtn);
            header.Add(wizardBtn);
            
            section.Add(header);
            
            // Mode selector tabs
            var modeRow = new VisualElement();
            modeRow.style.flexDirection = FlexDirection.Row;
            modeRow.style.marginBottom = 4;
            
            var currentMode = _quickFillMode.TryGetValue(propPath, out var mode) ? mode : EQuickFillMode.Linear;
            
            var linearTab = CreateModeTab("Linear", EQuickFillMode.Linear, currentMode, propPath, root, property);
            var additiveTab = CreateModeTab("Additive", EQuickFillMode.Additive, currentMode, propPath, root, property);
            var stepsTab = CreateModeTab("Steps", EQuickFillMode.Steps, currentMode, propPath, root, property);
            
            modeRow.Add(linearTab);
            modeRow.Add(additiveTab);
            modeRow.Add(stepsTab);
            section.Add(modeRow);
            
            // Parameters based on mode
            var paramsRow = new VisualElement();
            paramsRow.style.flexDirection = FlexDirection.Row;
            paramsRow.style.alignItems = Align.Center;
            paramsRow.style.flexWrap = Wrap.Wrap;
            
            var scaler = property.managedReferenceValue as AbstractScaler;
            
            switch (currentMode)
            {
                case EQuickFillMode.Linear:
                    paramsRow.Add(CreateParamField("From:", qfParams.StartValue, v => qfParams.StartValue = v, 50));
                    paramsRow.Add(CreateParamField("To:", qfParams.EndValue, v => qfParams.EndValue = v, 50));
                    break;
                    
                case EQuickFillMode.Additive:
                    paramsRow.Add(CreateParamField("Base:", qfParams.BaseValue, v => qfParams.BaseValue = v, 50));
                    paramsRow.Add(CreateParamField("+/Lvl:", qfParams.Increment, v => qfParams.Increment = v, 50));
                    break;
                    
                case EQuickFillMode.Steps:
                    paramsRow.Add(CreateParamField("From:", qfParams.StartValue, v => qfParams.StartValue = v, 50));
                    paramsRow.Add(CreateParamField("To:", qfParams.EndValue, v => qfParams.EndValue = v, 50));
                    paramsRow.Add(CreateIntParamField("Steps:", qfParams.StepCount, v => qfParams.StepCount = v, 40));
                    break;
            }
            
            // Apply button
            var applyBtn = new Button(() => ApplyQuickFill(property, root, currentMode, qfParams, scaler));
            applyBtn.text = "Apply";
            applyBtn.style.height = 18;
            applyBtn.style.fontSize = 9;
            applyBtn.style.paddingLeft = 8;
            applyBtn.style.paddingRight = 8;
            applyBtn.style.marginLeft = 8;
            applyBtn.style.backgroundColor = Colors.AccentGreen;
            applyBtn.style.color = Color.white;
            ApplyButtonStyle(applyBtn);
            paramsRow.Add(applyBtn);
            
            section.Add(paramsRow);
            
            return section;
        }
        
        private VisualElement CreateModeTab(string label, EQuickFillMode mode, EQuickFillMode current, 
            string propPath, VisualElement root, SerializedProperty property)
        {
            bool isActive = mode == current;
            
            var tab = new Button(() =>
            {
                _quickFillMode[propPath] = mode;
                ScheduleRebuild(root, property);
            });
            tab.text = label;
            tab.style.height = 18;
            tab.style.fontSize = 9;
            tab.style.paddingLeft = 8;
            tab.style.paddingRight = 8;
            tab.style.marginRight = 2;
            tab.style.borderTopLeftRadius = 3;
            tab.style.borderTopRightRadius = 3;
            tab.style.borderBottomLeftRadius = 0;
            tab.style.borderBottomRightRadius = 0;
            
            if (isActive)
            {
                tab.style.backgroundColor = Colors.AccentPurple;
                tab.style.color = Color.white;
            }
            else
            {
                tab.style.backgroundColor = Colors.ButtonBackground;
                tab.RegisterCallback<MouseEnterEvent>(_ => tab.style.backgroundColor = Colors.ButtonHover);
                tab.RegisterCallback<MouseLeaveEvent>(_ => tab.style.backgroundColor = Colors.ButtonBackground);
            }
            
            return tab;
        }
        
        private VisualElement CreateParamField(string label, float value, Action<float> onChange, int fieldWidth)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginRight = 8;
            
            var labelEl = new Label(label);
            labelEl.style.fontSize = 9;
            labelEl.style.color = Colors.HintText;
            labelEl.style.marginRight = 2;
            container.Add(labelEl);
            
            var field = new FloatField { value = value };
            field.style.width = fieldWidth;
            field.style.fontSize = 10;
            field.RegisterValueChangedCallback(evt => onChange(evt.newValue));
            container.Add(field);
            
            return container;
        }
        
        private VisualElement CreateIntParamField(string label, int value, Action<int> onChange, int fieldWidth)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.marginRight = 8;
            
            var labelEl = new Label(label);
            labelEl.style.fontSize = 9;
            labelEl.style.color = Colors.HintText;
            labelEl.style.marginRight = 2;
            container.Add(labelEl);
            
            var field = new IntegerField { value = value };
            field.style.width = fieldWidth;
            field.style.fontSize = 10;
            field.RegisterValueChangedCallback(evt => onChange(Mathf.Max(1, evt.newValue)));
            container.Add(field);
            
            return container;
        }
        
        private void ApplyQuickFill(SerializedProperty property, VisualElement root, EQuickFillMode mode, 
            QuickFillParams qfParams, AbstractScaler scaler)
        {
            var lvp = property.FindPropertyRelative("LevelValues");
            if (lvp == null) return;
            
            for (int i = 0; i < lvp.arraySize; i++)
            {
                float t = lvp.arraySize > 1 ? (float)i / (lvp.arraySize - 1) : 0f;
                float value = mode switch
                {
                    EQuickFillMode.Linear => Mathf.Lerp(qfParams.StartValue, qfParams.EndValue, t),
                    EQuickFillMode.Additive => qfParams.BaseValue + (qfParams.Increment * i),
                    EQuickFillMode.Steps => CalculateStepValue(t, qfParams.StartValue, qfParams.EndValue, qfParams.StepCount),
                    _ => 1f
                };
                lvp.GetArrayElementAtIndex(i).floatValue = value;
            }
            
            if (scaler != null) RegenerateCurve(property, scaler);
            property.serializedObject.ApplyModifiedProperties();
            ScheduleRebuild(root, property);
        }
        
        private float CalculateStepValue(float t, float start, float end, int stepCount)
        {
            int step = Mathf.FloorToInt(t * stepCount);
            float stepT = stepCount > 0 ? (float)step / stepCount : 0f;
            return Mathf.Lerp(start, end, stepT);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Level Values Grid
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateLevelValuesGrid(SerializedProperty property, AbstractScaler scaler, VisualElement root)
        {
            var section = new VisualElement { name = "LevelValuesGrid" };
            section.style.marginTop = 6;
            
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;
            
            var headerLabel = new Label("Level Values");
            headerLabel.style.fontSize = 10;
            headerLabel.style.color = Colors.HintText;
            headerLabel.style.flexGrow = 1;
            headerRow.Add(headerLabel);
            
            var lvp = property.FindPropertyRelative("LevelValues");
            if (lvp != null)
            {
                var countLabel = new Label($"({lvp.arraySize} levels)");
                countLabel.style.fontSize = 9;
                countLabel.style.color = Colors.HintText;
                headerRow.Add(countLabel);
            }
            
            section.Add(headerRow);
            
            if (lvp == null || lvp.arraySize == 0)
            {
                section.Add(new Label("No level values") { style = { color = Colors.HintText, fontSize = 10 } });
                return section;
            }
            
            // Grid of level values (4 columns)
            var grid = new VisualElement { name = "Grid" };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            
            for (int i = 0; i < lvp.arraySize; i++)
            {
                int index = i; // Capture for closure
                var cell = new VisualElement();
                cell.style.width = new Length(25f, LengthUnit.Percent);
                cell.style.paddingRight = 4;
                cell.style.paddingBottom = 2;
                
                var cellContent = new VisualElement();
                cellContent.style.flexDirection = FlexDirection.Row;
                cellContent.style.alignItems = Align.Center;
                
                var levelLabel = new Label($"{index + 1}:");
                levelLabel.style.width = 24;
                levelLabel.style.fontSize = 9;
                levelLabel.style.color = Colors.HintText;
                levelLabel.style.unityTextAlign = TextAnchor.MiddleRight;
                levelLabel.style.marginRight = 2;
                cellContent.Add(levelLabel);
                
                var valueField = new FloatField();
                valueField.value = lvp.GetArrayElementAtIndex(index).floatValue;
                valueField.style.flexGrow = 1;
                valueField.style.fontSize = 10;
                
                valueField.RegisterValueChangedCallback(evt =>
                {
                    lvp.GetArrayElementAtIndex(index).floatValue = evt.newValue;
                    RegenerateCurve(property, scaler);
                    property.serializedObject.ApplyModifiedProperties();
                    // Rebuild to update curve preview
                    ScheduleRebuild(root, property);
                });
                
                cellContent.Add(valueField);
                cell.Add(cellContent);
                grid.Add(cell);
            }
            
            section.Add(grid);
            return section;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Curve Preview
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateCurvePreview(SerializedProperty property, AbstractScaler scaler)
        {
            var section = new VisualElement { name = "CurvePreview" };
            section.style.marginTop = 8;
            
            var headerLabel = new Label("Scaling Curve");
            headerLabel.style.fontSize = 10;
            headerLabel.style.color = Colors.HintText;
            headerLabel.style.marginBottom = 2;
            section.Add(headerLabel);
            
            var lvp = property.FindPropertyRelative("LevelValues");
            if (lvp == null || lvp.arraySize == 0)
            {
                section.Add(new Label("No level values") { style = { color = Colors.HintText, fontSize = 10, marginTop = 4 } });
                return section;
            }
            
            float minVal = float.MaxValue, maxVal = float.MinValue;
            for (int i = 0; i < lvp.arraySize; i++)
            {
                float v = lvp.GetArrayElementAtIndex(i).floatValue;
                minVal = Mathf.Min(minVal, v);
                maxVal = Mathf.Max(maxVal, v);
            }
            int count = lvp.arraySize;
            
            var curveContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 4 } };
            
            // Y-axis
            var yAxis = new VisualElement { style = { width = 35, justifyContent = Justify.SpaceBetween, paddingRight = 4 } };
            yAxis.Add(new Label($"{maxVal:F1}") { style = { fontSize = 9, color = Colors.HintText, unityTextAlign = TextAnchor.MiddleRight } });
            yAxis.Add(new Label($"{minVal:F1}") { style = { fontSize = 9, color = Colors.HintText, unityTextAlign = TextAnchor.MiddleRight } });
            curveContainer.Add(yAxis);
            
            var curveWrapper = new VisualElement { style = { flexGrow = 1 } };
            var scalingProp = property.FindPropertyRelative("Scaling");
            var curve = scalingProp?.animationCurveValue ?? new AnimationCurve();
            
            var curveField = new CurveField { value = curve };
            curveField.style.height = 50;
            curveField.style.borderTopLeftRadius = 4;
            curveField.style.borderTopRightRadius = 4;
            curveField.style.borderBottomLeftRadius = 4;
            curveField.style.borderBottomRightRadius = 4;
            curveField.SetEnabled(false);
            curveWrapper.Add(curveField);
            curveContainer.Add(curveWrapper);
            section.Add(curveContainer);
            
            // X-axis
            var xAxis = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween, marginLeft = 35, marginTop = 2 } };
            xAxis.Add(new Label("Lv1") { style = { fontSize = 9, color = Colors.HintText } });
            xAxis.Add(new Label($"Lv{count}") { style = { fontSize = 9, color = Colors.HintText } });
            section.Add(xAxis);
            
            return section;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Behaviours Section
        // ═══════════════════════════════════════════════════════════════════════════
        
        private VisualElement CreateBehavioursSection(SerializedProperty property, VisualElement root)
        {
            var section = new VisualElement { name = "BehavioursSection" };
            section.style.marginTop = 8;
            section.style.paddingTop = 8;
            section.style.borderTopWidth = 1;
            section.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            
            var behavioursProp = property.FindPropertyRelative("Behaviours");
            if (behavioursProp == null) return section;
            
            // Header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 4;
            
            var headerLabel = new Label("Behaviours");
            headerLabel.style.fontSize = 10;
            headerLabel.style.color = Colors.HintText;
            headerLabel.style.flexGrow = 1;
            headerRow.Add(headerLabel);
            
            if (behavioursProp.arraySize > 0)
            {
                var countLabel = new Label($"({behavioursProp.arraySize})");
                countLabel.style.fontSize = 9;
                countLabel.style.color = Colors.AccentOrange;
                headerRow.Add(countLabel);
            }
            
            section.Add(headerRow);
            
            // Behaviours list using PropertyField
            var listField = new PropertyField(behavioursProp, "");
            listField.style.marginLeft = 0;
            section.Add(listField);
            
            // Bind to ensure PropertyField updates
            section.Bind(property.serializedObject);
            
            return section;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Utility Methods
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static VisualElement CreateRow(int marginBottom = 0)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            if (marginBottom > 0) row.style.marginBottom = marginBottom;
            return row;
        }
        
        private static VisualElement CreateDropdownContainer()
        {
            var c = new VisualElement();
            c.style.flexDirection = FlexDirection.Row;
            c.style.alignItems = Align.Center;
            c.style.paddingLeft = 6;
            c.style.paddingRight = 4;
            c.style.paddingTop = 2;
            c.style.paddingBottom = 2;
            c.style.borderTopLeftRadius = 4;
            c.style.borderTopRightRadius = 4;
            c.style.borderBottomLeftRadius = 4;
            c.style.borderBottomRightRadius = 4;
            c.style.borderTopWidth = 1;
            c.style.borderBottomWidth = 1;
            c.style.borderLeftWidth = 1;
            c.style.borderRightWidth = 1;
            c.style.borderTopColor = Colors.AccentPurple;
            c.style.borderBottomColor = Colors.AccentPurple;
            c.style.borderLeftColor = Colors.AccentPurple;
            c.style.borderRightColor = Colors.AccentPurple;
            c.style.backgroundColor = new Color(0.2f, 0.18f, 0.22f, 0.8f);
            c.Add(new Label("◆") { style = { color = Colors.AccentPurple, fontSize = 10, marginRight = 4 } });
            return c;
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
        
        private static void ApplyQuickFillButtonStyle(Button btn)
        {
            btn.style.height = 18;
            btn.style.fontSize = 9;
            btn.style.paddingLeft = 4;
            btn.style.paddingRight = 4;
            btn.style.marginRight = 2;
            btn.style.marginBottom = 2;
            ApplyButtonStyle(btn);
        }
        
        private void FillConstant(SerializedProperty property, float value)
        {
            var lvp = property.FindPropertyRelative("LevelValues");
            if (lvp == null) return;
            for (int i = 0; i < lvp.arraySize; i++)
                lvp.GetArrayElementAtIndex(i).floatValue = value;
            var scaler = property.managedReferenceValue as AbstractScaler;
            if (scaler != null) RegenerateCurve(property, scaler);
            property.serializedObject.ApplyModifiedProperties();
        }
        
        private void ResizeLevelValues(SerializedProperty property, AbstractScaler scaler, int size)
        {
            var lvp = property.FindPropertyRelative("LevelValues");
            if (lvp == null) return;
            int old = lvp.arraySize;
            float last = old > 0 ? lvp.GetArrayElementAtIndex(old - 1).floatValue : 1f;
            lvp.arraySize = size;
            for (int i = old; i < size; i++)
                lvp.GetArrayElementAtIndex(i).floatValue = last;
            RegenerateCurve(property, scaler);
        }
        
        private void RegenerateCurve(SerializedProperty property, AbstractScaler scaler)
        {
            var ip = property.FindPropertyRelative("Interpolation");
            var mode = ip != null ? (EScalerInterpolation)ip.enumValueIndex : EScalerInterpolation.Linear;
            RegenerateCurveWithInterpolation(property, scaler, mode);
        }
        
        private void RegenerateCurveWithInterpolation(SerializedProperty property, AbstractScaler scaler, EScalerInterpolation interpolation)
        {
            var lvp = property.FindPropertyRelative("LevelValues");
            var sp = property.FindPropertyRelative("Scaling");
            if (lvp == null || lvp.arraySize == 0) return;
            
            var curve = new AnimationCurve();
            int n = lvp.arraySize;
            for (int i = 0; i < n; i++)
            {
                float t = n > 1 ? (float)i / (n - 1) : 0f;
                curve.AddKey(new Keyframe(t, lvp.GetArrayElementAtIndex(i).floatValue));
            }
            
            var tm = interpolation switch
            {
                EScalerInterpolation.Constant => UnityEditor.AnimationUtility.TangentMode.Constant,
                EScalerInterpolation.Linear => UnityEditor.AnimationUtility.TangentMode.Linear,
                _ => UnityEditor.AnimationUtility.TangentMode.ClampedAuto
            };
            
            for (int i = 0; i < curve.length; i++)
            {
                UnityEditor.AnimationUtility.SetKeyLeftTangentMode(curve, i, tm);
                UnityEditor.AnimationUtility.SetKeyRightTangentMode(curve, i, tm);
            }
            
            if (sp != null)
                sp.animationCurveValue = curve;
        }
        
        private void CopyScaler(SerializedProperty property, AbstractScaler source, VisualElement root)
        {
            var type = source.GetType();
            var newScaler = Activator.CreateInstance(type) as AbstractScaler;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), newScaler);
            property.managedReferenceValue = newScaler;
            property.serializedObject.ApplyModifiedProperties();
            ScheduleRebuild(root, property);
        }
    }
}