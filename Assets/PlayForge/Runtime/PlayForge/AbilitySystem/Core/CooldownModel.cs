namespace FarEmerald.PlayForge
{
    /// <summary>
    /// How an ability's uses come back: how many it holds, how many are in hand, and what a use spends. A capability an
    /// ability's component implements, as Charges does; with none, an ability has <see cref="SingleCooldown"/>, one use, back
    /// as its Cooldown ends. The Cooldown check asks it what's in hand, an ability spends a use through it as it commits,
    /// and a HUD reads it through <see cref="AbilitySpec.CooldownModel"/>, whatever the ability's components are.
    /// </summary>
    public interface ICooldownModel
    {
        /// <summary>The most uses the entity's ability holds at once.</summary>
        int Max(AbilitySpec spec);

        /// <summary>The uses in hand now; the Cooldown check refuses the ability with none.</summary>
        int InHand(AbilitySpec spec);

        /// <summary>A use spent, as the ability commits its usage: its cooldown started, or a charge spent.</summary>
        void Spend(AbilitySpec spec, ProcessDataPacket data);

        /// <summary>Why the Cooldown check refuses the ability with none in hand, in its words, as "It's cooling down".</summary>
        string NoneInHand(AbilitySpec spec);
    }

    /// <summary>
    /// The single cooldown, an ability's cooldown model where none of its components gives another: one use, spent as its
    /// Cooldown is applied, and back as the Cooldown ends, as its caster no longer carries the Cooldown's asset tag.
    /// </summary>
    public sealed class SingleCooldown : ICooldownModel
    {
        /// <summary>The one every ability with no model of its own follows.</summary>
        public static readonly SingleCooldown Instance = new();

        private SingleCooldown() { }

        /// <summary>One.</summary>
        public int Max(AbilitySpec spec) => 1;

        /// <summary>One, or none while its Cooldown is on its caster.</summary>
        public int InHand(AbilitySpec spec) => spec != null && spec.OnCooldown ? 0 : 1;

        /// <summary>Applies its Cooldown, with the activation's magnitudes.</summary>
        public void Spend(AbilitySpec spec, ProcessDataPacket data) => spec?.ApplyCooldown(data, ownContainer: false);

        public string NoneInHand(AbilitySpec spec) => "It's cooling down";
    }
}
