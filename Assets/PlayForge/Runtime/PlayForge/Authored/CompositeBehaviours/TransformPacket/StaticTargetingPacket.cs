using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class StaticTargetingPacket : AbstractTargetingPacket
    {
        private Vector3 _position;
        private Quaternion _rotation;
        private Vector3 _scale;

        public StaticTargetingPacket() : base(null, null)
        {
        }

        public StaticTargetingPacket(Vector3 position) : base(null, null)
        {
            _position = position;
            _rotation = Quaternion.identity;
            _scale = Vector3.one;
        }

        public StaticTargetingPacket(Vector3 _pos, Quaternion _rot, Vector3 _scale) : base(null, null)
        {
            _position = _pos;
            _rotation = _rot;
            this._scale = _scale;
        }

        public StaticTargetingPacket(Transform transform)  : base(transform, null)
        {
            _position = transform.position;
            _rotation = transform.rotation;
            _scale = transform.localScale;
        }

        public StaticTargetingPacket(AbstractTargetingPacket other) : base(other)
        {
            _position = other.position;
            _rotation = other.rotation;
            _scale = other.scale;
        }
        
        public override Vector3 position
        {
            get => _position;
            set => _position = value;
        }

        public override Quaternion rotation
        {
            get => _rotation;
            set => _rotation = value;
        }
            
        public override Vector3 scale
        {
            get => _scale;
            set => _scale = value;
        }
    }
}
