using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// A hero or the dummy in the arena: a GameplayAbilitySystem on a body of its own, which walks where it's sent, attacks
    /// what it's set on, casts its abilities and its items', and holds its items. Its attributes, tags, levels and Local
    /// Data are the GAS's; this is the arena's side of it.
    /// </summary>
    public class DemoUnit : MonoBehaviour
    {
        public DemoGame Game { get; private set; }
        public DemoSeat Seat { get; private set; }
        public GameplayAbilitySystem Gas { get; private set; }

        /// <summary>The entity it stands for, the roster's asset.</summary>
        public EntityIdentity Identity { get; private set; }

        public bool Alive => Gas != null && !Gas.IsDead;
        public bool IsPlayer => Seat != null && Seat.IsPlayer;
        public bool IsDummy => Seat != null && Seat.IsDummy;
        public string Name => Identity != null ? Identity.GetName() : name;
        public DemoRoster Roster => Game != null ? Game.Roster : null;

        private ProcessRelay relay;
        private EntityIdentity copy;
        private Vector3? destination;
        private DemoUnit target;
        private int holds;

        // ═══════════════════════════════════════════════════════════════════════════
        // Making one
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A unit for a seat: a body in its color, and its entity, registered at the seat's level on the seat's side, with
        /// the seat's abilities' levels and its items.
        /// </summary>
        public static DemoUnit Create(DemoGame game, DemoSeat seat, EntityIdentity identity, Color color, Vector3 at, Quaternion facing)
        {
            var host = new GameObject(identity.GetName());
            host.transform.SetPositionAndRotation(new Vector3(at.x, 0f, at.z), facing);
            var collider = host.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.radius = 0.55f;
            collider.height = 2f;

            var unit = host.AddComponent<DemoUnit>();
            unit.Game = game;
            unit.Seat = seat;
            unit.Identity = identity;
            unit.MakeBody(color, game.BodyMaterial);

            // A copy of the entity that starts at the seat's level, so its attributes are worked out at that level as it
            // starts; its abilities and items are the roster's own assets
            unit.copy = Instantiate(identity);
            unit.copy.name = identity.name;
            unit.copy.StartingLevel = Mathf.Clamp(seat.Level, 1, Mathf.Max(1, identity.MaxLevel));
            unit.Gas = GameplayAbilitySystem.AddToGameObject(host, unit.copy);
            unit.Gas.ProcessLifecycle = EProcessLifecycle.Synchronous;
            unit.Gas.ProcessTiming = EProcessStepTiming.UpdateAndLate;

            var data = ProcessDataPacket.Default();
            data.SetPrimary(Tags.AFFILIATION, new List<Tag> { game.SideOf(seat) });
            if (!ProcessControl.Register(unit.Gas).With(data).Run(out unit.relay))
            {
                ForgeLog.Error($"Demo: ProcessControl refused {identity.GetName()}.");
                return unit;
            }

            foreach (var ability in identity.StartingAbilities)
                if (ability) unit.ApplyLevel(ability, seat.LevelOf(ability));
            foreach (var held in seat.Items)
                unit.Give(held.Item, held.Level, held.Count);
            return unit;
        }

        // A capsule in its color, with a nose to show where it faces
        private void MakeBody(Color color, Material material)
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            Tint(body.GetComponent<Renderer>(), color, material);

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            Destroy(nose.GetComponent<Collider>());
            nose.transform.SetParent(transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.45f, 0.45f);
            nose.transform.localScale = new Vector3(0.25f, 0.18f, 0.35f);
            Tint(nose.GetComponent<Renderer>(), Color.Lerp(color, Color.black, 0.45f), material);
        }

        private static void Tint(Renderer renderer, Color color, Material material)
        {
            if (renderer == null) return;
            if (material != null) renderer.sharedMaterial = material;
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>Takes the unit out: its entity ends, and its body goes.</summary>
        public void Remove()
        {
            if (relay != null) relay.TerminateImmediate();
            relay = null;
            if (this != null && gameObject != null) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (copy) Destroy(copy);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // What it has
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>An attribute's value now, or 0 where it has none.</summary>
        public float Value(Attribute attribute) =>
            Gas != null && attribute != null && Gas.TryGetAttributeValue(attribute, out var value) ? value.CurrentValue : 0f;

        /// <summary>An attribute's base, as the most a pool like Health holds.</summary>
        public float Base(Attribute attribute) =>
            Gas != null && attribute != null && Gas.TryGetAttributeValue(attribute, out var value) ? value.BaseValue : 0f;

        /// <summary>The share of its health it has left.</summary>
        public float HealthShare
        {
            get
            {
                float most = Base(Roster?.Health);
                return most > 0f ? Mathf.Clamp01(Value(Roster.Health) / most) : 0f;
            }
        }

        /// <summary>Whether it has a tag now.</summary>
        public bool Has(Tag tag) => Gas != null && tag.IsValid && Gas.GetTagCache() is { } tags && tags.GetWeight(tag) > 0;

        /// <summary>A float its entity's Local Data holds, or the fallback.</summary>
        public float LocalFloat(Tag key, float fallback) =>
            Gas != null && key.IsValid && Gas.TryGetLocalData(key, out var data) ? data.floatValue : fallback;

        /// <summary>How near it must stand to attack.</summary>
        public float AttackRange => LocalFloat(Roster != null ? Roster.AttackRange : default, 2f);

        /// <summary>Whether it can see and target another: not while that one is invisible or untargetable to it.</summary>
        public bool CanTarget(DemoUnit other)
        {
            if (other == null || !other.Alive || other == this) return false;
            if (DemoTeams.AreAllies(Gas, other.Gas)) return true;
            return !other.Has(Roster.Invisible) && !other.Has(Roster.Untargetable);
        }

        public float DistanceTo(DemoUnit other) => other ? Flat(other.transform.position - transform.position).magnitude : float.MaxValue;

        // ═══════════════════════════════════════════════════════════════════════════
        // Abilities and items
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The ability in a slot: 0 its attack, 1 to 3 its Q, W and E.</summary>
        public Ability AbilityAt(int slot) =>
            Identity != null && slot >= 0 && slot < Identity.StartingAbilities.Count ? Identity.StartingAbilities[slot] : null;

        /// <summary>The ability the seat's item at an index gives, or null.</summary>
        public Ability ItemAbilityAt(int index) =>
            Seat != null && index >= 0 && index < Seat.Items.Count ? Seat.Items[index].Item?.ActiveAbility : null;

        public bool Ready(Ability ability)
        {
            if (!Alive || ability == null) return false;
            var abilities = Gas.GetAbilitySystem();
            return abilities != null && abilities.TryGetAbilityContainer(ability, out var container) && abilities.CanActivateAbility(container.Index);
        }

        public bool Casting(Ability ability)
        {
            if (!Alive || ability == null) return false;
            var abilities = Gas.GetAbilitySystem();
            return abilities != null && abilities.TryGetAbilityContainer(ability, out var container) && container.IsActive;
        }

        /// <summary>The seconds left of an ability's cooldown, or 0.</summary>
        public float CooldownLeft(Ability ability) =>
            Alive && ability != null && ability.Cooldown != null && Gas.TryGetEffectContainer(ability.Cooldown, out var cooldown)
                ? Mathf.Max(0f, cooldown.DurationRemaining) : 0f;

        /// <summary>Casts an ability with the data given, or none, where its targeting then waits for a click.</summary>
        public bool Cast(Ability ability, ProcessDataPacket data = null, EAbilityActivationPolicyExtended? policy = null)
        {
            if (!Alive || ability == null) return false;
            var abilities = Gas.GetAbilitySystem();
            if (abilities == null || !abilities.TryGetAbilityContainer(ability, out var container)) return false;
            return abilities.TryActivateAbility(abilities.CreateActivationRequest(container.Index, data, policy));
        }

        /// <summary>Ends what it's casting: an ability still waiting for its click among them.</summary>
        public void CancelCasts()
        {
            if (!Alive) return;
            var abilities = Gas.GetAbilitySystem();
            foreach (var ability in Identity.StartingAbilities)
                if (ability && Casting(ability)) abilities.Inject(ability, InterruptSequenceInjection.Instance);
            foreach (var held in Seat.Items)
                if (held.Item && held.Item.ActiveAbility && Casting(held.Item.ActiveAbility)) abilities.Inject(held.Item.ActiveAbility, InterruptSequenceInjection.Instance);
        }

        /// <summary>
        /// The data an ability cast at another unit carries: the unit, its place, and, for the AI, its difficulty, which
        /// every damage effect multiplies by.
        /// </summary>
        public ProcessDataPacket DataAt(DemoUnit other)
        {
            var data = ProcessDataPacket.Default();
            if (other != null && other.Gas != null)
            {
                data.SetPrimary(Tags.TARGET_REAL, (ITarget)other.Gas);
                data.SetPrimary(Tags.POSITION, other.transform.position);
            }
            if (!IsPlayer && Roster != null && Roster.Difficulty.IsValid) data.SetByCaller(Roster.Difficulty, Game.DifficultyMultiplier);
            return data;
        }

        /// <summary>The data of a cast away from another unit, as an escape's: a point a few steps the other way.</summary>
        public ProcessDataPacket DataAway(DemoUnit other, float distance = 6f)
        {
            var data = DataAt(null);
            var away = other != null ? Flat(transform.position - other.transform.position) : transform.forward;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            data.SetPrimary(Tags.POSITION, transform.position + away.normalized * distance);
            return data;
        }

        /// <summary>An ability's level on it now.</summary>
        public void ApplyLevel(Ability ability, int level)
        {
            if (Gas == null || ability == null) return;
            Gas.SetLevel(ability.GetAssetTag(), new IntValuePair(level, Mathf.Max(level, ability.MaxLevel)));
        }

        /// <summary>The level an ability is at on it.</summary>
        public int LevelOf(BaseForgeLevelProvider provider) =>
            Gas != null && provider != null ? Gas.GetLevel(provider.GetAssetTag()).CurrentValue : 0;

        /// <summary>Gives it an item, equipped, at a level, or more of one that stacks.</summary>
        public void Give(Item item, int level, int count = 1)
        {
            var items = Gas != null ? Gas.GetItemSystem() : null;
            if (items == null || item == null) return;
            if (!items.HasItem(item))
            {
                if (!items.GiveAndEquipItem(item, level, out _)) return;
                count--;
            }
            if (count > 0) items.GiveItem(item, count);
        }

        /// <summary>Takes an item from it, all of it.</summary>
        public void Take(Item item)
        {
            var items = Gas != null ? Gas.GetItemSystem() : null;
            if (items == null || item == null) return;
            for (int guard = 0; guard < 16 && items.HasItem(item); guard++)
                if (!items.RemoveItem(item)) break;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Orders
        // ═══════════════════════════════════════════════════════════════════════════

        public void MoveTo(Vector3 point)
        {
            target = null;
            destination = new Vector3(point.x, 0f, point.z);
        }

        public void Attack(DemoUnit other)
        {
            destination = null;
            target = other;
        }

        public void Stop()
        {
            destination = null;
            target = null;
        }

        /// <summary>The unit it's set on, if any.</summary>
        public DemoUnit Target => target;

        /// <summary>Holds it still and deaf to orders, as a dash does while it carries it.</summary>
        public void Hold(bool held) => holds = Mathf.Max(0, holds + (held ? 1 : -1));

        private bool shown = true;

        // Out of sight while it's dead, and to the other side while it's invisible
        private void ShowState()
        {
            bool visible = Alive && !(Roster != null && !IsPlayer && Has(Roster.Invisible));
            if (visible == shown) return;
            shown = visible;
            foreach (var each in GetComponentsInChildren<Renderer>()) each.enabled = visible;
            if (TryGetComponent<Collider>(out var body)) body.enabled = Alive;
        }

        private void Update()
        {
            ShowState();
            if (!Alive || holds > 0 || Roster == null) return;
            bool stuck = Has(Roster.Stunned) || Has(Roster.Channeling);
            float speed = Mathf.Max(0f, Value(Roster.MovementSpeed));

            if (target != null)
            {
                if (!CanTarget(target))
                {
                    target = null;
                    return;
                }
                var to = Flat(target.transform.position - transform.position);
                if (to.magnitude > AttackRange)
                {
                    if (!stuck) Step(target.transform.position, speed);
                    return;
                }
                Face(to);
                var attack = AbilityAt(0);
                if (!stuck && Ready(attack)) Cast(attack, DataAt(target), EAbilityActivationPolicyExtended.ActivateIfIdle);
                return;
            }

            if (destination.HasValue && !stuck && Step(destination.Value, speed)) destination = null;
        }

        // A step toward a point at a speed; true once it's there
        private bool Step(Vector3 point, float speed)
        {
            var to = Flat(point - transform.position);
            float distance = to.magnitude;
            if (distance < 0.05f) return true;
            float step = speed * Time.deltaTime;
            Face(to);
            transform.position += to / distance * Mathf.Min(step, distance);
            return step >= distance;
        }

        private void Face(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 720f * Time.deltaTime);
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
