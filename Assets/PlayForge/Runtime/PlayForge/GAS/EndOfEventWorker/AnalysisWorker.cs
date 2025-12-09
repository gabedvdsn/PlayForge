namespace FarEmerald.PlayForge
{
    public abstract class AnalysisWorker
    {
        public abstract void Activate(IGameplayAbilitySystem gas);
    }
    
    public class DeathEventWorker : AnalysisWorker
    {
        public override void Activate(IGameplayAbilitySystem gas)
        {
            if (gas.GetAttributeSystem().TryGetAttributeValue(Attribute.Generate("Tester", ""), out AttributeValue value))
            {
                if (value.CurrentValue <= 0)
                {
                    gas.ToGASObject()?.gameObject.SetActive(false);
                }
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