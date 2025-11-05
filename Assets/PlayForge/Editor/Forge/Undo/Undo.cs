// GasifyUndoHelpers.cs
using System;
using System.Reflection;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public static class Undo
    {
        /// Record a member set (field or property). Captures old/new and pushes to stack (and executes).
        public static void SetMemberWithUndo(this UndoStack stack, object target, MemberInfo member, object newValue, string label = null)
        {
            var acc = new MemberAccessor(target, member);
            var oldValue = acc.GetValue();

            // Skip if equal
            if (Equals(oldValue, newValue)) return;

            var act = new SetMemberAction(acc, oldValue, newValue, label);
            stack.Do(act);
        }

        public static MemberInfo GetFieldOrProperty(Type t, string name)
        {
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null) return f;
            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (p != null) return p;
            throw new MissingMemberException($"{t.Name}.{name} not found");
        }
    }
}
