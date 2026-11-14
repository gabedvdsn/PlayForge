using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractAbilityRelatedTask
    {
        protected AbstractAbilityRelatedTask() { }

        /// <summary>
        /// Determines whether another ability proxy can be active at the same time as the proxy containing this task.
        /// 
        /// For example, a proxy with some animation events has a critical section, and another proxy with a critical section must not interrupt the conclusion of the animation (and the injections relevant to the animation).
        /// 
        /// If a proxy with a critical section is active, no other proxy with a critical section can be active, regardless of ability activation policy.
        /// </summary>
        public abstract bool IsCriticalSection { get; }
        
        /// <summary>
        /// A number of the task, read for its activation as it runs: at its ability's level, against its caster, its target
        /// and its data (FN-20). None reads 0.
        /// </summary>
        protected static float Read(ScalableFloat number, AbilityDataPacket data) =>
            number == null ? 0f : number.Evaluate(NumberContext.Of(data));

        /// <summary>A whole number of the task, read for its activation, as <see cref="Read(ScalableFloat, AbilityDataPacket)"/> reads.</summary>
        protected static int Read(ScalableInt number, AbilityDataPacket data) =>
            number == null ? 0 : number.Evaluate(NumberContext.Of(data));

        #region Task Methods
        
        public virtual void Prepare(AbilityDataPacket data) { }

        public abstract UniTask Activate(AbilityDataPacket data, CancellationToken token);
        
        public virtual void Clean(AbilityDataPacket data) { }
        
        #endregion
    }
}
