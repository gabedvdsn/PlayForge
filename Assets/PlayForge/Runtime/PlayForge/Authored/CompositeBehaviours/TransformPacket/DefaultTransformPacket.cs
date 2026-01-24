using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class DefaultTransformPacket : AbstractTransformPacket
    {
        private Transform transform;

        public DefaultTransformPacket(Transform transform)
        {
            this.transform = transform;
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
