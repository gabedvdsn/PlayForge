using UnityEditor;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// An ability's Recharge (TW-17): its kind, named without "Recharge", as Each Charge or All At Once, or one of your own,
    /// then the kind's own fields. A recharge is always of some kind, so the picker offers no None.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractChargeRecharge))]
    public class ChargeRechargeDrawer : AbstractTypeRefDrawer<AbstractChargeRecharge>
    {
        protected override string CleanTypeName(string typeName) =>
            typeName.EndsWith("Recharge") && typeName.Length > "Recharge".Length ? typeName.Substring(0, typeName.Length - "Recharge".Length) : typeName;

        protected override bool AcceptClear(SerializedProperty prop) => false;
    }
}
