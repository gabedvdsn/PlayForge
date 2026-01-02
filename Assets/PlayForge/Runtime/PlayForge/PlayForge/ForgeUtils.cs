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
    }
}
