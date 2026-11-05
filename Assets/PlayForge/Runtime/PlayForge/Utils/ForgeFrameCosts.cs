using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine.Profiling;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// One of a frame's costliest processes in the Profiler's capture: its process ID, and the time its steps took in the
    /// frame, every timing's (FEAT-18).
    /// </summary>
    [Serializable]
    public struct ForgeProcessCost
    {
        /// <summary>The process's ID, its cache index.</summary>
        public int Process;

        /// <summary>The time its steps took in the frame, in nanoseconds.</summary>
        public long Nanoseconds;
    }

    /// <summary>
    /// A frame's costliest processes, which go into the Profiler's capture as the frame's metadata while the Profiler
    /// records, for the PlayForge Processes module to list (FEAT-18, Decision 32). The costs go under <see cref="Tag"/>,
    /// the costliest first, and each one's type's name, in UTF-8, under the tags after it in the same order, so a saved
    /// capture still names them. The scene's ProcessControl puts them in as its frame ends; while the Profiler doesn't
    /// record, nothing is timed for them.
    /// </summary>
    public static class ForgeFrameCosts
    {
        /// <summary>The ID PlayForge's frame metadata goes under.</summary>
        public static readonly Guid MetaDataId = new("c86cc568-1368-4fd1-883e-850bbf4784b8");

        /// <summary>The tag a frame's costs go under. The type's name of the cost at i goes under the tag 1 + i after it.</summary>
        public const int Tag = 0;

        /// <summary>How many of a frame's costliest processes go into its metadata.</summary>
        public const int Most = 10;

        /// <summary>Whether the Profiler recorded as the last frame ended, so this frame's steps are timed.</summary>
        public static bool Recording { get; private set; }

        /// <summary>The last recorded frame's costliest processes, the costliest first.</summary>
        public static IReadOnlyList<ForgeProcessCost> LastFrame => costs;

        // The frame's number, by which a block knows the time it holds is an earlier frame's
        internal static int Frame { get; private set; }

        // The blocks that stepped in the frame, each once; the costliest of them, in order; and each type's name
        private static readonly List<ProcessControlBlock> stepped = new();
        private static readonly ProcessControlBlock[] top = new ProcessControlBlock[Most];
        private static readonly List<ForgeProcessCost> costs = new(Most);
        private static readonly Dictionary<Type, byte[]> names = new();
        private static readonly byte[] unnamed = Encoding.UTF8.GetBytes("Unknown");

        // A step's time, added to its block's for the frame
        internal static void Stepped(ProcessControlBlock pcb, long ticks)
        {
            if (pcb.CostFrame != Frame)
            {
                pcb.CostFrame = Frame;
                pcb.FrameTicks = 0;
                stepped.Add(pcb);
            }
            pcb.FrameTicks += ticks;
        }

        // As the frame ends: its costliest go into the capture while the Profiler records, and the next starts afresh
        internal static void EndFrame()
        {
            if (Recording && stepped.Count > 0) Emit();
            stepped.Clear();
            Frame++;
            Recording = Profiler.enabled;
        }

        private static void Emit()
        {
            // The costliest, kept in order as each comes, so nothing is sorted or allocated
            int count = 0;
            foreach (var pcb in stepped)
            {
                if (count == Most && pcb.FrameTicks <= top[Most - 1].FrameTicks) continue;
                int at = count < Most ? count++ : Most - 1;
                while (at > 0 && top[at - 1].FrameTicks < pcb.FrameTicks)
                {
                    top[at] = top[at - 1];
                    at--;
                }
                top[at] = pcb;
            }

            costs.Clear();
            for (int i = 0; i < count; i++)
                costs.Add(new ForgeProcessCost { Process = top[i].CacheIndex, Nanoseconds = top[i].FrameTicks * 1_000_000_000L / Stopwatch.Frequency });
            Profiler.EmitFrameMetaData(MetaDataId, Tag, costs);
            for (int i = 0; i < count; i++)
            {
                Profiler.EmitFrameMetaData(MetaDataId, Tag + 1 + i, NameOf(top[i].ProcessType));
                top[i] = null;
            }
        }

        private static byte[] NameOf(Type type)
        {
            if (type == null) return unnamed;
            if (!names.TryGetValue(type, out var bytes)) names[type] = bytes = Encoding.UTF8.GetBytes(type.Name);
            return bytes;
        }
    }
}
