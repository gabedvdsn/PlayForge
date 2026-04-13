using UnityEngine;

namespace FarEmerald.PlayForge
{
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
