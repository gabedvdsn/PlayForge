using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An aura (TW-10, Decision 50): an effect that holds on each it takes near its holder, and comes off as they leave, as
    /// Mekansm's or a captain's command aura does. An item's holds while it's equipped, an ability's while it's known, an
    /// effect's while it lasts on the one it's on, an entity's as it lives. It checks who's near every so often.
    /// </summary>
    [Serializable]
    public class AuraEntry
    {
        [Tooltip("The effect it holds on each it takes while they're near, taken off as they leave")]
        public GameplayEffect Effect;

        [Tooltip("How far it reaches from its holder, read at each check")]
        [NumberMin(0f)]
        public ScalableFloat Radius = 5f;

        [Tooltip("Measures along the ground, leaving height out; off, a sphere")]
        public bool Flat = true;

        [Tooltip("Whom it takes, from its holder's sides: allies, enemies, or both. A holder with no sides takes anyone")]
        public EAffinityFilter Takes = EAffinityFilter.Allies;

        [Tooltip("Its holder too, where it takes the holder's side")]
        public bool IncludeHolder = true;

        [Tooltip("How often it checks who's near, in seconds of its holder's time, read at its holder's level; 0, every step")]
        [NumberMin(0f)] public ScalableFloat Interval = 0.25f;
    }

    /// <summary>Gives a holder the auras it has, and takes them back (TW-10).</summary>
    public static class ForgeAuras
    {
        /// <summary>Gives <paramref name="holder"/> each aura, which holds its effect on those near it from its next step.</summary>
        public static void Provide(List<AuraEntry> entries, ISource holder)
        {
            if (entries == null || entries.Count == 0 || (holder as IGameplayAbilitySystem)?.GetAuras() is not { } auras) return;
            foreach (var entry in entries)
                if (entry?.Effect != null) auras.Add(entry, holder);
        }

        /// <summary>Takes back what <see cref="Provide"/> gave, its effects off each it held them on.</summary>
        public static void Remove(List<AuraEntry> entries, ISource holder)
        {
            if (entries == null || entries.Count == 0 || (holder as IGameplayAbilitySystem)?.GetAuras() is not { } auras) return;
            foreach (var entry in entries)
                if (entry != null) auras.Remove(entry);
        }

        /// <summary>The auras a list of components holds, each an Aura component's; null where it holds none (FN-14).</summary>
        public static List<AuraEntry> Of<T>(List<T> components) where T : AbstractForgeComponent
        {
            if (components == null) return null;
            List<AuraEntry> auras = null;
            foreach (var component in components)
                if (component is IAuraComponent held && held.Aura != null) (auras ??= new List<AuraEntry>()).Add(held.Aura);
            return auras;
        }
    }

    /// <summary>A component that holds an aura: its holder's effect on those near it (TW-10, FN-14).</summary>
    public interface IAuraComponent
    {
        /// <summary>The aura it holds.</summary>
        AuraEntry Aura { get; }
    }

    /// <summary>An aura an effect holds while it lasts, on those near the one it's on (TW-10, FN-14).</summary>
    [Serializable]
    public sealed class AuraEffectComponent : AbstractEffectComponent, IAuraComponent
    {
        public AuraEntry Aura = new();
        AuraEntry IAuraComponent.Aura => Aura;
    }

    /// <summary>An aura an ability holds while its holder knows it, on those near its holder (TW-10, FN-14).</summary>
    [Serializable]
    public sealed class AuraAbilityComponent : AbstractAbilityComponent, IAuraComponent
    {
        public AuraEntry Aura = new();
        AuraEntry IAuraComponent.Aura => Aura;
    }

    /// <summary>An aura an item holds while it's equipped, on those near its holder (TW-10, FN-14).</summary>
    [Serializable]
    public sealed class AuraItemComponent : AbstractItemComponent, IAuraComponent
    {
        public AuraEntry Aura = new();
        AuraEntry IAuraComponent.Aura => Aura;
    }

    /// <summary>An aura an entity holds while it lives, on those near it (TW-10, FN-14).</summary>
    [Serializable]
    public sealed class AuraEntityComponent : AbstractEntityComponent, IAuraComponent
    {
        public AuraEntry Aura = new();
        AuraEntry IAuraComponent.Aura => Aura;
    }

    /// <summary>
    /// An entity's running auras (TW-10): each checks who's near at its interval, by its holder's time, holds its effect on
    /// each it takes, from an origin of its own, and takes it off as they leave, die or end, as its holder dies, or as the
    /// aura goes. Two of one aura, as two of an item, are two runs, each holding its own.
    /// </summary>
    public sealed class AuraSet
    {
        private sealed class Run
        {
            public AuraEntry Entry;
            public ISource Holder;
            public IEffectOrigin Origin;
            public float Waited;
            public readonly List<ITarget> Held = new();
        }

        private readonly List<Run> runs = new();

        // Who a check finds near, used again by each, so a check allocates nothing of its own
        private readonly List<ITarget> near = new();

        /// <summary>How many auras it runs.</summary>
        public int Count => runs.Count;

        /// <summary>Runs an aura for its holder, which checks who's near at its next step.</summary>
        public void Add(AuraEntry entry, ISource holder)
        {
            if (entry?.Effect == null || holder == null) return;
            runs.Add(new Run
            {
                Entry = entry, Holder = holder, Origin = IEffectOrigin.GenerateSourceDerivation(holder), Waited = float.PositiveInfinity,
            });
        }

        /// <summary>Stops one run of an aura, its effects off each it held them on.</summary>
        public void Remove(AuraEntry entry)
        {
            for (int i = runs.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(runs[i].Entry, entry)) continue;
                TakeOffAll(runs[i]);
                runs.RemoveAt(i);
                return;
            }
        }

        /// <summary>Steps each aura by its holder's time, checking who's near where its interval has passed.</summary>
        public void Step(float deltaTime)
        {
            for (int i = 0; i < runs.Count; i++)
            {
                var run = runs[i];
                run.Waited += Mathf.Max(0f, deltaTime);
                if (run.Waited < (run.Entry.Interval?.Evaluate(NumberContext.Of(run.Holder)) ?? 0f)) continue;
                run.Waited = 0f;
                Check(run);
            }
        }

        /// <summary>Stops every aura, their effects off each they held them on, as the holder ends.</summary>
        public void Clear()
        {
            foreach (var run in runs) TakeOffAll(run);
            runs.Clear();
        }

        // Who's near now, of the sides it takes; then its effect off each that left and on each that came
        private void Check(Run run)
        {
            var entry = run.Entry;
            var holder = run.Holder;
            near.Clear();
            var here = holder.GetTargetingPacket();
            bool holderDead = holder is ITarget holding && holding.IsDead;
            if (!holderDead && here != null && entry.Effect != null)
            {
                float radius = entry.Radius?.Evaluate(NumberContext.Of(holder)) ?? 0f;
                if (radius > 0f) ForgeEntities.Within(here.position, radius, entry.Flat, near, holder, entry.IncludeHolder);
                var sides = (holder as ITarget)?.GetAffiliation();
                for (int i = near.Count - 1; i >= 0; i--)
                    if (near[i].IsDead || !ForgeEntities.Takes(entry.Takes, sides, near[i])) near.RemoveAt(i);
            }

            for (int i = run.Held.Count - 1; i >= 0; i--)
            {
                var held = run.Held[i];
                if (near.Contains(held)) continue;
                TakeOff(run, held);
                run.Held.RemoveAt(i);
            }

            // On each near that hasn't it, or whose has ended on its own, as a durational effect does, in the project's
            // resolve order (TW-31)
            ForgeResolveOrder.Sort(near);
            foreach (var target in near)
            {
                bool holds = run.Held.Contains(target);
                if (holds && Holds(target, entry.Effect, run.Origin)) continue;
                var spec = target.GenerateEffectSpec(run.Origin, entry.Effect);
                if (spec == null) continue;
                spec.LentByAura = true;
                if (!target.ApplyGameplayEffect(spec)) continue;
                if (!holds) run.Held.Add(target);
            }
        }

        // Whether the effect this aura put on the target is still there
        private static bool Holds(ITarget target, GameplayEffect effect, IEffectOrigin origin)
        {
            if (target is not IGameplayAbilitySystem gas || !gas.TryGetEffectContainers(effect, out var containers)) return false;
            foreach (var container in containers)
                if (container != null && ReferenceEquals(container.Spec?.Origin, origin)) return true;
            return false;
        }

        private static void TakeOff(Run run, ITarget held)
        {
            // One that has ended took the effect with it
            if (held is UnityEngine.Object unityObject && !unityObject) return;
            if (held is not IGameplayAbilitySystem gas || !Contains(ForgeEntities.Live, gas)) return;
            gas.GetEffectSystem()?.RemoveGameplayEffect(run.Entry.Effect, run.Origin);
        }

        private static void TakeOffAll(Run run)
        {
            foreach (var held in run.Held) TakeOff(run, held);
            run.Held.Clear();
        }

        private static bool Contains(IReadOnlyList<IGameplayAbilitySystem> list, IGameplayAbilitySystem gas)
        {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], gas)) return true;
            return false;
        }
    }
}
