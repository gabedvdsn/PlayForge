using UnityEditor;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // ═══════════════════════════════════════════════════════════════════════════════
    // The tag requirements, as the Effect mockup's table (ED-37)
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// An effect's tag requirements on one side, the target's or the source's, as the Effect mockup's table: Applying,
    /// While On and Removed By, each with what it requires and what it must not have (ED-37). The drawer drew a tinted box
    /// with colored subsections, a type badge, a Name and import buttons of its own, and rebuilt itself as it opened.
    /// </summary>
    [CustomPropertyDrawer(typeof(EffectTagRequirements))]
    public class EffectTagRequirementsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var table = new ForgeRequirementsTable(property, "When",
                new ForgeRequirementsTable.Group("Applying", nameof(EffectTagRequirements.ApplicationRequirements),
                    "Must pass for the effect to be applied"),
                new ForgeRequirementsTable.Group("While on", nameof(EffectTagRequirements.OngoingRequirements),
                    "While it fails, the effect stays, but its ticks don't execute"),
                new ForgeRequirementsTable.Group("Removed by", nameof(EffectTagRequirements.RemovalRequirements),
                    "When it's met, the effect is removed"));
            // The style sheet lays out the table, wherever the drawer is drawn (ED-20)
            ForgeTheme.Apply(table);
            return table;
        }
    }

    /// <summary>
    /// An ability's tag requirements, as the same table: the caster's, the caster's while the ability runs (FEAT-5), and
    /// the target's, each with what it requires and what it must not have (ED-37).
    /// </summary>
    [CustomPropertyDrawer(typeof(AbilityTagRequirements))]
    public class AbilityTagRequirementsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var table = new ForgeRequirementsTable(property, "Who",
                new ForgeRequirementsTable.Group("Caster", nameof(AbilityTagRequirements.SourceRequirements),
                    "Checked as the ability is asked to activate, and again after targeting"),
                new ForgeRequirementsTable.Group("While active", nameof(AbilityTagRequirements.WhileActiveRequirements),
                    "The caster's, for as long as the ability runs: when it fails, the ability is interrupted"),
                new ForgeRequirementsTable.Group("Target", nameof(AbilityTagRequirements.TargetRequirements),
                    "Checked after targeting, when there's a target"));
            ForgeTheme.Apply(table);
            return table;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════════
    // A lone requirement group
    // ═══════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// A requirement group of its own, as a targeting task's or a template's: the same table, of one row named by the
    /// field, with what it requires and what it must not have; a cell opens its list, with the group's name and template
    /// under it (ED-66). It drew a tinted box with green and red stripes, colored count badges, a Name and an Import of
    /// its own, and rebuilt itself as it opened.
    /// </summary>
    [CustomPropertyDrawer(typeof(AvoidRequireTagGroup))]
    public class AvoidRequireTagGroupDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var table = new ForgeRequirementsTable(property, "",
                new ForgeRequirementsTable.Group(preferredLabel ?? property.displayName, null,
                    "Must pass: the tags it requires, and those it must not have"));
            ForgeTheme.Apply(table);
            return table;
        }
    }
}
