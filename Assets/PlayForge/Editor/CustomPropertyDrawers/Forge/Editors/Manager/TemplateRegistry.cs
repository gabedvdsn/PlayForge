using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Template tag for categorizing asset templates.
    /// Separate from gameplay Tag to avoid confusion.
    /// </summary>
    [Serializable]
    public class TemplateTag
    {
        public string Name;
        public string ColorHex;
        
        public TemplateTag() { }
        
        public TemplateTag(string name, Color color)
        {
            Name = name;
            ColorHex = ColorUtility.ToHtmlStringRGB(color);
        }
        
        public Color GetColor()
        {
            if (ColorUtility.TryParseHtmlString("#" + ColorHex, out var color))
                return color;
            return Color.gray;
        }
        
        public override string ToString() => Name;
        
        public override bool Equals(object obj)
        {
            if (obj is TemplateTag other)
                return Name == other.Name;
            return false;
        }
        
        public override int GetHashCode() => Name?.GetHashCode() ?? 0;
    }
    
    /// <summary>
    /// Represents a single asset template entry.
    /// </summary>
    [Serializable]
    public class AssetTemplate
    {
        public string AssetGUID;
        public string TemplateTagName;
        public bool IsDefault;
        public string DisplayName; // Cached for UI display
        
        [NonSerialized]
        private ScriptableObject _cachedAsset;
        
        public ScriptableObject GetAsset()
        {
            if (_cachedAsset == null && !string.IsNullOrEmpty(AssetGUID))
            {
                var path = AssetDatabase.GUIDToAssetPath(AssetGUID);
                if (!string.IsNullOrEmpty(path))
                    _cachedAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            }
            return _cachedAsset;
        }
        
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(AssetGUID)) return false;
            var path = AssetDatabase.GUIDToAssetPath(AssetGUID);
            return !string.IsNullOrEmpty(path);
        }
    }
    
    /// <summary>
    /// Collection of templates for a specific asset type.
    /// </summary>
    [Serializable]
    public class AssetTypeTemplateCollection
    {
        public string TypeName;
        public List<AssetTemplate> Templates = new List<AssetTemplate>();
    }
    
    /// <summary>
    /// Root container for all template data.
    /// </summary>
    [Serializable]
    public class TemplateRegistryData
    {
        public List<TemplateTag> Tags = new List<TemplateTag>();
        public List<AssetTypeTemplateCollection> Collections = new List<AssetTypeTemplateCollection>();
    }
    
    /// <summary>
    /// Static registry for managing asset templates.
    /// Persisted via EditorPrefs as JSON.
    /// </summary>
    public static class TemplateRegistry
    {
        private const string PREFS_KEY = "PlayForge_TemplateRegistry";
        
        private static TemplateRegistryData _data;
        private static bool _isDirty;
        
        // Default template tags
        private static readonly TemplateTag[] DefaultTags = new[]
        {
            new TemplateTag("Starter", new Color(0.4f, 0.8f, 0.4f)),
            new TemplateTag("Combat", new Color(1f, 0.4f, 0.4f)),
            new TemplateTag("Utility", new Color(0.4f, 0.6f, 1f)),
            new TemplateTag("Passive", new Color(0.7f, 0.5f, 1f)),
            new TemplateTag("Debug", new Color(1f, 0.8f, 0.2f)),
        };
        
        public static TemplateRegistryData Data
        {
            get
            {
                if (_data == null)
                    Load();
                return _data;
            }
        }
        
        public static void Load()
        {
            var json = EditorPrefs.GetString(PREFS_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    _data = JsonUtility.FromJson<TemplateRegistryData>(json);
                }
                catch
                {
                    _data = null;
                }
            }
            
            if (_data == null)
            {
                _data = new TemplateRegistryData();
                _data.Tags.AddRange(DefaultTags);
            }
            
            // Ensure default tags exist
            foreach (var defaultTag in DefaultTags)
            {
                if (!_data.Tags.Any(t => t.Name == defaultTag.Name))
                    _data.Tags.Add(defaultTag);
            }
        }
        
        public static void Save()
        {
            if (_data == null) return;
            
            // Clean up invalid templates before saving
            foreach (var collection in _data.Collections)
            {
                collection.Templates.RemoveAll(t => !t.IsValid());
            }
            
            var json = JsonUtility.ToJson(_data, false);
            EditorPrefs.SetString(PREFS_KEY, json);
            _isDirty = false;
        }
        
        public static void MarkDirty()
        {
            _isDirty = true;
        }
        
        public static bool IsDirty => _isDirty;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Template Tag Management
        // ═══════════════════════════════════════════════════════════════════════════
        
        public static IEnumerable<TemplateTag> GetAllTags() => Data.Tags;
        
        public static TemplateTag GetTag(string name)
        {
            return Data.Tags.FirstOrDefault(t => t.Name == name);
        }
        
        public static void AddTag(TemplateTag tag)
        {
            if (Data.Tags.Any(t => t.Name == tag.Name))
                return;
            
            Data.Tags.Add(tag);
            MarkDirty();
        }
        
        public static void RemoveTag(string name)
        {
            Data.Tags.RemoveAll(t => t.Name == name);
            
            // Remove tag from all templates
            foreach (var collection in Data.Collections)
            {
                foreach (var template in collection.Templates)
                {
                    if (template.TemplateTagName == name)
                        template.TemplateTagName = null;
                }
            }
            
            MarkDirty();
        }
        
        public static void UpdateTag(string oldName, TemplateTag newTag)
        {
            var existing = Data.Tags.FirstOrDefault(t => t.Name == oldName);
            if (existing != null)
            {
                // Update references in templates
                foreach (var collection in Data.Collections)
                {
                    foreach (var template in collection.Templates)
                    {
                        if (template.TemplateTagName == oldName)
                            template.TemplateTagName = newTag.Name;
                    }
                }
                
                existing.Name = newTag.Name;
                existing.ColorHex = newTag.ColorHex;
                MarkDirty();
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Template Management
        // ═══════════════════════════════════════════════════════════════════════════
        
        public static AssetTypeTemplateCollection GetCollection(Type assetType)
        {
            var collection = Data.Collections.FirstOrDefault(c => c.TypeName == assetType.Name);
            if (collection == null)
            {
                collection = new AssetTypeTemplateCollection { TypeName = assetType.Name };
                Data.Collections.Add(collection);
            }
            return collection;
        }
        
        public static IEnumerable<AssetTemplate> GetTemplates(Type assetType)
        {
            return GetCollection(assetType).Templates;
        }
        
        public static AssetTemplate GetDefaultTemplate(Type assetType)
        {
            return GetCollection(assetType).Templates.FirstOrDefault(t => t.IsDefault && t.IsValid());
        }
        
        public static void AddTemplate(Type assetType, ScriptableObject asset, string tagName = null, bool isDefault = false)
        {
            var collection = GetCollection(assetType);
            var guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));
            
            // Check if already exists
            if (collection.Templates.Any(t => t.AssetGUID == guid))
                return;
            
            // If setting as default, clear other defaults
            if (isDefault)
            {
                foreach (var t in collection.Templates)
                    t.IsDefault = false;
            }
            
            var template = new AssetTemplate
            {
                AssetGUID = guid,
                TemplateTagName = tagName,
                IsDefault = isDefault,
                DisplayName = PlayForgeManager.GetAssetDisplayName(asset)
            };
            
            collection.Templates.Add(template);
            MarkDirty();
        }
        
        public static void RemoveTemplate(Type assetType, string guid)
        {
            var collection = GetCollection(assetType);
            collection.Templates.RemoveAll(t => t.AssetGUID == guid);
            MarkDirty();
        }
        
        public static void SetDefaultTemplate(Type assetType, string guid)
        {
            var collection = GetCollection(assetType);
            foreach (var t in collection.Templates)
            {
                t.IsDefault = t.AssetGUID == guid;
            }
            MarkDirty();
        }
        
        public static void ClearDefaultTemplate(Type assetType)
        {
            var collection = GetCollection(assetType);
            foreach (var t in collection.Templates)
            {
                t.IsDefault = false;
            }
            MarkDirty();
        }
        
        public static void SetTemplateTag(Type assetType, string guid, string tagName)
        {
            var collection = GetCollection(assetType);
            var template = collection.Templates.FirstOrDefault(t => t.AssetGUID == guid);
            if (template != null)
            {
                template.TemplateTagName = tagName;
                MarkDirty();
            }
        }
        
        /// <summary>
        /// Applies a template to a target asset by copying serialized properties.
        /// </summary>
        public static bool ApplyTemplate(ScriptableObject template, ScriptableObject target)
        {
            if (template == null || target == null) return false;
            if (template.GetType() != target.GetType()) return false;
            
            try
            {
                EditorUtility.CopySerialized(template, target);
                EditorUtility.SetDirty(target);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[TemplateRegistry] Failed to apply template: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Gets templates filtered by tag.
        /// </summary>
        public static IEnumerable<AssetTemplate> GetTemplatesByTag(Type assetType, string tagName)
        {
            return GetTemplates(assetType).Where(t => t.TemplateTagName == tagName && t.IsValid());
        }
        
        public static void Reset()
        {
            _data = new TemplateRegistryData();
            _data.Tags.AddRange(DefaultTags);
            Save();
        }
    }
}
