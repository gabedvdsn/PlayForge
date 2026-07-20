using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Level-gated logging for PlayForge runtime diagnostics. Framework code routes
    /// informational output through here so users control verbosity in one place —
    /// in-editor via The Forge → Settings → Logging (persisted and re-applied on domain
    /// reload), or from game code by setting <see cref="MinimumLevel"/> directly.
    ///
    /// <para>Deliberately NOT used for: authored debug tools the user invokes on purpose
    /// (TagCache.LogWeights, ThresholdDebuggerEffectWorker, LogAction), ProcessControl's
    /// own inspector-gated logs, or per-sequence EnableInjectionLogging output.</para>
    /// </summary>
    public static class ForgeLog
    {
        public enum ELevel
        {
            /// <summary>Chatty lifecycle traces (sequence chain progress, registrations).</summary>
            Verbose = 0,
            /// <summary>Notable one-off events.</summary>
            Info = 1,
            Warning = 2,
            Error = 3,
            /// <summary>Silence everything, including framework errors.</summary>
            None = 4,
        }

        /// <summary>Messages below this level are dropped. Default: Info.</summary>
        public static ELevel MinimumLevel = ELevel.Info;

        private const string Prefix = "[PlayForge] ";

        public static void Verbose(string message, Object context = null)
        {
            if (MinimumLevel > ELevel.Verbose) return;
            Debug.Log(Prefix + message, context);
        }

        public static void Info(string message, Object context = null)
        {
            if (MinimumLevel > ELevel.Info) return;
            Debug.Log(Prefix + message, context);
        }

        public static void Warning(string message, Object context = null)
        {
            if (MinimumLevel > ELevel.Warning) return;
            Debug.LogWarning(Prefix + message, context);
        }

        public static void Error(string message, Object context = null)
        {
            if (MinimumLevel > ELevel.Error) return;
            Debug.LogError(Prefix + message, context);
        }
    }
}
