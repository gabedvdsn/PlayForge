using System;

namespace FarEmerald.PlayForge.Extended
{
    /// <summary>
    /// The kinds of tag a tag field takes, which its picker offers: one context per kind, as ForgeContext names them, or a
    /// project's own (TAG-6).
    ///
    /// Usage:
    /// [ForgeTagContext(ForgeContext.Granted, ForgeContext.AssetTag)]
    /// public Tag Required;
    ///
    /// Or with string literals:
    /// [ForgeTagContext("Granted", "Asset Tag")]
    /// public Tag Required;
    ///
    /// An old context's name reads as its new one, as Trigger does as Event (Decision 74).
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
        /// <param name="context">One or more contexts (e.g., ForgeContext.Granted, ForgeContext.Event)</param>
        public ForgeTagContext(params string[] context)
        {
            // An old context's name reads as its new one (TAG-6)
            Context = ForgeContext.Current(context);
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
        public Tag DefaultTag { get; }

        public ForgeTagDefault(Tag defaultTag)
        {
            DefaultTag = defaultTag;
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
    
    #region System Tags

    /// <summary>
    /// Exposes a tag written in code to the editor's tag pickers, on a static Tag property or field, a project's own as
    /// PlayForge's in Tags: only a code tag marked so is offered, so a project chooses which of its tags its designers see
    /// (TAG-5, Decision 73). Its contexts say which fields offer it; with none, it's under Code. It was ForgeContextDefault,
    /// which a property alone could carry.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public class ForgeTagExposed : System.Attribute
    {
        public string[] Context { get; }

        public ForgeTagExposed(params string[] context)
        {
            Context = ForgeContext.Current(context);
        }
    }
    
    #endregion

}