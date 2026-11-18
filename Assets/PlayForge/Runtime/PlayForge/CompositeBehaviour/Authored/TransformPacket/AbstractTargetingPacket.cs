using JetBrains.Annotations;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Where something is, for targeting: its <c>position</c>, <c>rotation</c> and <c>scale</c>, the entity it is
    /// (<c>target</c>) and its <c>transform</c> where it has them, and the <c>direction</c> it was aimed along, as
    /// Select Direction sets. Each entity gives one (<c>ITarget.GetTargetingPacket</c>), and a targeting task files the
    /// packet of what it picks under the activation's Target key, each packet's entity becoming one of the activation's
    /// targets. <c>TargetingPacket</c> follows a transform, and <c>StaticTargetingPacket</c> holds its own values.
    /// </summary>
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
