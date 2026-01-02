using System;
using System.Collections.Generic;
using System.Linq;
using FarEmerald.PlayForge;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    /// <summary>
    /// Custom property drawer for the _linkedSource field on GameplayEffect.
    /// Shows a dropdown of all ILevelProvider assets (Abilities and EntityIdentities)
    /// with visual feedback about the link status.
    /// </summary>
    [CustomPropertyDrawer(typeof(LinkedSourceAttribute))]
    public class LevelProviderDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.style.marginTop = 2;
            root.style.marginBottom = 4;
            
            BuildUI(root, property);
            return root;
        }
        
        private void BuildUI(VisualElement root, SerializedProperty property)
        {
            root.Clear();
            
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.paddingLeft = 4;
            container.style.paddingRight = 4;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.backgroundColor = new Color(0.18f, 0.18f, 0.2f, 0.6f);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.borderLeftWidth = 2;
            root.Add(container);
            
            var currentValue = property.objectReferenceValue;
            var isLinked = currentValue != null && currentValue is ILevelProvider;
            
            // Set border color based on link status
            container.style.borderLeftColor = isLinked ? Colors.AccentGreen : Colors.HintText;
            
            // Link icon
            var linkIcon = new Label(isLinked ? "🔗" : "○");
            linkIcon.style.fontSize = 12;
            linkIcon.style.width = 20;
            linkIcon.style.color = isLinked ? Colors.AccentGreen : Colors.HintText;
            linkIcon.tooltip = isLinked ? "Linked to level provider" : "No link (standalone effect)";
            container.Add(linkIcon);
            
            // Label
            var label = new Label("Level Source");
            label.style.width = 85;
            label.style.color = Colors.LabelText;
            label.style.fontSize = 11;
            container.Add(label);
            
            // Object field with filtering
            var objectField = new ObjectField();
            objectField.objectType = typeof(ScriptableObject);
            objectField.value = currentValue;
            objectField.style.flexGrow = 1;
            objectField.allowSceneObjects = false;
            
            objectField.RegisterValueChangedCallback(evt =>
            {
                var newValue = evt.newValue as ScriptableObject;
                
                // Validate that the new value implements ILevelProvider
                if (newValue != null && !(newValue is ILevelProvider))
                {
                    Debug.LogWarning($"[PlayForge] {newValue.name} does not implement ILevelProvider. Only Abilities and Entities can be linked.");
                    objectField.SetValueWithoutNotify(evt.previousValue);
                    return;
                }
                
                property.objectReferenceValue = newValue;
                property.serializedObject.ApplyModifiedProperties();
                
                // Rebuild UI to update visual state
                BuildUI(root, property);
            });
            
            container.Add(objectField);
            
            // Quick link buttons
            var btnContainer = new VisualElement();
            btnContainer.style.flexDirection = FlexDirection.Row;
            btnContainer.style.marginLeft = 4;
            container.Add(btnContainer);
            
            // Clear button
            if (isLinked)
            {
                var clearBtn = new Button(() =>
                {
                    property.objectReferenceValue = null;
                    property.serializedObject.ApplyModifiedProperties();
                    BuildUI(root, property);
                });
                clearBtn.text = "✕";
                clearBtn.tooltip = "Clear link (make standalone)";
                clearBtn.style.width = 22;
                clearBtn.style.height = 20;
                clearBtn.style.fontSize = 10;
                ApplyButtonStyle(clearBtn);
                btnContainer.Add(clearBtn);
            }
            
            // Browse button
            var browseBtn = new Button(() => ShowProviderPicker(property, root));
            browseBtn.text = "...";
            browseBtn.tooltip = "Browse level providers";
            browseBtn.style.width = 22;
            browseBtn.style.height = 20;
            browseBtn.style.fontSize = 10;
            browseBtn.style.marginLeft = 2;
            ApplyButtonStyle(browseBtn);
            btnContainer.Add(browseBtn);
            
            // Info row when linked
            if (isLinked)
            {
                var provider = currentValue as ILevelProvider;
                var infoRow = new VisualElement();
                infoRow.style.flexDirection = FlexDirection.Row;
                infoRow.style.alignItems = Align.Center;
                infoRow.style.marginTop = 4;
                infoRow.style.marginLeft = 24;
                root.Add(infoRow);
                
                // Type badge
                var typeName = currentValue is Ability ? "Ability" : 
                               currentValue is EntityIdentity ? "Entity" : "Provider";
                var typeColor = currentValue is Ability ? Colors.AccentBlue : 
                                currentValue is EntityIdentity ? Colors.AccentOrange : Colors.AccentPurple;
                
                var typeBadge = new Label(typeName);
                typeBadge.style.fontSize = 9;
                typeBadge.style.color = typeColor;
                typeBadge.style.backgroundColor = new Color(typeColor.r, typeColor.g, typeColor.b, 0.2f);
                typeBadge.style.paddingLeft = 4;
                typeBadge.style.paddingRight = 4;
                typeBadge.style.paddingTop = 1;
                typeBadge.style.paddingBottom = 1;
                typeBadge.style.borderTopLeftRadius = 3;
                typeBadge.style.borderTopRightRadius = 3;
                typeBadge.style.borderBottomLeftRadius = 3;
                typeBadge.style.borderBottomRightRadius = 3;
                typeBadge.style.marginRight = 6;
                infoRow.Add(typeBadge);
                
                // Max level info
                var maxLevelLabel = new Label($"Max Level: {provider.GetMaxLevel()}");
                maxLevelLabel.style.fontSize = 10;
                maxLevelLabel.style.color = Colors.AccentGreen;
                maxLevelLabel.style.marginRight = 8;
                infoRow.Add(maxLevelLabel);
                
                // Starting level info
                var startLevelLabel = new Label($"Start: {provider.GetStartingLevel()}");
                startLevelLabel.style.fontSize = 10;
                startLevelLabel.style.color = Colors.HintText;
                infoRow.Add(startLevelLabel);
            }
        }
        
        private void ShowProviderPicker(SerializedProperty property, VisualElement root)
        {
            var window = EditorWindow.CreateInstance<LevelProviderPickerWindow>();
            window.Initialize(property, () => BuildUI(root, property));
            window.ShowUtility();
        }
        
        private static void ApplyButtonStyle(Button btn)
        {
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 3;
            btn.style.borderBottomRightRadius = 3;
            btn.style.backgroundColor = Colors.ButtonBackground;
            btn.RegisterCallback<MouseEnterEvent>(_ => btn.style.backgroundColor = Colors.ButtonHover);
            btn.RegisterCallback<MouseLeaveEvent>(_ => btn.style.backgroundColor = Colors.ButtonBackground);
        }
    }
    
    /// <summary>
    /// Window for browsing and selecting ILevelProvider assets.
    /// </summary>
    public class LevelProviderPickerWindow : EditorWindow
    {
        private SerializedProperty targetProperty;
        private Action onSelected;
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private int selectedTab = 0; // 0 = All, 1 = Abilities, 2 = Entities
        
        private List<ScriptableObject> allProviders = new List<ScriptableObject>();
        private List<ScriptableObject> filteredProviders = new List<ScriptableObject>();
        
        public void Initialize(SerializedProperty property, Action callback)
        {
            targetProperty = property;
            onSelected = callback;
            
            titleContent = new GUIContent("Select Level Provider");
            minSize = new Vector2(350, 400);
            maxSize = new Vector2(500, 600);
            
            RefreshProviders();
        }
        
        private void RefreshProviders()
        {
            allProviders.Clear();
            
            // Find all Abilities
            var abilityGuids = AssetDatabase.FindAssets("t:Ability");
            foreach (var guid in abilityGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var ability = AssetDatabase.LoadAssetAtPath<Ability>(path);
                if (ability != null)
                    allProviders.Add(ability);
            }
            
            // Find all EntityIdentities
            var entityGuids = AssetDatabase.FindAssets("t:EntityIdentity");
            foreach (var guid in entityGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var entity = AssetDatabase.LoadAssetAtPath<EntityIdentity>(path);
                if (entity != null)
                    allProviders.Add(entity);
            }
            
            ApplyFilter();
        }
        
        private void ApplyFilter()
        {
            filteredProviders.Clear();
            
            foreach (var provider in allProviders)
            {
                // Type filter
                if (selectedTab == 1 && !(provider is Ability)) continue;
                if (selectedTab == 2 && !(provider is EntityIdentity)) continue;
                
                // Search filter
                if (!string.IsNullOrEmpty(searchFilter))
                {
                    var providerInterface = provider as ILevelProvider;
                    var name = providerInterface?.GetProviderName() ?? provider.name;
                    if (!name.ToLowerInvariant().Contains(searchFilter.ToLowerInvariant()))
                        continue;
                }
                
                filteredProviders.Add(provider);
            }
        }
        
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            
            // Header
            var header = new Label("🔗 Select Level Provider");
            header.style.fontSize = 14;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = Colors.HeaderText;
            header.style.marginBottom = 12;
            root.Add(header);
            
            // Search bar
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.marginBottom = 8;
            root.Add(searchRow);
            
            var searchField = new TextField();
            searchField.style.flexGrow = 1;
            searchField.value = searchFilter;
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchFilter = evt.newValue;
                ApplyFilter();
                RebuildList();
            });
            searchRow.Add(searchField);
            
            // Tab bar
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.marginBottom = 8;
            root.Add(tabBar);
            
            var tabs = new[] { "All", "⚔ Abilities", "👤 Entities" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tabIndex = i;
                var tabBtn = new Button(() =>
                {
                    selectedTab = tabIndex;
                    ApplyFilter();
                    RebuildList();
                    UpdateTabStyles(tabBar);
                });
                tabBtn.text = tabs[i];
                tabBtn.style.flexGrow = 1;
                tabBtn.style.paddingTop = 4;
                tabBtn.style.paddingBottom = 4;
                tabBtn.style.marginRight = i < tabs.Length - 1 ? 4 : 0;
                tabBtn.style.borderTopLeftRadius = 4;
                tabBtn.style.borderTopRightRadius = 4;
                tabBtn.style.borderBottomLeftRadius = 4;
                tabBtn.style.borderBottomRightRadius = 4;
                tabBar.Add(tabBtn);
            }
            
            UpdateTabStyles(tabBar);
            
            // "(None)" option
            var noneRow = CreateProviderRow(null);
            root.Add(noneRow);
            
            // Divider
            var divider = new VisualElement();
            divider.style.height = 1;
            divider.style.backgroundColor = Colors.DividerColor;
            divider.style.marginTop = 4;
            divider.style.marginBottom = 4;
            root.Add(divider);
            
            // Scrollable list
            var scrollView = new ScrollView();
            scrollView.name = "ProviderList";
            scrollView.style.flexGrow = 1;
            root.Add(scrollView);
            
            RebuildList();
        }
        
        private void UpdateTabStyles(VisualElement tabBar)
        {
            for (int i = 0; i < tabBar.childCount; i++)
            {
                var btn = tabBar[i] as Button;
                if (btn == null) continue;
                
                btn.style.backgroundColor = i == selectedTab 
                    ? Colors.AccentBlue 
                    : Colors.ButtonBackground;
                btn.style.color = i == selectedTab 
                    ? Colors.HeaderText 
                    : Colors.LabelText;
            }
        }
        
        private void RebuildList()
        {
            var scrollView = rootVisualElement.Q<ScrollView>("ProviderList");
            if (scrollView == null) return;
            
            scrollView.Clear();
            
            foreach (var provider in filteredProviders)
            {
                var row = CreateProviderRow(provider);
                scrollView.Add(row);
            }
            
            if (filteredProviders.Count == 0)
            {
                var emptyLabel = new Label("No providers found");
                emptyLabel.style.color = Colors.HintText;
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.marginTop = 20;
                scrollView.Add(emptyLabel);
            }
        }
        
        private VisualElement CreateProviderRow(ScriptableObject provider)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 6;
            row.style.paddingRight = 6;
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.marginBottom = 2;
            row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.5f);
            row.style.borderTopLeftRadius = 4;
            row.style.borderTopRightRadius = 4;
            row.style.borderBottomLeftRadius = 4;
            row.style.borderBottomRightRadius = 4;
            
            row.RegisterCallback<MouseEnterEvent>(_ => 
                row.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.8f));
            row.RegisterCallback<MouseLeaveEvent>(_ => 
                row.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.5f));
            
            if (provider == null)
            {
                // "(None)" option
                var noneIcon = new Label("○");
                noneIcon.style.fontSize = 12;
                noneIcon.style.width = 20;
                noneIcon.style.color = Colors.HintText;
                row.Add(noneIcon);
                
                var noneLabel = new Label("(None - Standalone Effect)");
                noneLabel.style.flexGrow = 1;
                noneLabel.style.color = Colors.HintText;
                noneLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                row.Add(noneLabel);
            }
            else
            {
                var providerInterface = provider as ILevelProvider;
                
                // Type icon
                var icon = provider is Ability ? "⚔" : "👤";
                var iconLabel = new Label(icon);
                iconLabel.style.fontSize = 12;
                iconLabel.style.width = 20;
                iconLabel.style.color = provider is Ability ? Colors.AccentBlue : Colors.AccentOrange;
                row.Add(iconLabel);
                
                // Name
                var name = providerInterface?.GetProviderName() ?? provider.name;
                var nameLabel = new Label(name);
                nameLabel.style.flexGrow = 1;
                nameLabel.style.color = Colors.LabelText;
                row.Add(nameLabel);
                
                // Max level badge
                var maxLevel = providerInterface?.GetMaxLevel() ?? 1;
                var levelBadge = new Label($"Lv{maxLevel}");
                levelBadge.style.fontSize = 9;
                levelBadge.style.color = Colors.AccentGreen;
                levelBadge.style.backgroundColor = new Color(Colors.AccentGreen.r, Colors.AccentGreen.g, Colors.AccentGreen.b, 0.2f);
                levelBadge.style.paddingLeft = 4;
                levelBadge.style.paddingRight = 4;
                levelBadge.style.paddingTop = 1;
                levelBadge.style.paddingBottom = 1;
                levelBadge.style.borderTopLeftRadius = 3;
                levelBadge.style.borderTopRightRadius = 3;
                levelBadge.style.borderBottomLeftRadius = 3;
                levelBadge.style.borderBottomRightRadius = 3;
                row.Add(levelBadge);
            }
            
            // Click handler
            row.RegisterCallback<ClickEvent>(_ =>
            {
                targetProperty.objectReferenceValue = provider;
                targetProperty.serializedObject.ApplyModifiedProperties();
                onSelected?.Invoke();
                Close();
            });
            
            return row;
        }
    }
}