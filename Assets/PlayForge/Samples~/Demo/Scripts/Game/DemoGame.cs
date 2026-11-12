using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Demo
{
    /// <summary>What the opponent is.</summary>
    public enum DemoOpponent
    {
        [InspectorName("An AI Hero")] AIHero,
        [InspectorName("The Training Dummy")] TrainingDummy,
    }

    /// <summary>How hard the AI hero hits, which it hands its abilities by Set By Caller.</summary>
    public enum DemoDifficulty
    {
        Easy,
        Normal,
        Hard,
    }

    /// <summary>An item a seat holds, at its level.</summary>
    [Serializable]
    public sealed class DemoSeatItem
    {
        public Item Item;
        public int Level = 1;
        [Tooltip("How many it holds, for an item that stacks")]
        public int Count = 1;
    }

    /// <summary>
    /// A side of the fight as the panel sets it: which hero, at what level, with which abilities' levels and which items.
    /// It outlives the unit standing for it, which dies and comes back as the seat says.
    /// </summary>
    public sealed class DemoSeat
    {
        public readonly bool IsPlayer;
        public int HeroIndex;
        public bool IsDummy;
        public int Level = 1;
        public readonly Dictionary<Ability, int> AbilityLevels = new();
        public readonly List<DemoSeatItem> Items = new();

        /// <summary>The unit standing for it now, or null while it's down.</summary>
        public DemoUnit Unit;

        /// <summary>When its unit went down, or below 0 while it stands.</summary>
        public float DownAt = -1f;

        public DemoSeat(bool isPlayer, int heroIndex)
        {
            IsPlayer = isPlayer;
            HeroIndex = heroIndex;
        }

        public int LevelOf(Ability ability) => ability != null && AbilityLevels.TryGetValue(ability, out var level) ? level : ability?.StartingLevel ?? 1;
    }

    /// <summary>
    /// The demo's arena: the player's hero and its opponent, an AI hero or the training dummy. It puts each in, gives and
    /// takes items, sets levels, and brings a hero back where it started after it dies. The panel drives it; the HUD
    /// reads it.
    /// </summary>
    public class DemoGame : MonoBehaviour
    {
        public static DemoGame Current { get; private set; }

        public DemoRoster Roster;
        public Transform PlayerStart;
        public Transform OpponentStart;
        [Tooltip("Seconds a hero stays down before it comes back")]
        public float RespawnSeconds = 5f;
        [Tooltip("The material a unit's body takes, tinted by its hero's color; with none, the render pipeline's default")]
        public Material BodyMaterial;

        public DemoSeat PlayerSeat { get; private set; }
        public DemoSeat OpponentSeat { get; private set; }
        public DemoUnit Player => PlayerSeat?.Unit;
        public DemoUnit Opponent => OpponentSeat?.Unit;
        public DemoDifficulty Difficulty { get; private set; } = DemoDifficulty.Normal;
        public DemoOpponent OpponentKind => OpponentSeat != null && OpponentSeat.IsDummy ? DemoOpponent.TrainingDummy : DemoOpponent.AIHero;

        /// <summary>Raised as who fights, a level or an item changes, for the panel to redraw.</summary>
        public event Action Changed;

        private void Awake()
        {
            Current = this;
            PlayerSeat = new DemoSeat(true, 0);
            OpponentSeat = new DemoSeat(false, Roster != null && Roster.Heroes.Count > 1 ? 1 : 0);
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        private void Start()
        {
            Spawn(PlayerSeat);
            Spawn(OpponentSeat);
        }

        private void Update()
        {
            Watch(PlayerSeat);
            Watch(OpponentSeat);
        }

        // A unit down comes back after a while, as its seat says
        private void Watch(DemoSeat seat)
        {
            if (seat == null) return;
            if (seat.Unit && seat.Unit.Alive) return;
            if (seat.DownAt < 0f)
            {
                seat.DownAt = Time.time;
                Changed?.Invoke();
                return;
            }
            if (Time.time - seat.DownAt >= RespawnSeconds) Spawn(seat);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // What the panel changes
        // ═══════════════════════════════════════════════════════════════════════════

        /// <summary>The hero a seat plays. Its abilities' levels go back to their start; its items stay.</summary>
        public void SetHero(DemoSeat seat, int heroIndex)
        {
            if (seat == null || heroIndex < 0 || heroIndex >= Roster.Heroes.Count) return;
            seat.HeroIndex = heroIndex;
            seat.IsDummy = false;
            seat.AbilityLevels.Clear();
            Spawn(seat);
        }

        /// <summary>What the opponent is: an AI hero, or the training dummy.</summary>
        public void SetOpponent(DemoOpponent kind)
        {
            OpponentSeat.IsDummy = kind == DemoOpponent.TrainingDummy;
            Spawn(OpponentSeat);
        }

        public void SetDifficulty(DemoDifficulty difficulty)
        {
            Difficulty = difficulty;
            Changed?.Invoke();
        }

        /// <summary>A seat's level. Its unit comes back at it, as a hero's attributes are worked out from its level.</summary>
        public void SetLevel(DemoSeat seat, int level)
        {
            var identity = IdentityOf(seat);
            if (identity == null) return;
            seat.Level = Mathf.Clamp(level, 1, Mathf.Max(1, identity.MaxLevel));
            Spawn(seat);
        }

        /// <summary>An ability's level on a seat, which its unit takes at once.</summary>
        public void SetAbilityLevel(DemoSeat seat, Ability ability, int level)
        {
            if (seat == null || ability == null) return;
            seat.AbilityLevels[ability] = Mathf.Clamp(level, 1, Mathf.Max(1, ability.MaxLevel));
            if (seat.Unit) seat.Unit.ApplyLevel(ability, seat.AbilityLevels[ability]);
            Changed?.Invoke();
        }

        /// <summary>An item's level on a seat. Its unit takes the item again, at the level.</summary>
        public void SetItemLevel(DemoSeat seat, Item item, int level)
        {
            var held = seat?.Items.Find(each => each.Item == item);
            if (held == null) return;
            held.Level = Mathf.Clamp(level, 1, Mathf.Max(1, item.MaxLevel));
            if (seat.Unit)
            {
                seat.Unit.Take(item);
                seat.Unit.Give(item, held.Level, held.Count);
            }
            Changed?.Invoke();
        }

        /// <summary>Gives a seat an item, or one more of one that stacks, up to its most.</summary>
        public void Give(DemoSeat seat, Item item)
        {
            if (seat == null || item == null) return;
            var held = seat.Items.Find(each => each.Item == item);
            if (held == null)
            {
                held = new DemoSeatItem { Item = item, Level = Mathf.Max(1, item.StartingLevel) };
                seat.Items.Add(held);
                if (seat.Unit) seat.Unit.Give(item, held.Level);
            }
            else if (held.Count < Mathf.Max(1, item.Definition.MaxStackSize))
            {
                held.Count++;
                if (seat.Unit) seat.Unit.Give(item, held.Level);
            }
            Changed?.Invoke();
        }

        /// <summary>A unit used up one of an item, as the Healing Salve's use does; its seat holds one less.</summary>
        public void Consumed(DemoUnit unit, Item item)
        {
            var seat = unit != null ? unit.Seat : null;
            var held = seat?.Items.Find(each => each.Item == item);
            if (held == null) return;
            held.Count--;
            if (held.Count <= 0) seat.Items.Remove(held);
            Changed?.Invoke();
        }

        /// <summary>Takes an item from a seat, all of it.</summary>
        public void Take(DemoSeat seat, Item item)
        {
            if (seat == null || item == null) return;
            seat.Items.RemoveAll(each => each.Item == item);
            if (seat.Unit) seat.Unit.Take(item);
            Changed?.Invoke();
        }

        /// <summary>Both sides back where they started, at full health.</summary>
        public void ResetFight()
        {
            Spawn(PlayerSeat);
            Spawn(OpponentSeat);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Units
        // ═══════════════════════════════════════════════════════════════════════════

        public EntityIdentity IdentityOf(DemoSeat seat)
        {
            if (seat == null || Roster == null) return null;
            if (seat.IsDummy) return Roster.TrainingDummy;
            return seat.HeroIndex >= 0 && seat.HeroIndex < Roster.Heroes.Count ? Roster.Heroes[seat.HeroIndex].Identity : null;
        }

        public Color ColorOf(DemoSeat seat)
        {
            if (seat == null || Roster == null) return Color.white;
            if (seat.IsDummy) return Roster.DummyColor;
            return seat.HeroIndex >= 0 && seat.HeroIndex < Roster.Heroes.Count ? Roster.Heroes[seat.HeroIndex].Color : Color.white;
        }

        /// <summary>Puts the seat's unit in at its start, in place of the one standing for it.</summary>
        public void Spawn(DemoSeat seat)
        {
            var identity = IdentityOf(seat);
            if (identity == null) return;
            if (seat.Unit) seat.Unit.Remove();

            var start = seat.IsPlayer ? PlayerStart : OpponentStart;
            var at = start ? start.position : Vector3.zero;
            var facing = start ? start.rotation : Quaternion.identity;
            seat.Unit = DemoUnit.Create(this, seat, identity, ColorOf(seat), at, facing);
            seat.DownAt = -1f;
            if (!seat.IsPlayer)
            {
                if (seat.IsDummy) seat.Unit.gameObject.AddComponent<DemoDummy>().Mend = Roster.DummyMend;
                else seat.Unit.gameObject.AddComponent<DemoAI>();
            }
            Changed?.Invoke();
        }

        /// <summary>The side a seat fights for, as the registration's affiliation.</summary>
        public Tag SideOf(DemoSeat seat) => seat.IsPlayer ? Roster.PlayerSide : Roster.OpponentSide;

        /// <summary>The other side's unit.</summary>
        public DemoUnit FoeOf(DemoUnit unit)
        {
            if (unit == null) return null;
            return unit.Seat == PlayerSeat ? Opponent : Player;
        }

        /// <summary>The multiplier the AI's difficulty gives its damage.</summary>
        public float DifficultyMultiplier => Difficulty switch
        {
            DemoDifficulty.Easy => 0.6f,
            DemoDifficulty.Hard => 1.4f,
            _ => 1f,
        };

        internal void Announce() => Changed?.Invoke();
    }
}
