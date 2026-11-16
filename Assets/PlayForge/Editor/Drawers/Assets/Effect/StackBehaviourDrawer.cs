using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>
    /// A stack behaviour, as an effect's Stack Behaviours list holds it: its kind, picked from the behaviours there are,
    /// then its fields, folding to a line of their values, as a worker is drawn. The behaviours configure a
    /// stacking container, a type apart from the one the effect behaviour drawer takes, so they had no drawer: Unity's own
    /// field gave a new element no kind to pick, and none could be added. Each is named for what it does.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractStackingConfig), true)]
    public class StackBehaviourDrawer : AbstractGenericDrawer<AbstractStackingConfig>
    {
        // Stack Amount Operation shared its name with the duration's own Stack Amount, which it scales
        protected override string CleanTypeName(string name) => name switch
        {
            nameof(StackBoundsConfig) => "Stack Bounds",
            nameof(StackAmountOperation) => "Scaled Stack Amount",
            nameof(InitializationOperation) => "Starting Stacks",
            _ => base.CleanTypeName(name),
        };
    }
}
