using UnityEngine;

namespace FarEmerald.PlayForge
{
    public struct ModifiedAttributeValue
    {
        public float DeltaCurrentValue;
        public float DeltaBaseValue;

        public ModifiedAttributeValue(float deltaCurrentValue, float deltaBaseValue)
        {
            DeltaCurrentValue = deltaCurrentValue;
            DeltaBaseValue = deltaBaseValue;
        }

        public ModifiedAttributeValue Combine(ModifiedAttributeValue other)
        {
            return new ModifiedAttributeValue(
                DeltaCurrentValue + other.DeltaCurrentValue, 
                DeltaBaseValue + other.DeltaBaseValue);
        }

        public ModifiedAttributeValue Negate()
        {
            return new ModifiedAttributeValue(
                -DeltaCurrentValue,
                -DeltaBaseValue
            );
        }
        
        public ModifiedAttributeValue Multiply(AttributeValue attributeValue, bool oneMinus = true)
        {
            if (oneMinus)
            {
                return new ModifiedAttributeValue(
                    DeltaCurrentValue - DeltaCurrentValue * attributeValue.CurrentValue,
                    DeltaBaseValue - DeltaBaseValue * attributeValue.BaseValue
                );
            }
            
            return new ModifiedAttributeValue(
                DeltaCurrentValue * attributeValue.CurrentValue,
                DeltaBaseValue * attributeValue.BaseValue
            );
            
        }

        public ModifiedAttributeValue Multiply(ModifiedAttributeValue modifiedAttributeValue, bool oneMinus = true)
        {
            if (oneMinus)
            {
                return new ModifiedAttributeValue(
                    DeltaCurrentValue - DeltaCurrentValue * modifiedAttributeValue.DeltaCurrentValue,
                    DeltaBaseValue - DeltaBaseValue * modifiedAttributeValue.DeltaBaseValue
                );
            }
            
            return new ModifiedAttributeValue(
                DeltaCurrentValue * modifiedAttributeValue.DeltaCurrentValue,
                DeltaBaseValue * modifiedAttributeValue.DeltaBaseValue
            );
        }

        public ESignPolicy SignPolicy => ForgeHelper.SignPolicy(DeltaCurrentValue, DeltaBaseValue);

        public AttributeValue ToAttributeValue() => new(DeltaCurrentValue, DeltaBaseValue);

        public override string ToString()
        {
            return $"[ MAV ] {DeltaCurrentValue}/{DeltaBaseValue}";
        }

        public static ModifiedAttributeValue operator -(ModifiedAttributeValue mav1, ModifiedAttributeValue mav2)
        {
            return new ModifiedAttributeValue(mav1.DeltaCurrentValue - mav2.DeltaCurrentValue,
                mav1.DeltaBaseValue - mav2.DeltaBaseValue);
        }
        public static ModifiedAttributeValue operator /(ModifiedAttributeValue mav1, float v)
        {
            return new ModifiedAttributeValue(mav1.DeltaCurrentValue / v, mav1.DeltaBaseValue / v);
        }
    }

}
