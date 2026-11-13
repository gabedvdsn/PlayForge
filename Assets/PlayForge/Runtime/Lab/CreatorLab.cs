using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>What the lab's space holds (LAB-1, LAB-9).</summary>
    public enum LabMode
    {
        /// <summary>Test dummies the author puts down.</summary>
        Dummies,

        /// <summary>Teams of bots that fight.</summary>
        Battle,

        /// <summary>An empty space, where abilities still cast and effects still land.</summary>
        None,
    }

    /// <summary>
    /// A bot's place in a battle (LAB-9): what it's made of, its side, its level and where it starts, and what the author
    /// gave it since, which a reset gives again as it makes the bot anew.
    /// </summary>
    public sealed class LabSeat
    {
        public ScriptableObject Entity;
        public LabSide Side;
        public int Level;
        public Vector3 Home;
        public readonly List<Item> Items = new();
        public readonly List<Ability> Taught = new();
        public readonly Dictionary<Ability, int> AbilityLevels = new();

        /// <summary>The bot standing for it now, or null between a reset's take-out and its make.</summary>
        public LabUnit Unit;
    }

    /// <summary>
    /// The Creator Lab (FEAT-35): a space where an author puts units down, gives them their project's abilities, effects
    /// and items, casts, and watches the numbers move (`Docs/CREATOR_LAB_DESIGN.md`). It builds what it needs as it wakes:
    /// PlayForge, where the scene has none; the ground, a light and its camera; a world of its own, which it steps by its
    /// own time (LAB-8); its own caster, a neutral source for what the author drops; and its panels (LAB-5).
    /// <para>
    /// Window → PlayForge → Creator Lab opens it in a new scene (LAB-1). A test makes one with <see cref="Create"/> and
    /// drives it through the same calls the panels make.
    /// </para>
    /// </summary>
    public class CreatorLab : MonoBehaviour
    {
        /// <summary>The lab playing now.</summary>
        public static CreatorLab Current { get; private set; }

        /// <summary>The library a lab takes when it's given none: the editor's, which the lab's editor assembly sets.</summary>
        public static Func<ILabLibrary> DefaultLibrary;

        /// <summary>Opens an asset in The Forge, where the editor gives a way; null in a build.</summary>
        public static Action<UnityEngine.Object> OpenInForge;

        [Tooltip("The project's rules; with none, the lab makes its own, finding Health and Movement Speed by name")]
        public LabRules Rules;

        [Tooltip("The material units and the ground take, tinted; with none, the render pipeline's default")]
        public Material Material;

        [Tooltip("How wide the ground is, on a side")]
        public float GroundSize = 80f;

        [Tooltip("Whether it builds its panels; off, it runs with no UI, as a test drives it")]
        public bool ShowPanels = true;

        [Tooltip("How fast its time runs against Unity's, from a quarter to four times")]
        [Range(0.25f, 4f)] public float Speed = 1f;

        [Tooltip("The time a step of one frame takes, while paused")]
        public float StepSeconds = 1f / 60f;

        public ILabLibrary Library { get; private set; }
        public ILabRules ActiveRules { get; private set; }
        public LabCamera View { get; private set; }
        public LabPanels Panels { get; private set; }

        /// <summary>The lab's own world, where every unit it makes steps.</summary>
        public ProcessWorld World { get; private set; }

        public LabMode Mode { get; private set; } = LabMode.Dummies;

        /// <summary>The lab's own caster: on no side, with no body, the source of what's dropped with no unit selected.</summary>
        public LabUnit Caster { get; private set; }

        /// <summary>The units the author put down, the caster aside.</summary>
        public IReadOnlyList<LabUnit> Units => units;

        public LabUnit SelectedUnit { get; private set; }
        public ScriptableObject SelectedAsset { get; private set; }

        /// <summary>The unit the author controls, if any: moved and made to cast by the lab's controls (LAB-11).</summary>
        public LabUnit ControlledUnit { get; private set; }

        /// <summary>The lab's controls, which give the author's unit its orders through its driver (LAB-11).</summary>
        public LabControls Controls { get; private set; }

        /// <summary>Whether its time stands still.</summary>
        public bool Paused { get; set; }

        /// <summary>The lab's time, in seconds, since it began.</summary>
        public float Elapsed { get; private set; }

        /// <summary>Raised as what it holds or what's selected changes, for the panels to redraw.</summary>
        public event Action Changed;

        /// <summary>The battle's seats, a bot standing for each (LAB-9).</summary>
        public IReadOnlyList<LabSeat> Seats => seats;

        /// <summary>Whether the battle's bots fight.</summary>
        public bool Fighting { get; private set; }

        /// <summary>The side left standing as a battle ends, or null while it goes on or none is.</summary>
        public LabSide? Winner { get; private set; }

        /// <summary>Whether the battle is over: a side stands alone, or no one stands at all.</summary>
        public bool Ended { get; private set; }

        private readonly List<LabSeat> seats = new();
        private readonly List<LabUnit> units = new();
        private readonly List<UnityEngine.Object> made = new();
        private GameObject ground;
        private GameObject templates;
        private ILabLibrary givenLibrary;
        private bool stepOnce;

        // ═══════════════════════════════════════════════════════════════════════════
        // Making one
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A lab on an object of its own, playing the library given, or the default one, by the rules given.</summary>
        public static CreatorLab Create(ILabLibrary library = null, LabRules rules = null, bool panels = true)
        {
            var host = new GameObject("Creator Lab");
            host.SetActive(false);
            var lab = host.AddComponent<CreatorLab>();
            lab.givenLibrary = library;
            lab.Rules = rules;
            lab.ShowPanels = panels;
            host.SetActive(true);
            return lab;
        }

        private void Awake()
        {
            Current = this;
            BringUpPlayForge();
            Library = givenLibrary ?? DefaultLibrary?.Invoke() ?? new ListLibrary("Nothing", null);
            ActiveRules = Rules ? Rules : Own(LabRules.MakeDefault(Library));
            if (Rules) Rules.FindIn(Library);
            World = new ProcessWorld();
            BuildSpace();
            Caster = MakeCaster();
            Controls = gameObject.AddComponent<LabControls>();
            if (ShowPanels) Panels = gameObject.AddComponent<LabPanels>();
        }

        private void OnDestroy()
        {
            foreach (var unit in units)
                if (unit) unit.Remove();
            units.Clear();
            if (Caster) Caster.Remove();
            foreach (var thing in made)
                if (thing) Destroy(thing);
            made.Clear();
            if (ground) Destroy(ground);
            if (View) Destroy(View.gameObject);
            if (templates) Destroy(templates);
            if (Current == this) Current = null;
            LabSides.Forget();
        }

        private T Own<T>(T thing) where T : UnityEngine.Object
        {
            made.Add(thing);
            return thing;
        }

        // PlayForge's ProcessControl and GameRoot, brought up as a Bootstrapper brings them up, from a GameRoot made here,
        // where the scene has none, as the Battle sample brings them up
        private void BringUpPlayForge()
        {
            if (ProcessControl.Instance && GameRoot.Instance) return;

            templates = new GameObject("Creator Lab Templates") { hideFlags = HideFlags.HideAndDontSave };
            templates.SetActive(false);
            var root = new GameObject("Game Root");
            root.transform.SetParent(templates.transform, false);
            var gameRoot = root.AddComponent<GameRoot>();
            gameRoot.EntityData = MadeIdentity("Game Root");
            gameRoot.ProcessLifecycle = EProcessLifecycle.Synchronous;
            gameRoot.ProcessTiming = EProcessStepTiming.UpdateAndLate;

            // A Bootstrapper brings PlayForge up as it wakes, so it's filled in before it's switched on
            var host = new GameObject("Bootstrapper");
            host.SetActive(false);
            var bootstrapper = host.AddComponent<Bootstrapper>();
            bootstrapper.GameRootPrefab = gameRoot;
            bootstrapper.RegisterSets = new List<AttributeSet>();
            host.SetActive(true);
        }

        // An entity with an attribute set of one attribute it never uses, for the Game Root and the lab's caster
        private EntityIdentity MadeIdentity(string entityName)
        {
            var attribute = Own(ScriptableObject.CreateInstance<Attribute>());
            attribute.hideFlags = HideFlags.DontSave;
            attribute.name = attribute.Name = $"{entityName} Unused";
            attribute.Textures = new List<TextureItem>();

            var set = Own(ScriptableObject.CreateInstance<AttributeSet>());
            set.hideFlags = HideFlags.DontSave;
            set.name = set.Name = $"{entityName} Set";
            set.AssetTag = Tag.GenerateAsUnique($"Lab Set {entityName}");
            set.Attributes = new List<AttributeSetElement>
            {
                new()
                {
                    Attribute = attribute,
                    Current = new AttributeMagnitudeSpec { Magnitude = 1f, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                    Base = new AttributeMagnitudeSpec { Magnitude = 1f, RealMagnitude = EMagnitudeOperation.UseMagnitude },
                    Overflow = new AttributeOverflowData { Policy = EAttributeOverflowPolicy.Unlimited },
                    RetentionGroup = Tags.DEFAULT,
                    Constraints = new AttributeConstraints { AutoClamp = true, RoundingMode = EAttributeRoundingPolicy.None },
                },
            };
            set.SubSets = new List<AttributeSet>();
            set.WorkerGroup = new StandardWorkerGroup();

            var entity = Own(ScriptableObject.CreateInstance<EntityIdentity>());
            entity.hideFlags = HideFlags.DontSave;
            entity.name = entity.Name = entityName;
            entity.Description = "";
            entity.Textures = new List<TextureItem>();
            entity.AssetTag = Tag.GenerateAsUnique($"Lab Entity {entityName}");
            entity.Affiliation = new List<Tag>();
            entity.GrantedTags = new List<Tag>();
            entity.ContextTags = new List<Tag>();
            entity.MaxLevel = 1;
            entity.StartingAbilities = new List<Ability>();
            entity.StartingItems = new List<StartingItemContainer>();
            entity.AttributeSet = set;
            entity.WorkerGroup = new StandardWorkerGroup();
            return entity;
        }

        // The ground, a light from above, and the camera looking down at the middle
        private void BuildSpace()
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Creator Lab Ground";
            ground.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f);
            LabUnit.Tint(ground.GetComponent<Renderer>(), new Color(0.2f, 0.21f, 0.23f), Material);

            var sun = new GameObject("Creator Lab Light").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.1f;
            sun.transform.rotation = Quaternion.Euler(55f, -30f, 0f);
            sun.transform.SetParent(ground.transform, true);

            var eye = new GameObject("Creator Lab Camera");
            var camera = eye.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.11f, 0.12f, 0.13f);
            camera.fieldOfView = 50f;
            camera.farClipPlane = 500f;
            eye.tag = "MainCamera";
            View = eye.AddComponent<LabCamera>();
            View.Remember();
            View.Place();
        }

        private LabUnit MakeCaster()
        {
            var caster = LabUnit.Create(new LabUnitSpec
            {
                Identity = MadeIdentity("The Lab"), Rules = ActiveRules, Side = LabSides.Neutral, World = World,
                Ring = false, Name = "The Lab",
            });
            if (caster == null) return null;
            caster.SteppedByLab = true;
            // No body, so nothing picks or sees it
            foreach (var renderer in caster.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            if (caster.TryGetComponent<Collider>(out var body)) body.enabled = false;
            return caster;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Time (LAB-8)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Goes one step of <see cref="StepSeconds"/> while paused.</summary>
        public void StepFrame() => stepOnce = true;

        /// <summary>
        /// Steps the lab's time by a time given: its world, so its entities, their abilities and their effects go on;
        /// then each unit's body and each dummy's count. Unity's own time is left as it is.
        /// </summary>
        public void Step(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            Elapsed += deltaTime;
            World.Step(deltaTime);
            for (int i = units.Count - 1; i >= 0; i--)
            {
                var unit = units[i];
                if (!unit)
                {
                    units.RemoveAt(i);
                    continue;
                }
                unit.Step(deltaTime);
                if (unit.TryGetComponent<LabDummy>(out var dummy)) dummy.Step(deltaTime);
                if (unit.TryGetComponent<LabBot>(out var bot)) bot.Step(deltaTime);
            }
            if (Caster) Caster.Step(deltaTime);
            if (Fighting) Judge();
        }

        // A battle is over as no more than one side has a bot standing
        private void Judge()
        {
            LabSide? standing = null;
            foreach (var seat in seats)
            {
                if (!seat.Unit || !seat.Unit.Alive) continue;
                if (standing.HasValue && !standing.Value.Tag.Equals(seat.Side.Tag)) return;
                standing = seat.Side;
            }
            Winner = standing;
            Ended = true;
            SetFighting(false);
            Changed?.Invoke();
        }

        private void Update()
        {
            if (!Paused) Step(ForgeTime.UnscaledDeltaTime * Mathf.Clamp(Speed, 0.25f, 4f));
            else if (stepOnce) Step(StepSeconds);
            stepOnce = false;
            ReadSpace();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Units (LAB-2, LAB-4)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A unit of an entity or an attribute set, on a side, at a point, in the lab's world.</summary>
        public LabUnit AddUnit(ScriptableObject entityOrSet, LabSide side, Vector3 at, int level = 0, bool stays = false)
        {
            var identity = entityOrSet as EntityIdentity;
            var set = entityOrSet as AttributeSet;
            if (identity == null && set == null) return null;
            var unit = LabUnit.Create(new LabUnitSpec
            {
                Identity = identity, Set = set, Rules = ActiveRules, Side = side, Level = level, At = at,
                Facing = Quaternion.LookRotation(Vector3.back), World = World, Material = Material, Stays = stays,
            });
            if (unit == null) return null;
            unit.SteppedByLab = true;
            units.Add(unit);
            Changed?.Invoke();
            return unit;
        }

        /// <summary>A test dummy (LAB-4): a unit that stands, counts what it takes, mends, and is revived where it falls.</summary>
        public LabDummy AddDummy(ScriptableObject entityOrSet, LabSide side, Vector3 at, int level = 0)
        {
            var unit = AddUnit(entityOrSet, side, at, level, stays: true);
            if (unit == null) return null;
            var dummy = unit.gameObject.AddComponent<LabDummy>();
            Changed?.Invoke();
            return dummy;
        }

        /// <summary>Takes a unit out of the lab, and its seat where it's a bot.</summary>
        public void RemoveUnit(LabUnit unit)
        {
            if (!unit || !units.Remove(unit)) return;
            if (SelectedUnit == unit) SelectedUnit = null;
            if (ControlledUnit == unit) ReleaseControl();
            seats.RemoveAll(seat => seat.Unit == unit);
            unit.Remove();
            Changed?.Invoke();
        }

        /// <summary>Takes every unit out, and every seat: the battle with them.</summary>
        public void Clear()
        {
            foreach (var unit in units.ToArray()) RemoveUnit(unit);
            seats.Clear();
            Fighting = false;
            Winner = null;
            Ended = false;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Battle (LAB-9)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A bot on a side, of an entity or a set, at a level: a seat, which a reset makes it from again, and the bot
        /// standing for it. With no point given, it stands in its side's line.
        /// </summary>
        public LabBot AddBot(ScriptableObject entityOrSet, LabSide side, Vector3? at = null, int level = 0)
        {
            if (entityOrSet is not (EntityIdentity or AttributeSet)) return null;
            var seat = new LabSeat { Entity = entityOrSet, Side = side, Level = level, Home = at ?? NextSpot(side) };
            seats.Add(seat);
            return MakeBot(seat);
        }

        // The bot for a seat, given what the author gave it, fighting as the battle does
        private LabBot MakeBot(LabSeat seat)
        {
            var unit = AddUnit(seat.Entity, seat.Side, seat.Home, seat.Level, stays: true);
            if (unit == null) return null;
            seat.Unit = unit;
            unit.transform.rotation = Quaternion.LookRotation(Facing(seat.Side));
            foreach (var ability in seat.Taught) unit.Teach(ability, Mathf.Max(1, ability.StartingLevel));
            foreach (var pair in seat.AbilityLevels) unit.ApplyLevel(pair.Key, pair.Value);
            foreach (var item in seat.Items) unit.Give(item, Mathf.Max(1, item.StartingLevel));
            var bot = unit.gameObject.AddComponent<LabBot>();
            bot.Fighting = Fighting;
            Changed?.Invoke();
            return bot;
        }

        // Each side's line: Blue to the south, Red to the north, Green to the west and Gold to the east
        private Vector3 NextSpot(LabSide side)
        {
            int index = seats.Count(seat => seat.Side.Tag.Equals(side.Tag));
            float along = (index % 8 - 3.5f) * 2.5f;
            float back = index / 8 * 2.5f;
            if (side.Tag.Equals(LabSides.Red.Tag)) return new Vector3(along, 0f, 8f + back);
            if (side.Tag.Equals(LabSides.Green.Tag)) return new Vector3(-10f - back, 0f, along);
            if (side.Tag.Equals(LabSides.Gold.Tag)) return new Vector3(10f + back, 0f, along);
            return new Vector3(along, 0f, -8f - back);
        }

        private static Vector3 Facing(LabSide side)
        {
            if (side.Tag.Equals(LabSides.Red.Tag)) return Vector3.back;
            if (side.Tag.Equals(LabSides.Green.Tag)) return Vector3.right;
            if (side.Tag.Equals(LabSides.Gold.Tag)) return Vector3.left;
            return Vector3.forward;
        }

        /// <summary>
        /// A quick battle: Blue against Red, so many a side, each of the entity given, or the one selected, or else picked
        /// at random from the project's entities.
        /// </summary>
        public void QuickBattle(int perSide = 4, ScriptableObject entity = null)
        {
            SetMode(LabMode.Battle);
            Clear();
            entity ??= SelectedAsset is EntityIdentity or AttributeSet ? SelectedAsset : null;
            var pool = new List<ScriptableObject>();
            if (entity) pool.Add(entity);
            else
            {
                pool.AddRange(Library.Assets(LabAssetKind.Entities));
                if (pool.Count == 0) pool.AddRange(Library.Assets(LabAssetKind.AttributeSets));
            }
            if (pool.Count == 0) return;
            for (int side = 0; side < 2; side++)
            for (int i = 0; i < Mathf.Max(1, perSide); i++)
            {
                var pick = pool[Mathf.Min(pool.Count - 1, (int)(ForgeRandom.Value * pool.Count))];
                AddBot(pick, side == 0 ? LabSides.Blue : LabSides.Red);
            }
            Changed?.Invoke();
        }

        /// <summary>The battle's bots fight, and the lab's time runs.</summary>
        public void Play()
        {
            Winner = null;
            Ended = false;
            Paused = false;
            SetFighting(true);
            Changed?.Invoke();
        }

        /// <summary>Every bot back where it started, at full, made anew from its seat with what it was given; none fight.</summary>
        public void ResetBattle()
        {
            SetFighting(false);
            Winner = null;
            Ended = false;
            // The author keeps the seat they control, on its new bot
            var controlled = SeatOf(ControlledUnit);
            if (controlled != null) ReleaseControl();
            foreach (var seat in seats)
            {
                if (seat.Unit)
                {
                    units.Remove(seat.Unit);
                    if (SelectedUnit == seat.Unit) SelectedUnit = null;
                    seat.Unit.Remove();
                }
                seat.Unit = null;
            }
            foreach (var seat in seats.ToArray()) MakeBot(seat);
            if (controlled?.Unit) TakeControl(controlled.Unit);
            Changed?.Invoke();
        }

        // Each bot fights as the battle does, but the one the author controls
        private void SetFighting(bool fighting)
        {
            Fighting = fighting;
            foreach (var seat in seats)
                if (seat.Unit && seat.Unit != ControlledUnit && seat.Unit.TryGetComponent<LabBot>(out var bot)) bot.Fighting = fighting;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // The author's unit (LAB-11)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Puts down a unit of an entity or a set, on a side, at a point, and takes control of it.</summary>
        public LabUnit PutDownOwnUnit(ScriptableObject entityOrSet, LabSide side, Vector3 at, int level = 0)
        {
            var unit = AddUnit(entityOrSet, side, at, level);
            if (unit) TakeControl(unit);
            return unit;
        }

        /// <summary>
        /// Takes control of a unit, letting go of any other: the lab's controls order it about, and a bot stops fighting
        /// until it's let go.
        /// </summary>
        public void TakeControl(LabUnit unit)
        {
            // Only a unit of the lab's: not its caster, nor one it has taken out, as a reset's bots, which go at the frame's end
            if (!unit || !units.Contains(unit) || ControlledUnit == unit) return;
            ReleaseControl();
            ControlledUnit = unit;
            if (unit.TryGetComponent<LabBot>(out var bot)) bot.Fighting = false;
            unit.Stop();
            Changed?.Invoke();
        }

        /// <summary>Lets go of the author's unit: a bot fights again as the battle does, and the camera follows it no more.</summary>
        public void ReleaseControl()
        {
            var unit = ControlledUnit;
            ControlledUnit = null;
            if (!unit) return;
            if (unit.TryGetComponent<LabBot>(out var bot)) bot.Fighting = Fighting;
            if (View && View.Following == unit.transform) View.Following = null;
            Changed?.Invoke();
        }

        /// <summary>Has the camera follow a unit, or none.</summary>
        public void Follow(LabUnit unit)
        {
            if (View) View.Following = unit ? unit.transform : null;
        }

        /// <summary>The seat a bot stands for, or null for a unit that's no bot.</summary>
        public LabSeat SeatOf(LabUnit unit) => unit ? seats.Find(seat => seat.Unit == unit) : null;

        /// <summary>Each dummy back where it was put, at full health, its count cleared.</summary>
        public void ResetDummies()
        {
            foreach (var unit in units)
            {
                if (!unit || !unit.TryGetComponent<LabDummy>(out var dummy)) continue;
                unit.GoHome();
                if (!unit.Alive) unit.Gas.Revive(unit.Home);
                dummy.MendNow();
                dummy.Clear();
            }
            Changed?.Invoke();
        }

        /// <summary>What the space holds: dummies, a battle, or nothing. Each mode starts with its units cleared.</summary>
        public void SetMode(LabMode mode)
        {
            if (mode == Mode) return;
            Mode = mode;
            Clear();
            Changed?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Selecting
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Selects a unit, which then shows in the middle dock and is the source of what's dropped.</summary>
        public void Select(LabUnit unit)
        {
            SelectedUnit = unit;
            if (unit) SelectedAsset = null;
            Changed?.Invoke();
        }

        /// <summary>Selects an asset, which then shows in the middle dock, level by level.</summary>
        public void Select(ScriptableObject asset)
        {
            SelectedAsset = asset;
            if (asset) SelectedUnit = null;
            Changed?.Invoke();
        }

        /// <summary>The unit under a point of the screen, if any, and the point on the ground under it.</summary>
        public bool TryPick(Vector2 screen, out LabUnit unit, out Vector3 point)
        {
            unit = null;
            point = default;
            if (!View) return false;
            // A unit put down or moved this frame is where its body is, not where physics last saw it
            Physics.SyncTransforms();
            var ray = View.Camera.ScreenPointToRay(screen);
            if (Physics.Raycast(ray, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            {
                unit = hit.collider.GetComponentInParent<LabUnit>();
                if (unit == Caster) unit = null;
                point = unit ? unit.transform.position : hit.point;
                return true;
            }
            return View.GroundAt(screen, out point);
        }

        // Esc ends a cast waiting for its click; a left-click in the space selects the unit under it, or nothing over
        // the ground
        private void ReadSpace()
        {
            var input = ForgeInput.Current;
            if (input.KeyPressed(KeyCode.Escape)) CancelCasts();
            if (!input.PointerPressed(0) || !input.TryGetPointerPosition(out var pointer)) return;
            if (Panels && Panels.IsOverPanel(pointer)) return;
            if (TryPick(pointer, out var unit, out _)) Select(unit);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // What's dropped (LAB-6)
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// An asset dropped on a point of the screen: an effect lands on the unit there, an ability is cast at it or at the
        /// ground, an item is given to it, and an entity or a set is put down there as a dummy on the side given.
        /// </summary>
        public bool Drop(ScriptableObject asset, Vector2 screen, LabSide? side = null)
        {
            if (!asset || !TryPick(screen, out var unit, out var point)) return false;
            return DropAt(asset, unit, point, side);
        }

        /// <summary>An asset dropped on a unit, or on the ground at a point where the unit is null.</summary>
        public bool DropAt(ScriptableObject asset, LabUnit unit, Vector3 point, LabSide? side = null)
        {
            switch (asset)
            {
                case GameplayEffect effect:
                    return unit && ApplyEffect(effect, unit);
                case Ability ability:
                    return unit ? CastAt(ability, unit) : CastAt(ability, point);
                case Item item:
                    return unit && GiveItem(item, unit);
                case EntityIdentity or AttributeSet:
                    return AddDummy(asset, side ?? LabSides.Red, point) != null;
                default:
                    return false;
            }
        }

        /// <summary>The unit what's dropped comes from: the one selected, where it's alive, or the lab's own caster.</summary>
        public LabUnit Source(LabUnit besides = null) =>
            SelectedUnit && SelectedUnit.Alive && SelectedUnit != besides ? SelectedUnit : Caster;

        /// <summary>An effect lands on a unit, from the unit given or the lab's source.</summary>
        public bool ApplyEffect(GameplayEffect effect, LabUnit onto, LabUnit from = null)
        {
            if (!effect || !onto || onto.Gas == null) return false;
            var source = from ? from : Source();
            if (!source || source.Gas == null) return false;
            var origin = IEffectOrigin.GenerateSourceDerivation(source.Gas);
            return onto.Gas.ApplyGameplayEffect(onto.Gas.GenerateEffectSpec(origin, effect));
        }

        /// <summary>
        /// The lab's source casts an ability at a unit, its target in the data, as a click targeting takes with no click.
        /// A source that doesn't know the ability learns it first; the lab's caster stands beside the target to cast.
        /// </summary>
        public bool CastAt(Ability ability, LabUnit target)
        {
            if (!ability || !target) return false;
            var caster = Prepare(ability, target.transform.position, target);
            return caster && caster.Cast(ability, caster.DataAt(target));
        }

        /// <summary>The lab's source casts an ability at a point on the ground; one that needs a unit waits for a click.</summary>
        public bool CastAt(Ability ability, Vector3 point)
        {
            if (!ability) return false;
            var caster = Prepare(ability, point, null);
            return caster && caster.Cast(ability, caster.DataAt(point));
        }

        // The source, knowing the ability; the lab's caster put beside where it's cast
        private LabUnit Prepare(Ability ability, Vector3 at, LabUnit target)
        {
            var caster = Source(target);
            if (!caster) return null;
            if (caster == Caster) caster.transform.position = new Vector3(at.x, 0f, at.z - 1.5f);
            var abilities = caster.Gas.GetAbilitySystem();
            if (abilities == null) return null;
            if (!abilities.TryGetAbilityContainer(ability, out _) && !caster.Teach(ability, Mathf.Max(1, ability.StartingLevel)))
                return null;
            return caster;
        }

        /// <summary>An item given to a unit, equipped, at its starting level; a bot's seat keeps it for a reset.</summary>
        public bool GiveItem(Item item, LabUnit to)
        {
            if (!item || !to) return false;
            to.Give(item, Mathf.Max(1, item.StartingLevel));
            SeatOf(to)?.Items.Add(item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>An item taken from a unit, all of it.</summary>
        public void TakeItem(Item item, LabUnit from)
        {
            if (!item || !from) return;
            from.Take(item);
            SeatOf(from)?.Items.RemoveAll(each => each == item);
            Changed?.Invoke();
        }

        /// <summary>An ability taught a unit, at its starting level; a bot's seat keeps it for a reset.</summary>
        public bool Teach(Ability ability, LabUnit to)
        {
            if (!ability || !to || !to.Teach(ability, Mathf.Max(1, ability.StartingLevel))) return false;
            SeatOf(to)?.Taught.Add(ability);
            Changed?.Invoke();
            return true;
        }

        /// <summary>An ability a unit knows, taken.</summary>
        public void Forget(Ability ability, LabUnit from)
        {
            if (!ability || !from) return;
            from.Forget(ability);
            if (SeatOf(from) is { } seat)
            {
                seat.Taught.Remove(ability);
                seat.AbilityLevels.Remove(ability);
            }
            Changed?.Invoke();
        }

        /// <summary>An ability's level on a unit; a bot's seat keeps it for a reset.</summary>
        public void SetAbilityLevel(Ability ability, LabUnit on, int level)
        {
            if (!ability || !on) return;
            level = Mathf.Clamp(level, 1, Mathf.Max(1, ability.MaxLevel));
            on.ApplyLevel(ability, level);
            if (SeatOf(on) is { } seat) seat.AbilityLevels[ability] = level;
            Changed?.Invoke();
        }

        /// <summary>Ends what the lab's caster and the selected unit are casting, as Esc does.</summary>
        public void CancelCasts()
        {
            if (Caster) Caster.CancelCasts();
            if (SelectedUnit) SelectedUnit.CancelCasts();
        }

        /// <summary>Raises <see cref="Changed"/>, for a change made to a unit from outside.</summary>
        public void Announce() => Changed?.Invoke();
    }
}
