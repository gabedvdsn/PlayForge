// GasifyUndoActions.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

#if UNITY_EDITOR
using UnityEngine;
#endif

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// Unified accessor for FieldInfo or PropertyInfo.
    public readonly struct MemberAccessor
    {
        public readonly object Target;
        public readonly MemberInfo Member;

        public MemberAccessor(object target, MemberInfo member)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            Member = member ?? throw new ArgumentNullException(nameof(member));
            if (target.GetType().IsValueType)
                throw new NotSupportedException("Value-type targets not supported (boxed structs won't mutate). Wrap in a reference container.");
        }

        public Type MemberType =>
            Member switch
            {
                FieldInfo f  => f.FieldType,
                PropertyInfo p => p.PropertyType,
                _ => throw new NotSupportedException("Unsupported member")
            };

        public object GetValue() =>
            Member switch
            {
                FieldInfo f    => f.GetValue(Target),
                PropertyInfo p => p.GetValue(Target),
                _ => throw new NotSupportedException("Unsupported member")
            };

        public void SetValue(object value)
        {
            switch (Member)
            {
                case FieldInfo f:
                    f.SetValue(Target, value);
                    break;
                case PropertyInfo p:
                    if (!p.CanWrite)
                        throw new InvalidOperationException($"Property {p.Name} not writable.");
                    p.SetValue(Target, value);
                    break;
                default: throw new NotSupportedException("Unsupported member");
            }
        }

#if UNITY_EDITOR
        public IEnumerable<UnityEngine.Object> UnityTargets
        {
            get
            {
                if (Target is UnityEngine.Object uo) { yield return uo; }
            }
        }
