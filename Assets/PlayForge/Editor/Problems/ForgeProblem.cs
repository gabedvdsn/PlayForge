using System;
using System.Collections.Generic;
using UnityEditor;
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

        /// <summary>
        /// The rule that found the problem, which the Problems view's By Rule view groups it under (FEAT-25). The validator
        /// gives it as the rule checks.
        /// </summary>
        public ForgeValidationRule Rule { get; internal set; }

        /// <summary>
        /// The one step that fixes the problem, where a simple step can, as the Problems view's Quick Fix; or null where
        /// fixing it needs a choice, as which stage should apply an ability's usage effects (FEAT-25).
        /// </summary>
        public ForgeQuickFix Fix { get; set; }

        public override string ToString() => $"{Severity}: {Message}";
    }

    /// <summary>
    /// A problem's quick fix (FEAT-25): what it does, as its button says, and the step itself. The step writes through a
    /// SerializedObject, or records Undo, so the Undo step the Problems view opens around it takes it back; and the
    /// validator, which follows each edit, checks the asset again.
    /// </summary>
    public sealed class ForgeQuickFix
    {
        public ForgeQuickFix(string name, Action apply)
        {
            Name = name;
            Apply = apply;
        }

        /// <summary>What the fix does, as "Store the Tag".</summary>
        public string Name { get; }

        /// <summary>The step that fixes the problem.</summary>
        public Action Apply { get; }
    }

    /// <summary>
    /// A check the validator runs on each PlayForge asset. Derive from it, with a public constructor that takes nothing,
    /// to add a check of your own: the validator finds every one (Q8). A problem a simple step fixes can carry the step
    /// as its <see cref="ForgeProblem.Fix"/> (FEAT-25).
    /// </summary>
    public abstract class ForgeValidationRule
    {
        /// <summary>Adds the problems this rule finds in the asset.</summary>
        public abstract void Check(Object asset, List<ForgeProblem> problems);

        /// <summary>
        /// What the rule finds, as the Problems view's By Rule view names its problems' group: by default its class's name,
        /// spaced out, without "Rule" (FEAT-25).
        /// </summary>
        public virtual string Name
        {
            get
            {
                var name = GetType().Name;
                if (name.EndsWith("Rule") && name.Length > 4) name = name.Substring(0, name.Length - 4);
                return ObjectNames.NicifyVariableName(name);
            }
        }
    }
}
