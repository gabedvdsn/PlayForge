using UnityEngine;

namespace FarEmerald.PlayForge
{
    public abstract class AbstractMonoProcessInstantiator
    {
        public AbstractMonoProcess Create(AbstractMonoProcess process, ProcessDataPacket data, bool isInstantiated)
        {
            return process.gameObject.scene.name is not null 
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
