using System;
using System.Collections.Generic;
using FarEmerald.PlayForge.Extended;
using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// An attribute's part in a project (TW-29): a role, by its tag, and the attribute that plays it. PlayForge's features
    /// read the roles it ships, as death at 0 reads <see cref="Tags.ROLE_HEALTH"/>; a game's code reads its own.
    /// </summary>
    [Serializable]
    public class AttributeRole
    {
        [Tooltip("The role, by its tag: one of PlayForge's, as Health, which death at 0 reads, or one of your own")]
        [ForgeTagContext(ForgeContext.Role)]
        public Tag Role;

        [Tooltip("The project's attribute that plays it")]
        public Attribute Attribute;
    }

    /// <summary>
    /// A Forge Project's settings (TW-29, Decision 57): what its game reads as it runs, its attributes' roles and the crowd
    /// control rules an entity follows where it names none of its own. A running game finds them through its GameRoot.
    /// </summary>
    [Serializable]
    public class ForgeProjectSettings
    {
        [Tooltip("The part each attribute plays in the project, by a role tag: PlayForge's own read Health and Luck, and your " +
                 "code reads its own")]
        public List<AttributeRole> Roles = new();

        [Tooltip("The crowd control rules an entity follows where it names none of its own (TW-16)")]
        public CrowdControlRules CrowdControl;

        [Tooltip("How The Forge makes the project's assets: where each type's go and how they're named, their defaults, and " +
                 "the Cost and Cooldown templates (TW-30)")]
        public ForgeAuthoringSettings Authoring = new();

        /// <summary>The attribute playing <paramref name="role"/>, the first that does, or null where none does.</summary>
        public Attribute AttributeFor(Tag role)
        {
            if (!role.IsValid || Roles == null) return null;
            for (int i = 0; i < Roles.Count; i++)
            {
                var each = Roles[i];
                if (each != null && each.Role.Equals(role) && each.Attribute) return each.Attribute;
            }
            return null;
        }

        /// <summary>The roles <paramref name="attribute"/> plays, each once.</summary>
        public List<Tag> RolesOf(Attribute attribute)
        {
            var found = new List<Tag>();
            if (!attribute || Roles == null) return found;
            foreach (var each in Roles)
                if (each != null && each.Attribute == attribute && each.Role.IsValid && !found.Contains(each.Role)) found.Add(each.Role);
            return found;
        }
    }

    /// <summary>
    /// The attributes playing roles in the current project (TW-29): PlayForge's features read the roles it ships, as
    /// <see cref="Tags.ROLE_HEALTH"/>, and a game's code reads its own by their tags.
    /// </summary>
    public static class ForgeRoles
    {
        /// <summary>The attribute playing <paramref name="role"/> in the current project, or null.</summary>
        public static Attribute AttributeFor(Tag role) =>
            role.IsValid && ForgeProject.Current is { } project ? project.Settings?.AttributeFor(role) : null;
    }
}
