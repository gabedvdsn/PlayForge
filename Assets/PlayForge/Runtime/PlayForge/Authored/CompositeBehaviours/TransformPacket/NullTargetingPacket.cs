using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class NullTargetingPacket : DefaultTargetingPacket
    {
        private Vector3 pos = Vector3.zero;
        private Quaternion rot = Quaternion.identity;
        private Vector3 scl = Vector3.zero;

        public NullTargetingPacket() : base(null, null)
        {
        }

        public override Vector3 position
        {
            get => pos;
            set => pos = value;
        }

        public override Quaternion rotation
        {
            get => rot;
            set => rot = value;
        }
            
        public override Vector3 scale
        {
            get => scl;
            set => scl = value;
        }
    }
}
