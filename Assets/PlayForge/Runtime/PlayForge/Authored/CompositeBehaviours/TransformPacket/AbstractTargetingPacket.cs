using JetBrains.Annotations;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractTargetingPacket
    {
        public Transform transform { get; set; }
        public ITarget target { get; set; }
        public Vector3 direction { get; set; }

        public bool HasTransform => transform;
        public bool HasTarget => target is not null;

        protected AbstractTargetingPacket(AbstractTargetingPacket other)
        {
            if (other is null) return;
            
            transform = other.transform;
            target = other.target;
            direction = other.direction;
        }

        protected AbstractTargetingPacket(Transform transform, ITarget target)
        {
            this.transform = transform;
            this.target = target;
        }
        
        public abstract Vector3 position { get; set; }
        public abstract Quaternion rotation { get; set; }
        public abstract Vector3 scale { get; set; }
    }
}
