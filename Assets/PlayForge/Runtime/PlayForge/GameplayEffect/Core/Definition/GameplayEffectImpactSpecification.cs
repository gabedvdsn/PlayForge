using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    public class GameplayEffectImpactSpecification
    {
        public Attribute AttributeTarget;
        public EEffectImpactTarget TargetImpact;
        public ECalculationOperation ImpactOperation;
        
        public EAffiliationPolicy AffiliationPolicy;
        
        [ForgeCategory(Forge.Categories.ImpactType)]
        public List<Tag> ImpactTypes;
        public bool ReverseImpactOnRemoval;
        public EEffectReApplicationPolicy ReApplicationPolicy;
        
        public float Magnitude;
        public AbstractMagnitudeModifier MagnitudeCalculation;
        public EMagnitudeOperation MagnitudeCalculationOperation;
        
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
            ReApplicationPolicy = o.ReApplicationPolicy;
            Magnitude = o.Magnitude;
            MagnitudeCalculation = o.MagnitudeCalculation;
            
            Packets = new ContainedEffectPacket[o.Packets.Length];
            for (int i = 0; i < o.Packets.Length; i++)
            {
                Packets[i] = new ContainedEffectPacket()
                {
                    Policy = o.Packets[i].Policy,
                    ContainedEffect = new GameplayEffect(o.Packets[i].ContainedEffect)
                };
            }
        }

        public void ApplyImpactSpecifications(GameplayEffectSpec spec)
        {
            MagnitudeCalculation.Initialize(spec);
        }

        public float GetMagnitude(GameplayEffectSpec spec)
        {
            float calculatedMagnitude = MagnitudeCalculation.Evaluate(spec);
            
            return MagnitudeCalculationOperation switch
            {
                EMagnitudeOperation.Add => Magnitude + calculatedMagnitude,
                EMagnitudeOperation.Multiply => Magnitude * calculatedMagnitude,
                EMagnitudeOperation.UseMagnitude => Magnitude,
                EMagnitudeOperation.UseCalculation => calculatedMagnitude,
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
        Multiply,
        Add,
        UseMagnitude,
        UseCalculation
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
        Unaffiliated,
        Affiliated,
        Any
    }
}
