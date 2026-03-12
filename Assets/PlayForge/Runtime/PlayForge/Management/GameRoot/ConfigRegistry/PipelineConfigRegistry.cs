using System.Collections.Generic;
using JetBrains.Annotations;

namespace FarEmerald.PlayForge
{
    public static class PipelineConfigRegistry
    {
        public class ConfigPipeline
        {
            
        }
        
        /// <summary>
        /// [Calling Source] : ( [Asset Tag] : [Pipeline] )
        /// </summary>
        private static readonly Dictionary<ISource, Dictionary<Tag, List<GameplayPipelineConfiguration>>> configs = new();
        
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
