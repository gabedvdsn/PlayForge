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
    public class GameplayEffectImpactSpecification
    {
        public Attribute AttributeTarget;
        public EEffectImpactTarget TargetImpact;
        public ECalculationOperation ImpactOperation = ECalculationOperation.Add;
        
        public EAffiliationPolicy AffiliationPolicy = EAffiliationPolicy.UseAffiliationList;
        public EAnyAllPolicy AffiliationComparison = EAnyAllPolicy.Any;
        [ForgeTagContext(ForgeContext.Affiliation)] public List<Tag> Affiliations = new List<Tag>();
        
        [ForgeTagContext(ForgeContext.Impact)] 
        public List<Tag> ImpactTypes;
        public bool ReverseImpactOnRemoval;
        
        public float Magnitude;
        [SerializeReference] public AbstractScaler MagnitudeScaler;
        public EMagnitudeOperation RealMagnitude = EMagnitudeOperation.AddScaler;
        
        public ContainedEffectPacket[] Packets;

        public GameplayEffectImpactSpecification()
        {
        }

        public GameplayEffectImpactSpecification(GameplayEffectImpactSpecification o)
        {
            AttributeTarget = o.AttributeTarget;
            TargetImpact = o.TargetImpact;
            ImpactOperation = o.ImpactOperation;
            AffiliationPolicy = o.AffiliationPolicy;
            ImpactTypes = o.ImpactTypes;
            ReverseImpactOnRemoval = o.ReverseImpactOnRemoval;
            Magnitude = o.Magnitude;
            MagnitudeScaler = o.MagnitudeScaler;
            
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
            MagnitudeScaler?.Initialize(spec);
        }

        public float GetMagnitude(GameplayEffectSpec spec)
        {
            float calculatedMagnitude = MagnitudeScaler?.Evaluate(spec) ?? 0f;
            
            return RealMagnitude switch
            {
                EMagnitudeOperation.AddScaler => Magnitude + calculatedMagnitude,
                EMagnitudeOperation.MultiplyWithScaler => Magnitude * calculatedMagnitude,
                EMagnitudeOperation.UseMagnitude => Magnitude,
                EMagnitudeOperation.UseScaler => calculatedMagnitude,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        
        public float GetMagnitude(AbstractStackingEffectContainer container)
        {
            float calculatedMagnitude = MagnitudeScaler?.Evaluate(container) ?? 0f;
            
            return RealMagnitude switch
            {
                EMagnitudeOperation.AddScaler => Magnitude + calculatedMagnitude,
                EMagnitudeOperation.MultiplyWithScaler => Magnitude * calculatedMagnitude,
                EMagnitudeOperation.UseMagnitude => Magnitude,
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
