using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge.Lab;
using UnityEngine;

namespace FarEmerald.PlayForge.Samples.Survivors
{
    /// <summary>What a draft's cards offer.</summary>
    public enum ESurvivorsDraftKind
    {
        /// <summary>Abilities, as a level-up offers: new ones, or a level more of those held.</summary>
        Ability,

        /// <summary>Passives, as a boss wave's end offers: new ones, or a level more of those held.</summary>
        Passive,
    }

    /// <summary>One card: the ability or passive it offers, and whether it's new or which level it brings.</summary>
    public sealed class SurvivorsCard
    {
        public Ability Ability;
        public Item Passive;
        public bool IsNew;
        public int From;
        public int To;

        public string Name => Ability ? Ability.GetName() : Passive ? Passive.GetName() : "";
        public string Description => Ability ? Ability.GetDescription() : Passive ? Passive.GetDescription() : "";
        public Texture2D Icon => Ability ? Ability.GetDefaultIcon() : Passive ? Passive.GetDefaultIcon() : null;
    }

    /// <summary>
    /// The cards up: drawn at random from the roster's pool, each a new ability or passive while the hero holds fewer than
    /// it may, or a level more of one it holds below its most. Taking one teaches the ability, or raises its level on the
    /// hero's GAS; a passive is an item, given equipped, whose effects grow with its level.
    /// </summary>
    public sealed class SurvivorsDraft
    {
        public ESurvivorsDraftKind Kind { get; private set; }
        public List<SurvivorsCard> Cards { get; } = new();

        /// <summary>A draft of a kind for the game's hero, or null where nothing's left to offer.</summary>
        public static SurvivorsDraft For(ESurvivorsDraftKind kind, SurvivorsGame game)
        {
            var hero = game.Hero;
            var roster = game.Roster;
            if (!hero || !roster) return null;
            var offers = new List<SurvivorsCard>();
            if (kind == ESurvivorsDraftKind.Ability)
            {
                var held = hero.Known();
                foreach (var ability in roster.AbilityPool)
                {
                    if (!ability) continue;
                    if (held.Contains(ability))
                    {
                        int level = hero.LevelOf(ability);
                        if (level < ability.MaxLevel) offers.Add(new SurvivorsCard { Ability = ability, From = level, To = level + 1 });
                    }
                    else if (held.Count(each => roster.AbilityPool.Contains(each)) < roster.MaxAbilities)
                        offers.Add(new SurvivorsCard { Ability = ability, IsNew = true, To = 1 });
                }
            }
            else
            {
                var held = hero.Items();
                foreach (var passive in roster.PassivePool)
                {
                    if (!passive) continue;
                    if (held.Contains(passive))
                    {
                        int level = hero.LevelOf(passive);
                        if (level < passive.MaxLevel) offers.Add(new SurvivorsCard { Passive = passive, From = level, To = level + 1 });
                    }
                    else if (held.Count(each => roster.PassivePool.Contains(each)) < roster.MaxPassives)
                        offers.Add(new SurvivorsCard { Passive = passive, IsNew = true, To = 1 });
                }
            }
            if (offers.Count == 0) return null;
            var draft = new SurvivorsDraft { Kind = kind };
            int count = kind == ESurvivorsDraftKind.Ability ? roster.AbilityCards : roster.PassiveCards;
            while (draft.Cards.Count < count && offers.Count > 0)
            {
                int pick = game.Random.Next(offers.Count);
                draft.Cards.Add(offers[pick]);
                offers.RemoveAt(pick);
            }
            return draft;
        }

        /// <summary>Takes a card: its ability learned or a level up, its passive given or a level up.</summary>
        public void Apply(int index, SurvivorsGame game)
        {
            var hero = game.Hero;
            if (!hero || index < 0 || index >= Cards.Count) return;
            var card = Cards[index];
            if (card.Ability)
            {
                if (card.IsNew) hero.Teach(card.Ability, 1);
                else hero.ApplyLevel(card.Ability, card.To);
                return;
            }
            if (!card.Passive) return;
            // Given again at its new level, so its effects apply afresh at it
            if (!card.IsNew) hero.Take(card.Passive);
            hero.Give(card.Passive, card.To);
            hero.Gas.SetLevel(card.Passive.GetAssetTag(), new IntValuePair(card.To, Mathf.Max(card.To, card.Passive.MaxLevel)));
        }
    }
}
