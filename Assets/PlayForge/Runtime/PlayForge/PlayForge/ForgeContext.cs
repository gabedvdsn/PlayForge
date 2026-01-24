namespace FarEmerald.PlayForge.Extended
{
    /// <summary>
    /// Predefined context tags for ForgeTagContext attribute.
    /// These define the semantic context in which tags are used.
    /// 
    /// Usage:
    /// [ForgeTagContext(ForgeTags.Effect, ForgeTags.Visibility)]
    /// public string VisibilityTag;
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
        
        /// <summary>Tags used in Attribute assets</summary>
        public const string Attribute = "Attribute";
        
        /// <summary>Tags used in AttributeSet assets</summary>
        public const string AttributeSet = "AttributeSet";
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Semantic Contexts
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Tags that identify an asset (AssetTag field)</summary>
        public const string AssetIdentifier = "Asset Identifier";
        
        public const string ContextIdentifier = "Context Identifier";
        
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
        
        /// <summary>Tags for categorization/filtering</summary>
        public const string Texture = "Texture";
        
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
        
        /// <summary>Tags for impact/damage calculations</summary>
        public const string Impact = "Impact";
        
        /// <summary>Tags for duration-related logic</summary>
        public const string Duration = "Duration";

        /// <summary>Tags for effect impact retention groups</summary>
        public const string RetentionGroup = "Impact Retention";
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Special Contexts
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Tags that can be used anywhere (no filtering)</summary>
        public const string Universal = "Universal";
        
        /// <summary>Tags for internal/system use</summary>
        public const string System = "System";
        
        /// <summary>Tags for debugging/development</summary>
        public const string Debug = "Debug";
    }
}