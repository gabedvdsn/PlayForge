using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>Who an area's effects reach, by their side against the caster's.</summary>
    public enum EDemoAffects
    {
        Enemies,
        Allies,
        Everyone,
    }

    /// <summary>The sides of the arena, by affiliation, and what stands in an area.</summary>
    public static class DemoTeams
    {
        private static readonly Collider[] hits = new Collider[64];

        /// <summary>Whether two share a side: an affiliation tag in common.</summary>
        public static bool AreAllies(IAffiliated one, IAffiliated other)
        {
            if (one == null || other == null) return false;
            if (ReferenceEquals(one, other)) return true;
            var mine = one.GetAffiliation();
            var theirs = other.GetAffiliation();
            if (mine == null || theirs == null) return false;
            foreach (var tag in mine)
                if (theirs.Contains(tag)) return true;
            return false;
        }

        /// <summary>Whether an area of the caster's reaches a target, by their sides.</summary>
        public static bool Reaches(ISource caster, ITarget target, EDemoAffects affects) => affects switch
        {
            EDemoAffects.Enemies => !AreAllies(caster, target),
            EDemoAffects.Allies => AreAllies(caster, target),
            _ => true,
        };

        /// <summary>The entities whose colliders are within a radius of a point, each once, the dead left out.</summary>
        public static List<ITarget> Around(Vector3 center, float radius, LayerMask layers)
        {
            var found = new List<ITarget>();
            int count = Physics.OverlapSphereNonAlloc(center, radius, hits, layers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var target = hits[i] ? hits[i].GetComponentInParent<ITarget>() : null;
                if (target != null && !target.IsDead && !found.Contains(target)) found.Add(target);
            }
            return found;
        }

        /// <summary>
        /// Applies an effect from an ability's data to a target, with the magnitudes its caller set, as the ability's
        /// own effects take them.
        /// </summary>
        public static bool Apply(AbilityDataPacket data, ITarget target, GameplayEffect effect)
        {
            if (effect == null || target == null || target.IsDead) return false;
            var spec = target.GenerateEffectSpec(data.EffectOrigin, effect);
            if (spec == null) return false;
            spec.TakeSetByCaller(data.SetByCallerMagnitudes);
            return target.ApplyGameplayEffect(spec);
        }
    }

    /// <summary>
    /// Applies effects to every entity within a radius of the ability's target, or of its caster, by side: Volley's arrows,
    /// Rallying Cry's armor, Thunderclap's stun. The demo's own task, as a game writes its own.
    /// </summary>
    [Serializable]
    public class AreaEffectTask : AbstractAbilityTask
    {
        [Tooltip("How far from its middle the area reaches")]
        public float Radius = 3f;

        [Tooltip("Around the caster; off, around the ability's target")]
        public bool AroundCaster;

        [Tooltip("Who the area's effects reach, by their side against the caster's")]
        public EDemoAffects Affects = EDemoAffects.Enemies;

        [Tooltip("The effects each one reached takes")]
        public List<GameplayEffect> Effects = new();

        [Tooltip("The layers whose colliders the area finds")]
        public LayerMask Layers = ~0;

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var caster = data.EffectOrigin.GetSource();
            if (caster == null) return UniTask.CompletedTask;

            Vector3 center;
            if (AroundCaster) center = caster.GetTargetingPacket().position;
            else if (data.GetTargetingPacket(Tags.TARGET, out var packet, autoInterrupt: false) && packet != null) center = packet.position;
            else return UniTask.CompletedTask;

            foreach (var target in DemoTeams.Around(center, Radius, Layers))
            {
                if (!DemoTeams.Reaches(caster, target, Affects)) continue;
                foreach (var effect in Effects) DemoTeams.Apply(data, target, effect);
            }
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Applies effects to the caster, whatever the ability targets: the empowerment Shadow Step leaves its caster, the
    /// untargetable moment of Tumble's roll. An Apply Effects task applies to the ability's targets alone.
    /// </summary>
    [Serializable]
    public class ApplyToCasterTask : AbstractAbilityTask
    {
        [Tooltip("The effects the caster takes")]
        public List<GameplayEffect> Effects = new();

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (data.EffectOrigin.GetSource() is ITarget caster)
                foreach (var effect in Effects) DemoTeams.Apply(data, caster, effect);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Moves the caster toward the ability's target, or away from it, over a moment: Tumble's roll. While it moves, the
    /// hero takes no other order.
    /// </summary>
    [Serializable]
    public class DashTask : AbstractAbilityTask
    {
        [Tooltip("How far the caster moves, at most")]
        public float Distance = 5f;

        [Tooltip("How long the move takes, in seconds")]
        public float Duration = 0.2f;

        [Tooltip("Away from the target; off, toward it")]
        public bool AwayFromTarget;

        public override bool IsCriticalSection => true;

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (data.EffectOrigin.GetSource() is not Component caster || caster == null) return;
            if (!data.GetTargetingPacket(Tags.TARGET, out var target, autoInterrupt: false) || target == null) return;

            var from = caster.transform.position;
            var toward = target.position - from;
            toward.y = 0f;
            if (toward.sqrMagnitude < 0.0001f) return;
            if (AwayFromTarget) toward = -toward.normalized * Distance;
            var to = from + Vector3.ClampMagnitude(toward, Distance);

            var unit = caster.GetComponent<DemoUnit>();
            if (unit) unit.Hold(true);
            try
            {
                float elapsed = 0f;
                while (elapsed < Duration)
                {
                    token.ThrowIfCancellationRequested();
                    elapsed += ForgeTime.DeltaTime;
                    if (!caster) return;
                    caster.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / Duration));
                    await ForgeTick.NextFrame(token);
                }
                if (caster) caster.transform.position = to;
            }
            finally
            {
                if (unit) unit.Hold(false);
            }
        }
    }

    /// <summary>
    /// Uses up one of an item the caster holds, as a potion's use does. The Healing Salve's ability ends with it, after
    /// its heal: the last one used, the item and its ability go.
    /// </summary>
    [Serializable]
    public class ConsumeItemTask : AbstractAbilityTask
    {
        [Tooltip("The item one of which is used up")]
        public Item Item;

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (Item == null || data.EffectOrigin.GetSource() is not IGameplayAbilitySystem caster) return UniTask.CompletedTask;
            var items = caster.GetItemSystem();
            if (items == null || !items.HasItem(Item)) return UniTask.CompletedTask;
            var unit = caster is Component component && component ? component.GetComponent<DemoUnit>() : null;
            if (items.RemoveItem(Item, 1) > 0 && unit && DemoGame.Current) DemoGame.Current.Consumed(unit, Item);
            return UniTask.CompletedTask;
        }
    }

    /// <summary>
    /// Puts the caster at the ability's target at once, as a blink: to the point, no farther than its reach, or behind the
    /// entity targeted, as Shadow Step does.
    /// </summary>
    [Serializable]
    public class BlinkTask : AbstractAbilityTask
    {
        [Tooltip("How far the blink reaches, at most")]
        public float Reach = 6f;

        [Tooltip("Behind the entity targeted, by this far; 0 puts the caster at the target itself")]
        public float Behind = 0f;

        public override bool IsCriticalSection => false;

        public override UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (data.EffectOrigin.GetSource() is not Component caster || caster == null) return UniTask.CompletedTask;
            if (!data.GetTargetingPacket(Tags.TARGET, out var target, autoInterrupt: false) || target == null) return UniTask.CompletedTask;

            var from = caster.transform.position;
            var to = target.position;
            if (Behind > 0f)
            {
                var facing = target.rotation * Vector3.forward;
                facing.y = 0f;
                if (facing.sqrMagnitude < 0.0001f) facing = (to - from).normalized;
                to -= facing.normalized * Behind;
            }
            var step = to - from;
            step.y = 0f;
            caster.transform.position = from + Vector3.ClampMagnitude(step, Reach);
            if (step.sqrMagnitude > 0.0001f)
            {
                var face = Behind > 0f ? target.position - caster.transform.position : step;
                face.y = 0f;
                if (face.sqrMagnitude > 0.0001f) caster.transform.rotation = Quaternion.LookRotation(face);
            }
            if (caster.GetComponent<DemoUnit>() is { } unit && unit) unit.Stop();
            return UniTask.CompletedTask;
        }
    }
}
