using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public static class TaskUtil
    {
        #region UniTask Helpers

        public static async UniTask WhileAsync(Func<bool> condition, CancellationToken token, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            while (condition())
            {
                token.ThrowIfCancellationRequested();
                await UniTask.Yield(timing, token);
            }
        }

        public static async UniTask DoWhileAsync(Func<UniTask> body, Func<bool> condition, CancellationToken token, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            do
            {
                token.ThrowIfCancellationRequested();
                await body();
                await UniTask.Yield(timing, token);
            } while (condition());
        }

        public static async UniTask DoWhile(Func<UniTask> body, float duration, CancellationToken token, bool useUnscaledTime = false, PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            float elapsedDuration = 0f;
            do
            {
                token.ThrowIfCancellationRequested();

                float time = useUnscaledTime ? Time.unscaledTime : Time.time;
                await body();
                await UniTask.Yield(timing, token);
                float elapsed = time - (useUnscaledTime ? Time.unscaledTime : Time.time);
                elapsedDuration += elapsed;

            } while (elapsedDuration < duration);
        }
        
        #endregion
    }
}
