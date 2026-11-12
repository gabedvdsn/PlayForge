using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Lab
{
    /// <summary>What a unit is made from, and where it stands (LAB-2).</summary>
    public sealed class LabUnitSpec
    {
        /// <summary>The entity it stands for; or else <see cref="Set"/>, made into an entity of its own.</summary>
        public EntityIdentity Identity;

        /// <summary>An attribute set, for a unit with no entity of its own.</summary>
        public AttributeSet Set;

        /// <summary>The project's rules, which say its health, its speed and what stops it.</summary>
        public ILabRules Rules;

        /// <summary>The side it stands on, which its registration's affiliation gives.</summary>
        public LabSide Side;

        /// <summary>The level it starts at, or 0 for its entity's own.</summary>
        public int Level;

        /// <summary>Its body's color; with none, its side's.</summary>
        public Color? Color;

        public Vector3 At;
        public Quaternion Facing = Quaternion.identity;

        /// <summary>A world of the lab's own to step in, or null for the scene's.</summary>
        public ProcessWorld World;

        /// <summary>The material its body takes, tinted; with none, the render pipeline's default.</summary>
        public Material Material;

        /// <summary>A ring at its feet in its side's color.</summary>
        public bool Ring = true;

        /// <summary>
        /// Whether it stays as it dies, to be revived, whatever its entity says: a dummy's, which the lab revives where it
        /// falls (LAB-4).
        /// </summary>
        public bool Stays;

        /// <summary>Its name; with none, its entity's.</summary>
        public string Name;
    }

    /// <summary>
    /// A unit in the lab, and the Demo's hero (LAB-2): a GameplayAbilitySystem on a body of its own, which walks where it's
    /// sent, attacks what it's set on, casts its abilities and its items', and holds its items. Its attributes, tags,
    /// levels and Local Data are the GAS's; this is the space's side of it. Movement is the lab's, a straight line on the
    /// ground at its speed, as a project's own body would be for a game.
    /// </summary>
    public class LabUnit : MonoBehaviour
    {
        /// <summary>Every unit in the scene, in the order they were made.</summary>
        public static IReadOnlyList<LabUnit> All => all;
        private static readonly List<LabUnit> all = new();

        public GameplayAbilitySystem Gas { get; private set; }

        /// <summary>The entity it stands for: the asset, or the one made for its attribute set.</summary>
        public EntityIdentity Identity { get; private set; }

        /// <summary>The attribute set it was made from, where it had no entity.</summary>
        public AttributeSet Set { get; private set; }

        public ILabRules Rules { get; private set; }
        public LabSide Side { get; private set; }
        public ProcessWorld World { get; private set; }
        public ProcessRelay Relay => relay;

        /// <summary>Where it was put, which a reset puts it back to.</summary>
        public Vector3 Home { get; set; }

        /// <summary>
        /// Whether the lab steps it with its own time, through <see cref="Step"/>; else it steps itself each frame by
        /// PlayForge's clock, as in a scene.
        /// </summary>
        public bool SteppedByLab { get; set; }

        /// <summary>
        /// Whether it goes out of sight while its rules hide it, as the Demo's opponent does while it's invisible to the
        /// player; the lab shows every side.
        /// </summary>
        public bool HidesFromView { get; set; }

        /// <summary>What each cast's data carries besides its target, as the Demo's AI hands its difficulty.</summary>
        public event Action<LabUnit, ProcessDataPacket> Decorating;

        public bool Alive => Gas != null && !Gas.IsDead;
        public string Name => !string.IsNullOrEmpty(givenName) ? givenName : Identity != null ? Identity.GetName() : name;

        private ProcessRelay relay;
        private EntityIdentity copy;
        private bool madeIdentity;
        private string givenName;
        private Vector3? destination;
        private LabUnit target;
        private int holds;
        private bool shown = true;
        private readonly List<Renderer> renderers = new();

        // ═══════════════════════════════════════════════════════════════════════════
        // Making one
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// A unit: a body in its color, and its entity, registered at its level on its side, in the lab's world where one
        /// is given. Null where it has neither an entity nor a set, or where ProcessControl refuses it.
        /// </summary>
        public static LabUnit Create(LabUnitSpec spec) => Create<LabUnit>(spec);

        /// <summary>A unit of a kind of its own, as the Demo's hero, which keeps its seat besides.</summary>
        public static T Create<T>(LabUnitSpec spec) where T : LabUnit
        {
            if (spec == null || (spec.Identity == null && spec.Set == null)) return null;
            var made = spec.Identity == null;
            var identity = made ? MakeIdentity(spec.Set) : spec.Identity;

            var host = new GameObject(string.IsNullOrEmpty(spec.Name) ? identity.GetName() : spec.Name);
            host.transform.SetPositionAndRotation(new Vector3(spec.At.x, 0f, spec.At.z), spec.Facing);
            var collider = host.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.radius = 0.55f;
            collider.height = 2f;

            var unit = host.AddComponent<T>();
            unit.Identity = identity;
            unit.Set = spec.Set;
            unit.madeIdentity = made;
            unit.givenName = spec.Name;
            unit.Rules = spec.Rules;
            unit.Side = spec.Side;
            unit.World = spec.World;
            unit.Home = host.transform.position;
            unit.MakeBody(spec.Color ?? spec.Side.Color, spec.Material, spec.Ring ? spec.Side.Color : (Color?)null);

            // A copy of the entity that starts at the level asked, so its attributes are worked out at that level as it
            // starts; its abilities and items are the project's own assets
            unit.copy = made ? identity : Instantiate(identity);
            if (!made) unit.copy.name = identity.name;
            if (spec.Level > 0) unit.copy.StartingLevel = Mathf.Clamp(spec.Level, 1, Mathf.Max(1, identity.MaxLevel));
            if (spec.Stays)
            {
                // The copy's own, so the project's entity stays as it is
                unit.copy.Death = new EntityDeath { Policy = EDeathPolicy.Stay };
            }
            unit.Gas = GameplayAbilitySystem.AddToGameObject(host, unit.copy);
            unit.Gas.ProcessLifecycle = EProcessLifecycle.Synchronous;
            unit.Gas.ProcessTiming = EProcessStepTiming.UpdateAndLate;

            var data = ProcessDataPacket.Default();
            if (spec.Side.IsValid) data.SetPrimary(Tags.AFFILIATION, new List<Tag> { spec.Side.Tag });
            var registering = ProcessControl.Register(unit.Gas).With(data);
            if (spec.World != null) registering = registering.In(spec.World);
            if (!registering.Run(out unit.relay))
                ForgeLog.Error($"Creator Lab: ProcessControl refused {unit.Name}.");
            return unit;
        }

        // An entity of its own for an attribute set, which the unit takes with it as it goes
        private static EntityIdentity MakeIdentity(AttributeSet set)
        {
            var entity = ScriptableObject.CreateInstance<EntityIdentity>();
            entity.hideFlags = HideFlags.DontSave;
            entity.name = entity.Name = set != null ? set.GetName() : "Unit";
            entity.Description = "";
            entity.Textures = new List<TextureItem>();
            entity.AssetTag = Tag.GenerateAsUnique($"Lab Entity {entity.Name}");
            entity.Affiliation = new List<Tag>();
            entity.GrantedTags = new List<Tag>();
            entity.ContextTags = new List<Tag>();
            entity.StartingAbilities = new List<Ability>();
            entity.StartingItems = new List<StartingItemContainer>();
            entity.AttributeSet = set;
            entity.WorkerGroup = new StandardWorkerGroup();
            return entity;
        }

        // A capsule in its color with a nose to show where it faces, and a ring at its feet in its side's
        private void MakeBody(Color color, Material material, Color? ring)
        {
            var body = Part(PrimitiveType.Capsule, "Body", new Vector3(0f, 1f, 0f), Vector3.one, color, material);
            Part(PrimitiveType.Cube, "Nose", new Vector3(0f, 1.45f, 0.45f), new Vector3(0.25f, 0.18f, 0.35f),
                Color.Lerp(color, Color.black, 0.45f), material);
            if (ring.HasValue)
                Part(PrimitiveType.Cylinder, "Ring", new Vector3(0f, 0.02f, 0f), new Vector3(1.5f, 0.01f, 1.5f), ring.Value, material);
            renderers.Clear();
            renderers.AddRange(GetComponentsInChildren<Renderer>());
            body.name = "Body";
        }

        private GameObject Part(PrimitiveType shape, string partName, Vector3 at, Vector3 size, Color color, Material material)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = partName;
            DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(transform, false);
            part.transform.localPosition = at;
            part.transform.localScale = size;
            Tint(part.GetComponent<Renderer>(), color, material);
            return part;
        }

        /// <summary>Tints a renderer, through a block, so the material is shared.</summary>
        public static void Tint(Renderer renderer, Color color, Material material = null)
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

        protected virtual void OnEnable()
        {
            if (!all.Contains(this)) all.Add(this);
        }

        protected virtual void OnDisable() => all.Remove(this);

        protected virtual void OnDestroy()
        {
            all.Remove(this);
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

        /// <summary>The share of its health it has left, or 1 where its rules name no health.</summary>
        public float HealthShare
        {
            get
            {
                var health = Rules?.Health;
                if (health == null) return 1f;
                float most = Base(health);
                return most > 0f ? Mathf.Clamp01(Value(health) / most) : 0f;
            }
        }

        /// <summary>Whether it has a tag now.</summary>
        public bool Has(Tag tag) => Gas != null && tag.IsValid && Gas.GetTagCache() is { } tags && tags.GetWeight(tag) > 0;

        /// <summary>Whether it has any of the tags.</summary>
        public bool HasAny(IReadOnlyList<Tag> tags) => Gas != null && Gas.GetTagCache().HasAny(tags);

        /// <summary>A float its entity's Local Data holds, or the fallback.</summary>
        public float LocalFloat(Tag key, float fallback) =>
            Gas != null && key.IsValid && Gas.TryGetLocalData(key, out var data) ? data.floatValue : fallback;

        /// <summary>How near it must stand to attack.</summary>
        public float AttackRange => Rules == null ? 2f : LocalFloat(Rules.AttackRangeKey, Rules.AttackRange);

        /// <summary>
        /// Whether it can see and target another: an ally always, a foe unless that one is hidden or shielded, as an
        /// invisible or untargetable one is.
        /// </summary>
        public bool CanTarget(LabUnit other)
        {
            if (other == null || !other.Alive || other == this) return false;
            if (LabSides.AreAllies(Gas, other.Gas)) return true;
            return Rules == null || (!other.HasAny(Rules.Hides) && !other.HasAny(Rules.Shields));
        }

        public float DistanceTo(LabUnit other) => other ? Flat(other.transform.position - transform.position).magnitude : float.MaxValue;

        // ═══════════════════════════════════════════════════════════════════════════
        // Abilities and items
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The entity's starting ability in a slot, from 0, or null.</summary>
        public Ability AbilityAt(int slot) =>
            Identity != null && Identity.StartingAbilities != null && slot >= 0 && slot < Identity.StartingAbilities.Count
                ? Identity.StartingAbilities[slot] : null;

        /// <summary>Its attack, the starting ability its rules name, or null.</summary>
        public Ability AttackAbility => Rules != null ? AbilityAt(Rules.AttackSlot) : AbilityAt(0);

        /// <summary>Its abilities other than its attack, in its entity's order, as a player's keys take them.</summary>
        public IEnumerable<Ability> Casts()
        {
            var attack = AttackAbility;
            if (Identity?.StartingAbilities == null) yield break;
            foreach (var ability in Identity.StartingAbilities)
                if (ability != null && ability != attack) yield return ability;
        }

        /// <summary>Every ability it knows now: its own, its items' and those effects lend it.</summary>
        public List<Ability> Known()
        {
            var known = new List<Ability>();
            var abilities = Gas != null ? Gas.GetAbilitySystem() : null;
            if (abilities == null) return known;
            foreach (var container in abilities.GetAbilityContainers())
                if (container?.Spec?.Base != null) known.Add(container.Spec.Base);
            return known;
        }

        /// <summary>The active abilities of the items it holds, in the order it took them, as its item keys take them.</summary>
        public List<Ability> ItemAbilities()
        {
            var found = new List<Ability>();
            var items = Gas != null ? Gas.GetItemSystem() : null;
            if (items == null) return found;
            foreach (var container in items.GetItemContainers())
            {
                var ability = container?.Spec?.Base?.ActiveAbility;
                if (ability != null && !found.Contains(ability)) found.Add(ability);
            }
            return found;
        }

        /// <summary>The items it holds, each once, in the order it took them.</summary>
        public List<Item> Items()
        {
            var found = new List<Item>();
            var items = Gas != null ? Gas.GetItemSystem() : null;
            if (items == null) return found;
            foreach (var container in items.GetItemContainers())
            {
                var item = container?.Spec?.Base;
                if (item != null && !found.Contains(item)) found.Add(item);
            }
            return found;
        }

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

        /// <summary>
        /// Casts an ability it knows with the data given, or none, where its targeting then waits for a click. One it
        /// doesn't know, it can't cast.
        /// </summary>
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
            if (abilities == null) return;
            foreach (var container in abilities.GetAbilityContainers())
                if (container != null && container.IsActive) abilities.Inject(container.Index, InterruptSequenceInjection.Instance);
        }

        /// <summary>The data an ability cast at another unit carries: the unit and its place.</summary>
        public ProcessDataPacket DataAt(LabUnit other)
        {
            var data = ProcessDataPacket.Default();
            if (other != null && other.Gas != null)
            {
                data.SetPrimary(Tags.TARGET_REAL, (ITarget)other.Gas);
                data.SetPrimary(Tags.POSITION, other.transform.position);
            }
            Decorating?.Invoke(this, data);
            return data;
        }

        /// <summary>The data an ability cast at a point on the ground carries: the point.</summary>
        public ProcessDataPacket DataAt(Vector3 point)
        {
            var data = ProcessDataPacket.Default();
            data.SetPrimary(Tags.POSITION, new Vector3(point.x, 0f, point.z));
            Decorating?.Invoke(this, data);
            return data;
        }

        /// <summary>The data of a cast away from another unit, as an escape's: a point a few steps the other way.</summary>
        public ProcessDataPacket DataAway(LabUnit other, float distance = 6f)
        {
            var away = other != null ? Flat(transform.position - other.transform.position) : transform.forward;
            if (away.sqrMagnitude < 0.0001f) away = -transform.forward;
            return DataAt(transform.position + away.normalized * distance);
        }

        /// <summary>An ability's level on it now.</summary>
        public void ApplyLevel(Ability ability, int level)
        {
            if (Gas == null || ability == null) return;
            Gas.SetLevel(ability.GetAssetTag(), new IntValuePair(level, Mathf.Max(level, ability.MaxLevel)));
        }

        /// <summary>The level an ability, an item or its entity is at on it.</summary>
        public int LevelOf(BaseForgeLevelProvider provider) =>
            Gas != null && provider != null ? Gas.GetLevel(provider.GetAssetTag()).CurrentValue : 0;

        /// <summary>Gives it an ability it doesn't know, at a level.</summary>
        public bool Teach(Ability ability, int level = 1)
        {
            var abilities = Gas != null ? Gas.GetAbilitySystem() : null;
            if (abilities == null || ability == null || abilities.TryGetAbilityContainer(ability, out _)) return false;
            return abilities.GiveAbility(ability, Mathf.Max(1, level), out _);
        }

        /// <summary>Takes an ability it knows.</summary>
        public bool Forget(Ability ability)
        {
            var abilities = Gas != null ? Gas.GetAbilitySystem() : null;
            return abilities != null && ability != null && abilities.RemoveAbility(ability);
        }

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

        public void AttackUnit(LabUnit other)
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
        public LabUnit Target => target;

        /// <summary>Where it's walking to, if anywhere.</summary>
        public Vector3? Destination => destination;

        /// <summary>Holds it still and deaf to orders, as a dash does while it carries it.</summary>
        public void Hold(bool held) => holds = Mathf.Max(0, holds + (held ? 1 : -1));

        /// <summary>Puts it back where it was put, facing as it was, with no orders.</summary>
        public void GoHome()
        {
            Stop();
            transform.position = Home;
        }

        // Out of sight while it's dead, and while it's hidden where it hides from view
        private void ShowState()
        {
            bool visible = Alive && !(HidesFromView && Rules != null && HasAny(Rules.Hides));
            if (visible == shown) return;
            shown = visible;
            foreach (var each in renderers)
                if (each) each.enabled = visible;
            if (TryGetComponent<Collider>(out var body)) body.enabled = Alive;
        }

        protected virtual void Update()
        {
            if (!SteppedByLab) Step(ForgeTime.DeltaTime);
        }

        /// <summary>
        /// A step of its own by the time given: it shows or hides, walks toward its destination or its target, and attacks
        /// its target once it's in range and ready. The lab calls it with its own time; a scene's unit steps itself.
        /// </summary>
        public void Step(float deltaTime)
        {
            ShowState();
            if (!Alive || holds > 0 || deltaTime <= 0f) return;
            bool stuck = Rules != null && HasAny(Rules.Stops);
            var speedAttribute = Rules?.MovementSpeed;
            float speed = speedAttribute != null ? Mathf.Max(0f, Value(speedAttribute)) : Rules?.WalkSpeed ?? 4f;

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
                    if (!stuck) Walk(target.transform.position, speed, deltaTime);
                    return;
                }
                Face(to, deltaTime);
                var attack = AttackAbility;
                if (!stuck && Ready(attack)) Cast(attack, DataAt(target), EAbilityActivationPolicyExtended.ActivateIfIdle);
                return;
            }

            if (destination.HasValue && !stuck && Walk(destination.Value, speed, deltaTime)) destination = null;
        }

        // A step toward a point at a speed; true once it's there
        private bool Walk(Vector3 point, float speed, float deltaTime)
        {
            var to = Flat(point - transform.position);
            float distance = to.magnitude;
            if (distance < 0.05f) return true;
            float step = speed * deltaTime;
            Face(to, deltaTime);
            transform.position += to / distance * Mathf.Min(step, distance);
            return step >= distance;
        }

        private void Face(Vector3 direction, float deltaTime)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 720f * deltaTime);
        }

        private static Vector3 Flat(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
