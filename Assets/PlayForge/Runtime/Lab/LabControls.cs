using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>
    /// A player's controls for one unit, as a MOBA's: right-click the ground to move, right-click a foe it can target to
    /// attack it, Q, W, E and R for its abilities past its attack, 1 to 6 for its items' abilities, S to stop, Escape to
    /// cancel a cast. An ability that needs a target waits for a left-click, as its own click targeting reads it;
    /// meanwhile a ring on the ground shows the ability's reach, another ability's key casts that one instead, and a
    /// right-click order lets go of it. It reads PlayForge's input, ForgeInput, so a test or a game's own input stands
    /// in, and gives its orders through a driver: its own, its unit's rules', or the lab's. The Creator Lab's take its
    /// controlled unit; the Demo's player is these, its hero's.
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

        [Tooltip("A right-click order also lets go of a cast still choosing its target, as a MOBA's does")]
        public bool OrderCancelsTargeting = true;

        [Tooltip("While its unit chooses a target for an ability, a ring on the ground at the ability's reach")]
        public bool ShowReach = true;

        [Tooltip("The reach ring's color while the pointer is within it")]
        public Color ReachColor = new(0.45f, 0.8f, 1f, 0.9f);

        [Tooltip("The reach ring's color while the pointer is beyond it")]
        public Color BeyondColor = new(1f, 0.42f, 0.35f, 0.9f);

        [Tooltip("How wide the reach ring's line is, in metres")]
        [Min(0.01f)] public float ReachWidth = 0.08f;

        [Tooltip("The reach ring's material; with none, an unlit one of Unity's sprites")]
        public Material ReachMaterial;

        private const int RingPoints = 72;
        private LineRenderer ring;
        private static Material defaultRingMaterial;

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
            if (!unit || !unit.Alive)
            {
                HideReach();
                return;
            }
            var input = ForgeInput.Current;
            var driver = DriverFor(unit);

            if (input.PointerPressed(1) && input.TryGetPointerPosition(out var pointer) && !PointerOverPanel(pointer))
            {
                // An order: a cast still choosing its target is let go
                if (OrderCancelsTargeting) unit.CancelTargeting();
                Order(unit, driver, pointer);
            }
            for (int i = 0; i < AbilityKeys.Count; i++)
                if (input.KeyPressed(AbilityKeys[i])) Cast(unit, driver, AbilityAt(unit, i));
            for (int i = 0; i < ItemKeys.Count; i++)
                if (input.KeyPressed(ItemKeys[i])) Cast(unit, driver, ItemAbility(unit, i));
            if (input.KeyPressed(StopKey)) driver.Stop(unit);
            if (input.KeyPressed(CancelKey)) unit.CancelCasts();
            DrawReach(unit, input);
        }

        protected virtual void OnDisable() => HideReach();

        protected virtual void OnDestroy()
        {
            if (!ring) return;
            if (Application.isPlaying) Destroy(ring.gameObject);
            else DestroyImmediate(ring.gameObject);
        }

        /// <summary>
        /// The ring on the ground at the reach of the ability its unit chooses a target for, in the reach color while the
        /// pointer is within it and the beyond color past it; hidden while it chooses none, or the ability reaches anywhere.
        /// </summary>
        protected virtual void DrawReach(LabUnit unit, IForgeInput input)
        {
            float reach = ShowReach && unit.TryGetTargeting(out var ability) ? unit.Reach(ability) : 0f;
            if (reach <= 0f)
            {
                HideReach();
                return;
            }
            var line = Ring();
            line.enabled = true;
            line.widthMultiplier = ReachWidth;
            var center = unit.transform.position;
            for (int i = 0; i < RingPoints; i++)
            {
                float angle = i * Mathf.PI * 2f / RingPoints;
                line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * reach, 0.06f, Mathf.Sin(angle) * reach));
            }
            bool beyond = false;
            if (input.TryGetPointerPosition(out var pointer) && !PointerOverPanel(pointer) && TryAim(pointer, out var other, out var point))
            {
                var offset = (other ? other.transform.position : point) - center;
                offset.y = 0f;
                beyond = offset.magnitude > reach;
            }
            line.startColor = line.endColor = beyond ? BeyondColor : ReachColor;
        }

        /// <summary>The reach ring, hidden, as while its unit chooses no target.</summary>
        protected void HideReach()
        {
            if (ring) ring.enabled = false;
        }

        // A loop of points on the ground, made the first time it's drawn
        private LineRenderer Ring()
        {
            if (ring) return ring;
            var host = new GameObject("Reach Ring");
            ring = host.AddComponent<LineRenderer>();
            ring.loop = true;
            ring.useWorldSpace = true;
            ring.positionCount = RingPoints;
            ring.numCapVertices = 0;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sharedMaterial = ReachMaterial ? ReachMaterial : DefaultRingMaterial();
            return ring;
        }

        private static Material DefaultRingMaterial()
        {
            if (defaultRingMaterial) return defaultRingMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) return null;
            defaultRingMaterial = new Material(shader) { name = "Reach Ring", hideFlags = HideFlags.DontSave };
            return defaultRingMaterial;
        }

        // With no data, so an ability that needs a target asks for it, as its click targeting does
        private static void Cast(LabUnit unit, ILabUnitDriver driver, Ability ability)
        {
            if (ability) driver.Cast(unit, ability, null);
        }

        /// <summary>
        /// The keys, as a line a screen shows: "Right-click: move or attack · Q W E R: cast · 1–6: items · S: stop · Esc:
        /// cancel", as they're set.
        /// </summary>
        public string KeysLine() =>
            $"Right-click: move or attack  ·  {KeysWords(AbilityKeys)}: cast  ·  {KeysWords(ItemKeys)}: items  ·  "
            + $"{KeyName(StopKey)}: stop  ·  {KeyName(CancelKey)}: cancel";

        /// <summary>What lets go of a cast choosing its target, as "Esc or a right-click".</summary>
        public string CancelWords() => $"{KeyName(CancelKey)}{(OrderCancelsTargeting ? " or a right-click" : "")}";

        /// <summary>A key as its cap reads: 1 for Alpha 1, Esc for Escape.</summary>
        public static string KeyName(KeyCode key) => key switch
        {
            >= KeyCode.Alpha0 and <= KeyCode.Alpha9 => ((int)key - (int)KeyCode.Alpha0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            KeyCode.Escape => "Esc",
            _ => key.ToString(),
        };

        /// <summary>Keys as their caps read, a run of three or more digits as 1–6.</summary>
        public static string KeysWords(IReadOnlyList<KeyCode> keys)
        {
            if (keys == null || keys.Count == 0) return "";
            bool digits = keys.All(key => key is >= KeyCode.Alpha0 and <= KeyCode.Alpha9);
            bool run = digits && keys.Count > 2 && keys.Select((key, i) => (int)key - i).Distinct().Count() == 1;
            return run ? $"{KeyName(keys[0])}–{KeyName(keys[keys.Count - 1])}" : string.Join(" ", keys.Select(KeyName));
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
