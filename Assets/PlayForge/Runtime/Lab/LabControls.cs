using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// A player's controls for one unit, as a MOBA's (LAB-11, Decision 44): right-click the ground to move, right-click a
    /// foe it can target to attack it, Q, W, E and R for its abilities past its attack, 1 to 6 for its items' abilities,
    /// S to stop, Escape to cancel a cast. An ability that needs a target waits for a left-click, as its own click
    /// targeting reads it. It reads PlayForge's input, ForgeInput, so a test or a game's own input stands in, and gives
    /// its orders through a driver: its own, its unit's rules', or the lab's. The Creator Lab's take its controlled unit;
    /// the Demo's player is these, its hero's.
    /// </summary>
    [AddComponentMenu("PlayForge/Lab/Lab Controls")]
    public class LabControls : MonoBehaviour
    {
        [Tooltip("The camera the pointer aims through; with none, the lab's, or else the main camera")]
        public Camera Camera;

        [Tooltip("The layers a right-click finds the ground or a unit on, where it aims through its camera")]
        public LayerMask Layers = ~0;

        [Tooltip("The unit it controls; with none, the one the Creator Lab's author controls")]
        public LabUnit Unit;

        [Tooltip("The driver its orders go through; with none, its unit's rules' driver, or else the lab's")]
        public LabDriver Driver;

        [Tooltip("The keys that cast its abilities past its attack, in its entity's order")]
        public List<KeyCode> AbilityKeys = new() { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R };

        [Tooltip("The keys that cast its items' active abilities, in the order it took them")]
        public List<KeyCode> ItemKeys = new()
            { KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6 };

        [Tooltip("The key that stops it")]
        public KeyCode StopKey = KeyCode.S;

        [Tooltip("The key that cancels a cast waiting for its click")]
        public KeyCode CancelKey = KeyCode.Escape;

        /// <summary>The unit it controls now: its own, or the one <see cref="ControlledUnit"/> gives.</summary>
        public LabUnit Controlled => Unit ? Unit : ControlledUnit();

        /// <summary>The driver its orders to a unit go through: its own, the unit's rules', or the lab's.</summary>
        public ILabUnitDriver DriverFor(LabUnit unit)
        {
            if (Driver) return Driver;
            var fromRules = unit ? unit.Rules?.Driver : null;
            if (fromRules is Object asset && !asset) fromRules = null;
            return fromRules ?? LabBodyDriver.Instance;
        }

        protected virtual void Update()
        {
            var unit = Controlled;
            if (!unit || !unit.Alive) return;
            var input = ForgeInput.Current;
            var driver = DriverFor(unit);

            if (input.PointerPressed(1) && input.TryGetPointerPosition(out var pointer) && !PointerOverPanel(pointer))
                Order(unit, driver, pointer);
            for (int i = 0; i < AbilityKeys.Count; i++)
                if (input.KeyPressed(AbilityKeys[i])) Cast(unit, driver, AbilityAt(unit, i));
            for (int i = 0; i < ItemKeys.Count; i++)
                if (input.KeyPressed(ItemKeys[i])) Cast(unit, driver, ItemAbility(unit, i));
            if (input.KeyPressed(StopKey)) driver.Stop(unit);
            if (input.KeyPressed(CancelKey)) unit.CancelCasts();
        }

        // With no data, so an ability that needs a target asks for it, as its click targeting does
        private static void Cast(LabUnit unit, ILabUnitDriver driver, Ability ability)
        {
            if (ability) driver.Cast(unit, ability, null);
        }

        /// <summary>The unit it controls where none is set on it: the one the Creator Lab's author controls.</summary>
        protected virtual LabUnit ControlledUnit()
        {
            var lab = CreatorLab.Current;
            return lab ? lab.ControlledUnit : null;
        }

        /// <summary>The ability a key of its abilities casts: the unit's abilities past its attack, in order.</summary>
        protected virtual Ability AbilityAt(LabUnit unit, int index) => unit.Casts().ElementAtOrDefault(index);

        /// <summary>The ability a key of its items casts: the active ability of the item it took that many items in.</summary>
        protected virtual Ability ItemAbility(LabUnit unit, int index)
        {
            var abilities = unit.ItemAbilities();
            return index >= 0 && index < abilities.Count ? abilities[index] : null;
        }

        /// <summary>Whether a point of the screen is over a panel, which keeps a click there the panel's.</summary>
        protected virtual bool PointerOverPanel(Vector2 screen)
        {
            var lab = CreatorLab.Current;
            return lab && lab.Panels && lab.Panels.IsOverPanel(screen);
        }

        /// <summary>Whether the unit takes another for a foe to attack: off its side, standing, and one it can target.</summary>
        protected virtual bool IsFoe(LabUnit unit, LabUnit other) =>
            other && other != unit && other.Alive && !LabSides.AreAllies(unit.Gas, other.Gas) && unit.CanTarget(other);

        /// <summary>A right-click: a foe under the pointer to attack, or else the ground, or an ally, to walk to.</summary>
        protected virtual void Order(LabUnit unit, ILabUnitDriver driver, Vector2 screen)
        {
            if (!TryAim(screen, out var other, out var point)) return;
            if (IsFoe(unit, other)) driver.Attack(unit, other);
            else driver.MoveTo(unit, point);
        }

        /// <summary>
        /// What's under a point of the screen: the unit there, if any, and the point on the ground or the unit. Through the
        /// Creator Lab's own picking where it has no camera of its own and a lab is open; else through its camera, or the
        /// main one, on its layers.
        /// </summary>
        protected virtual bool TryAim(Vector2 screen, out LabUnit unit, out Vector3 point)
        {
            unit = null;
            point = default;
            var lab = CreatorLab.Current;
            if (!Camera && lab && lab.View) return lab.TryPick(screen, out unit, out point);

            if (!ForgeInput.TryAimThrough(Camera, screen, out var ray)) return false;
            Physics.SyncTransforms();
            if (!Physics.Raycast(ray, out var hit, 500f, Layers, QueryTriggerInteraction.Ignore)) return false;
            unit = hit.collider.GetComponentInParent<LabUnit>();
            point = hit.point;
            return true;
        }
    }
}
