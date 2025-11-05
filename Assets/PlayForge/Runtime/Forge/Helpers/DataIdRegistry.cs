using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace FarEmerald.PlayForge.Extended
{
    public static class DataIdRegistry
    {
        private static HashSet<int> Reserved = new();
        private static FrameworkProject Source;

        public static void RebuildFrom(FrameworkProject source)
        {
            Source = source;

            Reserved.Clear();

            foreach (var d in Source.Abilities) Reserve(d.Id);
            foreach (var d in Source.Attributes) Reserve(d.Id);
            foreach (var d in Source.Tags) Reserve(d.Id);
            foreach (var d in Source.Effects) Reserve(d.Id);
            foreach (var d in Source.Entities) Reserve(d.Id);
            foreach (var d in Source.AttributeSets) Reserve(d.Id);
        }

        public static void Reset()
        {
            Reserved = new();
            Source = null;
        }

        public static bool Reserve(int id)
        {
            if (id <= 0) return false; // only positive IDs are permanent
            return Reserved.Add(id);
        }

        public static void Release(int id)
        {
            if (id > 0) Reserved.Remove(id);
        }

        public static int Generate()
        {
            // Generate a random positive 31-bit int (1..Int32.MaxValue)
            // Retry on the extremely rare chance of collision
            Span<byte> buf = stackalloc byte[4];
            while (true)
            {
                RandomNumberGenerator.Fill(buf);
                int candidate = BitConverter.ToInt32(buf);
                candidate &= 0x7FFFFFFF;          // make it positive
                if (candidate == 0) continue;     // avoid 0
                if (Reserved.Add(candidate))      // success
                    return candidate;
            }
        }
        
    }
}
