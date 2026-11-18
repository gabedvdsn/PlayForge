using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A targeting packet that points at nothing: a <c>StaticTargetingPacket</c> with no entity and no transform, its
    /// position, rotation and scale left unset.
    /// </summary>
    public class NullTargetingPacket : StaticTargetingPacket
    {
        public NullTargetingPacket() : base()
        {
        }
    }
}
