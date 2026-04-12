namespace FarEmerald.PlayForge
{
    public interface IHasIntentToTarget
    {
        public void SetTarget(ITarget _target, AbstractTargetingPacket targeting);
    }
}
