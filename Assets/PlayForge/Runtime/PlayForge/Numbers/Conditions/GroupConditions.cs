using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// Whether every one of its conditions holds (FEAT-29); with none, it holds. A member that can't be worked out where
    /// it's read doesn't hold, so neither does this.
    /// </summary>
    [Serializable]
    [GameplayCondition("All Of", Category = "Logic")]
    public class AllCondition : GameplayCondition
    {
        [Tooltip("The conditions that must all hold")]
        [SerializeReference]
        public List<GameplayCondition> Conditions = new();

        // It reads what its members do, and each member says for itself where it can't be worked out
        public override NumberReads Reads => GroupCondition.ReadsOf(Conditions);
        public override bool CanEvaluate(in NumberContext context) => true;

        protected override bool Evaluate(in NumberContext context)
        {
            if (Conditions == null) return true;
            for (int i = 0; i < Conditions.Count; i++)
                if (Conditions[i] != null && !Conditions[i].Holds(in context)) return false;
            return true;
        }

        public override void Initialize(IAttributeImpactCarrier carrier) => GroupCondition.Initialize(Conditions, carrier);

        public override string Describe() => GroupCondition.Describe(Conditions, " and ", "always");
    }

    /// <summary>
    /// Whether any one of its conditions holds (FEAT-29); with none, it doesn't. A member that can't be worked out where
    /// it's read doesn't hold, and the others still can.
    /// </summary>
    [Serializable]
    [GameplayCondition("Any Of", Category = "Logic")]
    public class AnyCondition : GameplayCondition
    {
        [Tooltip("The conditions of which one must hold")]
        [SerializeReference]
        public List<GameplayCondition> Conditions = new();

        public override NumberReads Reads => GroupCondition.ReadsOf(Conditions);
        public override bool CanEvaluate(in NumberContext context) => true;

        protected override bool Evaluate(in NumberContext context)
        {
            if (Conditions == null) return false;
            for (int i = 0; i < Conditions.Count; i++)
                if (Conditions[i] != null && Conditions[i].Holds(in context)) return true;
            return false;
        }

        public override void Initialize(IAttributeImpactCarrier carrier) => GroupCondition.Initialize(Conditions, carrier);

        public override string Describe() => GroupCondition.Describe(Conditions, " or ", "never");
    }

    /// <summary>
    /// Whether its condition doesn't hold (FEAT-29). Where that condition can't be worked out, neither can this, so it
    /// doesn't hold either: not having a target's tag doesn't hold where there's no target.
    /// </summary>
    [Serializable]
    [GameplayCondition("Not", Category = "Logic")]
    public class NotCondition : GameplayCondition
    {
        [Tooltip("The condition that must not hold")]
        [SerializeReference]
        public GameplayCondition Condition;

        public override NumberReads Reads => Condition?.Reads ?? NumberReads.None;

        public override bool CanEvaluate(in NumberContext context) => Condition == null || Condition.CanEvaluate(in context);

        protected internal override string WhyNot(in NumberContext context) =>
            Condition != null ? Condition.WhyNot(in context) : base.WhyNot(in context);

        protected override bool Evaluate(in NumberContext context) => Condition != null && !Condition.Holds(in context);

        public override void Initialize(IAttributeImpactCarrier carrier) => Condition?.Initialize(carrier);

        public override string Describe() => $"not ({NumberText.Of(Condition)})";
    }

    // What All Of and Any Of share
    internal static class GroupCondition
    {
        public static NumberReads ReadsOf(List<GameplayCondition> conditions)
        {
            var reads = NumberReads.None;
            if (conditions == null) return reads;
            for (int i = 0; i < conditions.Count; i++)
                if (conditions[i] != null) reads |= conditions[i].Reads;
            return reads;
        }

        public static void Initialize(List<GameplayCondition> conditions, IAttributeImpactCarrier carrier)
        {
            if (conditions == null) return;
            for (int i = 0; i < conditions.Count; i++) conditions[i]?.Initialize(carrier);
        }

        // Its members' lines joined, a group among them in brackets: "(a or b) and c"
        public static string Describe(List<GameplayCondition> conditions, string join, string none)
        {
            var text = new StringBuilder();
            int count = 0;
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    var condition = conditions[i];
                    if (condition == null) continue;
                    if (count++ > 0) text.Append(join);
                    bool group = condition is AllCondition { Conditions: { Count: > 1 } } or AnyCondition { Conditions: { Count: > 1 } };
                    text.Append(group ? $"({NumberText.Of(condition)})" : NumberText.Of(condition));
                }
            }
            return count == 0 ? none : text.ToString();
        }
    }
}
