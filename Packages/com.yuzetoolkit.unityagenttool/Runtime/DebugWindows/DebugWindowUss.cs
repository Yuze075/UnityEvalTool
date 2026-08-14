#nullable enable
using UnityEngine;
using UnityEngine.UIElements;
using YuzeToolkit.UnityAgent;

namespace YuzeToolkit
{
    internal static class DebugWindowUss
    {
        public const string LayerClass = "yuzu-debug-debug-layer";
        public const string PanelClass = "yuzu-debug-panel";
        public const string PanelHeaderClass = "yuzu-debug-panel-header";
        public const string PanelTitleClass = "yuzu-debug-panel-title";
        public const string PanelTabBarClass = "yuzu-debug-panel-tab-bar";
        public const string PanelTabClass = "yuzu-debug-panel-tab";
        public const string PanelTabActiveClass = "yuzu-debug-panel-tab-active";
        public const string PanelContentClass = "yuzu-debug-panel-content";
        public const string WindowClass = "yuzu-debug-window";
        public const string WindowContentClass = "yuzu-debug-window-content";
        public const string WindowBackgroundClass = "yuzu-debug-window-background";
        public const string FoldoutClass = "yuzu-debug-foldout";
        public const string HeaderClass = "yuzu-debug-header";
        public const string DisclosureClass = "yuzu-debug-disclosure";
        public const string DisclosureOpenClass = "yuzu-debug-disclosure-open";
        public const string RowClass = "yuzu-debug-row";
        public const string SectionClass = "yuzu-debug-section";
        public const string FirstSectionClass = "yuzu-debug-first-section";
        public const string InlineGroupClass = "yuzu-debug-inline-group";
        public const string InlineRowGroupClass = "yuzu-debug-inline-group-row";
        public const string InlineColumnGroupClass = "yuzu-debug-inline-group-column";
        public const string InlineFieldLabelClass = "yuzu-debug-inline-field-label";
        public const string LabelClass = "yuzu-debug-label";
        public const string MutedLabelClass = "yuzu-debug-label-muted";
        public const string FieldClass = "yuzu-debug-field";
        public const string FieldWithoutLabelClass = "yuzu-debug-field-no-label";
        public const string ButtonClass = "yuzu-debug-button";
        public const string StateButtonClass = "yuzu-debug-state-button";
        public const string BoolButtonClass = "yuzu-debug-bool-button";
        public const string BoolButtonLabelClass = "yuzu-debug-bool-button-label";
        public const string BoolButtonStatusClass = "yuzu-debug-bool-button-status";
        public const string BoolSwitchClass = "yuzu-debug-bool-switch";
        public const string BoolSwitchThumbClass = "yuzu-debug-bool-switch-thumb";
        public const string OwnedToggleClass = "yuzu-debug-owned-toggle";
        public const string OwnedToggleStatusClass = "yuzu-debug-owned-toggle-status";
        public const string PrimaryButtonClass = "yuzu-debug-primary-button";
        public const string IconButtonClass = "yuzu-debug-icon-button";
        public const string PreviousIconClass = "yuzu-debug-previous-icon";
        public const string NextIconClass = "yuzu-debug-next-icon";
        public const string StateLabelClass = "yuzu-debug-state-label";
        public const string TagClass = "yuzu-debug-tag";
        public const string ReadOnlyLabelClass = "yuzu-debug-readonly-label";
        public const string SegmentedRowClass = "yuzu-debug-segmented-row";
        public const string SegmentButtonClass = "yuzu-debug-segment-button";
        public const string ActiveClass = "yuzu-debug-active";
        public const string ToneSuccessClass = "yuzu-debug-tone-success";
        public const string ToneDangerClass = "yuzu-debug-tone-danger";
        public const string ToneRedClass = "yuzu-debug-tone-red";
        public const string ToneGreenClass = "yuzu-debug-tone-green";
        public const string ToneBlueClass = "yuzu-debug-tone-blue";
        public const string ToneYellowClass = "yuzu-debug-tone-yellow";
        public const string TonePinkClass = "yuzu-debug-tone-pink";
        public const string ToneWhiteClass = "yuzu-debug-tone-white";
        public const string MiniValueClass = "yuzu-debug-mini-value";
        public const string ProgressClass = "yuzu-debug-progress";
        public const string SliderRowClass = "yuzu-debug-slider-row";
        public const string SliderClass = "yuzu-debug-slider";
        public const string SliderValueClass = "yuzu-debug-slider-value";
        public const string SliderFillerClass = "yuzu-debug-slider-filler";
        public const string SliderThumbClass = "yuzu-debug-slider-thumb";
        public const string PreviewClass = "yuzu-debug-preview";
        public const string ImageClass = "yuzu-debug-image";
        public const string EnumFieldClass = "yuzu-debug-enum-field";
        public const string EnumLabelClass = "yuzu-debug-enum-label";
        public const string EnumButtonClass = "yuzu-debug-enum-button";
        public const string EnumButtonOpenClass = "yuzu-debug-enum-button-open";
        public const string EnumButtonTextClass = "yuzu-debug-enum-button-text";
        public const string EnumChevronClass = "yuzu-debug-enum-chevron";
        public const string EnumPopupClass = "yuzu-debug-enum-popup";
        public const string EnumPopupScrollClass = "yuzu-debug-enum-popup-scroll";
        public const string EnumPopupItemClass = "yuzu-debug-enum-popup-item";
        public const string EnumPopupItemSelectedClass = "yuzu-debug-enum-popup-item-selected";
        public const string EnumPopupItemTextClass = "yuzu-debug-enum-popup-item-text";
        public const string EnumPopupCheckClass = "yuzu-debug-enum-popup-check";
        public const string EnumPopupCheckGlyphClass = "yuzu-debug-enum-popup-check-glyph";

