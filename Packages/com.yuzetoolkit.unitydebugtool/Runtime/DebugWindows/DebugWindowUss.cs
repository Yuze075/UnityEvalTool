#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace YuzeToolkit
{
    internal static class DebugWindowUss
    {
        public const string LayerClass = "yuzu-debug-debug-layer";
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

        public static void ApplyLayer(VisualElement layer)
        {
            layer.AddToClassList(LayerClass);
            layer.pickingMode = PickingMode.Ignore;
        }

        public static void ApplyWindow(VisualElement window)
        {
            window.AddToClassList(WindowClass);
            window.pickingMode = PickingMode.Position;
            DisableKeyboardFocus(window);
        }

        public static void ApplyWindowBackground(VisualElement background)
        {
            background.AddToClassList(WindowBackgroundClass);
        }

        public static void ApplyWindowContent(VisualElement content)
        {
            content.AddToClassList(WindowContentClass);
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
            DisableKeyboardFocus(header);
        }

        public static void ApplyRow(VisualElement row)
        {
            row.AddToClassList(RowClass);
        }

        public static void ApplySection(Label label)
        {
            label.enableRichText = false;
            label.AddToClassList(SectionClass);
        }

        public static void ApplyFirstSection(Label label)
        {
            label.AddToClassList(FirstSectionClass);
        }

        public static void ApplyInlineGroup(VisualElement group)
        {
            group.AddToClassList(InlineGroupClass);
        }

        public static void ApplyInlineGroupDirection(VisualElement group, FlexDirection direction)
        {
            group.AddToClassList(direction == FlexDirection.Row ? InlineRowGroupClass : InlineColumnGroupClass);
        }

        public static void ApplyInlineFieldLabel(Label label)
        {
            label.enableRichText = false;
            label.AddToClassList(InlineFieldLabelClass);
        }

        public static void ApplyLabel(Label label, bool muted = false)
        {
            label.enableRichText = false;
            label.AddToClassList(LabelClass);
            if (muted)
                label.AddToClassList(MutedLabelClass);
        }

        public static void ApplyField<TValue>(BaseField<TValue> field)
        {
            field.AddToClassList(FieldClass);
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
            DisableKeyboardFocus(button);
        }

        public static void ApplyStateButton(Button button)
        {
            button.AddToClassList(StateButtonClass);
        }

        public static void ApplyBoolButton(Button button)
        {
            button.AddToClassList(BoolButtonClass);
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
        }

        public static void ApplyIconButton(Button button, bool previous)
        {
            button.AddToClassList(IconButtonClass);
            var icon = new DirectionArrowIcon(previous) { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(previous ? PreviousIconClass : NextIconClass);
            button.Add(icon);
        }

        public static void ApplyStateLabel(Label label)
        {
            label.enableRichText = false;
            label.AddToClassList(StateLabelClass);
        }

        public static void ApplyTag(Label label)
        {
            label.enableRichText = false;
            label.AddToClassList(TagClass);
        }

        public static void ApplyReadOnlyLabel(Label label)
        {
            label.enableRichText = false;
            label.AddToClassList(ReadOnlyLabelClass);
        }

        public static void ApplySegmentedRow(VisualElement row)
        {
            row.AddToClassList(SegmentedRowClass);
        }

        public static void ApplySegmentButton(Button button)
        {
            button.AddToClassList(SegmentButtonClass);
        }

        public static void ApplyActiveState(VisualElement element, bool active)
        {
            element.EnableInClassList(ActiveClass, active);
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
        }

        public static void ApplyMiniValue(Label label)
        {
            label.AddToClassList(MiniValueClass);
            ApplyLabel(label);
        }

        public static void ApplyProgress(ProgressBar progress)
        {
            progress.AddToClassList(ProgressClass);
        }

        public static void ApplySliderRow(VisualElement row)
        {
            row.AddToClassList(SliderRowClass);
        }

        public static void ApplySlider(Slider slider)
        {
            slider.AddToClassList(SliderClass);
            DisableKeyboardFocus(slider);
        }

        public static void ApplySlider(SliderInt slider)
        {
            slider.AddToClassList(SliderClass);
            DisableKeyboardFocus(slider);
        }

        public static void ApplySliderValue(Label label)
        {
            label.AddToClassList(SliderValueClass);
            ApplyLabel(label);
        }

        public static void ApplySliderFiller(VisualElement filler)
        {
            filler.AddToClassList(SliderFillerClass);
        }

        public static void ApplySliderThumb(VisualElement thumb)
        {
            thumb.AddToClassList(SliderThumbClass);
        }

        public static void ApplyPreview(VisualElement previewRoot)
        {
            previewRoot.AddToClassList(PreviewClass);
        }

        public static void ApplyImage(VisualElement preview)
        {
            preview.AddToClassList(ImageClass);
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
