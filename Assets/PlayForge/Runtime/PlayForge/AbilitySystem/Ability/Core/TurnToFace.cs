using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// What a turn to face a target does when reaching its Start Angle takes longer than its Max Wait.
    /// </summary>
    public enum ETurnTooLong
    {
        /// <summary>It waits as long as it takes.</summary>
        NoLimit,

        /// <summary>The ability's first stage starts anyway, the turn going on.</summary>
        StartAnyway,

        /// <summary>The activation is cancelled, its cost unpaid, and the turn ends.</summary>
        Cancel,
    }

    /// <summary>
    /// How an ability's activation turns its caster to face its target or its point before its first stage.
    /// </summary>
    [Serializable]
    public class TurnToFaceSettings
    {
        [Tooltip("How fast it turns, in degrees a second of its own time, read as it starts turning, so it can read " +
                 "a turn rate attribute; 0 faces at once")]
        public ScalableFloat TurnSpeed = 540f;

        [Tooltip("How close to facing its target, in degrees, it turns before the ability's first stage starts")]
        [Range(0f, 180f)] public float StartAngle = 30f;

        [Tooltip("How close to facing its target, in degrees, it goes on turning to once the stage has started, even " +
                 "after the ability ends, until another order, its death or its game's word ends the turn; no wider " +
                 "than the Start Angle")]
        [Range(0f, 180f)] public float FacingAngle = 2f;

        [Tooltip("What happens when reaching the Start Angle takes longer than Max Wait: it waits as long as it " +
                 "takes, the stage starts anyway, or the activation is cancelled, its cost unpaid")]
        public ETurnTooLong IfTooLong = ETurnTooLong.NoLimit;

        [Tooltip("The seconds of its own time it waits to reach the Start Angle, where If Too Long sets a limit")]
        [Min(0f)] public float MaxWait = 1f;

        /// <summary>What it does, in one line, as "540 °/s, starting within 30°".</summary>
        public string Describe()
        {
            var line = $"{NumberText.Of(TurnSpeed, "°/s")}, starting within {StartAngle:0.#}°";
            return IfTooLong switch
            {
                ETurnTooLong.StartAnyway => $"{line}, or after {MaxWait:0.##}s",
                ETurnTooLong.Cancel => $"{line}, cancelled after {MaxWait:0.##}s",
                _ => line,
            };
        }
    }

    /// <summary>
    /// What turns an activation's caster to face its target or its point before its first stage, as an entity's Turns
    /// To Face and an ability's Turn To Face do.
    /// </summary>
    public static class ForgeTurnToFace
    {
        /// <summary>An ability's Turn To Face component, or null where it has none.</summary>
        public static TurnToFaceAbilityComponent Of(Ability ability)
        {
            if (ability == null || ability.Components == null) return null;
            foreach (var component in ability.Components)
                if (component is TurnToFaceAbilityComponent turn) return turn;
            return null;
        }

        /// <summary>
        /// Turns the caster to face the activation's target, its first under Target Real, which it follows as it moves;
        /// else the point its targeting picked, or the one it was handed; and waits, by the caster's own time, until it
        /// faces it within the Start Angle, as <paramref name="settings"/> say for a turn that takes too long. Nothing
        /// to face, or a target that's the caster, turns nothing. The turn goes on to the Facing Angle after. A turn
        /// taken over before it faces, by another order or the game's word, cancels the activation, as a cancel ends
        /// the turn.
        /// </summary>
        public static async UniTask WindUp(IGameplayAbilitySystem caster, AbilityDataPacket data,
            TurnToFaceSettings settings, CancellationToken token)
        {
            if (caster == null || data == null || settings == null || GASCore.Of(caster) is not { } core) return;
            var turn = core.Turn;
            float speed = settings.TurnSpeed?.Evaluate(NumberContext.Of(data)) ?? 0f;
            float facing = Mathf.Min(settings.FacingAngle, settings.StartAngle);
            if (data.TryGetFirstTarget(out var target) && target != null)
            {
                if (ReferenceEquals(target, caster)) return;
                turn.Face(target, speed, facing);
            }
            else if (TryPoint(caster, data, out var point)) turn.Face(point, speed, facing);
            else return;

            // A target with no place to face gave no order
            if (!turn.Turning) return;
            int order = turn.Order;
            float waited = 0f;
            try
            {
                while (true)
                {
                    // Another order took the turn over, or the game's word or its death ended it short: it gives way
                    if (turn.Order != order)
                        throw new OperationCanceledException("Another turn took over the turn to face its target");
                    if (!turn.Turning)
                    {
                        if (turn.Reached) return;
                        throw new OperationCanceledException("The turn to face its target was stopped");
                    }
                    if (turn.AngleLeft <= settings.StartAngle) return;

                    if (settings.IfTooLong != ETurnTooLong.NoLimit && waited >= settings.MaxWait)
                    {
                        if (settings.IfTooLong == ETurnTooLong.Cancel)
                            throw new OperationCanceledException("The turn to face its target took too long");

                        // Starts anyway, the turn going on
                        return;
                    }
                    await ForgeTick.Next(core.Clock, token);
                    waited += core.Clock.DeltaTime;
                }
            }
            catch (OperationCanceledException)
            {
                // A cancelled activation's turn ends with it, where it's still its own
                if (turn.Order == order) turn.Stop();
                throw;
            }
        }

        // The point the activation's targeting picked, under Target or its ability's implicit tag, or the one it was
        // handed; never the caster's own place
        private static bool TryPoint(IGameplayAbilitySystem caster, AbilityDataPacket data, out Vector3 point)
        {
            point = default;
            var own = caster.GetTargetingPacket();
            if (data.TryGetFirst(Tags.TARGET, out AbstractTargetingPacket picked) && picked != null
                && !Same(picked, own))
            {
                point = picked.position;
                return true;
            }
            var implicitTag = data.AbilitySpec?.Base?.Behaviour?.ImplicitTag ?? default;
            if (implicitTag.IsValid && data.TryGetFirst(implicitTag, out AbstractTargetingPacket implicitPacket)
                && implicitPacket != null && !Same(implicitPacket, own))
            {
                point = implicitPacket.position;
                return true;
            }
            return data.TryGet(Tags.POSITION, EDataTarget.Primary, out point);
        }

        // Whether a packet is the caster's own: the MonoBehaviour hub makes a new one each time it's asked, on its
        // object
        private static bool Same(AbstractTargetingPacket packet, AbstractTargetingPacket own) =>
            ReferenceEquals(packet, own) || own != null && own.HasTransform && packet.transform == own.transform
            || own != null && packet.target != null && ReferenceEquals(packet.target, own.target);
    }

    /// <summary>
    /// Turn To Face: how this ability turns its caster to face its target or its point before its first stage, in place
    /// of its entity's Turns To Face; or, with Turns off, that it doesn't, as a cast behind the caster doesn't. An
    /// ability holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Turns its caster to face its target or its point before its first stage, its own way, " +
                             "or not at all where its entity's Turns To Face would.")]
    public sealed class TurnToFaceAbilityComponent : AbstractAbilityComponent
    {
        [Tooltip("Whether this ability turns its caster: off, it starts facing however it faces, where its entity's " +
                 "Turns To Face would turn it")]
        public bool Turns = true;

        [Tooltip("How it turns, in place of its entity's Turns To Face")]
        public TurnToFaceSettings Turn = new();

        public override bool AllowsSeveral => false;

        /// <summary>What it does, in one line.</summary>
        public override string Describe() => Turns ? Turn?.Describe() ?? "" : "Doesn't turn";

        public override UniTask WindUp(AbilityComponentContext ctx, CancellationToken token) =>
            Turns && ctx.Source is IGameplayAbilitySystem caster && ctx.Data is AbilityDataPacket data
                ? ForgeTurnToFace.WindUp(caster, data, Turn, token)
                : UniTask.CompletedTask;
    }

    /// <summary>
    /// Turns To Face: every ability of the entity with a target or a point turns it to face it before its first stage,
    /// which starts once it's within the Start Angle; the turn goes on to the Facing Angle after. An ability with a
    /// Turn To Face of its own turns its own way, or not at all. An entity holds one at most.
    /// </summary>
    [Serializable]
    [ForgeMenu(Description = "Its abilities turn it to face their target or point before their first stage.")]
    public sealed class TurnsToFaceEntityComponent : AbstractEntityComponent
    {
        [Tooltip("How its abilities turn it; an ability's own Turn To Face turns its own way, or not at all")]
        public TurnToFaceSettings Turn = new();

        public override bool AllowsSeveral => false;

        /// <summary>What it does, in one line.</summary>
        public override string Describe() => Turn?.Describe() ?? "";

        public override UniTask WindUp(EntityComponentContext ctx, AbilityComponentContext ability,
            CancellationToken token) =>
            ForgeTurnToFace.Of(ability.Spec?.Base) == null && ctx.System is { } caster
            && ability.Data is AbilityDataPacket data
                ? ForgeTurnToFace.WindUp(caster, data, Turn, token)
                : UniTask.CompletedTask;
    }
}
