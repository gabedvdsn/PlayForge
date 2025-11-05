// GasifyUndoCore.cs
using System;
using System.Collections.Generic;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace FarEmerald.PlayForge.Extended.Editor
{
    public interface IUndoable
    {
        string Label { get; }
        /// Execute forward (redo direction).
        void Do();
        /// Revert the change.
        void Undo();

        /// Unity objects affected (for Editor Undo tracking)
        IEnumerable<UnityEngine.Object> UnityTargets { get; }
        /// Optional coalescing hint: return a key that identifies "same operation context"
        /// (e.g., (target, memberName)); return null to disable coalescing.
        object CoalesceKey { get; }
        /// Attempt to merge another action into this one (return true if merged).
        bool TryCoalesce(IUndoable next);
    }

    /// <summary>
    /// Central manager for app-level undo/redo, with grouping and capacity.
    /// </summary>
    public sealed class UndoStack
    {
        private readonly Stack<IUndoable> _undo = new();
        private readonly Stack<IUndoable> _redo = new();
        private readonly List<IUndoable> _openGroup = new();
        private readonly int _capacity;
        private bool _isGrouping;
        private IUndoable _lastCommitted; // for coalescing

        public UndoStack(int capacity = 500)
        {
            _capacity = Math.Max(1, capacity);
        }

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public bool IsGrouping => _isGrouping;

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            _openGroup.Clear();
            _isGrouping = false;
            _lastCommitted = null;
        }

        /// Push and execute an action (clears redo).
        public void Do(IUndoable action)
        {
            if (action == null) return;

            // Execute forward
            EditorUndoBridge.BeforeDo(action);
            action.Do();
            EditorUndoBridge.AfterDo(action);

            // Handle grouping or standalone push
            if (_isGrouping)
            {
                _openGroup.Add(action);
            }
            else
            {
                // Coalesce with last if possible
                if (_lastCommitted != null
                    && _lastCommitted.CoalesceKey != null
                    && Equals(_lastCommitted.CoalesceKey, action.CoalesceKey)
                    && _lastCommitted.TryCoalesce(action))
                {
                    // merged; do nothing else
                }
                else
                {
                    PushUndo(action);
                    _lastCommitted = action;
                }
                _redo.Clear();
            }
        }

        /// Begin grouping multiple actions into a single composite undo item.
        public void BeginGroup()
        {
            if (_isGrouping) throw new InvalidOperationException("Group already open.");
            _isGrouping = true;
            _openGroup.Clear();
        }

        /// End group; pushes a single composite action (if any).
        public void EndGroup(string label = "Batch Edit")
        {
            if (!_isGrouping) return;
            _isGrouping = false;

            if (_openGroup.Count == 0) return;

            var composite = new CompositeAction(label, _openGroup.ToArray());
            _openGroup.Clear();

            // Coalesce with last if possible
            if (_lastCommitted is { CoalesceKey: not null }
                && Equals(_lastCommitted.CoalesceKey, composite.CoalesceKey)
                && _lastCommitted.TryCoalesce(composite))
            {
                // merged
            }
            else
            {
                PushUndo(composite);
                _lastCommitted = composite;
            }
            _redo.Clear();
        }

        public void Undo()
        {
            if (!CanUndo) return;
            var a = _undo.Pop();
            EditorUndoBridge.BeforeUndo(a);
            a.Undo();
            EditorUndoBridge.AfterUndo(a);
            _redo.Push(a);
            _lastCommitted = null; // break coalescing chain
        }

        public void Redo()
        {
            if (!CanRedo) return;
            var a = _redo.Pop();
            EditorUndoBridge.BeforeDo(a);
            a.Do();
            EditorUndoBridge.AfterDo(a);
            _undo.Push(a);
            _lastCommitted = a;
        }

        private void PushUndo(IUndoable a)
        {
            _undo.Push(a);
            // Enforce capacity
            while (_undo.Count > _capacity)
            {
                // Drop the oldest by rebuilding the stack (cheap enough for editor use)
                var tmp = new Stack<IUndoable>(_capacity);
                int keep = _capacity;
                foreach (var it in _undo)
                {
                    if (keep-- <= 0) break;
                    tmp.Push(it);
                }
                _undo.Clear();
                foreach (var it in tmp) _undo.Push(it);
            }
        }
    }

    /// Simple composite action for grouping.
    public sealed class CompositeAction : IUndoable
    {
        private readonly IUndoable[] _steps;
        public string Label { get; }
        public CompositeAction(string label, IUndoable[] steps)
        {
            Label = label ?? "Batch Edit";
            _steps = steps ?? Array.Empty<IUndoable>();
        }

        public void Do()
        {
            foreach (var s in _steps) s.Do();
        }
        public void Undo()
        {
            for (int i = _steps.Length - 1; i >= 0; --i)
                _steps[i].Undo();
        }

        public IEnumerable<UnityEngine.Object> UnityTargets
        {
            get
            {
                foreach (var s in _steps)
                    if (s.UnityTargets != null)
                        foreach (var t in s.UnityTargets) yield return t;
            }
        }

        public object CoalesceKey => null; // groups are typically final
        public bool TryCoalesce(IUndoable next) => false;
    }

    internal static class EditorUndoBridge
    {
#if UNITY_EDITOR
        public static void BeforeDo(IUndoable action)
        {
            RecordTargets(action, prefix: action.Label ?? "Do");
        }

        public static void AfterDo(IUndoable action)
        {
            MarkDirty(action);
        }

        public static void BeforeUndo(IUndoable action)
        {
            RecordTargets(action, prefix: "Undo " + (action.Label ?? ""));
        }

        public static void AfterUndo(IUndoable action)
        {
            MarkDirty(action);
        }

        private static void RecordTargets(IUndoable action, string prefix)
        {
            if (action.UnityTargets == null) return;
            var list = CachedTargets(action);
            if (list.Count > 0)
                UnityEditor.Undo.RecordObjects(list.ToArray(), string.IsNullOrEmpty(prefix) ? "Edit" : prefix);
        }

        private static void MarkDirty(IUndoable action)
        {
            if (action.UnityTargets == null) return;
            foreach (var t in action.UnityTargets)
                if (t) EditorUtility.SetDirty(t);
        }

        private static readonly List<UnityEngine.Object> _cache = new();
        private static List<UnityEngine.Object> CachedTargets(IUndoable a)
        {
            _cache.Clear();
            foreach (var t in a.UnityTargets)
                if (t) _cache.Add(t);
            return _cache;
        }
#else
        public static void BeforeDo(IUndoable action) { }
        public static void AfterDo(IUndoable action) { }
        public static void BeforeUndo(IUndoable action) { }
        public static void AfterUndo(IUndoable action) { }
#endif
    }
}
