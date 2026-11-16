using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    /// <summary>An effect's behaviour: its kind, then its fields, folding to a line of their values.</summary>
    [CustomPropertyDrawer(typeof(AbstractEffectConfig<AbstractEffectContainer>), true)]
    public class EffectBehaviourDrawer : AbstractGenericDrawer<AbstractEffectConfig<AbstractEffectContainer>>
    {
    }
}
