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
        public EEffectImpactTarget TargetImpact;
        public ECalculationOperation ImpactOperation = ECalculationOperation.Add;
        
        public EAffiliationPolicy AffiliationPolicy = EAffiliationPolicy.AlwaysAllow;
        public EAnyAllPolicy AffiliationComparison = EAnyAllPolicy.Any;
        [ForgeTagContext(ForgeContext.Affiliation)] public List<Tag> Affiliations = new List<Tag>();
        
        [ForgeTagContext(ForgeContext.Impact)] 
        public List<Tag> ImpactTypes;
        public bool ReverseImpactOnRemoval;
        
        [ScalerOperationKeyword("Magnitude Scaler")]
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

    public enum EEffectImpactTargetLimited
    {
        Current,
        Base
    }

    public enum EEffectImpactTarget
    {
        Current,
        Base,
        CurrentAndBase
    }

    public enum EEffectImpactTargetExpanded
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
