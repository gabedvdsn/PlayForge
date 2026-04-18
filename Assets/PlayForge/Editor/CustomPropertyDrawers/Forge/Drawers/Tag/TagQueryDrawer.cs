using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    [CustomPropertyDrawer(typeof(TagQuery))]
    public class TagQueryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    marginBottom = 2,
                    marginTop = 2,
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingTop = 4,
                    paddingBottom = 4,
                    backgroundColor = Colors.ItemBackground,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3
                }
            };

            // ═══════════════════════════════════════════════════════════════════════════
            // Row 1: Tag Field
            // ═══════════════════════════════════════════════════════════════════════════
            
            var tagRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 4
                }
            };

            var tagLabel = new Label("Tag")
            {
                style =
                {
                    width = 70,
                    minWidth = 70,
                    unityTextAlign = UnityEngine.TextAnchor.MiddleLeft,
                    fontSize = 11,
                    color = Colors.LabelText
                }
            };
            tagRow.Add(tagLabel);

            var tagField = new PropertyField(property.FindPropertyRelative("Tag"), "");
            tagField.style.flexGrow = 1;
            tagField.style.minWidth = 120;
            tagField.BindProperty(property.FindPropertyRelative("Tag"));
            tagRow.Add(tagField);

            container.Add(tagRow);

            // ═══════════════════════════════════════════════════════════════════════════
            // Row 2: Match Mode | Operator | Magnitude
            // ═══════════════════════════════════════════════════════════════════════════
            
            var conditionRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center
                }
            };

            // Match Mode
            var matchModeLabel = new Label("Match")
            {
                style =
                {
                    width = 70,
                    minWidth = 70,
                    unityTextAlign = UnityEngine.TextAnchor.MiddleLeft,
                    fontSize = 11,
                    color = Colors.LabelText
                }
            };
            conditionRow.Add(matchModeLabel);

            var matchModeProp = property.FindPropertyRelative("MatchMode");
            var matchModeField = new EnumField((ETagMatchMode)matchModeProp.enumValueIndex);
            matchModeField.style.minWidth = 100;
            matchModeField.style.flexGrow = 0.5f;
            matchModeField.BindProperty(matchModeProp);
            matchModeField.tooltip = "Exact: Match only this tag\n" +
                                     "IncludeChildren: Match this tag or any child tags\n" +
                                     "IncludeParents: Match this tag or any parent tags\n" +
                                     "SameRoot: Match any tag with the same root category";
            conditionRow.Add(matchModeField);

            // Operator
            var opProp = property.FindPropertyRelative("Operator");
            var opField = new EnumField((EComparisonOperator)opProp.enumValueIndex);
            opField.style.minWidth = 90;
            opField.style.marginLeft = 8;
            opField.style.flexGrow = 0.5f;
            opField.BindProperty(opProp);
            opField.tooltip = "Comparison operator for the tag count";
            conditionRow.Add(opField);

            // Magnitude
            var magProp = property.FindPropertyRelative("Magnitude");
            var magField = new IntegerField();
            magField.style.minWidth = 40;
            magField.style.maxWidth = 50;
            magField.style.marginLeft = 8;
            magField.BindProperty(magProp);
            magField.tooltip = "Value to compare tag count against";
            conditionRow.Add(magField);

            container.Add(conditionRow);

            return container;
        }
    }
}