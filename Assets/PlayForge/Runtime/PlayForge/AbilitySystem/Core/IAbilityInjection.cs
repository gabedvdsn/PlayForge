/*using System.Linq;
using Unity.VisualScripting.Dependencies.NCalc;

namespace FarEmerald.PlayForge
{
    public interface IAbilityInjection
    {
        public bool OnContainerInject(AbilitySpecContainer container, AbilityDataPacket data);
        public bool OnProxyInject(AbilityProxy proxy, AbilityDataPacket data);
        public bool OnStageInject(AbilityTaskBehaviourStage stage, AbilityDataPacket data);
        public bool OnTaskInject(AbstractAbilityRelatedTask task, AbilityDataPacket datta);
    }

    public abstract class LimitedAbilityInjection : IAbilityInjection
    {
        public virtual bool OnContainerInject(AbilitySpecContainer container, AbilityDataPacket data)
        {
            return true;
        }

        public abstract bool OnProxyInject(AbilityProxy proxy, AbilityDataPacket data);

        public virtual bool OnStageInject(AbilityTaskBehaviourStage stage, AbilityDataPacket data)
        {
            return true;
        }

        public virtual bool OnTaskInject(AbstractAbilityRelatedTask task, AbilityDataPacket datta)
        {
            return true;
        }
        public virtual string InjectionName()
        {
            return "Limited Injection";
        }
    }

    public class InterruptInjection : LimitedAbilityInjection
    {
        public override bool OnContainerInject(AbilitySpecContainer container, AbilityDataPacket data)
        {
            // CTS cancellation is handled per-handle in AbilitySpecContainer.Inject
            return true;
        }

        public override bool OnProxyInject(AbilityProxy proxy, AbilityDataPacket data)
        {
            // Do nothing -- handled externally via the high-level cts token
            return true;
        }
        
        public override string InjectionName()
        {
            return "Interrupt";
        }
    }

    public class SkipCurrentStageInjection : LimitedAbilityInjection
    {
        public override bool OnProxyInject(AbilityProxy proxy, AbilityDataPacket data)
        {
            if (proxy.StageIndex < 0 || proxy.stageSources[proxy.StageIndex] is null) return false;
            proxy.stageSources[proxy.StageIndex].Cancel();
            return true;
        }
        
        public override string InjectionName()
        {
            return "Skip Current Stage";
        }
    }

    public class SkipAndMaintainCurrentStageInjection : LimitedAbilityInjection
    {
        public override bool OnProxyInject(AbilityProxy proxy, AbilityDataPacket data)
        {
            proxy.maintainedStages += 1;
            proxy.nextStageSignal?.TrySetResult();
            return true;
        }
        
        public override string InjectionName()
        {
            return "Skip and Maintain Current Stage";
        }
    }

    public class StopMaintainLastInjection : LimitedAbilityInjection
    {
        public override bool OnProxyInject(AbilityProxy proxy, AbilityDataPacket data)
        {
            if (proxy.StageIndex < 0 || proxy.stageSources.Count == 0) return false;
            proxy.stageSources[proxy.stageSources.Keys.ToArray()[0]]?.Cancel();
            return true;
        }
        
        public override string InjectionName()
        {
            return "Stop Maintaining Last";
        }
    }

    public class StopMaintainAllInjection : LimitedAbilityInjection
    {
        public override bool OnProxyInject(AbilityProxy proxy, AbilityDataPacket data)
        {
            if (proxy.StageIndex < 0 || proxy.stageSources.Count == 0) return false;
            foreach (var stageIndex in proxy.stageSources.Keys) proxy.stageSources[stageIndex]?.Cancel();
            return true;
        }
        
        public override string InjectionName()
        {
            return "Stop Maintaining All";
        }
    }
}*/