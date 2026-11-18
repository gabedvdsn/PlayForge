namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What gives an entity workers through its worker group: <c>InitWorkers</c> gives them, and <c>RemoveWorkers</c>
    /// takes them back. An entity's identity and its attribute set give theirs as it initializes, an ability while the
    /// entity has it, and an item while it's equipped.
    /// </summary>
    public interface IWorkerGroupSource
    {
        public void InitWorkers(ISource system);
        public void RemoveWorkers(ISource system);
    }
}
