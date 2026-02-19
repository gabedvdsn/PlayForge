
using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(AbstractMonoProcessInstantiator), true)]
    public class ProcessInstantiatorDrawer : AbstractGenericDrawer<AbstractMonoProcessInstantiator>
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
            return null;
        }
    }
}
