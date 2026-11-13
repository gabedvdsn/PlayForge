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

        [ForgeTagExposed(ForgeContext.Affiliation)]
        public static Tag SYSTEM { get; } = Tag.GenerateAsUnique("System");

        [ForgeTagExposed] public static Tag GAME_ROOT { get; } = Tag.GenerateAsUnique("Game Root");
        
        [ForgeTagExposed] public static Tag NONE { get; } = Tag.GenerateAsUnique("None");
        
        [ForgeTagExposed(ForgeContext.ImpactType)]
        public static Tag ALLOW { get; } = Tag.GenerateAsUnique("Always Allow");

        [ForgeTagExposed(ForgeContext.ImpactType)]
        public static Tag DISALLOW { get; } = Tag.GenerateAsUnique("Never Allow");

        /// <summary>The type of damage a return sends back, so no return returns it again and no lifesteal heals from it (TW-15).</summary>
        [ForgeTagExposed(ForgeContext.ImpactType)]
        public static Tag RETURNED { get; } = Tag.GenerateAsUnique("Returned");
        
        [ForgeTagExposed]
        public static Tag DEBUG { get; } = Tag.GenerateAsUnique("Debug");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Base Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeTagExposed(ForgeContext.RetentionGroup)]
        public static Tag IGNORE { get; } = Tag.GenerateAsUnique("Ignore");
        
        [ForgeTagExposed(ForgeContext.TextureSlot)]
        public static Tag PRIMARY { get; } = Tag.GenerateAsUnique("Primary");
        
        [ForgeTagExposed(ForgeContext.RetentionGroup)]
        public static Tag DEFAULT { get; } = Tag.GenerateAsUnique("Default");
        
        [ForgeTagExposed(ForgeContext.RetentionGroup)]
        public static Tag ADDITIONAL { get; } = Tag.GenerateAsUnique("Bonus");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Classification Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeTagExposed(ForgeContext.Visibility)]
        public static Tag ALWAYS_VISIBLE { get; } = Tag.GenerateAsUnique("Always Visible");
        
        [ForgeTagExposed(ForgeContext.Visibility)]
        public static Tag NOT_VISIBLE { get; } = Tag.GenerateAsUnique("Not Visible");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Mono Process Initialization Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag PARENT_TRANSFORM { get; } = Tag.GenerateAsUnique("Transform");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag POSITION { get; } = Tag.GenerateAsUnique("Position");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag ROTATION { get; } = Tag.GenerateAsUnique("Rotation");
        
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag GAMEPLAY_ABILITY_SYSTEM { get; } = Tag.GenerateAsUnique("GameplayAbilitySystem");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag DERIVATION { get; } = Tag.GenerateAsUnique("Derivation");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag AFFILIATION { get; } = Tag.GenerateAsUnique("Affiliation");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag SOURCE { get; } = Tag.GenerateAsUnique("Source");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag TARGET { get; } = Tag.GenerateAsUnique("Target");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag TARGET_REAL { get; } = Tag.GenerateAsUnique("TargetReal");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag TARGET_POS { get; } = Tag.GenerateAsUnique("TargetPosition");

        /// <summary>A core event's subject: the one hit, the one killed, or the entity itself (TW-5).</summary>
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag SUBJECT { get; } = Tag.GenerateAsUnique("EventSubject");

        /// <summary>An impact event's impact, an <see cref="ImpactData"/> (TW-5).</summary>
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag IMPACT { get; } = Tag.GenerateAsUnique("EventImpact");

        // ─── The roles PlayForge's features read, each filled by a project's attribute (TW-29) ───

        /// <summary>
        /// The attribute an entity dies of as it reaches 0, where no death watcher of its own watches; which On Hit counts,
        /// lifesteal heals and Execute reads where they name none; and whose loss names the killer.
        /// </summary>
        [ForgeTagExposed(ForgeContext.AttributeRole)] public static Tag ROLE_HEALTH { get; } = Tag.GenerateAsUnique("Health");

        /// <summary>The attribute read on the caster of every chance that gives no luck of its own.</summary>
        [ForgeTagExposed(ForgeContext.AttributeRole)] public static Tag ROLE_LUCK { get; } = Tag.GenerateAsUnique("Luck");

        /// <summary>The attribute the Creator Lab's units walk at, in units a second, where its rules name none.</summary>
        [ForgeTagExposed(ForgeContext.AttributeRole)] public static Tag ROLE_MOVEMENT_SPEED { get; } = Tag.GenerateAsUnique("Movement Speed");

        // ─── The gameplay events the core sends, where an ability listens (TW-5) ───

        /// <summary>To the dealer, as an impact it dealt lands on another entity.</summary>
        [ForgeTagExposed(ForgeContext.Event)] public static Tag IMPACT_DEALT { get; } = Tag.GenerateAsUnique("Impact Dealt");

        /// <summary>To the one hit, as an impact another entity dealt lands on it.</summary>
        [ForgeTagExposed(ForgeContext.Event)] public static Tag IMPACT_TAKEN { get; } = Tag.GenerateAsUnique("Impact Taken");

        /// <summary>To the killer, as an entity it dealt the last blow to dies.</summary>
        [ForgeTagExposed(ForgeContext.Event)] public static Tag KILL { get; } = Tag.GenerateAsUnique("Kill");

        /// <summary>To the one who died, with its killer, if another, as Source.</summary>
        [ForgeTagExposed(ForgeContext.Event)] public static Tag DIED { get; } = Tag.GenerateAsUnique("Died");

        /// <summary>To a staying entity as it revives.</summary>
        [ForgeTagExposed(ForgeContext.Event)] public static Tag REVIVED { get; } = Tag.GenerateAsUnique("Revived");

        /// <summary>To the one hit as it blocks an effect, its dealer as Source (TW-12).</summary>
        [ForgeTagExposed(ForgeContext.Event)] public static Tag BLOCKED { get; } = Tag.GenerateAsUnique("Blocked");

        /// <summary>To the one hit as it evades an effect, its dealer as Source (TW-12).</summary>
        [ForgeTagExposed(ForgeContext.Event)] public static Tag EVADED { get; } = Tag.GenerateAsUnique("Evaded");

        /// <summary>To the dealer as the one it hit blocks its effect, that one as Subject (TW-12).</summary>
        [ForgeTagExposed(ForgeContext.Event)] public static Tag HIT_BLOCKED { get; } = Tag.GenerateAsUnique("Hit Blocked");

        /// <summary>To the dealer as the one it hit evades its effect, that one as Subject (TW-12).</summary>
        [ForgeTagExposed(ForgeContext.Event)] public static Tag HIT_EVADED { get; } = Tag.GenerateAsUnique("Hit Evaded");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag DATA { get; } = Tag.GenerateAsUnique("Data");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag ASSETS { get; } = Tag.GenerateAsUnique("Assets");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag EFFECTS { get; } = Tag.GenerateAsUnique("Effects");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag ABILITIES { get; } = Tag.GenerateAsUnique("Abilities");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag ENTITIES { get; } = Tag.GenerateAsUnique("Entities");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag ATTRIBUTES { get; } = Tag.GenerateAsUnique("Attributes");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag ATTRIBUTE_SETS { get; } = Tag.GenerateAsUnique("AttributeSets");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag TAGS { get; } = Tag.GenerateAsUnique("Tags");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag INDEX { get; } = Tag.GenerateAsUnique("Index");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag OBJECT { get; } = Tag.GenerateAsUnique("Object");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag GAMEOBJECT { get; } = Tag.GenerateAsUnique("GameObject");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag PROCESS { get; } = Tag.GenerateAsUnique("Process");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag DELTA { get; } = Tag.GenerateAsUnique("Delta");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag RADIUS { get; } = Tag.GenerateAsUnique("Radius");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag DURATION { get; } = Tag.GenerateAsUnique("Duration");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag DISTANCE { get; } = Tag.GenerateAsUnique("Distance");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag CENTER { get; } = Tag.GenerateAsUnique("Center");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag ORIGIN { get; } = Tag.GenerateAsUnique("Origin");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag DIMENSIONS { get; } = Tag.GenerateAsUnique("Dimensions");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag VECTOR { get; } = Tag.GenerateAsUnique("Vector");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag VECTOR2 { get; } = Tag.GenerateAsUnique("Vector2");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag VECTOR3 { get; } = Tag.GenerateAsUnique("Vector3");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag HEIGHT { get; } = Tag.GenerateAsUnique("Height");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag WAIT_FOR { get; } = Tag.GenerateAsUnique("WaitFor");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag ITERATIONS { get; } = Tag.GenerateAsUnique("Iterations");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag VALID { get; } = Tag.GenerateAsUnique("Valid");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag INVALID { get; } = Tag.GenerateAsUnique("Invalid");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag CAMERA { get; } = Tag.GenerateAsUnique("Camera");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag PROCESS_RELAY { get; } = Tag.GenerateAsUnique("ProcessRelay");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag CONTAINER { get; } = Tag.GenerateAsUnique("Container");
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag UI { get; } = Tag.GenerateAsUnique("UI");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Functional Tags
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Used to communicate the act of targeting to the targeted entity</summary>
        [ForgeTagExposed(ForgeContext.DataKey)] public static Tag TARGETED_INTENT { get; } = Tag.GenerateAsUnique("TargetedIntent");
        
        /// <summary>The role a projectile reads its speed by, among the attributes it carries or its origin's (FEAT-24)</summary>
        [ForgeTagExposed(ForgeContext.SetByCallerKey)] public static Tag PROJECTILE_SPEED { get; } = Tag.GenerateAsUnique("ProjectileSpeed");
        
        // ═══════════════════════════════════════════════════════════════════════════
        // Report Tags
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A process's status as it failed as it began (TAG-5)</summary>
        [ForgeTagExposed] public static Tag FAILED_TO_INITIALIZE { get; } = Tag.GenerateAsUnique("FailedToInitialize");

        /// <summary>A process's status as it failed while it ran (TAG-5)</summary>
        [ForgeTagExposed] public static Tag FAILED_WHILE_ACTIVE { get; } = Tag.GenerateAsUnique("FailedWhileActive");
    }
}
