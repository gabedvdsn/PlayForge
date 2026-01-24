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
    public class AbilityBehaviour
    {
        [SerializeReference]
        public AbstractTargetingAbilityTask Targeting;
        
        [Tooltip("Implicitly provides the casting system as a target.")]
        public bool UseImplicitTargeting = true;
        
        public List<AbilityTaskBehaviourStage> Stages;

        public AbilityProxy GenerateProxy()
        {
            return new AbilityProxy(this);
        }
    }

    [Serializable]
    public class AbilityTaskBehaviourStage
    {
        /// <summary>
        /// Describes when the Stage should be considered complete
        /// </summary>
        [SerializeReference]
        public IProxyStagePolicy StagePolicy = new AllProxyStagePolicy();
        
        [SerializeReference]
        public List<AbstractAbilityTask> Tasks = new();
        
        [Space(5)]
        
        [Tooltip("At the beginning of this stage, should the ability usage effects be applied?")]
        public bool ApplyUsageEffects;
    }

    public enum EAnyAllPolicy
    {
        Any,
        All
    }
    
    
}
