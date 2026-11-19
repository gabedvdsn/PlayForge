using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;
using Seq = FarEmerald.PlayForge.SequenceTaskLibrary;
using SyncSeq = FarEmerald.PlayForge.SyncSequenceTaskLibrary;

namespace FarEmerald.PlayForge.Samples.TaskSequences
{
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
