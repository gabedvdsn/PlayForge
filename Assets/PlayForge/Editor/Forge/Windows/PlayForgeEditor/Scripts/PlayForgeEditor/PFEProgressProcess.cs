using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeEditor
    {
        enum EProgressLock
        {
            None,
            All,
            Editing,
            ReadWrite
        }

        static string ProgressLockMessage(EProgressLock @lock)
        {
            return @lock switch
            {
                EProgressLock.None => "",
                EProgressLock.All => "All activity",
                EProgressLock.Editing => "Data editing",
                EProgressLock.ReadWrite => "Read/write",
                _ => throw new ArgumentOutOfRangeException(nameof(@lock), @lock, null)
            };
        }

        private class ProgressPacket : IConsoleMessenger
        {
            public void Trace(ConsoleEntry ce, PlayForgeEditor editor, bool inOut)
            {
                
            }
            public bool HasTrace(ConsoleEntry ce, PlayForgeEditor editor)
            {
                throw new NotImplementedException();
            }
            public void Link(ConsoleEntry ce, PlayForgeEditor editor)
            {
                
            }
            public bool HasLink(ConsoleEntry ce, PlayForgeEditor editor)
            {
                throw new NotImplementedException();
            }
            public bool CanResolve(ConsoleEntry ce, PlayForgeEditor editor)
            {
                return false;
            }
        }
        
        private bool ProgressLocked = false;
        private EProgressLock ProgressLock;
        
        private bool DoTaskProcess(EProgressLock @lock, string _title, Func<Action<float>, UniTask> task, Action<float> onProgress = null, Action onFinish = null, bool runout = true, string runoutTitle = "Complete", bool alertVis = true)
        {
            if (ProgressLocked) return false;
            
            if (alertVis)
            {
                ToggleProgressBar(true, _title);
                SetProgressBar();
            }
            
            var _onProgress = new Action<float>(v =>
            {
                onProgress?.Invoke(v);
                if (alertVis) SetProgressBar(v);
            });

            var _onFinish = new Action(() =>
            {
                onFinish?.Invoke();
                if (alertVis && !runout) ToggleProgressBar(false);
            });

            DoTaskProcessAsync(@lock, task, _onProgress, _onFinish, runout && alertVis, runoutTitle).Forget();

            return true;
        }

        async UniTask DoTaskProcessAsync(EProgressLock @lock, Func<Action<float>, UniTask> task, Action<float> onProgress, Action onFinish, bool runout, string runoutTitle)
        {
            if (task is null) return;

            ProgressLock = @lock;
            ProgressLocked = true;
            
            cts?.Cancel();
            cts?.Dispose();

            cts = new CancellationTokenSource();
            token = cts.Token;

            LogConsoleEntry(Console.Process.ProcessBegin(@lock));
            
            await task.Invoke(onProgress);

            LogConsoleEntry(Console.Process.ProcessEnd(@lock));
            
            onFinish?.Invoke();
            
            ProgressLock = EProgressLock.None;
            ProgressLocked = false;

            if (!runout)
            {
                cts?.Cancel();
                cts?.Dispose();
                cts = null;
                return;
            }

            await DoProgressBarRunout(runoutTitle);
            
            cts?.Cancel();
            cts?.Dispose();

            cts = null;
        }
        
        const int runoutDuration = 3000;

        async UniTask DoProgressBarRunout(string runoutTitle, int duration = runoutDuration)
        {
            progressBar.title = runoutTitle;
            await UniTask.Delay(duration, cancellationToken: token);
            ToggleProgressBar(false);
        }

        private VisualElement progressRoot;
        private ProgressBar progressBar;
        private Label progressLabel;
        
        private bool progressBarActive = false;
        private int progress_visDotsEveryFrames = 150;
        private string progress_visDots = "";
        private int progress_visFrames = 0;
        private string progress_title;
        
        private int maxConcurrencyThreads = 0;
        private CancellationTokenSource cts;
        private CancellationToken token;
        
        void BindProgress()
        {
            progressRoot = projectViewRoot.Q("ProgressBar");
            progressBar = progressRoot.Q<ProgressBar>("Progress");
            progressLabel = progressRoot.Q<Label>("ProgressTitle");
        }
        
        void BuildProgress()
        {
            ToggleProgressBar(false);
        }

        void ToggleProgressBar(bool flag, string _title = null)
        {
            progressBarActive = flag;
            progressRoot.style.display = progressBarActive ? DisplayStyle.Flex : DisplayStyle.None;
            progress_title = _title ?? "Untitled";
        }
        
        private void SetProgressBar(float value = 0f)
        {
            progressBar.value = value;
            progressBar.title = $"{(value * 100):00}%";
            
            progress_visFrames += 1;
            if (progress_visFrames >= progress_visDotsEveryFrames)
            {
                progress_visDots += ".";
                if (progress_visDots.Length > 3) progress_visDots = "";
                progressLabel.text = progress_title + progress_visDots;
                progress_visFrames = 0;
            }
        }
        
        void TestProgressBar()
        {
            SetNavigationPermit(EForgeContextExpanded.All, false, "All activity is suspended while progress is being tested.");
            DoTaskProcess(EProgressLock.All, "Testing Progress Bar", DoProgressTest, runoutTitle: "Testing Complete",
                onFinish: () => { SetNavigationPermit(EForgeContextExpanded.All, true); });
        }

        async UniTask DoProgressTest(Action<float> onProgress)
        {
            var progress = Progress<float>.Create(onProgress);

            float elapsed = 0f;
            float duration = 750f;

            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                elapsed += Time.deltaTime;
                float p = elapsed / duration;
                progress.Report(p);
            }
        }

        class Progress<T>
        {
            private Action<T> onProgress;
            private T value;
            public T Value => value;
            
            public static Progress<T> Create(Action<T> onProgress, bool initReport = true)
            {
                var p = new Progress<T>
                {
                    onProgress = onProgress
                };
                
                if (initReport) p.Report(default);
                
                return p;
            }

            public void Report(T v)
            {
                value = v;
                onProgress?.Invoke(value);
            }
        }
    }
}
