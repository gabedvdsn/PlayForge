using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What makes a mono process's object and takes it back, as a pool hands out an idle object and keeps it again as
    /// its process ends. Derive from it and override <c>PrepareNew</c>, which makes or hands out an object of the
    /// prefab registered, <c>PrepareExisting</c>, which readies one already in a scene, and <c>CleanProcess</c>, which
    /// takes the object back as its process ends. A registration's <c>InstantiatedBy</c>, a process's own
    /// <c>Instantiator</c> or its handler's gives one; with none, the object is cloned and destroyed.
    /// </summary>
    public abstract class AbstractMonoProcessInstantiator
    {
        public AbstractMonoProcess Create(AbstractMonoProcess process, ProcessDataPacket data, bool isInstantiated)
        {
            return ProcessIsSceneActive(process) 
                ? PrepareExisting(process, data) 
                : PrepareNew(process, data);
        }

        /// <summary>
        /// Instantiate a new MonoBehaviour process and return it
        /// </summary>
        /// <param name="process">The Prefab to instantiate</param>
        /// <param name="data">The runtime data with respect to the process</param>
        /// <returns></returns>
        protected abstract AbstractMonoProcess PrepareNew(AbstractMonoProcess process, ProcessDataPacket data);

        /// <summary>
        /// Regulate an already instantiated MonoBehaviour process and return it
        /// </summary>
        /// <param name="process">The instantiated MonoBehaviour process</param>
        /// <param name="data">The runtime data with respect to the process</param>
        /// <returns></returns>
        protected abstract AbstractMonoProcess PrepareExisting(AbstractMonoProcess process, ProcessDataPacket data); 

        /// <summary>
        /// Responsible for cleaning the MonoBehaviour component of the process as it exists within the scene.
        /// If the Instantiator is left null in related fields, the MonoBehaviour object will be destroyed.
        /// </summary>
        /// <param name="process">The MonoBehaviour process being cleaned</param>
        public abstract void CleanProcess(AbstractMonoProcess process);

        protected bool ProcessIsSceneActive(AbstractMonoProcess process)
        {
            return process.gameObject.scene.IsValid();
        }
    }
}
