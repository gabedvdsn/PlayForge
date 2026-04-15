namespace FarEmerald.PlayForge
{
    public abstract class AbstractGasTargetingAbilityTask : AbstractTargetingAbilityTask
    {
        public AvoidRequireTagGroup Requirements = new();
        
        protected override bool TargetIsValid(ITarget target)
        {
            return Requirements.Validate(target.GetAppliedTags()) && base.TargetIsValid(target);
        }
    }
}
