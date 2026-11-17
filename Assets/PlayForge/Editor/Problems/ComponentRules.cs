using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// Charges that never run out: built-in charges, counted, with no Cooldown to bring them back. A recharge of one's own
    /// decides for itself. It took the place of a rule of a cost or a cooldown no stage applied, as an ability applies
    /// them as it starts.
    /// </summary>
    internal sealed class ChargesRule : ForgeComponentRule<ChargesAbilityComponent>
    {
        public override string Name => "Charges that never run out";

        protected override void Check(Object asset, ChargesAbilityComponent component, int nth, string path, List<ForgeProblem> problems)
        {
            if (asset is not Ability ability || NeverRunOut(ability, component) is not { } message) return;
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, message, nameof(Ability.Components)));
        }

        // Each Charge and All At Once, exactly: a recharge of one's own, even one made from them, decides for itself
        private static bool BuiltIn(AbstractChargeRecharge recharge) =>
            recharge == null || recharge.GetType() == typeof(EachChargeRecharge) || recharge.GetType() == typeof(AllAtOnceRecharge);

        // Why its charges never run out, or null where they do or aren't counted
        private static string NeverRunOut(Ability ability, ChargesAbilityComponent charges)
        {
            if (ability.Cooldown != null || !BuiltIn(charges.Recharge) || !charges.CountsCharges) return null;
            return "Its charges never run out, as it has no Cooldown to bring them back. Give it a Cooldown, or a recharge of " +
                   "your own.";
        }
    }

    /// <summary>An ability that toggles an Instant effect, which is never on to turn off.</summary>
    internal sealed class ToggleRule : ForgeComponentRule<ToggleAbilityComponent>
    {
        public override string Name => "Toggles that never turn off";

        protected override void Check(Object asset, ToggleAbilityComponent component, int nth, string path, List<ForgeProblem> problems)
        {
            if (asset is not Ability || component.Effect == null
                || component.Effect.DurationSpecification?.DurationPolicy != EEffectDurationPolicy.Instant) return;
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                "It toggles an Instant effect, which is never on to turn off. Toggle a lasting one.", nameof(Ability.Components)));
        }
    }

    /// <summary>An item whose Uses never counts, as it lends no ability to use.</summary>
    internal sealed class UsesRule : ForgeComponentRule<UsesItemComponent>
    {
        public override string Name => "Uses that never count";

        protected override void Check(Object asset, UsesItemComponent component, int nth, string path, List<ForgeProblem> problems)
        {
            if (nth > 1 || asset is not Item item || item.ActiveAbility != null) return;
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                "Its Uses never count: it has no Active Ability to use. Give it one.", path));
        }
    }

    /// <summary>An entity whose Respawn never revives it, as it ends as it dies.</summary>
    internal sealed class RespawnRule : ForgeComponentRule<RespawnEntityComponent>
    {
        public override string Name => "Respawns that never revive";

        protected override void Check(Object asset, RespawnEntityComponent component, int nth, string path, List<ForgeProblem> problems)
        {
            if (nth > 1 || asset is not EntityIdentity entity || EntityDeath.Stays(entity.Death)) return;
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning,
                "Its Respawn never revives it: it ends as it dies. Set its Death to stay.", path));
        }
    }

    /// <summary>
    /// A transfer that never moves as it's set: one whose delivery its effect's duration can't make, as an additional
    /// impact's, or one that names no attribute to give or to get. Its transfers are numbered among themselves.
    /// </summary>
    internal sealed class TransfersRule : ForgeComponentRule<TransferEffectComponent>
    {
        public override string Name => "Transfers that never move";

        protected override void Check(Object asset, TransferEffectComponent component, int nth, string path, List<ForgeProblem> problems)
        {
            if (asset is not GameplayEffect effect || component.Transfer is not { } transfer) return;
            var problem = !transfer.FromAttribute || !transfer.ToAttribute
                ? "It names no attribute to give or to get, so it moves nothing."
                : effect.DurationSpecification != null ? ImpactFields.DeliveryProblem(effect.DurationSpecification, transfer.Delivery) : null;
            if (problem == null) return;
            problems.Add(new ForgeProblem(asset, ForgeProblemSeverity.Warning, $"Transfer {nth}: {problem}",
                $"{path}.{nameof(TransferEffectComponent.Transfer)}"));
        }
    }
}
