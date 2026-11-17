using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What a pooled cue object does as its pool hands it out and takes it back, as Unreal's cue actors recycle: a
    /// MonoBehaviour on the prefab resets itself here, as a particle system that plays again from the start.
    /// </summary>
    public interface IPooledCue
    {
        /// <summary>As its pool hands it out, placed and active.</summary>
        void Taken();

        /// <summary>As it goes back to its pool, before it's put away.</summary>
        void Returned();
    }

    /// <summary>
    /// The pools pooled Prefab cues take their objects from: one per prefab, its idle objects put away under a root
    /// that outlives a scene, in place of an object made and destroyed each time a cue plays.
    /// </summary>
    public static class ForgeCuePool
    {
        private static readonly Dictionary<GameObject, Stack<GameObject>> idle = new();
        private static GameObject root;
        private static Runner runner;

        // Which pool an object it made belongs to, and whether it's out
        private sealed class Member : MonoBehaviour
        {
            public GameObject Prefab;
            public bool Out;
        }

        // Gives back each object whose time has come, as a burst's after its Lifetime
        private sealed class Runner : MonoBehaviour
        {
            public readonly List<(GameObject Made, float At)> Due = new();

            private void Update()
            {
                for (int i = Due.Count - 1; i >= 0; i--)
                {
                    var (made, at) = Due[i];
                    if (made != null && ForgeTime.Time < at) continue;
                    Due.RemoveAt(i);
                    Give(made);
                }
            }
        }

        // Where idle objects wait: made the first time it's needed, kept past a scene's end while the game plays
        private static Transform Root()
        {
            if (root != null) return root.transform;
            root = new GameObject("PlayForge Cue Pool");
            if (Application.isPlaying) Object.DontDestroyOnLoad(root);
            runner = root.AddComponent<Runner>();
            return root.transform;
        }

        /// <summary>
        /// An object of the prefab, from its pool where one waits there, else made; placed, and active.
        /// </summary>
        public static GameObject Take(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null) return null;
            GameObject made = null;
            // One destroyed while it waited, as with its parent, is skipped
            if (idle.TryGetValue(prefab, out var waiting))
                while (made == null && waiting.Count > 0) made = waiting.Pop();
            if (made == null) made = Make(prefab);

            made.transform.SetParent(parent, false);
            made.transform.SetPositionAndRotation(position, rotation);
            made.GetComponent<Member>().Out = true;
            made.SetActive(true);
            Tell(made, taken: true);
            return made;
        }

        // An object of the prefab, made for its pool, inactive
        private static GameObject Make(GameObject prefab)
        {
            var made = Object.Instantiate(prefab, Root());
            made.SetActive(false);
            made.AddComponent<Member>().Prefab = prefab;
            return made;
        }

        /// <summary>Gives an object back to its prefab's pool; one that came from none is destroyed.</summary>
        public static void Give(GameObject made)
        {
            if (made == null) return;
            var member = made.GetComponent<Member>();
            if (member == null || member.Prefab == null)
            {
                Object.Destroy(made);
                return;
            }
            // Given back once, however many times it's handed back
            if (!member.Out) return;
            member.Out = false;
            Tell(made, taken: false);
            made.SetActive(false);
            made.transform.SetParent(Root(), false);
            if (!idle.TryGetValue(member.Prefab, out var waiting)) idle[member.Prefab] = waiting = new Stack<GameObject>();
            waiting.Push(made);
        }

        /// <summary>Gives an object back after a number of seconds, as a burst's prefab after its Lifetime.</summary>
        public static void GiveAfter(GameObject made, float seconds)
        {
            if (made == null) return;
            if (made.GetComponent<Member>() == null)
            {
                Object.Destroy(made, seconds);
                return;
            }
            Root();
            runner.Due.Add((made, ForgeTime.Time + seconds));
        }

        /// <summary>
        /// Makes objects of the prefab up to a number waiting in its pool, so a fight's first hit doesn't hitch.
        /// </summary>
        public static void Preallocate(GameObject prefab, int count)
        {
            if (prefab == null || count <= 0) return;
            if (!idle.TryGetValue(prefab, out var waiting)) idle[prefab] = waiting = new Stack<GameObject>();
            while (waiting.Count < count) waiting.Push(Make(prefab));
        }

        /// <summary>How many of the prefab's objects wait in its pool.</summary>
        public static int Idle(GameObject prefab)
        {
            if (prefab == null || !idle.TryGetValue(prefab, out var waiting)) return 0;
            int count = 0;
            foreach (var made in waiting)
                if (made != null) count++;
            return count;
        }

        /// <summary>Destroys every object waiting in every pool.</summary>
        public static void Clear()
        {
            foreach (var waiting in idle.Values)
                foreach (var made in waiting)
                    if (made != null) Object.Destroy(made);
            idle.Clear();
            if (runner != null) runner.Due.Clear();
        }

        // Each reset on the object hears it; one that throws is reported, and the rest hear on
        private static void Tell(GameObject made, bool taken)
        {
            foreach (var each in made.GetComponentsInChildren<IPooledCue>(true))
            {
                try
                {
                    if (taken) each.Taken();
                    else each.Returned();
                }
                catch (Exception exception)
                {
                    ForgeLog.Exception(ForgeLog.ESystem.Effects, exception);
                }
            }
        }
    }
}
