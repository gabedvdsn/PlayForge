using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using Unity.VisualScripting;

namespace FarEmerald.PlayForge
{
    public static partial class Tags
    {
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Special Tags
        // ═══════════════════════════════════════════════════════════════════════════

        [ForgeContextDefault(ForgeContext.System, ForgeContext.Affiliation, ForgeContext.Debug)]
        public static Tag SYSTEM => Tag.Generate("GameRoot");
        
        public static Tag NONE => Tag.Generate("None");
        
        [ForgeContextDefault(ForgeContext.Debug, ForgeContext.Texture)]
        public static Tag DEBUG => Tag.Generate("Debug");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Base Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeContextDefault(ForgeContext.RetentionGroup)]
        public static Tag IGNORE => Tag.Generate("Ignore");
        
        [ForgeContextDefault(ForgeContext.Texture)]
        public static Tag PRIMARY => Tag.Generate("Primary");
        
        [ForgeContextDefault(ForgeContext.RetentionGroup)]
        public static Tag DEFAULT => Tag.Generate("Default");
        
        [ForgeContextDefault(ForgeContext.RetentionGroup)]
        public static Tag ADDITIONAL => Tag.Generate("Bonus");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Classification Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeContextDefault(ForgeContext.Visibility)]
        public static Tag ALWAYS_VISIBLE => Tag.Generate("Always Visible");
        
        [ForgeContextDefault(ForgeContext.Visibility)]
        public static Tag NOT_VISIBLE => Tag.Generate("Not Visible");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Data Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        public static Tag GAS => Tag.Generate("GAS");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Mono Process Initialization Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        public static Tag PARENT_TRANSFORM => Tag.Generate("Transform");
        public static Tag POSITION => Tag.Generate("Position");
        public static Tag ROTATION => Tag.Generate("Rotation");
        
        public static Tag DERIVATION => Tag.Generate("Derivation");
        public static Tag AFFILIATION => Tag.Generate("Affiliation");
        public static Tag SOURCE => Tag.Generate("Source");
        
        public static Tag TARGET_REAL => Tag.Generate("Target");
        public static Tag TARGET_POS => Tag.Generate("TargetPosition");
        public static Tag DATA => Tag.Generate("Data");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Functional Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag TARGETED_INTENT => Tag.Generate("TargetedIntent");
        
        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag PROJECTILE_SPEED => Tag.Generate("TargetedIntent");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Report Tags
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag FAILED_TO_INITIALIZE => Tag.Generate("FailedToInitialize");
        
        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag FAILED_WHILE_ACTIVE => Tag.Generate("FailedWhileActive");
    }
}
