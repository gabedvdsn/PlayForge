using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class TheForge
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // UI Utilities
        // ═══════════════════════════════════════════════════════════════════════════

        private Button CreateButton(string text, Action onClick, string tooltip = null)
        {
            var btn = new Button(onClick) { text = text };
            btn.focusable = false;
            if (!string.IsNullOrEmpty(tooltip))
                btn.tooltip = tooltip;
            return btn;
        }

        private VisualElement CreateSectionHeader(string title, string subtitle)
        {
            var container = new VisualElement();
            
            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 14;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = Colors.HeaderText;
            container.Add(titleLabel);
            
            var subtitleLabel = new Label(subtitle);
            subtitleLabel.style.fontSize = 10;
            subtitleLabel.style.color = Colors.HintText;
            subtitleLabel.style.marginTop = 2;
            container.Add(subtitleLabel);
            
            return container;
        }

        private VisualElement CreateDivider(int marginVertical = 8)
        {
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = Colors.DividerColor;
            divider.style.marginTop = marginVertical;
            divider.style.marginBottom = marginVertical;
            return divider;
        }

        private VisualElement CreateSpacer()
        {
            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            return spacer;
        }

        private void ApplyButtonStyle(Button btn)
        {
            btn.style.backgroundColor = Colors.ButtonBackground;
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Cache Management
        // ═══════════════════════════════════════════════════════════════════════════

        private void RefreshAssetCache()
        {
            projectChangedSinceRefresh = false;
            cachedAssets.Clear();
            
            foreach (var typeInfo in AssetTypes)
            {
                var guids = AssetDatabase.FindAssets($"t:{typeInfo.Type.Name}");
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                    if (asset != null)
                        cachedAssets.Add(asset);
                }
            }
            
            if (!ForgeTagRegistry.IsCacheValid)
                ForgeTagRegistry.RefreshCache();
        }
        
        private void RefreshAll()
        {
            RefreshAssetCache();
            ForgeTagRegistry.RefreshCache();
            ShowTab(currentTab);
        }

        private int CountAssetsOfType(Type type)
        {
            return cachedAssets.Count(a => a.GetType() == type);
        }

        // The folder Project Settings → PlayForge names for the type's new assets (ED-21)
        private string GetPathForType(Type type) => PlayForgeSettings.FolderFor(type);

        private string GetRelativeTime(DateTime time)
        {
            var diff = DateTime.Now - time;
            if (diff.TotalMinutes < 1) return "just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return time.ToString("MMM d");
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Data Extraction Helpers
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static string GetAbilityName(Ability a) => 
            !string.IsNullOrEmpty(a.GetName()) ? a.GetName() : a.name;
        
        /// <summary>
        /// Cost magnitude across the ability's level range, evaluated through the real
        /// scaler pipeline: "10/15/20/25 Mana".
        /// </summary>
        internal static string GetAbilityCostSummary(Ability a)
        {
            var cost = a.Cost;
            if (cost == null) return "-";

            var series = AbilityInspector.BuildEffectSeries(
                cost, Mathf.Max(1, a.GetLinkedStartingLevel()), a.GetLinkedMaxLevel());

            var impact = series?.Impacts.FirstOrDefault();
            if (impact == null)
            {
                var effectName = cost.GetName();
                return string.IsNullOrEmpty(effectName) || effectName == cost.GetType().Name ? "-" : effectName;
            }

            var magnitudes = impact.Total.Select(Mathf.Abs).ToArray();
            string attr = impact.Attribute != null ? impact.Attribute.GetName() : "";
            return $"{ImpactSeries.FormatSeries(magnitudes)} {attr}".TrimEnd();
        }

        /// <summary>Cooldown duration across the ability's level range: "8/7/6/5s".</summary>
        internal static string GetAbilityCooldownSummary(Ability a)
        {
            var cooldown = a.Cooldown;
            if (cooldown == null) return "-";

            var series = AbilityInspector.BuildEffectSeries(
                cooldown, Mathf.Max(1, a.GetLinkedStartingLevel()), a.GetLinkedMaxLevel());

            if (series?.Duration == null || series.DurationPolicy != EEffectDurationPolicy.Durational)
            {
                var effectName = cooldown.GetName();
                return string.IsNullOrEmpty(effectName) || effectName == cooldown.GetType().Name ? "-" : effectName;
            }

            return $"{series.FormatDuration()}s";
        }
        
        private static string GetAbilitySourceReqCount(Ability a)
        {
            var reqs = a.Tags.TagRequirements.SourceRequirements;
            if (reqs == null) return "0";
            int count = (reqs.RequireTags?.Count ?? 0) + (reqs.AvoidTags?.Count ?? 0);
            return count.ToString();
        }
        
        private static string GetAbilityTargetReqCount(Ability a)
        {
            var reqs = a.Tags.TagRequirements.TargetRequirements;
            if (reqs == null) return "0";
            int count = (reqs.RequireTags?.Count ?? 0) + (reqs.AvoidTags?.Count ?? 0);
            return count.ToString();
        }
        
        private static string GetEffectName(GameplayEffect e) => 
            !string.IsNullOrEmpty(e.GetName()) ? e.GetName() : e.name;
        
        /// <summary>
        /// Duration across the effect's level range (single value when the effect has no
        /// level provider): "Instant", "∞", or "3/4/5/6s".
        /// </summary>
        private static string GetEffectDuration(GameplayEffect e)
        {
            var spec = e.DurationSpecification;
            if (spec == null) return "-";

            switch (spec.DurationPolicy)
            {
                case EEffectDurationPolicy.Instant:
                    return "Instant";
                case EEffectDurationPolicy.Infinite:
                    return spec.EnablePeriodicTicks ? $"∞ (tick {spec.TickIntervalOperation?.Magnitude:0.##}s)" : "∞";
            }

            var series = AbilityInspector.BuildEffectSeries(e, 1, Mathf.Max(1, e.GetLinkedMaxLevel()));
            if (series?.Duration == null) return "-";

            string ticks = series.PeriodicTicks ? $" ({ImpactSeries.FormatSeries(series.Ticks.Select(t => (float)t).ToArray(), "0")}t)" : "";
            return $"{series.FormatDuration()}s{ticks}";
        }

        /// <summary>
        /// Impact magnitude across the effect's level range, per the first authored entry:
        /// "Health −30" or "Health 4/6/8/10 (tick)".
        /// </summary>
        private static string GetEffectImpact(GameplayEffect e)
        {
            var series = AbilityInspector.BuildEffectSeries(e, 1, Mathf.Max(1, e.GetLinkedMaxLevel()));
            if (series == null || series.Impacts.Count == 0) return "-";

            var impact = series.Impacts[0];
            string attr = impact.Attribute != null ? impact.Attribute.GetName() : "?";
            string values = impact.Operation == ECalculationOperation.Multiply
                ? $"×{impact.FormatMagnitude()}"
                : impact.FormatMagnitude();
            string delivery = impact.Delivery switch
            {
                EImpactDelivery.Periodic => " (tick)",
                EImpactDelivery.Continuous => " (mod)",
                _ => "",
            };
            string more = series.Impacts.Count > 1 ? $" +{series.Impacts.Count - 1}" : "";
            return $"{attr} {values}{delivery}{more}";
        }
        
        private static string GetEffectSourceReqCount(GameplayEffect e)
        {
            var reqs = e.SourceRequirements;
            if (reqs == null) return "0";
            int count = GetTagRequirementsCount(reqs);
            return count.ToString();
        }

        private static int GetTagRequirementsCount(EffectTagRequirements requirements)
        {
            var (appReq, appAvoid) = GetTagGroupCounts(requirements.ApplicationRequirements);
            var (ongoingReq, ongoingAvoid) = GetTagGroupCounts(requirements.OngoingRequirements);
            var (removalReq, removalAvoid) = GetTagGroupCounts(requirements.RemovalRequirements);
            
            int totalReq = appReq + ongoingReq + removalReq;
            int totalAvoid = appAvoid + ongoingAvoid + removalAvoid;

            return totalReq + totalAvoid;
            
            static (int required, int avoided) GetTagGroupCounts(AvoidRequireTagGroup tags)
            {
                int reqCount = tags.RequireTags.Count;
                int avoidCount = tags.AvoidTags.Count;
            
                return (reqCount, avoidCount);
            }
        }
        
        private static string GetEffectTargetReqCount(GameplayEffect e)
        {
            var reqs = e.TargetRequirements;
            if (reqs == null) return "0";
            int count = GetTagRequirementsCount(reqs);
            return count.ToString();
        }
        
        private static string GetAttributeName(Attribute a) => 
            !string.IsNullOrEmpty(a.Name) ? a.Name : a.name;
        
        private static string GetAttributeDescription(Attribute a)
        {
            var desc = a.Description;
            if (string.IsNullOrEmpty(desc)) return "-";
            return desc.Length > 60 ? desc.Substring(0, 57) + "..." : desc;
        }
        
        private static string GetEntityName(EntityIdentity e) => 
            !string.IsNullOrEmpty(e.GetName()) ? e.GetName() : e.name;

        private static string GetAttributeSetName(AttributeSet a) =>
            !string.IsNullOrEmpty(a.GetName()) ? a.GetName() : a.name;
        
        private static string GetItemName(Item i) =>
            !string.IsNullOrEmpty(i.GetName()) ? i.GetName() : i.name;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Analysis
        // ═══════════════════════════════════════════════════════════════════════════
        
        private void RunAnalysis()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("PlayForge Asset Analysis");
            sb.AppendLine("========================\n");
            
            foreach (var typeInfo in AssetTypes)
            {
                var count = CountAssetsOfType(typeInfo.Type);
                sb.AppendLine($"{typeInfo.DisplayName}: {count}");
            }
            
            sb.AppendLine($"\nTotal: {cachedAssets.Count} assets");
            
            sb.AppendLine("\n--- Tag Registry ---");
            sb.AppendLine($"Unique Tags: {ForgeTagRegistry.GetAllTags().Count()}");
            sb.AppendLine($"Contexts: {ForgeTagRegistry.GetAllContextKeys().Count()}");
            
            var topTags = ForgeTagRegistry.GetAllTagRecords()
                .OrderByDescending(r => r.TotalUsageCount)
                .Take(10);
            
            sb.AppendLine("\nTop 10 Tags:");
            foreach (var record in topTags)
            {
                var contexts = string.Join(", ", record.UsageByContext.Keys.Take(2));
                sb.AppendLine($"  • {record.Tag}: {record.TotalUsageCount}x ({contexts})");
            }
            
            EditorUtility.DisplayDialog("Analysis Results", sb.ToString(), "OK");
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // PlayForge Visualizer Window
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class PlayForgeVisualizer : EditorWindow
    {
        private ScriptableObject targetAsset;
        private int previewLevel = -1; // -1 = uninitialised; clamped into range per asset

        public void SetAsset(ScriptableObject asset)
        {
            targetAsset = asset;
            previewLevel = -1;

            if (asset != null)
            {
                var typeInfo = TheForge.AssetTypes.FirstOrDefault(t => t.Type == asset.GetType());
                titleContent = new GUIContent(TheForge.GetAssetDisplayName(asset));
                minSize = new Vector2(420, 340);
            }

            Rebuild();
        }

        private void CreateGUI()
        {
            // The style sheet sizes and colors the kit's glyphs (ED-20)
            ForgeTheme.Apply(rootVisualElement);
            Rebuild();
        }

        private void Rebuild()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);

            if (targetAsset == null)
            {
                var noAsset = new Label("No asset selected");
                noAsset.style.color = Colors.HintText;
                noAsset.style.paddingLeft = 16;
                noAsset.style.paddingTop = 16;
                root.Add(noAsset);
                return;
            }

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 16;
            scroll.style.paddingRight = 16;
            scroll.style.paddingTop = 16;
            scroll.style.paddingBottom = 16;
            root.Add(scroll);

            var content = scroll.contentContainer;
            var typeInfo = TheForge.AssetTypes.FirstOrDefault(t => t.Type == targetAsset.GetType());

            BuildVizHeader(content, typeInfo);

            switch (targetAsset)
            {
                case Ability ability:
                    BuildAbilityViz(content, ability);
                    break;
                case GameplayEffect effect:
                    BuildEffectViz(content, effect);
                    break;
                case EntityIdentity entity:
                    BuildEntityViz(content, entity);
                    break;
                default:
                    var placeholder = new Label($"No visualizer for {targetAsset.GetType().Name} yet.");
                    placeholder.style.color = Colors.HintText;
                    placeholder.style.marginTop = 20;
                    content.Add(placeholder);
                    break;
            }
        }

        private void BuildVizHeader(VisualElement content, TheForge.AssetTypeInfo typeInfo)
        {
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 12;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Colors.BorderDark;
            content.Add(header);

            var icon = new ForgeTypeIcon(typeInfo?.Type);
            icon.style.marginRight = 12;
            header.Add(icon);

            var titleContainer = new VisualElement();
            titleContainer.style.flexGrow = 1;
            header.Add(titleContainer);

            var title = new Label(TheForge.GetAssetDisplayName(targetAsset));
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Colors.HeaderText;
            titleContainer.Add(title);

            var subtitle = new Label($"{typeInfo?.DisplayName ?? "Asset"} • {targetAsset.name}");
            subtitle.style.fontSize = 10;
            subtitle.style.color = Colors.HintText;
            titleContainer.Add(subtitle);

            var selectBtn = new Button(() =>
            {
                Selection.activeObject = targetAsset;
                EditorGUIUtility.PingObject(targetAsset);
            }) { text = "Select" };
            selectBtn.style.fontSize = 10;
            header.Add(selectBtn);
        }

        // ─── Shared building blocks ─────────────────────────────────────────────

        private Label VizSection(VisualElement content, string text)
        {
            var label = new Label(text);
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = Colors.HeaderText;
            label.style.marginTop = 12;
            label.style.marginBottom = 4;
            content.Add(label);
            return label;
        }

        private void VizLine(VisualElement content, string text, Color? color = null, int indent = 0)
        {
            var label = new Label(text);
            label.style.fontSize = 11;
            label.style.color = color ?? Colors.LabelText;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginLeft = indent;
            label.style.marginBottom = 1;
            content.Add(label);
        }

        private void VizAssetLink(VisualElement content, string prefix, UnityEngine.Object asset, int indent = 0)
        {
            if (asset == null) return;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginLeft = indent;
            row.style.marginBottom = 1;
            content.Add(row);

            if (!string.IsNullOrEmpty(prefix))
            {
                var pre = new Label(prefix);
                pre.style.fontSize = 11;
                pre.style.color = Colors.LabelText;
                row.Add(pre);
            }

            var link = new Label(TheForge.GetAssetDisplayName(asset as ScriptableObject) is { Length: > 0 } n ? n : asset.name);
            link.style.fontSize = 11;
            link.style.color = Colors.AccentBlue;
            link.RegisterCallback<MouseEnterEvent>(_ => link.style.unityFontStyleAndWeight = FontStyle.Bold);
            link.RegisterCallback<MouseLeaveEvent>(_ => link.style.unityFontStyleAndWeight = FontStyle.Normal);
            link.RegisterCallback<ClickEvent>(evt =>
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                evt.StopPropagation();
            });
            row.Add(link);
        }

        private int BuildLevelSlider(VisualElement content, int minLevel, int maxLevel)
        {
            if (previewLevel < minLevel || previewLevel > maxLevel)
                previewLevel = maxLevel;

            if (maxLevel <= minLevel) return previewLevel;

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;
            content.Add(row);

            var slider = new SliderInt($"Preview Level ({minLevel}–{maxLevel})", minLevel, maxLevel)
            {
                value = previewLevel,
                showInputField = true,
            };
            slider.style.flexGrow = 1;
            slider.RegisterValueChangedCallback(evt =>
            {
                previewLevel = evt.newValue;
                Rebuild();
            });
            row.Add(slider);

            return previewLevel;
        }

        private static string DeliveryLabel(EImpactDelivery delivery) => delivery switch
        {
            EImpactDelivery.Continuous => "while active",
            EImpactDelivery.Periodic => "per tick",
            _ => "on apply",
        };

        private void BuildImpactSeriesLines(VisualElement content, EffectLevelSeries series, int indent)
        {
            if (series == null) return;

            foreach (var impact in series.Impacts)
            {
                string attr = impact.Attribute != null ? impact.Attribute.GetName() : "?";
                string dyn = impact.IsDynamic ? $" *{impact.DynamicReason}" : "";
                string values = impact.Operation == ECalculationOperation.Multiply
                    ? $"×{impact.FormatMagnitude()}"
                    : impact.FormatMagnitude();
                string total = impact.Delivery == EImpactDelivery.Periodic
                    ? $"  → total {impact.FormatTotal()}"
                    : "";

                VizLine(content, $"{attr}: {values} ({impact.Operation}, {DeliveryLabel(impact.Delivery)}){total}{dyn}",
                    indent: indent);
            }

            if (series.DurationPolicy == EEffectDurationPolicy.Durational && series.Duration != null)
            {
                string ticks = series.PeriodicTicks
                    ? $", ticks {ImpactSeries.FormatSeries(series.Ticks.Select(t => (float)t).ToArray(), "0")}"
                    : "";
                VizLine(content, $"Duration: {series.FormatDuration()}s{ticks}", Colors.HintText, indent);
            }
            else if (series.DurationPolicy == EEffectDurationPolicy.Infinite)
            {
                VizLine(content, "Duration: infinite (until removed)", Colors.HintText, indent);
            }
        }

        // ─── Ability ────────────────────────────────────────────────────────────

        private void BuildAbilityViz(VisualElement content, Ability ability)
        {
            var breakdown = AbilityInspector.Build(ability);

            if (ability.LinkedProvider != null)
                VizAssetLink(content, "Level provider: ", ability.LinkedProvider);

            int level = BuildLevelSlider(content, breakdown.MinLevel, breakdown.MaxLevel);

            VizSection(content, $"Description (Level {level})");
            var lines = breakdown.Describe(level);
            if (lines.Count == 0) VizLine(content, "No declared gameplay.", Colors.HintText);
            foreach (var line in lines) VizLine(content, line);

            VizSection(content, "Effects Across Levels");
            bool any = false;
            foreach (var usage in breakdown.GameplayUsages)
            {
                any = true;
                BuildUsageBlock(content, usage, 0);
            }
            if (!any) VizLine(content, "No effects discovered.", Colors.HintText);

            if (breakdown.Cost?.Series != null || breakdown.Cooldown?.Series != null)
            {
                VizSection(content, "Usage");
                if (breakdown.Cost != null)
                {
                    VizAssetLink(content, "Cost: ", breakdown.Cost.Effect);
                    BuildImpactSeriesLines(content, breakdown.Cost.Series, 12);
                }
                if (breakdown.Cooldown != null)
                {
                    VizAssetLink(content, "Cooldown: ", breakdown.Cooldown.Effect);
                    BuildImpactSeriesLines(content, breakdown.Cooldown.Series, 12);
                }
            }

            if (breakdown.Sequences.Count > 0)
            {
                VizSection(content, "Sequences");
                foreach (var sequence in breakdown.Sequences)
                {
                    VizLine(content, $"{sequence.Name} (stage {sequence.StageIndex})");
                    if (!string.IsNullOrEmpty(sequence.Description)) VizLine(content, sequence.Description, Colors.HintText, 12);
                    foreach (var annotation in sequence.Annotations)
                    {
                        if (!string.IsNullOrEmpty(annotation.Text)) VizLine(content, $"• {annotation.Text}", Colors.HintText, 12);
                    }
                }
            }
        }

        private void BuildUsageBlock(VisualElement content, AbilityEffectUsage usage, int indent)
        {
            string role = usage.Role switch
            {
                EAbilityEffectRole.StageApplication => $"stage {usage.StageIndex}",
                EAbilityEffectRole.StageApplicationWithRemoval => $"stage {usage.StageIndex}, removed on exit",
                EAbilityEffectRole.SequenceApplication => $"sequence{(usage.ApplicationsHint > 1 ? $" ×{usage.ApplicationsHint}" : "")}",
                EAbilityEffectRole.LoadedAsset => "loaded (unclaimed)",
                EAbilityEffectRole.Referenced => "referenced",
                _ => usage.Role.ToString(),
            };
            string scope = usage.Scope switch
            {
                EEffectTargetScope.Caster => "self",
                EEffectTargetScope.AcquiredTarget => "target",
                _ => "?",
            };

            VizAssetLink(content, $"[{role} → {scope}] ", usage.Effect, indent);
            BuildImpactSeriesLines(content, usage.Series, indent + 12);

            foreach (var contained in usage.Contained)
            {
                BuildUsageBlock(content, contained, indent + 16);
            }
        }

        // ─── Effect ─────────────────────────────────────────────────────────────

        private void BuildEffectViz(VisualElement content, GameplayEffect effect)
        {
            if (effect.LinkedProvider != null)
                VizAssetLink(content, "Level provider: ", effect.LinkedProvider);

            int maxLevel = Mathf.Max(1, effect.GetLinkedMaxLevel());
            var series = AbilityInspector.BuildEffectSeries(effect, 1, maxLevel);

            VizSection(content, "Policy");
            var duration = effect.DurationSpecification;
            VizLine(content, $"{duration?.DurationPolicy} • re-application {duration?.ReApplicationPolicy} • {duration?.ReApplicationInteraction}");
            if (duration is { EnablePeriodicTicks: true })
                VizLine(content, $"Periodic ticks{(duration.TickOnApplication ? " + tick on application" : "")}", Colors.HintText);

            VizSection(content, maxLevel > 1 ? $"Impacts (Levels 1–{maxLevel})" : "Impacts");
            if (series == null || series.Impacts.Count == 0) VizLine(content, "No attribute impact (tag/worker-only effect).", Colors.HintText);
            else BuildImpactSeriesLines(content, series, 0);

            var grantedTags = effect.Tags.GrantedTags;
            if (grantedTags is { Count: > 0 })
            {
                VizSection(content, "Granted Tags");
                foreach (var tag in grantedTags) VizLine(content, tag.ToString(), Colors.AccentCyan);
            }

            if (effect.ImpactSpecification?.Packets is { Length: > 0 } packets)
            {
                VizSection(content, "Contained Effects");
                foreach (var packet in packets)
                {
                    if (packet.ContainedEffect == null) continue;
                    VizAssetLink(content, $"[{packet.Policy}] ", packet.ContainedEffect);
                }
            }
        }

        // ─── Entity ─────────────────────────────────────────────────────────────

        private void BuildEntityViz(VisualElement content, EntityIdentity entity)
        {
            if (entity.LinkedProvider != null)
                VizAssetLink(content, "Level provider: ", entity.LinkedProvider);

            VizLine(content, $"Levels {entity.GetStartingLevel()}–{entity.GetMaxLevel()} • activation {entity.ActivationPolicy}", Colors.HintText);

            VizSection(content, "Attribute Set");
            if (entity.AttributeSet == null)
            {
                VizLine(content, "None assigned.", Colors.HintText);
            }
            else
            {
                VizAssetLink(content, "", entity.AttributeSet);
                foreach (var element in entity.AttributeSet.Attributes ?? Enumerable.Empty<AttributeSetElement>())
                {
                    if (element?.Attribute == null) continue;
                    var value = element.ValueFromMagnitude;
                    string overflow = element.Overflow.Policy != EAttributeOverflowPolicy.Unlimited
                        ? $" [{element.Overflow.Policy}]"
                        : "";
                    VizLine(content, $"{element.Attribute.GetName()}: {value.CurrentValue:0.##} / {value.BaseValue:0.##}{overflow}", indent: 12);
                }

                if (entity.AttributeSet.SubSets is { Count: > 0 })
                {
                    foreach (var subset in entity.AttributeSet.SubSets.Where(s => s != null))
                    {
                        VizAssetLink(content, "Subset: ", subset, 12);
                    }
                }
            }

            VizSection(content, "Starting Abilities");
            if (entity.StartingAbilities is { Count: > 0 })
            {
                foreach (var ability in entity.StartingAbilities.Where(a => a != null))
                {
                    VizAssetLink(content, "", ability, 0);
                    var costSummary = TheForge.GetAbilityCostSummary(ability);
                    var cdSummary = TheForge.GetAbilityCooldownSummary(ability);
                    VizLine(content, $"Cost {costSummary} • Cooldown {cdSummary}", Colors.HintText, 12);
                }
            }
            else
            {
                VizLine(content, "None.", Colors.HintText);
            }

            if (entity.StartingItems is { Count: > 0 })
            {
                VizSection(content, "Starting Items");
                VizLine(content, $"{entity.StartingItems.Count} item(s)", Colors.HintText);
            }

            var granted = entity.GetGrantedTags()?.ToList();
            if (granted is { Count: > 0 })
            {
                VizSection(content, "Granted Tags");
                foreach (var tag in granted) VizLine(content, tag.ToString(), Colors.AccentCyan);
            }
        }
    }
}