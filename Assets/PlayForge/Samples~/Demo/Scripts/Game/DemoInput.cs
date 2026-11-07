using UnityEngine;
#if DEMO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>The keys the demo reads.</summary>
    public enum DemoKey
    {
        Q,
        W,
        E,
        Item1,
        Item2,
        Item3,
        Item4,
        Item5,
        Item6,
        Stop,
        Cancel,
    }

    /// <summary>What the demo reads of the player's input: keys and the pointer. A test puts its own in.</summary>
    public interface IDemoInput
    {
        /// <summary>Whether the key went down this frame.</summary>
        bool KeyPressed(DemoKey key);

        /// <summary>Whether the pointer's button went down this frame: 0 the left, 1 the right.</summary>
        bool PointerPressed(int button);

        /// <summary>Where the pointer is on the screen, in pixels from its bottom left.</summary>
        Vector2 PointerPosition { get; }
    }

    /// <summary>
    /// The input the demo reads: the Input System's keyboard and mouse where its package is in and on, else the Input
    /// Manager's, unless a test puts its own in. Click targeting reads PlayForge's own, ForgeInput, the same way.
    /// </summary>
    public static class DemoInput
    {
        private static IDemoInput input;

        public static IDemoInput Current => input ??= Default();

        /// <summary>Puts an input in, or the project's own back with null; returns the one it replaced.</summary>
        public static IDemoInput Use(IDemoInput next)
        {
            var previous = Current;
            input = next;
            return previous;
        }

        private static IDemoInput Default()
        {
#if DEMO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            return new InputSystemInput();
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new LegacyInput();
#else
            return new NoInput();
#endif
        }

#if DEMO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private sealed class InputSystemInput : IDemoInput
        {
            public bool KeyPressed(DemoKey key)
            {
                var keys = Keyboard.current;
                if (keys == null) return false;
                var control = key switch
                {
                    DemoKey.Q => keys.qKey,
                    DemoKey.W => keys.wKey,
                    DemoKey.E => keys.eKey,
                    DemoKey.Item1 => keys.digit1Key,
                    DemoKey.Item2 => keys.digit2Key,
                    DemoKey.Item3 => keys.digit3Key,
                    DemoKey.Item4 => keys.digit4Key,
                    DemoKey.Item5 => keys.digit5Key,
                    DemoKey.Item6 => keys.digit6Key,
                    DemoKey.Stop => keys.sKey,
                    _ => keys.escapeKey,
                };
                return control.wasPressedThisFrame;
            }

            public bool PointerPressed(int button)
            {
                var mouse = Mouse.current;
                if (mouse == null) return false;
                return button switch
                {
                    0 => mouse.leftButton.wasPressedThisFrame,
                    1 => mouse.rightButton.wasPressedThisFrame,
                    _ => mouse.middleButton.wasPressedThisFrame,
                };
            }

            public Vector2 PointerPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        private sealed class LegacyInput : IDemoInput
        {
            public bool KeyPressed(DemoKey key) => Input.GetKeyDown(key switch
            {
                DemoKey.Q => KeyCode.Q,
                DemoKey.W => KeyCode.W,
                DemoKey.E => KeyCode.E,
                DemoKey.Item1 => KeyCode.Alpha1,
                DemoKey.Item2 => KeyCode.Alpha2,
                DemoKey.Item3 => KeyCode.Alpha3,
                DemoKey.Item4 => KeyCode.Alpha4,
                DemoKey.Item5 => KeyCode.Alpha5,
                DemoKey.Item6 => KeyCode.Alpha6,
                DemoKey.Stop => KeyCode.S,
                _ => KeyCode.Escape,
            });

            public bool PointerPressed(int button) => Input.GetMouseButtonDown(button);

            public Vector2 PointerPosition => Input.mousePosition;
        }
#else
        private sealed class NoInput : IDemoInput
        {
            public bool KeyPressed(DemoKey key) => false;
            public bool PointerPressed(int button) => false;
            public Vector2 PointerPosition => Vector2.zero;
        }
#endif
    }
}
