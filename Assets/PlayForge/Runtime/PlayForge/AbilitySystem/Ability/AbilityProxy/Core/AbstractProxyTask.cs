using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractProxyTask
    {
        [HideInInspector] public string ReadOnlyDescription = 
            "This is an Ability Proxy Task (APT). APTs implement 3 methods: Prepare, Activate, and Clean. " +
            "Prepare is always called before the APT is activated, and Clean is always called after the " +
            "APT is finished activating, regardless of the manner the activation is resolved.";

        /// <summary>
        /// Determines whether another ability proxy can be active at the same time as the proxy containing this task.
        /// For example, a proxy with some animation events has a critical section, and another proxy with a critical section must not interrupt the conclusion of the animation (and the injections relevant to the animation).
        /// If a proxy with a critical section is active, no other proxy with a critical section can be active, regardless of ability activation policy.
        /// </summary>
        public abstract bool IsCriticalSection { get; }
        
        #region Task Methods
        
        public virtual void Prepare(AbilityDataPacket data)
        {
            
        }

        public abstract UniTask Activate(AbilityDataPacket data, CancellationToken token);


        public virtual void Clean(AbilityDataPacket data)
        {
            
        }
        
        #endregion
    }
}
