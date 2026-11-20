using UnityEditor;

namespace FarEmerald.PlayForge.Editor
{
    // A Scalable number in a generic drawer's folded line, summed up as its own field sums it up, as "Uses 3"
    public abstract partial class AbstractGenericDrawer<T>
    {
        static partial void SummarizeScalable(SerializedProperty prop, string label, ref bool scalable, ref string summary)
        {
            if (prop.propertyType != SerializedPropertyType.Generic || prop.type is not (nameof(ScalableFloat) or nameof(ScalableInt))) return;
            scalable = true;
            string number = MagnitudeField.SummaryOf(prop, "");
            summary = string.IsNullOrEmpty(number) ? null : $"{label} {number}";
        }
    }
}
