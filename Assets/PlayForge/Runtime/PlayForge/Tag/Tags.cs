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
        public static Tag SYSTEM => Tag.GenerateAsUnique("GameRoot");
        
        public static Tag NONE => Tag.GenerateAsUnique("None");
        
        [ForgeContextDefault(ForgeContext.Impact)]
        public static Tag ALLOW => Tag.GenerateAsUnique("Always Allow");

        [ForgeContextDefault(ForgeContext.Impact)]
        public static Tag DISALLOW => Tag.GenerateAsUnique("Never Allow");
        
        [ForgeContextDefault(ForgeContext.Debug, ForgeContext.Texture)]
        public static Tag DEBUG => Tag.GenerateAsUnique("Debug");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Base Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeContextDefault(ForgeContext.RetentionGroup)]
        public static Tag IGNORE => Tag.GenerateAsUnique("Ignore");
        
        [ForgeContextDefault(ForgeContext.Texture)]
        public static Tag PRIMARY => Tag.GenerateAsUnique("Primary");
        
        [ForgeContextDefault(ForgeContext.RetentionGroup)]
        public static Tag DEFAULT => Tag.GenerateAsUnique("Default");
        
        [ForgeContextDefault(ForgeContext.RetentionGroup)]
        public static Tag ADDITIONAL => Tag.GenerateAsUnique("Bonus");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Classification Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeContextDefault(ForgeContext.Visibility)]
        public static Tag ALWAYS_VISIBLE => Tag.GenerateAsUnique("Always Visible");
        
        [ForgeContextDefault(ForgeContext.Visibility)]
        public static Tag NOT_VISIBLE => Tag.GenerateAsUnique("Not Visible");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Data Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        public static Tag GAS => Tag.GenerateAsUnique("GAS");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Mono Process Initialization Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        public static Tag PARENT_TRANSFORM => Tag.GenerateAsUnique("Transform");
        public static Tag POSITION => Tag.GenerateAsUnique("Position");
        public static Tag ROTATION => Tag.GenerateAsUnique("Rotation");
        
        public static Tag DERIVATION => Tag.GenerateAsUnique("Derivation");
        public static Tag AFFILIATION => Tag.GenerateAsUnique("Affiliation");
        public static Tag SOURCE => Tag.GenerateAsUnique("Source");
        
        public static Tag TARGET_REAL => Tag.GenerateAsUnique("Target");
        public static Tag TARGET_POS => Tag.GenerateAsUnique("TargetPosition");
        public static Tag DATA => Tag.GenerateAsUnique("Data");
        public static Tag INDEX => Tag.GenerateAsUnique("Index");
        public static Tag OBJECT => Tag.GenerateAsUnique("Object");
        public static Tag GAMEOBJECT => Tag.GenerateAsUnique("GameObject");
        public static Tag PROCESS => Tag.GenerateAsUnique("Process");
        public static Tag DELTA => Tag.GenerateAsUnique("Delta");
        public static Tag RADIUS => Tag.GenerateAsUnique("Radius");
        public static Tag DURATION => Tag.GenerateAsUnique("Duration");
        public static Tag DISTANCE => Tag.GenerateAsUnique("Distance");
        public static Tag CENTER => Tag.GenerateAsUnique("Center");
        public static Tag ORIGIN => Tag.GenerateAsUnique("Origin");
        public static Tag DIMENSIONS => Tag.GenerateAsUnique("Dimensions");
        public static Tag VECTOR => Tag.GenerateAsUnique("Vector");
        public static Tag VECTOR2 => Tag.GenerateAsUnique("Vector2");
        public static Tag VECTOR3 => Tag.GenerateAsUnique("Vector3");
        public static Tag HEIGHT => Tag.GenerateAsUnique("Height");
        public static Tag WAIT_FOR => Tag.GenerateAsUnique("WaitFor");
        public static Tag ITERATIONS => Tag.GenerateAsUnique("Iterations");
        public static Tag VALID => Tag.GenerateAsUnique("Valid");
        public static Tag INVALID => Tag.GenerateAsUnique("Invalid");
        public static Tag CAMERA => Tag.GenerateAsUnique("Camera");
        public static Tag PROCESS_RELAY => Tag.GenerateAsUnique("ProcessRelay");
        public static Tag CONTAINER => Tag.GenerateAsUnique("Container");
        public static Tag UI => Tag.GenerateAsUnique("UI");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Functional Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag TARGETED_INTENT => Tag.GenerateAsUnique("TargetedIntent");
        
        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag PROJECTILE_SPEED => Tag.GenerateAsUnique("ProjectileSpeed");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Report Tags
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag FAILED_TO_INITIALIZE => Tag.GenerateAsUnique("FailedToInitialize");
        
        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag FAILED_WHILE_ACTIVE => Tag.GenerateAsUnique("FailedWhileActive");
    }
}
