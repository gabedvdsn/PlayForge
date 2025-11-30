using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public struct AbilityCallbackStatus
    {
        public AbilityDataPacket Data;
        public List<AbstractProxyTask> Tasks;
        public AbilityProxyStage Stage;
        public IAbilityInjection Injection;
        public bool Success;

        public AbilitySpec Ability => Data.Spec as AbilitySpec;
        public float TimeElapsed => TimeUtility.Get(Ability.Base.Tags.AssetTag, out float time) ? time : -1f;
        public AbstractProxyTask Task => Tasks?.Count > 0 ? Tasks[0] : null;

        private AbilityCallbackStatus(AbilityDataPacket data, List<AbstractProxyTask> tasks, AbilityProxyStage stage, IAbilityInjection injection, bool success)
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
        public static AbilityCallbackStatus GenerateForTask(AbilityDataPacket data, AbstractProxyTask task, AbilityProxyStage stage, bool completed)
        {
            return new AbilityCallbackStatus(data, new List<AbstractProxyTask>(){ task }, stage, null, completed);
        }

        /// <summary>
        /// Stage begins or ends
        /// </summary>
        /// <param name="data">Associated ability data</param>
        /// <param name="stage">Stage begun/ended</param>
        /// <param name="success">Whether the stage was begun/ended without cancellation/error</param>
        /// <returns></returns>
        public static AbilityCallbackStatus GenerateForStageEvent(AbilityDataPacket data, AbilityProxyStage stage, bool success)
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
        public static AbilityCallbackStatus GenerateForInjection(AbilityDataPacket data, AbilityProxyStage stage, IAbilityInjection injection, bool injectionSuccessful)
        {
            return new AbilityCallbackStatus(data, stage.Tasks, stage, injection, injectionSuccessful);
        }
    }
}
