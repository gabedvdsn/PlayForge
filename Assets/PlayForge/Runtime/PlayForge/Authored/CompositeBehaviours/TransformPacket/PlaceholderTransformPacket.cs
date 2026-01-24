using UnityEngine;

namespace FarEmerald.PlayForge
{
    public class PlaceholderTransformPacket : AbstractTransformPacket
    {
        private Vector3 _position;
        private Quaternion _rotation;
        private Vector3 _scale;
        
        public PlaceholderTransformPacket(Vector3 _pos, Quaternion _rot, Vector3 _scale)
        {
            _position = _pos;
            _rotation = _rot;
            this._scale = _scale;
        }

        public PlaceholderTransformPacket(Transform transform)
        {
            _position = transform.position;
            _rotation = transform.rotation;
            _scale = transform.localScale;
        }

        public PlaceholderTransformPacket(AbstractTransformPacket other)
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
