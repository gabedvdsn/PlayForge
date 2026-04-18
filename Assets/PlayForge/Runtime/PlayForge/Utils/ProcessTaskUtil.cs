using System;
using System.Diagnostics.Tracing;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    public static class ProcessTaskUtil
    {
        #region Ability Runtime Helpers
        
        /// <summary>
        /// Retrieves an AbstractTransformPacket from the data packet at the given tag
        /// and converts it to a DefaultTransformPacket via ToDefault().
        /// Returns false and optionally interrupts the sequence if the target is missing or invalid.
        /// </summary>
        public static bool GetTargetingPacket(this ProcessDataPacket data, Tag query, out AbstractTargetingPacket packet, bool autoInterrupt = true)
        {
            packet = null;
            if (data is null)
            {
                if (autoInterrupt) Interrupt();
                return false;
            }

            packet = data.GetPrimary<AbstractTargetingPacket>(query);
            if (packet is not null) return true;
            
            if (autoInterrupt) Interrupt();
            return false;

            void Interrupt()
            {
                if (data is SequenceDataPacket seq)
                {
                    seq.Interrupt();
                }
            }
        }
        
        public static void SetTargetingPacket(this ProcessDataPacket data, Tag query, AbstractTargetingPacket targeting)
        {
            data.SetPrimary(query, targeting);
        }

        public static bool TryApplyEffects(this ProcessDataPacket data, GameObject obj, Tag query)
        {
            return obj.TryGetComponent<ITarget>(out var target) && data.TryApplyEffects(target, query);
        }
        
        public static bool TryApplyEffects(this ProcessDataPacket data, ITarget target, Tag query)
        {
            if (target is null) return false;
            if (!data.TryGetLoadedAssets<GameplayEffect>(query, out var effects)) return false;
            if (data is not AbilityDataPacket adp) return false;

            foreach (var effect in effects) target.ApplyGameplayEffect(target.GenerateEffectSpec(adp.EffectOrigin, effect));
            return true;
        }
        
        public static bool TryApplyEffects(this AbilityDataPacket data, ITarget target, Tag query)
        {
            if (target is null) return false;
            if (!data.TryGetLoadedAssets<GameplayEffect>(query, out var effects)) return false;

            foreach (var effect in effects) target.ApplyGameplayEffect(target.GenerateEffectSpec(data.EffectOrigin, effect));
            return true;
        }

        public static DataValue<T> GetLoadedAssets<T>(this ProcessDataPacket data, Tag query)
        {
            return data.TryGetLoadedAssets<T>(query, out var assets) ? assets : new DataValue<T>();
        }
        
        public static bool TryGetLoadedAssets<T>(this ProcessDataPacket data, Tag query, out DataValue<T> assets)
        {
            return data.TryGetAll(query, out assets);
        }
        
        #endregion
        
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
