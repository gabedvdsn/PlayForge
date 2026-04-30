using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FarEmerald.PlayForge.Extended.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Editor window for debugging all GameplayAbilitySystem instances in the scene.
    /// Two tabs: Details (per-entity deep inspection) and Overview (cross-entity attribute comparison).
    /// Entity list is sortable by Alphabetical, Affiliation, or Cache Index.
    /// </summary>
    public class GASDebugger : EditorWindow
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // ENUMS
        // ═══════════════════════════════════════════════════════════════════════════

        private enum ESortMode { Alphabetical, Affiliation, CacheIndex }
        private enum ETab { Details, Overview }

        // ═══════════════════════════════════════════════════════════════════════════
        // UI ELEMENTS
        // ═══════════════════════════════════════════════════════════════════════════

        private VisualElement _leftPanel;
        private VisualElement _rightPanel;
        private ScrollView _entityList;
        private ScrollView _detailsScroll;
        private VisualElement _overviewContainer;
        private Label _statusLabel;
        private VisualElement _tabBar;
        private VisualElement _overviewAttributePicker;
        private ScrollView _overviewTableScroll;

        // ═══════════════════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════════════════

        private List<GameplayAbilitySystem> _gasObjects = new();
        private GameplayAbilitySystem _selectedGAS;
        private bool _autoRefresh = true;
        private float _refreshInterval = 0.25f;
        private double _lastRefreshTime;
        private ESortMode _sortMode = ESortMode.Alphabetical;
        private ETab _activeTab = ETab.Details;

        // Overview state
        private HashSet<IAttribute> _selectedOverviewAttributes = new();
        private bool _overviewPickerExpanded;
        private bool _overviewAttributesInitialized;

        // Reflection cache
        private FieldInfo _effectShelfField;
        private FieldInfo _tagCacheField;

        // Expansion state
        private HashSet<string> _expandedAttributes = new();
        private HashSet<string> _expandedAbilities = new();

        // Frame summary display state
        //   • Per-GAS rolling sample buffer → configurable window size (in frames)
        //   • Last significant impact dealt / received per GAS (persists across frames)
        private int _frameSummaryWindowFrames = 20;
        private float _significantImpactThreshold = 0f; // any non-zero counts
        private readonly Dictionary<GameplayAbilitySystem, FrameSummaryRollingStats> _frameSummaryStats = new();
        private bool _frameSummaryHooked;

        private class FrameSummaryRollingStats
        {
            public readonly Queue<FrameSummarySample> Samples = new();
            public ImpactData? LastSignificantDealt;
            public ImpactData? LastSignificantReceived;
            public int LastSnapshotFrame = -1;
        }

        private struct FrameSummarySample
        {
            public int ImpactCount;
            public int DealtCount;
            public int ReceivedCount;
            public float TotalDamageDealt;
            public float TotalHealingDealt;
            public float TotalDamageReceived;
            public float TotalHealingReceived;
            public int ExecutedActions;
            public int InvalidatedActions;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // COLORS
        // ═══════════════════════════════════════════════════════════════════════════

        private static readonly Color HeaderColor = new(0.4f, 0.8f, 0.9f);
        private static readonly Color DerivationColor = new(0.6f, 0.6f, 0.8f);
        private static readonly Color AttributeColor = new(0.9f, 0.7f, 0.3f);
        private static readonly Color EffectColor = new(0.6f, 0.4f, 0.9f);
        private static readonly Color AbilityColor = new(0.4f, 0.7f, 1f);
        private static readonly Color TagColor = new(0.3f, 0.7f, 0.6f);
        private static readonly Color ActiveTagColor = new(0.7f, 0.4f, 0.8f);
        private static readonly Color CooldownColor = new(0.8f, 0.4f, 0.3f);
        private static readonly Color ImpactColor = new(0.7f, 0.4f, 0.3f);
        private static readonly Color SelectedColor = new(0.3f, 0.5f, 0.7f);
        private static readonly Color HoverColor = new(0.25f, 0.25f, 0.28f);
        private static readonly Color BackgroundDark = new(0.18f, 0.18f, 0.2f);
        private static readonly Color BackgroundMedium = new(0.22f, 0.22f, 0.24f);

        // Affiliation color palette (deterministic from tag name hash)
        private static readonly Color[] AffiliationPalette =
        {
            new(0.26f, 0.59f, 0.98f), // Blue
            new(0.30f, 0.69f, 0.31f), // Green
            new(0.94f, 0.33f, 0.31f), // Red
            new(0.93f, 0.69f, 0.13f), // Amber
            new(0.61f, 0.15f, 0.69f), // Purple
            new(0.00f, 0.74f, 0.83f), // Cyan
            new(0.96f, 0.49f, 0.00f), // Orange
            new(0.91f, 0.47f, 0.76f), // Pink
        };

        private static Dictionary<string, Color> affiliationColors;

        // ═══════════════════════════════════════════════════════════════════════════
        // WINDOW SETUP
        // ═══════════════════════════════════════════════════════════════════════════

        [MenuItem("Tools/PlayForge/Runtime Tools/GAS Debugger")]
        public static void ShowWindow()
        {
            var window = GetWindow<GASDebugger>();
            window.titleContent = new GUIContent("GAS Debug", EditorGUIUtility.IconContent("d_UnityEditor.DebugInspectorWindow").image);
            window.minSize = new Vector2(800, 500);
        }

        private void OnEnable()
        {
            _effectShelfField = typeof(GameplayAbilitySystem).GetField("EffectShelf",
                BindingFlags.NonPublic | BindingFlags.Instance);
            _tagCacheField = typeof(GameplayAbilitySystem).GetField("TagCache",
                BindingFlags.NonPublic | BindingFlags.Instance);
            affiliationColors = new Dictionary<string, Color>();

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
            _expandedAttributes.Clear();
            _expandedAbilities.Clear();
            _overviewAttributesInitialized = false;
            _frameSummaryStats.Clear();

            EditorApplication.delayCall += () =>
            {
                if (this != null && rootVisualElement != null)
                {
                    RefreshEntityList();
                    RefreshRightPanel();
                    UpdatePlayModeIndicator();
                }
            };
        }

        private void UpdatePlayModeIndicator()
        {
            var indicator = rootVisualElement?.Q<Label>("play-mode-indicator");
            if (indicator != null)
            {
                indicator.text = Application.isPlaying ? "\u25b6 PLAYING" : "\u23f8 EDITOR";
                indicator.style.color = Application.isPlaying
                    ? new Color(0.4f, 0.9f, 0.4f)
                    : new Color(0.7f, 0.7f, 0.7f);
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // GUI CONSTRUCTION
        // ═══════════════════════════════════════════════════════════════════════════

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = BackgroundDark;

            // Toolbar
            root.Add(CreateToolbar());

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

            // Right panel - Tabs
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

            RefreshEntityList();
            UpdatePlayModeIndicator();
        }

        private void Update()
        {
            if (!_autoRefresh) return;
            if (EditorApplication.timeSinceStartup - _lastRefreshTime <= _refreshInterval) return;
            _lastRefreshTime = EditorApplication.timeSinceStartup;

            int currentCount = FindObjectsByType<GameplayAbilitySystem>(FindObjectsSortMode.None).Length;
            if (currentCount != _gasObjects.Count)
            {
                RefreshEntityList();
            }

            // Ensure every live GAS has frame-summary hooks attached. Cheap check:
            // _frameSummaryStats already has the GAS registered on first hook.
            EnsureFrameSummaryHooks();

            if (_activeTab == ETab.Details && _selectedGAS != null)
            {
                RefreshDetails();
            }
            else if (_activeTab == ETab.Overview)
            {
                RefreshOverviewTable();
            }
        }

        /// <summary>
        /// Subscribe to OnFrameComplete on any GAS we haven't seen yet.
        /// The subscription appends a rolling sample + last-significant-impact tracker.
        /// </summary>
        private void EnsureFrameSummaryHooks()
        {
            foreach (var gas in _gasObjects)
            {
                if (gas == null) continue;
                if (_frameSummaryStats.ContainsKey(gas)) continue;

                var callbacks = gas.Callbacks;
                if (callbacks == null) continue;

                var stats = new FrameSummaryRollingStats();
                _frameSummaryStats[gas] = stats;

                // Capture the GAS reference for the closure
                var capturedGas = gas;
                GameplayAbilitySystemCallbacks.FrameCompleteDelegate onFrame = snapshot =>
                {
                    if (!_frameSummaryStats.TryGetValue(capturedGas, out var s)) return;
                    RecordFrameSummarySample(s, snapshot);
                };
                callbacks.OnFrameComplete += onFrame;

                GameplayAbilitySystemCallbacks.ImpactDelegate onDealt = impact =>
                {
                    if (!_frameSummaryStats.TryGetValue(capturedGas, out var s)) return;
                    if (Mathf.Abs(impact.RealImpact.CurrentValue) > _significantImpactThreshold)
                        s.LastSignificantDealt = impact;
                };
                GameplayAbilitySystemCallbacks.ImpactDelegate onReceived = impact =>
                {
                    if (!_frameSummaryStats.TryGetValue(capturedGas, out var s)) return;
                    if (Mathf.Abs(impact.RealImpact.CurrentValue) > _significantImpactThreshold)
                        s.LastSignificantReceived = impact;
                };
                callbacks.OnImpactDealt += onDealt;
                callbacks.OnImpactReceived += onReceived;
            }
        }

        private void RecordFrameSummarySample(FrameSummaryRollingStats stats, FrameSummarySnapshot snapshot)
        {
            float dDmg = 0f, dHeal = 0f, rDmg = 0f, rHeal = 0f;
            for (int i = 0; i < snapshot.ImpactsDealt.Count; i++)
            {
                var v = snapshot.ImpactsDealt[i].RealImpact.CurrentValue;
                if (v < 0) dDmg += -v; else if (v > 0) dHeal += v;
            }
            for (int i = 0; i < snapshot.ImpactsReceived.Count; i++)
            {
                var v = snapshot.ImpactsReceived[i].RealImpact.CurrentValue;
                if (v < 0) rDmg += -v; else if (v > 0) rHeal += v;
            }

            var sample = new FrameSummarySample
            {
                ImpactCount = snapshot.Impacts.Count,
                DealtCount = snapshot.ImpactsDealt.Count,
                ReceivedCount = snapshot.ImpactsReceived.Count,
                TotalDamageDealt = dDmg,
                TotalHealingDealt = dHeal,
                TotalDamageReceived = rDmg,
                TotalHealingReceived = rHeal,
                ExecutedActions = snapshot.Executed,
                InvalidatedActions = snapshot.Invalidated
            };

            stats.Samples.Enqueue(sample);
            while (stats.Samples.Count > Mathf.Max(1, _frameSummaryWindowFrames))
                stats.Samples.Dequeue();
            stats.LastSnapshotFrame = Time.frameCount;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // TOOLBAR
        // ═══════════════════════════════════════════════════════════════════════════

        private VisualElement CreateToolbar()
        {
            var toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8, paddingRight = 8,
                    paddingTop = 4, paddingBottom = 4,
                    backgroundColor = BackgroundMedium,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.15f, 0.15f, 0.15f)
                }
            };

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

            var refreshBtn = new Button(() => { RefreshEntityList(); RefreshRightPanel(); })
            {
                text = "\u21bb Refresh",
                style = { marginRight = 8 }
            };
            toolbar.Add(refreshBtn);

            var autoRefreshToggle = new Toggle("Auto-refresh") { value = _autoRefresh };
            autoRefreshToggle.RegisterValueChangedCallback(evt => _autoRefresh = evt.newValue);
            autoRefreshToggle.style.marginRight = 16;
            toolbar.Add(autoRefreshToggle);

            toolbar.Add(new Label("Interval:") { style = { marginRight = 4 } });
            var intervalField = new FloatField { value = _refreshInterval, style = { width = 50 } };
            intervalField.RegisterValueChangedCallback(evt => _refreshInterval = Mathf.Max(0.1f, evt.newValue));
            toolbar.Add(intervalField);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            var playModeLabel = new Label(Application.isPlaying ? "\u25b6 PLAYING" : "\u23f8 EDITOR")
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

        // ═══════════════════════════════════════════════════════════════════════════
        // LEFT PANEL (Entity List)
        // ═══════════════════════════════════════════════════════════════════════════

        private VisualElement CreateLeftPanel()
        {
            var panel = new VisualElement
            {
                style =
                {
                    width = 260,
                    borderRightWidth = 1,
                    borderRightColor = new Color(0.15f, 0.15f, 0.15f),
                    backgroundColor = BackgroundDark
                }
            };

            // Header with sort dropdown
            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8, paddingRight = 8,
                    paddingTop = 6, paddingBottom = 6,
                    backgroundColor = BackgroundMedium
                }
            };

            header.Add(new Label("Entities")
            {
                style =
                {
                    fontSize = 11,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = new Color(0.8f, 0.8f, 0.8f),
                    marginRight = 8
                }
            });

            // Sort mode dropdown
            var sortField = new EnumField(_sortMode)
            {
                style = { width = 100, height = 18 }
            };
            sortField.RegisterValueChangedCallback(evt =>
            {
                _sortMode = (ESortMode)evt.newValue;
                RefreshEntityList();
            });
            header.Add(sortField);

            header.Add(new VisualElement { style = { flexGrow = 1 } });
            header.Add(new Label { name = "entity-count", style = { fontSize = 10, color = new Color(0.5f, 0.5f, 0.5f) } });
            panel.Add(header);

            _entityList = new ScrollView { style = { flexGrow = 1 } };
            panel.Add(_entityList);

            return panel;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // RIGHT PANEL (Tabs)
        // ═══════════════════════════════════════════════════════════════════════════

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

            // Tab bar (fixed-height, never shrinks)
            _tabBar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexShrink = 0,
                    backgroundColor = BackgroundMedium,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.15f, 0.15f, 0.15f)
                }
            };
            _tabBar.Add(CreateTabButton("Details", ETab.Details));
            _tabBar.Add(CreateTabButton("Overview", ETab.Overview));
            panel.Add(_tabBar);

            // Details content — must flex-grow so ScrollView has a bounded height
            var detailsContainer = new VisualElement
            {
                name = "details-container",
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    minHeight = 0
                }
            };

            var detailsHeader = new VisualElement
            {
                name = "details-header",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexShrink = 0,
                    alignItems = Align.Center,
                    paddingLeft = 12, paddingRight = 12,
                    paddingTop = 10, paddingBottom = 10,
                    marginTop = 2,
                    backgroundColor = BackgroundMedium,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.15f, 0.15f, 0.15f)
                }
            };
            detailsHeader.Add(new Label("Select an entity to view details")
            {
                name = "details-title",
                style =
                {
                    fontSize = 12,
                    color = new Color(0.6f, 0.6f, 0.6f),
                    unityFontStyleAndWeight = FontStyle.Italic
                }
            });
            detailsContainer.Add(detailsHeader);

            _detailsScroll = new ScrollView
            {
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    minHeight = 0,
                    paddingLeft = 12, paddingRight = 12,
                    paddingTop = 8, paddingBottom = 8
                }
            };
            detailsContainer.Add(_detailsScroll);
            panel.Add(detailsContainer);

            // Overview content
            _overviewContainer = new VisualElement
            {
                name = "overview-container",
                style = { flexGrow = 1, display = DisplayStyle.None }
            };
            panel.Add(_overviewContainer);

            BuildOverviewContent();
            UpdateTabVisuals();

            return panel;
        }

        private VisualElement CreateTabButton(string label, ETab tab)
        {
            bool isActive = _activeTab == tab;
            var btn = new Button(() =>
            {
                _activeTab = tab;
                UpdateTabVisuals();
                RefreshRightPanel();
            })
            {
                text = label,
                name = $"tab-{tab}",
                style =
                {
                    paddingLeft = 16, paddingRight = 16,
                    paddingTop = 6, paddingBottom = 6,
                    borderBottomWidth = 2,
                    borderBottomColor = isActive ? HeaderColor : Color.clear,
                    backgroundColor = Color.clear,
                    color = isActive ? HeaderColor : new Color(0.6f, 0.6f, 0.6f),
                    unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal,
                    borderTopLeftRadius = 0, borderTopRightRadius = 0,
                    borderBottomLeftRadius = 0, borderBottomRightRadius = 0
                }
            };
            return btn;
        }

        private void UpdateTabVisuals()
        {
            // Update tab button styles
            foreach (ETab tab in Enum.GetValues(typeof(ETab)))
            {
                var btn = _tabBar?.Q<Button>($"tab-{tab}");
                if (btn == null) continue;
                bool isActive = _activeTab == tab;
                btn.style.borderBottomColor = isActive ? HeaderColor : Color.clear;
                btn.style.color = isActive ? HeaderColor : new Color(0.6f, 0.6f, 0.6f);
                btn.style.unityFontStyleAndWeight = isActive ? FontStyle.Bold : FontStyle.Normal;
            }

            // Show/hide containers
            var detailsContainer = _rightPanel?.Q("details-container");
            if (detailsContainer != null)
                detailsContainer.style.display = _activeTab == ETab.Details ? DisplayStyle.Flex : DisplayStyle.None;

            if (_overviewContainer != null)
                _overviewContainer.style.display = _activeTab == ETab.Overview ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RefreshRightPanel()
        {
            if (_activeTab == ETab.Details)
                RefreshDetails();
            else
                RefreshOverviewTable();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ENTITY LIST
        // ═══════════════════════════════════════════════════════════════════════════

        private void RefreshEntityList()
        {
            _gasObjects.Clear();
            _gasObjects.AddRange(FindObjectsByType<GameplayAbilitySystem>(FindObjectsSortMode.None));

            if (_selectedGAS != null && !_gasObjects.Contains(_selectedGAS))
                _selectedGAS = null;

            _entityList ??= new ScrollView { style = { flexGrow = 1 } };
            _entityList.Clear();

            var countLabel = _leftPanel?.Q<Label>("entity-count");
            if (countLabel != null)
                countLabel.text = $"({_gasObjects.Count})";

            if (_gasObjects.Count == 0)
            {
                _entityList.Add(new Label("No GAS objects in scene")
                {
                    style =
                    {
                        color = new Color(0.5f, 0.5f, 0.5f),
                        unityFontStyleAndWeight = FontStyle.Italic,
                        paddingLeft = 12, paddingTop = 12
                    }
                });
                _statusLabel.text = $"Found 0 GAS objects | {DateTime.Now:HH:mm:ss}";
                UpdatePlayModeIndicator();
                return;
            }

            var sorted = GetSortedEntities();

            if (_sortMode == ESortMode.Affiliation)
            {
                BuildAffiliationGroupedList(sorted);
            }
            else
            {
                foreach (var gas in sorted)
                {
                    _entityList.Add(CreateEntityListItem(gas));
                }
            }

            _statusLabel.text = $"Found {_gasObjects.Count} GAS objects | {DateTime.Now:HH:mm:ss}";
            UpdatePlayModeIndicator();
        }

        private List<GameplayAbilitySystem> GetSortedEntities()
        {
            return _sortMode switch
            {
                ESortMode.Alphabetical => _gasObjects
                    .OrderBy(g => GetDisplayName(g))
                    .ToList(),
                ESortMode.Affiliation => _gasObjects
                    .OrderBy(g => GetFirstAffiliationName(g))
                    .ThenBy(g => GetDisplayName(g))
                    .ToList(),
                ESortMode.CacheIndex => _gasObjects
                    .OrderBy(g => GetCacheIndex(g))
                    .ToList(),
                _ => _gasObjects
            };
        }

        private void BuildAffiliationGroupedList(List<GameplayAbilitySystem> sorted)
        {
            string lastGroup = null;

            foreach (var gas in sorted)
            {
                string group = GetFirstAffiliationName(gas);
                if (group != lastGroup)
                {
                    lastGroup = group;
                    var groupColor = GetAffiliationColor(group);

                    var groupHeader = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            paddingLeft = 8, paddingRight = 8,
                            paddingTop = 4, paddingBottom = 4,
                            backgroundColor = new Color(groupColor.r, groupColor.g, groupColor.b, 0.1f),
                            borderBottomWidth = 1,
                            borderBottomColor = new Color(groupColor.r, groupColor.g, groupColor.b, 0.3f)
                        }
                    };

                    // Colored dot
                    groupHeader.Add(new VisualElement
                    {
                        style =
                        {
                            width = 8, height = 8,
                            backgroundColor = groupColor,
                            borderTopLeftRadius = 4, borderTopRightRadius = 4,
                            borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                            marginRight = 6
                        }
                    });

                    groupHeader.Add(new Label(group)
                    {
                        style =
                        {
                            fontSize = 10,
                            unityFontStyleAndWeight = FontStyle.Bold,
                            color = groupColor
                        }
                    });

                    _entityList.Add(groupHeader);
                }

                _entityList.Add(CreateEntityListItem(gas));
            }
        }

        private VisualElement CreateEntityListItem(GameplayAbilitySystem gas)
        {
            bool isSelected = gas == _selectedGAS;
            string displayName = GetDisplayName(gas);
            string affiliation = GetFirstAffiliationName(gas);
            Color affColor = GetAffiliationColor(affiliation);

            var item = new VisualElement
            {
                name = $"entity-item-{gas.GetInstanceID()}",
                userData = gas,
                pickingMode = PickingMode.Position,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 8, paddingRight = 8,
                    paddingTop = 6, paddingBottom = 6,
                    backgroundColor = isSelected ? SelectedColor : Color.clear,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.15f, 0.15f, 0.15f)
                }
            };

            item.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    SelectEntity(gas);
                    evt.StopPropagation();
                }
            });

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

            // Icon with affiliation-colored background
            var iconContainer = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = 18, height = 18,
                    backgroundColor = new Color(affColor.r, affColor.g, affColor.b, 0.35f),
                    borderTopLeftRadius = 3, borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    marginRight = 6
                }
            };
            iconContainer.Add(new Label("\u25c6")
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    color = affColor,
                    fontSize = 9,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            });
            item.Add(iconContainer);

            // Name
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

            // Cache index badge (only in CacheIndex sort mode)
            if (_sortMode == ESortMode.CacheIndex)
            {
                int idx = GetCacheIndex(gas);
                var idxBadge = CreateBadge($"#{idx}", new Color(0.5f, 0.5f, 0.5f));
                idxBadge.pickingMode = PickingMode.Ignore;
                idxBadge.style.marginRight = 4;
                item.Add(idxBadge);
            }

            // Level badge
            if (gas.EntityData != null)
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

            _expandedAttributes.Clear();
            _expandedAbilities.Clear();

            UpdateEntitySelectionVisuals(previousSelection, gas);

            if (_activeTab == ETab.Details)
                RefreshDetails();
        }

        private void UpdateEntitySelectionVisuals(GameplayAbilitySystem previous, GameplayAbilitySystem current)
        {
            if (previous != null)
            {
                try
                {
                    var prevItem = _entityList.Q<VisualElement>($"entity-item-{previous.GetInstanceID()}");
                    if (prevItem != null)
                    {
                        prevItem.style.backgroundColor = Color.clear;
                        var nameLabel = prevItem.Q<Label>("entity-name");
                        if (nameLabel != null) nameLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
                    }
                }
                catch (MissingReferenceException) { }
            }

            if (current != null)
            {
                try
                {
                    var currItem = _entityList.Q<VisualElement>($"entity-item-{current.GetInstanceID()}");
                    if (currItem != null)
                    {
                        currItem.style.backgroundColor = SelectedColor;
                        var nameLabel = currItem.Q<Label>("entity-name");
                        if (nameLabel != null) nameLabel.style.color = Color.white;
                    }
                }
                catch (MissingReferenceException)
                {
                    _selectedGAS = null;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // DETAILS TAB
        // ═══════════════════════════════════════════════════════════════════════════

        private void RefreshDetails()
        {
            // Preserve scroll position across rebuild so sections that
            // populate/unpopulate (e.g. effects) don't cause the view to jump.
            var savedOffset = _detailsScroll?.scrollOffset ?? Vector2.zero;

            _detailsScroll.Clear();

            var titleLabel = _rightPanel.Q<Label>("details-title");

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

            try { _ = _selectedGAS.gameObject; }
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

            if (titleLabel != null)
            {
                titleLabel.text = GetDisplayName(_selectedGAS);
                titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleLabel.style.color = HeaderColor;
                ConfigureTitleGameObjectShortcut(titleLabel, _selectedGAS);
            }

            _detailsScroll.Add(CreateEntityInfoSection());
            _detailsScroll.Add(CreateAttributesSection());
            _detailsScroll.Add(CreateAbilitiesSection());
            _detailsScroll.Add(CreateTagsSection());
            _detailsScroll.Add(CreateEffectsSection());
            _detailsScroll.Add(CreateFrameSummarySection());
            _detailsScroll.Add(CreateWorkersSection());

            // Restore scroll position after layout is recalculated.
            // Using GeometryChangedEvent once so we only set it after the
            // new content has real dimensions — otherwise the offset would
            // be clamped to zero against stale (empty) content height.
            RestoreScrollOffset(savedOffset);
        }

        private void RestoreScrollOffset(Vector2 offset)
        {
            if (_detailsScroll == null) return;
            if (offset.sqrMagnitude <= 0.0001f) return;

            void OnGeometryChanged(GeometryChangedEvent _)
            {
                _detailsScroll.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);

                // Clamp against the new content so we don't over-scroll when
                // content shrinks (e.g. an effect section collapsing).
                var content = _detailsScroll.contentContainer;
                var viewport = _detailsScroll.contentViewport;
                float maxY = Mathf.Max(0f, content.layout.height - viewport.layout.height);
                float maxX = Mathf.Max(0f, content.layout.width - viewport.layout.width);

                _detailsScroll.scrollOffset = new Vector2(
                    Mathf.Clamp(offset.x, 0f, maxX),
                    Mathf.Clamp(offset.y, 0f, maxY));
            }

            _detailsScroll.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        /// <summary>
        /// Makes the details title behave as an interactive button:
        ///   • Single-click  → pings the GameObject in the Hierarchy
        ///   • Double-click  → selects it as the active editor object
        /// </summary>
        private void ConfigureTitleGameObjectShortcut(Label titleLabel, GameplayAbilitySystem gas)
        {
            titleLabel.tooltip = "Click to ping in Hierarchy, double-click to select";
            titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            titleLabel.pickingMode = PickingMode.Position;
            titleLabel.RegisterCallback<MouseEnterEvent>(_ =>
            {
                titleLabel.style.color = new Color(
                    Mathf.Min(1f, HeaderColor.r + 0.1f),
                    Mathf.Min(1f, HeaderColor.g + 0.1f),
                    Mathf.Min(1f, HeaderColor.b + 0.1f));
            });
            titleLabel.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                titleLabel.style.color = HeaderColor;
            });
            titleLabel.RegisterCallback<ClickEvent>(evt =>
            {
                if (_selectedGAS == null) return;
                try
                {
                    var go = _selectedGAS.gameObject;
                    if (go == null) return;
                    if (evt.clickCount >= 2)
                    {
                        Selection.activeGameObject = go;
                        EditorGUIUtility.PingObject(go);
                    }
                    else
                    {
                        EditorGUIUtility.PingObject(go);
                    }
                }
                catch (MissingReferenceException) { }
            });
        }

        private VisualElement CreateFrameSummarySection()
        {
            var section = CreateSection("Frame Summary", ImpactColor);

            // ── Configuration row (window size + threshold) ──────────────────
            var configRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 6 }
            };
            configRow.Add(new Label("Avg window (frames):")
            {
                style = { fontSize = 10, color = new Color(0.65f, 0.65f, 0.65f), marginRight = 4 }
            });
            var windowField = new IntegerField { value = _frameSummaryWindowFrames, style = { width = 60, marginRight = 12 } };
            windowField.RegisterValueChangedCallback(evt =>
                _frameSummaryWindowFrames = Mathf.Clamp(evt.newValue, 1, 600));
            configRow.Add(windowField);

            configRow.Add(new Label("Significant ≥")
            {
                style = { fontSize = 10, color = new Color(0.65f, 0.65f, 0.65f), marginRight = 4 }
            });
            var threshField = new FloatField { value = _significantImpactThreshold, style = { width = 60 } };
            threshField.RegisterValueChangedCallback(evt =>
                _significantImpactThreshold = Mathf.Max(0f, evt.newValue));
            configRow.Add(threshField);
            section.Add(configRow);

            // ── Averages / totals over the window ────────────────────────────
            if (!_frameSummaryStats.TryGetValue(_selectedGAS, out var stats) || stats.Samples.Count == 0)
            {
                section.Add(CreateEmptyLabel("No frame data yet"));
            }
            else
            {
                int n = stats.Samples.Count;
                double impacts = 0, dealt = 0, received = 0;
                double dDmg = 0, dHeal = 0, rDmg = 0, rHeal = 0;
                double executed = 0, invalidated = 0;
                foreach (var s in stats.Samples)
                {
                    impacts += s.ImpactCount;
                    dealt += s.DealtCount;
                    received += s.ReceivedCount;
                    dDmg += s.TotalDamageDealt;
                    dHeal += s.TotalHealingDealt;
                    rDmg += s.TotalDamageReceived;
                    rHeal += s.TotalHealingReceived;
                    executed += s.ExecutedActions;
                    invalidated += s.InvalidatedActions;
                }

                section.Add(new Label($"Averages over last {n} frame(s):")
                {
                    style = { fontSize = 10, color = new Color(0.55f, 0.55f, 0.55f), marginBottom = 2 }
                });
                section.Add(CreateDetailRow("Impacts / frame", (impacts / n).ToString("F2"), Color.white));
                section.Add(CreateDetailRow("  Dealt / frame", (dealt / n).ToString("F2"), Color.white));
                section.Add(CreateDetailRow("  Received / frame", (received / n).ToString("F2"), Color.white));
                section.Add(CreateDetailRow("Damage dealt / frame", (dDmg / n).ToString("F2"), new Color(0.95f, 0.45f, 0.4f)));
                section.Add(CreateDetailRow("Healing dealt / frame", (dHeal / n).ToString("F2"), new Color(0.45f, 0.9f, 0.5f)));
                section.Add(CreateDetailRow("Damage received / frame", (rDmg / n).ToString("F2"), new Color(0.95f, 0.45f, 0.4f)));
                section.Add(CreateDetailRow("Healing received / frame", (rHeal / n).ToString("F2"), new Color(0.45f, 0.9f, 0.5f)));
                section.Add(CreateDetailRow("Actions executed / frame", (executed / n).ToString("F2"), new Color(0.7f, 0.7f, 0.9f)));
                section.Add(CreateDetailRow("Actions invalidated / frame", (invalidated / n).ToString("F2"), new Color(0.8f, 0.6f, 0.4f)));
            }

            // ── Last significant impact (dealt / received) ──────────────────
            section.Add(CreateSubHeader("Last Significant Impact"));
            if (stats != null && stats.LastSignificantDealt.HasValue)
            {
                section.Add(CreateImpactDetailRow("Dealt", stats.LastSignificantDealt.Value));
            }
            else
            {
                section.Add(CreateEmptyLabel("No significant impact dealt"));
            }

            if (stats != null && stats.LastSignificantReceived.HasValue)
            {
                section.Add(CreateImpactDetailRow("Received", stats.LastSignificantReceived.Value));
            }
            else
            {
                section.Add(CreateEmptyLabel("No significant impact received"));
            }

            return section;
        }

        /// <summary>
        /// Render a one-line summary of an impact: "[direction] attribute ±N (from/to name)".
        /// </summary>
        private VisualElement CreateImpactDetailRow(string direction, ImpactData impact)
        {
            string attrName = impact.Attribute?.GetName() ?? "?";
            float delta = impact.RealImpact.CurrentValue;
            string sign = delta < 0 ? "" : "+";
            Color valueColor = delta < 0
                ? new Color(0.95f, 0.45f, 0.4f)
                : new Color(0.45f, 0.9f, 0.5f);

            string counterparty;
            if (direction == "Dealt")
            {
                counterparty = ResolveDisplayNameFor(impact.Target);
            }
            else // Received
            {
                counterparty = ResolveDisplayNameFor(impact.SourcedModifier.Derivation?.GetSource());
            }

            string text = $"{attrName} {sign}{delta:F2}  ({(direction == "Dealt" ? "→" : "←")} {counterparty})";
            return CreateDetailRow(direction, text, valueColor);
        }

        private VisualElement CreateWorkersSection()
        {
            var section = CreateSection("Workers", new Color(0.85f, 0.55f, 0.25f));

            // ── Impact workers (via AbilitySystem) ──────────────────────────
            section.Add(CreateSubHeader("Impact Workers"));
            if (_selectedGAS.FindAbilitySystem(out var abilSys))
            {
                var impactWorkers = CollectImpactWorkers(abilSys);
                if (impactWorkers.Count == 0)
                {
                    section.Add(CreateEmptyLabel("No impact workers"));
                }
                else
                {
                    foreach (var kv in impactWorkers)
                    {
                        string attrName = kv.Key?.GetName() ?? "(any)";
                        section.Add(CreateDetailRow(attrName, $"{kv.Value.Count} worker(s)", Color.white));
                        foreach (var w in kv.Value)
                            section.Add(CreateDetailRow("  " + WorkerTypeName(w), $"[{w.Execution}]", new Color(0.7f, 0.7f, 0.7f)));
                    }
                }
            }
            else
            {
                section.Add(CreateEmptyLabel("No ability system"));
            }

            // ── Analysis workers (via GAS) ──────────────────────────────────
            section.Add(CreateSubHeader("Analysis Workers"));
            var analysisCache = _selectedGAS.GetAnalysisCache();
            if (analysisCache == null || analysisCache.Workers.Count == 0)
            {
                section.Add(CreateEmptyLabel("No analysis workers"));
            }
            else
            {
                foreach (var w in analysisCache.Workers)
                    section.Add(CreateDetailRow(WorkerTypeName(w), w.GetType().Namespace ?? "", new Color(0.8f, 0.8f, 0.8f)));
            }

            // ── Tag workers (via TagCache reflection field) ─────────────────
            section.Add(CreateSubHeader("Tag Workers"));
            var tagCache = _tagCacheField?.GetValue(_selectedGAS) as TagCache;
            if (tagCache == null)
            {
                section.Add(CreateEmptyLabel("No tag cache"));
            }
            else
            {
                int reg = tagCache.RegisteredWorkerCount;
                int act = tagCache.ActiveWorkerCount;
                section.Add(CreateInfoRow("Registered", reg.ToString(), Color.white));
                section.Add(CreateInfoRow("Active", act.ToString(), act > 0 ? ActiveTagColor : new Color(0.55f, 0.55f, 0.55f)));

                foreach (var w in tagCache.RegisteredWorkers)
                {
                    bool isActive = tagCache.IsWorkerActive(w);
                    section.Add(CreateDetailRow(
                        WorkerTypeName(w),
                        isActive ? "ACTIVE" : "idle",
                        isActive ? ActiveTagColor : new Color(0.55f, 0.55f, 0.55f)));
                }
            }

            return section;
        }

        private static Dictionary<IAttribute, List<AbstractImpactWorker>> CollectImpactWorkers(AbilitySystemComponent abilSys)
        {
            var result = new Dictionary<IAttribute, List<AbstractImpactWorker>>();
            var field = typeof(AbilitySystemComponent).GetField("ImpactWorkerCache",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            var cache = field?.GetValue(abilSys) as ImpactWorkerCache;
            if (cache == null) return result;
            foreach (var kv in cache.Cache)
            {
                result[kv.Key] = new List<AbstractImpactWorker>(kv.Value);
            }
            return result;
        }

        /// <summary>
        /// Best-effort name for an ITarget/ISource: EntityData name → GameObject name → "unknown".
        /// </summary>
        private static string ResolveDisplayNameFor(ITarget target)
        {
            if (target == null) return "unknown";
            var gasObj = target.ToGAS()?.ToGASObject();
            if (gasObj != null)
            {
                if (gasObj.EntityData != null && !string.IsNullOrEmpty(gasObj.EntityData.GetName()))
                    return gasObj.EntityData.GetName();
                if (gasObj.gameObject != null) return gasObj.gameObject.name;
            }
            return "unknown";
        }

        private static string WorkerTypeName(object worker)
        {
            if (worker == null) return "(null)";
            var t = worker.GetType();
            return t.Name;
        }

        private VisualElement CreateEntityInfoSection()
        {
            var section = CreateSection("Entity Info", HeaderColor);

            if (_selectedGAS.EntityData == null)
            {
                section.Add(CreateInfoRow("Status", "No EntityIdentity assigned", Color.red));
                return section;
            }

            var data = _selectedGAS.EntityData;
            var level = _selectedGAS.GetLevel();
            
            section.Add(CreateInfoRow("Name", data.GetName(), Color.white));
            section.Add(CreateInfoRow("Level", $"{level.CurrentValue} / {level.MaxValue}", Color.white));
            section.Add(CreateInfoRow("Asset Tag", data.AssetTag.GetName(), new Color(0.7f, 0.7f, 0.7f)));
            section.Add(CreateInfoRow("Cache Index", GetCacheIndex(_selectedGAS) < 0 ? "(Unregistered)" : GetCacheIndex(_selectedGAS).ToString(), new Color(0.7f, 0.7f, 0.7f)));

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
                    paddingBottom = 4, marginBottom = 4,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };
            headerRow.Add(new Label("") { style = { width = 20 } });
            headerRow.Add(new Label("Attribute") { style = { width = 130, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10 } });
            headerRow.Add(new Label("Current") { style = { width = 70, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10, unityTextAlign = TextAnchor.MiddleRight } });
            headerRow.Add(new Label("Base") { style = { width = 70, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10, unityTextAlign = TextAnchor.MiddleRight } });
            headerRow.Add(new Label("Ratio") { style = { width = 60, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10, unityTextAlign = TextAnchor.MiddleRight } });
            section.Add(headerRow);

            foreach (var kvp in cache.OrderBy(k => k.Key?.GetName() ?? ""))
            {
                section.Add(CreateAttributeRow(kvp.Key, kvp.Value));
            }

            return section;
        }

        private VisualElement CreateAttributeRow(IAttribute attribute, CachedAttributeValue cached)
        {
            string attrKey = attribute != null ? attribute.GetName() : "Unknown";
            bool isExpanded = _expandedAttributes.Contains(attrKey);

            var container = new VisualElement();

            var row = new VisualElement
            {
                pickingMode = PickingMode.Position,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 2, paddingBottom = 2, paddingLeft = 2,
                    marginBottom = 1,
                    backgroundColor = isExpanded ? new Color(0.25f, 0.25f, 0.28f) : Color.clear,
                    borderTopLeftRadius = 2, borderTopRightRadius = 2,
                    borderBottomLeftRadius = isExpanded ? 0 : 2,
                    borderBottomRightRadius = isExpanded ? 0 : 2
                }
            };

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    if (_expandedAttributes.Contains(attrKey))
                        _expandedAttributes.Remove(attrKey);
                    else
                        _expandedAttributes.Add(attrKey);
                    RefreshDetails();
                    evt.StopPropagation();
                }
            });

            row.RegisterCallback<MouseEnterEvent>(_ => { if (!isExpanded) row.style.backgroundColor = HoverColor; });
            row.RegisterCallback<MouseLeaveEvent>(_ => { if (!isExpanded) row.style.backgroundColor = Color.clear; });

            row.Add(new Label(isExpanded ? "\u25bc" : "\u25b6")
            {
                pickingMode = PickingMode.Ignore,
                style = { width = 20, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 8, unityTextAlign = TextAnchor.MiddleCenter }
            });

            row.Add(new Label(attrKey)
            {
                pickingMode = PickingMode.Ignore,
                style = { width = 130, color = Color.white, fontSize = 11 }
            });

            float current = cached.ActiveValue.CurrentValue;
            row.Add(new Label(current.ToString("F2"))
            {
                pickingMode = PickingMode.Ignore,
                style = { width = 70, color = AttributeColor, fontSize = 11, unityTextAlign = TextAnchor.MiddleRight }
            });

            float baseVal = cached.ActiveValue.BaseValue;
            row.Add(new Label(baseVal.ToString("F2"))
            {
                pickingMode = PickingMode.Ignore,
                style = { width = 70, color = new Color(0.7f, 0.7f, 0.7f), fontSize = 11, unityTextAlign = TextAnchor.MiddleRight }
            });

            float ratio = baseVal > 0 ? current / baseVal : 0;
            Color ratioColor = ratio >= 1f ? TagColor : (ratio > 0.25f ? AttributeColor : Color.red);
            row.Add(new Label($"{ratio:P1}")
            {
                pickingMode = PickingMode.Ignore,
                style = { width = 60, color = ratioColor, fontSize = 11, unityTextAlign = TextAnchor.MiddleRight }
            });

            // Progress bar
            var progressContainer = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    flexGrow = 1, height = 6, marginLeft = 8,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f),
                    borderTopLeftRadius = 2, borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2, borderBottomRightRadius = 2
                }
            };
            progressContainer.Add(new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = Length.Percent(Mathf.Clamp01(ratio) * 100),
                    height = Length.Percent(100),
                    backgroundColor = ratioColor,
                    borderTopLeftRadius = 2, borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2, borderBottomRightRadius = 2
                }
            });
            row.Add(progressContainer);

            container.Add(row);

            if (isExpanded)
                container.Add(CreateAttributeDerivationsPanel(cached));

            return container;
        }

        private VisualElement CreateAttributeDerivationsPanel(CachedAttributeValue cached)
        {
            var panel = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.2f, 0.2f, 0.22f),
                    paddingLeft = 24, paddingRight = 8,
                    paddingTop = 4, paddingBottom = 6,
                    marginBottom = 4,
                    borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                    borderLeftWidth = 2, borderLeftColor = AttributeColor
                }
            };

            var rootRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 }
            };
            rootRow.Add(new Label("Root Value:") { style = { color = new Color(0.5f, 0.5f, 0.5f), fontSize = 10, width = 80 } });
            rootRow.Add(new Label(cached.RootValue.ToString()) { style = { color = new Color(0.7f, 0.7f, 0.7f), fontSize = 10 } });
            panel.Add(rootRow);

            panel.Add(new Label("Derivations:")
            {
                style = { color = DerivationColor, fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, marginTop = 4, marginBottom = 4 }
            });

            var retainedValues = cached.RetainedValues;
            if (retainedValues == null || retainedValues.Count == 0)
            {
                panel.Add(new Label("No derivations")
                {
                    style = { color = new Color(0.5f, 0.5f, 0.5f), fontSize = 10, unityFontStyleAndWeight = FontStyle.Italic, marginLeft = 8 }
                });
            }
            else
            {
                foreach (var kvp in retainedValues)
                    panel.Add(CreateDerivationGroupRow(kvp.Key, kvp.Value));
            }

            return panel;
        }

        private VisualElement CreateDerivationGroupRow(Tag groupTag, RetainedCachedValue rcv)
        {
            var group = new VisualElement
            {
                style = { marginBottom = 4, paddingLeft = 8, borderLeftWidth = 1, borderLeftColor = DerivationColor }
            };

            var header = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 }
            };

            string groupName = $"{groupTag.GetName()} {(rcv.Derivations.Count > 0 ? !rcv.HasDerivations ? "(All Zombified)" : string.Empty : "(No Derivations)")}";
            header.Add(new Label(groupName)
            {
                style = { color = DerivationColor, fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 }
            });
            header.Add(new Label($"Total: {FormattedValueString(rcv.Value)}")
            {
                style = { color = AttributeColor, fontSize = 9 }
            });
            group.Add(header);

            if (rcv.Derivations != null)
            {
                foreach (var derivKvp in rcv.Derivations)
                {
                    var derivRow = new VisualElement
                    {
                        style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, paddingLeft = 8, marginBottom = 1 }
                    };

                    string sourceName = derivKvp.Key?.GetCacheKey().GetName() ?? "Unknown";
                    derivRow.Add(new Label($"\u2022 {sourceName}")
                    {
                        style = { color = new Color(0.6f, 0.6f, 0.6f), fontSize = 9 }
                    });

                    if (!derivKvp.Key?.DerivationAlive() ?? false)
                        derivRow.Add(CreateBadge("Zombie", ForgeDrawerStyles.Colors.BorderLight, "Attribute source no longer exists."));

                    derivRow.Add(new VisualElement { style = { flexGrow = 1 } });
                    derivRow.Add(new Label(FormattedValueString(derivKvp.Value))
                    {
                        style = { color = new Color(0.7f, 0.7f, 0.7f), fontSize = 9 }
                    });

                    group.Add(derivRow);
                }
            }

            return group;
        }

        private string FormattedValueString(AttributeValue value)
        {
            var s = "";
            s += value.CurrentValue > 0 ? $"+{value.CurrentValue}" : value.CurrentValue;
            s += "/";
            s += value.BaseValue > 0 ? $"+{value.BaseValue}" : value.BaseValue;
            return s;
        }

        private VisualElement CreateEffectsSection()
        {
            var section = CreateSection("Active Effects", EffectColor);

            var effectShelf = _effectShelfField?.GetValue(_selectedGAS) as List<AbstractEffectContainer>;

            if (effectShelf == null || effectShelf.Count == 0)
            {
                section.Add(CreateEmptyLabel("No active effects"));
                return section;
            }

            foreach (var container in effectShelf)
                section.Add(CreateEffectRow(container));

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
                    paddingTop = 4, paddingBottom = 4, paddingLeft = 4,
                    marginBottom = 2,
                    backgroundColor = new Color(EffectColor.r, EffectColor.g, EffectColor.b, 0.1f),
                    borderLeftWidth = 2, borderLeftColor = EffectColor,
                    borderTopLeftRadius = 2, borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2, borderBottomRightRadius = 2
                }
            };

            string effectName = container.Spec?.Base?.GetName() ?? "Unknown Effect";
            row.Add(new Label(effectName) { style = { flexGrow = 1, color = Color.white, fontSize = 11 } });

            var impact = container.Spec?.GetTrackedImpact() ?? new TrackedImpact();
            row.Add(CreateBadge(impact.Last.ToString(), ImpactColor));
            row.Add(CreateBadge(impact.Total.ToString(), ImpactColor));

            var durationPolicy = container.Spec?.Base?.DurationSpecification?.DurationPolicy ?? EEffectDurationPolicy.Instant;

            if (durationPolicy == EEffectDurationPolicy.Infinite)
            {
                row.Add(CreateBadge("\u221e", EffectColor, "Infinite"));
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
                section.Add(CreateAbilityRow(container));

            return section;
        }

        private VisualElement CreateAbilityRow(AbilitySpecContainer container)
        {
            string abilityKey = container.Spec?.Base?.GetName() ?? "Unknown";
            bool isExpanded = _expandedAbilities.Contains(abilityKey);

            var outerContainer = new VisualElement();

            var row = new VisualElement
            {
                pickingMode = PickingMode.Position,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 4, paddingBottom = 4, paddingLeft = 4,
                    marginBottom = isExpanded ? 0 : 2,
                    backgroundColor = new Color(AbilityColor.r, AbilityColor.g, AbilityColor.b, isExpanded ? 0.2f : 0.1f),
                    borderLeftWidth = 2,
                    borderLeftColor = container.IsClaiming ? TagColor : AbilityColor,
                    borderTopLeftRadius = 2, borderTopRightRadius = 2,
                    borderBottomLeftRadius = isExpanded ? 0 : 2,
                    borderBottomRightRadius = isExpanded ? 0 : 2
                }
            };

            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    if (_expandedAbilities.Contains(abilityKey))
                        _expandedAbilities.Remove(abilityKey);
                    else
                        _expandedAbilities.Add(abilityKey);
                    RefreshDetails();
                    evt.StopPropagation();
                }
            });

            row.Add(new Label(isExpanded ? "\u25bc" : "\u25b6")
            {
                pickingMode = PickingMode.Ignore,
                style = { width = 16, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 8, unityTextAlign = TextAnchor.MiddleCenter, marginRight = 2 }
            });

            if (container.IsClaiming)
            {
                row.Add(new Label("\u25b6")
                {
                    pickingMode = PickingMode.Ignore,
                    style = { color = TagColor, fontSize = 10, marginRight = 4 }
                });
            }

            row.Add(new Label(container.Spec?.Base?.GetName() ?? "Unknown Ability")
            {
                pickingMode = PickingMode.Ignore,
                style = { flexGrow = 1, color = Color.white, fontSize = 11 }
            });

            int level = container.Spec?.GetLevel().CurrentValue ?? 0;
            int maxLevel = container.Spec?.GetLevel().MaxValue ?? 1;
            var levelBadge = CreateBadge($"Lv.{level}/{maxLevel}", AbilityColor);
            levelBadge.pickingMode = PickingMode.Ignore;
            row.Add(levelBadge);

            if (container.IsActive)
            {
                var activeBadge = CreateBadge("ACTIVE", ActiveTagColor);
                activeBadge.pickingMode = PickingMode.Ignore;
                row.Add(activeBadge);
            }

            if (container.IsClaiming)
            {
                var claimBadge = CreateBadge("CLAIMED", TagColor);
                claimBadge.pickingMode = PickingMode.Ignore;
                row.Add(claimBadge);
            }
            
            if (container.IsCooldown)
            {
                var cooldownBadge = CreateBadge("COOLDOWN", CooldownColor);
                cooldownBadge.pickingMode = PickingMode.Ignore;
                row.Add(cooldownBadge);
            }

            outerContainer.Add(row);

            if (isExpanded)
                outerContainer.Add(CreateAbilityDetailsPanel(container));

            return outerContainer;
        }

        private VisualElement CreateAbilityDetailsPanel(AbilitySpecContainer container)
        {
            var panel = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.2f, 0.2f, 0.22f),
                    paddingLeft = 24, paddingRight = 8,
                    paddingTop = 6, paddingBottom = 8,
                    marginBottom = 4,
                    borderBottomLeftRadius = 4, borderBottomRightRadius = 4,
                    borderLeftWidth = 2, borderLeftColor = AbilityColor
                }
            };

            var ability = container.Spec?.Base;
            if (ability == null)
            {
                panel.Add(CreateEmptyLabel("No ability data"));
                return panel;
            }

            panel.Add(CreateDetailRow("Name", ability.GetName(), Color.white));
            panel.Add(CreateDetailRow("Description", ability.GetDescription() ?? "N/A", new Color(0.7f, 0.7f, 0.7f)));
            panel.Add(CreateDetailRow("Level", $"{container.Spec.GetLevel()} / {ability.MaxLevel}", AbilityColor));
            panel.Add(CreateDetailRow("Asset Tag", ability.Tags?.AssetTag.GetName() ?? "None", new Color(0.6f, 0.6f, 0.6f)));

            if (ability.Definition != null)
            {
                panel.Add(CreateSubHeader("Definition"));
                panel.Add(CreateDetailRow("  Activation Policy", ability.Definition.ActivationPolicy.ToString(), new Color(0.7f, 0.7f, 0.7f)));
            }

            if (ability.Cooldown != null)
            {
                panel.Add(CreateSubHeader("Cooldown"));
                panel.Add(CreateDetailRow("  Duration", $"{ability.Cooldown.DurationSpecification.DurationOperation.Magnitude}s", new Color(0.7f, 0.7f, 0.7f)));
            }

            if (ability.Cost != null && ability.Cost.ImpactSpecification is not null)
            {
                panel.Add(CreateSubHeader("Cost"));
                panel.Add(CreateDetailRow("  Attribute", ability.Cost.ImpactSpecification.AttributeTarget.GetName(), AttributeColor));
                panel.Add(CreateDetailRow("  Amount", ability.Cost.ImpactSpecification.MagnitudeOperation.Magnitude.ToString("F1"), new Color(0.7f, 0.7f, 0.7f)));
            }

            if (ability.Tags?.PassiveGrantedTags != null && ability.Tags.PassiveGrantedTags.Count > 0)
            {
                panel.Add(CreateSubHeader("Granted Tags"));
                var tagsContainer = new VisualElement
                {
                    style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginLeft = 8 }
                };
                foreach (var tag in ability.Tags.PassiveGrantedTags)
                {
                    var tagBadge = CreateBadge(tag.GetName(), TagColor);
                    tagBadge.style.marginRight = 4;
                    tagBadge.style.marginBottom = 2;
                    tagsContainer.Add(tagBadge);
                }
                panel.Add(tagsContainer);
            }

            panel.Add(CreateSubHeader("Runtime State"));
            panel.Add(CreateDetailRow("  Is Active", container.IsActive.ToString(), container.IsActive ? TagColor : new Color(0.5f, 0.5f, 0.5f)));
            panel.Add(CreateDetailRow("  Is Claiming", container.IsClaiming.ToString(), container.IsClaiming ? TagColor : new Color(0.5f, 0.5f, 0.5f)));

            return panel;
        }

        private VisualElement CreateTagsSection()
        {
            var section = CreateSection("Tags", TagColor);

            // var tagCache = _tagCacheField?.GetValue(_selectedGAS) as TagCache;
            var tagCache = _selectedGAS.GetTagCache();
            var tags = tagCache?.GetAppliedTags();

            if (tags == null || tags.Count == 0)
            {
                section.Add(CreateEmptyLabel("No active tags"));
                return section;
            }

            var tagContainer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap }
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

        // ═══════════════════════════════════════════════════════════════════════════
        // OVERVIEW TAB
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildOverviewContent()
        {
            _overviewContainer.Clear();

            // Attribute picker toggle bar
            var pickerBar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingLeft = 12, paddingRight = 12,
                    paddingTop = 6, paddingBottom = 6,
                    backgroundColor = BackgroundMedium
                }
            };

            var togglePickerBtn = new Button(() =>
            {
                _overviewPickerExpanded = !_overviewPickerExpanded;
                RefreshOverviewPicker();
            })
            {
                text = "Attributes \u25bc",
                style = { marginRight = 8 }
            };
            pickerBar.Add(togglePickerBtn);

            pickerBar.Add(new Label { name = "overview-attr-count", style = { fontSize = 10, color = new Color(0.5f, 0.5f, 0.5f) } });
            pickerBar.Add(new VisualElement { style = { flexGrow = 1 } });

            _overviewContainer.Add(pickerBar);

            // Picker panel (collapsible)
            _overviewAttributePicker = new VisualElement
            {
                name = "overview-picker",
                style =
                {
                    display = DisplayStyle.None,
                    backgroundColor = new Color(0.2f, 0.2f, 0.22f),
                    paddingLeft = 12, paddingRight = 12,
                    paddingTop = 8, paddingBottom = 8,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.15f, 0.15f, 0.15f)
                }
            };
            _overviewContainer.Add(_overviewAttributePicker);

            // Table scroll
            _overviewTableScroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal)
            {
                style = { flexGrow = 1, paddingLeft = 8, paddingRight = 8, paddingTop = 8 }
            };
            _overviewContainer.Add(_overviewTableScroll);
        }

        private void RefreshOverviewPicker()
        {
            _overviewAttributePicker.Clear();
            _overviewAttributePicker.style.display = _overviewPickerExpanded ? DisplayStyle.Flex : DisplayStyle.None;

            if (!_overviewPickerExpanded) return;

            // Collect all attribute names across all GAS entities
            var allAttributes = CollectAllAttributes();

            // Auto-select first 6 on first open
            if (!_overviewAttributesInitialized && allAttributes.Count > 0)
            {
                _overviewAttributesInitialized = true;
                foreach (var name in allAttributes.Take(6))
                    _selectedOverviewAttributes.Add(name);
            }

            // All / None buttons
            var btnRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginBottom = 6 }
            };
                
            // btnRow.Add(new Toggle("Abbreviate"));
            
            btnRow.Add(new Button(() =>
            {
                foreach (var name in allAttributes) _selectedOverviewAttributes.Add(name);
                RefreshOverviewPicker();
                RefreshOverviewTable();
            }) { text = "All", style = { marginRight = 4 } });
            btnRow.Add(new Button(() =>
            {
                _selectedOverviewAttributes.Clear();
                RefreshOverviewPicker();
                RefreshOverviewTable();
            }) { text = "None" });
            _overviewAttributePicker.Add(btnRow);

            // Attribute toggles in a wrap container
            var toggleContainer = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap }
            };

            foreach (var attrName in allAttributes)
            {
                bool selected = _selectedOverviewAttributes.Contains(attrName);
                var toggle = new Toggle(attrName.GetAbbreviation()) { value = selected };
                toggle.style.marginRight = 12;
                toggle.style.marginBottom = 2;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) _selectedOverviewAttributes.Add(attrName);
                    else _selectedOverviewAttributes.Remove(attrName);
                    UpdateOverviewAttrCount();
                    RefreshOverviewTable();
                });
                toggleContainer.Add(toggle);
            }

            _overviewAttributePicker.Add(toggleContainer);
            UpdateOverviewAttrCount();
        }

        private void UpdateOverviewAttrCount()
        {
            var countLabel = _overviewContainer?.Q<Label>("overview-attr-count");
            if (countLabel != null)
                countLabel.text = $"{_selectedOverviewAttributes.Count} attributes selected";
        }

        private void RefreshOverviewTable()
        {
            if (_overviewTableScroll == null) return;
            _overviewTableScroll.Clear();

            // Auto-initialize attribute selection if needed
            if (!_overviewAttributesInitialized)
            {
                var allAttrs = CollectAllAttributes();
                if (allAttrs.Count > 0)
                {
                    _overviewAttributesInitialized = true;
                    foreach (var name in allAttrs.Take(6))
                        _selectedOverviewAttributes.Add(name);
                    UpdateOverviewAttrCount();
                }
            }

            if (_gasObjects.Count == 0)
            {
                _overviewTableScroll.Add(CreateEmptyLabel("No GAS objects in scene"));
                return;
            }

            var selectedAttrs = _selectedOverviewAttributes.OrderBy(s => s.GetName()).ToList();
            if (selectedAttrs.Count == 0)
            {
                _overviewTableScroll.Add(CreateEmptyLabel("No attributes selected. Click 'Attributes' to configure."));
                return;
            }

            // Build table
            var table = new VisualElement();

            // Header row
            var headerRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    paddingBottom = 4, marginBottom = 4,
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };

            headerRow.Add(new Label("Entity")
            {
                style = { width = 160, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10, unityFontStyleAndWeight = FontStyle.Bold }
            });

            foreach (var attrName in selectedAttrs)
            {
                headerRow.Add(new Label(attrName.GetAbbreviation())
                {
                    style =
                    {
                        width = 80,
                        color = AttributeColor,
                        fontSize = 10,
                        unityFontStyleAndWeight = FontStyle.Bold,
                        unityTextAlign = TextAnchor.MiddleRight,
                        overflow = Overflow.Hidden,
                        textOverflow = TextOverflow.Ellipsis
                    },
                    tooltip = attrName.GetName()
                });
            }

            table.Add(headerRow);

            // Data rows
            var sorted = GetSortedEntities();
            foreach (var gas in sorted)
            {
                var dataRow = CreateOverviewRow(gas, selectedAttrs);
                table.Add(dataRow);
            }

            _overviewTableScroll.Add(table);
        }

        private VisualElement CreateOverviewRow(GameplayAbilitySystem gas, List<IAttribute> selectedAttrs)
        {
            string displayName = GetDisplayName(gas);
            string affiliation = GetFirstAffiliationName(gas);
            Color affColor = GetAffiliationColor(affiliation);
            bool isSelected = gas == _selectedGAS;

            var row = new VisualElement
            {
                pickingMode = PickingMode.Position,
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    paddingTop = 3, paddingBottom = 3, paddingLeft = 4,
                    marginBottom = 1,
                    backgroundColor = isSelected ? new Color(SelectedColor.r, SelectedColor.g, SelectedColor.b, 0.5f) : Color.clear,
                    borderTopLeftRadius = 2, borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2, borderBottomRightRadius = 2
                }
            };

            // Click to select and switch to details
            row.RegisterCallback<MouseDownEvent>(evt =>
            {
                if (evt.button == 0)
                {
                    _activeTab = ETab.Details;
                    UpdateTabVisuals();
                    SelectEntity(gas);
                    RefreshDetails();
                    evt.StopPropagation();
                }
            });

            row.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (gas != _selectedGAS) row.style.backgroundColor = HoverColor;
            });
            row.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (gas != _selectedGAS) row.style.backgroundColor = Color.clear;
            });

            // Affiliation dot + name
            var nameContainer = new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, width = 160 }
            };

            nameContainer.Add(new VisualElement
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    width = 6, height = 6,
                    backgroundColor = affColor,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                    marginRight = 6
                }
            });

            nameContainer.Add(new Label(displayName)
            {
                pickingMode = PickingMode.Ignore,
                style =
                {
                    color = Color.white, fontSize = 11,
                    overflow = Overflow.Hidden, textOverflow = TextOverflow.Ellipsis
                }
            });
            row.Add(nameContainer);

            // Attribute values
            IReadOnlyDictionary<IAttribute, CachedAttributeValue> cache = null;
            if (gas.FindAttributeSystem(out var attrSystem))
                cache = attrSystem.GetAttributeCache();

            foreach (var attr in selectedAttrs)
            {
                string valText = "-";
                Color valColor = new Color(0.4f, 0.4f, 0.4f);

                if (cache != null)
                {
                    var match = cache.FirstOrDefault(k => k.Key?.GetName() == attr.GetName());
                    if (match.Key != null)
                    {
                        float current = match.Value.ActiveValue.CurrentValue;
                        float baseVal = match.Value.ActiveValue.BaseValue;
                        valText = current.ToString("F1");

                        float ratio = baseVal > 0 ? current / baseVal : 0;
                        valColor = ratio >= 1f ? TagColor : (ratio > 0.25f ? AttributeColor : Color.red);
                    }
                }

                row.Add(new Label(valText)
                {
                    pickingMode = PickingMode.Ignore,
                    style = { width = 80, color = valColor, fontSize = 11, unityTextAlign = TextAnchor.MiddleRight }
                });
            }

            return row;
        }

        private List<IAttribute> CollectAllAttributes()
        {
            var attributes = new HashSet<IAttribute>();
            foreach (var gas in _gasObjects)
            {
                if (!gas.FindAttributeSystem(out var attrSystem)) continue;
                var cache = attrSystem.GetAttributeCache();
                if (cache == null) continue;
                foreach (var kvp in cache)
                {
                    if (kvp.Key is null) continue;
                    attributes.Add(kvp.Key);
                }
            }
            return attributes.OrderBy(n => n.GetName()).ToList();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════════════

        private static string GetDisplayName(GameplayAbilitySystem gas)
        {
            return gas.EntityData != null ? gas.EntityData.GetName() : gas.gameObject.name;
        }

        private static string GetFirstAffiliationName(GameplayAbilitySystem gas)
        {
            if (gas.EntityData.Affiliation != null && gas.EntityData.Affiliation.Count > 0)
            {
                if (string.IsNullOrEmpty(gas.EntityData.Affiliation[0].DisplayName)) return "Unknown";
                return gas.EntityData.Affiliation[0].DisplayName;
            }
            return "None";
        }

        private static int GetCacheIndex(GameplayAbilitySystem gas)
        {
            return gas.ProcessRelay?.CacheIndex ?? -1;
        }

        private static Color GetAffiliationColor(string affiliationName)
        {
            if (string.IsNullOrEmpty(affiliationName) || affiliationName == "None")
                return new Color(0.5f, 0.5f, 0.5f);

            if (affiliationColors.TryGetValue(affiliationName, out var c)) return c;
            affiliationColors[affiliationName] = AffiliationPalette[affiliationColors.Count % AffiliationPalette.Length];
            return affiliationColors[affiliationName];
        }

        private VisualElement CreateSection(string title, Color accentColor)
        {
            var section = new VisualElement
            {
                style =
                {
                    marginBottom = 16,
                    paddingLeft = 8, paddingRight = 8,
                    paddingTop = 8, paddingBottom = 8,
                    backgroundColor = BackgroundMedium,
                    borderLeftWidth = 3, borderLeftColor = accentColor,
                    borderTopLeftRadius = 4, borderTopRightRadius = 4,
                    borderBottomLeftRadius = 4, borderBottomRightRadius = 4
                }
            };

            section.Add(new Label(title)
            {
                style =
                {
                    fontSize = 12,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    color = accentColor,
                    marginBottom = 8
                }
            });

            return section;
        }

        private VisualElement CreateInfoRow(string label, string value, Color valueColor)
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginBottom = 2 }
            };
            row.Add(new Label(label + ":") { style = { width = 100, color = new Color(0.6f, 0.6f, 0.6f), fontSize = 11 } });
            row.Add(new Label(value) { style = { color = valueColor, fontSize = 11 } });
            return row;
        }

        private VisualElement CreateDetailRow(string label, string value, Color valueColor)
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginBottom = 2 }
            };
            row.Add(new Label(label + ":") { style = { width = 120, color = new Color(0.5f, 0.5f, 0.5f), fontSize = 10 } });
            row.Add(new Label(value)
            {
                style =
                {
                    color = valueColor, fontSize = 10,
                    flexGrow = 1, flexShrink = 1,
                    overflow = Overflow.Hidden,
                    textOverflow = TextOverflow.Ellipsis
                }
            });
            return row;
        }

        private Label CreateSubHeader(string text)
        {
            return new Label(text)
            {
                style =
                {
                    color = AbilityColor,
                    fontSize = 10,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginTop = 8, marginBottom = 4
                }
            };
        }

        private VisualElement CreateBadge(string text, Color color, string tooltip = null)
        {
            return new Label(text)
            {
                tooltip = tooltip,
                style =
                {
                    fontSize = 9,
                    color = color,
                    backgroundColor = new Color(color.r, color.g, color.b, 0.15f),
                    paddingLeft = 6, paddingRight = 6,
                    paddingTop = 2, paddingBottom = 2,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                    unityTextAlign = TextAnchor.MiddleCenter
                }
            };
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
                    paddingLeft = 6, paddingRight = 6,
                    paddingTop = 2, paddingBottom = 2,
                    borderTopLeftRadius = 3, borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3, borderBottomRightRadius = 3
                }
            };

            container.Add(new Label(tagName) { style = { fontSize = 9, color = TagColor } });

            if (weight > 1)
            {
                container.Add(new Label($" \u00d7{weight}")
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
    }
}
