using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(Attribute)), CanEditMultipleObjects]
    public class AttributeEditor : BasePlayForgeEditor
    {
        [SerializeField] private Texture2D m_AttributeIcon;

        private Attribute attribute;

        // Every field writes through the SerializedObject, so the inspector edits a multiple selection (ED-12)
        protected override bool SupportsMultipleObjects => true;

        // ═══════════════════════════════════════════════════════════════════════════
        // Header Configuration (IMGUI Header)
        // ═══════════════════════════════════════════════════════════════════════════

        protected override BaseForgeLevelProvider GetAsset()
        {
            return null;
        }
        protected override string GetDisplayName()
        {
            string _name = !string.IsNullOrEmpty(attribute.GetName()) ? attribute.Name : "Unnamed Attribute";
            if (!string.IsNullOrEmpty(attribute.Abbreviation)) _name += $" ({attribute.Abbreviation})";
            return _name;
        }
        
        protected override string GetDisplayDescription()
        {
            if (attribute == null) return "";
            var desc = attribute.Description;
            return !string.IsNullOrEmpty(desc) ? Truncate(desc, 80) : "No description provided.";
        }
        
        protected override Texture2D GetHeaderIcon()
        {
            if (attribute.Textures != null && attribute.Textures.Count > 0)
            {
                var tex = attribute.Textures[0].Texture;
                if (tex != null) return tex;
            }
            return m_AttributeIcon;
        }
        
        protected override string GetAssetTypeLabel() => "ATTRIBUTE";
        
        protected override Color GetAssetTypeColor() => Colors.GetAssetColor(typeof(Attribute));
        
        protected override string GetDocumentationTopic() => "attributes";
        
        protected override bool ShowImportButton => true;
        protected override void RebuildLevelSourceContent()
        {
            
        }
        protected override void RebuildLevelingContent()
        {
            
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Inspector GUI
        // ═══════════════════════════════════════════════════════════════════════════

        protected override bool AssignLocalAsset()
        {
            attribute = serializedObject.targetObject as Attribute;
            return attribute is not null;
        }
        
        private SectionResult identitySection;
        private SectionResult localDataSection;
        private SectionResult usedBySection;
        private ForgeUsedBy usedBy;

        protected override void BuildInspectorContent(VisualElement parent)
        {
            // The style doc's order: Identity first, Local Data last among what the asset holds (Q9). Used By then
            // shows what refers to the attribute (ED-15)
            BuildIdentitySection(parent);
            BuildLocalDataSection(parent);
            BuildUsedBySection(parent);
            UpdateSummaries();
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Identity Section
        // ═══════════════════════════════════════════════════════════════════════════

        private void BuildIdentitySection(VisualElement parent)
        {
            identitySection = CreateSection(new SectionConfig
            {
                Name = "Identity",
                Title = "Identity",
                HelpTopic = "attributes/definition",

                SerializedObject = serializedObject,
                PropertyPaths = new []{nameof(Attribute.Name), nameof(Attribute.Abbreviation), nameof(Attribute.Description), nameof(Attribute.Textures)},
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    MarkDirty(attribute);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    MarkDirty(attribute);
                    Repaint();
                },
                GetDefaultValue = path =>
                {
                    return path switch
                    {
                        nameof(Attribute.Name) => $"Unnamed Attribute",
                        nameof(Attribute.Abbreviation) => string.Empty,
                        nameof(Attribute.Description) => string.Empty,
                        nameof(Attribute.Textures) => null,
                        _ => null
                    };
                }
            });
            parent.Add(identitySection.Section);

            var content = identitySection.Content;

            // Name, Abbreviation and Description bind to their properties. They wrote straight into the asset when they
            // lost focus, with no Undo; now an edit goes through the SerializedObject, so Undo takes it back, it reaches
            // every selected attribute, and the header follows it (ED-12).
            content.Add(CreateBoundTextField(nameof(Attribute.Name), "Name"));
            content.Add(CreateBoundTextField(nameof(Attribute.Abbreviation), "Abbreviation"));
            content.Add(CreateBoundTextField(nameof(Attribute.Description), "Description", multiline: true, minHeight: 18));

            // What it is, which says how its bonuses add up and how a number reads it (TW-1)
            var role = CreatePropertyField(serializedObject.FindProperty(nameof(Attribute.Role)), "Role", "Role");
            content.Add(role);

            // As a Barrier: what it absorbs for, how it decays, and whether it fades with its effect (TW-12)
            var barrier = new VisualElement { name = "BarrierSettings" };
            barrier.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Attribute.AbsorbsFor)), "AbsorbsFor", "Absorbs For"));
            barrier.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Attribute.DecayPerSecond)), "DecayPerSecond", "Decay Per Second"));
            barrier.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Attribute.DecayAsShareOfBase)), "DecayAsShareOfBase", "Decay As Share Of Base"));
            barrier.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Attribute.FadesWithEffect)), "FadesWithEffect", "Fades With Effect"));
            content.Add(barrier);
            void ShowBarrier()
            {
                var roleProperty = serializedObject.FindProperty(nameof(Attribute.Role));
                bool shows = roleProperty != null && !roleProperty.hasMultipleDifferentValues && roleProperty.enumValueIndex == (int)EAttributeRole.Barrier;
                barrier.style.display = shows ? DisplayStyle.Flex : DisplayStyle.None;
            }
            ShowBarrier();
            role.RegisterCallback<SerializedPropertyChangeEvent>(_ => ShowBarrier());

            // What scales the gains to it, as Healing Received scales Health's (TW-15)
            content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Attribute.GainsScaledBy)), "GainsScaledBy", "Gains Scaled By"));

            // The roles it plays in its project (TW-29), as TW-28's note said which slot it filled
            var part = IsMultiple ? null : ProjectPartOf(attribute);
            var projectPart = CreateNote(part ?? "");
            projectPart.name = "ProjectPart";
            projectPart.style.display = part == null ? DisplayStyle.None : DisplayStyle.Flex;
            content.Add(projectPart);

            var textures = serializedObject.FindProperty(nameof(Attribute.Textures));
            var texturesField = CreatePropertyField(textures, "Textures", "Textures");
            texturesField.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                MarkDirty(attribute);
                UpdateHeader();
                Repaint(); // Refresh IMGUI header
            });
            content.Add(texturesField);
        }

        /// <summary>
        /// The roles an attribute plays in its project, as "Plays Health in Arena", by their tags, or null where it plays
        /// none (TW-29).
        /// </summary>
        internal static string ProjectPartOf(Attribute attribute)
        {
            if (attribute == null) return null;
            var project = ForgeProjects.Of(attribute);
            var roles = project != null ? project.Settings?.RolesOf(attribute) : null;
            if (roles == null || roles.Count == 0) return null;
            var names = roles.Select(role => role.ToString()).ToList();
            string said = names.Count == 1 ? names[0] : string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1];
            string what = roles.Contains(Tags.ROLE_HEALTH) ? ": what an entity dies of at 0, and what On Hit counts where an entry names none" : "";
            return $"Plays {said} in {project.name}{what}.";
        }

        private void BuildLocalDataSection(VisualElement parent)
        {
            localDataSection = CreateSection(new SectionConfig
            {
                Name = "LocalData",
                Title = "Local Data",
                HelpTopic = "attributes/localdata",

                SerializedObject = serializedObject,
                PropertyPaths = new[] { nameof(Attribute.LocalData) },
                OnImportComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    MarkDirty(attribute);
                    Repaint();
                },
                OnClearComplete = () =>
                {
                    serializedObject.Update();
                    UpdateHeader();
                    MarkDirty(attribute);
                    Repaint();
                }
            });
            parent.Add(localDataSection.Section);

            localDataSection.Content.Add(CreatePropertyField(serializedObject.FindProperty(nameof(Attribute.LocalData)), "LocalData", ""));
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Used By Section
        // ═══════════════════════════════════════════════════════════════════════════

        // The assets that use the attribute, each a row that selects it. The section was a list of sentences about where
        // attributes can go, and a Find All References button that opened a dialog telling the user how to search (ED-15)
        private void BuildUsedBySection(VisualElement parent)
        {
            usedBySection = CreateSection(new SectionConfig
            {
                Name = "UsedBy",
                Title = "Used By",
                HelpTopic = "attributes/usage",
                IncludeButtons = new[] { false, false, true }
            });
            parent.Add(usedBySection.Section);

            if (IsMultiple)
            {
                usedBySection.Content.Add(CreateNote("Select one attribute to see what uses it."));
                return;
            }
            usedBy = new ForgeUsedBy(attribute);
            usedBy.Changed += UpdateSummaries;
            usedBySection.Content.Add(usedBy);
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Summaries: the line each collapsed section shows
        // ═══════════════════════════════════════════════════════════════════════════

        protected override void OnAssetChanged() => UpdateSummaries();

        private void UpdateSummaries()
        {
            if (identitySection != null) identitySection.Summary.text = IsMultiple ? "" : attribute.Abbreviation;
            if (localDataSection != null)
                localDataSection.Summary.text = IsMultiple ? "" : CountOf(attribute.LocalData?.Count ?? 0, "entry", "entries");
            if (usedBySection != null) usedBySection.Summary.text = usedBy == null ? "" : CountOf(usedBy.Count, "asset", "assets");
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // Abstract Implementations
        // ═══════════════════════════════════════════════════════════════════════════

        protected override void Refresh()
        {
            serializedObject.Update();
            Repaint();
        }
    }
}