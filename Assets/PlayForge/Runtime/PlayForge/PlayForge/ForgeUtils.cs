using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    public static class ForgeUtils
    {
        public static bool TryGet(this List<DataWrapper> src, Tag target, EDataWrapperType type, out DataWrapper data)
        {
            foreach (var d in src)
            {
                if (d.Key == target && d.Type == type)
                {
                    data = d;
                    return true;
                }
            }

            data = default;
            return false;
        }

        public static int IndexMin<T>(this IList<T> list, Func<T, float> selector)
        {
            int minIndex = 0;
            float minValue = float.MinValue;
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
