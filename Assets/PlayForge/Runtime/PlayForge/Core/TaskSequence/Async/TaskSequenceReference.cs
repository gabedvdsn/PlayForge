using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Marks a static method as a TaskSequence/TaskSequenceChain provider.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TaskSequenceMethodAttribute : System.Attribute
    {
        public string DisplayName { get; }
        public TaskSequenceMethodAttribute(string displayName = null) => DisplayName = displayName;
    }
    
    /// <summary>
    /// Serializable reference to a TaskSequence method.
    /// </summary>
    [Serializable]
    public struct TaskSequenceReference
    {
        [SerializeField] private string _typeName;
        [SerializeField] private string _methodName;
        
        public bool IsValid => !string.IsNullOrEmpty(_typeName) && !string.IsNullOrEmpty(_methodName);
        
        public TaskSequenceReference(MethodInfo method)
        {
            _typeName = method.DeclaringType?.AssemblyQualifiedName;
            _methodName = method.Name;
        }
        
        /// <summary>
        /// The sequence the method makes: a sequence or a chain as it returns one, and a sequence of the definition it
        /// returns, which the sequence dropdown offers too and which found no sequence before.
        /// </summary>
        public IActiveSequence GetActiveSequence() => Invoke() switch
        {
            IActiveSequence sequence => sequence,
            TaskSequenceDefinition definition => new TaskSequence(definition),
            _ => null
        };
        
        public bool IsChain() => GetMethod()?.ReturnType == typeof(TaskSequenceChain);
        
        public object Invoke() => GetMethod()?.Invoke(null, null);
        
        public MethodInfo GetMethod()
        {
            if (!IsValid) return null;
            var type = Type.GetType(_typeName);
            return type?.GetMethod(_methodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }
        
        public string GetDisplayName()
        {
            var method = GetMethod();
            if (method == null) return _methodName ?? "(None)";
            var attr = method.GetCustomAttribute<TaskSequenceMethodAttribute>();
            return attr?.DisplayName ?? method.Name;
        }
    }
}
