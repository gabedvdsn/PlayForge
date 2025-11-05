using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Abilities are separated into groups by activation policy. Depending on the policy, activation can proceed or is blocked.
    /// Even if the policy allows simultaneous activation, additional validation steps are taken before activation actually proceeds.
    /// If an ability has no targeting proxy (TargetingProxy is null)
    /// </summary>
    [Serializable]
    public class AbilityProxySpecification
    {
        [Header("Targeting Instructions")]
        
        public AbstractTargetingProxyTask TargetingProxy;
        [Tooltip("Implicitly provides the casting system as a target.")]
        public bool UseImplicitTargeting = true;
        
        [Header("Proxy Stages")]
        
        public AbilityProxyStage[] Stages;

        public AbilityProxy GenerateProxy()
        {
            return new AbilityProxy(this);
        }
    }

    [Serializable]
    public class AbilityProxyStage
    {
        public EAnyAllPolicy TaskPolicy;
        public AbstractProxyTask[] Tasks;
        
        [Space(5)]
        
        [Tooltip("After this stage, should the ability usage effects be applied?")]
        public bool ApplyUsageEffects;
    }

    public enum EAnyAllPolicy
    {
        Any,
        All
    }
}
