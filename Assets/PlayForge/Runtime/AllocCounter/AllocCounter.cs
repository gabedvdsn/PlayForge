using System;
using UnityEngine.Profiling;

namespace FarEmerald
{
    public class AllocCounter
    {
        public const int BYTES_TO_MB = 1024 * 1024;
        
        private UnityEngine.Profiling.Recorder rec;

        public AllocCounter()
        {
            rec = Recorder.Get("GC.Alloc");
            rec.enabled = false;
            
            #if !UNITY_WEBGL
            rec.FilterToCurrentThread();
            #endif

            rec.enabled = true;
        }

        public int Stop()
        {
            if (rec is null) throw new InvalidOperationException("AllocCounter was not started");

            rec.enabled = false;
            
#if !UNITY_WEBGL
            rec.CollectFromAllThreads();
#endif

            int result = rec.sampleBlockCount;
            rec = null;
            return result;
        }
    }

    public class CircularBuffer<T>
    {
        private readonly T[] buffer;
        private int head, tail;
        public int Count { get; private set; }
        public int Capacity => buffer.Length;

        public CircularBuffer(int size)
        {
            if (size <= 0) throw new ArgumentOutOfRangeException(nameof(size));
            buffer = new T[size];
        }

        public void Enqueue(T item)
        {
            buffer[head] = item;
            head = (head + 1) % Capacity;
            if (Count == Capacity) tail = (tail + 1) % Capacity;
            else Count++;
        }

        public T Dequeue()
        {
            if (Count == 0) throw new InvalidOperationException("Buffer is empty");
            var item = buffer[tail];
            tail = (tail + 1) % Capacity;
            Count--;
            return item;
        }

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                if (Count == 0 || buffer == null) throw new InvalidOperationException();
                return buffer[(tail + index) % Capacity];
            }
        }
    }
}
