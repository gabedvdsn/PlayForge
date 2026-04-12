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
        public static bool GetTargetingPacket(this ProcessDataPacket data, Tag query, out DefaultTargetingPacket packet, bool autoInterrupt = true)
        {
            packet = null;
            if (data is null)
            {
                if (autoInterrupt) Interrupt();
                return false;
            }

            // Look up the transform packet directly using the provided tag.
            var target = data.GetPrimary<AbstractTargetingPacket>(query);
            if (target is null)
            {
                if (autoInterrupt) Interrupt();
                return false;
            }

            // Convert to DefaultTransformPacket. DefaultTransformPacket.ToDefault() returns
            // itself when its transform is valid, or NullTransformPacket when null.
            // StaticTransformPacket.ToDefault() returns a NullTransformPacket with its values.
            packet = target.ToDefault();
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
            if (!data.GetEffectAssets(query, out var effects)) return false;
            if (data is not AbilityDataPacket adp) return false;

            foreach (var effect in effects) target.ApplyGameplayEffect(target.GenerateEffectSpec(adp.EffectOrigin, effect));
            return true;
        }
        
        public static bool TryApplyEffects(this AbilityDataPacket data, ITarget target, Tag query)
        {
            if (target is null) return false;
            if (!data.GetEffectAssets(query, out var effects)) return false;

            foreach (var effect in effects) target.ApplyGameplayEffect(target.GenerateEffectSpec(data.EffectOrigin, effect));
            return true;
        }
        
        #endregion
        
        #region Asset Loader Getters
        
        public static bool GetAssets<T>(this ProcessDataPacket data, Tag query, out DataValue<T> assets)
        {
            return data.TryGet(query, out assets);
        }
        
        public static bool GetAssets(this ProcessDataPacket data, Tag query, out DataValue<BaseForgeAsset> assets)
        {
            return data.TryGet(query, out assets);
        }
        
        public static bool GetEffectAssets(this ProcessDataPacket data, Tag query, out DataValue<GameplayEffect> assets)
        {
            return data.TryGet(query, out assets);
        }
        
        public static bool GetEntityAssets(this ProcessDataPacket data, Tag query, out DataValue<EntityIdentity> assets)
        {
            return data.TryGet(query, out assets);
        }
        
        public static bool GetAbilityAssets(this ProcessDataPacket data, Tag query, out DataValue<Ability> assets)
        {
            return data.TryGet(query, out assets);
        }
        
        public static bool GetAttributeAssets(this ProcessDataPacket data, Tag query, out DataValue<Attribute> assets)
        {
            return data.TryGet(query, out assets);
        }
        
        public static bool GetAttributeSetAssets(this ProcessDataPacket data, Tag query, out DataValue<AttributeSet> assets)
        {
            return data.TryGet(query, out assets);
        }
        
        public static bool GetTagAssets(this ProcessDataPacket data, Tag query, out DataValue<Tag> assets)
        {
            return data.TryGet(query, out assets);
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
