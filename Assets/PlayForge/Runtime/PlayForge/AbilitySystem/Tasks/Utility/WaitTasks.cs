using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>What a Wait For Tag task waits for: its tag gained, or lost (FN-24).</summary>
    public enum ETagWait
    {
        /// <summary>The tag on its entity; one it holds already counts.</summary>
        Gained,

        /// <summary>The tag off its entity; one it doesn't hold counts.</summary>
        Lost,
    }

    /// <summary>
    /// A wait's own: what it waits on, its timeout and what it records where what it waits for comes (FN-24). A wait holds
    /// its stage until what it waits for comes or its timeout runs out, in its caster's time, and with no timeout as long as
    /// its stage runs.
    /// </summary>
    [Serializable]
    [ForgeCategory("Waits")]
    public abstract class AbstractWaitTask : AbstractAbilityTask
    {
        [Tooltip("Whose it waits on: its caster, or its activation's target")]
        public NumberSide On = NumberSide.Caster;

        [Tooltip("How long it waits, in seconds of its caster's time, before it gives up; 0, as long as its stage runs")]
        [NumberMin(0f)] public ScalableFloat Timeout = 0f;

        [Tooltip("Where what it waits for comes, the activation's data takes this tag, for a later stage's condition; none records nothing")]
        [ForgeTagContext(ForgeContext.StageFlag)]
        public Tag RecordAs;

        public override bool IsCriticalSection => false;

        /// <summary>
        /// Waits a step at a time until <paramref name="done"/> says so or the timeout runs out, in its caster's time, and
        /// records where it came; whether it came.
        /// </summary>
        protected async UniTask<bool> WaitFor(Func<bool> done, AbilityDataPacket data, CancellationToken token)
        {
            float timeout = Read(Timeout, data);
            float waited = 0f;
            while (!done())
            {
                if (timeout > 0f && waited >= timeout) return false;
                await ForgeTick.NextFrame(token);
                waited += ForgeTime.DeltaTimeFor(data);
            }
            if (RecordAs.IsValid) data.SetPrimary(RecordAs, true);
            return true;
        }
    }

    /// <summary>
    /// Waits for a gameplay event sent to its caster or its target, by its tag or one under it, as a combo's next stage
    /// waits for its hit to connect (FN-24). The core's own events, as Impact Taken, come while it waits, as the core sends
    /// them where anything listens.
    /// </summary>
    [Serializable]
    public class WaitForEventTask : AbstractWaitTask
    {
        [Tooltip("The event it waits for, by its tag or one under it")]
        [ForgeTagContext(ForgeContext.Event)]
        public Tag Event;

        public override string Description => Event.IsValid ? $"Wait for {Event}" : null;

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var abilities = EntityOf(data, On)?.GetAbilitySystem();
            if (abilities == null || !Event.IsValid) return;
            bool heard = false;
            void Hear(Tag tag, ProcessDataPacket _)
            {
                if (tag.MatchesOrIsChildOf(Event)) heard = true;
            }
            abilities.Callbacks.OnGameplayEvent += Hear;
            abilities.Await(Event);
            try
            {
                await WaitFor(() => heard, data, token);
            }
            finally
            {
                abilities.StopAwaiting(Event);
                abilities.Callbacks.OnGameplayEvent -= Hear;
            }
        }
    }

    /// <summary>
    /// Waits for its caster or its target to gain a tag, or to lose it, as a stage waits for a stun to wear off (FN-24). A
    /// tag already held counts as gained, and one not held as lost, so it waits for the state, not a change.
    /// </summary>
    [Serializable]
    public class WaitForTagTask : AbstractWaitTask
    {
        [Tooltip("The tag it waits for")]
        [ForgeTagContext(ForgeContext.Granted)]
        public Tag Tag;

        [Tooltip("What it waits for: the tag gained, or lost; one already held counts as gained, and one not held as lost")]
        public ETagWait Until = ETagWait.Gained;

        public override string Description => Tag.IsValid ? $"Wait until {Tag} is {(Until == ETagWait.Gained ? "gained" : "lost")}" : null;

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            var entity = EntityOf(data, On);
            if (entity == null || !Tag.IsValid) return;
            var tags = entity.GetTagCache();
            // An absent tag weighs -1, so a held one is above 0
            bool Holds() => tags != null && tags.GetWeight(Tag) > 0 == (Until == ETagWait.Gained);
            await WaitFor(Holds, data, token);
        }
    }

    /// <summary>
    /// Waits for an attribute of its caster or its target to cross a value, as a berserk's next stage waits for its Health
    /// to fall under half (FN-24). The value is read as it starts, at its ability's level; the attribute each step.
    /// </summary>
    [Serializable]
    public class WaitForAttributeTask : AbstractWaitTask
    {
        [Tooltip("The attribute it watches")]
        public Attribute Attribute;

        [Tooltip("Which of its values: its current, its base, what's missing of it, or its current's share of its base")]
        public EAttributeTargetBinary What = EAttributeTargetBinary.Current;

        [Tooltip("How the attribute compares with Value for it to stop waiting")]
        public EComparisonOperator Comparison = EComparisonOperator.LessOrEqual;

        [Tooltip("What the attribute is compared with, read as it starts at its ability's level")]
        public ScalableFloat Value = 0f;

        private static readonly RetentionGroups Whole = new();

        public override string Description => Attribute != null ? $"Wait until {Attribute.GetName()} {NumberComparison.Symbol(Comparison)} {Value}" : null;

        public override async UniTask Activate(AbilityDataPacket data, CancellationToken token)
        {
            if (Attribute == null) return;
            var context = NumberContext.Of(data);
            float against = Value == null ? 0f : Value.Evaluate(context);
            bool Holds() => context.TryGetAttribute(On, Attribute, Whole, out var value)
                            && NumberComparison.Holds(AttributeParts.Read(value, What), Comparison, against);
            await WaitFor(Holds, data, token);
        }
    }
}
