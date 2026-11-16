using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Seq = FarEmerald.PlayForge.SequenceTaskLibrary;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
    /// <summary>
    /// Every helper of the two task libraries, in use. SequenceTaskLibrary, Seq here, is for a task's own async code:
    /// each helper takes the task's token, stops when its stage does, and holds while its sequence is paused.
    /// SyncSequenceTaskLibrary, SyncSeq, has the same helpers as step tasks for a stage to take, which run in a synced
    /// sequence, and in an async one a step a frame. Each example builds a sequence whose stages use an async helper and
    /// then its synced twin, on the objects it's given. The Library Showcase component runs them on a scene's objects;
    /// the two that need none are in the sequence dropdown as well.
    /// <para>
    /// A synced helper takes its arguments as it's made, with the sequence, so give it values that hold for the whole
    /// run: a transform to follow rather than a point that moves. A synced task keeps the frame's time as it's stepped,
    /// so in an async sequence it doesn't hold while the sequence is paused, as the async helpers do.
    /// </para>
    /// </summary>
    public static class TaskSequenceLibraryExamples
    {
        // An ease in and out, for the helpers that take a curve. With none, they're linear
        private static readonly AnimationCurve Ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private static readonly KeyCode[] NumberKeys = { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3 };

        /// <summary>
        /// Moving a transform: to a point, by an offset, in its parent's space, along a path, in an arc, towards a target,
        /// round it, in a dash, and along a path that ends on it. The tracking helpers follow the target as it moves.
        /// </summary>
        public static TaskSequence Movement(Transform mover, Transform target)
        {
            Vector3 home = mover.position;
            Vector3 local = mover.localPosition;
            Vector3[] path = { home + new Vector3(2f, 0f, 0f), home + new Vector3(2f, 0f, 2f), home + new Vector3(0f, 0f, 2f) };
            Vector3[] back = { home + new Vector3(2f, 0f, 2f), home + new Vector3(2f, 0f, 0f), home };

            return TaskSequenceBuilder.Create("Library: Movement")
                .Task(async (d, t) => await Seq.MoveTo(mover, home + Vector3.up * 2f, 0.75f, t))
                .Task(SyncSeq.MoveTo(mover, home, 0.75f))
                .Task(async (d, t) => await Seq.MoveBy(mover, Vector3.right * 2f, 0.75f, t, Ease))
                .Task(SyncSeq.MoveBy(mover, Vector3.left * 2f, 0.75f, Ease))
                .Task(async (d, t) => await Seq.MoveLocalTo(mover, local + Vector3.forward, 0.5f, t))
                .Task(SyncSeq.MoveLocalTo(mover, local, 0.5f))
                .Task(async (d, t) => await Seq.MoveAlongPath(mover, path, 1.5f, t))
                .Task(SyncSeq.MoveAlongPath(mover, back, 1.5f))
                .Task(async (d, t) => await Seq.ArcTo(mover, home + Vector3.right * 3f, 1f, 2f, t))
                .Task(SyncSeq.ArcTo(mover, home, 1f, 2f))
                // Towards the target until within a unit and a half of it, then home again between the two
                .Task(async (d, t) => await Seq.MoveTowards(mover, target, 4f, t, 1.5f))
                .Task(SyncSeq.MoveTo(mover, home, 0.5f))
                .Task(SyncSeq.MoveTowards(mover, target, 4f, 1.5f))
                .Task(SyncSeq.MoveTo(mover, home, 0.5f))
                .Task(async (d, t) => await Seq.ArcTowards(mover, target, 5f, 1.5f, t, 1.5f))
                .Task(SyncSeq.MoveTo(mover, home, 0.5f))
                .Task(SyncSeq.ArcTowards(mover, target, 5f, 1.5f, 1.5f))
                // Round the target, which never ends by itself: each stage's max duration cuts it to two seconds
                .Stage(s => s
                    .WithMaxDuration(2f)
                    .Task(async (d, t) => await Seq.Orbit(mover, target, 3f, 180f, Vector3.up, t)))
                .Stage(s => s
                    .WithMaxDuration(2f)
                    .Task(SyncSeq.Orbit(mover, target, 3f, 180f, Vector3.up)))
                .Task(SyncSeq.MoveTo(mover, home, 0.5f))
                .Task(async (d, t) => await Seq.Dash(mover, Vector3.forward, 3f, 0.2f, t))
                .Task(SyncSeq.Dash(mover, Vector3.back, 3f, 0.2f))
                .Task(async (d, t) => await Seq.MoveAlongPathToTarget(mover, path, target, 5f, t, 1.5f))
                .Task(SyncSeq.MoveTo(mover, home, 0.5f))
                .Task(SyncSeq.MoveAlongPathToTarget(mover, path, target, 5f, 1.5f))
                .Task(SyncSeq.MoveTo(mover, home, 0.5f))
                .BuildSequence();
        }

        /// <summary>
        /// Turning a transform: to a rotation, to euler angles, by an offset, to face a point or a transform, and to keep
        /// facing a transform as it moves.
        /// </summary>
        public static TaskSequence Rotation(Transform turner, Transform target)
        {
            Quaternion start = turner.rotation;

            return TaskSequenceBuilder.Create("Library: Rotation")
                .Task(async (d, t) => await Seq.RotateTo(turner, Quaternion.Euler(0f, 90f, 0f), 0.75f, t))
                .Task(SyncSeq.RotateTo(turner, start, 0.75f))
                .Task(async (d, t) => await Seq.RotateTo(turner, new Vector3(0f, -90f, 0f), 0.75f, t, Ease))
                .Task(SyncSeq.RotateTo(turner, new Vector3(0f, 45f, 0f), 0.75f, Ease))
                .Task(async (d, t) => await Seq.RotateBy(turner, new Vector3(0f, 180f, 0f), 0.75f, t))
                .Task(SyncSeq.RotateBy(turner, new Vector3(0f, 180f, 0f), 0.75f))
                // The synced LookAt takes its point as it's made, so it faces the left of where the turner stood then
                .Task(async (d, t) => await Seq.LookAt(turner, turner.position + Vector3.right, 0.5f, t))
                .Task(SyncSeq.LookAt(turner, turner.position + Vector3.left, 0.5f))
                .Task(async (d, t) => await Seq.LookAt(turner, target, 0.5f, t))
                .Task(SyncSeq.RotateTo(turner, start, 0.5f))
                .Task(SyncSeq.LookAt(turner, target, 0.5f))
                // Keeping the target in sight, which never ends by itself: two seconds each
                .Stage(s => s
                    .WithMaxDuration(2f)
                    .Task(async (d, t) => await Seq.LookAtTracking(turner, target, 180f, t)))
                .Stage(s => s
                    .WithMaxDuration(2f)
                    .Task(SyncSeq.LookAtTracking(turner, target, 180f)))
                .Task(SyncSeq.RotateTo(turner, start, 0.5f))
                .BuildSequence();
        }

        /// <summary>
        /// Scaling a transform: to a size, to a uniform scale, and in a punch that swells and settles back; and the
        /// position, rotation and scale together, where a null leaves one as it is.
        /// </summary>
        public static TaskSequence Scale(Transform scaled)
        {
            Vector3 size = scaled.localScale;
            Vector3 position = scaled.position;
            Quaternion rotation = scaled.rotation;

            return TaskSequenceBuilder.Create("Library: Scale")
                .Task(async (d, t) => await Seq.ScaleTo(scaled, size * 2f, 0.5f, t))
                .Task(SyncSeq.ScaleTo(scaled, size, 0.5f))
                .Task(async (d, t) => await Seq.ScaleTo(scaled, 0.5f, 0.5f, t, Ease))
                .Task(SyncSeq.ScaleTo(scaled, 1.5f, 0.5f, Ease))
                .Task(SyncSeq.ScaleTo(scaled, size, 0.25f))
                .Task(async (d, t) => await Seq.PunchScale(scaled, 0.5f, 0.4f, t))
                .Task(SyncSeq.PunchScale(scaled, 0.5f, 0.4f))
                .Task(async (d, t) => await Seq.TransformTo(scaled, position + Vector3.up, Quaternion.Euler(0f, 45f, 0f), size * 1.5f, 1f, t))
                .Task(SyncSeq.TransformTo(scaled, position, rotation, null, 1f))
                .Task(SyncSeq.ScaleTo(scaled, size, 0.25f))
                .BuildSequence();
        }

        /// <summary>
        /// Fading and coloring: a canvas group's alpha, a sprite's alpha and color, and a material's color and a float of
        /// its shader's. Each part runs if its object is given, as a builder is plain code: a stage can be added or left out
        /// as it's built.
        /// </summary>
        public static TaskSequence Visuals(CanvasGroup group, SpriteRenderer sprite, Renderer renderer, string colorProperty)
        {
            var builder = TaskSequenceBuilder.Create("Library: Visuals");

            if (group)
            {
                builder
                    .Task(async (d, t) => await Seq.FadeAlpha(group, 0f, 0.5f, t))
                    .Task(SyncSeq.FadeAlpha(group, 1f, 0.5f));
            }

            if (sprite)
            {
                Color color = sprite.color;
                builder
                    .Task(async (d, t) => await Seq.FadeAlpha(sprite, 0.2f, 0.5f, t))
                    .Task(SyncSeq.FadeAlpha(sprite, 1f, 0.5f))
                    .Task(async (d, t) => await Seq.ColorTo(sprite, Color.red, 0.5f, t))
                    .Task(SyncSeq.ColorTo(sprite, color, 0.5f));
            }

            if (renderer)
            {
                builder
                    .Task(async (d, t) => await Seq.MaterialColorTo(renderer, Color.cyan, 0.5f, t, property: colorProperty))
                    .Task(SyncSeq.MaterialColorTo(renderer, Color.white, 0.5f, property: colorProperty))
                    .Task(async (d, t) => await Seq.MaterialFloatTo(renderer, "_Metallic", 1f, 0.5f, t))
                    .Task(SyncSeq.MaterialFloatTo(renderer, "_Metallic", 0f, 0.5f));
            }

            return builder.BuildSequence();
        }

        /// <summary>
        /// Sound: a clip played through, with the source's volume faded as it plays, out and then back in.
        /// </summary>
        public static TaskSequence Audio(AudioSource source, AudioClip clip)
        {
            return TaskSequenceBuilder.Create("Library: Audio")
                .Stage(s => s
                    .WithName("Fading out as it plays")
                    .Task(async (d, t) => await Seq.PlayAndWait(source, clip, 1f, t))
                    .Task(async (d, t) => await Seq.FadeVolume(source, 0.1f, clip.length, t)))
                .Stage(s => s
                    .WithName("Fading back in as it plays")
                    .Task(SyncSeq.PlayAndWait(source, clip, 1f))
                    .Task(SyncSeq.FadeVolume(source, 1f, clip.length, Ease)))
                .BuildSequence();
        }

        /// <summary>
        /// An animator: a trigger set and its state played through, and a float parameter eased to a value.
        /// </summary>
        public static TaskSequence Animating(Animator animator, string trigger, string floatParameter)
        {
            return TaskSequenceBuilder.Create("Library: Animator")
                .Task(async (d, t) => await Seq.PlayAnimationState(animator, trigger, t))
                .Task(SyncSeq.PlayAnimationState(animator, trigger))
                .Task(async (d, t) => await Seq.LerpAnimFloat(animator, floatParameter, 1f, 0.5f, t))
                .Task(SyncSeq.LerpAnimFloat(animator, floatParameter, 0f, 0.5f))
                .BuildSequence();
        }

        /// <summary>
        /// A rigidbody thrown in an arc by forces, so it meets colliders on its way, and back. Physics moves it whatever its
        /// sequence does, so this is the one helper that doesn't hold while its sequence is paused, and it has no synced
        /// twin.
        /// </summary>
        public static TaskSequence Thrown(Rigidbody body, Vector3 destination)
        {
            Vector3 start = body.position;

            return TaskSequenceBuilder.Create("Library: Physics")
                .Task(async (d, t) => await Seq.ArcForce(body, destination, 1.2f, 3f, t))
                .Delay(0.5f)
                // Left where physics puts it, rather than snapped to the point
                .Task(async (d, t) => await Seq.ArcForce(body, start, 1.2f, 3f, t, snapOnComplete: false))
                .BuildSequence();
        }

        /// <summary>
        /// UI Toolkit: a box slid, faded, scaled, punched, colored and sized, and a label typed out, plainly and then
        /// skippably, Space showing the rest. The box and the label are made here, added under the root given as the run
        /// starts, and taken off as it ends.
        /// </summary>
        public static TaskSequence Interface(VisualElement root)
        {
            var box = new VisualElement { name = "Library Box" };
            box.style.width = 120f;
            box.style.height = 40f;
            box.style.backgroundColor = Color.gray;
            var label = new Label { name = "Library Label" };

            return TaskSequenceBuilder.Create("Library: UI Toolkit")
                .Do(_ =>
                {
                    root.Add(box);
                    root.Add(label);
                })
                .Task(async (d, t) => await Seq.SlideElement(box, Vector2.zero, new Vector2(200f, 0f), 0.5f, t))
                .Task(SyncSeq.SlideElement(box, new Vector2(200f, 0f), new Vector2(200f, 100f), 0.5f))
                .Task(async (d, t) => await Seq.SlideElementTo(box, new Vector2(0f, 100f), 0.5f, t))
                .Task(SyncSeq.SlideElementTo(box, Vector2.zero, 0.5f, Ease))
                .Task(async (d, t) => await Seq.FadeElement(box, 1f, 0.2f, 0.4f, t))
                .Task(SyncSeq.FadeElement(box, 0.2f, 0.6f, 0.4f))
                .Task(async (d, t) => await Seq.FadeElementTo(box, 0.3f, 0.4f, t))
                .Task(SyncSeq.FadeElementTo(box, 1f, 0.4f))
                .Task(async (d, t) => await Seq.ScaleElement(box, 1f, 1.5f, 0.3f, t))
                .Task(SyncSeq.ScaleElement(box, 1.5f, 1f, 0.3f))
                .Task(async (d, t) => await Seq.PunchScaleElement(box, 0.3f, 0.3f, t))
                .Task(SyncSeq.PunchScaleElement(box, 0.3f, 0.3f))
                .Task(async (d, t) => await Seq.ColorElement(box, Color.gray, Color.blue, 0.4f, t))
                .Task(SyncSeq.ColorElement(box, Color.blue, Color.green, 0.4f))
                .Task(async (d, t) => await Seq.ColorElementTo(box, Color.red, 0.4f, t))
                .Task(SyncSeq.ColorElementTo(box, Color.gray, 0.4f))
                .Task(async (d, t) => await Seq.WidthTo(box, 120f, 240f, 0.4f, t))
                .Task(SyncSeq.WidthTo(box, 240f, 120f, 0.4f))
                .Task(async (d, t) => await Seq.HeightTo(box, 40f, 80f, 0.4f, t))
                .Task(SyncSeq.HeightTo(box, 80f, 40f, 0.4f))
                .Task(async (d, t) => await Seq.TypeText(label, "Typed a letter at a time, and slower at a comma.", 0.04f, t))
                .Task(SyncSeq.TypeText(label, "Typed again, synced.", 0.04f))
                .Task(async (d, t) =>
                {
                    bool typed = await Seq.TypeTextSkippable(label, "Space shows the rest of this at once, however long it is.", 0.06f,
                        () => ForgeInput.Current.KeyPressed(KeyCode.Space), t);
                    ForgeLog.Info(typed ? "[Library] Typed to the end." : "[Library] Skipped.");
                })
                .Task(SyncSeq.TypeTextSkippable(label, "And once more, synced: Space skips this one too.", 0.06f,
                    () => ForgeInput.Current.KeyPressed(KeyCode.Space)))
                .Delay(1f)
                .OnTerminate((ctx, completed) =>
                {
                    box.RemoveFromHierarchy();
                    label.RemoveFromHierarchy();
                })
                .BuildSequence();
        }

        /// <summary>
        /// The general lerps, for anything the other helpers don't cover: a progress from 0 to 1, or a float, a vector or a
        /// color, each handed to a setter of yours every frame. These setters keep their values in locals of the method that
        /// built the sequence, which every run of it shares, and the last stage logs where they ended.
        /// </summary>
        [TaskSequenceMethod("Library: Lerps")]
        public static TaskSequence Lerps()
        {
            float progress = 0f;
            float number = 0f;
            Vector3 vector = Vector3.zero;
            Color color = Color.black;

            return TaskSequenceBuilder.Create("Library: Lerps")
                .Task(async (d, t) => await Seq.Lerp(0.5f, Ease, p => progress = p, t))
                .Task(SyncSeq.Lerp(0.5f, null, p => progress = 1f - p))
                .Task(async (d, t) => await Seq.LerpFloat(0f, 10f, 0.5f, value => number = value, t))
                .Task(SyncSeq.LerpFloat(10f, 5f, 0.5f, value => number = value, Ease))
                .Task(async (d, t) => await Seq.LerpVector3(Vector3.zero, Vector3.one, 0.5f, value => vector = value, t))
                .Task(SyncSeq.LerpVector3(Vector3.one, Vector3.up, 0.5f, value => vector = value))
                .Task(async (d, t) => await Seq.LerpColor(Color.black, Color.white, 0.5f, value => color = value, t))
                .Task(SyncSeq.LerpColor(Color.white, Color.yellow, 0.5f, value => color = value))
                .Do(_ => ForgeLog.Info($"[Library] The lerps ended at {progress:F2}, {number:F1}, {vector} and {color}."))
                .BuildSequence();
        }

        /// <summary>
        /// Time and the player. The async helpers count the sequence's time, so they hold while it's paused, and DeltaTime
        /// and IsPaused let code of your own do the same. Waits for the player read ForgeInput. A press reads as down for
        /// the whole frame it happened in: the async waits look from the next frame on, but a synced one looks as its stage
        /// starts, so each async wait here yields a frame before its synced twin waits for the same input. The synced Yield
        /// ends on its first step, a stage's only frame in a synced sequence; in an async one it doesn't wait at all.
        /// </summary>
        [TaskSequenceMethod("Library: Timing and Input")]
        public static TaskSequence TimingAndInput()
        {
            return TaskSequenceBuilder.Create("Library: Timing and Input")
                .Task(async (d, t) =>
                {
                    await Seq.Delay(0.5f, t);
                    await Seq.Yield(t);
                    await Seq.Yield(t, PlayerLoopTiming.LastPostLateUpdate);
                    await Seq.WaitFrames(10, t);

                    // Time of its own, by the sequence's delta time, which is 0 while it's paused
                    float waited = 0f;
                    while (waited < 0.5f)
                    {
                        await Seq.Yield(t);
                        waited += Seq.DeltaTime(t);
                        if (Seq.IsPaused(t)) ForgeLog.Verbose("[Library] Paused: the wait holds.");
                    }
                    ForgeLog.Info("[Library] Waited, async.");
                })
                .Task(SyncSeq.Delay(0.5f))
                .Task(SyncSeq.Yield())
                .Task(SyncSeq.WaitFrames(10))
                .Do(_ => ForgeLog.Info("[Library] Waited, synced. Press Space."))
                .Task(async (d, t) =>
                {
                    await Seq.WaitForKey(KeyCode.Space, t);
                    ForgeLog.Info("[Library] And again, synced.");
                    await Seq.Yield(t);
                })
                .Task(SyncSeq.WaitForKey(KeyCode.Space))
                .Do(_ => ForgeLog.Info("[Library] Press 1, 2 or 3."))
                .Task(async (d, t) =>
                {
                    ForgeLog.Info($"[Library] {await Seq.WaitForAnyKey(NumberKeys, t)}. And again, synced.");
                    await Seq.Yield(t);
                })
                .Task(SyncSeq.WaitForAnyKey(key => ForgeLog.Info($"[Library] {key}, synced. Now click."), KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3))
                .Task(async (d, t) =>
                {
                    await Seq.WaitForPointer(0, t);
                    ForgeLog.Info("[Library] And again, synced.");
                    await Seq.Yield(t);
                })
                .Task(SyncSeq.WaitForPointer(0))
                .Do(_ => ForgeLog.Info("[Library] Hold Left Shift, then let go."))
                .Task(async (d, t) =>
                {
                    await Seq.WaitUntil(() => ForgeInput.Current.KeyHeld(KeyCode.LeftShift), t);
                    await Seq.WaitWhile(() => ForgeInput.Current.KeyHeld(KeyCode.LeftShift), t);
                    ForgeLog.Info("[Library] And again, synced.");
                })
                .Task(SyncSeq.WaitUntil(() => ForgeInput.Current.KeyHeld(KeyCode.LeftShift)))
                .Task(SyncSeq.WaitWhile(() => ForgeInput.Current.KeyHeld(KeyCode.LeftShift)))
                .Do(_ => ForgeLog.Info("[Library] Done."))
                .BuildSequence();
        }
    }

    /// <summary>
    /// Runs the library examples on a scene's objects: give it the objects the chosen group needs, enter Play mode, and
    /// press its key. Each group runs through ProcessControl, so the scene needs PlayForge up, a ProcessControl and a
    /// GameRoot, as a Bootstrapper brings them. The examples move, turn and recolor what they're given, and put most of it
    /// back as they end.
    /// </summary>
    [AddComponentMenu("PlayForge/Samples/Task Sequences/Library Showcase")]
    public sealed class TaskSequenceLibraryShowcase : MonoBehaviour
    {
        public enum EGroup
        {
            Movement,
            Rotation,
            Scale,
            Visuals,
            Audio,
            Animator,
            Physics,
            UserInterface,
            Lerps,
            TimingAndInput
        }

        [Tooltip("The examples it runs")]
        public EGroup Group;

        [Tooltip("The key that runs them, read through ForgeInput")]
        public KeyCode RunKey = KeyCode.Return;

        [Tooltip("What moves, turns and scales")]
        public Transform Subject;

        [Tooltip("What the subject follows, faces and circles, and the rigidbody is thrown at")]
        public Transform Target;

        [Tooltip("A canvas group to fade")]
        public CanvasGroup Fader;

        [Tooltip("A sprite to fade and color")]
        public SpriteRenderer Sprite;

        [Tooltip("A renderer whose material is colored")]
        public Renderer Model;

        [Tooltip("The material's color property: _Color in the Built-in pipeline, _BaseColor in URP")]
        public string ColorProperty = "_Color";

        [Tooltip("An audio source to play the clip on")]
        public AudioSource Speaker;

        [Tooltip("The clip it plays")]
        public AudioClip Clip;

        [Tooltip("An animator to drive")]
        public Animator Animated;

        [Tooltip("A trigger of the animator's, whose state it plays through")]
        public string Trigger = "Play";

        [Tooltip("A float parameter of the animator's, to ease")]
        public string FloatParameter = "Blend";

        [Tooltip("A rigidbody to throw at the target, or four units ahead of itself with none")]
        public Rigidbody Body;

        [Tooltip("The UI document whose root the UI Toolkit examples add to")]
        public UIDocument Document;

        private void Update()
        {
            if (!ForgeInput.Current.KeyPressed(RunKey)) return;

            if (!ProcessControl.Instance || !GameRoot.Instance)
            {
                ForgeLog.Warning("PlayForge isn't up: the scene needs a Bootstrapper.", this);
                return;
            }

            var sequence = Build(out string needs);
            if (sequence == null)
            {
                ForgeLog.Warning($"The {Group} examples need {needs}.", this);
                return;
            }

            ForgeLog.Info($"Running the library's {Group} examples.", this);
            if (sequence.RegisterAndRun(SequenceDataPacket.SceneRoot()) == null) ForgeLog.Warning("ProcessControl refused them.", this);
        }

        // The chosen group's sequence on this component's objects, or null and what it's missing
        private TaskSequence Build(out string needs)
        {
            needs = null;
            switch (Group)
            {
                case EGroup.Movement:
                    if (Subject && Target) return TaskSequenceLibraryExamples.Movement(Subject, Target);
                    needs = "a Subject and a Target";
                    return null;
                case EGroup.Rotation:
                    if (Subject && Target) return TaskSequenceLibraryExamples.Rotation(Subject, Target);
                    needs = "a Subject and a Target";
                    return null;
                case EGroup.Scale:
                    if (Subject) return TaskSequenceLibraryExamples.Scale(Subject);
                    needs = "a Subject";
                    return null;
                case EGroup.Visuals:
                    if (Fader || Sprite || Model) return TaskSequenceLibraryExamples.Visuals(Fader, Sprite, Model, ColorProperty);
                    needs = "a Fader, a Sprite or a Model";
                    return null;
                case EGroup.Audio:
                    if (Speaker && Clip) return TaskSequenceLibraryExamples.Audio(Speaker, Clip);
                    needs = "a Speaker and a Clip";
                    return null;
                case EGroup.Animator:
                    if (Animated) return TaskSequenceLibraryExamples.Animating(Animated, Trigger, FloatParameter);
                    needs = "an Animated animator";
                    return null;
                case EGroup.Physics:
                    if (!Body)
                    {
                        needs = "a Body";
                        return null;
                    }
                    return TaskSequenceLibraryExamples.Thrown(Body, Target ? Target.position : Body.position + Body.transform.forward * 4f);
                case EGroup.UserInterface:
                    if (Document && Document.rootVisualElement != null) return TaskSequenceLibraryExamples.Interface(Document.rootVisualElement);
                    needs = "a Document with a panel";
                    return null;
                case EGroup.Lerps:
                    return TaskSequenceLibraryExamples.Lerps();
                default:
                    return TaskSequenceLibraryExamples.TimingAndInput();
            }
        }
    }
}
