using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FarEmerald.PlayForge.Extended;
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
        public Tag ImplicitTag = Tags.TARGET;

        [SerializeReference] 
        public List<AbstractSetAssetsTask> AssetLoaders = new();
        
        // Empty, not null: an ability made in code had no list until it was saved and loaded, so adding a stage threw (GAS-63)
        public List<AbilityTaskBehaviourStage> Stages = new();

        /// <summary>
        /// The abilities, by asset tag or a context tag, or a child of one, that cancel this one as they activate, in
        /// each stage that names none of its own. A critical stage is committed, so nothing cancels it. Nothing
        /// cancelled an ability but an interrupt by slot (FEAT-5).
        /// </summary>
        [ForgeTagContext(ForgeContext.ContextIdentifier)] public List<Tag> CancelledBy = new();

        /// <summary>
        /// The abilities, by asset tag or a context tag, or a child of one, that can't activate while this one runs, in
        /// each stage that names none of its own. Blocking was the claim alone, per activation policy (FEAT-5).
        /// </summary>
        [ForgeTagContext(ForgeContext.ContextIdentifier)] public List<Tag> Blocks = new();

        public AbilityProxy GenerateProxy(string abilityName = null)
        {
            return new AbilityProxy(this, abilityName);
        }
    }

    [Serializable]
    public class AbilityTaskBehaviourStage
    {
        /// <summary>
        /// Describes when the Stage should be considered complete
        /// </summary>
        [SerializeReference]
        public IAbilityProxyStagePolicy StagePolicy = new AllProxyStagePolicy();
        
        [SerializeReference]
        public List<AbstractAbilityTask> Tasks = new();
        
        [Space(5)]
        
        [Tooltip("At the beginning of this stage, should the ability usage effects be applied?")]
        public bool ApplyUsageEffects;
        public bool ApplyUsageAtStageStart = true;

        /// <summary>
        /// When the stage runs: always, or only while a tag is there, or only while it's missing. Two stages that name one
        /// tag, one wanting it and one wanting it missing, branch: an attack's recovery on a hit and on a miss (TS-7).
        /// </summary>
        public AbilityStageCondition Condition = new();

        /// <summary>
        /// The abilities that cancel the ability as they activate while this stage runs, as the ability's Cancelled By
        /// says; a stage that names none takes the ability's. A critical stage can't be cancelled (FEAT-5).
        /// </summary>
        [ForgeTagContext(ForgeContext.ContextIdentifier)] public List<Tag> CancelledBy = new();

        /// <summary>
        /// The abilities that can't activate while this stage runs, as the ability's Blocks says; a stage that names none
        /// takes the ability's (FEAT-5).
        /// </summary>
        [ForgeTagContext(ForgeContext.ContextIdentifier)] public List<Tag> Blocks = new();

        /// <summary>The stage's own Cancelled By, or <paramref name="ability"/>'s when it names none (FEAT-5).</summary>
        public List<Tag> CancelledByOr(List<Tag> ability) => CancelledBy is { Count: > 0 } ? CancelledBy : ability;

        /// <summary>The stage's own Blocks, or <paramref name="ability"/>'s when it names none (FEAT-5).</summary>
        public List<Tag> BlocksOr(List<Tag> ability) => Blocks is { Count: > 0 } ? Blocks : ability;

        /// <summary>Whether a task in the stage is a critical section, which commits the ability to the stage.</summary>
        public bool IsCritical
        {
            get
            {
                if (Tasks == null) return false;
                for (int i = 0; i < Tasks.Count; i++)
                    if (Tasks[i] != null && Tasks[i].IsCriticalSection) return true;
                return false;
            }
        }
    }

    /// <summary>
    /// When an ability's stage runs: always, or only while a tag is there, or only while it's missing, in the
    /// activation's data, on the caster or on the target. The compiler emitted no branch, so there was no on-hit or
    /// on-miss stage (TS-7, C12).
    /// </summary>
    [Serializable]
    public class AbilityStageCondition
    {
        [Tooltip("Where the tag is looked for: the activation's data, where an Apply Effects task records a landing, or the caster's or target's tags")]
        public EAbilityStageConditionSubject On = EAbilityStageConditionSubject.Activation;

        [Tooltip("The tag the stage asks for. With none, the stage always runs")]
        public Tag Tag;

        [Tooltip("Run the stage while the tag is missing instead, as a miss's recovery does")]
        public bool WhenMissing;

        /// <summary>Whether the stage asks anything; one that doesn't always runs.</summary>
        public bool IsSet => Tag.IsValid;

        /// <summary>Whether the stage runs in this activation.</summary>
        public bool Holds(AbilityDataPacket data)
        {
            if (!IsSet || data == null) return true;
            bool there = On switch
            {
                EAbilityStageConditionSubject.Activation => data.ContainsKey(Tag),
                EAbilityStageConditionSubject.Caster => data.EffectOrigin?.GetSource() is { } caster && caster.GetTagWeight(Tag) > 0,
                EAbilityStageConditionSubject.Target => data.TryGetFirstTarget(out var target) && target != null && target.GetTagWeight(Tag) > 0,
                _ => true,
            };
            return there != WhenMissing;
        }
    }

    /// <summary>Where a stage's condition looks for its tag (TS-7).</summary>
    public enum EAbilityStageConditionSubject
    {
        [InspectorName("The Activation")] Activation,
        Caster,
        Target,
    }

    public enum EAnyAllPolicy
    {
        Any,
        All
    }
    
    
}
