using FarEmerald.PlayForge.Lab;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>
    /// A hero or the dummy in the arena: the Creator Lab's unit (LAB-7), which walks where it's sent, attacks what it's set
    /// on, casts its abilities and its items', and holds its items, with the Demo's seat and game besides. Its body, its
    /// movement and its casting are the lab's, so the two can't drift; the Demo's roster gives it its rules.
    /// </summary>
    public class DemoUnit : LabUnit
    {
        public DemoGame Game { get; private set; }
        public DemoSeat Seat { get; private set; }

        public bool IsPlayer => Seat != null && Seat.IsPlayer;
        public bool IsDummy => Seat != null && Seat.IsDummy;
        public DemoRoster Roster => Game != null ? Game.Roster : null;

        /// <summary>
        /// A unit for a seat: a body in its color, and its entity, registered at the seat's level on the seat's side, with
        /// the seat's abilities' levels and its items. The AI's casts carry its difficulty, which every damage effect
        /// multiplies by.
        /// </summary>
        public static DemoUnit Create(DemoGame game, DemoSeat seat, EntityIdentity identity, Color color, Vector3 at, Quaternion facing)
        {
            var unit = Create<DemoUnit>(new LabUnitSpec
            {
                Identity = identity, Rules = game.Roster, Side = new LabSide(seat.IsPlayer ? "You" : "Opponent", game.SideOf(seat), color),
                Level = seat.Level, Color = color, At = at, Facing = facing, Material = game.BodyMaterial, Ring = false,
            });
            if (unit == null) return null;
            unit.Game = game;
            unit.Seat = seat;
            unit.HidesFromView = !seat.IsPlayer;
            unit.Decorating += (caster, data) =>
            {
                var roster = game.Roster;
                if (!seat.IsPlayer && roster != null && roster.Difficulty.IsValid) data.SetByCaller(roster.Difficulty, game.DifficultyMultiplier);
            };

            foreach (var ability in identity.StartingAbilities)
                if (ability) unit.ApplyLevel(ability, seat.LevelOf(ability));
            foreach (var held in seat.Items)
                unit.Give(held.Item, held.Level, held.Count);
            return unit;
        }

        /// <summary>The ability the seat's item at an index gives, or null.</summary>
        public Ability ItemAbilityAt(int index) =>
            Seat != null && index >= 0 && index < Seat.Items.Count ? Seat.Items[index].Item?.ActiveAbility : null;

        /// <summary>Sets it on another unit, to walk into range and attack.</summary>
        public void Attack(DemoUnit other) => AttackUnit(other);
    }
}
