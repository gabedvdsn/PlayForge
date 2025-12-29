using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Central registry for tracking tag usage across all PlayForge assets.
    /// Provides context-aware tag filtering and usage statistics.
    /// </summary>
    [InitializeOnLoad]
    public static class TagRegistry
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Data Structures
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Represents a unique context where tags are used.
        /// Uses string-based context identifiers (from ForgeContext constants).
        /// </summary>
        public class TagContext : IEquatable<TagContext>
        {
            public string[] ContextStrings { get; }
            public string ContextKey { get; }
            public string FriendlyName { get; }
            
            public TagContext(string[] contextStrings)
            {
                ContextStrings = contextStrings ?? Array.Empty<string>();
                ContextKey = GenerateKey(ContextStrings);
                FriendlyName = GenerateFriendlyName(ContextStrings);
            }
            
            private static string GenerateKey(string[] contexts)
            {
                if (contexts == null || contexts.Length == 0) return "Uncontextualized";
                return string.Join("+", contexts.OrderBy(c => c));
            }
            
            private static string GenerateFriendlyName(string[] contexts)
            {
                if (contexts == null || contexts.Length == 0) return "Uncontextualized";
                return string.Join(" / ", contexts);
            }
            
            public bool Equals(TagContext other)
            {
                if (other == null) return false;
                return ContextKey == other.ContextKey;
            }
            
            public override bool Equals(object obj) => Equals(obj as TagContext);
            public override int GetHashCode() => ContextKey.GetHashCode();
        }
        
        /// <summary>
        /// Tracks a single usage of a tag - which asset and which field path.
        /// </summary>
        public class FieldUsageInfo
        {
            public ScriptableObject Asset { get; }
            public string FieldPath { get; }
            public string FriendlyFieldName { get; }
            
            public FieldUsageInfo(ScriptableObject asset, string fieldPath)
            {
                Asset = asset;
                FieldPath = fieldPath ?? "";
                FriendlyFieldName = GenerateFriendlyFieldName(fieldPath);
            }
            
            private static string GenerateFriendlyFieldName(string fieldPath)
            {
                if (string.IsNullOrEmpty(fieldPath)) return "";
                
                // Convert "Tags.SourceRequirements.RequireTags" to "Source Requirements → Require Tags"
                var parts = fieldPath.Split('.');
                var friendlyParts = new List<string>();
                
                foreach (var part in parts)
                {
                    // Skip common container names
                    if (part == "Tags" || part == "Definition") continue;
                    
                    // Convert camelCase/PascalCase to spaces
                    var friendly = System.Text.RegularExpressions.Regex.Replace(part, "([a-z])([A-Z])", "$1 $2");
                    friendly = System.Text.RegularExpressions.Regex.Replace(friendly, "([A-Z]+)([A-Z][a-z])", "$1 $2");
                    friendlyParts.Add(friendly);
                }
                
                return string.Join(" → ", friendlyParts);
            }
            
            public override bool Equals(object obj)
            {
                if (obj is FieldUsageInfo other)
                    return Asset == other.Asset && FieldPath == other.FieldPath;
                return false;
            }
            
            public override int GetHashCode() => HashCode.Combine(Asset, FieldPath);
        }
        
        /// <summary>
        /// Tracks usage of a single tag across contexts.
        /// </summary>
        public class TagUsageRecord
        {
            public Tag Tag { get; }
            public Dictionary<string, ContextUsage> UsageByContext { get; } = new Dictionary<string, ContextUsage>();
            
            public int TotalUsageCount => UsageByContext.Values.Sum(u => u.Usages.Count);
            
            // Convenience property for backward compatibility
            public int TotalAssetCount => UsageByContext.Values
                .SelectMany(u => u.Usages)
                .Select(u => u.Asset)
                .Distinct()
                .Count();
            
            public TagUsageRecord(Tag tag)
            {
                Tag = tag;
            }
            
            public void AddUsage(TagContext context, ScriptableObject asset, string fieldPath)
            {
                if (!UsageByContext.TryGetValue(context.ContextKey, out var usage))
                {
                    usage = new ContextUsage(context);
                    UsageByContext[context.ContextKey] = usage;
                }
                
                usage.AddUsage(asset, fieldPath);
            }
            
            // Legacy overload for backward compatibility
            public void AddUsage(TagContext context, ScriptableObject asset)
            {
                AddUsage(context, asset, "");
            }
            
            public bool RemoveUsage(TagContext context, ScriptableObject asset)
            {
                if (UsageByContext.TryGetValue(context.ContextKey, out var usage))
                {
                    usage.RemoveAsset(asset);
                    
                    // Clean up empty context entries
                    if (usage.Usages.Count == 0)
                        UsageByContext.Remove(context.ContextKey);
                    
                    return true;
                }
                return false;
            }
            
            public bool RemoveAssetFromAllContexts(ScriptableObject asset)
            {
                bool removed = false;
                var emptyContexts = new List<string>();
                
                foreach (var kvp in UsageByContext)
                {
                    if (kvp.Value.RemoveAsset(asset))
                        removed = true;
                    
                    if (kvp.Value.Usages.Count == 0)
                        emptyContexts.Add(kvp.Key);
                }
                
                foreach (var key in emptyContexts)
                    UsageByContext.Remove(key);
                
                return removed;
            }
        }
        
        /// <summary>
        /// Tracks usage within a specific context.
        /// </summary>
        public class ContextUsage
        {
            public TagContext Context { get; }
            public List<FieldUsageInfo> Usages { get; } = new List<FieldUsageInfo>();
            
            // Convenience property for backward compatibility - unique assets
            public List<ScriptableObject> Assets => Usages.Select(u => u.Asset).Distinct().ToList();
            
            public ContextUsage(TagContext context)
            {
                Context = context;
            }
            
            public void AddUsage(ScriptableObject asset, string fieldPath)
            {
                var info = new FieldUsageInfo(asset, fieldPath);
                if (!Usages.Any(u => u.Asset == asset && u.FieldPath == fieldPath))
                    Usages.Add(info);
            }
            
            public bool RemoveAsset(ScriptableObject asset)
            {
                return Usages.RemoveAll(u => u.Asset == asset) > 0;
            }
            
            /// <summary>
            /// Gets usages grouped by asset.
            /// </summary>
            public Dictionary<ScriptableObject, List<FieldUsageInfo>> GetUsagesByAsset()
            {
                return Usages
                    .GroupBy(u => u.Asset)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
            
            /// <summary>
            /// Gets unique field paths used in this context.
            /// </summary>
            public IEnumerable<string> GetUniqueFieldPaths()
            {
                return Usages.Select(u => u.FieldPath).Distinct().Where(p => !string.IsNullOrEmpty(p));
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Cache State
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static Dictionary<Tag, TagUsageRecord> _tagCache = new Dictionary<Tag, TagUsageRecord>();
        private static Dictionary<string, HashSet<Tag>> _contextToTags = new Dictionary<string, HashSet<Tag>>();
        private static HashSet<Tag> _allTags = new HashSet<Tag>();
        private static bool _isDirty = true;
        private static bool _isScanning = false;
        private static DateTime _lastScanTime = DateTime.MinValue;
        
        // Asset types to scan
        private static readonly Type[] ScanTypes = new Type[]
        {
            typeof(Ability),
            typeof(GameplayEffect),
            typeof(EntityIdentity),
            typeof(Attribute),
            typeof(AttributeSet)
        };
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Static Constructor - Auto-refresh on asset changes
        // ═══════════════════════════════════════════════════════════════════════════
        
        static TagRegistry()
        {
            EditorApplication.delayCall += () =>
            {
                if (_isDirty) RefreshCache();
            };
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Public API
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Gets all unique tags in the project.
        /// </summary>
        public static IEnumerable<Tag> GetAllTags()
        {
            EnsureCacheValid();
            return _allTags;
        }
        
        /// <summary>
        /// Gets all tag usage records with full context information.
        /// </summary>
        public static IEnumerable<TagUsageRecord> GetAllTagRecords()
        {
            EnsureCacheValid();
            return _tagCache.Values;
        }
        
        /// <summary>
        /// Gets tags that have been used in a specific context.
        /// </summary>
        /// <param name="contextStrings">The context strings to filter by (e.g., ForgeContext.Effect).</param>
        /// <param name="includeUniversal">If true, also includes Universal tags.</param>
        public static IEnumerable<Tag> GetTagsForContext(string[] contextStrings, bool includeUniversal = true)
        {
            EnsureCacheValid();
            
            if (contextStrings == null || contextStrings.Length == 0)
                return _allTags;
            
            var result = new HashSet<Tag>();
            var context = new TagContext(contextStrings);
            
            // Get tags from exact context match
            if (_contextToTags.TryGetValue(context.ContextKey, out var contextSet))
            {
                foreach (var tag in contextSet)
                    result.Add(tag);
            }
            
            // Also get tags that match any of the context strings individually
            foreach (var ctxStr in contextStrings)
            {
                foreach (var kvp in _contextToTags)
                {
                    if (kvp.Key.Contains(ctxStr))
                    {
                        foreach (var tag in kvp.Value)
                            result.Add(tag);
                    }
                }
            }
            
            // Include universal tags
            if (includeUniversal)
            {
                var universalKey = new TagContext(new[] { ForgeContext.Universal }).ContextKey;
                if (_contextToTags.TryGetValue(universalKey, out var universalSet))
                {
                    foreach (var tag in universalSet)
                        result.Add(tag);
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Gets tags that have been used in fields with the same ForgeTagContext attribute.
        /// </summary>
        public static IEnumerable<Tag> GetTagsForContext(ForgeTagContext attr)
        {
            if (attr == null)
                return GetAllTags();
            
            return GetTagsForContext(attr.Context, attr.IncludeUniversal);
        }
        
        /// <summary>
        /// Gets usage record for a specific tag.
        /// </summary>
        public static TagUsageRecord GetTagUsage(Tag tag)
        {
            EnsureCacheValid();
            return _tagCache.TryGetValue(tag, out var record) ? record : null;
        }
        
        /// <summary>
        /// Gets all unique contexts that have been used.
        /// </summary>
        public static IEnumerable<string> GetAllContextKeys()
        {
            EnsureCacheValid();
            return _contextToTags.Keys;
        }
        
        /// <summary>
        /// Gets all unique TagContext objects.
        /// </summary>
        public static IEnumerable<TagContext> GetAllContexts()
        {
            EnsureCacheValid();
            return _tagCache.Values
                .SelectMany(r => r.UsageByContext.Values)
                .Select(u => u.Context)
                .Distinct();
        }
        
        /// <summary>
        /// Registers a new tag usage. Called when a tag is assigned in a property drawer.
        /// </summary>
        public static void RegisterTagUsage(Tag tag, string[] contextStrings, ScriptableObject asset, string fieldPath = "")
        {
            if (!IsValidTag(tag)) return;
            if (asset == null) return;
            
            var context = new TagContext(contextStrings);
            
            if (!_tagCache.TryGetValue(tag, out var record))
            {
                record = new TagUsageRecord(tag);
                _tagCache[tag] = record;
                _allTags.Add(tag);
            }
            
            record.AddUsage(context, asset, fieldPath);
            
            if (!_contextToTags.TryGetValue(context.ContextKey, out var contextSet))
            {
                contextSet = new HashSet<Tag>();
                _contextToTags[context.ContextKey] = contextSet;
            }
            contextSet.Add(tag);
        }
        
        /// <summary>
        /// Unregisters a tag usage. Called when a tag is cleared or changed in a property drawer.
        /// If the tag has no remaining usages, it will be removed from the registry.
        /// </summary>
        public static void UnregisterTagUsage(Tag tag, string[] contextStrings, ScriptableObject asset)
        {
            if (!IsValidTag(tag)) return;
            if (asset == null) return;
            
            if (!_tagCache.TryGetValue(tag, out var record))
                return;
            
            var context = new TagContext(contextStrings);
            record.RemoveUsage(context, asset);
            
            // If tag has no more usages, remove it entirely
            if (record.TotalUsageCount == 0)
            {
                RemoveTagFromCache(tag);
            }
            else
            {
                // Clean up context-to-tag mapping if needed
                CleanupContextMapping(tag, context);
            }
        }
        
        /// <summary>
        /// Unregisters all usages of a tag for a specific asset (across all contexts).
        /// Called when an asset is deleted or when we need a full cleanup.
        /// </summary>
        public static void UnregisterTagFromAsset(Tag tag, ScriptableObject asset)
        {
            if (!IsValidTag(tag)) return;
            if (asset == null) return;
            
            if (!_tagCache.TryGetValue(tag, out var record))
                return;
            
            record.RemoveAssetFromAllContexts(asset);
            
            // If tag has no more usages, remove it entirely
            if (record.TotalUsageCount == 0)
            {
                RemoveTagFromCache(tag);
            }
        }
        
        /// <summary>
        /// Checks if a tag is valid (not default and has a non-empty name).
        /// Used for registration from property drawers.
        /// </summary>
        public static bool IsValidTag(Tag tag)
        {
            if (tag.Equals(default(Tag))) return false;
            if (string.IsNullOrWhiteSpace(tag.Name)) return false;
            return true;
        }
        
        /// <summary>
        /// Checks if a tag should be included during scanning.
        /// More permissive than IsValidTag - allows empty names so they show as "unnamed" in the browser.
        /// Only skips tags where Name is null (truly uninitialized).
        /// </summary>
        private static bool IsTagSetForScanning(Tag tag)
        {
            // Only skip if Name is null (truly uninitialized)
            // Allow empty string names so they show up as "unnamed" in the tag browser
            return tag.Name != null;
        }
        
        /// <summary>
        /// Removes a tag completely from the cache.
        /// </summary>
        private static void RemoveTagFromCache(Tag tag)
        {
            _tagCache.Remove(tag);
            _allTags.Remove(tag);
            
            // Remove from all context mappings
            var emptyContexts = new List<string>();
            foreach (var kvp in _contextToTags)
            {
                kvp.Value.Remove(tag);
                if (kvp.Value.Count == 0)
                    emptyContexts.Add(kvp.Key);
            }
            
            foreach (var key in emptyContexts)
                _contextToTags.Remove(key);
        }
        
        /// <summary>
        /// Cleans up context-to-tag mapping if a tag no longer has usages in that context.
        /// </summary>
        private static void CleanupContextMapping(Tag tag, TagContext context)
        {
            if (!_tagCache.TryGetValue(tag, out var record))
                return;
            
            // If the tag has no usages in this context, remove from context mapping
            if (!record.UsageByContext.ContainsKey(context.ContextKey))
            {
                if (_contextToTags.TryGetValue(context.ContextKey, out var contextSet))
                {
                    contextSet.Remove(tag);
                    if (contextSet.Count == 0)
                        _contextToTags.Remove(context.ContextKey);
                }
            }
        }
        
        /// <summary>
        /// Marks the cache as dirty, triggering a refresh on next access.
        /// </summary>
        public static void MarkDirty()
        {
            _isDirty = true;
        }
        
        /// <summary>
        /// Forces a full cache refresh.
        /// </summary>
        public static void RefreshCache()
        {
            if (_isScanning) return;
            
            _isScanning = true;
            
            try
            {
                _tagCache.Clear();
                _contextToTags.Clear();
                _allTags.Clear();
                
                foreach (var assetType in ScanTypes)
                {
                    ScanAssetsOfType(assetType);
                }
                
                _isDirty = false;
                _lastScanTime = DateTime.Now;
                
                Debug.Log($"[TagRegistry] Scanned {_allTags.Count} unique tags across {_contextToTags.Count} contexts.");
            }
            finally
            {
                _isScanning = false;
            }
        }
        
        /// <summary>
        /// Gets the time of the last cache scan.
        /// </summary>
        public static DateTime LastScanTime => _lastScanTime;
        
        /// <summary>
        /// Gets whether the cache is currently valid.
        /// </summary>
        public static bool IsCacheValid => !_isDirty && _lastScanTime != DateTime.MinValue;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Internal Scanning Logic
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static void EnsureCacheValid()
        {
            if (_isDirty || _lastScanTime == DateTime.MinValue)
                RefreshCache();
        }
        
        private static void ScanAssetsOfType(Type assetType)
        {
            var guids = AssetDatabase.FindAssets($"t:{assetType.Name}");
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                
                if (asset == null) continue;
                
                ScanObject(asset, asset, assetType, "");
            }
        }
        
        private static void ScanObject(object obj, ScriptableObject rootAsset, Type rootType, string parentFieldPath)
        {
            if (obj == null) return;
            
            var type = obj.GetType();
            var fields = GetAllFields(type);
            
            foreach (var field in fields)
            {
                var fieldType = field.FieldType;
                var fieldPath = string.IsNullOrEmpty(parentFieldPath) 
                    ? field.Name 
                    : $"{parentFieldPath}.{field.Name}";
                
                // Get context from ForgeTagContext attribute
                var contextAttr = field.GetCustomAttribute<ForgeTagContext>();
                var context = contextAttr != null 
                    ? new TagContext(contextAttr.Context)
                    : DeriveContextFromField(field, rootType);
                
                // Check for Tag field
                if (fieldType == typeof(Tag))
                {
                    var tag = (Tag)field.GetValue(obj);
                    
                    // Special handling for AssetIdentifier tags (like AssetTag)
                    // If the name is empty but it's an AssetIdentifier context, derive name from asset
                    if (contextAttr != null && 
                        contextAttr.Context != null && 
                        contextAttr.Context.Contains(ForgeContext.AssetIdentifier) &&
                        string.IsNullOrEmpty(tag.Name))
                    {
                        // Derive tag name from the asset's display name
                        var derivedName = GetAssetDisplayName(rootAsset);
                        if (!string.IsNullOrEmpty(derivedName))
                        {
                            tag = Tag.Generate(derivedName);
                            RegisterTagInCache(tag, context, rootAsset, fieldPath);
                        }
                    }
                    // Normal tag scanning
                    else if (IsTagSetForScanning(tag))
                    {
                        RegisterTagInCache(tag, context, rootAsset, fieldPath);
                    }
                }
                // Check for List<Tag>
                else if (fieldType == typeof(List<Tag>))
                {
                    var list = field.GetValue(obj) as List<Tag>;
                    if (list != null)
                    {
                        foreach (var tag in list)
                        {
                            if (IsTagSetForScanning(tag))
                                RegisterTagInCache(tag, context, rootAsset, fieldPath);
                        }
                    }
                }
                // Check for HashSet<Tag>
                else if (fieldType == typeof(HashSet<Tag>))
                {
                    var set = field.GetValue(obj) as HashSet<Tag>;
                    if (set != null)
                    {
                        foreach (var tag in set)
                        {
                            if (IsTagSetForScanning(tag))
                                RegisterTagInCache(tag, context, rootAsset, fieldPath);
                        }
                    }
                }
                // Check for Tag[]
                else if (fieldType == typeof(Tag[]))
                {
                    var arr = field.GetValue(obj) as Tag[];
                    if (arr != null)
                    {
                        foreach (var tag in arr)
                        {
                            if (IsTagSetForScanning(tag))
                                RegisterTagInCache(tag, context, rootAsset, fieldPath);
                        }
                    }
                }
                // Recurse into nested objects
                else if (ShouldRecurse(fieldType))
                {
                    var value = field.GetValue(obj);
                    
                    if (value is System.Collections.IList list)
                    {
                        foreach (var item in list)
                        {
                            if (item != null)
                                ScanObject(item, rootAsset, rootType, fieldPath);
                        }
                    }
                    else if (value != null)
                    {
                        ScanObject(value, rootAsset, rootType, fieldPath);
                    }
                }
            }
        }
        
        private static TagContext DeriveContextFromField(FieldInfo field, Type rootType)
        {
            var derivedContexts = new List<string>();
            
            // Add asset type context
            if (rootType == typeof(Ability)) derivedContexts.Add(ForgeContext.Ability);
            else if (rootType == typeof(GameplayEffect)) derivedContexts.Add(ForgeContext.Effect);
            else if (rootType == typeof(EntityIdentity)) derivedContexts.Add(ForgeContext.Entity);
            else if (rootType == typeof(Attribute)) derivedContexts.Add(ForgeContext.Attribute);
            else if (rootType == typeof(AttributeSet)) derivedContexts.Add(ForgeContext.AttributeSet);
            
            // Add semantic context based on field name
            var fieldName = field.Name.ToLowerInvariant();
            
            if (fieldName.Contains("grant")) derivedContexts.Add(ForgeContext.Granted);
            else if (fieldName.Contains("require")) derivedContexts.Add(ForgeContext.Required);
            else if (fieldName.Contains("block")) derivedContexts.Add(ForgeContext.Blocked);
            else if (fieldName.Contains("visibility") || fieldName.Contains("visible")) derivedContexts.Add(ForgeContext.Visibility);
            else if (fieldName.Contains("affiliation") || fieldName.Contains("team")) derivedContexts.Add(ForgeContext.Affiliation);
            else if (fieldName.Contains("target")) derivedContexts.Add(ForgeContext.Targeting);
            else if (fieldName.Contains("category") || fieldName.Contains("type")) derivedContexts.Add(ForgeContext.Category);
            else if (fieldName.Contains("assettag") || fieldName.Contains("identifier")) derivedContexts.Add(ForgeContext.AssetIdentifier);
            else if (fieldName.Contains("cost")) derivedContexts.Add(ForgeContext.Cost);
            else if (fieldName.Contains("cooldown")) derivedContexts.Add(ForgeContext.Cooldown);
            
            return new TagContext(derivedContexts.ToArray());
        }
        
        private static void RegisterTagInCache(Tag tag, TagContext context, ScriptableObject asset, string fieldPath)
        {
            if (!_tagCache.TryGetValue(tag, out var record))
            {
                record = new TagUsageRecord(tag);
                _tagCache[tag] = record;
                _allTags.Add(tag);
            }
            
            record.AddUsage(context, asset, fieldPath);
            
            if (!_contextToTags.TryGetValue(context.ContextKey, out var contextSet))
            {
                contextSet = new HashSet<Tag>();
                _contextToTags[context.ContextKey] = contextSet;
            }
            contextSet.Add(tag);
        }
        
        private static IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fields = new List<FieldInfo>();
            
            while (type != null && type != typeof(object))
            {
                fields.AddRange(type.GetFields(flags)
                    .Where(f => f.DeclaringType == type));
                type = type.BaseType;
            }
            
            return fields;
        }
        
        private static bool ShouldRecurse(Type type)
        {
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum)
                return false;
            
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                return false;
            
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) ||
                type == typeof(Color) || type == typeof(Rect) || type == typeof(Bounds) ||
                type == typeof(AnimationCurve) || type == typeof(Gradient))
                return false;
            
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var elementType = type.GetGenericArguments()[0];
                return ShouldRecurse(elementType) || elementType == typeof(Tag);
            }
            
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                return ShouldRecurse(elementType) || elementType == typeof(Tag);
            }
            
            if (type.IsClass && (type.IsSerializable || type.GetCustomAttribute<SerializableAttribute>() != null))
                return true;
            
            if (type.IsValueType && !type.IsPrimitive)
                return true;
            
            return false;
        }
        
        /// <summary>
        /// Gets the display name for an asset, checking for common name properties.
        /// Formats the name as a valid tag (removes spaces).
        /// </summary>
        private static string GetAssetDisplayName(ScriptableObject asset)
        {
            if (asset == null) return null;
            
            string rawName = null;
            
            // Try to get name from Definition.Name (common pattern for PlayForge assets)
            var defField = asset.GetType().GetField("Definition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (defField != null)
            {
                var def = defField.GetValue(asset);
                if (def != null)
                {
                    var nameField = def.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (nameField != null)
                    {
                        rawName = nameField.GetValue(def) as string;
                    }
                }
            }
            
            // Try direct Name field if Definition.Name was empty
            if (string.IsNullOrEmpty(rawName))
            {
                var directNameField = asset.GetType().GetField("Name", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (directNameField != null)
                {
                    rawName = directNameField.GetValue(asset) as string;
                }
            }
            
            // Fall back to asset file name
            if (string.IsNullOrEmpty(rawName))
            {
                rawName = asset.name;
            }
            
            // Format as tag name (remove spaces and underscores)
            if (!string.IsNullOrEmpty(rawName))
            {
                return rawName.Replace(" ", "").Replace("_", "");
            }
            
            return null;
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // Asset Import Hook - Auto-refresh on asset changes
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public class TagRegistryAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool shouldRefresh = false;
            
            var allPaths = importedAssets.Concat(deletedAssets).Concat(movedAssets);
            
            foreach (var path in allPaths)
            {
                if (path.EndsWith(".asset"))
                {
                    shouldRefresh = true;
                    break;
                }
            }
            
            if (shouldRefresh)
            {
                TagRegistry.MarkDirty();
            }
        }
    }
}