using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The base of every task an ability runs: its targeting task, an <c>AbstractTargetingAbilityTask</c>, and its
    /// stages' tasks and asset loaders, each an <c>AbstractAbilityTask</c>. Each is handed the activation's data:
    /// <c>Prepare</c> as its stage starts, <c>Activate</c>, the work its stage waits on, and <c>Clean</c> as the stage
    /// ends; <c>IsCriticalSection</c> says whether it's critical work, which commits its ability to its stage and keeps
    /// out another ability's critical work while it runs.
    ///
    /// <para>One task object serves every activation of its ability, so what it keeps for one belongs in the
    /// activation's data, not in its fields. <c>Read</c> gives one of its numbers at its ability's level,
    /// <c>EntityOf</c> its caster or its first target, and <c>ApplyTo</c> applies an effect from the ability with the
    /// activation's magnitudes.</para>
    /// </summary>
    public abstract class AbstractAbilityRelatedTask
    {
        protected AbstractAbilityRelatedTask() { }

        /// <summary>
        /// Determines whether another ability proxy can be active at the same time as the proxy containing this task.
        /// 
        /// For example, a proxy with some animation events has a critical section, and another proxy with a critical section must not interrupt the conclusion of the animation (and the injections relevant to the animation).
        /// 
        /// If a proxy with a critical section is active, no other proxy with a critical section can be active, regardless of ability activation policy.
        /// Under Activate Anyway or Refuse, the other is refused as Critical; under Queue, it waits in the input buffer.
        /// </summary>
        public abstract bool IsCriticalSection { get; }
        
        /// <summary>
        /// A number of the task, read for its activation as it runs: at its ability's level, against its caster, its target
        /// and its data. None reads 0.
        /// </summary>
        protected static float Read(ScalableFloat number, AbilityDataPacket data) =>
            number == null ? 0f : number.Evaluate(NumberContext.Of(data));

        /// <summary>A whole number of the task, read for its activation, as <see cref="Read(ScalableFloat, AbilityDataPacket)"/> reads.</summary>
        protected static int Read(ScalableInt number, AbilityDataPacket data) =>
            number == null ? 0 : number.Evaluate(NumberContext.Of(data));

        /// <summary>
        /// The entity a task acts on for its activation: its caster, or the activation's first target, where it's an entity;
        /// null where there's none.
        /// </summary>
        protected static IGameplayAbilitySystem EntityOf(AbilityDataPacket data, NumberSide on)
        {
            if (data == null) return null;
            if (on == NumberSide.Caster) return data.EffectOrigin?.GetSource()?.ToGAS();
            return data.TryGetAt(Tags.TARGET_REAL, 0, out ITarget target) ? target as IGameplayAbilitySystem : null;
        }

        /// <summary>
        /// Applies an effect from the activation's origin to a target, with the activation's caller's magnitudes, as an Apply
        /// Effects task's effects land; whether it landed.
        /// </summary>
        protected static bool ApplyTo(AbilityDataPacket data, ITarget target, GameplayEffect effect)
        {
            if (data == null || effect == null || target == null || target.IsDead) return false;
            var spec = target.GenerateEffectSpec(data.EffectOrigin, effect);
            if (spec == null) return false;
            spec.TakeSetByCaller(data.SetByCallerMagnitudes);
            return target.ApplyGameplayEffect(spec);
        }

        #region Task Methods
        
        public virtual void Prepare(AbilityDataPacket data) { }

        public abstract UniTask Activate(AbilityDataPacket data, CancellationToken token);
        
        public virtual void Clean(AbilityDataPacket data) { }
        
        #endregion
    }
}
