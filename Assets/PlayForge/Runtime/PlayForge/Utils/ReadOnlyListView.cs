using System.Collections;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A read-only view of a list that <c>foreach</c> walks without allocating, through the list's own struct
    /// enumerator. Walked as an <see cref="IEnumerable{T}"/>, as any list's, the enumerator is boxed (GAS-P4).
    /// </summary>
    public readonly struct ReadOnlyListView<T> : IReadOnlyList<T>
    {
        private static readonly List<T> None = new();

        private readonly List<T> list;

        public ReadOnlyListView(List<T> list)
        {
            this.list = list;
        }

        public int Count => (list ?? None).Count;

        public T this[int index] => (list ?? None)[index];

        public List<T>.Enumerator GetEnumerator() => (list ?? None).GetEnumerator();

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
