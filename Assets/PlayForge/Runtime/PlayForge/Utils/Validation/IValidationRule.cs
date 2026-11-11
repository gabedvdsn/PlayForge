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
    /// </summary>
    public interface IAbilityValidationRule
    {
        bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error);

        public string GetName();
    }
}