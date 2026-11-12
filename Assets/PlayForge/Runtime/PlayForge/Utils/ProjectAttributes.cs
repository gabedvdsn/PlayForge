using UnityEngine;

namespace FarEmerald.PlayForge
{
    /// <summary>
    /// The attributes PlayForge reads by their part in the game, one set for the project (TW-28, Decision 49), edited under
    /// Project Settings → PlayForge and kept in a Resources folder, so a build has it too. Each slot is optional, and an
    /// empty one leaves things as they were; an asset's own setting always wins over the project's.
    /// </summary>
    public sealed class ProjectAttributes : ScriptableObject
    {
        /// <summary>The asset's name in a Resources folder, where <see cref="ForgeAttributes"/> finds it.</summary>
        public const string ResourceName = "PlayForge Project Attributes";

        [Tooltip("What an entity dies of as it reaches 0, where no death watcher of its own watches for its death and its " +
                 "Death doesn't say otherwise; and what On Hit counts as a hit where an entry names no attribute")]
        public Attribute Health;

        [Tooltip("What an ability's Mana Cost spends, where it has no Cost effect")]
        public Attribute Mana;

        [Tooltip("Read on the caster of every chance that gives no luck of its own: above 0 a roll is made that many more " +
                 "times and the best kept, below 0 the worst")]
        public Attribute Luck;

        [Tooltip("What the Creator Lab's units walk at, in units a second, where its rules name none")]
        public Attribute MovementSpeed;

        [Header("Crowd Control")]
        [Tooltip("The crowd control rules an entity follows where it names none of its own (TW-16)")]
        public CrowdControlRules CrowdControl;
    }

    /// <summary>
    /// The project's attributes (TW-28): its Health, Mana, Luck and Movement Speed, each null where the project sets none.
    /// They're read from the <see cref="ProjectAttributes"/> asset in a Resources folder, or the one a test uses in its
    /// place.
    /// </summary>
    public static class ForgeAttributes
    {
        private static ProjectAttributes used;
        private static ProjectAttributes loaded;
        private static bool looked;

        // Statics outlive play mode when domain reload is off
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Clear()
        {
            used = null;
            Refresh();
        }

        /// <summary>The project's attributes: the asset a test uses, or else the project's own, or null for none.</summary>
        public static ProjectAttributes Project
        {
            get
            {
                if (used) return used;

                // Looked for once, and again where the asset found has since gone
                if (!looked || (loaded is not null && !loaded))
                {
                    loaded = Resources.Load<ProjectAttributes>(ProjectAttributes.ResourceName);
                    looked = true;
                }
                return loaded ? loaded : null;
            }
        }

        /// <summary>What an entity dies of at 0, and On Hit's attribute where an entry names none; or null.</summary>
        public static Attribute Health => Project is { } project && project.Health ? project.Health : null;

        /// <summary>What an ability's Mana Cost spends; or null.</summary>
        public static Attribute Mana => Project is { } project && project.Mana ? project.Mana : null;

        /// <summary>Read on the caster of every chance that gives no luck of its own; or null.</summary>
        public static Attribute Luck => Project is { } project && project.Luck ? project.Luck : null;

        /// <summary>What the Creator Lab's units walk at where its rules name none; or null.</summary>
        public static Attribute MovementSpeed => Project is { } project && project.MovementSpeed ? project.MovementSpeed : null;

        /// <summary>The crowd control rules an entity follows where it names none of its own; or null (TW-16).</summary>
        public static CrowdControlRules CrowdControl => Project is { } project && project.CrowdControl ? project.CrowdControl : null;

        /// <summary>
        /// Uses <paramref name="attributes"/> in place of the project's own, as a test does, or the project's own again for
        /// null. Gives back the one used before, to put back.
        /// </summary>
        public static ProjectAttributes Use(ProjectAttributes attributes)
        {
            var was = used;
            used = attributes;
            return was;
        }

        /// <summary>Looks for the project's asset again, as after it's made or moved in the editor.</summary>
        public static void Refresh()
        {
            loaded = null;
            looked = false;
        }
    }
}
