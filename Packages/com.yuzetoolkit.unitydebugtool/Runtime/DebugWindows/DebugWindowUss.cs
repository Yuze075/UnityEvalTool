#nullable enable
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
        public const string RowClass = "yuzu-debug-row";
        public const string SectionClass = "yuzu-debug-section";
        public const string FirstSectionClass = "yuzu-debug-first-section";
        public const string InlineGroupClass = "yuzu-debug-inline-group";
        public const string InlineRowGroupClass = "yuzu-debug-inline-group-row";
        public const string InlineColumnGroupClass = "yuzu-debug-inline-group-column";
        public const string LabelClass = "yuzu-debug-label";
        public const string MutedLabelClass = "yuzu-debug-label-muted";
        public const string FieldClass = "yuzu-debug-field";
        public const string FieldWithoutLabelClass = "yuzu-debug-field-no-label";
        public const string ButtonClass = "yuzu-debug-button";
        public const string StateButtonClass = "yuzu-debug-state-button";
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
        public const string PreviewClass = "yuzu-debug-preview";
        public const string ImageClass = "yuzu-debug-image";

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
                DisableKeyboardFocus(toggle);
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

        public static void ApplyLabel(Label label, bool muted = false)
        {
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
            button.AddToClassList(ButtonClass);
            DisableKeyboardFocus(button);
        }

        public static void ApplyStateButton(Button button)
        {
            button.AddToClassList(StateButtonClass);
        }

        public static void ApplyStateLabel(Label label)
        {
            label.AddToClassList(StateLabelClass);
        }

        public static void ApplyTag(Label label)
        {
            label.AddToClassList(TagClass);
        }

        public static void ApplyReadOnlyLabel(Label label)
        {
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
    }
}
