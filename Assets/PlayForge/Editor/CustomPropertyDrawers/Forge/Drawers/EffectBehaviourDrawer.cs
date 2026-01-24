using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(AbstractIntrinsicEffectBehaviour), true)]
    public class EffectBehaviourDrawer : AbstractGenericDrawer<AbstractIntrinsicEffectBehaviour>
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
