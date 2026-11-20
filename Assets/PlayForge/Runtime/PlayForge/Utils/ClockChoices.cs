using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// A clock picked in an inspector, for what runs a sequence: an ability's Run Sequence task, or a component of your own.
    /// Each kind gives the clock for a run on a packet as the run starts; a [Serializable] class of your own made from this
    /// one is offered beside them. The field left empty, None, leaves the run the clock it would have had.
    /// </summary>
    [Serializable]
    public abstract class AbstractClockChoice
    {
        /// <summary>The clock a run on this packet goes by, or null for the one it would have had. Null here.</summary>
        public virtual IForgeClock ClockFor(ProcessDataPacket data) => null;

        /// <summary>
        /// The clock of a packet's source: an ability's caster's own, its time scale times the game's, or the game's for
        /// any other packet. A packet's own clock isn't asked: a choice is what gives one.
        /// </summary>
        protected static IForgeClock OwnersClock(ProcessDataPacket data) => ForgeTime.ClockOf(data?.Source);
    }

    /// <summary>An ability's caster's own time: its time scale times the game's; the game's for anything else.</summary>
    [Serializable]
    [ForgeMenu(Name = "Caster's", Description = "An ability's caster's own time, its time scale times the game's; the game's for anything else.")]
    public class CasterClockChoice : AbstractClockChoice
    {
        public override IForgeClock ClockFor(ProcessDataPacket data) => OwnersClock(data);
    }

    /// <summary>The game's time, whatever an ability's caster's is: a slowed caster's sequence on it isn't slowed.</summary>
    [Serializable]
    [ForgeMenu(Name = "Game's", Description = "The game's time, whatever an ability's caster's is.")]
    public class GameClockChoice : AbstractClockChoice
    {
        public override IForgeClock ClockFor(ProcessDataPacket data) => GameClock.Instance;
    }

    /// <summary>The game's time without its time scale: a sequence on it runs while the game is paused.</summary>
    [Serializable]
    [ForgeMenu(Name = "Unscaled", Description = "The game's time without its time scale: it runs while the game is paused or slowed.")]
    public class UnscaledClockChoice : AbstractClockChoice
    {
        public override IForgeClock ClockFor(ProcessDataPacket data) => UnscaledClock.Instance;
    }

    /// <summary>
    /// A pace of its own over another clock, as a finisher's slow motion at half or a flourish at double: a
    /// <see cref="ScaledClock"/> of its scale, made for each run, over the clock its Of picks, or the caster's.
    /// </summary>
    [Serializable]
    [ForgeMenu(Name = "Scaled", Description = "A pace of its own over another clock, as half speed or double; over the caster's unless it picks another.")]
    public class ScaledClockChoice : AbstractClockChoice
    {
        [Tooltip("How fast it runs against the clock it scales: 1 as that one, 0.5 at half, 2 at double, 0 stopped")]
        [Min(0f)] public float Scale = 1f;

        [Tooltip("The clock it scales: none for the caster's, or the game's, unscaled, or another")]
        [SerializeReference] public AbstractClockChoice Of;

        public override IForgeClock ClockFor(ProcessDataPacket data) =>
            new ScaledClock(Scale, Of?.ClockFor(data) ?? OwnersClock(data));
    }
}
