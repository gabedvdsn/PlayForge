namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Why an activation was refused, or None where it wasn't: it activated, waits in the input buffer, or starts next
    /// frame. A refused activation said only false.
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

        /// <summary>A running ability's stage blocks it, and the request doesn't wait.</summary>
        Blocked,

        /// <summary>
        /// Another ability holds the claim and the request doesn't wait, or the ability's own activation still holds
        /// it, or the input buffer has no window to wait in.
        /// </summary>
        Busy,

        /// <summary>
        /// Another ability's critical section holds its claim, and this ability has one too, so it's kept out, whatever
        /// policy either was asked under. Under Queue, it waits in the input buffer instead.
        /// </summary>
        Critical,

        /// <summary>Its chance to activate didn't come up.</summary>
        Chance,

        /// <summary>A crowd control rule its caster is under stops it, as a stun stops every ability.</summary>
        CrowdControl,

        /// <summary>
        /// The target or the point it was handed lies beyond its Cast Range, so an AI can walk closer and ask again.
        /// </summary>
        Range,

        /// <summary>
        /// After its targeting, its target failed one of its Target Checks, which <see cref="ActivationRefusal.Rule"/>
        /// names, or its target tag requirements, where it names none. The activation had started, and ends there with
        /// nothing of it paid; the ability system tells it as it tells any refusal.
        /// </summary>
        Target,
    }

    /// <summary>
    /// A refused activation: the ability, its slot, why, for a check, which, and in the check's own words, why.
    /// </summary>
    public readonly struct ActivationRefusal
    {
        /// <summary>The ability, or null where the entity knows none it was asked for.</summary>
        public readonly Ability Ability;

        /// <summary>The ability's slot, or -1 where there's none.</summary>
        public readonly int Index;

        public readonly EActivationRefusal Reason;

        /// <summary>
        /// The name of the check that refused it, for <see cref="EActivationRefusal.Rule"/> or
        /// <see cref="EActivationRefusal.Target"/>; the reason a component that refused it gave; or the tag of the crowd
        /// control rule, as Stunned, for <see cref="EActivationRefusal.CrowdControl"/>.
        /// </summary>
        public readonly string Rule;

        /// <summary>
        /// Why, in the words of the check that refused it, as "Its caster's Mana, 10, can't pay 50": a validation rule's
        /// error, or a component's reason; null where no check refused it, as for a block or the system off.
        /// </summary>
        public readonly string Detail;

        public ActivationRefusal(Ability ability, int index, EActivationRefusal reason, string rule = null, string detail = null)
        {
            Ability = ability;
            Index = index;
            Reason = reason;
            Rule = rule;
            Detail = detail;
        }

        /// <summary>Whether it was refused.</summary>
        public bool Refused => Reason != EActivationRefusal.None;

        public override string ToString()
        {
            string what = Ability != null ? Ability.GetName() : "An ability";
            string said = Rule != null ? $"{what}: {Reason} ({Rule})" : $"{what}: {Reason}";
            return string.IsNullOrEmpty(Detail) || Detail == Rule ? said : $"{said}: {Detail}";
        }
    }
}
