using System;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{
    public partial class PlayForgeEditor
    {
        private VisualElement navbarRoot;

        private ToolbarButton nb_homeButton;
        private ToolbarButton nb_createMenuButton;
        private ToolbarButton nb_analyticsButton;
        private ToolbarButton nb_developButton;
        private ToolbarButton nb_validateButton;
        private ToolbarButton nb_settingsButton;
        private ToolbarButton nb_optionsButton;

        private VisualElement nb_activeButton;
        
        private void BindNavBar()
        {
            navbarRoot = contentRoot.Q("NavBar");
            
            nb_homeButton = navbarRoot.Q<ToolbarButton>("Home");    
            nb_createMenuButton = navbarRoot.Q<ToolbarButton>("Create");
            nb_analyticsButton = navbarRoot.Q<ToolbarButton>("Analytics");    
            nb_developButton = navbarRoot.Q<ToolbarButton>("Develop");
            nb_validateButton = navbarRoot.Q<ToolbarButton>("Validate");    
            nb_settingsButton = navbarRoot.Q<ToolbarButton>("Settings");    
            nb_optionsButton = navbarRoot.Q<ToolbarButton>("Options");
        }

        private void BuildNavBar()
        {
            nb_homeButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (nb_activeButton == nb_homeButton) return;
                nb_homeButton.style.backgroundColor = ColorLight * 1.3f;
            });
            nb_createMenuButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (nb_activeButton == nb_createMenuButton) return;
                nb_createMenuButton.style.backgroundColor = ColorLight * 1.3f;
            });
            nb_analyticsButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (nb_activeButton == nb_analyticsButton) return;
                nb_analyticsButton.style.backgroundColor = ColorLight * 1.3f;
            });
            nb_developButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (nb_activeButton == nb_developButton) return;
                nb_developButton.style.backgroundColor = ColorLight * 1.3f;
            });
            nb_validateButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (nb_activeButton == nb_validateButton) return;
                nb_validateButton.style.backgroundColor = ColorLight * 1.3f;
            });
            nb_settingsButton.RegisterCallback<PointerEnterEvent>(_ =>
            {
                if (nb_activeButton == nb_settingsButton) return;
                nb_settingsButton.style.backgroundColor = ColorLight * 1.3f;
            });

            nb_homeButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (nb_activeButton == nb_homeButton) return;
                nb_homeButton.style.backgroundColor = ColorLight;
            });
            nb_createMenuButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (nb_activeButton == nb_createMenuButton) return;
                nb_createMenuButton.style.backgroundColor = ColorLight;
            });
            nb_analyticsButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (nb_activeButton == nb_analyticsButton) return;
                nb_analyticsButton.style.backgroundColor = ColorLight;
            });
            nb_developButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (nb_activeButton == nb_developButton) return;
                nb_developButton.style.backgroundColor = ColorLight;
            });
            nb_validateButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (nb_activeButton == nb_validateButton) return;
                nb_validateButton.style.backgroundColor = ColorLight;
            });
            nb_settingsButton.RegisterCallback<PointerLeaveEvent>(_ =>
            {
                if (nb_activeButton == nb_settingsButton) return;
                nb_settingsButton.style.backgroundColor = ColorLight;
            });
            
            nb_homeButton.clicked += () => OnClickNavBarButton(nb_homeButton, EForgeContext.Home, (_, _) => DoContextAction(EForgeContextExpanded.Home));
            nb_createMenuButton.clicked += () => OnClickNavBarButton(nb_createMenuButton, EForgeContext.Creator, (_, _) => DoContextAction(EForgeContextExpanded.Creator));
            nb_analyticsButton.clicked += () => OnClickNavBarButton(nb_analyticsButton, EForgeContext.Analytics, (_, _) => DoContextAction(EForgeContextExpanded.Analytics));
            nb_developButton.clicked += () => OnClickNavBarButton(nb_developButton, EForgeContext.Develop, (_, _) => DoContextAction(EForgeContextExpanded.Develop));
            nb_validateButton.clicked += () => OnClickNavBarButton(nb_validateButton, EForgeContext.Validate, (_, _) => DoContextAction(EForgeContextExpanded.Validate));
            nb_settingsButton.clicked += () => OnClickNavBarButton(nb_settingsButton, EForgeContext.Settings, (_, _) => DoContextAction(EForgeContextExpanded.Settings));
            nb_optionsButton.clicked += () => OpenNavBarOptionsMenu(nb_optionsButton.worldBound.center);

            return;

            void OnClickNavBarButton(Button src, EForgeContext action, Action<EForgeContext, object> func, object payload = null, bool useFancy = true)
            {
                func?.Invoke(action, payload);
                
                if (!useFancy) return;
                
                nb_activeButton = src;
                RefreshNavBar();
            }
        }

        private void RefreshNavBar()
        {
            var src = ActiveContext switch
            {
                EForgeContext.Home => nb_homeButton,
                EForgeContext.Creator => nb_createMenuButton,
                EForgeContext.Analytics => nb_analyticsButton,
                EForgeContext.Develop => nb_developButton,
                EForgeContext.Validate => nb_validateButton,
                EForgeContext.Settings => nb_settingsButton,
                _ => throw new ArgumentOutOfRangeException()
            };

            ApplyFancyButtonBarBorders(src, 1, 0,
                nb_homeButton,
                nb_createMenuButton,
                nb_analyticsButton,
                nb_developButton,
                nb_validateButton,
                nb_settingsButton
            );
        }

        void OpenNavBarOptionsMenu(Vector2 pos)
        {
            GenericMenu menu = new GenericMenu();
            
            menu.AddItem(new GUIContent("Show in Explorer"), false, () =>
            {
                var path = ForgePaths.FrameworkFolder(Project.MetaName);
                if (Directory.Exists(path)) EditorUtility.RevealInFinder(Path.Combine(path, Project.MetaName));
            });

            var r = new Rect(pos, Vector2.zero);
            menu.DropDown(r);
        }
    }
}
