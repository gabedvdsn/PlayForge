using System;
using System.Collections.Generic;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A condition read against a number's context (FEAT-29, Decision 43): whether something holds where the number is
    /// read, as "the target's Health is below 0.3 of its base". A Conditional scaler reads one to choose its branch. It is
    /// a class, so a project adds its own as it adds a kind of number: override <see cref="Evaluate(in NumberContext)"/>,
    /// and name it, file it and say what it reads with <see cref="GameplayConditionAttribute"/>:
    /// <code>
    /// [Serializable]
    /// [GameplayCondition("Target Is Caster", Reads = NumberReads.Caster | NumberReads.Target)]
    /// public class TargetIsCaster : GameplayCondition
    /// {
    ///     protected override bool Evaluate(in NumberContext context) => ReferenceEquals(context.Caster, context.Target);
    ///
    ///     public override string Describe() => "the target is the caster";
    /// }
    /// </code>
    /// <para>
    /// Where the context lacks what a condition reads, as a target's tag where there's no target, it doesn't hold, and it
    /// logs once. A condition's own numbers read with the condition as their owner. Its fields draw in the inspector with
    /// no editor code, with <c>[Tooltip]</c> and <c>[ForgeShowWhen]</c>; it is saved by its class's name, as a
    /// <c>[SerializeReference]</c> value is, so renaming the class needs Unity's <c>[MovedFrom]</c>.
    /// </para>
    /// </summary>
    [Serializable]
    public abstract class GameplayCondition
    {
        // What it reads, from its [GameplayCondition], looked up once
        [NonSerialized] private bool readsKnown;
        [NonSerialized] private NumberReads reads;

        /// <summary>
        /// Works out whether the condition holds. The context holds what the number's site has; what the condition reads,
        /// as <see cref="GameplayConditionAttribute.Reads"/> declares, is there.
        /// </summary>
        protected virtual bool Evaluate(in NumberContext context) => false;

        /// <summary>
        /// The condition's one line, as "Target's Health &lt; 50", which a Conditional's summary reads between its branches.
        /// With none, it reads as the condition's name.
        /// </summary>
        public virtual string Describe() => null;

        /// <summary>
        /// What the condition needs of its context to be worked out: by default, what its
        /// <see cref="GameplayConditionAttribute"/> says. A condition whose needs follow its settings overrides this, as
        /// a tag condition reads the side it looks at.
        /// </summary>
        public virtual NumberReads Reads
        {
            get
            {
                if (!readsKnown)
                {
                    reads = GameplayConditionAttribute.Of(GetType())?.Reads ?? NumberReads.None;
                    readsKnown = true;
                }
                return reads;
            }
        }

        /// <summary>
        /// Whether the condition can be worked out in this context: by default, whether the context provides what it reads
        /// (<see cref="Reads"/>). Where it can't, it doesn't hold, and it logs once.
        /// </summary>
        public virtual bool CanEvaluate(in NumberContext context) => context.Has(Reads);

        /// <summary>Why the condition can't be worked out in this context, for the log: what it reads that isn't there.</summary>
        protected internal virtual string WhyNot(in NumberContext context) =>
            $"it reads {Reads & ~context.Provides}, which isn't there";

        /// <summary>
        /// Readies the condition's numbers against an impact carrier, as an effect's spec readies its own as it's made: a
        /// scaler that captures an attribute on creation captures it here. A condition that holds numbers or other
        /// conditions passes it on to them.
        /// </summary>
        public virtual void Initialize(IAttributeImpactCarrier carrier) { }

        // How deep conditions are being read inside each other, so one that holds itself stops
        private const int MostDepth = 16;
        private static int depth;

        // The conditions that have said they can't be worked out where they were read, each said once
        private static readonly HashSet<Type> Unresolved = new();
        private static bool loggedDepth;

        /// <summary>
        /// Whether the condition holds in this context. Where it can't be worked out there, it doesn't, and it logs once.
        /// A read allocates nothing.
        /// </summary>
        public bool Holds(in NumberContext context)
        {
            if (!CanEvaluate(in context))
            {
                if (Unresolved.Add(GetType()))
                    ForgeLog.Warning($"A {NumberText.ConditionName(GetType())} condition can't be worked out where it was read: {WhyNot(in context)}, so it doesn't hold (FEAT-29).");
                return false;
            }
            if (depth >= MostDepth)
            {
                if (!loggedDepth)
                {
                    loggedDepth = true;
                    ForgeLog.Warning($"A {NumberText.ConditionName(GetType())} condition is read inside itself, {MostDepth} deep, so it doesn't hold (FEAT-29).");
                }
                return false;
            }

            depth++;
            try
            {
                return Evaluate(in context);
            }
            finally
            {
                depth--;
            }
        }

        public override string ToString() => NumberText.Of(this);
    }

    /// <summary>
    /// Names a condition in the condition menu, files it under a category, and says what it reads of a number's context
    /// (FEAT-29). Without it, a condition takes its class's name without "Condition", and is read everywhere.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GameplayConditionAttribute : System.Attribute
    {
        /// <summary>The condition's name in the condition menu, as "Target Is Caster".</summary>
        public string Name { get; }

        /// <summary>The condition menu's group the condition sits in, as "Logic"; with none, the menu's top level.</summary>
        public string Category { get; set; }

        /// <summary>What the condition needs of a number's context, as <c>NumberReads.Caster | NumberReads.Target</c>.</summary>
        public NumberReads Reads { get; set; }

        public GameplayConditionAttribute(string name)
        {
            Name = name;
        }

        private static readonly Dictionary<Type, GameplayConditionAttribute> Known = new();

        /// <summary>A condition's attribute, or null where it has none. Looked up once a type.</summary>
        public static GameplayConditionAttribute Of(Type type)
        {
            if (type == null) return null;
            if (Known.TryGetValue(type, out var found)) return found;
            found = (GameplayConditionAttribute)GetCustomAttribute(type, typeof(GameplayConditionAttribute), false);
            Known[type] = found;
            return found;
        }
    }

    /// <summary>A comparison between two numbers, as a condition makes one, and how a line writes it (FEAT-29).</summary>
    public static class NumberComparison
    {
        /// <summary>Whether <paramref name="left"/> compares to <paramref name="right"/> as the operator says.</summary>
        public static bool Holds(float left, EComparisonOperator comparison, float right) => comparison switch
        {
            EComparisonOperator.LessThan => left < right,
            EComparisonOperator.LessOrEqual => left <= right,
            EComparisonOperator.Equal => UnityEngine.Mathf.Approximately(left, right),
            EComparisonOperator.NotEqual => !UnityEngine.Mathf.Approximately(left, right),
            EComparisonOperator.GreaterOrEqual => left >= right,
            EComparisonOperator.GreaterThan => left > right,
            _ => false,
        };

        /// <summary>The operator as a line writes it: &lt;, ≤, =, ≠, ≥ or &gt;.</summary>
        public static string Symbol(EComparisonOperator comparison) => comparison switch
        {
            EComparisonOperator.LessThan => "<",
            EComparisonOperator.LessOrEqual => "≤",
            EComparisonOperator.Equal => "=",
            EComparisonOperator.NotEqual => "≠",
            EComparisonOperator.GreaterOrEqual => "≥",
            EComparisonOperator.GreaterThan => ">",
            _ => "?",
        };
    }
}
