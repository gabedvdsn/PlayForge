using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What an ability system's callback is handed: the activation's <c>Data</c> and the ability it runs
    /// (<c>Ability</c>, its spec); for a stage's or a task's callback, the <c>Stage</c> and its <c>Tasks</c>,
    /// <c>Task</c> being the one a task's callback tells of; for an injection's, the <c>Injection</c>; and
    /// <c>Success</c>, which at an end says whether the task or the stage got there without being cancelled or failing,
    /// and for an injection whether it was taken.
    /// </summary>
    public struct AbilityCallbackStatus
    {
        public readonly AbilityDataPacket Data;
        public readonly List<AbstractAbilityTask> Tasks;
        public readonly AbilityTaskBehaviourStage Stage;
        public readonly ISequenceInjection Injection;
        public readonly bool Success;

        public AbilitySpec Ability => Data.EffectOrigin as AbilitySpec;
        public AbstractAbilityTask Task => Tasks?.Count > 0 ? Tasks[0] : null;

        private AbilityCallbackStatus(AbilityDataPacket data, List<AbstractAbilityTask> tasks, AbilityTaskBehaviourStage stage, ISequenceInjection injection, bool success)
        {
            Data = data;
            Tasks = tasks;
            Stage = stage;
            Injection = injection;
            Success = success;
        }

        /// <summary>
        /// Task begins or ends
        /// </summary>
        /// <param name="data">Associated ability data</param>
        /// <param name="task">Task begun/ended</param>
        /// <param name="stage">Stage holding task</param>
        /// <param name="completed">Whether the task was begun/ended without cancellation/error</param>
        /// <returns></returns>
        public static AbilityCallbackStatus GenerateForTask(AbilityDataPacket data, AbstractAbilityTask task, AbilityTaskBehaviourStage stage, bool completed)
        {
            return new AbilityCallbackStatus(data, new List<AbstractAbilityTask>(){ task }, stage, null, completed);
        }

        /// <summary>
        /// Stage begins or ends
        /// </summary>
        /// <param name="data">Associated ability data</param>
        /// <param name="stage">Stage begun/ended</param>
        /// <param name="success">Whether the stage was begun/ended without cancellation/error</param>
        /// <returns></returns>
        public static AbilityCallbackStatus GenerateForStageEvent(AbilityDataPacket data, AbilityTaskBehaviourStage stage, bool success)
        {
            return new AbilityCallbackStatus(data, stage.Tasks, stage, null, success);
        }
        
        /// <summary>
        /// Ability begins or ends
        /// </summary>
        /// <param name="data">Associated ability data</param>
        /// <returns></returns>
        public static AbilityCallbackStatus GenerateForAbilityEvent(AbilityDataPacket data)
        {
            return new AbilityCallbackStatus(data, null, null, null, true);
        }
        
        /// <summary>
        /// Injection is applied
        /// </summary>
        /// <param name="data">Associated ability data</param>
        /// <param name="stage">Active stage injected at (does NOT return maintained stages, even if active task runtime is ended and is waiting on maintained stages)</param>
        /// <param name="injection">The injection</param>
        /// <param name="injectionSuccessful">Whether the injection was successful</param>
        /// <returns></returns>
        public static AbilityCallbackStatus GenerateForInjection(AbilityDataPacket data, AbilityTaskBehaviourStage stage, ISequenceInjection injection, bool injectionSuccessful)
        {
            return new AbilityCallbackStatus(data, stage.Tasks, stage, injection, injectionSuccessful);
        }
    }
}
