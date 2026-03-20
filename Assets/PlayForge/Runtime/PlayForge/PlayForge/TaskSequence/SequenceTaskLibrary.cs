using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Reusable async building blocks for common gameplay operations.
    /// Designed to be awaited inside TaskSequence .Task() lambdas where the
    /// caller has access to the data packet and cancellation token.
    ///
    /// Usage:
    ///   .Task(async (d, t) =>
    ///   {
    ///       var target = d.GetPrimary&lt;Transform&gt;(Tags.TARGET);
    ///       await Seq.MoveTo(target, Vector3.zero, 1.5f, t, easeCurve);
    ///   })
    ///
    /// All timed methods:
    ///   - Accept an optional AnimationCurve for easing (null = linear)
    ///   - Snap to the final value after completion to avoid float drift
    ///   - Respect CancellationToken for interrupt/skip/timeout support
    ///   - Handle zero/negative duration by snapping immediately
    /// </summary>
    public static class SequenceTaskLibrary
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // POSITION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Lerps a transform's world position to the destination over the given duration.
        /// </summary>
        public static async UniTask MoveTo(Transform target, Vector3 destination, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            var start = target.position;
            await RunLerp(duration, curve, p => target.position = Vector3.LerpUnclamped(start, destination, p), token);
            target.position = destination;
        }
        
        /// <summary>
        /// Moves a transform by an offset relative to its current position.
        /// </summary>
        public static async UniTask MoveBy(Transform target, Vector3 offset, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            var start = target.position;
            var end = start + offset;
            await RunLerp(duration, curve, p => target.position = Vector3.LerpUnclamped(start, end, p), token);
            target.position = end;
        }
        
        /// <summary>
        /// Lerps a transform's local position to the destination.
        /// </summary>
        public static async UniTask MoveLocalTo(Transform target, Vector3 destination, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            var start = target.localPosition;
            await RunLerp(duration, curve, p => target.localPosition = Vector3.LerpUnclamped(start, destination, p), token);
            target.localPosition = destination;
        }
        
        /// <summary>
        /// Moves a transform along a path of waypoints sequentially.
        /// </summary>
        public static async UniTask MoveAlongPath(Transform target, Vector3[] waypoints, float totalDuration,
            CancellationToken token, AnimationCurve curve = null)
        {
            if (waypoints == null || waypoints.Length == 0) return;
            
            float segmentDuration = totalDuration / waypoints.Length;
            foreach (var wp in waypoints)
            {
                await MoveTo(target, wp, segmentDuration, token, curve);
            }
        }
        
        /// <summary>
        /// Moves a transform in a parabolic arc from its current position to the destination.
        /// The arc adds height above the straight-line path between start and end.
        ///
        /// The optional curve parameter eases the time progression (how fast you move
        /// through the arc), NOT the arc shape itself. The parabola is always smooth.
        ///
        /// Math:
        ///   XZ = lerp(start, end, t)                       — linear
        ///   Y  = lerp(startY, endY, t) + 4·H·t·(1−t)      — linear + parabolic offset
        ///
        /// At t=0.5 the offset is exactly H, so the apex is H above the midpoint
        /// of the straight line between start and end positions.
        /// </summary>
        /// <param name="target">Transform to move.</param>
        /// <param name="destination">World-space landing position.</param>
        /// <param name="duration">Total flight time in seconds.</param>
        /// <param name="arcHeight">Peak height of the parabolic offset above the linear path.</param>
        /// <param name="token">Cancellation token for interrupt/skip/timeout.</param>
        /// <param name="curve">Optional easing curve applied to time progression (not arc shape).</param>
        public static async UniTask ArcTo(Transform target, Vector3 destination, float duration,
            float arcHeight, CancellationToken token, AnimationCurve curve = null)
        {
            var start = target.position;
            
            await RunLerp(duration, curve, t =>
            {
                // Parabolic offset: peaks at t=0.5 with value = arcHeight
                float parabola = 4f * arcHeight * t * (1f - t);
                
                // Horizontal: linear interpolation
                float x = Mathf.LerpUnclamped(start.x, destination.x, t);
                float z = Mathf.LerpUnclamped(start.z, destination.z, t);
                
                // Vertical: linear interpolation + parabolic arc
                float y = Mathf.LerpUnclamped(start.y, destination.y, t) + parabola;
                
                target.position = new Vector3(x, y, z);
            }, token);
            
            target.position = destination;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ROTATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Lerps a transform's rotation to the destination.
        /// </summary>
        public static async UniTask RotateTo(Transform target, Quaternion destination, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            var start = target.rotation;
            await RunLerp(duration, curve, p => target.rotation = Quaternion.SlerpUnclamped(start, destination, p), token);
            target.rotation = destination;
        }
        
        /// <summary>
        /// Lerps a transform's rotation to the given euler angles.
        /// </summary>
        public static async UniTask RotateTo(Transform target, Vector3 eulerAngles, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            await RotateTo(target, Quaternion.Euler(eulerAngles), duration, token, curve);
        }
        
        /// <summary>
        /// Rotates a transform by the given euler offset relative to its current rotation.
        /// </summary>
        public static async UniTask RotateBy(Transform target, Vector3 eulerOffset, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            var dest = target.rotation * Quaternion.Euler(eulerOffset);
            await RotateTo(target, dest, duration, token, curve);
        }
        
        /// <summary>
        /// Smoothly rotates a transform to look at a world position.
        /// </summary>
        public static async UniTask LookAt(Transform target, Vector3 worldPosition, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            var dir = (worldPosition - target.position).normalized;
            if (dir == Vector3.zero) return;
            await RotateTo(target, Quaternion.LookRotation(dir), duration, token, curve);
        }
        
        /// <summary>
        /// Smoothly rotates a transform to look at another transform's position (sampled once at start).
        /// </summary>
        public static async UniTask LookAt(Transform target, Transform lookTarget, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            await LookAt(target, lookTarget.position, duration, token, curve);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // SCALE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Lerps a transform's local scale to the destination.
        /// </summary>
        public static async UniTask ScaleTo(Transform target, Vector3 destination, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            var start = target.localScale;
            await RunLerp(duration, curve, p => target.localScale = Vector3.LerpUnclamped(start, destination, p), token);
            target.localScale = destination;
        }
        
        /// <summary>
        /// Lerps a transform's local scale to a uniform value.
        /// </summary>
        public static async UniTask ScaleTo(Transform target, float uniformScale, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            await ScaleTo(target, Vector3.one * uniformScale, duration, token, curve);
        }
        
        /// <summary>
        /// Punch scale effect: scales up then returns to original. Useful for hit feedback.
        /// </summary>
        public static async UniTask PunchScale(Transform target, float intensity, float duration,
            CancellationToken token)
        {
            var original = target.localScale;
            var peak = original * (1f + intensity);
            float half = duration * 0.5f;
            
            await ScaleTo(target, peak, half, token);
            await ScaleTo(target, original, half, token);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // COMPOSITE TRANSFORM
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Simultaneously lerps position, rotation, and scale in a single pass.
        /// Pass null for any component you want to skip.
        /// </summary>
        public static async UniTask TransformTo(Transform target,
            Vector3? position, Quaternion? rotation, Vector3? scale,
            float duration, CancellationToken token, AnimationCurve curve = null)
        {
            var startPos = target.position;
            var startRot = target.rotation;
            var startScale = target.localScale;
            
            await RunLerp(duration, curve, p =>
            {
                if (position.HasValue)
                    target.position = Vector3.LerpUnclamped(startPos, position.Value, p);
                if (rotation.HasValue)
                    target.rotation = Quaternion.SlerpUnclamped(startRot, rotation.Value, p);
                if (scale.HasValue)
                    target.localScale = Vector3.LerpUnclamped(startScale, scale.Value, p);
            }, token);
            
            if (position.HasValue) target.position = position.Value;
            if (rotation.HasValue) target.rotation = rotation.Value;
            if (scale.HasValue) target.localScale = scale.Value;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // VISUAL - ALPHA / COLOR
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Fades a CanvasGroup's alpha.
        /// </summary>
        public static async UniTask FadeAlpha(CanvasGroup target, float endAlpha, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            float start = target.alpha;
            await RunLerp(duration, curve, p => target.alpha = Mathf.LerpUnclamped(start, endAlpha, p), token);
            target.alpha = endAlpha;
        }
        
        /// <summary>
        /// Fades a SpriteRenderer's alpha.
        /// </summary>
        public static async UniTask FadeAlpha(SpriteRenderer target, float endAlpha, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            float start = target.color.a;
            await RunLerp(duration, curve, p =>
            {
                var c = target.color;
                c.a = Mathf.LerpUnclamped(start, endAlpha, p);
                target.color = c;
            }, token);
            var final = target.color;
            final.a = endAlpha;
            target.color = final;
        }
        
        /// <summary>
        /// Lerps a SpriteRenderer's color.
        /// </summary>
        public static async UniTask ColorTo(SpriteRenderer target, Color endColor, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            var start = target.color;
            await RunLerp(duration, curve, p => target.color = Color.LerpUnclamped(start, endColor, p), token);
            target.color = endColor;
        }
        
        /// <summary>
        /// Lerps a Material's color property.
        /// </summary>
        public static async UniTask MaterialColorTo(Renderer target, Color endColor, float duration,
            CancellationToken token, AnimationCurve curve = null, string property = "_Color")
        {
            var start = target.material.GetColor(property);
            await RunLerp(duration, curve, p => target.material.SetColor(property, Color.LerpUnclamped(start, endColor, p)), token);
            target.material.SetColor(property, endColor);
        }
        
        /// <summary>
        /// Lerps a Material's float property (e.g. _Metallic, _Glossiness, dissolve).
        /// </summary>
        public static async UniTask MaterialFloatTo(Renderer target, string property, float endValue, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            float start = target.material.GetFloat(property);
            await RunLerp(duration, curve, p => target.material.SetFloat(property, Mathf.LerpUnclamped(start, endValue, p)), token);
            target.material.SetFloat(property, endValue);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // AUDIO
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Plays a clip and waits for it to finish.
        /// </summary>
        public static async UniTask PlayAndWait(AudioSource source, AudioClip clip, float volume,
            CancellationToken token)
        {
            source.PlayOneShot(clip, volume);
            await UniTask.Delay(TimeSpan.FromSeconds(clip.length), cancellationToken: token);
        }
        
        /// <summary>
        /// Lerps an AudioSource's volume.
        /// </summary>
        public static async UniTask FadeVolume(AudioSource source, float endVolume, float duration,
            CancellationToken token, AnimationCurve curve = null)
        {
            float start = source.volume;
            await RunLerp(duration, curve, p => source.volume = Mathf.LerpUnclamped(start, endVolume, p), token);
            source.volume = endVolume;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ANIMATOR
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Sets a trigger and waits for the resulting animation state to finish.
        /// </summary>
        public static async UniTask PlayAnimationState(Animator animator, string triggerParam,
            CancellationToken token, int layer = 0)
        {
            animator.SetTrigger(triggerParam);
            await UniTask.Yield(PlayerLoopTiming.Update, token);
            await UniTask.WaitWhile(() =>
            {
                var info = animator.GetCurrentAnimatorStateInfo(layer);
                return info.normalizedTime < 1f;
            }, cancellationToken: token);
        }
        
        /// <summary>
        /// Lerps an Animator float parameter.
        /// </summary>
        public static async UniTask LerpAnimFloat(Animator animator, string parameter,
            float endValue, float duration, CancellationToken token, AnimationCurve curve = null)
        {
            float start = animator.GetFloat(parameter);
            await RunLerp(duration, curve, p => animator.SetFloat(parameter, Mathf.LerpUnclamped(start, endValue, p)), token);
            animator.SetFloat(parameter, endValue);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // PHYSICS
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Launches a Rigidbody in a parabolic arc from its current position to the destination
        /// using physics forces. The body interacts with colliders during flight.
        ///
        /// Temporarily disables standard gravity and applies a computed custom gravity to
        /// achieve the exact arc shape. On completion (or cancellation), the body is snapped
        /// to the destination and original gravity settings are restored.
        ///
        /// Derived from the same parabola as ArcTo:
        ///   y(t) = y₀ + Δy·(t/D) + 4·H·(t/D)·(1−t/D)
        ///
        ///   Initial velocity:   v₀ = (Δx/D,  (Δy + 4H)/D,  Δz/D)
        ///   Custom gravity:     a  = (0,      −8H/D²,        0)
        ///
        /// The arc height H is the peak offset above the straight-line path, identical
        /// to the transform-based ArcTo. The arc shape is a perfect parabola regardless
        /// of start/end height difference.
        /// </summary>
        /// <param name="rb">Rigidbody to launch.</param>
        /// <param name="destination">World-space landing position.</param>
        /// <param name="duration">Total flight time in seconds.</param>
        /// <param name="arcHeight">Peak height of the parabolic offset above the linear path.</param>
        /// <param name="token">Cancellation token for interrupt/skip/timeout.</param>
        /// <param name="snapOnComplete">If true, snaps position to exact destination on completion.</param>
        public static async UniTask ArcForce(Rigidbody rb, Vector3 destination, float duration,
            float arcHeight, CancellationToken token, bool snapOnComplete = true)
        {
            if (duration <= 0f)
            {
                rb.position = destination;
                rb.linearVelocity = Vector3.zero;
                return;
            }
            
            var start = rb.position;
            var delta = destination - start;
            
            // Save and override gravity
            bool wasUsingGravity = rb.useGravity;
            rb.useGravity = false;
            
            // Compute initial velocity from the parabolic arc's first derivative at t=0
            //   vx = Δx / D
            //   vy = (Δy + 4H) / D     (accounts for both height difference and arc)
            //   vz = Δz / D
            Vector3 initialVelocity = new Vector3(
                delta.x / duration,
                (delta.y + 4f * arcHeight) / duration,
                delta.z / duration
            );
            
            // Compute custom gravity from the parabolic arc's second derivative (constant)
            //   ay = −8H / D²
            // Applied as force = mass * acceleration each FixedUpdate
            float customGravity = -8f * arcHeight / (duration * duration);
            Vector3 gravityForce = new Vector3(0f, customGravity, 0f);
            
            // Launch
            rb.linearVelocity = initialVelocity;
            
            float elapsed = 0f;
            try
            {
                while (elapsed < duration)
                {
                    await UniTask.Yield(PlayerLoopTiming.FixedUpdate, token);
                    elapsed += Time.fixedDeltaTime;
                    
                    // Apply custom gravity as acceleration (ForceMode.Acceleration ignores mass)
                    rb.AddForce(gravityForce, ForceMode.Acceleration);
                }
            }
            finally
            {
                // Restore gravity setting
                rb.useGravity = wasUsingGravity;
                
                if (snapOnComplete)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.position = destination;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // GENERIC LERP PRIMITIVES
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Raw lerp: calls setter each frame with progress (0→1), applying optional easing.
        /// The universal building block for anything not covered by a specific method.
        /// 
        /// Usage:
        ///   await Seq.Lerp(1.5f, ease, p => {
        ///       myLight.intensity = Mathf.Lerp(0f, 5f, p);
        ///       myLight.range = Mathf.Lerp(1f, 10f, p);
        ///   }, token);
        /// </summary>
        public static async UniTask Lerp(float duration, AnimationCurve curve, Action<float> setter,
            CancellationToken token)
        {
            await RunLerp(duration, curve, setter, token);
        }
        
        /// <summary>
        /// Lerps between two float values with a setter callback.
        /// </summary>
        public static async UniTask LerpFloat(float from, float to, float duration, Action<float> setter,
            CancellationToken token, AnimationCurve curve = null)
        {
            await RunLerp(duration, curve, p => setter(Mathf.LerpUnclamped(from, to, p)), token);
            setter(to);
        }
        
        /// <summary>
        /// Lerps between two Vector3 values with a setter callback.
        /// </summary>
        public static async UniTask LerpVector3(Vector3 from, Vector3 to, float duration, Action<Vector3> setter,
            CancellationToken token, AnimationCurve curve = null)
        {
            await RunLerp(duration, curve, p => setter(Vector3.LerpUnclamped(from, to, p)), token);
            setter(to);
        }
        
        /// <summary>
        /// Lerps between two Color values with a setter callback.
        /// </summary>
        public static async UniTask LerpColor(Color from, Color to, float duration, Action<Color> setter,
            CancellationToken token, AnimationCurve curve = null)
        {
            await RunLerp(duration, curve, p => setter(Color.LerpUnclamped(from, to, p)), token);
            setter(to);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // TIMING UTILITIES
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Yields for one frame. Shorthand for UniTask.Yield().
        /// </summary>
        public static async UniTask Yield(CancellationToken token,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            await UniTask.Yield(timing, token);
        }
        
        /// <summary>
        /// Waits for N frames.
        /// </summary>
        public static async UniTask WaitFrames(int frameCount, CancellationToken token,
            PlayerLoopTiming timing = PlayerLoopTiming.Update)
        {
            await UniTask.DelayFrame(frameCount, timing, token);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // INTERNAL LERP ENGINE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Core lerp loop driving all timed operations.
        /// </summary>
        private static async UniTask RunLerp(float duration, AnimationCurve curve, Action<float> setter,
            CancellationToken token)
        {
            if (duration <= 0f)
            {
                setter(1f);
                return;
            }
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                elapsed += Time.deltaTime;
                
                float t = Mathf.Clamp01(elapsed / duration);
                float progress = curve?.Evaluate(t) ?? t;
                setter(progress);
            }
        }
    }
}