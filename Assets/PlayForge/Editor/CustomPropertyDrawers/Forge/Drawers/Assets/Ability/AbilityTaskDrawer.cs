using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An ability task: its kind, as "Delay" for a DelayTask, then the kind's own fields (ED-56). The base drawer names a
    /// kind without "AbilityTask" or "Task", and builds the fields again as the kind changes, which this drawer did itself.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractAbilityTask))]
    public class AbilityTaskDrawer : AbstractTypeRefDrawer<AbstractAbilityTask>
    {
    }
}
