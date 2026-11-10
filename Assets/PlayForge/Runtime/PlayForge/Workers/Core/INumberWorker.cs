using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A worker that answers numbers (FEAT-29, NUM-12): a number of the Worker kind, or an Asset Part number's Worker
    /// Value, asks it for a value under a key, and it answers from what it knows, as how much health its effect's target
    /// has lost since the effect began. Any worker can implement it: an effect's workers answer about their effect, and
    /// an entity's workers, those its worker groups give it, about the entity. Every number field can ask, so a worker
    /// can drive an effect's magnitude, a task's range, a threshold or a cue's parameter.
    /// <para>
    /// The context it's asked in is what it's asked about: for an effect's worker, the effect's own context, whose
    /// <see cref="NumberContext.Carrier"/> is the effect's spec, where its state for that application is
    /// (<see cref="GameplayEffectSpec.GetWorkerState{T}"/>); for an entity's worker, the asking number's context. It
    /// answers false for a key it doesn't know, and the asker reads its default.
    /// </para>
    /// </summary>
    public interface INumberWorker
    {
        /// <summary>The value under a key, about what the context holds; false for a key the worker doesn't know.</summary>
        bool TryGetNumber(Tag key, in NumberContext context, out float value);
    }

    /// <summary>
    /// The workers on each entity that answer numbers, and the way to ask them or an effect's workers (FEAT-29, NUM-12).
    /// A worker group installs its workers that answer as it gives them to an entity, and takes them back with them; code
    /// that gives an entity a worker of its own can do the same with <see cref="Provide"/>.
    /// </summary>
    public static class NumberWorkers
    {
        // Each entity's answering workers, by its GAS, kept weakly, so an entity that's gone isn't held
        private static readonly ConditionalWeakTable<object, List<INumberWorker>> ByEntity = new();

        // One entity, however it's held: as its GAS where it has one
        private static object KeyOf(ITarget entity) => entity == null ? null : (object)entity.ToGAS() ?? entity;

        /// <summary>Gives an entity a worker that answers numbers.</summary>
        public static void Provide(ITarget entity, INumberWorker worker)
        {
            var key = KeyOf(entity);
            if (key == null || worker == null) return;
            ByEntity.GetOrCreateValue(key).Add(worker);
        }

        /// <summary>Takes a worker that answers numbers back from an entity: one of it, where it was given more than once.</summary>
        public static void Remove(ITarget entity, INumberWorker worker)
        {
            var key = KeyOf(entity);
            if (key == null || worker == null) return;
            if (ByEntity.TryGetValue(key, out var workers)) workers.Remove(worker);
        }

        /// <summary>Gives an entity each worker in a list that answers numbers, as a worker group installs its workers.</summary>
        internal static void ProvideEach<T>(ITarget entity, List<T> workers)
        {
            if (workers == null) return;
            for (int i = 0; i < workers.Count; i++)
                if (workers[i] is INumberWorker answers) Provide(entity, answers);
        }

        /// <summary>Takes back each worker in a list that answers numbers.</summary>
        internal static void RemoveEach<T>(ITarget entity, List<T> workers)
        {
            if (workers == null) return;
            for (int i = 0; i < workers.Count; i++)
                if (workers[i] is INumberWorker answers) Remove(entity, answers);
        }

        /// <summary>
        /// Asks an entity's workers for a value under a key, in the asking number's context; the first that answers is
        /// the value. False where none does.
        /// </summary>
        public static bool TryAsk(ITarget entity, Tag key, in NumberContext context, out float value)
        {
            value = 0f;
            var entityKey = KeyOf(entity);
            if (!key.IsValid || entityKey == null || !ByEntity.TryGetValue(entityKey, out var workers)) return false;
            for (int i = 0; i < workers.Count; i++)
                if (workers[i] != null && workers[i].TryGetNumber(key, in context, out value)) return true;
            value = 0f;
            return false;
        }

        /// <summary>
        /// Asks an effect's workers for a value under a key, about that application of the effect: in its own context,
        /// whose carrier is its spec. The first that answers is the value; false where none does.
        /// </summary>
        public static bool TryAsk(GameplayEffectSpec effect, Tag key, out float value)
        {
            value = 0f;
            var workers = effect?.Base != null ? effect.Base.Workers : null;
            if (!key.IsValid || workers == null || workers.Count == 0) return false;
            var about = NumberContext.Of(effect);
            for (int i = 0; i < workers.Count; i++)
                if (workers[i] is INumberWorker answers && answers.TryGetNumber(key, in about, out value)) return true;
            value = 0f;
            return false;
        }
    }
}
