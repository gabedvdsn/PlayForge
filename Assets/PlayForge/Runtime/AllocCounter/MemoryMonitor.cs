using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace FarEmerald
{
    public class MemoryMonitor : MonoBehaviour
    {
        public const int BYTES_TO_MB = 1024 * 1024;
        
        public TMPro.TextMeshProUGUI allocatedRamText, reservedRamText, monoRamText, gcCountText;
        public UnityEngine.UI.RawImage memoryGraphImage;
        public int historyLength = 300, graphHeight = 100;

        public Color32 allocatedColor = new Color32(0, 255, 0, 255),
            monoColor = new(0, 150, 255, 255),
            reservedColor = new Color32(200, 200, 200, 255),
            gcEventColor = new Color32(255, 0, 0, 255);

        private static readonly Color32 backgroundColor = new Color32(0, 0, 0, 255);

        private CircularBuffer<long> allocated, reserved, mono, gcAlloc;
        private CircularBuffer<bool> gcEvents;
        private Texture2D graphTexture;
        private Color32[] pixels;

        private Recorder rec;
        private int lastGcCount;

        private long lastMemUsed, maxReserved;

        private void Start()
        {
            allocated = new(historyLength);
            reserved = new(historyLength);
            mono = new(historyLength);
            gcAlloc = new(historyLength);
            gcEvents = new(historyLength);

            rec = Recorder.Get("GC.Alloc");
            rec.enabled = false;
            rec.FilterToCurrentThread();
            rec.enabled = true;

            graphTexture = new Texture2D(historyLength, graphHeight + 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            pixels = new Color32[graphTexture.width * graphTexture.height];
            memoryGraphImage.texture = graphTexture;
        }

        private void Update()
        {
            long allocBytes = Profiler.GetTotalAllocatedMemoryLong();
            long resBytes = Profiler.GetTotalReservedMemoryLong();
            long monoBytes = Profiler.GetMonoUsedSizeLong();
            long gcAllocs = rec.sampleBlockCount;

            int gcCount = GC.CollectionCount(0);
            bool gcHappened = gcCount != lastGcCount;
            lastGcCount = gcCount;
            
            UpdateText(allocatedRamText, allocBytes / BYTES_TO_MB, allocatedColor);
            UpdateText(reservedRamText, resBytes / BYTES_TO_MB, reservedColor);
            UpdateText(monoRamText, monoBytes / BYTES_TO_MB, monoColor);
            UpdateText(gcCountText, gcCount, gcHappened ? Color.red : gcEventColor);

            if (resBytes > maxReserved) maxReserved = resBytes;
            
            allocated.Enqueue(allocBytes);
            reserved.Enqueue(resBytes);
            mono.Enqueue(monoBytes);
            gcAlloc.Enqueue(gcAllocs);
            gcEvents.Enqueue(gcHappened);

            DrawGraph();
        }

        void UpdateText(TMPro.TextMeshProUGUI text, float value, Color color)
        {
            if (!text) return;
            text.text = $"{value:F1} MB";
            text.color = color;
        }

        void DrawGraph()
        {
            if (!graphTexture) return;

            Array.Fill(pixels, backgroundColor);
            int width = graphTexture.width;

            for (int i = 0; i < allocated.Count; i++)
            {
                int x = i;
                float scale = graphHeight / (float)Math.Max(maxReserved, 1);
                int hMono = (int)(mono[i] * scale);
                int hAlloc = (int)(allocated[i] * scale);
                int hRes = (int)(reserved[i] * scale);

                for (int y = 0; y < hRes; y++)
                {
                    int idx = x + y * width;
                    pixels[idx] = y < hMono ? monoColor : y < hAlloc ? allocatedColor : reservedColor;
                }

                if (gcEvents[i])
                {
                    for (int y = 0; y < graphHeight; y++)
                    {
                        pixels[x + y * width] = gcEventColor;
                    }
                }
            }

            if (gcEvents.Count > 2 && gcEvents[^1])
            {
                long monoDiff = mono[^2] - mono[^1];
                // Debug.Log($"GC Event detected! Mono memory change: {monoDiff / BYTES_TO_MB:F2} MB");
            }

            graphTexture.SetPixels32(pixels);
        }
    }
}
