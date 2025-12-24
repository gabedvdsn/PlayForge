using System;

namespace FarEmerald.PlayForge
{
    public interface IValidationRule<T> where T : IValidationReady
    {
        bool Validate(T data, out string error);
    }

    [Serializable]
    public abstract class AbstractAttributeValidationRule : IAbilityValidationRule
    {
        public Attribute Attribute;

        public abstract bool Validate(AbilityDataPacket data, out string error);
        public abstract string GetName();
    }
    
    public interface IAbilityValidationRule : IValidationRule<AbilityDataPacket>
    {
        public string GetName();
    }
}