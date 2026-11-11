using System;
using System.Collections.Generic;
using System.Text;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A number's read, explained (FEAT-32): a tree of its parts, each with what it read and what it gave. Monkey King's
    /// charged hit reads −180 because its Group adds his Attack Damage, 100, to a Conditional that gives 80, as his Jingu
    /// Charges, 4, are above 0. A part whose context lacks what it reads says what it needs, "the target", and gives its
    /// fallback, so a read in the editor with no entities says where it would change in play.
    /// <para>
    /// Each part reads as the number does, through the same context, so the parts and the read agree. A read that rolls,
    /// as a Randomized scaler's, rolls again for its parts. A trace allocates, and is for the editor, tools and tests.
    /// </para>
    /// <para>
    /// A kind or a condition of a project's own explains its parts by overriding <see cref="NumberKind.Explain"/> or
    /// <see cref="GameplayCondition.Explain"/> and adding them: <c>trace.Add(Min, context, this, "Min")</c>.
    /// </para>
    /// </summary>
    public sealed class NumberTrace
    {
        private readonly List<NumberTrace> parts = new();

        /// <summary>How the part takes part in what holds it: "When", "True", "Add", "Min"; none for the read itself.</summary>
        public string Role { get; private set; }

        /// <summary>For a kind's or a condition's part, its name, as the kind menu gives it: "Conditional".</summary>
        public string Kind { get; private set; }

        /// <summary>The part's line, as its field's summary reads it.</summary>
        public string Label { get; private set; }

        /// <summary>What the part gave: a number's value, a kind's, or 1 for a condition that holds and 0 otherwise.</summary>
        public float Value { get; private set; }

        /// <summary>Whether the part is a condition, which holds or doesn't.</summary>
        public bool IsCondition { get; private set; }

        /// <summary>For a condition's part, whether it holds.</summary>
        public bool Holds { get; private set; }

        /// <summary>What this part reads that its context lacks, as "the target"; null where it read all it reads.</summary>
        public string Missing { get; private set; }

        /// <summary>The part's own parts.</summary>
        public IReadOnlyList<NumberTrace> Parts => parts;

        /// <summary>Whether everything the read and its parts read was there.</summary>
        public bool Resolved
        {
            get
            {
                if (Missing != null) return false;
                foreach (var part in parts)
                    if (!part.Resolved) return false;
                return true;
            }
        }

        /// <summary>The parts, this one among them, whose context lacks what they read.</summary>
        public IEnumerable<NumberTrace> Unresolved
        {
            get
            {
                if (Missing != null) yield return this;
                foreach (var part in parts)
                foreach (var unresolved in part.Unresolved)
                    yield return unresolved;
            }
        }

        /// <summary>What the read needs that its context lacks, as "the target" or "the caster and the target"; null where nothing.</summary>
        public string Needs
        {
            get
            {
                var needs = new List<string>();
                foreach (var part in Unresolved)
                    if (!needs.Contains(part.Missing)) needs.Add(part.Missing);
                return needs.Count == 0 ? null : NumberText.Join(needs);
            }
        }

        private NumberTrace() { }

        // ═══════════════════════════════════════════════════════════════════════════
        // Tracing a read
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>A number's read in a context, explained; with its owner where its object's own code reads it.</summary>
        public static NumberTrace Of(ScalableFloat number, in NumberContext context, object owner = null) =>
            Number(number, owner != null ? context.WithOwner(owner) : context, null);

        /// <summary>A whole number's read in a context, explained.</summary>
        public static NumberTrace Of(ScalableInt number, in NumberContext context, object owner = null)
        {
            var read = owner != null ? context.WithOwner(owner) : context;
            var trace = new NumberTrace { Label = NumberText.Of(number) };
            if (number == null) return trace;
            if (number.IsConstant) trace.Value = number.Constant;
            else if (number.Scaler is NumberKind kind && !kind.CanEvaluate(in read)) trace.Value = number.Magnitude;
            else trace.Value = number.Evaluate(in read);
            if (!number.IsConstant) trace.parts.Add(KindOf(number.Scaler, in read, null));
            return trace;
        }

        /// <summary>A kind's read in a context, explained: a scaler's, or a kind of number's.</summary>
        public static NumberTrace Of(AbstractScaler kind, in NumberContext context) => KindOf(kind, in context, null);

        /// <summary>A condition's read in a context, explained: whether it holds, and why.</summary>
        public static NumberTrace Of(GameplayCondition condition, in NumberContext context) => ConditionOf(condition, in context, null);

        // ═══════════════════════════════════════════════════════════════════════════
        // Adding parts, as a kind or a condition explains itself
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>Adds a number the part reads, read as its owner's code reads it, and returns it.</summary>
        public NumberTrace Add(ScalableFloat number, in NumberContext context, object owner, string role)
        {
            var part = Number(number, owner != null ? context.WithOwner(owner) : context, role);
            parts.Add(part);
            return part;
        }

        /// <summary>Adds a kind the part reads, as a Group's member, and returns it.</summary>
        public NumberTrace Add(AbstractScaler kind, in NumberContext context, string role)
        {
            var part = KindOf(kind, in context, role);
            parts.Add(part);
            return part;
        }

        /// <summary>Adds a condition the part reads, and returns it.</summary>
        public NumberTrace Add(GameplayCondition condition, in NumberContext context, string role)
        {
            var part = ConditionOf(condition, in context, role);
            parts.Add(part);
            return part;
        }

        /// <summary>Adds a value the part read, as an attribute's or a level, and returns it.</summary>
        public NumberTrace Add(string label, float value, string role = null)
        {
            var part = new NumberTrace { Role = role, Label = label, Value = value };
            parts.Add(part);
            return part;
        }

        /// <summary>Says the part reads something its context lacks, as "the target".</summary>
        public NumberTrace Needing(string missing)
        {
            Missing = string.IsNullOrEmpty(missing) ? "what it reads" : missing;
            return this;
        }

        // A number: its value, read as a field reads it, and its kind's read, for a number that isn't a constant
        private static NumberTrace Number(ScalableFloat number, in NumberContext context, string role)
        {
            var trace = new NumberTrace { Role = role, Label = NumberText.Of(number) };
            if (number == null) return trace;
            // A kind that can't be worked out reads its field's number, as Evaluate does, without its warning
            if (number.IsConstant) trace.Value = number.Constant;
            else if (number.Scaler is NumberKind kind && !kind.CanEvaluate(in context)) trace.Value = number.Magnitude;
            else trace.Value = number.Evaluate(in context);
            if (!number.IsConstant) trace.parts.Add(KindOf(number.Scaler, in context, null));
            return trace;
        }

        private static NumberTrace KindOf(AbstractScaler kind, in NumberContext context, string role)
        {
            var trace = new NumberTrace { Role = role, Kind = NumberText.KindName(kind?.GetType()), Label = NumberText.Describe(kind) };
            if (kind == null) return trace;

            switch (kind)
            {
                case NumberKind own:
                    if (!own.CanEvaluate(in context))
                    {
                        trace.Value = own.FallbackValue;
                        trace.Needing(NumberText.Reads(own.Reads & ~context.Provides));
                        return trace;
                    }
                    trace.Value = NumberContext.ReadScaler(kind, in context);
                    own.Explain(in context, trace);
                    return trace;

                case ScalerGroup { HasMembers: true } group:
                    trace.Value = NumberContext.ReadScaler(kind, in context);
                    foreach (var member in group.Calculations)
                        if (member.Calculation != null) trace.Add(member.Calculation, in context, OperationName(member.RelativeOperation));
                    return trace;

                case AttributeBackedScaler attribute:
                    var side = attribute.CaptureFrom == ESourceTarget.Target ? NumberSide.Target : NumberSide.Caster;
                    trace.Value = NumberContext.ReadScaler(kind, in context);
                    if (context.EntityOn(side) == null) trace.Needing(side == NumberSide.Target ? "the target" : "the caster");
                    return trace;

                case RandomizedScaler randomized:
                    trace.Value = NumberContext.ReadScaler(kind, in context);
                    trace.Label = $"{trace.Label}, a roll";
                    if (randomized.VarianceMode == EVarianceMode.Multiplier)
                    {
                        trace.Add(randomized.MinMultiplier, in context, randomized, "Min");
                        trace.Add(randomized.MaxMultiplier, in context, randomized, "Max");
                    }
                    else trace.Add(randomized.Variance, in context, randomized, "Variance");
                    return trace;

                default:
                    trace.Value = NumberContext.ReadScaler(kind, in context);
                    return trace;
            }
        }

        private static NumberTrace ConditionOf(GameplayCondition condition, in NumberContext context, string role)
        {
            var trace = new NumberTrace
            {
                Role = role, IsCondition = true, Kind = condition == null ? null : NumberText.ConditionName(condition.GetType()),
                Label = NumberText.Of(condition),
            };
            if (condition == null) return trace;
            if (!condition.CanEvaluate(in context)) return trace.Needing(NumberText.Reads(condition.Reads & ~context.Provides));
            trace.Holds = condition.Holds(in context);
            trace.Value = trace.Holds ? 1f : 0f;
            condition.Explain(in context, trace);
            return trace;
        }

        private static string OperationName(ECalculationOperation operation) => operation switch
        {
            ECalculationOperation.Add => "Add",
            ECalculationOperation.Multiply => "Multiply",
            ECalculationOperation.FlatBonus => "Flat Bonus",
            ECalculationOperation.Override => "Override",
            _ => operation.ToString(),
        };

        // ═══════════════════════════════════════════════════════════════════════════
        // As text
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The tree as lines, a part to a line, each under what reads it: "True: −0.1 × Target's Health (base) = −20", and a
        /// condition's "When: Target's Health &lt; 0.3 × Target's Health (base): holds".
        /// </summary>
        public override string ToString()
        {
            var text = new StringBuilder();
            Write(text, 0);
            return text.ToString();
        }

        private void Write(StringBuilder text, int depth)
        {
            if (depth > 0) text.Append('\n');
            text.Append(' ', depth * 2);
            if (!string.IsNullOrEmpty(Role)) text.Append(Role).Append(": ");
            text.Append(string.IsNullOrEmpty(Label) ? Kind ?? "" : Label);
            if (IsCondition)
            {
                if (Missing == null) text.Append(Holds ? ": holds" : ": doesn't hold");
            }
            else text.Append(" = ").Append(NumberText.Number(Value));
            if (Missing != null) text.Append(" (needs ").Append(Missing).Append(')');
            foreach (var part in parts) part.Write(text, depth + 1);
        }
    }
}