        public static void ApplyLayer(VisualElement layer)
        {
            layer.AddToClassList(LayerClass);
            layer.pickingMode = PickingMode.Ignore;
        }

        public static void ApplyWindow(VisualElement window)
        {
            window.AddToClassList(WindowClass);
            window.pickingMode = PickingMode.Position;
            window.style.flexGrow = 1;
            window.style.minWidth = 0;
            window.style.minHeight = 0;
            window.style.backgroundColor = AgentUi.Transparent;
            DisableKeyboardFocus(window);
        }

        public static void ApplyPanel(VisualElement panel)
        {
            panel.AddToClassList(PanelClass);
            panel.pickingMode = PickingMode.Position;
        }

        public static void ApplyPanelHeader(VisualElement header) => header.AddToClassList(PanelHeaderClass);

        public static void ApplyPanelTitle(Label title) => title.AddToClassList(PanelTitleClass);

        public static void ApplyPanelTabBar(VisualElement tabBar) => tabBar.AddToClassList(PanelTabBarClass);

        public static void ApplyPanelTab(Button tab)
        {
            tab.AddToClassList(PanelTabClass);
            DisableKeyboardFocus(tab);
        }

        public static void ApplyPanelTabState(Button tab, bool active) =>
            tab.EnableInClassList(PanelTabActiveClass, active);

        public static void ApplyPanelContent(VisualElement content) => content.AddToClassList(PanelContentClass);

        public static void ApplyWindowBackground(VisualElement background)
        {
            background.AddToClassList(WindowBackgroundClass);
        }

        public static void ApplyWindowContent(VisualElement content)
        {
            content.AddToClassList(WindowContentClass);
            content.style.flexGrow = 1;
            content.style.minWidth = 0;
            content.style.minHeight = 0;
            content.style.paddingLeft = 20;
            content.style.paddingRight = 20;
            content.style.paddingTop = 16;
            content.style.paddingBottom = 20;
            content.style.backgroundColor = AgentUi.Background;
            DisableKeyboardFocus(content);
            if (content is ScrollView scrollView)
            {
                DisableKeyboardFocus(scrollView.horizontalScroller);
                DisableKeyboardFocus(scrollView.verticalScroller);
            }
        }

        public static void ApplyFoldout(Foldout foldout)
        {
            foldout.AddToClassList(FoldoutClass);
            foldout.style.marginBottom = 10;
            foldout.style.paddingLeft = 12;
            foldout.style.paddingRight = 12;
            foldout.style.paddingTop = 8;
            foldout.style.paddingBottom = 8;
            foldout.style.backgroundColor = AgentUi.Panel;
            foldout.style.borderTopLeftRadius = 10;
            foldout.style.borderTopRightRadius = 10;
            foldout.style.borderBottomLeftRadius = 10;
            foldout.style.borderBottomRightRadius = 10;
            AgentUi.SetBorder(foldout, AgentUi.Border, 1);
            DisableKeyboardFocus(foldout);
            if (foldout.Q<Toggle>() is { } toggle)
            {
                DisableKeyboardFocus(toggle);
                var indicator = new VisualElement { pickingMode = PickingMode.Ignore };
                indicator.AddToClassList(DisclosureClass);
                (toggle.Q<VisualElement>(className: "unity-toggle__input") ?? toggle).Insert(0, indicator);
                void Sync(bool open) => indicator.EnableInClassList(DisclosureOpenClass, open);
                Sync(foldout.value);
                foldout.RegisterValueChangedCallback(evt => Sync(evt.newValue));
            }
        }

