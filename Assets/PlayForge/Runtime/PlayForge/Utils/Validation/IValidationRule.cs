namespace FarEmerald.PlayForge
{
    public interface IValidationRule<T> where T : IValidationReady
    {   
        bool Validate(T data, out string error);
    }

    public interface IAbilityValidationRule : IValidationRule<AbilityDataPacket>
    {
        
    }
}