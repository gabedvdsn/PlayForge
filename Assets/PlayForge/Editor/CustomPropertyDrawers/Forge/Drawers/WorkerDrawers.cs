using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{

    public abstract class AbstractWorkerDrawer<T> : AbstractGenericDrawer<T> where T : class
    {
        protected override bool AcceptOpen(SerializedProperty prop) => false;
        protected override bool AcceptAdd() => false;
        protected override void PopulateSummary(VisualElement container, SerializedProperty property)
        {
            
        }
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // ATTRIBUTE WORKER DRAWERS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AbstractAttributeWorker), true)]
    public class AttributeWorkerDrawer : AbstractWorkerDrawer<AbstractAttributeWorker>
    {
    }
    
    [CustomPropertyDrawer(typeof(AbstractFocusedAttributeWorker), true)]
    public class FocusedAttributeWorkerDrawer : AbstractWorkerDrawer<AbstractFocusedAttributeWorker>
    {
        protected override bool AcceptOpen(SerializedProperty prop) => false;
        protected override bool AcceptAdd() => false;
    }
    
    [CustomPropertyDrawer(typeof(AbstractRelativeAttributeWorker), true)]
    public class RelativeAttributeWorkerDrawer : AbstractGenericDrawer<AbstractRelativeAttributeWorker>
    {
        protected override bool AcceptOpen(SerializedProperty prop) => false;
        protected override bool AcceptAdd() => false;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // IMPACT WORKER DRAWERS
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AbstractImpactWorker), true)]
    public class ImpactWorkerDrawer : AbstractGenericDrawer<AbstractImpactWorker>
    {
        protected override bool AcceptOpen(SerializedProperty prop) => false;
        protected override bool AcceptAdd() => false;
    }
    
    [CustomPropertyDrawer(typeof(AbstractContextImpactWorker), true)]
    public class ContextImpactWorkerDrawer : AbstractGenericDrawer<AbstractContextImpactWorker>
    {
        protected override bool AcceptOpen(SerializedProperty prop) => false;
        protected override bool AcceptAdd() => false;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // EFFECT WORKER DRAWER
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AbstractEffectWorker), true)]
    public class EffectWorkerDrawer : AbstractGenericDrawer<AbstractEffectWorker>
    {
        protected override bool AcceptOpen(SerializedProperty prop) => false;
        protected override bool AcceptAdd() => false;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // TAG WORKER DRAWER
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AbstractTagWorker), true)]
    public class TagWorkerDrawer : AbstractGenericDrawer<AbstractTagWorker>
    {
        protected override bool AcceptOpen(SerializedProperty prop) => false;
        protected override bool AcceptAdd() => false;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // ANALYSIS WORKER DRAWER
    // ═══════════════════════════════════════════════════════════════════════════════
    
    [CustomPropertyDrawer(typeof(AbstractAnalysisWorker), true)]
    public class AnalysisWorkerDrawer : AbstractGenericDrawer<AbstractAnalysisWorker>
    {
        protected override bool AcceptOpen(SerializedProperty prop) => false;
        protected override bool AcceptAdd() => false;
    }
    
    // ═══════════════════════════════════════════════════════════════════════════════
    // TAG WORKER REQUIREMENTS DRAWER
    // ═══════════════════════════════════════════════════════════════════════════════
    
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
            
            // Weight field
            var weightProp = property.FindPropertyRelative("RequiredWeight");
            var weightField = new IntegerField
            {
                style = { width = 40, marginLeft = 4 }
            };
            weightField.BindProperty(weightProp);
            container.Add(weightField);
            
            return container;
        }
    }
}