#endif

        public string Key => $"{Target.GetHashCode()}::{Member.DeclaringType?.FullName}::{Member.Name}";
    }

    /// Set a member value (field or property) with old/new values.
    public sealed class SetMemberAction : IUndoable
    {
        private readonly MemberAccessor _acc;
        private object _oldValue;
        private object _newValue;

        public string Label { get; }
        public SetMemberAction(MemberAccessor acc, object oldValue, object newValue, string label = null)
        {
            _acc = acc;
            _oldValue = oldValue;
            _newValue = newValue;
            Label = label ?? $"Set {_acc.Member.Name}";
        }

        public void Do()   => _acc.SetValue(_newValue);
        public void Undo() => _acc.SetValue(_oldValue);

        public IEnumerable<UnityEngine.Object> UnityTargets
        {
            get
            {
#if UNITY_EDITOR
                return _acc.UnityTargets;
#else
                return Array.Empty<UnityEngine.Object>();
#endif
            }
        }

        public object CoalesceKey => _acc.Key;

        // Merge rapid successive sets on the same member/target: keep the earliest old, latest new
        public bool TryCoalesce(IUndoable next)
        {
            if (next is SetMemberAction s && s._acc.Key == _acc.Key)
            {
                _newValue = s._newValue;
                return true;
            }
            return false;
        }
    }

    // === List actions ===

    public sealed class ListInsertAction<T> : IUndoable
    {
        private readonly IList<T> _list;
        private readonly int _index;
        private readonly T _item;
        private readonly UnityEngine.Object _unityTarget;
        public string Label { get; }

        public ListInsertAction(IList<T> list, int index, T item, UnityEngine.Object unityTarget = null, string label = null)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _index = index;
            _item = item;
            _unityTarget = unityTarget;
            Label = label ?? "Insert Item";
        }

        public void Do()   => _list.Insert(_index, _item);
        public void Undo() => _list.RemoveAt(_index);

        public IEnumerable<UnityEngine.Object> UnityTargets => _unityTarget ? new[] { _unityTarget } : Array.Empty<UnityEngine.Object>();
        public object CoalesceKey => null;
        public bool TryCoalesce(IUndoable next) => false;
    }

    public sealed class ListRemoveAtAction<T> : IUndoable
    {
        private readonly IList<T> _list;
        private readonly int _index;
        private T _removed;
        private readonly UnityEngine.Object _unityTarget;
        public string Label { get; }

        public ListRemoveAtAction(IList<T> list, int index, UnityEngine.Object unityTarget = null, string label = null)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _index = index;
            _unityTarget = unityTarget;
            Label = label ?? "Remove Item";
        }

        public void Do()
        {
            _removed = _list[_index];
            _list.RemoveAt(_index);
        }

        public void Undo()
        {
            _list.Insert(_index, _removed);
        }

        public IEnumerable<UnityEngine.Object> UnityTargets => _unityTarget ? new[] { _unityTarget } : Array.Empty<UnityEngine.Object>();
        public object CoalesceKey => null;
        public bool TryCoalesce(IUndoable next) => false;
    }

    public sealed class ListSetAction<T> : IUndoable
    {
        private readonly IList<T> _list;
        private readonly int _index;
        private T _old;
        private T _new;
        private readonly UnityEngine.Object _unityTarget;
        public string Label { get; }

        public ListSetAction(IList<T> list, int index, T newValue, UnityEngine.Object unityTarget = null, string label = null)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _index = index;
            _unityTarget = unityTarget;
            _new = newValue;
            Label = label ?? "Set Item";
        }

        public void Do()
        {
            _old = _list[_index];
            _list[_index] = _new;
        }

        public void Undo()
        {
            _list[_index] = _old;
        }

        public IEnumerable<UnityEngine.Object> UnityTargets => _unityTarget ? new[] { _unityTarget } : Array.Empty<UnityEngine.Object>();
        public object CoalesceKey => (_list, _index);
        public bool TryCoalesce(IUndoable next)
        {
            if (next is ListSetAction<T> ls && ReferenceEquals(ls._list, _list) && ls._index == _index)
            {
                _new = ls._new;
                return true;
            }
            return false;
        }
    }

    // === Dictionary actions ===

    public sealed class DictionaryAddAction<TKey, TValue> : IUndoable
    {
        private readonly IDictionary<TKey, TValue> _dict;
        private readonly TKey _key;
        private readonly TValue _value;
        private readonly UnityEngine.Object _unityTarget;
        public string Label { get; }

        public DictionaryAddAction(IDictionary<TKey, TValue> dict, TKey key, TValue value, UnityEngine.Object unityTarget = null, string label = null)
        {
            _dict = dict ?? throw new ArgumentNullException(nameof(dict));
            _key = key;
            _value = value;
            _unityTarget = unityTarget;
            Label = label ?? "Add Entry";
        }

        public void Do()   => _dict.Add(_key, _value);
        public void Undo() => _dict.Remove(_key);

        public IEnumerable<UnityEngine.Object> UnityTargets => _unityTarget ? new[] { _unityTarget } : Array.Empty<UnityEngine.Object>();
        public object CoalesceKey => null;
        public bool TryCoalesce(IUndoable next) => false;
    }

    public sealed class DictionaryRemoveAction<TKey, TValue> : IUndoable
    {
        private readonly IDictionary<TKey, TValue> _dict;
        private readonly TKey _key;
        private TValue _removed;
        private bool _hadValue;
        private readonly UnityEngine.Object _unityTarget;
        public string Label { get; }

        public DictionaryRemoveAction(IDictionary<TKey, TValue> dict, TKey key, UnityEngine.Object unityTarget = null, string label = null)
        {
            _dict = dict ?? throw new ArgumentNullException(nameof(dict));
            _key = key;
            _unityTarget = unityTarget;
            Label = label ?? "Remove Entry";
        }

        public void Do()
        {
            _hadValue = _dict.TryGetValue(_key, out _removed);
            if (_hadValue) _dict.Remove(_key);
        }

        public void Undo()
        {
            if (_hadValue) _dict[_key] = _removed;
        }

        public IEnumerable<UnityEngine.Object> UnityTargets => _unityTarget ? new[] { _unityTarget } : Array.Empty<UnityEngine.Object>();
        public object CoalesceKey => null;
        public bool TryCoalesce(IUndoable next) => false;
    }
}
