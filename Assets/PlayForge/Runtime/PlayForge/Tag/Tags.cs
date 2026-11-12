using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Extended;

namespace FarEmerald.PlayForge
{
    public static partial class Tags
    {
        // Each is made once, as the class first loads. Made on every read, each took two locks and two lookups of its
        // 64-character name, and a recompute reads several (GAS-P4)
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Special Tags
        // ═══════════════════════════════════════════════════════════════════════════

        [ForgeContextDefault(ForgeContext.System, ForgeContext.Affiliation, ForgeContext.Debug)]
        public static Tag SYSTEM { get; } = Tag.GenerateAsUnique("System");

        public static Tag GAME_ROOT { get; } = Tag.GenerateAsUnique("Game Root");
        
        public static Tag NONE { get; } = Tag.GenerateAsUnique("None");
        
        [ForgeContextDefault(ForgeContext.Impact)]
        public static Tag ALLOW { get; } = Tag.GenerateAsUnique("Always Allow");

        [ForgeContextDefault(ForgeContext.Impact)]
        public static Tag DISALLOW { get; } = Tag.GenerateAsUnique("Never Allow");
        
        [ForgeContextDefault(ForgeContext.Debug, ForgeContext.Texture)]
        public static Tag DEBUG { get; } = Tag.GenerateAsUnique("Debug");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Base Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeContextDefault(ForgeContext.RetentionGroup)]
        public static Tag IGNORE { get; } = Tag.GenerateAsUnique("Ignore");
        
        [ForgeContextDefault(ForgeContext.Texture)]
        public static Tag PRIMARY { get; } = Tag.GenerateAsUnique("Primary");
        
        [ForgeContextDefault(ForgeContext.RetentionGroup)]
        public static Tag DEFAULT { get; } = Tag.GenerateAsUnique("Default");
        
