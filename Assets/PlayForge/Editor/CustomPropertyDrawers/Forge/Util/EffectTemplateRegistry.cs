using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Registry for storing GameplayEffect templates used for Cost, Cooldown, etc.
    /// Templates are stored as asset GUIDs in EditorPrefs.
    /// </summary>
    public static class EffectTemplateRegistry
    {
        private const string PREFS_PREFIX = "PlayForge_EffectTemplate_";
        
        /// <summary>
        /// Gets the template effect for a given key (e.g., "Cost", "Cooldown").
        /// </summary>
        public static GameplayEffect GetTemplate(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            
            var guid = EditorPrefs.GetString(PREFS_PREFIX + key, "");
            if (string.IsNullOrEmpty(guid)) return null;
            
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            
            return AssetDatabase.LoadAssetAtPath<GameplayEffect>(path);
        }
        
        /// <summary>
        /// Sets the template effect for a given key.
        /// </summary>
        public static void SetTemplate(string key, GameplayEffect effect)
        {
            if (string.IsNullOrEmpty(key)) return;
            
            if (effect == null)
            {
                EditorPrefs.DeleteKey(PREFS_PREFIX + key);
            }
            else
            {
                var path = AssetDatabase.GetAssetPath(effect);
                var guid = AssetDatabase.AssetPathToGUID(path);
                EditorPrefs.SetString(PREFS_PREFIX + key, guid);
            }
        }
        
        /// <summary>
        /// Checks if a template is set for a given key.
        /// </summary>
        public static bool HasTemplate(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            var guid = EditorPrefs.GetString(PREFS_PREFIX + key, "");
            return !string.IsNullOrEmpty(guid);
        }
        
        /// <summary>
        /// Clears the template for a given key.
        /// </summary>
        public static void ClearTemplate(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            EditorPrefs.DeleteKey(PREFS_PREFIX + key);
        }
        
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