        public static void ApplyHeader(Toggle? header)
        {
            if (header == null) return;
            header.AddToClassList(HeaderClass);
            header.pickingMode = PickingMode.Position;
            header.style.minHeight = 32;
            header.style.color = AgentUi.Text;
            DisableKeyboardFocus(header);
        }

        public static void ApplyRow(VisualElement row)
        {
            row.AddToClassList(RowClass);
            row.style.minWidth = 0;
            row.style.minHeight = 38;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;
            row.style.paddingLeft = 10;
            row.style.paddingRight = 10;
            row.style.paddingTop = 5;
            row.style.paddingBottom = 5;
            row.style.backgroundColor = AgentUi.PanelInset;
            row.style.borderTopLeftRadius = 8;
            row.style.borderTopRightRadius = 8;
            row.style.borderBottomLeftRadius = 8;
            row.style.borderBottomRightRadius = 8;
            AgentUi.SetBorder(row, AgentUi.Border, 1);
        }

        public static void ApplySection(Label label)
        {
            label.enableRichText = false;
            label.AddToClassList(SectionClass);
            AgentUi.ApplyTypography(label, AgentTypography.BodyStrong);
            label.style.color = AgentUi.Text;
            label.style.marginTop = 14;
            label.style.marginBottom = 8;
        }

        public static void ApplyFirstSection(Label label)
        {
            label.AddToClassList(FirstSectionClass);
            label.style.marginTop = 0;
        }

        public static void ApplyInlineGroup(VisualElement group)
        {
            group.AddToClassList(InlineGroupClass);
            group.style.minWidth = 0;
            group.style.marginBottom = 6;
            group.style.alignItems = Align.Center;
        }

        public static void ApplyInlineGroupDirection(VisualElement group, FlexDirection direction)
        {
            group.AddToClassList(direction == FlexDirection.Row ? InlineRowGroupClass : InlineColumnGroupClass);
            group.style.flexDirection = direction;
        }

        public static void ApplyInlineFieldLabel(Label label)
        {
            label.enableRichText = false;
            label.AddToClassList(InlineFieldLabelClass);
            label.style.minWidth = 130;
            label.style.color = AgentUi.TextSecondary;
            AgentUi.ApplyTypography(label, AgentTypography.Control);
        }

        public static void ApplyLabel(Label label, bool muted = false)
        {
            label.enableRichText = false;
            label.AddToClassList(LabelClass);
            label.style.color = muted ? AgentUi.Muted : AgentUi.Text;
            label.style.whiteSpace = WhiteSpace.Normal;
            AgentUi.ApplyTypography(label, AgentTypography.Body);
            if (muted)
                label.AddToClassList(MutedLabelClass);
        }

        public static void ApplyField<TValue>(BaseField<TValue> field)
        {
            field.AddToClassList(FieldClass);
            field.style.minWidth = 0;
            field.style.flexGrow = 1;
            field.style.height = 32;
            field.style.marginLeft = 6;
            field.style.marginRight = 0;
            field.style.marginTop = 0;
            field.style.marginBottom = 0;
            field.style.backgroundColor = AgentUi.Input;
            field.style.borderTopLeftRadius = 8;
            field.style.borderTopRightRadius = 8;
            field.style.borderBottomLeftRadius = 8;
            field.style.borderBottomRightRadius = 8;
            field.style.color = AgentUi.Text;
            AgentUi.SetBorder(field, AgentUi.Border, 1);
            if (field is TextField)
            {
                // Pointer focus is admitted by DebugVisualFactory only after a left-click in this field.
                field.focusable = true;
                field.tabIndex = -1;
            }
            else
            {
                DisableKeyboardFocus(field);
            }
        }

