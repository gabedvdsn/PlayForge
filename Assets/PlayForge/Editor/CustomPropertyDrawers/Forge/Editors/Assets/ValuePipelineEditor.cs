using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// A Value Pipeline's inspector (TW-2): what it's for, its stages in order, each of a kind with its fields, and a line
    /// that says them. Unity's own fields, as <c>Docs/EDITOR_STYLE.md</c> has it.
    /// </summary>
    [CustomEditor(typeof(ValuePipeline))]
    public class ValuePipelineEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var pipeline = (ValuePipeline)target;
            var root = new VisualElement { name = "ValuePipelineRoot" };
            ForgeTheme.Apply(root);
            // The shared header, as every PlayForge asset's inspector opens with (FN-12)
            root.Add(ForgeAssetHeaders.For(this));

            var description = new TextField("Description")
            {
                name = "Description", multiline = true, bindingPath = nameof(ValuePipeline.Description),
                tooltip = "What the pipeline is for",
            };
            description.AddToClassList(TextField.alignedFieldUssClassName);
            root.Add(description);

            // What a value goes through, in a line, which follows the stages as they're edited
            var line = new Label(pipeline.Describe()) { name = "PipelineLine", tooltip = "The stages a value goes through, in order" };
            line.style.whiteSpace = WhiteSpace.Normal;
            root.Add(line);

            var stages = new PropertyField(serializedObject.FindProperty(nameof(ValuePipeline.Stages)), "Stages")
            {
                name = "Stages",
                tooltip = "The stages a value goes through, in order, each reading the hit's caster and target",
            };
            ForgeLists.AddsEmpty(stages, serializedObject, nameof(ValuePipeline.Stages));
            stages.RegisterCallback<SerializedPropertyChangeEvent>(_ => line.text = pipeline.Describe());
            root.Add(stages);
            return root;
        }
    }

    /// <summary>A pipeline stage: its kind, always of some kind, then its fields, as a worker is drawn (TW-2).</summary>
    [CustomPropertyDrawer(typeof(AbstractValueStage), true)]
    public class ValueStageDrawer : AbstractGenericDrawer<AbstractValueStage>
    {
        protected override bool AcceptClear(SerializedProperty prop) => false;
    }
}
