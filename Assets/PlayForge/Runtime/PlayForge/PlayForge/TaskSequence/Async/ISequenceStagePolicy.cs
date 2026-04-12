using System.Threading;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Determines how a stage completes based on its tasks.
    /// </summary>
    public interface ISequenceStagePolicy
    {
        /// <summary>
        /// Awaits completion of the stage's tasks according to policy rules.
        /// </summary>
        /// <param name="runtime">The sequence runtime</param>
        /// <param name="stage">The stage being executed</param>
        /// <param name="taskUnits">The executing task UniTasks</param>
        /// <param name="stageCts">Cancellation source for this stage</param>
        UniTask AwaitCompletion(
            TaskSequenceRuntime runtime, 
            SequenceStage stage, 
            UniTask[] taskUnits, 
            CancellationTokenSource stageCts);
    }
}
