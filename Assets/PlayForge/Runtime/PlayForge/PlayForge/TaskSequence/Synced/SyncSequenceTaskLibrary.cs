using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Reusable synchronous building blocks for common gameplay operations.
    /// Each method returns an ISequenceTask that advances per-frame via Step().
    /// Also usable in async sequences — the DelegateSyncTask.Execute bridge loops Step automatically.
    /// Designed for use with TaskSequenceBuilder.
    ///
    /// Usage:
    ///   var runner = TaskSequenceBuilder.Create("Chase")
    ///       .Stage(s => s
    ///           .Task(SyncSeq.MoveTowards(chaser, target, 5f))
    ///       )
    ///       .BuildSyncRunner();
    ///
    /// All timed tasks:
    ///   - Accept an optional AnimationCurve for easing (null = linear)
    ///   - Snap to the final value on completion to avoid float drift
    ///   - Handle zero/negative duration by completing immediately
    ///
    /// All tracking tasks (MoveTowards, LookAtTracking, etc.):
    ///   - Re-sample the target each frame for dynamic following
    ///   - Complete based on distance/condition, not duration
    /// </summary>
    public static class SyncSequenceTaskLibrary
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // POSITION — TIMED
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lerps a transform's world position to the destination over the given duration.
        /// </summary>
        public static ISequenceTask MoveTo(Transform target, Vector3 destination, float duration,
            AnimationCurve curve = null)
        {
            Vector3 start = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    target.position = Vector3.LerpUnclamped(start, destination, p);
                    if (t >= 1f) { target.position = destination; return true; }
                    return false;
                },
                prepare: _ => { start = target.position; elapsed = 0f; }
            );
        }

        /// <summary>
        /// Moves a transform by an offset relative to its starting position.
        /// </summary>
        public static ISequenceTask MoveBy(Transform target, Vector3 offset, float duration,
            AnimationCurve curve = null)
        {
            Vector3 start = default;
            Vector3 end = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    target.position = Vector3.LerpUnclamped(start, end, p);
                    if (t >= 1f) { target.position = end; return true; }
                    return false;
                },
                prepare: _ => { start = target.position; end = start + offset; elapsed = 0f; }
            );
        }

        /// <summary>
        /// Lerps a transform's local position to the destination.
        /// </summary>
        public static ISequenceTask MoveLocalTo(Transform target, Vector3 destination, float duration,
            AnimationCurve curve = null)
        {
            Vector3 start = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    target.localPosition = Vector3.LerpUnclamped(start, destination, p);
                    if (t >= 1f) { target.localPosition = destination; return true; }
                    return false;
                },
                prepare: _ => { start = target.localPosition; elapsed = 0f; }
            );
        }

        /// <summary>
        /// Moves a transform in a parabolic arc to the destination.
        /// Arc height is the peak offset above the straight-line path.
        /// </summary>
        public static ISequenceTask ArcTo(Transform target, Vector3 destination, float duration,
            float arcHeight, AnimationCurve curve = null)
        {
            Vector3 start = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;

                    float parabola = 4f * arcHeight * p * (1f - p);
                    float x = Mathf.LerpUnclamped(start.x, destination.x, p);
                    float z = Mathf.LerpUnclamped(start.z, destination.z, p);
                    float y = Mathf.LerpUnclamped(start.y, destination.y, p) + parabola;
                    target.position = new Vector3(x, y, z);

                    if (t >= 1f) { target.position = destination; return true; }
                    return false;
                },
                prepare: _ => { start = target.position; elapsed = 0f; }
            );
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // POSITION — TRACKING (dynamic target, speed-based)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Moves towards a target transform at a fixed speed. Completes when within stoppingDistance.
        /// Re-samples target position each frame.
        /// </summary>
        public static ISequenceTask MoveTowards(Transform mover, Transform target, float speed,
            float stoppingDistance = 0.1f)
        {
            return new DelegateSyncTask((_, dt) =>
            {
                if (!target) return true;
                float dist = Vector3.Distance(mover.position, target.position);
                if (dist <= stoppingDistance) return true;
                mover.position = Vector3.MoveTowards(mover.position, target.position, speed * dt);
                return false;
            });
        }

        /// <summary>
        /// Moves towards a target transform at a fixed speed, arcing upward.
        /// The arc offset is proportional to the remaining distance to create a natural curve.
        /// Completes when within stoppingDistance.
        /// </summary>
        public static ISequenceTask ArcTowards(Transform mover, Transform target, float speed,
            float arcHeight, float stoppingDistance = 0.1f)
        {
            float initialDistance = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    if (!target) return true;
                    Vector3 dest = target.position;
                    float dist = Vector3.Distance(mover.position, dest);
                    if (dist <= stoppingDistance) { mover.position = dest; return true; }

                    // Progress along the path (1 = start, 0 = arrived)
                    float ratio = initialDistance > 0f ? Mathf.Clamp01(dist / initialDistance) : 0f;
                    float parabola = 4f * arcHeight * ratio * (1f - ratio);

                    Vector3 flatTarget = Vector3.MoveTowards(mover.position, dest, speed * dt);
                    flatTarget.y += parabola;
                    mover.position = flatTarget;
                    return false;
                },
                prepare: _ =>
                {
                    initialDistance = target ? Vector3.Distance(mover.position, target.position) : 0f;
                }
            );
        }

        /// <summary>
        /// Orbits around a center transform at a given radius and angular speed.
        /// Never completes on its own — use with stage MaxDuration, WaitUntil, or repeat break.
        /// </summary>
        public static ISequenceTask Orbit(Transform mover, Transform center, float radius,
            float degreesPerSecond, Vector3 axis)
        {
            float angle = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    if (!center) return true;
                    angle += degreesPerSecond * dt;
                    Vector3 offset = Quaternion.AngleAxis(angle, axis) * (Vector3.forward * radius);
                    mover.position = center.position + offset;
                    return false;
                },
                prepare: _ => { angle = 0f; }
            );
        }

        /// <summary>
        /// Moves a transform in a direction by a distance over a duration.
        /// Useful for knockback, dash, or lunge effects.
        /// </summary>
        public static ISequenceTask Dash(Transform target, Vector3 direction, float distance,
            float duration, AnimationCurve curve = null)
        {
            return MoveBy(target, direction.normalized * distance, duration, curve);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ROTATION
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Slerps a transform's rotation to the destination over the given duration.
        /// </summary>
        public static ISequenceTask RotateTo(Transform target, Quaternion destination, float duration,
            AnimationCurve curve = null)
        {
            Quaternion start = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    target.rotation = Quaternion.SlerpUnclamped(start, destination, p);
                    if (t >= 1f) { target.rotation = destination; return true; }
                    return false;
                },
                prepare: _ => { start = target.rotation; elapsed = 0f; }
            );
        }

        /// <summary>
        /// Slerps a transform's rotation to the given euler angles.
        /// </summary>
        public static ISequenceTask RotateTo(Transform target, Vector3 eulerAngles, float duration,
            AnimationCurve curve = null)
        {
            return RotateTo(target, Quaternion.Euler(eulerAngles), duration, curve);
        }

        /// <summary>
        /// Rotates a transform by the given euler offset relative to its starting rotation.
        /// </summary>
        public static ISequenceTask RotateBy(Transform target, Vector3 eulerOffset, float duration,
            AnimationCurve curve = null)
        {
            Quaternion start = default;
            Quaternion dest = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    target.rotation = Quaternion.SlerpUnclamped(start, dest, p);
                    if (t >= 1f) { target.rotation = dest; return true; }
                    return false;
                },
                prepare: _ => { start = target.rotation; dest = start * Quaternion.Euler(eulerOffset); elapsed = 0f; }
            );
        }

        /// <summary>
        /// Smoothly rotates to look at a world position over the given duration.
        /// </summary>
        public static ISequenceTask LookAt(Transform target, Vector3 worldPosition, float duration,
            AnimationCurve curve = null)
        {
            var dir = (worldPosition - target.position).normalized;
            if (dir == Vector3.zero) return new DelegateSyncTask((_, _) => true);
            return RotateTo(target, Quaternion.LookRotation(dir), duration, curve);
        }

        /// <summary>
        /// Continuously rotates to face a target transform each frame.
        /// Speed is in degrees per second. Never completes — use with MaxDuration or repeat break.
        /// </summary>
        public static ISequenceTask LookAtTracking(Transform mover, Transform target,
            float degreesPerSecond)
        {
            return new DelegateSyncTask((_, dt) =>
            {
                if (!target) return true;
                var dir = (target.position - mover.position).normalized;
                if (dir == Vector3.zero) return false;
                var desired = Quaternion.LookRotation(dir);
                mover.rotation = Quaternion.RotateTowards(mover.rotation, desired, degreesPerSecond * dt);
                return false;
            });
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // SCALE
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Lerps a transform's local scale to the destination.
        /// </summary>
        public static ISequenceTask ScaleTo(Transform target, Vector3 destination, float duration,
            AnimationCurve curve = null)
        {
            Vector3 start = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    target.localScale = Vector3.LerpUnclamped(start, destination, p);
                    if (t >= 1f) { target.localScale = destination; return true; }
                    return false;
                },
                prepare: _ => { start = target.localScale; elapsed = 0f; }
            );
        }

        /// <summary>
        /// Lerps a transform's local scale to a uniform value.
        /// </summary>
        public static ISequenceTask ScaleTo(Transform target, float uniformScale, float duration,
            AnimationCurve curve = null)
        {
            return ScaleTo(target, Vector3.one * uniformScale, duration, curve);
        }

        /// <summary>
        /// Punch scale effect: scales up then returns to original.
        /// </summary>
        public static ISequenceTask PunchScale(Transform target, float intensity, float duration)
        {
            Vector3 original = default;
            Vector3 peak = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = Mathf.Clamp01(elapsed / duration);
                    // Triangle wave: 0→1→0
                    float punch = t < 0.5f ? t * 2f : (1f - t) * 2f;
                    target.localScale = Vector3.LerpUnclamped(original, peak, punch);
                    if (t >= 1f) { target.localScale = original; return true; }
                    return false;
                },
                prepare: _ => { original = target.localScale; peak = original * (1f + intensity); elapsed = 0f; }
            );
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // COMPOSITE TRANSFORM
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Simultaneously lerps position, rotation, and scale in a single pass.
        /// Pass null for any component you want to skip.
        /// </summary>
        public static ISequenceTask TransformTo(Transform target,
            Vector3? position, Quaternion? rotation, Vector3? scale,
            float duration, AnimationCurve curve = null)
        {
            Vector3 startPos = default;
            Quaternion startRot = default;
            Vector3 startScale = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;

                    if (position.HasValue) target.position = Vector3.LerpUnclamped(startPos, position.Value, p);
                    if (rotation.HasValue) target.rotation = Quaternion.SlerpUnclamped(startRot, rotation.Value, p);
                    if (scale.HasValue) target.localScale = Vector3.LerpUnclamped(startScale, scale.Value, p);

                    if (t >= 1f)
                    {
                        if (position.HasValue) target.position = position.Value;
                        if (rotation.HasValue) target.rotation = rotation.Value;
                        if (scale.HasValue) target.localScale = scale.Value;
                        return true;
                    }
                    return false;
                },
                prepare: _ =>
                {
                    startPos = target.position;
                    startRot = target.rotation;
                    startScale = target.localScale;
                    elapsed = 0f;
                }
            );
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // VISUAL — ALPHA / COLOR
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fades a CanvasGroup's alpha.
        /// </summary>
        public static ISequenceTask FadeAlpha(CanvasGroup target, float endAlpha, float duration,
            AnimationCurve curve = null)
        {
            float start = 0f;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    target.alpha = Mathf.LerpUnclamped(start, endAlpha, p);
                    if (t >= 1f) { target.alpha = endAlpha; return true; }
                    return false;
                },
                prepare: _ => { start = target.alpha; elapsed = 0f; }
            );
        }

        /// <summary>
        /// Fades a SpriteRenderer's alpha.
        /// </summary>
        public static ISequenceTask FadeAlpha(SpriteRenderer target, float endAlpha, float duration,
            AnimationCurve curve = null)
        {
            float start = 0f;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    var c = target.color;
                    c.a = Mathf.LerpUnclamped(start, endAlpha, p);
                    target.color = c;
                    if (t >= 1f) { c.a = endAlpha; target.color = c; return true; }
                    return false;
                },
                prepare: _ => { start = target.color.a; elapsed = 0f; }
            );
        }

        /// <summary>
        /// Lerps a SpriteRenderer's color.
        /// </summary>
        public static ISequenceTask ColorTo(SpriteRenderer target, Color endColor, float duration,
            AnimationCurve curve = null)
        {
            Color start = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    target.color = Color.LerpUnclamped(start, endColor, p);
                    if (t >= 1f) { target.color = endColor; return true; }
                    return false;
                },
                prepare: _ => { start = target.color; elapsed = 0f; }
            );
        }

        /// <summary>
        /// Lerps a Material's color property.
        /// </summary>
        public static ISequenceTask MaterialColorTo(Renderer target, Color endColor, float duration,
            AnimationCurve curve = null, string property = "_Color")
        {
            Color start = default;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    target.material.SetColor(property, Color.LerpUnclamped(start, endColor, p));
                    if (t >= 1f) { target.material.SetColor(property, endColor); return true; }
                    return false;
                },
                prepare: _ => { start = target.material.GetColor(property); elapsed = 0f; }
            );
        }

        /// <summary>
        /// Lerps a Material's float property.
        /// </summary>
        public static ISequenceTask MaterialFloatTo(Renderer target, string property, float endValue,
            float duration, AnimationCurve curve = null)
        {
            float start = 0f;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    target.material.SetFloat(property, Mathf.LerpUnclamped(start, endValue, p));
                    if (t >= 1f) { target.material.SetFloat(property, endValue); return true; }
                    return false;
                },
                prepare: _ => { start = target.material.GetFloat(property); elapsed = 0f; }
            );
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // AUDIO
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Plays a clip and waits for it to finish.
        /// </summary>
        public static ISequenceTask PlayAndWait(AudioSource source, AudioClip clip, float volume)
        {
            float elapsed = 0f;
            float length = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    return elapsed >= length;
                },
                prepare: _ =>
                {
                    elapsed = 0f;
                    length = clip.length;
                    source.PlayOneShot(clip, volume);
                }
            );
        }

        /// <summary>
        /// Lerps an AudioSource's volume.
        /// </summary>
        public static ISequenceTask FadeVolume(AudioSource source, float endVolume, float duration,
            AnimationCurve curve = null)
        {
            float start = 0f;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    source.volume = Mathf.LerpUnclamped(start, endVolume, p);
                    if (t >= 1f) { source.volume = endVolume; return true; }
                    return false;
                },
                prepare: _ => { start = source.volume; elapsed = 0f; }
            );
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // ANIMATOR
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets a trigger and waits for the resulting animation state to finish.
        /// </summary>
        public static ISequenceTask PlayAnimationState(Animator animator, string triggerParam,
            int layer = 0)
        {
            bool triggered = false;
            return new DelegateSyncTask(
                step: (_, _) =>
                {
                    if (!triggered)
                    {
                        animator.SetTrigger(triggerParam);
                        triggered = true;
                        return false;
                    }
                    var info = animator.GetCurrentAnimatorStateInfo(layer);
                    return info.normalizedTime >= 1f;
                },
                prepare: _ => { triggered = false; }
            );
        }

        /// <summary>
        /// Lerps an Animator float parameter.
        /// </summary>
        public static ISequenceTask LerpAnimFloat(Animator animator, string parameter,
            float endValue, float duration, AnimationCurve curve = null)
        {
            float start = 0f;
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    animator.SetFloat(parameter, Mathf.LerpUnclamped(start, endValue, p));
                    if (t >= 1f) { animator.SetFloat(parameter, endValue); return true; }
                    return false;
                },
                prepare: _ => { start = animator.GetFloat(parameter); elapsed = 0f; }
            );
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // UI TOOLKIT (VisualElement)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Slides a VisualElement by animating its translate style property.
        /// </summary>
        public static ISequenceTask SlideElement(VisualElement element, Vector2 from, Vector2 to,
            float duration, AnimationCurve curve = null)
        {
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    float x = Mathf.LerpUnclamped(from.x, to.x, p);
                    float y = Mathf.LerpUnclamped(from.y, to.y, p);
                    element.style.translate = new Translate(x, y);
                    if (t >= 1f) { element.style.translate = new Translate(to.x, to.y); return true; }
                    return false;
                },
                prepare: _ => { element.style.translate = new Translate(from.x, from.y); elapsed = 0f; }
            );
        }

        /// <summary>
        /// Fades a VisualElement's opacity.
        /// </summary>
        public static ISequenceTask FadeElement(VisualElement element, float fromAlpha, float toAlpha,
            float duration, AnimationCurve curve = null)
        {
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    element.style.opacity = Mathf.LerpUnclamped(fromAlpha, toAlpha, p);
                    if (t >= 1f) { element.style.opacity = toAlpha; return true; }
                    return false;
                },
                prepare: _ => { element.style.opacity = fromAlpha; elapsed = 0f; }
            );
        }

        /// <summary>
        /// Animates a VisualElement's scale (uniform).
        /// </summary>
        public static ISequenceTask ScaleElement(VisualElement element, float from, float to,
            float duration, AnimationCurve curve = null)
        {
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    float s = Mathf.LerpUnclamped(from, to, p);
                    element.style.scale = new Scale(Vector3.one * s);
                    if (t >= 1f) { element.style.scale = new Scale(Vector3.one * to); return true; }
                    return false;
                },
                prepare: _ => { element.style.scale = new Scale(Vector3.one * from); elapsed = 0f; }
            );
        }

        /// <summary>
        /// Animates a VisualElement's background color.
        /// </summary>
        public static ISequenceTask ColorElement(VisualElement element, Color from, Color to,
            float duration, AnimationCurve curve = null)
        {
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    element.style.backgroundColor = Color.LerpUnclamped(from, to, p);
                    if (t >= 1f) { element.style.backgroundColor = to; return true; }
                    return false;
                },
                prepare: _ => { element.style.backgroundColor = from; elapsed = 0f; }
            );
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // GENERIC LERP PRIMITIVES
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Raw lerp: calls setter each frame with progress (0→1), applying optional easing.
        /// Returns an ISequenceTask that completes after the given duration.
        /// </summary>
        public static ISequenceTask Lerp(float duration, AnimationCurve curve, Action<float> setter)
        {
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    setter(p);
                    return t >= 1f;
                },
                prepare: _ => { elapsed = 0f; }
            );
        }

        /// <summary>
        /// Lerps between two float values with a setter callback.
        /// </summary>
        public static ISequenceTask LerpFloat(float from, float to, float duration,
            Action<float> setter, AnimationCurve curve = null)
        {
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    setter(Mathf.LerpUnclamped(from, to, p));
                    if (t >= 1f) { setter(to); return true; }
                    return false;
                },
                prepare: _ => { elapsed = 0f; }
            );
        }

        /// <summary>
        /// Lerps between two Vector3 values with a setter callback.
        /// </summary>
        public static ISequenceTask LerpVector3(Vector3 from, Vector3 to, float duration,
            Action<Vector3> setter, AnimationCurve curve = null)
        {
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    setter(Vector3.LerpUnclamped(from, to, p));
                    if (t >= 1f) { setter(to); return true; }
                    return false;
                },
                prepare: _ => { elapsed = 0f; }
            );
        }

        /// <summary>
        /// Lerps between two Color values with a setter callback.
        /// </summary>
        public static ISequenceTask LerpColor(Color from, Color to, float duration,
            Action<Color> setter, AnimationCurve curve = null)
        {
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) =>
                {
                    elapsed += dt;
                    float t = duration > 0f ? Mathf.Clamp01(elapsed / duration) : 1f;
                    float p = curve?.Evaluate(t) ?? t;
                    setter(Color.LerpUnclamped(from, to, p));
                    if (t >= 1f) { setter(to); return true; }
                    return false;
                },
                prepare: _ => { elapsed = 0f; }
            );
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // TIMING / UTILITY
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Waits for a number of seconds. Completes when elapsed time reaches the duration.
        /// </summary>
        public static ISequenceTask Delay(float seconds)
        {
            float elapsed = 0f;
            return new DelegateSyncTask(
                step: (_, dt) => { elapsed += dt; return elapsed >= seconds; },
                prepare: _ => { elapsed = 0f; }
            );
        }

        /// <summary>
        /// Waits until a condition is met.
        /// </summary>
        public static ISequenceTask WaitUntil(Func<bool> predicate)
        {
            return new DelegateSyncTask(_ => predicate());
        }

        /// <summary>
        /// Waits while a condition is true.
        /// </summary>
        public static ISequenceTask WaitWhile(Func<bool> predicate)
        {
            return new DelegateSyncTask(_ => !predicate());
        }

        /// <summary>
        /// Completes after exactly N frames.
        /// </summary>
        public static ISequenceTask WaitFrames(int frameCount)
        {
            int remaining = 0;
            return new DelegateSyncTask(
                step: (_, _) => { remaining--; return remaining <= 0; },
                prepare: _ => { remaining = frameCount; }
            );
        }
    }
}
