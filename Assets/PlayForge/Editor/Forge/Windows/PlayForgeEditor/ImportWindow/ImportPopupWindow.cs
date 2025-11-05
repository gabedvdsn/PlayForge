using System;
using System.Collections.Generic;
using System.Linq;
using Codice.Client.Common;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FarEmerald.PlayForge.Extended.Editor
{

    public class ImportPopupWindow : EditorWindow
    {

        public VisualTreeAsset Root;
        public VisualTreeAsset ImportRow;

        private string Title;
        private List<ForgeDataNode> Nodes;
        private EDataType Kind;

        private Func<string> subTitle;
        private Action<PlayForgeEditor.DataContainer> onPick;
        private Func<ForgeDataNode, List<ImportPopupDescrPacket>> makeDescr;

        public static bool isShown = false;
        
        public static ImportPopupWindow Open(
            string _title, List<ForgeDataNode> nodes, EDataType kind,
            Func<string> subTitle,
            Action<PlayForgeEditor.DataContainer> onPick, // When select import
            Func<ForgeDataNode, List<ImportPopupDescrPacket>> makeDescr  //
        )
        {
            var w = GetWindow<ImportPopupWindow>("PlayForge Import Menu");
            
            w.Title = _title;
            w.Nodes = nodes;
            w.Kind = kind;

            w.subTitle = subTitle;
            w.onPick = onPick;
            w.makeDescr = makeDescr;

            isShown = true;
            
            w.Show();

            w.Bind();
            w.Build();
            w.Refresh();
            
            return w;
        }

        private void CreateGUI()
        {
            var _root = rootVisualElement;

            if (Root is not null)
            {
                var content = Root.CloneTree();
                _root.Add(content);
            }
        }

        private void OnDestroy()
        {
            isShown = false;
        }

        private VisualElement root;
        
        private Label headerLabel;
        
        private ListView items;
        
        private Label descrLabel;
        private Button descrImport;

        private ListView descrItems;

        private int selectedIndex = -1;
        private List<ImportPopupDescrPacket> descrPackets;
        
        void Bind()
        {
            root = rootVisualElement.Q("Root");
            
            headerLabel = root.Q<Label>("Title");
            items = root.Q<ListView>("List");
            
            descrLabel = root.Q<Label>("DescrLabel");
            descrImport = root.Q<Button>("DescrImport");

            descrItems = root.Q<ListView>("DescrList");
        }

        void Build()
        {
            descrPackets = new List<ImportPopupDescrPacket>();

            headerLabel.text = $"{Title}{GetTitleSub()}";
            
            items.itemsSource = Nodes;
            items.makeItem = () => ImportRow.CloneTree();
            items.bindItem = (ve, idx) =>
            {
                var data = Nodes[idx];

                var iLabel = ve.Q<Label>("Label");

                iLabel.text = GetTitle(data);

                var import = ve.Q<Button>("Import");
                import.clicked += () =>
                {
                    onPick?.Invoke(new PlayForgeEditor.DataContainer(data, Kind));
                    Close();
                };

                import.style.display = DisplayStyle.None;
                
                ve.RegisterCallback<ClickEvent>(evt =>
                {
                    selectedIndex = idx;
                    evt.StopPropagation();
                    Refresh();
                });
                
                ve.RegisterCallback<PointerEnterEvent>(evt =>
                {
                    evt.StopPropagation();
                    if (selectedIndex == idx) return;
                    
                    ve.style.backgroundColor = (PlayForgeEditor.ColorDark + PlayForgeEditor.ColorLight) * .5f;
                    import.style.display = DisplayStyle.Flex;
                });
                
                ve.RegisterCallback<PointerLeaveEvent>(evt =>
                {
                    evt.StopPropagation();

                    if (selectedIndex == idx) return;
                    ve.style.backgroundColor = Color.clear;
                    
                    import.style.display = DisplayStyle.None;
                });
                
                if (selectedIndex == idx)
                {
                    ve.style.backgroundColor = PlayForgeEditor.ColorSelected;
                    import.style.display = DisplayStyle.Flex;
                }
            };

            items.selectionChanged += evt =>
            {
                Debug.Log("Clear selection");
                items.ClearSelection();
            };

            items.RegisterCallback<ClickEvent>(evt =>
            {
                selectedIndex = -1;
                items.ClearSelection();
                Refresh();
            });

            descrItems.itemsSource = descrPackets;
            descrItems.makeNoneElement = () =>
            {
                var holder = CreateDescrHolder();

                var icon = holder.Q<VisualElement>("icon");
                icon.style.backgroundImage = PlayForgeEditor.GetConsoleAlertIcon(EValidationCode.Ok);

                var body = holder.Q("holder");
                body.Add(CreateDescrText("Select an item for more details."));

                return holder;
            };

            descrItems.makeItem = CreateDescrHolder;
            descrItems.bindItem = (ve, idx) =>
            {
                var data = descrItems.itemsSource[idx] as ImportPopupDescrPacket;
                if (data is null) return;
                
                var icon = ve.Q("icon");
                icon.style.backgroundImage = PlayForgeEditor.GetConsoleAlertIcon(data.Code);

                var holder = ve.Q("holder");
                foreach (var msg in data.Messages)
                {
                    holder.Add(CreateDescrText(msg));
                }
            };
        }

        string GetTitle(ForgeDataNode node)
        {
            return $"{node.Name}{GetTitleSub()}";
        }

        string GetTitleSub()
        {
            string _stub = subTitle?.Invoke() ?? "";
            string _sub = string.IsNullOrEmpty(_stub) ? "" : $" \u2192 {_stub}";
            return _sub;
        }

        void Refresh()
        {
            if (selectedIndex < 0)
            {
                descrPackets.Clear();
                descrLabel.text = "No option selected...";
                
                descrImport.style.display = DisplayStyle.None;
            }
            else
            {
                var node = Nodes[selectedIndex];
                descrPackets = makeDescr?.Invoke(node) ?? new List<ImportPopupDescrPacket>()
                {
                    new(EValidationCode.Ok, new List<string>() { "Could not build description packets..." })
                };

                descrItems.itemsSource = descrPackets;
                
                foreach (var p in descrPackets) Debug.Log($"{p.Code} {p.Messages[0]}");
                
                descrLabel.text = GetTitle(Nodes[selectedIndex]);

                descrImport.style.display = DisplayStyle.Flex;
            }

            headerLabel.text = $"{Title}{GetTitleSub()}";
            
            items.Rebuild();
            descrItems.Rebuild();
        }

        void SetDescPackets()
        {
            if (selectedIndex < 0) return;
            
            
        }

        private VisualElement CreateDescrHolder()
        {
            var holder = new VisualElement()
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            var alertIcon = new VisualElement()
            {
                name = "icon",
                style =
                {
                    marginTop = 4,
                    minWidth = 12, maxWidth = 12,
                    minHeight = 12, maxHeight = 12
                }
            };
            
            holder.Add(alertIcon);

            var messagesHolder = new VisualElement()
            {
                name = "holder",
                style =
                {
                    paddingLeft = 4
                }
            };
            
            holder.Add(messagesHolder);
            
            return holder;
        }
        
        private Label CreateDescrText(string text)
        {
            return new Label($"• {text}")
            {
                style =
                {
                    marginTop = 1, marginBottom = 1,
                    paddingTop = 1, paddingBottom = 1,
                    fontSize = 11, unityTextAlign = TextAnchor.MiddleLeft, flexWrap = Wrap.Wrap
                }
            };
        }
    }
}
