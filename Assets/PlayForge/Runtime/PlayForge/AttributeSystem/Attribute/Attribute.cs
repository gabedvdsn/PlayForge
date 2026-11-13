using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Serialization;

namespace FarEmerald.PlayForge
{
    [CreateAssetMenu(menuName = "PlayForge/Attribute", fileName = "Attribute_", order = 23)]
    public class Attribute : BaseForgeAsset, IAttribute
    {
        public string Name;
        public string Abbreviation;
        public string Description;
        public List<TextureItem> Textures;

        [Tooltip("What the attribute is: how bonuses to it add up, and how a number reads it, unless an effect or the " +
                 "number says otherwise (TW-1)")]
        public EAttributeRole Role = EAttributeRole.Value;

        [Tooltip("As a Barrier: the attribute it absorbs for, as Health. Every workable loss to that one comes off this " +
                 "first, as much as it holds (TW-12)")]
        public Attribute AbsorbsFor;

        [Tooltip("As a Barrier: how much it loses each second, by its holder's time, as a barrier fades; 0, none (TW-12)")]
        [Min(0f)] public float DecayPerSecond;

        [Tooltip("As a Barrier: reads Decay Per Second as a share of its base, so a base of 100 at 0.1 loses 10 a second")]
        public bool DecayAsShareOfBase;

        [Tooltip("As a Barrier: what a lasting effect gives it fades to 0 by the time that effect ends (TW-12)")]
        public bool FadesWithEffect;

        [Tooltip("Each workable gain to its current is multiplied by this attribute's value on the same entity, as Health's by " +
                 "Healing Received: 0 blocks healing outright. None, none (TW-15)")]
        public Attribute GainsScaledBy;
        
        #region Internal
        
        public override string GetName()
        {
            return Name;
        }
        public override IEnumerable<Tag> GetGrantedTags()
        {
            yield break;
        }
        public string GetAbbreviation()
        {
            return Abbreviation;
        }
        public List<TextureItem> GetTextures()
        {
            return Textures;
        }
        public override string GetDescription()
        {
            return Description;
        }
        public override Texture2D GetDefaultIcon()
        {
            return ForgeHelper.GetTextureItem(Textures, PlayForge.Tags.PRIMARY);
        }

        public bool Equals(Attribute other)
        {
            return other == this;
        }

        public bool Equals(IAttribute other)
        {
            return GetName() == other.GetName();
        }
        
        public override bool Equals(object obj) => obj is IAttribute other && Equals(other);

        public override int GetHashCode() => base.GetHashCode();

        #endregion
    }

    /// <summary>
    /// What an attribute is, which sets how bonuses to it add up and how a number reads it by default (TW-1, Decision 46):
    /// the user's question of 2026-11-11, how several effects' Damage Amplification adds up and how an attack's scaler
    /// reads it. An effect's impact can add up its own way, and a number can read it its own way.
    /// </summary>
    public enum EAttributeRole
    {
        /// <summary>A value, as Health or Strength: bonuses add and multiply as each effect says; a number reads it as it is.</summary>
        Value,

        /// <summary>
        /// A share more, as Damage Amplification at 0.2 for 20%: bonuses add, 0.2 and 0.3 making 0.5; a number reads it as a
        /// multiplier, 1 + x, 1.5.
        /// </summary>
        BonusShare,

        /// <summary>A factor, as Damage Dealt at 1: bonuses add and multiply as each effect says; a number reads it as it is.</summary>
        Multiplier,

        /// <summary>
        /// A share taken off, or a chance, as Damage Resistance at 0.3: bonuses stack with diminishing returns, each on what's
        /// left, 1 − (1 − x)(1 − v), so two of 30% make 51% and none takes it past 1; a number reads what's left, 1 − x.
        /// </summary>
        Resistance,

        /// <summary>
        /// A barrier, as a shield before Health: it absorbs for the attribute it names, each workable loss to that one taken
        /// from it first, and it can decay, by a rate or with the effect that gave it (TW-12).
        /// </summary>
        Barrier,
    }
}
