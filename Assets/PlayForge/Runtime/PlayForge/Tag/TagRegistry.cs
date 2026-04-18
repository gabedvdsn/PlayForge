using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Runtime registry for hierarchical tags. Provides O(1) lookups for parent/child relationships
    /// by maintaining cached mappings. Thread-safe for read operations.
    /// 
    /// Usage:
    /// - Tags are automatically registered when created via Tag.Generate()
    /// - Use TagHierarchy.GetChildren(), GetDescendants(), etc. for fast hierarchy queries
    /// - Call TagHierarchy.Initialize() at game start to pre-populate from known tags
    /// </summary>
    public static class TagRegistry
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Cache Storage
        // ═══════════════════════════════════════════════════════════════════════════
        
        // All registered tags by their full path
        private static readonly Dictionary<string, Tag> _tagsByPath = new();
        
        // Parent -> Direct Children mapping (one level only)
        private static readonly Dictionary<string, HashSet<string>> _directChildren = new();
        
        // Parent -> All Descendants mapping (cached for performance)
        private static readonly Dictionary<string, HashSet<string>> _allDescendantsCache = new();
        
        // Child -> Parent mapping for reverse lookups
        private static readonly Dictionary<string, string> _parentMap = new();
        
        // All root tags (tags with no parent)
        private static readonly HashSet<string> _rootTags = new();

        private static readonly Dictionary<string, Tag> _deterministicTagsByName = new();
        
        // Lock for thread safety during registration
        private static readonly object _lock = new();
        
        // Tracks if we've done initial population
        private static bool _initialized;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Initialization
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Initialize the hierarchy with a set of known tags.
        /// Call this at game startup to pre-populate the registry.
        /// </summary>
        public static void Initialize(IEnumerable<Tag> knownTags = null)
        {
            lock (_lock)
            {
                if (_initialized) return;
                
                // Register built-in tags from Tags class via reflection
                RegisterBuiltInTags();
                
                // Register any provided tags
                if (knownTags != null)
                {
                    foreach (var tag in knownTags)
                    {
                        RegisterInternal(tag);
                    }
                }
                
                _initialized = true;
            }
        }
        
        /// <summary>
        /// Forces re-initialization, clearing all caches.
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                _tagsByPath.Clear();
                _directChildren.Clear();
                _allDescendantsCache.Clear();
                _parentMap.Clear();
                _rootTags.Clear();
                _deterministicTagsByName.Clear();
                _initialized = false;
            }
        }
        
        private static void RegisterBuiltInTags()
        {
            // Use reflection to find all static Tag properties in the Tags class
            var tagsType = typeof(Tags);
            var properties = tagsType.GetProperties(
                System.Reflection.BindingFlags.Public | 
                System.Reflection.BindingFlags.Static);
            
            foreach (var prop in properties)
            {
                if (prop.PropertyType == typeof(Tag))
                {
                    try
                    {
                        var tag = (Tag)prop.GetValue(null);
                        RegisterInternal(tag);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[TagHierarchy] Failed to register tag from Tags.{prop.Name}: {e.Message}");
                    }
                }
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Registration
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Registers a tag with the hierarchy. Called automatically by Tag.Generate().
        /// </summary>
        public static void Register(Tag tag)
        {
            if (string.IsNullOrEmpty(tag.Name)) return;
            
            lock (_lock)
            {
                RegisterInternal(tag);
            }
        }
        
        /// <summary>
        /// Registers multiple tags at once.
        /// </summary>
        public static void RegisterMany(IEnumerable<Tag> tags)
        {
            if (tags == null) return;
            
            lock (_lock)
            {
                foreach (var tag in tags)
                {
                    if (!string.IsNullOrEmpty(tag.Name))
                    {
                        RegisterInternal(tag);
                    }
                }
            }
        }
        
        private static void RegisterInternal(Tag tag)
        {
            string path = tag.Name;
            if (string.IsNullOrEmpty(path)) return;
            
            // Already registered?
            if (_tagsByPath.ContainsKey(path)) return;
            
            // Store the tag
            _tagsByPath[path] = tag;
            
            // Determine parent path
            int lastDot = path.LastIndexOf('.');
            string parentPath = lastDot >= 0 ? path.Substring(0, lastDot) : null;
            
            // Cache parent relationship
            if (parentPath != null)
            {
                _parentMap[path] = parentPath;
                
                // Add to parent's direct children
                if (!_directChildren.TryGetValue(parentPath, out var children))
                {
                    children = new HashSet<string>();
                    _directChildren[parentPath] = children;
                }
                children.Add(path);
                
                // Invalidate descendants cache for all ancestors
                InvalidateDescendantsCache(parentPath);
                
                // Ensure parent is registered (creates implicit parent tags)
                if (!_tagsByPath.ContainsKey(parentPath))
                {
                    RegisterInternal(Tag.GenerateAsUnique(parentPath));
                }
            }
            else
            {
                // This is a root tag
                _rootTags.Add(path);
            }
        }
        
        private static void InvalidateDescendantsCache(string path)
        {
            _allDescendantsCache.Remove(path);
            
            // Also invalidate all ancestors
            if (_parentMap.TryGetValue(path, out var parent))
            {
                InvalidateDescendantsCache(parent);
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Lookup API
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns true if the tag is registered in the hierarchy.
        /// </summary>
        public static bool IsRegistered(Tag tag)
        {
            return !string.IsNullOrEmpty(tag.Name) && _tagsByPath.ContainsKey(tag.Name);
        }

        /// <summary>
        /// Resolves a tag by its Name string, returning the registered instance with
        /// DisplayName intact. If not registered, falls back to Tag.Generate (DisplayName = name).
        /// Use this instead of Tag.GenerateAsUnique when reconstructing from a serialized Name.
        /// </summary>
        public static Tag Resolve(string name)
        {
            if (string.IsNullOrEmpty(name)) return default;
            if (_tagsByPath.TryGetValue(name, out var tag)) return tag;
            return Tag.Generate(name);
        }

        /// <summary>
        /// Returns true if the tag exists in the hierarchy.
        /// </summary>
        public static bool Exists(string path)
        {
            return !string.IsNullOrEmpty(path) && _tagsByPath.ContainsKey(path);
        }
        
        /// <summary>
        /// Gets all registered tags.
        /// </summary>
        public static IEnumerable<Tag> GetAllTags()
        {
            return _tagsByPath.Values;
        }
        
        /// <summary>
        /// Gets all root tags (tags with no parent).
        /// </summary>
        public static IEnumerable<Tag> GetRootTags()
        {
            foreach (var path in _rootTags)
            {
                if (_tagsByPath.TryGetValue(path, out var tag))
                {
                    yield return tag;
                }
            }
        }
        
        /// <summary>
        /// Gets the direct children of a tag (one level deep).
        /// </summary>
        public static IEnumerable<Tag> GetDirectChildren(Tag parent)
        {
            if (string.IsNullOrEmpty(parent.Name)) yield break;
            
            if (_directChildren.TryGetValue(parent.Name, out var children))
            {
                foreach (var childPath in children)
                {
                    if (_tagsByPath.TryGetValue(childPath, out var child))
                    {
                        yield return child;
                    }
                }
            }
        }
        
        /// <summary>
        /// Gets all descendants of a tag (all levels deep). Uses cached results.
        /// </summary>
        public static IEnumerable<Tag> GetAllDescendants(Tag parent)
        {
            if (string.IsNullOrEmpty(parent.Name)) yield break;
            
            // Check cache first
            if (!_allDescendantsCache.TryGetValue(parent.Name, out var descendants))
            {
                // Build cache
                descendants = new HashSet<string>();
                CollectDescendants(parent.Name, descendants);
                _allDescendantsCache[parent.Name] = descendants;
            }
            
            foreach (var path in descendants)
            {
                if (_tagsByPath.TryGetValue(path, out var tag))
                {
                    yield return tag;
                }
            }
        }
        
        private static void CollectDescendants(string parentPath, HashSet<string> result)
        {
            if (!_directChildren.TryGetValue(parentPath, out var children)) return;
            
            foreach (var childPath in children)
            {
                result.Add(childPath);
                CollectDescendants(childPath, result);
            }
        }
        
        /// <summary>
        /// Gets the parent tag, or null if this is a root tag.
        /// </summary>
        public static Tag? GetParent(Tag child)
        {
            if (string.IsNullOrEmpty(child.Name)) return null;
            
            if (_parentMap.TryGetValue(child.Name, out var parentPath))
            {
                if (_tagsByPath.TryGetValue(parentPath, out var parent))
                {
                    return parent;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets all ancestors of a tag (from parent to root).
        /// </summary>
        public static IEnumerable<Tag> GetAncestors(Tag child)
        {
            string current = child.Name;
            
            while (_parentMap.TryGetValue(current, out var parentPath))
            {
                if (_tagsByPath.TryGetValue(parentPath, out var parent))
                {
                    yield return parent;
                }
                current = parentPath;
            }
        }
        
        /// <summary>
        /// Gets all tags at a specific depth level.
        /// </summary>
        public static IEnumerable<Tag> GetTagsAtDepth(int depth)
        {
            foreach (var tag in _tagsByPath.Values)
            {
                if (tag.Depth == depth)
                {
                    yield return tag;
                }
            }
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Fast Matching API
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Fast check if 'tag' is equal to 'query' or is a descendant of it.
        /// Uses cached parent mappings for O(depth) performance.
        /// </summary>
        public static bool MatchesOrIsDescendantOf(Tag tag, Tag query)
        {
            if (tag.Equals(query)) return true;
            return tag.IsChildOf(query);
        }
        
        /// <summary>
        /// Fast check if 'tag' is equal to 'query' or is an ancestor of it.
        /// </summary>
        public static bool MatchesOrIsAncestorOf(Tag tag, Tag query)
        {
            if (tag.Equals(query)) return true;
            return query.IsChildOf(tag);
        }
        
        /// <summary>
        /// Checks if the tag collection contains the query tag according to the match mode.
        /// </summary>
        public static bool Contains(IEnumerable<Tag> tags, Tag query, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            if (tags == null) return false;
            
            switch (matchMode)
            {
                case ETagMatchMode.Exact:
                    return tags.Contains(query);
                    
                case ETagMatchMode.IncludeChildren:
                    // Query matches if any tag equals query or is a child of query
                    foreach (var tag in tags)
                    {
                        if (tag.MatchesOrIsChildOf(query))
                            return true;
                    }
                    return false;
                    
                case ETagMatchMode.IncludeParents:
                    // Query matches if any tag equals query or is a parent of query
                    foreach (var tag in tags)
                    {
                        if (tag.MatchesOrIsParentOf(query))
                            return true;
                    }
                    return false;
                    
                case ETagMatchMode.SameRoot:
                    var queryRoot = query.GetRoot();
                    foreach (var tag in tags)
                    {
                        if (tag.GetRoot().Equals(queryRoot))
                            return true;
                    }
                    return false;
                    
                default:
                    return false;
            }
        }
        
        /// <summary>
        /// Counts how many tags in the collection match the query according to the match mode.
        /// </summary>
        public static int Count(IEnumerable<Tag> tags, Tag query, ETagMatchMode matchMode = ETagMatchMode.Exact)
        {
            if (tags == null) return 0;
            
            int count = 0;
            
            switch (matchMode)
            {
                case ETagMatchMode.Exact:
                    foreach (var tag in tags)
                    {
                        if (tag.Equals(query)) count++;
                    }
                    break;
                    
                case ETagMatchMode.IncludeChildren:
                    foreach (var tag in tags)
                    {
                        if (tag.MatchesOrIsChildOf(query)) count++;
                    }
                    break;
                    
                case ETagMatchMode.IncludeParents:
                    foreach (var tag in tags)
                    {
                        if (tag.MatchesOrIsParentOf(query)) count++;
                    }
                    break;
                    
                case ETagMatchMode.SameRoot:
                    var queryRoot = query.GetRoot();
                    foreach (var tag in tags)
                    {
                        if (tag.GetRoot().Equals(queryRoot)) count++;
                    }
                    break;
            }
            
            return count;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Utility Methods
        // ═══════════════════════════════════════════════════════════════════════════

        public static class TagUtil
        {
            public const int rngGenerationBufferSize = 64;
            
            /// <summary>
        /// Generates a random tag string of length 32–64 that does not exist in the hierarchy.
        /// The tag uses letters, digits, dots, and underscores. Dot placement is controlled to
        /// ensure the result satisfies <see cref="IsValidPath"/>.
        /// </summary>
        /// <returns>A unique random tag string guaranteed not to be registered.</returns>
            public static Tag GenerateUniqueRandomTag(int N = rngGenerationBufferSize)
            {
                // Characters valid per IsValidPath: letters, digits, dot, underscore.
                // Dots are excluded from the first/last positions and cannot appear consecutively.
                const string AlphaNum = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                const string Interior = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.";

                const int MinLength = 32;
                const int MaxLength = 64;
                
                var rng    = new System.Random();
                var buffer = new System.Text.StringBuilder(64);

                while (true)
                {
                    buffer.Clear();

                    int length = Mathf.Clamp(N, MinLength, MaxLength);

                    // First character: letter or digit only (no dot/underscore at start)
                    buffer.Append(AlphaNum[rng.Next(AlphaNum.Length)]);

                    for (int i = 1; i < length - 1; i++)
                    {
                        // Never place a dot directly after another dot
                        char prev = buffer[buffer.Length - 1];
                        if (prev == '.')
                        {
                            // Force a non-dot character
                            buffer.Append(AlphaNum[rng.Next(AlphaNum.Length)]);
                        }
                        else
                        {
                            buffer.Append(Interior[rng.Next(Interior.Length)]);
                        }
                    }

                    // Last character: letter or digit only (no dot/underscore at end)
                    buffer.Append(AlphaNum[rng.Next(AlphaNum.Length)]);

                    string candidate = buffer.ToString();

                    // Verify uniqueness against the live registry (lock for thread safety)
                    lock (_lock)
                    {
                        if (!_tagsByPath.ContainsKey(candidate))
                            return candidate;
                    }
                    // Collision is astronomically unlikely given the search space, but retry if it happens.
                }
            }
            
            /// <summary>
            /// Creates a deterministic tag string from a given input and prefix, padded to
            /// exactly <paramref name="N"/> characters using a SHA-256-derived fill.
            ///
            /// Output format: <c>[prefix][input][padding]</c>, hard-clamped to <paramref name="N"/>.
            ///
            /// If <c>prefix + input</c> already meets or exceeds <paramref name="N"/>, the
            /// result is simply truncated — no padding is appended.
            ///
            /// The same <paramref name="input"/>, <paramref name="prefix"/>, and
            /// <paramref name="N"/> will always produce the same output.
            /// </summary>
            /// <param name="input">The source string to encode into the tag.</param>
            /// <param name="prefix">A fixed string prepended before the input.</param>
            /// <param name="N">Target tag length, clamped internally to [32, 64].</param>
            /// <returns>A deterministic tag string of exactly <paramref name="N"/> characters
            /// (or fewer only if prefix + input together exceed it).</returns>
            public static Tag GenerateDeterministicTag(string input, string prefix, int N = rngGenerationBufferSize)
            {
                lock (_lock)
                {
                    if (_deterministicTagsByName.TryGetValue(input + prefix, out var t)) return t;
                }
                
                // Valid characters per IsValidPath (no dot at end, so omit it from padding pool)
                const string PaddingChars =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";

                N = System.Math.Clamp(N, 32, 64);

                string head = (prefix ?? string.Empty) + (input ?? string.Empty);

                // Already at or beyond the target length — truncate and return
                if (head.Length >= N)
                {
                    var _tag = Tag.Generate(head.Substring(0, N), input);
                    lock (_lock) _deterministicTagsByName[input + prefix] = _tag;
                    return _tag;
                }

                int paddingNeeded = N - head.Length;

                // Derive padding deterministically from the head via SHA-256.
                // Using the full head (not just input) means different prefixes always diverge.
                byte[] hashBytes;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    hashBytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(head));

                var sb = new System.Text.StringBuilder(N);
                sb.Append(head);

                // Walk through hash bytes (cycling if needed) to pick padding characters.
                // Each byte is mapped uniformly-ish onto PaddingChars via modulo — the
                // character set size (63) is small enough that modulo bias is negligible.
                for (int i = 0; i < paddingNeeded; i++)
                    sb.Append(PaddingChars[hashBytes[i % hashBytes.Length] % PaddingChars.Length]);

                var _tag2 = Tag.Generate(sb.ToString(), input);
                lock (_lock) _deterministicTagsByName[input + prefix] = _tag2;
                return _tag2;
            }
        }
        
        /// <summary>
        /// Creates a new child tag under the given parent.
        /// Example: CreateChild("Status.Debuff", "Burn") returns "Status.Debuff.Burn"
        /// </summary>
        public static Tag CreateChild(Tag parent, string childName)
        {
            return parent.Child(childName);
        }
        
        /// <summary>
        /// Creates a tag path from segments.
        /// Example: FromSegments("Status", "Debuff", "Burn") returns "Status.Debuff.Burn"
        /// </summary>
        public static Tag FromSegments(params string[] segments)
        {
            if (segments == null || segments.Length == 0)
                return default;
            
            return Tag.GenerateAsUnique(string.Join(".", segments));
        }
        
        /// <summary>
        /// Validates that a tag path is properly formatted.
        /// </summary>
        public static bool IsValidPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            
            // Cannot start or end with dot
            if (path[0] == '.' || path[path.Length - 1] == '.') return false;
            
            // Cannot have consecutive dots
            if (path.Contains("..")) return false;
            
            // Must not contain invalid characters
            foreach (char c in path)
            {
                if (!char.IsLetterOrDigit(c) && c != '.' && c != '_' && c != ' ')
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Gets debug info about the hierarchy state.
        /// </summary>
        public static string GetDebugInfo()
        {
            return $"[TagHierarchy] Tags: {_tagsByPath.Count}, Roots: {_rootTags.Count}, " +
                   $"Parents: {_directChildren.Count}, Cache: {_allDescendantsCache.Count}";
        }
    }
}
