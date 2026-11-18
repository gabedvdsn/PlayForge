namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of a targeting task that picks entities, as Select GAS Target and the Auto Target tasks do: its
    /// <c>Requirements</c>, tag queries a target must pass and tag queries it must not, decide which entities it takes,
    /// through <c>TargetIsValid</c>.
    /// </summary>
    public abstract class AbstractGasTargetingAbilityTask : AbstractTargetingAbilityTask
    {
        public AvoidRequireTagGroup Requirements = new();
        
        protected override bool TargetIsValid(ITarget target)
        {
            return Requirements.Validate(target.GetAppliedTags()) && base.TargetIsValid(target);
        }
    }
}
