using System;

namespace FarEmerald.PlayForge
{
    [Serializable]
    public abstract class AbstractAttributeValidationRule : IAbilityValidationRule
    {
        public Attribute Attribute;

        public abstract bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error);
        public abstract string GetName();
    }
    
    /// <summary>
    /// A rule an ability's activation keeps, for its source or its target. It took the generic IValidationRule, which
    /// nothing else took; it declares its own Validate (FEAT-36, Decision 44).
    /// <para>
    /// A rule is a question, asked as often as the activation needs it answered: as it's requested, each frame a
    /// buffered request waits, and again once targeting has found its target, as the caster may have changed meanwhile.
    /// So a rule changes nothing and draws nothing: one that rolled a chance rolled it each time it was asked (GAS-73).
    /// </para>
    /// </summary>
    public interface IAbilityValidationRule
    {
        bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error);

        public string GetName();
    }
}