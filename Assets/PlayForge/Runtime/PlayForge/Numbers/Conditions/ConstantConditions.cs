using System;

namespace FarEmerald.PlayForge
{
    /// <summary>A condition that always holds (FEAT-29).</summary>
    [Serializable]
    [GameplayCondition("Always")]
    public class AlwaysCondition : GameplayCondition
    {
        protected override bool Evaluate(in NumberContext context) => true;

        public override string Describe() => "always";
    }

    /// <summary>A condition that never holds (FEAT-29).</summary>
    [Serializable]
    [GameplayCondition("Never")]
    public class NeverCondition : GameplayCondition
    {
        protected override bool Evaluate(in NumberContext context) => false;

        public override string Describe() => "never";
    }
}
