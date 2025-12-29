using System;

namespace FarEmerald.PlayForge.Extended
{
    /// <summary>
    /// Specifies the context(s) in which a tag field is used.
    /// This allows the TagDrawer to filter available tags based on context.
    /// 
    /// Usage:
    /// [ForgeTagContext(ForgeContext.Effect, ForgeContext.Visibility)]
    /// public Tag VisibilityTag;
    /// 
    /// Or with string literals:
    /// [ForgeTagContext("Effect", "Visibility")]
    /// public Tag VisibilityTag;
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ForgeTagContext : System.Attribute
    {
        /// <summary>
        /// The context strings that define where this tag is used.
        /// </summary>
        public string[] Context { get; }

        /// <summary>
        /// If true, Universal tags are always included regardless of context.
        /// Default is true.
        /// </summary>
        public bool IncludeUniversal { get; set; } = true;

        /// <summary>
        /// If true, allows creating new tags from this field's dropdown.
        /// Default is true.
        /// </summary>
        public bool AllowCreate { get; set; } = true;

        /// <summary>
        /// Creates a tag context attribute with the specified context strings.
        /// Use ForgeContext constants or string literals.
        /// </summary>
        /// <param name="context">One or more context identifiers (e.g., ForgeContext.Effect, ForgeContext.Visibility)</param>
        public ForgeTagContext(params string[] context)
        {
            Context = context ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Predefined context strings for ForgeTagContext attribute.
    /// These define the semantic context in which tags are used.
    /// 
    /// Usage:
    /// [ForgeTagContext(ForgeContext.Effect, ForgeContext.Visibility)]
    /// public Tag VisibilityTag;
    /// </summary>
    public static class ForgeContext
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // Asset Type Contexts
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Tags used in Ability assets</summary>
        public const string Ability = "Ability";
        
        /// <summary>Tags used in GameplayEffect assets</summary>
        public const string Effect = "Effect";
        
        /// <summary>Tags used in EntityIdentity assets</summary>
        public const string Entity = "Entity";
        
        /// <summary>Tags used in System.Attribute assets</summary>
        public const string Attribute = "Attribute";
        
        /// <summary>Tags used in AttributeSet assets</summary>
        public const string AttributeSet = "AttributeSet";
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Semantic Contexts
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Tags that identify an asset (AssetTag field)</summary>
        public const string AssetIdentifier = "AssetIdentifier";
        
        /// <summary>Tags granted to entities/targets</summary>
        public const string Granted = "Granted";
        
        /// <summary>Tags required for activation/application</summary>
        public const string Required = "Required";
        
        /// <summary>Tags that block activation/application</summary>
        public const string Blocked = "Blocked";
        
        /// <summary>Tags for visibility/targeting conditions</summary>
        public const string Visibility = "Visibility";
        
        /// <summary>Tags for affiliation/team grouping</summary>
        public const string Affiliation = "Affiliation";
        
        /// <summary>Tags for categorization/filtering</summary>
        public const string Category = "Category";
        
        /// <summary>Tags for targeting conditions</summary>
        public const string Targeting = "Targeting";
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Functional Contexts
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Tags used in workers/processors</summary>
        public const string Worker = "Worker";
        
        /// <summary>Tags used in modifiers</summary>
        public const string Modifier = "Modifier";
        
        /// <summary>Tags used in conditions/requirements</summary>
        public const string Condition = "Condition";
        
        /// <summary>Tags used in stages/phases</summary>
        public const string Stage = "Stage";
        
        /// <summary>Tags used in triggers/events</summary>
        public const string Trigger = "Trigger";
        
        /// <summary>Tags used in cost definitions</summary>
        public const string Cost = "Cost";
        
        /// <summary>Tags used in cooldown definitions</summary>
        public const string Cooldown = "Cooldown";
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Special Contexts
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Tags that can be used anywhere (no filtering)</summary>
        public const string Universal = "Universal";
        
        /// <summary>Tags for internal/system use</summary>
        public const string System = "System";
        
        /// <summary>Tags for debugging/development</summary>
        public const string Debug = "Debug";
        
        /// <summary>Tags for impact/damage calculations</summary>
        public const string Impact = "Impact";
        
        /// <summary>Tags for duration-related logic</summary>
        public const string Duration = "Duration";
    }

    /// <summary>
    /// Marks a tag field as read-only in the inspector.
    /// The tag can still be set programmatically.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ForgeTagReadOnly : System.Attribute
    {
    }

    /// <summary>
    /// Specifies a default tag value for a field if none is set.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ForgeTagDefault : System.Attribute
    {
        public string DefaultTagName { get; }

        public ForgeTagDefault(string defaultTagName)
        {
            DefaultTagName = defaultTagName;
        }
    }

    /// <summary>
    /// Validates that a tag field is not empty when the asset is saved.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ForgeTagRequired : System.Attribute
    {
        public string ValidationMessage { get; }

        public ForgeTagRequired(string message = null)
        {
            ValidationMessage = message ?? "This tag field is required.";
        }
    }

    /// <summary>
    /// Specifies that this tag field should only accept tags matching specific prefixes.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ForgeTagPrefix : System.Attribute
    {
        public string[] Prefixes { get; }

        public ForgeTagPrefix(params string[] prefixes)
        {
            Prefixes = prefixes ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Groups related tag fields together in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ForgeTagGroup : System.Attribute
    {
        public string GroupName { get; }
        public int Order { get; }

        public ForgeTagGroup(string groupName, int order = 0)
        {
            GroupName = groupName;
            Order = order;
        }
    }

    /// <summary>
    /// Controls whether Universal tags are included when filtering by context.
    /// By default, Universal tags are always included.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ForgeTagExcludeUniversal : System.Attribute
    {
    }

    /// <summary>
    /// Prevents creating new tags from this field's dropdown.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ForgeTagNoCreate : System.Attribute
    {
    }

    /// <summary>
    /// Provides a custom tooltip for the tag field.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ForgeTagTooltip : System.Attribute
    {
        public string Tooltip { get; }

        public ForgeTagTooltip(string tooltip)
        {
            Tooltip = tooltip;
        }
    }
}