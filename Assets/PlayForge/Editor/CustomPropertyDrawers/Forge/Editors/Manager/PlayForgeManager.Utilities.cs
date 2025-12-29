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
    public partial class PlayForgeManager
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // UI Utilities
        // ═══════════════════════════════════════════════════════════════════════════
        
        internal Button CreateButton(string text, Action onClick, string tooltip = null)
        {
            var btn = new Button(onClick) { text = text };
            btn.focusable = false;
            if (!string.IsNullOrEmpty(tooltip))
                btn.tooltip = tooltip;
            return btn;
        }
        
        internal VisualElement CreateSectionHeader(string title, string subtitle)
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
        
        internal VisualElement CreateDivider(int marginVertical = 8)
        {
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = Colors.DividerColor;
            divider.style.marginTop = marginVertical;
            divider.style.marginBottom = marginVertical;
            return divider;
        }
        
        internal void ApplyButtonStyle(Button btn)
        {
            btn.style.backgroundColor = Colors.ButtonBackground;
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Cache Management
        // ═══════════════════════════════════════════════════════════════════════════
        
        internal void RefreshAssetCache()
        {
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
            
            if (!TagRegistry.IsCacheValid)
                TagRegistry.RefreshCache();
        }
        
        private void RefreshAll()
        {
            RefreshAssetCache();
            TagRegistry.RefreshCache();
            ShowTab(currentTab);
        }
        
        internal int CountAssetsOfType(Type type)
        {
            return cachedAssets.Count(a => a.GetType() == type);
        }
        
        internal string GetPathForType(Type type)
        {
            var key = PREFS_PREFIX + "Path_" + type.Name;
            if (EditorPrefs.HasKey(key))
                return EditorPrefs.GetString(key);
            
            return DefaultPaths.TryGetValue(type, out var path) ? path : "Assets/Data";
        }
        
        internal void SetPathForType(Type type, string path)
        {
            EditorPrefs.SetString(PREFS_PREFIX + "Path_" + type.Name, path);
        }
        
        internal string GetRelativeTime(DateTime time)
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
        
        private static string GetAbilityCostSummary(Ability a)
        {
            var cost = a.Cost;
            if (cost == null) return "-";
            var effectName = cost.GetName();
            if (string.IsNullOrEmpty(effectName) || effectName == cost.GetType().Name)
                return "-";
            return effectName;
        }
        
        private static string GetAbilityCooldownSummary(Ability a)
        {
            var cooldown = a.Cooldown;
            if (cooldown == null) return "-";
            var effectName = cooldown.GetName();
            if (string.IsNullOrEmpty(effectName) || effectName == cooldown.GetType().Name)
                return "-";
            return effectName;
        }
        
        private static string GetEffectName(GameplayEffect e) => 
            !string.IsNullOrEmpty(e.GetName()) ? e.GetName() : e.name;
        
        private static string GetEffectDuration(GameplayEffect e)
        {
            var spec = e.DurationSpecification;
            if (spec == null) return "-";
            var str = spec.ToString();
            if (string.IsNullOrEmpty(str) || str == spec.GetType().Name)
                return "-";
            return str;
        }
        
        private static string GetEffectImpact(GameplayEffect e)
        {
            var spec = e.ImpactSpecification;
            if (spec == null) return "-";
            var str = spec.ToString();
            if (string.IsNullOrEmpty(str) || str == spec.GetType().Name)
                return "-";
            return str;
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
                sb.AppendLine($"{typeInfo.Icon} {typeInfo.DisplayName}: {count}");
            }
            
            sb.AppendLine($"\nTotal: {cachedAssets.Count} assets");
            
            sb.AppendLine("\n--- Tag Registry ---");
            sb.AppendLine($"Unique Tags: {TagRegistry.GetAllTags().Count()}");
            sb.AppendLine($"Contexts: {TagRegistry.GetAllContextKeys().Count()}");
            
            var topTags = TagRegistry.GetAllTagRecords()
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
        
        public void SetAsset(ScriptableObject asset)
        {
            targetAsset = asset;
            
            if (asset != null)
            {
                var typeInfo = PlayForgeManager.AssetTypes.FirstOrDefault(t => t.Type == asset.GetType());
                titleContent = new GUIContent($"{typeInfo?.Icon ?? "?"} {PlayForgeManager.GetAssetDisplayName(asset)}");
                minSize = new Vector2(400, 300);
            }
            
            Rebuild();
        }
        
        private void CreateGUI()
        {
            Rebuild();
        }
        
        private void Rebuild()
        {
            var root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.paddingTop = 16;
            root.style.paddingBottom = 16;
            root.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            if (targetAsset == null)
            {
                var noAsset = new Label("No asset selected");
                noAsset.style.color = Colors.HintText;
                root.Add(noAsset);
                return;
            }
            
            var typeInfo = PlayForgeManager.AssetTypes.FirstOrDefault(t => t.Type == targetAsset.GetType());
            
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 16;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Colors.BorderDark;
            root.Add(header);
            
            var icon = new Label(typeInfo?.Icon ?? "?");
            icon.style.fontSize = 28;
            icon.style.marginRight = 12;
            icon.style.color = typeInfo?.Color ?? Colors.LabelText;
            header.Add(icon);
            
            var titleContainer = new VisualElement();
            header.Add(titleContainer);
            
            var title = new Label(PlayForgeManager.GetAssetDisplayName(targetAsset));
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = Colors.HeaderText;
            titleContainer.Add(title);
            
            var subtitle = new Label($"{typeInfo?.DisplayName ?? "Asset"} • {targetAsset.name}");
            subtitle.style.fontSize = 10;
            subtitle.style.color = Colors.HintText;
            titleContainer.Add(subtitle);
            
            var placeholder = new Label("Visualizer content coming soon...");
            placeholder.style.color = Colors.LabelText;
            placeholder.style.whiteSpace = WhiteSpace.Normal;
            placeholder.style.marginTop = 20;
            root.Add(placeholder);
        }
    }
}
