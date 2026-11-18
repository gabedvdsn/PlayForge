using System.Collections.Generic;
using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A static store of <c>GameplayPipelineConfiguration</c> lists, kept per source and per asset tag, and cleared as
    /// play starts. Nothing in the package registers or reads one, and <c>Register</c> adds only to a list a source and
    /// a tag already hold, which nothing makes.
    /// </summary>
    public static class PipelineConfigRegistry
    {
        /// <summary>
        /// A class with no members, which nothing in the package uses.
        /// </summary>
        public class ConfigPipeline
        {
            
        }
        
        /// <summary>
        /// [Calling Source] : ( [Asset Tag] : [Pipeline] )
        /// </summary>
        private static readonly Dictionary<ISource, Dictionary<Tag, List<GameplayPipelineConfiguration>>> configs = new();

        // Its keys are one session's sources, and statics outlive play mode when domain reload is off
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear() => configs.Clear();
        
        public static void Register(ISource caller, Tag target, IEnumerable<GameplayPipelineConfiguration> pipeline)
        {
            if (configs.ContainsKey(caller) && configs[caller].ContainsKey(target))
            {
                configs[caller][target].AddRange(pipeline);
            }
        }
        
        public static void Register(ISource caller, Tag target, params GameplayPipelineConfiguration[] pipeline)
        {
            if (configs.ContainsKey(caller) && configs[caller].ContainsKey(target))
            {
                configs[caller][target].AddRange(pipeline);
            }
        }

        public static void Unregister(ISource caller)
        {
            configs.Remove(caller);
        }
        
        public static void Unregister(ISource caller, Tag target)
        {
            if (!configs.ContainsKey(caller)) return;
            configs[caller].Remove(target);
        }
        
        public static void Unregister(ISource caller, Tag target, GameplayPipelineConfiguration pipeline)
        {
            if (!configs.ContainsKey(caller) || !configs[caller].ContainsKey(target)) return;
            configs[caller][target].Remove(pipeline);
        }
        
        public static void Unregister(ISource caller, Tag target, IEnumerable<GameplayPipelineConfiguration> pipeline)
        {
            if (!configs.ContainsKey(caller) || !configs[caller].ContainsKey(target)) return;
            foreach (var p in pipeline) configs[caller][target].Remove(p);
        }
        
        public static void Unregister(ISource caller, Tag target, params GameplayPipelineConfiguration[] pipeline)
        {
            if (!configs.ContainsKey(caller) || !configs[caller].ContainsKey(target)) return;
            foreach (var p in pipeline) configs[caller][target].Remove(p);
        }
        
        // methods to find config class

        public static bool AnyConfigsRegistered(ISource caller)
        {
            return configs.ContainsKey(caller);
        }

        [CanBeNull]
        public static List<GameplayPipelineConfiguration> GetConfigPipeline(ISource caller, Tag target)
        {
            if (!configs.TryGetValue(caller, out var targets) || !targets.TryGetValue(target, out var pipeline))
            {
                return null;
            }

            return pipeline;
        }
    }
}
