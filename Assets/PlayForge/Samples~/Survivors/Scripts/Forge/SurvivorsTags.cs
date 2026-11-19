namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>
    /// The Survivors demo's own tags: what a volley hands the processes it spawns, the Local Data its abilities keep, and
    /// the roles its processes read their numbers by.
    /// </summary>
    public static class SurvivorsTags
    {
        /// <summary>The direction a spawned process flies, a Vector3 in its registration data.</summary>
        [ForgeTagExposed(ForgeContext.DataKey)] public static readonly Tag Aim = Tag.Generate("Survivors.Aim");

        /// <summary>Where an orbiting process starts around its caster, in degrees, in its registration data.</summary>
        [ForgeTagExposed(ForgeContext.DataKey)] public static readonly Tag Phase = Tag.Generate("Survivors.Phase");

        /// <summary>The Local Data key the process an ability's volley spawns is under.</summary>
        [ForgeTagExposed(ForgeContext.LocalDataKey)] public static readonly Tag Projectile = Tag.Generate("Survivors.Projectile");

        /// <summary>The Local Data key how many a volley spawns is under, a number that grows with the ability's level.</summary>
        [ForgeTagExposed(ForgeContext.LocalDataKey)] public static readonly Tag Count = Tag.Generate("Survivors.Count");

        /// <summary>The Local Data key how a volley aims is under: Targets, Fan, Ring, Spots or Orbit.</summary>
        [ForgeTagExposed(ForgeContext.LocalDataKey)] public static readonly Tag Pattern = Tag.Generate("Survivors.Pattern");

        /// <summary>The Local Data key a fan's width, in degrees, is under.</summary>
        [ForgeTagExposed(ForgeContext.LocalDataKey)] public static readonly Tag Spread = Tag.Generate("Survivors.Spread");

        /// <summary>The Local Data key the seconds between a volley's shots are under; 0 looses them at once.</summary>
        [ForgeTagExposed(ForgeContext.LocalDataKey)] public static readonly Tag Interval = Tag.Generate("Survivors.Interval");

        /// <summary>The Local Data key the entity a summoning raises is under.</summary>
        [ForgeTagExposed(ForgeContext.LocalDataKey)] public static readonly Tag Summons = Tag.Generate("Survivors.Summons");

        /// <summary>The role a process's radius is read by: an Area on its caster multiplies it.</summary>
        public static readonly Tag Area = Tag.Generate("Survivors.Role.Area");

        /// <summary>The role a lasting process's time is read by: a Duration on its caster multiplies it.</summary>
        public static readonly Tag Duration = Tag.Generate("Survivors.Role.Duration");

        /// <summary>The role the enemies a projectile passes through are read by, past its own.</summary>
        public static readonly Tag Pierce = Tag.Generate("Survivors.Role.Pierce");
    }
}
