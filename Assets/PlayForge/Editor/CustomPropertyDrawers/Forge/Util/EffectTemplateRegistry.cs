using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// The effects a new Cost or Cooldown effect copies, by "Cost" or "Cooldown". Project Settings → PlayForge holds them,
    /// with the project; they lived in EditorPrefs, which every project on the machine shares (ED-21).
    /// </summary>
    public static class EffectTemplateRegistry
    {
        /// <summary>
        /// Gets the template effect for a given key (e.g., "Cost", "Cooldown").
        /// </summary>
        public static GameplayEffect GetTemplate(string key) => string.IsNullOrEmpty(key) ? null : PlayForgeSettings.UsageEffect(key);

        /// <summary>
        /// Sets the template effect for a given key, as one Undo step.
        /// </summary>
        public static void SetTemplate(string key, GameplayEffect effect)
        {
            if (string.IsNullOrEmpty(key)) return;
            PlayForgeSettings.SetUsageEffect(key, effect);
        }

        /// <summary>
        /// Checks if a template is set for a given key.
        /// </summary>
        public static bool HasTemplate(string key) => GetTemplate(key) != null;

        /// <summary>
        /// Clears the template for a given key.
        /// </summary>
        public static void ClearTemplate(string key) => SetTemplate(key, null);
        
        /// <summary>
        /// Gets the display name for a template key.
        /// </summary>
        public static string GetDisplayName(string key)
        {
            return key switch
            {
                "Cost" => "Cost Effect",
                "Cooldown" => "Cooldown Effect",
                _ => key + " Effect"
            };
        }
        
        /// <summary>
        /// Gets tooltip description for a template key.
        /// </summary>
        public static string GetDescription(string key)
        {
            return key switch
            {
                "Cost" => "Template for ability cost effects. Used when creating new Cost effects on abilities.",
                "Cooldown" => "Template for ability cooldown effects. Used when creating new Cooldown effects on abilities.",
                _ => $"Template for {key} effects."
            };
        }
        
        /// <summary>
        /// All known template keys.
        /// </summary>
        public static readonly string[] KnownKeys = { "Cost", "Cooldown" };
    }
}
