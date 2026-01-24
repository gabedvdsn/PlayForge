using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// Editor window for debugging all GameplayAbilitySystem instances in the scene.
    /// Shows attributes, effects, abilities, and tags for each GAS entity.
    /// </summary>
    public class GASDebugWindow : EditorWindow
    {
        // UI Elements
        private VisualElement _leftPanel;
        private VisualElement _rightPanel;
        private ScrollView _entityList;
        private ScrollView _detailsScroll;
        private Label _statusLabel;
        
        // State
        private List<GameplayAbilitySystem> _gasObjects = new();
        private GameplayAbilitySystem _selectedGAS;
        private bool _autoRefresh = true;
        private float _refreshInterval = 0.25f;
        private double _lastRefreshTime;
        
        // Reflection cache for accessing private fields
        private FieldInfo _effectShelfField;
        private FieldInfo _tagCacheField;
        
        // Colors
        private static readonly Color HeaderColor = new Color(0.4f, 0.8f, 0.9f);
        private static readonly Color AttributeColor = new Color(0.9f, 0.7f, 0.3f);
        private static readonly Color EffectColor = new Color(0.6f, 0.4f, 0.9f);
        private static readonly Color AbilityColor = new Color(0.4f, 0.7f, 1f);
        private static readonly Color TagColor = new Color(0.3f, 0.7f, 0.6f);
        private static readonly Color ActiveTagColor = new Color(0.7f, 0.4f, 0.8f);
        private static readonly Color ImpactColor = new Color(0.7f, 0.4f, 0.3f);
        private static readonly Color SelectedColor = new Color(0.3f, 0.5f, 0.7f);
        private static readonly Color HoverColor = new Color(0.25f, 0.25f, 0.28f);
        private static readonly Color BackgroundDark = new Color(0.18f, 0.18f, 0.2f);
        private static readonly Color BackgroundMedium = new Color(0.22f, 0.22f, 0.24f);
        
        [MenuItem("PlayForge/GAS Debug Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<GASDebugWindow>();
            window.titleContent = new GUIContent("GAS Debug", EditorGUIUtility.IconContent("d_UnityEditor.DebugInspectorWindow").image);
            window.minSize = new Vector2(800, 500);
        }
        
        private void OnEnable()
        {
            // Cache reflection for private field access
            _effectShelfField = typeof(GameplayAbilitySystem).GetField("EffectShelf", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            _tagCacheField = typeof(GameplayAbilitySystem).GetField("TagCache", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }
        
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            _selectedGAS = null;
            _gasObjects.Clear();
            
            // Schedule rebuild on next frame to ensure UI is ready
            EditorApplication.delayCall += () =>
            {
                if (this != null && rootVisualElement != null)
                {
                    RefreshEntityList();
                    RefreshDetails();
                    UpdatePlayModeIndicator();
                }
            };
        }
        
        private void UpdatePlayModeIndicator()
        {
            var indicator = rootVisualElement?.Q<Label>("play-mode-indicator");
            if (indicator != null)
            {
                indicator.text = Application.isPlaying ? "▶ PLAYING" : "⏸ EDITOR";
                indicator.style.color = Application.isPlaying 
                    ? new Color(0.4f, 0.9f, 0.4f) 
                    : new Color(0.7f, 0.7f, 0.7f);
            }
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = BackgroundDark;
            
            // Toolbar
            var toolbar = CreateToolbar();
            root.Add(toolbar);
            
            // Main content (split view)
            var mainContent = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexGrow = 1
                }
            };
            root.Add(mainContent);
            
            // Left panel - Entity list
            _leftPanel = CreateLeftPanel();
            mainContent.Add(_leftPanel);
            
            // Right panel - Details
            _rightPanel = CreateRightPanel();
            mainContent.Add(_rightPanel);
            
            // Status bar
            _statusLabel = new Label("Ready")
            {
                style =
                {
                    paddingLeft = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    fontSize = 10,
                    color = new Color(0.6f, 0.6f, 0.6f),
                    backgroundColor = BackgroundMedium,
                    borderTopWidth = 1,
                    borderTopColor = new Color(0.15f, 0.15f, 0.15f)
                }
            };
            root.Add(_statusLabel);
            
            // Initial refresh
            RefreshEntityList();
            UpdatePlayModeIndicator();
        }
        
        private void Update()
        {
            if (!_autoRefresh) return;
            
            if (EditorApplication.timeSinceStartup - _lastRefreshTime > _refreshInterval)
            {
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                
                // Check if entity count changed
                int currentCount = FindObjectsByType<GameplayAbilitySystem>(FindObjectsSortMode.None).Length;
                if (currentCount != _gasObjects.Count)
                {
                    RefreshEntityList();
                }
                
                // Refresh details if something is selected
                if (_selectedGAS != null)
                {
                    RefreshDetails();
                }
            }
        }
        
        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 4,
                    paddingBottom = 4,
                    backgroundColor = BackgroundMedium,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.15f, 0.15f, 0.15f)
                }
            };
            
            // Title
            toolbar.Add(new Label("GAS Debug")
            {
                style =
                {
                    fontSize = 14,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = HeaderColor,
                    marginRight = 16
                }
            });
            
            // Refresh button
            var refreshBtn = new Button(() => { RefreshEntityList(); RefreshDetails(); })
            {
                text = "↻ Refresh",
                style = { marginRight = 8 }
            };
            toolbar.Add(refreshBtn);
            
            // Auto-refresh toggle
            var autoRefreshToggle = new Toggle("Auto-refresh") { value = _autoRefresh };
            autoRefreshToggle.RegisterValueChangedCallback(evt => _autoRefresh = evt.newValue);
            autoRefreshToggle.style.marginRight = 16;
            toolbar.Add(autoRefreshToggle);
            
            // Refresh interval
            toolbar.Add(new Label("Interval:") { style = { marginRight = 4 } });
            var intervalField = new FloatField { value = _refreshInterval, style = { width = 50 } };
            intervalField.RegisterValueChangedCallback(evt => _refreshInterval = Mathf.Max(0.1f, evt.newValue));
            toolbar.Add(intervalField);
            
            // Spacer
            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });
            
            // Play mode indicator
            var playModeLabel = new Label(Application.isPlaying ? "▶ PLAYING" : "⏸ EDITOR")
            {
                name = "play-mode-indicator",
                style =
                {
                    fontSize = 10,
                    color = Application.isPlaying ? new Color(0.4f, 0.9f, 0.4f) : new Color(0.7f, 0.7f, 0.7f),
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            toolbar.Add(playModeLabel);
            
            return toolbar;
        }
        
        private VisualElement CreateLeftPanel()
        {
            var panel = new VisualElement
            {
                style =
                {
                    width = 250,
                    borderRightWidth = 1,
                    borderRightColor = new Color(0.15f, 0.15f, 0.15f),
                    backgroundColor = BackgroundDark
                }
            };
            
            // Header
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 6,
                    paddingBottom = 6,
                    backgroundColor = BackgroundMedium
                }
            };
            header.Add(new Label("Entities")
            {
                style =
                {
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.8f, 0.8f, 0.8f)
                }
            });
            header.Add(new VisualElement { style = { flexGrow = 1 } });
            header.Add(new Label { name = "entity-count", style = { fontSize = 10, color = new Color(0.5f, 0.5f, 0.5f) } });
            panel.Add(header);
            
            // Entity list
            _entityList = new ScrollView
            {
                style = { flexGrow = 1 }
            };
            panel.Add(_entityList);
            
            return panel;
        }
        
        private VisualElement CreateRightPanel()
        {
            var panel = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = BackgroundDark
                }
            };
            
            // Header
            var header = new VisualElement
            {
                name = "details-header",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 8,
                    paddingBottom = 8,
                    backgroundColor = BackgroundMedium
                }
            };
            header.Add(new Label("Select an entity to view details")
            {
                name = "details-title",
                style =
                {
                    fontSize = 12,
                    color = new Color(0.6f, 0.6f, 0.6f),
                    unityFontStyleAndWeight = FontStyle.Italic
                }
            });
            panel.Add(header);
            
            // Details scroll view
            _detailsScroll = new ScrollView
            {
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 12,
                    paddingRight = 12,
                    paddingTop = 8,
                    paddingBottom = 8
                }
            };
            panel.Add(_detailsScroll);
            
            return panel;
        }
        
        private void RefreshEntityList()
        {
            _gasObjects.Clear();
            _gasObjects.AddRange(FindObjectsByType<GameplayAbilitySystem>(FindObjectsSortMode.None));
            
            // Validate selected GAS still exists
            if (_selectedGAS != null && !_gasObjects.Contains(_selectedGAS))
            {
                _selectedGAS = null;
            }
            
            _entityList.Clear();
            
            var countLabel = _leftPanel.Q<Label>("entity-count");
            if (countLabel != null)
            {
                countLabel.text = $"({_gasObjects.Count})";
            }
            
            foreach (var gas in _gasObjects)
            {
                var item = CreateEntityListItem(gas);
                _entityList.Add(item);
            }
            
            if (_gasObjects.Count == 0)
            {
                _entityList.Add(new Label("No GAS objects in scene")
                {
                    style =
                    {
                        color = new Color(0.5f, 0.5f, 0.5f),
                        unityFontStyleAndWeight = FontStyle.Italic,
                        paddingLeft = 12,
                        paddingTop = 12
                    }
                });
            }
            
            _statusLabel.text = $"Found {_gasObjects.Count} GAS objects | {DateTime.Now:HH:mm:ss}";
            UpdatePlayModeIndicator();
        }
        
        private VisualElement CreateEntityListItem(GameplayAbilitySystem gas)
        {
            bool isSelected = gas == _selectedGAS;
            
            var item = new VisualElement
            {
                name = $"entity-item-{gas.GetInstanceID()}",
                userData = gas,
                pickingMode = PickingMode.Position, // Ensure clicks are captured
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 6,
                    paddingBottom = 6,
                    backgroundColor = isSelected ? SelectedColor : Color.clear,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.15f, 0.15f, 0.15f)
                }
            };
            
            // Use MouseDownEvent for more responsive clicking
            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0) // Left click only
                {
                    SelectEntity(gas);
                    evt.StopPropagation();
                }
            });
            
            // Hover effects (only if not selected)
            item.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (item.userData as GameplayAbilitySystem != _selectedGAS)
                    item.style.backgroundColor = HoverColor;
            });
            item.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (item.userData as GameplayAbilitySystem != _selectedGAS)
                    item.style.backgroundColor = Color.clear;
            });
            
            // Icon
            item.Add(new Label("◆")
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    color = HeaderColor,
                    fontSize = 10,
                    marginRight = 6
                }
            });
            
            // Name
            string displayName = gas.Data != null ? gas.Data.GetName() : gas.gameObject.name;
            item.Add(new Label(displayName)
            {
                name = "entity-name",
                pickingMode = PickingMode.Ignore,
                style =
                {
                    flexGrow = 1,
                    color = isSelected ? Color.white : new Color(0.85f, 0.85f, 0.85f),
                    fontSize = 11
                }
            });
            
            // Level badge
            if (gas.Data != null)
            {
                var badge = CreateBadge($"Lv.{gas.GetLevel()}", new Color(0.5f, 0.5f, 0.5f));
                badge.pickingMode = PickingMode.Ignore;
                item.Add(badge);
            }
            
            return item;
        }
        
        private void SelectEntity(GameplayAbilitySystem gas)
        {
            var previousSelection = _selectedGAS;
            _selectedGAS = gas;
            
            // Update visual state without full rebuild
            UpdateEntitySelectionVisuals(previousSelection, gas);
            RefreshDetails();
        }
        
        private void UpdateEntitySelectionVisuals(GameplayAbilitySystem previous, GameplayAbilitySystem current)
        {
            // Deselect previous (check if it still exists)
            if (previous != null)
            {
                try
                {
                    var prevItem = _entityList.Q<VisualElement>($"entity-item-{previous.GetInstanceID()}");
                    if (prevItem != null)
                    {
                        prevItem.style.backgroundColor = Color.clear;
                        var nameLabel = prevItem.Q<Label>("entity-name");
                        if (nameLabel != null)
                            nameLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
                    }
                }
                catch (MissingReferenceException)
                {
                    // Object was destroyed, ignore
                }
            }
            
            // Select current
            if (current != null)
            {
                try
                {
                    var currItem = _entityList.Q<VisualElement>($"entity-item-{current.GetInstanceID()}");
                    if (currItem != null)
                    {
                        currItem.style.backgroundColor = SelectedColor;
                        var nameLabel = currItem.Q<Label>("entity-name");
                        if (nameLabel != null)
                            nameLabel.style.color = Color.white;
                    }
                }
                catch (MissingReferenceException)
                {
                    // Object was destroyed, clear selection
                    _selectedGAS = null;
                }
            }
        }
        
        private void RefreshDetails()
        {
            _detailsScroll.Clear();
            
            var titleLabel = _rightPanel.Q<Label>("details-title");
            
            // Check if selected GAS was destroyed
            if (_selectedGAS == null)
            {
                if (titleLabel != null)
                {
                    titleLabel.text = "Select an entity to view details";
                    titleLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    titleLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                }
                return;
            }
            
            // Verify object still exists (Unity null check for destroyed objects)
            try
            {
                // Access something to trigger MissingReferenceException if destroyed
                _ = _selectedGAS.gameObject;
            }
            catch (MissingReferenceException)
            {
                _selectedGAS = null;
                if (titleLabel != null)
                {
                    titleLabel.text = "Select an entity to view details";
                    titleLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                    titleLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
                }
                return;
            }
            
            // Update header
            if (titleLabel != null)
            {
                string name = _selectedGAS.Data != null ? _selectedGAS.Data.GetName() : _selectedGAS.gameObject.name;
                titleLabel.text = name;
                titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleLabel.style.color = HeaderColor;
            }
            
            // Entity Info Section
            _detailsScroll.Add(CreateEntityInfoSection());
            
            // Attributes Section
            _detailsScroll.Add(CreateAttributesSection());
            
            // Effects Section
            _detailsScroll.Add(CreateEffectsSection());
            
            // Abilities Section
            _detailsScroll.Add(CreateAbilitiesSection());
            
            // Tags Section
            _detailsScroll.Add(CreateTagsSection());
        }
        
        private VisualElement CreateEntityInfoSection()
        {
            var section = CreateSection("Entity Info", HeaderColor);
            
            if (_selectedGAS.Data == null)
            {
                section.Add(CreateInfoRow("Status", "No EntityIdentity assigned", Color.red));
                return section;
            }
            
            var data = _selectedGAS.Data;
            
            section.Add(CreateInfoRow("Name", data.GetName(), Color.white));
            section.Add(CreateInfoRow("Level", $"{_selectedGAS.GetLevel()} / {data.MaxLevel}", Color.white));
            section.Add(CreateInfoRow("Asset Tag", data.AssetTag.GetName(), new Color(0.7f, 0.7f, 0.7f)));
            
            // Affiliation
            if (data.Affiliation != null && data.Affiliation.Count > 0)
            {
                var affiliationStr = string.Join(", ", data.Affiliation.Select(t => t.GetName()));
                section.Add(CreateInfoRow("Affiliation", affiliationStr, TagColor));
            }
            
            return section;
        }
        
        private VisualElement CreateAttributesSection()
        {
            var section = CreateSection("Attributes", AttributeColor);
            
            if (!_selectedGAS.FindAttributeSystem(out var attrSystem))
            {
                section.Add(CreateEmptyLabel("No attribute system"));
                return section;
            }
            
            var cache = attrSystem.GetAttributeCache();
            if (cache == null || cache.Count == 0)
            {
                section.Add(CreateEmptyLabel("No attributes defined"));
                return section;
            }
            
            // Header row
            var headerRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingBottom = 4,
                    marginBottom = 4,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };
            headerRow.Add(new Label("Attribute") { style = { width = 150, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10 } });
            headerRow.Add(new Label("Current") { style = { width = 70, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10, unityTextAlign = TextAnchor.MiddleRight } });
            headerRow.Add(new Label("Base") { style = { width = 70, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10, unityTextAlign = TextAnchor.MiddleRight } });
            headerRow.Add(new Label("Ratio") { style = { width = 60, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10, unityTextAlign = TextAnchor.MiddleRight } });
            section.Add(headerRow);
            
            foreach (var kvp in cache.OrderBy(k => k.Key?.name ?? ""))
            {
                var row = CreateAttributeRow(kvp.Key, kvp.Value);
                section.Add(row);
            }
            
            return section;
        }
        
        private VisualElement CreateAttributeRow(Attribute attribute, CachedAttributeValue cached)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 2,
                    paddingBottom = 2
                }
            };
            
            // Attribute name
            string attrName = attribute != null ? attribute.GetName() : "Unknown";
            row.Add(new Label(attrName)
            {
                style =
                {
                    width = 150,
                    color = Color.white,
                    fontSize = 11
                }
            });
            
            // Current value
            float current = cached.Value.CurrentValue;
            row.Add(new Label(current.ToString("F1"))
            {
                style =
                {
                    width = 70,
                    color = AttributeColor,
                    fontSize = 11,
                    unityTextAlign = TextAnchor.MiddleRight
                }
            });
            
            // Base value
            float baseVal = cached.Value.BaseValue;
            row.Add(new Label(baseVal.ToString("F1"))
            {
                style =
                {
                    width = 70,
                    color = new Color(0.7f, 0.7f, 0.7f),
                    fontSize = 11,
                    unityTextAlign = TextAnchor.MiddleRight
                }
            });
            
            // Ratio
            float ratio = baseVal > 0 ? current / baseVal : 0;
            Color ratioColor = ratio >= 1f ? TagColor : (ratio > 0.25f ? AttributeColor : Color.red);
            row.Add(new Label($"{ratio:P0}")
            {
                style =
                {
                    width = 60,
                    color = ratioColor,
                    fontSize = 11,
                    unityTextAlign = TextAnchor.MiddleRight
                }
            });
            
            // Progress bar
            var progressContainer = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    height = 6,
                    marginLeft = 8,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f),
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2
                }
            };
            
            var progressBar = new VisualElement
            {
                style =
                {
                    width = Length.Percent(Mathf.Clamp01(ratio) * 100),
                    height = Length.Percent(100),
                    backgroundColor = ratioColor,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2
                }
            };
            progressContainer.Add(progressBar);
            row.Add(progressContainer);
            
            return row;
        }
        
        private VisualElement CreateEffectsSection()
        {
            var section = CreateSection("Active Effects", EffectColor);
            
            // Get effects via reflection
            var effectShelf = _effectShelfField?.GetValue(_selectedGAS) as List<AbstractEffectContainer>;
            
            if (effectShelf == null || effectShelf.Count == 0)
            {
                section.Add(CreateEmptyLabel("No active effects"));
                return section;
            }
            
            foreach (var container in effectShelf)
            {
                var effectRow = CreateEffectRow(container);
                section.Add(effectRow);
            }
            
            return section;
        }
        
        private VisualElement CreateEffectRow(AbstractEffectContainer container)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 4,
                    marginBottom = 2,
                    backgroundColor = new Color(EffectColor.r, EffectColor.g, EffectColor.b, 0.1f),
                    borderLeftWidth = 2,
                    borderLeftColor = EffectColor,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2
                }
            };
            
            // Effect name
            string effectName = container.Spec?.Base?.GetName() ?? "Unknown Effect";
            row.Add(new Label(effectName)
            {
                style =
                {
                    flexGrow = 1,
                    color = Color.white,
                    fontSize = 11
                }
            });
            
            // Impact
            var impact = container.GetTrackedImpact();
            row.Add(CreateBadge(impact.Last.ToString(), ImpactColor));
            row.Add(CreateBadge(impact.Total.ToString(), ImpactColor));
            
            // Duration info
            var durationPolicy = container.Spec?.Base?.DurationSpecification?.DurationPolicy ?? EEffectDurationPolicy.Instant;
            
            if (durationPolicy == EEffectDurationPolicy.Infinite)
            {
                row.Add(CreateBadge("∞", EffectColor, "Infinite"));
            }
            else if (durationPolicy == EEffectDurationPolicy.Durational)
            {
                float remaining = container.DurationRemaining;
                float total = container.TotalDuration;
                row.Add(CreateBadge($"{remaining:F1}s", EffectColor, $"{remaining:F1} / {total:F1}s"));
            }
            else
            {
                row.Add(CreateBadge("Instant", new Color(0.5f, 0.5f, 0.5f)));
            }
            
            // Stacks if applicable
            // (Add stack count if your system supports it)
            
            return row;
        }
        
        private VisualElement CreateAbilitiesSection()
        {
            var section = CreateSection("Abilities", AbilityColor);
            
            if (!_selectedGAS.FindAbilitySystem(out var abilSystem))
            {
                section.Add(CreateEmptyLabel("No ability system"));
                return section;
            }
            
            var containers = abilSystem.GetAbilityContainers();
            if (containers == null || containers.Count == 0)
            {
                section.Add(CreateEmptyLabel("No abilities"));
                return section;
            }
            
            foreach (var container in containers)
            {
                var abilityRow = CreateAbilityRow(container);
                section.Add(abilityRow);
            }
            
            return section;
        }
        
        private VisualElement CreateAbilityRow(AbilitySpecContainer container)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 4,
                    marginBottom = 2,
                    backgroundColor = new Color(AbilityColor.r, AbilityColor.g, AbilityColor.b, 0.1f),
                    borderLeftWidth = 2,
                    borderLeftColor = container.IsClaiming ? TagColor : AbilityColor,
                    borderTopLeftRadius = 2,
                    borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2,
                    borderBottomRightRadius = 2
                }
            };
            
            // Status indicator
            if (container.IsClaiming)
            {
                row.Add(new Label("▶")
                {
                    style =
                    {
                        color = TagColor,
                        fontSize = 10,
                        marginRight = 4
                    }
                });
            }
            
            // Ability name
            string abilityName = container.Spec?.Base?.GetName() ?? "Unknown Ability";
            row.Add(new Label(abilityName)
            {
                style =
                {
                    flexGrow = 1,
                    color = Color.white,
                    fontSize = 11
                }
            });
            
            // Level
            int level = container.Spec?.Level ?? 0;
            int maxLevel = container.Spec?.Base?.MaxLevel ?? 1;
            row.Add(CreateBadge($"Lv.{level}/{maxLevel}", AbilityColor));
            
            // State
            if (container.IsActive)
            {
                row.Add(CreateBadge("ACTIVE", ActiveTagColor));
            }
            
            // State
            if (container.IsClaiming)
            {
                row.Add(CreateBadge("CLAIMED", TagColor));
            }
            
            return row;
        }
        
        private VisualElement CreateTagsSection()
        {
            var section = CreateSection("Tags", TagColor);
            
            var tagCache = _tagCacheField?.GetValue(_selectedGAS) as TagCache;
            var tags = tagCache?.GetAppliedTags();
            
            if (tags == null || tags.Count == 0)
            {
                section.Add(CreateEmptyLabel("No active tags"));
                return section;
            }
            
            // Tag container with wrap
            var tagContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap
                }
            };
            
            foreach (var tag in tags.Where(t => t != null).OrderBy(t => t.ToString()))
            {
                int weight = tagCache.GetWeight(tag);
                var tagElement = CreateTagBadge(tag.ToString(), weight);
                tagElement.style.marginRight = 4;
                tagElement.style.marginBottom = 4;
                tagContainer.Add(tagElement);
            }
            
            section.Add(tagContainer);
            
            return section;
        }
        
        private VisualElement CreateTagBadge(string tagName, int weight)
        {
            var container = new VisualElement
            {
                tooltip = weight > 1 ? $"Weight: {weight}" : null,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    backgroundColor = new Color(TagColor.r, TagColor.g, TagColor.b, 0.15f),
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3
                }
            };
            
            // Tag name
            container.Add(new Label(tagName)
            {
                style =
                {
                    fontSize = 9,
                    color = TagColor
                }
            });
            
            // Weight indicator (only show if > 1)
            if (weight > 1)
            {
                container.Add(new Label($" ×{weight}")
                {
                    style =
                    {
                        fontSize = 8,
                        color = new Color(TagColor.r, TagColor.g, TagColor.b, 0.7f),
                        unityFontStyleAndWeight = FontStyle.Bold
                    }
                });
            }
            
            return container;
        }
        
        #region UI Helpers
        
        private VisualElement CreateSection(string title, Color accentColor)
        {
            var section = new VisualElement
            {
                style =
                {
                    marginBottom = 16,
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 8,
                    paddingBottom = 8,
                    backgroundColor = BackgroundMedium,
                    borderLeftWidth = 3,
                    borderLeftColor = accentColor,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4
                }
            };
            
            // Header
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 8
                }
            };
            
            header.Add(new Label(title)
            {
                style =
                {
                    fontSize = 12,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = accentColor
                }
            });
            
            section.Add(header);
            
            return section;
        }
        
        private VisualElement CreateInfoRow(string label, string value, Color valueColor)
        {
            var row = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 2
                }
            };
            
            row.Add(new Label(label + ":")
            {
                style =
                {
                    width = 100,
                    color = new Color(0.6f, 0.6f, 0.6f),
                    fontSize = 11
                }
            });
            
            row.Add(new Label(value)
            {
                style =
                {
                    color = valueColor,
                    fontSize = 11
                }
            });
            
            return row;
        }
        
        private VisualElement CreateBadge(string text, Color color, string tooltip = null)
        {
            var badge = new Label(text)
            {
                tooltip = tooltip,
                style =
                {
                    fontSize = 9,
                    color = color,
                    backgroundColor = new Color(color.r, color.g, color.b, 0.15f),
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
            return badge;
        }
        
        private Label CreateEmptyLabel(string text)
        {
            return new Label(text)
            {
                style =
                {
                    color = new Color(0.5f, 0.5f, 0.5f),
                    unityFontStyleAndWeight = FontStyle.Italic,
                    fontSize = 11
                }
            };
        }
        
        #endregion
    }
}