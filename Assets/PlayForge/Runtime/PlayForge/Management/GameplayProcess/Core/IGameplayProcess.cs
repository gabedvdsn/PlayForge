namespace FarEmerald.PlayForge
{
    public interface IGameplayProcess : IGameplayProcessHandler
    {
        public ProcessRelay ProcessRelay { get; }
    }
}
