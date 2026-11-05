using UnityEngine;
#if PLAYFORGE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What click targeting reads of the player's input: whether a pointer's button went down this frame, and the ray the
    /// pointer aims along (FEAT-11, Decision 30). The defaults read a mouse, a pen or a touch through a camera; a game with
    /// a gamepad's reticle, a controller's ray or input of its own puts its own in with <see cref="ForgeInput.Use"/>.
    /// </summary>
    public interface IForgeInput
    {
        /// <summary>
        /// Whether the button went down this frame: 0 is the primary (the left button, or a touch), 1 the secondary (the
        /// right) and 2 the middle.
        /// </summary>
        bool PointerPressed(int button);

        /// <summary>The ray the pointer aims along, or false with nothing to aim through, as with no camera.</summary>
        bool TryGetPointerRay(out Ray ray);
    }

    /// <summary>
    /// The input click targeting reads: the project's own, unless <see cref="Use"/> puts another in. Targeting read the
    /// Input Manager's mouse and <c>Camera.main</c> itself, so a game on the Input System, with a camera of its own or a
    /// test couldn't stand in, and a scene with no main camera threw at the click (FEAT-11). A source-scanning test keeps
    /// Unity's input out of the rest of the runtime.
    /// </summary>
    public static class ForgeInput
    {
        private static IForgeInput input;

        /// <summary>The input read now.</summary>
        public static IForgeInput Current => input ??= Default();

        /// <summary>Puts an input in, or the project's own back with null. Returns the one it replaced, to put back.</summary>
        public static IForgeInput Use(IForgeInput next)
        {
            var previous = Current;
            input = next;
            return previous;
        }

        /// <summary>
        /// The project's own input: the Input System's pointer when its package is in and its input handling is on, else
        /// the Input Manager's mouse, else none, which never presses.
        /// </summary>
        public static IForgeInput Default()
        {
#if PLAYFORGE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            return new InputSystemPointerInput();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new LegacyPointerInput();
#else
            return NoInput.Instance;
#endif
        }

        /// <summary>The ray from a camera through a point on the screen: the camera given, or else <c>Camera.main</c>.</summary>
        public static bool TryAimThrough(Camera camera, Vector2 screenPoint, out Ray ray)
        {
            var aim = camera != null ? camera : Camera.main;
            if (aim == null)
            {
                ForgeLog.Warning("Click targeting found no camera to aim through. Tag one MainCamera, or give the input a camera.");
                ray = default;
                return false;
            }

            ray = aim.ScreenPointToRay(screenPoint);
            return true;
        }

        // A project with neither input handling on
        private sealed class NoInput : IForgeInput
        {
            public static readonly NoInput Instance = new();

            public bool PointerPressed(int button) => false;

            public bool TryGetPointerRay(out Ray ray)
            {
                ray = default;
                return false;
            }
        }
    }

#if ENABLE_LEGACY_INPUT_MANAGER
    /// <summary>The Input Manager's mouse, aimed through a camera: the one given, or else <c>Camera.main</c> (FEAT-11).</summary>
    public sealed class LegacyPointerInput : IForgeInput
    {
        /// <summary>The camera to aim through, or null for <c>Camera.main</c>.</summary>
        public Camera Camera { get; set; }

        public bool PointerPressed(int button) => Input.GetMouseButtonDown(button);

        public bool TryGetPointerRay(out Ray ray) => ForgeInput.TryAimThrough(Camera, Input.mousePosition, out ray);
    }
#endif

#if PLAYFORGE_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
    /// <summary>
    /// The Input System's pointer, a mouse, a pen or a touch, aimed through a camera: the one given, or else
    /// <c>Camera.main</c> (FEAT-11). A mouse presses each of its three buttons, and a pen or a touch the primary.
    /// </summary>
    public sealed class InputSystemPointerInput : IForgeInput
    {
        /// <summary>The camera to aim through, or null for <c>Camera.main</c>.</summary>
        public Camera Camera { get; set; }

        public bool PointerPressed(int button)
        {
            var mouse = Mouse.current;
            switch (button)
            {
                case 0:
                    return (mouse != null && mouse.leftButton.wasPressedThisFrame)
                        || (Pointer.current is { } pointer && pointer.press.wasPressedThisFrame);
                case 1:
                    return mouse != null && mouse.rightButton.wasPressedThisFrame;
                case 2:
                    return mouse != null && mouse.middleButton.wasPressedThisFrame;
                default:
                    return false;
            }
        }

        public bool TryGetPointerRay(out Ray ray)
        {
            if (Pointer.current is not { } pointer)
            {
                ray = default;
                return false;
            }

            return ForgeInput.TryAimThrough(Camera, pointer.position.ReadValue(), out ray);
        }
    }
#endif
}
