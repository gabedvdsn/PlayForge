using System;
using System.Threading;
using Cysharp.Threading.Tasks;

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
        
        #endregion
    }
}
