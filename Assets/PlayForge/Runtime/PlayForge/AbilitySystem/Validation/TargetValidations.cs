using System;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>Which way a Facing check looks.</summary>
    public enum EFacingCheck
    {
        /// <summary>The target lies within the angle in front of its caster.</summary>
        InFrontOfCaster,

        /// <summary>The caster stands within the angle behind its target, as a backstab wants.</summary>
        BehindTarget
    }

    /// <summary>
    /// Affiliation: the target stands to its caster as it says, by the sides they hold, as an area's or an aura's filter
    /// takes them: an enemy shares none of the caster's sides, an ally shares one. A caster with no sides takes anyone as
    /// either.
    /// </summary>
    [Serializable]
    [ValidationSide(EValidationSide.Target)]
    [ForgeMenu(Name = "Affiliation", Description = "Activates only on a target that stands to its caster as it says: an enemy, an ally, itself, or anyone else.")]
    public class AffiliationValidation : IAbilityValidationRule
    {
        [Tooltip("How the target must stand to its caster: an enemy shares none of its sides, an ally shares one, and the " +
                 "caster is its own ally")]
        public EAffiliationFilter Target = EAffiliationFilter.Enemies;

        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            var target = getSource?.Invoke();
            var caster = data?.EffectOrigin?.GetSource();
            if (target == null || caster == null)
            {
                error = target == null ? ValidationText.NoOne : ValidationText.NoActivation;
                return false;
            }

            bool passes = ForgeEntities.Takes(Target, caster, caster.GetAffiliation(), target);
            error = passes ? null : $"{ValidationText.NameOf(target)} isn't {Wanted()}";
            return passes;
        }

        // What the target had to be, as a refusal says it
        private string Wanted() => Target switch
        {
            EAffiliationFilter.Enemies => "an enemy of its caster",
            EAffiliationFilter.Allies => "an ally of its caster",
            EAffiliationFilter.OtherAllies => "another ally of its caster",
            EAffiliationFilter.Self => "its caster itself",
            EAffiliationFilter.Everyone => "anyone",
            _ => "anyone but its caster"
        };

        public string GetName() => "Affiliation";
    }

    /// <summary>
    /// Line of Sight: nothing on the layers it names blocks a line from the caster to its target, run at a height above
    /// each, so the ground under them doesn't. The two ends' own colliders don't block it. The line runs between their
    /// targeting packets, so it's drawn for an entity of either hub, and 3D physics colliders block it.
    /// </summary>
    [Serializable]
    [ValidationSide(EValidationSide.Target)]
    [ForgeMenu(Name = "Line of Sight", Description = "Activates only where nothing on the layers given blocks a line from the caster to its target; 3D physics colliders block it.")]
    public class LineOfSightValidation : IAbilityValidationRule
    {
        [Tooltip("The layers whose colliders block the line")]
        public LayerMask Blocking = ~0;

        [Tooltip("How high above each end the line runs, so the ground under them doesn't block it")]
        [Min(0f)] public float Height = 1f;

        // What the line hits, kept between checks so that a check allocates nothing. A rule is asked on the main thread
        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            var target = getSource?.Invoke();
            var from = data?.EffectOrigin?.GetSource()?.GetTargetingPacket();
            var to = target?.GetTargetingPacket();
            if (from == null || to == null)
            {
                error = target == null ? ValidationText.NoOne : ValidationText.NoActivation;
                return false;
            }

            var start = from.position + Vector3.up * Height;
            var line = to.position + Vector3.up * Height - start;
            float length = line.magnitude;
            error = null;
            if (length < 0.0001f) return true;

            int count = Physics.RaycastNonAlloc(start, line / length, Hits, length, Blocking, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = Hits[i].transform;
                if (hit == null || Owns(from, hit) || Owns(to, hit)) continue;
                error = $"{hit.name} blocks the line to {ValidationText.NameOf(target)}";
                return false;
            }
            return true;
        }

        // Whether a hit is an end's own: its object or one under it
        private static bool Owns(AbstractTargetingPacket end, Transform hit) => end.transform != null && hit.IsChildOf(end.transform);

        public string GetName() => "Line of Sight";
    }

    /// <summary>
    /// Facing: the target lies within an angle in front of its caster, or the caster within an angle behind its target, as
    /// a backstab wants; each facing its targeting packet's, along the ground. An angle of 360 takes anywhere.
    /// </summary>
    [Serializable]
    [ValidationSide(EValidationSide.Target)]
    [ForgeMenu(Name = "Facing", Description = "Activates only where its target lies within an angle in front of its caster, or its caster within an angle behind its target.")]
    public class FacingValidation : IAbilityValidationRule
    {
        [Tooltip("In front of the caster, or behind the target, as a backstab")]
        public EFacingCheck Facing = EFacingCheck.InFrontOfCaster;

        [Tooltip("The angle's width in degrees, centred on the facing: 90 takes 45 to either side")]
        [Range(0f, 360f)] public float Angle = 90f;

        public bool Validate(AbilityDataPacket data, Func<ITarget> getSource, out string error)
        {
            var target = getSource?.Invoke();
            var from = data?.EffectOrigin?.GetSource()?.GetTargetingPacket();
            var to = target?.GetTargetingPacket();
            if (from == null || to == null)
            {
                error = target == null ? ValidationText.NoOne : ValidationText.NoActivation;
                return false;
            }

            error = null;
            if (Angle >= 360f) return true;
            bool behind = Facing == EFacingCheck.BehindTarget;
            // The way the facing points, and the way from its owner to the other, along the ground
            var facing = behind ? -(to.rotation * Vector3.forward) : from.rotation * Vector3.forward;
            var toward = behind ? from.position - to.position : to.position - from.position;
            facing.y = 0f;
            toward.y = 0f;
            if (facing.sqrMagnitude < 0.0001f || toward.sqrMagnitude < 0.0001f) return true;

            float off = Vector3.Angle(facing, toward);
            if (off <= Angle * 0.5f + 0.01f) return true;
            error = behind
                ? $"Its caster stands {off:0}° off {ValidationText.NameOf(target)}'s back, more than {Angle * 0.5f:0}°"
                : $"{ValidationText.NameOf(target)} stands {off:0}° off its caster's facing, more than {Angle * 0.5f:0}°";
            return false;
        }

        public string GetName() => "Facing";
    }
}
