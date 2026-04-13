using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(ISequenceInjection))]
    public class SequenceInjectionDrawer : AbstractGenericDrawer<ISequenceInjection>
    {
        protected override bool AcceptOpen(SerializedProperty prop)
        {
            return false;
        }
        protected override bool AcceptAdd()
        {
            return false;
        }
        protected override bool AcceptClear(SerializedProperty prop)
        {
            return false;
        }
        protected override Type GetDefault(SerializedProperty prop)
        {
            return typeof(InterruptSequenceInjection);
        }

        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            string v = base.GetStringValue(prop, value);
            return v.Replace("Injection", "");
        }
    }
}
