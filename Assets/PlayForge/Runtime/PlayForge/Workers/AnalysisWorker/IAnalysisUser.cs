namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What holds analysis workers: <c>GetAnalysisCache</c> gives the cache a worker group gives its analysis workers
    /// to. Every GAS hub is one.
    /// </summary>
    public interface IAnalysisUser
    {
        public AnalysisWorkerCache GetAnalysisCache();
    }
}
