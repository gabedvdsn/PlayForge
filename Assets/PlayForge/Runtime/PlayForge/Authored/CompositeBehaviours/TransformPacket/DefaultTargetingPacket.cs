using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class DefaultTargetingPacket : AbstractTargetingPacket
    {

        public DefaultTargetingPacket(Transform transform) : base(transform, null)
        {
        }
        
        public DefaultTargetingPacket(Transform transform, ITarget target) : base(transform, target)
        {
        }
        
        public DefaultTargetingPacket(AbstractTargetingPacket other) : base(other.transform, other.target)
        {
        }
        
        /// <summary>
        /// Returns this packet if it wraps a valid transform, otherwise a NullTransformPacket.
        /// </summary>
        public override DefaultTargetingPacket ToDefault()
        {
            return transform != null ? this : new NullTargetingPacket();
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