        [ForgeContextDefault(ForgeContext.RetentionGroup)]
        public static Tag ADDITIONAL { get; } = Tag.GenerateAsUnique("Bonus");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Classification Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeContextDefault(ForgeContext.Visibility)]
        public static Tag ALWAYS_VISIBLE { get; } = Tag.GenerateAsUnique("Always Visible");
        
        [ForgeContextDefault(ForgeContext.Visibility)]
        public static Tag NOT_VISIBLE { get; } = Tag.GenerateAsUnique("Not Visible");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Mono Process Initialization Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeContextDefault(ForgeContext.Data)] public static Tag PARENT_TRANSFORM { get; } = Tag.GenerateAsUnique("Transform");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag POSITION { get; } = Tag.GenerateAsUnique("Position");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ROTATION { get; } = Tag.GenerateAsUnique("Rotation");
        
        [ForgeContextDefault(ForgeContext.Data)] public static Tag GAMEPLAY_ABILITY_SYSTEM { get; } = Tag.GenerateAsUnique("GameplayAbilitySystem");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DERIVATION { get; } = Tag.GenerateAsUnique("Derivation");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag AFFILIATION { get; } = Tag.GenerateAsUnique("Affiliation");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag SOURCE { get; } = Tag.GenerateAsUnique("Source");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag TARGET { get; } = Tag.GenerateAsUnique("Target");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag TARGET_REAL { get; } = Tag.GenerateAsUnique("TargetReal");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag TARGET_POS { get; } = Tag.GenerateAsUnique("TargetPosition");

        /// <summary>A core event's subject: the one hit, the one killed, or the entity itself (TW-5).</summary>
        [ForgeContextDefault(ForgeContext.Data)] public static Tag SUBJECT { get; } = Tag.GenerateAsUnique("EventSubject");

        /// <summary>An impact event's impact, an <see cref="ImpactData"/> (TW-5).</summary>
        [ForgeContextDefault(ForgeContext.Data)] public static Tag IMPACT { get; } = Tag.GenerateAsUnique("EventImpact");

        // ─── The gameplay events the core sends, where an ability listens (TW-5) ───

        /// <summary>To the dealer, as an impact it dealt lands on another entity.</summary>
        [ForgeContextDefault(ForgeContext.Trigger)] public static Tag IMPACT_DEALT { get; } = Tag.GenerateAsUnique("Impact Dealt");

        /// <summary>To the one hit, as an impact another entity dealt lands on it.</summary>
        [ForgeContextDefault(ForgeContext.Trigger)] public static Tag IMPACT_TAKEN { get; } = Tag.GenerateAsUnique("Impact Taken");

        /// <summary>To the killer, as an entity it dealt the last blow to dies.</summary>
        [ForgeContextDefault(ForgeContext.Trigger)] public static Tag KILL { get; } = Tag.GenerateAsUnique("Kill");

        /// <summary>To the one who died, with its killer, if another, as Source.</summary>
        [ForgeContextDefault(ForgeContext.Trigger)] public static Tag DIED { get; } = Tag.GenerateAsUnique("Died");

        /// <summary>To a staying entity as it revives.</summary>
        [ForgeContextDefault(ForgeContext.Trigger)] public static Tag REVIVED { get; } = Tag.GenerateAsUnique("Revived");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DATA { get; } = Tag.GenerateAsUnique("Data");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ASSETS { get; } = Tag.GenerateAsUnique("Assets");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag EFFECTS { get; } = Tag.GenerateAsUnique("Effects");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ABILITIES { get; } = Tag.GenerateAsUnique("Abilities");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ENTITIES { get; } = Tag.GenerateAsUnique("Entities");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ATTRIBUTES { get; } = Tag.GenerateAsUnique("Attributes");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ATTRIBUTE_SETS { get; } = Tag.GenerateAsUnique("AttributeSets");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag TAGS { get; } = Tag.GenerateAsUnique("Tags");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag INDEX { get; } = Tag.GenerateAsUnique("Index");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag OBJECT { get; } = Tag.GenerateAsUnique("Object");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag GAMEOBJECT { get; } = Tag.GenerateAsUnique("GameObject");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag PROCESS { get; } = Tag.GenerateAsUnique("Process");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DELTA { get; } = Tag.GenerateAsUnique("Delta");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag RADIUS { get; } = Tag.GenerateAsUnique("Radius");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DURATION { get; } = Tag.GenerateAsUnique("Duration");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DISTANCE { get; } = Tag.GenerateAsUnique("Distance");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag CENTER { get; } = Tag.GenerateAsUnique("Center");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ORIGIN { get; } = Tag.GenerateAsUnique("Origin");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag DIMENSIONS { get; } = Tag.GenerateAsUnique("Dimensions");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag VECTOR { get; } = Tag.GenerateAsUnique("Vector");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag VECTOR2 { get; } = Tag.GenerateAsUnique("Vector2");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag VECTOR3 { get; } = Tag.GenerateAsUnique("Vector3");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag HEIGHT { get; } = Tag.GenerateAsUnique("Height");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag WAIT_FOR { get; } = Tag.GenerateAsUnique("WaitFor");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag ITERATIONS { get; } = Tag.GenerateAsUnique("Iterations");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag VALID { get; } = Tag.GenerateAsUnique("Valid");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag INVALID { get; } = Tag.GenerateAsUnique("Invalid");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag CAMERA { get; } = Tag.GenerateAsUnique("Camera");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag PROCESS_RELAY { get; } = Tag.GenerateAsUnique("ProcessRelay");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag CONTAINER { get; } = Tag.GenerateAsUnique("Container");
        [ForgeContextDefault(ForgeContext.Data)] public static Tag UI { get; } = Tag.GenerateAsUnique("UI");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Functional Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag TARGETED_INTENT { get; } = Tag.GenerateAsUnique("TargetedIntent");
        
        /// <summary>The role a projectile reads its speed by, among the attributes it carries or its origin's (FEAT-24)</summary>
        [ForgeContextDefault(ForgeContext.Data)] public static Tag PROJECTILE_SPEED { get; } = Tag.GenerateAsUnique("ProjectileSpeed");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Report Tags
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag FAILED_TO_INITIALIZE { get; } = Tag.GenerateAsUnique("FailedToInitialize");
        
        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        public static Tag FAILED_WHILE_ACTIVE { get; } = Tag.GenerateAsUnique("FailedWhileActive");
    }
}
