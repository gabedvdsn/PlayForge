using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A targeting packet that follows a <c>Transform</c>: its position, rotation and scale are the transform's, read
    /// as they're asked for, and setting them moves it. The <c>GameplayAbilitySystem</c> component's
    /// <c>GetTargetingPacket</c> gives one on its own transform, with itself as the target.
    /// </summary>
    public class TargetingPacket : AbstractTargetingPacket
    {
        public TargetingPacket(Transform transform, ITarget target) : base(transform, target)
        {
        }
        
        public TargetingPacket(AbstractTargetingPacket other) : base(other.transform, other.target)
        {
        }
        
        public override Vector3 position
        {
            get => transform.position;
            set => transform.position = value;
        }

        public override Quaternion rotation
        {
            get => transform.rotation;
            set => transform.rotation = value;
        }
            
        public override Vector3 scale
        {
            get => transform.localScale;
            set => transform.localScale = value;
        }

        
    }
}
