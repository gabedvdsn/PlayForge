using System.Collections.Generic;
using Time = UnityEngine.Time;

namespace FarEmerald.PlayForge
{
    public static class TimeUtility
    {

        private static Dictionary<Tag, float> timers = new();
        
        public static Tag StartTimer()
        {
            var tag = Tags.Get("AnonymousTimer");
            StartTimer(tag);
            return tag;
        }
        
        public static void StartTimer(Tag id, bool scaled = true)
        {
            timers[id] = scaled ? Time.time : Time.unscaledTime;
        }

        public static bool Get(Tag id, out float time)
        {
            return timers.TryGetValue(id, out time);
        }

        public static bool End(Tag id, out float time)
        {
            if (!Get(id, out time)) return false;
            
            timers.Remove(id);
            return true;

        }
    }
}
