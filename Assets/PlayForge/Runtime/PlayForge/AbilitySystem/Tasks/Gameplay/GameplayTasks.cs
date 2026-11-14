using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A game's own mover, as the Creator Lab's units are, which PlayForge's movement tasks tell what they do (FN-24): a
    /// Dash holds it while it moves its entity, and a Blink stops it after it puts its entity elsewhere, so it drops the
    /// path it was on. A component on the entity's object that implements it hears; an entity without one moves as the
    /// task puts it.
    /// </summary>
    public interface IForgeMover
    {
        /// <summary>A task moves the entity itself while held, and the mover takes no order of its own; counted.</summary>
        void Hold(bool held);

        /// <summary>The entity was put elsewhere, so the mover drops what it was walking toward.</summary>
        void Stop();
    }

    /// <summary>
    /// Applies effects to each entity within a radius of its caster, its activation's target or its point, of the sides it
    /// takes, as Volley's arrows, Rallying Cry's armor or Thunderclap's stun (FN-24, from the Demo into the framework). Each
    /// is found by its position on either hub, or with physics on its layers, and takes the effects from the activation's
    /// origin with its caller's magnitudes, the dead left out.
    /// </summary>
    [Serializable]
    [ForgeCategory("Effects")]
    public class AreaEffectTask : AbstractAbilityTask
    {
        [Tooltip("How far from its middle the area reaches, read as it starts at its ability's level")]
        [NumberMin(0f)] public ScalableFloat Radius = 3f;

        [Tooltip("Where its middle is: its caster, its activation's target, or its activation's point")]
        public EAreaCentre Around = EAreaCentre.Target;

        [Tooltip("Whom it reaches, by side against its caster's")]
        public EAffinityFilter Takes = EAffinityFilter.Enemies;

        [Tooltip("Its caster too, where the area reaches it and takes its side")]
        public bool IncludeCaster;

        [Tooltip("Measures along the ground, leaving height out; off, a sphere")]
        public bool Flat = true;

        [Tooltip("Finds entities with physics, by collider on its Detection Layers. Off, each entity on either hub is found " +
                 "by its position, with no collider needed")]
        public bool UsePhysics;

        [Tooltip("Layers to find entities on, where it uses physics")]
        [ShownWhen(nameof(UsePhysics))]
        public LayerMask DetectionLayers = ~0;

        [Tooltip("The effects each one it reaches takes")]
        public List<GameplayEffect> Effects = new();

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var caster = data.EffectOrigin?.GetSource();
            if (caster == null || Effects == null || Effects.Count == 0) return UniTask.CompletedTask;
            Vector3? centre = Around switch
            {
                EAreaCentre.Caster => caster.GetTargetingPacket()?.position,
                EAreaCentre.Point => data.TryGet(Tags.POSITION, EDataTarget.Primary, out Vector3 point) ? point : null,
                _ => data.GetTargetingPacket(Tags.TARGET, out var packet, autoInterrupt: false) && packet != null ? packet.position : null,
            };
            if (!centre.HasValue) return UniTask.CompletedTask;

            float radius = Read(Radius, data);
            var found = new List<ITarget>();
            if (UsePhysics) ForgeEntities.Around(centre, radius, DetectionLayers, caster, IncludeCaster, found);
            else ForgeEntities.Within(centre.Value, radius, Flat, found, caster, IncludeCaster);
            var sides = data.EffectOrigin.GetAffiliation();
            foreach (var target in found)
            {
                if (target == null || target.IsDead || !ForgeEntities.Takes(Takes, sides, target)) continue;
                foreach (var effect in Effects) ApplyTo(data, target, effect);
            }
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Applies effects to its caster, whatever the ability targets, as the empowerment Shadow Step leaves its caster or the
    /// untargetable moment of Tumble's roll (FN-24, from the Demo into the framework). An Apply Effects task applies to the
    /// ability's targets alone.
    /// </summary>
    [Serializable]
    [ForgeCategory("Effects")]
    public class ApplyToCasterTask : AbstractAbilityTask
    {
        [Tooltip("The effects its caster takes, with its caller's magnitudes")]
        public List<GameplayEffect> Effects = new();

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (data.EffectOrigin?.GetSource() is ITarget caster && Effects != null)
                foreach (var effect in Effects) ApplyTo(data, caster, effect);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Moves its caster toward its ability's target, or away from it, over a moment, as Tumble's roll (FN-24, from the Demo
    /// into the framework). Its distance and duration are read as it starts, at its ability's level; it moves along the
    /// ground by its caster's time, a critical section, holding its caster's own mover, where it has one, while it moves.
    /// A caster with no object in the scene doesn't move.
    /// </summary>
    [Serializable]
    [ForgeCategory("Movement")]
    public class DashTask : AbstractAbilityTask
    {
        [Tooltip("How far its caster moves, at most")]
        [NumberMin(0f)] public ScalableFloat Distance = 5f;

        [Tooltip("How long the move takes, in seconds of its caster's time")]
        [NumberMin(0f)] public ScalableFloat Duration = 0.2f;

        [Tooltip("Away from the target; off, toward it")]
        public bool AwayFromTarget;

        public override bool IsCriticalSection => true;

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (data.EffectOrigin?.GetSource() is not Component caster || caster == null) return;
            if (!data.GetTargetingPacket(Tags.TARGET, out var target, autoInterrupt: false) || target == null) return;

            float distance = Read(Distance, data);
            float duration = Read(Duration, data);
            var from = caster.transform.position;
            var toward = target.position - from;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.0001f) return;
            if (AwayFromTarget) toward = -toward.normalized * distance;
            var to = from + Vector3.ClampMagnitude(toward, distance);

            var mover = caster.GetComponent<IForgeMover>();
            mover?.Hold(true);
            try
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += ForgeTime.DeltaTimeFor(data);
                    if (!caster) return;
                    caster.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                    await ForgeTick.NextFrame(token);
                }
                if (caster) caster.transform.position = to;
            }
            finally
            {
                mover?.Hold(false);
            }
        }
    }

    /// <summary>
    /// Puts its caster at its ability's target at once, as a blink: at the point, no farther than its reach, or behind the
    /// entity targeted, as Shadow Step does (FN-24, from the Demo into the framework). Its reach and how far behind are read
    /// as it starts, at its ability's level; its caster's own mover, where it has one, is stopped after.
    /// </summary>
    [Serializable]
    [ForgeCategory("Movement")]
    public class BlinkTask : AbstractAbilityTask
    {
        [Tooltip("How far the blink reaches, at most")]
        [NumberMin(0f)] public ScalableFloat Reach = 6f;

        [Tooltip("Behind the entity targeted, by this far; 0 puts the caster at the target itself")]
        [NumberMin(0f)] public ScalableFloat Behind = 0f;

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (data.EffectOrigin?.GetSource() is not Component caster || caster == null) return UniTask.CompletedTask;
            if (!data.GetTargetingPacket(Tags.TARGET, out var target, autoInterrupt: false) || target == null) return UniTask.CompletedTask;

            float reach = Read(Reach, data);
            float behind = Read(Behind, data);
            var from = caster.transform.position;
            var to = target.position;
            if (behind > 0f)
            {
                var facing = target.rotation * Vector3.forward;
                facing.y = 0f;
                if (facing.sqrMagnitude < 0.0001f) facing = (to - from).normalized;
                to -= facing.normalized * behind;
            }
            var step = to - from;
            step.y = 0f;
            caster.transform.position = from + Vector3.ClampMagnitude(step, reach);
            if (step.sqrMagnitude > 0.0001f)
            {
                var face = behind > 0f ? target.position - caster.transform.position : step;
                face.y = 0f;
                if (face.sqrMagnitude > 0.0001f) caster.transform.rotation = Quaternion.LookRotation(face);
            }
            caster.GetComponent<IForgeMover>()?.Stop();
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Uses up items its caster holds, as a potion's use does: the Healing Salve's ability ends with it, after its heal, and
    /// the last one used, the item and its ability go (FN-24, from the Demo into the framework). Its item system's Item
    /// Consumed hears it, so a game's own bookkeeping, as a shop's stock, follows.
    /// </summary>
    [Serializable]
    [ForgeCategory("Items")]
    public class ConsumeItemTask : AbstractAbilityTask
    {
        [Tooltip("The item it uses up")]
        public Item Item;

        [Tooltip("How many it uses up each time, read as it runs at its ability's level")]
        [NumberMin(1f)] public ScalableInt Count = 1;

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (Item == null || data.EffectOrigin?.GetSource()?.ToGAS() is not { } caster) return UniTask.CompletedTask;
            caster.GetItemSystem()?.ConsumeItem(Item, Mathf.Max(1, Read(Count, data)));
            return UniTask.CompletedTask;
        }
    }
}
