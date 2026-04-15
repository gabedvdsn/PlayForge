using System;

namespace FarEmerald.PlayForge.Extended
{
    /// <summary>
    /// Marks a GameplayEffect field as template-able.
    /// The drawer will show an "Import from Template" option when a template is configured.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class ForgeEffectTemplateAttribute : System.Attribute
    {
        /// <summary>
        /// The template key used to look up the template in settings.
        /// Common values: "Cost", "Cooldown"
        /// </summary>
        public string TemplateKey { get; }
        
        /// <summary>
        /// Display name shown in UI. If null, uses TemplateKey.
        /// </summary>
        public string DisplayName { get; }
        
        public ForgeEffectTemplateAttribute(string templateKey, string displayName = null)
        {
            TemplateKey = templateKey;
            DisplayName = displayName ?? templateKey;
        }
    }
}
