using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static FarEmerald.PlayForge.Extended.Editor.ForgeDrawerStyles;

namespace FarEmerald.PlayForge.Extended.Editor
{
    // ═══════════════════════════════════════════════════════════════════════════
    // AttributeSetElement Drawer
    //
    // Layout
    //   ▸ Header           collapse toggle + Attribute field
    //                      compact 2-line summary (visible only when collapsed)
    //
    //   When expanded:
    //     - Retention group
    //     - Collision policy
    //     - ALWAYS-VISIBLE summary block (so the user can see the configured magnitudes /
    //       overflow / constraints without having to flip between tabs).
    //     - "🔗 Link Current to Base" toggle (always visible, persisted on the element via
    //       LinkCurrentToBase). When ON, the Current tab is disabled and shows a hint;
    //       runtime resolution forwards Current → Base.
    //     - TabView: Current | Base — each edits Magnitude / Real Magnitude / Scaling.
    //     - Overflow (element-wide)
    //     - Constraints (element-wide)
    // ═══════════════════════════════════════════════════════════════════════════

    [CustomPropertyDrawer(typeof(AttributeSetElement))]
    public class AttributeSetElementDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, bool> _collapsedStates = new();

        private static bool IsCollapsed(string path) => _collapsedStates.TryGetValue(path, out var c) && c;
        private static void SetCollapsed(string path, bool collapsed) => _collapsedStates[path] = collapsed;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement { name = "AttributeSetElementRoot" };
            bool startCollapsed = IsCollapsed(property.propertyPath);

            // ─── Locate sub-properties ───────────────────────────────────────
            var attributeProp   = property.FindPropertyRelative(nameof(AttributeSetElement.Attribute));
            var currentProp     = property.FindPropertyRelative(nameof(AttributeSetElement.Current));
            var baseProp        = property.FindPropertyRelative(nameof(AttributeSetElement.Base));
            var linkProp        = property.FindPropertyRelative(nameof(AttributeSetElement.LinkCurrentToBase));
            var overflowProp    = property.FindPropertyRelative(nameof(AttributeSetElement.Overflow));
            var collisionProp   = property.FindPropertyRelative(nameof(AttributeSetElement.CollisionPolicy));
            var constraintsProp = property.FindPropertyRelative(nameof(AttributeSetElement.Constraints));
            var retGroupProp    = property.FindPropertyRelative(nameof(AttributeSetElement.RetentionGroup));

