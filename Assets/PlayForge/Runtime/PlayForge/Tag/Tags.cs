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
        public static Tag SYSTEM => Tag.GenerateAsUnique("System");

        public static Tag GAME_ROOT => Tag.GenerateAsUnique("Game Root");
        
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
        // Mono Process Initialization Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeContextDefault(ForgeContext.Data)] public static Tag PARENT_TRANSFORM => Tag.GenerateAsUnique("Transform");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag POSITION => Tag.GenerateAsUnique("Position");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ROTATION => Tag.GenerateAsUnique("Rotation");
        
        [ForgeContextDefault(ForgeContext.Data)] public static Tag GAMEPLAY_ABILITY_SYSTEM => Tag.GenerateAsUnique("GameplayAbilitySystem");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DERIVATION => Tag.GenerateAsUnique("Derivation");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag AFFILIATION => Tag.GenerateAsUnique("Affiliation");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag SOURCE => Tag.GenerateAsUnique("Source");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag TARGET => Tag.GenerateAsUnique("Target");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag TARGET_REAL => Tag.GenerateAsUnique("TargetReal");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag TARGET_POS => Tag.GenerateAsUnique("TargetPosition");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DATA => Tag.GenerateAsUnique("Data");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ASSETS => Tag.GenerateAsUnique("Assets");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag EFFECTS => Tag.GenerateAsUnique("Effects");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ABILITIES => Tag.GenerateAsUnique("Abilities");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ENTITIES => Tag.GenerateAsUnique("Entities");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ATTRIBUTES => Tag.GenerateAsUnique("Attributes");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ATTRIBUTE_SETS => Tag.GenerateAsUnique("AttributeSets");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag TAGS => Tag.GenerateAsUnique("Tags");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag INDEX => Tag.GenerateAsUnique("Index");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag OBJECT => Tag.GenerateAsUnique("Object");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag GAMEOBJECT => Tag.GenerateAsUnique("GameObject");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag PROCESS => Tag.GenerateAsUnique("Process");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DELTA => Tag.GenerateAsUnique("Delta");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag RADIUS => Tag.GenerateAsUnique("Radius");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DURATION => Tag.GenerateAsUnique("Duration");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DISTANCE => Tag.GenerateAsUnique("Distance");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag CENTER => Tag.GenerateAsUnique("Center");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ORIGIN => Tag.GenerateAsUnique("Origin");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DIMENSIONS => Tag.GenerateAsUnique("Dimensions");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag VECTOR => Tag.GenerateAsUnique("Vector");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag VECTOR2 => Tag.GenerateAsUnique("Vector2");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag VECTOR3 => Tag.GenerateAsUnique("Vector3");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag HEIGHT => Tag.GenerateAsUnique("Height");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag WAIT_FOR => Tag.GenerateAsUnique("WaitFor");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ITERATIONS => Tag.GenerateAsUnique("Iterations");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag VALID => Tag.GenerateAsUnique("Valid");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag INVALID => Tag.GenerateAsUnique("Invalid");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag CAMERA => Tag.GenerateAsUnique("Camera");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag PROCESS_RELAY => Tag.GenerateAsUnique("ProcessRelay");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag CONTAINER => Tag.GenerateAsUnique("Container");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag UI => Tag.GenerateAsUnique("UI");
        
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
