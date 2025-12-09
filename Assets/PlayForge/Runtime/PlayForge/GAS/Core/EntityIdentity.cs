using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class EntityIdentity
    {
        public GASIdentityData Identity = new();
        
        public EAbilityActivationPolicy ActivationPolicy = EAbilityActivationPolicy.SingleActiveQueue;
        public int MaxAbilities = 99;
        public List<Ability> StartingAbilities = new();
        public bool AllowDuplicateAbilities;
        
        public List<AbstractImpactWorker> ImpactWorkers = new();
        
        public AttributeSet AttributeSet = new();
        public List<AbstractAttributeWorker> AttributeChangeEvents = new();
        
        public List<AbstractTagWorker> TagWorkers = new();
    }

    public class GameRootEntity : EntityIdentity
    {
        public GameRootEntity()
        {
            MaxAbilities = int.MaxValue;
            ActivationPolicy = EAbilityActivationPolicy.NoRestrictions;
        }
    }
}
