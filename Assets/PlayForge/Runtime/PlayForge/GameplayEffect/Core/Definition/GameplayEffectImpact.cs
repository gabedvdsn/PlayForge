using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FarEmerald.PlayForge.Extended;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public class GameplayEffectImpact
    {
        public Attribute AttributeTarget;
        public EAttributeTarget TargetImpact;
        public ECalculationOperation ImpactOperation = ECalculationOperation.Add;

        /// <summary>
        /// Only meaningful when <see cref="ImpactOperation"/> is Multiply. Controls how this
        /// multiply impacter composes with multiply impacters from OTHER carriers acting on
        /// the same attribute slot. See <see cref="EMultiplyBehaviour"/> for semantics.
        /// </summary>
        public EMultiplyBehaviour MultiplyBehaviour = EMultiplyBehaviour.Multiplicative;

        public EAffiliationPolicy AffiliationPolicy = EAffiliationPolicy.AlwaysAllow;
        public EAnyAllPolicy AffiliationComparison = EAnyAllPolicy.Any;
        [ForgeTagContext(ForgeContext.Affiliation)] public List<Tag> Affiliations = new List<Tag>();

        [ForgeTagContext(ForgeContext.Impact)]
        public List<Tag> ImpactTypes;

        /// <summary>
        /// Drives both the retention rule and the per-tick rollback path:
        /// <list type="bullet">
        ///   <item>For Current-only impacters, set true to be retained as a sustained modifier
        ///   (auto-reverses on removal via Recompute).</item>
        ///   <item>For periodic-tick contributions committed past the impacter list, set true
        ///   to enqueue a synthetic rollback carrier on container removal that subtracts
        ///   <see cref="TrackedImpact"/> from the attribute's one-shot history.</item>
        ///   <item>Redundant for Base-touching impacters (Base contributions are always retained).
        ///   The drawer flags this with a warning.</item>
        ///   <item>Meaningless for instant-policy effects (no removal phase). Drawer hides.</item>
        /// </list>
        /// </summary>
        public bool ReverseImpactOnRemoval;

        [ScalerOperationKeyword("Magnitude")]
        public ScalerMagnitudeOperation MagnitudeOperation;

        public ContainedEffectPacket[] Packets;

        public GameplayEffectImpact()
        {
        }

        public GameplayEffectImpact(GameplayEffectImpact o)
        {
            AttributeTarget = o.AttributeTarget;
            TargetImpact = o.TargetImpact;
            ImpactOperation = o.ImpactOperation;
            MultiplyBehaviour = o.MultiplyBehaviour;
            AffiliationPolicy = o.AffiliationPolicy;
            ImpactTypes = o.ImpactTypes;
            ReverseImpactOnRemoval = o.ReverseImpactOnRemoval;
            MagnitudeOperation = o.MagnitudeOperation;
            
            Packets = new ContainedEffectPacket[o.Packets.Length];
            for (int i = 0; i < o.Packets.Length; i++)
            {
                Packets[i] = new ContainedEffectPacket()
                {
                    Policy = o.Packets[i].Policy,
                    ContainedEffect = o.Packets[i].ContainedEffect
                };
            }
        }

        public void ApplyImpactSpecifications(GameplayEffectSpec spec)
        {
            MagnitudeOperation.Scaler?.Initialize(spec);
        }

        public float GetMagnitude(GameplayEffectSpec spec)
        {
            return MagnitudeOperation.Evaluate(spec);
        }
        
        public float GetMagnitude(AbstractStackingEffectContainer container)
        {
            float calculatedMagnitude = MagnitudeOperation.Scaler?.Evaluate(container) ?? 0f;
            
            return MagnitudeOperation.RealMagnitude switch
            {
                EMagnitudeOperation.AddScaler => MagnitudeOperation.Magnitude + calculatedMagnitude,
                EMagnitudeOperation.MultiplyWithScaler => MagnitudeOperation.Magnitude * calculatedMagnitude,
                EMagnitudeOperation.UseMagnitude => MagnitudeOperation.Magnitude,
                EMagnitudeOperation.UseScaler => calculatedMagnitude,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public IEnumerable<GameplayEffect> GetContainedEffects(EApplyTickRemove policy)
        {
            return Packets.Where(packet => packet.Policy == policy).Select(p => p.ContainedEffect);
        }
    }
    
    [Serializable]
    public struct ContainedEffectPacket
    {
        public EApplyTickRemove Policy;
        public GameplayEffect ContainedEffect;
    }

    public enum EMagnitudeOperation
    {
        MultiplyWithScaler,
        AddScaler,
        UseMagnitude,
        UseScaler
    }
    
    public enum EAttributeTargetLimited
    {
        CurrentAndBase,
        Base
    }

    public enum EAttributeTargetBinary
    {
        Current,
        Base
    }

    public enum EAttributeTarget
    {
        Current,
        Base,
        CurrentAndBase
    }

    public enum EAttributeTargetExpanded
    {
        Current,
        Base,
        CurrentAndBase,
        CurrentOrBase
    }

    public enum EApplyTickRemove
    {
        OnApply,
        OnTick,
        OnRemove
    }

    public enum EAffiliationPolicy
    {
        UseAffiliationList,
        Unaffiliated,
        Affiliated,
        AlwaysAllow
    }
}
