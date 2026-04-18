using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Represents a gameplay tag with hierarchical support using dot notation.
    /// Example: "Status.Debuff.Burn" where "Status" is the root, "Debuff" is a child, and "Burn" is the leaf.
    /// </summary>
    [Serializable]
    public struct Tag : IHasReadableDefinition, IEquatable<Tag>
    {
        /// <summary>
        /// The full hierarchical name using dot notation (e.g., "Status.Debuff.Burn").
        /// For deterministic tags this includes hash padding.
        /// </summary>
        public string Name;

        /// <summary>
        /// Human-readable display name shown in the editor.
        /// For regular tags this equals Name. For deterministic tags this is the
        /// original input before hash padding (e.g., "Position" instead of "PositionaBcDe...").
        /// Falls back to Name if not set.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// Cached parent path for performance. Set automatically when registered with TagHierarchy.
        /// </summary>
        [NonSerialized]
        internal string CachedParentPath;

        public bool IsValid => !string.IsNullOrEmpty(Name);

        // ═══════════════════════════════════════════════════════════════════════════
        // Constructors & Factory Methods
        // ═══════════════════════════════════════════════════════════════════════════

        private Tag(string name)
        {
            Name = name ?? "";
            DisplayName = name ?? "";
            CachedParentPath = null;
        }

        private Tag(string name, string displayName)
        {
            Name = name ?? "";
            DisplayName = displayName ?? name ?? "";
            CachedParentPath = null;
        }

        public static Tag Generate(string _name)
        {
            var tag = new Tag(_name);
            TagRegistry.Register(tag);
            return tag;
        }

        /// <summary>
        /// Creates a tag with a separate display name. Used internally by
        /// deterministic tag generation where Name contains hash padding.
        /// </summary>
        public static Tag Generate(string _name, string _displayName)
        {
            var tag = new Tag(_name, _displayName);
            TagRegistry.Register(tag);
            return tag;
        }

        public static Tag GenerateAsUnique(string _name, string _prefix = "", int size = 64)
        {
            var tag = TagRegistry.TagUtil.GenerateDeterministicTag(_name, _prefix, Mathf.Min(Mathf.Max(0, size), 64));
            TagRegistry.Register(tag);
            return tag;
        }
        
        /// <summary>
        /// Creates a child tag under this parent.
        /// Example: Tag.Generate("Status").Child("Debuff") returns "Status.Debuff"
        /// </summary>
        public Tag Child(string childName)
        {
            if (string.IsNullOrEmpty(childName))
                return this;
            
            string newName = string.IsNullOrEmpty(Name) ? childName : $"{Name}.{childName}";
            return GenerateAsUnique(newName);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Hierarchy Properties
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns the depth of this tag in the hierarchy (1 = root, 2 = one level deep, etc.)
        /// </summary>
        public int Depth
        {
            get
            {
                if (string.IsNullOrEmpty(Name)) return 0;
                int count = 1;
                for (int i = 0; i < Name.Length; i++)
                {
                    if (Name[i] == '.') count++;
                }
                return count;
            }
        }
        
        /// <summary>
        /// Returns true if this is a root tag (no parent).
        /// </summary>
        public bool IsRoot => !string.IsNullOrEmpty(Name) && !Name.Contains('.');
        
        /// <summary>
        /// Returns the leaf name (last segment after the final dot).
        /// Example: "Status.Debuff.Burn".GetLeafName() returns "Burn"
        /// </summary>
        public string GetLeafName()
        {
            if (string.IsNullOrEmpty(Name)) return "";
            int lastDot = Name.LastIndexOf('.');
            return lastDot < 0 ? Name : Name.Substring(lastDot + 1);
        }
        
        /// <summary>
        /// Returns the parent path (everything before the last dot).
        /// Example: "Status.Debuff.Burn".GetParentPath() returns "Status.Debuff"
        /// </summary>
        public string GetParentPath()
        {
            if (string.IsNullOrEmpty(Name)) return null;
            
            // Use cached value if available
            if (CachedParentPath != null) return CachedParentPath == "" ? null : CachedParentPath;
            
            int lastDot = Name.LastIndexOf('.');
            return lastDot < 0 ? null : Name.Substring(0, lastDot);
        }
        
        /// <summary>
        /// Returns the parent tag, or null if this is a root tag.
        /// </summary>
        public Tag? GetParent()
        {
            var parentPath = GetParentPath();
            return parentPath != null ? GenerateAsUnique(parentPath) : null;
        }
        
        /// <summary>
        /// Returns all ancestor paths from immediate parent to root.
        /// Example: "Status.Debuff.Burn" returns ["Status.Debuff", "Status"]
        /// </summary>
        public IEnumerable<string> GetAncestorPaths()
        {
            string current = GetParentPath();
            while (current != null)
            {
                yield return current;
                int lastDot = current.LastIndexOf('.');
                current = lastDot < 0 ? null : current.Substring(0, lastDot);
            }
        }
        
        /// <summary>
        /// Returns all ancestor tags from immediate parent to root.
        /// </summary>
        public IEnumerable<Tag> GetAncestors()
        {
            foreach (var path in GetAncestorPaths())
            {
                yield return GenerateAsUnique(path);
            }
        }
        
        /// <summary>
        /// Returns the root tag (first segment).
        /// Example: "Status.Debuff.Burn".GetRoot() returns "Status"
        /// </summary>
        public Tag GetRoot()
        {
            if (string.IsNullOrEmpty(Name)) return this;
            int firstDot = Name.IndexOf('.');
            return firstDot < 0 ? this : GenerateAsUnique(Name.Substring(0, firstDot));
        }
        
        /// <summary>
        /// Returns all path segments.
        /// Example: "Status.Debuff.Burn".GetSegments() returns ["Status", "Debuff", "Burn"]
        /// </summary>
        public string[] GetSegments()
        {
            if (string.IsNullOrEmpty(Name)) return Array.Empty<string>();
            return Name.Split('.');
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Hierarchy Matching
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Returns true if this tag is a child (direct or nested) of the given parent.
        /// Example: "Status.Debuff.Burn".IsChildOf("Status.Debuff") returns true
        /// Example: "Status.Debuff.Burn".IsChildOf("Status") returns true
        /// </summary>
        public bool IsChildOf(Tag parent)
        {
            if (string.IsNullOrEmpty(parent.Name) || string.IsNullOrEmpty(Name))
                return false;
            
            // Must be longer than parent and start with parent name followed by dot
            return Name.Length > parent.Name.Length 
                   && Name.StartsWith(parent.Name) 
                   && Name[parent.Name.Length] == '.';
        }
        
        /// <summary>
        /// Returns true if this tag is a direct child (one level deep) of the given parent.
        /// Example: "Status.Debuff".IsDirectChildOf("Status") returns true
        /// Example: "Status.Debuff.Burn".IsDirectChildOf("Status") returns false
        /// </summary>
        public bool IsDirectChildOf(Tag parent)
        {
            if (!IsChildOf(parent)) return false;
            
            // Check there are no more dots after the parent path
            string remainder = Name.Substring(parent.Name.Length + 1);
            return !remainder.Contains('.');
        }
        
        /// <summary>
        /// Returns true if the given tag is a child (direct or nested) of this tag.
        /// </summary>
        public bool IsParentOf(Tag child) => child.IsChildOf(this);
        
        /// <summary>
        /// Returns true if this tag matches the other exactly OR is a child of it.
        /// Useful for "has tag or any child of tag" queries.
        /// </summary>
        public bool MatchesOrIsChildOf(Tag other)
        {
            return Equals(other) || IsChildOf(other);
        }
        
        /// <summary>
        /// Returns true if this tag matches the other exactly OR is a parent of it.
        /// Useful for "has tag or any parent of tag" queries.
        /// </summary>
        public bool MatchesOrIsParentOf(Tag other)
        {
            return Equals(other) || IsParentOf(other);
        }
        
        /// <summary>
        /// Returns true if this tag shares a common ancestor with the other tag.
        /// </summary>
        public bool SharesAncestorWith(Tag other)
        {
            if (string.IsNullOrEmpty(Name) || string.IsNullOrEmpty(other.Name))
                return false;
            
            return GetRoot().Equals(other.GetRoot());
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // IHasReadableDefinition Implementation
        // ═══════════════════════════════════════════════════════════════════════════
        
        public string GetName() => Name;
        public string GetDescription() => "";
        public Texture2D GetDefaultIcon() => null;

        // ═══════════════════════════════════════════════════════════════════════════
        // Equality & Operators
        // ═══════════════════════════════════════════════════════════════════════════

        public override string ToString() => Name ?? "";

        public static bool operator !=(Tag a, Tag b) => !a.Equals(b);
        public static bool operator ==(Tag a, Tag b) => a.Equals(b);

        public bool Equals(Tag other) => Name == other.Name;
        public override bool Equals(object obj) => obj is Tag other && Equals(other);
        public override int GetHashCode() => Name?.GetHashCode() ?? 0;
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Implicit Conversions
        // ═══════════════════════════════════════════════════════════════════════════
        
        public static implicit operator string(Tag tag) => tag.Name;
        public static implicit operator Tag(string name) => GenerateAsUnique(name);
    }
    
    /// <summary>
    /// Mode for matching tags in queries.
    /// </summary>
    public enum ETagMatchMode
    {
        /// <summary>Must match the tag exactly.</summary>
        Exact,
        
        /// <summary>Match the tag or any of its children.</summary>
        IncludeChildren,
        
        /// <summary>Match the tag or any of its parents (ancestors).</summary>
        IncludeParents,
        
        /// <summary>Match if the tag shares the same root.</summary>
        SameRoot
    }
}