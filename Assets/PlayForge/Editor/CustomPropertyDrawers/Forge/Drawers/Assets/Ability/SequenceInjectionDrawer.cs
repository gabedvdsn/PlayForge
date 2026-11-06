using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>An Inject task's injection: its kind, named without "Injection", always of some kind (ED-56).</summary>
    [CustomPropertyDrawer(typeof(ISequenceInjection))]
    public class SequenceInjectionDrawer : AbstractGenericDrawer<ISequenceInjection>
    {
        protected override bool AcceptClear(SerializedProperty prop) => false;

        protected override string GetStringValue(SerializedProperty prop, Type value) =>
            base.GetStringValue(prop, value).Replace("Injection", "").Trim();
    }
}
