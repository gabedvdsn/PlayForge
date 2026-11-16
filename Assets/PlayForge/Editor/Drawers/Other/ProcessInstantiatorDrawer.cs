using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>A process's instantiator: its kind, or None, then its fields.</summary>
    [CustomPropertyDrawer(typeof(AbstractMonoProcessInstantiator), true)]
    public class ProcessInstantiatorDrawer : AbstractGenericDrawer<AbstractMonoProcessInstantiator>
    {
    }
}
