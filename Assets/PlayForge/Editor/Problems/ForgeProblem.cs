using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>How serious a problem is: a Warning is likely a mistake, and an Error stops something working.</summary>
    public enum ForgeProblemSeverity
    {
        Warning,
        Error,
    }

    /// <summary>One problem the validator found in a PlayForge asset: the asset, how serious it is, and what's wrong.</summary>
    public sealed class ForgeProblem
    {
        public ForgeProblem(Object asset, ForgeProblemSeverity severity, string message, string propertyPath = null)
        {
            Asset = asset;
            Severity = severity;
            Message = message;
            PropertyPath = propertyPath;
        }

        /// <summary>The asset the problem is in.</summary>
        public Object Asset { get; }

        public ForgeProblemSeverity Severity { get; }

        /// <summary>What's wrong, in a sentence or two, as the inspector says it in place.</summary>
        public string Message { get; }

        /// <summary>The field the problem is about, as a property path, or null for the asset as a whole.</summary>
        public string PropertyPath { get; }

        public override string ToString() => $"{Severity}: {Message}";
    }

    /// <summary>
    /// A check the validator runs on each PlayForge asset. Derive from it, with a public constructor that takes nothing,
    /// to add a check of your own: the validator finds every one (Q8).
    /// </summary>
    public abstract class ForgeValidationRule
    {
        /// <summary>Adds the problems this rule finds in the asset.</summary>
        public abstract void Check(Object asset, List<ForgeProblem> problems);
    }
}
