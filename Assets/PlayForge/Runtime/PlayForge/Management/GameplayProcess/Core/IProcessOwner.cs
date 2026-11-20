using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What owns processes. ProcessControl counts a process as its owner's where the owner is the process's handler, or
    /// else its packet's <see cref="ProcessDataPacket.Source"/>, and the work done with the packet runs by the owner's
    /// clock. A position or rotation payload of an owner places a MonoBehaviour process where the owner is. An entity's
    /// ability system is one.
    /// </summary>
    public interface IProcessOwner
    {
        /// <summary>The clock the owner's processes run by. Null, as it is unless an owner gives one, for the game's.</summary>
        IForgeClock ProcessClock => null;

        /// <summary>Where the owner is, as a process placed at it takes it.</summary>
        Vector3 ProcessPosition { get; }

        /// <summary>Which way the owner faces, as a process placed at it takes it.</summary>
        Quaternion ProcessRotation { get; }
    }
}