            var curMagProp     = currentProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.Magnitude));
            var curScalingProp = currentProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.Scaling));
            var curRealMagProp = currentProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.RealMagnitude));

            var baseMagProp     = baseProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.Magnitude));
            var baseScalingProp = baseProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.Scaling));
            var baseRealMagProp = baseProp.FindPropertyRelative(nameof(AttributeMagnitudeSpec.RealMagnitude));

            // ─── Container chrome ────────────────────────────────────────────
            var container = new VisualElement { name = "Container" };
            container.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 0.5f);
            container.style.borderTopLeftRadius = 4;
            container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = 4;
            container.style.borderBottomRightRadius = 4;
            container.style.borderLeftWidth = 3;
            container.style.borderLeftColor = Colors.AccentGray;
            container.style.paddingTop = 4;
            container.style.paddingBottom = 4;
            container.style.paddingLeft = 6;
            container.style.paddingRight = 6;
            container.style.marginTop = 2;
            container.style.marginBottom = 2;
            root.Add(container);

            // ─── Header row (always visible) ─────────────────────────────────
            var headerRow = new VisualElement { name = "HeaderRow" };
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            container.Add(headerRow);

            var collapseBtn = new Button { name = "CollapseBtn" };
            collapseBtn.focusable = false;
            collapseBtn.text = startCollapsed ? Icons.ChevronRight : Icons.ChevronDown;
            collapseBtn.style.width = 18;
            collapseBtn.style.height = 18;
            collapseBtn.style.fontSize = 8;
            collapseBtn.style.marginRight = 4;
            collapseBtn.style.paddingLeft = 0;
            collapseBtn.style.paddingRight = 0;
            collapseBtn.style.paddingTop = 0;
            collapseBtn.style.paddingBottom = 0;
            collapseBtn.style.unityTextAlign = TextAnchor.MiddleCenter;
            collapseBtn.style.backgroundColor = Colors.ButtonBackground;
            collapseBtn.style.borderTopLeftRadius = 3;
            collapseBtn.style.borderTopRightRadius = 3;
            collapseBtn.style.borderBottomLeftRadius = 3;
            collapseBtn.style.borderBottomRightRadius = 3;
            headerRow.Add(collapseBtn);

            var attributeField = new PropertyField(attributeProp, "");
            attributeField.style.flexGrow = 1;
            attributeField.style.minWidth = 100;
            attributeField.BindProperty(attributeProp);
            headerRow.Add(attributeField);

            // ─── Collapsed summary (shown ONLY when collapsed; the always-visible
            //     summary lives inside expandedContent below) ─────────────────
            var collapsedSummary = BuildSummaryColumn();
            collapsedSummary.style.marginLeft = 22; // align under collapse btn
            collapsedSummary.style.display = startCollapsed ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(collapsedSummary);

            // ─── Expanded content ────────────────────────────────────────────
            var expandedContent = new VisualElement { name = "ExpandedContent" };
            expandedContent.style.marginTop = 6;
            expandedContent.style.display = startCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
            container.Add(expandedContent);

            // Retention + Collision (element-wide)
            expandedContent.Add(BuildLabeledRow("Retention Group", new PropertyField(retGroupProp, "") { style = { flexGrow = 1 } }, retGroupProp));
            expandedContent.Add(BuildLabeledRow("Collision",       new PropertyField(collisionProp, "") { style = { flexGrow = 1 } }, collisionProp));
            
            expandedContent.Add(CreateDivider(6, 0));

            var persistentSummary = BuildSummaryColumn();
            persistentSummary.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f, 0.55f);
            persistentSummary.style.paddingLeft = 6;
            persistentSummary.style.paddingRight = 6;
            persistentSummary.style.paddingTop = 4;
            persistentSummary.style.paddingBottom = 4;
            persistentSummary.style.borderTopLeftRadius = 3;
            persistentSummary.style.borderTopRightRadius = 3;
            persistentSummary.style.borderBottomLeftRadius = 3;
            persistentSummary.style.borderBottomRightRadius = 3;
            persistentSummary.style.marginBottom = 6;
            expandedContent.Add(persistentSummary);
            
            // ─── Tabs: Current | Base ───────────────────────────────────────
            var tabHeaderRow = new VisualElement();
            tabHeaderRow.style.flexDirection = FlexDirection.Row;
            tabHeaderRow.style.marginTop = 2;
            tabHeaderRow.style.marginBottom = 4;
            expandedContent.Add(tabHeaderRow);

            var currentTabBtn = MakeTabButton("Current", true);
            var baseTabBtn    = MakeTabButton("Base", false);
            tabHeaderRow.Add(currentTabBtn);
            tabHeaderRow.Add(baseTabBtn);
            
            tabHeaderRow.Add(CreateFlexSpacer());

            // Quick Import button — copies the inactive tab's spec into the active one in one click.
            // Label/tooltip swap to reflect direction of the copy as the user changes tabs.
            // Disabled while LinkCurrentToBase is ON, since Current is already mirrored.
            bool currentTabActive = true; // tracked locally so the button knows the direction at click time
            var importMagnitudeButton = new Button
            {
                text = "↑ Import from Base",
                tooltip = "Copy Base's Magnitude / Scaling / Real Magnitude into Current",
                focusable = false,
                style =
                {
                    fontSize = 9,
                    height = 20,
                    paddingLeft = 6,
                    paddingRight = 6,
                    marginRight = 6,
                    backgroundColor = Colors.ButtonBackground,
                    color = Colors.AccentGreen,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3
                }
            };
            importMagnitudeButton.clicked += () =>
            {
                // Copy spec FROM the inactive tab INTO the active tab.
                // Scaling is a [SerializeReference] managed reference — both sides share the
                // same instance after copy (same semantics as the Link toggle's previous behavior).
                if (currentTabActive)
                {
                    curMagProp.floatValue = baseMagProp.floatValue;
                }
                else
                {
                    baseMagProp.floatValue = curMagProp.floatValue;
                }
                property.serializedObject.ApplyModifiedProperties();
            };
            
            var importButton = new Button
            {
                text = "↑ Import from Base",
                tooltip = "Copy Base's Magnitude / Scaling / Real Magnitude into Current",
                focusable = false,
                style =
                {
                    fontSize = 9,
                    height = 20,
                    paddingLeft = 6,
                    paddingRight = 6,
                    marginRight = 6,
                    backgroundColor = Colors.ButtonBackground,
                    color = Colors.AccentGreen,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3
                }
            };
            importButton.clicked += () =>
            {
                // Copy spec FROM the inactive tab INTO the active tab.
                // Scaling is a [SerializeReference] managed reference — both sides share the
                // same instance after copy (same semantics as the Link toggle's previous behavior).
                if (currentTabActive)
                {
                    curMagProp.floatValue           = baseMagProp.floatValue;
                    curRealMagProp.enumValueIndex   = baseRealMagProp.enumValueIndex;
                    curScalingProp.managedReferenceValue = baseScalingProp.managedReferenceValue;
                }
                else
                {
                    baseMagProp.floatValue           = curMagProp.floatValue;
                    baseRealMagProp.enumValueIndex   = curRealMagProp.enumValueIndex;
                    baseScalingProp.managedReferenceValue = curScalingProp.managedReferenceValue;
                }
                property.serializedObject.ApplyModifiedProperties();
            };
            tabHeaderRow.Add(importMagnitudeButton);
            tabHeaderRow.Add(importButton);

            var linkToggle = new Toggle("🔗 Link Current to Base") { tooltip = "When ON, Current always mirrors Base. The Current tab is disabled and runtime resolution forwards Current → Base." };
            linkToggle.style.flexGrow = 0;
            linkToggle.style.marginRight = 8;
            linkToggle.BindProperty(linkProp);
            tabHeaderRow.Add(linkToggle);

            var currentTabContent = BuildSideEditor(curMagProp, curScalingProp, curRealMagProp);
            var baseTabContent    = BuildSideEditor(baseMagProp, baseScalingProp, baseRealMagProp);
            currentTabContent.style.display = DisplayStyle.Flex;
            baseTabContent.style.display = DisplayStyle.None;
            expandedContent.Add(currentTabContent);
            expandedContent.Add(baseTabContent);

            // Hint shown over the Current tab body when linked
            var linkedHint = new Label("Current is linked to Base — values shown for reference only. Toggle off to edit independently.");
            linkedHint.style.fontSize = 9;
            linkedHint.style.color = Colors.AccentGreen;
            linkedHint.style.unityFontStyleAndWeight = FontStyle.Italic;
            linkedHint.style.marginBottom = 4;
            linkedHint.style.display = DisplayStyle.None;
            currentTabContent.Insert(0, linkedHint);

            void RefreshImportAllButton()
            {
                if (currentTabActive)
                {
                    importButton.text = "↑ Import All";
                    importButton.tooltip = "Copy Base's Magnitude / Scaling / Real Magnitude into Current";
                }
                else
                {
                    importButton.text = "↓ Import All";
                    importButton.tooltip = "Copy Current's Magnitude / Scaling / Real Magnitude into Base";
                }
                // While linked, Current mirrors Base automatically — importing into Current is
                // a no-op and importing FROM Current would copy the (mirrored) Base value back
                // onto itself. Disable to avoid confusion.
                importButton.SetEnabled(!linkProp.boolValue);
            }
            
            void RefreshImportMagnitudeButton()
            {
                if (currentTabActive)
                {
                    importMagnitudeButton.text = "↑ Import Magnitude";
                    importMagnitudeButton.tooltip = "Copy Base's Magnitude into Current";
                }
                else
                {
                    importMagnitudeButton.text = "↓ Import Magnitude";
                    importMagnitudeButton.tooltip = "Copy Current's Magnitude into Base";
                }
                // While linked, Current mirrors Base automatically — importing into Current is
                // a no-op and importing FROM Current would copy the (mirrored) Base value back
                // onto itself. Disable to avoid confusion.
                importMagnitudeButton.SetEnabled(!linkProp.boolValue);
            }

            void ApplyLinkedState()
            {
                bool linked = linkProp.boolValue;
                currentTabContent.SetEnabled(!linked);
                linkedHint.style.display = linked ? DisplayStyle.Flex : DisplayStyle.None;
                RefreshImportAllButton();
                RefreshImportMagnitudeButton();
            }

            void SelectTab(bool current)
            {
                currentTabActive = current;
                currentTabContent.style.display = current ? DisplayStyle.Flex : DisplayStyle.None;
                baseTabContent.style.display    = current ? DisplayStyle.None : DisplayStyle.Flex;
                StyleTabButton(currentTabBtn, current);
                StyleTabButton(baseTabBtn,   !current);
                RefreshImportAllButton();
                RefreshImportMagnitudeButton();
            }

            currentTabBtn.clicked += () => SelectTab(true);
            baseTabBtn.clicked    += () => SelectTab(false);
            linkToggle.RegisterValueChangedCallback(_ => ApplyLinkedState());

            // ─── Overflow + Constraints ─────────────────────────────────────
            expandedContent.Add(CreateDivider());
            expandedContent.Add(BuildLabeledRow("Bounds",      new PropertyField(overflowProp, "")    { style = { flexGrow = 1 } }, overflowProp));
            expandedContent.Add(BuildLabeledRow("Constraints", new PropertyField(constraintsProp, "") { style = { flexGrow = 1 } }, constraintsProp));

            // ═══════════════════════════════════════════════════════════════════
            // Summary update — runs both summary columns off the same data
            // ═══════════════════════════════════════════════════════════════════
            void UpdateSummary()
            {
                string attrName = attributeProp.objectReferenceValue != null && attributeProp.objectReferenceValue is Attribute attr
                    ? attr.GetName()
                    : "(no attribute)";

                string currentText = linkProp.boolValue
                    ? $"={FormatSide(baseMagProp, baseScalingProp, baseRealMagProp)} (linked)"
                    : FormatSide(curMagProp, curScalingProp, curRealMagProp);
                string baseText = FormatSide(baseMagProp, baseScalingProp, baseRealMagProp);

                string line1 = $"{attrName}    C: {currentText}    B: {baseText}";
                string line2 = $"{FormatOverflow(overflowProp)}    {FormatConstraints(constraintsProp)}";

                ((Label)collapsedSummary.ElementAt(0)).text = line1;
                ((Label)collapsedSummary.ElementAt(1)).text = line2;
                ((Label)persistentSummary.ElementAt(0)).text = line1;
                ((Label)persistentSummary.ElementAt(1)).text = line2;

                // Border tint by collision policy
                container.style.borderLeftColor = (EAttributeElementCollisionPolicy)collisionProp.enumValueIndex switch
                {
                    EAttributeElementCollisionPolicy.UseThis     => Colors.AccentGreen,
                    EAttributeElementCollisionPolicy.UseExisting => Colors.AccentOrange,
                    EAttributeElementCollisionPolicy.Combine     => Colors.AccentBlue,
                    _ => Colors.AccentGray
                };
            }

            void ToggleCollapse()
            {
                bool newState = !IsCollapsed(property.propertyPath);
                SetCollapsed(property.propertyPath, newState);
                collapseBtn.text = newState ? Icons.ChevronRight : Icons.ChevronDown;
                collapsedSummary.style.display = newState ? DisplayStyle.Flex : DisplayStyle.None;
                expandedContent.style.display  = newState ? DisplayStyle.None : DisplayStyle.Flex;
                if (newState) UpdateSummary();
            }

            collapseBtn.clicked += ToggleCollapse;

            // ─── Live bounds enforcement ────────────────────────────────────
            // Whenever the user edits Current/Base magnitudes OR the Overflow policy /
            // Floor / Ceil values change, clamp the magnitudes through AttributeOverflowData.
            // The runtime ApplyBounds() uses the same helper — drawer and runtime cannot
            // disagree on policy semantics.
            //
            // Reentrancy guard: writing the clamped values back through SerializedProperty
            // re-fires TrackSerializedObjectValue. The guard ensures we don't recurse, and
            // the equality check on the second pass would short-circuit anyway.
            bool clampingInProgress = false;

            void EnforceOverflowBounds()
            {
                if (clampingInProgress) return;
                clampingInProgress = true;
                try
                {
                    var overflow = ReadOverflowFromProperty(overflowProp);

                    float baseMagOriginal = baseMagProp.floatValue;
                    float curMagOriginal  = curMagProp.floatValue;

                    float clampedBase = overflow.ClampBase(baseMagOriginal);
                    float clampedCur  = overflow.ClampCurrent(curMagOriginal, clampedBase);

                    bool dirty = false;
                    if (!Mathf.Approximately(clampedBase, baseMagOriginal))
                    {
                        baseMagProp.floatValue = clampedBase;
                        dirty = true;
                    }
                    if (!Mathf.Approximately(clampedCur, curMagOriginal))
                    {
                        curMagProp.floatValue = clampedCur;
                        dirty = true;
                    }
                    if (dirty) property.serializedObject.ApplyModifiedProperties();
                }
                finally
                {
                    clampingInProgress = false;
                }
            }

            // Initial state — Current tab selected, link state honored
            SelectTab(true);
            ApplyLinkedState();

            // Initial summary build (delayed so bindings settle)
            root.schedule.Execute(() =>
            {
                EnforceOverflowBounds();
                UpdateSummary();
            }).StartingIn(50);

            // Rebuild summary + enforce bounds on ANY property change inside this element.
            // (Single track callback so a magnitude change and a policy change both flow
            // through the same path; cheap because both ops bail out when nothing changed.)
            root.TrackSerializedObjectValue(property.serializedObject, _ =>
            {
                EnforceOverflowBounds();
                UpdateSummary();
            });

            return root;
        }

        /// <summary>Reconstruct an <see cref="AttributeOverflowData"/> from its serialized form
        /// so we can call its clamp helpers from the drawer without touching the underlying object.</summary>
        private static AttributeOverflowData ReadOverflowFromProperty(SerializedProperty overflowProp)
        {
            var policyProp = overflowProp.FindPropertyRelative(nameof(AttributeOverflowData.Policy));
            var floorProp  = overflowProp.FindPropertyRelative(nameof(AttributeOverflowData.Floor));
            var ceilProp   = overflowProp.FindPropertyRelative(nameof(AttributeOverflowData.Ceil));

            return new AttributeOverflowData
            {
                Policy = (EAttributeOverflowPolicy)policyProp.enumValueIndex,
                Floor = new AttributeValue(
                    floorProp.FindPropertyRelative(nameof(AttributeValue.CurrentValue)).floatValue,
                    floorProp.FindPropertyRelative(nameof(AttributeValue.BaseValue)).floatValue),
                Ceil = new AttributeValue(
                    ceilProp.FindPropertyRelative(nameof(AttributeValue.CurrentValue)).floatValue,
                    ceilProp.FindPropertyRelative(nameof(AttributeValue.BaseValue)).floatValue),
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // Component builders
        // ─────────────────────────────────────────────────────────────────────

        private static VisualElement BuildSummaryColumn()
        {
            // Two-line column: line 1 = magnitudes, line 2 = overflow + constraints.
            var col = new VisualElement();
            col.style.flexDirection = FlexDirection.Column;

            var line1 = new Label();
            line1.style.fontSize = 10;
            line1.style.color = Colors.AccentBlue;
            line1.style.whiteSpace = WhiteSpace.Normal;
            col.Add(line1);

            var line2 = new Label();
            line2.style.fontSize = 10;
            line2.style.color = Colors.HintText;
            line2.style.whiteSpace = WhiteSpace.Normal;
            line2.style.marginTop = 1;
            col.Add(line2);

            return col;
        }

        private static Button MakeTabButton(string label, bool active)
        {
            var btn = new Button { text = label };
            btn.style.height = 20;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.borderTopLeftRadius = 3;
            btn.style.borderTopRightRadius = 3;
            btn.style.borderBottomLeftRadius = 0;
            btn.style.borderBottomRightRadius = 0;
            StyleTabButton(btn, active);
            return btn;
        }

        private static void StyleTabButton(Button btn, bool active)
        {
            btn.style.backgroundColor = active
                ? new Color(Colors.AccentBlue.r, Colors.AccentBlue.g, Colors.AccentBlue.b, 0.25f)
                : Colors.ButtonBackground;
            btn.style.color = active ? Colors.AccentBlue : Colors.LabelText;
            btn.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
            btn.focusable = false;
        }

        private static VisualElement BuildSideEditor(
            SerializedProperty magnitudeProp,
            SerializedProperty scalingProp,
            SerializedProperty realMagProp)
        {
            var box = new VisualElement();
            box.style.paddingLeft = 6;
            box.style.paddingTop = 6;
            box.style.paddingBottom = 6;
            box.style.paddingRight = 6;
            box.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f, 0.6f);
            box.style.borderTopLeftRadius = 0;
            box.style.borderTopRightRadius = 3;
            box.style.borderBottomLeftRadius = 3;
            box.style.borderBottomRightRadius = 3;
            box.style.marginBottom = 4;

            var magRow = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 4 } };
            var magLabel = new Label("Magnitude") { style = { width = 100, fontSize = 10, color = Colors.HintText } };
            magRow.Add(magLabel);
            var magField = new FloatField { bindingPath = magnitudeProp.propertyPath };
            magField.style.flexGrow = 1;
            magRow.Add(magField);
            box.Add(magRow);

            var realMagField = new PropertyField(realMagProp, "Real Magnitude");
            realMagField.style.marginBottom = 4;
            realMagField.BindProperty(realMagProp);
            box.Add(realMagField);

            var scalingField = new PropertyField(scalingProp, "Scaling");
            scalingField.BindProperty(scalingProp);
            box.Add(scalingField);

            return box;
        }

        private static VisualElement BuildLabeledRow(string label, VisualElement editor, SerializedProperty bindTo = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 4;

            var lbl = new Label(label);
            lbl.style.width = 100;
            lbl.style.fontSize = 10;
            lbl.style.color = Colors.HintText;
            row.Add(lbl);

            if (editor is PropertyField pf && bindTo != null) pf.BindProperty(bindTo);
            row.Add(editor);
            return row;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Summary formatters
        // ─────────────────────────────────────────────────────────────────────

        private static string FormatSide(SerializedProperty magProp, SerializedProperty scalingProp, SerializedProperty realMagProp)
        {
            float mag = magProp.floatValue;
            string scalerName = scalingProp.managedReferenceValue is AbstractCachedScaler s ? PrettyScalerName(s) : null;
            var realMag = (EMagnitudeOperation)realMagProp.enumValueIndex;

            if (string.IsNullOrEmpty(scalerName) || realMag == EMagnitudeOperation.UseMagnitude)
                return $"{mag:F2}";

            string op = realMag switch
            {
                EMagnitudeOperation.AddScaler          => "+",
                EMagnitudeOperation.MultiplyWithScaler => "×",
                EMagnitudeOperation.UseScaler          => "=",
                _ => "?"
            };
            return realMag == EMagnitudeOperation.UseScaler
                ? $"{op}{scalerName}"
                : $"{mag:F2}{op}{scalerName}";
        }

        private static string PrettyScalerName(AbstractCachedScaler s)
        {
            string n = s.GetType().Name;
            if (n.StartsWith("Cached")) n = n.Substring("Cached".Length);
            if (n.EndsWith("Scaler")) n = n.Substring(0, n.Length - "Scaler".Length);
            return n.Length > 0 ? n : "Scaler";
        }

        private static string FormatOverflow(SerializedProperty overflowProp)
        {
            var policyProp = overflowProp.FindPropertyRelative("Policy");
            if (policyProp == null) return "Bounds: ?";
            var policy = (EAttributeOverflowPolicy)policyProp.enumValueIndex;
            return policy switch
            {
                EAttributeOverflowPolicy.ZeroToBase   => "Bounds: 0 → Base",
                EAttributeOverflowPolicy.FloorToBase  => "Bounds: Floor → Base",
                EAttributeOverflowPolicy.ZeroToCeil   => "Bounds: 0 → Ceil",
                EAttributeOverflowPolicy.FloorToCeil  => "Bounds: Floor → Ceil",
                EAttributeOverflowPolicy.Unlimited    => "Bounds: ∞",
                _ => $"Bounds: {policy}"
            };
        }

        private static string FormatConstraints(SerializedProperty constraintsProp)
        {
            bool clamp     = constraintsProp.FindPropertyRelative("AutoClamp")?.boolValue ?? false;
            bool autoScale = constraintsProp.FindPropertyRelative("AutoScaleWithBase")?.boolValue ?? false;
            var rounding   = (EAttributeRoundingPolicy)(constraintsProp.FindPropertyRelative("RoundingMode")?.enumValueIndex ?? 0);
            float snap     = constraintsProp.FindPropertyRelative("SnapInterval")?.floatValue ?? 0f;

            var parts = new List<string>();
            if (clamp) parts.Add("Clamp");
            if (autoScale) parts.Add("AutoScale");
            if (rounding != EAttributeRoundingPolicy.None)
            {
                parts.Add(rounding == EAttributeRoundingPolicy.SnapTo ? $"Snap({snap:g2})" : rounding.ToString());
            }
            return parts.Count == 0 ? "Constraints: none" : "Constraints: " + string.Join(", ", parts);
        }
    }
}
