using System;

namespace FarEmerald.PlayForge
{
    /// <summary>A condition that always holds.</summary>
    [Serializable]
    [GameplayCondition("Always")]
    [ForgeMenu(Description = "Always holds.")]
    public class AlwaysCondition : GameplayCondition
    {
        protected override bool Evaluate(in NumberContext context) => true;

        public override string Describe() => "always";
    }

    /// <summary>A condition that never holds.</summary>
    [Serializable]
    [GameplayCondition("Never")]
    [ForgeMenu(Description = "Never holds.")]
    public class NeverCondition : GameplayCondition
    {
        protected override bool Evaluate(in NumberContext context) => false;

        public override string Describe() => "never";
    }
}
