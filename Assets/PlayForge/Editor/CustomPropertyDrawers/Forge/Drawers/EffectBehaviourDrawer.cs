using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(AbstractEffectConfig<AbstractEffectContainer>), true)]
    public class EffectBehaviourDrawer : AbstractGenericDrawer<AbstractEffectConfig<AbstractEffectContainer>>
    {
        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return false;
        }
        protected override bool AcceptAdd()
        {
            return false;
        }
    }
}
