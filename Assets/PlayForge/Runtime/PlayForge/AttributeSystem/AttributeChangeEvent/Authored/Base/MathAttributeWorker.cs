using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Multiplies the SMAVs under the primary attribute by the current value
    /// </summary>
    public class MathAttributeWorker : AbstractRelativeAttributeWorker
    {
        [Header("Math Event")]
        
        public ECalculationOperation Operation = ECalculationOperation.Multiply;
        public EEffectImpactTarget OperationTarget;
        public EMathApplicationPolicy OperationPolicy;
        
        public override void Activate(IGameplayAbilitySystem system, Dictionary<Attribute, CachedAttributeValue> attributeCache,
            ChangeValue change)
        {
            var result = ForgeHelper.AttributeMathEvent(change.Value.ToAttributeValue(), GetRelative(attributeCache, change), Operation, OperationTarget, OperationPolicy);
            change.Override(result);
        }
    }

    public enum ESignPolicy
    {
        Negative,
        Positive,
        ZeroBiased,
        ZeroNeutral
    }

    public enum ESignPolicyExtended
    {
        Negative,
        Positive,
        ZeroBiased,
        ZeroNeutral,
        Any
    }

    public enum EMathApplicationPolicy
    {
        AsIs,
        OnePlus,
        OneMinus
    }
}
