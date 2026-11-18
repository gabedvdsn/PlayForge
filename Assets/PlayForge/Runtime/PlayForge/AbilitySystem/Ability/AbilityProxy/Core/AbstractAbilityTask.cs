using System;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of a task in an ability's stage, as Apply Effects, Delay or Run Sequence: a stage starts its tasks
    /// together and ends as its Completion says, as its last task's <c>Activate</c> ends or its first's. A task of your
    /// own derives from it and overrides <c>Activate</c>, the work, and <c>IsCriticalSection</c>, with <c>Prepare</c>
    /// and <c>Clean</c> for what it does as its stage starts and ends; <c>[ForgeMenu]</c> files it in the stage's task
    /// picker. What it keeps for one activation goes in <c>data.GetTaskState&lt;T&gt;(this)</c>, as one task serves
    /// every activation of its ability.
    /// </summary>
    [Serializable]
    public abstract class AbstractAbilityTask : AbstractAbilityRelatedTask
    {
        public virtual string Description => null;
    }
}
