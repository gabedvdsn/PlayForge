using System;
using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A scaler's behaviour: its kind, named without "ScalerBehaviour" or "Behaviour", then the kind's own fields (ED-56).
    /// The picker offers no None: a list's "−" removes a behaviour. An empty slot stays empty as it's drawn (ED-12).
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractScalerBehaviour), true)]
    public class ScalerBehaviourDrawer : AbstractTypeRefDrawer<AbstractScalerBehaviour>
    {
        protected override string GetStringValue(SerializedProperty prop, Type value)
        {
            if (value is null) return base.GetStringValue(prop, value);
            var name = value.Name.Replace("ScalerBehaviour", "").Replace("Behaviour", "");
            return ObjectNames.NicifyVariableName(name);
        }

        protected override bool AcceptClear(SerializedProperty prop) => false;
    }
}
