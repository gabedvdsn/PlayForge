namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Why an activation was refused, or None where it wasn't: it activated, waits in the input buffer, or starts next
    /// frame. A refused activation said only false (FEAT-17).
    /// </summary>
    public enum EActivationRefusal
    {
        /// <summary>Not refused: it activated, waits in the input buffer, or starts next frame.</summary>
        None,

        /// <summary>The entity doesn't know the ability, or knows none that carries the tag.</summary>
        NotKnown,

        /// <summary>The ability system is off, as an ended entity's is.</summary>
        Disabled,

        /// <summary>The ability system is locked.</summary>
        Locked,

        /// <summary>ProcessControl takes no new processes, and an ability runs as one.</summary>
        NoNewProcesses,

        /// <summary>The ability's level is 0, and it's set to ignore activation there.</summary>
        LevelZero,

        /// <summary>The caster's tags fail the ability's source requirements, or its While active group.</summary>
        Requirements,

        /// <summary>The ability's cooldown is on.</summary>
        Cooldown,

        /// <summary>The ability's cost would take an attribute it spends below 0.</summary>
        Cost,

        /// <summary>The caster is dead.</summary>
        Dead,

        /// <summary>Another of the ability's caster checks refused it, which <see cref="ActivationRefusal.Rule"/> names.</summary>
        Rule,

        /// <summary>A running ability's stage blocks it (FEAT-5), and the request doesn't wait.</summary>
        Blocked,

        /// <summary>
        /// Another ability holds the claim and the request doesn't wait, or the ability's own activation still holds
        /// it, or the input buffer has no window to wait in.
        /// </summary>
        Busy,

        /// <summary>An Activate Anyway ability's critical section keeps another out.</summary>
        Critical,

        /// <summary>Its chance to activate didn't come up (TW-8).</summary>
        Chance,

        /// <summary>A crowd control rule its caster is under stops it, as a stun stops every ability (TW-16).</summary>
        CrowdControl,

        /// <summary>
        /// The target or the point it was handed lies beyond its Cast Range (FN-14), so an AI can walk closer and ask again.
        /// </summary>
        Range,
    }

    /// <summary>A refused activation: the ability, its slot, why, and for a caster check, which (FEAT-17).</summary>
    public readonly struct ActivationRefusal
    {
        /// <summary>The ability, or null where the entity knows none it was asked for.</summary>
        public readonly Ability Ability;

        /// <summary>The ability's slot, or -1 where there's none.</summary>
        public readonly int Index;

        public readonly EActivationRefusal Reason;

        /// <summary>
        /// The name of the caster check that refused it, for <see cref="EActivationRefusal.Rule"/>; or the tag of the
        /// crowd control rule, as Stunned, for <see cref="EActivationRefusal.CrowdControl"/> (TW-16).
        /// </summary>
        public readonly string Rule;

        public ActivationRefusal(Ability ability, int index, EActivationRefusal reason, string rule = null)
        {
            Ability = ability;
            Index = index;
            Reason = reason;
            Rule = rule;
        }

        /// <summary>Whether it was refused.</summary>
        public bool Refused => Reason != EActivationRefusal.None;

        public override string ToString()
        {
            string what = Ability != null ? Ability.GetName() : "An ability";
            return Rule != null ? $"{what}: {Reason} ({Rule})" : $"{what}: {Reason}";
        }
    }
}
