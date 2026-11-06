using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>A process's instantiator: its kind, or None, then its fields (ED-56).</summary>
    [CustomPropertyDrawer(typeof(AbstractMonoProcessInstantiator), true)]
    public class ProcessInstantiatorDrawer : AbstractGenericDrawer<AbstractMonoProcessInstantiator>
    {
    }
}