        public static void ApplyFieldWithoutLabel<TValue>(BaseField<TValue> field)
        {
            field.AddToClassList(FieldWithoutLabelClass);
        }

        public static void ApplyButton(Button button)
        {
            button.enableRichText = false;
            button.AddToClassList(ButtonClass);
            button.style.height = 32;
            button.style.minWidth = 72;
            button.style.flexShrink = 0;
            button.style.marginLeft = 3;
            button.style.marginRight = 3;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 12;
            button.style.backgroundImage = StyleKeyword.None;
            button.style.backgroundColor = AgentUi.Surface3;
            button.style.color = AgentUi.Text;
            button.style.borderTopLeftRadius = 16;
            button.style.borderTopRightRadius = 16;
            button.style.borderBottomLeftRadius = 16;
            button.style.borderBottomRightRadius = 16;
            AgentUi.SetBorder(button, AgentUi.Border, 1);
            AgentUi.ApplyTypography(button, AgentTypography.Control);
            DisableKeyboardFocus(button);
        }

        public static void ApplyStateButton(Button button)
        {
            button.AddToClassList(StateButtonClass);
            button.style.flexGrow = 1;
        }

        public static void ApplyBoolButton(Button button)
        {
            button.AddToClassList(BoolButtonClass);
            button.style.flexGrow = 1;
        }

        public static Label ApplyOwnedToggle(Toggle toggle)
        {
            toggle.AddToClassList(OwnedToggleClass);
            var input = toggle.Q<VisualElement>(className: "unity-toggle__input")
                ?? throw new System.InvalidOperationException("Toggle input visual was not created.");

            var status = new Label { pickingMode = PickingMode.Ignore };
            status.enableRichText = false;
            status.AddToClassList(OwnedToggleStatusClass);
            input.Add(status);

            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList(BoolSwitchClass);
            var thumb = new VisualElement { pickingMode = PickingMode.Ignore };
            thumb.AddToClassList(BoolSwitchThumbClass);
            track.Add(thumb);
            input.Add(track);
            return status;
        }

        public static void ApplyPrimaryButton(Button button)
        {
            button.AddToClassList(PrimaryButtonClass);
            button.style.backgroundColor = AgentUi.Accent;
            button.style.color = AgentUi.AccentForeground;
        }

