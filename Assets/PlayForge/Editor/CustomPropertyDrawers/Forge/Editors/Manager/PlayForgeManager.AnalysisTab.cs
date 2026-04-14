using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeManager
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // ANALYSIS TAB - STATE
        // ═══════════════════════════════════════════════════════════════════════════
        
        // View mode
        private EAnalysisViewMode analysisViewMode = EAnalysisViewMode.Distribution;
        private EAnalysisGroup analysisGroup = EAnalysisGroup.Effects;
        
        // Sorting
        private string analysisSortColumn = "Power";
        private bool analysisSortAscending = false;
        private static EAnalysisSortMode analysisSortMode = EAnalysisSortMode.Value; // Value vs Power contribution
        
        // Filtering
        private string analysisSearchFilter = "";
        private EEffectDurationPolicy? analysisFilterDuration = null;
        private EEffectImpactTarget? analysisFilterImpactTarget = null;
        private ECalculationOperation? analysisFilterImpactOp = null;
        // private string analysisFilterContextTag = null;
        
        // Ability-specific filters
        private EAbilityActivationPolicy? analysisFilterAbilityPolicy = null;
        
        // Selected asset for breakdown view
        private ScriptableObject analysisSelectedAsset = null;
        
        // Preset
        private AnalysisPreset currentPreset = AnalysisPreset.BalanceCheck;
        
        // Cache
        private Dictionary<ScriptableObject, PowerAnalysis> _analysisCache = new();
        private DateTime _lastAnalysisCacheTime = DateTime.MinValue;
        private const float ANALYSIS_CACHE_LIFETIME_SECONDS = 30f;
        
        // Effect power cache (for ability analysis to reference)
        private Dictionary<GameplayEffect, float> _effectPowerCache = new();
        
        // UI Constants
        private static readonly Color AnalysisAccentColor = new Color(0.4f, 0.7f, 0.9f);
        private static readonly Color OverpoweredColor = new Color(0.9f, 0.4f, 0.4f);
        private static readonly Color UnderpoweredColor = new Color(0.5f, 0.5f, 0.9f);
        private static readonly Color BalancedColor = new Color(0.5f, 0.8f, 0.5f);
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ENUMS
        // ═══════════════════════════════════════════════════════════════════════════
        
        private enum EAnalysisViewMode
        {
            Distribution,   // Overview with histogram and top-level stats
            Grid,           // Spreadsheet view with all assets and metrics
            Breakdown       // Detailed view of a single selected asset
        }
        
        private enum EAnalysisGroup
        {
            Effects,
            Abilities,
            Items,
            Entities
        }
        
        private enum EAnalysisSortMode
        {
            Value,          // Sort by raw metric value
            PowerContribution // Sort by weighted power contribution
        }
        
        private enum EBalanceWarning
        {
            None,
            PotentiallyOverpowered,
            PotentiallyUnderpowered,
            HighComplexity,
            NoImpact,
            MissingData
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // DATA CLASSES
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// A single measurable metric for an asset.
        /// </summary>
        [Serializable]
        private class PowerMetric
        {
            public string Id;               // Unique identifier (e.g., "ImpactMagnitude")
            public string Name;             // Display name
            public string Description;      // Tooltip description
            public float RawValue;          // Calculated from asset
            public float NormalizedValue;   // 0-1 after normalization
            public float ContributedPower;  // RawValue × Weight (after normalization)
            
            public PowerMetric(string id, string name, string description, float rawValue)
            {
                Id = id;
                Name = name;
                Description = description;
                RawValue = rawValue;
            }
        }
        
        /// <summary>
        /// Complete analysis result for a single asset.
        /// </summary>
        private class PowerAnalysis
        {
            public ScriptableObject Asset;
            public EAnalysisGroup Group;
            public List<PowerMetric> Metrics = new();
            public float TotalPowerScore;
            
            public float Percentile;
            public EBalanceWarning Warning = EBalanceWarning.None;
            public string WarningMessage;
            
            // For abilities - track discovered effects
            public List<GameplayEffect> DiscoveredEffects = new();

            public float GetModeValue(EAnalysisSortMode mode, string metricId)
            {
                return mode switch
                {

                    EAnalysisSortMode.Value => GetMetricValue(metricId),
                    EAnalysisSortMode.PowerContribution => GetMetricPower(metricId),
                    _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
                };
            }
            
            public float GetMetricValue(string metricId)
            {
                return Metrics.FirstOrDefault(m => m.Id == metricId)?.RawValue ?? 0f;
            }
            
            public float GetMetricPower(string metricId)
            {
                return Metrics.FirstOrDefault(m => m.Id == metricId)?.ContributedPower ?? 0f;
            }
            
            
        }
        
        /// <summary>
        /// Defines metric weights and normalization for analysis.
        /// </summary>
        [Serializable]
        private class AnalysisPreset
        {
            public string Name;
            public string Description;
            public Dictionary<string, float> MetricWeights = new();
            
            public float GetWeight(string metricId) => 
                MetricWeights.TryGetValue(metricId, out var w) ? w : 0f;
            
            // ═══════════════════════════════════════════════════════════════════════════
            // Built-in Presets
            // ═══════════════════════════════════════════════════════════════════════════
            
            public static readonly AnalysisPreset BalanceCheck = new()
            {
                Name = "Balance Check",
                Description = "Identifies outliers that may need tuning",
                MetricWeights = new()
                {
                    // Effects
                    ["ImpactMagnitude"] = 1f,
                    ["DurationScore"] = 0.8f,
                    ["TickPotential"] = 1.2f,
                    ["StackPotential"] = 0.6f,
                    ["ContainedEffects"] = 1.2f,
                    ["TagsGranted"] = 0.4f,
                    ["Complexity"] = 0.3f,
                    
                    // Abilities
                    ["EffectPowerSum"] = 1.0f,
                    ["EffectCount"] = 0.3f,
                    ["CostScore"] = -0.5f,      // Negative: higher cost = lower power
                    ["CooldownScore"] = -0.4f,  // Negative: longer cooldown = lower power
                    ["StageCount"] = 0.2f,
                    ["TaskCount"] = 0.1f,
                    ["LevelRange"] = 0.3f,
                    ["AbilityComplexity"] = 0.2f,
                }
            };
            
            public static readonly AnalysisPreset ComplexityAudit = new()
            {
                Name = "Complexity Audit", 
                Description = "Finds overly complex assets that may confuse players or cause bugs",
                MetricWeights = new()
                {
                    // Effects
                    ["ImpactMagnitude"] = 0.0f,
                    ["DurationScore"] = 0.2f,
                    ["TickPotential"] = 0.3f,
                    ["StackPotential"] = 0.8f,
                    ["ContainedEffects"] = 1.5f,
                    ["TagsGranted"] = 0.6f,
                    ["TagsRequired"] = 1.0f,
                    ["WorkerCount"] = 1.4f,
                    ["Complexity"] = 2.0f,
                    
                    // Abilities
                    ["EffectPowerSum"] = 0.0f,
                    ["EffectCount"] = 1.0f,
                    ["StageCount"] = 1.5f,
                    ["TaskCount"] = 1.2f,
                    ["ValidationRuleCount"] = 1.0f,
                    ["AbilityComplexity"] = 2.0f,
                }
            };
            
            public static readonly AnalysisPreset ThreatAssessment = new()
            {
                Name = "Threat Assessment",
                Description = "Ranks by damage/offensive potential",
                MetricWeights = new()
                {
                    // Effects
                    ["ImpactMagnitude"] = 1.5f,
                    ["DurationScore"] = 0.5f,
                    ["TickPotential"] = 1.2f,
                    ["StackPotential"] = 1.0f,
                    ["ContainedEffects"] = 0.8f,
                    ["ImpactOpMultiplier"] = 1.3f,
                    
                    // Abilities
                    ["EffectPowerSum"] = 1.5f,
                    ["EffectCount"] = 0.5f,
                    ["CostScore"] = -0.2f,
                    ["CooldownScore"] = -0.3f,
                }
            };
            
            public static readonly AnalysisPreset[] AllPresets = new[]
            {
                BalanceCheck,
                ComplexityAudit,
                ThreatAssessment
            };
        }
        
        /// <summary>
        /// Defines a column in the analysis grid.
        /// </summary>
        private class AnalysisColumnDef
        {
            public string MetricId;         // Links to PowerMetric.Id (null for non-metric columns)
            public string Header;           // Column header text
            public int Width;
            public bool IsSortable;
            public Func<PowerAnalysis, string> GetDisplayValue;
            public Func<PowerAnalysis, Color?> GetColor; // Optional color override
            public string Tooltip = null;
            
            public AnalysisColumnDef(string metricId, string header, int width, 
                Func<PowerAnalysis, string> getValue,
                Func<PowerAnalysis, Color?> getColor = null,
                bool sortable = true, string tooltip = null)
            {
                MetricId = metricId;
                Header = header;
                Width = width;
                GetDisplayValue = getValue;
                GetColor = getColor;
                IsSortable = sortable;
                Tooltip = tooltip;
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // COLUMN DEFINITIONS
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static readonly List<AnalysisColumnDef> EffectAnalysisColumns = new()
        {
            new(null, "Name", 160, 
                a => GetEffectName((GameplayEffect)a.Asset),
                a => Colors.LabelText),
            new(null, "Power", 70, 
                a => a.TotalPowerScore.ToString("F1"),
                a => GetPowerColor(a.Percentile), tooltip: "Formulaic power rating"),
            new(null, "%ile", 55, 
                a => $"{a.Percentile:F0}%",
                a => GetPowerColor(a.Percentile), tooltip: "Power rating percentile"),
            new("ImpactMagnitude", "Magnitude", 80, 
                a => a.GetModeValue(analysisSortMode, "ImpactMagnitude").ToString("F1")),
            new("DurationScore", "Duration", 85, 
                a => FormatDuration(a), tooltip: "Duration policy and length"),
            new("TickPotential", "TickP", 65, 
                a => a.GetModeValue(analysisSortMode, "TickPotential").ToString("F0"), tooltip: "Total tick potential magnitude"),
            new("StackPotential", "StackP", 65, 
                a => a.GetModeValue(analysisSortMode, "StackPotential").ToString("F0"), tooltip: "Total stack potential magnitude"),
            new("ContainedEffects", "ChainP", 60, 
                a => a.GetMetricValue("ContainedEffects").ToString("F0"), tooltip: "Total chained effect potential magnitude"),
            new("TagsGranted", "Tags+", 50, 
                a => a.GetMetricValue("TagsGranted").ToString("F0"), tooltip: "Tags granted score"),
            new("TagsRequired", "Reqs", 50, 
                a => a.GetMetricValue("TagsRequired").ToString("F0"), tooltip: "Requirements score"),
            new("WorkerCount", "Workers", 60, 
                a => a.GetMetricValue("WorkerCount").ToString("F0"), tooltip: "Worker count score"),
        };
        
        private static readonly List<AnalysisColumnDef> AbilityAnalysisColumns = new()
        {
            new(null, "Name", 160, 
                a => GetAbilityName((Ability)a.Asset),
                a => Colors.LabelText),
            new(null, "Power", 70, 
                a => a.TotalPowerScore.ToString("F1"),
                a => GetPowerColor(a.Percentile), tooltip: "Formulaic power rating"),
            new(null, "%ile", 55, 
                a => $"{a.Percentile:F0}%",
                a => GetPowerColor(a.Percentile), tooltip: "Power rating percentile"),
            new("EffectPowerSum", "ΣEffect", 70, 
                a => a.GetMetricValue("EffectPowerSum").ToString("F1"), tooltip: "Sum of effect power scores"),
            new("EffectCount", "Effects", 60, 
                a => a.GetMetricValue("EffectCount").ToString("F0"), tooltip: "Number of distinct effects applied"),
            new("CostScore", "Cost", 55, 
                a => FormatAbilityCost(a), tooltip: "Cost magnitude (negative weight)"),
            new("CooldownScore", "CD", 55, 
                a => FormatAbilityCooldown(a), tooltip: "Cooldown duration (negative weight)"),
            new("StageCount", "Stages", 55, 
                a => a.GetMetricValue("StageCount").ToString("F0"), tooltip: "Number of behaviour stages"),
            new("TaskCount", "Tasks", 55, 
                a => a.GetMetricValue("TaskCount").ToString("F0"), tooltip: "Total tasks across all stages"),
            new("LevelRange", "Lvls", 50, 
                a => FormatLevelRange((Ability)a.Asset), tooltip: "Level range (Start-Max)"),
            new("AbilityComplexity", "Cmplx", 55, 
                a => a.GetMetricValue("AbilityComplexity").ToString("F0"), tooltip: "Composite complexity score"),
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // BUILD ANALYSIS TAB
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildAnalysisTab()
        {
            // ROW 1: Group selection tabs + Preset
            var groupBar = new VisualElement();
            groupBar.style.flexDirection = FlexDirection.Row;
            groupBar.style.marginBottom = 6;
            contentContainer.Add(groupBar);
            
            foreach (EAnalysisGroup group in Enum.GetValues(typeof(EAnalysisGroup)))
            {
                var groupInfo = GetGroupInfo(group);
                var btn = CreateAnalysisGroupButton(groupInfo.icon, groupInfo.name, group);
                groupBar.Add(btn);
            }
            
            groupBar.Add(CreateSpacer());
            
            // Preset dropdown (compact)
            var presetLabel = new Label("Preset:");
            presetLabel.style.color = Colors.HintText;
            presetLabel.style.fontSize = 9;
            presetLabel.style.alignSelf = Align.Center;
            presetLabel.style.marginRight = 4;
            groupBar.Add(presetLabel);
            
            var presetDropdown = new PopupField<AnalysisPreset>(
                AnalysisPreset.AllPresets.ToList(),
                currentPreset,
                p => p.Name,
                p => p.Name
            );
            presetDropdown.style.width = 160;
            presetDropdown.style.height = 22;
            presetDropdown.RegisterValueChangedCallback(evt =>
            {
                currentPreset = evt.newValue;
                InvalidateAnalysisCache();
                RefreshAnalysisView();
            });
            groupBar.Add(presetDropdown);
            
            // ROW 2: View mode tabs + Search
            var viewBar = new VisualElement();
            viewBar.style.flexDirection = FlexDirection.Row;
            viewBar.style.marginBottom = 6;
            contentContainer.Add(viewBar);
            
            var btnDistribution = CreateViewModeButton("📊", "Dist", EAnalysisViewMode.Distribution);
            viewBar.Add(btnDistribution);
            var btnGrid = CreateViewModeButton("▤", "Grid", EAnalysisViewMode.Grid);
            viewBar.Add(btnGrid);
            var btnBreakdown = CreateViewModeButton("📋", "Detail", EAnalysisViewMode.Breakdown);
            viewBar.Add(btnBreakdown);
            
            ConfigureViewModeButtons(new []
            {
                (btnDistribution, EAnalysisViewMode.Distribution),
                (btnGrid, EAnalysisViewMode.Grid),
                (btnBreakdown, EAnalysisViewMode.Breakdown)
            });
            
            viewBar.Add(CreateSpacer());
            
            // Search (compact)
            var searchIcon = new Label("🔍");
            searchIcon.style.marginRight = 4;
            searchIcon.style.color = Colors.HintText;
            searchIcon.style.alignSelf = Align.Center;
            searchIcon.style.fontSize = 10;
            viewBar.Add(searchIcon);
            
            var searchField = new TextField();
            searchField.style.width = 120;
            searchField.style.height = 20;
            searchField.value = analysisSearchFilter;
            searchField.RegisterValueChangedCallback(evt =>
            {
                analysisSearchFilter = evt.newValue;
                RefreshAnalysisView();
            });
            viewBar.Add(searchField);
            
            // ROW 3: Filter bar (context-sensitive)
            if (analysisGroup == EAnalysisGroup.Effects)
            {
                BuildEffectFilterBar();
            }
            else if (analysisGroup == EAnalysisGroup.Abilities)
            {
                BuildAbilityFilterBar();
            }
            // TODO: Add filter bars for Items, Entities
            
            // Main content area
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.name = "AnalysisScrollView";
            scrollView.horizontalScrollerVisibility = ScrollerVisibility.Auto;
            contentContainer.Add(scrollView);
            
            var contentArea = new VisualElement();
            contentArea.name = "AnalysisContent";
            contentArea.style.minWidth = 800;
            scrollView.Add(contentArea);
            
            // Build the selected view
            RefreshAnalysisView();
        }
        
        private void RefreshAnalysisView()
        {
            var contentArea = rootVisualElement.Q<VisualElement>("AnalysisContent");
            if (contentArea == null) return;
            
            contentArea.Clear();
            
            // Get and analyze assets
            var analyses = GetAnalysesForGroup(analysisGroup);
            
            switch (analysisViewMode)
            {
                case EAnalysisViewMode.Distribution:
                    BuildDistributionView(contentArea, analyses);
                    break;
                case EAnalysisViewMode.Grid:
                    BuildGridView(contentArea, analyses);
                    break;
                case EAnalysisViewMode.Breakdown:
                    BuildBreakdownView(contentArea, analyses);
                    break;
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // FILTER BAR - EFFECTS
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildEffectFilterBar()
        {
            var filterBar = CreateFilterBarContainer();
            contentContainer.Add(filterBar);
            
            // Duration filter
            filterBar.Add(CreateFilterLabel("Duration:"));
            filterBar.Add(CreateEffectDurationFilterButton("All", null));
            filterBar.Add(CreateEffectDurationFilterButton("Instant", EEffectDurationPolicy.Instant));
            filterBar.Add(CreateEffectDurationFilterButton("Timed", EEffectDurationPolicy.Durational));
            filterBar.Add(CreateEffectDurationFilterButton("Infinite", EEffectDurationPolicy.Infinite));
            
            filterBar.Add(CreateFilterSeparator());
            
            // Impact target filter
            filterBar.Add(CreateFilterLabel("Target:"));
            filterBar.Add(CreateEffectImpactTargetFilterButton("All", null));
            filterBar.Add(CreateEffectImpactTargetFilterButton("Curr", EEffectImpactTarget.Current));
            filterBar.Add(CreateEffectImpactTargetFilterButton("Base", EEffectImpactTarget.Base));
            filterBar.Add(CreateEffectImpactTargetFilterButton("Both", EEffectImpactTarget.CurrentAndBase));
            
            filterBar.Add(CreateFilterSeparator());
            
            // Impact operation filter
            filterBar.Add(CreateFilterLabel("Op:"));
            filterBar.Add(CreateEffectImpactOpFilterButton("All", null));
            filterBar.Add(CreateEffectImpactOpFilterButton("Add", ECalculationOperation.Add));
            filterBar.Add(CreateEffectImpactOpFilterButton("Mult", ECalculationOperation.Multiply));
            filterBar.Add(CreateEffectImpactOpFilterButton("Set", ECalculationOperation.Override));
            filterBar.Add(CreateEffectImpactOpFilterButton("Flat", ECalculationOperation.FlatBonus));
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // FILTER BAR - ABILITIES
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildAbilityFilterBar()
        {
            var filterBar = CreateFilterBarContainer();
            contentContainer.Add(filterBar);
            
            // Activation policy filter
            filterBar.Add(CreateFilterLabel("Policy:"));
            filterBar.Add(CreateAbilityPolicyFilterButton("All", null));
            filterBar.Add(CreateAbilityPolicyFilterButton("Always", EAbilityActivationPolicy.AlwaysActivate));
            filterBar.Add(CreateAbilityPolicyFilterButton("IfIdle", EAbilityActivationPolicy.ActivateIfIdle));
            filterBar.Add(CreateAbilityPolicyFilterButton("QueueIfIdle", EAbilityActivationPolicy.QueueActivationIfBusy));
        }
        
        private VisualElement CreateFilterBarContainer()
        {
            var filterBar = new VisualElement();
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.flexWrap = Wrap.Wrap;
            filterBar.style.marginBottom = 6;
            filterBar.style.paddingLeft = 8;
            filterBar.style.paddingTop = 4;
            filterBar.style.paddingBottom = 4;
            filterBar.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            filterBar.style.borderTopLeftRadius = 4;
            filterBar.style.borderTopRightRadius = 4;
            filterBar.style.borderBottomLeftRadius = 4;
            filterBar.style.borderBottomRightRadius = 4;
            return filterBar;
        }
        
        private Label CreateFilterLabel(string text)
        {
            var label = new Label(text);
            label.style.fontSize = 9;
            label.style.color = Colors.HintText;
            label.style.alignSelf = Align.Center;
            label.style.marginRight = 3;
            label.style.marginLeft = 4;
            return label;
        }
        
        private Button CreateEffectDurationFilterButton(string text, EEffectDurationPolicy? value)
        {
            bool isSelected = analysisFilterDuration == value;
            var btn = new Button { text = text };
            StyleSmallFilterButton(btn, isSelected, AnalysisAccentColor);
            btn.clicked += () =>
            {
                analysisFilterDuration = value;
                ShowTab(2); // Rebuild to update button states
            };
            return btn;
        }
        
        private Button CreateEffectImpactTargetFilterButton(string text, EEffectImpactTarget? value)
        {
            bool isSelected = analysisFilterImpactTarget == value;
            var btn = new Button { text = text };
            StyleSmallFilterButton(btn, isSelected, AnalysisAccentColor);
            btn.clicked += () =>
            {
                analysisFilterImpactTarget = value;
                ShowTab(2); // Rebuild to update button states
            };
            return btn;
        }
        
        private Button CreateEffectImpactOpFilterButton(string text, ECalculationOperation? value)
        {
            bool isSelected = analysisFilterImpactOp == value;
            var btn = new Button { text = text };
            StyleSmallFilterButton(btn, isSelected, AnalysisAccentColor);
            btn.clicked += () =>
            {
                analysisFilterImpactOp = value;
                ShowTab(2); // Rebuild to update button states
            };
            return btn;
        }
        
        private Button CreateAbilityPolicyFilterButton(string text, EAbilityActivationPolicy? value)
        {
            bool isSelected = analysisFilterAbilityPolicy == value;
            var btn = new Button { text = text };
            StyleSmallFilterButton(btn, isSelected, AnalysisAccentColor);
            btn.clicked += () =>
            {
                analysisFilterAbilityPolicy = value;
                ShowTab(2); // Rebuild to update button states
            };
            return btn;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // VIEW: DISTRIBUTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildDistributionView(VisualElement container, List<PowerAnalysis> analyses)
        {
            if (!analyses.Any())
            {
                container.Add(CreateEmptyLabel("No assets found for analysis"));
                return;
            }
            
            // Summary stats row
            var statsRow = new VisualElement();
            statsRow.style.flexDirection = FlexDirection.Row;
            statsRow.style.marginBottom = 12;
            container.Add(statsRow);
            
            var avgPower = analyses.Average(a => a.TotalPowerScore);
            var minPower = analyses.Min(a => a.TotalPowerScore);
            var maxPower = analyses.Max(a => a.TotalPowerScore);
            var stdDev = CalculateStdDev(analyses.Select(a => a.TotalPowerScore));
            
            statsRow.Add(CreateStatCard("Assets", analyses.Count.ToString(), AnalysisAccentColor));
            statsRow.Add(CreateStatCard("Avg", avgPower.ToString("F1"), Colors.LabelText));
            statsRow.Add(CreateStatCard("Min", minPower.ToString("F1"), UnderpoweredColor));
            statsRow.Add(CreateStatCard("Max", maxPower.ToString("F1"), OverpoweredColor));
            statsRow.Add(CreateStatCard("σ", stdDev.ToString("F1"), Colors.HintText));
            
            // Warning counts
            var warningCounts = analyses.GroupBy(a => a.Warning)
                .Where(g => g.Key != EBalanceWarning.None)
                .ToDictionary(g => g.Key, g => g.Count());
            
            if (warningCounts.Any())
            {
                statsRow.Add(CreateSpacer());
                foreach (var kvp in warningCounts)
                {
                    var color = kvp.Key switch
                    {
                        EBalanceWarning.PotentiallyOverpowered => OverpoweredColor,
                        EBalanceWarning.PotentiallyUnderpowered => UnderpoweredColor,
                        EBalanceWarning.HighComplexity => Colors.AccentYellow,
                        EBalanceWarning.NoImpact => Colors.HintText,
                        _ => Colors.LabelText
                    };
                    statsRow.Add(CreateStatCard($"⚠ {GetWarningShortName(kvp.Key)}", kvp.Value.ToString(), color));
                }
            }
            
            // Histogram with axis labels
            container.Add(CreateSectionLabel("Distribution", 4));
            container.Add(BuildHistogramWithAxes(analyses, 20));
            
            // Top/Bottom rankings side by side
            var rankingsRow = new VisualElement();
            rankingsRow.style.flexDirection = FlexDirection.Row;
            rankingsRow.style.marginTop = 12;
            container.Add(rankingsRow);
            
            // Top 5
            var topContainer = new VisualElement();
            topContainer.style.flexGrow = 1;
            topContainer.style.marginRight = 8;
            rankingsRow.Add(topContainer);
            
            topContainer.Add(CreateSectionLabel("🔝 Highest Power", 0));
            foreach (var analysis in analyses.OrderByDescending(a => a.TotalPowerScore).Take(5))
            {
                topContainer.Add(CreateRankingRow(analysis, GetPowerColor(analysis.Percentile)));
            }
            
            // Bottom 5
            var bottomContainer = new VisualElement();
            bottomContainer.style.flexGrow = 1;
            bottomContainer.style.marginLeft = 8;
            rankingsRow.Add(bottomContainer);
            
            bottomContainer.Add(CreateSectionLabel("🔻 Lowest Power", 0));
            foreach (var analysis in analyses.OrderBy(a => a.TotalPowerScore).Take(5))
            {
                bottomContainer.Add(CreateRankingRow(analysis, GetPowerColor(analysis.Percentile)));
            }
            
            // Warnings section
            var warnings = analyses.Where(a => a.Warning != EBalanceWarning.None).ToList();
            if (warnings.Any())
            {
                container.Add(CreateSectionLabel("⚠ Warnings", 12));
                foreach (var analysis in warnings.Take(10))
                {
                    container.Add(CreateWarningRow(analysis));
                }
                if (warnings.Count > 10)
                {
                    container.Add(CreateMoreLabel(warnings.Count - 10));
                }
            }
        }
        
        private string GetWarningShortName(EBalanceWarning warning)
        {
            return warning switch
            {
                EBalanceWarning.PotentiallyOverpowered => "OP",
                EBalanceWarning.PotentiallyUnderpowered => "UP",
                EBalanceWarning.HighComplexity => "Cmplx",
                EBalanceWarning.NoImpact => "None",
                _ => "?"
            };
        }
        
        private VisualElement CreateStatCard(string label, string value, Color valueColor)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.8f);
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.marginRight = 6;
            card.style.borderTopLeftRadius = 4;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = 4;
            card.style.borderBottomRightRadius = 4;
            card.style.minWidth = 55;
            
            var valueLabel = new Label(value);
            valueLabel.style.fontSize = 14;
            valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            valueLabel.style.color = valueColor;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(valueLabel);
            
            var nameLabel = new Label(label);
            nameLabel.style.fontSize = 8;
            nameLabel.style.color = Colors.HintText;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            card.Add(nameLabel);
            
            return card;
        }
        
        private VisualElement BuildHistogramWithAxes(List<PowerAnalysis> analyses, int bucketCount)
        {
            // Outer container with padding for axes
            var outerContainer = new VisualElement();
            outerContainer.style.flexDirection = FlexDirection.Row;
            outerContainer.style.height = 140;
            
            if (!analyses.Any()) return outerContainer;
            
            var min = analyses.Min(a => a.TotalPowerScore);
            var max = analyses.Max(a => a.TotalPowerScore);
            var range = max - min;
            if (range < 0.001f) range = 1f;
            
            var buckets = new int[bucketCount];
            foreach (var a in analyses)
            {
                int bucket = Mathf.Clamp(Mathf.FloorToInt((a.TotalPowerScore - min) / range * bucketCount), 0, bucketCount - 1);
                buckets[bucket]++;
            }
            
            var maxBucket = buckets.Max();
            if (maxBucket == 0) maxBucket = 1;
            
            // Y-axis labels
            var yAxis = new VisualElement();
            yAxis.style.width = 30;
            yAxis.style.flexDirection = FlexDirection.Column;
            yAxis.style.justifyContent = Justify.SpaceBetween;
            yAxis.style.paddingTop = 4;
            yAxis.style.paddingBottom = 20;
            outerContainer.Add(yAxis);
            
            var yMaxLabel = new Label(maxBucket.ToString());
            yMaxLabel.style.fontSize = 9;
            yMaxLabel.style.color = Colors.HintText;
            yMaxLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            yAxis.Add(yMaxLabel);
            
            var yMidLabel = new Label((maxBucket / 2).ToString());
            yMidLabel.style.fontSize = 9;
            yMidLabel.style.color = Colors.HintText;
            yMidLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            yAxis.Add(yMidLabel);
            
            var yMinLabel = new Label("0");
            yMinLabel.style.fontSize = 9;
            yMinLabel.style.color = Colors.HintText;
            yMinLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            yAxis.Add(yMinLabel);
            
            // Main chart area
            var chartArea = new VisualElement();
            chartArea.style.flexGrow = 1;
            chartArea.style.flexDirection = FlexDirection.Column;
            outerContainer.Add(chartArea);
            
            // Bars container
            var barsContainer = new VisualElement();
            barsContainer.style.flexGrow = 1;
            barsContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            barsContainer.style.borderTopLeftRadius = 4;
            barsContainer.style.borderTopRightRadius = 4;
            barsContainer.style.paddingLeft = 4;
            barsContainer.style.paddingRight = 4;
            barsContainer.style.paddingTop = 4;
            barsContainer.style.flexDirection = FlexDirection.Row;
            barsContainer.style.alignItems = Align.FlexEnd;
            chartArea.Add(barsContainer);
            
            for (int i = 0; i < bucketCount; i++)
            {
                var bar = new VisualElement();
                bar.style.flexGrow = 1;
                bar.style.marginLeft = 1;
                bar.style.marginRight = 1;
                
                float heightPercent = (float)buckets[i] / maxBucket;
                bar.style.height = new StyleLength(new Length(heightPercent * 100, LengthUnit.Percent));
                
                float t = (float)i / bucketCount;
                bar.style.backgroundColor = Color.Lerp(UnderpoweredColor, OverpoweredColor, t);
                
                float bucketMin = min + range * i / bucketCount;
                float bucketMax = min + range * (i + 1) / bucketCount;
                bar.tooltip = $"{buckets[i]} assets\nPower: {bucketMin:F1} - {bucketMax:F1}";
                
                barsContainer.Add(bar);
            }
            
            // X-axis labels
            var xAxis = new VisualElement();
            xAxis.style.flexDirection = FlexDirection.Row;
            xAxis.style.justifyContent = Justify.SpaceBetween;
            xAxis.style.height = 16;
            xAxis.style.paddingLeft = 4;
            xAxis.style.paddingRight = 4;
            chartArea.Add(xAxis);
            
            var xMinLabel = new Label(min.ToString("F0"));
            xMinLabel.style.fontSize = 9;
            xMinLabel.style.color = Colors.HintText;
            xAxis.Add(xMinLabel);
            
            var xMidLabel = new Label(((min + max) / 2).ToString("F0"));
            xMidLabel.style.fontSize = 9;
            xMidLabel.style.color = Colors.HintText;
            xAxis.Add(xMidLabel);
            
            var xMaxLabel = new Label(max.ToString("F0"));
            xMaxLabel.style.fontSize = 9;
            xMaxLabel.style.color = Colors.HintText;
            xAxis.Add(xMaxLabel);
            
            return outerContainer;
        }
        
        private VisualElement CreateRankingRow(PowerAnalysis analysis, Color color)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.paddingLeft = 8;
            
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.4f));
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = Color.clear);
            row.RegisterCallback<ClickEvent>(_ =>
            {
                analysisSelectedAsset = analysis.Asset;
                analysisViewMode = EAnalysisViewMode.Breakdown;
                ShowTab(2);
            });
            
            var scoreLabel = new Label(analysis.TotalPowerScore.ToString("F1"));
            scoreLabel.style.width = 45;
            scoreLabel.style.fontSize = 10;
            scoreLabel.style.color = color;
            scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            row.Add(scoreLabel);
            
            var nameLabel = new Label(GetAssetDisplayName(analysis.Asset));
            nameLabel.style.flexGrow = 1;
            nameLabel.style.fontSize = 10;
            nameLabel.style.color = Colors.LabelText;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(nameLabel);
            
            var percentileLabel = new Label($"{analysis.Percentile:F0}%");
            percentileLabel.style.width = 35;
            percentileLabel.style.fontSize = 9;
            percentileLabel.style.color = Colors.HintText;
            percentileLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(percentileLabel);
            
            return row;
        }
        
        private VisualElement CreateWarningRow(PowerAnalysis analysis)
        {
            var warningColor = analysis.Warning switch
            {
                EBalanceWarning.PotentiallyOverpowered => OverpoweredColor,
                EBalanceWarning.PotentiallyUnderpowered => UnderpoweredColor,
                EBalanceWarning.HighComplexity => Colors.AccentYellow,
                EBalanceWarning.NoImpact => Colors.HintText,
                _ => Colors.LabelText
            };
            
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 3;
            row.style.paddingBottom = 3;
            row.style.paddingLeft = 8;
            row.style.backgroundColor = new Color(warningColor.r, warningColor.g, warningColor.b, 0.1f);
            row.style.borderLeftWidth = 3;
            row.style.borderLeftColor = warningColor;
            row.style.marginBottom = 2;
            
            row.RegisterCallback<ClickEvent>(_ =>
            {
                Selection.activeObject = analysis.Asset;
                EditorGUIUtility.PingObject(analysis.Asset);
            });
            
            var icon = new Label("⚠");
            icon.style.color = warningColor;
            icon.style.marginRight = 6;
            icon.style.fontSize = 10;
            row.Add(icon);
            
            var nameLabel = new Label(GetAssetDisplayName(analysis.Asset));
            nameLabel.style.width = 140;
            nameLabel.style.fontSize = 10;
            nameLabel.style.color = Colors.LabelText;
            row.Add(nameLabel);
            
            var warningLabel = new Label(analysis.WarningMessage ?? analysis.Warning.ToString());
            warningLabel.style.flexGrow = 1;
            warningLabel.style.fontSize = 9;
            warningLabel.style.color = warningColor;
            row.Add(warningLabel);
            
            return row;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // VIEW: GRID
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildGridView(VisualElement container, List<PowerAnalysis> analyses)
        {
            if (!analyses.Any())
            {
                container.Add(CreateEmptyLabel("No assets found for analysis"));
                return;
            }
            
            var columns = GetColumnsForGroup(analysisGroup);
            
            // Results count
            var resultsLabel = new Label($"{analyses.Count} assets");
            resultsLabel.style.fontSize = 10;
            resultsLabel.style.color = Colors.HintText;
            resultsLabel.style.marginBottom = 6;
            container.Add(resultsLabel);
            
            // Table container
            var tableContainer = new VisualElement();
            tableContainer.style.borderTopWidth = 1;
            tableContainer.style.borderBottomWidth = 1;
            tableContainer.style.borderLeftWidth = 1;
            tableContainer.style.borderRightWidth = 1;
            tableContainer.style.borderTopColor = TableBorderColor;
            tableContainer.style.borderBottomColor = TableBorderColor;
            tableContainer.style.borderLeftColor = TableBorderColor;
            tableContainer.style.borderRightColor = TableBorderColor;
            tableContainer.style.borderTopLeftRadius = 6;
            tableContainer.style.borderTopRightRadius = 6;
            tableContainer.style.borderBottomLeftRadius = 6;
            tableContainer.style.borderBottomRightRadius = 6;
            tableContainer.style.overflow = Overflow.Hidden;
            container.Add(tableContainer);
            
            // Header row
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.backgroundColor = HeaderBgColor;
            headerRow.style.minHeight = HEADER_HEIGHT;
            headerRow.style.borderBottomWidth = 2;
            headerRow.style.borderBottomColor = TableBorderColor;
            headerRow.style.borderLeftWidth = ROW_BORDER_WIDTH;
            headerRow.style.borderLeftColor = HeaderBgColor;
            tableContainer.Add(headerRow);
            
            foreach (var col in columns)
            {
                headerRow.Add(CreateAnalysisHeaderCell(col));
            }
            
            // Actions column
            headerRow.Add(CreateHeaderCell("Actions", 80, false));
            
            // Sort analyses
            var sorted = SortAnalyses(analyses, columns);
            
            // Data rows
            bool alt = false;
            foreach (var analysis in sorted)
            {
                tableContainer.Add(CreateAnalysisDataRow(analysis, columns, alt));
                alt = !alt;
            }
        }
        
        private VisualElement CreateAnalysisHeaderCell(AnalysisColumnDef col)
        {
            var cell = new VisualElement();
            cell.style.width = col.Width;
            cell.style.minHeight = HEADER_HEIGHT;
            cell.style.paddingLeft = CELL_PADDING_H;
            cell.style.paddingRight = CELL_PADDING_H;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = new Color(TableBorderColor.r, TableBorderColor.g, TableBorderColor.b, 0.5f);
            
            if (!string.IsNullOrEmpty(col.Tooltip))
                cell.tooltip = col.Tooltip;
            
            // Determine sort indicator
            string sortIndicator = "";
            if (col.IsSortable && analysisSortColumn == (col.MetricId ?? col.Header))
            {
                sortIndicator = analysisSortAscending ? " ▲" : " ▼";
                if (analysisSortMode == EAnalysisSortMode.PowerContribution && col.MetricId != null)
                {
                    sortIndicator += "ₚ"; // Subscript p for power mode
                }
            }
            
            var label = new Label(col.Header + sortIndicator);
            label.style.fontSize = 10;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Colors.LabelText;
            cell.Add(label);
            
            if (col.IsSortable)
            {
                cell.RegisterCallback<ClickEvent>(_ =>
                {
                    string sortKey = col.MetricId ?? col.Header;
                    if (analysisSortColumn == sortKey)
                    {
                        // Cycle: Value Asc -> Value Desc -> Power Asc -> Power Desc -> Value Asc
                        if (col.MetricId != null)
                        {
                            if (analysisSortMode == EAnalysisSortMode.Value)
                            {
                                if (analysisSortAscending)
                                    analysisSortAscending = false;
                                else
                                {
                                    analysisSortMode = EAnalysisSortMode.PowerContribution;
                                    analysisSortAscending = true;
                                }
                            }
                            else
                            {
                                if (analysisSortAscending)
                                    analysisSortAscending = false;
                                else
                                {
                                    analysisSortMode = EAnalysisSortMode.Value;
                                    analysisSortAscending = true;
                                }
                            }
                        }
                        else
                        {
                            analysisSortAscending = !analysisSortAscending;
                        }
                    }
                    else
                    {
                        analysisSortColumn = sortKey;
                        analysisSortAscending = false; // Default to descending for power
                        analysisSortMode = EAnalysisSortMode.Value;
                    }
                    RefreshAnalysisView();
                });
                cell.RegisterCallback<MouseEnterEvent>(_ => cell.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f, 0.4f));
                cell.RegisterCallback<MouseLeaveEvent>(_ => cell.style.backgroundColor = Color.clear);
            }
            
            return cell;
        }
        
        private VisualElement CreateAnalysisDataRow(PowerAnalysis analysis, List<AnalysisColumnDef> columns, bool alternate)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.minHeight = ROW_HEIGHT;
            row.style.backgroundColor = alternate ? RowAltColor : Color.clear;
            row.style.borderLeftWidth = ROW_BORDER_WIDTH;
            row.style.borderLeftColor = TableBorderColor.Fade(.3f);
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = TableBorderColor.Fade(.3f);
            
            // Warning indicator
            if (analysis.Warning != EBalanceWarning.None)
            {
                var warningColor = analysis.Warning switch
                {
                    EBalanceWarning.PotentiallyOverpowered => OverpoweredColor,
                    EBalanceWarning.PotentiallyUnderpowered => UnderpoweredColor,
                    _ => Colors.AccentYellow
                };
                
                row.style.borderLeftColor = warningColor;
            }
            
            row.RegisterCallback<MouseEnterEvent>(_ => row.style.backgroundColor = RowHoverColor);
            row.RegisterCallback<MouseLeaveEvent>(_ => row.style.backgroundColor = alternate ? RowAltColor : Color.clear);
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.clickCount == 2)
                {
                    analysisSelectedAsset = analysis.Asset;
                    analysisViewMode = EAnalysisViewMode.Breakdown;
                    ShowTab(2);
                }
            });
            
            foreach (var col in columns)
            {
                var value = col.GetDisplayValue(analysis);
                var color = col.GetColor?.Invoke(analysis) ?? Colors.LabelText;
                row.Add(CreateAnalysisDataCell(value, col.Width, color));
            }
            
            // Actions cell
            row.Add(CreateAnalysisActionsCell(analysis));
            
            return row;
        }
        
        private VisualElement CreateAnalysisDataCell(string value, int width, Color color)
        {
            var cell = new VisualElement();
            cell.style.width = width;
            cell.style.minHeight = ROW_HEIGHT;
            cell.style.paddingLeft = CELL_PADDING_H;
            cell.style.paddingRight = CELL_PADDING_H;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = new Color(TableBorderColor.r, TableBorderColor.g, TableBorderColor.b, 0.2f);
            
            var label = new Label(value);
            label.style.fontSize = 10;
            label.style.color = color;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            cell.Add(label);
            
            return cell;
        }
        
        private VisualElement CreateAnalysisActionsCell(PowerAnalysis analysis)
        {
            var cell = new VisualElement();
            cell.style.width = 80;
            cell.style.minHeight = ROW_HEIGHT;
            cell.style.flexDirection = FlexDirection.Row;
            cell.style.alignItems = Align.Center;
            cell.style.justifyContent = Justify.Center;
            cell.style.borderRightWidth = 1;
            cell.style.borderRightColor = new Color(TableBorderColor.r, TableBorderColor.g, TableBorderColor.b, 0.2f);
            
            var selectBtn = new Button(() =>
            {
                Selection.activeObject = analysis.Asset;
                EditorGUIUtility.PingObject(analysis.Asset);
            });
            selectBtn.text = "Select";
            selectBtn.style.fontSize = 9;
            selectBtn.style.paddingLeft = 6;
            selectBtn.style.paddingRight = 6;
            selectBtn.style.paddingTop = 2;
            selectBtn.style.paddingBottom = 2;
            selectBtn.style.borderTopLeftRadius = 3;
            selectBtn.style.borderTopRightRadius = 3;
            selectBtn.style.borderBottomLeftRadius = 3;
            selectBtn.style.borderBottomRightRadius = 3;
            selectBtn.style.backgroundColor = Colors.ButtonBackground;
            cell.Add(selectBtn);
            
            return cell;
        }
        
        private List<PowerAnalysis> SortAnalyses(List<PowerAnalysis> analyses, List<AnalysisColumnDef> columns)
        {
            var col = columns.FirstOrDefault(c => (c.MetricId ?? c.Header) == analysisSortColumn);
            
            IEnumerable<PowerAnalysis> sorted;
            
            if (analysisSortColumn == "Power")
            {
                sorted = analysisSortAscending 
                    ? analyses.OrderBy(a => a.TotalPowerScore)
                    : analyses.OrderByDescending(a => a.TotalPowerScore);
            }
            else if (analysisSortColumn == "%ile")
            {
                sorted = analysisSortAscending 
                    ? analyses.OrderBy(a => a.Percentile)
                    : analyses.OrderByDescending(a => a.Percentile);
            }
            else if (analysisSortColumn == "Name")
            {
                sorted = analysisSortAscending 
                    ? analyses.OrderBy(a => GetAssetDisplayName(a.Asset))
                    : analyses.OrderByDescending(a => GetAssetDisplayName(a.Asset));
            }
            else if (col?.MetricId != null)
            {
                if (analysisSortMode == EAnalysisSortMode.PowerContribution)
                {
                    sorted = analysisSortAscending 
                        ? analyses.OrderBy(a => a.GetMetricPower(col.MetricId))
                        : analyses.OrderByDescending(a => a.GetMetricPower(col.MetricId));
                }
                else
                {
                    sorted = analysisSortAscending 
                        ? analyses.OrderBy(a => a.GetMetricValue(col.MetricId))
                        : analyses.OrderByDescending(a => a.GetMetricValue(col.MetricId));
                }
            }
            else
            {
                sorted = analyses.OrderByDescending(a => a.TotalPowerScore);
            }
            
            return sorted.ToList();
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // VIEW: BREAKDOWN
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void BuildBreakdownView(VisualElement container, List<PowerAnalysis> analyses)
        {
            // Asset selector at top
            var selectorRow = new VisualElement();
            selectorRow.style.flexDirection = FlexDirection.Row;
            selectorRow.style.alignItems = Align.Center;
            selectorRow.style.marginBottom = 10;
            container.Add(selectorRow);
            
            var selectorLabel = new Label("Asset:");
            selectorLabel.style.marginRight = 6;
            selectorLabel.style.color = Colors.LabelText;
            selectorLabel.style.fontSize = 11;
            selectorRow.Add(selectorLabel);
            
            // Dropdown of all assets in group
            var assetOptions = analyses.Select(a => a.Asset).ToList();
            if (!assetOptions.Contains(analysisSelectedAsset))
            {
                analysisSelectedAsset = assetOptions.FirstOrDefault();
            }
            
            if (assetOptions.Any())
            {
                var dropdown = new PopupField<ScriptableObject>(
                    assetOptions,
                    analysisSelectedAsset ?? assetOptions.First(),
                    a => GetAssetDisplayName(a),
                    a => GetAssetDisplayName(a)
                );
                dropdown.style.width = 220;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    analysisSelectedAsset = evt.newValue;
                    RefreshAnalysisView();
                });
                selectorRow.Add(dropdown);
            }
            
            if (analysisSelectedAsset == null)
            {
                container.Add(CreateEmptyLabel("Select an asset to view breakdown"));
                return;
            }
            
            var analysis = analyses.FirstOrDefault(a => a.Asset == analysisSelectedAsset);
            if (analysis == null)
            {
                container.Add(CreateEmptyLabel("Asset not found in current filter"));
                return;
            }
            
            // Header with total power
            var headerBox = new VisualElement();
            headerBox.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.8f);
            headerBox.style.paddingLeft = 14;
            headerBox.style.paddingRight = 14;
            headerBox.style.paddingTop = 10;
            headerBox.style.paddingBottom = 10;
            headerBox.style.marginBottom = 12;
            headerBox.style.borderTopLeftRadius = 6;
            headerBox.style.borderTopRightRadius = 6;
            headerBox.style.borderBottomLeftRadius = 6;
            headerBox.style.borderBottomRightRadius = 6;
            headerBox.style.borderLeftWidth = 4;
            headerBox.style.borderLeftColor = GetPowerColor(analysis.Percentile);
            container.Add(headerBox);
            
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerBox.Add(headerRow);
            
            var typeInfo = AssetTypes.FirstOrDefault(t => t.Type == analysis.Asset.GetType());
            var icon = new Label(typeInfo?.Icon ?? "?");
            icon.style.fontSize = 22;
            icon.style.marginRight = 10;
            icon.style.color = typeInfo?.Color ?? Colors.LabelText;
            headerRow.Add(icon);
            
            var titleContainer = new VisualElement();
            titleContainer.style.flexGrow = 1;
            headerRow.Add(titleContainer);
            
            var titleLabel = new Label(GetAssetDisplayName(analysis.Asset));
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Colors.HeaderText;
            titleContainer.Add(titleLabel);
            
            var subtitleLabel = new Label($"{typeInfo?.DisplayName ?? "Asset"} • {currentPreset.Name}");
            subtitleLabel.style.fontSize = 9;
            subtitleLabel.style.color = Colors.HintText;
            titleContainer.Add(subtitleLabel);
            
            var scoreContainer = new VisualElement();
            scoreContainer.style.alignItems = Align.FlexEnd;
            headerRow.Add(scoreContainer);
            
            var scoreLabel = new Label(analysis.TotalPowerScore.ToString("F1"));
            scoreLabel.style.fontSize = 22;
            scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            scoreLabel.style.color = GetPowerColor(analysis.Percentile);
            scoreContainer.Add(scoreLabel);
            
            var percentileLabel = new Label($"{analysis.Percentile:F0}th percentile");
            percentileLabel.style.fontSize = 9;
            percentileLabel.style.color = Colors.HintText;
            scoreContainer.Add(percentileLabel);
            
            // Warning if present
            if (analysis.Warning != EBalanceWarning.None)
            {
                var warningRow = new VisualElement();
                warningRow.style.flexDirection = FlexDirection.Row;
                warningRow.style.marginTop = 6;
                warningRow.style.alignItems = Align.Center;
                headerBox.Add(warningRow);
                
                var warningIcon = new Label("⚠");
                warningIcon.style.color = Colors.AccentYellow;
                warningIcon.style.marginRight = 6;
                warningRow.Add(warningIcon);
                
                var warningText = new Label(analysis.WarningMessage ?? analysis.Warning.ToString());
                warningText.style.color = Colors.AccentYellow;
                warningText.style.fontSize = 10;
                warningRow.Add(warningText);
            }
            
            // For abilities, show discovered effects
            if (analysis.Group == EAnalysisGroup.Abilities && analysis.DiscoveredEffects.Any())
            {
                container.Add(CreateSectionLabel($"Discovered Effects ({analysis.DiscoveredEffects.Count})", 0));
                
                var effectsContainer = new VisualElement();
                effectsContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
                effectsContainer.style.paddingLeft = 10;
                effectsContainer.style.paddingRight = 10;
                effectsContainer.style.paddingTop = 6;
                effectsContainer.style.paddingBottom = 6;
                effectsContainer.style.borderTopLeftRadius = 4;
                effectsContainer.style.borderTopRightRadius = 4;
                effectsContainer.style.borderBottomLeftRadius = 4;
                effectsContainer.style.borderBottomRightRadius = 4;
                effectsContainer.style.marginBottom = 10;
                container.Add(effectsContainer);
                
                foreach (var effect in analysis.DiscoveredEffects.Take(8))
                {
                    var effectRow = new VisualElement();
                    effectRow.style.flexDirection = FlexDirection.Row;
                    effectRow.style.alignItems = Align.Center;
                    effectRow.style.paddingTop = 2;
                    effectRow.style.paddingBottom = 2;
                    
                    var effectIcon = new Label("✦");
                    effectIcon.style.color = Colors.AssetEffect;
                    effectIcon.style.marginRight = 6;
                    effectIcon.style.fontSize = 10;
                    effectRow.Add(effectIcon);
                    
                    var effectName = new Label(GetEffectName(effect));
                    effectName.style.fontSize = 10;
                    effectName.style.color = Colors.LabelText;
                    effectName.style.flexGrow = 1;
                    effectRow.Add(effectName);
                    
                    // Show effect power if cached
                    if (_effectPowerCache.TryGetValue(effect, out var power))
                    {
                        var powerLabel = new Label($"Power: {power:F1}");
                        powerLabel.style.fontSize = 9;
                        powerLabel.style.color = Colors.HintText;
                        effectRow.Add(powerLabel);
                    }
                    
                    effectsContainer.Add(effectRow);
                }
                
                if (analysis.DiscoveredEffects.Count > 8)
                {
                    effectsContainer.Add(CreateMoreLabel(analysis.DiscoveredEffects.Count - 8));
                }
            }
            
            // Metric breakdown
            container.Add(CreateSectionLabel("Metric Breakdown", 0));
            
            var metricsContainer = new VisualElement();
            metricsContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            metricsContainer.style.paddingLeft = 10;
            metricsContainer.style.paddingRight = 10;
            metricsContainer.style.paddingTop = 6;
            metricsContainer.style.paddingBottom = 6;
            metricsContainer.style.borderTopLeftRadius = 4;
            metricsContainer.style.borderTopRightRadius = 4;
            metricsContainer.style.borderBottomLeftRadius = 4;
            metricsContainer.style.borderBottomRightRadius = 4;
            container.Add(metricsContainer);
            
            // Sort metrics by absolute contribution
            var sortedMetrics = analysis.Metrics
                .OrderByDescending(m => Mathf.Abs(m.ContributedPower))
                .ToList();
            
            var maxContribution = sortedMetrics.Any() 
                ? sortedMetrics.Max(m => Mathf.Abs(m.ContributedPower)) 
                : 1f;
            
            foreach (var metric in sortedMetrics)
            {
                metricsContainer.Add(CreateMetricRow(metric, maxContribution, currentPreset));
            }
        }
        
        private VisualElement CreateMetricRow(PowerMetric metric, float maxContribution, AnalysisPreset preset)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;
            row.tooltip = metric.Description;
            
            // Metric name
            var nameLabel = new Label(metric.Name);
            nameLabel.style.width = 100;
            nameLabel.style.fontSize = 10;
            nameLabel.style.color = Colors.LabelText;
            row.Add(nameLabel);
            
            // Raw value
            var valueLabel = new Label(metric.RawValue.ToString("F1"));
            valueLabel.style.width = 50;
            valueLabel.style.fontSize = 10;
            valueLabel.style.color = Colors.HintText;
            valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(valueLabel);
            
            // Weight
            var weight = preset.GetWeight(metric.Id);
            var weightLabel = new Label($"×{weight:F1}");
            weightLabel.style.width = 35;
            weightLabel.style.fontSize = 9;
            weightLabel.style.color = weight < 0 ? Colors.AccentRed : Colors.HintText;
            weightLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            row.Add(weightLabel);
            
            // Bar visualization
            var barContainer = new VisualElement();
            barContainer.style.flexGrow = 1;
            barContainer.style.height = 14;
            barContainer.style.backgroundColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            barContainer.style.borderTopLeftRadius = 2;
            barContainer.style.borderTopRightRadius = 2;
            barContainer.style.borderBottomLeftRadius = 2;
            barContainer.style.borderBottomRightRadius = 2;
            barContainer.style.marginLeft = 6;
            barContainer.style.marginRight = 6;
            row.Add(barContainer);
            
            float barWidth = maxContribution > 0 ? Mathf.Abs(metric.ContributedPower) / maxContribution : 0;
            var bar = new VisualElement();
            bar.style.width = new StyleLength(new Length(barWidth * 100, LengthUnit.Percent));
            bar.style.height = new StyleLength(StyleKeyword.Auto);
            bar.style.flexGrow = 1;
            bar.style.backgroundColor = metric.ContributedPower >= 0 ? AnalysisAccentColor : Colors.AccentRed;
            bar.style.borderTopLeftRadius = 2;
            bar.style.borderTopRightRadius = 2;
            bar.style.borderBottomLeftRadius = 2;
            bar.style.borderBottomRightRadius = 2;
            barContainer.Add(bar);
            
            // Contribution value
            var contribLabel = new Label(metric.ContributedPower.ToString("F1"));
            contribLabel.style.width = 45;
            contribLabel.style.fontSize = 10;
            contribLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            contribLabel.style.color = metric.ContributedPower >= 0 ? AnalysisAccentColor : Colors.AccentRed;
            contribLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            row.Add(contribLabel);
            
            return row;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ANALYSIS CALCULATIONS
        // ═══════════════════════════════════════════════════════════════════════════
        
        private List<PowerAnalysis> GetAnalysesForGroup(EAnalysisGroup group)
        {
            // Check cache
            var cacheValid = (DateTime.Now - _lastAnalysisCacheTime).TotalSeconds < ANALYSIS_CACHE_LIFETIME_SECONDS;
            
            // Get assets for group
            var assets = GetAssetsForGroup(group);
            
            // Apply filters
            assets = ApplyAnalysisFilters(assets, group);
            
            // Pre-compute effect power scores for ability analysis
            if (group == EAnalysisGroup.Abilities)
            {
                PrecomputeEffectPowerScores();
            }
            
            // Analyze each asset (using cache where valid)
            var results = new List<PowerAnalysis>();
            foreach (var asset in assets)
            {
                if (cacheValid && _analysisCache.TryGetValue(asset, out var cached))
                {
                    results.Add(cached);
                }
                else
                {
                    var analysis = AnalyzeAsset(asset, group);
                    _analysisCache[asset] = analysis;
                    results.Add(analysis);
                }
            }
            
            if (!cacheValid)
                _lastAnalysisCacheTime = DateTime.Now;
            
            // Calculate percentiles
            if (results.Any())
            {
                var sorted = results.OrderBy(r => r.TotalPowerScore).ToList();
                for (int i = 0; i < sorted.Count; i++)
                {
                    sorted[i].Percentile = sorted.Count > 1 ? (float)i / (sorted.Count - 1) * 100f : 50f;
                }
                
                // Detect warnings
                foreach (var analysis in results)
                {
                    DetectWarnings(analysis, results);
                }
            }
            
            return results;
        }
        
        private void PrecomputeEffectPowerScores()
        {
            _effectPowerCache.Clear();
            
            var effects = cachedAssets.OfType<GameplayEffect>().ToList();
            foreach (var effect in effects)
            {
                var analysis = new PowerAnalysis { Asset = effect, Group = EAnalysisGroup.Effects };
                AnalyzeEffect(effect, analysis);
                
                float total = 0f;
                foreach (var metric in analysis.Metrics)
                {
                    var weight = currentPreset.GetWeight(metric.Id);
                    total += metric.RawValue * weight;
                }
                
                _effectPowerCache[effect] = total;
            }
        }
        
        private List<ScriptableObject> GetAssetsForGroup(EAnalysisGroup group)
        {
            return group switch
            {
                EAnalysisGroup.Effects => cachedAssets.Where(a => a is GameplayEffect).ToList(),
                EAnalysisGroup.Abilities => cachedAssets.Where(a => a is Ability).ToList(),
                EAnalysisGroup.Items => cachedAssets.Where(a => a is Item).ToList(),
                EAnalysisGroup.Entities => cachedAssets.Where(a => a is EntityIdentity).ToList(),
                _ => new List<ScriptableObject>()
            };
        }
        
        private List<ScriptableObject> ApplyAnalysisFilters(List<ScriptableObject> assets, EAnalysisGroup group)
        {
            var filtered = assets.AsEnumerable();
            
            // Search filter
            if (!string.IsNullOrEmpty(analysisSearchFilter))
            {
                var search = analysisSearchFilter.ToLower();
                filtered = filtered.Where(a => GetAssetDisplayName(a).ToLower().Contains(search) 
                                               || a.name.ToLower().Contains(search));
            }
            
            // Group-specific filters
            if (group == EAnalysisGroup.Effects)
            {
                filtered = filtered.Cast<GameplayEffect>().Where(e =>
                {
                    if (analysisFilterDuration.HasValue && e.DurationSpecification?.DurationPolicy != analysisFilterDuration.Value)
                        return false;
                    if (analysisFilterImpactTarget.HasValue && e.ImpactSpecification?.TargetImpact != analysisFilterImpactTarget.Value)
                        return false;
                    if (analysisFilterImpactOp.HasValue && e.ImpactSpecification?.ImpactOperation != analysisFilterImpactOp.Value)
                        return false;
                    return true;
                }).Cast<ScriptableObject>();
            }
            else if (group == EAnalysisGroup.Abilities)
            {
                filtered = filtered.Cast<Ability>().Where(a =>
                {
                    if (analysisFilterAbilityPolicy.HasValue && a.Definition.ActivationPolicy.Translate() != analysisFilterAbilityPolicy.Value)
                        return false;
                    return true;
                }).Cast<ScriptableObject>();
            }
            
            return filtered.ToList();
        }
        
        private PowerAnalysis AnalyzeAsset(ScriptableObject asset, EAnalysisGroup group)
        {
            var analysis = new PowerAnalysis
            {
                Asset = asset,
                Group = group
            };
            
            switch (group)
            {
                case EAnalysisGroup.Effects:
                    AnalyzeEffect((GameplayEffect)asset, analysis);
                    break;
                case EAnalysisGroup.Abilities:
                    AnalyzeAbility((Ability)asset, analysis);
                    break;
                case EAnalysisGroup.Items:
                    AnalyzeItem((Item)asset, analysis);
                    break;
                case EAnalysisGroup.Entities:
                    AnalyzeEntity((EntityIdentity)asset, analysis);
                    break;
            }
            
            // Calculate total power from metrics
            foreach (var metric in analysis.Metrics)
            {
                var weight = currentPreset.GetWeight(metric.Id);
                metric.ContributedPower = metric.RawValue * weight;
                analysis.TotalPowerScore += metric.ContributedPower;
            }
            
            return analysis;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // EFFECT ANALYSIS
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void AnalyzeEffect(GameplayEffect effect, PowerAnalysis analysis)
        {
            var impact = effect.ImpactSpecification;
            var duration = effect.DurationSpecification;
            
            // Impact Magnitude
            float magnitude = impact?.MagnitudeOperation.Magnitude ?? 0f;
            if (impact?.MagnitudeOperation.Scaler?.LevelValues != null && impact.MagnitudeOperation.Scaler.LevelValues.Length > 0)
            {
                magnitude = impact.MagnitudeOperation.RealMagnitude switch
                {
                    EMagnitudeOperation.AddScaler => impact.MagnitudeOperation.Magnitude + impact.MagnitudeOperation.Scaler.LevelValues.Max(),
                    EMagnitudeOperation.MultiplyWithScaler => impact.MagnitudeOperation.Magnitude * impact.MagnitudeOperation.Scaler.LevelValues.Max(),
                    EMagnitudeOperation.UseScaler => impact.MagnitudeOperation.Scaler.LevelValues.Max(),
                    _ => impact.MagnitudeOperation.Magnitude
                };
            }
            analysis.Metrics.Add(new PowerMetric("ImpactMagnitude", "Magnitude", 
                "The magnitude of attribute modification", Mathf.Abs(magnitude)));
            
            // Impact Operation multiplier
            float opMultiplier = impact?.ImpactOperation switch
            {
                ECalculationOperation.Add => 1f,
                ECalculationOperation.Multiply => 1.5f,
                ECalculationOperation.Override => 2f,
                ECalculationOperation.FlatBonus => 1f,
                _ => 1f
            };
            analysis.Metrics.Add(new PowerMetric("ImpactOpMultiplier", "Op Weight",
                "Weight based on operation type (Multiply/Override = stronger)", opMultiplier));
            
            // Duration Score
            float durationScore = duration?.DurationPolicy switch
            {
                EEffectDurationPolicy.Instant => 1f,
                EEffectDurationPolicy.Infinite => 10f,
                EEffectDurationPolicy.Durational => Mathf.Clamp(duration.DurationOperation.Magnitude * 0.5f, 1f, 20f),
                _ => 1f
            };
            analysis.Metrics.Add(new PowerMetric("DurationScore", "Duration",
                "Score based on duration policy and length", durationScore));
            
            // Tick Potential (magnitude × ticks)
            float tickPotential = 0f;
            if (duration != null && duration.EnablePeriodicTicks)
            {
                tickPotential = duration.DurationPolicy switch
                {
                    EEffectDurationPolicy.Durational => Mathf.Max(0, duration.TicksOperation.Magnitude - 1) * Mathf.Abs(magnitude),
                    EEffectDurationPolicy.Infinite => duration.TickIntervalOperation.Magnitude > 0 ? Mathf.Abs(magnitude) : 0f,
                    _ => 0f
                };
            }
            analysis.Metrics.Add(new PowerMetric("TickPotential", "Tick Potential",
                "Magnitude × (ticks - 1) for periodic effects", tickPotential));
            
            // Stack Potential
            float stackPotential = 0f;
            if (duration != null && duration.ReApplicationPolicy == EEffectReApplicationPolicy.StackExistingContainers)
            {
                stackPotential = duration.StackAmountOperation.Magnitude;
                if (duration.StackAmountOperation.Scaler?.LevelValues != null && duration.StackAmountOperation.Scaler.LevelValues.Length > 0)
                {
                    stackPotential = Mathf.Max(stackPotential, duration.StackAmountOperation.Scaler.LevelValues.Max());
                }
                stackPotential *= Mathf.Abs(magnitude);
            }
            analysis.Metrics.Add(new PowerMetric("StackPotential", "Stack Potential",
                "Maximum stacks × magnitude", stackPotential));
            
            // Contained Effects
            int containedCount = impact?.Packets?.Length ?? 0;
            analysis.Metrics.Add(new PowerMetric("ContainedEffects", "Chained Effects",
                "Number of contained/triggered effects", containedCount));
            
            // Tags Granted
            int tagsGranted = effect.Tags.GrantedTags?.Count ?? 0;
            analysis.Metrics.Add(new PowerMetric("TagsGranted", "Tags Granted",
                "Number of tags applied to target", tagsGranted));
            
            // Tags Required (complexity measure)
            int sourceReqs = GetTagRequirementsCount(effect.SourceRequirements);
            int targetReqs = GetTagRequirementsCount(effect.TargetRequirements);
            analysis.Metrics.Add(new PowerMetric("TagsRequired", "Tag Requirements",
                "Total source + target tag requirements", sourceReqs + targetReqs));
            
            // Worker Count
            int workerCount = effect.Workers?.Count ?? 0;
            analysis.Metrics.Add(new PowerMetric("WorkerCount", "Workers",
                "Number of effect workers attached", workerCount));
            
            // Complexity (composite)
            float complexity = containedCount * 2f + (sourceReqs + targetReqs) * 0.5f + workerCount;
            analysis.Metrics.Add(new PowerMetric("Complexity", "Complexity",
                "Composite complexity score", complexity));
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ABILITY ANALYSIS
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void AnalyzeAbility(Ability ability, PowerAnalysis analysis)
        {
            // Discover all effects the ability applies
            var discoveredEffects = DiscoverAbilityEffects(ability);
            analysis.DiscoveredEffects = discoveredEffects;
            
            // Effect Power Sum - sum of all effect power scores
            float effectPowerSum = 0f;
            foreach (var effect in discoveredEffects)
            {
                if (_effectPowerCache.TryGetValue(effect, out var power))
                {
                    effectPowerSum += power;
                }
            }
            analysis.Metrics.Add(new PowerMetric("EffectPowerSum", "Effect Power",
                "Sum of all applied effect power scores", effectPowerSum));
            
            // Effect Count
            analysis.Metrics.Add(new PowerMetric("EffectCount", "Effect Count",
                "Number of distinct effects applied", discoveredEffects.Count));
            
            // Cost Score (higher cost = higher score, but negative weight makes it reduce power)
            float costScore = 0f;
            if (ability.Cost != null)
            {
                var costMag = ability.Cost.ImpactSpecification?.MagnitudeOperation.Magnitude ?? 0f;
                if (ability.Cost.ImpactSpecification?.MagnitudeOperation.Scaler?.LevelValues != null)
                {
                    var scaler = ability.Cost.ImpactSpecification.MagnitudeOperation.Scaler;
                    costMag = ability.Cost.ImpactSpecification.MagnitudeOperation.RealMagnitude switch
                    {
                        EMagnitudeOperation.AddScaler => costMag + scaler.LevelValues.Average(),
                        EMagnitudeOperation.MultiplyWithScaler => costMag * scaler.LevelValues.Average(),
                        EMagnitudeOperation.UseScaler => scaler.LevelValues.Average(),
                        _ => costMag
                    };
                }
                costScore = Mathf.Abs(costMag);
            }
            analysis.Metrics.Add(new PowerMetric("CostScore", "Cost",
                "Cost magnitude (higher = less efficient)", costScore));
            
            // Cooldown Score (longer cooldown = higher score, negative weight)
            float cooldownScore = 0f;
            if (ability.Cooldown != null)
            {
                var cdDuration = ability.Cooldown.DurationSpecification?.DurationOperation.Magnitude ?? 0f;
                if (ability.Cooldown.DurationSpecification?.DurationOperation.Scaler?.LevelValues != null)
                {
                    var scaler = ability.Cooldown.DurationSpecification.DurationOperation.Scaler;
                    cdDuration = ability.Cooldown.DurationSpecification.DurationOperation.RealMagnitude switch
                    {
                        EMagnitudeOperation.AddScaler => cdDuration + scaler.LevelValues.Average(),
                        EMagnitudeOperation.MultiplyWithScaler => cdDuration * scaler.LevelValues.Average(),
                        EMagnitudeOperation.UseScaler => scaler.LevelValues.Average(),
                        _ => cdDuration
                    };
                }
                cooldownScore = cdDuration;
            }
            analysis.Metrics.Add(new PowerMetric("CooldownScore", "Cooldown",
                "Cooldown duration in seconds", cooldownScore));
            
            // Stage Count
            int stageCount = ability.Behaviour?.Stages?.Count ?? 0;
            analysis.Metrics.Add(new PowerMetric("StageCount", "Stages",
                "Number of behaviour stages", stageCount));
            
            // Task Count
            int taskCount = 0;
            if (ability.Behaviour?.Stages != null)
            {
                foreach (var stage in ability.Behaviour.Stages)
                {
                    taskCount += stage.Tasks?.Count ?? 0;
                }
            }
            analysis.Metrics.Add(new PowerMetric("TaskCount", "Tasks",
                "Total tasks across all stages", taskCount));
            
            // Level Range
            int levelRange = ability.MaxLevel - ability.StartingLevel;
            analysis.Metrics.Add(new PowerMetric("LevelRange", "Level Range",
                "Max level minus starting level", levelRange));
            
            // Validation Rule Count
            int ruleCount = (ability.SourceActivationRules?.Count ?? 0) + (ability.TargetActivationRules?.Count ?? 0);
            analysis.Metrics.Add(new PowerMetric("ValidationRuleCount", "Rules",
                "Number of activation validation rules", ruleCount));
            
            // Tag Requirements
            int tagReqs = 0;
            if (ability.Tags.TagRequirements != null)
            {
                var srcReqs = ability.Tags.TagRequirements.SourceRequirements;
                var tgtReqs = ability.Tags.TagRequirements.TargetRequirements;
                tagReqs = (srcReqs?.RequireTags?.Count ?? 0) + (srcReqs?.AvoidTags?.Count ?? 0)
                        + (tgtReqs?.RequireTags?.Count ?? 0) + (tgtReqs?.AvoidTags?.Count ?? 0);
            }
            analysis.Metrics.Add(new PowerMetric("TagsRequired", "Tag Reqs",
                "Total tag requirements", tagReqs));
            
            // Ability Complexity (composite)
            float complexity = stageCount + taskCount * 0.5f + ruleCount + tagReqs * 0.3f + discoveredEffects.Count;
            analysis.Metrics.Add(new PowerMetric("AbilityComplexity", "Complexity",
                "Composite ability complexity", complexity));
        }
        
        /// <summary>
        /// Discovers all GameplayEffect references in an ability by:
        /// 1. Checking Cost and Cooldown
        /// 2. Traversing all tasks in all stages
        /// 3. Using reflection to find GameplayEffect fields on tasks
        /// </summary>
        private List<GameplayEffect> DiscoverAbilityEffects(Ability ability)
        {
            var effects = new HashSet<GameplayEffect>();
            
            // Add Cost and Cooldown if present
            if (ability.Cost != null)
                effects.Add(ability.Cost);
            if (ability.Cooldown != null)
                effects.Add(ability.Cooldown);
            
            // Traverse stages and tasks
            if (ability.Behaviour?.Stages != null)
            {
                foreach (var stage in ability.Behaviour.Stages)
                {
                    if (stage.Tasks == null) continue;
                    
                    foreach (var task in stage.Tasks)
                    {
                        if (task == null) continue;
                        DiscoverEffectsInObject(task, effects);
                    }
                }
            }
            
            // Also check targeting task if present
            if (ability.Behaviour?.Targeting != null)
            {
                DiscoverEffectsInObject(ability.Behaviour.Targeting, effects);
            }
            
            return effects.Where(e => e != null).ToList();
        }
        
        /// <summary>
        /// Uses reflection to find all GameplayEffect fields and List fields on an object.
        /// </summary>
        private void DiscoverEffectsInObject(object obj, HashSet<GameplayEffect> effects)
        {
            if (obj == null) return;
            
            var type = obj.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            
            foreach (var field in fields)
            {
                try
                {
                    // Direct GameplayEffect field
                    if (field.FieldType == typeof(GameplayEffect))
                    {
                        var effect = field.GetValue(obj) as GameplayEffect;
                        if (effect != null)
                            effects.Add(effect);
                    }
                    // List<GameplayEffect> field
                    else if (field.FieldType == typeof(List<GameplayEffect>))
                    {
                        var list = field.GetValue(obj) as List<GameplayEffect>;
                        if (list != null)
                        {
                            foreach (var effect in list)
                            {
                                if (effect != null)
                                    effects.Add(effect);
                            }
                        }
                    }
                    // Array of GameplayEffect
                    else if (field.FieldType == typeof(GameplayEffect[]))
                    {
                        var arr = field.GetValue(obj) as GameplayEffect[];
                        if (arr != null)
                        {
                            foreach (var effect in arr)
                            {
                                if (effect != null)
                                    effects.Add(effect);
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore reflection errors
                }
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // PLACEHOLDER ANALYZERS (To be implemented)
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void AnalyzeItem(Item item, PowerAnalysis analysis)
        {
            // TODO: Implement item analysis
            analysis.Metrics.Add(new PowerMetric("Placeholder", "Not Implemented", 
                "Item analysis coming soon", 0f));
        }
        
        private void AnalyzeEntity(EntityIdentity entity, PowerAnalysis analysis)
        {
            // TODO: Implement entity analysis
            analysis.Metrics.Add(new PowerMetric("Placeholder", "Not Implemented", 
                "Entity analysis coming soon", 0f));
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // WARNING DETECTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void DetectWarnings(PowerAnalysis analysis, List<PowerAnalysis> allAnalyses)
        {
            // Overpowered (>95th percentile)
            if (analysis.Percentile > 95f)
            {
                analysis.Warning = EBalanceWarning.PotentiallyOverpowered;
                analysis.WarningMessage = $"Power score in top 5% ({analysis.Percentile:F0}th percentile)";
                return;
            }
            
            // Underpowered (<5th percentile)
            if (analysis.Percentile < 5f)
            {
                analysis.Warning = EBalanceWarning.PotentiallyUnderpowered;
                analysis.WarningMessage = $"Power score in bottom 5% ({analysis.Percentile:F0}th percentile)";
                return;
            }
            
            // High complexity
            var complexity = analysis.GetMetricValue("Complexity");
            var abilityComplexity = analysis.GetMetricValue("AbilityComplexity");
            if (complexity > 10f || abilityComplexity > 15f)
            {
                analysis.Warning = EBalanceWarning.HighComplexity;
                analysis.WarningMessage = $"High complexity ({complexity:F0}/{abilityComplexity:F0}) may be hard to maintain";
                return;
            }
            
            // No impact
            if (analysis.TotalPowerScore < 0.1f && analysis.TotalPowerScore > -0.1f)
            {
                analysis.Warning = EBalanceWarning.NoImpact;
                analysis.WarningMessage = "Asset has negligible power impact";
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // HELPER METHODS
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void InvalidateAnalysisCache()
        {
            _analysisCache.Clear();
            _effectPowerCache.Clear();
            _lastAnalysisCacheTime = DateTime.MinValue;
        }
        
        private (string icon, string name) GetGroupInfo(EAnalysisGroup group)
        {
            return group switch
            {
                EAnalysisGroup.Effects => ("✦", "Effects"),
                EAnalysisGroup.Abilities => ("⚡", "Abilities"),
                EAnalysisGroup.Items => ("🎁", "Items"),
                EAnalysisGroup.Entities => ("👤", "Entities"),
                _ => ("?", "Unknown")
            };
        }
        
        private List<AnalysisColumnDef> GetColumnsForGroup(EAnalysisGroup group)
        {
            return group switch
            {
                EAnalysisGroup.Effects => EffectAnalysisColumns,
                EAnalysisGroup.Abilities => AbilityAnalysisColumns,
                _ => EffectAnalysisColumns
            };
        }
        
        private Button CreateAnalysisGroupButton(string icon, string name, EAnalysisGroup group)
        {
            bool isSelected = analysisGroup == group;
            var btn = new Button(() =>
            {
                analysisGroup = group;
                InvalidateAnalysisCache();
                ShowTab(2);
            });
            btn.text = $"{icon} {name}";
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.paddingTop = 5;
            btn.style.paddingBottom = 5;
            btn.style.marginRight = 3;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.backgroundColor = isSelected ? AnalysisAccentColor : Colors.ButtonBackground;
            btn.style.color = isSelected ? Colors.HeaderText : Colors.LabelText;
            btn.style.fontSize = 11;
            
            btn.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (!isSelected) btn.style.backgroundColor = Colors.ButtonHover;
            });
            btn.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                btn.style.backgroundColor = isSelected ? AnalysisAccentColor : Colors.ButtonBackground;
            });
            
            return btn;
        }
        
        private Button CreateViewModeButton(string icon, string name, EAnalysisViewMode mode)
        {
            bool isSelected = analysisViewMode == mode;
            var btn = new Button();
            btn.text = $"{icon} {name}";
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.paddingTop = 3;
            btn.style.paddingBottom = 3;
            btn.style.marginRight = 3;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            btn.style.fontSize = 10;
            btn.style.borderTopColor = isSelected ? AnalysisAccentColor : Colors.ButtonBackground;
            btn.style.borderBottomColor = isSelected ? AnalysisAccentColor : Colors.ButtonBackground;
            btn.style.borderLeftColor = isSelected ? AnalysisAccentColor : Colors.ButtonBackground;
            btn.style.borderRightColor = isSelected ? AnalysisAccentColor : Colors.ButtonBackground;
            btn.style.backgroundColor = isSelected ? new Color(AnalysisAccentColor.r, AnalysisAccentColor.g, AnalysisAccentColor.b, 0.2f) : Colors.ButtonBackground;
            btn.style.color = isSelected ? AnalysisAccentColor : Colors.LabelText;
            
            return btn;
        }

        private void ConfigureViewModeButtons(params (Button btn, EAnalysisViewMode mode)[] buttons)
        {
            foreach (var btnGroup in buttons)
            {
                btnGroup.btn.clicked += () =>
                {
                    analysisViewMode = btnGroup.mode;

                    foreach (var group in buttons)
                    {
                        bool isSelected = group.mode == analysisViewMode;
                        group.btn.style.borderTopColor = isSelected ? AnalysisAccentColor : Colors.ButtonBackground;
                        group.btn.style.borderBottomColor = isSelected ? AnalysisAccentColor : Colors.ButtonBackground;
                        group.btn.style.borderLeftColor = isSelected ? AnalysisAccentColor : Colors.ButtonBackground;
                        group.btn.style.borderRightColor = isSelected ? AnalysisAccentColor : Colors.ButtonBackground;
                        group.btn.style.backgroundColor = isSelected ? new Color(AnalysisAccentColor.r, AnalysisAccentColor.g, AnalysisAccentColor.b, 0.2f) : Colors.ButtonBackground;
                        group.btn.style.color = isSelected ? AnalysisAccentColor : Colors.LabelText;
                    }
                    
                    RefreshAnalysisView();
                };
            }
        }
        
        private static Color GetPowerColor(float percentile)
        {
            if (percentile > 80f) return OverpoweredColor;
            if (percentile > 60f) return new Color(0.9f, 0.7f, 0.4f); // Orange
            if (percentile > 40f) return BalancedColor;
            if (percentile > 20f) return new Color(0.6f, 0.7f, 0.9f); // Light blue
            return UnderpoweredColor;
        }
        
        private static string FormatDuration(PowerAnalysis analysis)
        {
            if (analysis.Asset is not GameplayEffect effect) return "-";
            var dur = effect.DurationSpecification;
            if (dur == null) return "-";
            
            return dur.DurationPolicy switch
            {
                EEffectDurationPolicy.Instant => "Instant",
                EEffectDurationPolicy.Infinite => "∞",
                EEffectDurationPolicy.Durational => $"{dur.DurationOperation.Magnitude:F1}s",
                _ => "-"
            };
        }
        
        private static string FormatAbilityCost(PowerAnalysis analysis)
        {
            if (analysis.Asset is not Ability ability) return "-";
            if (ability.Cost == null) return "-";
            
            var mag = ability.Cost.ImpactSpecification?.MagnitudeOperation.Magnitude ?? 0f;
            return Mathf.Abs(mag) < 0.01f ? "-" : mag.ToString("F0");
        }
        
        private static string FormatAbilityCooldown(PowerAnalysis analysis)
        {
            if (analysis.Asset is not Ability ability) return "-";
            if (ability.Cooldown == null) return "-";
            
            var dur = ability.Cooldown.DurationSpecification?.DurationOperation.Magnitude ?? 0f;
            return dur < 0.01f ? "-" : $"{dur:F1}s";
        }
        
        private static string FormatLevelRange(Ability ability)
        {
            return $"{ability.StartingLevel}-{ability.MaxLevel}";
        }
        
        private static float CalculateStdDev(IEnumerable<float> values)
        {
            var list = values.ToList();
            if (list.Count < 2) return 0f;
            
            float avg = list.Average();
            float sumSquares = list.Sum(v => (v - avg) * (v - avg));
            return Mathf.Sqrt(sumSquares / (list.Count - 1));
        }
    }
}