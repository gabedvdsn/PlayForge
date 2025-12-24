using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(AbilityTaskBehaviourStage))]
    public class AbilityTaskBehaviourStageDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement()
            {
                style =
                {
                    flexGrow = 0,
                    flexShrink = 1,
                    marginBottom = 8,
                    paddingBottom = 8,
                    paddingTop = 8,
                    paddingLeft = 4,
                    paddingRight = 4,
                    backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f),
                    borderBottomLeftRadius = 4,
                    borderBottomRightRadius = 4,
                    borderTopLeftRadius = 4,
                    borderTopRightRadius = 4
                }
            };
            
            // Debug: List all available properties to diagnose issues
            #if UNITY_EDITOR && false // Set to true to enable debug output
            DebugListProperties(property);
            #endif
            
            var header = new VisualElement()
            {
                style =
                {
                    flexGrow = 1,
                    flexShrink = 0,
                    flexDirection = FlexDirection.Row,
                    marginBottom = 4
                }
            };

            root.Add(header);
            
            var left = new VisualElement()
            {
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };
            
            header.Add(left);

            // Get the stage index from the property path
            var stageIndex = GetStageIndex(property.propertyPath);
            
            var stageLabel = new Label()
            {
                text = $"Stage {stageIndex}",
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 13,
                    marginBottom = 2
                }
            };
            
            left.Add(stageLabel);
            
            // ApplyUsageEffects field
            var applyUsageEffectsProp = property.FindPropertyRelative("ApplyUsageEffects");
            if (applyUsageEffectsProp != null)
            {
                var usageEffects = new PropertyField(applyUsageEffectsProp)
                {
                    label = "Apply Usage Effects (Cost & Cooldown)",
                    tooltip = "After this stage completes, should the ability usage effects (Cost and Cooldown) be applied?"
                };
                usageEffects.style.fontSize = 11;
                usageEffects.style.marginLeft = 0;
                usageEffects.style.marginTop = 2;
                left.Add(usageEffects);
            }
            else
            {
                left.Add(CreateErrorLabel("ApplyUsageEffects property not found"));
            }

            var right = new VisualElement()
            {
                style =
                {
                    flexGrow = 0,
                    flexShrink = 0,
                    minWidth = 200
                }
            };
            
            header.Add(right);
            
            // StagePolicy field
            var stagePolicyProp = property.FindPropertyRelative("StagePolicy");
            if (stagePolicyProp != null)
            {
                var policyField = new PropertyField(stagePolicyProp)
                {
                    label = "Stage Policy"
                };
                policyField.style.marginLeft = 8;
                right.Add(policyField);
            }
            else
            {
                right.Add(CreateErrorLabel("StagePolicy property not found"));
            }

            // Tasks list
            var tasksContainer = new VisualElement()
            {
                style =
                {
                    marginTop = 8,
                    paddingLeft = 8,
                    borderLeftWidth = 2,
                    borderLeftColor = new Color(0.3f, 0.5f, 0.7f, 0.5f)
                }
            };
            
            var tasksProp = property.FindPropertyRelative("Tasks");
            if (tasksProp != null)
            {
                var tasksField = new PropertyField(tasksProp)
                {
                    label = "Tasks"
                };
                tasksContainer.Add(tasksField);
            }
            else
            {
                tasksContainer.Add(CreateErrorLabel("Tasks property not found"));
                
                // Debug output to help diagnose
                Debug.LogWarning($"[AbilityTaskBehaviourStageDrawer] Could not find 'Tasks' property. " +
                                 $"Property path: {property.propertyPath}, Type: {property.type}");
            }
            
            root.Add(tasksContainer);

            return root;
        }
        
        private Label CreateErrorLabel(string message)
        {
            return new Label(message)
            {
                style =
                {
                    color = new Color(1f, 0.5f, 0.5f),
                    unityFontStyleAndWeight = FontStyle.Italic
                }
            };
        }
        
        private int GetStageIndex(string propertyPath)
        {
            // Property path will be something like "Proxy.Stages.Array.data[0]"
            var match = System.Text.RegularExpressions.Regex.Match(propertyPath, @"data\[(\d+)\]");
            if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
            {
                return index;
            }
            return 0;
        }
        
        /// <summary>
        /// Debug helper to list all properties on a SerializedProperty
        /// </summary>
        private void DebugListProperties(SerializedProperty property)
        {
            Debug.Log($"[AbilityTaskBehaviourStageDrawer] Listing properties for: {property.propertyPath}");
            
            var iterator = property.Copy();
            var endProperty = property.GetEndProperty();
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;
                        
                    Debug.Log($"  - {iterator.name} (type: {iterator.propertyType}, path: {iterator.propertyPath})");
                    
                } while (iterator.NextVisible(false));
            }
        }
    }
}