        public static void ApplyIconButton(Button button, bool previous)
        {
            button.AddToClassList(IconButtonClass);
            button.style.minWidth = 32;
            button.style.width = 32;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            var icon = new DirectionArrowIcon(previous) { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(previous ? PreviousIconClass : NextIconClass);
            button.Add(icon);
        }

        public static void ApplyStateLabel(Label label)
        {
            label.enableRichText = false;
            label.AddToClassList(StateLabelClass);
            ApplyLabel(label);
            label.style.paddingLeft = 9;
            label.style.paddingRight = 9;
            label.style.paddingTop = 5;
            label.style.paddingBottom = 5;
            label.style.backgroundColor = AgentUi.Surface3;
            label.style.borderTopLeftRadius = 12;
            label.style.borderTopRightRadius = 12;
            label.style.borderBottomLeftRadius = 12;
            label.style.borderBottomRightRadius = 12;
        }

        public static void ApplyTag(Label label)
        {
            label.enableRichText = false;
            label.AddToClassList(TagClass);
            ApplyLabel(label);
            label.style.color = AgentUi.Accent;
            label.style.backgroundColor = AgentUi.Active;
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;
            label.style.borderTopLeftRadius = 10;
            label.style.borderTopRightRadius = 10;
            label.style.borderBottomLeftRadius = 10;
            label.style.borderBottomRightRadius = 10;
        }

        public static void ApplyReadOnlyLabel(Label label)
        {
            label.enableRichText = false;
            label.AddToClassList(ReadOnlyLabelClass);
            ApplyLabel(label, true);
            label.style.flexGrow = 1;
        }

        public static void ApplySegmentedRow(VisualElement row)
        {
            row.AddToClassList(SegmentedRowClass);
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.minWidth = 0;
        }

        public static void ApplySegmentButton(Button button)
        {
            button.AddToClassList(SegmentButtonClass);
            ApplyButton(button);
        }

        public static void ApplyActiveState(VisualElement element, bool active)
        {
            element.EnableInClassList(ActiveClass, active);
            element.style.backgroundColor = active ? AgentUi.Active : AgentUi.Surface3;
            element.style.color = active ? AgentUi.Accent : AgentUi.Text;
        }

        public static void ApplyTone(VisualElement element, DebugTone tone)
        {
            element.EnableInClassList(ToneSuccessClass, tone == DebugTone.Success);
            element.EnableInClassList(ToneDangerClass, tone == DebugTone.Danger);
            element.EnableInClassList(ToneRedClass, tone == DebugTone.Red);
            element.EnableInClassList(ToneGreenClass, tone == DebugTone.Green);
            element.EnableInClassList(ToneBlueClass, tone == DebugTone.Blue);
            element.EnableInClassList(ToneYellowClass, tone == DebugTone.Yellow);
            element.EnableInClassList(TonePinkClass, tone == DebugTone.Pink);
            element.EnableInClassList(ToneWhiteClass, tone == DebugTone.White);
            element.style.color = tone switch
            {
                DebugTone.Success or DebugTone.Green => AgentUi.Success,
                DebugTone.Danger or DebugTone.Red => AgentUi.Error,
                DebugTone.Yellow => AgentUi.Warning,
                DebugTone.Blue => AgentUi.Accent,
                DebugTone.Pink => (Color)new Color32(236, 128, 191, 255),
                _ => AgentUi.Text
            };
        }

        public static void ApplyMiniValue(Label label)
        {
            label.AddToClassList(MiniValueClass);
            ApplyLabel(label);
        }

        public static void ApplyProgress(ProgressBar progress)
        {
            progress.AddToClassList(ProgressClass);
            progress.style.flexGrow = 1;
            progress.style.height = 18;
            progress.style.color = AgentUi.Text;
        }

        public static void ApplySliderRow(VisualElement row)
        {
            row.AddToClassList(SliderRowClass);
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.flexGrow = 1;
        }

        public static void ApplySlider(Slider slider)
        {
            slider.AddToClassList(SliderClass);
            slider.style.flexGrow = 1;
            DisableKeyboardFocus(slider);
        }

        public static void ApplySlider(SliderInt slider)
        {
            slider.AddToClassList(SliderClass);
            slider.style.flexGrow = 1;
            DisableKeyboardFocus(slider);
        }

        public static void ApplySliderValue(Label label)
        {
            label.AddToClassList(SliderValueClass);
            ApplyLabel(label);
            label.style.width = 58;
            label.style.unityTextAlign = TextAnchor.MiddleRight;
        }

        public static void ApplySliderFiller(VisualElement filler)
        {
            filler.AddToClassList(SliderFillerClass);
            filler.style.backgroundColor = AgentUi.Accent;
        }

        public static void ApplySliderThumb(VisualElement thumb)
        {
            thumb.AddToClassList(SliderThumbClass);
            thumb.style.backgroundColor = AgentUi.Text;
        }

        public static void ApplyPreview(VisualElement previewRoot)
        {
            previewRoot.AddToClassList(PreviewClass);
            previewRoot.style.minHeight = 80;
            previewRoot.style.backgroundColor = AgentUi.PanelInset;
            previewRoot.style.borderTopLeftRadius = 8;
            previewRoot.style.borderTopRightRadius = 8;
            previewRoot.style.borderBottomLeftRadius = 8;
            previewRoot.style.borderBottomRightRadius = 8;
        }

        public static void ApplyImage(VisualElement preview)
        {
            preview.AddToClassList(ImageClass);
            preview.style.flexGrow = 1;
        }

        private static void DisableKeyboardFocus(VisualElement element)
        {
            element.focusable = false;
            element.tabIndex = -1;
        }

        private sealed class DirectionArrowIcon : VisualElement
        {
            private readonly bool _previous;

            public DirectionArrowIcon(bool previous)
            {
                _previous = previous;
                generateVisualContent += Draw;
            }

            private void Draw(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                var center = contentRect.center;
                var direction = _previous ? 1f : -1f;
                painter.strokeColor = new Color32(207, 211, 214, 255);
                painter.lineWidth = 2f;
                painter.lineCap = LineCap.Round;
                painter.lineJoin = LineJoin.Round;
                painter.BeginPath();
                painter.MoveTo(new Vector2(center.x + direction * 3.5f, center.y - 5f));
                painter.LineTo(new Vector2(center.x - direction * 2.5f, center.y));
                painter.LineTo(new Vector2(center.x + direction * 3.5f, center.y + 5f));
                painter.Stroke();
            }
        }
    }
}
