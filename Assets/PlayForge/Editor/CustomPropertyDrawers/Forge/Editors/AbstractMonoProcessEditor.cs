using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomEditor(typeof(AbstractMonoProcess), true)]
    public class AbstractMonoProcessEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            
            // Process Settings Section
            var processSection = CreateSection("Process Settings", new Color(0.55f, 0.78f, 0.91f));
            root.Add(processSection);
            
            // Lifecycle
            var lifecycleContainer = new VisualElement();
            lifecycleContainer.style.marginBottom = 8;
            processSection.Add(lifecycleContainer);
            
            var lifecycleField = new PropertyField(serializedObject.FindProperty(nameof(AbstractMonoProcess.ProcessLifecycle)));
            lifecycleField.RegisterValueChangeCallback(_ => UpdateLifecycleHint(lifecycleContainer));
            lifecycleContainer.Add(lifecycleField);
            
            var lifecycleHint = CreateHintLabel("LifecycleHint");
            lifecycleContainer.Add(lifecycleHint);
            
            // Timing
            var timingContainer = new VisualElement();
            timingContainer.style.marginBottom = 8;
            processSection.Add(timingContainer);
            
            var timingField = new PropertyField(serializedObject.FindProperty(nameof(AbstractMonoProcess.ProcessTiming)));
            timingField.RegisterValueChangeCallback(_ => UpdateTimingHint(timingContainer));
            timingContainer.Add(timingField);
            
            var timingHint = CreateHintLabel("TimingHint");
            timingContainer.Add(timingHint);
            
            // Priority
            var priorityContainer = new VisualElement();
            priorityContainer.style.marginBottom = 8;
            processSection.Add(priorityContainer);
            
            var priorityMethodField = new PropertyField(serializedObject.FindProperty(nameof(AbstractMonoProcess.PriorityMethod)));
            priorityMethodField.RegisterValueChangeCallback(_ => 
            {
                UpdatePriorityVisibility(priorityContainer);
                UpdatePriorityMethodHint(priorityContainer);
            });
            priorityContainer.Add(priorityMethodField);
            
            var priorityMethodHint = CreateHintLabel("PriorityMethodHint");
            priorityContainer.Add(priorityMethodHint);
            
            var manualPriorityField = new PropertyField(serializedObject.FindProperty(nameof(AbstractMonoProcess.ProcessStepPriority)), "Priority Value");
            manualPriorityField.name = "ManualPriorityField";
            manualPriorityField.style.marginTop = 4;
            priorityContainer.Add(manualPriorityField);
            
            // Instantiator
            var instantiatorProp = serializedObject.FindProperty(nameof(AbstractMonoProcess.Instantiator));
            if (instantiatorProp != null)
            {
                var instantiatorField = new PropertyField(instantiatorProp);
                instantiatorField.style.marginBottom = 4;
                processSection.Add(instantiatorField);
                
                var instantiatorHint = CreateHintLabel("InstantiatorHint");
                instantiatorHint.text = "Custom instantiation/destruction logic. Uses Object.Instantiate and Object.Destroy when null.";
                processSection.Add(instantiatorHint);
            }
            
            // Separator
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            separator.style.marginTop = 12;
            separator.style.marginBottom = 8;
            root.Add(separator);
            
            /*// Draw default inspector for all other fields
            var defaultInspector = new IMGUIContainer(() =>
            {
                serializedObject.Update();
                
                var iterator = serializedObject.GetIterator();
                iterator.NextVisible(true); // Skip script field
                
                while (iterator.NextVisible(false))
                {
                    // Skip the fields we already drew
                    if (IsProcessField(iterator.name)) continue;
                    
                    EditorGUILayout.PropertyField(iterator, true);
                }
                
                serializedObject.ApplyModifiedProperties();
            });
            root.Add(defaultInspector);*/

            Debug.Log(target);
            Debug.Log(serializedObject.targetObject);
            //DrawDefaultInspector();
            
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            
            // Initial hint updates
            root.schedule.Execute(() =>
            {
                UpdateLifecycleHint(lifecycleContainer);
                UpdateTimingHint(timingContainer);
                UpdatePriorityVisibility(priorityContainer);
                UpdatePriorityMethodHint(priorityContainer);
            }).ExecuteLater(50);
            
            return root;
        }
        
        private bool IsProcessField(string name)
        {
            return name is "ProcessLifecycle" or "ProcessTiming" or "PriorityMethod" 
                or "ProcessStepPriority" or "Instantiator";
        }
        
        private VisualElement CreateSection(string title, Color accentColor)
        {
            var section = new VisualElement();
            section.style.marginBottom = 4;
            section.style.paddingLeft = 8;
            section.style.paddingRight = 8;
            section.style.paddingTop = 8;
            section.style.paddingBottom = 8;
            section.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
            section.style.borderTopLeftRadius = 4;
            section.style.borderTopRightRadius = 4;
            section.style.borderBottomLeftRadius = 4;
            section.style.borderBottomRightRadius = 4;
            //section.style.borderLeftWidth = 3;
            section.style.borderLeftColor = accentColor;
            
            var header = new Label(title);
            header.style.fontSize = 12;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = accentColor;
            header.style.marginBottom = 4;
            section.Add(header);
            
            return section;
        }
        
        private Label CreateHintLabel(string name)
        {
            var hint = new Label();
            hint.name = name;
            hint.style.fontSize = 10;
            hint.style.color = new Color(0.6f, 0.6f, 0.6f);
            hint.style.marginLeft = 3;
            hint.style.marginTop = 2;
            hint.style.whiteSpace = WhiteSpace.Normal;
            return hint;
        }
        
        private void UpdateLifecycleHint(VisualElement container)
        {
            var hint = container.Q<Label>("LifecycleHint");
            if (hint == null) return;
            
            var prop = serializedObject.FindProperty("ProcessLifecycle");
            hint.text = prop.enumValueIndex switch
            {
                0 => "Runs once, terminates when RunProcess completes.",
                1 => "Runs once, then waits. Can be resumed externally.",
                2 => "Starts waiting. Must be explicitly started via ProcessControl.",
                _ => ""
            };
        }
        
        private void UpdateTimingHint(VisualElement container)
        {
            var hint = container.Q<Label>("TimingHint");
            if (hint == null) return;
            
            var prop = serializedObject.FindProperty("ProcessTiming");
            hint.text = prop.enumValueIndex switch
            {
                0 => "Process is completely detached from the Update loop.",
                1 => "WhenUpdate called every frame.",
                2 => "WhenLateUpdate called every frame.",
                3 => "WhenFixedUpdate called every physics step.",
                4 => "WhenUpdate + WhenLateUpdate each frame.",
                5 => "WhenUpdate + WhenFixedUpdate.",
                6 => "WhenLateUpdate + WhenFixedUpdate.",
                7 => "All three update methods called.",
                _ => ""
            };
        }
        
        private void UpdatePriorityVisibility(VisualElement container)
        {
            var manualField = container.Q("ManualPriorityField");
            if (manualField == null) return;
            
            var prop = serializedObject.FindProperty("PriorityMethod");
            manualField.style.display = prop.enumValueIndex == 0 
                ? DisplayStyle.Flex 
                : DisplayStyle.None;
        }
        
        private void UpdatePriorityMethodHint(VisualElement container)
        {
            var hint = container.Q<Label>("PriorityMethodHint");
            if (hint == null) return;
            
            var prop = serializedObject.FindProperty("PriorityMethod");
            hint.text = prop.enumValueIndex switch
            {
                0 => "Explicit priority value. Lower = earlier.",
                1 => "Stepped before other processes.",
                2 => "Stepped after other processes.",
                _ => ""
            };
        }
    }
}