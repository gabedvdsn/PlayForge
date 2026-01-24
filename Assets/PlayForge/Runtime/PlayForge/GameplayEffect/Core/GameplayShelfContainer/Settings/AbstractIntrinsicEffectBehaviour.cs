using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public abstract class AbstractIntrinsicEffectBehaviour
    {
         
    }

    public abstract class AbstractEffectBehaviour : AbstractIntrinsicEffectBehaviour
    {
        
    }

    public abstract class AbstractStackingBehaviour : AbstractIntrinsicEffectBehaviour
    {
        public abstract void InitializeContainer(AbstractStackingEffectContainer container);
        public abstract int GetStacks(AbstractStackingEffectContainer container, int amount = 1);

        public virtual bool FindAmongManyContainers => false;

        public abstract int Order { get; }
        
        public virtual AbstractStackingEffectContainer GetCorrectContainer(GameplayEffectSpec spec, bool ongoing, AbstractStackingEffectContainer[] containers)
        {
            return containers.FirstOrDefault();
        }
}

    public enum EStackBoundsOnMaxPolicy
    {
        DoNothing,
        AppendNewContainer
    }

    public class StackBoundsBehaviour : AbstractStackingBehaviour
    {
        public int Min;
        public int Max;
        public EStackBoundsOnMaxPolicy OnMaxPolicy = EStackBoundsOnMaxPolicy.DoNothing;

        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            container.Stack(Min);
        }
        
        public override int GetStacks(AbstractStackingEffectContainer container, int amount = 1)
        {
            return Mathf.Min(Max, Mathf.Max(Min, container.Stacks + amount));
        }
        public override int Order => 999;

        public override AbstractStackingEffectContainer GetCorrectContainer(GameplayEffectSpec spec, bool ongoing, AbstractStackingEffectContainer[] containers)
        {
            return OnMaxPolicy == EStackBoundsOnMaxPolicy.DoNothing 
                ? base.GetCorrectContainer(spec, ongoing, containers) 
                : containers.FirstOrDefault(c => c.Stacks < Max);
        }
    }

    public class StackAmountOperation : AbstractStackingBehaviour
    {
        public int InitializationStacks = 0;
        public EMagnitudeOperation Operation;
        public AbstractScaler Scaler;
        public ERoundingOperation Rounding;

        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            
        }
        
        public override int GetStacks(AbstractStackingEffectContainer container, int amount = 1)
        {
            if (Scaler is null) return amount;
            
            float stacks = Operation switch
            {
                EMagnitudeOperation.MultiplyWithScaler => amount * Scaler.Evaluate(container.Spec.GetImpactDerivation()),
                EMagnitudeOperation.AddScaler => amount + Scaler.Evaluate(container.Spec.GetImpactDerivation()),
                EMagnitudeOperation.UseMagnitude => amount,
                EMagnitudeOperation.UseScaler => Scaler.Evaluate(container.Spec.GetImpactDerivation()),
                _ => throw new ArgumentOutOfRangeException()
            };

            return Rounding switch
            {

                ERoundingOperation.Floor => Mathf.FloorToInt(stacks),
                ERoundingOperation.Ceil => Mathf.CeilToInt(stacks),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
        public override int Order => 1;
    }

    public class InitializationOperation : AbstractStackingBehaviour
    {
        public int InitializationStacks = 0;
        public EMagnitudeOperation Operation;
        public AbstractScaler Scaler;
        public ERoundingOperation Rounding;
        
        public override void InitializeContainer(AbstractStackingEffectContainer container)
        {
            if (Scaler is null)
            {
                container.Stack(InitializationStacks);
                return;
            }
            
            float stacks = Operation switch
            {
                EMagnitudeOperation.MultiplyWithScaler => InitializationStacks * Scaler.Evaluate(container.Spec.GetImpactDerivation()),
                EMagnitudeOperation.AddScaler => InitializationStacks + Scaler.Evaluate(container.Spec.GetImpactDerivation()),
                EMagnitudeOperation.UseMagnitude => InitializationStacks,
                EMagnitudeOperation.UseScaler => Scaler.Evaluate(container.Spec.GetImpactDerivation()),
                _ => throw new ArgumentOutOfRangeException()
            };

            var intStacks = Rounding switch
            {
                ERoundingOperation.Floor => Mathf.FloorToInt(stacks),
                ERoundingOperation.Ceil => Mathf.CeilToInt(stacks),
                _ => throw new ArgumentOutOfRangeException()
            };
            
            container.Stack(intStacks);
        }
        public override int GetStacks(AbstractStackingEffectContainer container, int amount = 1)
        {
            return amount;
        }
        public override int Order => -1;
    }

    // The other intrinsic behaviours...

}
