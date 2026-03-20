namespace FarEmerald.PlayForge
{
    public interface IGameplayProcess : IHasReadableDefinition
    {
        public ProcessRelay ProcessRelay { get; }
    }
}
