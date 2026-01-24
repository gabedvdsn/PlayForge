using System.Linq;
using Unity.VisualScripting.Dependencies.NCalc;

namespace FarEmerald.PlayForge
{
    public interface IAbilityInjection
    {
        public bool OnContainerInject(AbilitySpecContainer container);
        public bool OnProxyInject(AbilityProxy proxy);
        public bool OnStageInject(AbilityTaskBehaviourStage stage);
        public bool OnTaskInject(AbstractAbilityRelatedTask task);
    }

    public abstract class LimitedAbilityInjection : IAbilityInjection
    {
        public virtual bool OnContainerInject(AbilitySpecContainer container)
        {
            return true;
        }

        public abstract bool OnProxyInject(AbilityProxy proxy);

        public virtual bool OnStageInject(AbilityTaskBehaviourStage stage)
        {
            return true;
        }

        public virtual bool OnTaskInject(AbstractAbilityRelatedTask task)
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
        public override bool OnContainerInject(AbilitySpecContainer container)
        {
            container.cts?.Cancel();
            return true;
        }

        public override bool OnProxyInject(AbilityProxy proxy)
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
        public override bool OnProxyInject(AbilityProxy proxy)
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
        public override bool OnProxyInject(AbilityProxy proxy)
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
        public override bool OnProxyInject(AbilityProxy proxy)
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
        public override bool OnProxyInject(AbilityProxy proxy)
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
}