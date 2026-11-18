using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Extension methods for a list: <c>IndexMin</c> and <c>IndexMax</c> give the index of the element whose selected
    /// value is lowest or highest, the first where several tie, and 0 for an empty list.
    /// </summary>
    public static class ForgeUtils
    {
        public static int IndexMin<T>(this IList<T> list, Func<T, float> selector)
        {
            int minIndex = 0;
            // The search starts above every value. It started at the lowest float, so nothing was below it and the first
            // always won: Use Minimum took the first element
            float minValue = float.MaxValue;
            for (int i = 0; i < list.Count; i++)
            {
                float value = selector(list[i]);
                if (value < minValue)
                {
                    minIndex = i;
                    minValue = value;
                }
            }

            return minIndex;
        }
        
        public static int IndexMax<T>(this IList<T> list, Func<T, float> selector)
        {
            int maxIndex = 0;
            float maxValue = float.MinValue;
            for (int i = 0; i < list.Count; i++)
            {
                float value = selector(list[i]);
                if (value > maxValue)
                {
                    maxIndex = i;
                    maxValue = value;
                }
            }

            return maxIndex;
        }
    }
}
