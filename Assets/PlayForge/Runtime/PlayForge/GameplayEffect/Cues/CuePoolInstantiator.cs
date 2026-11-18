using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Makes a MonoBehaviour process's object from the cue pool, <see cref="ForgeCuePool"/>, and gives it back as the
    /// process ends, as a pooled Process cue's: an object of the process's prefab waiting there is handed out, else one is
    /// made, placed where the process's packet says. Give it to a registration with
    /// <see cref="ProcessRegistration.InstantiatedBy"/>.
    /// </summary>
    public sealed class CuePoolInstantiator : AbstractMonoProcessInstantiator
    {
        /// <summary>The one every pooled cue shares: it keeps nothing of its own, as the pool keeps the objects.</summary>
        public static CuePoolInstantiator Shared { get; } = new();

        protected override AbstractMonoProcess PrepareNew(AbstractMonoProcess process, ProcessDataPacket data)
        {
            var position = data != null && data.TryGetFirst(Tags.POSITION, out Vector3 at) ? at : process.transform.position;
            var rotation = data != null && data.TryGetFirst(Tags.ROTATION, out Quaternion facing) ? facing : process.transform.rotation;
            var parent = data != null && data.TryGetFirst(Tags.TRANSFORM, out Transform under) ? under : null;
            var made = ForgeCuePool.Take(process.gameObject, position, rotation, parent);
            return made != null ? made.GetComponent(process.GetType()) as AbstractMonoProcess : null;
        }

        // A process already in the scene is its own object, which no pool holds
        protected override AbstractMonoProcess PrepareExisting(AbstractMonoProcess process, ProcessDataPacket data) => process;

        public override void CleanProcess(AbstractMonoProcess process)
        {
            if (process != null) ForgeCuePool.Give(process.gameObject);
        }
    }
}
