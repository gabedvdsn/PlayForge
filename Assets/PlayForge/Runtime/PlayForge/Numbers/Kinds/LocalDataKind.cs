using System;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Whose Local Data a Local Data number reads (FEAT-29).</summary>
    public enum ELocalDataFrom
    {
        [Tooltip("What the number belongs to: the ability's or the item's asset, or the entity's own Local Data")]
        [InspectorName("Its Own")] Own,

        [Tooltip("The caster's Local Data, as the entity holds it at runtime")]
        Caster,

        [Tooltip("The target's Local Data, as the entity holds it at runtime")]
        Target,
    }

    /// <summary>
    /// A value in Local Data, under a key (FEAT-29, NUM-8), as a hero's attack range: in what the number belongs to (the
    /// ability's or the item's asset, or an entity's own Local Data, where the number is the entity's), or in the caster's
    /// or the target's Local Data as the entity holds it at runtime. An entry that holds a float or a whole number reads
    /// as that number; where there's no number under the key, it reads its <see cref="Default"/>.
    /// </summary>
    [Serializable]
    [NumberKind("Local Data")]
    public class LocalDataKind : NumberKind
    {
        [Tooltip("The key the value is under")]
        [ForgeTagContext(ForgeContext.LocalDataKey)]
        public Tag Key;

        [Tooltip("Whose Local Data it reads: what the number belongs to, as its ability, or the caster's or the target's")]
        public ELocalDataFrom From = ELocalDataFrom.Own;

        [Tooltip("What it reads where there's no number under the key")]
        public float Default;

        /// <summary>What it needs: Local Data of its own, or the entity it reads.</summary>
        public override NumberReads Reads => From switch
        {
            ELocalDataFrom.Caster => NumberReads.Caster,
            ELocalDataFrom.Target => NumberReads.Target,
            _ => NumberReads.LocalData,
        };

        protected override float Fallback => Default;

        protected override float Evaluate(in NumberContext context)
        {
            bool found = From == ELocalDataFrom.Own
                ? context.TryGetLocalData(Key, out float value)
                : context.TryGetLocalData(From == ELocalDataFrom.Caster ? NumberSide.Caster : NumberSide.Target, Key, out value);
            return found ? value : Default;
        }

        // "Data.Range in its Local Data, else 2"
        public override string Describe()
        {
            var whose = From switch
            {
                ELocalDataFrom.Caster => "the caster's",
                ELocalDataFrom.Target => "the target's",
                _ => "its",
            };
            return $"{(Key.IsValid ? Key.ToString() : "a key")} in {whose} Local Data, else {NumberText.Number(Default)}";
        }
    }
}
