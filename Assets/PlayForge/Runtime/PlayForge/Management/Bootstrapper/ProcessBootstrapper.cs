using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Brings ProcessControl up as a scene starts, for a scene of processes and task sequences with no entities. It names
    /// a ProcessControl prefab; without one it makes one itself, so a scene needs nothing but a Process Bootstrapper. A
    /// scene with entities takes the Bootstrapper instead, which brings up the GameRoot as well.
    /// </summary>
    public class ProcessBootstrapper : MonoBehaviour
    {
        [Tooltip("The ProcessControl it brings up. None, one it makes")]
        public ProcessControl ProcessControlPrefab;

        [Tooltip("Or in Start() when false")]
        public bool BootstrapOnAwake = true;

        private void Awake()
        {
            if (BootstrapOnAwake) Bootstrap();
        }

        private void Start()
        {
            if (!BootstrapOnAwake) Bootstrap();
        }

        private void Bootstrap()
        {
            TagRegistry.Initialize();
            ProcessControl.BringUp(ProcessControlPrefab);

            BootstrapOverrides();

            ProcessControl.Instance.DeferredInit();
            Destroy(gameObject);
        }

        protected virtual void BootstrapOverrides()
        {
            // Any further bootstrap initialization here
        }
    }
}
