using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Whose sides a side-changing effect gives its target (TW-18).</summary>
    public enum ESideSource
    {
        /// <summary>Its caster's, as they are when it lands: a mind control.</summary>
        Caster,

        /// <summary>The sides it names, as a pacify's Neutral.</summary>
        Named,
    }

    /// <summary>
    /// The side changes an entity is under (TW-18), each from an effect that lasts: the sides the highest Priority gives, a
    /// tie to the latest, or none, where its own hold.
    /// </summary>
    public sealed class SideSet
    {
        private struct Held
        {
            public object Key;
            public List<Tag> Sides;
            public int Priority;
            public long Order;
        }

        private readonly List<Held> held = new();
        private long order;

        /// <summary>How many changes it's under.</summary>
        public int Count => held.Count;

        /// <summary>
        /// Raised as the sides the changes give may have changed: those before and after, null for none, and whether the
        /// change that did it interrupts what its entity runs.
        /// </summary>
        internal Action<List<Tag>, List<Tag>, bool> Changed;

        /// <summary>The sides the changes give, or null where it's under none and its own hold.</summary>
        public List<Tag> Current
        {
            get
            {
                if (held.Count == 0) return null;
                int best = 0;
                for (int i = 1; i < held.Count; i++)
                {
                    var each = held[i];
                    var top = held[best];
                    if (each.Priority > top.Priority || each.Priority == top.Priority && each.Order > top.Order) best = i;
                }
                return held[best].Sides;
            }
        }

        /// <summary>A change taken up, by what holds it: the sides it gives, at its priority, and whether it interrupts.</summary>
        internal void Add(object key, List<Tag> sides, int priority, bool interrupts = false)
        {
            var before = Current;
            held.Add(new Held { Key = key, Sides = sides ?? new List<Tag>(), Priority = priority, Order = ++order });
            Changed?.Invoke(before, Current, interrupts);
        }

        /// <summary>A change let go, by what held it, and whether it interrupts. Whether it held one.</summary>
        internal bool Remove(object key, bool interrupts = false)
        {
            for (int i = 0; i < held.Count; i++)
            {
                if (!ReferenceEquals(held[i].Key, key)) continue;
                var before = Current;
                held.RemoveAt(i);
                Changed?.Invoke(before, Current, interrupts);
                return true;
            }
            return false;
        }

        /// <summary>Lets every change go, as its entity ends.</summary>
        internal void Clear() => held.Clear();
    }
}
