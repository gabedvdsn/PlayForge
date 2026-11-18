namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What can be aimed at a target and turned to another, as a homing projectile, <c>AbstractTargetedMonoProcess</c>,
    /// is: <c>SetTarget</c> gives it the target and its targeting packet. A disjoint turns the shots aimed at an
    /// entity, through it, to where the entity stood.
    /// </summary>
    public interface IHasIntentToTarget
    {
        public void SetTarget(ITarget _target, AbstractTargetingPacket _targeting);
    }
}
