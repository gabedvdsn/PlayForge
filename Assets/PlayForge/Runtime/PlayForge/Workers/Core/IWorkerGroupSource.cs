namespace FarEmerald.PlayForge
{
    public interface IWorkerGroupSource
    {
        public void InitWorkers(ISource system);
        public void RemoveWorkers(ISource system);
    }
}
