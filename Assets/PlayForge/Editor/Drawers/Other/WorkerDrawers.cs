using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Editor
{

    /// <summary>A worker: its kind, always of some kind, then its fields, folding to a line of their values.</summary>
    public abstract class AbstractWorkerDrawer<T> : AbstractGenericDrawer<T> where T : class
    {
        protected override bool AcceptClear(SerializedProperty prop) => false;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // ATTRIBUTE WORKER DRAWERS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// An attribute worker, as a worker group's Attribute list holds one: its kind, picked from the attribute workers
    /// and never None, then its fields, folding to a line of their values.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractAttributeWorker), true)]
    public class AttributeWorkerDrawer : AbstractWorkerDrawer<AbstractAttributeWorker>
    {
    }
    
    /// <summary>
    /// A focused attribute worker, one made from <c>AbstractFocusedAttributeWorker</c>: its kind, picked from those
    /// workers and never None, then its fields, folding to a line of their values.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractFocusedAttributeWorker), true)]
    public class FocusedAttributeWorkerDrawer : AbstractWorkerDrawer<AbstractFocusedAttributeWorker>
    {
    }
    
    /// <summary>
    /// A relative attribute worker, one made from <c>AbstractRelativeAttributeWorker</c>: its kind, picked from those
    /// workers or None, then its fields, folding to a line of their values.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractRelativeAttributeWorker), true)]
    public class RelativeAttributeWorkerDrawer : AbstractGenericDrawer<AbstractRelativeAttributeWorker>
    {
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // IMPACT WORKER DRAWERS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// An impact worker, as a worker group's Impact list holds one: its kind, picked from the impact workers or None,
    /// then its fields, folding to a line of their values.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractImpactWorker), true)]
    public class ImpactWorkerDrawer : AbstractGenericDrawer<AbstractImpactWorker>
    {
    }
    
    /// <summary>
    /// A context impact worker, one made from <c>AbstractContextImpactWorker</c>: its kind, picked from those workers
    /// or None, then its fields, folding to a line of their values.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractContextImpactWorker), true)]
    public class ContextImpactWorkerDrawer : AbstractGenericDrawer<AbstractContextImpactWorker>
    {
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // EFFECT WORKER DRAWER
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// An effect worker, as an effect's Workers list holds one: its kind, picked from the effect workers or None, then
    /// its fields, folding to a line of their values.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractEffectWorker), true)]
    public class EffectWorkerDrawer : AbstractGenericDrawer<AbstractEffectWorker>
    {
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // TAG WORKER DRAWER
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// A tag worker, as a worker group's Tag list holds one: its kind, picked from the tag workers or None, then its
    /// fields, folding to a line of their values.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractTagWorker), true)]
    public class TagWorkerDrawer : AbstractGenericDrawer<AbstractTagWorker>
    {
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // ANALYSIS WORKER DRAWER
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// An analysis worker, as a worker group's Analysis list holds one: its kind, picked from the analysis workers or
    /// None, then its fields, folding to a line of their values.
    /// </summary>
    [CustomPropertyDrawer(typeof(AbstractAnalysisWorker), true)]
    public class AnalysisWorkerDrawer : AbstractGenericDrawer<AbstractAnalysisWorker>
    {
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // TAG WORKER REQUIREMENTS DRAWER
    // ═══════════════════════════════════════════════════════════════════════════════
    
    /// <summary>
    /// A tag worker's <c>Requirements</c>: its list of tag requirements alone, labelled Tag Requirements.
    /// </summary>
    [CustomPropertyDrawer(typeof(TagWorkerRequirements))]
    public class TagWorkerRequirementsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            
            var packetsProperty = property.FindPropertyRelative("TagPackets");
            if (packetsProperty != null)
            {
                var listView = new PropertyField(packetsProperty, "Tag Requirements");
                container.Add(listView);
            }
            
            return container;
        }
    }
    
    /// <summary>
    /// One of a tag worker's tag requirements, in a row: its tag, Require or Avoid, and the weight, a number of any
    /// kind. Require holds while the entity's weight of the tag is at least the weight, and Avoid while it's below.
    /// </summary>
    [CustomPropertyDrawer(typeof(TagWorkerRequirementPacket))]
    public class TagWorkerRequirementPacketDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 2
                }
            };
            
            // Tag field
            var tagProp = property.FindPropertyRelative("Tag");
            var tagField = new PropertyField(tagProp, "")
            {
                style = { flexGrow = 1, minWidth = 100 }
            };
            container.Add(tagField);
            
            // Policy dropdown
            var policyProp = property.FindPropertyRelative("Policy");
            var policyField = new EnumField((ERequireAvoidPolicy)policyProp.enumValueIndex)
            {
                style = { width = 70, marginLeft = 4 }
            };
            policyField.BindProperty(policyProp);
            container.Add(policyField);
            
            // Weight field: a number of any kind, as each authored number is
            var weightProp = property.FindPropertyRelative("RequiredWeight");
            var weightField = new PropertyField(weightProp, "")
            {
                style = { minWidth = 60, flexShrink = 0, marginLeft = 4 }
            };
            container.Add(weightField);
            
            return container;
        }
    }
}