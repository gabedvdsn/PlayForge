namespace FarEmerald.PlayForge
{
    public abstract class AnalysisWorker
    {
        public abstract void Activate(GASComponent gas);
    }
    
    public class DeathEventWorker : AnalysisWorker
    {
        public override void Activate(GASComponent gas)
        {
            if (gas.AttributeSystem.TryGetAttributeValue(Attribute.Generate("Tester", ""), out AttributeValue value))
            {
                if (value.CurrentValue <= 0)
                {
                    gas.gameObject.SetActive(false);
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