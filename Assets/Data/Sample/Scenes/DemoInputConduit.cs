using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FarEmerald.PlayForge.Examples
{
    /// <summary>
    /// Conduit between user input and runtime systems.
    /// Uses direct polling via Unity's new Input System — no Input Action Assets required.
    ///
    /// Provides both synchronous queries (for Update loops) and async awaitables
    /// (for TaskSequence integration). All async methods respect CancellationToken
    /// for interrupt/skip/timeout support.
    ///
    /// Access via your singleton pattern:
    ///   DemoManager.Input.IsKeyDown(Key.Space)
    ///   await DemoManager.Input.AwaitKeyDown(Key.Q, token);
    ///   var hit = await DemoManager.Input.AwaitWorldClick(camera, token);
    /// </summary>
    public class DemoInputConduit : MonoBehaviour
    {
        // ═══════════════════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════════════════
        
        [Tooltip("Default layer mask for world raycasts. Set to Everything if unassigned.")]
        [SerializeField] private LayerMask _worldRaycastMask = ~0;
        
        [Tooltip("Maximum raycast distance for world clicks.")]
        [SerializeField] private float _worldRaycastDistance = 500f;
        
        /// <summary>
        /// When true, all input queries return false / await indefinitely.
        /// Use to suppress input during cutscenes, menus, etc.
        /// </summary>
        public bool InputSuppressed { get; set; }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // SYNCHRONOUS QUERIES (for Update loops, conditions, etc.)
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>Returns true on the frame a keyboard key is pressed down.</summary>
        public bool IsKeyDown(Key key)
        {
            if (InputSuppressed) return false;
            return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }
        
        /// <summary>Returns true while a keyboard key is held.</summary>
        public bool IsKeyHeld(Key key)
        {
            if (InputSuppressed) return false;
            return Keyboard.current != null && Keyboard.current[key].isPressed;
        }
        
        /// <summary>Returns true on the frame a keyboard key is released.</summary>
        public bool IsKeyUp(Key key)
        {
            if (InputSuppressed) return false;
            return Keyboard.current != null && Keyboard.current[key].wasReleasedThisFrame;
        }
        
        /// <summary>Returns true on the frame any keyboard key is pressed.</summary>
        public bool IsAnyKeyDown()
        {
            if (InputSuppressed) return false;
            return Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
        }
        
        /// <summary>Returns true on the frame a mouse button is pressed.</summary>
        public bool IsMouseDown(int button = 0)
        {
            if (InputSuppressed) return false;
            if (Mouse.current == null) return false;
            return button switch
            {
                0 => Mouse.current.leftButton.wasPressedThisFrame,
                1 => Mouse.current.rightButton.wasPressedThisFrame,
                2 => Mouse.current.middleButton.wasPressedThisFrame,
                _ => false
            };
        }
        
        /// <summary>Returns true while a mouse button is held.</summary>
        public bool IsMouseHeld(int button = 0)
        {
            if (InputSuppressed) return false;
            if (Mouse.current == null) return false;
            return button switch
            {
                0 => Mouse.current.leftButton.isPressed,
                1 => Mouse.current.rightButton.isPressed,
                2 => Mouse.current.middleButton.isPressed,
                _ => false
            };
        }
        
        /// <summary>Returns the current mouse position in screen coordinates.</summary>
        public Vector2 MousePosition
        {
            get
            {
                if (Mouse.current == null) return Vector2.zero;
                return Mouse.current.position.ReadValue();
            }
        }
        
        /// <summary>Returns the mouse scroll delta this frame.</summary>
        public Vector2 ScrollDelta
        {
            get
            {
                if (Mouse.current == null) return Vector2.zero;
                return Mouse.current.scroll.ReadValue();
            }
        }
        
        /// <summary>
        /// Performs a raycast from the camera through the current mouse position.
        /// Returns true if something was hit.
        /// </summary>
        public bool TryMouseRaycast(Camera cam, out RaycastHit hit, LayerMask? mask = null)
        {
            hit = default;
            if (cam == null || Mouse.current == null) return false;
            
            var ray = cam.ScreenPointToRay(MousePosition);
            return Physics.Raycast(ray, out hit, _worldRaycastDistance, mask ?? _worldRaycastMask);
        }
        
        /// <summary>
        /// Performs a raycast from the camera through the current mouse position.
        /// Returns true if something was hit. Uses Camera.main.
        /// </summary>
        public bool TryMouseRaycast(out RaycastHit hit, LayerMask? mask = null)
        {
            return TryMouseRaycast(Camera.main, out hit, mask);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ASYNC AWAITABLES — KEYBOARD
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Awaits until a specific key is pressed down.
        /// </summary>
        public async UniTask AwaitKeyDown(Key key, CancellationToken token)
        {
            await UniTask.WaitUntil(() => IsKeyDown(key), PlayerLoopTiming.Update, token);
        }
        
        /// <summary>
        /// Awaits until a specific key is released.
        /// </summary>
        public async UniTask AwaitKeyUp(Key key, CancellationToken token)
        {
            await UniTask.WaitUntil(() => IsKeyUp(key), PlayerLoopTiming.Update, token);
        }
        
        /// <summary>
        /// Awaits until any keyboard key is pressed. Returns which key was pressed.
        /// </summary>
        public async UniTask<Key> AwaitAnyKeyDown(CancellationToken token)
        {
            Key pressed = default;
            await UniTask.WaitUntil(() =>
            {
                if (InputSuppressed || Keyboard.current == null) return false;
                if (!Keyboard.current.anyKey.wasPressedThisFrame) return false;
                
                foreach (var key in Keyboard.current.allKeys)
                {
                    if (!key.wasPressedThisFrame) continue;
                    pressed = key.keyCode;
                    return true;
                }
                return false;
            }, PlayerLoopTiming.Update, token);
            
            return pressed;
        }
        
        /// <summary>
        /// Awaits until a key is held for the specified duration. Returns false if released early.
        /// Useful for hold-to-confirm interactions.
        /// </summary>
        public async UniTask<bool> AwaitKeyHold(Key key, float holdSeconds, CancellationToken token)
        {
            // Wait for initial press
            await AwaitKeyDown(key, token);
            
            float elapsed = 0f;
            while (elapsed < holdSeconds)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                
                if (!IsKeyHeld(key)) return false;
                elapsed += Time.deltaTime;
            }
            
            return true;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ASYNC AWAITABLES — MOUSE
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Awaits until a mouse button is pressed. Returns the screen position of the click.
        /// </summary>
        public async UniTask<Vector2> AwaitMouseDown(CancellationToken token, int button = 0)
        {
            await UniTask.WaitUntil(() => IsMouseDown(button), PlayerLoopTiming.Update, token);
            return MousePosition;
        }
        
        /// <summary>
        /// Awaits until a mouse button is released. Returns the screen position at release.
        /// </summary>
        public async UniTask<Vector2> AwaitMouseUp(CancellationToken token, int button = 0)
        {
            await UniTask.WaitUntil(() =>
            {
                if (InputSuppressed || Mouse.current == null) return false;
                return button switch
                {
                    0 => Mouse.current.leftButton.wasReleasedThisFrame,
                    1 => Mouse.current.rightButton.wasReleasedThisFrame,
                    2 => Mouse.current.middleButton.wasReleasedThisFrame,
                    _ => false
                };
            }, PlayerLoopTiming.Update, token);
            return MousePosition;
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ASYNC AWAITABLES — WORLD INTERACTION
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Awaits a mouse click that hits something in the world.
        /// Returns the RaycastHit. Ignores clicks that don't hit anything.
        /// </summary>
        public async UniTask<RaycastHit> AwaitWorldClick(Camera cam, CancellationToken token,
            int button = 0, LayerMask? mask = null)
        {
            RaycastHit hit = default;
            
            await UniTask.WaitUntil(() =>
            {
                if (!IsMouseDown(button)) return false;
                return TryMouseRaycast(cam, out hit, mask);
            }, PlayerLoopTiming.Update, token);
            
            return hit;
        }
        
        /// <summary>
        /// Awaits a mouse click that hits something in the world. Uses Camera.main.
        /// </summary>
        public async UniTask<RaycastHit> AwaitWorldClick(CancellationToken token,
            int button = 0, LayerMask? mask = null)
        {
            return await AwaitWorldClick(Camera.main, token, button, mask);
        }
        
        /// <summary>
        /// Awaits a mouse click and returns the world position.
        /// If the click hits geometry, returns the hit point.
        /// If it misses, projects the click onto a plane at the given Y height.
        /// </summary>
        public async UniTask<Vector3> AwaitWorldPosition(Camera cam, CancellationToken token,
            int button = 0, float fallbackPlaneY = 0f, LayerMask? mask = null)
        {
            await UniTask.WaitUntil(() => IsMouseDown(button), PlayerLoopTiming.Update, token);
            
            if (TryMouseRaycast(cam, out var hit, mask))
            {
                return hit.point;
            }
            
            // Fallback: project onto horizontal plane
            var ray = cam.ScreenPointToRay(MousePosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, fallbackPlaneY, 0f));
            if (plane.Raycast(ray, out float enter))
            {
                return ray.GetPoint(enter);
            }
            
            return Vector3.zero;
        }
        
        /// <summary>
        /// Awaits a mouse click and returns the world position. Uses Camera.main.
        /// </summary>
        public async UniTask<Vector3> AwaitWorldPosition(CancellationToken token,
            int button = 0, float fallbackPlaneY = 0f, LayerMask? mask = null)
        {
            return await AwaitWorldPosition(Camera.main, token, button, fallbackPlaneY, mask);
        }
        
        /// <summary>
        /// Awaits a mouse click that hits a specific component type on a collider.
        /// Useful for "click on an enemy", "click on an item", etc.
        /// </summary>
        public async UniTask<T> AwaitClickOnComponent<T>(Camera cam, CancellationToken token,
            int button = 0, LayerMask? mask = null) where T : Component
        {
            T result = null;
            
            await UniTask.WaitUntil(() =>
            {
                if (!IsMouseDown(button)) return false;
                if (!TryMouseRaycast(cam, out var hit, mask)) return false;
                result = hit.collider.GetComponentInParent<T>();
                return result != null;
            }, PlayerLoopTiming.Update, token);
            
            return result;
        }
        
        /// <summary>
        /// Awaits a mouse click that hits a specific component type. Uses Camera.main.
        /// </summary>
        public async UniTask<T> AwaitClickOnComponent<T>(CancellationToken token,
            int button = 0, LayerMask? mask = null) where T : Component
        {
            return await AwaitClickOnComponent<T>(Camera.main, token, button, mask);
        }
        
        /// <summary>
        /// Awaits a mouse click that hits a specific GameObject.
        /// Checks the hit collider's GameObject and all parents.
        /// Ignores clicks on other objects.
        /// </summary>
        public async UniTask AwaitClickOnObject(GameObject target, Camera cam, CancellationToken token,
            int button = 0, LayerMask? mask = null)
        {
            await UniTask.WaitUntil(() =>
            {
                if (!IsMouseDown(button)) return false;
                if (!TryMouseRaycast(cam, out var hit, mask)) return false;
        
                var hitObj = hit.collider.transform;
                while (hitObj != null)
                {
                    if (hitObj.gameObject == target) return true;
                    hitObj = hitObj.parent;
                }
                return false;
            }, PlayerLoopTiming.Update, token);
        }

        /// <summary>
        /// Awaits a mouse click that hits a specific GameObject. Uses Camera.main.
        /// </summary>
        public async UniTask AwaitClickOnObject(GameObject target, CancellationToken token,
            int button = 0, LayerMask? mask = null)
        {
            await AwaitClickOnObject(target, Camera.main, token, button, mask);
        }
        
        // ═══════════════════════════════════════════════════════════════════════════
        // ASYNC AWAITABLES — COMPOSITE / RACING
        // ═══════════════════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Awaits until any one of the specified keys is pressed. Returns which key was pressed.
        /// Useful for "Press Q, W, or E to select an ability" patterns.
        /// </summary>
        public async UniTask<Key> AwaitAnyOfKeys(CancellationToken token, params Key[] keys)
        {
            Key pressed = default;
            
            await UniTask.WaitUntil(() =>
            {
                foreach (var key in keys)
                {
                    if (!IsKeyDown(key)) continue;
                    pressed = key;
                    return true;
                }
                return false;
            }, PlayerLoopTiming.Update, token);
            
            return pressed;
        }
        
        /// <summary>
        /// Awaits either a key press or a mouse click, whichever comes first.
        /// Returns the result type and relevant data.
        /// </summary>
        public async UniTask<InputResult> AwaitKeyOrClick(Key key, CancellationToken token,
            int mouseButton = 0)
        {
            var result = new InputResult();
            
            await UniTask.WaitUntil(() =>
            {
                if (IsKeyDown(key))
                {
                    result.Type = InputResultType.Key;
                    result.Key = key;
                    return true;
                }
                if (IsMouseDown(mouseButton))
                {
                    result.Type = InputResultType.Mouse;
                    result.ScreenPosition = MousePosition;
                    return true;
                }
                return false;
            }, PlayerLoopTiming.Update, token);
            
            return result;
        }
        
        /// <summary>
        /// Awaits a confirm (left click or specified key) or cancel (right click or Escape).
        /// Returns true for confirm, false for cancel.
        /// </summary>
        public async UniTask<bool> AwaitConfirmOrCancel(CancellationToken token,
            Key confirmKey = Key.None, Key cancelKey = Key.Escape)
        {
            bool confirmed = false;
            
            await UniTask.WaitUntil(() =>
            {
                // Confirm: left click or confirm key
                if (IsMouseDown(0) || (confirmKey != Key.None && IsKeyDown(confirmKey)))
                {
                    confirmed = true;
                    return true;
                }
                // Cancel: right click or cancel key
                if (IsMouseDown(1) || IsKeyDown(cancelKey))
                {
                    confirmed = false;
                    return true;
                }
                return false;
            }, PlayerLoopTiming.Update, token);
            
            return confirmed;
        }
        
        /// <summary>
        /// Awaits a world click with a confirm/cancel pattern.
        /// Left click = confirm (returns position), Right click or Escape = cancel (returns null).
        /// Useful for targeted abilities: "Click to cast, right-click to cancel."
        /// </summary>
        public async UniTask<Vector3?> AwaitTargetedWorldClick(Camera cam, CancellationToken token,
            float fallbackPlaneY = 0f, LayerMask? mask = null, Key cancelKey = Key.Escape)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, token);
                
                // Cancel
                if (IsMouseDown(1) || IsKeyDown(cancelKey))
                    return null;
                
                // Confirm
                if (!IsMouseDown(0)) continue;
                
                if (TryMouseRaycast(cam, out var hit, mask))
                    return hit.point;
                
                var ray = cam.ScreenPointToRay(MousePosition);
                var plane = new Plane(Vector3.up, new Vector3(0f, fallbackPlaneY, 0f));
                if (plane.Raycast(ray, out float enter))
                    return ray.GetPoint(enter);
            }
            
            token.ThrowIfCancellationRequested();
            return null;
        }
        
        /// <summary>
        /// Awaits a targeted world click. Uses Camera.main.
        /// </summary>
        public async UniTask<Vector3?> AwaitTargetedWorldClick(CancellationToken token,
            float fallbackPlaneY = 0f, LayerMask? mask = null, Key cancelKey = Key.Escape)
        {
            return await AwaitTargetedWorldClick(Camera.main, token, fallbackPlaneY, mask, cancelKey);
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // INPUT RESULT
    // ═══════════════════════════════════════════════════════════════════════════════
    
    public struct InputResult
    {
        public InputResultType Type;
        public Key Key;
        public Vector2 ScreenPosition;
    }
    
    public enum InputResultType
    {
        None,
        Key,
        Mouse
    }
}
