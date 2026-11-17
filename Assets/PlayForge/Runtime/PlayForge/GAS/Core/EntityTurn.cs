using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A game's own turner, on an entity's object: it turns its character itself, by its animation or its controller,
    /// as each step of a turn PlayForge orders asks, in place of PlayForge, which sets the entity's facing straight.
    /// PlayForge reads how far is left to turn from the entity's facing, its object's rotation, so that's what the
    /// turner turns.
    /// </summary>
    public interface IForgeTurner
    {
        /// <summary>
        /// A step of a turn: turn toward <paramref name="direction"/>, flat, at up to
        /// <paramref name="degreesPerSecond"/>, 0 at once, for this step's <paramref name="deltaTime"/> of the entity's
        /// own time.
        /// </summary>
        void Face(Vector3 direction, float degreesPerSecond, float deltaTime);
    }

    /// <summary>
    /// An entity's turn: an order to face a target, which it follows as it moves, or a point, flat, at a speed in
    /// degrees a second of its own time, 0 at once, until it faces it within an angle. Another order replaces it; its
    /// death, its game's word (<see cref="IGameplayAbilitySystem.StopTurning"/>) and reaching the angle end it. Each
    /// step turns the entity's facing, its targeting packet's rotation, unless its object has a turner of the game's
    /// own, an <see cref="IForgeTurner"/>, which takes the step in its place. An ability's turn to face its target
    /// orders one, which goes on after the ability's first stage starts, and after it ends.
    /// </summary>
    internal sealed class EntityTurn
    {
        private readonly IGameplayAbilitySystem owner;

        // Its own packet, taken as an order begins: the MonoBehaviour hub makes a new one each time it's asked
        private AbstractTargetingPacket body;

        // What it faces: a target's packet, which moves with it, or a point
        private AbstractTargetingPacket toward;
        private Vector3 point;
        private float speed, within;

        // The game's own turner on its object, looked for once an order
        private IForgeTurner turner;

        internal EntityTurn(IGameplayAbilitySystem owner) => this.owner = owner;

        /// <summary>Whether it's turning: an order it hasn't finished.</summary>
        public bool Turning { get; private set; }

        /// <summary>Each order's number, so what gave one can tell whether the turn is still its own.</summary>
        public int Order { get; private set; }

        /// <summary>
        /// Whether its last order ended by facing what it faced, or having nothing left to face, rather than being
        /// stopped.
        /// </summary>
        public bool Reached { get; private set; }

        /// <summary>
        /// How far, in degrees, flat, it has yet to turn to face what it's ordered to; 0 with no order.
        /// </summary>
        public float AngleLeft => Turning && TryDirection(out var facing, out var to) ? Vector3.Angle(facing, to) : 0f;

        /// <summary>
        /// Orders a turn to face a target, which it follows as it moves; itself, or one with no place, turns nothing.
        /// </summary>
        public void Face(ITarget target, float degreesPerSecond, float withinDegrees)
        {
            if (target == null || ReferenceEquals(target, owner) || target.GetTargetingPacket() is not { } packet)
                return;
            toward = packet;
            Begin(degreesPerSecond, withinDegrees);
        }

        /// <summary>Orders a turn to face a point.</summary>
        public void Face(Vector3 at, float degreesPerSecond, float withinDegrees)
        {
            toward = null;
            point = at;
            Begin(degreesPerSecond, withinDegrees);
        }

        /// <summary>Ends the turn, where there is one, short of what it faces.</summary>
        public void Stop() => End(false);

        private void End(bool reached)
        {
            if (Turning) Reached = reached;
            Turning = false;
            body = toward = null;
            turner = null;
        }

        private void Begin(float degreesPerSecond, float withinDegrees)
        {
            speed = Mathf.Max(0f, degreesPerSecond);
            within = Mathf.Clamp(withinDegrees, 0f, 180f);
            body = owner.GetTargetingPacket();
            turner = body != null && body.HasTransform ? body.transform.GetComponent<IForgeTurner>() : null;
            Order++;
            Reached = false;
            Turning = true;
        }

        /// <summary>
        /// A step of the turn by the entity's own time: it ends the turn where it faces within its angle, or has
        /// nothing left to face, and otherwise turns toward it, by its own turner where it has one.
        /// </summary>
        internal void Step(float deltaTime)
        {
            if (!Turning) return;
            if (!TryDirection(out var facing, out var to) || Vector3.Angle(facing, to) <= within)
            {
                End(true);
                return;
            }
            if (deltaTime <= 0f) return;

            // A turner the game destroyed takes no more steps
            if (turner is Object unity && !unity) turner = null;
            if (turner != null)
            {
                turner.Face(to, speed, deltaTime);
                return;
            }

            var faced = Quaternion.LookRotation(to, Vector3.up);
            body.rotation = speed <= 0f ? faced : Quaternion.RotateTowards(body.rotation, faced, speed * deltaTime);
        }

        // Its flat facing and the flat way to what it faces; false where it or what it faces has no place, as an object
        // that's gone, or it stands on it
        private bool TryDirection(out Vector3 facing, out Vector3 to)
        {
            facing = to = default;
            if (!HasPlace(body)) return false;
            Vector3 at;
            if (toward != null)
            {
                if (!HasPlace(toward)) return false;
                at = toward.position;
            }
            else at = point;

            to = at - body.position;
            to.y = 0f;
            facing = body.rotation * Vector3.forward;
            facing.y = 0f;
            return to.sqrMagnitude > 1e-6f && facing.sqrMagnitude > 1e-6f;
        }

        // A packet on an object has a place while its object lasts; one of its own, as a runtime entity's, always does
        private static bool HasPlace(AbstractTargetingPacket packet) =>
            packet != null && (packet.HasTransform || packet.transform is null);
    }
}
