using System;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractTagWorker
    {
        [Header("Tag Worker")] 
        
        public TagWorkerRequirements Requirements;
        [Tooltip("Allow multiple instances of this worker")]
        public bool AllowMultipleInstances;
        
        [Space(5)]
        
        [Tooltip("Ticks between calls to Tick.")]
        public int TickPause;
        
        public abstract void Activate(IGameplayAbilitySystem component);
        public abstract void Tick(IGameplayAbilitySystem component);
        public abstract void Resolve(IGameplayAbilitySystem component);

        public abstract AbstractTagWorkerInstance Generate(ITagHandler system);
        
        public bool ValidateWorkFor(ITagHandler system)
        {
            var appliedTags = system.GetAppliedTags();
            foreach (TagWorkerRequirementPacket packet in Requirements.TagPackets)
            {
                var weight = system.GetWeight(packet.Tag);
                switch (packet.Policy)
                {
                    case ERequireAvoidPolicy.Require:
                        if (weight < packet.RequiredWeight) return false;
                        break;
                    case ERequireAvoidPolicy.Avoid:
                        if (weight >= packet.RequiredWeight) return false;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                /*switch (packet.Policy)
                {
                    case ERequireAvoidPolicy.Require:
                        if (!appliedTags.Contains(packet.Tag)) return false;
                        break;
                    case ERequireAvoidPolicy.Avoid:
                        if (appliedTags.Contains(packet.Tag)) return false;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                if (system.GetWeight(packet.Tag) < packet.RequiredWeight) return false;*/
            }

            return true;
        }
    }
    
    public abstract class AbstractTagWorkerInstance
    {
        public AbstractTagWorker Base;
        private IGameplayAbilitySystem System;

        public int TicksRemaining;

        protected AbstractTagWorkerInstance(AbstractTagWorker workerBase, IGameplayAbilitySystem system)
        {
            Base = workerBase;
            System = system;

            TicksRemaining = 0;
        }

        public void Initialize()
        {
            Base.Activate(System);
        }
        
        public void Tick()
        {
            if (TicksRemaining <= 0)
            {
                Base.Tick(System);
                TicksRemaining = Base.TickPause;
            }
            else TicksRemaining -= 1;
        }

        public void Resolve()
        {
            Base.Resolve(System);
        }
    }
}
