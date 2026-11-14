namespace FarEmerald.PlayForge
{
    /// <summary>
    /// How a stage's tasks end it: the compiler maps each to a sequence's stage policy, Any to the first task's end and
    /// All to every task's (AbilitySequenceCompiler). The legacy execution that awaited one itself is gone (FN-21).
    /// </summary>
    public interface IAbilityProxyStagePolicy
    {
    }

    /// <summary>The stage ends as its first task does.</summary>
    public class AnyProxyStagePolicy : IAbilityProxyStagePolicy
    {
    }

    /// <summary>The stage ends as its last task does.</summary>
    public class AllProxyStagePolicy : IAbilityProxyStagePolicy
    {
    }
}
