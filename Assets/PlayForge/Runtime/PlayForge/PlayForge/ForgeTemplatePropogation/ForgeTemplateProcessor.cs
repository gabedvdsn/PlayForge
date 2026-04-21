#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Asset save hook that triggers template propagation and override detection.
    /// 
    /// When an asset is saved:
    ///   1. If the asset is used as a template by other assets → prompt to propagate
    ///   2. If the asset has a template → update its override set by comparing against template
    /// 
    /// Propagation cascades: if A templates B and B templates C, saving A will
    /// propagate to B, then B's changes propagate to C.
    /// </summary>
    public class ForgeTemplateProcessor : AssetModificationProcessor
    {
        /// <summary>
        /// Set to false to suppress the propagation prompt and always propagate.
        /// Can be wired to a user-facing setting.
        /// </summary>
        public static bool PromptBeforePropagate = true;
        
        /// <summary>
        /// Set to false to completely disable automatic template processing on save.
        /// </summary>
        public static bool Enabled = true;
        
        // Reentrance guard: propagation dirties children → save → OnWillSaveAssets fires again
        private static bool _propagating;
        
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (!Enabled || _propagating) return paths;
            
            // Collect all BaseForgeAsset instances being saved
            var templateAssets = new List<BaseForgeAsset>();
            var childAssets = new List<BaseForgeAsset>();
            
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<BaseForgeAsset>(path);
                if (asset == null) continue;
                
                // Skip types that don't support templates
                if (!ForgeTemplateConfig.SupportsTemplates(asset.GetType())) continue;
                
                // Check if this asset is a template for others
                if (ForgeTemplatePropagator.IsUsedAsTemplate(asset))
                {
                    templateAssets.Add(asset);
                }
                
                // Check if this asset has a template (update overrides)
                if (asset.HasTemplate)
                {
                    childAssets.Add(asset);
                }
            }
            
            // Phase 1: Update overrides on child assets being saved
            foreach (var child in childAssets)
            {
                ForgeTemplatePropagator.UpdateOverrides(child);
            }
            
            // Phase 2: Propagate from template assets to their children
            if (templateAssets.Count > 0)
            {
                PropagateTemplateChanges(templateAssets);
            }
            
            return paths;
        }
        
        private static void PropagateTemplateChanges(List<BaseForgeAsset> templates)
        {
            // Count total children across all templates
            var allChildren = new Dictionary<BaseForgeAsset, List<BaseForgeAsset>>();
            int totalChildren = 0;
            
            foreach (var template in templates)
            {
                var children = ForgeTemplatePropagator.FindChildAssets(template);
                if (children.Count > 0)
                {
                    allChildren[template] = children;
                    totalChildren += children.Count;
                }
            }
            
            if (totalChildren == 0) return;
            
            // Check if any non-excluded field actually changed — skip prompt if not
            bool anyChanges = false;
            foreach (var kvp in allChildren)
            {
                if (ForgeTemplatePropagator.WouldPropagationModifyAny(kvp.Key, kvp.Value))
                {
                    anyChanges = true;
                    break;
                }
            }
            if (!anyChanges) return;
            
            // Prompt if enabled
            if (PromptBeforePropagate)
            {
                var templateNames = string.Join(", ", allChildren.Keys.Select(t => t.GetName()));
                bool proceed = EditorUtility.DisplayDialog(
                    "Template Propagation",
                    $"The following template assets were modified:\n\n{templateNames}\n\n" +
                    $"Propagate changes to {totalChildren} child asset(s)?\n\n" +
                    "Only non-overridden fields will be updated.",
                    "Propagate",
                    "Skip");
                
                if (!proceed) return;
            }
            
            // Propagate with reentrance guard
            _propagating = true;
            try
            {
                var modified = new HashSet<BaseForgeAsset>();
                
                foreach (var kvp in allChildren)
                {
                    var results = ForgeTemplatePropagator.PropagateToAllChildren(kvp.Key);
                    foreach (var r in results) modified.Add(r);
                }
                
                if (modified.Count > 0)
                {
                    // Save on next editor tick to avoid re-triggering OnWillSaveAssets
                    var count = modified.Count;
                    EditorApplication.delayCall += () =>
                    {
                        AssetDatabase.SaveAssets();
                        Debug.Log($"[PlayForge Templates] Propagated changes to {count} asset(s).");
                    };
                }
            }
            finally
            {
                _propagating = false;
            }
        }
    }
    
    /// <summary>
    /// Initialization hook that validates template references on domain reload.
    /// </summary>
    [InitializeOnLoad]
    public static class ForgeTemplateValidator
    {
        static ForgeTemplateValidator()
        {
            // Delay validation to avoid issues during domain reload
            EditorApplication.delayCall += ValidateOnLoad;
        }
        
        private static void ValidateOnLoad()
        {
            // Optional: validate all template references are still valid
            // This catches stale references from deleted assets
        }
    }
}
#endif