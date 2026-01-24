using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractTransformPacket
    {
        public abstract Vector3 position { get; set; }
        public abstract Quaternion rotation { get; set; }
        public abstract Vector3 scale { get; set; }
    }
}
