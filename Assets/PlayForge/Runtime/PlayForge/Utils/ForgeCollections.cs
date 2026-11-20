using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Helpers for lists and dictionaries, all extension methods: a random choice, a shuffle and a weighted sample, by
    /// <see cref="ForgeRandom"/>; adds and removes that check the key first; and checks over a sequence.
    /// </summary>
    public static class ForgeCollections
    {
        public static T RandomChoice<T>(this List<T> list) => list is null ? default : list[Mathf.FloorToInt(ForgeRandom.Value * list.Count)];

        public static void SafeAdd<K, V>(this Dictionary<K, V> dict, K key, V value, bool overrideValue)
        {
            if (!dict.ContainsKey(key)) dict[key] = value;
            else if (overrideValue) dict[key] = value;
        }
        
        public static void SafeAdd<K, V>(this Dictionary<K, List<V>> dict, K key, V value)
        {
            if (dict.ContainsKey(key)) dict[key].Add(value);
            else dict[key] = new List<V>() { value };
        }
        
        public static void SafeAdd<K, V>(this Dictionary<K, HashSet<V>> dict, K key, V value)
        {
            if (dict.ContainsKey(key)) dict[key].Add(value);
            else dict[key] = new HashSet<V>() { value };
        }

        public static void SafeAddRange<K, V>(this Dictionary<K, List<V>> dict, K key, List<V> values)
        {
            if (dict.ContainsKey(key)) dict[key].AddRange(values);
            else dict[key] = values;
        }

        public static bool SafeRemove<K, V>(this Dictionary<K, V> dict, K key)
        {
            return dict.Remove(key);
        }

        public static bool SafeRemove<K, V>(this Dictionary<K, List<V>> dict, K key, V value)
        {
            return dict.ContainsKey(key) && dict[key].Remove(value);
        }

        public static void Shuffle<T>(this List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Mathf.FloorToInt(ForgeRandom.Value * (n + 1));
                (list[k], list[n]) = (list[n], list[k]);
            }
        }

        public static List<T> WeightedSample<T>(this List<T> list, int N, List<float> weights)
        {
            float weightSum = weights.Sum();
            List<float> normalizedWeights = weights.Select(w => w / weightSum).ToList();
            List<T> selected = new List<T>();

            for (int _ = 0; _ < N; _++)
            {
                float randomValue = ForgeRandom.Value;
                float cumulative = 0f;

                for (int i = 0; i < list.Count; i++)
                {
                    cumulative += normalizedWeights[i];
                    if (randomValue <= cumulative)
                    {
                        selected.Add(list[i]);
                        break;
                    }
                }
            }

            return selected;
        }

        public static bool ContainsAll<T>(this IEnumerable<T> list, IEnumerable<T> match)
        {
            return match.All(list.Contains);
        }

        public static bool TrueForAny<T>(this IEnumerable<T> list, Predicate<T> condition)
        {
            foreach (var i in list)
            {
                if (condition?.Invoke(i) ?? false) return true;
            }

            return false;
        }
    }
}
