using UnityEditor;
using UnityEngine;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public class PlayForgePromptWindow : EditorWindow
    {
        [MenuItem("Tools/PlayForge/More/Prompt Screen", false, 123)]
        private static void ShowWindow()
        {
            var window = GetWindow<PlayForgePromptWindow>();
            window.titleContent = new GUIContent("FESGAS Gasify");
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("This is your prompt about FESGAS!");
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope("box"))
            {
                if (GUILayout.Button("Close", GUILayout.Width(60)))
                {
                    Close();
                }
            
                if (GUILayout.Button("Open", GUILayout.Width(60)))
                {
                    var w = GetWindow<PlayForgeEditor>("PlayForge");
                    w.minSize = new Vector2(450, 350);
                    w.Show();
                    Close();
                }
            }
        }
    }
}
