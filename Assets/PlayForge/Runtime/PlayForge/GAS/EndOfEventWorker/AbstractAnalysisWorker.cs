using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public abstract class AbstractAnalysisWorker : Taggable
    {
        public abstract void Activate(IGameplayAbilitySystem gas);
        public abstract HashSet<Tag> GetAllTags();
    }
    
    public abstract class AttributeWatcherWorker : AbstractAnalysisWorker
    {
        public Attribute Target;
        
        public override void Activate(IGameplayAbilitySystem gas)
        {
            if (!gas.GetAttributeSystem().TryGetAttributeValue(Target, out AttributeValue value)) return;
            
            if (value.CurrentValue <= 0)
            {
                gas.ToGASObject()?.gameObject.SetActive(false);
            }
        }

        public bool ContainAtRuntime => false;
    }

    public interface IWorker
    {
        /// <summary>
        /// Initialize a per-GAS container at runtime for this worker
        /// </summary>
        public bool ContainAtRuntime { get; }
    }
}