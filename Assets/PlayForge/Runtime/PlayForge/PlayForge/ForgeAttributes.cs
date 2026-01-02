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