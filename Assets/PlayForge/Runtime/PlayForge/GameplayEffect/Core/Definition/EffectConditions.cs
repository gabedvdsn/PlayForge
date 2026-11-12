using System;
using System.Collections.Generic;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An effect's conditions, beside the tags of each phase of its requirements (TW-19, Decision 58): those that must hold
    /// for it to land, those that keep it going, and those that take it off, as Attribute Shift stops at its floor. A phase
    /// passes only when its tags pass and its conditions all hold; the Ongoing and Removal ones are checked at each frame's
    /// end while it lasts, as no tag needs to change for an attribute a condition reads to.
    /// </summary>
    [Serializable]
    public class EffectConditions
    {
        [Tooltip("Each must hold for it to land, beside its tags to apply")]
        [SerializeReference] public List<GameplayCondition> Application = new();

        [Tooltip("Each must hold for it to go on: while one fails, it pauses, as while its Ongoing tags fail")]
        [SerializeReference] public List<GameplayCondition> Ongoing = new();

        [Tooltip("Once each holds, with its Removal tags where it names any, it comes off")]
        [SerializeReference] public List<GameplayCondition> Removal = new();

        /// <summary>Whether it has Ongoing or Removal conditions, which are checked as it lasts.</summary>
        public bool Watched => Has(Ongoing) || Has(Removal);

        /// <summary>Whether a list names any condition; an empty slot, which a list's "+" leaves, names none.</summary>
        public static bool Has(List<GameplayCondition> conditions)
        {
            if (conditions == null) return false;
            for (int i = 0; i < conditions.Count; i++)
                if (conditions[i] != null) return true;
            return false;
        }

        /// <summary>Whether each condition of a list holds where it's read; an empty list holds.</summary>
        public static bool AllHold(List<GameplayCondition> conditions, in NumberContext context)
        {
            if (conditions == null) return true;
            for (int i = 0; i < conditions.Count; i++)
                if (conditions[i] != null && !conditions[i].Holds(in context)) return false;
            return true;
        }
    }
}
