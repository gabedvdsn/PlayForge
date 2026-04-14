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
            
            /// <summary>
            /// Indicates this is a system-defined default tag, not from an asset.
            /// </summary>
            public bool IsSystemDefault { get; }
            
            public FieldUsageInfo(ScriptableObject asset, string fieldPath, bool isSystemDefault = false)
            {
                Asset = asset;
                FieldPath = fieldPath ?? "";
                FriendlyFieldName = GenerateFriendlyFieldName(fieldPath);
                IsSystemDefault = isSystemDefault;
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
            
            /// <summary>
            /// If true, this tag was registered via [ForgeContextDefault] attribute.
            /// </summary>
            public bool IsSystemDefault { get; private set; }
            
            public int TotalUsageCount => UsageByContext.Values.Sum(u => u.Usages.Count);
            
            // Convenience property for backward compatibility
            public int TotalAssetCount => UsageByContext.Values
                .SelectMany(u => u.Usages)
                .Select(u => u.Asset)
                .Where(a => a != null)
                .Distinct()
                .Count();
            
            public TagUsageRecord(Tag tag)
            {
                Tag = tag;
            }
            
            public void AddUsage(TagContext context, ScriptableObject asset, string fieldPath, bool isSystemDefault = false)
            {
                if (isSystemDefault)
                    IsSystemDefault = true;
                
                if (!UsageByContext.TryGetValue(context.ContextKey, out var usage))
                {
                    usage = new ContextUsage(context);
                    UsageByContext[context.ContextKey] = usage;
                }
                
                usage.AddUsage(asset, fieldPath, isSystemDefault);
            }
            
            // Legacy overload for backward compatibility
            public void AddUsage(TagContext context, ScriptableObject asset)
            {
                AddUsage(context, asset, "", false);
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
            public List<ScriptableObject> Assets => Usages
                .Where(u => u.Asset != null)
                .Select(u => u.Asset)
                .Distinct()
                .ToList();
            
            public ContextUsage(TagContext context)
            {
                Context = context;
            }
            
            public void AddUsage(ScriptableObject asset, string fieldPath, bool isSystemDefault = false)
            {
                var info = new FieldUsageInfo(asset, fieldPath, isSystemDefault);
                
                // For system defaults (asset is null), check by fieldPath only
                if (isSystemDefault)
                {
                    if (!Usages.Any(u => u.IsSystemDefault && u.FieldPath == fieldPath))
                        Usages.Add(info);
                }
                else
                {
                    if (!Usages.Any(u => u.Asset == asset && u.FieldPath == fieldPath))
                        Usages.Add(info);
                }
            }
            
            public bool RemoveAsset(ScriptableObject asset)
            {
                return Usages.RemoveAll(u => u.Asset == asset && !u.IsSystemDefault) > 0;
            }
            
            /// <summary>
            /// Gets usages grouped by asset.
            /// </summary>
            public Dictionary<ScriptableObject, List<FieldUsageInfo>> GetUsagesByAsset()
            {
                return Usages
                    .Where(u => u.Asset != null)
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
            
            /// <summary>
            /// Gets system default usages (from ForgeContextDefault attributes).
            /// </summary>
            public IEnumerable<FieldUsageInfo> GetSystemDefaults()
            {
                return Usages.Where(u => u.IsSystemDefault);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Cache State
        // ═══════════════════════════════════════════════════════════════════════════
        
        private static Dictionary<Tag, TagUsageRecord> _tagCache = new Dictionary<Tag, TagUsageRecord>();
        private static Dictionary<string, HashSet<Tag>> _contextToTags = new Dictionary<string, HashSet<Tag>>();
        private static HashSet<Tag> _allTags = new HashSet<Tag>();
        private static HashSet<Type> _additionalContextDefaultSources = new HashSet<Type>();
        private static bool _isDirty = true;
        private static bool _isScanning = false;
        private static DateTime _lastScanTime = DateTime.MinValue;
        
        // Types containing static Tag properties to scan for ForgeContextDefault
        private static readonly Type[] ContextDefaultSourceTypes = new Type[]
        {
            typeof(Tags)
        };
        
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
        /// Gets all tags that were registered as system defaults via ForgeContextDefault.
        /// </summary>
        public static IEnumerable<Tag> GetSystemDefaultTags()
        {
            EnsureCacheValid();
            return _tagCache.Values
                .Where(r => r.IsSystemDefault)
                .Select(r => r.Tag);
        }
        
        /// <summary>
        /// Gets system default tags for a specific context.
        /// </summary>
        public static IEnumerable<Tag> GetSystemDefaultTagsForContext(string[] contextStrings)
        {
            EnsureCacheValid();
            
            if (contextStrings == null || contextStrings.Length == 0)
                return GetSystemDefaultTags();
            
            var result = new HashSet<Tag>();
            
            foreach (var record in _tagCache.Values.Where(r => r.IsSystemDefault))
            {
                foreach (var contextUsage in record.UsageByContext.Values)
                {
                    // Check if any of the requested contexts match
                    if (contextStrings.Any(ctx => contextUsage.Context.ContextStrings.Contains(ctx)))
                    {
                        if (contextUsage.GetSystemDefaults().Any())
                        {
                            result.Add(record.Tag);
                            break;
                        }
                    }
                }
            }
            
            return result;
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
            
            // If tag has no more usages, remove it entirely (unless it's a system default)
            if (record.TotalUsageCount == 0 && !record.IsSystemDefault)
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
            
            // If tag has no more usages, remove it entirely (unless it's a system default)
            if (record.TotalUsageCount == 0 && !record.IsSystemDefault)
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
                
                // Scan ForgeContextDefault attributes first (system defaults)
                int defaultCount = ScanContextDefaults();
                
                // Then scan assets
                foreach (var assetType in ScanTypes)
                {
                    ScanAssetsOfType(assetType);
                }
                
                _isDirty = false;
                _lastScanTime = DateTime.Now;
                
                Debug.Log($"[TagRegistry] Scanned {_allTags.Count} unique tags across {_contextToTags.Count} contexts. ({defaultCount} system defaults)");
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

        /// <summary>
        /// Scans static Tag properties in registered types for ForgeContextDefault attributes.
        /// These define default tags that should always appear for specific contexts.
        /// </summary>
        /// <returns>Number of system default tags registered.</returns>
        private static int ScanContextDefaults()
        {
            int count = 0;
            
            // Scan built-in sources
            foreach (var sourceType in ContextDefaultSourceTypes)
            {
                count += ScanTypeForContextDefaults(sourceType);
            }
            
            // Scan any additional registered sources
            foreach (var sourceType in _additionalContextDefaultSources)
            {
                count += ScanTypeForContextDefaults(sourceType);
            }
            
            return count;
        }
        
        /// <summary>
        /// Scans a single type for static Tag properties with ForgeContextDefault attributes.
        /// </summary>
        private static int ScanTypeForContextDefaults(Type type)
        {
            int count = 0;
            var bindingFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            
            // Scan properties (e.g., public static Tag SYSTEM => Tag.Generate(...))
            foreach (var property in type.GetProperties(bindingFlags))
            {
                if (property.PropertyType != typeof(Tag))
                    continue;
                
                var contextAttr = property.GetCustomAttribute<ForgeContextDefault>();
                if (contextAttr == null || contextAttr.Context == null || contextAttr.Context.Length == 0)
                    continue;
                
                try
                {
                    var tag = (Tag)property.GetValue(null);
                    if (!IsTagSetForScanning(tag))
                        continue;
                    
                    // Register this tag for each context specified in the attribute
                    RegisterSystemDefaultTag(tag, contextAttr.Context, $"{type.Name}.{property.Name}");
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TagRegistry] Failed to get value of {type.Name}.{property.Name}: {ex.Message}");
                }
            }
            
            // Also scan fields in case someone uses fields instead of properties
            foreach (var field in type.GetFields(bindingFlags))
            {
                if (field.FieldType != typeof(Tag))
                    continue;
                
                var contextAttr = field.GetCustomAttribute<ForgeContextDefault>();
                if (contextAttr == null || contextAttr.Context == null || contextAttr.Context.Length == 0)
                    continue;
                
                try
                {
                    var tag = (Tag)field.GetValue(null);
                    if (!IsTagSetForScanning(tag))
                        continue;
                    
                    RegisterSystemDefaultTag(tag, contextAttr.Context, $"{type.Name}.{field.Name}");
                    count++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TagRegistry] Failed to get value of {type.Name}.{field.Name}: {ex.Message}");
                }
            }
            
            return count;
        }
        
        /// <summary>
        /// Registers a system default tag for all specified contexts.
        /// </summary>
        private static void RegisterSystemDefaultTag(Tag tag, string[] contexts, string sourcePath)
        {
            // Register the tag for each context individually
            foreach (var contextStr in contexts)
            {
                var context = new TagContext(new[] { contextStr });
                RegisterTagInCacheAsSystemDefault(tag, context, sourcePath);
            }
            
            // Also register with the combined context if there are multiple
            if (contexts.Length > 1)
            {
                var combinedContext = new TagContext(contexts);
                RegisterTagInCacheAsSystemDefault(tag, combinedContext, sourcePath);
            }
        }
        
        /// <summary>
        /// Registers a tag in the cache as a system default.
        /// </summary>
        private static void RegisterTagInCacheAsSystemDefault(Tag tag, TagContext context, string sourcePath)
        {
            if (!_tagCache.TryGetValue(tag, out var record))
            {
                record = new TagUsageRecord(tag);
                _tagCache[tag] = record;
                _allTags.Add(tag);
            }
            
            // Register with null asset (system default) and the source path
            record.AddUsage(context, null, sourcePath, isSystemDefault: true);
            
            if (!_contextToTags.TryGetValue(context.ContextKey, out var contextSet))
            {
                contextSet = new HashSet<Tag>();
                _contextToTags[context.ContextKey] = contextSet;
            }
            contextSet.Add(tag);
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
                var fieldPath = string.IsNullOrEmpty(parentFieldPath) 
                    ? field.Name 
                    : $"{parentFieldPath}.{field.Name}";
                
                var fieldType = field.FieldType;
                
                // Check for single Tag field
                if (fieldType == typeof(Tag))
                {
                    var tag = (Tag)field.GetValue(obj);
                    if (IsTagSetForScanning(tag))
                    {
                        var context = GetContextFromField(field, rootType);
                        RegisterTagInCache(tag, context, rootAsset, fieldPath);
                    }
                }
                // Check for List<Tag>
                else if (fieldType.IsGenericType && 
                         fieldType.GetGenericTypeDefinition() == typeof(List<>) &&
                         fieldType.GetGenericArguments()[0] == typeof(Tag))
                {
                    var tagList = field.GetValue(obj) as IList<Tag>;
                    if (tagList != null)
                    {
                        var context = GetContextFromField(field, rootType);
                        for (int i = 0; i < tagList.Count; i++)
                        {
                            var tag = tagList[i];
                            if (IsTagSetForScanning(tag))
                            {
                                var itemPath = $"{fieldPath}[{i}]";
                                RegisterTagInCache(tag, context, rootAsset, itemPath);
                            }
                        }
                    }
                }
                // Check for Tag[]
                else if (fieldType.IsArray && fieldType.GetElementType() == typeof(Tag))
                {
                    var tagArray = field.GetValue(obj) as Tag[];
                    if (tagArray != null)
                    {
                        var context = GetContextFromField(field, rootType);
                        for (int i = 0; i < tagArray.Length; i++)
                        {
                            var tag = tagArray[i];
                            if (IsTagSetForScanning(tag))
                            {
                                var itemPath = $"{fieldPath}[{i}]";
                                RegisterTagInCache(tag, context, rootAsset, itemPath);
                            }
                        }
                    }
                }
                // Recurse into nested objects
                else if (ShouldRecurse(fieldType))
                {
                    var value = field.GetValue(obj);
                    
                    if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var list = value as System.Collections.IList;
                        if (list != null)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                var item = list[i];
                                if (item != null)
                                    ScanObject(item, rootAsset, rootType, $"{fieldPath}[{i}]");
                            }
                        }
                    }
                    else if (fieldType.IsArray)
                    {
                        var array = value as Array;
                        if (array != null)
                        {
                            for (int i = 0; i < array.Length; i++)
                            {
                                var item = array.GetValue(i);
                                if (item != null)
                                    ScanObject(item, rootAsset, rootType, $"{fieldPath}[{i}]");
                            }
                        }
                    }
                    else if (value != null)
                    {
                        ScanObject(value, rootAsset, rootType, fieldPath);
                    }
                }
            }
        }
        
        private static TagContext GetContextFromField(FieldInfo field, Type rootType)
        {
            // First check for explicit ForgeTagContext attribute
            var contextAttr = field.GetCustomAttribute<ForgeTagContext>();
            if (contextAttr != null && contextAttr.Context.Length > 0)
            {
                return new TagContext(contextAttr.Context);
            }
            
            // Otherwise derive context from field name and root type
            return DeriveContextFromField(field, rootType);
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
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Registration API for Additional Context Default Sources
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Registers an additional type to be scanned for ForgeContextDefault attributes.
        /// Call this during editor initialization if you have custom tag definition classes.
        /// </summary>
        public static void RegisterContextDefaultSource(Type type)
        {
            if (type != null && !_additionalContextDefaultSources.Contains(type))
            {
                _additionalContextDefaultSources.Add(type);
                MarkDirty();
            }
        }
        
        /// <summary>
        /// Gets all types that will be scanned for ForgeContextDefault attributes.
        /// </summary>
        public static IEnumerable<Type> GetContextDefaultSourceTypes()
        {
            return ContextDefaultSourceTypes.Concat(_additionalContextDefaultSources);
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